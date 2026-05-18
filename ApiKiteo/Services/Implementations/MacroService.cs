using System.Text;
using ApiKiteo.API.Common;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class MacroService : IMacroService
{
    private readonly IMacroRepository _repo;
    private readonly ILogger<MacroService> _logger;

    // Columnas en orden — deben coincidir exactamente con el SELECT del repo
    private static readonly string[] Headers =
    [
        "WkName", "Vin", "VinDesc", "Overlay", "Grupo",
        "Item", "ItemDescripcion", "Locacion", "Tipo", "Cliente",
        "Operador", "Recorddate", "Entregado", "EntregadoPor"
    ];

    public MacroService(IMacroRepository repo, ILogger<MacroService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StreamCsvAsync(
        IReadOnlyList<string> wknames,
        string? tipo,
        string? cliente,
        DateOnly? desde,
        DateOnly? hasta,
        Stream output,
        CancellationToken ct = default)
    {
        // UTF-8 con BOM para que Excel lo abra correctamente sin configurar nada
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

        // Cabecera CSV
        await writer.WriteLineAsync(string.Join(",", Headers));

        var totalFilas = 0;

        await _repo.StreamMacroAsync(
            wknames, tipo, cliente, desde, hasta,
            async rows =>
            {
                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();

                    var d = (IDictionary<string, object?>)row;

                    var linea = string.Join(",",
                    [
                        EscapeCsv(d.GetValueOrDefault("WkName")),
                        EscapeCsv(d.GetValueOrDefault("Vin")),
                        EscapeCsv(d.GetValueOrDefault("vinDesc")),
                        EscapeCsv(d.GetValueOrDefault("overlay")),
                        EscapeCsv(d.GetValueOrDefault("Grupo")),
                        EscapeCsv(d.GetValueOrDefault("item")),
                        EscapeCsv(d.GetValueOrDefault("ItemDescripcion")),
                        EscapeCsv(d.GetValueOrDefault("Locacion")),
                        EscapeCsv(d.GetValueOrDefault("tipo")),
                        EscapeCsv(d.GetValueOrDefault("Cliente")),
                        EscapeCsv(d.GetValueOrDefault("Operador")),
                        EscapeCsvDate(d.GetValueOrDefault("recorddate")),
                        EscapeCsvDate(d.GetValueOrDefault("Entregado")),
                        EscapeCsv(d.GetValueOrDefault("EntregadoPor"))
                    ]);

                    await writer.WriteLineAsync(linea);
                    totalFilas++;

                    // Flush cada 1000 filas — evita acumular demasiado en buffer
                    if (totalFilas % 1_000 == 0)
                        await writer.FlushAsync(ct);
                }
            },
            ct);

        await writer.FlushAsync(ct);

        _logger.LogInformation(
            "MacroExport completado | wknames={Wk} tipo={T} cliente={C} filas={F}",
            string.Join(",", wknames), tipo, cliente, totalFilas);
    }

    // ── Helpers CSV ───────────────────────────────────────────────────────────

    /// <summary>
    /// RFC 4180: si el valor contiene coma, comilla o salto de línea
    /// se envuelve en comillas dobles y las comillas internas se duplican.
    /// </summary>
    private static string EscapeCsv(object? val)
    {
        if (val is null || val is DBNull) return string.Empty;

        var s = val.ToString()!;

        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";

        return s;
    }

    /// <summary>
    /// Fechas en formato ISO 8601 yyyy-MM-dd HH:mm:ss para que Excel las interprete.
    /// </summary>
    private static string EscapeCsvDate(object? val)
    {
        if (val is null || val is DBNull) return string.Empty;

        if (val is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss");

        if (DateTime.TryParse(val.ToString(), out var parsed))
            return parsed.ToString("yyyy-MM-dd HH:mm:ss");

        return val.ToString() ?? string.Empty;
    }
}