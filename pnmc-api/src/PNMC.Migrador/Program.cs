using System.Globalization;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Data.SqlClient;

namespace PNMC.Migrador;

/// <summary>
/// Aplica los guiones de <c>pnmc-database/schema/</c> a una base, en orden y una sola vez cada uno,
/// dejando constancia en la propia base de cuáles ya corrieron.
/// </summary>
/// <remarks>
/// <para>
/// POR QUÉ EXISTE. Hasta el esquema se aplicaba por dos caminos y ninguno
/// llevaba la cuenta de nada:
/// </para>
/// <list type="number">
///   <item><description>
///     <c>scripts/seed-local-db.sh</c>, con una <b>lista escrita a mano</b>. Su propio comentario
///     confiesa el fallo: <c>V20260822_01</c> pasó un día sin figurar en ella, de modo que el
///     guion construía bases sin el tipo <c>agrupacion</c> mientras las pruebas —que recorren el
///     directorio— sí lo aplicaban. Una lista que hay que acordarse de actualizar se queda atrás;
///     la pregunta no es si pasará, sino cuándo.
///   </description></item>
///   <item><description>
///     El antiguo arranque del API (<c>DatabaseBootstrapper</c>), que creaba tablas al levantar.
///     Ese segundo camino se retiró: desde el corte cero DbUp es la única autoridad de esquema.
///   </description></item>
/// </list>
/// <para>
/// Y ninguno de los dos sabía <b>qué se había aplicado ya</b>. Sin historial, la única defensa era
/// que cada guion fuese idempotente —lo son, con <c>IF NOT EXISTS</c>—, lo que funciona hasta el
/// primer guion que no pueda serlo (una corrección de datos, un <c>DROP</c>, un <c>UPDATE</c>
/// masivo). Peor: sin historial nada impide que un árbol atrasado aplique guiones viejos sobre una
/// base nueva, que es exactamente lo que ocurrió el 24 de agosto y devolvió a la vida un modelo de
/// datos ya retirado.
/// </para>
/// <para>
/// DbUp <b>descubre</b> los guiones del directorio (no hay lista que
/// mantener), los ordena por nombre —la convención <c>V<i>fecha</i>_<i>nn</i>__<i>titulo</i></c> ya
/// ordena bien— y anota cada uno en la tabla <c>SchemaVersions</c> al aplicarlo. Un guion que ya
/// figura ahí no se vuelve a ejecutar nunca, sea idempotente o no.
/// </para>
/// <para>
/// El API no ejecuta DDL: antes de iniciarlo se debe ejecutar <c>migrar</c>. La base conserva en
/// <c>dbo.SchemaVersions</c> el historial que permite saber exactamente qué guiones se aplicaron.
/// </para>
/// </remarks>
public static class Program
{
    private const string TablaDeHistorial = "SchemaVersions";

    public static int Main(string[] argumentos)
    {
        var orden = argumentos.FirstOrDefault()?.ToLowerInvariant() ?? "ayuda";
        var cadena = CadenaDeConexion(argumentos);

        if (orden is "ayuda" or "--help" or "-h")
        {
            Ayuda();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(cadena))
        {
            Error("No hay cadena de conexion. Pasa --conexion \"...\" o define PNMC_MIGRADOR_CONEXION.");
            return 2;
        }

        string directorio;
        try
        {
            directorio = LocalizarEsquema();
        }
        catch (DirectoryNotFoundException fallo)
        {
            Error(fallo.Message);
            return 2;
        }

        var constructor = DeployChanges.To
            .SqlDatabase(cadena)
            .WithScriptsFromFileSystem(directorio)
            .JournalToSqlTable("dbo", TablaDeHistorial)
            .LogTo(new RegistroEnConsola())
            .WithTransactionPerScript();

        var motor = constructor.Build();

        return orden switch
        {
            "estado" => Estado(motor, directorio),
            "migrar" => Migrar(motor),
            "baseline" => Baseline(motor, directorio),
            "divipola-mgn-2025" => ImportarDivipolaMgn2025(cadena),
            "divipola-mgn-2025-si-falta" => ImportarDivipolaMgn2025SiFalta(cadena),
            _ => OrdenDesconocida(orden),
        };
    }

