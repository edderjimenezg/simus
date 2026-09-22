using System.Runtime.CompilerServices;

namespace PNMC.Api.Tests;

/// <summary>
/// Localiza carpetas del repositorio desde una prueba, sin depender de dónde acabe el binario.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. Varias pruebas necesitan leer ficheros del repositorio —los guiones de
/// <c>pnmc-database/schema</c>, el contrato <c>openapi.yaml</c>— y todas resolvían la ruta subiendo
/// desde <see cref="AppContext.BaseDirectory"/> hasta encontrar la carpeta. Eso funciona con la
/// salida de compilación por omisión, que cuelga del propio árbol… y <b>no funciona en cuanto
/// alguien la redirige</b>:
/// </para>
/// <code>
/// dotnet test -p:BaseOutputPath=&lt;temporal&gt;/
/// </code>
/// <para>
/// que es justo como conviene ejecutar cuando hay varias sesiones sobre el mismo árbol, para no
/// pelearse por los DLL con un API en marcha. Desde ese temporal <b>no hay ningún repositorio por
/// encima</b>, así que el barrido nunca encuentra nada y la prueba muere con una excepción que no
/// menciona lo que estaba midiendo.
/// </para>
/// <para>
/// LO PEOR NO ES QUE FALLE, ES CÓMO FALLA. Una prueba que no puede pasar nunca en ese modo tampoco
/// puede <b>demostrar su mutante</b>: se siembra el defecto, sale rojo, y el rojo parece la prueba
/// funcionando cuando en realidad lo produce la localización. Un mutante que muere por el motivo
/// equivocado no demuestra nada — lo detectó la sesión par sembrando un
/// mutante mío en su modo de ejecución.
/// </para>
/// <para>
/// LA SOLUCIÓN, que ya usaba <see cref="EsquemaSinAliadosTests"/> por haber tropezado antes: pedir
/// al <b>compilador</b> la ruta del fichero fuente con <see cref="CallerFilePathAttribute"/>. Se
/// resuelve al compilar y apunta al árbol de fuentes pase lo que pase con la carpeta de salida. Se
/// conserva el barrido como respaldo para el caso en que las fuentes no estén donde se compilaron.
/// </para>
/// </remarks>
public static class RutasDelRepositorio
{
    /// <summary>Carpeta de guiones de esquema (<c>pnmc-database/schema</c>).</summary>
    public static string Esquema([CallerFilePath] string origen = "") => Carpeta("schema", origen);

    /// <summary>Carpeta de semillas (<c>pnmc-database/seed</c>).</summary>
    public static string Semillas([CallerFilePath] string origen = "") => Carpeta("seed", origen);

    /// <summary>
    /// Raíz del repositorio, para las pruebas que leen ficheros de fuera de <c>pnmc-database</c>
    /// (por ejemplo <c>scripts/seed-local-db.sh</c>).
    /// </summary>
    /// <remarks>
    /// Se resuelve como el padre de <c>pnmc-database</c>, y no con un barrido propio, para que
    /// herede las tres estrategias de <see cref="Carpeta"/> —variable de entorno, fuentes,
    /// binario— en vez de tener una cuarta que se desincronice.
    /// </remarks>
    public static string Raiz([CallerFilePath] string origen = "") =>
        Directory.GetParent(Carpeta("schema", origen))!.Parent!.FullName;

    /// <summary>
    /// Una carpeta de <c>pnmc-database</c>, resuelta desde las fuentes y no desde el binario.
    /// </summary>
    /// <param name="nombre">Nombre de la carpeta bajo <c>pnmc-database</c>.</param>
    /// <param name="origen">Lo rellena el compilador; no se pasa a mano.</param>
    public static string Carpeta(string nombre, [CallerFilePath] string origen = "")
    {
        // 1. La variable de entorno manda: es el escape de quien tenga el repo en otro sitio.
        var indicada = Environment.GetEnvironmentVariable(ArnesSqlServer.VariableDeEsquema);
        if (nombre == "schema" && !string.IsNullOrWhiteSpace(indicada) && Directory.Exists(indicada))
        {
            return indicada;
        }

        // 2. Desde las fuentes: este fichero vive en pnmc-api/tests/PNMC.Api.Tests/, asi que
        //    tres niveles arriba esta la raiz del repositorio.
        if (!string.IsNullOrWhiteSpace(origen))
        {
            var desdeFuentes = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(origen)!, "..", "..", "..", "pnmc-database", nombre));
            if (Directory.Exists(desdeFuentes))
            {
                return desdeFuentes;
            }
        }

        // 3. Respaldo: subir desde el binario, por si las fuentes no estan donde se compilaron.
        for (var actual = new DirectoryInfo(AppContext.BaseDirectory); actual is not null; actual = actual.Parent)
        {
            var candidato = Path.Combine(actual.FullName, "pnmc-database", nombre);
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new InvalidOperationException(
            $"No se encontro 'pnmc-database/{nombre}'. Se busco desde las fuentes ({origen}) y "
            + $"subiendo desde {AppContext.BaseDirectory}. Define {ArnesSqlServer.VariableDeEsquema} si el repo esta en otro sitio.");
    }
}
