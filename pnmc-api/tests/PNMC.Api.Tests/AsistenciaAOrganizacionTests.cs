using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Lo que un funcionario puede hacer por una organización que no es suya.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que se dé por enviado un mensaje que nadie va a leer; que quien
/// escribe tenga que conocer los correos de la organización; que una organización se archive sin
/// que conste por qué; y que alguien archive la entidad institucional, que responde por todo
/// registro que nadie haya reclamado.
/// </para>
/// </summary>
public sealed class AsistenciaAOrganizacionTests
{
    private const string Clave = "ClaveExterna123";

    [Fact]
    public async Task Escribirle_a_una_organizacion_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();

        var respuesta = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/admin/organizaciones/1/mensajes", new { asunto = "Hola", mensaje = "Prueba" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Si_nadie_puede_leerlo_no_se_da_por_enviado()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);

        // UNA ORGANIZACION CREADA DESDE LA CONSOLA no tiene todavía ninguna cuenta: su
        // administración llega cuando ella la reclame.
        var creada = await (await consola.PostAsJsonAsync("/api/v1/admin/organizaciones", new
        {
            nombre = "Fundación sin cuentas",
            correoContacto = "sincuentas@fundacion.test",
            codigoDepartamentoSede = "05",
            codigoMunicipioSede = "05001",
            responsableNombre = "Ana Restrepo",
            responsableAutorizacionDatos = true,
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetString();

        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/mensajes", new
        {
            asunto = "Falta tu personería",
            mensaje = "Necesitamos el documento para continuar.",
        });

        // DAR POR ENVIADO UN MENSAJE QUE NADIE VA A RECIBIR es peor que no poder mandarlo. Y la
        // respuesta es en sí misma información de gestión.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("no ha sido reclamada", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_mensaje_llega_al_panel_de_quien_administra_la_organizacion()
    {
        await using var factory = new TestWebApplicationFactory();
        var (organizacion, organizacionId) = await OrganizacionExternaAsync(factory, "recibe.mensaje@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        var enviado = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/mensajes", new
        {
            asunto = "Tu festival lleva tres meses en borrador",
            mensaje = "Si necesitas ayuda para enviarlo a revisión, escríbenos.",
        });
        Assert.Equal(HttpStatusCode.OK, enviado.StatusCode);

        // EL AMBITO ES OBLIGATORIO en el buzón: la misma ruta sirve al panel externo y a la
        // consola, y sin decirlo no se sabe qué bandeja se está pidiendo.
        var suyas = await (await organizacion.GetAsync("/api/v1/notificaciones?ambito=externo"))
            .Content.ReadFromJsonAsync<JsonElement>();
        // EL DTO DEL BUZON ES EL DE SIEMPRE —`title`, no `titulo`—: este circuito no estrena
        // contrato, reutiliza la bandeja que la organización ya tiene.
        var titulos = suyas.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("title").GetString()).ToList();

        // SE REUTILIZA LA BANDEJA QUE YA TIENE: una tabla propia de mensajes daría dos bandejas
        // distintas en el panel de la organización.
        Assert.Contains("Tu festival lleva tres meses en borrador", titulos);
    }

    [Fact]
    public async Task Quien_escribe_no_necesita_conocer_los_correos_de_la_organizacion()
    {
        await using var factory = new TestWebApplicationFactory();
        var (_, organizacionId) = await OrganizacionExternaAsync(factory, "sin.saber.correo@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        // EL CUERPO NO LLEVA DESTINATARIO. Pedírselo abriría la puerta a escribirle a cualquiera
        // desde una pantalla que dice «organización».
        var enviado = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/mensajes", new
        {
            asunto = "Aviso",
            mensaje = "El servidor resuelve a quién le llega.",
        });

        var cuerpo = await enviado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, cuerpo.GetProperty("destinatarios").GetInt32());
    }

