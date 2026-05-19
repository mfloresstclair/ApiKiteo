namespace ApiKiteo.API.Infrastructure.Metrics;

// ─── Snapshot completo que devuelve el endpoint ────────────────────────────────
/// <summary>
/// Foto instantánea del estado del microservicio en el momento de la consulta.
/// </summary>
public sealed record MetricsSnapshot
{
    /// <summary>Cuándo se tomó esta foto (UTC).</summary>
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Desde cuándo está corriendo el proceso (UTC).</summary>
    public DateTime ServiceStartUtc { get; init; }

    /// <summary>Tiempo total que lleva vivo el servicio.</summary>
    public string Uptime { get; init; } = string.Empty;

    /// <summary>Resumen general de requests desde el arranque.</summary>
    public LifetimeSummary Lifetime { get; init; } = new();

    /// <summary>Carga del último minuto completo (rolling window de 60 s).</summary>
    public WindowSummary LastMinute { get; init; } = new();

    /// <summary>Carga de la última hora completa (rolling window de 60 min).</summary>
    public WindowSummary LastHour { get; init; } = new();

    /// <summary>Distribución de carga hora por hora del día de hoy (UTC).</summary>
    public IReadOnlyList<HourlyBucket> HourlyToday { get; init; } = [];

    /// <summary>Top 10 endpoints más llamados desde el arranque.</summary>
    public IReadOnlyList<EndpointStat> TopEndpoints { get; init; } = [];

    /// <summary>Top 10 endpoints más lentos (percentil 95).</summary>
    public IReadOnlyList<EndpointStat> SlowestEndpoints { get; init; } = [];

    /// <summary>Estado de recursos del proceso.</summary>
    public ResourceInfo Resources { get; init; } = new();
}

// ─── Resumen de toda la vida del servicio ─────────────────────────────────────
public sealed record LifetimeSummary
{
    public long TotalRequests        { get; init; }
    public long TotalErrors          { get; init; }   // 5xx
    public long TotalClientErrors    { get; init; }   // 4xx
    public long TotalSuccesses       { get; init; }   // 2xx
    public double ErrorRatePct       { get; init; }   // % de errores sobre total
    public double AvgDurationMs      { get; init; }
    public double MaxDurationMs      { get; init; }
    public double P95DurationMs      { get; init; }
}

// ─── Ventana de tiempo (último minuto o última hora) ──────────────────────────
public sealed record WindowSummary
{
    /// <summary>Requests dentro de la ventana.</summary>
    public int RequestCount          { get; init; }
    public int ErrorCount            { get; init; }
    public double AvgDurationMs      { get; init; }
    public double ErrorRatePct       { get; init; }

    /// <summary>Requests por segundo promedio dentro de la ventana.</summary>
    public double RequestsPerSecond  { get; init; }
}

// ─── Balde de una hora del día (para la gráfica de barras del día) ────────────
public sealed record HourlyBucket
{
    /// <summary>Hora del día en UTC, ej: "09:00".</summary>
    public string Hour               { get; init; } = string.Empty;
    public int RequestCount          { get; init; }
    public int ErrorCount            { get; init; }
    public double AvgDurationMs      { get; init; }

    /// <summary>Clasificación de carga: Idle / Low / Medium / High / Peak.</summary>
    public string LoadLevel          { get; init; } = "Idle";
}

// ─── Estadística por endpoint ─────────────────────────────────────────────────
public sealed record EndpointStat
{
    /// <summary>Método HTTP + ruta, ej: "GET /escaneo".</summary>
    public string Endpoint           { get; init; } = string.Empty;
    public long   CallCount          { get; init; }
    public double AvgDurationMs      { get; init; }
    public double P95DurationMs      { get; init; }
    public double MaxDurationMs      { get; init; }
    public long   ErrorCount         { get; init; }
}

// ─── Recursos del proceso ──────────────────────────────────────────────────────
public sealed record ResourceInfo
{
    public long WorkingSetMb         { get; init; }
    public int  ThreadCount          { get; init; }
    public int  ActiveConnections    { get; init; }   // contador manual incrementado por el middleware
    public double CpuTotalMs         { get; init; }
}
