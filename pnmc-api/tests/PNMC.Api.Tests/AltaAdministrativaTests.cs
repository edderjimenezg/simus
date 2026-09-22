using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Dar de alta organizaciones y Festivales desde el Panel de Gestión Administrativa.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que el Programa acabe figurando como organización responsable de
/// un festival que no organiza; que una organización creada desde la consola nazca con un
/// funcionario como su administrador; y que el alta administrativa se convierta en una variante
/// paralela del modelo con reglas distintas de las del canal externo.
/// </para>
/// </summary>
public sealed class AltaAdministrativaTests
{
    private const string Organizaciones = "/api/v1/admin/organizaciones";
    private const string Festivales = "/api/v1/admin/festivales";

    private static object OrganizacionNueva(string nombre) => new
    {
        nombre,
        correoContacto = $"contacto{Guid.NewGuid():N}@fundacion.org",
        codigoDepartamentoSede = "05",
        codigoMunicipioSede = "05001",
        responsableNombre = "Ana Restrepo",
        responsableTipoDocumento = "CC",
        responsableNumeroDocumento = "1020304050",
        responsableCorreo = "ana@fundacion.org",
        responsableAutorizacionDatos = true,
    };

    private static object FestivalNuevo(string nombre, int? organizacionResponsableId = null) => new
    {
        festival = new
        {
            nombre,
            descripcion = "Festival incorporado por el Programa.",
            nivelCobertura = "municipal",
            codigoDepartamento = "05",
            codigoMunicipio = "05001",
            practicasMusicalesIds = new[] { 1 },
            territoriosSonorosIds = Array.Empty<int>(),
        },
        organizacionResponsableId,
    };

