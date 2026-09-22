using PNMC.Api.Endpoints;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El mes y la duración de una Edición, que la historia de usuario pide autocalculados.
/// </summary>
/// <remarks>
/// <para>
/// <b>LA TABLA DE CASOS ES LA MISMA QUE LA DEL FRONTEND</b>, en
/// <c>tiempo-de-la-edicion.spec.ts</c>. La regla está escrita dos veces a propósito —el formulario
/// la necesita EN VIVO mientras se teclean las fechas, y el API la sirve a quien lee la edición— y
/// lo único que impide que las dos se separen es que estén fijadas por los mismos casos. Si aquí se
/// añade uno, allí también.
/// </para>
/// <para>
/// NINGUNO DE LOS DOS SE GUARDA: un derivado almacenado es un derivado que se desincroniza.
/// </para>
/// </remarks>
public sealed class TiempoDeLaEdicionTests
{
    public static TheoryData<string, string?, string?, string?, int?> Casos() => new()
    {
        { "un día dura 1, no 0",                  "2026-03-01", "2026-03-01", "Marzo", 1 },
        { "cuatro días seguidos",                 "2026-09-01", "2026-09-04", "Septiembre", 4 },
        { "cruza de mes y los nombra los dos",    "2026-01-30", "2026-02-02", "Enero, Febrero", 4 },
        { "cruza tres meses",                     "2026-01-15", "2026-03-02", "Enero, Febrero, Marzo", 47 },
        { "cruza de año",                         "2026-12-30", "2027-01-02", "Diciembre, Enero", 4 },
        { "solo fecha de inicio",                 "2026-07-10", null, "Julio", null },
        { "solo fecha de fin",                    null, "2026-11-05", "Noviembre", null },
        { "sin fechas",                           null, null, null, null },
        { "fin anterior al inicio no inventa nada", "2026-05-10", "2026-05-01", null, null },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void CalculaMesYDuracion(string titulo, string? inicio, string? fin, string? mesEsperado, int? diasEsperados)
    {
        var (mes, dias) = TiempoDeLaEdicion.De(Leer(inicio), Leer(fin));

        Assert.Equal(mesEsperado, mes);
        Assert.Equal(diasEsperados, dias);
        Assert.NotNull(titulo);
    }

    private static DateOnly? Leer(string? texto) =>
        DateOnly.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, out var fecha) ? fecha : null;
}