    [Fact]
    public async Task La_ficha_guarda_lo_que_se_le_ha_escrito_y_si_lo_leyeron()
    {
        await using var factory = new TestWebApplicationFactory();
        var (_, organizacionId) = await OrganizacionExternaAsync(factory, "historial.mensajes@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/mensajes", new
        {
            asunto = "Primer aviso",
            mensaje = "Falta tu personería.",
        });

        var historial = await (await consola.GetAsync($"/api/v1/admin/organizaciones/{organizacionId}/mensajes"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var mensaje = historial.GetProperty("items")[0];

        Assert.Equal("Primer aviso", mensaje.GetProperty("asunto").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mensaje.GetProperty("remitenteNombre").GetString()));
        // «LE ESCRIBIMOS Y NO LO HA LEIDO» es información de gestión: distingue a quien ignora un
        // aviso de quien nunca lo recibió.
        Assert.Equal(JsonValueKind.Null, mensaje.GetProperty("leidoEn").ValueKind);
    }

    [Fact]
    public async Task Eliminar_una_organizacion_exige_decir_por_que()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var id = await OrganizacionDeConsolaAsync(consola, "Fundación que cierra");

        var sinMotivo = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new { estado = "eliminada" });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var conMotivo = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new
        {
            estado = "eliminada",
            motivo = "La fundación se disolvió en agosto de 2026.",
        });
        Assert.Equal(HttpStatusCode.OK, conMotivo.StatusCode);
    }

    [Fact]
    public async Task El_motivo_queda_en_la_bitacora_y_no_solo_en_pantalla()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var id = await OrganizacionDeConsolaAsync(consola, "Fundación con motivo");

        await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new
        {
            activa = false,
            motivo = "No responde desde marzo.",
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var linea = await db.AuditLogs.AsNoTracking()
            .Where(x => x.TableName == "Entidades" && x.RecordId == id)
            .OrderByDescending(x => x.Id).FirstAsync();

        // DENTRO DE SEIS MESES NADIE RECORDARA EL MOTIVO si solo se enseñó en pantalla.
        Assert.Contains("No responde desde marzo.", linea.NewValuesJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_entidad_institucional_no_se_puede_eliminar()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);

        int institucionalId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            institucionalId = await db.EntityProfiles.AsNoTracking()
                .Where(x => x.IsInstitutional).Select(x => x.Id).FirstAsync();
        }

        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{institucionalId}/estado", new
        {
            estado = "eliminada",
            motivo = "Un descuido.",
        });

        // RESPONDE POR TODO REGISTRO QUE NADIE HAYA RECLAMADO: archivarla dejaría huérfano medio
        // ecosistema, y la base lo descubriría tarde y con un error ilegible.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_estado_inventado_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var id = await OrganizacionDeConsolaAsync(consola, "Fundación con estado raro");

        // SON LOS DE `dbo.EstadosContenido`, a donde apunta la foránea: uno que no esté la
        // rechazaría con un error que no explica nada.
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new { estado = "lo-que-sea" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reactivar_no_exige_motivo()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var id = await OrganizacionDeConsolaAsync(consola, "Fundación que vuelve");

        await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new
        {
            activa = false,
            motivo = "Se dio de baja por error.",
        });

        // EL MOTIVO PROTEGE EL CIERRE, NO LA APERTURA: pedirlo para reactivar solo añade fricción a
        // deshacer un error.
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{id}/estado", new { activa = true });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ---------- Andamio ----------------------------------------------------------

    private static async Task<string> OrganizacionDeConsolaAsync(HttpClient consola, string nombre)
    {
        var creada = await (await consola.PostAsJsonAsync("/api/v1/admin/organizaciones", new
        {
            nombre,
            correoContacto = $"{Guid.NewGuid():N}@fundacion.test",
            codigoDepartamentoSede = "05",
            codigoMunicipioSede = "05001",
            responsableNombre = "Ana Restrepo",
            responsableAutorizacionDatos = true,
        })).Content.ReadFromJsonAsync<JsonElement>();
        return creada.GetProperty("id").GetString()!;
    }

    private static async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionExternaAsync(
        TestWebApplicationFactory factory, string correo)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
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

        (await client.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        var id = int.Parse(mias[0].GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return (client, id);
    }
}
