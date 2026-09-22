using System.Linq;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El perfil de una organización del ecosistema y su persona responsable, administrados por ella
/// misma desde <c>/api/v1/externo/organizaciones/{id}/perfil</c> y <c>.../responsable</c>.
/// </summary>
/// <remarks>
/// <para>
/// LO QUE ESTAS PRUEBAS VIGILAN NO ES QUE LOS CAMPOS VIAJEN. Eso lo comprueba una sola de ellas.
/// Las demás vigilan las cuatro cosas que, si se rompen, no se notan mirando la pantalla: que una
/// organización no pueda tocar la de otra, que el externo no pueda moverse solo en la escalera de
/// moderación, que un texto más largo que su columna se rechace en vez de guardarse cortado, y que
/// la bitácora escriba un verbo que <c>CK_BitacoraAuditoria_Accion</c> admita.
/// </para>
/// <para>
/// TODA PRUEBA QUE COMPRUEBA UNA PROHIBICIÓN MIRA LA BASE, no el código de estado. Un 200 que no
/// escribió lo prohibido y un 200 que sí lo escribió se ven igual desde el cliente.
/// </para>
/// </remarks>
public sealed class PerfilDeLaOrganizacionTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Clave = "ClaveExterna123";
    private static readonly string[] ErrorDeNombre = ["name"];

    private readonly TestWebApplicationFactory _factory;

    public PerfilDeLaOrganizacionTests(TestWebApplicationFactory factory) => _factory = factory;

    private sealed record Cuenta(HttpClient Cliente, int OrganizacionId, string Correo);

    /// <summary>
    /// Una cuenta externa con su organización recién dada de alta y la sesión abierta.
    /// </summary>
    /// <remarks>
    /// SE PASA POR EL ALTA REAL Y NO SE SIEMBRA LA FILA A MANO. El alta escribe las tres filas que
    /// estas rutas necesitan —<c>Entidades</c>, <c>UsuariosEntidades</c> y
    /// <c>EntidadesResponsable</c>— y las escribe como las escribe producción: con
    /// <c>EstadoRegistro = 'registrada'</c>, con el tipo de documento en MAYÚSCULA y con el correo
    /// de la persona copiado de su credencial. Sembrar a mano habría dejado fuera justo los tres
    /// detalles que estas pruebas comprueban.
    /// </remarks>
    private async Task<Cuenta> RegistrarAsync(string marca, string documento)
    {
        var cliente = _factory.CreateClient();
        var correo = $"perfil.{marca}@example.com";

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

        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = Clave,
        });
        entrada.EnsureSuccessStatusCode();

        return new Cuenta(cliente, int.Parse(creada!.OrganizationId, CultureInfo.InvariantCulture), correo);
    }

    private static async Task<string> TokenAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        var token = await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();
        Assert.NotNull(token);
        return token!.RequestToken;
    }

    private static async Task<HttpResponseMessage> GuardarAsync(
        HttpClient cliente,
        string ruta,
        object cuerpo,
        bool conToken = true)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Put, ruta) { Content = JsonContent.Create(cuerpo) };
        if (conToken)
        {
            mensaje.Headers.Add("X-CSRF-TOKEN", await TokenAsync(cliente));
        }

        return await cliente.SendAsync(mensaje);
    }

    private static Task<HttpResponseMessage> GuardarPerfilAsync(Cuenta cuenta, object cuerpo, bool conToken = true) =>
        GuardarAsync(cuenta.Cliente, $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil", cuerpo, conToken);

    /// <summary>Un cuerpo válido y completo, para que cada prueba solo cambie lo suyo.</summary>
    private static Dictionary<string, object?> CuerpoDePerfil(string nombre) => new()
    {
        ["nombre"] = nombre,
        ["nombreLegal"] = nombre + " S.A.S.",
        ["numeroIdentificacion"] = null,
        ["descripcion"] = "Trabaja con bandas escolares.",
        // UN CORREO POR ORGANIZACION, y por eso se deriva del nombre en vez de ser fijo.
        //
        // ERA `contacto@organizacion.test` PARA TODAS. Cada prueba de este fichero registra su
        // propia cuenta —con su propia organizacion— y despues le guardaba el perfil con ese correo:
        // siete organizaciones distintas compartiendo buzon. El 28 de agosto de 2026 Se define
        // que «un correo debe estar atado a una sola organizacion», y con la regla puesta la segunda
        // en guardar recibia un 400 legitimo. Lo que estaba mal era el andamio, no la regla.
        ["correoContacto"] = $"contacto.{Ranura(nombre)}@organizacion.test",
        ["telefonoContacto"] = "6041234567",
        ["sitioWeb"] = "https://organizacion.test",
        ["facebook"] = "https://facebook.com/organizacion",
        ["instagram"] = "https://instagram.com/organizacion",
        ["otroEnlace"] = "https://youtube.com/@organizacion",
        ["direccion"] = "Calle 50 # 40-20",
        ["codigoDepartamentoSede"] = "11",
        ["codigoMunicipioSede"] = "11001",
    };

    /// <summary>Un trozo de nombre valido para un correo: minusculas, sin tildes y sin espacios.</summary>
    private static string Ranura(string nombre)
    {
        var normalizado = nombre.Normalize(System.Text.NormalizationForm.FormD);
        var letras = normalizado
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        var ranura = new string(letras.ToArray()).Trim('-');
        // SE CORTA A TREINTA. Una prueba de este fichero guarda un nombre de 240 caracteres para
        // medir el tope; sin el corte, el correo derivado tendria 240 y lo rechazaria el validador
        // de formato, convirtiendo una prueba del tope del NOMBRE en un fallo del CORREO.
        return ranura.Length <= 30 ? ranura : ranura[..30].Trim('-');
    }

    private static Dictionary<string, object?> CuerpoDeResponsable(string nombre) => new()
    {
        ["responsableNombre"] = nombre,
        ["responsableTipoDocumento"] = "cc",
        ["responsableNumeroDocumento"] = "1098765432",
        ["responsableTelefono"] = "3009998877",
    };

    private async Task<EntityProfileRow> LeerEntidadAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.EntityProfiles.AsNoTracking().FirstAsync(fila => fila.Id == id);
    }

    private async Task<EntidadResponsableRow?> LeerResponsableAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.EntidadesResponsable.AsNoTracking().FirstOrDefaultAsync(fila => fila.IdEntidad == id);
    }

    /// <summary>
    /// Se mira el JSON en crudo y no un DTO: una clave de error nueva no cambia ningún tipo, así que
    /// deserializar dejaría pasar sin ruido el día en que el error salga apuntando a otro campo.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ClavesDeErrorAsync(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.TryGetProperty("errors", out var errores)
            ? errores.EnumerateObject().Select(propiedad => propiedad.Name).ToList()
            : [];
    }

    /// <summary>
    /// El arnés siembra siete estados y «registrada» no es uno de ellos, aunque sí esté en la base
    /// local y sea con el que nace toda organización dada de alta desde el sitio. Sin esta fila, la
    /// etiqueta del estado no podría comprobarse contra <c>dbo.EstadosContenido</c>.
    /// </summary>
    private async Task AsegurarEstadoRegistradaAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        if (await db.ContentStatuses.AnyAsync(fila => fila.Code == "registrada")) return;

        db.ContentStatuses.Add(new ContentStatusRow { Code = "registrada", Name = "Registrada" });
        await db.SaveChangesAsync();
    }

    // ==========================================================================================
    // LO QUE EL PANEL NECESITA LEER Y ESCRIBIR
    // ==========================================================================================

    [Fact]
    public async Task El_Perfil_Guarda_Y_Devuelve_Los_Campos_Que_La_Ruta_Vieja_No_Trae()
    {
        // LOS ONCE CAMPOS DESCRIPTIVOS SON EL MOTIVO DE ESTA RUTA. GET /external/organizations/{id}
        // devuelve nueve campos y ninguno de ellos, así que un panel construido sobre aquella ruta
        // no puede mostrar ni la descripción ni el teléfono ni las redes de la organización.
        await AsegurarEstadoRegistradaAsync();
        var cuenta = await RegistrarAsync("completo", "1020304060");

        var cuerpo = CuerpoDePerfil("Fundación Sonidos del Río");
        cuerpo["numeroIdentificacion"] = "900555444-1";
        var guardado = await GuardarPerfilAsync(cuenta, cuerpo);
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);

        var perfil = await cuenta.Cliente.GetFromJsonAsync<PerfilOrganizacionDto>(
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");
        Assert.NotNull(perfil);

        Assert.Equal("Fundación Sonidos del Río", perfil!.Nombre);
        Assert.Equal("Fundación Sonidos del Río S.A.S.", perfil.NombreLegal);
        Assert.Equal("Trabaja con bandas escolares.", perfil.Descripcion);
        Assert.Equal("6041234567", perfil.TelefonoContacto);
        Assert.Equal("https://organizacion.test", perfil.SitioWeb);
        Assert.Equal("https://facebook.com/organizacion", perfil.Facebook);
        Assert.Equal("https://instagram.com/organizacion", perfil.Instagram);
        Assert.Equal("https://youtube.com/@organizacion", perfil.OtroEnlace);
        Assert.Equal("Calle 50 # 40-20", perfil.Direccion);
        Assert.Equal("900555444-1", perfil.NumeroIdentificacion);

        // El perfil expone únicamente la sede registral; el alcance pertenece a los procesos.
        Assert.Equal("11", perfil.CodigoDepartamentoSede);
        Assert.Equal("Bogota, D.C.", perfil.NombreDepartamentoSede);
        Assert.Equal("11001", perfil.CodigoMunicipioSede);
        Assert.Equal("Bogota, D.C.", perfil.NombreMunicipioSede);

        // La etiqueta sale de dbo.EstadosContenido, que es la tabla a la que apunta
        // FK_Entidades_EstadosContenido: es la única lista que no puede desincronizarse del dato.
        Assert.Equal("activa", perfil.EstadoRegistro);
        Assert.Equal("Activa", perfil.EstadoRegistroEtiqueta);
        Assert.NotNull(perfil.FechaActualizacion);
    }

    // ==========================================================================================
    // AUTORIZACION
    // ==========================================================================================

    [Fact]
    public async Task Una_Organizacion_No_Lee_Ni_Escribe_La_De_Otra()
    {
        var duena = await RegistrarAsync("duena", "1020304061");
        var ajena = await RegistrarAsync("ajena", "1020304062");

        var lecturaDelPerfil = await ajena.Cliente.GetAsync($"/api/v1/externo/organizaciones/{duena.OrganizacionId}/perfil");
        Assert.True(
            lecturaDelPerfil.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Se esperaba 403 o 404 al leer el perfil ajeno y llegó {(int)lecturaDelPerfil.StatusCode}.");

        // EL RESPONSABLE VA APARTE PORQUE ES DATO PERSONAL DE UN TERCERO (Ley 1581 de 2012): el
        // nombre y la cédula de una persona no pueden alcanzarse desde la sesión de otra.
        var lecturaDelResponsable = await ajena.Cliente.GetAsync($"/api/v1/externo/organizaciones/{duena.OrganizacionId}/responsable");
        Assert.True(
            lecturaDelResponsable.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Se esperaba 403 o 404 al leer el responsable ajeno y llegó {(int)lecturaDelResponsable.StatusCode}.");

        var escrituraDelPerfil = await GuardarAsync(
            ajena.Cliente,
            $"/api/v1/externo/organizaciones/{duena.OrganizacionId}/perfil",
            CuerpoDePerfil("Organizacion Secuestrada"));
        Assert.True(
            escrituraDelPerfil.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Se esperaba 403 o 404 al escribir el perfil ajeno y llegó {(int)escrituraDelPerfil.StatusCode}.");

        var escrituraDelResponsable = await GuardarAsync(
            ajena.Cliente,
            $"/api/v1/externo/organizaciones/{duena.OrganizacionId}/responsable",
            CuerpoDeResponsable("Persona Suplantada"));
        Assert.True(
            escrituraDelResponsable.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Se esperaba 403 o 404 al escribir el responsable ajeno y llegó {(int)escrituraDelResponsable.StatusCode}.");

        // LA MITAD QUE IMPORTA: un rechazo que ya escribió no es un rechazo. Se pregunta a la base,
        // porque desde el cliente un 403 antes de guardar y uno después se ven igual.
        var entidad = await LeerEntidadAsync(duena.OrganizacionId);
        Assert.Equal("Organizacion duena", entidad.Name);
        var responsable = await LeerResponsableAsync(duena.OrganizacionId);
        Assert.NotNull(responsable);
        Assert.Equal("Persona duena", responsable!.ResponsableNombre);
    }

    [Fact]
    public async Task Sin_Sesion_Externa_Ninguna_De_Las_Cuatro_Rutas_Contesta()
    {
        var cuenta = await RegistrarAsync("anonima", "1020304063");
        var anonimo = _factory.CreateClient();

        var perfil = await anonimo.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");
        Assert.Equal(HttpStatusCode.Unauthorized, perfil.StatusCode);

        var responsable = await anonimo.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable");
        Assert.Equal(HttpStatusCode.Unauthorized, responsable.StatusCode);

        var escritura = await anonimo.PutAsJsonAsync(
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil",
            CuerpoDePerfil("Organizacion Sin Sesion"));
        Assert.Equal(HttpStatusCode.Unauthorized, escritura.StatusCode);
    }

    [Fact]
    public async Task Sin_Token_Antiforgery_No_Se_Guarda_Nada()
    {
        var cuenta = await RegistrarAsync("sintoken", "1020304064");

        var perfil = await GuardarPerfilAsync(cuenta, CuerpoDePerfil("Organizacion Sin Token"), conToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, perfil.StatusCode);

        var responsable = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            CuerpoDeResponsable("Persona Sin Token"),
            conToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, responsable.StatusCode);

        var entidad = await LeerEntidadAsync(cuenta.OrganizacionId);
        Assert.Equal("Organizacion sintoken", entidad.Name);
    }

    // ==========================================================================================
    // LO QUE EL EXTERNO NO PUEDE TOCAR
    // ==========================================================================================

    [Fact]
    public async Task Lo_Institucional_No_Cambia_Aunque_Viaje_En_El_Cuerpo()
    {
        // ESTA ES LA PRUEBA QUE CONVIERTE LA REGLA EN REGLA. Sin ella, «el externo no puede
        // publicarse solo» es un comentario. El cuerpo lleva a propósito siete campos que no están
        // en PerfilOrganizacionSolicitud: si alguien añadiera el campo al DTO y la asignación al
        // manejador, esto se pone rojo.
        var cuenta = await RegistrarAsync("institucional", "1020304065");
        var antes = await LeerEntidadAsync(cuenta.OrganizacionId);

        var cuerpo = CuerpoDePerfil("Organizacion Que Se Autopublica");
        cuerpo["estadoRegistro"] = "publicado";
        cuerpo["esInstitucional"] = true;
        cuerpo["activo"] = false;
        cuerpo["tipoEntidad"] = "festival";
        cuerpo["latitud"] = 4.65m;
        cuerpo["longitud"] = -74.05m;
        cuerpo["idUsuarioResponsable"] = 1;

        var guardado = await GuardarPerfilAsync(cuenta, cuerpo);
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);

        var despues = await LeerEntidadAsync(cuenta.OrganizacionId);

        // Lo que SÍ se pidió cambiar, cambió: si no, esta prueba pasaría con un manejador que no
        // escribe nada en absoluto.
        Assert.Equal("Organizacion Que Se Autopublica", despues.Name);

        // La escalera de moderación no se mueve desde fuera. El único sitio del API que escribe
        // El perfil externo no expone ninguna transición institucional de EstadoRegistro.
        Assert.Equal(antes.StatusCode, despues.StatusCode);
        Assert.Equal("activa", despues.StatusCode);

        // UQ_Entidades_EsInstitucional es un índice único filtrado con UNA sola fila hoy: ponerlo a
        // 1 desde fuera reventaría el guardado o convertiría a esta organización en la responsable
        // por defecto de todo registro sin dueño.
        Assert.False(despues.IsInstitutional);
        Assert.True(despues.IsActive);
        Assert.Equal("organizacion", despues.EntityType);
        // Latitud y Longitud ya no son columnas de dbo.Entidades: V20260912_03 las retiró porque
        // eran del geovisor de septiembre y ningún camino del API las escribía. La ubicación propia
        // de una organización es su sede DIVIPOLA, y esa NO se toca desde este cuerpo.
        Assert.Equal(antes.HeadquartersDepartmentCode, despues.HeadquartersDepartmentCode);
        Assert.Equal(antes.HeadquartersMunicipalityCode, despues.HeadquartersMunicipalityCode);
        Assert.Equal(antes.CreatedByUserId, despues.CreatedByUserId);
        Assert.Equal(antes.ResponsibleUserId, despues.ResponsibleUserId);
        Assert.Equal(antes.CreatedAt, despues.CreatedAt);
    }

    [Fact]
    public async Task Con_Un_Festival_Ya_Entregado_El_Numero_De_Identificacion_Se_Sigue_Pudiendo_Corregir()
    {
        // ESTA PRUEBA FIJABA LO CONTRARIO HASTA EL 14 DE SEPTIEMBRE DE 2026. Vigilaba que, en cuanto
        // el Programa recibía un registro de la organización, el NIT dejara de poder corregirse: el
        // campo se ignoraba en silencio y el panel lo pintaba como texto con un «escríbeles si hay
        // un error».
        //
        // La dirección de producto retiró ese bloqueo. Una errata en el NIT es justo lo que la
        // organización tiene que poder arreglar sin escribir un correo, y lo que sostiene el
        // circuito de revisión no es un campo congelado sino la bitácora, que registra quién lo
        // cambió y cuándo. Lo que NO se relaja —que el número siga siendo único entre
        // organizaciones y que pase la validación de formato del alta— lo vigilan sus propias
        // pruebas.
        //
        // Se conserva el mismo montaje —una organización con un Festival ya entregado— porque es
        // exactamente el caso que antes quedaba bloqueado.
        var cuenta = await RegistrarAsync("enrevision", "1020304066");

        var primero = CuerpoDePerfil("Organizacion En Revision");
        primero["numeroIdentificacion"] = "900111222-3";
        Assert.Equal(HttpStatusCode.OK, (await GuardarPerfilAsync(cuenta, primero)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.FestivalRecords.Add(new FestivalRow
            {
                Name = "Festival entregado",
                CoverageLevel = "nacional",
                StatusCode = "en_revision",
                OrganizacionPrincipalId = cuenta.OrganizacionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var segundo = CuerpoDePerfil("Organizacion En Revision");
        segundo["numeroIdentificacion"] = "900999888-7";
        var guardado = await GuardarPerfilAsync(cuenta, segundo);

        // El número pedido es el que queda, en la respuesta y en la fila.
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        var perfil = await guardado.Content.ReadFromJsonAsync<PerfilOrganizacionDto>();
        Assert.NotNull(perfil);
        Assert.Equal("900999888-7", perfil!.NumeroIdentificacion);
        Assert.Equal("900999888-7", (await LeerEntidadAsync(cuenta.OrganizacionId)).IdentificationNumber);
    }

    // ==========================================================================================
    // VALIDACION CONTRA LO QUE IMPONE LA BASE
    // ==========================================================================================

    [Fact]
    public async Task Un_Nombre_De_241_Caracteres_Se_Rechaza_Y_Uno_De_240_Entra()
    {
        // dbo.Entidades.Nombre es nvarchar(240) —max_length 480 bytes en sys.columns, medido el 27
        // de agosto de 2026—. ValidationHelpers.SanitizeText corta por lo sano, que para el nombre
        // legal de una organización significa guardarlo mutilado sin decírselo a nadie.
        var cuenta = await RegistrarAsync("largo", "1020304067");

        // El nombre legal se fija corto a propósito: con el valor por omisión —el nombre más
        // « S.A.S.»— los dos casos llevarían además un nombreLegal pasado de largo, y la prueba no
        // distinguiría cuál de los dos topes se disparó.
        var pasado = CuerpoDePerfil(new string('a', 241));
        pasado["nombreLegal"] = "Nombre Legal Corto";
        var rechazado = await GuardarPerfilAsync(cuenta, pasado);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Equal(ErrorDeNombre, await ClavesDeErrorAsync(rechazado));
        Assert.Equal("Organizacion largo", (await LeerEntidadAsync(cuenta.OrganizacionId)).Name);

        // EL LÍMITE ES EL DE LA COLUMNA, NI UNO MENOS. Sin esta mitad, un tope escrito con un «>=»
        // en vez de un «>» pasaría desapercibido y recortaría un carácter a todo el mundo.
        var justo = new string('b', 240);
        var cabe = CuerpoDePerfil(justo);
        cabe["nombreLegal"] = "Nombre Legal Corto";
        var aceptado = await GuardarPerfilAsync(cuenta, cabe);
        Assert.Equal(HttpStatusCode.OK, aceptado.StatusCode);
        Assert.Equal(justo, (await LeerEntidadAsync(cuenta.OrganizacionId)).Name);
    }

    [Fact]
    public async Task Guardar_Sin_Cambiar_El_Nit_No_Se_Rechaza_A_Si_Mismo()
    {
        // AltaDeOrganizacion.ValidarAsync comprueba la unicidad del NIT con
        // AnyAsync(item => item.IdentificationNumber == numero), SIN excluir a la entidad que se
        // está editando: nació para dar de alta, donde no hay ninguna que excluir. Reutilizada tal
        // cual, le contestaría a la organización «ya existe una organización registrada con esta
        // identificación» —la suya— y la dejaría sin poder guardar nunca más su perfil.
        var cuenta = await RegistrarAsync("mismonit", "1020304068");

        var cuerpo = CuerpoDePerfil("Organizacion Con Nit");
        cuerpo["numeroIdentificacion"] = "900777666-5";
        Assert.Equal(HttpStatusCode.OK, (await GuardarPerfilAsync(cuenta, cuerpo)).StatusCode);

        cuerpo["descripcion"] = "Ahora corrige solo la descripción.";
        var segundo = await GuardarPerfilAsync(cuenta, cuerpo);
        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        Assert.Equal("900777666-5", (await LeerEntidadAsync(cuenta.OrganizacionId)).IdentificationNumber);
    }

    [Fact]
    public async Task El_Nit_De_Otra_Organizacion_Se_Sigue_Rechazando()
    {
        // LA OTRA MITAD DE LA REGLA ANTERIOR, y va en dirección contraria: la salida al choque
        // consigo misma no puede ser dejar de comprobar la unicidad. Sin esta prueba, «no preguntes
        // cuando el número no cambia» y «no preguntes nunca» pasan las dos.
        var primera = await RegistrarAsync("nitpropio", "1020304069");
        var segunda = await RegistrarAsync("nitajeno", "1020304070");

        var suyo = CuerpoDePerfil("Organizacion Con Nit Propio");
        suyo["numeroIdentificacion"] = "900444333-2";
        Assert.Equal(HttpStatusCode.OK, (await GuardarPerfilAsync(primera, suyo)).StatusCode);

        var robado = CuerpoDePerfil("Organizacion Con Nit Ajeno");
        robado["numeroIdentificacion"] = "900444333-2";
        var rechazado = await GuardarPerfilAsync(segunda, robado);

        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Contains("identificationNumber", await ClavesDeErrorAsync(rechazado), StringComparer.OrdinalIgnoreCase);
        Assert.Null((await LeerEntidadAsync(segunda.OrganizacionId)).IdentificationNumber);
    }

    [Fact]
    public async Task La_Sede_Obligatoria_Se_Actualiza_Como_Un_Par_Divipola()
    {
        var cuenta = await RegistrarAsync("territorio", "1020304071");

        var cuerpo = CuerpoDePerfil("Organizacion Con Sede");
        cuerpo["codigoDepartamentoSede"] = "05";
        cuerpo["codigoMunicipioSede"] = "05001";
        Assert.Equal(HttpStatusCode.OK, (await GuardarPerfilAsync(cuenta, cuerpo)).StatusCode);

        var entidad = await LeerEntidadAsync(cuenta.OrganizacionId);
        Assert.Equal("05", entidad.HeadquartersDepartmentCode);
        Assert.Equal("05001", entidad.HeadquartersMunicipalityCode);
    }

    [Fact]
    public async Task Un_Enlace_Que_No_Es_Http_No_Entra()
    {
        var cuenta = await RegistrarAsync("enlace", "1020304072");

        var cuerpo = CuerpoDePerfil("Organizacion Con Enlace Raro");
        cuerpo["sitioWeb"] = "javascript:alert(1)";
        var rechazado = await GuardarPerfilAsync(cuenta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Contains("sitioWeb", await ClavesDeErrorAsync(rechazado), StringComparer.OrdinalIgnoreCase);
    }

    // ==========================================================================================
    // LA PERSONA RESPONSABLE
    // ==========================================================================================

    [Fact]
    public async Task El_Responsable_Se_Corrige_Pero_Su_Correo_Y_Su_Autorizacion_No()
    {
        var cuenta = await RegistrarAsync("responsable", "1020304073");

        var leido = await cuenta.Cliente.GetFromJsonAsync<ResponsableOrganizacionDto>(
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable");
        Assert.NotNull(leido);
        Assert.Equal("Persona responsable", leido!.ResponsableNombre);

        // EL ALTA ESCRIBE «CC» EN MAYÚSCULA Y dbo.TiposDocumento GUARDA «cc»: no hay clave foránea
        // entre las dos tablas. El DTO devuelve el código del catálogo para que el desplegable del
        // panel encuentre su opción, y la etiqueta para que se lea en palabras.
        Assert.Equal("cc", leido.ResponsableTipoDocumento);
        Assert.Equal("Cédula de ciudadanía", leido.ResponsableTipoDocumentoEtiqueta);
        Assert.Equal(cuenta.Correo, leido.ResponsableCorreo);
        Assert.True(leido.ResponsableAutorizacionDatos);

        var cuerpo = CuerpoDeResponsable("Persona Corregida");
        cuerpo["responsableTipoDocumento"] = "CE";
        cuerpo["responsableNumeroDocumento"] = "1.020.304.055";
        // Cuatro campos que la solicitud no tiene. Si alguien los añadiera al DTO y al manejador,
        // esta prueba se pone roja: el correo es la credencial de acceso y no se cambia desde aquí,
        // y una autorización de tratamiento de datos revocada sin flujo de borrado dejaría el
        // nombre y la cédula guardados sin permiso (Ley 1581 de 2012).
        cuerpo["responsableCorreo"] = "otro.correo@example.com";
        cuerpo["responsableAutorizacionDatos"] = false;
        cuerpo["responsableDesde"] = "2020-01-01T00:00:00Z";
        cuerpo["idEntidad"] = 999;

        var guardado = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            cuerpo);
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);

        var fila = await LeerResponsableAsync(cuenta.OrganizacionId);
        Assert.NotNull(fila);
        Assert.Equal("Persona Corregida", fila!.ResponsableNombre);

        // El número se guarda sin puntos: quien lo escribe lo lee del documento y quien lo busca lo
        // teclea seguido; guardar las dos formas es no poder cruzarlas nunca.
        Assert.Equal("1020304055", fila.ResponsableNumeroDocumento);
        Assert.Equal("3009998877", fila.ResponsableTelefono);
        Assert.Equal(cuenta.Correo, fila.ResponsableCorreo);
        Assert.True(fila.ResponsableAutorizacionDatos);
        Assert.Equal(cuenta.OrganizacionId, fila.IdEntidad);
        Assert.NotNull(fila.FechaActualizacion);
    }

    [Fact]
    public async Task El_Tipo_De_Documento_Se_Compara_En_Minuscula_Y_Fuera_Del_Catalogo_Se_Rechaza()
    {
        var cuenta = await RegistrarAsync("tipodoc", "1020304074");

        // «CC» en mayúscula es exactamente lo que AltaDeOrganizacion ya escribió en las filas que
        // hay en la base local. Comparar sin bajar a minúscula rechazaría los datos existentes.
        var mayuscula = CuerpoDeResponsable("Persona Con Mayuscula");
        mayuscula["responsableTipoDocumento"] = "CC";
        var aceptado = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            mayuscula);
        Assert.Equal(HttpStatusCode.OK, aceptado.StatusCode);

        var inventado = CuerpoDeResponsable("Persona Con Tipo Inventado");
        inventado["responsableTipoDocumento"] = "carnet_del_club";
        var rechazado = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            inventado);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Contains("responsableTipoDocumento", await ClavesDeErrorAsync(rechazado), StringComparer.OrdinalIgnoreCase);

        var documentoCorto = CuerpoDeResponsable("Persona Con Documento Corto");
        documentoCorto["responsableNumeroDocumento"] = "123";
        var corto = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            documentoCorto);
        Assert.Equal(HttpStatusCode.BadRequest, corto.StatusCode);
        Assert.Contains("responsableNumeroDocumento", await ClavesDeErrorAsync(corto), StringComparer.OrdinalIgnoreCase);

        // Ninguno de los dos rechazos dejó rastro: la fila sigue como la dejó el primero.
        var fila = await LeerResponsableAsync(cuenta.OrganizacionId);
        Assert.NotNull(fila);
        Assert.Equal("Persona Con Mayuscula", fila!.ResponsableNombre);
    }

    [Fact]
    public async Task Sin_Fila_De_Responsable_Se_Contesta_404_Y_No_Una_Ficha_Vacia()
    {
        // dbo.EntidadesResponsable nació y solo la escribe el alta externa:
        // hoy tiene 2 filas frente a 19 entidades. Devolver campos vacíos haría pasar «nadie ha
        // declarado responsable» por «el responsable no tiene nombre».
        var cuenta = await RegistrarAsync("sinresponsable", "1020304075");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = await db.EntidadesResponsable.FirstAsync(item => item.IdEntidad == cuenta.OrganizacionId);
            db.EntidadesResponsable.Remove(fila);
            await db.SaveChangesAsync();
        }

        var lectura = await cuenta.Cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable");
        Assert.Equal(HttpStatusCode.NotFound, lectura.StatusCode);

        // Y TAMPOCO SE CREA AL GUARDAR. Faltan dos datos que no se pueden deducir: el correo, que es
        // la credencial de acceso, y la autorización de tratamiento de datos, que la ley obliga a
        // guardar y prohíbe presumir. Escribir aquí un «true» sería fabricar un consentimiento.
        var escritura = await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            CuerpoDeResponsable("Persona Que No Se Crea"));
        Assert.Equal(HttpStatusCode.NotFound, escritura.StatusCode);
        Assert.Null(await LeerResponsableAsync(cuenta.OrganizacionId));
    }

    // ==========================================================================================
    // AUDITORIA
    // ==========================================================================================

    [Fact]
    public async Task La_Bitacora_Guarda_Un_Verbo_Que_La_Base_Admite_Y_El_Evento_Aparte()
    {
        // CK_BitacoraAuditoria_Accion cierra la columna a trece verbos técnicos, medido en
        // PNMC_LOCAL. Escribir «actualizar_perfil_organizacion» tal cual
        // haría fallar el guardado ENTERO contra SQL Server mientras pasa contra SQLite, que es
        // donde corre casi toda esta suite: es el mismo defecto que tumbaba todo el circuito externo
        // de Festival hasta el 22 de agosto.
        var cuenta = await RegistrarAsync("bitacora", "1020304076");

        Assert.Equal(HttpStatusCode.OK, (await GuardarPerfilAsync(cuenta, CuerpoDePerfil("Organizacion Con Bitacora"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GuardarAsync(
            cuenta.Cliente,
            $"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable",
            CuerpoDeResponsable("Persona Con Bitacora"))).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = cuenta.OrganizacionId.ToString(CultureInfo.InvariantCulture);
        var filas = await db.AuditLogs.AsNoTracking()
            .Where(fila => fila.RecordId == registro)
            .ToListAsync();

        Assert.All(filas, fila => Assert.Contains(fila.Action, AccionesAuditoria.Admitidos));

        var delPerfil = Assert.Single(
            filas,
            fila => fila.TableName == "Entidades"
                && (fila.NewValuesJson ?? string.Empty).Contains("actualizar_perfil_organizacion", StringComparison.Ordinal));
        Assert.Equal(AccionesAuditoria.Actualizar, delPerfil.Action);

        // El nombre del evento no se pierde: viaja en ValoresNuevos, que la base exige que sea JSON
        // válido (CHECK isjson(ValoresNuevos) = 1). Se comprueba parseándolo, no buscando texto.
        using var documento = JsonDocument.Parse(delPerfil.NewValuesJson!);
        Assert.Equal("actualizar_perfil_organizacion", documento.RootElement.GetProperty("Evento").GetString());

        var delResponsable = Assert.Single(filas, fila => fila.TableName == "EntidadesResponsable");
        Assert.Equal(AccionesAuditoria.Actualizar, delResponsable.Action);
        using var segundo = JsonDocument.Parse(delResponsable.NewValuesJson!);
        Assert.Equal("actualizar_responsable_organizacion", segundo.RootElement.GetProperty("Evento").GetString());
    }
}
