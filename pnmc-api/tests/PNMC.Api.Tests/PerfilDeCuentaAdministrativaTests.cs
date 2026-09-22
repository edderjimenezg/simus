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
/// Una cuenta administrativa guarda quién es, y no solo con qué correo entra.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL HUECO QUE ESTO CIERRA.</b> `Usuarios.Identificacion` y `Usuarios.CodigoTipoDocumento`
/// existían en la base y la entidad no las mapeaba, así que la consola no tenía dónde pedirlas: una
/// cuenta administrativa era un nombre y un correo. Quedó definido el 15 de
/// septiembre de 2026: «es importante plantear una ruta de modificación de perfil de estos usuarios
/// administrativos donde podamos pedirles la información básica… porque eso hoy no está planteado».
/// </para>
/// <para>
/// <b>EL TIPO DE DOCUMENTO ES VOCABULARIO CONTROLADO</b> y sale de `dbo.TiposDocumento`, la tabla
/// del catálogo del país. No se acepta texto libre: guardar «cedula» o «CC.» dejaría la ficha
/// enseñando un código que ninguna lista reconoce.
/// </para>
/// </remarks>
public sealed class PerfilDeCuentaAdministrativaTests
{
    private static readonly string[] SoloGestor = ["gestor_interno"];

    private static async Task<(HttpClient Webmaster, string Correo)> PreparadoAsync(TestWebApplicationFactory factory)
    {
        var webmaster = await CmsTestClient.LoginAsync(factory);
        return (webmaster, $"perfil.{Guid.NewGuid():N}@pnmc.local");
    }

    [Fact]
    public async Task El_catalogo_de_tipos_de_documento_sale_de_la_tabla_y_se_lee_con_tildes()
    {
        // LAS TILDES SON EL DATO. La tabla las guardaba sin ellas y por eso el alta externa llevaba
        // una copia escrita a mano: «Cedula» en un desplegable es una falta de ortografía en
        // pantalla. Se corrigieron en la base para poder retirar la copia.
        await using var factory = new TestWebApplicationFactory();
        var webmaster = await CmsTestClient.LoginAsync(factory);

        var tipos = await webmaster.GetFromJsonAsync<JsonElement>(
            "/api/v1/admin/usuarios/catalogos/tipos-documento");

        var etiquetas = tipos.EnumerateArray()
            .Select(t => t.GetProperty("etiqueta").GetString())
            .ToList();

        Assert.Contains("Cédula de ciudadanía", etiquetas);
        Assert.Contains("Número único de identificación personal", etiquetas);
        // DIEZ Y NO OCHO: la copia del alta externa tenía dos que la tabla no —«Documento de
        // identificación extranjero» y «Carné diplomático»—, así que el alta aceptaba códigos que el
        // catálogo no reconocía. Se añadieron a la tabla, que es la fuente.
        Assert.Equal(10, etiquetas.Count);
    }

