using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El historial de UN registro concreto, y que la bitácora diga qué se tocó, no solo su número.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que el historial de una noticia enseñe también las actuaciones
/// sobre el evento que comparte identificador; que la Agenda no aparezca en su propia pestaña de
/// auditoría; y que la bitácora diga «alguien publicó el 7» sin decir qué es el 7.
/// </para>
/// </summary>
public sealed class AuditoriaPorRegistroTests
{
    private const string Auditoria = "/api/v1/admin/auditoria";

    [Fact]
    public async Task El_historial_se_pide_por_tabla_y_registro_a_la_vez()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var noticia = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Noticia con historial",
            resumen = "Para comprobar el historial por registro.",
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = noticia.GetProperty("id").GetInt64();

        var evento = await (await client.PostAsJsonAsync("/api/v1/institucional/agenda", new
        {
            titulo = "Evento con historial",
            descripcion = "Comparte identificador con la noticia.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var historial = await (await client.GetAsync($"{Auditoria}?tabla=Noticias&registroId={id}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // SIN EL FILTRO DE TABLA, el historial de la noticia 1 enseñaría también el del evento 1.
        foreach (var linea in historial.GetProperty("items").EnumerateArray())
        {
            Assert.Equal("Noticias", linea.GetProperty("tabla").GetString());
        }
        Assert.True(evento.GetProperty("id").GetInt64() > 0);
    }

    [Fact]
    public async Task La_agenda_aparece_en_su_propia_pestana_de_auditoria()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/institucional/agenda", new
        {
            titulo = "Evento clasificado",
            descripcion = "Su línea de bitácora tiene que caer en el grupo de Agenda.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
        });

        var porGrupo = await (await client.GetAsync($"{Auditoria}?grupo=agenda"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // LA AGENDA ESCRIBE «EventosAgenda» y la clasificación solo conocía «Agenda»: todas sus
        // líneas caían en «otros» y esta pestaña salía vacía.
        Assert.True(porGrupo.GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task La_bitacora_dice_que_registro_se_toco_y_no_solo_su_numero()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Una noticia con nombre propio",
            resumen = "Su título tiene que salir en la auditoría.",
        });

        var historial = await (await client.GetAsync($"{Auditoria}?grupo=noticias"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var primera = historial.GetProperty("items")[0];

        Assert.Equal("Una noticia con nombre propio", primera.GetProperty("nombreRegistro").GetString());
    }
}
