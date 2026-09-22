using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-014a. Puerta de entrada de la via de pruebas contra SQL Server real.
///
/// <para>
/// POR QUE EXISTE ESTE ARNES. Las 195 pruebas de la suite corren sobre SQLite
/// (<see cref="TestWebApplicationFactory"/>, que hace <c>UseSqlite</c> +
/// <c>EnsureCreated</c>). Esa combinacion fabrica la base a partir del modelo de
/// Entity Framework, de modo que la suite no toca ninguna de las dos fuentes de
/// esquema reales del proyecto:
/// </para>
/// <list type="number">
///   <item><description>
///     Los guiones de <c>pnmc-database/schema/*.sql</c>, que sobre una base nueva
///     producen 49 tablas, 61 restricciones CHECK, 1 disparador
///     (<c>TR_RegistrosEcosistema_ValidarOrigen</c>) y 1 procedimiento almacenado
///     (<c>sp_ActualizarMetricasMapa</c>). Comprobado el 21 ago 2026 aplicandolos sobre
///     una base recien creada del contenedor SQL local.
///   </description></item>
///   <item><description>
///     La rama de SQL Server de <c>DatabaseBootstrapper.EnsureReadyAsync</c>, que en
///     arranque anade las 13 tablas restantes (ContenidoWeb, EquipoWeb,
///     VersionesFestival, PropuestasDeCambio, CatalogoEditorial...) y las
///     columnas que los guiones no traen. Con SQLite esa rama NUNCA se ejecuta:
///     el <c>if (db.Database.IsSqlServer())</c> se va por el <c>else</c>.
///   </description></item>
/// </list>
///
/// <para>
/// POR QUE ES OPCIONAL Y NO REEMPLAZA A NADA. La suite verde es hoy la unica red de
/// seguridad del proyecto. Cambiarle el motor a las 195 de golpe es la forma mas
/// rapida de dejarla roja. Esta via corre APARTE, esta APAGADA por omision y solo
/// lleva pruebas de muestra. Sin la variable de entorno, sus pruebas se declaran
/// omitidas y el recuento de pruebas que pasan no se mueve.
/// </para>
///
/// <para>
/// COMO SE ENCIENDE (PowerShell, con el contenedor arriba):
/// <code>
/// $env:PNMC_PRUEBAS_SQLSERVER = "1"
/// dotnet test pnmc-api/tests/PNMC.Api.Tests
/// </code>
/// </para>
/// </summary>
public static class ArnesSqlServer
{
    /// <summary>Variable que enciende la via. Sin ella, las pruebas se omiten.</summary>
    public const string VariableDeEncendido = "PNMC_PRUEBAS_SQLSERVER";

    /// <summary>Cadena de conexion completa a la base <c>master</c>, si se quiere sustituir la de por omision.</summary>
    public const string VariableDeCadena = "PNMC_PRUEBAS_SQLSERVER_CADENA";

    /// <summary>Ruta al directorio con los guiones de esquema, si la busqueda automatica no sirve.</summary>
    /// <remarks>
    /// <para>
    /// ES UN ESCAPE, NO UN REQUISITO, y conviene dejarlo dicho porque ya paso lo contrario. Del 23
    /// al 24 de agosto de 2026 una sesion la exportaba en cada orden y la transmitio a las demas
    /// como parte del procedimiento —«exporta estas dos variables o el fixture se cae»—. No era
    /// configuracion: tapaba un defecto real del arnes, que resolvia la ruta subiendo desde el
    /// binario y por tanto no encontraba nada si alguien redirigia la salida de compilacion. Con
    /// la variable puesta, su carril estaba siempre verde y el defecto era invisible; sin ella, el
    /// carril ENTERO era imposible de ejecutar en ese modo. Se arreglo resolviendo la ruta desde
    /// las fuentes (ver <see cref="RutasDelRepositorio"/>).
    /// </para>
    /// <para>
    /// LA REGLA QUE QUEDA: <b>si una orden necesita siempre la misma variable de escape, esa
    /// variable esta tapando algo.</b> Un rodeo repetido deja de leerse como sintoma y pasa a
    /// leerse como configuracion, y entonces nadie vuelve a mirarlo. Hoy no hace falta ninguna
    /// variable: basta <c>PNMC_PRUEBAS_SQLSERVER=1</c>.
    /// </para>
    /// </remarks>
    public const string VariableDeEsquema = "PNMC_PRUEBAS_SQLSERVER_ESQUEMA";

    /// <summary>
    /// Prefijo obligatorio de toda base desechable. Es tambien el filtro del barrido de
    /// huerfanas y el guardian del borrado: nada que no empiece asi se llega a borrar,
    /// asi que la base de desarrollo del dueno (<c>PNMC_LOCAL</c>) queda fuera de alcance
    /// por construccion y no por cuidado del programador.
    /// </summary>
    public const string PrefijoBaseDesechable = "PNMC_PRUEBAS_";

    /// <summary>Base de desarrollo del dueno. Jamas se toca.</summary>
    public const string BaseProhibida = "PNMC_LOCAL";

