using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class MapEndpoints
{
    private const string MonolithicTerritoriesAsset = "Departamentos-Municipos-COL.json";
    private const string DepartmentsAsset = "Departamentos-COL.simplified.topojson";
    private static readonly TimeSpan GeoAssetCacheDuration = TimeSpan.FromDays(1);

    public static RouteGroupBuilder MapMapEndpoints(this RouteGroupBuilder group)
    {
        // LAS RUTAS DEL MAPA, EN ESPAÑOL COMO EL RESTO DEL PROYECTO.
        //
        // Llegaron en inglés del desarrollo de referencia —`/map/topojson/departments`— y se
        // quedaron así mientras alrededor todo se nombraba en español: `publico/festivales`,
        // `publico/divipola`, `administracion/notificaciones`. La regla del proyecto es explícita
        // —nomenclatura funcional en español en lo que es nuestro— y una ruta pública es de lo más
        // visible que tiene un API.
        //
        // SE RENOMBRAN Y NO SE DUPLICAN. Un alias en inglés dejaría dos nombres vivos para lo mismo
        // y nadie sabría cuál es el bueno; el único consumidor es nuestro propio geovisor y viaja en
        // el mismo despliegue. Las dos excepciones están abajo, y son excepciones con motivo.
        var api = group.MapGroup("/mapa").WithTags("mapa");

        // ANONIMAS A PROPOSITO. Sirven fronteras administrativas publicas del DANE,
        // no datos del PNMC. La ruta `territories` conserva el fichero monolitico para
        // clientes anteriores; el geovisor actual usa departamentos simplificados y
        // solicita un fragmento municipal solo al abrir un departamento. Cerrarlas
        // deja el visor publico en blanco y no protege informacion de ninguna persona.
        api.MapGet("/cartografia/territorios", (HttpContext context, IWebHostEnvironment environment) =>
            ServeGeoAsset(context, environment, MonolithicTerritoriesAsset)).AllowAnonymous();

        // Rutas optimizadas del geovisor. La vista nacional solo necesita los 33
        // departamentos; los municipios se descargan por departamento cuando el
        // usuario hace drill-down. Esto evita enviar y expandir los 1.122
        // municipios (28 MB de TopoJSON, cientos de MB en memoria) al abrir el mapa.
        api.MapGet("/cartografia/departamentos", (HttpContext context, IWebHostEnvironment environment) =>
            ServeGeoAsset(context, environment, DepartmentsAsset)).AllowAnonymous();

        api.MapGet("/cartografia/departamentos/{codigoDepartamento}/municipios", (
            string codigoDepartamento,
            HttpContext context,
            IWebHostEnvironment environment) =>
        {
            var normalizedCode = NormalizeGeoAssetDepartmentCode(codigoDepartamento);
            return normalizedCode is null
                ? Results.NotFound()
                : ServeGeoAsset(context, environment, "municipalities", $"{normalizedCode}.topojson");
        }).AllowAnonymous();

        // POR QUE SIGUEN AQUI, AUNQUE EL FRONTEND NO LAS LLAME. La auditoría de rutas del 12 de
        // septiembre de 2026 las marcó como «sin consumidor», y estuvieron a punto de retirarse. No
        // se retiran, y conviene que quede escrito para no volver a abrirlo:
        //
        //  · `geojson/*` son el nombre anterior del mismo fichero que sirve `cartografia/territorios`,
        //    y se conservan A PROPOSITO como compatibilidad: `ApiIntegrationTests` lo comprueba por
        //    su nombre. Un consumidor externo que todavía pida el nombre viejo no se entera de que
        //    cambiamos de formato, que es justamente para lo que existe un alias.
        //  · `/resumen` y `/departamentos/{codigo}/detalle` no los llama nuestro geovisor —arma su
        //    resumen en el navegador— pero son API PUBLICA y son la superficie sobre la que seis
        //    pruebas de integración comprueban que la lectura pública usa solo la versión vigente de
        //    cada Festival. Retirarlas habría quitado esa comprobación a cambio de nada.
        //
        // LA LECCION, QUE ES LA QUE IMPORTA: «nadie la llama desde nuestro frontend» es evidencia,
        // no veredicto. Lo dice la cabecera de `tools/auditar-rutas.mjs` y aquí se confirmó.

        // Y SE QUEDAN EN INGLES A PROPOSITO, que es la otra mitad de la decisión de arriba: existen
        // para que un consumidor externo que pida el NOMBRE VIEJO siga encontrando el fichero.
        // Traducirlas las convertiría en un tercer nombre nuevo y dejaría de cumplir su única
        // función, que es responder al que ya estaba publicado.
        api.MapGet("/geojson/departments", (HttpContext context, IWebHostEnvironment environment) =>
            ServeGeoAsset(context, environment, MonolithicTerritoriesAsset)).AllowAnonymous();

        api.MapGet("/geojson/municipalities", (HttpContext context, IWebHostEnvironment environment) =>
            ServeGeoAsset(context, environment, MonolithicTerritoriesAsset)).AllowAnonymous();

        // ANONIMA A PROPOSITO (14 de 39). El conteo por departamento que colorea el mapa
        // del geovisor publico. Devuelve agregados de Festivales publicados por departamento,
        // nunca filas: ni un nombre, ni un correo,
        // ni una coordenada. Ademas cuenta solo festivales publicados, porque lee por
        // LecturaFestivalesPublicados.ConsultarAsync, de modo que un borrador no se
        // asoma en el numero.
        api.MapGet("/resumen", async (string? layer, PnmcDbContext dbContext, CacheDivipola divipola, CancellationToken cancellationToken) =>
        {
            var normalizedLayer = string.IsNullOrWhiteSpace(layer) ? "General" : layer;

            var festivals = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken);
            var (departments, _) = await divipola.ObtenerAsync(dbContext, cancellationToken);

            var summary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in festivals)
            {
                AddFestival(summary, item.CodigoDepartamento);
            }

            var items = summary
                .Select(kvp => new MapDepartmentSummaryDto(
                    ResolveDepartment(kvp.Key, departments),
                    kvp.Value,
                    kvp.Value))
                .OrderByDescending(item => item.Records)
                .ToList();

            return Results.Ok(new MapSummaryResponseDto(normalizedLayer, items));
        }).AllowAnonymous();

        // ANONIMA A PROPOSITO (15 de 39). El detalle que se abre al pinchar un
        // departamento en el geovisor: es la continuacion natural de /summary y sin ella
        // el mapa es un dibujo sin contenido. Lista nombre y municipio de Festivales,
        // sin correo,
        // telefono ni direccion. Los festivales vuelven a salir de
        // LecturaFestivalesPublicados, asi que un festival en revision no se filtra por
        // aqui.
        api.MapGet("/departamentos/{codigoDepartamento}/detalle", async (
            string codigoDepartamento,
            PnmcDbContext dbContext,
            CacheDivipola divipola,
            CancellationToken cancellationToken) =>
        {
            var targetCode = NormalizeDepartmentCode(codigoDepartamento);
            if (targetCode.Length == 0) return Results.NotFound();

            var festivals = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken);
            var (departments, municipalities) = await divipola.ObtenerAsync(dbContext, cancellationToken);

            var festivalItems = festivals
                .Where(item => NormalizeDepartmentCode(item.CodigoDepartamento) == targetCode)
                .Select(item => new FestivalDrilldownItemDto(
                    item.Festival.Id.ToString(CultureInfo.InvariantCulture), item.Nombre,
                    ResolveMunicipality(item.CodigoMunicipio, municipalities)))
                .ToList();

            var response = new DepartmentDrilldownResponseDto(
                ResolveDepartment(targetCode, departments),
                festivalItems);
            return Results.Ok(response);
        }).AllowAnonymous();

        return group;
    }

    private static void AddFestival(Dictionary<string, int> summary, string? departmentCode)
    {
        var normalizedDepartmentCode = NormalizeDepartmentCode(departmentCode);
        if (normalizedDepartmentCode.Length == 0) return;

        summary[normalizedDepartmentCode] = summary.TryGetValue(normalizedDepartmentCode, out var current)
            ? current + 1
            : 1;
    }

    private static string NormalizeDepartmentCode(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return string.Empty;
        return digits.PadLeft(2, '0')[^2..];
    }

    private static string? NormalizeGeoAssetDepartmentCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2 || value.Any(character => !char.IsDigit(character)))
        {
            return null;
        }

        return value.PadLeft(2, '0');
    }

    private static string ResolveDepartment(string? departmentCode, IReadOnlyDictionary<string, string> departments)
    {
        if (string.IsNullOrWhiteSpace(departmentCode)) return string.Empty;
        return departments.TryGetValue(departmentCode, out var name) ? name : departmentCode;
    }

    private static string ResolveMunicipality(string? municipalityCode, IReadOnlyDictionary<string, string> municipalities)
    {
        if (string.IsNullOrWhiteSpace(municipalityCode)) return string.Empty;
        return municipalities.TryGetValue(municipalityCode, out var name) ? name : municipalityCode;
    }

    /// <summary>
    /// Sirve la cartografia del DANE tal cual esta en disco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes esta ruta abria el fichero, lo parseaba entero a <c>JsonDocument</c>,
    /// clonaba la raiz y lo volvia a serializar en cada peticion. Son <b>28 MB</b>.
    /// El cliente recibia exactamente los mismos bytes que hay en disco, asi que
    /// todo ese trabajo —y las decenas de megas que asignaba por peticion— no
    /// producia ninguna diferencia observable. Con dos visitantes a la vez, la
    /// factura era de memoria y de recolector de basura, no solo de tiempo.
    /// </para>
    /// <para>
    /// Ahora se envia el fichero directamente, con su fecha y un ETag. Eso habilita
    /// la peticion condicional: el navegador que ya lo tiene manda
    /// <c>If-None-Match</c> y recibe un <b>304 sin cuerpo</b>. En el geovisor, que
    /// pide este fondo antes de dibujar nada, es la diferencia entre 28 MB y cero
    /// en cada visita despues de la primera.
    /// </para>
    /// <para>
    /// El ETag se deriva de tamaño y fecha de modificacion, no del contenido: es un
    /// asset de despliegue —no cambia sin un despliegue nuevo— y calcular un hash de
    /// 28 MB en cada peticion reintroduciria el problema que se acaba de quitar.
    /// </para>
    /// <para>
    /// Se pierde la validacion implicita que hacia el parseo. Es aceptable: un
    /// fichero corrupto seria un defecto de empaquetado, y parsearlo en cada
    /// peticion no protegia al cliente de nada, solo retrasaba el mismo error.
    /// </para>
    /// </remarks>
    private static IResult ServeGeoAsset(
        HttpContext context,
        IWebHostEnvironment environment,
        params string[] relativePathSegments)
    {
        var geoDirectory = Path.Combine(environment.ContentRootPath, "Assets", "geo");
        var geoJsonPath = Path.Combine([geoDirectory, .. relativePathSegments]);
        var info = new FileInfo(geoJsonPath);
        if (!info.Exists)
        {
            return Results.NotFound(new { message = $"Geo file '{Path.GetFileName(geoJsonPath)}' not found." });
        }

        context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = GeoAssetCacheDuration,
            MustRevalidate = true,
        };

        var etag = new EntityTagHeaderValue($"\"{info.Length:x}-{info.LastWriteTimeUtc.Ticks:x}\"");
        return Results.File(
            geoJsonPath,
            contentType: "application/json",
            lastModified: info.LastWriteTimeUtc,
            entityTag: etag);
    }
}
