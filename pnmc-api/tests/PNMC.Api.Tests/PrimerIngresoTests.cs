using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Una cuenta administrativa se entrega, y quien la recibe termina de configurarla.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL RECORRIDO, DECIDIDO EL 15 DE SEPTIEMBRE DE 2026.</b> «Ellos no tienen una interfaz como tal
/// para registrarse: a ellos se les registra simplemente un correo y una contraseña por defecto. En
/// su primer inicio de sesión les debe pedir cambiar la contraseña; una vez cambien la contraseña se
/// les pide completar esos datos básicos y una vez eso pase ya se le activa el usuario.»
/// </para>
/// <para>
/// <b>SE COMPRUEBA EN EL SERVIDOR Y NO EN LA PANTALLA.</b> Un recorrido que solo existe en la
/// interfaz se salta escribiendo la ruta, que es la misma clase de defecto que los permisos por
/// cuenta vinieron a cerrar.
/// </para>
/// </remarks>
public sealed class PrimerIngresoTests
{
    private const string Entregada = "ClaveDeEntrega123";
    private static readonly string[] SoloGestor = ["gestor_interno"];
    private static readonly string[] SoloAgenda = ["agenda"];

    /// <summary>Crea una cuenta como se entrega de verdad y devuelve su sesión.</summary>
    private static async Task<(int Id, string Correo, HttpClient Sesion)> CuentaEntregadaAsync(
        TestWebApplicationFactory factory)
    {
        var webmaster = await CmsTestClient.LoginAsync(factory);
        var correo = $"entregada.{Guid.NewGuid():N}@pnmc.local";

        var alta = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Cuenta entregada",
            Email = correo,
            Roles = SoloGestor,
            Password = Entregada,
            IsActive = true,
        });
        alta.EnsureSuccessStatusCode();

        var creada = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(creada.GetProperty("user").GetProperty("id").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);

        // Los módulos se deciden ANTES de entregar la cuenta.
        var permisos = await webmaster.PutAsJsonAsync($"/api/v1/admin/usuarios/{id}/modulos", new { modulos = SoloAgenda });
        Assert.True(permisos.IsSuccessStatusCode,
            $"conceder «agenda» respondió {(int)permisos.StatusCode}: {await permisos.Content.ReadAsStringAsync()}");

        return (id, correo, await CmsTestClient.LoginAsync(factory, correo, Entregada));
    }

    [Fact]
    public async Task Una_cuenta_recien_entregada_llega_pendiente_de_los_dos_pasos()
    {
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var yo = await sesion.GetFromJsonAsync<JsonElement>("/api/v1/admin/auth/me");
        var cuenta = yo.GetProperty("user");

        Assert.True(cuenta.GetProperty("debeCambiarContrasena").GetBoolean());
        Assert.False(cuenta.GetProperty("perfilCompletado").GetBoolean());
    }

    [Fact]
    public async Task Sin_terminar_el_recorrido_no_entra_a_su_modulo_aunque_lo_tenga_activado()
    {
        // TIENE «agenda» CONCEDIDO y aun así no entra: el recorrido va antes que el permiso.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var agenda = await sesion.GetAsync("/api/v1/institucional/agenda");
        var cuerpo = await agenda.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, agenda.StatusCode);
        Assert.Contains("cambiar la contraseña", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_perfil_no_se_puede_completar_antes_de_cambiar_la_contrasena()
    {
        // EL ORDEN SE APLICA, no solo se sugiere.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var intento = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio
        {
            PrimerNombre = "Camila", PrimerApellido = "Ruiz",
            TipoDocumento = "cc", Identificacion = "1020304050",
        });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Contains("cambiar la contraseña", await intento.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_contrasena_nueva_no_puede_ser_la_misma_que_se_entrego()
    {
        // SI LO FUERA, el paso quedaría dado sin cambiar nada y la credencial que eligió otra
        // persona seguiría siendo la buena.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var intento = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = Entregada, Nueva = Entregada });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
        Assert.Contains("distinta", await intento.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_sin_saber_la_actual_se_rechaza()
    {
        // UNA SESION ABIERTA EN UN EQUIPO PRESTADO no puede bastar para cambiarle la clave a alguien.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var intento = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = "la-que-no-es", Nueva = "OtraClaveLarga123" });

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
    }

    [Fact]
    public async Task El_recorrido_completo_deja_la_cuenta_trabajando()
    {
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        var cambio = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = Entregada, Nueva = "SuPropiaClave456" });
        cambio.EnsureSuccessStatusCode();

        // TODAVIA NO ENTRA: falta decir quién es.
        Assert.Equal(HttpStatusCode.Forbidden, (await sesion.GetAsync("/api/v1/institucional/agenda")).StatusCode);

        var perfil = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio
        {
            PrimerNombre = "Camila", SegundoNombre = "Andrea",
            PrimerApellido = "Ruiz", SegundoApellido = "Barragán",
            TipoDocumento = "cc", Identificacion = "1020304050", Telefono = "3001112233",
        });
        perfil.EnsureSuccessStatusCode();

        // Y AHORA SI, sobre el módulo que se le activó antes de entregarle la cuenta.
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/api/v1/institucional/agenda")).StatusCode);
        // Y NO SOBRE LOS DEMAS: terminar el recorrido no concede módulos.
        Assert.Equal(HttpStatusCode.Forbidden, (await sesion.GetAsync("/api/v1/institucional/noticias")).StatusCode);
    }

    [Fact]
    public async Task El_nombre_completo_se_compone_de_sus_cuatro_partes()
    {
        // SE COMPONE Y NO SE PIDE APARTE: pedirlo dos veces es dejar que discrepen.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = Entregada, Nueva = "SuPropiaClave456" });

        var perfil = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio
        {
            PrimerNombre = "Camila", SegundoNombre = "Andrea",
            PrimerApellido = "Ruiz", SegundoApellido = "Barragán",
            TipoDocumento = "cc", Identificacion = "1020304050",
        });

        var respuesta = await perfil.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Camila Andrea Ruiz Barragán", respuesta.GetProperty("nombreCompleto").GetString());
    }

    [Fact]
    public async Task Al_perfil_le_faltan_los_que_identifican_y_lo_dice_campo_a_campo()
    {
        // EL SEGUNDO NOMBRE Y EL SEGUNDO APELLIDO NO LOS TIENE TODO EL MUNDO; el primero de cada
        // uno, sí, y sin documento no se puede responder quién hizo qué.
        await using var factory = new TestWebApplicationFactory();
        var (_, _, sesion) = await CuentaEntregadaAsync(factory);

        await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = Entregada, Nueva = "SuPropiaClave456" });

        var vacio = await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio());
        var cuerpo = await vacio.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
        Assert.Contains("primerNombre", cuerpo, StringComparison.Ordinal);
        Assert.Contains("primerApellido", cuerpo, StringComparison.Ordinal);
        Assert.Contains("identificacion", cuerpo, StringComparison.Ordinal);
        Assert.Contains("tipoDocumento", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_una_cuenta_existente_no_la_manda_otra_vez_al_recorrido()
    {
        // REABRIR LA FICHA DE ALGUIEN PARA CORREGIRLE EL CORREO no puede mandarlo de vuelta a la
        // pantalla de bienvenida.
        await using var factory = new TestWebApplicationFactory();
        var (id, correo, sesion) = await CuentaEntregadaAsync(factory);
        var webmaster = await CmsTestClient.LoginAsync(factory);

        await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = Entregada, Nueva = "SuPropiaClave456" });
        await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio
        {
            PrimerNombre = "Camila", PrimerApellido = "Ruiz",
            TipoDocumento = "cc", Identificacion = "1020304050",
        });

        var edicion = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FullName = "Camila Ruiz", Email = correo, Roles = SoloGestor, IsActive = true,
        });
        edicion.EnsureSuccessStatusCode();

        var despues = await edicion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(despues.GetProperty("user").GetProperty("debeCambiarContrasena").GetBoolean());
        Assert.True(despues.GetProperty("user").GetProperty("perfilCompletado").GetBoolean());
    }
}
