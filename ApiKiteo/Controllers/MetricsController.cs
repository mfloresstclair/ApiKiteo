using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Infrastructure.Metrics;
using ApiKiteo.API.Infrastructure.Database;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Endpoints de observabilidad del microservicio.
///
/// GET /api/health  → ping rápido (proceso vivo + SQL alcanzable)
/// GET /api/metrics → foto completa de carga y rendimiento del día
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[Tags("Observabilidad")]
public sealed class MetricsController : ControllerBase
{
    private readonly MetricsCollector _collector;
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<MetricsController> _log;

    public MetricsController(
        MetricsCollector collector,
        IDbConnectionFactory db,
        ILogger<MetricsController> log)
    {
        _collector = collector;
        _db = db;
        _log = log;
    }

    // ── GET /api/health ───────────────────────────────────────────────────────

    /// <summary>
    /// Health check rápido. Verifica proceso vivo + SQL Server alcanzable.
    /// Apto para watchdogs o scripts de monitoreo de planta.
    /// </summary>
    /// <response code="200">Servicio sano.</response>
    /// <response code="503">SQL Server no responde.</response>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var sqlOk = false;
        var sqlDetail = string.Empty;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 5;   // no bloquear el health check más de 5 s
            await cmd.ExecuteScalarAsync(ct);

            sqlOk = true;
            sqlDetail = "SELECT 1 OK";
        }
        catch (Exception ex)
        {
            sqlDetail = "SQL Server no responde.";
            _log.LogWarning(ex, "Health check — SQL Server no responde");
        }

        var uptime = DateTime.UtcNow - _collector.StartedAtUtc;

        var body = new
        {
            status = sqlOk ? "healthy" : "degraded",
            uptime = FormatUptime(uptime),
            startedUtc = _collector.StartedAtUtc,
            checkedUtc = DateTime.UtcNow,
            checks = new
            {
                api = new { ok = true, detail = "Proceso respondiendo" },
                sql = new { ok = sqlOk, detail = sqlDetail }
            }
        };

        return sqlOk
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }

    // ── GET /api/metrics ──────────────────────────────────────────────────────

    /// <summary>
    /// Foto completa del rendimiento y carga del microservicio.
    /// Incluye rolling windows (1 min / 1 hora), distribución por hora del día
    /// y top 10 endpoints más llamados / más lentos.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(MetricsSnapshot), StatusCodes.Status200OK)]
    public IActionResult GetMetrics()
    {
        return Ok(_collector.BuildSnapshot());
    }

    // ── Helper privado ────────────────────────────────────────────────────────

    private static string FormatUptime(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
}