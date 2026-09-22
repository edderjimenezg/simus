using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// EL REFRESCO DE METADATOS DE <c>dbo.MediosWeb</c>, que es el camino que nunca corría.
/// <para>
/// <b>QUÉ SE ROMPIÓ, con su fecha.</b> El 30 de agosto de 2026 el arranque del API cayó entero en
/// «modo degradado» con un <c>DbUpdateConcurrencyException</c>: «expected to affect 1 row(s), but
/// actually affected 0». La causa: <c>Version</c> es token de concurrencia
/// (<c>PnmcDbContext.cs</c>), el sembrador adjuntaba una instancia mínima sin ella, y EF
/// emitía <c>WHERE IdMedioWeb = @id AND Version = 0</c>. Ninguna fila tiene versión 0.
/// </para>
/// <para>
/// <b>POR QUÉ NINGUNA PRUEBA LO VIO.</b> Todas las que tocan este sembrador arrancan contra una
/// base vacía, así que todas las ranuras entran por la rama de INSERT. La rama de UPDATE solo
/// corre cuando el catálogo compilado dice algo distinto de lo que hay guardado, y hasta ese día
/// nunca había dicho nada distinto. Esta prueba entra por esa rama a propósito.
/// </para>
/// <para>
/// <b>LO QUE SE LLEVÓ POR DELANTE</b> justifica que la prueba exista: el bootstrap aborta en el
/// primer fallo, así que con él caían también la poda del historial y cualquier paso posterior, y
/// el API seguía sirviendo —en verde a la vista— con la base a medio preparar.
/// </para>
/// </summary>
public sealed class MediosWebSeederRefrescoTests : IDisposable
{
    private readonly string ruta = Path.Combine(
        Path.GetTempPath(), $"pnmc-siembra-medios-{Guid.NewGuid():N}.db");

    private PnmcDbContext Abrir()
    {
        var opciones = new DbContextOptionsBuilder<PnmcDbContext>()
            .UseSqlite($"Data Source={ruta}")
            .Options;
        return new PnmcDbContext(opciones);
    }

    public void Dispose()
    {
        try { File.Delete(ruta); } catch (IOException) { /* el fichero temporal no importa */ }
    }

    /// <summary>La primera ranura del catálogo compilado, que es de donde salen los valores buenos.</summary>
    private static SembradorDeContenidoWeb.CatalogImage PrimeraDelCatalogo()
        => SembradorDeContenidoWeb.LoadImageCatalog()[0];

    [Fact]
    public async Task RefrescaLosMetadatosDeUnaRanuraQueYaExisteYNoDejaCaerElArranque()
    {
        var esperada = PrimeraDelCatalogo();

        await using (var db = Abrir())
        {
            await db.Database.EnsureCreatedAsync();

            // La fila nace con metadatos VIEJOS y con una versión que no es cero: es el estado
            // real de una base que ya arrancó alguna vez y sobre la que alguien publicó.
            db.ImagenesWeb.Add(new ImagenWebRow
            {
                Key = esperada.Key,
                GroupId = esperada.GroupId,
                GroupLabel = esperada.GroupLabel,
                Section = esperada.Section,
                Label = esperada.Label,
                Use = esperada.Use,
                Editable = esperada.Editable,
                AltText = esperada.Alt ?? string.Empty,
                SuggestedWidth = 1600,
                SuggestedHeight = 900,
                Version = 7,
                UpdatedBy = "Sistema",
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = Abrir())
        {
            // NO LANZA. Es la mitad de la prueba: antes del arreglo, esta línea era la que tiraba
            // el arranque entero.
            await MediosWebSeeder.EnsureSeededAsync(db, NullLogger.Instance);
        }

        await using (var comprobacion = Abrir())
        {
            var fila = await comprobacion.ImagenesWeb.SingleAsync(x => x.Key == esperada.Key);

            // Y LA OTRA MITAD: que el refresco haya escrito de verdad. Un sembrador que se tragara
            // la excepción en silencio pasaría la primera mitad y dejaría el número viejo en
            // pantalla, que es el defecto que se estaba persiguiendo.
            Assert.Equal(esperada.SuggestedWidth, fila.SuggestedWidth);
            Assert.Equal(esperada.SuggestedHeight, fila.SuggestedHeight);

            // La versión NO se toca al refrescar metadatos: no es una edición de contenido, y
            // subirla haría fallar el siguiente guardado del editor por conflicto inventado.
            Assert.Equal(7, fila.Version);
        }
    }

    [Fact]
    public async Task SembrarDosVecesSeguidasNoEscribeNadaLaSegunda()
    {
        await using (var db = Abrir())
        {
            await db.Database.EnsureCreatedAsync();
            await MediosWebSeeder.EnsureSeededAsync(db, NullLogger.Instance);
        }

        int cuantas;
        DateTime marca;
        await using (var db = Abrir())
        {
            cuantas = await db.ImagenesWeb.CountAsync();
            marca = await db.ImagenesWeb.OrderBy(x => x.Id).Select(x => x.UpdatedAt).FirstAsync();
            await MediosWebSeeder.EnsureSeededAsync(db, NullLogger.Instance);
        }

        await using (var comprobacion = Abrir())
        {
            Assert.Equal(cuantas, await comprobacion.ImagenesWeb.CountAsync());
            Assert.Equal(
                marca,
                await comprobacion.ImagenesWeb.OrderBy(x => x.Id).Select(x => x.UpdatedAt).FirstAsync());
        }
    }
}
