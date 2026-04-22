using System.Text.Json;
using KiteoAdmin.API.Common;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Models.Responses;
using KiteoAdmin.API.Repositories.Interfaces;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Services.Implementations;

public sealed class VinsService : IVinsService
{
    private readonly IVinsRepository _repo;
    private readonly ILogger<VinsService> _logger;

    public VinsService(IVinsRepository repo, ILogger<VinsService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── /semana_loc ───────────────────────────────────────────────────────────

    public async Task<ServiceResult<SemanaLocResponse>> GetSemanaLocAsync(
        string wkname, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanaLocAsync(wkname, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new SemanaLocItem
                {
                    Vin = d.GetStr("vin"),
                    Locacion = d.GetInt("locacion"),
                    Grupo = d.GetStr("grupo"),
                    Item = d.GetStr("item"),
                    Descripcion = d.GetStr("descripcion")
                })
                .ToList();

            return ServiceResult<SemanaLocResponse>.Ok(
                new SemanaLocResponse(true, wkname, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanaLoc {Wk}", wkname);
            return ServiceResult<SemanaLocResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /semana_grp_status ────────────────────────────────────────────────────

    public async Task<ServiceResult<SemanaGrpStatusResponse>> GetSemanaGrpStatusAsync(
        string wkname, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanaGrpStatusAsync(wkname, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new SemanaGrpStatusItem
                {
                    Grupo = d.GetStr("Grupo") ?? d.GetStr("grupo") ?? string.Empty,
                    Vindesc = NormalizeVindesc(d.GetStr("vindesc")),
                    Vines = d.GetInt("vines") ?? 0,
                    Porcentaje = d.GetDecimal("Porcentaje") ?? 0m
                })
                .ToList();

            return ServiceResult<SemanaGrpStatusResponse>.Ok(
                new SemanaGrpStatusResponse(true, wkname, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanaGrpStatus {Wk}", wkname);
            return ServiceResult<SemanaGrpStatusResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /semana_grp_faltantes ─────────────────────────────────────────────────

    public async Task<ServiceResult<SemanaGrpFaltantesResponse>> GetSemanaGrpFaltantesAsync(
        SemanaGrpFaltantesRequest request, CancellationToken ct = default)
    {
        try
        {
            // El SP espera: {"grupos": ["GRP01","GRP02"]}
            var jsonGrupos = JsonSerializer.Serialize(new { grupos = request.Grupos });
            var det = request.Det ?? "1";

            var rows = await _repo.GetSemanaGrpFaltantesAsync(
                request.Wkname, jsonGrupos, det, ct);

            // Resultado genérico — columnas varían según det
            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => d.ToDictionary(k => k.Key, v => v.Value))
                .ToList();

            return ServiceResult<SemanaGrpFaltantesResponse>.Ok(
                new SemanaGrpFaltantesResponse(
                    true, request.Wkname, det, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanaGrpFaltantes {Wk}", request.Wkname);
            return ServiceResult<SemanaGrpFaltantesResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /semana_vin_status ────────────────────────────────────────────────────

    public async Task<ServiceResult<SemanaVinStatusResponse>> GetSemanaVinStatusAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanaVinStatusAsync(wkname, cliente, tipo, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new SemanaVinStatusItem
                {
                    Locacion = d.GetInt("Locacion") ?? d.GetInt("locacion"),
                    Vin = d.GetStr("Vin") ?? d.GetStr("vin"),
                    Porcentaje = d.GetDecimal("Porcentaje") ?? 0m
                })
                .ToList();

            return ServiceResult<SemanaVinStatusResponse>.Ok(
                new SemanaVinStatusResponse(true, wkname, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanaVinStatus {Wk}", wkname);
            return ServiceResult<SemanaVinStatusResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }
    // ── Helper: normalizar vindesc → formato estandarizado ─────────────────
    // Regla única: si contiene número + WDO (con cualquier prefijo o sufijo)
    //              extraer solo NúmeroWDO sin espacios ni sufijos.
    //              Si no tiene WDO → pasar crudo (BodyCVZC, BodyCVZD, etc.)
    //
    //   "CEEA+ 8 WDO"   → "8WDO"
    //   "CEEA+ 10 WDO"  → "10WDO"
    //   "12 WDO EFX"    → "12WDO"   ← sufijo removido
    //   "12 WDO HDX"    → "12WDO"   ← sufijo removido
    //   "13 WDO EFX"    → "13WDO"   ← sufijo removido
    //   "10WDO"         → "10WDO"   ← ya correcto
    //   "5WDO"          → "5WDO"    ← ya correcto
    //   "BodyCVZC"      → "BodyCVZC" ← sin WDO, pasa crudo
    //   null / ""       → null
    private static string? NormalizeVindesc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Buscar patrón: cualquier número seguido (con o sin espacios) de WDO
        // Captura solo el número — el sufijo (EFX, HDX, etc.) y el prefijo (CEEA+) se ignoran 
        var match = System.Text.RegularExpressions.Regex.Match(
            raw.Trim(),
            @"(\d+)\s*WDO",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
            return match.Groups[1].Value + "WDO";

        // Sin número+WDO → devolver crudo trimmed (BodyCVZC, BodyCVZD, etc.)
        return raw.Trim();
    }

}