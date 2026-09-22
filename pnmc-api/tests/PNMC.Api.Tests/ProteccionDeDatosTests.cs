using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PNMC.Api.Security;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Las claves que firman las tres cookies de sesión sobreviven a un reinicio.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO, Y POR QUÉ NO SE VE EN LOCAL. Sin configurar nada, ASP.NET guarda el anillo de
/// claves en el perfil del usuario. En un portátil eso sobrevive a los reinicios y nadie lo nota.
/// En App Service ese directorio se pierde en cada despliegue: las claves cambian, las cookies
/// emitidas antes dejan de validarse y <b>cada publicación expulsa a todo el que estuviera
/// dentro</b>. Sin error y sin traza: la gente vuelve a ver la pantalla de entrada y cree que se
/// le caducó la sesión.
/// </para>
/// <para>
/// CÓMO SE MIDE. Dos aplicaciones distintas, una detrás de otra, apuntando al mismo directorio de
/// claves: la primera protege un texto, la segunda —que no comparte nada con ella salvo ese
/// directorio— tiene que poder recuperarlo. Es exactamente lo que ocurre entre dos despliegues, y
/// no se puede simular con una sola instancia.
/// </para>
/// </remarks>
public sealed class ProteccionDeDatosTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _directorioDeClaves;

    public ProteccionDeDatosTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _directorioDeClaves = Path.Combine(Path.GetTempPath(), $"pnmc-claves-{Guid.NewGuid():N}");
    }

    private WebApplicationFactory<Program> ConClaves(string directorio) =>
        _factory.WithWebHostBuilder(builder => builder.UseSetting(ProteccionDeDatos.ClaveDeRuta, directorio));

    [Fact]
    public void Lo_Protegido_Por_Una_Instancia_Lo_Lee_La_Siguiente()
    {
        const string secreto = "sesion-de-una-persona";
        string protegido;

        using (var primera = ConClaves(_directorioDeClaves))
        {
            protegido = primera.Services
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("prueba")
                .Protect(secreto);
        }

        // LO ENCONTRÓ UN MUTANTE. Sin esta comprobación, esta prueba pasaba IGUAL con la
        // persistencia desconectada: dentro de un mismo proceso, las dos instancias caen en el
        // anillo por omisión —el del perfil del usuario— y se leen entre ellas de todos modos. El
        // viaje de ida y vuelta no distingue «guardó las claves donde le dije» de «las guardó
        // donde siempre». Esto sí: comprueba que el anillo aterrizó en el directorio pedido.
        Assert.NotEmpty(Directory.GetFiles(_directorioDeClaves, "key-*.xml"));

        // Segunda aplicación, construida de cero. Lo único que comparte con la anterior es el
        // directorio: es el equivalente exacto de publicar una versión nueva.
        using var segunda = ConClaves(_directorioDeClaves);
        var recuperado = segunda.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("prueba")
            .Unprotect(protegido);

        Assert.Equal(secreto, recuperado);
    }

    [Fact]
    public void Con_Directorios_Distintos_No_Se_Leen_Entre_Si()
    {
        // El canario de la prueba de arriba. Si esto NO fallara, aquella pasaría con el anillo en
        // cualquier sitio y no estaría midiendo la persistencia, sino nada.
        const string secreto = "sesion-de-una-persona";
        var otroDirectorio = Path.Combine(Path.GetTempPath(), $"pnmc-claves-{Guid.NewGuid():N}");

        try
        {
            string protegido;
            using (var primera = ConClaves(_directorioDeClaves))
            {
                protegido = primera.Services
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("prueba")
                    .Protect(secreto);
            }

            using var segunda = ConClaves(otroDirectorio);
            var protector = segunda.Services
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("prueba");

            Assert.ThrowsAny<Exception>(() => protector.Unprotect(protegido));
        }
        finally
        {
            BorrarSiExiste(otroDirectorio);
        }
    }

    [Fact]
    public void El_Anillo_Se_Aisla_Con_Un_Nombre_Fijo()
    {
        // OTRO MUTANTE SUPERVIVIENTE, Y ESTE NO SE PODÍA MATAR POR COMPORTAMIENTO. Sin
        // `SetApplicationName`, el discriminador se deriva de la RUTA DEL CONTENIDO: dos
        // despliegues en rutas distintas no se leen las claves aunque compartan el directorio, y
        // volvería el defecto por la puerta de atrás. Dentro de una misma suite las dos instancias
        // comparten ruta, así que ningún viaje de ida y vuelta puede notar la diferencia: lo único
        // que queda es fijar el valor configurado, que es exactamente lo que se quiere fijar.
        using var aplicacion = ConClaves(_directorioDeClaves);

        var opciones = aplicacion.Services.GetRequiredService<IOptions<DataProtectionOptions>>();

        Assert.Equal(ProteccionDeDatos.NombreDeAplicacion, opciones.Value.ApplicationDiscriminator);
    }

    [Fact]
    public void En_Local_Sin_Ruta_Declarada_No_Se_Toca_Nada()
    {
        // En un portátil el comportamiento de serie es el correcto, y cambiarlo obligaría a
        // limpiar un directorio más a mano.
        var configuracion = new ConfigurationBuilder().Build();

        var elegido = ProteccionDeDatos.ResolverDirectorio(configuracion, esEntornoLocal: true, inicioDelPerfil: "/home");

        Assert.Null(elegido);
    }

    [Fact]
    public void Fuera_De_Local_El_Anillo_Cuelga_Del_Perfil_Del_Servicio()
    {
        // En App Service `HOME` apunta a un recurso compartido que sobrevive al despliegue y que
        // ven todas las instancias. Sin esta rama haría falta declarar la ruta a mano en cada
        // ambiente, y el que se olvidara arrancaría con el defecto puesto.
        var configuracion = new ConfigurationBuilder().Build();

        var elegido = ProteccionDeDatos.ResolverDirectorio(configuracion, esEntornoLocal: false, inicioDelPerfil: "/home");

        Assert.Equal(Path.Combine("/home", "pnmc-claves"), elegido);
    }

    [Fact]
    public void La_Ruta_Declarada_Manda_Sobre_El_Perfil()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ProteccionDeDatos.ClaveDeRuta] = "/ruta/elegida" })
            .Build();

        var elegido = ProteccionDeDatos.ResolverDirectorio(configuracion, esEntornoLocal: false, inicioDelPerfil: "/home");

        Assert.Equal("/ruta/elegida", elegido);
    }

    [Fact]
    public void Fuera_De_Local_Y_Sin_Perfil_No_Se_Inventa_Una_Ruta()
    {
        // Preferible dejar el comportamiento de serie a escribir claves en un directorio que
        // nadie eligió: el fallo se vería igual, pero encima con ficheros repartidos por ahí.
        var configuracion = new ConfigurationBuilder().Build();

        var elegido = ProteccionDeDatos.ResolverDirectorio(configuracion, esEntornoLocal: false, inicioDelPerfil: null);

        Assert.Null(elegido);
    }

    public void Dispose() => BorrarSiExiste(_directorioDeClaves);

    private static void BorrarSiExiste(string directorio)
    {
        try
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
        catch (IOException)
        {
            // Directorio temporal del sistema: si algo lo tiene abierto, se queda ahí.
        }
    }
}
