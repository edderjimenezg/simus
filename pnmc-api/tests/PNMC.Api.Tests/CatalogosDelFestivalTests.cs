using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// <c>GET /api/v1/externo/catalogos/festival</c> sirve los trece catalogos que el formulario de
/// Festival necesita para poder pintarse.
/// </summary>
/// <remarks>
/// <para>
/// DE DONDE VIENE ESTO, con su fecha. <c>schema/V20260824_02__festivales_simus.sql</c> trajo el 24
/// de agosto de 2026 el modelo de Festivales de SIMUS: once tablas de catalogo, creadas VACIAS a
/// proposito. Comprobado el 28 de agosto: las once seguian con CERO filas, ninguna estaba mapeada en EF
/// Core, y esta ruta devolvia solo dos listas —practicas musicales y territorios sonoros—. Es decir:
/// el formulario no tenia de donde sacar once de sus trece desplegables.
/// </para>
/// <para>
/// LO QUE ESTA CLASE FIJA, Y LO QUE NO. Fija el contrato de la ruta: que las trece claves viajan,
/// que cada catalogo llega con sus filas, y que el orden es el de <c>OrdenVisualizacion</c> y no el
/// alfabetico. NO fija el CONTENIDO de los catalogos: eso lo siembra
/// <c>seed/V20260828_01__catalogos_festival_seed.sql</c> y se comprobo contra SQL Server contando
/// las 54 filas. Una prueba que ademas exigiera «hay diez tipologias» se pondria en rojo el dia que
/// el Ministerio anada la once, que no es un defecto.
/// </para>
/// <para>
/// POR ESO CADA PRUEBA SIEMBRA LO SUYO. La base de pruebas se construye solo con los guiones de
/// <c>schema/</c>, sin las semillas, asi que los catalogos llegan vacios: sembrar aqui las filas que
/// la prueba necesita es lo que hace que mida la RUTA y no la semilla.
/// </para>
/// </remarks>
public sealed class CatalogosDelFestivalTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Ruta = "/api/v1/externo/catalogos/festival";
    private const string Clave = "CatalogosPrueba2026";

    private readonly TestWebApplicationFactory _factory;

    public CatalogosDelFestivalTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>Las trece claves que el formulario espera encontrar en la respuesta.</summary>
    private static readonly string[] LasTreceListas =
    [
        "practicasMusicales", "territoriosSonoros", "tipologias", "expresionesArtisticas",
        "fuentesFinanciacion", "modalidadesParticipacion", "naturalezasEntidad", "tiposIngreso",
        "tiposOrganizador", "zonasUrbanoRural", "titulacionesColectivas", "regionesOcad",
    ];

    private static readonly string[] OrdenEsperado = ["Zarzuela", "Otra", "Ninguna"];

    private async Task<HttpClient> EntrarComoExternaAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var ahora = DateTime.UtcNow;
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var cuenta = new UserRow
            {
                FullName = "Persona Externa " + sufijo,
                Email = $"catalogos.{sufijo}@example.com",
                AccessChannel = "externo",
                ProfileType = "organizacion",
                IsActive = true,
                CreatedAt = ahora,
                UpdatedAt = ahora,
            };
            cuenta.PasswordHash = AdminAuthEndpoints.HashPassword(cuenta, Clave);
            db.Users.Add(cuenta);
            await db.SaveChangesAsync();
            db.UsuariosRoles.Add(new UsuarioRolRow { UserId = cuenta.Id, RoleId = 6, CreatedAt = ahora });
            await db.SaveChangesAsync();

            var cliente = _factory.CreateClient();
            var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
            {
                Email = cuenta.Email,
                Password = Clave
            });
            entrada.EnsureSuccessStatusCode();
            return cliente;
        }
    }

    private static async Task<JsonElement> LeerCatalogosAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync(Ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Sin_Sesion_Externa_No_Se_Sirven_Los_Catalogos()
    {
        // NO ES UNA RUTA PUBLICA, y conviene que quede fijado: el grupo `/externo` lleva
        // `ExternalPolicy` (`FestivalesExternosEndpoints.cs`). Los catalogos no son secretos,
        // pero abrir una ruta del canal externo sin sesion abre tambien la superficie que la
        // acompana, y esta clase es la unica que mira esta ruta.
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_Sesion_Viajan_Las_Trece_Listas()
    {
        // EL DEFECTO QUE ESTO CIERRA: hasta viajaban DOS. Once desplegables
        // del formulario de Festival no tenian de donde salir.
        var cliente = await EntrarComoExternaAsync();

        var catalogos = await LeerCatalogosAsync(cliente);

        var ausentes = LasTreceListas.Where(clave => !catalogos.TryGetProperty(clave, out _)).ToList();
        Assert.True(ausentes.Count == 0, "Faltan en la respuesta: " + string.Join(", ", ausentes));

        // Y cada una es una lista, no un objeto ni un nulo: el front hace `.map()` sobre las trece.
        foreach (var clave in LasTreceListas)
        {
            Assert.Equal(JsonValueKind.Array, catalogos.GetProperty(clave).ValueKind);
        }
    }

    [Fact]
    public async Task El_Orden_Lo_Manda_OrdenVisualizacion_Y_No_El_Alfabeto()
    {
        // ESTE ES EL MOTIVO DE QUE SE MAPEARA `OrdenVisualizacion`. «Otra» y «Ninguna» son las
        // opciones de escape de cada catalogo y su sitio es el final de la lista; ordenando por
        // nombre, «Otra» cae en mitad y quien busca su caso concreto se lo salta.
        //
        // LAS TRES FILAS ESTAN ELEGIDAS PARA QUE LOS DOS ORDENES DISCREPEN: por alfabeto seria
        // Ninguna, Otra, Zarzuela; por `Orden` es Zarzuela, Otra, Ninguna. Con tres nombres
        // cualesquiera la prueba pasaria con las dos implementaciones y no mediria nada.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var sufijo = Guid.NewGuid().ToString("N")[..6];
            db.TipologiasFestival.AddRange(
                new TipologiaFestivalRow { Nombre = "Zarzuela " + sufijo, Orden = 1 },
                new TipologiaFestivalRow { Nombre = "Otra " + sufijo, Orden = 2 },
                new TipologiaFestivalRow { Nombre = "Ninguna " + sufijo, Orden = 3 });
            await db.SaveChangesAsync();
        }

        var cliente = await EntrarComoExternaAsync();
        var catalogos = await LeerCatalogosAsync(cliente);

        var nombres = catalogos.GetProperty("tipologias").EnumerateArray()
            .Select(item => item.GetProperty("nombre").GetString() ?? string.Empty)
            .Where(nombre => nombre.StartsWith("Zarzuela", StringComparison.Ordinal)
                          || nombre.StartsWith("Otra", StringComparison.Ordinal)
                          || nombre.StartsWith("Ninguna", StringComparison.Ordinal))
            .Select(nombre => nombre.Split(' ')[0])
            .ToList();

        Assert.Equal(OrdenEsperado, nombres);
    }

    [Fact]
    public async Task Cada_Catalogo_Trae_Identificador_Y_Rotulo()
    {
        // SIN EL IDENTIFICADOR NO SE PUEDE GUARDAR y sin el rotulo no se puede elegir. Es el
        // contrato entero de un desplegable, y `CatalogoFestivalDto` lo cumple con dos campos.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var sufijo = Guid.NewGuid().ToString("N")[..6];
            db.RegionesOcad.Add(new RegionOcadRow { Nombre = "Region " + sufijo, Orden = 1 });
            await db.SaveChangesAsync();
        }

        var cliente = await EntrarComoExternaAsync();
        var catalogos = await LeerCatalogosAsync(cliente);

        var region = catalogos.GetProperty("regionesOcad").EnumerateArray().First();
        Assert.True(region.TryGetProperty("id", out var id), "falta `id` en regionesOcad");
        Assert.True(region.TryGetProperty("nombre", out var nombre), "falta `nombre` en regionesOcad");
        Assert.True(id.GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(nombre.GetString()));
    }

    [Fact]
    public async Task Las_Dos_Listas_Que_Ya_Viajaban_Siguen_Viajando()
    {
        // LO QUE MAS FACIL SE ROMPE AL AMPLIAR UNA RESPUESTA es lo que ya estaba. `practicasMusicales`
        // y `territoriosSonoros` las consume el modal de crear Festival desde el 28 de agosto
        // (`crear-festival-modal.component.ts`), y una de las dos desapareciendo dejaria el modal
        // con un desplegable vacio y sin error visible.
        var cliente = await EntrarComoExternaAsync();

        var catalogos = await LeerCatalogosAsync(cliente);

        Assert.Equal(JsonValueKind.Array, catalogos.GetProperty("practicasMusicales").ValueKind);
        Assert.Equal(JsonValueKind.Array, catalogos.GetProperty("territoriosSonoros").ValueKind);
    }
}
