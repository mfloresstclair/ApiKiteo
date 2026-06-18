using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

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
                    Porcentaje = d.GetDecimal("Porcentaje") ?? 0m,
                    Descripcion = d.GetStr("descripcion"),
                    Motherharness = d.GetStr("motherharness"),
                    TotalCircuitos = d.GetInt("total_circuitos") ?? 0,
                    EscaneadosCircuitos = d.GetInt("escaneados_circuitos") ?? 0
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
                request.Wkname, jsonGrupos, det,
                request.Descripcion,   // NUEVO
                ct);

            // Resultado genérico — columnas varían según det
            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d =>
                {
                    var dict = d.ToDictionary(k => k.Key, v => v.Value);

                    var key = dict.Keys.FirstOrDefault(k =>
                        k.Equals("locacion", StringComparison.OrdinalIgnoreCase));

                    if (key is not null && dict[key] is not null)
                    {
                        var loc = Convert.ToInt32(dict[key]);

                        dict[key] = det switch
                        {
                            "1" => loc == 0 ? "MANDAR A FINAL" : (object?)loc,  // detalle: número o MANDAR A FINAL
                            "0" => loc == 0 ? "MANDAR A FINAL" : null,           // resumen: MANDAR A FINAL o null
                            _ => loc == 0 ? "MANDAR A FINAL" : (object?)loc    // default: igual que det=1
                        };
                    }

                    return dict;
                })
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
        string wkname, string cliente, string tipo,
        byte modo = 1,
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanaVinStatusAsync(wkname, cliente, tipo, modo, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Where(d => (d.GetInt("Locacion") ?? 0) != 0)
                .Select(d => new SemanaVinStatusItem
                {
                    Locacion = d.GetInt("Locacion"),
                    Vin = d.GetStr("Vin"),
                    Vindesc = NormalizeVindesc(d.GetStr("vinDesc")),
                    Sequence = d.GetStr("sequence"),
                    Porcentaje = d.GetDecimal("Porcentaje") ?? 0m,
                    Entregado = d.GetStr("entregado"),
                    EntregadoPor = d.GetStr("entregadoPor")
                })
                .ToList();

            return ServiceResult<SemanaVinStatusResponse>.Ok(
                new SemanaVinStatusResponse(true, wkname, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error GetSemanaVinStatus {Wk} modo={M}", wkname, modo);
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

    // ── /buscar_circuito ──────────────────────────────────────────────────────

    public async Task<ServiceResult<BuscarCircuitoResponse>> BuscarCircuitoAsync(
        string wkname, string circuito, bool soloFaltantes,
        CancellationToken ct = default)
    {
        try
        {
            // El SP espera varchar(1): '1' = solo pendientes, '0' = todos
            var soloFaltantesParam = soloFaltantes ? "1" : "0";

            var rows = await _repo.BuscarCircuitoAsync(wkname, circuito, soloFaltantesParam, ct);
            var list = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            // El SP devuelve fila única de error en casos 400 / 404
            if (list.Count == 1)
            {
                var spError = TryExtractSpError<BuscarCircuitoResponse>(list[0]);
                if (spError is not null) return spError;
            }

            var resultados = list
                .Select(d => new BuscarCircuitoItem
                {
                    Locacion = d.GetInt("Locacion"),
                    EscaneadoEn = FormatDateTime(d.GetValueOrDefault("escaneado_en")),
                    Vin = d.GetStr("Vin"),
                    Grupo = d.GetStr("Grupo"),
                    Vindesc = d.GetStr("vindesc"),
                    Overlay = d.GetStr("overlay"),
                    Item = d.GetStr("item"),
                    Descripcion = d.GetStr("descripcion"),
                    Estado = d.GetStr("estado"),        // "PENDIENTE" | "KITEADO" | "ENTREGADO"
                    Operador = d.GetStr("operador"),
                    Entregado = FormatDateTime(d.GetValueOrDefault("entregado")),
                    EntregadoPor = d.GetStr("entregado_por"),
                    EsMandarAFinal = (d.GetInt("es_mandar_a_final") ?? 0) == 1
                })
                .ToList();

            return ServiceResult<BuscarCircuitoResponse>.Ok(
                new BuscarCircuitoResponse(
                    true, wkname, circuito, soloFaltantes, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en BuscarCircuito wkname={Wk} circuito={Cir}", wkname, circuito);
            return ServiceResult<BuscarCircuitoResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lee http_status del rowset del SP.
    /// Devuelve null si es 200 o si la columna no existe (resultado normal).
    /// </summary>
    private static ServiceResult<T>? TryExtractSpError<T>(IDictionary<string, object?> d)
    {
        var rawStatus = d.GetValueOrDefault("http_status");
        if (rawStatus is null) return null;

        if (!int.TryParse(rawStatus.ToString(), out var httpStatus)) return null;
        if (httpStatus == 200) return null;

        var mensaje = d.GetValueOrDefault("message")?.ToString() ?? "Error al procesar la solicitud.";
        var codigo = d.GetValueOrDefault("code")?.ToString() ?? ErrorCodes.Kiteo500;

        return ServiceResult<T>.Fail(httpStatus, mensaje, codigo);
    }

    /// <summary>
    /// Convierte DateTime a ISO 8601 completo con hora — campo Entregado sí tiene hora relevante.
    /// </summary>
    private static string? FormatDateTime(object? val)
    {
        if (val is null || val is DBNull) return null;

        return val switch
        {
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
            _ => DateTime.TryParse(val.ToString(), out var parsed)
                               ? parsed.ToString("yyyy-MM-ddTHH:mm:ss")
                               : val.ToString()
        };
    }

}