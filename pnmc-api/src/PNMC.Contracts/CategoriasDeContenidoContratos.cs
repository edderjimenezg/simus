namespace PNMC.Contracts;

/// <summary>
/// Una categoría temática de contenido.
/// </summary>
/// <remarks>
/// <c>CodigoModulo</c> es `agenda`, `noticias`, `editorial` o <b>`comun`</b>. El último las hace
/// compartidas por los tres sin obligar a que todas lo sean.
/// </remarks>
public sealed record CategoriaDeContenidoDto(
    int Id,
    string CodigoModulo,
    string NombreCategoria,
    string Slug,
    string? Descripcion,
    int OrdenVisualizacion,
    /// <summary>
    /// Cuántos contenidos la usan.
    /// </summary>
    /// <remarks>
    /// ES LO QUE PERMITE DECIDIR. Sin esta cifra, elegir entre categorías propias de cada módulo y
    /// categorías comunes es una discusión sin datos: con ella se ve qué categoría se está usando
    /// en dos sitios y merece compartirse, y cuál está vacía y sobra.
    /// </remarks>
    int ContenidosQueLaUsan);

public sealed class GuardarCategoriaSolicitud
{
    public string CodigoModulo { get; set; } = string.Empty;
    public string NombreCategoria { get; set; } = string.Empty;
    /// <summary>Opcional: si no viene, se calcula del nombre.</summary>
    public string? Slug { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>Opcional: si no viene, la nueva va al final de su módulo.</summary>
    public int? OrdenVisualizacion { get; set; }
}

/// <summary>Con qué categoría se fusiona la que se está retirando.</summary>
/// <remarks>
/// LA QUE ABSORBE PUEDE SER DE OTRO MODULO, y ese es el caso interesante: fusionar «Encuentros» de
/// Agenda dentro de «Encuentros» común es cómo se converge a un vocabulario compartido sin perder
/// lo ya publicado.
/// </remarks>
public sealed class FusionarCategoriaSolicitud
{
    public int DestinoId { get; set; }
}
