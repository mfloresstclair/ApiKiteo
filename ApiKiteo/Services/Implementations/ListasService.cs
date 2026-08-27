using System.Globalization;
using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

/// <summary>
/// Listas de prioridad — modelo de 3 niveles.
/// El `orden` de la lista ES la prioridad: 1 va primero.
/// Un circuito que vive en dos listas le cuenta a la de mayor prioridad;
/// en la otra sale como "cedido".
/// </summary>
public sealed class ListasService : IListasService
{
    private static readonly JsonSerializerOptions _camelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string ErrSinRespuesta = "El SP no devolvió respuesta.";
    private const string ErrInterno      = "Error interno. Contacta a soporte.";

    /// <summary>
    /// ISO 8601 invariante. `GetStr` sobre un DateTime usa la cultura DEL
    /// SERVIDOR: en un Windows es-MX sale "21/08/2026 11:16:00 p. m." y el
    /// cliente delgado revienta al parsear. El resto de los services del repo
    /// ya formatean explícito; éste era el único que no.
    /// </summary>
    private static string? Fecha(IDictionary<string, object?> d, string key)
    {
        var raw = d.GetValueOrDefault(key);
        return raw switch
        {
            null            => null,
            DateTime dt     => dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset o=> o.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            _ => DateTime.TryParse(raw.ToString(), CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var p)
                 ? p.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                 : raw.ToString()
        };
    }

    /// <summary>
    /// Traduce el (http_status, code, message) del SP a un fallo de la API.
    ///
    /// Los 500 NO viajan tal cual: el CATCH del SP devuelve ERROR_MESSAGE()
    /// crudo, y eso le enseña el esquema de la base a un operador de línea
    /// ("The INSERT statement conflicted with the CHECK constraint
    /// 'CK_kl_color' ... table 'dbo.kit_lista'"). Se registra completo y se
    /// manda el mensaje genérico.
    ///
    /// Los códigos también se normalizan al vocabulario de la API: el SP dice
    /// 'NOT_FOUND', la API dice KITEO_404. Sin esto un `switch (err.codigo)`
    /// en el cliente recibía KITEO_404 desde GET y NOT_FOUND desde PATCH para
    /// el mismo caso.
    /// </summary>
    private ServiceResult<T> DesdeSp<T>(
        IDictionary<string, object?> d, int httpStatus, string contexto)
    {
        var message = d.GetStr("message") ?? string.Empty;

        if (httpStatus >= 500)
        {
            _logger.LogError("SP falló en {Ctx} | http={S} | {Msg}",
                contexto, httpStatus, message);
            return ServiceResult<T>.Fail(500, ErrInterno, ErrorCodes.Kiteo500);
        }

        var codigo = httpStatus switch
        {
            404 => ErrorCodes.Kiteo404,
            403 => ErrorCodes.Kiteo403,
            _   => ErrorCodes.Kiteo400
        };

        return ServiceResult<T>.Fail(httpStatus, message, codigo);
    }

    private readonly IListasRepository      _repo;
    private readonly ILogger<ListasService> _logger;

    public ListasService(IListasRepository repo, ILogger<ListasService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 1 — contenedor
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<ListaPrioridadListResponse>> GetPrioridadesAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.PrioridadListAsync(wkname, cliente, tipo, ct);

            var prioridades = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ListaPrioridadItem
                {
                    Id          = d.GetInt("id")           ?? 0,
                    Wkname      = d.GetStr("wkname")       ?? string.Empty,
                    Cliente     = d.GetStr("cliente")      ?? string.Empty,
                    Tipo        = d.GetStr("tipo")         ?? string.Empty,
                    Nombre      = d.GetStr("nombre")       ?? string.Empty,
                    CreadoPor   = d.GetStr("creado_por"),
                    CreatedAt   = Fecha(d, "created_at"),
                    TotalListas = d.GetInt("total_listas") ?? 0,
                    TotalItems  = d.GetInt("total_items")  ?? 0
                })
                .ToList();

