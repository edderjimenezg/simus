using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Traslada las claves del CMS que cambiaron de nombre, antes de sembrar.
/// </summary>
/// <remarks>
/// <para>
/// La sección del portal que hoy es «Ecosistema» se llamó «SIMUS» mientras se
/// creyó que el Sistema de Información de la Música era un módulo propio. No lo
/// es: es una plataforma del Ministerio que vive en simus.mincultura.gov.co, y
/// el portal solo enlaza hacia ella. Al corregirlo, las 59 claves
/// <c>simus_*</c> del registro pasaron a <c>ecosistema_*</c>.
/// </para>
/// <para>
/// El sembrador nunca borra: si esto no existiera, cada base ya sembrada se
/// quedaría con las 59 filas viejas <b>y</b> las 59 nuevas, y el panel mostraría
/// una pestaña fantasma «SIMUS» con textos que ninguna página lee. Un editor no
/// tiene forma de saber cuál de las dos copias sale al sitio.
/// </para>
/// <para>
/// La regla de decisión es la que importa, y se resume en una frase: <b>lo que
/// escribió una persona se traslada; lo que sembró el sistema se descarta</b>.
/// Una fila intacta —sin publicar, versión 1, sembrada por «Sistema»— se borra
/// para que el sembrador la vuelva a crear con la copia nueva, que es la que ya
/// no presenta SIMUS como propio. Una fila que alguien tocó conserva su texto
/// bajo la clave nueva, junto con su historial: preferimos una frase vieja que
/// haya que corregir a mano antes que perder el trabajo de una editora.
/// </para>
/// <para>
/// Es idempotente y corre en cada arranque. Cuando no queda ninguna clave vieja
/// —el caso normal a partir del segundo arranque— no hace ni una consulta de
/// escritura.
/// </para>
/// </remarks>
public static class RenombradoContenidoWeb
{
    /// <summary>
    /// Prefijos renombrados, del viejo al nuevo.
    /// <para>
    /// Es una lista de prefijos y no de claves una por una porque el cambio fue
    /// exactamente eso: el mismo sufijo bajo otro nombre de sección. Enumerar
    /// 59 pares invitaría a que la número 60 se olvidara.
    /// </para>
    /// </summary>
    private static readonly (string Viejo, string Nuevo)[] Prefijos =
    [
        ("simus_", "ecosistema_"),
    ];

    /// <summary>
    /// Claves sueltas que cambiaron de nombre, una por una.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A diferencia de los prefijos, aquí el nombre viejo y el nuevo no
    /// comparten forma, así que hay que enumerarlos. Se reutiliza el mismo
    /// criterio: lo que escribió una persona se traslada, lo que sembró el
    /// sistema se descarta para que el sembrador lo rehaga.
    /// </para>
    /// <para>
    /// <b>nav_mapa → nav_ecosistema</b> (agosto de 2026): el enlace del menú
    /// que dice «Ecosistema» tenía el identificador del mapa, así que la clave
    /// que lo renombra se llamaba <c>nav_mapa</c>. Un editor que quisiera
    /// cambiar cómo se llama «Ecosistema» estaba renombrando otra cosa sin
    /// saberlo.
    /// </para>
    /// </remarks>
    private static readonly (string Viejo, string Nuevo)[] ClavesRenombradas =
    [
        ("nav_mapa", "nav_ecosistema"),
    ];

    /// <summary>
    /// Claves que dejaron de ser editables y hay que quitar del sitio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se enumeran una por una, y no se deduce «toda fila que no esté en el
    /// catálogo». La deducción es tentadora y peligrosa: si el catálogo
    /// incrustado se quedara desfasado —se toca el registro y se olvida
    /// <c>npm run cms:catalog</c>— una regla automática despublicaría media web
    /// en el arranque siguiente, en silencio. Una lista escrita a mano no puede
    /// equivocarse más allá de lo que dice.
    /// </para>
    /// <para>
    /// <b>home_btn_about / home_btn_ejes</b> (agosto de 2026): los dos botones
    /// del hero de la portada. Su rótulo volvió al código, junto al destino al
    /// que llevan, porque son navegación y no copia.
    /// </para>
    /// <para>
    /// <b>agenda_filter_fixed / agenda_filter_fixed_note</b> (Fase 4):
    /// describían un «filtro fijo» que la Agenda no tiene y nunca tuvo. Se
    /// quitaron del registro entonces, pero sus filas se quedaron en la base
    /// <b>y publicadas</b>, porque este paso todavía no existía. Son el caso que
    /// motivó escribirlo.
    /// </para>
    /// </remarks>
    private static readonly string[] ClavesRetiradas =
    [
        "home_btn_about",
        "home_btn_ejes",
        "agenda_filter_fixed",
        "agenda_filter_fixed_note",
        // La «introducción del geovisor». Su único uso era un parche que
        // sustituía la descripción del primer paso del tutorial; al declarar
        // los 14 campos propios del tutorial, el parche se quitó y la clave se
        // quedó sin lector. `/mapa` es un geovisor a pantalla completa y no
        // tiene dónde pintarla.
        "map_description",
    ];

