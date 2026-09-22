using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PNMC.Api.Security;

/// <summary>
/// Donde viven las claves que firman las tres cookies de sesion.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO QUE ESTO CIERRA, Y POR QUE NO SE VE EN LOCAL. Sin configurar nada, ASP.NET guarda el
/// anillo de claves en el perfil del usuario que ejecuta el proceso. En un portatil eso sobrevive
/// a los reinicios y nadie lo nota nunca. En un servicio administrado —App Service, contenedores,
/// cualquier cosa que reemplace el sistema de ficheros al publicar— ese directorio se pierde en
/// cada despliegue: las claves cambian, las cookies emitidas antes dejan de validarse y
/// <b>cada publicacion expulsa a todo el que estuviera dentro</b>. No hay error, no hay traza: la
/// gente vuelve a ver la pantalla de entrada y cree que se le caduco la sesion.
/// </para>
/// <para>
/// ES PEOR CON MAS DE UNA INSTANCIA. Dos procesos con anillos distintos firman con claves
/// distintas: quien entra por uno recibe 401 al siguiente clic si el balanceador lo manda al otro.
/// El fallo aparece y desaparece, que es la forma mas cara de encontrarlo.
/// </para>
/// <para>
/// COMO SE ELIGE EL SITIO. Por orden: lo que diga <c>Security:RutaDeClaves</c>; si no,
/// <c>$HOME/pnmc-claves</c> cuando la variable <c>HOME</c> exista y el entorno no sea local —en App
/// Service <c>HOME</c> apunta a un recurso compartido que sobrevive al despliegue y lo ven todas
/// las instancias—; y si no hay ninguna de las dos, no se toca nada y se deja el comportamiento de
/// serie, que es lo que conviene en un portatil.
/// </para>
/// <para>
/// <c>SetApplicationName</c> NO ES DECORATIVO: sin el, el nombre se deriva de la ruta del
/// contenido, asi que dos despliegues en rutas distintas no se leen las claves aunque compartan el
/// directorio. Fijarlo es lo que hace que el anillo sirva de verdad.
/// </para>
/// </remarks>
public static class ProteccionDeDatos
{
    /// <summary>El nombre con el que se aisla el anillo. Fijo a proposito: ver arriba.</summary>
    public const string NombreDeAplicacion = "PNMC";

    /// <summary>La clave de configuracion que manda sobre todo lo demas.</summary>
    public const string ClaveDeRuta = "Security:RutaDeClaves";

    /// <summary>
    /// Decide el directorio del anillo, o <c>null</c> si no hay que tocar nada.
    /// </summary>
    /// <remarks>
    /// Se separa de <see cref="Configurar"/> para poder comprobar la tabla de decision sin montar
    /// la aplicacion: es una funcion de tres entradas y una salida, y asi se prueba como tal.
    /// </remarks>
    public static string? ResolverDirectorio(IConfiguration configuracion, bool esEntornoLocal, string? inicioDelPerfil)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var declarado = configuracion[ClaveDeRuta];
        if (!string.IsNullOrWhiteSpace(declarado))
        {
            // Manda aunque el entorno sea local: es lo que permite que una prueba compruebe que
            // las claves sobreviven, y lo que deja abrir una excepcion sin tocar codigo.
            return declarado.Trim();
        }

        if (esEntornoLocal || string.IsNullOrWhiteSpace(inicioDelPerfil))
        {
            return null;
        }

        return Path.Combine(inicioDelPerfil, "pnmc-claves");
    }

    /// <summary>Aplica la decision sobre los servicios, si hay algo que aplicar.</summary>
    public static void Configurar(IServiceCollection servicios, IConfiguration configuracion, bool esEntornoLocal)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        var directorio = ResolverDirectorio(configuracion, esEntornoLocal, Environment.GetEnvironmentVariable("HOME"));
        if (directorio is null)
        {
            return;
        }

        Directory.CreateDirectory(directorio);

        servicios.AddDataProtection()
            .SetApplicationName(NombreDeAplicacion)
            .PersistKeysToFileSystem(new DirectoryInfo(directorio));
    }
}