    [Fact]
    public async Task Una_cuenta_guarda_su_identificacion_y_su_telefono()
    {
        await using var factory = new TestWebApplicationFactory();
        var (webmaster, correo) = await PreparadoAsync(factory);

        var alta = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila Gestora",
            Email = correo,
            Roles = SoloGestor,
            Password = "ClaveDePrueba123",
            IsActive = true,
            Identificacion = "1020304050",
            TipoDocumento = "cc",
            Telefono = "3001112233",
        });
        alta.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Users.SingleAsync(x => x.Email == correo);

        Assert.Equal("1020304050", fila.Identificacion);
        Assert.Equal("cc", fila.CodigoTipoDocumento);
        Assert.Equal("3001112233", fila.Telefono);
    }

    [Fact]
    public async Task El_listado_devuelve_el_tipo_de_documento_en_palabras_y_no_su_codigo()
    {
        // EL CODIGO NUNCA SALE A PANTALLA. «cc» no es un tipo de documento para quien lee la ficha.
        await using var factory = new TestWebApplicationFactory();
        var (webmaster, correo) = await PreparadoAsync(factory);

        await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila Gestora", Email = correo, Roles = SoloGestor,
            Password = "ClaveDePrueba123", IsActive = true,
            Identificacion = "1020304050", TipoDocumento = "cc",
        });

        var cuentas = await webmaster.GetFromJsonAsync<List<AdminUserDto>>("/api/v1/admin/auth/users");
        var suya = cuentas!.Single(c => c.Email == correo);

        Assert.Equal("cc", suya.TipoDocumento);
        Assert.Equal("Cédula de ciudadanía", suya.TipoDocumentoEtiqueta);
        Assert.Equal("1020304050", suya.Identificacion);
    }

    [Fact]
    public async Task Un_tipo_de_documento_que_no_esta_en_el_catalogo_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var (webmaster, correo) = await PreparadoAsync(factory);

        var alta = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila Gestora", Email = correo, Roles = SoloGestor,
            Password = "ClaveDePrueba123", IsActive = true,
            Identificacion = "1020304050", TipoDocumento = "carnet-del-club",
        });

        Assert.Equal(HttpStatusCode.BadRequest, alta.StatusCode);
        Assert.Contains("no pertenece al catálogo", await alta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_numero_sin_su_tipo_se_rechaza_y_un_tipo_sin_numero_tambien()
    {
        // UN NUMERO SIN TIPO NO DICE QUE ES, y un tipo sin número no identifica a nadie.
        await using var factory = new TestWebApplicationFactory();
        var (webmaster, correo) = await PreparadoAsync(factory);

        var soloNumero = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila", Email = correo, Roles = SoloGestor,
            Password = "ClaveDePrueba123", IsActive = true, Identificacion = "1020304050",
        });
        var soloTipo = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila", Email = $"otra.{Guid.NewGuid():N}@pnmc.local", Roles = SoloGestor,
            Password = "ClaveDePrueba123", IsActive = true, TipoDocumento = "cc",
        });

        Assert.Equal(HttpStatusCode.BadRequest, soloNumero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, soloTipo.StatusCode);
    }

    [Fact]
    public async Task Una_cuenta_sin_identificacion_se_sigue_pudiendo_guardar()
    {
        // NO SE EXIGE, PARA NO BLOQUEAR LAS CUENTAS QUE YA EXISTEN. Ninguna la tiene, y exigirla
        // convertiría cambiar un rol en un formulario que no se puede guardar sin ir a buscar una
        // cédula.
        await using var factory = new TestWebApplicationFactory();
        var (webmaster, correo) = await PreparadoAsync(factory);

        var alta = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Camila Sin Documento", Email = correo, Roles = SoloGestor,
            Password = "ClaveDePrueba123", IsActive = true,
        });

        Assert.Equal(HttpStatusCode.OK, alta.StatusCode);
    }

    [Fact]
    public async Task La_constante_del_alta_externa_coincide_con_la_tabla()
    {
        // MIENTRAS LA COPIA SIGA AHI, NO PUEDE DIVERGIR. `ExternalOrganizationEndpoints` lleva los
        // ocho tipos escritos a mano; su motivo original —la tabla sin mapear— ya no existe, y
        // retirarla exige enhebrar el contexto por dos firmas. Hasta entonces, esta prueba es lo
        // que impide que las dos listas dejen de decir lo mismo.
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var enLaTabla = await db.TiposDocumento.AsNoTracking()
            .Where(t => t.Activo)
            .Select(t => t.Codigo)
            .ToListAsync();

        // SE LEE POR REFLEXION PORQUE LA CONSTANTE ES `internal`, y abrirla al proyecto de pruebas
        // solo para vigilarla sería ensanchar su alcance por una razón que va a desaparecer.
        var campo = typeof(PNMC.Api.Endpoints.ExternalOrganizationEndpoints)
            .GetField("TiposDeDocumento", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(campo);
        var copia = (IReadOnlyDictionary<string, string>)campo!.GetValue(null)!;
        var enLaCopia = copia.Keys.ToList();

        Assert.Equal(
            enLaTabla.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            enLaCopia.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }
}
