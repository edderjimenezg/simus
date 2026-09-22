namespace PNMC.Contracts;

/// <summary>
/// Una iniciativa del Programa que atraviesa varios módulos.
/// </summary>
/// <remarks>
/// NO ES UNA CATEGORIA TEMATICA. La categoría dice de qué trata un contenido; el proyecto
/// transversal dice a qué iniciativa pertenece. Un evento puede ser de «Encuentros» y además de
/// Celebra la Música.
/// </remarks>
public sealed record ProyectoTransversalDto(
    int Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool Activo,
    int OrdenVisualizacion,
    /// <summary>Cuánto contenido lo lleva enlazado. Decide si desactivarlo tiene consecuencias.</summary>
    int ContenidosEnlazados);

public sealed class GuardarProyectoTransversalSolicitud
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int? OrdenVisualizacion { get; set; }
}
