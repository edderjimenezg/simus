using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.ImportacionAsistida;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class RetencionImportacionesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RetencionImportacionesTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Una_previsualizacion_de_mas_de_treinta_dias_pierde_detalle_pero_conserva_cabecera()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var usuarioId = await db.Users.Select(usuario => usuario.Id).FirstAsync();
        var ahora = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        var lote = Lote(usuarioId, "previsualizado", ahora.AddDays(-31));
        db.LotesImportacion.Add(lote);
        await db.SaveChangesAsync();
        var fila = Fila(lote.Id);
        db.FilasImportacion.Add(fila);
        await db.SaveChangesAsync();
        db.HallazgosImportacion.Add(new HallazgoImportacionRow
        {
            FilaImportacionId = fila.Id,
            Severidad = "informacion",
            Codigo = "prueba",
            Mensaje = "Hallazgo temporal",
        });
        await db.SaveChangesAsync();

        var resultado = await new DepuradorImportaciones(db).EjecutarAsync(ahora, 30);
        db.ChangeTracker.Clear();

        var conservado = await db.LotesImportacion.SingleAsync(item => item.Id == lote.Id);
        Assert.Equal(1, resultado.LotesExpirados);
        Assert.Equal("expirado", conservado.Estado);
        Assert.Equal(ahora, conservado.FechaDepuracion);
        Assert.False(await db.FilasImportacion.AnyAsync(item => item.LoteImportacionId == lote.Id));
        Assert.False(await db.HallazgosImportacion.AnyAsync(item => item.FilaImportacionId == fila.Id));
        Assert.Contains(await db.AuditLogs.Where(item => item.RecordId == lote.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync(),
            item => item.NewValuesJson!.Contains("PrevisualizacionImportacionExpirada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_lote_aplicado_retira_la_copia_de_contacto_y_conserva_su_evidencia()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var usuarioId = await db.Users.Select(usuario => usuario.Id).FirstAsync();
        var ahora = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        var lote = Lote(usuarioId, "aplicado", ahora.AddDays(-1));
        lote.FechaAplicacion = ahora.AddDays(-1);
        lote.ClaveIdempotencia = $"retencion-{Guid.NewGuid():N}";
        lote.FilasAplicadas = 1;
        db.LotesImportacion.Add(lote);
        await db.SaveChangesAsync();
        var fila = Fila(lote.Id);
        db.FilasImportacion.Add(fila);
        await db.SaveChangesAsync();

        var resultado = await new DepuradorImportaciones(db).EjecutarAsync(ahora, 30);
        db.ChangeTracker.Clear();

        var conservado = await db.LotesImportacion.SingleAsync(item => item.Id == lote.Id);
        var filaConservada = await db.FilasImportacion.SingleAsync(item => item.Id == fila.Id);
        var datos = JsonSerializer.Deserialize<DatosDeFestival>(filaConservada.ContenidoNormalizadoJson);
        Assert.Equal(1, resultado.LotesAplicadosMinimizados);
        Assert.Equal("aplicado", conservado.Estado);
        Assert.Equal(ahora, conservado.FechaDepuracion);
        Assert.NotNull(datos);
        Assert.Null(datos.CorreoContacto);
        Assert.Null(datos.TelefonoContacto);
        Assert.Equal("https://example.org", datos.SitioWeb);
        Assert.Equal("Festival de prueba", datos.Nombre);
    }

    private static LoteImportacionRow Lote(int usuarioId, string estado, DateTime fecha) => new()
    {
        ModuloId = Modulos.Festivales,
        NombreArchivo = $"retencion-{Guid.NewGuid():N}.csv",
        Formato = "csv",
        HuellaArchivo = new string('a', 64),
        HuellaPlan = new string('b', 64),
        VersionContrato = 1,
        Estado = estado,
        UsuarioId = usuarioId,
        FechaPrevisualizacion = fecha,
        TotalFilas = 1,
        FilasImportables = 1,
        FilasRechazadas = 0,
    };

    private static FilaImportacionRow Fila(long loteId) => new()
    {
        LoteImportacionId = loteId,
        NumeroFila = 2,
        Resultado = "crear",
        PuedeImportarse = true,
        ContenidoNormalizadoJson = JsonSerializer.Serialize(new DatosDeFestival(
            "Festival de prueba", "Descripción", "nacional", null, null, null, null,
            "anual", null, "persona@example.org", "+573001234567",
            "https://example.org", null, null, null)),
    };
}
