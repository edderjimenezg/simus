using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Lo que ve quien decide: la ficha de <c>GET /institucional/festivales/{id}</c>.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO QUE ESTAS PRUEBAS FIJAN. La ficha devolvía siete campos y el historial. No devolvía
/// las prácticas musicales, ni los territorios sonoros, ni las ediciones, ni el nombre del
/// departamento y el municipio —solo sus códigos DIVIPOLA—. Un funcionario que la abriera veía
/// <c>05001</c> donde la organización escribió «Medellín», y no veía en absoluto los dos catálogos
/// que esa organización diligenció ni los años que declaró haber hecho.
/// </para>
/// <para>
/// Y NO LA LLAMABA NADIE: ningún componente Angular pedía esta ruta. La bandeja pintaba los cuatro
/// campos de la LISTA —nombre, organización, cobertura y fecha— y sobre eso se publicaba o se
/// rechazaba. El criterio es este: «todos los datos que se diligenciaron,
/// las versiones años y poder ver toda la info».
/// </para>
/// <para>
/// POR QUÉ LAS LISTAS VACÍAS TIENEN SU PROPIA PRUEBA. Un Festival sin catálogos ni ediciones es el
/// caso corriente al principio del trámite. Si esos campos viajaran como <c>null</c> en vez de como
/// lista vacía, el <c>&#64;for</c> de la plantilla no fallaría en la pantalla del que tiene datos
/// —fallaría justo en la del que no los tiene—, que es la mitad de la bandeja.
/// </para>
/// </remarks>
public sealed class FichaDeRevisionInstitucionalTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Clave = "ClaveExterna123";

    private readonly TestWebApplicationFactory _factory;

    public FichaDeRevisionInstitucionalTests(TestWebApplicationFactory factory) => _factory = factory;

    private sealed record Catalogos(int PracticaId, int TerritorioId, string CodigoDepartamento, string CodigoMunicipio);

    /// <summary>
    /// Siembra una práctica, un territorio y una fila de DIVIPOLA.
    /// </summary>
    /// <remarks>
    /// DIVIPOLA NO ES OPCIONAL PARA ESTA PRUEBA: el alta del Festival valida el municipio contra
    /// esa tabla y responde «El municipio indicado no existe en DIVIPOLA». Sembrarla es lo que
    /// permite pedir cobertura municipal, que es el único caso donde hay dos códigos que traducir.
    /// </remarks>
    private Catalogos SembrarCatalogos(string marca)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var practica = new PracticaMusicalRow { Nombre = "Músicas de banda " + marca };
        var territorio = new TerritorioSonoroRow { Nombre = "Andes " + marca };
        db.PracticasMusicales.Add(practica);
        db.TerritoriosSonoros.Add(territorio);
        // UNA SOLA VEZ PARA TODA LA CLASE. `IClassFixture` comparte la base entre las cuatro
        // pruebas y `UQ` sobre (departamento, municipio) rechaza la segunda inserción: sin esta
        // comprobación, tres de las cuatro fallaban por la siembra y ninguna llegaba a medir nada.
        if (!db.DivipolaLocations.Any(item => item.MunicipalityCode == "05001"))
        {
            db.DivipolaLocations.Add(new DivipolaLocationRow
            {
                DepartmentCode = "05",
                DepartmentName = "ANTIOQUIA",
                MunicipalityCode = "05001",
                MunicipalityName = "MEDELLIN",
            });
        }
        db.SaveChanges();

        return new Catalogos(practica.Id, territorio.Id, "05", "05001");
    }

    /// <summary>Una organización dada de alta por el camino real, con su Festival diligenciado.</summary>
    private async Task<(HttpClient Cliente, int FestivalId)> RegistrarFestivalAsync(string marca, string documento, Catalogos? catalogos)
    {
        var cliente = _factory.CreateClient();
        var correo = $"ficha.{marca}@example.com";

        var alta = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion " + marca,
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            FullName = "Persona " + marca,
            FirstName = "Persona",
            FirstSurname = marca,
            DocumentType = "CC",
            DocumentNumber = documento,
            NumeroDocumento = documento,
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var creada = await alta.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(creada);

        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = Clave,
        });
        entrada.EnsureSuccessStatusCode();

        var organizacionId = int.Parse(creada!.OrganizationId, CultureInfo.InvariantCulture);
        object cuerpo = catalogos is null
            ? new { nombre = "Festival " + marca, nivelCobertura = "nacional" }
            : new
            {
                nombre = "Festival " + marca,
                descripcion = "Doce días de músicas de banda",
                periodicidad = "anual",
                correoContacto = "contacto@" + marca + ".org",
                telefonoCelular = "3101234567",
                instagram = "https://instagram.com/" + marca,
                facebook = "https://facebook.com/" + marca,
                paginaWeb = "https://" + marca + ".org",
                otroEnlace = "https://youtube.com/@" + marca,
                observacionesContacto = "Atención de lunes a viernes",
                nivelCobertura = "municipal",
                codigoDepartamento = catalogos.CodigoDepartamento,
                codigoMunicipio = catalogos.CodigoMunicipio,
                practicasMusicalesIds = new[] { catalogos.PracticaId },
                territoriosSonorosIds = new[] { catalogos.TerritorioId },
            };

        var festival = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales", cuerpo);
        Assert.Equal(HttpStatusCode.Created, festival.StatusCode);

        using var documentoFestival = JsonDocument.Parse(await festival.Content.ReadAsStringAsync());
        var festivalId = int.Parse(documentoFestival.RootElement.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);

        // Estas pruebas describen la ficha institucional, no la transición de publicación.
        // Las ediciones solo pueden crearse para un Festival publicado; se deja ese preestado
        // explícito para que el fixture no contradiga la regla de negocio que debe conservarse.
        using (var alcance = _factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = db.FestivalRecords.Single(item => item.Id == festivalId);
            fila.StatusCode = "publicado";
            db.SaveChanges();
        }
        return (cliente, festivalId);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, HttpMethod metodo, string ruta, object cuerpo)
    {
        var testigo = await cliente.GetAsync(new Uri("/api/v1/externo/organizaciones/csrf", UriKind.Relative));
        testigo.EnsureSuccessStatusCode();
        var token = await testigo.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();

        var peticion = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("X-CSRF-TOKEN", token!.RequestToken);
        return await cliente.SendAsync(peticion);
    }

    private async Task<JsonDocument> FichaAsync(int festivalId)
    {
        var institucional = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var respuesta = await institucional.GetAsync(new Uri($"/api/v1/institucional/festivales/{festivalId}", UriKind.Relative));
        respuesta.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_Ficha_Trae_Los_Dos_Catalogos_Que_La_Organizacion_Marco()
    {
        var catalogos = SembrarCatalogos("cat");
        var (_, festivalId) = await RegistrarFestivalAsync("cat", "1200000001", catalogos);

        using var ficha = await FichaAsync(festivalId);

        // CON SU NOMBRE Y NO CON SU IDENTIFICADOR: quien revisa lee «Músicas de banda», no «7».
        var practicas = ficha.RootElement.GetProperty("practicasMusicales").EnumerateArray().ToList();
        var territorios = ficha.RootElement.GetProperty("territoriosSonoros").EnumerateArray().ToList();

        Assert.Single(practicas);
        Assert.Equal("Músicas de banda cat", practicas[0].GetProperty("nombre").GetString());
        Assert.Single(territorios);
        Assert.Equal("Andes cat", territorios[0].GetProperty("nombre").GetString());

        // La ficha compartida de revisión no puede completarse desde el resumen de la bandeja.
        // Estos son los datos que antes aparecían como «Sin registrar» aunque sí existieran.
        Assert.Equal("3101234567", ficha.RootElement.GetProperty("telefonoCelular").GetString());
        Assert.Equal("https://instagram.com/cat", ficha.RootElement.GetProperty("instagram").GetString());
        Assert.Equal("https://facebook.com/cat", ficha.RootElement.GetProperty("facebook").GetString());
        Assert.Equal("https://cat.org", ficha.RootElement.GetProperty("paginaWeb").GetString());
        Assert.Equal("https://youtube.com/@cat", ficha.RootElement.GetProperty("otroEnlace").GetString());
        Assert.Equal("Atención de lunes a viernes", ficha.RootElement.GetProperty("observacionesContacto").GetString());
    }

    [Fact]
    public async Task La_Ficha_Trae_Las_Ediciones_De_La_Mas_Reciente_A_La_Mas_Antigua()
    {
        var catalogos = SembrarCatalogos("edi");
        var (cliente, festivalId) = await RegistrarFestivalAsync("edi", "1200000002", catalogos);

        foreach (var anio in new[] { 2024, 2026, 2025 })
        {
            var alta = await EnviarAsync(cliente, HttpMethod.Post,
                $"/api/v1/externo/festivales/{festivalId}/ediciones",
                new { anio, nombre = $"Edición {anio}", estado = "en_preparacion" });
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        using var ficha = await FichaAsync(festivalId);
        var ediciones = ficha.RootElement.GetProperty("ediciones").EnumerateArray().ToList();

        // DE MÁS RECIENTE A MÁS ANTIGUA, y se siembran desordenadas a propósito: con el orden de
        // inserción la prueba pasaría igual sin ningún `OrderByDescending`.
        Assert.Equal([2026, 2025, 2024], ediciones.Select(item => item.GetProperty("anio").GetInt32()).ToArray());
        Assert.Equal("Edición 2026", ediciones[0].GetProperty("nombre").GetString());
        // El rótulo viaja resuelto: el vocabulario lo impone `CK_EdicionesFestival_Estado` y
        // traducirlo en la plantilla sería una segunda copia de esa lista.
        Assert.Equal("En preparación", ediciones[0].GetProperty("estadoEtiqueta").GetString());
    }

    [Fact]
    public async Task La_Ficha_Traduce_El_Codigo_Divipola_Y_Rotula_La_Cobertura()
    {
        var catalogos = SembrarCatalogos("terr");
        var (_, festivalId) = await RegistrarFestivalAsync("terr", "1200000003", catalogos);

        using var ficha = await FichaAsync(festivalId);

        // El código sigue viajando —es el dato— y el nombre viaja al lado, que es lo que se lee.
        Assert.Equal("05", ficha.RootElement.GetProperty("codigoDepartamento").GetString());
        Assert.Equal("05001", ficha.RootElement.GetProperty("codigoMunicipio").GetString());
        Assert.Equal("Antioquia", ficha.RootElement.GetProperty("departamentoNombre").GetString());
        Assert.Equal("Medellin", ficha.RootElement.GetProperty("municipioNombre").GetString());
        Assert.Equal("Municipal", ficha.RootElement.GetProperty("nivelCoberturaEtiqueta").GetString());
    }

    [Fact]
    public async Task Un_Festival_Sin_Catalogos_Ni_Ediciones_Trae_Listas_Vacias_Y_No_Nulos()
    {
        var (_, festivalId) = await RegistrarFestivalAsync("vacio", "1200000004", catalogos: null);

        using var ficha = await FichaAsync(festivalId);

        // Es el caso corriente al principio del trámite. Con `null` el `@for` de la plantilla
        // fallaría justo en la ficha del que todavía no ha llenado nada.
        Assert.Equal(JsonValueKind.Array, ficha.RootElement.GetProperty("practicasMusicales").ValueKind);
        Assert.Empty(ficha.RootElement.GetProperty("practicasMusicales").EnumerateArray());
        Assert.Empty(ficha.RootElement.GetProperty("territoriosSonoros").EnumerateArray());
        Assert.Empty(ficha.RootElement.GetProperty("ediciones").EnumerateArray());
        // Cobertura nacional: `CK_Festivales_NivelCobertura` prohíbe que lleve territorio, así que
        // aquí no hay código que traducir y el nombre tiene que ser nulo, no una cadena inventada.
        Assert.Equal(JsonValueKind.Null, ficha.RootElement.GetProperty("departamentoNombre").ValueKind);
        Assert.Equal("Nacional", ficha.RootElement.GetProperty("nivelCoberturaEtiqueta").GetString());
    }
}