            return ServiceResult<ListaPrioridadListResponse>.Ok(
                new ListaPrioridadListResponse(true, prioridades));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetPrioridades wkname={W} cliente={C} tipo={T}",
                wkname, cliente, tipo);
            return ServiceResult<ListaPrioridadListResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaPrioridadCrearResponse>> CrearPrioridadAsync(
        ListaPrioridadCrearRequest request, CancellationToken ct = default)
    {
        try
        {
            // Trim SIMÉTRICO con GetPrioridades: si se inserta " wk37..." con
            // espacio y después se busca "wk37...", SQL Server NO ignora el
            // espacio a la izquierda y el contenedor queda invisible para
            // siempre. Se creó, devolvió 200, y no aparece en ninguna lista.
            var row = await _repo.PrioridadCrearAsync(
                request.Wkname.Trim(), request.Cliente.Trim(), request.Tipo.Trim(),
                request.Nombre.Trim(), request.CreadoPor.Trim(), ct);

            if (row is null)
                return ServiceResult<ListaPrioridadCrearResponse>.Fail(
                    500, ErrSinRespuesta, ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;

            if (httpStatus != 200)
                return DesdeSp<ListaPrioridadCrearResponse>(d, httpStatus, "CrearPrioridad");

            var id = d.GetInt("prioridad_id") ?? 0;

            _logger.LogInformation(
                "Prioridad creada | id={Id} wkname={W} nombre={N} por={U}",
                id, request.Wkname, request.Nombre, request.CreadoPor);

            return ServiceResult<ListaPrioridadCrearResponse>.Ok(
                new ListaPrioridadCrearResponse(true, id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearPrioridad wkname={W}", request.Wkname);
            return ServiceResult<ListaPrioridadCrearResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 2 — listas
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<ListasActivasResponse>> GetActivasAsync(
        int prioridadId, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetActivasAsync(prioridadId, ct);

            var listas = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ListaActivaItem
                {
                    Id                = d.GetInt("id")                 ?? 0,
                    PrioridadId       = d.GetInt("prioridad_id")       ?? prioridadId,
                    Orden             = d.GetInt("orden")              ?? 0,
                    Nombre            = d.GetStr("nombre")             ?? string.Empty,
                    ColorHex          = d.GetStr("color_hex")          ?? string.Empty,
                    FiltrosJson       = d.GetStr("filtros_json"),
                    AsignadoA         = d.GetStr("asignado_a"),
                    CreadoPor           = d.GetStr("creado_por"),
                    CreatedAt           = Fecha(d, "created_at"),
                    TotalItems          = d.GetInt("total_items")          ?? 0,
                    ItemsCedidos        = d.GetInt("items_cedidos")        ?? 0,
                    PendienteEfectivo   = d.GetInt("pendiente_efectivo")   ?? 0,
                    // Columna del parche 1. Si la instancia todavía no lo tiene
                    // aplicado, GetInt devuelve null y esto queda en 0 en vez
                    // de reventar.
                    CircuitosPendientes = d.GetInt("circuitos_pendientes") ?? 0,
                    // Columna del SQL v2. Misma defensa: 0 si el servidor
                    // todavia no lo tiene. Es la unica de las tres que esta
                    // en PIEZAS (VINs distintos).
                    PiezasPendientes    = d.GetInt("piezas_pendientes")    ?? 0
                })
                .ToList();

            return ServiceResult<ListasActivasResponse>.Ok(
                new ListasActivasResponse(true, listas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetActivas prioridad={P}", prioridadId);
            return ServiceResult<ListasActivasResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaCrearResponse>> CrearAsync(
        ListaCrearRequest request, CancellationToken ct = default)
    {
        try
        {
            // Array raíz, camelCase, sin wrapper — así lo espera OPENJSON.
            // Sin items la lista nace vacía y se llena después con /items.
            var jsonItems = request.Items is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Items, _camelCase)
                : null;

            var row = await _repo.CrearAsync(
                request.PrioridadId,
                request.Nombre.Trim(),
                request.ColorHex.ToUpperInvariant(),
                request.FiltrosJson,
                request.AsignadoA?.Trim(),
                request.CreadoPor.Trim(),
                jsonItems,
                request.Orden,
                ct);

            if (row is null)
                return ServiceResult<ListaCrearResponse>.Fail(
                    500, ErrSinRespuesta, ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;

            if (httpStatus != 200)
                return DesdeSp<ListaCrearResponse>(d, httpStatus, "Crear");

            var listaId    = d.GetInt("lista_id")         ?? 0;
            var orden      = d.GetInt("orden")            ?? 0;
            var insertados = d.GetInt("items_insertados") ?? 0;

            _logger.LogInformation(
                "Lista creada | id={Id} prioridad={P} orden={O} color={C} items={N} por={U}",
                listaId, request.PrioridadId, orden, request.ColorHex, insertados, request.CreadoPor);

            return ServiceResult<ListaCrearResponse>.Ok(
                new ListaCrearResponse(true, listaId, orden, insertados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Crear prioridad={P}", request.PrioridadId);
            return ServiceResult<ListaCrearResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaOkResponse>> ActualizarAsync(
        int listaId, ListaActualizarRequest request, CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.ActualizarAsync(
                listaId,
                string.IsNullOrWhiteSpace(request.Nombre) ? null : request.Nombre.Trim(),
                request.ColorHex?.ToUpperInvariant(),
                request.AsignadoA,   // "" borra, null deja igual — lo resuelve el SP
                request.Username.Trim(),
                ct);

            var res = MapSimpleResult(row, listaId, "Actualizar");

            // El log va DESPUÉS de evaluar http_status. Antes decía "Lista
            // actualizada" aunque el SP hubiera devuelto 404, y el log mentía
            // justo cuando alguien investigaba una lista perdida.
            if (res.IsSuccess)
                _logger.LogInformation(
                    "Lista actualizada | id={Id} color={C} asignado={A} por={U}",
                    listaId, request.ColorHex ?? "(sin cambio)",
                    request.AsignadoA ?? "(sin cambio)", request.Username);

            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Actualizar lista={Id}", listaId);
            return ServiceResult<ListaOkResponse>.Fail(500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaReordenarResponse>> ReordenarAsync(
        int listaId, ListaReordenarRequest request, CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.ReordenarAsync(listaId, request.Direccion, request.Username, ct);

            if (row is null)
                return ServiceResult<ListaReordenarResponse>.Fail(
                    500, ErrSinRespuesta, ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;

            if (httpStatus != 200)
                return DesdeSp<ListaReordenarResponse>(d, httpStatus, "Reordenar");

            var orden = d.GetInt("orden") ?? 0;

            _logger.LogInformation(
                "Lista reordenada | id={Id} direccion={D} orden={O} por={U}",
                listaId, request.Direccion, orden, request.Username);

            return ServiceResult<ListaReordenarResponse>.Ok(
                new ListaReordenarResponse(true, orden));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Reordenar lista={Id}", listaId);
            return ServiceResult<ListaReordenarResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaOkResponse>> EliminarAsync(
        int listaId, string username, CancellationToken ct = default)
    {
        try
        {
            // Verificar LPaccess antes de ejecutar
            var tieneAcceso = await _repo.HasLpAccessAsync(username, ct);
            if (!tieneAcceso)
                return ServiceResult<ListaOkResponse>.Fail(
                    403, "Requiere LPaccess para eliminar listas.", ErrorCodes.Kiteo403);

            var row = await _repo.EliminarAsync(listaId, username, ct);

            var res = MapSimpleResult(row, listaId, "Eliminar");

            if (res.IsSuccess)
                _logger.LogInformation("Lista eliminada | id={Id} por={U}", listaId, username);

            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Eliminar lista={Id}", listaId);
            return ServiceResult<ListaOkResponse>.Fail(500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 3 — circuitos
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<ListaDetalleResponse>> GetDetalleAsync(
        int listaId, CancellationToken ct = default)
    {
        try
        {
            var (header, itemRows) = await _repo.GetDetalleAsync(listaId, ct);

            if (header is null)
                return ServiceResult<ListaDetalleResponse>.Fail(
                    404, "Lista no encontrada.", ErrorCodes.Kiteo404);

            var h = (IDictionary<string, object?>)header;

            var lista = new ListaHeaderItem
            {
                Id              = h.GetInt("id")           ?? listaId,
                PrioridadId     = h.GetInt("prioridad_id") ?? 0,
                Orden           = h.GetInt("orden")        ?? 0,
                Nombre          = h.GetStr("nombre")       ?? string.Empty,
                ColorHex        = h.GetStr("color_hex")    ?? string.Empty,
                FiltrosJson     = h.GetStr("filtros_json"),
                AsignadoA       = h.GetStr("asignado_a"),
                CreadoPor       = h.GetStr("creado_por"),
                CreatedAt       = Fecha(h, "created_at"),
                Wkname          = h.GetStr("wkname")       ?? string.Empty,
                Cliente         = h.GetStr("cliente")      ?? string.Empty,
                Tipo            = h.GetStr("tipo")         ?? string.Empty,
                PrioridadNombre = h.GetStr("prioridad_nombre")
            };

            var items = itemRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ListaDetalleItem
                {
                    Id              = d.GetInt("id")   ?? 0,
                    Item            = d.GetStr("item") ?? string.Empty,
                    Locacion        = d.GetStr("locacion"),
                    Grupo           = d.GetStr("grupo"),
                    Etiqueta        = d.GetStr("etiqueta"),
                    CreadoPor       = d.GetStr("creado_por"),
                    CreatedAt       = Fecha(d, "created_at"),
                    NotaArea        = d.GetStr("nota_area"),
                    PendienteActual = d.GetInt("pendiente_actual") ?? 0,
                    TrabajadoActual = d.GetInt("trabajado_actual") ?? 0,
                    // Columna del parche 2. 0 si la instancia no lo tiene: la
                    // UI esconde la columna en vez de enseñar "orden 0", que
                    // seria mentira sobre un circuito que si tiene orden.
                    OrdenSemana     = d.GetInt("orden_semana")     ?? 0,
                    CedidoAOrden    = d.GetInt("cedido_a_orden")   // null = es de esta lista
                })
                .ToList();

            return ServiceResult<ListaDetalleResponse>.Ok(
                new ListaDetalleResponse(true, lista, items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetDetalle id={Id}", listaId);
            return ServiceResult<ListaDetalleResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaAgregarResponse>> AgregarItemsAsync(
        int listaId, ListaAgregarRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonItems = JsonSerializer.Serialize(request.Items, _camelCase);

            // null cuando no vienen: el SP distingue "no me mandaron grupos" de
            // "me mandaron una lista vacia", y en el primer caso no toca el
            // alcance en vez de borrarlo.
            var jsonGrupos = request.Grupos is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Grupos)
                : null;

            var row = await _repo.AgregarItemsAsync(
                listaId, jsonItems, jsonGrupos, request.Etiqueta, request.CreadoPor, ct);

            if (row is null)
                return ServiceResult<ListaAgregarResponse>.Fail(
                    500, ErrSinRespuesta, ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;

            if (httpStatus != 200)
                return DesdeSp<ListaAgregarResponse>(d, httpStatus, "AgregarItems");

            return ServiceResult<ListaAgregarResponse>.Ok(
                new ListaAgregarResponse(
                    true,
                    d.GetInt("items_insertados")            ?? 0,
                    d.GetInt("items_movidos")               ?? 0,
                    d.GetInt("items_ya_en_prioridad_mayor") ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AgregarItems lista={Id}", listaId);
            return ServiceResult<ListaAgregarResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaOkResponse>> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea, string? username,
        CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.ActualizarNotaAsync(listaId, itemId, notaArea, username, ct);
            return MapSimpleResult(row, listaId, "ActualizarNota");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ActualizarNota lista={Id} item={It}", listaId, itemId);
            return ServiceResult<ListaOkResponse>.Fail(500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaOkResponse>> QuitarItemAsync(
        int listaId, int itemId, string? username, CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.QuitarItemAsync(listaId, itemId, username, ct);
            return MapSimpleResult(row, listaId, "QuitarItem");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en QuitarItem lista={Id} item={It}", listaId, itemId);
            return ServiceResult<ListaOkResponse>.Fail(500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Panel F6
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<GruposMarcadosResponse>> GetGruposMarcadosAsync(
        int prioridadId, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GruposMarcadosAsync(prioridadId, ct);

            var grupos = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new GrupoMarcadoItem
                {
                    Grupo       = d.GetStr("grupo")        ?? string.Empty,
                    ListaId     = d.GetInt("lista_id")     ?? 0,
                    Orden       = d.GetInt("orden")        ?? 0,
                    ListaNombre = d.GetStr("lista_nombre") ?? string.Empty,
                    ColorHex    = d.GetStr("color_hex")    ?? string.Empty,
                    Circuitos   = d.GetInt("circuitos")    ?? 0
                })
                .ToList();

            return ServiceResult<GruposMarcadosResponse>.Ok(
                new GruposMarcadosResponse(true, grupos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetGruposMarcados prioridad={P}", prioridadId);
            return ServiceResult<GruposMarcadosResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<ListaPiezasResponse>> GetPiezasAsync(
        int listaId, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.PiezasAsync(listaId, ct);

            var lista = rows
                .Select(r => (IDictionary<string, object?>)r)
                .ToList();

            // El SP devuelve (http_status, code, message) cuando la lista no
            // existe. Sin esta rama el 404 se serializaba como una "pieza"
            // vacia y el cliente pintaba una fila fantasma.
            var primero = lista.FirstOrDefault();
            if (primero is not null && primero.ContainsKey("http_status"))
            {
                var st = primero.GetInt("http_status") ?? 500;
                if (st != 200) return DesdeSp<ListaPiezasResponse>(primero, st, "GetPiezas");
            }

            var piezas = lista
                .Where(d => d.ContainsKey("vin"))
                .Select(d => new ListaPiezaItem
                {
                    Vin              = d.GetStr("vin") ?? string.Empty,
                    Grupo            = d.GetStr("grupo"),
                    Locacion         = d.GetInt("locacion"),
                    VinDesc          = d.GetStr("vin_desc"),
                    Secuencia        = d.GetStr("secuencia"),
                    CircuitosFaltan  = d.GetInt("circuitos_faltan")  ?? 0,
                    CircuitosCedidos = d.GetInt("circuitos_cedidos") ?? 0,
                    Circuitos        = d.GetStr("circuitos") ?? string.Empty
                })
                .ToList();

            return ServiceResult<ListaPiezasResponse>.Ok(
                new ListaPiezasResponse(true, piezas.Count, piezas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetPiezas id={Id}", listaId);
            return ServiceResult<ListaPiezasResponse>.Fail(
                500, ErrInterno, ErrorCodes.Kiteo500);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private ServiceResult<ListaOkResponse> MapSimpleResult(
        dynamic? row, int listaId, string contexto)
    {
        if (row is null)
            return ServiceResult<ListaOkResponse>.Fail(
                404, $"Lista {listaId} no encontrada.", ErrorCodes.Kiteo404);

        var d          = (IDictionary<string, object?>)row;
        var httpStatus = d.GetInt("http_status") ?? 500;

        return httpStatus == 200
            ? ServiceResult<ListaOkResponse>.Ok(new ListaOkResponse(true))
            : DesdeSp<ListaOkResponse>(d, httpStatus, contexto);
    }
}
