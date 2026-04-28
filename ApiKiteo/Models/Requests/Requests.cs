using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiKiteo.API.Models.Requests;

// ─── Auth ────────────────────────────────────────────────────────────────────

/// <summary>POST /auth/login</summary>
public sealed record AuthLoginRequest(
    [Required] string Username,
    [Required] string Password
);

// ─── Semanas ──────────────────────────────────────────────────────────────────

// GET /semanas?cliente=TBB&tipo=CEA   (query params, no body)
// GET /semanas_pendientes              (sin parámetros)

/// <summary>POST /semana_grp_faltantes</summary>
public sealed record SemanaGrpFaltantesRequest(
    [Required] string Wkname,
    string? Det,
    [Required] List<string> Grupos
);

// ─── Escaneo ──────────────────────────────────────────────────────────────────

/// <summary>POST /vin_to_adjust</summary>
public sealed record VinToAdjustRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] string Empleado
);

/// <summary>POST /escanear_ajuste</summary>
public sealed record EscanearAjusteRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] string Empleado,
    [Required] List<string> Vines
);

/// <summary>POST /escanear</summary>
public sealed record EscanearRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] int Cantidad,
    [Required] string Empleado
);

/// <summary>POST /semana_vines_entrega</summary>
public sealed record SemanaVinesEntregaRequest(
    [Required] string Wkname,
    [Required] string Empleado,
    [Required] List<string> Vines,
    string? Comentario,
    string? Supervisor
);

// ─── Admin ────────────────────────────────────────────────────────────────────

/// <summary>POST /api/semanas/aprobar</summary>
public sealed record AprobarSemanaRequest(
    [Required] string Wkname,
    [Required] string AprobadoPor
);