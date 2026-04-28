namespace ApiKiteo.API.Common;

/// <summary>
/// Envuelve respuestas exitosas con paginación o totales.
/// Usado cuando el SP devuelve una colección de resultados.
/// </summary>
public sealed record ListResult<T>(
    bool Ok,
    string Wkname,
    int Total,
    IReadOnlyList<T> Resultados
);

/// <summary>
/// Respuesta de error estándar — contrato fijo hacia el cliente.
/// Forma: { exito, mensaje, codigo }
/// </summary>
public sealed record ErrorResponse(
    bool Exito,
    string Mensaje,
    string Codigo
)
{
    public static ErrorResponse Create(string mensaje, string codigo)
        => new(false, mensaje, codigo);
}

/// <summary>
/// Resultado interno que viaja entre Service → Controller.
/// Evita lanzar excepciones para flujos de negocio esperados.
/// </summary>
public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value      { get; private init; }
    public int HttpStatus { get; private init; }
    public string Mensaje { get; private init; } = string.Empty;
    public string Codigo  { get; private init; } = string.Empty;

    public static ServiceResult<T> Ok(T value) =>
        new() { IsSuccess = true, Value = value, HttpStatus = 200 };

    public static ServiceResult<T> Fail(int status, string mensaje, string codigo) =>
        new() { IsSuccess = false, HttpStatus = status, Mensaje = mensaje, Codigo = codigo };
}
