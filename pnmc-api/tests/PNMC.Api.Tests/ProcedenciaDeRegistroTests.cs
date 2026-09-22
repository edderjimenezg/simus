using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

using PNMC.Domain.Entities;

namespace PNMC.Api.Tests;

/// <summary>
/// De dónde viene cada registro: contexto, entidad que lo aportó y usuario que lo creó.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que la procedencia se confunda con la organización responsable
/// —un Festival registrado por el PNMC para la Fundación X sigue siendo de la Fundación X—; que un
/// registro nazca sin dejar rastro de quién lo incorporó; y que anotar dos veces reviente contra el
/// índice único en vez de no hacer nada.
/// </para>
/// </summary>
public sealed class ProcedenciaDeRegistroTests
{
    [Fact]
    public async Task Un_evento_creado_en_la_consola_queda_como_registro_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync("/api/v1/institucional/agenda", new
        {
            titulo = "Encuentro con procedencia",
            descripcion = "Evento creado desde la consola institucional.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var procedencia = creado.GetProperty("procedencia");

        Assert.Equal("administrativo", procedencia.GetProperty("contextoOrigen").GetString());
        Assert.True(procedencia.GetProperty("esInstitucional").GetBoolean());
        // EL USUARIO CONCRETO, NO UN NOMBRE FIJO: la cuenta de prueba es una cualquiera, y lo que
        // se comprueba es que se guarde la que tenía la sesión.
        Assert.False(string.IsNullOrWhiteSpace(procedencia.GetProperty("usuarioCreadorNombre").GetString()));
    }

    [Fact]
    public async Task La_noticia_y_la_ficha_editorial_tambien_la_guardan()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var noticia = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Una noticia con procedencia",
            resumen = "Creada desde la consola.",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var ficha = await (await client.PostAsJsonAsync("/api/v1/institucional/catalogo-editorial", new
        {
            codigo = "CE-PROC-001",
            titulo = "Cartilla con procedencia",
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("administrativo", noticia.GetProperty("procedencia").GetProperty("contextoOrigen").GetString());
        Assert.Equal("administrativo", ficha.GetProperty("procedencia").GetProperty("contextoOrigen").GetString());
    }

    [Fact]
    public async Task La_procedencia_viaja_tambien_en_el_listado()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Noticia listada",
            resumen = "Para comprobar el listado.",
        });

        var pagina = await (await client.GetAsync("/api/v1/institucional/noticias")).Content.ReadFromJsonAsync<JsonElement>();
        var primera = pagina.GetProperty("items")[0];

        // SI SOLO VIAJARA EN LA FICHA, la tabla tendría que pedir una ficha por fila para poder
        // pintar el sello, que es exactamente lo que el resolutor por página evita.
        Assert.Equal("administrativo", primera.GetProperty("procedencia").GetProperty("contextoOrigen").GetString());
    }

    [Fact]
    public async Task La_procedencia_no_se_reescribe()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Noticia estable",
            resumen = "Su procedencia no cambia al guardarla.",
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        var original = creada.GetProperty("procedencia").GetProperty("fechaRegistro").GetString();

        await client.PutAsJsonAsync($"/api/v1/institucional/noticias/{id}", new
        {
            titulo = "Noticia estable, corregida",
            resumen = "Su procedencia no cambia al guardarla.",
            version = creada.GetProperty("version").GetInt32(),
        });

        var despues = await (await client.GetAsync($"/api/v1/institucional/noticias/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        // DE DONDE VINO, NO DE DONDE ESTA. Reescribirla la convertiría en un dato que ya cuenta la
        // organización responsable, y se perdería el único rastro de quién la incorporó.
        Assert.Equal(original, despues.GetProperty("procedencia").GetProperty("fechaRegistro").GetString());
    }

    [Fact]
    public async Task Anotar_dos_veces_el_mismo_registro_no_revienta()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        await PNMC.Api.Endpoints.ProcedenciaDeRegistro.AnotarAsync(
            db, Modulos.Festivales, "4242", "externo", null, null, default);
        await db.SaveChangesAsync();

        await PNMC.Api.Endpoints.ProcedenciaDeRegistro.AnotarAsync(
            db, Modulos.Festivales, "4242", "administrativo", null, null, default);
        await db.SaveChangesAsync();

        // EL INDICE UNICO LO RECHAZARIA CON UN ERROR ILEGIBLE. Aquí simplemente no se anota la
        // segunda vez, que es lo que significa «de dónde vino».
        var filas = await db.ProcedenciasDeRegistro.AsNoTracking()
            .Where(x => x.ModuloId == Modulos.Festivales && x.RegistroId == "4242").ToListAsync();

        Assert.Single(filas);
        Assert.Equal("externo", filas[0].ContextoOrigen);
    }

    [Fact]
    public async Task Un_dominio_desconocido_se_rechaza_al_anotar()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // SIN ESTO, un dominio mal escrito en un sitio produciría fichas cuya procedencia «no
        // consta» sin que nada lo señalara.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PNMC.Api.Endpoints.ProcedenciaDeRegistro.AnotarAsync(
                db, "lo-que-sea", "1", "administrativo", null, null, default));
    }

    [Fact]
    public async Task El_acervo_historico_tiene_su_propio_contexto()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // NO ES `administrativo`. Un Festival del volcado histórico no lo incorporó el Programa
        // desde su consola: llamarlo institucional le atribuiría un acto que no consta.
        await PNMC.Api.Endpoints.ProcedenciaDeRegistro.AnotarAsync(
            db, Modulos.Festivales, "9001", PNMC.Api.Endpoints.ProcedenciaDeRegistro.Historico, null, null, default);
        await db.SaveChangesAsync();

        var procedencia = await PNMC.Api.Endpoints.ProcedenciaDeRegistro.LeerAsync(db, Modulos.Festivales, "9001", default);

        Assert.NotNull(procedencia);
        Assert.Equal("historico", procedencia!.ContextoOrigen);
        Assert.Equal("Acervo histórico", procedencia.ContextoEtiqueta);
        // NO CUENTA COMO INSTITUCIONAL: el sello de la consola no debe decir que lo puso el
        // Programa cuando lo único que se sabe es que es anterior al sistema.
        Assert.False(procedencia.EsInstitucional);
    }

    [Fact]
    public async Task Un_contexto_inventado_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // LOS CONTEXTOS SON UNA LISTA CERRADA, y la base la respalda con su propia CHECK. Sin la
        // comprobación aquí, el error llegaría como un 500 que no dice qué palabra estaba mal.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PNMC.Api.Endpoints.ProcedenciaDeRegistro.AnotarAsync(
                db, Modulos.Festivales, "9002", "lo-que-sea", null, null, default));
    }
}
