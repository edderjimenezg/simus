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
/// El circuito de devolución campo por campo, de punta a punta: el funcionario abre la ficha, anota
/// qué cambiar en cada campo, guarda el borrador, lo envía, y la organización lo recibe.
/// </summary>
/// <remarks>
/// <para>
/// LO QUE ESTAS PRUEBAS FIJAN, Y NO EXISTIA. Hasta pedir ajustes era
/// escribir un párrafo en <c>RegistrosRevisionHistorial.Comentario</c>, nvarchar(2400), y la
/// organización lo leía como un aviso suelto en su tarjeta. Nada decía a qué campo se refería cada
/// frase, nada se podía marcar como atendido y nada se podía contar.
/// </para>
/// <para>
/// POR QUE SE PRUEBA POR EL CABLE Y NO CONTRA EL CONTEXTO. Porque la mitad del circuito es la
/// frontera entre las dos autenticaciones: el funcionario tiene cookie <c>pnmc.admin</c> y la
/// organización <c>pnmc.external</c>. Una prueba que escribiera las filas con
/// <c>dbContext.Add</c> daría verde con las dos puertas abiertas de par en par.
/// </para>
/// </remarks>
public sealed class RevisionPorCamposTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Clave = "ClaveExterna123";

    private readonly TestWebApplicationFactory _factory;

    public RevisionPorCamposTests(TestWebApplicationFactory factory) => _factory = factory;

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // El andamio
    // ═══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Una organización con su Festival ya enviado a revisión, que es donde empieza todo.</summary>
    private async Task<(HttpClient Organizacion, int FestivalId, int OrganizacionId)> FestivalEnRevisionAsync(
        string marca, string documento)
    {
        var cliente = _factory.CreateClient();
        var correo = $"campos.{marca}@example.com";

        var alta = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion " + marca,
            FullName = "Persona " + marca,
            NumeroDocumento = documento,
            HeadquartersDepartmentCode = "11",
            HeadquartersMunicipalityCode = "11001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var creada = await alta.Content.ReadFromJsonAsync<ExternalRegisterResponse>();

        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = Clave,
        });
        entrada.EnsureSuccessStatusCode();

        var organizacionId = int.Parse(creada!.OrganizationId, CultureInfo.InvariantCulture);
        var festival = await ExternaAsync(cliente, HttpMethod.Post,
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales",
            new { nombre = "Festival " + marca, descripcion = "Doce dias", nivelCobertura = "nacional" });
        Assert.Equal(HttpStatusCode.Created, festival.StatusCode);

        using var cuerpo = JsonDocument.Parse(await festival.Content.ReadAsStringAsync());
        var festivalId = int.Parse(cuerpo.RootElement.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);

        var envio = await ExternaAsync(cliente, HttpMethod.Post,
            $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);

        return (cliente, festivalId, organizacionId);
    }

    private static async Task<HttpResponseMessage> ExternaAsync(HttpClient cliente, HttpMethod metodo, string ruta, object cuerpo)
    {
        var testigo = await cliente.GetAsync(new Uri("/api/v1/externo/organizaciones/csrf", UriKind.Relative));
        testigo.EnsureSuccessStatusCode();
        var token = await testigo.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();

        var peticion = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("X-CSRF-TOKEN", token!.RequestToken);
        return await cliente.SendAsync(peticion);
    }

    private async Task<HttpClient> FuncionarioAsync()
    {
        var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var testigo = await cliente.GetAsync(new Uri("/api/v1/institucional/festivales/csrf", UriKind.Relative));
        testigo.EnsureSuccessStatusCode();
        var token = await testigo.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>();
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
        return cliente;
    }

    private static object Nota(string campoId, string etiqueta, string nota, string seccion = "generales") => new
    {
        ambito = "principal",
        seccionId = seccion,
        campoId,
        campoEtiqueta = etiqueta,
        valorObservado = "lo que decia",
        nota,
    };

    private static async Task<RevisionDeCamposDeRegistroDto> LeerAsync(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        var revision = await respuesta.Content.ReadFromJsonAsync<RevisionDeCamposDeRegistroDto>();
        Assert.NotNull(revision);
        return revision!;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // El borrador
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Un_Festival_Sin_Revision_Devuelve_La_Carcasa_Vacia_Y_No_Un_404()
    {
        // LA PANTALLA SE PINTA IGUAL EN LOS DOS CASOS, y por eso no es un 404: con un 404 el front
        // tendria que distinguir «este Festival no tiene revision» de «este Festival no existe»,
        // que son dos cosas y llegan con el mismo codigo.
        var (_, festivalId, _) = await FestivalEnRevisionAsync("vacia", "1300000001");
        var funcionario = await FuncionarioAsync();

        var revision = await LeerAsync(await funcionario.GetAsync(
            new Uri($"/api/v1/institucional/festivales/{festivalId}/revision", UriKind.Relative)));

        Assert.Equal(0, revision.Id);
        Assert.Equal("borrador", revision.Estado);
        Assert.Empty(revision.Observaciones);
        Assert.Equal("Festival vacia", revision.RegistroNombre);
    }

    [Fact]
    public async Task El_Borrador_Se_Guarda_Y_Se_Relee_Con_Sus_Notas()
    {
        var (_, festivalId, _) = await FestivalEnRevisionAsync("borra", "1300000002");
        var funcionario = await FuncionarioAsync();

        var guardado = await LeerAsync(await funcionario.PutAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision",
            new
            {
                observacionGeneral = "Faltan datos de contacto.",
                observaciones = new[]
                {
                    Nota("festival.nombre", "Nombre del festival", "Escribe el nombre completo, sin la sigla."),
                    Nota("festival.correoContacto", "Correo de contacto", "Este correo rebota.", "contacto-festival"),
                },
            }));

        Assert.NotEqual(0, guardado.Id);
        Assert.Equal("borrador", guardado.Estado);
        Assert.Equal(2, guardado.Observaciones.Count);

        // SE RELEE POR OTRA PETICION y no se da por buena la respuesta del PUT: lo que hay que medir
        // es que quedo escrito, no que el metodo devolvio lo que le pasaron.
        var releido = await LeerAsync(await funcionario.GetAsync(
            new Uri($"/api/v1/institucional/festivales/{festivalId}/revision", UriKind.Relative)));

        Assert.Equal(guardado.Id, releido.Id);
        Assert.Equal("Faltan datos de contacto.", releido.ObservacionGeneral);
        var nombre = releido.Observaciones.Single(item => item.CampoId == "festival.nombre");
        Assert.Equal("Nombre del festival", nombre.CampoEtiqueta);
        Assert.Equal("Escribe el nombre completo, sin la sigla.", nombre.Nota);
        Assert.Equal("lo que decia", nombre.ValorObservado);
        Assert.Equal("pendiente", nombre.Estado);
        Assert.Equal("contacto-festival",
            releido.Observaciones.Single(item => item.CampoId == "festival.correoContacto").SeccionId);
    }

    [Fact]
    public async Task Guardar_De_Nuevo_Reemplaza_La_Lista_Entera_Y_Borra_Lo_Que_Ya_No_Viene()
    {
        // SEMANTICA DE REEMPLAZO. Vaciar el texto de un campo es como se retira una nota desde la
        // pantalla; sin el borrado, esa nota seguiria llegandole a la organizacion.
        var (_, festivalId, _) = await FestivalEnRevisionAsync("reemp", "1300000003");
        var funcionario = await FuncionarioAsync();
        var ruta = $"/api/v1/institucional/festivales/{festivalId}/revision";

        var primero = await LeerAsync(await funcionario.PutAsJsonAsync(ruta, new
        {
            observaciones = new[]
            {
                Nota("festival.nombre", "Nombre del festival", "Uno."),
                Nota("festival.periodicidad", "Periodicidad", "Dos."),
            },
        }));
        var idDelNombre = primero.Observaciones.Single(item => item.CampoId == "festival.nombre").Id;

        var segundo = await LeerAsync(await funcionario.PutAsJsonAsync(ruta, new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Uno, corregido.") },
        }));

        Assert.Single(segundo.Observaciones);
        Assert.Equal("Uno, corregido.", segundo.Observaciones[0].Nota);
        // LA FILA ES LA MISMA: conservar el identificador es lo que sostiene el enlace desde la
        // pantalla de la organizacion, que apunta a la nota por su numero.
        Assert.Equal(idDelNombre, segundo.Observaciones[0].Id);
    }

    [Fact]
    public async Task Una_Nota_Vacia_No_Es_Un_Error_Sino_Una_Nota_Retirada()
    {
        var (_, festivalId, _) = await FestivalEnRevisionAsync("vac2", "1300000004");
        var funcionario = await FuncionarioAsync();

        var guardado = await LeerAsync(await funcionario.PutAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision",
            new
            {
                observaciones = new[]
                {
                    Nota("festival.nombre", "Nombre del festival", "Esta si."),
                    Nota("festival.periodicidad", "Periodicidad", "   "),
                },
            }));

        // NI 400 NI FILA EN BLANCO: la nota sin texto simplemente no existe.
        Assert.Single(guardado.Observaciones);
        Assert.Equal("festival.nombre", guardado.Observaciones[0].CampoId);
    }

    [Fact]
    public async Task Una_Nota_Sobre_Un_Perfil_Versionado_De_Otro_Festival_Se_Rechaza()
    {
        // EL IDENTIFICADOR DEL PERFIL VERSIONADO VIENE DEL CLIENTE. Sin esta comprobación, una nota podría
        // colgarse del perfil de OTRO Festival y aparecer en la ficha equivocada.
        var (_, festivalId, _) = await FestivalEnRevisionAsync("ajena", "1300000005");
        var funcionario = await FuncionarioAsync();

        var respuesta = await funcionario.PutAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision",
            new
            {
                observaciones = new[]
                {
                    new
                    {
                        ambito = "subregistro",
                        subregistroId = "987654",
                        seccionId = "edicion",
                        campoId = "perfilVersionado.nombre",
                        campoEtiqueta = "Nombre del perfil versionado",
                        valorObservado = (string?)null,
                        nota = "Cambia esto.",
                    },
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("no pertenece a este Festival", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // El envío
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Enviar_Sin_Senalar_Ningun_Campo_Se_Rechaza()
    {
        // ES LA DIFERENCIA CON LA RUTA DE DECISIONES, que pide un parrafo. Esta pide senalar que hay
        // que cambiar: enviarla vacia dejaria a la organizacion con un Festival devuelto y ninguna
        // instruccion, que es justo el caso que este circuito existe para cerrar.
        var (_, festivalId, _) = await FestivalEnRevisionAsync("sin", "1300000006");
        var funcionario = await FuncionarioAsync();

        var respuesta = await funcionario.PostAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision/enviar",
            new { observacionGeneral = "Revisa todo, por favor.", observaciones = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("al menos un campo", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enviar_Devuelve_El_Festival_A_La_Organizacion_Y_Deja_El_Expediente()
    {
        var (_, festivalId, _) = await FestivalEnRevisionAsync("envio", "1300000007");
        var funcionario = await FuncionarioAsync();

        var enviada = await LeerAsync(await funcionario.PostAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision/enviar",
            new
            {
                observacionGeneral = "Dos cosas.",
                observaciones = new[]
                {
                    Nota("festival.nombre", "Nombre del festival", "Sin la sigla."),
                    Nota("festival.descripcion", "Descripción del festival", "Muy corta."),
                },
            }));

        Assert.Equal("enviada", enviada.Estado);
        Assert.NotNull(enviada.FechaEnvio);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // 1. EL FESTIVAL VUELVE A SER EDITABLE. Sin esto la organizacion recibe la lista y no puede
        //    tocar nada: el PUT del Festival contesta 409 en cualquier estado que no sea borrador o
        //    ajustes solicitados.
        var festival = db.FestivalRecords.Single(item => item.Id == festivalId);
        Assert.Equal("ajustes_solicitados", festival.StatusCode);

        // 2. LAS DOS PUNTAS DE LA CONVERSACION, que es la mitad del pedido: quien envia y quien
        //    recibe, con su nombre COPIADO. El destinatario es quien mando el Festival a revision.
        var revision = db.RevisionesDeRegistro.Single(item => item.ModuloId == Modulos.Festivales
            && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("enviada", revision.Estado);
        Assert.Equal(CmsTestClient.WebmasterEmail, db.Users.Single(item => item.Id == revision.IdUsuarioRevisor).Email);
        Assert.Equal("Persona envio", revision.DestinatarioNombre);
        Assert.Equal("Organizacion envio", revision.OrganizacionNombre);

        // 3. EL EXPEDIENTE. `CamposObservados` existe desde y hasta hoy
        //    ninguna pantalla la llenaba: aqui deja de estar vacia.
        var historial = db.HistorialesRevisionRegistros
            .Where(item => item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.Accion == "FestivalCambiosPedidosPorCampo")
            .OrderByDescending(item => item.Fecha)
            .First();
        Assert.Equal("ajustes_solicitados", historial.EstadoNuevo);
        Assert.NotNull(historial.CamposObservados);
        using var campos = JsonDocument.Parse(historial.CamposObservados!);
        Assert.Equal(2, campos.RootElement.GetArrayLength());
        Assert.Equal("festival.nombre", campos.RootElement[0].GetProperty("campoId").GetString());
        // EL COMENTARIO SIGUE SIENDO TEXTO PLANO: es lo que la organizacion lee en su tarjeta, y un
        // JSON ahi se veria tal cual en pantalla.
        Assert.Contains("Nombre del festival", historial.Comentario!, StringComparison.Ordinal);

        // 4. EL AVISO le llega a quien tiene que corregir, y no al funcionario.
        var aviso = db.Notifications
            .Where(item => item.RecordId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.EventType == "FestivalCambiosPedidosPorCampo")
            .OrderByDescending(item => item.CreatedAt)
            .First();
        Assert.Equal(revision.IdUsuarioDestinatario, aviso.RecipientUserId);
        Assert.Contains("2 campos", aviso.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_Festival_Que_No_Esta_En_Revision_No_Recibe_Solicitud_De_Cambios()
    {
        // MOVERLO A `ajustes_solicitados` DESDE UN BORRADOR lo sacaria del sitio donde su
        // organizacion lo dejo, sin que nadie lo hubiera enviado a revisar.
        var cliente = _factory.CreateClient();
        var correo = "campos.borrador@example.com";
        await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion borrador",
            FullName = "Persona borrador",
            NumeroDocumento = "1300000008",
            HeadquartersDepartmentCode = "11",
            HeadquartersMunicipalityCode = "11001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave });
        entrada.EnsureSuccessStatusCode();

        int organizacionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            organizacionId = db.EntityProfiles.Single(item => item.Name == "Organizacion borrador").Id;
        }

        var festival = await ExternaAsync(cliente, HttpMethod.Post,
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales",
            new { nombre = "Festival en borrador", nivelCobertura = "nacional" });
        using var cuerpo = JsonDocument.Parse(await festival.Content.ReadAsStringAsync());
        var festivalId = int.Parse(cuerpo.RootElement.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);

        var funcionario = await FuncionarioAsync();
        var respuesta = await funcionario.PostAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision/enviar",
            new { observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Cambia esto.") } });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // El lado de la organización
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task La_Organizacion_Ve_Lo_Enviado_Y_No_Ve_El_Borrador()
    {
        // UN BORRADOR ES TRABAJO A MEDIAS. Ensenarlo seria pedirle a la organizacion que corrija
        // sobre notas que el funcionario todavia esta escribiendo.
        var (organizacion, festivalId, _) = await FestivalEnRevisionAsync("ver", "1300000009");
        var funcionario = await FuncionarioAsync();

        await funcionario.PutAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision", new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Todavia lo estoy pensando.") },
        });

        var enBorrador = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));
        Assert.Equal(0, enBorrador.Id);
        Assert.Empty(enBorrador.Observaciones);

        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Sin la sigla.") },
        });

        var despues = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));
        Assert.Equal("enviada", despues.Estado);
        Assert.Single(despues.Observaciones);
        Assert.Equal("Sin la sigla.", despues.Observaciones[0].Nota);
        // QUIEN LO PIDIO viaja con la lista: la organizacion tiene derecho a saber a quien contestar.
        Assert.False(string.IsNullOrWhiteSpace(despues.RevisorNombre));
    }

    [Fact]
    public async Task La_Organizacion_Marca_Un_Cambio_Como_Atendido_Y_Lo_Puede_Desmarcar()
    {
        var (organizacion, festivalId, _) = await FestivalEnRevisionAsync("marca", "1300000010");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Sin la sigla.") },
        });

        var pedidos = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));
        var notaId = pedidos.Observaciones[0].Id;

        var marcada = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/cambios-pedidos/{notaId}/atender", new { atendida = true });
        marcada.EnsureSuccessStatusCode();
        var despues = await marcada.Content.ReadFromJsonAsync<ObservacionDeCampoDeRegistroDto>();
        Assert.Equal("atendida", despues!.Estado);
        Assert.NotNull(despues.FechaAtencion);

        // SE PUEDE DESMARCAR: marcar por error es facil, y sin la vuelta atras la unica salida seria
        // pedirle al funcionario que reescriba la nota.
        var deshecha = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/cambios-pedidos/{notaId}/atender", new { atendida = false });
        deshecha.EnsureSuccessStatusCode();
        var vuelta = await deshecha.Content.ReadFromJsonAsync<ObservacionDeCampoDeRegistroDto>();
        Assert.Equal("pendiente", vuelta!.Estado);
        Assert.Null(vuelta.FechaAtencion);
    }

    [Fact]
    public async Task Otra_Organizacion_No_Puede_Leer_Los_Cambios_Pedidos_De_Un_Festival_Ajeno()
    {
        var (_, festivalId, _) = await FestivalEnRevisionAsync("mia", "1300000011");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Sin la sigla.") },
        });

        var (ajena, _, _) = await FestivalEnRevisionAsync("suya", "1300000012");
        var respuesta = await ajena.GetAsync(new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_Reenvio_Se_Niega_Mientras_Queden_Cambios_Sin_Atender()
    {
        // LA REGLA QUE PIDIO EL USUARIO EL 30 DE AGOSTO DE 2026: «cuando termino de atender los
        // ajustes, SOLO cuando termino de atenderlos, se me habilita volver a enviar».
        //
        // ESTA PRUEBA DECIA LO CONTRARIO. Hasta hoy `Reenviar_A_Revision_Cierra_La_Solicitud…`
        // atendia UNA de dos notas y esperaba 200: el Festival volvia a la bandeja del funcionario
        // con la mitad de sus correcciones sin tocar, y el circuito de campo por campo perdia justo
        // lo que aporta —saber que se atendio—.
        var (organizacion, festivalId, _) = await FestivalEnRevisionAsync("bloqueo", "1300000013");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[]
            {
                Nota("festival.nombre", "Nombre del festival", "Sin la sigla."),
                Nota("festival.periodicidad", "Periodicidad", "Di cada cuanto."),
            },
        });

        var pedidos = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));
        var uno = pedidos.Observaciones.Single(item => item.CampoId == "festival.nombre").Id;
        await ExternaAsync(organizacion, HttpMethod.Post, $"/api/v1/externo/cambios-pedidos/{uno}/atender", new { atendida = true });

        var reenvio = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.Conflict, reenvio.StatusCode);

        // EL NUMERO VIAJA EN LA RESPUESTA: el panel apaga su boton con `cambiosPedidos` de la lista,
        // pero quien llame a la ruta a pelo tiene que poder saber cuantos le faltan sin adivinarlo.
        using var cuerpo = JsonDocument.Parse(await reenvio.Content.ReadAsStringAsync());
        Assert.Equal(1, cuerpo.RootElement.GetProperty("cambiosPedidos").GetInt32());
        Assert.Contains("Queda 1 ajuste sugerido pendiente", cuerpo.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("Corrige el campo y guarda el ajuste", cuerpo.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // NADA SE MOVIO. Un rechazo a medias —el Festival ya en revision y la solicitud cerrada—
        // seria peor que aceptar el reenvio: dejaria al funcionario leyendo una ficha que la
        // pantalla de la organizacion sigue dando por corregible.
        var festival = db.FestivalRecords.Single(item => item.Id == festivalId);
        Assert.Equal("ajustes_solicitados", festival.StatusCode);
        var revision = db.RevisionesDeRegistro.Single(item => item.ModuloId == Modulos.Festivales
            && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("enviada", revision.Estado);
        Assert.Null(revision.FechaCierre);

        // Y LAS NOTAS QUEDAN COMO LAS DEJO LA ORGANIZACION: una marcada y otra no. El expediente
        // tiene que poder decir cual de los dos puntos nadie dio por resuelto; falsificarlo con un
        // UPDATE masivo convertiria el historial en una lista de exitos.
        var notas = db.RevisionesDeRegistroObservaciones
            .Where(item => item.IdRevision == revision.Id)
            .OrderBy(item => item.CampoId).ToList();
        Assert.Equal(["atendida", "pendiente"], notas.Select(item => item.Estado).ToArray());
    }

    [Fact]
    public async Task Atendidos_Todos_Los_Cambios_El_Reenvio_Se_Acepta_Y_Cierra_La_Solicitud()
    {
        var (organizacion, festivalId, organizacionId) = await FestivalEnRevisionAsync("cierre", "1300000023");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[]
            {
                Nota("festival.nombre", "Nombre del festival", "Sin la sigla."),
                Nota("festival.periodicidad", "Periodicidad", "Di cada cuanto."),
            },
        });

        var pedidos = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));

        // La organización corrige un campo señalado y, además, mejora otro por iniciativa propia.
        // Los dos tienen que llegar diferenciados al segundo ciclo institucional.
        using (var scopeCorreccion = _factory.Services.CreateScope())
        {
            var dbCorreccion = scopeCorreccion.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festivalCorregido = dbCorreccion.FestivalRecords.Single(item => item.Id == festivalId);
            festivalCorregido.Name = "Festival cierre sin sigla";
            festivalCorregido.Description = "Descripción ampliada por la organización";
            await dbCorreccion.SaveChangesAsync();
        }
        foreach (var nota in pedidos.Observaciones)
        {
            await ExternaAsync(organizacion, HttpMethod.Post,
                $"/api/v1/externo/cambios-pedidos/{nota.Id}/atender", new { atendida = true });
        }

        var reenvio = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = db.FestivalRecords.Single(item => item.Id == festivalId);
        Assert.Equal("en_revision", festival.StatusCode);

        var revision = db.RevisionesDeRegistro.Single(item => item.ModuloId == Modulos.Festivales
            && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("cerrada", revision.Estado);
        Assert.NotNull(revision.FechaCierre);

        // LA SOLICITUD CERRADA YA NO CUENTA: si el bloqueo mirase cualquier solicitud y no solo la
        // enviada, el Festival quedaria trancado para siempre despues del primer ciclo. Se comprueba
        // por donde se nota —la tarjeta del panel— y no leyendo el estado de las notas: que esten
        // «atendida» lo garantiza el propio montaje de esta prueba, que las marco una por una, asi
        // que afirmarlo no medía nada. Lo delato la revision adversarial.
        Assert.Equal(0, await CambiosPedidosAsync(organizacion, organizacionId, festivalId));

        var claveDelFestival = festivalId.ToString(CultureInfo.InvariantCulture);
        var envios = db.EnviosDeRevision.Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == claveDelFestival)
            .OrderBy(item => item.NumeroEnvio).ToList();
        Assert.Equal([1, 2], envios.Select(item => item.NumeroEnvio).ToArray());
        Assert.Null(envios[0].RevisionOrigenId);
        Assert.Equal(revision.Id, envios[1].RevisionOrigenId);

        var funcionarioComparacion = await FuncionarioAsync();
        var respuestaComparacion = await funcionarioComparacion.GetAsync(new Uri(
            $"/api/v1/institucional/festivales/{festivalId}/comparacion-envios", UriKind.Relative));
        respuestaComparacion.EnsureSuccessStatusCode();
        var comparacion = await respuestaComparacion.Content.ReadFromJsonAsync<ComparacionEnviosFestivalDto>();
        Assert.NotNull(comparacion);
        Assert.Equal(2, comparacion.NumeroEnvio);
        Assert.False(comparacion.EsPrimerEnvio);
        Assert.False(comparacion.ComparacionParcial);
        Assert.Equal(2, comparacion.CamposModificados);
        Assert.Equal(1, comparacion.AjustesSugeridosAtendidos);
        Assert.Equal(1, comparacion.OtrosCambios);
        Assert.True(comparacion.Cambios.Single(item => item.CampoId == "festival.nombre").RespondeAjusteSugerido);
        Assert.False(comparacion.Cambios.Single(item => item.CampoId == "festival.descripcion").RespondeAjusteSugerido);

        // La bandeja también nombra correctamente el ciclo antes de abrir la ficha. Si aquí no
        // viaja el número, el frontend vuelve a llamar «Registro inicial» a cada reenvío aunque
        // la comparación interior sí sepa que es el segundo o el tercero.
        var bandeja = await funcionarioComparacion.GetFromJsonAsync<List<FestivalRevisionInstitucionalDto>>(
            "/api/v1/institucional/festivales/en-revision");
        Assert.Equal(2, bandeja!.Single(item => item.Id == festivalId.ToString(CultureInfo.InvariantCulture)).NumeroEnvio);
    }

    [Fact]
    public async Task Los_Cambios_De_Un_Festival_No_Bloquean_El_Envio_De_Otro()
    {
        // LA PUERTA NO PUEDE ESTORBAR AL CAMINO NORMAL. Un Festival que nadie ha devuelto no tiene
        // ninguna solicitud; si la cuenta se hiciera sin filtrar por Festival, el primer envio de
        // cualquier otro de la misma organizacion se quedaria trancado.
        var (organizacion, devuelto, organizacionId) = await FestivalEnRevisionAsync("vecino", "1300000024");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{devuelto}/revision/enviar", new
        {
            observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Sin la sigla.") },
        });

        var otro = await CrearFestivalAsync(organizacion, organizacionId, "Festival vecino");
        var envio = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/festivales/{otro}/enviar-a-revision", new { });

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
    }

    [Fact]
    public async Task Un_Borrador_De_Solicitud_No_Bloquea_El_Envio_Del_Festival()
    {
        // SOLO BLOQUEA LA SOLICITUD `enviada`, y esta prueba es la que sostiene ese `== Enviada`.
        //
        // El PUT del borrador no comprueba en que estado esta el Festival, asi que un funcionario
        // puede dejar anotado un borrador sobre uno que la organizacion todavia no ha enviado. Si el
        // bloqueo mirase cualquier solicitud viva, ese primer envio contestaria 409 con un numero de
        // cambios que la organizacion NO PUEDE VER —`cambios-pedidos` solo devuelve lo enviado— ni
        // atender: un Festival trancado por una nota que nadie le mostro.
        var (organizacion, _, organizacionId) = await FestivalEnRevisionAsync("draft", "1300000025");
        var sinEnviar = await CrearFestivalAsync(organizacion, organizacionId, "Festival con borrador ajeno");

        var funcionario = await FuncionarioAsync();
        var borrador = await funcionario.PutAsJsonAsync(
            $"/api/v1/institucional/festivales/{sinEnviar}/revision",
            new { observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Lo estoy pensando.") } });
        borrador.EnsureSuccessStatusCode();

        var envio = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/festivales/{sinEnviar}/enviar-a-revision", new { });

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
    }

    private static async Task<int> CrearFestivalAsync(HttpClient organizacion, int organizacionId, string nombre)
    {
        var creado = await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales",
            new { nombre, descripcion = "Doce dias", nivelCobertura = "nacional" });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        using var cuerpo = JsonDocument.Parse(await creado.Content.ReadAsStringAsync());
        return int.Parse(cuerpo.RootElement.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task La_Tarjeta_Del_Panel_Cuenta_Solo_Los_Cambios_Sin_Atender()
    {
        var (organizacion, festivalId, organizacionId) = await FestivalEnRevisionAsync("cuenta", "1300000014");
        var funcionario = await FuncionarioAsync();
        await funcionario.PostAsJsonAsync($"/api/v1/institucional/festivales/{festivalId}/revision/enviar", new
        {
            observaciones = new[]
            {
                Nota("festival.nombre", "Nombre del festival", "Uno."),
                Nota("festival.periodicidad", "Periodicidad", "Dos."),
                Nota("festival.descripcion", "Descripción del festival", "Tres."),
            },
        });

        Assert.Equal(3, await CambiosPedidosAsync(organizacion, organizacionId, festivalId));

        var pedidos = await LeerAsync(await organizacion.GetAsync(
            new Uri($"/api/v1/externo/festivales/{festivalId}/cambios-pedidos", UriKind.Relative)));
        await ExternaAsync(organizacion, HttpMethod.Post,
            $"/api/v1/externo/cambios-pedidos/{pedidos.Observaciones[0].Id}/atender", new { atendida = true });

        Assert.Equal(2, await CambiosPedidosAsync(organizacion, organizacionId, festivalId));
    }

    private static async Task<int> CambiosPedidosAsync(HttpClient organizacion, int organizacionId, int festivalId)
    {
        var respuesta = await organizacion.GetAsync(
            new Uri($"/api/v1/externo/organizaciones/{organizacionId}/festivales", UriKind.Relative));
        respuesta.EnsureSuccessStatusCode();
        using var lista = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var tarjeta = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == festivalId.ToString(CultureInfo.InvariantCulture));
        return tarjeta.GetProperty("cambiosPedidos").GetInt32();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // Las puertas
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Sin_Sesion_Institucional_No_Se_Escribe_Ni_Se_Lee_La_Revision()
    {
        var (organizacion, festivalId, _) = await FestivalEnRevisionAsync("puerta", "1300000015");

        // LA ORGANIZACION TIENE SESION —la externa— y aun asi no entra por la puerta institucional:
        // son dos autenticaciones distintas y esta ruta exige rol interno.
        var lectura = await organizacion.GetAsync(
            new Uri($"/api/v1/institucional/festivales/{festivalId}/revision", UriKind.Relative));
        Assert.True(lectura.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"La lectura institucional contesto {lectura.StatusCode}");

        var anonimo = _factory.CreateClient();
        var envio = await anonimo.PostAsJsonAsync(
            $"/api/v1/institucional/festivales/{festivalId}/revision/enviar",
            new { observaciones = new[] { Nota("festival.nombre", "Nombre del festival", "Cambia.") } });
        Assert.True(envio.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"El envio anonimo contesto {envio.StatusCode}");
    }

}
