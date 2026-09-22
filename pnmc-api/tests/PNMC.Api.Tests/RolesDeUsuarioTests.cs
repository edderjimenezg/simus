using PNMC.Api.Security;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El orden y la unicidad del conjunto de roles, que desde entonces son <b>carga estructural</b>
/// y no cosmética.
/// </summary>
/// <remarks>
/// <para>
/// TRES COSAS DEPENDEN DE ESTA ORDENACIÓN, y ninguna es evidente leyendo el sitio donde se usa:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>La revalidación de cada petición</b> compara la huella del conjunto de la cookie contra
///     la del conjunto de la base (<c>Program.cs</c>, <c>RevalidarSesionAsync</c>). Si los dos
///     lados no ordenaran igual, <c>{webmaster, gestor}</c> y <c>{gestor, webmaster}</c> parecerían
///     conjuntos distintos y el sistema echaría a la gente en cada petición.
///   </description></item>
///   <item><description>
///     <b>El rol principal</b> que viaja en la cookie, en el DTO y —mientras dure la transicion— en la
///     columna <c>Usuarios.IdRol</c>. Si dependiera del plan de ejecución de SQL Server, la misma
///     cuenta se etiquetaría distinto entre dos peticiones.
///   </description></item>
///   <item><description>
///     <b>Y una equivalencia que hoy tapa un agujero.</b> <c>AdminDataEndpoints</c> decide qué
///     transiciones de estado permite por la UNIÓN de los roles. Se sembró el mutante que
///     sustituye la unión por «el primero de la lista» y <b>sobrevivió</b>: como esta ordenación
///     pone <c>webmaster</c> primero y webmaster puede todo lo que puede <c>gestor_interno</c>,
///     las dos reglas dan la misma respuesta. La equivalencia se apoya enteramente en que el
///     orden sea éste. <b>Si alguien reordena <c>RolesDeUsuario.Precedencia</c> o cambia
///     <c>Ordenar</c> por un <c>OrderBy</c> alfabético, esa equivalencia se rompe en silencio</b>
///     —alfabéticamente <c>gestor_interno</c> va antes que <c>webmaster</c>— y esta prueba es lo
///     único que se pondría rojo.
///   </description></item>
/// </list>
/// <para>
/// SE PRUEBA CON LA LISTA DESORDENADA A PROPÓSITO. Pasarla ya ordenada dejaría en verde una
/// implementación que no ordenara nada, que es la forma habitual de que una prueba de ordenación
/// no mida la ordenación.
/// </para>
/// </remarks>
public sealed class RolesDeUsuarioTests
{
    [Fact]
    public void Ordenar_Pone_Los_Roles_Por_Precedencia_Y_No_Por_Orden_De_Llegada()
    {
        var desordenados = new[] { "gestor_interno", "externo", "webmaster" };

        Assert.Equal(["webmaster", "gestor_interno", "externo"], RolesDeUsuario.Ordenar(desordenados));

        // Y al revés: da igual cómo lleguen.
        Assert.Equal(
            RolesDeUsuario.Ordenar(desordenados),
            RolesDeUsuario.Ordenar(desordenados.Reverse()));
    }

    [Fact]
    public void Ordenar_Normaliza_Quita_Repetidos_Y_Descarta_Los_Vacios()
    {
        var sucios = new[] { "  WEBMASTER ", "webmaster", "", "   ", null, "Gestor_Interno" };

        Assert.Equal(["webmaster", "gestor_interno"], RolesDeUsuario.Ordenar(sucios));
    }

    [Fact]
    public void Un_Rol_Que_Nadie_Declaro_Va_Al_Final_Y_En_Orden_Alfabetico()
    {
        // No se descarta: descartarlo silenciosamente haría que una cuenta con un rol residual
        // pareciera no tener ninguno, y «sin roles» ya significa otra cosa —que no puede entrar—.
        // Va al final porque la precedencia solo sabe ordenar lo que el modelo declara.
        Assert.Equal(
            ["webmaster", "aaa_desconocido", "zzz_desconocido"],
            RolesDeUsuario.Ordenar(["zzz_desconocido", "webmaster", "aaa_desconocido"]));
    }

    [Fact]
    public void El_Principal_Sale_De_La_Precedencia_Y_No_De_La_Posicion()
    {
        Assert.Equal("webmaster", RolesDeUsuario.Principal(["gestor_interno", "webmaster"]));
        Assert.Equal("gestor_interno", RolesDeUsuario.Principal(["externo", "gestor_interno"]));
        Assert.Equal("externo", RolesDeUsuario.Principal(["externo"]));
    }

    [Fact]
    public void Sin_Roles_El_Principal_Es_Vacio_Y_No_Un_Rol_Por_Omision()
    {
        // Devolver "externo" aquí sería el error caro: una cuenta sin asignaciones pasaría por
        // persona del ecosistema en vez de por cuenta rota, y nadie iría a arreglarla.
        Assert.Equal(string.Empty, RolesDeUsuario.Principal([]));
    }

    [Fact]
    public void Un_Rol_Residual_Solo_Es_Principal_Si_No_Hay_Nada_Declarado()
    {
        Assert.Equal("gestor_interno", RolesDeUsuario.Principal(["rol_que_nadie_declaro", "gestor_interno"]));
        Assert.Equal("rol_que_nadie_declaro", RolesDeUsuario.Principal(["rol_que_nadie_declaro"]));
    }
}
