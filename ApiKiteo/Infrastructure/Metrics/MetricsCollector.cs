using System.Collections.Concurrent;

namespace ApiKiteo.API.Infrastructure.Metrics;

/// <summary>
/// Recolector de métricas en memoria. Singleton thread-safe.
/// No tiene dependencias externas — todo vive en el proceso.
///
/// Registra:
///   - Contadores globales de requests / errores / duraciones
///   - Rolling window de 60 segundos (último minuto)
///   - Rolling window de 60 minutos (última hora)
///   - Baldes hora × hora del día de hoy (para la gráfica de barras)
///   - Estadísticas por endpoint (top llamados / más lentos)
/// </summary>
public sealed class MetricsCollector
{
    // ── Identidad del servicio ────────────────────────────────────────────────
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    // ── Contadores globales (Interlocked — lock-free) ─────────────────────────
    private long _totalRequests;
    private long _totalErrors;       // 5xx
    private long _totalClientErrors; // 4xx
    private long _totalSuccesses;    // 2xx

    // Suma y max de duraciones para avg / max lifetime
    private long   _durationSumMs;
    private long   _durationMaxMs;

    // Todas las duraciones de vida (capped a 100 000 para no explotar RAM)
    private readonly ConcurrentQueue<double> _allDurations = new();
    private const int MaxDurationSamples = 100_000;

    // ── Conexiones activas en este momento ───────────────────────────────────
    private int _activeConnections;
    public  int ActiveConnections => _activeConnections;

    // ── Rolling window de 60 segundos ─────────────────────────────────────────
    // Cada entrada: (timestamp, durationMs, isError)
    private readonly ConcurrentQueue<(DateTime ts, double ms, bool error)> _secondsWindow = new();

    // ── Rolling window de 60 minutos ──────────────────────────────────────────
    private readonly ConcurrentQueue<(DateTime ts, double ms, bool error)> _minutesWindow = new();

    // ── Baldes hora × hora del día (24 slots) ─────────────────────────────────
    // Slot[h] acumula requests de la hora h del día UTC de hoy.
    // Se resetean cuando cambia el día.
    private DateTime  _bucketsDay = DateTime.UtcNow.Date;
    private readonly HourlyData[] _hourlyBuckets = new HourlyData[24];

    // ── Estadísticas por endpoint ─────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, EndpointData> _endpoints = new();

    // ── Objeto de lock solo para las operaciones que requieren consistencia ───
    private readonly object _bucketLock = new();

