using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly int[] UnCatalogo = [1];

    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Festivales, int Versiones, int Propuestas, int Solicitudes)> CapturarEstadoCoincidenciasAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festivales = await db.FestivalRecords.AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => $"{item.Id}|{item.StatusCode}|{item.OrganizacionPrincipalId}")
            .ToListAsync();
        return (
            string.Join(';', festivales),
            await db.VersionesFestival.CountAsync(),
            await db.PropuestasDeCambio.CountAsync(),
            await db.RecordLinkRequests.CountAsync());
    }

    private async Task LoginAsWebmasterAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ExternalRegisterResponse> RegisterAndVerifyExternalPersonAsync(string email)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Persona Sesion Externa",
            NumeroDocumento = "1020304051",
            Phone = "3000000000",
            Email = email,
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registered = await registerResponse.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(registered);
        return registered;
    }

    private static async Task LoginExternalAsync(HttpClient client, string email, string password = "ClaveExterna123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> GetExternalCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/externo/organizaciones/csrf");
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token!.RequestToken));
        return token.RequestToken;
    }

    /// <summary>
    /// Deja la organizacion QUE LA CUENTA YA TIENE con los datos que pide la prueba, y la devuelve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ANTES CREABA UNA SEGUNDA. El cuerpo iba a <c>POST /api/v1/externo/organizaciones/</c>, que
    /// existia para eso. El 28 de agosto de 2026 Se define la regla contraria: «un correo de
    /// una organizacion no puede tener muchas organizaciones, es decir un correo debe estar atado a
    /// una sola organizacion». Con la regla puesta, once pruebas de este fichero se caian antes de
    /// medir nada, porque su tercera linea creaba la segunda organizacion de la cuenta.
    /// </para>
    /// <para>
    /// SE MANTIENE LA FIRMA —y por eso las once llamadas no cambian— pero por dentro lee la
    /// organizacion de la cuenta y le ajusta el nombre, el correo, la identificacion y el territorio
    /// por la ruta del perfil, que es la que usa la pantalla. El escenario resultante es el mismo con
    /// una diferencia: hay UNA organizacion y no dos.
    /// </para>
    /// <para>
    /// EL CUERPO SE LEE POR REFLEXION porque las once llamadas pasan objetos anonimos con formas
    /// distintas —unas traen territorio, otras no—. Un cambio de nombre en esos objetos deja de
    /// mapear en silencio, asi que el ajuste comprueba despues que el nombre quedo puesto.
    /// </para>
    /// </remarks>
    private static async Task<HttpResponseMessage> CreateExternalOrganizationAsync(HttpClient client, string csrfToken, object request)
    {
        string? Campo(string nombre) =>
            request.GetType().GetProperty(nombre)?.GetValue(request)?.ToString();

        var mias = await client.GetFromJsonAsync<List<OrganizacionResumen>>("/api/v1/externo/organizaciones/mis");
        Assert.NotNull(mias);
        Assert.True(mias!.Count > 0, "La cuenta tendria que haber nacido con su organizacion.");
        var id = int.Parse(mias[0].Id, CultureInfo.InvariantCulture);

        var mensaje = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/organizaciones/{id}/perfil")
        {
            Content = JsonContent.Create(new
            {
                nombre = Campo("name") ?? "Organizacion de prueba",
                nombreLegal = (string?)null,
                numeroIdentificacion = Campo("identificationNumber"),
                descripcion = (string?)null,
                correoContacto = Campo("contactEmail") ?? $"contacto.{id}@organizacion.test",
                telefonoContacto = (string?)null,
                sitioWeb = (string?)null,
                facebook = (string?)null,
                instagram = (string?)null,
                otroEnlace = (string?)null,
                direccion = (string?)null,
                codigoDepartamentoSede = Campo("headquartersDepartmentCode")
                    ?? Campo("departmentCode")
                    ?? "05",
                codigoMunicipioSede = Campo("headquartersMunicipalityCode")
                    ?? Campo("municipalityCode")
                    ?? "05001",
            })
        };
        mensaje.Headers.Add("X-CSRF-TOKEN", csrfToken);
        var ajuste = await client.SendAsync(mensaje);
        if (!ajuste.IsSuccessStatusCode) return ajuste;

        return await client.GetAsync($"/api/v1/externo/organizaciones/{id}");
    }

    /// <summary>Lo que devuelve <c>GET /api/v1/externo/organizaciones/mis</c>: id y nombre.</summary>
    private sealed record OrganizacionResumen(string Id, string Nombre);

    private static Task<HttpResponseMessage> CreateDraftFestivalAsync(HttpClient client, int organizacionId, string csrfToken, object request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/organizaciones/{organizacionId}/festivales")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(message);
    }

    private static Task<HttpResponseMessage> SendFestivalToReviewAsync(HttpClient client, int festivalId, string csrfToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision")
        {
            Content = JsonContent.Create(new { })
        };
        message.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(message);
    }

    private static async Task<string> GetInstitutionalCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/institucional/festivales/csrf");
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token!.RequestToken));
        return token.RequestToken;
    }

    private static Task<HttpResponseMessage> DecideFestivalAsync(HttpClient client, int festivalId, string csrfToken, object request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festivalId}/decisiones")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(message);
    }

    private static async Task<string> GetInstitutionalProposalCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/institucional/propuestas-cambio-festival/csrf");
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>();
        Assert.NotNull(token);
        return token!.RequestToken;
    }

    [Fact]
    public async Task Health_Endpoints_ReturnOk()
    {
        var live = await _client.GetAsync("/health/live");
        var ready = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.True(live.Headers.Contains("X-Correlation-ID"));
        Assert.True(ready.Headers.Contains("X-Correlation-ID"));
        Assert.Equal("nosniff", live.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", ready.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Participation_Create_And_Get_ByReference_Works()
    {
        var request = new ParticipationSubmissionRequest
        {
            ActorType = "individual",
            ActorTypeLabel = "Registro individual",
            ActorName = "Prueba Integracion",
            Email = "prueba@example.com",
            Phone = "3000000000",
            Department = "Bogota D.C.",
            Municipality = "Bogota D.C.",
            MusicalFields = "Formacion",
            Description = "Registro de prueba",
            Contribution = "Aporte de prueba",
            Consent = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/participaciones", request);
        createResponse.EnsureSuccessStatusCode();

        var createdPayload = await createResponse.Content.ReadFromJsonAsync<ParticipationSubmissionResponse>();
        Assert.NotNull(createdPayload);
        Assert.False(string.IsNullOrWhiteSpace(createdPayload!.Reference));
        Assert.False(string.IsNullOrWhiteSpace(createdPayload.ExternalSyncStatus));

        var readResponse = await _client.GetAsync($"/api/v1/participaciones/{createdPayload.Reference}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
    }

    [Fact]
    public async Task Participation_List_ReturnsPagedItems()
    {
        var request = new ParticipationSubmissionRequest
        {
            ActorType = "organization",
            ActorName = "Colectivo Test",
            Email = "colectivo@example.com",
            Phone = "3000000000",
            Department = "Antioquia",
            Municipality = "Medellin",
            MusicalFields = "Formacion",
            Description = "Registro de prueba",
            Contribution = "Aporte de prueba",
            Consent = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/participaciones", request);
        createResponse.EnsureSuccessStatusCode();

        await LoginAsWebmasterAsync();

        var listResponse = await _client.GetAsync("/api/v1/participaciones?limit=10&offset=0");
        if (!listResponse.IsSuccessStatusCode)
        {
            var errorPayload = await listResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Unexpected status {listResponse.StatusCode}: {errorPayload}");
        }

        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ParticipationSubmissionSummaryDto>>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Items);
    }

    // Regresion de H-A06-001: el listado de participacion devuelve nombre y correo de ciudadanos
    // y permite buscar por correo. Estuvo accesible sin autenticacion.
    [Fact]
    public async Task Participation_List_RequiresInstitutionalAuthentication()
    {
        var request = new ParticipationSubmissionRequest
        {
            ActorType = "organization",
            ActorName = "Colectivo Habeas Data",
            Email = "habeas.data@example.com",
            Phone = "3000000000",
            Department = "Antioquia",
            Municipality = "Medellin",
            MusicalFields = "Formacion",
            Description = "Registro de prueba",
            Contribution = "Aporte de prueba",
            Consent = true
        };

        // El envio ciudadano es publico por diseno: no debe exigir autenticacion.
        var createResponse = await _client.PostAsJsonAsync("/api/v1/participaciones", request);
        createResponse.EnsureSuccessStatusCode();

        // El listado expone datos personales: nunca sin autenticar.
        var anonymousList = await _client.GetAsync("/api/v1/participaciones?limit=10&offset=0");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);

        // La busqueda por correo convertia el endpoint en un directorio consultable.
        var anonymousSearch = await _client.GetAsync("/api/v1/participaciones?q=habeas.data%40example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSearch.StatusCode);

        // Con sesion institucional si responde.
        await LoginAsWebmasterAsync();
        var authorizedList = await _client.GetAsync("/api/v1/participaciones?limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, authorizedList.StatusCode);
    }

    [Fact]
    public async Task Admin_Data_Schema_ReturnsFieldDefinitions()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.GetAsync("/api/v1/admin/data/schema");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(payload);
        Assert.True(payload!.ContainsKey("festivals"));
        Assert.True(payload.ContainsKey("participation"));
        Assert.True(payload.ContainsKey("divipola"));
    }

    /// <summary>
    /// El alta externa es UN SOLO ACTO: cuenta activa, organizacion y persona responsable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LAS CUATRO FILAS SE COMPRUEBAN CONTRA LA BASE, no contra la respuesta. La respuesta la
    /// escribe el mismo manejador que se esta probando, asi que confirmar con ella lo que ella
    /// misma dice no comprueba nada: un manejador que devolviera el DTO sin guardar la fila del
    /// responsable pasaria en verde. Es justo el defecto que este cambio viene a cerrar
    /// —EntidadesResponsable con cero filas frente a diecisiete entidades— y no se puede vigilar
    /// desde el sitio donde se produjo.
    /// </para>
    /// <para>
    /// LA CUENTA NACE ACTIVA. Antes nacia con IsActive=false y esperaba un codigo que ningun
    /// remitente enviaba. La prueba de que ya no espera nada es que se puede iniciar sesion sin
    /// ningun paso intermedio.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task External_Registration_Creates_Account_Organization_And_Responsible_In_One_Act()
    {
        const string email = "alta.completa@example.com";
        var request = new ExternalRegisterRequest
        {
            OrganizationName = "Fundación Sonidos del Río",
            FullName = "Persona Responsable Test",
            // Se teclea como se lee en el documento, con puntos.
            NumeroDocumento = "1.020.304.052",
            Phone = "3000000000",
            Email = email,
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        };

        var response = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(payload);
        Assert.Equal("activo", payload!.AccountStatus);
        Assert.Equal("Fundación Sonidos del Río", payload.OrganizationName);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

            var usuario = db.Users.Single(item => item.Email == email);
            Assert.True(usuario.IsActive);
            Assert.Equal("externo", usuario.AccessChannel);

            var entidad = db.EntityProfiles.Single(item => item.Id == int.Parse(payload.OrganizationId, CultureInfo.InvariantCulture));
            Assert.Equal("organizacion", entidad.EntityType);

            // UN SOLO CORREO, Y ESTA LINEA ES LA QUE LO SOSTIENE. El que se teclea en el alta es a
            // la vez el institucional de la organizacion y el de acceso de la cuenta. El dia que
            // alguien vuelva a separarlos —dos campos, dos valores— esta igualdad se rompe.
            Assert.Equal(email, entidad.ContactEmail);

            // La organización no declara un alcance territorial. Conserva únicamente la sede
            // registral que la persona eligió contra DIVIPOLA durante el alta.
            Assert.Null(entidad.CoverageLevel);
            Assert.Null(entidad.DepartmentCode);
            Assert.Null(entidad.MunicipalityCode);
            Assert.Equal("05", entidad.HeadquartersDepartmentCode);
            Assert.Equal("05001", entidad.HeadquartersMunicipalityCode);

            var vinculo = db.UserEntities.Single(item => item.UserId == usuario.Id && item.EntityId == entidad.Id);
            Assert.Equal("administrador", vinculo.EntityRole);
            Assert.True(vinculo.IsActive);

            var responsable = db.EntidadesResponsable.Single(item => item.IdEntidad == entidad.Id);
            Assert.Equal("Persona Responsable Test", responsable.ResponsableNombre);
            // Guardada solo con digitos: quien la busque despues no tiene que adivinar el formato.
            Assert.Equal("1020304052", responsable.ResponsableNumeroDocumento);
            Assert.Equal("CC", responsable.ResponsableTipoDocumento);
            // Ley 1581: la autorizacion queda escrita, no se presume.
            Assert.True(responsable.ResponsableAutorizacionDatos);
        }

        // Sin paso intermedio ninguno: se entra con correo y contrasena.
        var login = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        login.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Un alta rechazada no deja cuenta: se valida TODO antes de escribir la primera fila.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LO QUE ESTA PRUEBA VIGILA, Y LO QUE NO. Vigila el orden: la validacion —incluida la de la
    /// organizacion, que consulta DIVIPOLA— corre entera antes de que se escriba el usuario, asi
    /// que un rechazo no puede dejar media cuenta. Moverla despues del primer guardado la pone en
    /// rojo.
    /// </para>
    /// <para>
    /// NO VIGILA LA TRANSACCION, y conviene decirlo en vez de dejar que el nombre lo sugiera. La
    /// primera version se llamaba «Failed_External_Registration_Leaves_No_Account_Behind» y su
    /// comentario afirmaba provocar un corte «DESPUES de que el usuario ya se ha escrito». Era
    /// falso: el municipio inexistente lo atrapa la validacion, de modo que no se escribe nada y
    /// la transaccion no llega a hacer falta. Se descubrio sembrando un mutante que la quitaba: la
    /// prueba siguio en verde.
    /// </para>
    /// <para>
    /// EL HUECO QUE QUEDA. La transaccion existe para lo que la validacion no puede ver —una
    /// restriccion de la base, un corte de red entre dos guardados—, y esta suite no sabe
    /// provocar ninguna de las dos cosas sin falsear el motor. Queda escrito como hueco conocido,
    /// no como algo cubierto.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Rejected_External_Registration_Writes_Nothing_Because_Validation_Runs_First()
    {
        const string primero = "nit.primero@example.com";
        const string segundo = "nit.repetido@example.com";
        // NIT PROPIO DE ESTA PRUEBA. El primero que puse era el mismo que usa
        // External_User_Can_Create_Organization_...; comparten la base del arnes, asi que la
        // segunda en correr recibia el rechazo por duplicado que la otra venia a provocar.
        const string nit = "900999888-1";

        ExternalRegisterRequest Peticion(string correo) => new()
        {
            OrganizationName = "Organización con NIT",
            OrganizationIdentificationNumber = nit,
            FullName = "Persona Responsable",
            NumeroDocumento = "1020304099",
            Phone = "3000000000",
            Email = correo,
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        };

        (await _client.PostAsJsonAsync("/api/v1/externo/auth/register", Peticion(primero)))
            .EnsureSuccessStatusCode();

        // EL NIT REPETIDO SE COMPRUEBA CONTRA LA BASE, no en memoria: es una consulta a Entidades
        // dentro de la validacion. Sirve justo para lo que esta prueba afirma —que la validacion
        // llega hasta el final, base incluida, ANTES de escribir la primera fila—, y no lo serviria
        // un campo mal formado, que se resuelve sin salir del proceso.
        var repetido = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", Peticion(segundo));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.False(db.Users.Any(item => item.Email == segundo));
    }

    /// <summary>
    /// La cedula es obligatoria, y el correo tiene que ser uno que la base acepte.
    /// </summary>
    /// <remarks>
    /// EL CORREO SIN PUNTO ES EL CASO QUE IMPORTA. `dbo.Usuarios` lleva
    /// CK_Usuarios_CorreoElectronico_Formato CHECK (CorreoElectronico LIKE '%_@_%._%'), y
    /// MailAddress por su cuenta acepta «juan@gmail»: la errata mas comun del mundo —comerse el
    /// «.com»— pasaba el navegador, pasaba el API y la tumbaba la base al guardar. Un 500 con
    /// traza en vez de «el correo no es valido».
    /// </remarks>
    [Theory]
    [InlineData("", "", "numeroDocumento")]
    [InlineData("12", "12", "numeroDocumento")]
    [InlineData("persona@ejemplo", "1020304051", "email")]
    public async Task External_Registration_Rejects_Bad_Identity_Fields(string correoOCedula, string cedula, string campoEsperado)
    {
        var esCorreo = correoOCedula.Contains('@', StringComparison.Ordinal);
        var response = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de prueba",
            FullName = "Persona Responsable",
            NumeroDocumento = cedula,
            Phone = "3000000000",
            Email = esCorreo ? correoOCedula : $"rechazo.{cedula}@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(campoEsperado, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task External_Verified_User_Can_Login_Read_Session_And_Logout()
    {
        const string email = "sesion.externa@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        loginResponse.EnsureSuccessStatusCode();
        var session = await loginResponse.Content.ReadFromJsonAsync<ExternalSessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(email, session!.Email);
        Assert.Equal("activo", session.AccountStatus);

        var meResponse = await _client.GetAsync("/api/v1/externo/auth/me");
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<ExternalSessionResponse>();
        Assert.NotNull(me);
        Assert.Equal(session.UserId, me!.UserId);

        // LA SESION TRAE LA ORGANIZACION, y sin esto el sitio publico no puede pintar el panel:
        // sabria que hay alguien dentro pero no de que organizacion. Desde que el alta es un solo
        // acto, toda cuenta externa nace con una, asi que una sesion con la lista vacia es una
        // cuenta rota, no un caso legitimo.
        Assert.Single(me.Organizations);
        Assert.Equal("Organizacion de prueba", me.Organizations[0].Name);
        Assert.Equal("administrador", me.Organizations[0].Role);
        Assert.Equal(session.Organizations[0].Id, me.Organizations[0].Id);

        var logoutResponse = await _client.PostAsync("/api/v1/externo/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        var afterLogout = await _client.GetAsync("/api/v1/externo/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    /// <summary>
    /// El primer ingreso se declara una vez, y solo en la respuesta del ingreso.
    /// </summary>
    /// <remarks>
    /// LO SOSTIENE EL §15.2 DEL PLAN: «correo confirmado -> primer login -> bienvenida ->
    /// informacion faltante -> Completar organizacion». Para dar esa bienvenida hay que saber que
    /// es la primera vez, y el dato ya estaba: <c>Usuarios.LastLoginAt</c>. Lo unico que hacia
    /// falta era mirarlo ANTES de sobrescribirlo con la hora de ahora.
    ///
    /// <c>/me</c> lo devuelve siempre en falso a proposito: para entonces la cuenta ya entro, y
    /// una bienvenida que reaparece en cada recarga deja de ser una bienvenida.
    /// </remarks>
    [Fact]
    public async Task External_First_Login_Is_Announced_Once_And_Never_Again()
    {
        const string email = "primer.ingreso@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);

        var primero = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        primero.EnsureSuccessStatusCode();
        var sesionInicial = await primero.Content.ReadFromJsonAsync<ExternalSessionResponse>();
        Assert.NotNull(sesionInicial);
        Assert.True(sesionInicial!.EsPrimerIngreso);

        // MUTANTE QUE MATA: leer LastLoginAt despues de asignarlo. Daria siempre falso y la
        // bienvenida no se enseñaria nunca.
        var me = await _client.GetAsync("/api/v1/externo/auth/me");
        me.EnsureSuccessStatusCode();
        var sesionDeMe = await me.Content.ReadFromJsonAsync<ExternalSessionResponse>();
        Assert.NotNull(sesionDeMe);
        Assert.False(sesionDeMe!.EsPrimerIngreso);

        await _client.PostAsync("/api/v1/externo/auth/logout", null);

        // Y AL VOLVER A ENTRAR, YA NO ES LA PRIMERA VEZ.
        var segundo = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        segundo.EnsureSuccessStatusCode();
        var sesionPosterior = await segundo.Content.ReadFromJsonAsync<ExternalSessionResponse>();
        Assert.NotNull(sesionPosterior);
        Assert.False(sesionPosterior!.EsPrimerIngreso);
    }

    [Fact]
    public async Task External_Login_Rejects_Deactivated_Account_And_Invalid_Credentials()
    {
        const string email = "no.verificada@example.com";
        var request = new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Persona Sin Verificar",
            NumeroDocumento = "1020304054",
            Phone = "3000000000",
            Email = email,
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", request);
        registerResponse.EnsureSuccessStatusCode();

        // LA CUENTA NACE ACTIVA: esta prueba comprobaba antes que una cuenta «sin verificar» no
        // entraba, y ese estado ya no existe. Lo que si tiene que seguir cerrando la puerta es que
        // alguien la haya DESACTIVADO, que es la unica forma de retirarle el acceso a una
        // organizacion. Se apaga por la base a proposito: no hay ruta que lo haga, y la prueba
        // vigila la guarda del ingreso, no la ruta que no existe.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var cuenta = db.Users.Single(item => item.Email == email);
            cuenta.IsActive = false;
            db.SaveChanges();
        }

        var desactivada = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = request.Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, desactivada.StatusCode);


        var invalidLogin = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveIncorrecta123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
    }

    [Fact]
    public async Task External_Session_Cannot_Access_Institutional_Endpoints_Or_Login()
    {
        const string email = "aislamiento.externo@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        loginResponse.EnsureSuccessStatusCode();

        var institutionalUsers = await _client.GetAsync("/api/v1/admin/auth/users");
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalUsers.StatusCode);

        var institutionalLogin = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = email,
            Password = "ClaveExterna123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalLogin.StatusCode);
    }

    [Fact]
    public async Task Institutional_Session_Is_Not_An_External_Session()
    {
        await LoginAsWebmasterAsync();

        var externalMe = await _client.GetAsync("/api/v1/externo/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, externalMe.StatusCode);
    }

    [Fact]
    public async Task External_User_Can_Create_Organization_With_Initial_Administrator_And_Csrf()
    {
        const string email = "admin.organizacion@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);

        var response = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Fundacion Organizacion Test",
            identificationNumber = "900123456-7",
            contactEmail = "contacto@organizacion.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001",
            administratorUserId = 999
        });
        // YA NO ES UN ALTA sino un ajuste de la organizacion con la que nacio la cuenta, asi que la
        // respuesta es 200 y no 201. Lo que la prueba comprueba —quien la administra y que quedo en
        // la bitacora— no cambia.
        response.EnsureSuccessStatusCode();

        var organization = await response.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);
        Assert.Equal("administrador", organization!.AdministratorRole);
        // «ACTIVA» PORQUE EL ARNES ABRE EL ENLACE DE CONFIRMACION, que es lo que hace una persona
        // nada más registrarse. El estado de nacimiento —«pendiente_de_confirmacion»— y lo que
        // bloquea lo comprueba LaOrganizacionYSusProcesosTests, que apaga ese gesto del arnés.
        Assert.Equal("activa", organization.Status);
        Assert.Equal("900123456-7", organization.IdentificationNumber);

        var readResponse = await _client.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}");
        readResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task External_Organization_Creation_Rejects_Missing_Session_And_Csrf()
    {
        var withoutSession = await _client.PostAsJsonAsync("/api/v1/externo/organizaciones/", new
        {
            name = "Organizacion Sin Sesion",
            contactEmail = "contacto@sin-sesion.test",
            coverageLevel = "nacional"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);

        const string email = "csrf.organizacion@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var withoutCsrf = await _client.PostAsJsonAsync("/api/v1/externo/organizaciones/", new
        {
            name = "Organizacion Sin Token",
            contactEmail = "contacto@sin-token.test",
            coverageLevel = "nacional"
        });
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);
    }

    [Fact]
    public async Task Institutional_Or_Another_External_User_Cannot_Administer_External_Organization()
    {
        const string ownerEmail = "propietaria.organizacion@example.com";
        await RegisterAndVerifyExternalPersonAsync(ownerEmail);
        await LoginExternalAsync(_client, ownerEmail);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);
        var createResponse = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Organizacion Protegida",
            contactEmail = "contacto@protegida.test",
            coverageLevel = "nacional"
        });
        createResponse.EnsureSuccessStatusCode();
        var organization = await createResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);

        var institutionalClient = _factory.CreateClient();
        var institutionalLogin = await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        institutionalLogin.EnsureSuccessStatusCode();
        var institutionalRead = await institutionalClient.GetAsync($"/api/v1/externo/organizaciones/{organization!.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalRead.StatusCode);

        var otherClient = _factory.CreateClient();
        var otherRegister = await otherClient.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Otra Persona Externa",
            NumeroDocumento = "1020304055",
            Phone = "3000000000",
            Email = "tercera.organizacion@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        otherRegister.EnsureSuccessStatusCode();
        var otherAccount = await otherRegister.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        await LoginExternalAsync(otherClient, "tercera.organizacion@example.com");

        var otherRead = await otherClient.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherRead.StatusCode);
    }

    [Fact]
    public async Task External_Administrator_Can_Create_Private_Festival_Draft_With_Catalogs_And_Audit()
    {
        const string email = "festival.borrador@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Fundación Festival Borrador",
            contactEmail = "contacto@festival-borrador.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);

        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), csrfToken, new
        {
            nombre = "Festival Borrador Protegido",
            descripcion = "Identidad estable del Festival",
            periodicidad = "anual",
            correoContacto = "festival@borrador.test",
            nivelCobertura = "municipal",
            codigoDepartamento = "05",
            codigoMunicipio = "05001",
            practicasMusicalesIds = UnCatalogo,
            territoriosSonorosIds = UnCatalogo,
            personaId = 999,
            administradorId = 999
        });
        Assert.True(festivalResponse.StatusCode == HttpStatusCode.Created, await festivalResponse.Content.ReadAsStringAsync());
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.NotNull(festival);
        Assert.Equal("Borrador", festival!.Estado);
        Assert.Equal(organization.Id, festival.OrganizacionPrincipalId);
        Assert.Single(festival.PracticasMusicales);
        Assert.Single(festival.TerritoriosSonoros);

        var ownFestivals = await _client.GetFromJsonAsync<List<FestivalBorradorDto>>($"/api/v1/externo/organizaciones/{organization.Id}/festivales");
        Assert.Contains(ownFestivals!, item => item.Id == festival.Id && item.Estado == "Borrador");

        var publicFestivals = await _client.GetFromJsonAsync<PagedResponse<FestivalPublicoDto>>("/api/v1/publico/festivales?limit=100&offset=0");
        Assert.DoesNotContain(publicFestivals!.Items, item => item.Nombre == "Festival Borrador Protegido");

        var mapResponse = await _client.GetFromJsonAsync<MapSummaryResponseDto>("/api/v1/mapa/resumen?layer=Festivales");
        var antioquia = Assert.Single(mapResponse!.Items, item => item.Department == "Antioquia");
        Assert.True(antioquia.Festivals >= 1);
    }

    [Fact]
    public async Task Festival_Draft_Creation_Rejects_Missing_Sessions_Csrf_And_Organization_Authorization()
    {
        var withoutSession = await _client.PostAsJsonAsync("/api/v1/externo/organizaciones/1/festivales", new { nombre = "Sin sesión" });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);

        const string ownerEmail = "festival.propietaria@example.com";
        await RegisterAndVerifyExternalPersonAsync(ownerEmail);
        await LoginExternalAsync(_client, ownerEmail);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Organización Festival Protegida",
            contactEmail = "contacto@festival-protegido.test",
            coverageLevel = "nacional"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);

        var withoutCsrf = await _client.PostAsJsonAsync($"/api/v1/externo/organizaciones/{organization!.Id}/festivales", new
        {
            nombre = "Festival sin token", nivelCobertura = "nacional"
        });
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);

        var institutionalClient = _factory.CreateClient();
        await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" });
        var institutionalCsrf = await institutionalClient.GetAsync("/api/v1/externo/organizaciones/csrf");
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalCsrf.StatusCode);
        var institutionalCreate = await institutionalClient.PostAsJsonAsync($"/api/v1/externo/organizaciones/{organization.Id}/festivales", new { nombre = "Festival institucional", nivelCobertura = "nacional" });
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalCreate.StatusCode);

        var otherClient = _factory.CreateClient();
        var registration = await otherClient.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Otra persona",
            NumeroDocumento = "1020304056",
            Phone = "3000000000",
            Email = "festival.otra@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        var otherAccount = await registration.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        await LoginExternalAsync(otherClient, "festival.otra@example.com");
        var otherCsrf = await GetExternalCsrfTokenAsync(otherClient);
        var otherCreate = await CreateDraftFestivalAsync(otherClient, int.Parse(organization.Id), otherCsrf, new { nombre = "Festival ajeno", nivelCobertura = "nacional" });
        Assert.Equal(HttpStatusCode.Forbidden, otherCreate.StatusCode);
    }

    [Fact]
    public async Task Crear_Un_Festival_Cierra_Su_Borrador_Temporal_Y_El_Siguiente_Registro_Empieza_Limpio()
    {
        var email = $"dos.festivales.{Guid.NewGuid():N}@example.com";
        var cuenta = await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var organizacionId = int.Parse(cuenta.OrganizationId, CultureInfo.InvariantCulture);

        var primerBorrador = await _client.GetFromJsonAsync<BorradorProcesoDto>(
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales/borrador");
        Assert.NotNull(primerBorrador);
        Assert.Equal("borrador", primerBorrador!.Estado);

        var primeraAlta = await CreateDraftFestivalAsync(
            _client,
            organizacionId,
            await GetExternalCsrfTokenAsync(_client),
            new
            {
                borradorProcesoId = primerBorrador.Id,
                nombre = "Primer Festival consecutivo",
                nivelCobertura = "nacional"
            });
        Assert.Equal(HttpStatusCode.Created, primeraAlta.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var cerrado = await db.BorradoresDeProceso.AsNoTracking()
                .SingleAsync(item => item.Id == long.Parse(primerBorrador.Id, CultureInfo.InvariantCulture));
            Assert.Equal("cerrado", cerrado.Estado);
        }

        var segundoBorrador = await _client.GetFromJsonAsync<BorradorProcesoDto>(
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales/borrador");
        Assert.NotNull(segundoBorrador);
        Assert.NotEqual(primerBorrador.Id, segundoBorrador!.Id);
        Assert.Equal("borrador", segundoBorrador.Estado);
        Assert.Equal("{}", segundoBorrador.DatosJson);

        var segundaAlta = await CreateDraftFestivalAsync(
            _client,
            organizacionId,
            await GetExternalCsrfTokenAsync(_client),
            new
            {
                borradorProcesoId = segundoBorrador.Id,
                nombre = "Segundo Festival consecutivo",
                nivelCobertura = "nacional"
            });
        Assert.Equal(HttpStatusCode.Created, segundaAlta.StatusCode);
    }

    [Fact]
    public async Task External_Administrator_Can_Read_Only_Eligible_Historical_Festival_Matches_Without_Mutations()
    {
        const string email = "coincidencias.festival@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, await GetExternalCsrfTokenAsync(_client), new
        {
            name = "Fundación Musical del Tolima",
            contactEmail = "contacto@fundacion-tolima.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);
        var organizationId = int.Parse(organization!.Id);

        FestivalRow coincidenciaNominal;
        FestivalRow coincidenciaHistorica;
        FestivalRow conResponsable;
        FestivalRow borrador;
        FestivalRow enRevision;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

            // DESDE, «elegible» ya no es «no tiene organizacion responsable» sino
            // «hoy lo tiene la institucion»: ningun festival se queda sin nadie que responda por
            // el, y reclamarlo es un traspaso. Por eso los historicos se siembran a nombre de la
            // entidad institucional en vez de dejarlos en NULL.
            var idInstitucional = db.EntityProfiles.Single(item => item.IsInstitutional).Id;

            coincidenciaNominal = new FestivalRow
            {
                Name = "Festival de la Montaña",
                OrganizacionPrincipalId = idInstitucional,
                Description = "Registro histórico para coincidencia nominal.",
                OrganizerDisplayName = "FUNDACION MUSICAL DEL TOLIMA",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Publicado",
                CreatedAt = DateTime.UtcNow
            };
            coincidenciaHistorica = new FestivalRow
            {
                Name = "Festival con referencia histórica",
                OrganizacionPrincipalId = idInstitucional,
                Description = "Registro histórico con evidencia contextual.",
                OrganizerDisplayName = "Nombre histórico diferente",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Publicado",
                CreatedAt = DateTime.UtcNow
            };
            conResponsable = new FestivalRow
            {
                Name = "Festival ya administrado",
                OrganizerDisplayName = "Fundación Musical del Tolima",
                OrganizacionPrincipalId = organizationId,
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Publicado",
                CreatedAt = DateTime.UtcNow
            };
            borrador = new FestivalRow
            {
                Name = "Festival borrador no elegible",
                OrganizacionPrincipalId = idInstitucional,
                OrganizerDisplayName = "Fundación Musical del Tolima",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Borrador",
                CreatedAt = DateTime.UtcNow
            };
            enRevision = new FestivalRow
            {
                Name = "Festival en revisión no elegible",
                OrganizacionPrincipalId = idInstitucional,
                OrganizerDisplayName = "Fundación Musical del Tolima",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "EnRevision",
                CreatedAt = DateTime.UtcNow
            };
            db.FestivalRecords.AddRange(coincidenciaNominal, coincidenciaHistorica, conResponsable, borrador, enRevision);
            db.SaveChanges();
            db.ReferenciasHistoricasFestival.Add(new ReferenciaHistoricaFestivalRow
            {
                OrganizacionId = organizationId,
                FestivalId = coincidenciaHistorica.Id,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var before = await CapturarEstadoCoincidenciasAsync();
        var matchesResponse = await _client.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}/festivales/coincidencias");
        matchesResponse.EnsureSuccessStatusCode();
        var matches = await matchesResponse.Content.ReadFromJsonAsync<List<CoincidenciaFestivalHistoricoDto>>();
        Assert.NotNull(matches);
        Assert.Contains(matches!, item => item.FestivalId == coincidenciaNominal.Id.ToString() && item.TipoCoincidencia == "NominalYTerritorial"
            && item.Evidencias.Any(evidencia => evidencia.Contains("organizador histórico", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(matches, item => item.FestivalId == coincidenciaHistorica.Id.ToString() && item.TipoCoincidencia == "EvidenciaHistoricaTerritorial"
            && item.Evidencias.Any(evidencia => evidencia.Contains("referencia histórica", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(matches, item => item.FestivalId == conResponsable.Id.ToString() || item.FestivalId == borrador.Id.ToString() || item.FestivalId == enRevision.Id.ToString());
        Assert.All(matches, item =>
        {
            Assert.DoesNotContain("correo", JsonSerializer.Serialize(item), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("auditoria", JsonSerializer.Serialize(item), StringComparison.OrdinalIgnoreCase);
        });

        var after = await CapturarEstadoCoincidenciasAsync();
        Assert.Equal(before, after);

        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}/festivales/coincidencias")).StatusCode);

        var institutional = _factory.CreateClient();
        (await institutional.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await institutional.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}/festivales/coincidencias")).StatusCode);

        var externalOther = _factory.CreateClient();
        var otherRegister = await externalOther.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Persona no autorizada",
            NumeroDocumento = "1020304057",
            Phone = "3000000000",
            Email = "coincidencias.ajena@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        var otherAccount = await otherRegister.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(otherAccount);
        await LoginExternalAsync(externalOther, "coincidencias.ajena@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await externalOther.GetAsync($"/api/v1/externo/organizaciones/{organization.Id}/festivales/coincidencias")).StatusCode);
    }

    [Fact]
    public async Task External_Administrator_Can_Send_Valid_Festival_Draft_To_Review_Without_Publication()
    {
        const string email = "festival.revision@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Organización Festival Revisión",
            contactEmail = "contacto@festival-revision.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);
        var creationToken = await GetExternalCsrfTokenAsync(_client);
        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), creationToken, new
        {
            nombre = "Festival Enviado a Revisión",
            nivelCobertura = "municipal",
            codigoDepartamento = "05",
            codigoMunicipio = "05001"
        });
        festivalResponse.EnsureSuccessStatusCode();
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.NotNull(festival);

        var reviewToken = await GetExternalCsrfTokenAsync(_client);
        var reviewResponse = await SendFestivalToReviewAsync(_client, int.Parse(festival!.Id), reviewToken);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewed = await reviewResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.Equal("EnRevision", reviewed!.Estado);
        Assert.Equal(festival.Id, reviewed.Id);

        var duplicateToken = await GetExternalCsrfTokenAsync(_client);
        var duplicateResponse = await SendFestivalToReviewAsync(_client, int.Parse(festival.Id), duplicateToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var publicFestivals = await _client.GetFromJsonAsync<PagedResponse<FestivalPublicoDto>>("/api/v1/publico/festivales?limit=100&offset=0");
        Assert.DoesNotContain(publicFestivals!.Items, item => item.Nombre == "Festival Enviado a Revisión");
        var mapResponse = await _client.GetFromJsonAsync<MapSummaryResponseDto>("/api/v1/mapa/resumen?layer=Festivales");
        Assert.True(Assert.Single(mapResponse!.Items, item => item.Department == "Antioquia").Festivals >= 1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        // Estas tres lineas comprobaban antes que la bitacora guardaba
        // Action="FestivalEnviadoARevision" y "EnRevision" dentro de ValoresNuevos. Las dos
        // cosas son justamente las que la base real RECHAZA: CK_BitacoraAuditoria_Accion solo
        // admite trece verbos tecnicos y FK_Festivales_EstadosContenido solo los siete codigos
        // en minuscula. La prueba estaba en verde documentando el defecto, no protegiendo nada,
        // porque EF crea la tabla de la suite sin ese CHECK ni esa clave foranea.
        // Ahora se comprueba lo contrario: el verbo es tecnico, el estado es canonico, y el
        // nombre del evento sigue estando, en su sitio. Ver Pnmc059VocabularioEstadosTests.
        // La tabla en el filtro, por la misma razon que en Pnmc059VocabularioEstadosTests:
        // la clave de la bitacora es (tabla, registro) y nunca el registro solo.
        var audit = db.AuditLogs.Single(item =>
            item.TableName == "Festivales" && item.RecordId == festival.Id && item.Action == "actualizar");
        Assert.Contains("\"Estado\":\"borrador\"", audit.PreviousValuesJson);
        Assert.Contains("\"Estado\":\"en_revision\"", audit.NewValuesJson);
        Assert.Contains("\"Evento\":\"FestivalEnviadoARevision\"", audit.NewValuesJson);
        var notification = db.Notifications.Single(item => item.RecordId == festival.Id && item.EventType == "FestivalEnviadoARevision");
        Assert.Equal("enviada", notification.Status);
        Assert.Equal(email, notification.RecipientEmail);
        Assert.Equal("Tu Festival “Festival Enviado a Revisión” fue enviado a revisión. Recibirás una notificación cuando se publique o si se solicitan ajustes.", notification.Body);

        var avisosInstitucionales = db.Notifications
            .Where(item => item.RecordId == festival.Id && item.EventType == "FestivalRecibidoParaRevision")
            .OrderBy(item => item.RecipientEmail)
            .ToList();
        Assert.Equal(
            ["admin@pnmc.local", "gestor@pnmc.local", "test@pnmc.local"],
            avisosInstitucionales.Select(item => item.RecipientEmail));
        Assert.All(avisosInstitucionales, item =>
        {
            Assert.Equal("institutional", item.AccessScope);
            Assert.Equal("Festival pendiente de revisión", item.Title);
            Assert.Equal("La organización “Organización Festival Revisión” envió a revisión para publicación el Festival “Festival Enviado a Revisión”.", item.Body);
        });
    }

    /// <summary>
    /// B5. Renombrar una organizacion —o cambiarle la persona que responde— NO puede reescribir lo
    /// que dice su historial pasado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL DEFECTO, MEDIDO ANTES DE TOCAR NADA. <c>RevisionInstitucionalFestivalesEndpoints.cs</c>
    /// armaba la ficha de revision resolviendo el nombre de la organizacion por JOIN CONTRA EL
    /// PRESENTE, y a esa misma respuesta le colgaba el historial entero. De modo que el dia en que
    /// una organizacion se cambiara el nombre, TODAS sus entradas pasadas —las de hace dos anos
    /// tambien— empezaban a decir el nombre nuevo. Nadie lo ve: la pantalla sigue respondiendo 200
    /// y las filas siguen ahi. Es la clase de fallo que solo se nota cuando alguien pregunta por un
    /// expediente viejo y la respuesta ya no cuadra con el papel.
    /// </para>
    /// <para>
    /// POR QUE SE CAMBIAN LAS DOS COSAS Y NO UNA. Con solo renombrar la organizacion, una
    /// implementacion que copiara el nombre pero siguiera resolviendo el responsable por JOIN
    /// pasaria en verde. Se cambian las dos y se comprueban las dos.
    /// </para>
    /// <para>
    /// Y COMPRUEBA LAS DOS MITADES: que la instantanea SE ESCRIBIO con los valores de entonces —una
    /// implementacion que dejara los campos nulos tambien «no varia» al renombrar— y que despues
    /// del cambio SIGUE diciendo lo mismo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Review_History_Keeps_The_Organization_And_Responsible_As_They_Were()
    {
        const string email = "festival.instantanea.historial@example.com";
        const string nombreOriginal = "Corporacion Cultural Nombre Original";
        const string nombreRenombrado = "Corporacion Cultural Ya Renombrada";
        const string responsableOriginal = "Marta Quintero Ruiz";
        const string responsableNuevo = "Andres Salcedo Mena";

        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);

        var organizationResponse = await CreateExternalOrganizationAsync(_client, await GetExternalCsrfTokenAsync(_client), new
        {
            name = nombreOriginal,
            contactEmail = "contacto@nombre-original.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        var organizacionId = int.Parse(organization!.Id, CultureInfo.InvariantCulture);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            // LA FILA YA EXISTE, Y ESO ES LA NOTICIA. Antes habia que fabricarla a mano porque
            // ninguna ruta la escribia; desde que el alta es un solo acto, toda organizacion nace
            // con su responsable. Aqui solo se le pone el nombre que esta prueba necesita para
            // comprobar despues que el historial guardo el de ENTONCES y no el de hoy.
            var responsable = db.EntidadesResponsable.Single(item => item.IdEntidad == organizacionId);
            responsable.ResponsableNombre = responsableOriginal;
            responsable.ResponsableCorreo = "marta@nombre-original.test";
            db.SaveChanges();
        }

        var festivalResponse = await CreateDraftFestivalAsync(_client, organizacionId, await GetExternalCsrfTokenAsync(_client), new
        {
            nombre = "Festival con historial que no se reescribe",
            nivelCobertura = "municipal",
            codigoDepartamento = "05",
            codigoMunicipio = "05001"
        });
        festivalResponse.EnsureSuccessStatusCode();
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        var festivalId = int.Parse(festival!.Id, CultureInfo.InvariantCulture);

        (await SendFestivalToReviewAsync(_client, festivalId, await GetExternalCsrfTokenAsync(_client)))
            .EnsureSuccessStatusCode();

        // 1. La instantanea SE ESCRIBIO. Sin esta mitad, unos campos nulos tambien «no varian».
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = db.HistorialesRevisionRegistros.Single(item =>
                item.ModuloId == Modulos.Festivales
                && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.Accion == "FestivalEnviadoARevision");

            Assert.Equal(organizacionId, fila.OrganizacionId);
            Assert.Equal(nombreOriginal, fila.OrganizacionNombre);
            Assert.Equal(responsableOriginal, fila.ResponsableNombre);
            Assert.False(string.IsNullOrWhiteSpace(fila.ActorNombre));
        }

        // 2. Cambian LAS DOS COSAS: el nombre de la organizacion y quien responde por ella.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var organizacion = db.EntityProfiles.Single(item => item.Id == organizacionId);
            organizacion.Name = nombreRenombrado;
            var responsable = db.EntidadesResponsable.Single(item => item.IdEntidad == organizacionId);
            responsable.ResponsableNombre = responsableNuevo;
            responsable.ResponsableNumeroDocumento = "8070605040";
            db.SaveChanges();
        }

        // 3. El historial sigue diciendo lo de entonces.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = db.HistorialesRevisionRegistros.Single(item =>
                item.ModuloId == Modulos.Festivales
                && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.Accion == "FestivalEnviadoARevision");

            Assert.Equal(nombreOriginal, fila.OrganizacionNombre);
            Assert.Equal(responsableOriginal, fila.ResponsableNombre);
        }

        // 4. Y la ficha institucional sirve la instantanea, no el JOIN contra el presente. Esta es
        //    la mitad que se ve por la pantalla: sin ella el dato correcto estaria guardado y la
        //    persona funcionaria seguiria leyendo el nombre de hoy sobre un hecho de ayer.
        var institucional = _factory.CreateClient();
        await institucional.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" });
        var ficha = await institucional.GetAsync($"/api/v1/institucional/festivales/{festivalId}");
        ficha.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await ficha.Content.ReadAsStringAsync());
        var entrada = documento.RootElement.GetProperty("historial").EnumerateArray()
            .Single(item => item.GetProperty("accion").GetString() == "FestivalEnviadoARevision");

        Assert.Equal(nombreOriginal, entrada.GetProperty("organizacionNombre").GetString());
        Assert.Equal(responsableOriginal, entrada.GetProperty("responsableNombre").GetString());
    }

    /// <summary>
    /// B6. El contacto de la ficha publica sale de la VERSION, no de la cabecera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL DEFECTO. Todo lo demas de la ficha —nombre, descripcion, cobertura, territorio,
    /// periodicidad, correo— ya viajaba en la version, precisamente para que editar un festival no
    /// cambiara lo que el publico ve hasta que la edicion se apruebe. El bloque de contacto se
    /// habia quedado fuera de esa regla y salia de `Festivales`, de modo que cambiar un telefono en
    /// el borrador lo cambiaba en la ficha PUBLICADA de inmediato, saltandose el circuito de
    /// revision entero. No era un agujero de seguridad: era una excepcion silenciosa a la regla que
    /// sostiene todo el modelo de versiones.
    /// </para>
    /// <para>
    /// COMO SE COMPRUEBA. Se publica una version con un contacto, se cambia LA CABECERA a otro
    /// distinto, y se exige que la ficha publica siga sirviendo el de la version. La cabecera se
    /// deja con valores inconfundibles para que, si la ficha volviera a leerla, el fallo diga
    /// exactamente de donde salio el dato.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Public_Festival_Contact_Comes_From_The_Version_Not_The_Header()
    {
        const string telefonoPublicado = "+57 604 111 2233";
        const string instagramPublicado = "https://instagram.com/festival-version-publicada";
        const string telefonoDeLaCabecera = "+57 999 SOLO EN LA CABECERA";
        const string instagramDeLaCabecera = "https://instagram.com/solo-en-la-cabecera";

        int festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var idInstitucional = db.EntityProfiles.Single(item => item.IsInstitutional).Id;

            var festival = new FestivalRow
            {
                Name = "Festival con contacto versionado",
                OrganizacionPrincipalId = idInstitucional,
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "publicado",
                ContactPhone = telefonoPublicado,
                InstagramUrl = instagramPublicado,
                CreatedAt = DateTime.UtcNow
            };
            db.FestivalRecords.Add(festival);
            db.SaveChanges();
            festivalId = festival.Id;

            db.VersionesFestival.Add(new VersionFestivalRow
            {
                FestivalOrigenId = festival.Id,
                NumeroVersion = 1,
                EsVigente = true,
                Nombre = festival.Name,
                NivelCobertura = "municipal",
                CodigoDepartamento = "05",
                CodigoMunicipio = "05001",
                TelefonoContacto = telefonoPublicado,
                Instagram = instagramPublicado,
                Director = "Directora Publicada",
                FechaPublicacion = DateTime.UtcNow,
                FechaCreacion = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        // La cabecera cambia. La ficha publicada no puede moverse: ese cambio todavia no ha pasado
        // por revision, y publicarlo de inmediato es justamente lo que este bloque impide.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = db.FestivalRecords.Single(item => item.Id == festivalId);
            festival.ContactPhone = telefonoDeLaCabecera;
            festival.InstagramUrl = instagramDeLaCabecera;
            db.SaveChanges();
        }

        var respuesta = await _client.GetAsync($"/api/v1/publico/festivales/{festivalId}");
        respuesta.EnsureSuccessStatusCode();
        var ficha = await respuesta.Content.ReadFromJsonAsync<FestivalPublicoDto>();
        Assert.NotNull(ficha);

        // LA INVARIANTE NO CAMBIO; CAMBIO CON QUE SE MIDE. Hasta se
        // medía con el telefono y con el nombre de la directora, y desde ese dia la lectura
        // publica no sirve ninguno de los dos: la direccion de producto decidio que no lleva datos
        // personales. Instagram viaja igual y se resuelve por el mismo camino
        // (`version is null ? festival.InstagramUrl : version.Instagram`), asi que mide lo mismo.
        Assert.Equal(instagramPublicado, ficha!.Instagram);

        // Y la mitad negativa, que es la que de verdad delata el defecto: lo de la cabecera NO sale.
        Assert.NotEqual(instagramDeLaCabecera, ficha.Instagram);

        // De paso, la comprobacion de que lo retirado sigue retirado, sobre el JSON en crudo.
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(telefonoPublicado, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(telefonoDeLaCabecera, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Directora Publicada", cuerpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// La contrapartida de la anterior: un festival HISTORICO —de los que no tienen ninguna
    /// version— sigue sirviendo el contacto de su cabecera.
    /// </summary>
    /// <remarks>
    /// Sin esta prueba, «el contacto sale de la version» lo cumple tambien una implementacion que
    /// devuelva null siempre, y los 30 festivales historicos se quedarian sin telefono en el sitio
    /// publico sin que nada se pusiera rojo.
    /// </remarks>
    [Fact]
    public async Task Historic_Festival_Without_Version_Still_Serves_Its_Header_Contact()
    {
        // SE MIDE CON INSTAGRAM Y NO CON EL TELEFONO desde: el telefono
        // dejo de viajar por la lectura publica. El respaldo a la cabecera es el mismo camino para
        // los dos campos (`version is null ? festival.X : version.X`), asi que la invariante que
        // esta prueba defiende —un festival historico no se queda sin sus datos— se sigue midiendo.
        const string instagramHistorico = "https://instagram.com/festival-historico-sin-version";

        int festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = new FestivalRow
            {
                Name = "Festival historico sin version",
                OrganizacionPrincipalId = db.EntityProfiles.Single(item => item.IsInstitutional).Id,
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "publicado",
                InstagramUrl = instagramHistorico,
                CreatedAt = DateTime.UtcNow
            };
            db.FestivalRecords.Add(festival);
            db.SaveChanges();
            festivalId = festival.Id;
        }

        var respuesta = await _client.GetAsync($"/api/v1/publico/festivales/{festivalId}");
        respuesta.EnsureSuccessStatusCode();
        var ficha = await respuesta.Content.ReadFromJsonAsync<FestivalPublicoDto>();

        Assert.Equal(instagramHistorico, ficha!.Instagram);
    }

    [Fact]
    public async Task Festival_Review_Requires_External_Authorization_Csrf_And_Complete_Draft()
    {
        var withoutSession = await _client.PostAsJsonAsync("/api/v1/externo/festivales/1/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);

        const string ownerEmail = "festival.autorizacion.revision@example.com";
        await RegisterAndVerifyExternalPersonAsync(ownerEmail);
        await LoginExternalAsync(_client, ownerEmail);
        var csrfToken = await GetExternalCsrfTokenAsync(_client);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, csrfToken, new
        {
            name = "Organización Revisión Protegida",
            contactEmail = "contacto@revision-protegida.test",
            coverageLevel = "municipal",
            departmentCode = "05",
            municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        var creationToken = await GetExternalCsrfTokenAsync(_client);
        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), creationToken, new
        {
            nombre = "Festival Protegido para Revisión", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001"
        });
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.NotNull(festival);

        var withoutCsrf = await _client.PostAsJsonAsync($"/api/v1/externo/festivales/{festival!.Id}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);

        var institutionalClient = _factory.CreateClient();
        await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" });
        var institutionalResponse = await institutionalClient.PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalResponse.StatusCode);

        var otherClient = _factory.CreateClient();
        var registration = await otherClient.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Otra persona de revisión",
            NumeroDocumento = "1020304058",
            Phone = "3000000000",
            Email = "festival.ajeno.revision@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        var otherAccount = await registration.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        await LoginExternalAsync(otherClient, "festival.ajeno.revision@example.com");
        var otherToken = await GetExternalCsrfTokenAsync(otherClient);
        var otherResponse = await SendFestivalToReviewAsync(otherClient, int.Parse(festival.Id), otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var incomplete = db.FestivalRecords.Single(item => item.Id == int.Parse(festival.Id));
            incomplete.Name = string.Empty;
            db.SaveChanges();
        }
        var incompleteToken = await GetExternalCsrfTokenAsync(_client);
        var incompleteResponse = await SendFestivalToReviewAsync(_client, int.Parse(festival.Id), incompleteToken);
        Assert.Equal(HttpStatusCode.BadRequest, incompleteResponse.StatusCode);
    }

    [Fact]
    public async Task Institutional_Review_Requests_Adjustments_Then_Publishes_And_Preserves_History()
    {
        const string email = "festival.decision.institucional@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var token = await GetExternalCsrfTokenAsync(_client);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, token, new
        {
            name = "Organización para decisión institucional", contactEmail = "decision@festival.test",
            coverageLevel = "municipal", departmentCode = "05", municipalityCode = "05001"
        });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);
        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), await GetExternalCsrfTokenAsync(_client), new
        {
            nombre = "Festival con decisión institucional", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001"
        });
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.NotNull(festival);
        (await SendFestivalToReviewAsync(_client, int.Parse(festival!.Id), await GetExternalCsrfTokenAsync(_client))).EnsureSuccessStatusCode();

        var externalQueue = await _client.GetAsync("/api/v1/institucional/festivales/en-revision");
        Assert.Equal(HttpStatusCode.Unauthorized, externalQueue.StatusCode);

        var institutionalClient = _factory.CreateClient();
        (await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" })).EnsureSuccessStatusCode();
        var queue = await institutionalClient.GetFromJsonAsync<List<FestivalRevisionInstitucionalDto>>("/api/v1/institucional/festivales/en-revision");
        Assert.Contains(queue!, item => item.Id == festival.Id);

        var noCsrf = await institutionalClient.PostAsJsonAsync($"/api/v1/institucional/festivales/{festival.Id}/decisiones", new { accion = "SolicitarAjustes", observacion = "Completa la descripción institucional." });
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var adjustments = await DecideFestivalAsync(institutionalClient, int.Parse(festival.Id), await GetInstitutionalCsrfTokenAsync(institutionalClient), new
        {
            accion = "SolicitarAjustes", observacion = "Completa la descripción institucional."
        });
        adjustments.EnsureSuccessStatusCode();

        var publicBefore = await _client.GetFromJsonAsync<PagedResponse<FestivalPublicoDto>>("/api/v1/publico/festivales?limit=100&offset=0");
        Assert.DoesNotContain(publicBefore!.Items, item => item.Nombre == "Festival con decisión institucional");
        var own = await _client.GetFromJsonAsync<List<FestivalBorradorDto>>($"/api/v1/externo/organizaciones/{organization.Id}/festivales");
        var adjusted = Assert.Single(own!, item => item.Id == festival.Id);
        Assert.Equal("AjustesSolicitados", adjusted.Estado);
        Assert.Contains("Completa", adjusted.ObservacionRevision);

        var updateWithoutCsrf = await _client.PutAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}", new { nombre = "Festival con decisión institucional", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001" });
        Assert.Equal(HttpStatusCode.BadRequest, updateWithoutCsrf.StatusCode);
        var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/festivales/{festival.Id}")
        {
            Content = JsonContent.Create(new { nombre = "Festival con decisión institucional", descripcion = "Descripción completada.", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001" })
        };
        update.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        (await _client.SendAsync(update)).EnsureSuccessStatusCode();
        (await SendFestivalToReviewAsync(_client, int.Parse(festival.Id), await GetExternalCsrfTokenAsync(_client))).EnsureSuccessStatusCode();

        var missingReason = await DecideFestivalAsync(institutionalClient, int.Parse(festival.Id), await GetInstitutionalCsrfTokenAsync(institutionalClient), new { accion = "Rechazar" });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        var invalidAction = await DecideFestivalAsync(institutionalClient, int.Parse(festival.Id), await GetInstitutionalCsrfTokenAsync(institutionalClient), new { accion = "Aprobar" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAction.StatusCode);
        var publish = await DecideFestivalAsync(institutionalClient, int.Parse(festival.Id), await GetInstitutionalCsrfTokenAsync(institutionalClient), new { accion = "Publicar" });
        publish.EnsureSuccessStatusCode();
        var publicAfter = await _client.GetFromJsonAsync<PagedResponse<FestivalPublicoDto>>("/api/v1/publico/festivales?limit=100&offset=0");
        Assert.Contains(publicAfter!.Items, item => item.Nombre == "Festival con decisión institucional");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Contains(db.HistorialesRevisionRegistros, item => item.RegistroId == festival.Id && item.Accion == "FestivalAjustesSolicitados" && item.Comentario!.Contains("Completa"));
        Assert.Contains(db.HistorialesRevisionRegistros, item => item.RegistroId == festival.Id && item.Accion == "FestivalPublicado");
        // Afirmaba Action == "FestivalPublicado", que CK_BitacoraAuditoria_Accion rechaza:
        // publicar un Festival fallaba contra SQL Server y esta prueba lo daba por bueno.
        // El verbo tecnico va en Accion; el evento funcional, en el historial y en el JSON.
        Assert.Contains(db.AuditLogs, item => item.RecordId == festival.Id
            && item.Action == "publicar"
            && item.NewValuesJson != null
            && item.NewValuesJson.Contains("FestivalPublicado", StringComparison.Ordinal));
        Assert.Contains(db.Notifications, item => item.RecordId == festival.Id && item.EventType == "FestivalPublicado" && item.RecipientEmail == email);
    }

    [Fact]
    public async Task Institutional_Review_Rejects_Decision_On_A_Festival_Outside_Review()
    {
        await LoginAsWebmasterAsync();
        var token = await GetInstitutionalCsrfTokenAsync(_client);
        var invalid = await DecideFestivalAsync(_client, 1, token, new { accion = "Publicar" });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
    }

    [Fact]
    public async Task Public_Festival_Directory_Exposes_Only_Approved_Fields_And_States()
    {
        FestivalRow publicado;
        FestivalRow borrador;
        FestivalRow enRevision;
        FestivalRow ajustes;
        FestivalRow rechazado;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var organizacion = new EntityProfileRow
            {
                EntityType = "organizacion",
                Name = "Fundación Pública de Prueba",
                IdentificationNumber = $"PUB-{Guid.NewGuid():N}",
                ContactEmail = "privado@organizacion.test",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "activa",
                IsActive = true,
                CreatedByUserId = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.EntityProfiles.Add(organizacion);
            db.SaveChanges();

            publicado = new FestivalRow
            {
                Name = "Festival Público CV-008",
                Description = "Descripción estable para consulta pública.",
                OrganizacionPrincipalId = organizacion.Id,
                Periodicidad = "Anual",
                ContactEmail = "contacto@festival-publico.test",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Publicado",
                CreatedAt = DateTime.UtcNow
            };
            borrador = new FestivalRow { Name = "Festival Borrador CV-008", CoverageLevel = "municipal", DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "Borrador", CreatedAt = DateTime.UtcNow };
            enRevision = new FestivalRow { Name = "Festival En Revisión CV-008", CoverageLevel = "municipal", DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "EnRevision", CreatedAt = DateTime.UtcNow };
            ajustes = new FestivalRow { Name = "Festival Ajustes CV-008", CoverageLevel = "municipal", DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "AjustesSolicitados", CreatedAt = DateTime.UtcNow };
            rechazado = new FestivalRow { Name = "Festival Rechazado CV-008", CoverageLevel = "municipal", DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "Rechazado", CreatedAt = DateTime.UtcNow };
            db.FestivalRecords.AddRange(publicado, borrador, enRevision, ajustes, rechazado);
            db.SaveChanges();
            db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.Festivales, publicado.Id, [1], DateTime.UtcNow);
            db.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, publicado.Id, [1], DateTime.UtcNow);
            db.SaveChanges();
        }

        var listResponse = await _client.GetAsync("/api/v1/publico/festivales?limit=100&offset=0");
        listResponse.EnsureSuccessStatusCode();
        var listado = await listResponse.Content.ReadFromJsonAsync<PagedResponse<FestivalPublicoDto>>();
        Assert.NotNull(listado);
        Assert.Contains(listado!.Items, item => item.Id == publicado.Id.ToString());
        Assert.DoesNotContain(listado.Items, item => item.Id == borrador.Id.ToString() || item.Id == enRevision.Id.ToString() || item.Id == ajustes.Id.ToString() || item.Id == rechazado.Id.ToString());

        var detailResponse = await _client.GetAsync($"/api/v1/publico/festivales/{publicado.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detalle = await detailResponse.Content.ReadFromJsonAsync<FestivalPublicoDto>();
        Assert.NotNull(detalle);
        Assert.Equal("Fundación Pública de Prueba", detalle!.OrganizacionResponsable);
        Assert.Equal("Antioquia", detalle.TerritorioPrincipal.Departamento);
        Assert.Equal("Medellin", detalle.TerritorioPrincipal.Municipio);
        Assert.Single(detalle.PracticasMusicales);
        Assert.Single(detalle.TerritoriosSonoros);
        // ANTES SE EXIGIA QUE EL CORREO SALIERA; AHORA SE EXIGE LO CONTRARIO. Es el cambio de
        // regla, y esta linea es el sitio donde se ve invertida.
        Assert.DoesNotContain("contacto@festival-publico.test", await detailResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var body = await detailResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("privado@organizacion.test", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("observacion", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auditoria", body, StringComparison.OrdinalIgnoreCase);

        var hiddenDetail = await _client.GetAsync($"/api/v1/publico/festivales/{borrador.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);
        var map = await _client.GetFromJsonAsync<DepartmentDrilldownResponseDto>("/api/v1/mapa/departamentos/05/detalle");
        Assert.Contains(map!.Festivals, item => item.Name == "Festival Público CV-008");
        Assert.DoesNotContain(map.Festivals, item => item.Name == "Festival Borrador CV-008" || item.Name == "Festival En Revisión CV-008" || item.Name == "Festival Ajustes CV-008" || item.Name == "Festival Rechazado CV-008");
    }

    [Fact]
    public async Task External_Administrator_Proposes_Changes_Without_Replacing_The_Public_Festival()
    {
        var anonymousClient = _factory.CreateClient();
        var withoutSession = await anonymousClient.PostAsJsonAsync("/api/v1/externo/festivales/1/propuestas-cambio", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);

        const string email = "festival.propuesta@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, await GetExternalCsrfTokenAsync(_client), new
        {
            name = "Organización de propuesta Festival", contactEmail = "propuesta@festival.test",
            coverageLevel = "municipal", departmentCode = "05", municipalityCode = "05001"
        });
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        Assert.NotNull(organization);
        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), await GetExternalCsrfTokenAsync(_client), new
        {
            nombre = "Festival Música del Río", descripcion = "Festival anual de músicas tradicionales.", periodicidad = "anual",
            correoContacto = "rio@festival.test", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001",
            practicasMusicalesIds = UnCatalogo, territoriosSonorosIds = UnCatalogo
        });
        festivalResponse.EnsureSuccessStatusCode();
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.NotNull(festival);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            // EL CODIGO CANONICO, no una grafia inventada. Aqui decia "Publicado" con mayuscula,
            // que es una grafia que NINGUN camino de produccion escribe: la decision institucional
            // escribe `publicado` y asi estan los 30 Festivales de la base real. Con ese amaño, la
            // prueba entraba por una puerta que en la vida real estaba cerrada —la comparacion del
            // endpoint era `!= "Publicado"`, en memoria y sensible a mayusculas— y dejaba en verde
            // una ruta que ningun Festival publicado podia alcanzar.
            var publicado = db.FestivalRecords.Single(item => item.Id == int.Parse(festival!.Id));
            publicado.StatusCode = EstadosFestival.Publicado;
            db.SaveChanges();
        }

        var publicBefore = await _client.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festival!.Id}");
        Assert.NotNull(publicBefore);
        Assert.Equal("Festival anual de músicas tradicionales.", publicBefore!.Descripcion);
        Assert.Single(publicBefore.PracticasMusicales);
        Assert.Single(publicBefore.TerritoriosSonoros);

        var withoutCsrf = await _client.PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio", new { });
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);

        var institutionalClient = _factory.CreateClient();
        (await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" })).EnsureSuccessStatusCode();
        var institutionalProposal = await institutionalClient.PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, institutionalProposal.StatusCode);

        var otherClient = _factory.CreateClient();
        var otherRegistration = await otherClient.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Otra administradora",
            NumeroDocumento = "1020304059",
            Phone = "3000000000",
            Email = "festival.propuesta.ajena@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        var otherAccount = await otherRegistration.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        await LoginExternalAsync(otherClient, "festival.propuesta.ajena@example.com");
        var otherProposal = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio") { Content = JsonContent.Create(new { }) };
        otherProposal.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(otherClient));
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.SendAsync(otherProposal)).StatusCode);

        var createProposal = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio") { Content = JsonContent.Create(new { }) };
        createProposal.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        var proposalResponse = await _client.SendAsync(createProposal);
        Assert.True(proposalResponse.StatusCode == HttpStatusCode.Created, await proposalResponse.Content.ReadAsStringAsync());
        var proposal = await proposalResponse.Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>();
        Assert.NotNull(proposal);
        Assert.Equal("borrador", proposal!.Estado);
        Assert.Equal(festival.Id, proposal.FestivalOrigenId);
        Assert.Single(proposal.PracticasMusicales);
        Assert.Single(proposal.TerritoriosSonoros);

        var repeatProposal = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio") { Content = JsonContent.Create(new { }) };
        repeatProposal.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        var repeated = await _client.SendAsync(repeatProposal);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var existingProposal = await repeated.Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>();
        Assert.Equal(proposal.Id, existingProposal!.Id);

        var directUpdate = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/festivales/{festival.Id}")
        {
            Content = JsonContent.Create(new { nombre = "Cambio directo indebido", nivelCobertura = "nacional" })
        };
        directUpdate.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(directUpdate)).StatusCode);

        var updateProposal = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio")
        {
            Content = JsonContent.Create(new
            {
                nombre = "Festival Nacional Música del Río", descripcion = "Festival nacional de músicas tradicionales y contemporáneas.",
                periodicidad = "bienal", correoContacto = "nuevo@festival.test", nivelCobertura = "nacional",
                practicasMusicalesIds = Array.Empty<int>(), territoriosSonorosIds = Array.Empty<int>()
            })
        };
        updateProposal.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        var updatedResponse = await _client.SendAsync(updateProposal);
        updatedResponse.EnsureSuccessStatusCode();
        var updatedProposal = await updatedResponse.Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>();
        Assert.Equal("Festival Nacional Música del Río", updatedProposal!.Nombre);
        Assert.Empty(updatedProposal.PracticasMusicales);
        Assert.Empty(updatedProposal.TerritoriosSonoros);

        var publicAfter = await _client.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festival.Id}");
        Assert.NotNull(publicAfter);
        Assert.Equal("Festival Música del Río", publicAfter!.Nombre);
        Assert.Equal("Festival anual de músicas tradicionales.", publicAfter.Descripcion);
        Assert.Equal("municipal", publicAfter.TerritorioPrincipal.NivelCobertura);
        Assert.Single(publicAfter.PracticasMusicales);
        Assert.Single(publicAfter.TerritoriosSonoros);
        var map = await _client.GetFromJsonAsync<DepartmentDrilldownResponseDto>("/api/v1/mapa/departamentos/05/detalle");
        Assert.Contains(map!.Festivals, item => item.Name == "Festival Música del Río");

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Single(verificationDb.VersionesFestival.Where(item => item.FestivalOrigenId == int.Parse(festival.Id) && item.EsVigente));
        // EL EXPEDIENTE VIVE EN LAS TABLAS GENERICAS: «activa» es «ni aplicada ni rechazada», y ya
        // no hay tablas de relación propias —las prácticas propuestas son una fila de campo, y solo
        // existe si la propuesta las cambia—.
        Assert.Single(verificationDb.PropuestasDeCambio.Where(item =>
            item.ModuloId == Modulos.Festivales && item.RegistroId == festival.Id
            && item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada));
        // LA PROPUESTA DEJA LAS PRACTICAS VACIAS, y eso ahora SE VE: en la forma vieja era la
        // ausencia de filas en una tabla de relación —indistinguible de «no se tocó»—; en la nueva
        // es un campo propuesto con valor vacío, que es una petición explícita de quitarlas.
        var practicasPropuestas = verificationDb.PropuestasDeCambioCampos.Single(item =>
            item.IdPropuesta == long.Parse(proposal.Id) && item.CampoId == "practicasMusicales");
        Assert.Null(practicasPropuestas.ValorPropuesto);
        Assert.Single(verificationDb.De<PracticaMusicalDeRegistroRow>(Modulos.Festivales, int.Parse(festival.Id)));
        // Mismo motivo: ninguno de esos dos nombres esta entre los trece verbos que admite
        // CK_BitacoraAuditoria_Accion. Se comprueba el verbo tecnico Y el evento, que sigue
        // distinguiendo un alta de una edicion desde ValoresNuevos.
        Assert.Contains(verificationDb.AuditLogs, item => item.Action == "crear"
            && item.RecordId == proposal.Id
            && item.NewValuesJson != null
            && item.NewValuesJson.Contains("\"Evento\":\"FestivalCambioPropuesto\"", StringComparison.Ordinal));
        Assert.Contains(verificationDb.AuditLogs, item => item.Action == "actualizar"
            && item.RecordId == proposal.Id
            && item.NewValuesJson != null
            && item.NewValuesJson.Contains("\"Evento\":\"FestivalCambioPropuestoActualizado\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Proposal_Governance_Publishes_A_New_Version_Without_Replacing_The_Current_One_Early()
    {
        const string email = "festival.gobierno.propuesta@example.com";
        await RegisterAndVerifyExternalPersonAsync(email);
        await LoginExternalAsync(_client, email);
        var organizationResponse = await CreateExternalOrganizationAsync(_client, await GetExternalCsrfTokenAsync(_client), new
        {
            name = "Organización gobierno propuesta", contactEmail = "gobierno@festival.test", coverageLevel = "municipal", departmentCode = "05", municipalityCode = "05001"
        });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<ExternalOrganizationDto>();
        var festivalResponse = await CreateDraftFestivalAsync(_client, int.Parse(organization!.Id), await GetExternalCsrfTokenAsync(_client), new
        {
            nombre = "Festival gobierno de versiones", descripcion = "Versión pública original.", periodicidad = "anual", correoContacto = "original@festival.test", nivelCobertura = "municipal", codigoDepartamento = "05", codigoMunicipio = "05001", practicasMusicalesIds = UnCatalogo, territoriosSonorosIds = UnCatalogo
        });
        var festival = await festivalResponse.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            // Codigo canonico; ver la nota de la prueba de propuestas de cambio.
            db.FestivalRecords.Single(item => item.Id == int.Parse(festival!.Id)).StatusCode = EstadosFestival.Publicado;
            db.SaveChanges();
        }

        var crear = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival!.Id}/propuestas-cambio") { Content = JsonContent.Create(new { }) };
        crear.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        var propuesta = await (await _client.SendAsync(crear)).Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>();
        Assert.NotNull(propuesta);
        var actualizar = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio")
        {
            Content = JsonContent.Create(new { nombre = "Festival gobierno N más uno", descripcion = "Contenido de la nueva versión.", periodicidad = "bienal", correoContacto = "nueva@festival.test", nivelCobertura = "nacional", practicasMusicalesIds = Array.Empty<int>(), territoriosSonorosIds = Array.Empty<int>() })
        };
        actualizar.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        (await _client.SendAsync(actualizar)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio/enviar-a-revision", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio/enviar-a-revision", new { })).StatusCode);
        var enviar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio/enviar-a-revision") { Content = JsonContent.Create(new { }) };
        enviar.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        var enviada = await _client.SendAsync(enviar);
        enviada.EnsureSuccessStatusCode();
        Assert.Equal("en_revision", (await enviada.Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>())!.Estado);
        var editarDuranteRevision = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio") { Content = JsonContent.Create(new { nombre = "Cambio bloqueado", nivelCobertura = "nacional" }) };
        editarDuranteRevision.Headers.Add("X-CSRF-TOKEN", await GetExternalCsrfTokenAsync(_client));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(editarDuranteRevision)).StatusCode);
        var publicBefore = await _client.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festival.Id}");
        Assert.Equal("Festival gobierno de versiones", publicBefore!.Nombre);

        var institutionalClient = _factory.CreateClient();
        (await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await institutionalClient.PostAsJsonAsync($"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio/enviar-a-revision", new { })).StatusCode);
        var queue = await institutionalClient.GetFromJsonAsync<List<PropuestaCambioFestivalRevisionDto>>("/api/v1/institucional/propuestas-cambio-festival/en-revision");
        Assert.Contains(queue!, item => item.Id == propuesta!.Id);
        var decision = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/propuestas-cambio-festival/{propuesta.Id}/decisiones") { Content = JsonContent.Create(new { accion = "Publicar" }) };
        decision.Headers.Add("X-CSRF-TOKEN", await GetInstitutionalProposalCsrfTokenAsync(institutionalClient));
        (await institutionalClient.SendAsync(decision)).EnsureSuccessStatusCode();

        var publicAfter = await _client.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festival.Id}");
        Assert.Equal("Festival gobierno N más uno", publicAfter!.Nombre);
        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var versiones = verificationDb.VersionesFestival.Where(item => item.FestivalOrigenId == int.Parse(festival.Id)).OrderBy(item => item.NumeroVersion).ToList();
        Assert.Equal(2, versiones.Count);
        Assert.False(versiones[0].EsVigente);
        Assert.True(versiones[1].EsVigente);
        // EL EXPEDIENTE, EN LAS TABLAS GENERICAS: `aplicada` es lo que antes se llamaba `Publicada`,
        // y `SubregistroResultanteId` la versión que nació de aplicarlo.
        var expediente = verificationDb.PropuestasDeCambio.Single(item => item.Id == long.Parse(propuesta.Id));
        Assert.Equal(EstadosDePropuesta.Aplicada, expediente.Estado);
        Assert.Equal(versiones[1].Id.ToString(CultureInfo.InvariantCulture), expediente.SubregistroResultanteId);
        // Este era el HTTP_500 que midio la sonda de botones el 21 de agosto: publicar una
        // propuesta escribia "FestivalPropuestaPublicada" en la bitacora y "Publicada" en el
        // historial, y la base rechaza los dos. La prueba pasaba porque SQLite no tiene esos
        // CHECK. Ahora se comprueba el verbo admitido, y ademas que el historial quedo en el
        // vocabulario comun.
        Assert.Contains(verificationDb.AuditLogs, item => item.Action == "publicar"
            && item.RecordId == propuesta.Id
            && item.NewValuesJson != null
            && item.NewValuesJson.Contains("FestivalPropuestaPublicada", StringComparison.Ordinal));
        Assert.Contains(verificationDb.HistorialesRevisionRegistros, item => item.RegistroId == propuesta.Id
            && item.EstadoNuevo == "publicado");
    }

    [Fact]
    public async Task Public_Read_Map_And_Analytics_Use_Only_The_Current_Festival_Version()
    {
        int festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = new FestivalRow { Name = "Nombre heredado no canónico", CoverageLevel = "municipal", DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "Publicado", CreatedAt = DateTime.UtcNow };
            db.FestivalRecords.Add(festival);
            db.SaveChanges();
            festivalId = festival.Id;
            var anterior = new VersionFestivalRow { FestivalOrigenId = festival.Id, NumeroVersion = 1, EsVigente = false, Nombre = "Versión anterior", NivelCobertura = "municipal", CodigoDepartamento = "05", CodigoMunicipio = "05001", FechaPublicacion = DateTime.UtcNow.AddDays(-1), FechaCreacion = DateTime.UtcNow.AddDays(-1) };
            var vigente = new VersionFestivalRow { FestivalOrigenId = festival.Id, NumeroVersion = 2, EsVigente = true, Nombre = "Festival canónico vigente", Descripcion = "Contenido vigente", NivelCobertura = "municipal", CodigoDepartamento = "76", CodigoMunicipio = "76001", Periodicidad = "anual", FechaPublicacion = DateTime.UtcNow, FechaCreacion = DateTime.UtcNow };
            db.VersionesFestival.AddRange(anterior, vigente);
            db.SaveChanges();
            db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, vigente.Id, [1], DateTime.UtcNow);
            db.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, vigente.Id, [1], DateTime.UtcNow);
            db.PropuestasDeCambio.Add(new PropuestaDeCambioRow { ModuloId = Modulos.Festivales, RegistroId = $"{festival.Id}", SubregistroId = $"{vigente.Id}", IdOrganizacion = 1, IdUsuarioProponente = 1, Estado = EstadosDePropuesta.Borrador, Motivo = "Cambio privado no público", FechaCreacion = DateTime.UtcNow, FechaActualizacion = DateTime.UtcNow });
            db.SaveChanges();
        }

        var ficha = await _client.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festivalId}");
        Assert.Equal("Festival canónico vigente", ficha!.Nombre);
        Assert.Equal("Contenido vigente", ficha.Descripcion);
        var mapaActual = await _client.GetFromJsonAsync<DepartmentDrilldownResponseDto>("/api/v1/mapa/departamentos/76/detalle");
        Assert.Contains(mapaActual!.Festivals, item => item.Id == festivalId.ToString() && item.Name == "Festival canónico vigente");
        var mapaAnterior = await _client.GetFromJsonAsync<DepartmentDrilldownResponseDto>("/api/v1/mapa/departamentos/05/detalle");
        Assert.DoesNotContain(mapaAnterior!.Festivals, item => item.Id == festivalId.ToString());
        var resumen = await _client.GetFromJsonAsync<ResumenAnaliticoFestivalesDto>("/api/v1/publico/analitica/festivales/resumen");
        Assert.Contains(resumen!.PorDepartamento, item => item.Nombre == "Valle del Cauca");
        Assert.Contains(resumen.PorPracticaMusical, item => item.Nombre == "Música andina colombiana");
        Assert.Equal(1, resumen.PorDepartamento.Single(item => item.Nombre == "Valle del Cauca").Total);
    }

    [Fact]
    public async Task Historical_Normalization_Diagnosis_Is_Read_Only_And_Normalization_Is_Idempotent()
    {
        int festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = new FestivalRow
            {
                Name = "Festival histórico para normalización",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                StatusCode = "Publicado",
                CreatedAt = DateTime.UtcNow
            };
            db.FestivalRecords.Add(festival);
            db.SaveChanges();
            festivalId = festival.Id;
        }

        await LoginAsWebmasterAsync();
        var diagnostico = await _client.GetAsync("/api/v1/institucional/festivales/diagnostico-normalizacion-versiones-historicas");
        diagnostico.EnsureSuccessStatusCode();
        var diagnosticoJson = JsonDocument.Parse(await diagnostico.Content.ReadAsStringAsync());
        Assert.Contains(diagnosticoJson.RootElement.GetProperty("pendientes").EnumerateArray(), item => item.GetProperty("idFestival").GetInt32() == festivalId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.False(db.VersionesFestival.Any(item => item.FestivalOrigenId == festivalId && item.EsVigente));
        }

        var token = await GetInstitutionalCsrfTokenAsync(_client);
        var primera = new HttpRequestMessage(HttpMethod.Post, "/api/v1/institucional/festivales/normalizar-versiones-historicas") { Content = JsonContent.Create(new { }) };
        primera.Headers.Add("X-CSRF-TOKEN", token);
        (await _client.SendAsync(primera)).EnsureSuccessStatusCode();

        var segundoToken = await GetInstitutionalCsrfTokenAsync(_client);
        var segunda = new HttpRequestMessage(HttpMethod.Post, "/api/v1/institucional/festivales/normalizar-versiones-historicas") { Content = JsonContent.Create(new { }) };
        segunda.Headers.Add("X-CSRF-TOKEN", segundoToken);
        (await _client.SendAsync(segunda)).EnsureSuccessStatusCode();

        using var resultado = _factory.Services.CreateScope();
        var resultadoDb = resultado.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Equal(1, resultadoDb.VersionesFestival.Count(item => item.FestivalOrigenId == festivalId && item.EsVigente));
        // El verbo tiene que ser uno de los trece que admite CK_BitacoraAuditoria_Accion;
        // el nombre del evento viaja en ValoresNuevos. Antes esta prueba aseveraba el
        // literal prohibido, es decir, fijaba el defecto como comportamiento esperado.
        Assert.Contains(resultadoDb.AuditLogs, item =>
            item.RecordId == festivalId.ToString()
            && item.Action == AccionesAuditoria.DeEvento("FestivalHistoricoNormalizado")
            && AccionesAuditoria.EsAdmitido(item.Action)
            && (item.NewValuesJson ?? string.Empty).Contains("FestivalHistoricoNormalizado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generic_Administrative_Festival_Writes_Are_Not_Exposed()
    {
        await LoginAsWebmasterAsync();

        var escritura = await _client.PostAsJsonAsync("/api/v1/admin/data/map/festivals", new { name = "No debe escribirse" });
        Assert.Equal(HttpStatusCode.NotFound, escritura.StatusCode);

        var importacion = await _client.PostAsJsonAsync("/api/v1/admin/data/records/festivals/bulk", new[]
        {
            new { id = "1", name = "No debe actualizar Festival existente", department = "Antioquia" }
        });
        Assert.Equal(HttpStatusCode.NotFound, importacion.StatusCode);
    }

    [Fact]
    public async Task Admin_Records_Aplica_El_Orden_Pedido_Y_Devuelve_Las_Facetas_Territoriales()
    {
        // HASTA EL 14 DE SEPTIEMBRE DE 2026 ESTA RUTA IGNORABA `orden`, `direccion` y
        // `departamento`: siempre devolvia por fecha, `territorios` vacio y decia haber aplicado
        // «actualizacion desc». La consola refleja lo que el servidor aplico, asi que sus cabeceras
        // no ordenaban nada y el filtro territorial decia «(0)». Medido en navegador.
        await LoginAsWebmasterAsync();

        var ascendente = await _client.GetFromJsonAsync<AdminRegistrosRespuestaDto>(
            "/api/v1/admin/data/records/festivals?orden=titulo&direccion=asc&limite=500");
        Assert.NotNull(ascendente);
        Assert.Equal("titulo", ascendente!.Orden);
        Assert.Equal("asc", ascendente.Direccion);
        var nombres = ascendente.Items.Select(item => item.Title).ToList();
        Assert.Equal(nombres.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList(), nombres);

        var descendente = await _client.GetFromJsonAsync<AdminRegistrosRespuestaDto>(
            "/api/v1/admin/data/records/festivals?orden=titulo&direccion=desc&limite=500");
        Assert.Equal("desc", descendente!.Direccion);
        Assert.Equal(nombres.AsEnumerable().Reverse().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).Reverse().ToList(),
            descendente.Items.Select(item => item.Title).ToList());

        // Un orden desconocido no rompe: cae en «prioridad» y la respuesta lo dice.
        var desconocido = await _client.GetFromJsonAsync<AdminRegistrosRespuestaDto>(
            "/api/v1/admin/data/records/festivals?orden=lo-que-sea");
        Assert.Equal("prioridad", desconocido!.Orden);

        // Las facetas territoriales suman el total de la lista, y filtrar por una de ellas
        // devuelve exactamente ese recuento.
        Assert.Equal(desconocido.Total, desconocido.Territorios.Sum(faceta => faceta.Total));
        var conTerritorio = desconocido.Territorios.FirstOrDefault(faceta => faceta.Codigo != FacetaDto.SinTerritorio);
        if (conTerritorio is not null)
        {
            var filtrado = await _client.GetFromJsonAsync<AdminRegistrosRespuestaDto>(
                "/api/v1/admin/data/records/festivals?departamento=" + conTerritorio.Codigo + "&limite=500");
            Assert.Equal(conTerritorio.Total, filtrado!.Total);
            Assert.All(filtrado.Items, item => Assert.Equal(conTerritorio.Etiqueta, item.Department));
        }
    }

    [Fact]
    public async Task Admin_Global_User_Rejects_Obsolete_Role()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Editor Obsoleto",
            Email = "editor.obsoleto@pnmc.local",
            Role = "editor",
            Password = "ClaveEditor123",
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("rol indicado no puede asignarse", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_Global_User_Can_Create_External_Role()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Externo Administrado",
            Email = "externo.administrado@pnmc.local",
            Role = "externo",
            Password = "ClaveExterno123",
            IsActive = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("externo", payload!.User.Role);
    }

    /// <summary>
    /// El listado de usuarios muestra los tres roles del modelo, y ninguno más.
    /// </summary>
    /// <remarks>
    /// El aserto que faltaba es el último: antes esta prueba comprobaba que aparecía
    /// <c>aliado_admin</c> con su alcance de entidad, y no comprobaba que <b>no</b> apareciera
    /// nada fuera del catálogo. Ahora es al revés, que es lo que sirve: si alguien reintroduce
    /// un rol por la puerta de atrás —una fila nueva en <c>Roles</c>, una migración a medio
    /// aplicar— esta prueba lo ve.
    /// </remarks>
    [Fact]
    public async Task Admin_Users_List_Includes_Final_Roles_And_Nothing_Else()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.GetAsync("/api/v1/admin/auth/users");
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<AdminUserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users!, item => item.Role == "webmaster");
        Assert.Contains(users!, item => item.Role == "gestor_interno");
        Assert.Contains(users!, item => item.Role == "externo");

        var fueraDelCatalogo = users!
            .Select(item => item.Role)
            .Where(rol => !PNMC.Api.Security.Permisos.EsRolDePlataforma(rol))
            .Distinct()
            .ToList();
        Assert.True(fueraDelCatalogo.Count == 1 && fueraDelCatalogo[0] == "rol_desconocido",
            "Aparecieron roles fuera del catálogo de plataforma: " + string.Join(", ", fueraDelCatalogo)
            + ". El único admitido aquí es 'rol_desconocido', que el arnés siembra a propósito "
            + "para que PuertaInstitucionalTests pueda comprobar que la lista blanca lo rechaza.");
    }

    [Fact]
    public async Task Generic_Administrative_Status_Cannot_Change_Festival()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/data/records/festivals/1/status",
            new AdminRecordStatusRequest
            {
                Status = "publicado",
                Comment = "Validación técnica de permisos de webmaster."
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_Internal_Can_Be_Created_Listed_And_Read()
    {
        await LoginAsWebmasterAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/admin/notificaciones", new NotificationCreateRequest
        {
            RecipientEmail = "test@pnmc.local",
            EventType = "registro_aprobado",
            Channel = "internal",
            Title = "Registro aprobado",
            Body = "Tu registro fue aprobado por el equipo PNMC.",
            ModuleId = "festivals",
            RecordId = "1",
            AmbitoAcceso = "institucional"
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(created);
        Assert.Equal("enviada", created!.Status);
        Assert.Equal("internal", created.Channel);

        var listResponse = await _client.GetAsync("/api/v1/notificaciones?ambito=institucional");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponse<NotificationDto>>();
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.Id == created.Id);

        var readResponse = await _client.PostAsync($"/api/v1/notificaciones/{created.Id}/lectura?ambito=institucional", null);
        readResponse.EnsureSuccessStatusCode();
        var read = await readResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(read);
        Assert.Equal("leida", read!.Status);
        Assert.NotNull(read.ReadAt);

        var dismissResponse = await _client.PostAsync("/api/v1/notificaciones/leidas/ocultar?ambito=institucional", null);
        dismissResponse.EnsureSuccessStatusCode();
        using var dismissed = JsonDocument.Parse(await dismissResponse.Content.ReadAsStringAsync());
        Assert.True(dismissed.RootElement.GetProperty("ocultadas").GetInt32() >= 1);

        var afterDismiss = await _client.GetAsync("/api/v1/notificaciones?ambito=institucional");
        afterDismiss.EnsureSuccessStatusCode();
        var afterDismissPayload = await afterDismiss.Content.ReadFromJsonAsync<PagedResponse<NotificationDto>>();
        Assert.DoesNotContain(afterDismissPayload!.Items, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Notifications_Keep_External_And_Institutional_Inboxes_Separated()
    {
        const string externalEmail = "avisos.ambito@example.com";
        await RegisterAndVerifyExternalPersonAsync(externalEmail);

        int externalUserId;
        long externalNoticeId;
        long institutionalNoticeForExternalUserId;
        long institutionalNoticeForWebmasterId;
        long externalNoticeForWebmasterId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            externalUserId = await db.Users.Where(item => item.Email == externalEmail).Select(item => item.Id).SingleAsync();
            var webmasterId = await db.Users.Where(item => item.Email == "test@pnmc.local").Select(item => item.Id).SingleAsync();
            var now = DateTime.UtcNow;
            var notices = new[]
            {
                new NotificationRow { RecipientUserId = externalUserId, RecipientEmail = externalEmail, EventType = "AvisoExterno", AccessScope = "external", Title = "Externo", Body = "Visible solo afuera", Status = "enviada", CreatedAt = now, SentAt = now },
                new NotificationRow { RecipientUserId = externalUserId, RecipientEmail = externalEmail, EventType = "AvisoInstitucional", AccessScope = "institutional", Title = "Institucional ajeno", Body = "No visible afuera", Status = "enviada", CreatedAt = now, SentAt = now },
                new NotificationRow { RecipientUserId = webmasterId, RecipientEmail = "test@pnmc.local", EventType = "AvisoInstitucional", AccessScope = "institutional", Title = "Institucional", Body = "Visible solo adentro", Status = "enviada", CreatedAt = now, SentAt = now },
                new NotificationRow { RecipientUserId = webmasterId, RecipientEmail = "test@pnmc.local", EventType = "AvisoExterno", AccessScope = "external", Title = "Externo ajeno", Body = "No visible adentro", Status = "enviada", CreatedAt = now, SentAt = now }
            };
            db.Notifications.AddRange(notices);
            await db.SaveChangesAsync();
            externalNoticeId = notices[0].Id;
            institutionalNoticeForExternalUserId = notices[1].Id;
            institutionalNoticeForWebmasterId = notices[2].Id;
            externalNoticeForWebmasterId = notices[3].Id;
        }

        await LoginExternalAsync(_client, externalEmail);
        var externalInbox = await _client.GetFromJsonAsync<PagedResponse<NotificationDto>>("/api/v1/notificaciones?ambito=externo");
        Assert.NotNull(externalInbox);
        Assert.Contains(externalInbox!.Items, item => item.Id == externalNoticeId.ToString(CultureInfo.InvariantCulture));
        Assert.DoesNotContain(externalInbox.Items, item => item.Id == institutionalNoticeForExternalUserId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/v1/notificaciones/{institutionalNoticeForExternalUserId}/lectura?ambito=externo", null)).StatusCode);

        var institutionalClient = _factory.CreateClient();
        (await institutionalClient.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = "test@pnmc.local", Password = "pnmc-master" })).EnsureSuccessStatusCode();
        var institutionalInbox = await institutionalClient.GetFromJsonAsync<PagedResponse<NotificationDto>>("/api/v1/notificaciones?ambito=institucional");
        Assert.NotNull(institutionalInbox);
        Assert.Contains(institutionalInbox!.Items, item => item.Id == institutionalNoticeForWebmasterId.ToString(CultureInfo.InvariantCulture));
        Assert.DoesNotContain(institutionalInbox.Items, item => item.Id == externalNoticeForWebmasterId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.NotFound, (await institutionalClient.PostAsync($"/api/v1/notificaciones/{externalNoticeForWebmasterId}/lectura?ambito=institucional", null)).StatusCode);
    }

    [Fact]
    public async Task Notifications_Reject_Whatsapp_Without_Configured_Provider()
    {
        await LoginAsWebmasterAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/admin/notificaciones", new NotificationCreateRequest
        {
            RecipientEmail = "test@pnmc.local",
            EventType = "recordatorio_actualizacion",
            Channel = "whatsapp",
            Title = "Recordatorio",
            Body = "Actualiza tu informacion."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("WhatsApp", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Record_Governance_Creates_Link_Duplicate_And_Quality_Items()
    {
        // QUIEN PIDE ES ALGUIEN DEL ECOSISTEMA, no la consola. Hasta
        // esta prueba creaba la solicitud con sesión de webmaster y pasaba, porque la ruta
        // estaba cerrada contra el esquema institucional: el mismo que después la revisa era el
        // único que podía crearla. Al retirar los roles de aliado —los únicos no internos que
        // cruzaban aquella puerta— el circuito se habría quedado sin peticionarios sin que nada
        // fallara. La ruta pasó al canal externo y esta prueba lo ejercita como tal.
        const string correoSolicitante = "vinculacion.externa@example.com";
        await RegisterAndVerifyExternalPersonAsync(correoSolicitante);
        await LoginExternalAsync(_client, correoSolicitante);

        var linkResponse = await _client.PostAsJsonAsync("/api/v1/solicitudes-de-vinculacion", new RecordLinkRequestCreateRequest
        {
            ModuleId = "festivals",
            RecordId = "1",
            RequestedScope = "responsable",
            Reason = "Solicito revisar la vinculacion con este registro historico.",
            EvidenceText = "Coinciden nombre, municipio y contacto."
        });
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);

        var link = await linkResponse.Content.ReadFromJsonAsync<RecordLinkRequestDto>();
        Assert.NotNull(link);
        Assert.Equal("pendiente", link!.Status);
        // ANTES ESTO ERA `Assert.Null`, y la premisa era «persona registrada sin actor». Esa premisa
        // se acabo el dia que el alta paso a ser un solo acto: no quedan cuentas externas sin
        // organizacion, asi que la solicitud nace sabiendo por quien se pregunta. Es mejor dato del
        // que habia, y por eso la prueba cambia de signo en vez de forzarse a seguir en verde.
        Assert.NotNull(link.EntidadId);

        // Y decidir sobre ella sigue siendo institucional.
        await LoginAsWebmasterAsync();

        var linkStatusResponse = await _client.PostAsJsonAsync(
            $"/api/v1/admin/solicitudes-de-vinculacion/{link.Id}/status",
            new RecordLinkRequestStatusRequest
            {
                Status = "en_revision",
                Comment = "Solicitud recibida para verificacion interna."
            });
        linkStatusResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "1",
            CandidateRecordId = "2",
            SimilarityLevel = "media",
            SimilarityScore = 72.5m,
            EvidenceJson = "{\"name\":\"similar\"}"
        });
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);

        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<RecordDuplicateCandidateDto>();
        Assert.NotNull(duplicate);
        Assert.Equal("pendiente", duplicate!.Status);

        var qualityResponse = await _client.PostAsJsonAsync("/api/v1/admin/data-quality/flags", new RecordQualityFlagCreateRequest
        {
            ModuleId = "festivals",
            RecordId = "1",
            FlagType = "requiere_actualizacion",
            Severity = "media",
            Detail = "Registro publicado sin actualizacion reciente."
        });
        Assert.Equal(HttpStatusCode.Created, qualityResponse.StatusCode);

        var quality = await qualityResponse.Content.ReadFromJsonAsync<RecordQualityFlagDto>();
        Assert.NotNull(quality);
        Assert.Equal("abierta", quality!.Status);
    }

    /// <summary>
    /// Antes de este cambio, `GET /admin/record-link-requests` devolvía únicamente identificadores:
    /// la bandeja de «Trabajo institucional» del panel administrativo mostraba «Registro #1» y
    /// «Organización #N» en vez del Festival y la entidad reales. Esta prueba fija que el nombre
    /// viaja con la fila.
    /// </summary>
    [Fact]
    public async Task Record_Link_Requests_Carry_The_Real_Names_Not_Just_Identifiers()
    {
        const string correoSolicitante = "vinculacion.nombres@example.com";
        await RegisterAndVerifyExternalPersonAsync(correoSolicitante);
        await LoginExternalAsync(_client, correoSolicitante);

        var linkResponse = await _client.PostAsJsonAsync("/api/v1/solicitudes-de-vinculacion", new RecordLinkRequestCreateRequest
        {
            ModuleId = "festivals",
            RecordId = "1",
            RequestedScope = "responsable",
            Reason = "Solicito revisar la vinculacion con este registro historico.",
            EvidenceText = "Coinciden nombre, municipio y contacto."
        });
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);
        var link = await linkResponse.Content.ReadFromJsonAsync<RecordLinkRequestDto>();
        Assert.NotNull(link);
        Assert.NotNull(link!.EntidadId);

        string nombreDeLaEntidad;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            nombreDeLaEntidad = await db.EntityProfiles.AsNoTracking()
                .Where(item => item.Id == int.Parse(link.EntidadId!, CultureInfo.InvariantCulture))
                .Select(item => item.Name).SingleAsync();
        }

        await LoginAsWebmasterAsync();
        var listado = await _client.GetFromJsonAsync<PagedResponse<RecordLinkRequestDto>>("/api/v1/admin/solicitudes-de-vinculacion");
        Assert.NotNull(listado);
        var fila = listado!.Items.Single(item => item.Id == link.Id);

        // Festival Id=1 lo siembra TestWebApplicationFactory con Name = "Festival Test".
        Assert.Equal("Festival Test", fila.RecordName);
        Assert.Equal(nombreDeLaEntidad, fila.EntidadNombre);
    }

    /// <summary>
    /// Mismo defecto que la prueba anterior, en la bandeja de reclamaciones de administración:
    /// `GET /institucional/reclamaciones-administracion` devolvía el Festival y la organización
    /// solicitante solo como identificadores.
    /// </summary>
    [Fact]
    public async Task Administration_Claims_Carry_The_Real_Names_Not_Just_Identifiers()
    {
        int entidadId;
        long claimId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var entidad = new PNMC.Domain.Entities.EntityProfileRow
            {
                EntityType = "organizacion",
                Name = "Fundación de la Reclamación",
                CoverageLevel = "municipal",
                StatusCode = "activa",
                CreatedByUserId = 1,
                CreatedAt = DateTime.UtcNow,
            };
            db.EntityProfiles.Add(entidad);
            await db.SaveChangesAsync();
            entidadId = entidad.Id;

            var claim = new PNMC.Domain.Entities.AdministrationClaimRow
            {
                ModuloId = Modulos.Festivales,
                CanonicalRecordId = "1",
                RequestingOrganizationId = entidadId,
                RequestingPersonId = 1,
                Status = "enviada",
                Justification = "Somos quienes organizamos este Festival desde 2019.",
                CreatedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1,
            };
            db.AdministrationClaims.Add(claim);
            await db.SaveChangesAsync();
            claimId = claim.Id;
        }

        await LoginAsWebmasterAsync();
        var listado = await _client.GetFromJsonAsync<List<ReclamacionAdministracionDto>>("/api/v1/institucional/reclamaciones-administracion");
        Assert.NotNull(listado);
        var fila = listado!.Single(item => item.Id == claimId.ToString(CultureInfo.InvariantCulture));

        Assert.Equal("Festival Test", fila.RegistroNombre);
        Assert.Equal("Fundación de la Reclamación", fila.OrganizacionSolicitanteNombre);
    }

    [Fact]
    public async Task Participation_ReturnsBadRequest_WhenMissingRequiredFields()
    {
        var request = new ParticipationSubmissionRequest
        {
            ActorType = "",
            ActorName = "",
            Email = "not-an-email",
            Phone = "",
            Department = "",
            Municipality = "",
            MusicalFields = "",
            Description = "",
            Contribution = "",
            Consent = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/participaciones", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participation_ReturnsBadRequest_WhenUrlsAreInvalid()
    {
        var request = new ParticipationSubmissionRequest
        {
            ActorType = "organization",
            ActorName = "Colectivo Test URL",
            Email = "colectivo.url@example.com",
            Phone = "3000000000",
            Department = "Antioquia",
            Municipality = "Medellin",
            MusicalFields = "Formacion",
            Description = "Registro de prueba",
            Contribution = "Aporte de prueba",
            Website = "ftp://invalid-url",
            FacebookUrl = "notaurl",
            Consent = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/participaciones", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_Festival_Endpoint_Returns_A_Paged_Payload()
    {
        var festivalsResponse = await _client.GetAsync("/api/v1/publico/festivales?limit=10&offset=0");
        festivalsResponse.EnsureSuccessStatusCode();
        var festivalsPayload = await festivalsResponse.Content.ReadFromJsonAsync<PagedResponse<FestivalPublicoDto>>();
        Assert.NotNull(festivalsPayload);

        var divipolaResponse = await _client.GetAsync("/api/v1/publico/divipola");
        divipolaResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Map_Topology_Endpoints_ReturnNewTerritorialObjects()
    {
        var topologyResponse = await _client.GetAsync("/api/v1/mapa/cartografia/territorios");
        topologyResponse.EnsureSuccessStatusCode();

        await using var topologyStream = await topologyResponse.Content.ReadAsStreamAsync();
        using var topologyDocument = await JsonDocument.ParseAsync(topologyStream);
        Assert.Equal("Topology", topologyDocument.RootElement.GetProperty("type").GetString());

        var objects = topologyDocument.RootElement.GetProperty("objects");
        Assert.True(objects.TryGetProperty("MGN_ADM_DPTO_POLITICO", out var departmentsObject));
        Assert.True(objects.TryGetProperty("MGN_ADM_MPIO_GRAFICO", out var municipalitiesObject));
        Assert.True(departmentsObject.GetProperty("geometries").GetArrayLength() > 0);
        Assert.True(municipalitiesObject.GetProperty("geometries").GetArrayLength() > 0);

        var compatibilityResponse = await _client.GetAsync("/api/v1/mapa/geojson/departments");
        compatibilityResponse.EnsureSuccessStatusCode();
        await using var compatibilityStream = await compatibilityResponse.Content.ReadAsStreamAsync();
        using var compatibilityDocument = await JsonDocument.ParseAsync(compatibilityStream);
        Assert.Equal("Topology", compatibilityDocument.RootElement.GetProperty("type").GetString());

        var departmentsResponse = await _client.GetAsync("/api/v1/mapa/cartografia/departamentos");
        departmentsResponse.EnsureSuccessStatusCode();

        var cacheControl = departmentsResponse.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Public);
        Assert.True(cacheControl.MustRevalidate);
        Assert.Equal(TimeSpan.FromDays(1), cacheControl.MaxAge);
        Assert.NotNull(departmentsResponse.Headers.ETag);
        Assert.NotNull(departmentsResponse.Content.Headers.LastModified);

        using var conditionalRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/mapa/cartografia/departamentos");
        conditionalRequest.Headers.IfNoneMatch.Add(departmentsResponse.Headers.ETag);
        var conditionalResponse = await _client.SendAsync(conditionalRequest);
        Assert.Equal(HttpStatusCode.NotModified, conditionalResponse.StatusCode);
        Assert.Equal(0, conditionalResponse.Content.Headers.ContentLength ?? 0);

        await using var departmentsStream = await departmentsResponse.Content.ReadAsStreamAsync();
        using var departmentsDocument = await JsonDocument.ParseAsync(departmentsStream);
        Assert.Equal("Topology", departmentsDocument.RootElement.GetProperty("type").GetString());

        var optimizedObjects = departmentsDocument.RootElement.GetProperty("objects");
        Assert.True(optimizedObjects.TryGetProperty("MGN_ADM_DPTO_POLITICO", out var optimizedDepartments));
        Assert.False(optimizedObjects.TryGetProperty("MGN_ADM_MPIO_GRAFICO", out _));
        var optimizedDepartmentGeometries = optimizedDepartments.GetProperty("geometries");
        Assert.Equal(33, optimizedDepartmentGeometries.GetArrayLength());

        var departmentCodes = optimizedDepartmentGeometries
            .EnumerateArray()
            .Select(geometry => geometry.GetProperty("properties").GetProperty("dpto_ccdgo").GetString())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Cast<string>()
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(33, departmentCodes.Length);
        Assert.Equal(33, departmentCodes.Distinct(StringComparer.Ordinal).Count());

        var sanAndres = optimizedDepartmentGeometries
            .EnumerateArray()
            .Single(geometry => geometry.GetProperty("properties").GetProperty("dpto_ccdgo").GetString() == "88");
        var sanAndresBbox = sanAndres.GetProperty("bbox")
            .EnumerateArray()
            .Select(coordinate => coordinate.GetDouble())
            .ToArray();
        Assert.Equal(4, sanAndresBbox.Length);
        Assert.InRange(sanAndresBbox[0], -81.736, -81.735);
        Assert.InRange(sanAndresBbox[1], 12.480, 12.481);
        Assert.InRange(sanAndresBbox[2], -81.350, -81.349);
        Assert.InRange(sanAndresBbox[3], 13.394, 13.395);

        var municipalityCount = 0;
        foreach (var departmentCode in departmentCodes)
        {
            var municipalitiesResponse = await _client.GetAsync(
                $"/api/v1/mapa/cartografia/departamentos/{departmentCode}/municipios");
            municipalitiesResponse.EnsureSuccessStatusCode();
            Assert.True(municipalitiesResponse.Headers.CacheControl?.Public);
            Assert.NotNull(municipalitiesResponse.Headers.ETag);

            await using var municipalitiesStream = await municipalitiesResponse.Content.ReadAsStreamAsync();
            using var municipalitiesDocument = await JsonDocument.ParseAsync(municipalitiesStream);
            var municipalitiesObjects = municipalitiesDocument.RootElement.GetProperty("objects");
            Assert.True(municipalitiesObjects.TryGetProperty("MGN_ADM_MPIO_GRAFICO", out var municipalityChunkObject));
            Assert.False(municipalitiesObjects.TryGetProperty("MGN_ADM_DPTO_POLITICO", out _));

            var municipalityGeometries = municipalityChunkObject.GetProperty("geometries");
            Assert.NotEqual(0, municipalityGeometries.GetArrayLength());
            Assert.All(municipalityGeometries.EnumerateArray(), geometry =>
                Assert.Equal(departmentCode,
                    geometry.GetProperty("properties").GetProperty("dpto_ccdgo").GetString()));
            municipalityCount += municipalityGeometries.GetArrayLength();
        }

        Assert.Equal(1122, municipalityCount);

        var missingChunkResponse = await _client.GetAsync(
            "/api/v1/mapa/cartografia/departamentos/00/municipios");
        Assert.Equal(HttpStatusCode.NotFound, missingChunkResponse.StatusCode);
    }
}
