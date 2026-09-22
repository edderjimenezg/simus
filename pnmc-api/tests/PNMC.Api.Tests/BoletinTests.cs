using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El boletin de la portada: alta publica, evidencia de la autorizacion y lista de la consola.
/// </summary>
/// <remarks>
/// <para>
/// QUE MIDE ESTA CLASE Y QUE NO. Mide el API. Las restricciones de la base
/// —<c>UQ_BoletinSuscripciones_Correo</c>, <c>CK_BoletinSuscripciones_Autorizacion</c>,
/// <c>CK_BoletinSuscripciones_Correo_Formato</c>, <c>CK_BoletinSuscripciones_Estado</c> y
/// <c>CK_BoletinSuscripciones_FechaBaja</c>— se comprobaron una a una contra SQL Server real el 28
/// de agosto de 2026, insertando la fila que cada una debe rechazar. Aqui no se pueden volver a
/// medir: el arnes de estas pruebas monta SQLite con <c>EnsureCreated</c>, que crea la tabla desde
/// el mapeo de EF y NO trae los CHECK del guion de esquema. Decirlo importa, porque una prueba que
/// pasara aqui no probaria que la base defiende nada.
/// </para>
/// <para>
/// LAS TRES DECISIONES QUE SE FIJAN, y que se perderian en la primera refactorizacion:
/// </para>
/// <list type="number">
/// <item><description>
/// Sin autorizacion no hay alta, y el rechazo es 400 con motivo. Es el requisito del articulo 9 de
/// la Ley 1581 de 2012, y es lo primero que se cae si alguien "simplifica" el DTO.
/// </description></item>
/// <item><description>
/// La respuesta es IDENTICA para un correo nuevo y para uno que ya estaba. Si dejara de serlo,
/// esta ruta anonima seria un comprobador abierto de quien esta en la lista.
/// </description></item>
/// <item><description>
/// La evidencia guardada es el texto que sirve el servidor, y al reactivar una baja se REESCRIBE.
/// Apoyarse en una autorizacion que la persona ya habia revocado no vale.
/// </description></item>
/// </list>
/// </remarks>
public sealed class BoletinTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BoletinTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>Un correo distinto por prueba: la unicidad hace que compartirlo las cruce.</summary>
    private static string Correo() => $"boletin.{Guid.NewGuid():N}"[..24] + "@pnmc.test";

    private async Task<HttpClient> ConsolaAsync()
    {
        var cliente = _factory.CreateClient();
        var entrada = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        entrada.EnsureSuccessStatusCode();
        return cliente;
    }

    private async Task<T> ConLaBaseAsync<T>(Func<PnmcDbContext, Task<T>> trabajo)
    {
        using var alcance = _factory.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await trabajo(contexto);
    }

    // -----------------------------------------------------------------------------------------
    // La politica
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task La_Politica_Se_Lee_Sin_Cuenta_Y_Trae_Version_Y_Texto()
    {
        // SIN CUENTA A PROPOSITO: es lo que la persona lee ANTES de aceptar. Exigir sesion aqui
        // seria pedirle que se registre para poder leer a que se registraria.
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/publico/boletin/politica");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("version").GetString()));
        // SE COMPRUEBA LO QUE LA LEY OBLIGA A QUE DIGA, no una palabra suelta. El art. 12 de la
        // Ley 1581 exige informar quién es el responsable y qué derechos tiene el titular; la
        // comprobación anterior buscaba «autorizacion» sin tilde y pasaba solo porque el texto
        // estaba escrito sin tildes dentro de una constante de C#.
        var texto = cuerpo.GetProperty("texto").GetString()!;
        Assert.Contains("Ministerio de las Culturas", texto, StringComparison.Ordinal);
        Assert.Contains("Ley 1581", texto, StringComparison.Ordinal);
        Assert.Contains("revocar", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lo_Que_Se_Guarda_Como_Evidencia_Es_El_Texto_Que_Sirve_La_Politica()
    {
        // EL NUDO DE TODO EL ASUNTO. La evidencia solo vale si es exactamente el texto que la
        // persona tuvo delante. Si el endpoint de alta guardara una constante distinta de la que
        // sirve la politica —o peor, el texto que mande el cliente—, la fila diria que alguien
        // autorizo algo que nunca vio.
        var cliente = _factory.CreateClient();
        var politica = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/publico/boletin/politica");
        var textoServido = politica.GetProperty("texto").GetString();

        var correo = Correo();
        var alta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo,
            autorizaTratamiento = true,
        });
        Assert.Equal(HttpStatusCode.Accepted, alta.StatusCode);

        // LA EVIDENCIA VIVE EN EL REGISTRO, no en la fila de la suscripcion. Lo que esta prueba
        // comprueba no ha cambiado y es lo que importa: el texto que el servidor SIRVIO y el que
        // GUARDO como prueba son el mismo. Si dejaran de serlo, la evidencia no probaria lo que
        // esa persona leyo.
        var autorizacion = await ConLaBaseAsync(db => db.AutorizacionesDeDatos
            .AsNoTracking()
            .SingleAsync(fila => fila.CorreoTitular == correo && fila.Finalidad == "boletin"));

        Assert.Equal(textoServido, autorizacion.TextoAceptado);
        Assert.Null(autorizacion.FechaRevocacion);
        Assert.False(autorizacion.TextoReconstruido);

        var guardado = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .SingleAsync(fila => fila.CorreoElectronico == correo));
        Assert.Equal("activa", guardado.Estado);
    }

    // -----------------------------------------------------------------------------------------
    // El alta
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_Alta_Valida_Queda_Activa_Y_Sin_Fecha_De_Baja()
    {
        var cliente = _factory.CreateClient();
        var correo = Correo();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo,
            autorizaTratamiento = true,
            origen = "portada",
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var fila = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .SingleAsync(item => item.CorreoElectronico == correo));

        Assert.Equal("activa", fila.Estado);
        Assert.Equal("portada", fila.Origen);
        Assert.Null(fila.FechaBaja);
    }

    [Fact]
    public async Task Sin_Autorizacion_No_Hay_Alta_Y_No_Queda_Fila()
    {
        // LAS DOS MITADES. Que responda 400 no basta: lo que importa es que NO se haya guardado
        // el correo. Un manejador que insertara y despues devolviera 400 pasaria la primera
        // comprobacion y habria recogido el dato igual.
        var cliente = _factory.CreateClient();
        var correo = Correo();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo,
            autorizaTratamiento = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("politica", cuerpo.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);

        var existe = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AnyAsync(item => item.CorreoElectronico == correo));
        Assert.False(existe);
    }

    [Fact]
    public async Task Sin_Decir_Nada_Sobre_La_Autorizacion_Tampoco_Hay_Alta()
    {
        // EL CAMPO AUSENTE NO ES UN «SI». `AutorizaTratamiento` es `bool?` justamente para que
        // omitirlo no llegue al manejador convertido en `false` silencioso ni, peor, en `true`
        // por omision de un DTO no anulable mal escrito.
        var cliente = _factory.CreateClient();
        var correo = Correo();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False(await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AnyAsync(item => item.CorreoElectronico == correo)));
    }

    [Theory]
    [InlineData("sin-arroba.pnmc.test")]
    [InlineData("@pnmc.test")]
    [InlineData("alguien@")]
    [InlineData("alguien@sinpunto")]
    [InlineData("dos@arrobas@pnmc.test")]
    [InlineData("con espacio@pnmc.test")]
    [InlineData("")]
    public async Task Un_Correo_Mal_Formado_Se_Rechaza(string correo)
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo,
            autorizaTratamiento = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_Correo_Se_Guarda_En_Minusculas_Y_Sin_Espacios_Alrededor()
    {
        // LA UNICIDAD SOLO SIGNIFICA ALGO SI SE NORMALIZA. Sin esto, «Ana@pnmc.test» y
        // «ana@pnmc.test» serian dos filas distintas y esa persona recibiria el boletin dos veces.
        var cliente = _factory.CreateClient();
        var correo = Correo();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo = $"  {correo.ToUpperInvariant()}  ",
            autorizaTratamiento = true,
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.True(await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AnyAsync(item => item.CorreoElectronico == correo)));
    }

    [Fact]
    public async Task Un_Correo_Repetido_Responde_Igual_Y_No_Duplica_La_Fila()
    {
        // NO SE PUEDE DISTINGUIR DESDE FUERA si el correo ya estaba. Se comparan cuerpo y codigo
        // de las dos respuestas: si alguien anadiera un «ya estabas suscrito», esta ruta anonima
        // se convertiria en un comprobador de pertenencia a la lista.
        var cliente = _factory.CreateClient();
        var correo = Correo();
        var peticion = new { correo, autorizaTratamiento = true };

        var primera = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", peticion);
        var segunda = await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", peticion);

        Assert.Equal(primera.StatusCode, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, segunda.StatusCode);
        Assert.Equal(
            await primera.Content.ReadAsStringAsync(),
            await segunda.Content.ReadAsStringAsync());

        var cuantas = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .CountAsync(item => item.CorreoElectronico == correo));
        Assert.Equal(1, cuantas);
    }

    [Fact]
    public async Task Un_Origen_Inventado_Cae_Al_Valor_Por_Omision()
    {
        // El vocabulario esta cerrado en el API. Aceptarlo tal cual dejaria que cualquiera
        // escribiera la columna que despues sirve para segmentar los envios.
        var cliente = _factory.CreateClient();
        var correo = Correo();

        await cliente.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new
        {
            correo,
            autorizaTratamiento = true,
            origen = "lo-que-sea",
        });

        var fila = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .SingleAsync(item => item.CorreoElectronico == correo));

        Assert.Equal("portada", fila.Origen);
    }

    // -----------------------------------------------------------------------------------------
    // La baja y la vuelta
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task La_Baja_No_Borra_La_Fila_Y_Le_Pone_Fecha()
    {
        var publico = _factory.CreateClient();
        var correo = Correo();
        await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo, autorizaTratamiento = true });

        var id = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .Where(item => item.CorreoElectronico == correo)
            .Select(item => item.Id)
            .SingleAsync());

        var consola = await ConsolaAsync();
        var baja = await consola.PostAsync($"/api/v1/admin/comunicaciones/boletin/{id}/baja", null);
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);

        // SIGUE EXISTIENDO. Si se borrara, la persona volveria a recibir el boletin en cuanto
        // alguien reimportara una lista vieja.
        var fila = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .SingleAsync(item => item.CorreoElectronico == correo));

        Assert.Equal("baja", fila.Estado);
        Assert.NotNull(fila.FechaBaja);
    }

    [Fact]
    public async Task Quien_Vuelve_Despues_De_Una_Baja_Autoriza_De_Nuevo()
    {
        // LA EVIDENCIA SE REESCRIBE, no se conserva la vieja. La persona esta autorizando AHORA;
        // apoyarse en un consentimiento que ya habia revocado seria usar como prueba justo lo que
        // dejo de ser cierto.
        var publico = _factory.CreateClient();
        var correo = Correo();
        await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo, autorizaTratamiento = true });

        var id = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .Where(item => item.CorreoElectronico == correo)
            .Select(item => item.Id)
            .SingleAsync());

        var consola = await ConsolaAsync();
        await consola.PostAsync($"/api/v1/admin/comunicaciones/boletin/{id}/baja", null);

        var altaVieja = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .Where(item => item.CorreoElectronico == correo)
            .Select(item => item.FechaAlta)
            .SingleAsync());

        await Task.Delay(1100); // La columna guarda segundos: sin esperar, las dos fechas serian iguales.

        var vuelta = await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo, autorizaTratamiento = true });
        Assert.Equal(HttpStatusCode.Accepted, vuelta.StatusCode);

        var fila = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .AsNoTracking()
            .SingleAsync(item => item.CorreoElectronico == correo));

        Assert.Equal("activa", fila.Estado);
        Assert.Null(fila.FechaBaja);
        Assert.True(fila.FechaAlta > altaVieja, "la fecha de alta tiene que ser la de esta autorizacion, no la de la anterior");

        // Y sigue habiendo una sola fila: volver no crea otra.
        Assert.Equal(1, await ConLaBaseAsync(db => db.BoletinSuscripciones
            .CountAsync(item => item.CorreoElectronico == correo)));
    }

    // -----------------------------------------------------------------------------------------
    // La consola
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/admin/comunicaciones/boletin/")]
    [InlineData("/api/v1/admin/comunicaciones/boletin/export.csv")]
    public async Task La_Lista_No_Se_Puede_Leer_Sin_Sesion(string ruta)
    {
        // ES LA MITAD QUE FALTA DE LA RUTA ANONIMA. Que cualquiera pueda APUNTARSE no significa
        // que cualquiera pueda VER quien esta apuntado.
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.True(
            respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"{ruta} respondio {(int)respuesta.StatusCode}, y sin sesion tiene que ser 401 o 403");
    }

    [Theory]
    [InlineData("/api/v1/admin/comunicaciones/boletin/")]
    [InlineData("/api/v1/admin/comunicaciones/boletin/export.csv")]
    public async Task Una_Sesion_Externa_Valida_Tampoco_Ve_La_Lista(string ruta)
    {
        // ESTA ES LA PRUEBA QUE MIDE `InstitutionalPolicy`, Y LA ANTERIOR NO.
        //
        // Lo descubrio un mutante. Al quitar el `RequireAuthorization(InstitutionalPolicy)` del
        // grupo de la consola, la prueba de «sin sesion» siguio en verde: la `FallbackPolicy` de
        // Program.cs ya exige usuario autenticado en toda ruta que no diga lo contrario, asi que
        // el 401 lo daba ella y no la linea que se estaba midiendo.
        //
        // La diferencia entre las dos guardas solo se ve con una sesion VALIDA pero del canal
        // equivocado: quien entro por el canal externo esta autenticado —la fallback lo deja
        // pasar— y aun asi no puede leer los correos de nadie.
        var cliente = _factory.CreateClient();
        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = "externo@pnmc.local",
            Password = "pnmc-externo"
        });
        entrada.EnsureSuccessStatusCode();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.True(
            respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"{ruta} respondio {(int)respuesta.StatusCode} a una sesion externa, y tiene que ser 401 o 403");
    }

    [Fact]
    public async Task La_Baja_Tampoco_Se_Puede_Pedir_Sin_Sesion()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsync("/api/v1/admin/comunicaciones/boletin/1/baja", null);

        Assert.True(respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_Listado_Filtra_Por_Estado_Y_Cuenta_Las_Activas()
    {
        var publico = _factory.CreateClient();
        var activo = Correo();
        var caido = Correo();
        await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo = activo, autorizaTratamiento = true });
        await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo = caido, autorizaTratamiento = true });

        var idCaido = await ConLaBaseAsync(db => db.BoletinSuscripciones
            .Where(item => item.CorreoElectronico == caido)
            .Select(item => item.Id)
            .SingleAsync());

        var consola = await ConsolaAsync();
        await consola.PostAsync($"/api/v1/admin/comunicaciones/boletin/{idCaido}/baja", null);

        var soloActivas = await consola.GetFromJsonAsync<JsonElement>("/api/v1/admin/comunicaciones/boletin/?estado=activa&limit=200");
        var correosActivos = soloActivas.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("correo").GetString())
            .ToList();

        Assert.Contains(activo, correosActivos);
        Assert.DoesNotContain(caido, correosActivos);

        var soloBajas = await consola.GetFromJsonAsync<JsonElement>("/api/v1/admin/comunicaciones/boletin/?estado=baja&limit=200");
        var correosDeBaja = soloBajas.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("correo").GetString())
            .ToList();

        Assert.Contains(caido, correosDeBaja);
        Assert.DoesNotContain(activo, correosDeBaja);
    }

    [Fact]
    public async Task La_Busqueda_Encuentra_Por_Parte_Del_Correo()
    {
        var publico = _factory.CreateClient();
        var marca = Guid.NewGuid().ToString("N")[..10];
        var correo = $"busqueda.{marca}@pnmc.test";
        await publico.PostAsJsonAsync("/api/v1/publico/boletin/suscripciones", new { correo, autorizaTratamiento = true });

        var consola = await ConsolaAsync();
        var encontrado = await consola.GetFromJsonAsync<JsonElement>($"/api/v1/admin/comunicaciones/boletin/?q={marca}");

        Assert.Equal(1, encontrado.GetProperty("total").GetInt32());
        Assert.Equal(correo, encontrado.GetProperty("items")[0].GetProperty("correo").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // La exportacion, que es el puente hacia el mailing
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task El_Csv_Sale_Con_Bom_Y_Con_Cabecera()
    {
        // EL BOM NO ES ADORNO: sin el, Excel en Windows abre el fichero en la pagina de codigos
        // del sistema y las tildes salen rotas. Es el mismo defecto que costo un rato el 26 de
        // agosto al sembrar la base con sqlcmd.
        var consola = await ConsolaAsync();

        var respuesta = await consola.GetAsync("/api/v1/admin/comunicaciones/boletin/export.csv");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/csv", respuesta.Content.Headers.ContentType?.MediaType);

        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length >= 3, "el CSV llego vacio");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var texto = Encoding.UTF8.GetString(bytes);
        Assert.Contains("correo,origen,estado,fecha_alta,fecha_baja", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_Csv_Desactiva_Los_Valores_Que_Excel_Leeria_Como_Formula()
    {
        // INYECCION DE FORMULAS EN CSV. Un valor que empieza por «=», «+», «-» o «@» lo ejecuta
        // Excel al abrir el fichero. Aqui el dato lo escribe cualquiera desde un formulario
        // publico, asi que no es una hipotesis de laboratorio.
        //
        // SE INSERTA DIRECTAMENTE EN LA BASE, y no por el formulario, porque el API rechaza este
        // correo: empieza por arroba y `NormalizarCorreo` lo descarta. Pero el escapado tiene que
        // funcionar igual, porque la lista tambien recibe filas de una importacion o de una
        // version futura del alta, y una defensa que solo cubre el camino de hoy no es defensa.
        var peligroso = "@sum(1+1)@pnmc.test";

        using (var alcance = _factory.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            if (!await contexto.BoletinSuscripciones.AnyAsync(item => item.CorreoElectronico == peligroso))
            {
                contexto.BoletinSuscripciones.Add(new BoletinSuscripcionRow
                {
                    CorreoElectronico = peligroso,
                    Origen = "portada",
                    Estado = "activa",
                    FechaAlta = DateTime.UtcNow,
                });
                await contexto.SaveChangesAsync();
            }
        }

        var consola = await ConsolaAsync();
        var texto = await consola.GetStringAsync("/api/v1/admin/comunicaciones/boletin/export.csv");

        Assert.Contains(peligroso, texto, StringComparison.Ordinal);
        // El dato sigue estando entero —no se ha mutilado— pero su celda empieza por comilla
        // simple, que es lo que hace que Excel lo trate como texto y no lo evalue.
        Assert.Contains($"'{peligroso},", texto, StringComparison.Ordinal);
    }
}
