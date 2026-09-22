using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Security;

/// <summary>
/// Cierra un grupo de rutas detrás del módulo de consola al que pertenece.
/// </summary>
/// <remarks>
/// <para>
/// <b>SOBRE EL GRUPO Y NO SOBRE CADA MANEJADOR, y es deliberado</b>, igual que
/// <see cref="Permisos.PoliticaFuncionario"/>. Muchas rutas de la consola ni siquiera reciben
/// <c>ClaimsPrincipal</c> en su firma: cerrarlas una a una obligaría a cambiar cada firma y, peor,
/// dejaría la puerta abierta en la siguiente ruta que alguien añada sin acordarse. Puesto en el
/// grupo, un endpoint nuevo nace cerrado.
/// </para>
/// <para>
/// <b>SE DEVUELVE 403 Y NO 404.</b> Quien llega aquí ya demostró que tiene sesión de consola y rol
/// interno: no se le está ocultando que el módulo existe —lo ve en la documentación y puede que en
/// la barra de un compañero—, se le está diciendo que a él no se lo han activado. Un 404 le haría
/// buscar un fallo que no existe.
/// </para>
/// <para>
/// <b>NO SUSTITUYE A LA POLITICA DE ROL, SE SUMA.</b> Primero hay que ser funcionario del Programa
/// —eso lo decide <c>RequireAuthorization</c>— y después tener ese módulo activado. Son dos
/// preguntas distintas: quién eres y qué te han activado.
/// </para>
/// </remarks>
public static class ExigeModuloExtensiones
{
    /// <summary>Exige que la cuenta tenga activado ese módulo de la consola.</summary>
    public static TConstructor ExigeModulo<TConstructor>(this TConstructor grupo, string modulo)
        where TConstructor : IEndpointConventionBuilder
    {
        // SE COMPRUEBA AL ARRANCAR Y NO EN LA PRIMERA PETICION. Un módulo mal escrito aquí dejaría
        // la ruta cerrada para todo el mundo en silencio; así el arranque falla y se ve enseguida.
        if (!ModulosDeLaConsola.Existe(modulo))
        {
            throw new ArgumentException(
                $"«{modulo}» no es un módulo de la consola. Los módulos son los de la barra izquierda "
                + "y viven en ModulosDeLaConsola; si acaba de añadir una sección, añádalo también allí.",
                nameof(modulo));
        }

        grupo.AddEndpointFilter(async (contexto, siguiente) =>
        {
            var db = contexto.HttpContext.RequestServices.GetRequiredService<PnmcDbContext>();
            var ct = contexto.HttpContext.RequestAborted;

            // PRIMERO EL RECORRIDO DE BIENVENIDA, Y AQUI TAMBIEN. Una cuenta que todavía usa la
            // contraseña que le puso otra persona, o que no ha dicho quién es, no trabaja en la
            // consola. Comprobarlo solo en la pantalla dejaría la puerta abierta a quien escriba la
            // ruta, que es exactamente el defecto que este filtro existe para cerrar.
            var cuenta = PermisosDeModulo.CuentaDe(contexto.HttpContext.User);
            if (cuenta is not null)
            {
                var estado = await db.Users.AsNoTracking()
                    .Where(x => x.Id == cuenta.Value)
                    .Select(x => new { x.DebeCambiarContrasena, x.PerfilCompletado })
                    .FirstOrDefaultAsync(ct);

                if (estado is not null && (estado.DebeCambiarContrasena || !estado.PerfilCompletado))
                {
                    return Results.Problem(
                        detail: estado.DebeCambiarContrasena
                            ? "Antes de trabajar en la consola tienes que cambiar la contraseña con la que se te entregó la cuenta."
                            : "Antes de trabajar en la consola tienes que completar tu perfil.",
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Falta terminar tu primer ingreso");
                }
            }

            var puede = await PermisosDeModulo.PuedeAsync(
                db, contexto.HttpContext.User, modulo, ct);

            if (!puede)
            {
                return Results.Problem(
                    detail: "Tu cuenta no tiene activado este módulo del Espacio de Gestión Administrativa. "
                        + "Quien administre los usuarios puede activártelo.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Módulo no activado para esta cuenta");
            }

            return await siguiente(contexto);
        });

        return grupo;
    }
}
