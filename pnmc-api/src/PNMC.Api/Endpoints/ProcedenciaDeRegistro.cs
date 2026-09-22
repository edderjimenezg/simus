using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Anota y resuelve de dónde vino cada registro del sistema.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUÉ EXISTE.</b> El sistema sabía quién RESPONDE por un registro
/// (<c>OrganizacionResponsableId</c>) y QUÉ PASÓ con él (<c>dbo.BitacoraAuditoria</c>). No sabía
/// DE DÓNDE VINO: si lo incorporó una organización desde su espacio o un funcionario desde el
/// Panel de Gestión Administrativa.
/// </para>
/// <para>
/// <b>TRES DIMENSIONES QUE NO SE MEZCLAN.</b> Procedencia (qué entidad lo aportó), usuario (qué
/// cuenta ejecutó la acción) y organización responsable (quién gestiona el proceso). Un
/// funcionario del PNMC puede registrar el festival de la Fundación X: la procedencia es el PNMC,
/// la responsable sigue siendo la Fundación X.
/// </para>
/// <para>
/// <b>SE ANOTA UNA VEZ, AL NACER.</b> Reescribirla después la convertiría en «de dónde está» en
/// vez de «de dónde vino», que es justo lo que ya cuenta la organización responsable.
/// </para>
/// <para>
/// <b>NO SUSTITUYE A LA AUDITORÍA.</b> La bitácora cuenta la historia completa y puede depurarse;
/// esto es un hecho fijo por registro que se consulta en cada listado. Deducirlo de la bitácora
/// obligaría a una consulta por fila en cada tabla que se pinte.
/// </para>
/// </remarks>
public static class ProcedenciaDeRegistro
{
    // ---------- Contextos ----------------------------------------------------------------

    /// <summary>Lo incorporó un funcionario desde el Panel de Gestión Administrativa.</summary>
    public const string Administrativo = "administrativo";

    /// <summary>Lo incorporó una organización desde su propio espacio, con su cuenta.</summary>
    public const string Externo = "externo";

    /// <summary>
    /// Entró por una carga asistida de archivo.
    /// </summary>
    /// <remarks>
    /// NO ES NINGUNO DE LOS DOS ANTERIORES: la ejecuta un funcionario, pero los datos vienen de
    /// una fuente de fuera. Distinguirlo es lo que permite responder después «esto lo escribió
    /// alguien» frente a «esto llegó en un archivo».
    /// </remarks>
    public const string Importacion = "importacion";

    /// <summary>Lo creó el arranque del sistema. No hay una persona detrás.</summary>
    public const string Siembra = "siembra";

    /// <summary>
    /// Venía del acervo anterior al sistema y no consta quién lo cargó.
    /// </summary>
    /// <remarks>
    /// NO ES `administrativo`. Un Festival del volcado histórico de SIMUS no lo incorporó el
    /// Programa desde su consola: llamarlo institucional le atribuiría un acto que no consta que
    /// hiciera, y se perdería la única información útil que queda —que es anterior al sistema—.
    /// </remarks>
    public const string Historico = "historico";

    /// <summary>
    /// Se creó verificando el sistema.
    /// </summary>
    /// <remarks>
    /// EXISTE PARA NO MEZCLAR LA PRUEBA CON EL ACERVO. Sin este contexto, marcar de golpe todo lo
    /// huérfano como histórico convertiría un festival de prueba en patrimonio documental.
    /// </remarks>
    public const string Prueba = "prueba";

    public static readonly string[] Contextos =
        [Administrativo, Externo, Importacion, Siembra, Historico, Prueba];

    // ---------- Modulos -------------------------------------------------------------------

    /// <summary>
    /// Los módulos que anotan procedencia.
    /// </summary>
    /// <remarks>
    /// <b>YA NO SE DECLARAN AQUI.</b> Hasta esta clase tenía sus propias
    /// constantes, en singular —<c>festival</c>, <c>organizacion</c>, <c>noticia</c>— mientras el
    /// resto del sistema decía <c>festivales</c>, <c>organizaciones</c>, <c>noticias</c>. El efecto
    /// era que la procedencia de un registro y su historial de revisión no se podían cruzar: el
    /// mismo Festival se llamaba de dos maneras y la consulta devolvía cero filas. Ahora el
    /// vocabulario es uno solo y vive en <see cref="Modulos"/>.
    /// </remarks>
    public static readonly string[] Dominios = Modulos.Todos;

    private static readonly Dictionary<string, string> EtiquetasDeContexto = new(StringComparer.Ordinal)
    {
        [Administrativo] = "Registro institucional",
        [Externo] = "Registro de la organización",
        [Importacion] = "Carga asistida",
        [Siembra] = "Siembra del sistema",
        [Historico] = "Acervo histórico",
        [Prueba] = "Dato de prueba",
    };

    // ---------- Escritura -----------------------------------------------------------------

