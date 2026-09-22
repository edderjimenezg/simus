using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La propuesta de cambio de una organización sobre un mercado suyo YA PUBLICADO.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS CIERRAN.</b> Es el último circuito que le faltaba a Mercados para
/// funcionar como Festivales: lo publicado no se edita en caliente, se propone el cambio y el
/// Programa decide. Sin esto, una organización que quisiera corregir el correo de un mercado ya
/// publicado no tenía por dónde, y la única salida era pedirlo por fuera del sistema.
/// </para>
/// <para>
/// Se recorre entero: la organización escribe el borrador —que nadie más ve—, lo envía, el Programa
/// lo compara campo por campo y decide; y al aplicar, la ficha pública cambia.
/// </para>
/// </remarks>
public sealed class PropuestasDeCambioDeMercadoTests
{
    private const string Clave = "ClaveExterna123";

    private static async Task<(HttpClient Sesion, int OrganizacionId, string Csrf)> OrganizacionAsync(
        TestWebApplicationFactory factory, string nombre)
    {
        var cliente = factory.CreateClient();
        var correo = $"propuesta.{Guid.NewGuid():N}@organizacion.test";

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = nombre,
            FullName = "Persona responsable",
            NumeroDocumento = "1020304051",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        await CorreoConfirmadoEnPruebas.ConfirmarAsync(factory, correo);
        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest { Email = correo, Password = Clave }))
            .EnsureSuccessStatusCode();

        var testigo = await cliente.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/externo/organizaciones/csrf");
        var csrf = testigo.GetProperty("requestToken").GetString()!;
        var mias = await cliente.GetFromJsonAsync<List<OrganizacionAdministradaDto>>("/api/v1/externo/organizaciones/mis");
        return (cliente, int.Parse(mias![0].Id, CultureInfo.InvariantCulture), csrf);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string csrf, string ruta, object cuerpo)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient cliente, string csrf, string ruta, object cuerpo)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Put, ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient cliente, string csrf, string ruta)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Delete, ruta);
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    /// <summary>Un mercado de esa organización, ya publicado: el único estado desde el que se propone.</summary>
    private static async Task<int> MercadoPublicadoAsync(
        TestWebApplicationFactory factory, HttpClient cliente, HttpClient consola, int organizacion, string csrf)
    {
        int alcance, modalidad;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            alcance = await db.AlcancesMercado.OrderBy(a => a.OrdenVisualizacion).Select(a => a.Id).FirstAsync();
            modalidad = await db.ModalidadesMercado.OrderBy(m => m.OrdenVisualizacion).Select(m => m.Id).FirstAsync();
        }

        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado de la propuesta",
            Descripcion = "Encuentro de intercambio ya publicado.",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            CorreoMercado = "contacto@propuesta.test",
            AlcanceId = alcance,
            ModalidadId = modalidad,
        });
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        (await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { }))
            .EnsureSuccessStatusCode();
        (await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercado.Id}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" })).EnsureSuccessStatusCode();

        return mercado.Id;
    }

    private static GuardarPropuestaDeCambioSolicitud CambiaElCorreo(string correo) => new()
    {
        Motivo = "El correo anterior ya no lo lee nadie.",
        Campos = [new CampoPropuestoSolicitud { CampoId = "correoMercado", ValorPropuesto = correo }],
    };

    [Fact]
    public async Task El_circuito_entero_va_del_borrador_a_la_decision_y_cambia_la_ficha_publicada()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización de la propuesta");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        // 1. SIN PROPUESTA SE DEVUELVE UNA CARCASA VACIA, no un 404: la pantalla se pinta igual en
        //    los dos casos, y es el mismo criterio que la revisión por campos.
        var nada = await cliente.GetFromJsonAsync<PropuestaDeCambioDto>($"/api/v1/externo/mercados/{mercadoId}/propuesta");
        Assert.Equal(0, nada!.Id);
        Assert.Equal("borrador", nada.Estado);
        Assert.Empty(nada.Campos);

        // 2. EL BORRADOR ES DE LA ORGANIZACION, y el Programa todavía no lo ve.
        var guardada = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta",
            CambiaElCorreo("nuevo@propuesta.test"));
        guardada.EnsureSuccessStatusCode();
        var borrador = await guardada.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();
        Assert.Equal("borrador", borrador!.Estado);
        Assert.Single(borrador.Campos);
        Assert.Equal("Correo de contacto", borrador.Campos[0].CampoEtiqueta);
        // EL «ANTES» TODAVIA NO SE HA CONGELADO: se copia al enviar, no al escribir.
        Assert.Null(borrador.Campos[0].ValorAnterior);

        var invisible = await consola.GetFromJsonAsync<PropuestaDeCambioDto>($"/api/v1/institucional/mercados/{mercadoId}/propuesta");
        Assert.Equal(0, invisible!.Id);
        Assert.Empty(invisible.Campos);

        // 3. AL ENVIAR SE CONGELA CONTRA QUE SE COMPARA.
        var enviada = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta/enviar", new { });
        enviada.EnsureSuccessStatusCode();
        var enRevision = await enviada.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();
        Assert.Equal("en_revision", enRevision!.Estado);
        Assert.Equal("contacto@propuesta.test", enRevision.Campos[0].ValorAnterior);
        Assert.Equal("nuevo@propuesta.test", enRevision.Campos[0].ValorPropuesto);

        // 4. LA BANDEJA DEL PROGRAMA LA VE, CON CUANTOS CAMPOS CAMBIAN.
        var bandeja = await consola.GetFromJsonAsync<PropuestaEnRevisionDto[]>("/api/v1/institucional/mercados/propuestas/en-revision");
        var fila = Assert.Single(bandeja!);
        Assert.Equal("Mercado de la propuesta", fila.NombreDelRegistro);
        Assert.Equal(1, fila.CamposQueCambian);

        // 5. APLICAR CAMBIA LA FICHA PUBLICADA. Es el único de los tres desenlaces que toca el
        //    registro, y por eso es el que hay que comprobar en la ficha y no solo en el expediente.
        var decidida = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/propuestas/{enRevision.Id}/decision",
            new DecidirPropuestaDeCambioSolicitud { Decision = "aplicar" });
        decidida.EnsureSuccessStatusCode();
        Assert.Equal("aplicada", (await decidida.Content.ReadFromJsonAsync<PropuestaDeCambioDto>())!.Estado);

        // EL CORREO NO SALE A LA FICHA PUBLICA a propósito —solo los del pie institucional—, así
        // que lo que se comprueba es el registro publicado tal y como lo ve su organización.
        var registro = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercadoId}");
        Assert.Equal("nuevo@propuesta.test", registro!.CorreoMercado);
        Assert.Equal("publicado", registro.EstadoRegistro);

        // 6. Y LA BANDEJA QUEDA VACIA: lo decidido deja de esperar.
        var tras = await consola.GetFromJsonAsync<PropuestaEnRevisionDto[]>("/api/v1/institucional/mercados/propuestas/en-revision");
        Assert.Empty(tras!);
    }

    [Fact]
    public async Task Rechazar_sin_motivo_no_es_una_decision_y_el_registro_no_cambia()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del rechazo");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        (await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", CambiaElCorreo("otro@propuesta.test")))
            .EnsureSuccessStatusCode();
        var enviada = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta/enviar", new { });
        var propuesta = await enviada.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();

        var sinMotivo = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/propuestas/{propuesta!.Id}/decision",
            new DecidirPropuestaDeCambioSolicitud { Decision = "rechazar" });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var conMotivo = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/propuestas/{propuesta.Id}/decision",
            new DecidirPropuestaDeCambioSolicitud { Decision = "rechazar", Motivo = "Ese correo es de una persona, no del mercado." });
        conMotivo.EnsureSuccessStatusCode();
        var rechazada = await conMotivo.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();
        Assert.Equal("rechazada", rechazada!.Estado);
        Assert.Equal("Ese correo es de una persona, no del mercado.", rechazada.MotivoDeLaDecision);

        // RECHAZADA NO ES APLICADA: el registro sigue diciendo lo que decía.
        var registro = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercadoId}");
        Assert.Equal("contacto@propuesta.test", registro!.CorreoMercado);
    }

    [Fact]
    public async Task Pedir_ajustes_devuelve_la_propuesta_a_la_organizacion_sin_cerrarla()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización de los ajustes");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        (await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", CambiaElCorreo("dudoso@propuesta.test")))
            .EnsureSuccessStatusCode();
        var enviada = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta/enviar", new { });
        var propuesta = await enviada.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();

        var ajustes = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/propuestas/{propuesta!.Id}/decision",
            new DecidirPropuestaDeCambioSolicitud { Decision = "pedir_ajustes", Motivo = "Confirma que ese correo es institucional." });
        ajustes.EnsureSuccessStatusCode();
        Assert.Equal("ajustes_solicitados", (await ajustes.Content.ReadFromJsonAsync<PropuestaDeCambioDto>())!.Estado);

        // SIGUE VIVA Y VUELVE A SER EDITABLE: es lo que la distingue de un rechazo.
        var suya = await cliente.GetFromJsonAsync<PropuestaDeCambioDto>($"/api/v1/externo/mercados/{mercadoId}/propuesta");
        Assert.Equal("ajustes_solicitados", suya!.Estado);
        Assert.Equal("Confirma que ese correo es institucional.", suya.MotivoDeLaDecision);

        var corregida = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta",
            CambiaElCorreo("institucional@propuesta.test"));
        corregida.EnsureSuccessStatusCode();
        Assert.Equal("institucional@propuesta.test",
            (await corregida.Content.ReadFromJsonAsync<PropuestaDeCambioDto>())!.Campos[0].ValorPropuesto);
    }

    [Fact]
    public async Task Mientras_el_programa_la_mira_la_organizacion_no_puede_cambiarla_ni_abandonarla()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del bloqueo");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        (await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", CambiaElCorreo("uno@propuesta.test")))
            .EnsureSuccessStatusCode();
        (await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta/enviar", new { }))
            .EnsureSuccessStatusCode();

        // MUTANTE QUE MATA: dejar pasar el PUT con la propuesta en revisión. La decisión se tomaría
        // sobre algo distinto de lo que el funcionario tiene delante.
        var cambio = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", CambiaElCorreo("dos@propuesta.test"));
        Assert.Equal(HttpStatusCode.Conflict, cambio.StatusCode);

        var abandono = await DeleteAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta");
        Assert.Equal(HttpStatusCode.Conflict, abandono.StatusCode);
    }

    [Fact]
    public async Task Un_campo_que_no_admite_propuesta_se_rechaza_y_se_explica()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del campo raro");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        // EL CATALOGO DE CAMPOS VIVE EN EL SERVIDOR. Sin esta comprobación, la tabla genérica
        // aceptaría cualquier nombre y al aplicar se escribiría lo que dijera el cliente.
        var respuesta = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", new GuardarPropuestaDeCambioSolicitud
        {
            Campos = [new CampoPropuestoSolicitud { CampoId = "estadoRegistro", ValorPropuesto = "publicado" }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sobre_un_mercado_que_no_esta_publicado_no_se_propone_nada()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del borrador");

        int alcance, modalidad;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            alcance = await db.AlcancesMercado.OrderBy(a => a.OrdenVisualizacion).Select(a => a.Id).FirstAsync();
            modalidad = await db.ModalidadesMercado.OrderBy(m => m.OrdenVisualizacion).Select(m => m.Id).FirstAsync();
        }

        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado todavía en borrador",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            AlcanceId = alcance,
            ModalidadId = modalidad,
        });
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        // UN BORRADOR SE EDITA DIRECTAMENTE. Proponer un cambio sobre él sería un circuito de más
        // para llegar al mismo sitio, y dos maneras de hacer lo mismo se separan sin que se note.
        var respuesta = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/propuesta",
            CambiaElCorreo("da@igual.test"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Aplicar_una_propuesta_queda_en_la_bitacora_del_mercado()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización de la bitácora");
        var mercadoId = await MercadoPublicadoAsync(factory, cliente, consola, organizacion, csrf);

        (await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta", CambiaElCorreo("bitacora@propuesta.test")))
            .EnsureSuccessStatusCode();
        var enviada = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/propuesta/enviar", new { });
        var propuesta = await enviada.Content.ReadFromJsonAsync<PropuestaDeCambioDto>();
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/propuestas/{propuesta!.Id}/decision",
            new DecidirPropuestaDeCambioSolicitud { Decision = "aplicar" })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        var lineas = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == "Mercados" && a.RecordId == registro)
            .ToListAsync();

        // LAS DOS PUNTAS DEL CIRCUITO DEJAN RASTRO: quién propuso y quién decidió. Con solo una, el
        // historial de la ficha enseñaría un cambio que nadie pidió o una petición que nadie cerró.
        Assert.Contains(lineas, l => l.NewValuesJson?.Contains("MercadoCambioPropuesto", StringComparison.Ordinal) == true);
        Assert.Contains(lineas, l => l.NewValuesJson?.Contains("MercadoPropuestaAplicada", StringComparison.Ordinal) == true);
    }
}
