using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Las operaciones que todos los puntos de entrada comparten se escriben una sola vez.
/// </summary>
/// <remarks>
/// <para>
/// LA REGLA. La auditoría encontró que comprobar el testigo de una
/// petición estaba escrito <b>catorce veces</b>, bajo seis nombres distintos —
/// <c>ValidarAntiforgeryAsync</c>, <c>Csrf</c>, <c>TestigoValido</c>, <c>TestigoValidoAsync</c>,
/// <c>EsPeticionValidaAsync</c> e <c>IsValidAntiforgeryRequestAsync</c>—, y que normalizar un
/// correo estaba escrito <b>diez veces</b>, en dos idiomas y con <b>tres comportamientos</b>: unas
/// devolvían nulo ante un valor en blanco, otras cadena vacía y otra además validaba el largo.
/// </para>
/// <para>
/// POR QUE UNA PRUEBA Y NO SOLO LA CORRECCION. Juntarlas sin dejar red dura hasta el siguiente punto
/// de entrada que alguien escriba: copiar cuatro líneas es más rápido que buscar dónde viven, y así
/// fue como llegaron a catorce. Lo que se fija aquí es que solo pueda haber una.
/// </para>
/// </remarks>
public sealed class AyudantesCompartidosTests
{
    [Fact]
    public void El_testigo_de_una_peticion_se_comprueba_en_un_solo_sitio()
    {
        var infractores = new List<string>();
        // Quien captura esa excepción está comprobando el testigo. La única que puede hacerlo es la
        // clase compartida; las demás delegan.
        foreach (var (nombre, contenido) in LeerFuentes())
        {
            if (nombre == "TestigoDePeticion.cs") continue;
            if (!contenido.Contains("catch (AntiforgeryValidationException", StringComparison.Ordinal)) continue;

            // `GuardasDelCms` y la normalización de versiones contestan con su propio mensaje
            // después de delegar la comprobación: eso es respuesta, no lógica repetida.
            if (contenido.Contains("TestigoDePeticion.ValidoAsync", StringComparison.Ordinal)) continue;

            infractores.Add(nombre);
        }

        Assert.True(
            infractores.Count == 0,
            "Estos ficheros vuelven a comprobar el testigo por su cuenta en vez de usar "
            + "`TestigoDePeticion`. Llegaron a ser catorce copias bajo seis nombres:\n  "
            + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Un_correo_se_normaliza_en_un_solo_sitio()
    {
        var infractores = new List<string>();
        // La forma canónica: recortar y bajar a minúsculas. Quien la escriba está duplicando.
        var forma = new Regex(@"\.Trim\(\)\s*\.ToLowerInvariant\(\)", RegexOptions.Compiled);

        foreach (var (nombre, contenido) in LeerFuentes())
        {
            if (nombre == "CorreoElectronico.cs") continue;
            foreach (Match m in forma.Matches(contenido))
            {
                // Solo cuenta cuando lo que se normaliza es un correo: el mismo recorte se usa para
                // niveles de cobertura o tipos de documento, y eso no es esta operación.
                var desde = Math.Max(0, m.Index - 120);
                var alrededor = contenido[desde..m.Index];
                if (alrededor.Contains("orreo", StringComparison.OrdinalIgnoreCase)
                    || alrededor.Contains("mail", StringComparison.OrdinalIgnoreCase))
                {
                    infractores.Add(nombre);
                    break;
                }
            }
        }

        Assert.True(
            infractores.Count == 0,
            "Estos ficheros normalizan un correo por su cuenta en vez de usar `CorreoElectronico`. "
            + "Llegaron a ser diez copias con tres comportamientos distintos, y que un correo en "
            + "blanco se guarde como NULL o como cadena vacía según por dónde entró es la clase de "
            + "diferencia que nadie ve hasta que una consulta cuenta mal:\n  "
            + string.Join("\n  ", infractores));
    }

    /// <summary>El control positivo: sin esto, los dos asertos pasarían con el barrido roto.</summary>
    [Fact]
    public void El_barrido_lee_las_fuentes_de_verdad()
    {
        var fuentes = LeerFuentes().ToList();

        Assert.True(fuentes.Count >= 100, $"Solo se leyeron {fuentes.Count} ficheros y el API tiene más de cien.");
        Assert.Contains(fuentes, f => f.Nombre == "TestigoDePeticion.cs");
        Assert.Contains(fuentes, f => f.Nombre == "CorreoElectronico.cs");
        Assert.Contains(fuentes, f => f.Contenido.Contains("TestigoDePeticion.ValidoAsync", StringComparison.Ordinal));
    }

    // ---------- Andamio ----------------------------------------------------------

    private static IEnumerable<(string Nombre, string Contenido)> LeerFuentes([CallerFilePath] string origen = "")
    {
        var raiz = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(origen)!, "..", "..", "src"));
        Assert.True(Directory.Exists(raiz), $"No se encontró {raiz}.");

        foreach (var ruta in Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories).OrderBy(r => r, StringComparer.Ordinal))
        {
            if (ruta.Contains("/obj/", StringComparison.Ordinal) || ruta.Contains("/bin/", StringComparison.Ordinal)) continue;
            yield return (Path.GetFileName(ruta), SinComentarios(File.ReadAllText(ruta)));
        }
    }

    /// <summary>
    /// Quita los comentarios: se prohíbe escribir la copia, no contar que existió.
    /// </summary>
    private static string SinComentarios(string fuente)
    {
        var sinBloques = Regex.Replace(fuente, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(sinBloques, "//[^\r\n]*", " ");
    }
}
