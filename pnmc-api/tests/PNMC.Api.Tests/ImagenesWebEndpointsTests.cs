using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// LAS IMÁGENES ADMINISTRABLES, de punta a punta.
/// <para>
/// Lo que estas pruebas defienden, en una frase: que el sitio no sirva nunca algo que no sea una
/// imagen, que retirar devuelva la portada de fábrica en vez de dejar un hueco, y que publicar
/// sea un permiso más estrecho que editar.
/// </para>
/// <para>
/// <b>ADVERTENCIA DE CARRIL.</b> Estas corren sobre SQLite fabricada desde el modelo de EF
/// (<c>TestWebApplicationFactory</c>), donde <b>ninguno</b> de los diecisiete CHECK de
/// <c>V20260829_01__medios_web.sql</c> existe. Lo que la base impone se prueba aparte, en
/// <see cref="ImagenesWebEsquemaSqlServerTests"/>, con <c>PNMC_PRUEBAS_SQLSERVER=1</c>. Sin esa otra clase,
/// esta suite estaría verde sobre restricciones que nunca se ejercitaron.
/// </para>
/// </summary>
public sealed class ImagenesWebEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public ImagenesWebEndpointsTests(TestWebApplicationFactory factory) => this.factory = factory;

    // Un PNG real de 64 × 40, producido con Pillow. Mismo fichero que en
    // MediosWebContratoTests: si el reconocedor lo aceptara allí y no aquí, sería el arnés y no
    // el código lo que estaría fallando.
    private const string PngReal =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAAAoCAIAAADBrGu+AAAAU0lEQVR4nO3RgQkAMQCDwBTkZ/7xO4YUPLJA8Gw/28v79jQ6IKMC" +
        "MiogowIyKiCjAjIqIKMCMiogowIyKiCjAjIqIKMCMiogowIyKiCjAjIqMNcFqA4C/LjkybcAAAAASUVORK5CYII=";

    // Un WebP real de 64 × 40. Distinto archivo, distinta huella: es lo que hace medible el ETag.
    private const string WebpReal =
        "UklGRtoAAABXRUJQVlA4IM4AAABwBwCdASpAACgAPmkmkEWxIiGb/HQBEAaEswDCANhE8iVfkcGu53BPWHQtwuKGJxo4YPR6GpEA" +
        "r3E1TX7F10z1IQxTgAD+/aR29YoEpxsGqc//LErWKYv9a3Kv6y06hvv0VNfV8O00Q9PN2/gW3stPQHINRyQves5e4vXZN+Ea3ib6" +
        "P4+1ytx+LnxU225S1FGU9ZAgK64eZ4VqmaCJAgZIwew1EKPmJtIzlTO9srrw4IOkVsKu3Zs/wSbH4skFLxSWN8Scghj9iHr9BpYA" +
        "AA==";

    private static byte[] Png => Convert.FromBase64String(PngReal);

    private static byte[] Webp => Convert.FromBase64String(WebpReal);

    /// <summary>
    /// Una clave de trabajo distinta por prueba. El arnés comparte una sola base entre todas las
    /// de esta clase, así que dos pruebas sobre la misma ranura se pisarían y el fallo dependería
    /// del orden en que xunit las ejecute.
    /// </summary>
    private const string ClaveHome1 = "home_hero_1";
    private const string ClaveHome2 = "home_hero_2";
    private const string ClaveHome3 = "home_hero_3";
    private const string ClaveHome4 = "home_hero_4";
    private const string ClaveEje1 = "eje_01_media";
    private const string ClaveEje2 = "eje_02_media";
    private const string ClaveEje3 = "eje_03_media";
    private const string ClaveHeroEjes = "hero_ejes";
    private const string ClaveHeroEco = "hero_ecosistema";
    private const string ClaveNoEditable = "marca_gov_co";

    // =========================================================================================
    // Utilidades
    // =========================================================================================

    private static MultipartFormDataContent Subida(
        byte[] archivo,
        int version,
        string nombre = "portada.png",
        string? tipoDeclarado = "image/png",
        bool publicar = false,
        byte[]? miniatura = null,
        string? alt = null)
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(archivo);
        if (tipoDeclarado is not null)
        {
            parte.Headers.ContentType = new MediaTypeHeaderValue(tipoDeclarado);
        }

        contenido.Add(parte, "file", nombre);
        contenido.Add(new StringContent(version.ToString(CultureInfo.InvariantCulture)), "version");
        contenido.Add(new StringContent(publicar ? "true" : "false"), "publish");

        if (miniatura is not null)
        {
            var parteMiniatura = new ByteArrayContent(miniatura);
            parteMiniatura.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
            contenido.Add(parteMiniatura, "thumbnail", "mini.webp");
        }

        if (alt is not null)
        {
            contenido.Add(new StringContent(alt), "alt");
        }

        return contenido;
    }

    private async Task<int> VersionDeAsync(string clave)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.ImagenesWeb.Where(x => x.Key == clave).Select(x => x.Version).FirstAsync();
    }

    /// <summary>Deja esa clave sin borrador, para que la prueba no dependa de quién corrió antes.</summary>
    /// <remarks>
    /// <b>LAS VEINTE PRUEBAS DE ESTA CLASE COMPARTEN UNA SOLA BASE</b> —`IClassFixture` monta una
    /// fábrica por clase— y xUnit no garantiza en qué orden las ejecuta. Varias escriben un borrador
    /// sobre `ClaveHome1`; las dos que comprueban que una subida RECHAZADA no toca la base afirmaban
    /// que el borrador es nulo, y eso solo era cierto si ninguna de las otras había corrido antes.
    /// Pasaban por suerte:, al renombrar la clase, cambió el orden que
    /// xUnit deriva del nombre del tipo y las dos empezaron a fallar sin que el producto cambiara
    /// una línea.
    ///
    /// Con esto la afirmación dice lo que quiere decir: <b>después de un rechazo no hay borrador
    /// PORQUE no lo escribió esta petición</b>, y no porque nadie hubiera pasado por aquí.
    /// </remarks>
    private async Task SinBorradorAsync(string clave)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ImagenesWeb.FirstAsync(x => x.Key == clave);
        fila.DraftContent = null;
        await db.SaveChangesAsync();
    }

    private async Task<ImagenWebRow> FilaDeAsync(string clave)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.ImagenesWeb.AsNoTracking().FirstAsync(x => x.Key == clave);
    }

    private static async Task<JsonElement> CuerpoAsync(HttpResponseMessage respuesta)
        => JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

    // =========================================================================================
    // La siembra
    // =========================================================================================

    /// <summary>
    /// SEMBRAR NO ES PUBLICAR. Es la prueba que garantiza que el día que esto entró el sitio se
    /// veía exactamente igual: las 44 ranuras existen, ninguna tiene bytes publicados, y
    /// el manifiesto público sale vacío.
    /// <para>
    /// <b>FÁBRICA PROPIA, y no es un capricho.</b> Con la del <c>IClassFixture</c> esta prueba
    /// pasaba o fallaba según el orden en que xunit ejecutara la clase: las demás publican
    /// imágenes sobre esa misma base, y «no hay nada publicado» solo es cierto justo después de
    /// sembrar. Se falló primero por eso. Una base recién sembrada es la única sobre la que la
    /// afirmación significa algo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task SembrarNoPublicaNadaYElSitioSigueConSusImagenesCompiladas()
    {
        await using var fabricaFresca = new TestWebApplicationFactory();
        using var scope = fabricaFresca.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var ranuras = await db.ImagenesWeb.AsNoTracking()
            .Select(x => new { x.Key, x.Editable, TienePublicado = x.PublishedContent != null, TieneBorrador = x.DraftContent != null })
            .ToListAsync();

        // El numero importa por si mismo: son TODAS las 44 ranuras vigentes del registro,
        // medidas al recorrer el sitio con el navegador.
        //
        // La propiedad que esta prueba defiende NO ha cambiado con ellas: sembrar no publica.
        // Las 44 nacen con las catorce columnas de blob en NULL, el manifiesto publico sale
        // vacio y el sitio se ve igual que el dia antes.
        Assert.Equal(44, ranuras.Count);
        Assert.DoesNotContain(ranuras, r => r.TienePublicado);
        // Ni siquiera borrador: las catorce columnas de blob nacen en NULL.
        Assert.DoesNotContain(ranuras, r => r.TieneBorrador);

        // Las dos marcas institucionales ajenas nacen no editables.
        Assert.Equal(2, ranuras.Count(r => !r.Editable));

        // Y el manifiesto que el front pide al arrancar sale vacío, así que ninguna <img> del
        // sitio apunta al API.
        var anonimo = fabricaFresca.CreateClient();
        var manifiesto = await CuerpoAsync(await anonimo.GetAsync("/api/v1/imagenes-web"));
        Assert.Equal(0, manifiesto.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ElCatalogoDeclaraCuarentaYCuatroRanurasYNingunaChocaConUnaClaveDeTexto()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var clavesDeImagen = await db.ImagenesWeb.AsNoTracking().Select(x => x.Key).ToListAsync();
        var clavesDeTexto = await db.ContenidoWeb.AsNoTracking().Select(x => x.Key).ToListAsync();

        Assert.Equal(44, clavesDeImagen.Count);
        // Si una clave existiera en los dos catálogos, la puerta de claves huérfanas —que busca
        // por subcadena— daría por buena la de imagen porque la de texto sí tiene lector.
        Assert.Empty(clavesDeImagen.Intersect(clavesDeTexto, StringComparer.Ordinal));
    }

    // =========================================================================================
    // Lo que no entra
    // =========================================================================================

    /// <summary>
    /// Un ejecutable de Windows renombrado a <c>.png</c> y declarado como <c>image/png</c>. Es el
    /// caso que separa «miro lo que dice el cliente» de «miro los bytes».
    /// </summary>
    [Fact]
    public async Task UnEjecutableRenombradoSeRechazaYNoTocaLaBase()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveHome1);

        var pe = new byte[512];
        pe[0] = 0x4D;
        pe[1] = 0x5A;
        pe[2] = 0x90;

        await SinBorradorAsync(ClaveHome1);

        var respuesta = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveHome1}", Subida(pe, version, "logo.png"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var fila = await FilaDeAsync(ClaveHome1);
        Assert.Null(fila.DraftContent);
        Assert.Equal(version, fila.Version);
    }

    [Fact]
    public async Task UnSvgSeRechaza()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveHome1);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"></svg>");

        var respuesta = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome1}",
            Subida(svg, version, "marca.svg", "image/svg+xml"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// El cliente declara <c>text/html</c> y sube un WebP de verdad. Se rechaza: un cliente que
    /// miente sobre el tipo es una señal, no un descuido que haya que corregirle en silencio.
    /// </summary>
    [Fact]
    public async Task UnTipoDeclaradoQueNoCoincideConElContenidoSeRechaza()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveHome1);

        var respuesta = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome1}",
            Subida(Webp, version, "portada.webp", "text/html"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// LO QUE SE SIRVE ES EL TIPO QUE DIJO EL RECONOCEDOR. Se sube un WebP con el nombre y la
    /// extensión de un PNG; el sitio debe servir <c>image/webp</c>.
    /// </summary>
    [Fact]
    public async Task ElTipoServidoSaleDelReconocedorYNoDelNombreDelArchivo()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveHome2);

        var subida = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome2}",
            Subida(Webp, version, "esto-parece-un-png.png", tipoDeclarado: null, publicar: true));
        subida.EnsureSuccessStatusCode();

        var publico = factory.CreateClient();
        var servida = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHome2}");

        Assert.Equal(HttpStatusCode.OK, servida.StatusCode);
        Assert.Equal("image/webp", servida.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Webp, await servida.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// La miniatura pasa por el mismo reconocedor. Sin esto sería el hueco por el que entra un
    /// archivo cualquiera mientras el principal se mira con lupa.
    /// </summary>
    [Fact]
    public async Task UnaMiniaturaQueNoEsWebpSeRechaza()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveHome1);

        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(parte, "file", "portada.png");
        contenido.Add(new StringContent(version.ToString(CultureInfo.InvariantCulture)), "version");
        contenido.Add(new StringContent("false"), "publish");

        // La miniatura dice ser WebP y es un PNG.
        var mini = new ByteArrayContent(Png);
        mini.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
        contenido.Add(mini, "thumbnail", "mini.webp");

        await SinBorradorAsync(ClaveHome1);

        var respuesta = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveHome1}", contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null((await FilaDeAsync(ClaveHome1)).DraftContent);
    }

    // =========================================================================================
    // El ciclo
    // =========================================================================================

    [Fact]
    public async Task GuardarBorradorNoSacaLaImagenAlSitio()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var version = await VersionDeAsync(ClaveEje1);

        var respuesta = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveEje1}", Subida(Png, version));
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await CuerpoAsync(respuesta);
        Assert.True(cuerpo.GetProperty("changed").GetBoolean());
        Assert.Equal("no_publicado", cuerpo.GetProperty("state").GetString());

        var publico = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveEje1}")).StatusCode);
    }

    /// <summary>
    /// Subir dos veces el mismo archivo no es un cambio. Sin esta comprobación, cada guardado
    /// gastaría una entrada de historial —de hasta 2 MiB— y subiría la versión, invalidando la
    /// caché de todos los visitantes por nada.
    /// </summary>
    [Fact]
    public async Task SubirElMismoArchivoDosVecesNoCuentaComoCambio()
    {
        var client = await CmsTestClient.LoginAsync(factory);

        var primera = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveEje2}", Subida(Png, await VersionDeAsync(ClaveEje2)));
        primera.EnsureSuccessStatusCode();
        var versionTrasLaPrimera = await VersionDeAsync(ClaveEje2);

        var segunda = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveEje2}", Subida(Png, versionTrasLaPrimera));
        segunda.EnsureSuccessStatusCode();

        Assert.False((await CuerpoAsync(segunda)).GetProperty("changed").GetBoolean());
        Assert.Equal(versionTrasLaPrimera, await VersionDeAsync(ClaveEje2));
    }

    /// <summary>
    /// RETIRAR QUITA DEL SITIO Y CONSERVA EL BORRADOR. El visitante vuelve a ver la imagen
    /// compilada; la editora sigue teniendo la suya sobre la mesa.
    /// </summary>
    [Fact]
    public async Task RetirarQuitaDelSitioYConservaElBorrador()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var subida = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome3}",
            Subida(Png, await VersionDeAsync(ClaveHome3), publicar: true));
        subida.EnsureSuccessStatusCode();

        var publico = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHome3}")).StatusCode);

        var retiro = await client.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome3}/retire",
            new ImagenesWebEndpointsRetirePayload("ya no aplica"));
        retiro.EnsureSuccessStatusCode();
        Assert.Equal("retirado", (await CuerpoAsync(retiro)).GetProperty("state").GetString());

        // El visitante: 404, así que el front cae en la imagen compilada.
        Assert.Equal(HttpStatusCode.NotFound, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHome3}")).StatusCode);

        // La editora: su borrador sigue ahí, byte a byte.
        var borrador = await client.GetAsync($"/api/v1/admin/imagenes-web/{ClaveHome3}/preview/borrador");
        Assert.Equal(HttpStatusCode.OK, borrador.StatusCode);
        Assert.Equal(Png, await borrador.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// PUBLICAR NO RESUCITA UNA CLAVE RETIRADA. Es el mismo defecto que
    /// <c>ContenidoWebEndpoints.cs</c> ya cierra para los textos: si subir con
    /// <c>publish=true</c> republicara, el botón «retirar» sería reversible por accidente.
    /// </summary>
    [Fact]
    public async Task PublicarNoResucitaUnaClaveRetirada()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var publico = factory.CreateClient();

        var primera = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome4}",
            Subida(Png, await VersionDeAsync(ClaveHome4), publicar: true));
        primera.EnsureSuccessStatusCode();

        var retiro = await client.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome4}/retire",
            new ImagenesWebEndpointsRetirePayload(null));
        retiro.EnsureSuccessStatusCode();

        // Ahora se sube OTRO archivo con publish=true sobre la clave retirada.
        var segunda = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome4}",
            Subida(Webp, await VersionDeAsync(ClaveHome4), "otra.webp", "image/webp", publicar: true));
        segunda.EnsureSuccessStatusCode();

        // Sigue retirada y el sitio sigue sin verla.
        Assert.Equal("retirado", (await CuerpoAsync(segunda)).GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHome4}")).StatusCode);

        // Pero el borrador nuevo SÍ se guardó: la subida no se pierde.
        var fila = await FilaDeAsync(ClaveHome4);
        Assert.Equal(Webp, fila.DraftContent);
        Assert.Null(fila.PublishedContent);

        // Y republicar, que es la acción explícita, sí la devuelve al sitio.
        var republicacion = await client.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHome4}/republish",
            new ImagenesWebEndpointsRetirePayload(null));
        republicacion.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHome4}")).StatusCode);
    }

    // =========================================================================================
    // La caché
    // =========================================================================================

    /// <summary>
    /// EL ETAG LLEVA LA HUELLA Y LA VERSIÓN, y hacen falta las dos. La huella cambia si cambia el
    /// archivo; la versión cambia si se retira y se republica EL MISMO archivo. Cada mitad tiene
    /// su caso aquí.
    /// </summary>
    [Fact]
    public async Task ElEtagCambiaAlPublicarOtraImagenYTambienAlRepublicarLaMisma()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        var publico = factory.CreateClient();

        await (await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHeroEjes}",
            Subida(Png, await VersionDeAsync(ClaveHeroEjes), publicar: true))).EnsureSuccessStatusCodeAsync();

        var primera = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEjes}");
        var etagA = primera.Headers.ETag!.Tag;

        // Con ese ETag, la segunda petición no trae cuerpo.
        publico.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(etagA));
        Assert.Equal(HttpStatusCode.NotModified, (await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEjes}")).StatusCode);
        publico.DefaultRequestHeaders.IfNoneMatch.Clear();

        // MITAD 1 — otra imagen, otra huella, otro ETag.
        await (await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHeroEjes}",
            Subida(Webp, await VersionDeAsync(ClaveHeroEjes), "otra.webp", "image/webp", publicar: true))).EnsureSuccessStatusCodeAsync();

        var segunda = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEjes}");
        var etagB = segunda.Headers.ETag!.Tag;
        Assert.NotEqual(etagA, etagB);

        // El ETag viejo ya no vale: llega el archivo nuevo entero, no un 304.
        publico.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(etagA));
        var conElViejo = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEjes}");
        Assert.Equal(HttpStatusCode.OK, conElViejo.StatusCode);
        publico.DefaultRequestHeaders.IfNoneMatch.Clear();

        // MITAD 2 — retirar y republicar EL MISMO archivo. La huella no cambia; la versión sí.
        await (await client.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHeroEjes}/retire", new ImagenesWebEndpointsRetirePayload(null))).EnsureSuccessStatusCodeAsync();
        await (await client.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHeroEjes}/republish", new ImagenesWebEndpointsRetirePayload(null))).EnsureSuccessStatusCodeAsync();

        var tercera = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEjes}");
        Assert.Equal(Webp, await tercera.Content.ReadAsByteArrayAsync());
        Assert.NotEqual(etagB, tercera.Headers.ETag!.Tag);
    }

    /// <summary>
    /// Con <c>?v</c> la URL nombra un recurso que no puede cambiar, así que se puede cachear un
    /// año. Sin él se revalida. Es la diferencia entre que el sitio cargue las portadas en cada
    /// visita o en ninguna.
    /// </summary>
    [Fact]
    public async Task LaUrlConVersionSeCacheaUnAnoYLaDesnudaSeRevalida()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        await (await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveHeroEco}",
            Subida(Png, await VersionDeAsync(ClaveHeroEco), publicar: true))).EnsureSuccessStatusCodeAsync();

        var publico = factory.CreateClient();
        var version = await VersionDeAsync(ClaveHeroEco);

        var conVersion = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEco}?v={version}");
        Assert.Contains("immutable", conVersion.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

        var sinVersion = await publico.GetAsync($"/api/v1/imagenes-web/{ClaveHeroEco}");
        Assert.True(sinVersion.Headers.CacheControl!.NoCache);
    }

    // =========================================================================================
    // Quién puede qué
    // =========================================================================================

    /// <summary>
    /// PUBLICAR ES UN PERMISO MÁS ESTRECHO QUE EDITAR. <c>gestor_interno</c> sube borradores y no
    /// alcanza el sitio público; solo <c>webmaster</c> cambia lo que ve el visitante. Es la misma
    /// regla que el repositorio ya aplica al contenido editorial.
    /// </summary>
    [Fact]
    public async Task ElRolDePublicarEsMasEstrechoQueElDeEditar()
    {
        var gestor = await CmsTestClient.LoginAsync(factory, "gestor@pnmc.local", "pnmc-gestor");

        // Guardar borrador: sí.
        var borrador = await gestor.PostAsync($"/api/v1/admin/imagenes-web/{ClaveEje3}", Subida(Png, await VersionDeAsync(ClaveEje3)));
        Assert.Equal(HttpStatusCode.OK, borrador.StatusCode);

        // Publicar en la misma subida: no.
        var conPublicacion = await gestor.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveEje3}",
            Subida(Webp, await VersionDeAsync(ClaveEje3), "otra.webp", "image/webp", publicar: true));
        Assert.Equal(HttpStatusCode.Forbidden, conPublicacion.StatusCode);

        // Las tres acciones que cambian el sitio: tampoco.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.PostAsJsonAsync($"/api/v1/admin/imagenes-web/{ClaveEje3}/publish", new ImagenesWebEndpointsVersionPayload(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.PostAsJsonAsync($"/api/v1/admin/imagenes-web/{ClaveEje3}/retire", new ImagenesWebEndpointsRetirePayload(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.PostAsJsonAsync($"/api/v1/admin/imagenes-web/{ClaveEje3}/republish", new ImagenesWebEndpointsRetirePayload(null))).StatusCode);

        // Y el webmaster sí publica ese mismo borrador.
        var webmaster = await CmsTestClient.LoginAsync(factory);
        var publicacion = await webmaster.PostAsJsonAsync(
            $"/api/v1/admin/imagenes-web/{ClaveEje3}/publish", new ImagenesWebEndpointsVersionPayload(null));
        Assert.Equal(HttpStatusCode.OK, publicacion.StatusCode);
    }

    /// <summary>
    /// LA MARCA INSTITUCIONAL AJENA NO SE REEMPLAZA. 403 y no 400: no es que el archivo esté mal,
    /// es que esa ranura no se toca. La base lo impide además por CHECK.
    /// </summary>
    [Fact]
    public async Task UnaClaveNoEditableRechazaLaSubidaAunqueSeaWebmaster()
    {
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsync(
            $"/api/v1/admin/imagenes-web/{ClaveNoEditable}",
            Subida(Png, await VersionDeAsync(ClaveNoEditable)));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Null((await FilaDeAsync(ClaveNoEditable)).DraftContent);
    }

    /// <summary>
    /// LA AFIRMACIÓN ES SOBRE LA CLAVE DEL ERROR, NO SOBRE EL 400. Es lo que hace medible el
    /// <c>.DisableAntiforgery()</c> del endpoint: quitarlo también daría 400 —o reventaría por la
    /// ausencia de <c>UseAntiforgery()</c>— pero sin la clave <c>antiforgery</c> del CMS. Afirmar
    /// solo el código de estado no habría medido nada.
    /// </summary>
    [Fact]
    public async Task LaSubidaSinTokenAntiforgeryDevuelveElErrorDelCms()
    {
        var client = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await client.PostAsync($"/api/v1/admin/imagenes-web/{ClaveHome1}", Subida(Png, 1));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await CuerpoAsync(respuesta);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("antiforgery", out _),
            "el 400 no vino de GuardasDelCms: llegó sin la clave 'antiforgery'");
    }

    [Fact]
    public async Task LasDosRutasPublicasNoPidenSesion()
    {
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/imagenes-web")).StatusCode);
        // Una clave que existe y no está publicada: 404, no 401.
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/v1/imagenes-web/{ClaveNoEditable}")).StatusCode);
    }

    /// <summary>
    /// Una clave inexistente y una existente sin publicar responden IGUAL. Distinguirlas
    /// convertiría esta ruta anónima en un comprobador de qué claves hay en el catálogo.
    /// </summary>
    [Fact]
    public async Task LaRutaPublicaNoDelataQueClavesExisten()
    {
        var anonimo = factory.CreateClient();

        var inexistente = await anonimo.GetAsync("/api/v1/imagenes-web/esta_clave_no_existe");
        var sinPublicar = await anonimo.GetAsync($"/api/v1/imagenes-web/{ClaveNoEditable}");

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sinPublicar.StatusCode);
        Assert.Equal(
            await inexistente.Content.ReadAsStringAsync(),
            await sinPublicar.Content.ReadAsStringAsync());
    }

    // =========================================================================================
    // El panel
    // =========================================================================================

    /// <summary>
    /// El listado del panel devuelve las cuatro ranuras y sus topes, y no lleva bytes en el
    /// cuerpo.
    /// <para>
    /// <b>Lo que esta prueba NO mide, y hay que decirlo:</b> si la CONSULTA lee los blobs. Un
    /// mutante que añadía la columna a la proyección de EF y no la serializaba sobrevivía a esta
    /// prueba entera, porque el tamaño de la respuesta no cambia. Esa propiedad —la que de verdad
    /// cuesta 64 MiB por clic— la mide <see cref="ImagenesWebSinBlobsEnLaConsultaTests"/>, que lee el
    /// SQL emitido.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ElListadoDelPanelDevuelveLasRanurasYSusTopes()
    {
        var client = await CmsTestClient.LoginAsync(factory);

        foreach (var clave in new[] { ClaveHome1, ClaveHome2, ClaveHome3, ClaveHome4 })
        {
            var fila = await FilaDeAsync(clave);
            if (fila.DraftContent is null && fila.Editable)
            {
                await client.PostAsync($"/api/v1/admin/imagenes-web/{clave}", Subida(Png, fila.Version, publicar: true));
            }
        }

        var respuesta = await client.GetAsync("/api/v1/admin/imagenes-web/groups/home_media");
        respuesta.EnsureSuccessStatusCode();
        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(4, (await CuerpoAsync(respuesta)).GetProperty("images").GetArrayLength());
        Assert.True(texto.Length < 8 * 1024, $"el listado del grupo pesó {texto.Length} bytes; debería no llevar los blobs");

        // Y trae los topes, para que el panel no los lleve escritos a mano. Es la corrección que
        // ya hubo que hacer en la nómina: la editora veía «cabe» y el servidor devolvía 400.
        var limites = JsonDocument.Parse(texto).RootElement.GetProperty("limits");
        Assert.Equal(MediosWebContrato.MaxBytes, limites.GetProperty("maxBytes").GetInt32());
        Assert.Equal(MediosWebContrato.MaxThumbnailBytes, limites.GetProperty("maxThumbnailBytes").GetInt32());
    }

    /// <summary>
    /// El historial se poda al mismo tope que el de los textos. Aquí el tope cuesta dinero de
    /// verdad: cada entrada guarda el archivo, hasta 2 MiB.
    /// </summary>
    [Fact]
    public async Task ElHistorialDeImagenesSePodaAlMismoTopeQueElDeTextos()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        const string clave = "estrategia_celebra_media";

        // Nueve subidas con contenido distinto cada vez. Se varía un píxel del PNG y se recalcula
        // nada: basta con alternar entre los dos archivos reales y añadir bytes de comentario.
        for (var i = 0; i < 9; i++)
        {
            var archivo = ConPngVariado(i);
            var respuesta = await client.PostAsync(
                $"/api/v1/admin/imagenes-web/{clave}",
                Subida(archivo, await VersionDeAsync(clave)));
            respuesta.EnsureSuccessStatusCode();
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var entradas = await db.HistorialDeImagenesWeb.CountAsync(x => x.Key == clave);

        Assert.Equal(PodaDelHistorialContenidoWeb.Tope, entradas);
    }

    /// <summary>
    /// Un PNG real con un chunk <c>tEXt</c> añadido al final: sigue siendo el mismo PNG válido
    /// para el reconocedor —que solo lee la cabecera— y tiene una huella distinta en cada
    /// iteración, que es lo que hace que las nueve subidas cuenten como nueve cambios.
    /// </summary>
    private static byte[] ConPngVariado(int i)
    {
        var original = Png;
        var relleno = Encoding.ASCII.GetBytes($"pnmc-variacion-{i:D4}");
        var salida = new byte[original.Length + relleno.Length];
        Array.Copy(original, salida, original.Length);
        Array.Copy(relleno, 0, salida, original.Length, relleno.Length);
        return salida;
    }
}

/// <summary>Cuerpos de petición. Fuera de la clase para que xunit no los tome por datos de teoría.</summary>
public sealed record ImagenesWebEndpointsRetirePayload(string? Reason);

public sealed record ImagenesWebEndpointsVersionPayload(int? Version);

internal static class RespuestaExtensiones
{
    /// <summary>
    /// Como <c>EnsureSuccessStatusCode</c>, pero el mensaje incluye el cuerpo. Sin esto, un fallo
    /// de preparación se lee como «Response status code does not indicate success: 400» y hay que
    /// depurar a ciegas cuál de las cuatro validaciones saltó.
    /// </summary>
    public static async Task EnsureSuccessStatusCodeAsync(this HttpResponseMessage respuesta)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"{(int)respuesta.StatusCode} {respuesta.StatusCode}: {cuerpo}");
    }
}
