using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace PNMC.Api.Security;

/// <summary>
/// Lo que toda ruta del espacio externo necesita saber antes de hacer nada: quién pide, y si la
/// petición viene de nuestra propia página.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTABA ESCRITO TRES VECES.</b> <c>FestivalesExternosEndpoints</c>,
/// <c>EdicionesFestivalExternosEndpoints</c> y <c>PropuestasDeCambioFestivalExternosEndpoints</c>
/// llevan cada uno su copia privada de estos dos ayudantes. Son cortos y hoy dicen lo mismo, pero
/// tres copias del mismo criterio de seguridad son tres sitios donde endurecerlo y dos donde
/// olvidarlo. Los ficheros nuevos usan esta pieza; los tres antiguos quedan nombrados como deuda
/// acotada en la bitácora, y su retirada es mecánica.
/// </para>
/// <para>
/// <b>EL ANTIFORGERY DEVUELVE UN BOOLEANO Y NO LANZA.</b> Quien llama tiene que poder responder un
/// 400 con un mensaje que se entienda —«actualiza la página e inténtalo de nuevo»— en vez de dejar
/// salir una excepción que el cliente lee como un fallo del servidor.
/// </para>
/// </remarks>
public static class SesionExterna
{
    /// <summary>El identificador de la persona que tiene la sesión externa, si lo hay.</summary>
    public static int? PersonaDe(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return int.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var persona)
            ? persona
            : null;
    }

    /// <summary>Si la petición trae el testigo antiforgery válido.</summary>
    /// <remarks>
    /// CONSERVA SU NOMBRE PORQUE DICE ALGO QUE EL OTRO NO. En el canal externo lo que importa no es
    /// que el testigo sea correcto sino lo que eso significa: que la petición viene de nuestra
    /// página y no de un formulario ajeno. La comprobación, en cambio, es la misma de siempre y por
    /// eso delega en <see cref="TestigoDePeticion"/> en vez de volver a escribirla.
    /// </remarks>
    public static Task<bool> VieneDeNuestraPaginaAsync(IAntiforgery antiforgery, HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(antiforgery);
        return TestigoDePeticion.ValidoAsync(antiforgery, contexto);
    }
}
