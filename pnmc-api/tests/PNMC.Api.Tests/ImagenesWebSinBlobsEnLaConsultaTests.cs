using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PNMC.Api.Endpoints;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// QUE ABRIR EL PANEL NO LEA LOS BLOBS. Es la propiedad que decide si el CMS de imágenes es
/// usable o inaceptable, y por eso tiene clase propia.
/// <para>
/// <b>POR QUÉ NO BASTA MEDIR EL TAMAÑO DE LA RESPUESTA</b>, que es lo que se intentó primero. Un
/// mutante que añadía <c>PublishedContent</c> a la proyección de EF sin serializarlo dejaba la
/// respuesta exactamente igual de pequeña y la prueba en verde. El coste no está en lo que
/// vuelve al navegador: está en que SQL Server lea las páginas LOB —hasta 64 MiB por cada clic en
/// un grupo— y en que EF reserve esos arreglos. Eso no se ve desde el cuerpo de la respuesta.
/// </para>
/// <para>
/// Lo que sí lo ve es el SQL. Un <c>SELECT</c> que no nombra la columna no lee sus páginas, así
/// que la afirmación es directa: <b>ninguna orden emitida al servir esta ruta puede nombrar una
/// columna varbinary</b>. Se lee del interceptor, no del código.
/// </para>
/// <para>
/// LO QUE ESTA PRUEBA NO CUBRE: corre sobre SQLite, donde <c>varbinary(max)</c> es BLOB y no hay
/// páginas LOB. El razonamiento de por qué no nombrar la columna evita leerla es de SQL Server;
/// lo que aquí se comprueba es el hecho comprobable en los dos motores —que la columna no aparece
/// en el SQL—, que es la causa de aquello.
/// </para>
/// </summary>
public sealed class ImagenesWebSinBlobsEnLaConsultaTests : IClassFixture<FabricaDeMediosConContador>
{
    private readonly FabricaDeMediosConContador factory;

    public ImagenesWebSinBlobsEnLaConsultaTests(FabricaDeMediosConContador factory) => this.factory = factory;

    /// <summary>Las siete columnas de blob de <c>dbo.MediosWeb</c> más las del historial.</summary>
    private static readonly string[] ColumnasDeBlob =
    [
        "BorradorContenido",
        "BorradorMiniatura",
        "PublicadoContenido",
        "PublicadoMiniatura",
    ];

    [Fact]
    public async Task AbrirUnGrupoNoLeeNingunaColumnaVarbinary()
    {
        var client = await CmsTestClient.LoginAsync(factory);

        // Se llena una ranura del grupo con bytes de verdad, o la prueba estaría midiendo sobre
        // filas vacías y cualquier consulta parecería barata.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = await db.ImagenesWeb.FirstAsync(x => x.Key == "home_hero_1");
            fila.DraftContent = new byte[512 * 1024];
            fila.DraftMime = "image/png";
            fila.DraftBytes = fila.DraftContent.Length;
            fila.DraftWidth = 1600;
            fila.DraftHeight = 900;
            fila.DraftHash = new string('a', 64);
            fila.PublishedContent = fila.DraftContent;
            fila.PublishedMime = fila.DraftMime;
            fila.PublishedBytes = fila.DraftBytes;
            fila.PublishedWidth = fila.DraftWidth;
            fila.PublishedHeight = fila.DraftHeight;
            fila.PublishedHash = fila.DraftHash;
            await db.SaveChangesAsync();
        }

        // Se calienta: la primera petición autenticada paga la revalidación de sesión, y sus
        // consultas no son las de esta ruta.
        await client.GetAsync("/api/v1/admin/imagenes-web/groups/home_media");

        factory.Contador.Reiniciar();
        var respuesta = await client.GetAsync("/api/v1/admin/imagenes-web/groups/home_media");
        respuesta.EnsureSuccessStatusCode();

        var sql = string.Join("\n", factory.Contador.Textos);
        Assert.NotEmpty(factory.Contador.Textos);

