namespace PNMC.Api.Endpoints;

/// <summary>
/// La convención de «ver un registro como se verá en su listado», antes de publicarlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE PROBLEMA RESUELVE.</b> Hasta la consola solo ofrecía «Ver en
/// el portal», y solo cuando el registro <b>ya</b> estaba publicado. Es decir: la única forma de
/// saber cómo iba a quedar algo era publicarlo. Lo que se descubre así —un titular que se corta a
/// dos líneas, un resumen vacío, una imagen con el encuadre equivocado— se descubre con el registro
/// ya en la calle.
/// </para>
/// <para>
/// <b>POR QUE «EN SU LISTADO» Y NO «LA FICHA COMPLETA».</b> Lo decidió la dirección en la pausa de
/// consolidación. La ficha de detalle enseña el texto entero y casi nunca sorprende; el listado es
/// donde el contenido se recorta, y es lo primero —a veces lo único— que ve una persona. Los
/// defectos que importan viven ahí.
/// </para>
/// <para>
/// <b>SE DEVUELVE EL MISMO DTO QUE LA LECTURA PUBLICA.</b> No uno parecido: el mismo, por el mismo
/// mapeador. Una previsualización que arma su propia forma deja de ser una previsualización en
/// cuanto la lectura pública cambie un campo, y lo hará en silencio. La <b>única</b> diferencia con
/// la ruta pública es que esta no aplica el filtro de visibilidad.
/// </para>
/// <para>
/// <b>Y POR ESO EXIGE SESION DE CONSOLA.</b> Devuelve registros que todavía no son públicos. La
/// página pública que los pinta dentro del marco los pide con la cookie de quien está en la
/// consola; a un visitante anónimo el servidor le responde 401 y el marco se queda con el listado
/// de siempre. El borrador no llega a nadie que no pudiera verlo ya en la consola.
/// </para>
/// <para>
/// <b>NO ESCRIBE NADA.</b> Previsualizar no es un paso del circuito, no cambia el estado, no deja
/// rastro de publicación y no adelanta la fecha de aparición.
/// </para>
/// </remarks>
public static class PrevisualizacionEnListado
{
    /// <summary>
    /// El prefijo común de las rutas de previsualización.
    /// </summary>
    /// <remarks>
    /// CADA MODULO REGISTRA LA SUYA, junto a sus otras rutas y usando su propio mapeador. Un fichero
    /// que las reuniera todas tendría que alcanzar los mapeadores privados de los tres módulos y
    /// convertirlos en internos, que es empezar a abrir puertas para una comodidad.
    /// </remarks>
    public const string Prefijo = "/admin/previsualizacion";

    /// <summary>El parámetro con el que la página pública sabe que la abrieron para previsualizar.</summary>
    /// <remarks>
    /// VIAJA EN LA DIRECCION A PROPOSITO, al revés que el testigo de confirmación de correo: aquí no
    /// hay ningún secreto en el identificador —quien lo escribe ya está en la consola— y en cambio
    /// hace falta que la dirección entera se pueda poner en el <c>src</c> de un marco.
    /// </remarks>
    public const string Parametro = "pnmcPrevisualizar";
}
