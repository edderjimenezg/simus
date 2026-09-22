using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Regla unica de «esto es publico»: el estado del contenido debe ser
/// <c>publicado</c>.
/// </summary>
/// <remarks>
/// <para>
/// El esquema trae desde el principio la tabla <c>EstadosContenido</c> con los
/// codigos <c>borrador</c>, <c>publicado</c> y <c>archivado</c>, y Noticias,
/// Agenda y los albumes de Galeria guardan a cual pertenecen. Las rutas anonimas
/// no la miraban: hacian <c>ToListAsync()</c> sobre la tabla entera. Un borrador
/// a medio escribir, o una nota archivada a proposito, se servia igual al
/// visitante.
/// </para>
/// <para>
/// No se noto antes porque las filas sembradas estan todas publicadas. Eso es lo
/// peligroso: el defecto no aparece hasta que alguien guarda el primer borrador
/// de verdad, y para entonces ya esta en el portal.
/// </para>
/// <para>
/// Vive en un solo sitio a proposito. Repetir la condicion en cada consulta es
/// exactamente como se llega a que una de ellas se olvide.
/// </para>
/// </remarks>
public static class EstadoPublicado
{
    public const string Codigo = "publicado";

    /// <summary>
    /// Identificador del estado publicado, o <c>null</c> si el catalogo no lo
    /// tiene.
    /// </summary>
    /// <remarks>
    /// Que devuelva <c>null</c> cuando el estado no existe es deliberado: quien
    /// filtra por <c>StatusId == null</c> no encuentra nada, y una lista vacia es
    /// el fallo correcto. Lo contrario —tratar «no se cual es el estado» como «no
    /// filtres»— convertiria un catalogo mal sembrado en una fuga de borradores.
    /// </remarks>
    public static Task<int?> ResolverIdAsync(PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        dbContext.ContentStatuses
            .AsNoTracking()
            .Where(estado => estado.Code == Codigo)
            .Select(estado => (int?)estado.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
