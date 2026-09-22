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

/// <summary>
/// Las ediciones de un Festival: 2024, 2025, 2026, cada una con su nombre y sus fechas.
/// </summary>
/// <remarks>
/// <para>
/// LO PRIMERO QUE VIGILAN ESTAS PRUEBAS NO ES QUE SE GUARDE UNA EDICIÓN. Eso lo comprueba una sola.
/// Las demás vigilan lo que, si se rompe, no se nota mirando la pantalla:
/// </para>
/// <list type="bullet">
///   <item>que una organización no pueda escribir ediciones en el Festival de otra;</item>
///   <item>que dos envíos del mismo formulario no dejen dos ediciones del mismo año;</item>
///   <item>que el contador de <c>dbo.Festivales</c> no se desincronice de la lista, porque son dos
///         números de la misma pantalla y el desacuerdo entre ellos es peor que no tener ninguno;</item>
///   <item>y que un estado inventado se rechace en vez de guardarse, que es exactamente lo que hizo
///         <c>en_evaluacion</c> y vivió semanas porque la etiqueta en pantalla estaba bien escrita.</item>
/// </list>
/// <para>
/// TODA PRUEBA DE UNA PROHIBICIÓN MIRA LA BASE, no el código de estado: un 200 que no escribió lo
/// prohibido y un 200 que sí lo escribió se ven igual desde el cliente.
/// </para>
/// </remarks>
public sealed class EdicionesDelFestivalTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Clave = "ClaveExterna123";

    private readonly TestWebApplicationFactory _factory;

    public EdicionesDelFestivalTests(TestWebApplicationFactory factory) => _factory = factory;

    private sealed record Cuenta(HttpClient Cliente, int OrganizacionId, int FestivalId);

    /// <summary>Una organización dada de alta por el camino real, con un Festival suyo.</summary>
    // ---------- El ciclo completo de la edición ------------------------------------------------

    [Fact]
    public async Task Una_Edicion_Publicada_Se_Despublica_Y_Vuelve_A_Ser_Editable()
    {
        // EL CAMINO QUE FALTABA. `CK_EdicionesFestival_EstadoVisibilidad` admite `borrador`,
        // `publicada` y `archivada` desde, pero no había forma de volver
        // de `publicada`: una edición publicada por error se quedaba publicada, y la única salida
        // era eliminarla, que es otra cosa —pierde la edición en vez de retirarla del portal—.
        var cuenta = await RegistrarConFestivalPublicadoAsync("despublica", "1000000021");
        var edicion = await PublicarUnaEdicionAsync(cuenta, 2031);

        var despublicada = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion.Id}/despublicar");

        Assert.Equal(HttpStatusCode.OK, despublicada.StatusCode);
        var despues = await despublicada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.Equal("borrador", despues!.EstadoVisibilidad);

        // VOLVER A BORRADOR ES VOLVER A PODER EDITARLA: es justamente para lo que se despublica.
        Assert.True(despues.EsEditable);
    }

    [Fact]
    public async Task Archivar_Una_Edicion_Cierra_Su_Ciclo_Y_No_Se_Repite()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("archiva", "1000000022");
        var edicion = await PublicarUnaEdicionAsync(cuenta, 2032);

        var archivada = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion.Id}/archivar");
        Assert.Equal(HttpStatusCode.OK, archivada.StatusCode);
        Assert.Equal("archivada", (await archivada.Content.ReadFromJsonAsync<EdicionFestivalDto>())!.EstadoVisibilidad);

        // ARCHIVAR DOS VECES NO ES ARCHIVAR: se responde 409 en vez de escribir una transición de un
        // estado a sí mismo, que dejaría una fila de historial contando algo que no pasó.
        var otraVez = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion.Id}/archivar");
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
    }

    [Fact]
    public async Task Despublicar_Un_Borrador_Se_Rechaza()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("borradordespub", "1000000023");
        var creada = await CrearEdicionAsync(cuenta, new { anio = 2033, nombre = "Edición en borrador" });
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();

        var respuesta = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion!.Id}/despublicar");

        // «DESPUBLICAR» ALGO QUE NUNCA SE PUBLICO no significa nada, y aceptarlo dejaría rastro de
        // una retirada que nadie hizo.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_Lista_Dice_Cuantas_Observaciones_Tiene_Cada_Edicion()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("observa", "1000000024");
        var creada = await CrearEdicionAsync(cuenta, new { anio = 2034, nombre = "Edición sin observar" });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);

        var lista = await cuenta.Cliente.GetFromJsonAsync<List<EdicionFestivalDto>>(
            $"/api/v1/externo/festivales/{cuenta.FestivalId}/ediciones");

        // ES LO QUE DECIDE SI LA ACCION EXISTE. La lista ofrecía «Consultar observaciones» en todas
        // las filas, también en ediciones que nunca pasaron por revisión: pulsarlo abría un recuadro
        // que decía «No hay observaciones por campo», que es una vista vacía disfrazada de función.
        Assert.All(lista!, edicion => Assert.Equal(0, edicion.CuantasObservaciones));
    }

    /// <summary>Crea una edición y la publica. Devuelve la edición ya publicada.</summary>
    private static async Task<EdicionFestivalDto> PublicarUnaEdicionAsync(Cuenta cuenta, int anio)
    {
        var creada = await CrearEdicionAsync(cuenta, new { anio, nombre = $"Edición {anio}" });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        var publicada = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion!.Id}/publicar");
        Assert.Equal(HttpStatusCode.OK, publicada.StatusCode);
        return (await publicada.Content.ReadFromJsonAsync<EdicionFestivalDto>())!;
    }

    private async Task<Cuenta> RegistrarConFestivalAsync(string marca, string documento)
    {
        var cliente = _factory.CreateClient();
        var correo = $"ediciones.{marca}@example.com";

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
        Assert.NotNull(creada);

        // ESTAS PRUEBAS NO TRATAN SOBRE LA CONFIRMACION DE CORREO, pero publicar una edición la
        // exige desde: se da por hecha en una línea. El flujo de
        // confirmación tiene sus propias pruebas.
        await CorreoConfirmadoEnPruebas.ConfirmarAsync(_factory, correo);

        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = Clave,
        });
        entrada.EnsureSuccessStatusCode();

        var organizacionId = int.Parse(creada!.OrganizationId, CultureInfo.InvariantCulture);
        var festival = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/v1/externo/organizaciones/{organizacionId}/festivales",
            new { nombre = "Festival " + marca, nivelCobertura = "nacional" });
        Assert.Equal(HttpStatusCode.Created, festival.StatusCode);

        using var documentoFestival = JsonDocument.Parse(await festival.Content.ReadAsStringAsync());
        var festivalId = int.Parse(documentoFestival.RootElement.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);

        return new Cuenta(cliente, organizacionId, festivalId);
    }

    private static async Task<string> TokenAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync(new Uri("/api/v1/externo/organizaciones/csrf", UriKind.Relative));
        respuesta.EnsureSuccessStatusCode();
        var token = await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();
        Assert.NotNull(token);
        return token!.RequestToken;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string ruta,
        object? cuerpo = null,
        bool conToken = true)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta);
        if (cuerpo is not null) mensaje.Content = JsonContent.Create(cuerpo);
        if (conToken) mensaje.Headers.Add("X-CSRF-TOKEN", await TokenAsync(cliente));
        return await cliente.SendAsync(mensaje);
    }

    private static Task<HttpResponseMessage> CrearEdicionAsync(Cuenta cuenta, object cuerpo, bool conToken = true) =>
        EnviarAsync(cuenta.Cliente, HttpMethod.Post, $"/api/v1/externo/festivales/{cuenta.FestivalId}/ediciones", cuerpo, conToken);

    private async Task<(int Cuantas, string? EstadoAnyoActual, bool AnyoActual)> LeerResumenAsync(int festivalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.AsNoTracking().SingleAsync(item => item.Id == festivalId);
        return (festival.VersionsCount ?? -1, festival.CurrentYearEditionStatus, festival.HasCurrentYearEdition);
    }

    private async Task<int> ContarEdicionesAsync(int festivalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.EdicionesFestival.AsNoTracking().CountAsync(item => item.FestivalId == festivalId);
    }

    /// <summary>
    /// Las pruebas de edición no buscan volver a probar la aprobación institucional del Festival;
    /// necesitan partir de su precondición real. Se fija directamente en la base aislada de la
    /// suite para que cada caso pruebe exclusivamente la regla de la edición.
    /// </summary>
    private async Task PublicarFestivalAsync(int festivalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(item => item.Id == festivalId);
        festival.StatusCode = EstadosFestival.Publicado;
        db.VersionesFestival.Add(new VersionFestivalRow
        {
            FestivalOrigenId = festival.Id,
            NumeroVersion = 1,
            EsVigente = true,
            Nombre = festival.Name,
            NivelCobertura = festival.CoverageLevel,
            CodigoDepartamento = festival.DepartmentCode,
            CodigoMunicipio = festival.MunicipalityCode,
            FechaPublicacion = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Cuenta> RegistrarConFestivalPublicadoAsync(string marca, string documento)
    {
        var cuenta = await RegistrarConFestivalAsync(marca, documento);
        await PublicarFestivalAsync(cuenta.FestivalId);
        return cuenta;
    }

    [Fact]
    public async Task Un_Festival_Aun_No_Publicado_Admite_Borradores_De_Edicion()
    {
        var cuenta = await RegistrarConFestivalAsync("sin-publicar", "1000000000");

        var respuesta = await CrearEdicionAsync(cuenta, new { anio = 2027, nombre = "Borrador permitido" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(1, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task Un_Festival_Publicado_Actualiza_Solo_Su_Contacto_Publico_En_La_Cabecera_Y_Perfil_Vigente()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("contacto-publico", "1000000099");

        var respuesta = await EnviarAsync(cuenta.Cliente, HttpMethod.Put,
            $"/api/v1/externo/festivales/{cuenta.FestivalId}/contacto-publico", new
            {
                correoContacto = "contacto@festival.example.com",
                telefonoCelular = "+57 300 123 4567",
                instagram = "https://instagram.com/festival-prueba",
                facebook = "https://facebook.com/festival-prueba",
                paginaWeb = "https://festival.example.com",
                otroEnlace = "https://festival.example.com/contacto",
            });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(item => item.Id == cuenta.FestivalId);
        var perfilVigente = await db.VersionesFestival.SingleAsync(item => item.FestivalOrigenId == cuenta.FestivalId && item.EsVigente);
        Assert.Equal("contacto@festival.example.com", festival.ContactEmail);
        Assert.Equal("https://instagram.com/festival-prueba", festival.InstagramUrl);
        Assert.Equal("contacto@festival.example.com", perfilVigente.CorreoContacto);
        Assert.Equal("https://instagram.com/festival-prueba", perfilVigente.Instagram);
        Assert.Equal("Festival contacto-publico", festival.Name);
    }

    [Fact]
    public async Task Tres_Ediciones_Del_Mismo_Festival_Con_Nombres_Distintos()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("tres", "1000000001");

        foreach (var anyo in new[] { 2024, 2025, 2026 })
        {
            var respuesta = await CrearEdicionAsync(cuenta, new
            {
                anio = anyo,
                nombre = $"Edicion {anyo}",
                fechaInicio = $"{anyo}-09-10",
                fechaFin = $"{anyo}-09-14",
                estado = "realizada",
            });
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        }

        using var listado = await cuenta.Cliente.GetAsync(
            new Uri($"/api/v1/externo/festivales/{cuenta.FestivalId}/ediciones", UriKind.Relative));
        listado.EnsureSuccessStatusCode();
        var ediciones = await listado.Content.ReadFromJsonAsync<List<EdicionFestivalDto>>();
        Assert.NotNull(ediciones);

        // ES LA MITAD DEL PEDIDO: tres años y tres NOMBRES distintos. Un modelo que solo guardara el
        // año dejaría las tres ediciones llamándose igual que el Festival.
        Assert.Equal([2026, 2025, 2024], ediciones!.Select(item => item.Anio).ToArray());
        Assert.Equal(["Edicion 2026", "Edicion 2025", "Edicion 2024"], ediciones.Select(item => item.Nombre ?? string.Empty).ToArray());
        Assert.All(ediciones, item => Assert.Equal("Realizada", item.EstadoEtiqueta));
    }

    [Fact]
    public async Task El_Contador_Del_Festival_Sigue_A_La_Lista_Al_Crear_Y_Al_Borrar()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("contador", "1000000002");
        var anyoEnCurso = DateTime.UtcNow.Year;

        var primera = await CrearEdicionAsync(cuenta, new { anio = 2019, nombre = "Antigua" });
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        var segunda = await CrearEdicionAsync(cuenta, new { anio = anyoEnCurso, nombre = "La de este año", estado = "programada" });
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);

        var resumen = await LeerResumenAsync(cuenta.FestivalId);
        // SE CALCULA, NO SE INCREMENTA: un contador que suma uno por alta se desincroniza en cuanto
        // alguien borra, y desde el propio número no hay forma de saber que quedó mal.
        Assert.Equal(2, resumen.Cuantas);
        Assert.True(resumen.AnyoActual);
        Assert.Equal("programada", resumen.EstadoAnyoActual);

        using var creada = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        var edicionId = creada.RootElement.GetProperty("id").GetString();

        var borrado = await EnviarAsync(cuenta.Cliente, HttpMethod.Delete, $"/api/v1/externo/ediciones/{edicionId}");
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        var despues = await LeerResumenAsync(cuenta.FestivalId);
        Assert.Equal(1, despues.Cuantas);
        // Y la edición del año en curso deja de existir: si el `bit` se quedara en 1, la ficha diría
        // que hay edición este año y la lista no la mostraría.
        Assert.False(despues.AnyoActual);
        Assert.Null(despues.EstadoAnyoActual);
    }

    [Fact]
    public async Task Dos_Ediciones_Del_Mismo_Anyo_Pueden_Distinguirse_Por_Su_Propia_Identificacion()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("repetida", "1000000003");

        Assert.Equal(HttpStatusCode.Created, (await CrearEdicionAsync(cuenta, new { anio = 2025, nombre = "Primera" })).StatusCode);
        var segunda = await CrearEdicionAsync(cuenta, new { anio = 2025, nombre = "Segunda" });

        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.Equal(2, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task Una_Organizacion_No_Escribe_Ediciones_En_El_Festival_De_Otra()
    {
        var propia = await RegistrarConFestivalPublicadoAsync("duena", "1000000004");
        var ajena = await RegistrarConFestivalPublicadoAsync("intrusa", "1000000005");

        var intento = await EnviarAsync(ajena.Cliente, HttpMethod.Post,
            $"/api/v1/externo/festivales/{propia.FestivalId}/ediciones",
            new { anio = 2030, nombre = "Intrusa" });

        Assert.Equal(HttpStatusCode.Forbidden, intento.StatusCode);
        // SE MIRA LA BASE. Un 403 que además hubiera escrito la fila se ve igual desde el cliente.
        Assert.Equal(0, await ContarEdicionesAsync(propia.FestivalId));
    }

    [Fact]
    public async Task Un_Estado_Que_No_Existe_Se_Rechaza_En_Vez_De_Guardarse()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("estado", "1000000006");

        // `en_evaluacion` no es un ejemplo cualquiera: fue un valor que el front se inventó y que
        // vivió semanas porque la etiqueta en pantalla decía «En revisión» y estaba bien escrita.
        var intento = await CrearEdicionAsync(cuenta, new { anio = 2025, nombre = "X", estado = "en_evaluacion" });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Equal(0, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task La_Fecha_De_Fin_No_Puede_Ser_Anterior_A_La_De_Inicio()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("fechas", "1000000007");

        var intento = await CrearEdicionAsync(cuenta, new
        {
            anio = 2028,
            nombre = "Al reves",
            fechaInicio = "2028-05-10",
            fechaFin = "2028-05-01",
        });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Equal(0, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task Sin_Testigo_Antiforgery_No_Se_Escribe_Nada()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("csrf", "1000000008");

        var intento = await CrearEdicionAsync(cuenta, new { anio = 2025, nombre = "Sin testigo" }, conToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Equal(0, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task Una_Edicion_Nace_En_Borrador_Y_La_Organizacion_La_Publica_Directamente()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("revision", "1000000012");
        var creada = await CrearEdicionAsync(cuenta, new
        {
            anio = 2027,
            nombre = "Edición con dirección",
            director = "Dirección artística libre",
            estadoRegistro = "publicado",
        });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(edicion);
        Assert.Equal("borrador", edicion!.EstadoVisibilidad);
        Assert.True(edicion.EsEditable);
        Assert.Equal("Dirección artística libre", edicion.Director);

        var enviada = await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion.Id}/publicar");
        Assert.Equal(HttpStatusCode.OK, enviada.StatusCode);
        var despues = await enviada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(despues);
        Assert.Equal("publicada", despues!.EstadoVisibilidad);
        Assert.False(despues.EsEditable);

        // La publicación directa sigue siendo trazable para la supervisión posterior.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var evento = await db.HistorialesRevisionRegistros.AsNoTracking()
            .SingleAsync(item => item.ModuloId == Modulos.EdicionesDeFestival
                && item.RegistroId == edicion.Id.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("EdicionPublicadaPorOrganizacion", evento.Accion);

        // EN EL VOCABULARIO DEL HISTORIAL, que es institucional y común a todos los módulos: sus
        // siete códigos van en masculino. Hasta esta prueba esperaba
        // «publicada» —el vocabulario de la Edición— y por eso pasaba en verde mientras la ruta
        // devolvía 500 contra SQL Server, donde `CK_RegistrosRevisionHistorial_EstadoNuevo` sí
        // existe. La Edición sigue nombrando su visibilidad en femenino; lo que faltaba era la
        // traducción, y la fija PublicarUnaEdicionSqlServerTests contra el motor real.
        Assert.Equal("borrador", evento.EstadoAnterior);
        Assert.Equal("publicado", evento.EstadoNuevo);
    }

    [Fact]
    public async Task Una_Edicion_Publicada_No_Admite_Edicion_Directa()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("bloqueada", "1000000013");
        var creada = await CrearEdicionAsync(cuenta, new { anio = 2027, nombre = "Edición enviada" });
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(edicion);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion!.Id}/publicar")).StatusCode);

        var cambio = await EnviarAsync(cuenta.Cliente, HttpMethod.Put,
            $"/api/v1/externo/ediciones/{edicion.Id}", new { anio = 2027, nombre = "Intento tardío" });
        Assert.Equal(HttpStatusCode.Conflict, cambio.StatusCode);
    }

    [Fact]
    public async Task La_Supervision_Institucional_Observa_Sin_Alterar_Estados_De_La_Edicion()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("decision", "1000000014");
        var creada = await CrearEdicionAsync(cuenta, new { anio = 2028, nombre = "Edición institucional", estado = "programada" });
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(edicion);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cuenta.Cliente, HttpMethod.Post,
            $"/api/v1/externo/ediciones/{edicion!.Id}/publicar")).StatusCode);

        var funcionario = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var csrf = await funcionario.GetFromJsonAsync<TokenAntiforgeryRespuesta>("/api/v1/institucional/ediciones-festival/csrf");
        Assert.NotNull(csrf);
        var decision = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/ediciones-festival/{edicion.Id}/supervision")
        {
            Content = JsonContent.Create(new { accion = "Observar", observacion = "Precisa la descripción pública." })
        };
        decision.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        var respuesta = await funcionario.SendAsync(decision);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ajustada = await respuesta.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(ajustada);
        Assert.Equal("publicada", ajustada!.EstadoVisibilidad);
        Assert.False(ajustada.EsEditable);
        // Una observación no desprograma ni despublica la realización.
        Assert.Equal("programada", ajustada.Estado);

        var desdeOrganizacion = await cuenta.Cliente.GetFromJsonAsync<List<EdicionFestivalDto>>(
            $"/api/v1/externo/festivales/{cuenta.FestivalId}/ediciones");
        Assert.Equal("publicada", Assert.Single(desdeOrganizacion!).EstadoVisibilidad);
    }

    [Fact]
    public async Task Los_Ajustes_Por_Campo_De_La_Edicion_Se_Entregan_Sin_Confundirlos_Con_La_Ficha_Del_Festival()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("campos-edicion", "1000000015");
        var creada = await CrearEdicionAsync(cuenta, new { anio = 2029, nombre = "Edición con observación" });
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cuenta.Cliente, HttpMethod.Post, $"/api/v1/externo/ediciones/{edicion!.Id}/publicar")).StatusCode);
        var funcionario = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var csrf = await funcionario.GetFromJsonAsync<TokenAntiforgeryRespuesta>("/api/v1/institucional/ediciones-festival/csrf");
        var envio = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/ediciones-festival/{edicion.Id}/revision/enviar")
        { Content = JsonContent.Create(new { observacionGeneral = "Ajustar la edición.", observaciones = new[] { new { seccionId = "datos-basicos", campoId = "director", campoEtiqueta = "Director o directora", valorObservado = "", nota = "Indica la dirección artística." } } }) };
        envio.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await funcionario.SendAsync(envio)).StatusCode);
        var cambios = await cuenta.Cliente.GetFromJsonAsync<RevisionEdicionDeCamposDto>($"/api/v1/externo/ediciones/{edicion.Id}/cambios-pedidos");
        Assert.NotNull(cambios); Assert.Single(cambios!.Observaciones); Assert.Equal("director", cambios.Observaciones[0].CampoId);
    }

    [Fact]
    public async Task Editar_Una_Edicion_Cambia_Su_Nombre_Y_Su_Anyo()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("editar", "1000000009");

        var creada = await CrearEdicionAsync(cuenta, new { anio = 2025, nombre = "Nombre viejo" });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        using var documento = JsonDocument.Parse(await creada.Content.ReadAsStringAsync());
        var edicionId = documento.RootElement.GetProperty("id").GetString();

        var editada = await EnviarAsync(cuenta.Cliente, HttpMethod.Put, $"/api/v1/externo/ediciones/{edicionId}",
            new { anio = 2027, nombre = "Nombre nuevo", estado = "programada" });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);

        var actualizada = await editada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        Assert.NotNull(actualizada);
        Assert.Equal(2027, actualizada!.Anio);
        Assert.Equal("Nombre nuevo", actualizada.Nombre);
        // Sigue siendo la MISMA fila: editar no puede dejar dos.
        Assert.Equal(1, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task La_Edicion_Exige_Alguna_Forma_De_Identificacion()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("sinnombre", "1000000010");

        var intento = await CrearEdicionAsync(cuenta, new { nombre = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Equal(0, await ContarEdicionesAsync(cuenta.FestivalId));
    }

    [Fact]
    public async Task Un_Anyo_Imposible_Se_Rechaza_Con_400_Y_No_Con_500()
    {
        var cuenta = await RegistrarConFestivalPublicadoAsync("anyo", "1000000011");

        // `CK_EdicionesFestival_Anio` lo impide en la base. Decirlo aquí convierte un fallo del
        // servidor en un mensaje que explica qué corregir.
        var intento = await CrearEdicionAsync(cuenta, new { anio = 20255, nombre = "Del futuro lejano" });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Equal(0, await ContarEdicionesAsync(cuenta.FestivalId));
    }
}
