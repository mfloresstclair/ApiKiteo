using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class LiberacionService : ILiberacionService
{
    private readonly ILiberacionRepository _repo;
    private readonly ILogger<LiberacionService> _logger;

    public LiberacionService(
        ILiberacionRepository repo,
        ILogger<LiberacionService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── GET /api/liberacion/semanas ───────────────────────────────────────────

    public async Task<ServiceResult<LiberacionSemanasResponse>> GetSemanasAsync(
        string estatus, string cliente, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanasAsync(estatus, cliente, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionSemanaItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Estatus = d.GetStr("estatus") ?? string.Empty,
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    CreadoEn = d.GetValueOrDefault("creado_en")?.ToString()
                })
                .ToList();

            return ServiceResult<LiberacionSemanasResponse>.Ok(
                new LiberacionSemanasResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetSemanas liberación cliente={C}", cliente);
            return ServiceResult<LiberacionSemanasResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/resumen ──────────────────────────────────────────

    public async Task<ServiceResult<LiberacionResumenResponse>> GetResumenAsync(
        LiberacionRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación resumen | usuario={U} semanas={N} cliente={C}",
                request.Username, request.Wknames.Count, request.Cliente);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var rows = await _repo.GetResumenAsync(json, request.Username, request.Cliente, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionResumenItem
                {
                    Item = d.GetStr("item") ?? string.Empty,
                    Cant = GetDecimal(d.GetValueOrDefault("Cant")),
                    Cliente = d.GetStr("cliente") ?? string.Empty
                })
                .ToList();

            return ServiceResult<LiberacionResumenResponse>.Ok(
                new LiberacionResumenResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación resumen usuario={U}", request.Username);
            return ServiceResult<LiberacionResumenResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/detalle ──────────────────────────────────────────

    public async Task<ServiceResult<LiberacionDetalleResponse>> GetDetalleAsync(
        LiberacionRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación detalle | usuario={U} semanas={N} cliente={C}",
                request.Username, request.Wknames.Count, request.Cliente);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var rows = await _repo.GetDetalleAsync(json, request.Username, request.Cliente, ct);

            // Devuelve todo — el WinForms pagina localmente con VirtualMode
            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionDetalleItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Tipo = d.GetStr("tipo") ?? string.Empty,
                    Item = d.GetStr("item") ?? string.Empty,
                    QtyOrdered = GetDecimal(d.GetValueOrDefault("qty_ordered")),
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    Vin = d.GetStr("vin") ?? string.Empty
                })
                .ToList();

            return ServiceResult<LiberacionDetalleResponse>.Ok(
                new LiberacionDetalleResponse(
                    Ok: true,
                    Total: resultados.Count,
                    Resultados: resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación detalle usuario={U}", request.Username);
            return ServiceResult<LiberacionDetalleResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static decimal GetDecimal(object? val)
    {
        if (val is null || val is DBNull) return 0m;
        return val switch
        {
            decimal d => d,
            double v => (decimal)v,
            float f => (decimal)f,
            _ => decimal.TryParse(val.ToString(), out var p) ? p : 0m
        };
    }
}