using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El recorrido completo de un mercado musical: la organización lo registra, lo envía, el Programa
/// decide.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES EL MISMO CIRCUITO QUE EL DE UN FESTIVAL,</b> y estas pruebas existen para que siga
/// siéndolo: borrador, se completa, se envía a revisión, el Programa publica o pide ajustes, y con
/// ajustes vuelve a ser editable. Un circuito a medias —por ejemplo, uno donde «ajustes
/// solicitados» no devuelve la edición— deja el registro atascado sin que nadie lo note hasta que
/// una organización llama por teléfono.
/// </para>
/// <para>
/// <b>Y QUE LA RELACION CON EL FESTIVAL SE COMPRUEBE TAMBIEN EN ESTE CANAL.</b> La organización no
/// manda su identificador en el cuerpo —lo pone la ruta—, pero sí manda el del festival, y ahí la
/// regla de pertenencia tiene que aplicarse igual que en la consola.
/// </para>
/// </remarks>
public sealed class MercadosDesdeLaOrganizacionTests
{
    private const string Clave = "ClaveExterna123";

    /// <summary>Registra una organización externa, le confirma el correo y devuelve su sesión.</summary>
    private static async Task<(HttpClient Sesion, int OrganizacionId, string Csrf)> OrganizacionAsync(
        TestWebApplicationFactory factory, string nombre)
    {
        var cliente = factory.CreateClient();
        var correo = $"mercados.{Guid.NewGuid():N}@organizacion.test";

        var alta = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
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
        });
        alta.EnsureSuccessStatusCode();

        // SIN CORREO CONFIRMADO NO SE REGISTRAN PROCESOS, y eso tiene sus propias pruebas. Estas
        // miden lo que pasa después, así que lo dan por hecho en una línea.
        await CorreoConfirmadoEnPruebas.ConfirmarAsync(factory, correo);

        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = Clave,
        });
        entrada.EnsureSuccessStatusCode();

        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var csrf = testigo.GetProperty("requestToken").GetString()!;

        var mias = await cliente.GetFromJsonAsync<List<OrganizacionAdministradaDto>>("/api/v1/externo/organizaciones/mis");
        Assert.NotNull(mias);
        var organizacion = int.Parse(mias![0].Id, System.Globalization.CultureInfo.InvariantCulture);

        return (cliente, organizacion, csrf);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient cliente, string csrf, string ruta, object cuerpo)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = JsonContent.Create(cuerpo),
        };
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient cliente, string csrf, string ruta)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Delete, ruta);
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient cliente, string csrf, string ruta, object cuerpo)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Put, ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        return await cliente.SendAsync(peticion);
    }

    private static MercadoUpsertRequest UnMercadoCompleto(PnmcDbContext? _ = null) => new()
    {
        Nombre = "Mercado Musical del Sur",
        Descripcion = "Encuentro de intercambio y circulación para músicas del sur del país.",
        NivelCobertura = "municipal",
        CodigoDepartamento = "05",
        CodigoMunicipio = "05001",
        CorreoMercado = "contacto@mercado.test",
    };

    [Fact]
    public async Task La_organizacion_registra_su_mercado_y_nace_en_borrador()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización que registra");

        var creado = await PostAsync(cliente, csrf,
            $"/api/v1/externo/organizaciones/{organizacion}/mercados", UnMercadoCompleto());

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.NotNull(mercado);
        Assert.Equal("borrador", mercado!.EstadoRegistro);
        Assert.Equal(organizacion, mercado.OrganizacionId);
    }

    [Fact]
    public async Task El_mercado_solo_aparece_en_la_lista_de_su_organizacion()
    {
        await using var factory = new TestWebApplicationFactory();
        var (unaCliente, unaOrganizacion, unCsrf) = await OrganizacionAsync(factory, "Organización dueña");
        var (otraCliente, otraOrganizacion, _) = await OrganizacionAsync(factory, "Organización vecina");

        (await PostAsync(unaCliente, unCsrf,
            $"/api/v1/externo/organizaciones/{unaOrganizacion}/mercados", UnMercadoCompleto())).EnsureSuccessStatusCode();

        var mios = await unaCliente.GetFromJsonAsync<List<MercadoDto>>($"/api/v1/externo/organizaciones/{unaOrganizacion}/mercados");
        var ajenos = await otraCliente.GetFromJsonAsync<List<MercadoDto>>($"/api/v1/externo/organizaciones/{otraOrganizacion}/mercados");

        Assert.Single(mios!);
        Assert.Empty(ajenos!);
    }

    [Fact]
    public async Task Una_organizacion_no_lee_los_mercados_de_otra()
    {
        // LA RUTA LLEVA EL IDENTIFICADOR, Y ESO NO LA CONVIERTE EN PUBLICA: sin esta comprobación
        // bastaría cambiar un número en la barra de direcciones.
        await using var factory = new TestWebApplicationFactory();
        var (_, ajena, _) = await OrganizacionAsync(factory, "Organización observada");
        var (cliente, _, _) = await OrganizacionAsync(factory, "Organización curiosa");

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{ajena}/mercados");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_testigo_antiforgery_no_se_registra_nada()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, _) = await OrganizacionAsync(factory, "Organización sin testigo");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/externo/organizaciones/{organizacion}/mercados", UnMercadoCompleto());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_mercado_incompleto_no_llega_al_Programa()
    {
        // ES MAS EXIGENTE QUE EL BORRADOR A PROPOSITO: un borrador se guarda con lo que haya, pero
        // lo que se entrega a revisión tiene que poder revisarse.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización con prisa");

        var minimo = new MercadoUpsertRequest
        {
            Nombre = "Mercado apenas empezado",
            NivelCobertura = "nacional",
        };
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", minimo);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var envio = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { });
        var cuerpo = await envio.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, envio.StatusCode);
        Assert.Contains("Describe el mercado", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_circuito_completo_deja_el_mercado_publicado()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del circuito");
        var consola = await CmsTestClient.LoginAsync(factory);

        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);

        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var envio = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { });
        envio.EnsureSuccessStatusCode();
        var enviado = await envio.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.Equal("en_revision", enviado!.EstadoRegistro);

        // EN REVISION NO SE TOCA: está en manos del Programa.
        var intento = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.Conflict, intento.StatusCode);

        var decision = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado.Id}/decision",
            new DecisionSobreMercado { Decision = "publicar" });
        decision.EnsureSuccessStatusCode();
        var publicado = await decision.Content.ReadFromJsonAsync<MercadoDto>();

        Assert.Equal("publicado", publicado!.EstadoRegistro);
        Assert.NotNull(publicado.FechaPublicacion);
    }

    [Fact]
    public async Task El_historial_del_mercado_cuenta_el_circuito_y_solo_a_su_organizacion()
    {
        // LA FICHA DE UN MERCADO SE LEE COMO LA DE UN FESTIVAL, y eso incluye su historial. Lo que
        // se fija aquí es que los movimientos salgan en orden —del más reciente al más antiguo—,
        // con el estado de antes y el de después donde los hay, y que una organización ajena no
        // pueda leerlos: es la trazabilidad de un registro que no administra.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización con historial");
        var consola = await CmsTestClient.LoginAsync(factory);

        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var envio = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { });
        envio.EnsureSuccessStatusCode();

        var decision = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado.Id}/decision",
            new DecisionSobreMercado { Decision = "publicar" });
        decision.EnsureSuccessStatusCode();

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{organizacion}/mercados/{mercado.Id}/historial");
        respuesta.EnsureSuccessStatusCode();
        var movimientos = await respuesta.Content.ReadFromJsonAsync<EntradaDeHistorialDeMercadoDto[]>();

        Assert.NotNull(movimientos);
        Assert.NotEmpty(movimientos);
        // Del más reciente al más antiguo: el primero es la publicación y el último, el registro.
        Assert.Equal("MercadoCreado", movimientos[^1].Accion);
        Assert.Contains(movimientos, m => m.Accion == "MercadoEnviadoARevision");
        Assert.All(movimientos, m => Assert.Equal("Mercado", m.Modulo));

        var envioRegistrado = movimientos.First(m => m.Accion == "MercadoEnviadoARevision");
        Assert.Equal("borrador", envioRegistrado.EstadoAnterior);
        Assert.Equal("en_revision", envioRegistrado.EstadoNuevo);

        // LA DECISION DE LA CONSOLA SE LEE COMO LO QUE FUE, y no como «decidir». Los dos lados del
        // circuito auditan con vocabularios distintos —la consola con verbos genéricos, el espacio
        // externo con eventos con nombre—, y sin traducirlo la publicación, que es el movimiento
        // que la organización viene a mirar, llegaba a su ficha como una actualización cualquiera.
        var publicacion = Assert.Single(movimientos, m => m.Accion == "MercadoPublicado");
        Assert.Equal("en_revision", publicacion.EstadoAnterior);
        Assert.Equal("publicado", publicacion.EstadoNuevo);
        Assert.DoesNotContain(movimientos, m => m.Accion == "decidir" || m.Accion == "guardar" || m.Accion == "crear");

        // Y NO LO LEE QUIEN NO ADMINISTRA EL MERCADO.
        var (ajena, otraOrganizacion, _) = await OrganizacionAsync(factory, "Organización que mira de lejos");
        var prohibido = await ajena.GetAsync($"/api/v1/externo/organizaciones/{otraOrganizacion}/mercados/{mercado.Id}/historial");
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
    }

    [Fact]
    public async Task Deshacer_el_envio_devuelve_el_mercado_a_borrador_y_abandonar_un_borrador_lo_archiva()
    {
        // NO SON LA MISMA ACCION AUNQUE SE PULSEN EN EL MISMO SITIO. Deshacer un envío conserva el
        // trabajo —vuelve a borrador y se puede corregir y reenviar—; abandonar un borrador lo
        // archiva. Tratarlas igual haría perder el trabajo de quien solo quería corregir algo.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización que se arrepiente");

        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { });

        var deshecho = await DeleteAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deshecho.StatusCode);
        var trasDeshacer = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercado.Id}");
        Assert.Equal("borrador", trasDeshacer!.EstadoRegistro);

        var abandonado = await DeleteAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, abandonado.StatusCode);
        var trasAbandonar = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercado.Id}");
        Assert.Equal("archivado", trasAbandonar!.EstadoRegistro);
    }

    [Fact]
    public async Task Un_mercado_publicado_no_lo_retira_su_organizacion_sino_que_lo_pide()
    {
        // ES PARTE DEL CATALOGO PUBLICO: quitarlo es una decisión del Programa, no de quien lo
        // registró. Y la solicitud que se crea tiene que llegar a la bandeja con el nombre del
        // mercado, o quien revisa ve «Registro #7» y no sabe de qué le hablan.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización con mercado publicado");
        var consola = await CmsTestClient.LoginAsync(factory);

        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { });
        await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado.Id}/decision",
            new DecisionSobreMercado { Decision = "publicar" });

        // Borrarlo por la puerta de los borradores no es posible.
        var intento = await DeleteAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}");
        Assert.Equal(HttpStatusCode.Conflict, intento.StatusCode);

        var sinJustificacion = await PostAsync(cliente, csrf,
            $"/api/v1/externo/mercados/{mercado.Id}/solicitudes-retiro", new { justificacion = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, sinJustificacion.StatusCode);

        var solicitud = await PostAsync(cliente, csrf,
            $"/api/v1/externo/mercados/{mercado.Id}/solicitudes-retiro", new { justificacion = "El mercado dejó de realizarse." });
        Assert.Equal(HttpStatusCode.Created, solicitud.StatusCode);

        // UNA SOLICITUD VIVA A LA VEZ.
        var repetida = await PostAsync(cliente, csrf,
            $"/api/v1/externo/mercados/{mercado.Id}/solicitudes-retiro", new { justificacion = "Otra vez." });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        // LLEGA A LA BANDEJA CON NOMBRE, no como «Registro #id».
        var bandeja = await consola.GetFromJsonAsync<PagedResponse<RecordLinkRequestDto>>("/api/v1/admin/solicitudes-de-vinculacion");
        var fila = Assert.Single(bandeja!.Items, item => item.ModuleId == "mercados_retiro");
        Assert.Equal(mercado.Nombre, fila.RecordName);

        // Y APROBARLA LA EJECUTA: una solicitud aprobada que deja el mercado publicado es una
        // decisión que nadie aplicó.
        var decision = await consola.PostAsJsonAsync($"/api/v1/admin/solicitudes-de-vinculacion/{fila.Id}/status",
            new RecordLinkRequestStatusRequest { Status = "aprobada", Comment = "Se retira." });
        decision.EnsureSuccessStatusCode();

        var tras = await consola.GetFromJsonAsync<FichaMercadoAdministrativaDto>($"/api/v1/admin/mercados/{mercado.Id}");
        Assert.Equal("archivado", tras!.Estado);
    }

    [Fact]
    public async Task El_contacto_de_un_mercado_publicado_se_corrige_sin_pasar_por_revision()
    {
        // LO PUBLICADO NO SE EDITA, PERO UN CORREO QUE REBOTA NO PUEDE ESPERAR: quien intenta
        // contactar al mercado desde el portal no llega a nadie mientras tanto.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización que corrige su correo");
        var consola = await CmsTestClient.LoginAsync(factory);

        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        // Mientras no esté publicado, esta vía no aplica: se edita con el resto de la información.
        var pronto = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/contacto-publico",
            new { correoMercado = "nuevo@mercado.test" });
        Assert.Equal(HttpStatusCode.Conflict, pronto.StatusCode);

        await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}/enviar-a-revision", new { });
        await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado.Id}/decision",
            new DecisionSobreMercado { Decision = "publicar" });

        var invalido = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}/contacto-publico",
            new { correoMercado = "esto-no-es-un-correo" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var corregido = await PutAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado.Id}/contacto-publico",
            new { correoMercado = "nuevo@mercado.test", telefonoMercado = "3001234567", sitioWebMercado = "https://mercado.test" });
        corregido.EnsureSuccessStatusCode();
        var actualizado = await corregido.Content.ReadFromJsonAsync<MercadoDto>();

        Assert.Equal("nuevo@mercado.test", actualizado!.CorreoMercado);
        // Y SIGUE PUBLICADO: corregir el contacto no lo devuelve a revisión.
        Assert.Equal("publicado", actualizado.EstadoRegistro);
    }

    [Fact]
    public async Task Pedir_ajustes_sin_decir_cuales_no_es_una_decision()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización que espera");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercado = await EnRevisionAsync(factory, cliente, csrf, organizacion);

        var sinMotivo = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/decision",
            new DecisionSobreMercado { Decision = "ajustes" });

        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
    }

    [Fact]
    public async Task Con_ajustes_solicitados_la_organizacion_vuelve_a_poder_editar()
    {
        // ES LA MITAD QUE SE OLVIDA. Un circuito que pide ajustes y no devuelve la edición deja el
        // registro atascado: la organización ve qué corregir y no puede tocarlo.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización que corrige");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercado = await EnRevisionAsync(factory, cliente, csrf, organizacion);

        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/decision",
            new DecisionSobreMercado { Decision = "ajustes", Motivo = "Falta precisar el lugar." }))
            .EnsureSuccessStatusCode();

        var cambio = UnMercadoCompleto();
        cambio.Nombre = "Mercado Musical del Sur, corregido";
        cambio.LugarEspecifico = "Plaza principal";

        var peticion = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/mercados/{mercado}")
        {
            Content = JsonContent.Create(cambio),
        };
        peticion.Headers.Add("X-CSRF-TOKEN", csrf);
        var guardado = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        var actualizado = await guardado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.Equal("Mercado Musical del Sur, corregido", actualizado!.Nombre);
        Assert.Equal("ajustes_solicitados", actualizado.EstadoRegistro);
    }

    [Fact]
    public async Task Un_festival_de_otra_organizacion_tampoco_se_cuela_por_este_canal()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del mercado externo");
        var ajeno = await FestivalAjenoAsync(factory);

        var peticion = UnMercadoCompleto();
        peticion.SeRealizaEnElMarcoDeUnFestival = true;
        peticion.FestivalId = ajeno;

        var respuesta = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("otra organización", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> EnRevisionAsync(
        TestWebApplicationFactory factory, HttpClient cliente, string csrf, int organizacion)
    {
        var completo = UnMercadoCompleto();
        completo.AlcanceId = await PrimerAlcanceAsync(factory);
        completo.ModalidadId = await PrimeraModalidadAsync(factory);

        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", completo);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        (await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { }))
            .EnsureSuccessStatusCode();
        return mercado.Id;
    }

    private static async Task<int> PrimerAlcanceAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.AlcancesMercado.OrderBy(a => a.OrdenVisualizacion).Select(a => a.Id).FirstAsync();
    }

    private static async Task<int> PrimeraModalidadAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.ModalidadesMercado.OrderBy(m => m.OrdenVisualizacion).Select(m => m.Id).FirstAsync();
    }

    private static async Task<int> FestivalAjenoAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organización dueña del festival ajeno",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();

        var festival = new FestivalRow
        {
            Name = "Festival que no es tuyo",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "publicado",
            OrganizacionPrincipalId = organizacion.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.FestivalRecords.Add(festival);
        await db.SaveChangesAsync();
        return festival.Id;
    }
}
