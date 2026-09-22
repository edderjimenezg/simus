using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Un guion de esquema que compara metadatos del catálogo con datos declara su intercalación.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO QUE ESTO CIERRA, Y POR QUÉ NO SE VE EN LOCAL. Azure SQL guarda los metadatos del
/// catálogo —<c>sys.objects.name</c> y compañía— en <c>SQL_Latin1_General_CP1_CI_AS</c>
/// <b>sin importar la intercalación de la base</b>. Medido contra la base
/// de pruebas, que <c>infra/main.bicep</c> crea con <c>Modern_Spanish_CI_AI</c>:
/// </para>
/// <code>
/// base de datos               Modern_Spanish_CI_AI
/// sys.objects.name            SQL_Latin1_General_CP1_CI_AS
/// variable tabla (sysname)    Modern_Spanish_CI_AI
/// </code>
/// <para>
/// Comparar las dos sin coaccionar da el error 468, «Cannot resolve the collation conflict». En el
/// contenedor local no pasa: ahí la base se crea sin intercalación explícita y hereda la del
/// servidor, así que las dos coinciden y el guion funciona. Por eso <b>esta prueba no puede ser de
/// comportamiento</b>: ninguna base local reproduce la separación entre catálogo y datos que hace
/// Azure. Lo que sí se puede vigilar es la forma del guion.
/// </para>
/// <para>
/// LO QUE COSTÓ. La migración se detuvo con 10 de los 22 guiones aplicados y la base a medio hacer.
/// DbUp usa una transacción por guion, no una para todo: cada guion aplicado queda aplicado.
/// </para>
/// <para>
/// LO QUE ESTA PRUEBA NO CUBRE, dicho para que nadie la lea de más. Solo mira las dos formas que
/// producen el fallo —comparar contra una variable escalar y contra una variable tabla o tabla
/// temporal—, y solo sobre las columnas de texto del catálogo. Comparar contra un literal
/// (<c>o.name = N'Festivales'</c>) es correcto y no se marca: el literal adopta la intercalación de
/// la columna. Tampoco entiende SQL: reconoce texto. Un guion que compare de otra forma pasa sin
/// que nadie se entere.
/// </para>
/// </remarks>
public sealed class IntercalacionDelCatalogoTests
{
    /// <summary>Columnas de texto de las vistas del catálogo. Las demás son enteros y no aplican.</summary>
    private const string ColumnasDeTexto = "name|type_desc|definition";

    /// <summary>
    /// Comparación contra una variable escalar: <c>alias.name = @algo</c>.
    /// </summary>
    private static readonly Regex ContraVariable = new(
        $@"\b(?<alias>\w+)\.(?<columna>{ColumnasDeTexto})\s*(?<collate>COLLATE\s+\w+\s*)?(?:=|<>|!=)\s*@\w+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Comparación contra una variable tabla o una tabla temporal:
    /// <c>alias.name IN (SELECT nombre FROM @tabla)</c>.
    /// </summary>
    private static readonly Regex ContraTabla = new(
        $@"\b(?<alias>\w+)\.(?<columna>{ColumnasDeTexto})\s*(?<collate>COLLATE\s+\w+\s*)?(?:NOT\s+)?IN\s*\(\s*SELECT[^)]*?FROM\s+[@#]\w+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Alias ligados a una vista del catálogo: <c>FROM sys.objects padre</c>.</summary>
    private static readonly Regex AliasDelCatalogo = new(
        @"\b(?:FROM|JOIN)\s+sys\.\w+\s+(?:AS\s+)?(?<alias>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<(string Fichero, string Texto)> Guiones()
    {
        var carpeta = Path.Combine(RutasDelRepositorio.Raiz(), "pnmc-database", "schema");
        return Directory.GetFiles(carpeta, "*.sql")
            .OrderBy(ruta => ruta, StringComparer.Ordinal)
            .Select(ruta => (Path.GetFileName(ruta), File.ReadAllText(ruta)))
            .ToList();
    }

    private static IEnumerable<(string Fichero, string Fragmento, bool TieneCollate)> Comparaciones()
    {
        foreach (var (fichero, texto) in Guiones())
        {
            var alias = AliasDelCatalogo.Matches(texto)
                .Select(coincidencia => coincidencia.Groups["alias"].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (alias.Count == 0)
            {
                continue;
            }

            foreach (var patron in new[] { ContraVariable, ContraTabla })
            {
                foreach (Match coincidencia in patron.Matches(texto))
                {
                    if (!alias.Contains(coincidencia.Groups["alias"].Value))
                    {
                        continue;
                    }

                    var fragmento = coincidencia.Value.Length > 120
                        ? coincidencia.Value[..120]
                        : coincidencia.Value;

                    yield return (fichero, fragmento.ReplaceLineEndings(" "), coincidencia.Groups["collate"].Success);
                }
            }
        }
    }

    [Fact]
    public void Toda_Comparacion_Entre_Catalogo_Y_Dato_Declara_Su_Intercalacion()
    {
        var sinCollate = Comparaciones()
            .Where(comparacion => !comparacion.TieneCollate)
            .Select(comparacion => $"{comparacion.Fichero}: {comparacion.Fragmento}")
            .ToList();

        Assert.True(
            sinCollate.Count == 0,
            "Estas comparaciones enfrentan metadatos del catalogo con un dato y no declaran " +
            "intercalacion. En Azure SQL eso es el error 468 y detiene la migracion a la mitad. " +
            "Se arregla coaccionando el lado del CATALOGO con COLLATE DATABASE_DEFAULT:" +
            Environment.NewLine + string.Join(Environment.NewLine, sinCollate));
    }

    [Fact]
    public void El_Barrido_Encuentra_Algo_Que_Mirar()
    {
        // EL CANARIO, Y NO ES DECORATIVO. La prueba de arriba pasa sola si el barrido no encuentra
        // ninguna comparacion: cero infracciones de cero casos examinados es verde y no mide nada.
        // Hoy hay exactamente una, en V20260823_01__retirada_aliados.sql. Si alguien reescribe ese
        // guion o cambia los patrones de arriba, esto se cae y obliga a mirar si el barrido sigue
        // sirviendo, en vez de dejarlo pasar en verde para siempre.
        var encontradas = Comparaciones().ToList();

        Assert.NotEmpty(encontradas);
    }
}
