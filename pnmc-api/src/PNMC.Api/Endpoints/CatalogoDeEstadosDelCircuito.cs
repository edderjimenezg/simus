using Microsoft.EntityFrameworkCore;
using PNMC.Api.ConsultaGuiada;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los estados que recorre un Festival, leídos del catálogo y no escritos en ninguna parte.
/// </summary>
/// <remarks>
/// <para>
/// <b>VIVE AQUI Y NO DENTRO DE UN ENDPOINT PORQUE LOS DOS ESPACIOS PREGUNTAN LO MISMO.</b> La
/// consola pregunta «¿cuántos festivales hay en revisión?» y una organización pregunta «¿cómo van
/// mis procesos?»: si cada uno construyera su vocabulario, bastaría con que uno de los dos olvidara
/// un estado para que la misma palabra significara cosas distintas según desde dónde se pregunte.
/// </para>
/// <para>
/// <b>SE LIMITA AL CIRCUITO DE UN FESTIVAL</b> más «archivado», que es su salida.
/// <c>EstadosContenido</c> comparte tabla con las organizaciones y las suscripciones, así que
/// contiene «Registrada», «Activa» o «Pendiente de confirmación». Aceptar «Registrada» habría hecho
/// que «¿cuántos festivales hay registrados?» —que quiere decir «en todos los estados»— se acotara
/// al estado de una entidad.
/// </para>
/// </remarks>
public static class CatalogoDeEstadosDelCircuito
{
    /// <summary>El vocabulario de estados con el que se leen las preguntas.</summary>
    public static async Task<VocabularioDeEstados> ObtenerAsync(PnmcDbContext db, CancellationToken ct)
    {
        var delFestival = EstadosFestival.DelCircuito
            .Append(EstadosFestival.Archivado)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filas = await db.ContentStatuses.AsNoTracking()
            .Select(item => new { item.Code, item.Name })
            .ToListAsync(ct);

        return VocabularioDeEstados.Construir(filas
            .Where(fila => delFestival.Contains(fila.Code))
            .Select(fila => new VocabularioDeEstados.FilaDeEstado(fila.Code, fila.Name)));
    }
}
