using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public sealed record FestivalPublicadoVigenteLectura(
    FestivalRow Festival,
    VersionFestivalRow? Version,
    string Nombre,
    string? Descripcion,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? CodigoMunicipio,
    string? Periodicidad,
    string? PeriodicidadDetalle,
    string? CorreoContacto,
    string? TelefonoContacto,
    string? Instagram,
    string? Facebook,
    string? SitioWeb,
    string? OtroEnlace,
    IReadOnlyList<CatalogoFestivalDto> PracticasMusicales,
    IReadOnlyList<CatalogoFestivalDto> TerritoriosSonoros,
    bool EsHistoricoSinVersion);

public static class LecturaFestivalesPublicados
{
    private const string EstadoPropuestaPublicada = "Publicada";

    /// <summary>
    /// Los Festivales tal como los ve el público, con su versión vigente resuelta.
    /// </summary>
    /// <param name="incluirAunqueNoEstePublicado">
    /// Un Festival que entra en la lectura sea cual sea su estado.
    /// </param>
    /// <remarks>
    /// <b>EL PARAMETRO EXISTE PARA PREVISUALIZAR, y por eso no lo usa ninguna ruta pública.</b> La
    /// consola necesita enseñar cómo se verá un Festival antes de publicarlo, y hacerlo con otra
    /// consulta —una que armara la ficha «parecida»— dejaría de parecerse en cuanto la lectura
    /// pública cambiara un campo, en silencio. Así la previsualización recorre <b>exactamente</b> el
    /// mismo camino: la misma resolución de versión vigente, el mismo retiro de propuestas sin
    /// aprobar, los mismos catálogos.
    /// <b>NO ABRE NINGUNA PUERTA:</b> quien decide si se puede pedir es la ruta, y la de
    /// previsualización exige sesión de consola.
    /// </remarks>
    public static async Task<List<FestivalPublicadoVigenteLectura>> ConsultarAsync(
        PnmcDbContext dbContext,
        CancellationToken cancellationToken,
        int? incluirAunqueNoEstePublicado = null)
    {
        // SIN ESTADO NO ES PUBLICO, Y ANTES LA CONDICION DECIA QUE SI.
        //
        // Empezaba por `item.StatusCode == null`: un Festival sin estado declarado salía en el
        // portal. Al escribir la prueba que lo fijaba se comprobó que ese caso NO SE PUEDE
        // CONSTRUIR EN NINGUNO DE LOS DOS MOTORES —la columna es NOT NULL tanto en SQL Server como
        // en el modelo de EF con el que la suite levanta SQLite—, así que la rama era código muerto
        // en todas partes. Lo que la hacía peor que inútil es lo que declaraba: que un registro
        // cuyo estado nadie decidió es un registro publicado, justo al revés de lo que hace el
        // resto del sistema —lo que entra por Importación Asistida nace en borrador—.
        //
        // `"Publicado"` con mayúscula se conserva: la base es insensible a mayúsculas y pudo
        // escribirse así antes de que `EstadosFestival` unificara el vocabulario; retirarlo
        // ocultaría de golpe las fichas que lo tengan.
        var festivales = await dbContext.FestivalRecords.AsNoTracking()
            .Where(item => item.StatusCode == "publicado" || item.StatusCode == "Publicado"
                || (incluirAunqueNoEstePublicado != null && item.Id == incluirAunqueNoEstePublicado))
            .ToListAsync(cancellationToken);
        var ids = festivales.Select(item => item.Id).ToArray();
        var versiones = ids.Length == 0 ? [] : await dbContext.VersionesFestival.AsNoTracking()
            .Where(item => ids.Contains(item.FestivalOrigenId) && item.EsVigente)
            .ToDictionaryAsync(item => item.FestivalOrigenId, cancellationToken);
        await RetirarVersionesSinAprobacionAsync(dbContext, versiones, cancellationToken);
        var idsVersiones = versiones.Values.Select(item => item.Id).ToArray();
        var practicasVersionadas = idsVersiones.Length == 0 ? [] : await dbContext
            .DeVarios<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, idsVersiones).AsNoTracking()
            .Join(dbContext.PracticasMusicales.AsNoTracking(), relacion => relacion.ValorId, practica => practica.Id,
                (relacion, practica) => new { relacion.RegistroId, practica.Id, practica.Nombre }).ToListAsync(cancellationToken);
        var territoriosVersionados = idsVersiones.Length == 0 ? [] : await dbContext
            .DeVarios<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, idsVersiones).AsNoTracking()
            .Join(dbContext.TerritoriosSonoros.AsNoTracking(), relacion => relacion.ValorId, territorio => territorio.Id,
                (relacion, territorio) => new { relacion.RegistroId, territorio.Id, territorio.Nombre }).ToListAsync(cancellationToken);
        var idsHistoricos = festivales.Where(item => !versiones.ContainsKey(item.Id)).Select(item => item.Id).ToArray();
        var practicasHistoricas = idsHistoricos.Length == 0 ? [] : await dbContext
            .DeVarios<PracticaMusicalDeRegistroRow>(Modulos.Festivales, idsHistoricos).AsNoTracking()
            .Join(dbContext.PracticasMusicales.AsNoTracking(), relacion => relacion.ValorId, practica => practica.Id,
                (relacion, practica) => new { relacion.RegistroId, practica.Id, practica.Nombre }).ToListAsync(cancellationToken);
        var territoriosHistoricos = idsHistoricos.Length == 0 ? [] : await dbContext
            .DeVarios<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, idsHistoricos).AsNoTracking()
            .Join(dbContext.TerritoriosSonoros.AsNoTracking(), relacion => relacion.ValorId, territorio => territorio.Id,
                (relacion, territorio) => new { relacion.RegistroId, territorio.Id, territorio.Nombre }).ToListAsync(cancellationToken);

        // PNMC-050. Antes, cada festival recorria las CUATRO listas enteras buscando las suyas
        // (un .Where por FestivalId dentro del Select de abajo). Eso es una union anidada en
        // memoria: con F festivales y N filas de catalogo costaba F x N comparaciones, y como N
        // crece con F, el coste del listado publico y del tablero de analitica crecia con el
        // CUADRADO del censo. Agrupar una vez deja el mismo resultado en F + N. No cambia ni una
        // linea de la respuesta: mismas filas y mismo orden.
        //
        // Se agrupa aqui y no en la base a proposito: las cuatro consultas ya vienen acotadas por
        // los ids en juego, y bajar la agrupacion a SQL costaria cuatro viajes mas.
        var practicasPorFestival = practicasHistoricas.ToLookup(
            item => Clasificaciones.Registro(item.RegistroId), item => new CatalogoFestivalDto(item.Id, item.Nombre));
        var territoriosPorFestival = territoriosHistoricos.ToLookup(
            item => Clasificaciones.Registro(item.RegistroId), item => new CatalogoFestivalDto(item.Id, item.Nombre));
        var practicasPorVersion = practicasVersionadas.ToLookup(
            item => Clasificaciones.Registro(item.RegistroId), item => new CatalogoFestivalDto(item.Id, item.Nombre));
        var territoriosPorVersion = territoriosVersionados.ToLookup(
            item => Clasificaciones.Registro(item.RegistroId), item => new CatalogoFestivalDto(item.Id, item.Nombre));

        return festivales.Select(festival =>
        {
            versiones.TryGetValue(festival.Id, out var version);
            // El indexador de ILookup devuelve una secuencia vacia cuando la clave no esta, que
            // es exactamente lo que devolvia el .Where anterior sin coincidencias.
            var practicas = version is null
                ? practicasPorFestival[festival.Id]
                : practicasPorVersion[version.Id];
            var territorios = version is null
                ? territoriosPorFestival[festival.Id]
                : territoriosPorVersion[version.Id];
            return new FestivalPublicadoVigenteLectura(
                festival, version, version?.Nombre ?? festival.Name, version?.Descripcion ?? festival.Description,
                version?.NivelCobertura ?? festival.CoverageLevel, version?.CodigoDepartamento ?? festival.DepartmentCode,
                version?.CodigoMunicipio ?? festival.MunicipalityCode, version?.Periodicidad ?? festival.Periodicidad,
                version?.PeriodicidadDetalle ?? festival.PeriodicidadDetalle,
                version?.CorreoContacto ?? festival.ContactEmail,
                // EL RESPALDO A LA CABECERA ES SOLO PARA EL FESTIVAL SIN VERSION, y por eso se
                // escribe con `version is null` y no con `??`. Un `??` habria dicho otra cosa: que
                // cuando la version deja un campo VACIO A PROPOSITO —porque la organizacion retiro
                // su telefono— la ficha vuelve a sacar el viejo de la cabecera. Retirar un dato es
                // una edicion como cualquier otra, y tiene que poder publicarse.
                version is null ? festival.ContactPhone : version.TelefonoContacto,
                version is null ? festival.InstagramUrl : version.Instagram,
                version is null ? festival.FacebookUrl : version.Facebook,
                version is null ? festival.WebsiteUrl : version.SitioWeb,
                version is null ? festival.OtherUrl : version.OtroEnlace,
                practicas.OrderBy(item => item.Nombre).ToList(), territorios.OrderBy(item => item.Nombre).ToList(), version is null);
        }).ToList();
    }

    /// <summary>
    /// PNMC-054. <c>EsVigente</c> no prueba por si sola que un revisor haya aprobado el contenido de esa
    /// version: la version nueva y el estado de la propuesta que la origina se escriben en pasos separados
    /// (RevisionInstitucionalPropuestasFestivalEndpoints.cs-109), de modo que puede quedar marcada como
    /// vigente una version cuya propuesta acabo en Rechazada, y la lectura publica la sirve como oficial.
    /// Aqui la lectura publica solo acepta una version si la propuesta que la produjo esta Publicada; si no
    /// lo esta, retrocede a la ultima version no repudiada, que es el estado que el rechazo debia dejar en
    /// pie, y si no queda ninguna cae al registro base del Festival.
    /// </summary>
    /// <summary>
    /// Las versiones que NO puede servir la lectura publica: las que nacieron de una propuesta que
    /// no acabo <c>Publicada</c>.
    /// </summary>
    /// <remarks>
    /// SE EXTRAJO A UN METODO PROPIO, al abrir la ruta publica de
    /// ediciones. Esa ruta necesita exactamente el mismo criterio que la ficha, y copiarlo habria
    /// dejado dos definiciones de «que es publico» que se separan en cuanto alguien toque una:
    /// la ficha dejaria de mostrar una version rechazada y la lista de ediciones seguiria
    /// ensenandola, que es la fuga de PNMC-054 por la puerta de al lado.
    /// </remarks>
    internal static async Task<HashSet<int>> VersionesRepudiadasAsync(
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // SOBRE LAS TABLAS GENERICAS desde. `SubregistroResultanteId`
        // es la versión que nació de aplicar la propuesta, y `aplicada` es lo que antes se llamaba
        // `Publicada`: una versión cuya propuesta no acabó así NO la sirve la lectura pública.
        var decisiones = await dbContext.PropuestasDeCambio.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.Festivales
                && item.SubregistroResultanteId != null
                && item.Estado != EstadosDePropuesta.Aplicada)
            .Select(item => item.SubregistroResultanteId!)
            .ToListAsync(cancellationToken);
        return decisiones
            .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static async Task RetirarVersionesSinAprobacionAsync(
        PnmcDbContext dbContext,
        Dictionary<int, VersionFestivalRow> versiones,
        CancellationToken cancellationToken)
    {
        if (versiones.Count == 0) return;

        var repudiadas = await VersionesRepudiadasAsync(dbContext, cancellationToken);
        if (repudiadas.Count == 0) return;

        var afectados = versiones.Where(par => repudiadas.Contains(par.Value.Id)).Select(par => par.Key).ToArray();
        if (afectados.Length == 0) return;

        var candidatas = await dbContext.VersionesFestival.AsNoTracking()
            .Where(item => afectados.Contains(item.FestivalOrigenId))
            .ToListAsync(cancellationToken);
        // PNMC-050, mismo cambio y mismo motivo que arriba: antes cada festival afectado
        // recorria la lista entera de candidatas. Se agrupa una vez.
        var candidatasPorFestival = candidatas.ToLookup(item => item.FestivalOrigenId);
        foreach (var festivalId in afectados)
        {
            var ultimaAprobada = candidatasPorFestival[festivalId]
                .Where(item => !repudiadas.Contains(item.Id))
                .OrderByDescending(item => item.NumeroVersion)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            if (ultimaAprobada is null) versiones.Remove(festivalId);
            else versiones[festivalId] = ultimaAprobada;
        }
    }
}