    public static async Task EnsureRenamedAsync(
        PnmcDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var claveDelCatalogo = LeerCatalogo(logger);
        if (claveDelCatalogo is null)
        {
            return;
        }

        foreach (var (viejo, nuevo) in Prefijos)
        {
            await TrasladarPrefijoAsync(db, logger, claveDelCatalogo, viejo, nuevo, cancellationToken);
        }

        await TrasladarClavesSueltasAsync(db, logger, claveDelCatalogo, cancellationToken);
        await RetirarClavesAsync(db, logger, claveDelCatalogo, cancellationToken);
    }

    /// <summary>
    /// Traslada las claves sueltas de <see cref="ClavesRenombradas"/>.
    /// </summary>
    /// <remarks>
    /// Misma regla que el traslado por prefijo, y por el mismo motivo: una fila
    /// intacta se borra para que el sembrador la rehaga con la copia nueva; una
    /// que alguien tocó conserva su texto bajo la clave nueva, con su historial.
    /// Preferimos una frase vieja que haya que corregir a mano antes que perder
    /// el trabajo de una editora.
    /// </remarks>
    private static async Task TrasladarClavesSueltasAsync(
        PnmcDbContext db,
        ILogger logger,
        HashSet<string> claveDelCatalogo,
        CancellationToken cancellationToken)
    {
        var trasladadas = 0;
        var descartadas = 0;

        foreach (var (viejo, nuevo) in ClavesRenombradas)
        {
            if (!claveDelCatalogo.Contains(nuevo))
            {
                logger.LogWarning(
                    "La clave {Nuevo} no existe en el catálogo; {Viejo} se deja intacta.", nuevo, viejo);
                continue;
            }

            var fila = await db.ContenidoWeb.FirstOrDefaultAsync(f => f.Key == viejo, cancellationToken);
            if (fila is null)
            {
                continue;
            }

            if (await db.ContenidoWeb.AnyAsync(f => f.Key == nuevo, cancellationToken))
            {
                if (EsIntacta(fila))
                {
                    db.ContenidoWeb.Remove(fila);
                    descartadas++;
                    continue;
                }

                logger.LogWarning(
                    "La clave {Viejo} tiene texto editado pero {Nuevo} ya existe. Se deja para revisión manual.",
                    viejo, nuevo);
                continue;
            }

            if (EsIntacta(fila))
            {
                db.ContenidoWeb.Remove(fila);
                descartadas++;
                continue;
            }

            // El historial se busca por la clave VIEJA: hay que moverlo antes de
            // reescribir la fila, o la consulta devolvería lo del destino.
            var historial = await db.HistorialDeContenidoWeb
                .Where(entrada => entrada.Key == viejo)
                .ToListAsync(cancellationToken);
            foreach (var entrada in historial)
            {
                entrada.Key = nuevo;
            }

            fila.Key = nuevo;
            trasladadas++;
        }

        if (trasladadas == 0 && descartadas == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Claves sueltas renombradas: {Trasladadas} con texto propio, {Descartadas} intactas descartadas.",
                trasladadas, descartadas);
        }
    }

    /// <summary>
    /// Quita del sitio las claves que ya no son editables, sin destruir nada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retirar es exactamente lo que hace falta aquí, y por eso existe la
    /// columna <c>FechaRetiro</c>: el sitio deja de servir el texto —ninguna
    /// página lo lee ya— pero el borrador y el historial se quedan. Borrar la
    /// fila habría tirado el registro de auditoría de quién escribió qué, que
    /// es justo lo que un registro de auditoría no debe permitir.
    /// </para>
    /// <para>
    /// La guarda del catálogo hace que esto sea reversible: si alguien vuelve a
    /// declarar la clave en el registro, este paso se convierte en un no-op en
    /// vez de pelearse con el sembrador arranque tras arranque.
    /// </para>
    /// </remarks>
    private static async Task RetirarClavesAsync(
        PnmcDbContext db,
        ILogger logger,
        HashSet<string> claveDelCatalogo,
        CancellationToken cancellationToken)
    {
        var porRetirar = ClavesRetiradas.Where(clave => !claveDelCatalogo.Contains(clave)).ToArray();
        if (porRetirar.Length == 0)
        {
            return;
        }

        var filas = await db.ContenidoWeb
            .Where(fila => porRetirar.Contains(fila.Key) && fila.Published != null)
            .ToListAsync(cancellationToken);

        if (filas.Count == 0)
        {
            return;
        }

        var ahora = DateTime.UtcNow;
        foreach (var fila in filas)
        {
            fila.Published = null;
            fila.Retired = ahora;
            fila.Version++;
            fila.UpdatedAt = ahora;
            fila.UpdatedBy = "Sistema";
            db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
            {
                Key = fila.Key,
                Action = "retirado",
                // Nulo porque retirar no reescribe el borrador: lo quita del
                // sitio. El texto sigue donde estaba, por si hay que volver.
                Value = null,
                User = "Sistema",
                At = ahora,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Claves retiradas del sitio por dejar de ser editables: {Claves}.",
                string.Join(", ", filas.Select(fila => fila.Key)));
        }
    }

    private static HashSet<string>? LeerCatalogo(ILogger logger)
    {
        try
        {
            return SembradorDeContenidoWeb.LoadCatalog().Select(entrada => entrada.Key).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception)
        {
            // Sin catálogo no se puede saber qué clave nueva es legítima, y
            // renombrar a ciegas produciría filas que ninguna página lee.
            // Detenerse aquí deja la base como estaba, que es recuperable.
            logger.LogError(exception, "No fue posible leer el catálogo; el renombrado de claves se omitió.");
            return null;
        }
    }

    private static async Task TrasladarPrefijoAsync(
        PnmcDbContext db,
        ILogger logger,
        HashSet<string> claveDelCatalogo,
        string viejo,
        string nuevo,
        CancellationToken cancellationToken)
    {
        var pendientes = await db.ContenidoWeb
            .Where(fila => fila.Key.StartsWith(viejo))
            .ToListAsync(cancellationToken);

        if (pendientes.Count == 0)
        {
            return;
        }

        var yaExisten = await db.ContenidoWeb
            .Where(fila => fila.Key.StartsWith(nuevo))
            .Select(fila => fila.Key)
            .ToListAsync(cancellationToken);
        var destinosOcupados = yaExisten.ToHashSet(StringComparer.Ordinal);

        var trasladadas = 0;
        var descartadas = 0;
        var conflictos = 0;

        foreach (var fila in pendientes)
        {
            var destino = string.Concat(nuevo, fila.Key.AsSpan(viejo.Length));

            if (!claveDelCatalogo.Contains(destino))
            {
                // La clave vieja no tiene equivalente en el catálogo actual: no
                // es un renombrado, es una clave retirada. No se toca —borrarla
                // sería decidir por el equipo editorial— pero se avisa, porque
                // seguirá apareciendo en el panel sin que ninguna página la lea.
                logger.LogWarning(
                    "La clave {Clave} no tiene equivalente en el catálogo ({Destino} no existe). Se deja intacta.",
                    fila.Key, destino);
                conflictos++;
                continue;
            }

            if (destinosOcupados.Contains(destino))
            {
                if (EsIntacta(fila))
                {
                    db.ContenidoWeb.Remove(fila);
                    descartadas++;
                    continue;
                }

                logger.LogWarning(
                    "La clave {Clave} tiene texto editado pero {Destino} ya existe. Se deja intacta para revisión manual.",
                    fila.Key, destino);
                conflictos++;
                continue;
            }

            if (EsIntacta(fila))
            {
                // El sembrador la recreará con la copia nueva unos milisegundos
                // después. Trasladarla habría conservado el texto viejo —el que
                // presentaba SIMUS como propio— para siempre.
                db.ContenidoWeb.Remove(fila);
                descartadas++;
                continue;
            }

            // El historial se busca por la clave VIEJA, así que hay que
            // capturarla antes de reescribir la fila: consultar después
            // devolvería las entradas del destino, que aún no existe.
            var claveOriginal = fila.Key;
            var historial = await db.HistorialDeContenidoWeb
                .Where(entrada => entrada.Key == claveOriginal)
                .ToListAsync(cancellationToken);
            foreach (var entrada in historial)
            {
                entrada.Key = destino;
            }

            fila.Key = destino;
            destinosOcupados.Add(destino);
            trasladadas++;
        }

        if (trasladadas == 0 && descartadas == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Claves renombradas de {Viejo}* a {Nuevo}*: {Trasladadas} con texto propio trasladadas, "
                + "{Descartadas} intactas descartadas para resembrar, {Conflictos} sin resolver.",
                viejo, nuevo, trasladadas, descartadas, conflictos);
        }
    }

    /// <summary>
    /// Una fila tal como la dejó la siembra: nadie la publicó, nadie la guardó.
    /// </summary>
    /// <remarks>
    /// Las <b>cuatro</b> condiciones se piden juntas a propósito, y ninguna
    /// basta sola: <c>Published</c> nulo no descarta que alguien guardara un
    /// borrador sin publicarlo; <c>Retired</c> nulo no descarta que se publicara
    /// y se retirara; <c>Version</c> es el token de concurrencia, que el panel
    /// incrementa en cada guardado; y <c>UpdatedBy</c> distingue la siembra de
    /// una edición hecha por una persona.
    /// </remarks>
    private static bool EsIntacta(ContenidoWebRow fila) =>
        fila.Published is null
        && fila.Retired is null
        && fila.Version == 1
        && fila.UpdatedBy == "Sistema";
}
