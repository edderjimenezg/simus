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
/// La devolución de un mercado con los cambios pedidos campo por campo.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS CIERRAN.</b> Antes, un mercado devuelto llevaba UN PARRAFO: todo lo que
/// hubiera que decir sobre treinta campos cabía ahí, y no había forma de saber a cuál se refería
/// cada frase, ni de marcar un punto como resuelto, ni de contar cuántos quedaban. Es el mismo
/// circuito que tiene un Festival desde.
/// </para>
/// <para>
/// Se recorre entero: el funcionario escribe y guarda el borrador —que nadie más ve—, lo envía, la
/// organización lo lee y atiende las notas, y al reenviar el mercado el expediente se cierra.
/// </para>
/// </remarks>
public sealed class RevisionPorCamposDeMercadoTests
{
    private const string Clave = "ClaveExterna123";

    private static async Task<(HttpClient Sesion, int OrganizacionId, string Csrf)> OrganizacionAsync(
        TestWebApplicationFactory factory, string nombre)
    {
        var cliente = factory.CreateClient();
        var correo = $"revision.{Guid.NewGuid():N}@organizacion.test";

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

    /// <summary>Un mercado de esa organización, ya enviado a revisión y esperando decisión.</summary>
    private static async Task<int> MercadoEnRevisionAsync(
        TestWebApplicationFactory factory, HttpClient cliente, int organizacion, string csrf)
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
            Nombre = "Mercado de la revisión",
            Descripcion = "Encuentro de intercambio para probar la devolución campo por campo.",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            CorreoMercado = "contacto@revision.test",
            AlcanceId = alcance,
            ModalidadId = modalidad,
        });
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        (await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercado!.Id}/enviar-a-revision", new { }))
            .EnsureSuccessStatusCode();
        return mercado.Id;
    }

    private static GuardarRevisionDeCamposDeRegistroSolicitud DosNotas() => new()
    {
        ObservacionGeneral = "Faltan dos cosas para poder publicarlo.",
        Observaciones =
        [
            new ObservacionDeCampoDeRegistroSolicitud
            {
                Ambito = "principal", SeccionId = "identificacion", CampoId = "nombre",
                CampoEtiqueta = "Nombre del mercado", ValorObservado = "Mercado de la revisión",
                Nota = "El nombre no dice de qué es el mercado.",
            },
            new ObservacionDeCampoDeRegistroSolicitud
            {
                Ambito = "principal", SeccionId = "contacto", CampoId = "correoMercado",
                CampoEtiqueta = "Correo de contacto", ValorObservado = "contacto@revision.test",
                Nota = "Ese correo rebota.",
            },
        ],
    };

    [Fact]
    public async Task El_circuito_entero_va_del_borrador_a_la_organizacion_y_vuelve()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización de la revisión");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);

        // 1. SIN REVISION ABIERTA SE DEVUELVE UNA CARCASA VACIA, no un 404: la pantalla se pinta
        //    igual en los dos casos, y un 404 la obligaría a distinguir «no hay» de «no existe».
        var vacia = await consola.GetFromJsonAsync<RevisionDeCamposDeRegistroDto>($"/api/v1/institucional/mercados/{mercadoId}/revision");
        Assert.Equal(0, vacia!.Id);
        Assert.Equal("borrador", vacia.Estado);
        Assert.Empty(vacia.Observaciones);

        // 2. EL BORRADOR SE GUARDA Y SOLO LO VE EL FUNCIONARIO.
        var guardada = await consola.PutAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision", DosNotas());
        guardada.EnsureSuccessStatusCode();
        var borrador = await guardada.Content.ReadFromJsonAsync<RevisionDeCamposDeRegistroDto>();
        Assert.Equal("borrador", borrador!.Estado);
        Assert.Equal(2, borrador.Observaciones.Count);

        var todaviaNada = await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>($"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos");
        Assert.Empty(todaviaNada!);

        // 3. AL ENVIAR, EL MERCADO VUELVE A LA ORGANIZACION CON LAS NOTAS.
        var envio = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar", DosNotas());
        envio.EnsureSuccessStatusCode();
        Assert.Equal("enviada", (await envio.Content.ReadFromJsonAsync<RevisionDeCamposDeRegistroDto>())!.Estado);

        var mercado = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercadoId}");
        Assert.Equal("ajustes_solicitados", mercado!.EstadoRegistro);

        var pedidos = await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>($"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos");
        Assert.Equal(2, pedidos!.Length);
        Assert.All(pedidos, p => Assert.Equal("pendiente", p.Estado));
        // EL ROTULO Y EL VALOR VIAJAN COPIADOS: son la evidencia de sobre qué se pidió el cambio.
        Assert.Contains(pedidos, p => p.CampoEtiqueta == "Correo de contacto" && p.ValorObservado == "contacto@revision.test");

        // 4. LA ORGANIZACION LAS ATIENDE, Y PUEDE DESMARCAR.
        var primera = pedidos[0];
        var atendida = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/cambios-pedidos/{primera.Id}/atender", new { atendida = true });
        atendida.EnsureSuccessStatusCode();
        Assert.Equal("atendida", (await atendida.Content.ReadFromJsonAsync<ObservacionDeCampoDeRegistroDto>())!.Estado);

        var desmarcada = await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/cambios-pedidos/{primera.Id}/atender", new { atendida = false });
        desmarcada.EnsureSuccessStatusCode();
        var vuelta = await desmarcada.Content.ReadFromJsonAsync<ObservacionDeCampoDeRegistroDto>();
        Assert.Equal("pendiente", vuelta!.Estado);
        // Y LA FECHA SE LIMPIA: dejarla diría que alguien la resolvió cuando vuelve a estar pendiente.
        Assert.Null(vuelta.FechaAtencion);

        // 5. AL REENVIAR, EL EXPEDIENTE SE CIERRA Y LO PENDIENTE SE DA POR ATENDIDO.
        (await PostAsync(cliente, csrf, $"/api/v1/externo/mercados/{mercadoId}/enviar-a-revision", new { }))
            .EnsureSuccessStatusCode();

        var trasReenviar = await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>($"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos");
        Assert.Empty(trasReenviar!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        var cerrada = await db.RevisionesDeRegistro.AsNoTracking()
            .Where(r => r.ModuloId == Modulos.Mercados && r.RegistroId == registro)
            .OrderByDescending(r => r.Id).FirstAsync();
        Assert.Equal("cerrada", cerrada.Estado);
        Assert.NotNull(cerrada.FechaCierre);
        Assert.All(await db.RevisionesDeRegistroObservaciones.AsNoTracking().Where(o => o.IdRevision == cerrada.Id).ToListAsync(),
            o => Assert.Equal("atendida", o.Estado));

        // 6. Y EL EXPEDIENTE GUARDA LAS DOS PUNTAS DE LA CONVERSACION.
        Assert.NotNull(cerrada.RevisorNombre);
        Assert.Equal("Organización de la revisión", cerrada.OrganizacionNombre);
    }

    [Fact]
    public async Task Una_devolucion_sin_senalar_ningun_campo_no_es_una_devolucion()
    {
        // ES LA DIFERENCIA CON LA DECISION DE LA BANDEJA. Aquella pide un párrafo; esta pide señalar
        // qué hay que cambiar. Enviarla vacía deja a la organización con el mercado devuelto y
        // ninguna instrucción, que es el caso que este circuito existe para cerrar.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización sin notas");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);

        var vacia = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar",
            new GuardarRevisionDeCamposDeRegistroSolicitud { ObservacionGeneral = "Corrige varias cosas.", Observaciones = [] });

        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
        var mercado = await cliente.GetFromJsonAsync<MercadoDto>($"/api/v1/externo/mercados/{mercadoId}");
        Assert.Equal("en_revision", mercado!.EstadoRegistro);
    }

    [Fact]
    public async Task Solo_se_devuelve_lo_que_esta_en_revision_y_no_se_envia_dos_veces()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del doble envío");
        var consola = await CmsTestClient.LoginAsync(factory);

        // Sobre un borrador que su organización no ha entregado no se decide nada.
        int alcance, modalidad;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            alcance = await db.AlcancesMercado.OrderBy(a => a.OrdenVisualizacion).Select(a => a.Id).FirstAsync();
            modalidad = await db.ModalidadesMercado.OrderBy(m => m.OrdenVisualizacion).Select(m => m.Id).FirstAsync();
        }
        var creado = await PostAsync(cliente, csrf, $"/api/v1/externo/organizaciones/{organizacion}/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado en borrador",
            Descripcion = "Todavía sin entregar.",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            CorreoMercado = "contacto@borrador.test",
            AlcanceId = alcance,
            ModalidadId = modalidad,
        });
        var enBorrador = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var pronto = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{enBorrador!.Id}/revision/enviar", DosNotas());
        Assert.Equal(HttpStatusCode.Conflict, pronto.StatusCode);

        // Y una vez enviada, no se envía otra vez ni se reescribe.
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar", DosNotas()))
            .EnsureSuccessStatusCode();

        var repetida = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar", DosNotas());
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        var reescritura = await consola.PutAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision", DosNotas());
        Assert.Equal(HttpStatusCode.Conflict, reescritura.StatusCode);
    }

    [Fact]
    public async Task Dos_notas_sobre_el_mismo_campo_no_se_admiten_y_una_nota_vacia_no_existe()
    {
        // UN CAMPO, UNA NOTA: dos instrucciones sobre el mismo campo pueden contradecirse y la
        // pantalla solo tiene sitio para una. Y la nota vacía es la diferencia entre pedir un
        // cambio y no pedirlo.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización de las notas repetidas");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);

        var repetida = new GuardarRevisionDeCamposDeRegistroSolicitud
        {
            Observaciones =
            [
                new ObservacionDeCampoDeRegistroSolicitud { SeccionId = "identificacion", CampoId = "nombre", CampoEtiqueta = "Nombre", Nota = "Una." },
                new ObservacionDeCampoDeRegistroSolicitud { SeccionId = "identificacion", CampoId = "nombre", CampoEtiqueta = "Nombre", Nota = "Y otra." },
            ],
        };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await consola.PutAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision", repetida)).StatusCode);

        var conVacia = new GuardarRevisionDeCamposDeRegistroSolicitud
        {
            Observaciones =
            [
                new ObservacionDeCampoDeRegistroSolicitud { SeccionId = "identificacion", CampoId = "nombre", CampoEtiqueta = "Nombre", Nota = "   " },
                new ObservacionDeCampoDeRegistroSolicitud { SeccionId = "contacto", CampoId = "correoMercado", CampoEtiqueta = "Correo", Nota = "Rebota." },
            ],
        };
        var guardada = await consola.PutAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision", conVacia);
        guardada.EnsureSuccessStatusCode();
        var revision = await guardada.Content.ReadFromJsonAsync<RevisionDeCamposDeRegistroDto>();
        var unica = Assert.Single(revision!.Observaciones);
        Assert.Equal("correoMercado", unica.CampoId);
    }

    [Fact]
    public async Task Los_cambios_pedidos_de_un_mercado_ajeno_no_se_leen_ni_se_atienden()
    {
        // ES LA TRAZABILIDAD DE UN REGISTRO QUE NO SE ADMINISTRA, y marcar una nota ajena como
        // atendida diría que alguien corrigió algo que no ha tocado.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización dueña");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar", DosNotas()))
            .EnsureSuccessStatusCode();
        var pedidos = await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>($"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos");

        var (ajena, _, csrfAjeno) = await OrganizacionAsync(factory, "Organización que mira de lejos");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await ajena.GetAsync($"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await PostAsync(ajena, csrfAjeno, $"/api/v1/externo/mercados/cambios-pedidos/{pedidos![0].Id}/atender", new { atendida = true })).StatusCode);
    }
    [Fact]
    public async Task Publicar_Un_Mercado_Cierra_Su_Revision_Viva()
    {
        // ENCONTRADO RECORRIENDO EL FLUJO EN EL NAVEGADOR: la ficha de
        // la organización decía «Publicado» arriba y, debajo, «el Programa pidió cambios» con una
        // nota sin atender. Era cierto en la base —el mercado estaba publicado y su expediente
        // seguía abierto— y la pantalla se contradecía a sí misma.
        //
        // MUTANTE QUE MATA: no cerrar la revisión al publicar. Publicar es el final de la
        // conversación: lo que se pidió, o se corrigió, o dejó de importar, y en cualquier caso la
        // organización no tiene nada que reenviar.
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacion, csrf) = await OrganizacionAsync(factory, "Organización del cierre");
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoEnRevisionAsync(factory, cliente, organizacion, csrf);

        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/revision/enviar", DosNotas()))
            .EnsureSuccessStatusCode();
        Assert.NotEmpty(await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>(
            $"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos") ?? []);

        (await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" })).EnsureSuccessStatusCode();

        // Ya no hay nada que corregir, y la ficha deja de decir lo contrario.
        Assert.Empty(await cliente.GetFromJsonAsync<ObservacionDeCampoDeRegistroDto[]>(
            $"/api/v1/externo/mercados/{mercadoId}/cambios-pedidos") ?? []);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        Assert.Empty(db.RevisionesDeRegistro.Where(r =>
            r.ModuloId == Modulos.Mercados && r.RegistroId == registro && r.Estado != "cerrada"));
    }

}