    /// <summary>
    /// Deja anotada la procedencia de un registro recién creado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SE AÑADE AL CONTEXTO Y NO GUARDA: quien llama está dentro de la transacción que crea el
    /// registro, y la procedencia tiene que nacer o morir con él. Guardar aquí dejaría una
    /// procedencia huérfana si el alta se deshace después.
    /// </para>
    /// <para>
    /// <b>NO PISA UNA PROCEDENCIA EXISTENTE.</b> Llamarla dos veces sobre el mismo registro es un
    /// defecto, y el índice único de la base lo rechazaría con un error ilegible; aquí simplemente
    /// no se anota la segunda vez, que es lo que significa «de dónde vino».
    /// </para>
    /// </remarks>
    public static async Task AnotarAsync(
        PnmcDbContext db,
        string dominio,
        string registroId,
        string contexto,
        int? organizacionProcedenciaId,
        int? usuarioCreadorId,
        CancellationToken ct)
    {
        if (!Array.Exists(Dominios, d => d == dominio))
        {
            throw new ArgumentException($"Módulo de procedencia desconocido: «{dominio}».", nameof(dominio));
        }
        if (!Array.Exists(Contextos, c => c == contexto))
        {
            throw new ArgumentException($"Contexto de procedencia desconocido: «{contexto}».", nameof(contexto));
        }

        var yaExiste = await db.ProcedenciasDeRegistro.AsNoTracking()
            .AnyAsync(x => x.ModuloId == dominio && x.RegistroId == registroId, ct);
        if (yaExiste) return;

        db.ProcedenciasDeRegistro.Add(new ProcedenciaDeRegistroRow
        {
            ModuloId = dominio,
            RegistroId = registroId,
            ContextoOrigen = contexto,
            OrganizacionProcedenciaId = organizacionProcedenciaId,
            UsuarioCreadorId = usuarioCreadorId,
            FechaRegistro = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Anota un alta hecha desde el Panel de Gestión Administrativa.
    /// </summary>
    /// <remarks>
    /// LA ENTIDAD INSTITUCIONAL SE RESUELVE AQUÍ Y NO LA PASA QUIEN LLAMA. Es siempre la misma
    /// —la fila marcada <c>EsInstitucional</c>, única por índice filtrado— y pedírsela a cada
    /// manejador daría tantas formas de equivocarse como manejadores.
    /// </remarks>
    public static async Task AnotarAltaInstitucionalAsync(
        PnmcDbContext db, string dominio, string registroId, int usuarioId, CancellationToken ct)
    {
        await AnotarAsync(db, dominio, registroId, Administrativo, await IdInstitucionalAsync(db, ct), usuarioId, ct);
    }

    /// <summary>El identificador de la entidad institucional, o <c>null</c> si aún no existe.</summary>
    /// <remarks>
    /// DEVOLVER NULO EN VEZ DE FALLAR: en una base a medio sembrar la entidad puede no existir
    /// todavía, y un cero o un uno inventados apuntarían a cualquier otra organización.
    /// </remarks>
    public static Task<int?> IdInstitucionalAsync(PnmcDbContext db, CancellationToken ct) =>
        db.EntityProfiles.AsNoTracking()
            .Where(x => x.IsInstitutional)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    // ---------- Lectura -------------------------------------------------------------------

    /// <summary>
    /// La procedencia de un registro, con los nombres ya resueltos.
    /// </summary>
    public static async Task<ProcedenciaDeRegistroDto?> LeerAsync(
        PnmcDbContext db, string dominio, string registroId, CancellationToken ct)
    {
        var mapa = await LeerVariasAsync(db, dominio, [registroId], ct);
        return mapa.TryGetValue(registroId, out var procedencia) ? procedencia : null;
    }

    /// <summary>
    /// La procedencia de una página entera de registros, en dos consultas.
    /// </summary>
    /// <remarks>
    /// DOS CONSULTAS Y NO DOS POR FILA: es el mismo criterio con el que ya se resuelven las
    /// etiquetas, las categorías y los territorios de la Agenda. Con veinte filas en pantalla, la
    /// alternativa son cuarenta viajes a la base para pintar una tabla.
    /// </remarks>
    public static async Task<Dictionary<string, ProcedenciaDeRegistroDto>> LeerVariasAsync(
        PnmcDbContext db, string dominio, IReadOnlyList<string> registros, CancellationToken ct)
    {
        if (registros.Count == 0) return [];

        var ids = registros.Distinct(StringComparer.Ordinal).ToList();
        var filas = await db.ProcedenciasDeRegistro.AsNoTracking()
            .Where(x => x.ModuloId == dominio && ids.Contains(x.RegistroId))
            .ToListAsync(ct);

        if (filas.Count == 0) return [];

        var organizaciones = filas.Select(f => f.OrganizacionProcedenciaId).Where(x => x.HasValue)
            .Select(x => x!.Value).Distinct().ToList();
        var usuarios = filas.Select(f => f.UsuarioCreadorId).Where(x => x.HasValue)
            .Select(x => x!.Value).Distinct().ToList();

        var nombresDeOrganizacion = organizaciones.Count == 0
            ? []
            : await db.EntityProfiles.AsNoTracking()
                .Where(e => organizaciones.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var nombresDeUsuario = usuarios.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(u => usuarios.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return filas.ToDictionary(
            f => f.RegistroId,
            f => new ProcedenciaDeRegistroDto(
                f.ContextoOrigen,
                EtiquetasDeContexto.TryGetValue(f.ContextoOrigen, out var etiqueta) ? etiqueta : f.ContextoOrigen,
                f.ContextoOrigen is Administrativo or Importacion,
                f.OrganizacionProcedenciaId,
                f.OrganizacionProcedenciaId is { } organizacion && nombresDeOrganizacion.TryGetValue(organizacion, out var nombreOrganizacion)
                    ? nombreOrganizacion : null,
                f.UsuarioCreadorId,
                f.UsuarioCreadorId is { } usuario && nombresDeUsuario.TryGetValue(usuario, out var nombreUsuario)
                    ? nombreUsuario : null,
                f.FechaRegistro),
            StringComparer.Ordinal);
    }
}
