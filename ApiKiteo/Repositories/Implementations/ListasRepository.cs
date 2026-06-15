using ApiKiteo.API.Common;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;
using Dapper;
using Microsoft.Extensions.Options;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class ListasRepository : IListasRepository
{
    private readonly IDbConnectionFactory    _db;
    private readonly StoredProceduresOptions _sp;

    public ListasRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    // ── GET /listas ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<dynamic>> GetActivasAsync(
        string? wkname, string? cliente, string? tipo,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.ListasActivas,
            new { wkname, cliente, tipo },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── POST /listas ───────────────────────────────────────────────────────────

    public async Task<dynamic?> GuardarAsync(
        string wkname, string cliente, string tipo,
        string? gruposJson, string? det, string? filtroLoc,
        string? textoBusqueda, string creadoPor, string jsonItems,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaGuardar,
            new
            {
                wkname,
                cliente,
                tipo,
                grupos_json = gruposJson,
                det,
                filtro_loc = filtroLoc,
                texto_busqueda = textoBusqueda,
                creado_por = creadoPor,
                jsonItems
            },   // ← FIX: el SP espera @jsonItems
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── GET /listas/:id ────────────────────────────────────────────────────────
    // Kit_lista_detalle devuelve 2 result sets: header + items

    public async Task<(dynamic? Header, IEnumerable<dynamic> Items)> GetDetalleAsync(
        int listaId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            _sp.ListaDetalle,
            new { lista_id = listaId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        var header = await multi.ReadFirstOrDefaultAsync();
        var items  = (await multi.ReadAsync()).ToList();
        return (header, items);
    }

    // ── POST /listas/:id/items ─────────────────────────────────────────────────
    public async Task<dynamic?> AgregarItemsAsync(
        int listaId, string jsonItems, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaAgregar,
            new { lista_id = listaId, jsonItems },   // ← FIX: el SP espera @jsonItems
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── PATCH /listas/:id/items/:item_id/nota ──────────────────────────────────

    public async Task<dynamic?> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaNota,
            new { lista_id = listaId, item_id = itemId, nota_area = notaArea },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── DELETE /listas/:id/items/:item_id ─────────────────────────────────────

    public async Task<dynamic?> QuitarItemAsync(
        int listaId, int itemId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaQuitarItem,
            new { lista_id = listaId, item_id = itemId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── DELETE /listas/:id ────────────────────────────────────────────────────

    public async Task<dynamic?> EliminarAsync(
        int listaId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            _sp.ListaEliminar,
            new { lista_id = listaId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    // ── Verificar LPaccess (inline SQL — sin SP equivalente) ──────────────────


    public async Task<bool> HasLpAccessAsync(
        string username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Mismo SP que AuthService.LoginAsync (Kit_vin_User_Access)
        var rows = await conn.QueryAsync(
            _sp.GetUserAccess,
            new { username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

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
