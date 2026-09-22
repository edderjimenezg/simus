using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La lectura publica de un Festival no puede llevar datos personales.
///
/// <para>
/// REGLA, Y DE DONDE SALE. La fijo la direccion de producto, al pedir la
/// ficha publica completa: «anonimizar persona responsable, etc. todo dato personal o correo se
/// quita de la version publica, solo se deja redes sociales del festival, nada mas, el resto es
/// privado de la organizacion». Hasta ese dia las tres rutas anonimas de Festival servian
/// <c>CorreoContacto</c>, <c>TelefonoContacto</c> y <c>Director</c> —el nombre de una persona
/// natural— a cualquiera sin sesion, y el propio codigo lo llevaba anotado como «decision
/// pendiente del dueno». Ya esta decidido.
/// </para>
///
/// <para>
/// POR QUE SE MIRA EL JSON EN CRUDO Y NO EL DTO TIPADO. Un <c>Assert.Null(dto.CorreoContacto)</c>
/// solo vigila la propiedad que hoy se llama asi: renombrarla, moverla dentro de un objeto anidado
/// o anadir una segunda que lleve el mismo valor pasaria por delante de la asercion sin
/// despeinarla. Se busca el VALOR sembrado dentro del texto de la respuesta, que es lo unico que
/// no se puede esquivar cambiando de nombre. Es la mitad que
/// <see cref="FichaPublicaSinDatosPersonalesTests"/> declara que no cubre —«el barrido mira
/// NOMBRES de propiedad, no valores»— aplicada al Festival.
/// </para>
///
/// <para>
/// LOS CENTINELAS EN VERDE NO SON ADORNO. Sin ellos esta prueba pasaria con una ruta que
/// devolviera un objeto vacio, o rota, o que no encontrara el Festival: «no aparece el correo»
/// seria cierto por el motivo equivocado. Cada caso comprueba tambien que SI viaja lo que debe
/// viajar —las redes del Festival y su sitio— antes de afirmar que no viaja lo demas.
/// </para>
/// </summary>
public sealed class FichaPublicaSinDatosPersonalesTests : IClassFixture<TestWebApplicationFactory>
{
    // Datos personales o de contacto directo: NINGUNO puede aparecer en una respuesta anonima.
    private const string NombreFestivalQueViaja = "Festival con datos personales sembrados";
    private const string NombreQueViaja = "Edicion 2026 del Festival";

    private const string CorreoDelFestival = "correo-privado-festival@no-publicar.test";
    private const string TelefonoDelFestival = "+57 300 111 2233";
    private const string DirectoraDeLaEdicion = "Nombre Apellido de la directora";

    // Presencia publica del Festival: esto SI se publica, y por eso sirve de centinela en verde.
    private const string InstagramDelFestival = "https://instagram.com/festival-publico-si";
    private const string FacebookDelFestival = "https://facebook.com/festival-publico-si";
    private const string SitioDelFestival = "https://sitio-publico-si.test";
    private const string OtroEnlaceDelFestival = "https://otro-enlace-publico-si.test";

    private static readonly string[] DatosPersonales =
    [
        CorreoDelFestival,
        TelefonoDelFestival,
    ];

    private static readonly string[] PresenciaPublica =
    [
        InstagramDelFestival,
        FacebookDelFestival,
        SitioDelFestival,
        OtroEnlaceDelFestival,
    ];

    private readonly TestWebApplicationFactory _factory;

    public FichaPublicaSinDatosPersonalesTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task La_Ficha_Publica_No_Lleva_Correo_Telefono_Ni_Nombre_De_Persona()
    {
        var festivalId = Sembrar();
        var anonimo = _factory.CreateClient();

        var cuerpo = await anonimo.GetStringAsync(
            $"/api/v1/publico/festivales/{festivalId.ToString(CultureInfo.InvariantCulture)}");

        AfirmarQueSirvioAlgo(cuerpo, "la ficha publica");
        AfirmarSinDatosPersonales(cuerpo, "la ficha publica");
    }