    [Fact]
    public async Task El_alta_administrativa_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PostAsJsonAsync(Organizaciones, OrganizacionNueva("Fundación sin sesión"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PostAsJsonAsync(Festivales, FestivalNuevo("Festival sin sesión"))).StatusCode);
    }

    [Fact]
    public async Task Una_organizacion_creada_en_la_consola_queda_con_procedencia_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Organizaciones, OrganizacionNueva("Fundación Cantoalegre")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(creada.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        var ficha = await (await client.GetAsync($"{Organizaciones}/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var procedencia = ficha.GetProperty("organizacion").GetProperty("procedencia");

        Assert.Equal("administrativo", procedencia.GetProperty("contextoOrigen").GetString());
        Assert.True(procedencia.GetProperty("esInstitucional").GetBoolean());
    }

    [Fact]
    public async Task Una_organizacion_creada_en_la_consola_no_queda_administrada_por_el_funcionario()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Organizaciones, OrganizacionNueva("Corporación Tambores")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(creada.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // PONER AL FUNCIONARIO COMO ADMINISTRADOR diría que el Programa gestiona la organización,
        // que es justo lo que hay que poder distinguir. La administración llega cuando la
        // organización la reclame.
        Assert.False(await db.UserEntities.AsNoTracking().AnyAsync(x => x.EntityId == id));
        var entidad = await db.EntityProfiles.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null(entidad.ResponsibleUserId);
    }

    [Fact]
    public async Task El_correo_del_responsable_es_el_suyo_y_no_el_del_funcionario()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Organizaciones, OrganizacionNueva("Asociación Marimba")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(creada.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var responsable = await db.EntidadesResponsable.AsNoTracking().SingleAsync(x => x.IdEntidad == id);

        // COPIAR EL CORREO DEL FUNCIONARIO dejaría a la organización con un contacto que no es suyo.
        Assert.Equal("ana@fundacion.org", responsable.ResponsableCorreo);
    }

    [Fact]
    public async Task Un_festival_registrado_por_el_programa_conserva_su_organizacion_responsable()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var organizacion = await (await client.PostAsJsonAsync(Organizaciones, OrganizacionNueva("Fundación Bandas del Sur")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var organizacionId = int.Parse(organizacion.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        var creado = await (await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival del Sur", organizacionId)))
            .Content.ReadFromJsonAsync<JsonElement>();
        var festivalId = creado.GetProperty("id").GetString();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.AsNoTracking()
            .SingleAsync(x => x.Id == int.Parse(festivalId!, System.Globalization.CultureInfo.InvariantCulture));
        var procedencia = await db.ProcedenciasDeRegistro.AsNoTracking()
            .SingleAsync(x => x.ModuloId == Modulos.Festivales && x.RegistroId == festivalId);

        // LAS TRES DIMENSIONES, SEPARADAS: lo incorporó el Programa, lo creó un funcionario, y lo
        // organiza la Fundación. Que el Programa lo registre no lo convierte en su responsable.
        Assert.Equal(organizacionId, festival.OrganizacionPrincipalId);
        Assert.Equal("administrativo", procedencia.ContextoOrigen);
        Assert.NotEqual(organizacionId, procedencia.OrganizacionProcedenciaId);
    }

    [Fact]
    public async Task Un_festival_puede_registrarse_sin_organizacion_responsable_todavia()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // UN FESTIVAL HISTORICO PUEDE NO TENER TODAVIA QUIEN RESPONDA POR EL, y eso no puede
        // impedir registrarlo: esperar a que alguien aparezca es como se queda sin registrar.
        var respuesta = await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival sin dueño"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_organizacion_responsable_inventada_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival huérfano", 987_654));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_alta_administrativa_aplica_las_mismas_reglas_que_el_canal_externo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // UN MUNICIPIO QUE NO PERTENECE AL DEPARTAMENTO lo rechaza la misma validación compartida.
        var respuesta = await client.PostAsJsonAsync(Festivales, new
        {
            festival = new
            {
                nombre = "Festival con territorio imposible",
                nivelCobertura = "municipal",
                codigoDepartamento = "05",
                codigoMunicipio = "99999",
                practicasMusicalesIds = Array.Empty<int>(),
                territoriosSonorosIds = Array.Empty<int>(),
            },
            organizacionResponsableId = (int?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_entidad_institucional_sembrada_el_alta_sigue_funcionando()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            foreach (var entidad in await db.EntityProfiles.Where(x => x.IsInstitutional).ToListAsync())
            {
                entidad.IsInstitutional = false;
            }
            await db.SaveChangesAsync();
        }

        var respuesta = await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival sobre base a medio sembrar"));

        // UN 503 AQUI DEJABA LA CONSOLA INSERVIBLE sobre una base recién levantada, por un dato
        // que la procedencia no necesita: el contexto «administrativo» ya dice que lo incorporó el
        // Programa, y la columna de la entidad es anulable justo para esto.
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var procedencia = await db.ProcedenciasDeRegistro.AsNoTracking()
                .OrderByDescending(x => x.Id).FirstAsync(x => x.ModuloId == Modulos.Festivales);
            Assert.Equal("administrativo", procedencia.ContextoOrigen);
            Assert.Null(procedencia.OrganizacionProcedenciaId);
        }
    }

    [Fact]
    public async Task Las_coincidencias_se_ofrecen_antes_de_registrar()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival de Música Campesina"));

        var coincidencias = await (await client.GetAsync($"{Festivales}/coincidencias?nombre=Campesina"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // SE OFRECEN, NO BLOQUEAN: dos festivales pueden llamarse parecido, y bloquear por nombre
        // produciría registros imposibles de crear.
        Assert.True(coincidencias.GetArrayLength() > 0);
    }

    [Fact]
    public async Task El_webmaster_publica_un_festival_sin_pasar_por_la_bandeja()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival que publica el Programa")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetString();

        var publicado = await client.PostAsJsonAsync($"{Festivales}/{id}/publicacion", new { estado = "publicado" });

        // UN FESTIVAL QUE EL PROPIO PROGRAMA INCORPORO NO TIENE A QUIEN ESPERAR: obligarlo a
        // recorrer una bandeja donde el revisor y el registrador son la misma persona convierte el
        // circuito en un trámite vacío.
        Assert.Equal(HttpStatusCode.OK, publicado.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.AsNoTracking()
            .SingleAsync(x => x.Id == int.Parse(id!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("publicado", festival.StatusCode);
    }

    [Fact]
    public async Task Desde_la_consola_no_se_escriben_los_estados_del_circuito_de_revision()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Festivales, FestivalNuevo("Festival con estado ajeno")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetString();

        // «aprobado» LO DECIDE LA BANDEJA. Escribirlo desde aquí dejaría el circuito de revisión
        // diciendo cosas que nadie decidió en él.
        var respuesta = await client.PostAsJsonAsync($"{Festivales}/{id}/publicacion", new { estado = "aprobado" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