    // ─────────────────────────────────────────────────────────────────────────
    public MetricsCollector()
    {
        // Inicializar los 24 baldes vacíos
        for (int i = 0; i < 24; i++)
            _hourlyBuckets[i] = new HourlyData();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  REGISTRO DE UN REQUEST
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Llamado por el middleware al terminar cada request.
    /// </summary>
    public void Record(string method, string path, int statusCode, double durationMs)
    {
        var now      = DateTime.UtcNow;
        var isError  = statusCode >= 500;
        var isClient = statusCode is >= 400 and < 500;

        // ── Contadores globales ───────────────────────────────────────────────
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _durationSumMs, (long)durationMs);

        // CAS loop para actualizar el máximo sin lock
        long prev = Volatile.Read(ref _durationMaxMs);
        while ((long)durationMs > prev)
        {
            long updated = Interlocked.CompareExchange(ref _durationMaxMs, (long)durationMs, prev);
            if (updated == prev) break;
            prev = updated;
        }

        if (isError)        Interlocked.Increment(ref _totalErrors);
        else if (isClient)  Interlocked.Increment(ref _totalClientErrors);
        else                Interlocked.Increment(ref _totalSuccesses);

        // Cola de duraciones (capped) para el P95 global
        if (_allDurations.Count < MaxDurationSamples)
            _allDurations.Enqueue(durationMs);

        // ── Rolling windows ───────────────────────────────────────────────────
        var entry = (now, durationMs, isError);
        _secondsWindow.Enqueue(entry);
        _minutesWindow.Enqueue(entry);

        // Limpiar entradas expiradas (el Dequeue es barato)
        var cutoff60s  = now.AddSeconds(-60);
        var cutoff60m  = now.AddMinutes(-60);

        while (_secondsWindow.TryPeek(out var head) && head.ts < cutoff60s)
            _secondsWindow.TryDequeue(out _);

        while (_minutesWindow.TryPeek(out var mHead) && mHead.ts < cutoff60m)
            _minutesWindow.TryDequeue(out _);

        // ── Balas hora × hora ─────────────────────────────────────────────────
        RecordHourlyBucket(now, durationMs, isError);

        // ── Estadísticas por endpoint ─────────────────────────────────────────
        var key = $"{method.ToUpperInvariant()} {NormalizePath(path)}";
        var ep  = _endpoints.GetOrAdd(key, _ => new EndpointData());
        ep.Record(durationMs, isError);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CONEXIONES ACTIVAS
    // ═════════════════════════════════════════════════════════════════════════

    public void IncrementActive() => Interlocked.Increment(ref _activeConnections);
    public void DecrementActive() => Interlocked.Decrement(ref _activeConnections);

    // ═════════════════════════════════════════════════════════════════════════
    //  CONSTRUCCIÓN DEL SNAPSHOT
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Genera la foto instantánea del estado actual. Llamado por el controller.
    /// </summary>
    public MetricsSnapshot BuildSnapshot()
    {
        var now      = DateTime.UtcNow;
        var uptime   = now - StartedAtUtc;
        var total    = Volatile.Read(ref _totalRequests);
        var errors   = Volatile.Read(ref _totalErrors);
        var clients  = Volatile.Read(ref _totalClientErrors);
        var ok       = Volatile.Read(ref _totalSuccesses);
        var sumMs    = Volatile.Read(ref _durationSumMs);
        var maxMs    = Volatile.Read(ref _durationMaxMs);

        // ── Snapshot ventanas ─────────────────────────────────────────────────
        var lastMinute = BuildWindowSummary(_secondsWindow.ToArray(), 60);
        var lastHour   = BuildWindowSummary(_minutesWindow.ToArray(), 3600);

        // ── Hourly buckets del día de hoy ─────────────────────────────────────
        var hourly = BuildHourlyBuckets();

        // ── Top endpoints ─────────────────────────────────────────────────────
        var allEps = _endpoints
            .Select(kv => BuildEndpointStat(kv.Key, kv.Value))
            .ToList();

        var topCalled   = allEps.OrderByDescending(e => e.CallCount).Take(10).ToList();
        var topSlowest  = allEps.OrderByDescending(e => e.P95DurationMs).Take(10).ToList();

        // ── P95 global ───────────────────────────────────────────────────────
        var p95 = CalculateP95(_allDurations.ToArray());

        // ── Recursos del proceso ──────────────────────────────────────────────
        var proc = System.Diagnostics.Process.GetCurrentProcess();

        return new MetricsSnapshot
        {
            CapturedAtUtc    = now,
            ServiceStartUtc  = StartedAtUtc,
            Uptime           = FormatUptime(uptime),

            Lifetime = new LifetimeSummary
            {
                TotalRequests     = total,
                TotalErrors       = errors,
                TotalClientErrors = clients,
                TotalSuccesses    = ok,
                ErrorRatePct      = total > 0 ? Math.Round(errors * 100.0 / total, 2) : 0,
                AvgDurationMs     = total > 0 ? Math.Round((double)sumMs / total, 2) : 0,
                MaxDurationMs     = maxMs,
                P95DurationMs     = p95
            },

            LastMinute      = lastMinute,
            LastHour        = lastHour,
            HourlyToday     = hourly,
            TopEndpoints    = topCalled,
            SlowestEndpoints = topSlowest,

            Resources = new ResourceInfo
            {
                WorkingSetMb      = proc.WorkingSet64 / 1_048_576,
                ThreadCount       = proc.Threads.Count,
                ActiveConnections = _activeConnections,
                CpuTotalMs        = proc.TotalProcessorTime.TotalMilliseconds
            }
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MÉTODOS PRIVADOS
    // ═════════════════════════════════════════════════════════════════════════

    private void RecordHourlyBucket(DateTime now, double durationMs, bool isError)
    {
        lock (_bucketLock)
        {
            // Resetear baldes si cambió el día
            if (now.Date != _bucketsDay)
            {
                _bucketsDay = now.Date;
                for (int i = 0; i < 24; i++)
                    _hourlyBuckets[i] = new HourlyData();
            }

            int h = now.Hour;
            _hourlyBuckets[h].Increment(durationMs, isError);
        }
    }

    private IReadOnlyList<HourlyBucket> BuildHourlyBuckets()
    {
        HourlyData[] snapshot;
        lock (_bucketLock) { snapshot = (HourlyData[])_hourlyBuckets.Clone(); }

        // Calcular el máximo de requests para clasificar la carga relativa
        int maxReq = snapshot.Max(b => b.Count);

        return Enumerable.Range(0, 24)
            .Select(h =>
            {
                var b      = snapshot[h];
                var avgMs  = b.Count > 0 ? Math.Round(b.SumMs / b.Count, 1) : 0;
                var level  = ClassifyLoad(b.Count, maxReq);
                return new HourlyBucket
                {
                    Hour         = $"{h:D2}:00",
                    RequestCount = b.Count,
                    ErrorCount   = b.Errors,
                    AvgDurationMs= avgMs,
                    LoadLevel    = level
                };
            })
            .ToList();
    }

    private static WindowSummary BuildWindowSummary(
        (DateTime ts, double ms, bool error)[] entries,
        double windowSeconds)
    {
        if (entries.Length == 0)
            return new WindowSummary();

        int errors  = entries.Count(e => e.error);
        double avgMs = entries.Average(e => e.ms);

        return new WindowSummary
        {
            RequestCount    = entries.Length,
            ErrorCount      = errors,
            AvgDurationMs   = Math.Round(avgMs, 2),
            ErrorRatePct    = Math.Round(errors * 100.0 / entries.Length, 2),
            RequestsPerSecond = Math.Round(entries.Length / windowSeconds, 3)
        };
    }

    private static EndpointStat BuildEndpointStat(string key, EndpointData data)
    {
        var samples = data.GetSamples();
        var p95     = CalculateP95(samples);

        return new EndpointStat
        {
            Endpoint      = key,
            CallCount     = data.Count,
            AvgDurationMs = data.Count > 0 ? Math.Round(data.SumMs / data.Count, 2) : 0,
            P95DurationMs = p95,
            MaxDurationMs = data.MaxMs,
            ErrorCount    = data.Errors
        };
    }

    /// <summary>Calcula el percentil 95 sobre un array de duraciones.</summary>
    private static double CalculateP95(double[] samples)
    {
        if (samples.Length == 0) return 0;
        var sorted = samples.OrderBy(x => x).ToArray();
        int idx    = (int)Math.Ceiling(0.95 * sorted.Length) - 1;
        return Math.Round(sorted[Math.Max(0, idx)], 2);
    }

    /// <summary>
    /// Normaliza rutas para agrupar: quita IDs numéricos y GUIDs.
    /// Ej: /api/roles/42 → /api/roles/{id}
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Reemplazar segmentos numéricos o GUID con placeholders
        return System.Text.RegularExpressions.Regex.Replace(
            path,
            @"(?<=/)(\d+|[0-9a-fA-F\-]{36})(?=/|$)",
            "{id}");
    }

    private static string ClassifyLoad(int count, int max)
    {
        if (max == 0 || count == 0)     return "Idle";
        double ratio = (double)count / max;
        return ratio switch
        {
            < 0.10 => "Idle",
            < 0.30 => "Low",
            < 0.60 => "Medium",
            < 0.85 => "High",
            _      => "Peak"
        };
    }

    private static string FormatUptime(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}h {ts.Minutes:D2}m {ts.Seconds:D2}s";

    // ─── Clases de apoyo internas ─────────────────────────────────────────────

    /// <summary>Datos acumulados de un balde de hora.</summary>
    private sealed class HourlyData
    {
        public int    Count  { get; private set; }
        public int    Errors { get; private set; }
        public double SumMs  { get; private set; }

        public void Increment(double ms, bool isError)
        {
            Count++;
            SumMs += ms;
            if (isError) Errors++;
        }
    }

    /// <summary>Datos acumulados por endpoint (thread-safe con lock interno).</summary>
    private sealed class EndpointData
    {
        private readonly object _lock = new();
        private readonly List<double> _samples = new(1000);

        public long   Count  { get; private set; }
        public long   Errors { get; private set; }
        public double SumMs  { get; private set; }
        public double MaxMs  { get; private set; }

        public void Record(double ms, bool isError)
        {
            lock (_lock)
            {
                Count++;
                SumMs += ms;
                if (ms > MaxMs)  MaxMs  = ms;
                if (isError)     Errors++;

                // Guardar máx. 5000 samples por endpoint para el P95
                if (_samples.Count < 5000)
                    _samples.Add(ms);
            }
        }

        public double[] GetSamples()
        {
            lock (_lock) { return _samples.ToArray(); }
        }
    }
}
