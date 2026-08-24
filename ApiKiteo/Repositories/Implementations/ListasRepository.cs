using ApiKiteo.API.Common;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;
using Dapper;
using Microsoft.Extensions.Options;

namespace ApiKiteo.API.Repositories.Implementations;

/// <summary>
/// Listas de prioridad — modelo de 3 niveles.
///   Nivel 1  kit_lista_prioridad  · el contenedor de la semana
///   Nivel 2  kit_lista            · la lista, con color y orden (= la prioridad)
///   Nivel 3  kit_lista_item       · el circuito, con su etiqueta
/// </summary>
public sealed class ListasRepository : IListasRepository
{
    private const int TimeoutSp = 30;

    private readonly IDbConnectionFactory    _db;
    private readonly StoredProceduresOptions _sp;

    public ListasRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 1 — contenedor
    // ══════════════════════════════════════════════════════════════════════════

    // ── POST /listas/prioridades ──────────────────────────────────────────────

    public async Task<dynamic?> PrioridadCrearAsync(
        string wkname, string cliente, string tipo, string nombre, string creadoPor,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaPrioridadCrear,
            new { wkname, cliente, tipo, nombre, creado_por = creadoPor },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── GET /listas/prioridades ───────────────────────────────────────────────

    public async Task<IEnumerable<dynamic>> PrioridadListAsync(
        string wkname, string cliente, string tipo,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.ListaPrioridadList,
            new { wkname, cliente, tipo },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 2 — listas
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /listas?prioridadId= ──────────────────────────────────────────────

    public async Task<IEnumerable<dynamic>> GetActivasAsync(
        int prioridadId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.ListasActivas,
            new { prioridad_id = prioridadId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── POST /listas ──────────────────────────────────────────────────────────

    public async Task<dynamic?> CrearAsync(
        int prioridadId, string nombre, string colorHex,
        string? filtrosJson, string? asignadoA, string creadoPor,
        string? jsonItems, int? orden,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaCrear,
            new
            {
                prioridad_id = prioridadId,
                nombre,
                color_hex    = colorHex,
                filtros_json = filtrosJson,
                asignado_a   = asignadoA,
                creado_por   = creadoPor,
                jsonItems,
                orden
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── PATCH /listas/:id ─────────────────────────────────────────────────────
    // asignadoA == "" borra el asignado; null lo deja como está (lo resuelve el SP).

    public async Task<dynamic?> ActualizarAsync(
        int listaId, string? nombre, string? colorHex, string? asignadoA, string username,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaActualizar,
            new
            {
                lista_id   = listaId,
                nombre,
                color_hex  = colorHex,
                asignado_a = asignadoA,
                username
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── POST /listas/:id/reordenar ────────────────────────────────────────────

    public async Task<dynamic?> ReordenarAsync(
        int listaId, short direccion, string username,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaReordenar,
            new { lista_id = listaId, direccion, username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── DELETE /listas/:id ────────────────────────────────────────────────────

    public async Task<dynamic?> EliminarAsync(
        int listaId, string username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaEliminar,
            new { lista_id = listaId, username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 3 — circuitos
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /listas/:id ───────────────────────────────────────────────────────
    // Kit_lista_detalle devuelve 2 result sets: header + items

    public async Task<(dynamic? Header, IEnumerable<dynamic> Items)> GetDetalleAsync(
        int listaId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            _sp.ListaDetalle,
            new { lista_id = listaId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);

        var header = await multi.ReadFirstOrDefaultAsync();

        // Sin header no vale la pena leer el segundo result set.
        if (header is null)
            return (null, Array.Empty<dynamic>());

        var items = (await multi.ReadAsync()).ToList();
        return (header, items);
    }

    // ── POST /listas/:id/items ────────────────────────────────────────────────

    public async Task<dynamic?> AgregarItemsAsync(
        int listaId, string jsonItems, string? etiqueta, string? creadoPor,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaAgregar,
            new { lista_id = listaId, jsonItems, etiqueta, creado_por = creadoPor },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── PUT /listas/:id/items/:item_id/nota ───────────────────────────────────

    public async Task<dynamic?> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea, string? username,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaNota,
            new { lista_id = listaId, item_id = itemId, nota_area = notaArea, username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── DELETE /listas/:id/items/:item_id ─────────────────────────────────────

    public async Task<dynamic?> QuitarItemAsync(
        int listaId, int itemId, string? username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaQuitarItem,
            new { lista_id = listaId, item_id = itemId, username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Panel F6 — franja de color por grupo
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<dynamic>> GruposMarcadosAsync(
        int prioridadId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.ListaGruposMarcados,
            new { prioridad_id = prioridadId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── GET /listas/:id/piezas ────────────────────────────────────────────────

    public async Task<IEnumerable<dynamic>> PiezasAsync(
        int listaId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.ListaPiezas,
            new { lista_id = listaId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);
    }

    // ── Verificar LPaccess (inline — sin SP propio) ───────────────────────────

    public async Task<bool> HasLpAccessAsync(
        string username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Mismo SP que AuthService.LoginAsync (Kit_vin_User_Access)
        var rows = await conn.QueryAsync(
            _sp.GetUserAccess,
            new { username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: TimeoutSp);

        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;

            // El SP puede devolver columna "Access" como string,
            // o columnas booleanas LPaccess/FAaccess — igual que AuthService
            var accStr = d.GetStr("Access")?.Trim().ToLowerInvariant();
            if (accStr == "lpaccess") return true;

            if (d.GetValueOrDefault("LPaccess") is bool lp && lp) return true;
        }

        return false;
    }
}
