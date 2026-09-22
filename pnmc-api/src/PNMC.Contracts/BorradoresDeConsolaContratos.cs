namespace PNMC.Contracts;

/// <summary>
/// El borrador automático de un formulario de la consola institucional.
/// </summary>
/// <remarks>
/// <c>DatosJson</c> viaja como texto y no como un objeto tipado a propósito: cada formulario guarda
/// su propia forma —un evento no se parece a una ficha bibliográfica—, y tiparlo obligaría a un
/// contrato por módulo que habría que tocar cada vez que uno de ellos gana un campo. Lo que el
/// servidor sí comprueba es que sea JSON válido: guardar texto roto haría irrecuperable justo lo
/// que este mecanismo existe para recuperar.
/// </remarks>
public sealed record BorradorDeConsolaDto(
    long Id,
    string Dominio,
    string DatosJson,
    int Version,
    DateTime FechaActualizacion);

public sealed class GuardarBorradorDeConsolaSolicitud
{
    public string? DatosJson { get; set; }

    /// <summary>
    /// La versión que se estaba editando.
    /// </summary>
    /// <remarks>
    /// AUSENTE SE ACEPTA porque el primer guardado de una sesión no ha leído ninguna. Presente y
    /// distinta se rechaza: significa que la misma persona tiene el formulario abierto en otra
    /// pestaña, y dejar que gane el último pulsador borraría lo escrito en la otra sin avisar.
    /// </remarks>
    public int? Version { get; set; }
}
