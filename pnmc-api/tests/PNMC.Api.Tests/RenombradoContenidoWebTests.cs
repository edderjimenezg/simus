using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El traslado de las claves <c>simus_*</c> a <c>ecosistema_*</c>.
/// </summary>
/// <remarks>
/// <para>
/// Lo que se vigila aquí no es el renombrado —eso es una concatenación— sino su
/// criterio: qué se descarta y qué se conserva. Equivocarse hacia un lado deja
/// el texto viejo publicado para siempre bajo un nombre nuevo; equivocarse
/// hacia el otro borra sin aviso lo que escribió una editora. Por eso las dos
/// mitades se prueban por separado.
/// </para>
/// </remarks>
public sealed class RenombradoContenidoWebTests
{
    /// <summary>
    /// Una base vacía, sin sembrar.
    /// </summary>
    /// <remarks>
    /// No se reutiliza <see cref="TestWebApplicationFactory"/> a propósito: esa
    /// arranca el bootstrapper, que siembra las 315 claves del catálogo. Con
    /// <c>ecosistema_*</c> ya presente, cada traslado caería en la rama de
    /// «destino ocupado» y las pruebas medirían otra cosa que la que dicen medir.
    /// </remarks>
    private static PnmcDbContext NuevaBase()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"pnmc-renombrado-{Guid.NewGuid():N}.db");
        var opciones = new DbContextOptionsBuilder<PnmcDbContext>()
            .UseSqlite($"Data Source={ruta}")
            .Options;

        var db = new PnmcDbContext(opciones);
        db.Database.EnsureCreated();
        return db;
    }

    private static ContenidoWebRow Fila(string clave, string borrador, Action<ContenidoWebRow>? ajustar = null)
    {
        var fila = new ContenidoWebRow
        {
            Key = clave,
            GroupId = "simus_hero",
            GroupLabel = "SIMUS - Encabezado",
            Section = "SIMUS",
            Label = "SIMUS - Título",
            CharacterLimit = 300,
            Draft = borrador,
            Published = null,
            Version = 1,
            UpdatedBy = "Sistema",
            UpdatedAt = DateTime.UtcNow,
        };
        ajustar?.Invoke(fila);
        return fila;
    }

    [Fact]
    public async Task Una_Fila_Intacta_Se_Descarta_Para_Que_El_Sembrador_La_Rehaga()
    {
        // La mitad que evita el texto zombi: si esta fila se trasladara, el
        // panel seguiría diciendo «SIMUS organiza información…» bajo la clave
        // nueva, y nadie sabría que ese texto es el viejo.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_about_p1", "SIMUS organiza información sobre actores…"));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key.StartsWith("simus_")).ToListAsync());
        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key == "ecosistema_about_p1").ToListAsync());
    }

    [Fact]
    public async Task Una_Fila_Publicada_Conserva_Su_Texto_Bajo_La_Clave_Nueva()
    {
        // La mitad que evita la pérdida: publicar es una decisión institucional
        // y no se deshace desde un arranque de servicio.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_hero_desc", "Borrador de la editora", fila =>
        {
            fila.Published = "Texto publicado por la editora";
            fila.Version = 4;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var trasladada = await db.ContenidoWeb.SingleAsync(f => f.Key == "ecosistema_hero_desc");
        Assert.Equal("Texto publicado por la editora", trasladada.Published);
        Assert.Equal("Borrador de la editora", trasladada.Draft);
        Assert.Equal(4, trasladada.Version);
        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key.StartsWith("simus_")).ToListAsync());
    }

    [Fact]
    public async Task Un_Borrador_Guardado_Sin_Publicar_Tambien_Se_Conserva()
    {
        // `Published` nulo no significa «intacta»: alguien pudo guardar sin
        // publicar. Si el criterio mirara solo esa columna, ese trabajo se
        // perdería en el siguiente arranque sin dejar rastro.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_join_desc", "Redacción a medias que no quiero perder", fila =>
        {
            fila.Version = 2;
            fila.UpdatedBy = "gestor@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var trasladada = await db.ContenidoWeb.SingleAsync(f => f.Key == "ecosistema_join_desc");
        Assert.Equal("Redacción a medias que no quiero perder", trasladada.Draft);
    }

    [Fact]
    public async Task El_Historial_Viaja_Con_La_Clave()
    {
        // Sin esto, «Restaurar» en el panel muestra una lista vacía para un
        // texto que sí tiene pasado: el historial quedaría archivado bajo un
        // nombre que ya no existe.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_map_cta", "Explorar", fila =>
        {
            fila.Version = 3;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
        {
            Key = "simus_map_cta",
            Action = "publicado",
            Value = "Explorar vista integrada",
            User = "editora@mincultura.gov.co",
            At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Empty(await db.HistorialDeContenidoWeb.Where(e => e.Key == "simus_map_cta").ToListAsync());
        var entrada = await db.HistorialDeContenidoWeb.SingleAsync(e => e.Key == "ecosistema_map_cta");
        Assert.Equal("Explorar vista integrada", entrada.Value);
    }

    [Fact]
    public async Task Correr_Dos_Veces_No_Cambia_Nada()
    {
        // Corre en cada arranque. Si no fuera idempotente, el segundo arranque
        // encontraría la clave nueva ocupada y el comportamiento dependería del
        // número de reinicios.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_hero_tag", "Antetítulo propio", fila =>
        {
            fila.Version = 2;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);
        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var filas = await db.ContenidoWeb.Where(f => f.Key == "ecosistema_hero_tag").ToListAsync();
        Assert.Single(filas);
        Assert.Equal("Antetítulo propio", filas[0].Draft);
    }

    [Fact]
    public async Task Una_Clave_Sin_Equivalente_En_El_Catalogo_No_Se_Toca()
    {
        // Un `simus_*` que no corresponde a ningún `ecosistema_*` del catálogo
        // no es un renombrado: es una clave retirada. Borrarla sería decidir
        // por el equipo editorial desde un arranque de servicio.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("simus_clave_que_ya_no_existe", "Texto huérfano"));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Single(await db.ContenidoWeb.Where(f => f.Key == "simus_clave_que_ya_no_existe").ToListAsync());
    }

    [Fact]
    public async Task Una_Clave_Retirada_Sale_Del_Sitio_Pero_Conserva_Su_Borrador()
    {
        // Los dos botones del hero dejaron de ser editables. La fila no se borra:
        // el sitio deja de servirla —ninguna página la lee ya— y el borrador y el
        // historial se quedan por si alguien tiene que consultar qué decía.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("home_btn_about", "Sobre el PNMC", fila =>
        {
            fila.GroupId = "home_ctas";
            fila.Section = "Home";
            fila.Published = "Sobre el PNMC";
            fila.Version = 3;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var fila = await db.ContenidoWeb.SingleAsync(f => f.Key == "home_btn_about");
        Assert.Null(fila.Published);
        Assert.NotNull(fila.Retired);
        Assert.Equal("Sobre el PNMC", fila.Draft);
        Assert.Contains(
            await db.HistorialDeContenidoWeb.Where(e => e.Key == "home_btn_about").ToListAsync(),
            entrada => entrada.Action == "retirado");
    }

    [Fact]
    public async Task Retirar_Dos_Veces_No_Anade_Una_Segunda_Entrada_Al_Historial()
    {
        // Corre en cada arranque. Sin la guarda de `Published != null`, el
        // historial de auditoría crecería una línea por reinicio del servicio y
        // dejaría de servir para leer qué pasó.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("home_btn_ejes", "Explorar Ejes", fila =>
        {
            fila.Published = "Explorar Ejes";
            fila.Version = 2;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);
        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Single(await db.HistorialDeContenidoWeb.Where(e => e.Key == "home_btn_ejes").ToListAsync());
    }

    // ---------- Claves sueltas: nav_mapa → nav_ecosistema ----------

    /// <summary>
    /// Una clave suelta con texto propio se traslada, no se pierde.
    /// </summary>
    /// <remarks>
    /// El renombrado por prefijo y el de claves sueltas son dos métodos con dos
    /// juegos de ramas. Las siete pruebas de arriba solo ejercitan el primero,
    /// así que el segundo entró en producción sin una sola prueba: lo señalaron
    /// dos revisiones independientes el mismo día.
    /// </remarks>
    [Fact]
    public async Task Una_Clave_Suelta_Con_Texto_Propio_Se_Traslada_Con_Su_Historial()
    {
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("nav_mapa", "Ecosistema", fila =>
        {
            fila.GroupId = "general_nav_footer";
            fila.Section = "Navegación y Footer";
            fila.Published = "Ecosistema musical";
            fila.Version = 5;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        db.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
        {
            Key = "nav_mapa",
            Action = "publicado",
            Value = "Ecosistema musical",
            User = "editora@mincultura.gov.co",
            At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var trasladada = await db.ContenidoWeb.SingleAsync(f => f.Key == "nav_ecosistema");
        Assert.Equal("Ecosistema musical", trasladada.Published);
        Assert.Equal("Ecosistema", trasladada.Draft);
        Assert.Equal(5, trasladada.Version);
        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key == "nav_mapa").ToListAsync());

        // El historial viaja con ella, o «Restaurar» mostraría una lista vacía
        // para un texto que sí tiene pasado.
        Assert.Empty(await db.HistorialDeContenidoWeb.Where(e => e.Key == "nav_mapa").ToListAsync());
        Assert.Single(await db.HistorialDeContenidoWeb.Where(e => e.Key == "nav_ecosistema").ToListAsync());
    }

    [Fact]
    public async Task Una_Clave_Suelta_Intacta_Se_Descarta_Para_Que_El_Sembrador_La_Rehaga()
    {
        // Si se trasladara, el panel seguiría mostrando el texto viejo bajo la
        // clave nueva y nadie sabría que ese texto es el de antes.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("nav_mapa", "Mapa ecosistémico"));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key == "nav_mapa").ToListAsync());
        Assert.Empty(await db.ContenidoWeb.Where(f => f.Key == "nav_ecosistema").ToListAsync());
    }

    [Fact]
    public async Task Trasladar_Una_Clave_Suelta_Dos_Veces_No_Cambia_Nada()
    {
        // Corre en cada arranque: sin idempotencia, el comportamiento dependería
        // del número de reinicios.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("nav_mapa", "Ecosistema", fila =>
        {
            fila.Version = 3;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);
        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        var filas = await db.ContenidoWeb.Where(f => f.Key == "nav_ecosistema").ToListAsync();
        Assert.Single(filas);
        Assert.Equal("Ecosistema", filas[0].Draft);
    }

    [Fact]
    public async Task Si_El_Destino_Ya_Existe_No_Se_Pisa_Lo_Editado()
    {
        // La rama que salva el trabajo de una editora: si las dos claves
        // coexisten y la vieja tiene texto propio, se deja para revisión
        // manual en vez de sobrescribir.
        using var db = NuevaBase();
        db.ContenidoWeb.Add(Fila("nav_mapa", "Lo que escribió la editora", fila =>
        {
            fila.Version = 4;
            fila.UpdatedBy = "editora@mincultura.gov.co";
        }));
        db.ContenidoWeb.Add(Fila("nav_ecosistema", "Lo que sembró el sistema"));
        await db.SaveChangesAsync();

        await RenombradoContenidoWeb.EnsureRenamedAsync(db, NullLogger.Instance);

        Assert.Equal("Lo que escribió la editora",
            (await db.ContenidoWeb.SingleAsync(f => f.Key == "nav_mapa")).Draft);
        Assert.Equal("Lo que sembró el sistema",
            (await db.ContenidoWeb.SingleAsync(f => f.Key == "nav_ecosistema")).Draft);
    }

    [Fact]
    public void El_Catalogo_Declara_La_Clave_Nueva_Del_Menu_Y_No_La_Vieja()
    {
        // La otra punta del renombrado. Si alguien volviera a declarar
        // `nav_mapa`, el traslado la movería en el arranque siguiente y el panel
        // perdería el campo sin explicación.
        var catalogo = SembradorDeContenidoWeb.LoadCatalog();

        Assert.Contains(catalogo, entrada => entrada.Key == "nav_ecosistema");
        Assert.DoesNotContain(catalogo, entrada => entrada.Key == "nav_mapa");
    }

    [Fact]
    public void El_Catalogo_Ya_No_Declara_Los_Botones_Del_Hero()
    {
        // La otra punta de la retirada. Si alguien volviera a declararlos en el
        // registro, `RetirarClavesAsync` los despublicaría en cada arranque y el
        // panel ofrecería editar un texto que nunca sale al sitio.
        var catalogo = SembradorDeContenidoWeb.LoadCatalog();

        Assert.DoesNotContain(catalogo, entrada => entrada.Key == "home_btn_about");
        Assert.DoesNotContain(catalogo, entrada => entrada.Key == "home_btn_ejes");
    }

    [Fact]
    public void El_Catalogo_Ya_No_Declara_Ninguna_Clave_Simus()
    {
        // La otra punta del renombrado. Si alguien volviera a sembrar una clave
        // `simus_*`, esta migración la trasladaría en el arranque siguiente y el
        // panel perdería el campo sin explicación.
        var catalogo = SembradorDeContenidoWeb.LoadCatalog();

        Assert.DoesNotContain(catalogo, entrada => entrada.Key.StartsWith("simus_", StringComparison.Ordinal));
        Assert.Contains(catalogo, entrada => entrada.Key == "ecosistema_hero_tag");

        // `ecosistema_simus_cta` fue el destino del renombrado y esta prueba comprobaba que
        // hubiera llegado. la dirección de producto retiró de /ecosistema la
        // tarjeta que enlazaba a SIMUS, y con ella las cuatro claves del grupo
        // `ecosistema_simus_externo`; el motivo está escrito en registro-de-textos-web.ts, sobre el
        // grupo `ecosistema_explore`. Sin lector, `cms:huerfanas` las habría reportado en cada
        // ejecución. La comprobación se da vuelta: la clave no debe volver.
        //
        // El enlace a SIMUS no se fue: vive en `ENLACES_EXTERNOS.simus` y en las redirecciones
        // `/simus/*` de app.routes.ts. Lo que se retiró es el texto editable de la tarjeta.
        Assert.DoesNotContain(catalogo, entrada => entrada.Key == "ecosistema_simus_cta");
    }
}