        foreach (var columna in ColumnasDeBlob)
        {
            Assert.False(
                sql.Contains(columna, StringComparison.OrdinalIgnoreCase),
                $"abrir un grupo del panel leyó la columna {columna}. Con 44 ranuras a 2 MiB " +
                $"por mitad, eso son decenas de MiB por cada clic.\nSQL:\n{sql}");
        }
    }

    /// <summary>
    /// El manifiesto público es la petición más frecuente del sitio: la pide el arranque de
    /// CUALQUIER página. Tampoco puede leer un blob.
    /// </summary>
    [Fact]
    public async Task ElManifiestoPublicoNoLeeNingunaColumnaVarbinary()
    {
        var anonimo = factory.CreateClient();
        await anonimo.GetAsync("/api/v1/imagenes-web");

        factory.Contador.Reiniciar();
        var respuesta = await anonimo.GetAsync("/api/v1/imagenes-web");
        respuesta.EnsureSuccessStatusCode();

        var sql = string.Join("\n", factory.Contador.Textos);
        Assert.NotEmpty(factory.Contador.Textos);

        foreach (var columna in ColumnasDeBlob)
        {
            Assert.False(
                sql.Contains(columna, StringComparison.OrdinalIgnoreCase),
                $"el manifiesto público leyó la columna {columna}.\nSQL:\n{sql}");
        }
    }

    /// <summary>
    /// El historial tampoco. Con seis entradas por clave guardando el archivo, materializar la
    /// entidad serían hasta 12 MiB para pintar una lista de fechas.
    /// </summary>
    [Fact]
    public async Task ElHistorialNoLeeElArchivoGuardado()
    {
        var client = await CmsTestClient.LoginAsync(factory);
        await client.GetAsync("/api/v1/admin/imagenes-web/home_hero_1/history");

        factory.Contador.Reiniciar();
        var respuesta = await client.GetAsync("/api/v1/admin/imagenes-web/home_hero_1/history");
        respuesta.EnsureSuccessStatusCode();

        var sql = string.Join("\n", factory.Contador.Textos);
        Assert.NotEmpty(factory.Contador.Textos);
        Assert.False(
            sql.Contains("Contenido", StringComparison.OrdinalIgnoreCase),
            $"el historial leyó la columna Contenido.\nSQL:\n{sql}");
    }
}

/// <summary>
/// Fábrica con el interceptor de consultas enchufado.
/// <para>
/// No comparte fábrica con <see cref="ImagenesWebEndpointsTests"/> a propósito: xunit corre clases en
/// paralelo, y un contador compartido mediría también el tráfico de la clase vecina.
/// </para>
/// </summary>
public sealed class FabricaDeMediosConContador : WebApplicationFactory<Program>
{
    public ContadorDeConsultas Contador { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PnmcDbContext>>();
            services.RemoveAll<PnmcDbContext>();

            var ruta = Path.Combine(Path.GetTempPath(), $"pnmc-medios-contador-{Guid.NewGuid():N}.db");
            services.AddDbContext<PnmcDbContext>(options => options
                .UseSqlite($"Data Source={ruta}")
                .AddInterceptors(Contador));

            using var ambito = services.BuildServiceProvider().CreateScope();
            var db = ambito.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.Database.EnsureCreated();

            db.Roles.Add(new RoleRow { Id = 1, Name = "webmaster" });
            var webmaster = new UserRow
            {
                Id = 1,
                FullName = "Usuario Prueba",
                Email = CmsTestClient.WebmasterEmail,
                AccessChannel = "interno",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            webmaster.PasswordHash = AdminAuthEndpoints.HashPassword(webmaster, CmsTestClient.WebmasterPassword);
            db.Users.Add(webmaster);
            db.UsuariosRoles.Add(new UsuarioRolRow { Id = 1, UserId = 1, RoleId = 1, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        });
    }
}
