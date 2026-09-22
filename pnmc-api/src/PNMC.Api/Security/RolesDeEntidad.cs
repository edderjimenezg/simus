namespace PNMC.Api.Security;

/// <summary>
/// Punto unico donde se dice <b>que roles responden por una organizacion</b>.
/// </summary>
/// <remarks>
/// <para>
/// EL ROL EN LA ENTIDAD NO ES EL ROL DE PLATAFORMA. <c>dbo.UsuariosRoles</c> guarda si alguien es
/// <c>webmaster</c>, <c>gestor_interno</c> o <c>externo</c>, y eso decide si puede iniciar sesion y
/// por que puerta. <c>dbo.UsuariosEntidades.RolEntidad</c> guarda otra cosa: que es esa persona
/// <b>dentro de una organizacion concreta</b>. Confundirlos es facil porque los dos se llaman
/// «rol»; <see cref="RolesDeUsuario"/> responde por el primero y este fichero por el segundo.
/// </para>
/// <para>
/// POR QUE EXISTE ESTE FICHERO, con su fecha y sus numeros. Hasta la
/// respuesta estaba escrita <b>ocho veces</b>: siete comparaciones sueltas
/// <c>EntityRole == "administrador"</c> repartidas por cinco ficheros del canal externo, y una
/// lista de dos elementos en <c>AdminOrganizacionesEndpoints</c>. Las ocho decian lo mismo salvo
/// que no: la de la consola aceptaba <c>propietario</c> y las siete de fuera no. Una regla copiada
/// es como se llega a que una copia se quede atras sin que nadie lo note —el mismo argumento con
/// el que existen <see cref="Permisos"/> y <see cref="RolesDeUsuario"/>—, y aqui ya habia pasado.
/// </para>
/// <para>
/// LO QUE COSTABA. En <c>PNMC_LOCAL</c> habia <b>12 vinculos <c>propietario</c> contra 6
/// <c>administrador</c></b>: la mayoria de las organizaciones tenia a su duena fuera. Se veia
/// entero en una pantalla: la barra de navegacion escribia el nombre de la organizacion —lo lee de
/// <c>/external/auth/me</c>, que si contaba los dos roles— mientras el panel decia «No fue posible
/// consultar los datos de la organizacion», porque <c>/externo/organizaciones/{id}/perfil</c>
/// respondia 403. Y no era un caso raro de datos viejos: crear una organizacion <b>desde la consola
/// interna</b> escribe <c>propietario</c> desde su contrato institucional mientras que crearla desde
/// el alta externa escribe <c>administrador</c> (<c>AltaDeOrganizacion</c>), de modo que toda
/// organizacion nacida por el primer camino dejaba a su responsable sin poder administrarla.
/// </para>
/// <para>
/// NO ES UNA PUERTA MAS ANCHA. Un vinculo cualquiera no basta y esa es la mitad que sostiene lo
/// demas: la lista tiene dos nombres, no es «cualquiera que aparezca en <c>UsuariosEntidades</c>».
/// Quien figure con otro rol —un contacto, un colaborador— sigue fuera, y quien tenga el vinculo
/// dado de baja (<c>Activo = 0</c>) tambien, porque el estado del vinculo se comprueba aparte y
/// sigue comprobandose. Lo fija <c>PropietarioRespondePorSuOrganizacionTests</c>, que prueba las
/// dos mitades y ademas que ser duena de la propia organizacion no abre la ajena.
/// </para>
/// <para>
/// ANADIR UN NOMBRE A ESTA LISTA ABRE, DE GOLPE, LAS OCHO RUTAS. Es lo que se buscaba —que la
/// decision se tome una vez y en un sitio— y es tambien la razon de que no se anada sin escribir
/// aqui por que.
/// </para>
/// </remarks>
public static class RolesDeEntidad
{
    /// <summary>
    /// Los roles cuyo titular responde por la organizacion y puede administrarla.
    /// </summary>
    /// <remarks>
    /// <c>propietario</c> es quien la creo o quien la tiene a su cargo; <c>administrador</c> es
    /// quien fue nombrado para gestionarla. Los dos administran; se conservan como nombres
    /// distintos porque la diferencia importa para saber a quien se le pide cuentas, no para
    /// decidir quien entra.
    /// </remarks>
    public static readonly string[] QueResponden = ["administrador", "propietario"];

    /// <summary>
    /// Si el rol indicado responde por la organizacion.
    /// </summary>
    /// <remarks>
    /// NO SIRVE DENTRO DE UNA CONSULTA DE EF. Este metodo es para el codigo que ya tiene la fila en
    /// memoria; en un <c>Where</c> que viaja a la base hay que usar
    /// <c>RolesDeEntidad.QueResponden.Contains(...)</c>, que EF traduce a <c>IN</c>. Un metodo
    /// propio ahi dentro lanza en tiempo de ejecucion, no de compilacion.
    /// </remarks>
    public static bool Responde(string? rolEnLaEntidad) =>
        rolEnLaEntidad is not null && QueResponden.Contains(rolEnLaEntidad, StringComparer.Ordinal);
}
