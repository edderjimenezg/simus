using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

using PNMC.Domain.Entities;

namespace PNMC.Api.Tests;

/// <summary>
/// Devolver un proceso bloqueado a custodia institucional.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que un Festival se quede congelado para siempre porque nadie de su
/// organización puede entrar; que se le quite la administración a alguien sin que conste por qué; y
/// que la liberación se pierda del historial de administración, dejando un registro que enseña
/// quién lo reclamó después pero no cómo llegó a estar libre.
/// </para>
/// </summary>
public sealed class LiberacionDeAdministracionTests
{
    private const string Festivales = "/api/v1/admin/festivales";

    [Fact]
    public async Task Liberar_exige_decir_por_que()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (festivalId, _) = await FestivalConDuenoAsync(factory, consola);

        var respuesta = await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new { motivo = "" });

        // ES LA DECISION MAS FUERTE DE LA PANTALLA: dentro de un año nadie recordará por qué.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_proceso_vuelve_al_programa_y_queda_reclamable()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (festivalId, organizacionId) = await FestivalConDuenoAsync(factory, consola, publicado: true);

        var respuesta = await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new
        {
            motivo = "La fundación se disolvió y nadie puede entrar a su cuenta.",
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        // SOLO ES RECLAMABLE SI ESTA PUBLICADO: es la regla del circuito, y la respuesta lo dice
        // para que la pantalla no prometa que ya lo puede pedir alguien.
        Assert.True(cuerpo.GetProperty("reclamable").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var institucional = await db.EntityProfiles.AsNoTracking().Where(x => x.IsInstitutional).Select(x => x.Id).FirstAsync();
        var festival = await db.FestivalRecords.AsNoTracking().SingleAsync(x => x.Id == festivalId);

        Assert.Equal(institucional, festival.OrganizacionPrincipalId);
        Assert.NotEqual(organizacionId, festival.OrganizacionPrincipalId);
    }

    [Fact]
    public async Task La_liberacion_queda_en_el_historial_de_administracion()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (festivalId, organizacionId) = await FestivalConDuenoAsync(factory, consola);

        await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new
        {
            motivo = "Nadie responde desde marzo.",
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var transferencia = await db.AdministrationTransfers.AsNoTracking()
            .SingleAsync(x => x.ModuloId == Modulos.Festivales && x.CanonicalRecordId == festivalId.ToString());

        // SIN ESTA FILA, el registro enseñaría quién lo reclamó después y no cómo llegó a estar
        // libre. Y no tiene reclamación detrás: no la pidió nadie, la decidió el Programa.
        Assert.Null(transferencia.ClaimId);
        Assert.Equal(organizacionId, transferencia.PreviousOrganizationId);
        Assert.Contains("Nadie responde desde marzo.", transferencia.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_procedencia_no_se_reescribe_al_liberar()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (festivalId, _) = await FestivalConDuenoAsync(factory, consola);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var antes = await db.ProcedenciasDeRegistro.AsNoTracking()
                .SingleAsync(x => x.ModuloId == Modulos.Festivales && x.RegistroId == festivalId.ToString());
            Assert.Equal("administrativo", antes.ContextoOrigen);
        }

        await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new { motivo = "Se liberó." });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var despues = await db.ProcedenciasDeRegistro.AsNoTracking()
                .SingleAsync(x => x.ModuloId == Modulos.Festivales && x.RegistroId == festivalId.ToString());

            // QUIEN INCORPORO EL FESTIVAL es un hecho del pasado. Lo que cambia al liberar es quién
            // responde por él ahora: son las dos dimensiones que el corte anterior separó.
            Assert.Equal("administrativo", despues.ContextoOrigen);
        }
    }

    [Fact]
    public async Task Un_proceso_que_ya_esta_en_custodia_no_se_libera_dos_veces()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var (festivalId, _) = await FestivalConDuenoAsync(factory, consola);

        await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new { motivo = "Primera vez." });
        var segunda = await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/liberar-administracion", new { motivo = "Otra vez." });

        // LIBERARLO OTRA VEZ no cambiaría nada y escribiría una transferencia de la institución a
        // sí misma.
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    // ---------- Andamio ----------------------------------------------------------

    /// <summary>Un Festival con una organización responsable que no es la institucional.</summary>
    private static async Task<(int FestivalId, int OrganizacionId)> FestivalConDuenoAsync(
        TestWebApplicationFactory factory, HttpClient consola, bool publicado = false)
    {
        var organizacion = await (await consola.PostAsJsonAsync("/api/v1/admin/organizaciones", new
        {
            nombre = "Fundación con proceso " + Guid.NewGuid().ToString("N")[..6],
            correoContacto = $"{Guid.NewGuid():N}@fundacion.test",
            codigoDepartamentoSede = "05",
            codigoMunicipioSede = "05001",
            responsableNombre = "Ana Restrepo",
            responsableAutorizacionDatos = true,
        })).Content.ReadFromJsonAsync<JsonElement>();
        var organizacionId = int.Parse(organizacion.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        var festival = await (await consola.PostAsJsonAsync(Festivales, new
        {
            festival = new
            {
                nombre = "Festival bloqueado " + Guid.NewGuid().ToString("N")[..6],
                nivelCobertura = "nacional",
                practicasMusicalesIds = Array.Empty<int>(),
                territoriosSonorosIds = Array.Empty<int>(),
            },
            organizacionResponsableId = organizacionId,
        })).Content.ReadFromJsonAsync<JsonElement>();
        var festivalId = int.Parse(festival.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        if (publicado)
        {
            await consola.PostAsJsonAsync($"{Festivales}/{festivalId}/publicacion", new { estado = "publicado" });
        }

        return (festivalId, organizacionId);
    }
}
