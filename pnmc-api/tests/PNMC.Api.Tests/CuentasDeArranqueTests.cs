using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Las cuentas de arranque del entorno local se siembran por los DOS caminos, no por uno.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO. <c>externo@pnmc.local</c> figura en la documentación con la contraseña <c>admin</c> y
/// <c>POST /api/v1/externo/auth/login</c> devolvía <b>401 siempre</b>. La causa no era la política
/// ni la contraseña: había DOS listas de cuentas de arranque. La rama de SQLite —la del carril
/// rápido— sembraba tres, y el bloque que hashea contra SQL Server, bajo
/// <c>Database:SeedBootstrapUsers</c>, sembraba dos. La tercera se quedaba con el literal
/// <c>pendiente_configurar_hash_seguro</c>, que no es Base64, de modo que
/// <c>VerifyHashedPassword</c> lanza y el login lo traduce a «contraseña incorrecta».
/// </para>
/// <para>
/// Medido contra <c>PNMC_LOCAL</c>: <c>admin@</c> y <c>gestor@</c> con hash
/// de 84 caracteres, las tres cuentas externas con el de 32. Tras el arreglo, y con la API
/// reiniciada, las cinco tienen 84 y el login responde <b>200</b>.
/// </para>
/// <para>
/// POR QUÉ ESTA PRUEBA MIRA LA LISTA Y NO HACE LOGIN. Intentarlo fue lo primero, y la prueba se puso
/// en rojo por un motivo que no era el defecto: el arné de SQL Server construye su base con
/// <c>schema/</c> y <c>seed/</c>, y en su configuración <c>Database:SeedBootstrapUsers</c> está en
/// <c>false</c>, así que ahí NINGUNA de las seis cuentas tiene hash utilizable —tampoco
/// <c>admin@</c>—. Una prueba de login en ese arné no puede distinguir el arreglo de su ausencia.
/// </para>
/// <para>
/// LO QUE SÍ SE PUEDE VIGILAR es la causa: que exista UNA lista y que las dos ramas la usen. Con una
/// sola, añadir una cuenta la siembra en los dos caminos y quitarla la quita de los dos; el
/// desajuste que produjo el 401 deja de ser posible por construcción.
/// </para>
/// </remarks>
public sealed class CuentasDeArranqueTests
{
    [Fact]
    public void La_Cuenta_Externa_Esta_En_La_Lista_De_Arranque()
    {
        var correos = DatabaseBootstrapper.CuentasDeArranque.Select(cuenta => cuenta.Correo).ToArray();

        // Era la que faltaba en la rama de SQL Server, y la que hizo falta para verificar el
        // recorrido de sesión: al no servir, hubo que dar de alta una organización por el
        // formulario real para poder seguir.
        Assert.Contains("externo@pnmc.local", correos, StringComparer.Ordinal);
    }

    [Fact]
    public void Las_Cinco_Cuentas_Que_La_Semilla_SQL_Crea_Reciben_Contrasena()
    {
        var correos = DatabaseBootstrapper.CuentasDeArranque
            .Select(cuenta => cuenta.Correo)
            .OrderBy(correo => correo, StringComparer.Ordinal)
            .ToArray();

        // Son exactamente las cinco que `seed/V20260519_07__datos_moderacion_consola.sql` inserta
        // con el marcador de siembra. Si la semilla añade una sexta y esta lista no, esa cuenta
        // nacerá sin poder iniciar sesión y nada lo dirá: el síntoma es un 401 con la contraseña
        // que la documentación declara.
        Assert.Equal(
            [
                "admin@pnmc.local",
                "externo@pnmc.local",
                "gestor@pnmc.local",
                "participante.dos@pnmc.local",
                "participante.tres@pnmc.local",
            ],
            correos);
    }

    [Fact]
    public void La_Cuenta_De_Sistema_NO_Esta_En_La_Lista()
    {
        var correos = DatabaseBootstrapper.CuentasDeArranque.Select(cuenta => cuenta.Correo).ToArray();

        // Y NO ES UN OLVIDO. `sistema@pnmc.local` es la cuenta a la que se atribuyen las acciones
        // automáticas, y `AdminAuthEndpoints.WebmastersQuePuedenEntrarAsync` cuenta con que NO pueda
        // iniciar sesión: si pudiera, la guarda del último webmaster contaría a alguien que nadie
        // usa y dejaría desactivar al único real. Darle contraseña «arreglaría» un 401 legítimo y
        // rompería esa guarda sin que ninguna otra prueba lo notara.
        Assert.DoesNotContain("sistema@pnmc.local", correos, StringComparer.Ordinal);
    }

    [Fact]
    public void Cada_Cuenta_Declara_Un_Rol_Que_El_Arranque_Crea()
    {
        // Los tres que `EnsureRoleAsync` crea antes de sembrar usuarios. Un rol que no exista hace
        // que `Roles.FirstAsync` lance en el arranque, y el API no levanta.
        string[] rolesDelArranque = ["webmaster", "gestor_interno", "externo"];

        foreach (var cuenta in DatabaseBootstrapper.CuentasDeArranque)
        {
            Assert.Contains(cuenta.Rol, rolesDelArranque, StringComparer.Ordinal);
        }
    }
}
