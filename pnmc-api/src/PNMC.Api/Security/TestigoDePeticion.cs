using Microsoft.AspNetCore.Antiforgery;

namespace PNMC.Api.Security;

/// <summary>
/// Comprueba el testigo que acompaña a una petición que escribe.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTABA ESCRITO TRECE VECES, BAJO CINCO NOMBRES.</b> La auditoría
/// encontró las mismas cuatro líneas copiadas en trece ficheros de puntos de entrada, llamadas
/// <c>ValidarAntiforgeryAsync</c> en nueve de ellos y <c>Csrf</c>, <c>TestigoValido</c>,
/// <c>TestigoValidoAsync</c> y <c>EsPeticionValidaAsync</c> en los otros cuatro. Todas hacían
/// exactamente lo mismo.
/// </para>
/// <para>
/// <b>POR QUE IMPORTA MAS DE LO QUE PARECE.</b> Cuatro líneas repetidas no cuestan mantenimiento
/// mientras nadie las toque; el problema es el día que haya que cambiarlas —un tipo de excepción
/// nuevo, un registro cuando el testigo falla, una excepción para una ruta concreta—. Entonces hay
/// que encontrar trece sitios y acertar en los trece, y basta olvidar uno para que una puerta quede
/// comprobando distinto que las demás sin que nada lo señale.
/// </para>
/// </remarks>
public static class TestigoDePeticion
{
    /// <summary>
    /// Si la petición trae un testigo válido.
    /// </summary>
    /// <remarks>
    /// DEVUELVE UN BOOLEANO Y NO LANZA, a propósito: quien llama contesta con su propio mensaje, y
    /// cada circuito dice lo suyo —«el borrador de revisión no pudo validarse», «la edición no pudo
    /// validarse»—. Un error genérico desde aquí obligaría a la persona a adivinar qué formulario
    /// falló.
    /// </remarks>
    public static async Task<bool> ValidoAsync(IAntiforgery antiforgery, HttpContext contexto)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(contexto);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