    private static readonly Regex NombreAceptado = new(
        $"^{PrefijoBaseDesechable}[A-Za-z0-9_]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Verdadero solo si alguien encendio la via a proposito.</summary>
    public static bool Habilitado
    {
        get
        {
            var valor = Environment.GetEnvironmentVariable(VariableDeEncendido);
            if (string.IsNullOrWhiteSpace(valor))
            {
                return false;
            }

            return valor.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "si" or "sí";
        }
    }

    /// <summary>Texto que ve quien lee el informe de pruebas cuando la via esta apagada.</summary>
    public const string MotivoDeOmision =
        "Via opcional contra SQL Server real (PNMC-014a). Requiere el contenedor simus-desarrollo-sqlserver "
        + "y " + VariableDeEncendido + "=1. Apagada por omision para no atar la suite a Docker.";

    /// <summary>
    /// Cadena de conexion a <c>master</c>. Los valores por omision son los del
    /// contenedor local declarado en <c>docker-compose.local.yml</c>; no hay secreto
    /// que proteger aqui porque el puerto esta publicado solo en 127.0.0.1 y el
    /// sistema nunca se ha desplegado.
    /// </summary>
    public static string CadenaMaestra()
    {
        var explicita = Environment.GetEnvironmentVariable(VariableDeCadena);
        if (!string.IsNullOrWhiteSpace(explicita))
        {
            return explicita.Trim();
        }

        var servidor = Environment.GetEnvironmentVariable("PNMC_PRUEBAS_SQLSERVER_SERVIDOR") ?? "127.0.0.1,14344";
        var usuario = Environment.GetEnvironmentVariable("PNMC_PRUEBAS_SQLSERVER_USUARIO") ?? "sa";
        var clave = Environment.GetEnvironmentVariable("PNMC_LOCAL_SA_PASSWORD") ?? "PnmcLocal_2026!";

        return $"Server={servidor};Database=master;User Id={usuario};Password={clave};"
            + "TrustServerCertificate=True;Encrypt=False;Connect Timeout=15";
    }

    /// <summary>
    /// Cambia la base de una cadena de conexion sin tocar el resto. Si la cadena no
    /// nombraba ninguna base —posible cuando alguien la pasa por
    /// <see cref="VariableDeCadena"/>— se anade, porque quedarse con la base por omision
    /// del inicio de sesion significaria correr las pruebas sobre lo que hubiera alli.
    /// </summary>
    public static string CadenaHacia(string cadenaMaestra, string nombreBase)
    {
        ExigirNombreDesechable(nombreBase);

        const string ClaveDeBase = @"(?i)\b(Database|Initial Catalog)\s*=\s*[^;]*";

        if (Regex.IsMatch(cadenaMaestra, ClaveDeBase))
        {
            return Regex.Replace(cadenaMaestra, ClaveDeBase, $"Database={nombreBase}");
        }

        var recortada = cadenaMaestra.TrimEnd();
        var separador = recortada.EndsWith(';') ? string.Empty : ";";
        return $"{recortada}{separador}Database={nombreBase}";
    }

    /// <summary>
    /// Rechaza cualquier nombre que no sea una base desechable de esta via. Se llama
    /// antes de CREATE y antes de DROP: el nombre se interpola en DDL (no admite
    /// parametros), asi que esta comprobacion es a la vez la defensa contra inyeccion
    /// y el seguro que impide borrar PNMC_LOCAL.
    /// </summary>
    public static void ExigirNombreDesechable(string nombreBase)
    {
        if (string.IsNullOrWhiteSpace(nombreBase)
            || !NombreAceptado.IsMatch(nombreBase)
            || string.Equals(nombreBase, BaseProhibida, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Nombre de base rechazado: '{nombreBase}'. Solo se admiten bases desechables "
                + $"con el prefijo '{PrefijoBaseDesechable}' y caracteres [A-Za-z0-9_].");
        }
    }

    /// <summary>Nombre unico por corrida: prefijo + marca de tiempo + azar.</summary>
    public static string NuevoNombreDeBase()
    {
        var marca = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var azar = Guid.NewGuid().ToString("N")[..8];
        return $"{PrefijoBaseDesechable}{marca}_{azar}";
    }
}

/// <summary>
/// <c>[Fact]</c> de la via de SQL Server. Se omite sola cuando la via esta apagada,
/// de modo que un portatil sin Docker sigue viendo la suite entera en verde.
/// <para>
/// La omision se decide al DESCUBRIR la prueba, no al ejecutarla: xunit 2.x no sabe
/// omitir en tiempo de ejecucion, y una prueba que decide saltarse desde dentro
/// termina pasando en verde sin haber probado nada, que es justo lo que este carril
/// trata de evitar.
/// </para>
/// </summary>
public sealed class HechoSqlServerAttribute : FactAttribute
{
    public HechoSqlServerAttribute()
    {
        if (!ArnesSqlServer.Habilitado)
        {
            Skip = ArnesSqlServer.MotivoDeOmision;
        }
    }
}
