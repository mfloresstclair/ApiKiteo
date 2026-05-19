namespace ApiKiteo.API.Infrastructure.Metrics;

/// <summary>
/// Middleware que cronometra cada request y alimenta al <see cref="MetricsCollector"/>.
/// Se registra una sola vez en Program.cs — antes de UseRouting.
///
/// Rutas excluidas del conteo (evitar ruido en las métricas):
///   - /swagger/*
///   - /api/metrics
///   - /api/health
/// </summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate  _next;
    private readonly MetricsCollector _collector;

    // Prefijos que no queremos contar en las métricas
    private static readonly string[] ExcludedPrefixes =
    [
        "/swagger",
        "/api/metrics",
        "/api/health"
    ];

    public MetricsMiddleware(RequestDelegate next, MetricsCollector collector)
    {
        _next      = next;
        _collector = collector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Excluir rutas de infraestructura
        if (ShouldExclude(path))
        {
            await _next(context);
            return;
        }

        // ── Marcar conexión activa ────────────────────────────────────────────
        _collector.IncrementActive();

        var start = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
            _collector.DecrementActive();

            _collector.Record(
                method:     context.Request.Method,
                path:       path,
                statusCode: context.Response.StatusCode,
                durationMs: elapsed.TotalMilliseconds);
        }
    }

    private static bool ShouldExclude(string path)
        => ExcludedPrefixes.Any(p =>
            path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
