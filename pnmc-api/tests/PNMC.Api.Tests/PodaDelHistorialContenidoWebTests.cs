using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El tope del historial de contenido web.
/// </summary>
/// <remarks>
/// <para>
/// Lo que se vigila aquí no es el recorte —eso es un <c>Skip</c>— sino sus dos
/// bordes. Por un lado, que <b>borre lo que sobra</b>: si no lo hiciera, el tope
/// existiría solo en la documentación. Por otro, y más importante, que
/// <b>no borre de más</b>: estas filas no se recuperan, y una poda que se lleve
/// la última revisión deja al panel sin nada que restaurar.
/// </para>
/// </remarks>
public sealed class PodaDelHistorialContenidoWebTests
{
    private static PnmcDbContext NuevaBase()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"pnmc-poda-{Guid.NewGuid():N}.db");
        var opciones = new DbContextOptionsBuilder<PnmcDbContext>()
            .UseSqlite($"Data Source={ruta}")
            .Options;

        var db = new PnmcDbContext(opciones);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Siembra <paramref name="cuantas"/> entradas, la más vieja primero.</summary>
    private static async Task SembrarHistorialAsync(PnmcDbContext db, string clave, int cuantas)
    {
        var origen = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < cuantas; i++)
        {
            db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
            {
                Key = clave,
                Action = "guardado",
                Value = $"version {i + 1}",
                User = "editora@mincultura.gov.co",
                At = origen.AddMinutes(i),
            });
        }
        await db.SaveChangesAsync();
    }

    private static Task<List<string?>> ValoresAsync(PnmcDbContext db, string clave) =>
        db.HistorialDeContenidoWeb
            .Where(e => e.Key == clave)
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
            .Select(e => e.Value)
            .ToListAsync();

    [Fact]
    public async Task Conserva_Las_Mas_Recientes_Y_Borra_El_Resto()
    {
        const int Sembradas = 12;
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", Sembradas);

        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);

        var valores = await ValoresAsync(db, "home_tag");
        Assert.Equal(PodaDelHistorialContenidoWeb.Tope, valores.Count);
        // Se conservan las últimas, no las primeras: el orden importa tanto como
        // el número. Recortar por el otro extremo dejaría el mismo número de
        // entradas, pero todas inservibles.
        //
        // Los extremos se calculan desde el tope y no se escriben a mano: con el
        // número puesto a mano, subir el tope rompía esta prueba por una razón
        // que no era un defecto —pasó al subirlo de 5 a 6— y la señal se
        // confundía con el ruido.
        Assert.Equal($"version {Sembradas}", valores[0]);
        Assert.Equal($"version {Sembradas - PodaDelHistorialContenidoWeb.Tope + 1}", valores[^1]);
    }

    [Fact]
    public async Task Una_Clave_Por_Debajo_Del_Tope_No_Se_Toca()
    {
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_title", 3);

        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);

        Assert.Equal(3, (await ValoresAsync(db, "home_title")).Count);
    }

    [Fact]
    public async Task Poda_Cada_Clave_Por_Separado()
    {
        // El recorte es por clave y no por tabla. Si el tope se aplicara al
        // conjunto, la clave más editada se llevaría por delante el historial de
        // todas las demás.
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", 9);
        await SembrarHistorialAsync(db, "home_title", 2);

        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);

        Assert.Equal(PodaDelHistorialContenidoWeb.Tope, (await ValoresAsync(db, "home_tag")).Count);
        Assert.Equal(2, (await ValoresAsync(db, "home_title")).Count);
    }

    [Fact]
    public async Task Podar_Dos_Veces_No_Cambia_Nada()
    {
        // Corre en cada arranque. Si no fuera idempotente, cada reinicio se
        // comería una entrada más y el historial se vaciaría solo.
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", 8);

        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);
        var tras_la_primera = await ValoresAsync(db, "home_tag");
        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);

        Assert.Equal(tras_la_primera, await ValoresAsync(db, "home_tag"));
    }

    [Fact]
    public async Task Al_Escribir_Hace_Sitio_Para_La_Entrada_Nueva()
    {
        // El caso de todos los días: la clave está en el tope y llega una edición.
        // La entrada nueva tiene que entrar y la más vieja salir, quedando el
        // total en el tope y NO en el tope más uno.
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", PodaDelHistorialContenidoWeb.Tope);

        db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
        {
            Key = "home_tag",
            Action = "publicado",
            Value = "la nueva",
            User = "editora@mincultura.gov.co",
            At = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc),
        });
        await PodaDelHistorialContenidoWeb.MarcarSobrantesAsync(db, "home_tag");
        await db.SaveChangesAsync();

        var valores = await ValoresAsync(db, "home_tag");
        Assert.Equal(PodaDelHistorialContenidoWeb.Tope, valores.Count);
        Assert.Equal("la nueva", valores[0]);
        Assert.DoesNotContain("version 1", valores);
    }

    [Fact]
    public async Task Al_Escribir_No_Toca_El_Historial_De_Otras_Claves()
    {
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", PodaDelHistorialContenidoWeb.Tope + 4);
        await SembrarHistorialAsync(db, "home_title", 2);

        db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
        {
            Key = "home_tag",
            Action = "guardado",
            Value = "otra más",
            User = "editora@mincultura.gov.co",
            At = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc),
        });
        await PodaDelHistorialContenidoWeb.MarcarSobrantesAsync(db, "home_tag");
        await db.SaveChangesAsync();

        Assert.Equal(2, (await ValoresAsync(db, "home_title")).Count);
    }

    [Fact]
    public async Task Cuenta_Las_Entradas_Pendientes_Y_No_Supone_Que_Es_Una()
    {
        // Una petición que escriba dos veces la misma clave —hoy no ocurre, pero
        // el import y el guardado por grupo comparten este camino— conservaría
        // una de más si la poda diera por hecho que solo se añade una entrada.
        using var db = NuevaBase();
        await SembrarHistorialAsync(db, "home_tag", PodaDelHistorialContenidoWeb.Tope);

        foreach (var texto in new[] { "primera nueva", "segunda nueva" })
        {
            db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
            {
                Key = "home_tag",
                Action = "guardado",
                Value = texto,
                User = "editora@mincultura.gov.co",
                At = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc),
            });
        }
        await PodaDelHistorialContenidoWeb.MarcarSobrantesAsync(db, "home_tag");
        await db.SaveChangesAsync();

        Assert.Equal(PodaDelHistorialContenidoWeb.Tope, (await ValoresAsync(db, "home_tag")).Count);
    }

    [Fact]
    public async Task La_Poda_No_Toca_El_Texto_Ni_El_Borrador()
    {
        // La propiedad que hace que esto sea aceptable: se pierde el rastro de
        // ediciones viejas, nunca el contenido. Si esta prueba fallara, podar
        // sería borrar trabajo del equipo editorial.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(new ContenidoWebRow
        {
            Key = "home_tag",
            GroupId = "home_hero",
            GroupLabel = "Encabezado Principal (Hero)",
            Section = "Home",
            Label = "Etiqueta superior",
            CharacterLimit = 100,
            Draft = "borrador vivo",
            Published = "texto publicado",
            Version = 9,
            UpdatedBy = "editora@mincultura.gov.co",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await SembrarHistorialAsync(db, "home_tag", 20);

        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);

        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(f => f.Key == "home_tag");
        Assert.Equal("borrador vivo", fila.Draft);
        Assert.Equal("texto publicado", fila.Published);
        Assert.Equal(9, fila.Version);
    }

    [Fact]
    public async Task Una_Base_Sin_Historial_No_Falla()
    {
        using var db = NuevaBase();
        await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, NullLogger.Instance);
        Assert.Empty(await db.HistorialDeContenidoWeb.ToListAsync());
    }
}
