using Microsoft.AspNetCore.Authentication.Cookies;

namespace PNMC.Api.Security;

/// <summary>
/// Los dos esquemas de autenticacion del portal: el institucional y el externo.
/// </summary>
/// <remarks>
/// <para>
/// <b>El nombre es historico y enganoso.</b> SIMUS —el Sistema de Informacion
/// de la Musica— es una plataforma del Ministerio que vive en
/// simus.mincultura.gov.co: el portal del PNMC enlaza hacia ella y nada mas. No
/// autentica a nadie aqui, no revisa registros y no comparte sesion. Quien lea
/// <c>SimusAuthentication.ExternalPolicy</c> en un endpoint no debe entender
/// «autorizado por SIMUS» sino «autorizado por el acceso externo del PNMC».
/// </para>
/// <para>
/// El renombrado esta pendiente y anotado en el Brief. Lo que NO puede cambiar
/// a la ligera es la cadena <c>"SimusExternal"</c>: es el nombre del esquema
/// con el que se firma la cookie de sesion externa, de modo que cambiarla
/// invalida de golpe la sesion de toda organizacion que este dentro. El
/// renombrado del tipo y el de la cadena son dos decisiones distintas.
/// </para>
/// </remarks>
public static class SimusAuthentication
{
    public const string InstitutionalScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Nombre del esquema de la cookie externa. Cambiarlo cierra la sesion de
    /// todas las organizaciones conectadas; ver las notas del tipo.
    /// </summary>
    public const string ExternalScheme = "SimusExternal";
    public const string InstitutionalPolicy = "institutional-principal";
    public const string ExternalPolicy = "external-principal";

    /// <summary>
    /// Cualquiera de las dos sesiones vale, y el manejador filtra por identidad y ámbito.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EXISTE PARA LO QUE ES DE LAS DOS PARTES. Cada aviso declara destinatario y ámbito. El
    /// manejador exige que ambos coincidan con la sesión desde la cual se consulta.
    /// </para>
    /// <para>
    /// NO ES UNA PUERTA MAS ANCHA, es una puerta distinta: sigue exigiendo sesion autenticada y
    /// ambito declarado. Lo que <b>no</b> hace es decidir nada sobre el contenido —eso lo hace
    /// el manejador, filtrando por destinatario y ámbito—. Usarla en una ruta que no aplique
    /// ambos filtros seria abrir esa ruta a todo el mundo con cuenta.
    /// </para>
    /// <para>
    /// Antes de que existiera, las dos rutas del buzon se cerraban con
    /// <c>.RequireAuthorization()</c> a secas, que evalua el esquema por defecto: el
    /// institucional. Las organizaciones destinatarias no podían leer sus avisos externos.
    /// </para>
    /// </remarks>
    public const string PoliticaCualquierSesion = "cualquier-sesion-valida";
    public const string AccessScopeClaim = "pnmc_access_scope";
    public const string InstitutionalScope = "institutional";
    public const string ExternalScope = "external";
}