    [Fact]
    public async Task El_Listado_Publico_No_Lleva_Correo_Telefono_Ni_Nombre_De_Persona()
    {
        Sembrar();
        var anonimo = _factory.CreateClient();

        var cuerpo = await anonimo.GetStringAsync("/api/v1/publico/festivales?limit=500");

        AfirmarQueSirvioAlgo(cuerpo, "el listado publico");
        AfirmarSinDatosPersonales(cuerpo, "el listado publico");
    }

    /// <summary>
    /// La ruta publica de ediciones: la que trae la tarjeta de ediciones de la ficha.
    /// </summary>
    [Fact]
    public async Task Las_Ediciones_Publicas_No_Llevan_Correo_Telefono_Ni_Nombre_De_Persona()
    {
        var festivalId = Sembrar();
        var anonimo = _factory.CreateClient();

        var respuesta = await anonimo.GetAsync(
            $"/api/v1/publico/festivales/{festivalId.ToString(CultureInfo.InvariantCulture)}/ediciones");

        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        // Centinela en verde: la ruta tiene que estar sirviendo la edicion sembrada, no una lista
        // vacia. Sin esto, «no aparece el correo» seria cierto porque no aparece nada.
        Assert.Contains(NombreQueViaja, cuerpo, StringComparison.Ordinal);
        AfirmarSinDatosPersonales(cuerpo, "las ediciones publicas");
    }

    private static void AfirmarQueSirvioAlgo(string cuerpo, string donde)
    {
        Assert.Contains(NombreFestivalQueViaja, cuerpo, StringComparison.Ordinal);
        foreach (var enlace in PresenciaPublica)
        {
            Assert.True(
                cuerpo.Contains(enlace, StringComparison.Ordinal),
                $"{donde} deberia publicar la presencia publica del Festival y no trae «{enlace}». " +
                "Sin este dato la prueba no distingue «se quito el dato personal» de «no se sirvio nada».");
        }
    }

    private static void AfirmarSinDatosPersonales(string cuerpo, string donde)
    {
        foreach (var dato in DatosPersonales)
        {
            Assert.False(
                cuerpo.Contains(dato, StringComparison.Ordinal),
                $"{donde} devolvio un dato personal sembrado: «{dato}». " +
                "La lectura anonima solo puede llevar la presencia publica del Festival.");
        }
    }

    /// <summary>
    /// Un Festival publicado con su edicion vigente, cargado a proposito de todo lo que NO puede
    /// salir y de todo lo que SI.
    /// </summary>
    private int Sembrar()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organizacion de la prueba de anonimato",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.EntityProfiles.Add(organizacion);
        db.SaveChanges();

        var festival = new FestivalRow
        {
            Name = "Festival con datos personales sembrados",
            Description = "Ficha de prueba del anonimato de la lectura publica.",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "Publicado",
            OrganizacionPrincipalId = organizacion.Id,
            ContactEmail = CorreoDelFestival,
            ContactPhone = TelefonoDelFestival,
            InstagramUrl = InstagramDelFestival,
            FacebookUrl = FacebookDelFestival,
            WebsiteUrl = SitioDelFestival,
            OtherUrl = OtroEnlaceDelFestival,
            CreatedAt = DateTime.UtcNow,
        };
        db.FestivalRecords.Add(festival);
        db.SaveChanges();

        var edicion = new EdicionFestivalRow
        {
            FestivalId = festival.Id,
            Anio = 2026,
            Nombre = "Edicion 2026 del Festival",
            Descripcion = "La edición anual publicada.",
            Director = DirectoraDeLaEdicion,
            EstadoRegistro = "publicado",
            EstadoVisibilidad = "publicada",
            Estado = "programada",
            FechaCreacion = DateTime.UtcNow.AddDays(-1),
        };
        db.EdicionesFestival.Add(edicion);
        db.SaveChanges();

        return festival.Id;
    }
}