    /// <summary>Qué guiones faltan por aplicar, sin tocar la base más que para leer el historial.</summary>
    private static int Estado(UpgradeEngine motor, string directorio)
    {
        Console.WriteLine($"Guiones en {directorio}");
        var pendientes = motor.GetScriptsToExecute();
        if (pendientes.Count == 0)
        {
            Console.WriteLine("Al dia: no hay guiones pendientes.");
            return 0;
        }

        Console.WriteLine($"Pendientes ({pendientes.Count}):");
        foreach (var guion in pendientes)
        {
            Console.WriteLine("  - " + guion.Name);
        }

        // Codigo 1 a proposito: sirve para que un paso de CI pueda preguntar «¿esta al dia?» y
        // fallar si no lo esta, sin tener que interpretar la salida de texto.
        return 1;
    }

    private static int Migrar(UpgradeEngine motor)
    {
        var resultado = motor.PerformUpgrade();
        if (resultado.Successful)
        {
            Console.WriteLine($"Migracion correcta: {resultado.Scripts.Count()} guion(es) aplicado(s).");
            return 0;
        }

        Error(resultado.Error?.Message ?? "fallo desconocido");
        return 1;
    }

    /// <summary>
    /// Marca como aplicados los guiones que ya están dentro de la base, <b>sin ejecutarlos</b>.
    /// </summary>
    /// <remarks>
    /// Es el paso que se corre UNA vez sobre una base que ya existía antes del migrador —la de
    /// desarrollo de hoy, y la de producción el día que la haya—. Sin él, el primer
    /// <c>migrar</c> intentaría aplicar los doce guiones sobre una base que ya los tiene: hoy
    /// sobrevivirían porque son idempotentes, pero apoyarse en eso es apoyarse en una propiedad
    /// que el próximo guion puede no tener.
    /// </remarks>
    private static int Baseline(UpgradeEngine motor, string directorio)
    {
        var pendientes = motor.GetScriptsToExecute();
        if (pendientes.Count == 0)
        {
            Console.WriteLine("No hay nada que marcar: el historial ya cubre todos los guiones.");
            return 0;
        }

        Console.WriteLine($"Se marcaran como aplicados, SIN ejecutarlos, {pendientes.Count} guion(es) de {directorio}:");
        foreach (var guion in pendientes)
        {
            Console.WriteLine("  - " + guion.Name);
        }

        motor.MarkAsExecuted();
        Console.WriteLine("Historial inicializado.");
        return 0;
    }

    private static int OrdenDesconocida(string orden)
    {
        Error($"Orden desconocida: '{orden}'.");
        Ayuda();
        return 2;
    }

    private static void Ayuda()
    {
        Console.WriteLine("""
            Migrador de esquema de PNMC (DbUp).

              dotnet run --project pnmc-api/src/PNMC.Migrador -- <orden> [--conexion "..."]

            Ordenes:
              estado     Lista los guiones pendientes. Sale con 1 si hay alguno (util en CI).
              migrar     Aplica los pendientes, en orden, y los anota en dbo.SchemaVersions.
              baseline   Marca los pendientes como aplicados SIN ejecutarlos. Solo la primera vez,
                         sobre una base que ya tenia el esquema antes de existir el migrador.
              divipola-mgn-2025
                         Descarga y concilia el catálogo DIVIPOLA MGN 2025 desde el DANE. Requiere
                         haber aplicado primero el esquema pendiente; no borra referencias locales.
              divipola-mgn-2025-si-falta
                         Descarga DIVIPOLA únicamente si el catálogo local está vacío.

            La cadena de conexion sale de --conexion o de la variable PNMC_MIGRADOR_CONEXION.
            Los guiones se leen de pnmc-database/schema, que se localiza subiendo desde el
            ejecutable; con PNMC_MIGRADOR_ESQUEMA se puede indicar otra carpeta.
            """);
    }

