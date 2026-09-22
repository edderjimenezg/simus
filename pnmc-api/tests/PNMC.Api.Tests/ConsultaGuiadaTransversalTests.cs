using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.ConsultaGuiada;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La Consulta Guiada como capacidad transversal: un núcleo, dos ámbitos, contexto declarado.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que una organización llegue a una consulta institucional;
/// que una respuesta calculada sobre toda la operación se presente como si fuera del Festival que
/// la persona tiene delante; y que una sugerencia escrita en pantalla devuelva «no encontré una
/// consulta», que es la forma más rápida de que nadie vuelva a usar el asistente.
/// </para>
/// </remarks>
public sealed class ConsultaGuiadaTransversalTests
{
    private const string Clave = "ClaveExterna123";

    /// <summary>
    /// Lo que una sugerencia nombra, sin vocabulario territorial.
    /// </summary>
    /// <remarks>
    /// SIN DIVIPOLA A PROPOSITO: una sugerencia que solo se resolviera nombrando un departamento
    /// sería una sugerencia que el propio asistente no sabe contestar tal como la ofrece escrita.
    /// El año sí se lee, que no necesita ninguna tabla.
    /// </remarks>
    private static EntidadesDeLaPregunta EntidadesDe(string pregunta) =>
        ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)),
            VocabularioTerritorial.Vacio,
            DateTime.UtcNow.Year);

    // ---------- El núcleo compartido -----------------------------------------------------------

    [Theory]
    [InlineData(AmbitoDeConsulta.Institucional)]
    [InlineData(AmbitoDeConsulta.Organizacion)]
    public async Task Cada_sugerencia_la_resuelve_el_interprete(AmbitoDeConsulta ambito)
    {
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(ambito);

        foreach (var sugerencia in CatalogoDeConsultas.SugerenciasDe(ambito))
        {
            var elegida = await interprete.InterpretarAsync(
                sugerencia, disponibles, EntidadesDe(sugerencia), CancellationToken.None);

            // UNA SUGERENCIA QUE NO SE RESUELVE ES PEOR QUE NO OFRECERLA: quien la pulsa concluye
            // —con razón— que el asistente no sabe responder ni sus propios ejemplos.
            Assert.True(elegida is not null, $"La sugerencia «{sugerencia}» no la resuelve ninguna consulta de {ambito}.");
        }
    }

    [Fact]
    public async Task El_interprete_solo_puede_elegir_lo_que_se_le_ofrece()
    {
        var interprete = new InterpreteDeterministico();

        // «pendientes» existe en los dos ámbitos con consultas distintas. Dándole solo las de la
        // organización, no hay forma de que devuelva la institucional: es lo que hace seguro
        // enchufar mañana un modelo en esta misma costura.
        var elegida = await interprete.InterpretarAsync(
            "¿qué está pendiente?",
            CatalogoDeConsultas.De(AmbitoDeConsulta.Organizacion),
            EntidadesDeLaPregunta.Ninguna,
            CancellationToken.None);

        Assert.NotNull(elegida);
        Assert.Equal(CatalogoDeConsultas.MisPendientes, elegida.Consulta.Id);
    }

    [Fact]
    public void Ningun_identificador_del_catalogo_se_repite_dentro_de_su_ambito()
    {
        foreach (var ambito in new[] { AmbitoDeConsulta.Institucional, AmbitoDeConsulta.Organizacion })
        {
            var identificadores = CatalogoDeConsultas.De(ambito).Select(item => item.Id).ToList();

            // EL IDENTIFICADOR ES EL CONTRATO: viaja como `consultaElegida` y es lo que permite
            // comprobar después qué se respondió. Repetido, la segunda entrada es inalcanzable.
            Assert.Equal(identificadores.Count, identificadores.Distinct(StringComparer.Ordinal).Count());
        }
    }

    // ---------- El contexto ---------------------------------------------------------------------

    [Fact]
    public async Task Sin_contexto_la_respuesta_declara_que_es_de_toda_la_operacion()
    {
        await using var factory = new TestWebApplicationFactory();
        using var consola = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await Preguntar(consola, "/api/v1/admin/analisis/consultar", "¿cómo está la plataforma hoy?");

        Assert.Equal("Toda la operación", respuesta.GetProperty("contexto").GetString());
    }

    [Fact]
    public async Task Desde_un_Festival_la_calidad_habla_de_ese_Festival()
    {
        await using var factory = new TestWebApplicationFactory();
        var festivalId = await UnFestivalAsync(factory);
        using var consola = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await Preguntar(
            consola, "/api/v1/admin/analisis/consultar", "¿qué información está incompleta?",
            new { festivalId });

        // LA MISMA PREGUNTA ACOTADA, NO OTRA CONSULTA. Obligar a formularla distinto según dónde
        // esté la persona sería pedirle que sepa dónde está parada.
        Assert.Contains("el Festival", respuesta.GetProperty("contexto").GetString()!, StringComparison.Ordinal);
        Assert.Equal("Calidad de datos del Festival", respuesta.GetProperty("consulta").GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task Una_consulta_que_no_se_acota_lo_dice_en_vez_de_fingir()
    {
        await using var factory = new TestWebApplicationFactory();
        var festivalId = await UnFestivalAsync(factory);
        using var consola = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await Preguntar(
            consola, "/api/v1/admin/analisis/consultar", "¿qué cobertura territorial tienen los Festivales?",
            new { festivalId });

        // DEVOLVER LA CIFRA GLOBAL SIN AVISAR LA HACE PARECER DE ESE FESTIVAL. El contrato obliga a
        // declararlo y la pantalla lo enseña junto a la tabla.
        var contexto = respuesta.GetProperty("contexto").GetString()!;
        Assert.StartsWith("Toda la operación", contexto, StringComparison.Ordinal);
        Assert.Contains("no se acota", contexto, StringComparison.Ordinal);
    }

    // ---------- El ámbito de la organización ----------------------------------------------------

    [Fact]
    public async Task La_organizacion_ve_su_catalogo_y_no_el_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "consulta.propia@example.com");

        var estado = await cliente.GetFromJsonAsync<EstadoDeConsultaGuiadaDto>(
            $"/api/v1/externo/organizaciones/{organizacionId}/consulta-guiada/estado",
            CancellationToken.None);

        Assert.NotNull(estado);
        Assert.Contains(estado.Consultas, item => item.Id == CatalogoDeConsultas.MisPendientes);
        // LAS BANDEJAS DEL PROGRAMA NO SON SUYAS. Ni siquiera anunciadas: un rótulo que no se puede
        // pedir es una promesa rota esperando a que alguien la pulse.
        Assert.DoesNotContain(estado.Consultas, item => item.Id == CatalogoDeConsultas.Pendientes);
        Assert.DoesNotContain(estado.Consultas, item => item.Id == CatalogoDeConsultas.Cobertura);
    }

    [Fact]
    public async Task Preguntar_por_una_organizacion_ajena_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var (primera, _) = await OrganizacionAsync(factory, "primera.org@example.com");
        var (_, ajenaId) = await OrganizacionAsync(factory, "segunda.org@example.com");

        var respuesta = await primera.PostAsJsonAsync(
            $"/api/v1/externo/organizaciones/{ajenaId}/consulta-guiada/consultar",
            new { pregunta = "¿qué necesita mi atención?" },
            CancellationToken.None);

        // 403 Y NO UNA LISTA VACIA: «esa organización no tiene nada» también es información que no
        // le corresponde.
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_organizacion_pregunta_por_lo_suyo_y_la_respuesta_lo_dice()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "mis.procesos@example.com");
        await UnFestivalDeAsync(factory, organizacionId, "Festival de la organización que pregunta");

        var respuesta = await Preguntar(
            cliente, $"/api/v1/externo/organizaciones/{organizacionId}/consulta-guiada/consultar",
            "¿cómo van mis procesos?");

        Assert.Equal(CatalogoDeConsultas.MisProcesos, respuesta.GetProperty("consultaElegida").GetString());
        Assert.Contains("la organización", respuesta.GetProperty("contexto").GetString()!, StringComparison.Ordinal);
        // LA CIFRA MANDA SOBRE EL VERBO, también aquí. Esta prueba fijaba «Tienes 1 Festivales»,
        // que era el texto que devolvía de verdad: una organización con un solo Festival es el caso
        // corriente en el espacio externo, no el raro.
        Assert.Contains("Tienes 1 Festival registrado", respuesta.GetProperty("respuesta").GetString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("1 Festivales", respuesta.GetProperty("respuesta").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_Festival_del_contexto_tiene_que_ser_suyo()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "contexto.ajeno@example.com");
        await UnFestivalDeAsync(factory, organizacionId, "Festival propio");
        var ajeno = await UnFestivalAsync(factory);

        var respuesta = await Preguntar(
            cliente, $"/api/v1/externo/organizaciones/{organizacionId}/consulta-guiada/consultar",
            "¿qué información me falta?", new { festivalId = ajeno });

        // PASAR UN FESTIVAL AJENO NO ACOTA NADA Y TAMPOCO FILTRA: se ignora, y la respuesta sigue
        // siendo de la organización entera, que es lo que esa persona sí puede ver.
        Assert.Contains("la organización", respuesta.GetProperty("contexto").GetString()!, StringComparison.Ordinal);
        // LA TABLA LLEVA EL TIPO DELANTE DESDE QUE ESTA CONSULTA MIRA TAMBIÉN LOS MERCADOS: sin esa
        // columna, dos registros de módulos distintos se leían como si fueran lo mismo.
        var filas = respuesta.GetProperty("consulta").GetProperty("filas").EnumerateArray().ToList();
        Assert.Single(filas);
        Assert.Equal("Festival", filas[0][0].GetString());
        Assert.Equal("Festival propio", filas[0][1].GetString());
    }

    // ---------- Andamio -------------------------------------------------------------------------

    private static async Task<JsonElement> Preguntar(HttpClient cliente, string ruta, string pregunta, object? contexto = null)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            ruta, new { pregunta, contexto });
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Un Festival cualquiera de la base sembrada, para poder dar un contexto real.</summary>
    private static async Task<int> UnFestivalAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.FestivalRecords.Select(item => item.Id).FirstAsync();
    }

    private static async Task<int> UnFestivalDeAsync(TestWebApplicationFactory factory, int organizacionId, string nombre)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var plantilla = await db.FestivalRecords.AsNoTracking().FirstAsync();
        var festival = new PNMC.Domain.Entities.FestivalRow
        {
            Name = nombre,
            StatusCode = EstadosFestival.Borrador,
            CoverageLevel = plantilla.CoverageLevel,
            DepartmentCode = plantilla.DepartmentCode,
            MunicipalityCode = plantilla.MunicipalityCode,
            OrganizacionPrincipalId = organizacionId,
            CreatedAt = DateTime.UtcNow,
        };
        db.FestivalRecords.Add(festival);
        await db.SaveChangesAsync();
        return festival.Id;
    }

    private static async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(
        TestWebApplicationFactory factory, string correo)
    {
        var cliente = factory.CreateClient();
        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de " + correo,
            FullName = "Responsable de la organización",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave },
            CancellationToken.None)).EnsureSuccessStatusCode();

        var mias = await cliente.GetFromJsonAsync<JsonElement>(
            "/api/v1/externo/organizaciones/mis");
        return (cliente, int.Parse(mias[0].GetProperty("id").GetString()!, CultureInfo.InvariantCulture));
    }
}
