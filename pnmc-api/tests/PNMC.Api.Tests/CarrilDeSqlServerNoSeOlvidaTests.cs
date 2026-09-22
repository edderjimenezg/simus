using System.Net.Sockets;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Si el contenedor de SQL Server está en pie, el carril que lo usa <b>tiene que estar
/// encendido</b>.
/// </summary>
/// <remarks>
/// <para>
/// EL PROBLEMA QUE CIERRA, Y ESTABA ESCRITO COMO PENDIENTE EN LA AUDITORÍA DE CIERRE. El carril
/// de SQL Server está apagado por omisión: sin <c>PNMC_PRUEBAS_SQLSERVER=1</c> se omiten
/// <b>22</b> pruebas, y entre ellas están casi todas las que miden la migración de SIMUS —la
/// paridad de esquema, la estructura, el catálogo de permisos, la siembra re-ejecutable y el alta
/// de usuarios—. El plan de construcción lo dice sin rodeos: <i>escribirlas y dejar la vía apagada
/// equivale a no escribirlas</i>. Y la omisión es <b>silenciosa</b>: la corrida acaba en verde y
/// el número de omitidas hay que ir a buscarlo.
/// </para>
/// <para>
/// POR QUÉ NO SE ENCIENDE SIN MÁS. Encenderla por omisión obligaría a tener Docker para correr
/// <c>dotnet test</c>, y quien no lo tenga pasaría de «22 omitidas» a «22 rojas», que es peor:
/// un rojo que no significa nada enseña a ignorar los rojos.
/// </para>
/// <para>
/// LO QUE SÍ SE PUEDE AFIRMAR, Y ES LO QUE COMPRUEBA ESTA PRUEBA: <b>si el puerto del contenedor
/// responde, no hay excusa para tener la vía apagada</b>. Quien tiene el contenedor arriba y no
/// puso la variable se la olvidó, y esto se lo dice con el nombre de la variable en el mensaje. A
/// quien no tiene Docker no le molesta: el puerto no responde y la prueba pasa.
/// </para>
/// <para>
/// NO USA EL ARNÉS NI ABRE UNA CONEXIÓN DE BASE DE DATOS. Comprueba el puerto TCP con un tiempo de
/// espera corto y se va. Abrir una conexión real la ataría a que las credenciales por omisión sean
/// las correctas, y entonces fallaría por una razón distinta de la que investiga.
/// </para>
/// <para>
/// EN EL CI NO HACE FALTA: allí el trabajo <c>api</c> levanta el contenedor como servicio y
/// exporta la variable, de modo que esta prueba pasa por la rama de «está encendida». Vive aquí
/// para el desarrollo en la máquina de cada quien, que es donde la omisión silenciosa hace daño.
/// </para>
/// </remarks>
public sealed class CarrilDeSqlServerNoSeOlvidaTests
{
    /// <summary>Host y puerto por omisión del contenedor local, los mismos que <see cref="ArnesSqlServer"/>.</summary>
    private const string HostPorOmision = "127.0.0.1";
    private const int PuertoPorOmision = 14344;

    /// <summary>Lo que se espera a que el puerto conteste antes de darlo por caído.</summary>
    private static readonly TimeSpan Espera = TimeSpan.FromMilliseconds(600);

    [Fact]
    public void Si_El_Contenedor_Responde_La_Via_Tiene_Que_Estar_Encendida()
    {
        if (ArnesSqlServer.Habilitado)
        {
            // Ya está encendida: no hay nada que avisar. Se afirma algo igualmente para que la
            // prueba no sea un cuerpo vacío que pasa sin mirar nada.
            Assert.True(ArnesSqlServer.Habilitado);
            return;
        }

        if (!PuertoResponde(HostPorOmision, PuertoPorOmision, Espera))
        {
            // Sin contenedor no hay nada que exigir. Es el caso de quien no usa Docker, y es
            // legítimo: las 22 pruebas de esa vía se omiten y esta también se calla.
            return;
        }

        Assert.Fail(
            $"El contenedor de SQL Server responde en {HostPorOmision}:{PuertoPorOmision}, pero "
            + $"{ArnesSqlServer.VariableDeEncendido} no está puesta: se van a OMITIR las pruebas que "
            + "miden la migración de SIMUS —paridad de esquema, estructura, catálogo de permisos, "
            + "siembra re-ejecutable y alta de usuarios— y la corrida acabará en verde sin haberlas "
            + "ejecutado.\n\n"
            + $"    PowerShell:  $env:{ArnesSqlServer.VariableDeEncendido} = \"1\"; dotnet test PNMC.Api.sln\n"
            + $"    bash:        {ArnesSqlServer.VariableDeEncendido}=1 dotnet test PNMC.Api.sln\n\n"
            + "Si de verdad quieres correr solo la vía rápida, para el contenedor.");
    }

    /// <summary>
    /// ¿Hay algo escuchando en ese puerto? Sin abrir una conexión de base de datos.
    /// </summary>
    private static bool PuertoResponde(string host, int puerto, TimeSpan espera)
    {
        try
        {
            using var cliente = new TcpClient();
            return cliente.ConnectAsync(host, puerto).Wait(espera) && cliente.Connected;
        }
        catch (Exception excepcion) when (excepcion is SocketException or AggregateException or ObjectDisposedException)
        {
            // Cualquier fallo al conectar significa lo mismo para esta prueba: no hay contenedor
            // que exigir. Se listan los tipos en vez de capturar todo, para que un fallo distinto
            // —uno que sí sea un defecto— siga saliendo a la superficie.
            return false;
        }
    }
}