    private static int ImportarDivipolaMgn2025(string cadena)
    {
        try
        {
            using var conexion = new SqlConnection(cadena);
            conexion.Open();
            ImportadorDivipolaMgn2025.ImportarAsync(conexion).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error)
        {
            Error(error.Message);
            return 1;
        }
    }

    private static int ImportarDivipolaMgn2025SiFalta(string cadena)
    {
        try
        {
            using var conexion = new SqlConnection(cadena);
            conexion.Open();
            using var consulta = new SqlCommand("SELECT COUNT_BIG(1) FROM dbo.Divipola", conexion);
            var existentes = Convert.ToInt64(consulta.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (existentes > 0)
            {
                Console.WriteLine($"DIVIPOLA ya contiene {existentes} fila(s); no se reemplaza automáticamente.");
                return 0;
            }
            ImportadorDivipolaMgn2025.ImportarAsync(conexion).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error)
        {
            Error(error.Message);
            return 1;
        }
    }

    private static string? CadenaDeConexion(string[] argumentos)
    {
        var indice = Array.FindIndex(argumentos, a => a.Equals("--conexion", StringComparison.OrdinalIgnoreCase));
        if (indice >= 0 && indice + 1 < argumentos.Length)
        {
            return argumentos[indice + 1];
        }

        return Environment.GetEnvironmentVariable("PNMC_MIGRADOR_CONEXION");
    }

    /// <summary>
    /// Encuentra <c>pnmc-database/schema</c> subiendo desde el ejecutable.
    /// </summary>
    /// <remarks>
    /// Mismo patron que <c>ParidadEsquemaSinArranqueTests</c>, y por el mismo motivo: la ruta
    /// relativa depende de desde donde se lance, y una ruta fija se rompe en cuanto alguien mueve
    /// el proyecto o lo publica.
    /// </remarks>
    private static string LocalizarEsquema()
    {
        var indicada = Environment.GetEnvironmentVariable("PNMC_MIGRADOR_ESQUEMA");
        if (!string.IsNullOrWhiteSpace(indicada))
        {
            if (!Directory.Exists(indicada))
            {
                throw new DirectoryNotFoundException($"PNMC_MIGRADOR_ESQUEMA apunta a '{indicada}', que no existe.");
            }

            return indicada;
        }

        for (var actual = new DirectoryInfo(AppContext.BaseDirectory); actual is not null; actual = actual.Parent)
        {
            var candidato = Path.Combine(actual.FullName, "pnmc-database", "schema");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new DirectoryNotFoundException(
            $"No se encontro 'pnmc-database/schema' subiendo desde {AppContext.BaseDirectory}; usa PNMC_MIGRADOR_ESQUEMA.");
    }

    private static void Error(string mensaje)
    {
        var antes = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("ERROR: " + mensaje);
        Console.ForegroundColor = antes;
    }

    /// <summary>Salida de DbUp a la consola, sin colores por nivel para no ensuciar los registros de CI.</summary>
    private sealed class RegistroEnConsola : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) => Escribir("traza", format, args);
        public void LogDebug(string format, params object[] args) => Escribir("detalle", format, args);
        public void LogInformation(string format, params object[] args) => Escribir("info", format, args);
        public void LogWarning(string format, params object[] args) => Escribir("aviso", format, args);
        public void LogError(string format, params object[] args) => Escribir("error", format, args);
        public void LogError(Exception ex, string format, params object[] args) => Escribir("error", format + " :: " + ex.Message, args);

        private static void Escribir(string nivel, string formato, params object[] argumentos)
        {
            var texto = argumentos.Length == 0 ? formato : string.Format(CultureInfo.InvariantCulture, formato, argumentos);
            Console.WriteLine($"[{nivel}] {texto}");
        }
    }
}
