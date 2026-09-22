using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La ficha de un mercado en la consola, y las tres decisiones de publicación que se toman en ella.
/// </summary>
/// <remarks>
/// <b>ES LA PAREJA DE LA FICHA DEL FESTIVAL, Y NO EXISTIA.</b> Un mercado solo se abría en el cajón
/// de edición de su panel: no había forma de verlo entero ni de publicarlo, retirarlo o archivarlo
/// desde su ficha. Lo que estas pruebas fijan es lo que hace que las dos fichas se recorran igual:
/// que la lectura traiga quién responde, de dónde vino y sus realizaciones; que solo se admitan los
/// tres estados que decide la consola; y que publicar sea del webmaster.
/// </remarks>
public sealed class FichaAdministrativaDeMercadoTests
{
    /// <summary>Un mercado creado desde la consola, con una edición suya.</summary>
    private static async Task<int> MercadoInstitucionalAsync(TestWebApplicationFactory factory, HttpClient consola)
    {
        var organizacion = await OrganizacionAsync(factory);
        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado Musical de la Consola",
            Descripcion = "Incorporado por el Programa.",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            CorreoMercado = "contacto@mercadoconsola.test",
            OrganizacionId = organizacion,
        });
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        return int.Parse(mercado!.Id.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static async Task<int> OrganizacionAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organización responsable del mercado",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.EntityProfiles.Add(fila);
        await db.SaveChangesAsync();

        // CON UNA PERSONA QUE LA ADMINISTRE, porque el aviso al retirar el mercado va a las cuentas
        // activas de la organización: sin ninguna no habría a quién avisar y la prueba mediría el
        // caso vacío en vez del mensaje.
        var persona = new UserRow
        {
            FullName = "Responsable de la organización",
            Email = $"responsable.{Guid.NewGuid():N}@organizacion.test",
            PasswordHash = "x",
            AccessChannel = "externo",
            IsActive = true,
            CorreoConfirmado = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(persona);
        await db.SaveChangesAsync();

        db.UserEntities.Add(new UserEntityRow
        {
            UserId = persona.Id,
            EntityId = fila.Id,
            EntityRole = "administrador",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return fila.Id;
    }

    [Fact]
    public async Task La_ficha_dice_que_es_el_mercado_quien_responde_y_de_donde_vino()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        var respuesta = await consola.GetAsync($"/api/v1/admin/mercados/{mercadoId}");
        respuesta.EnsureSuccessStatusCode();
        var ficha = await respuesta.Content.ReadFromJsonAsync<FichaMercadoAdministrativaDto>();

        Assert.NotNull(ficha);
        Assert.Equal("Mercado Musical de la Consola", ficha!.Nombre);
        Assert.Equal("borrador", ficha.Estado);
        Assert.NotNull(ficha.OrganizacionResponsableId);
        Assert.Equal("Organización responsable del mercado", ficha.OrganizacionResponsableNombre);

        // LA PROCEDENCIA ES DEL PROGRAMA Y LA ORGANIZACION RESPONSABLE ES OTRA: que el PNMC lo haya
        // incorporado no lo convierte en quien lo gestiona. Mercados nació sin anotar procedencia y
        // era el único proceso del Ecosistema que no podía responder de dónde vino.
        Assert.NotNull(ficha.Procedencia);
        Assert.True(ficha.Procedencia!.EsInstitucional);
        Assert.Empty(ficha.Ediciones);
    }

    [Fact]
    public async Task Desde_la_consola_un_mercado_se_publica_se_retira_y_se_archiva_y_nada_mas()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        var publicado = await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" });
        publicado.EnsureSuccessStatusCode();
        Assert.Equal("publicado", (await consola.GetFromJsonAsync<FichaMercadoAdministrativaDto>($"/api/v1/admin/mercados/{mercadoId}"))!.Estado);

        var retirado = await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "borrador" });
        retirado.EnsureSuccessStatusCode();
        Assert.Equal("borrador", (await consola.GetFromJsonAsync<FichaMercadoAdministrativaDto>($"/api/v1/admin/mercados/{mercadoId}"))!.Estado);

        // MUTANTE QUE MATA: admitir cualquier estado. Escribir «en_revision» desde aquí dejaría la
        // bandeja esperando una decisión que nadie tomó en ella.
        var inventado = await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "en_revision" });
        Assert.Equal(HttpStatusCode.BadRequest, inventado.StatusCode);
    }

    [Fact]
    public async Task La_fecha_de_publicacion_dice_cuando_salio_la_primera_vez_y_no_se_borra_al_retirarlo()
    {
        // Es un hecho, no el estado de hoy: retirar un mercado del portal no deshace que estuvo.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" });

        DateTime? primera;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            primera = await db.Mercados.AsNoTracking().Where(m => m.Id == mercadoId).Select(m => m.FechaPublicacion).FirstAsync();
        }
        Assert.NotNull(primera);

        await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "borrador" });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var despues = await db.Mercados.AsNoTracking().Where(m => m.Id == mercadoId).Select(m => m.FechaPublicacion).FirstAsync();
            Assert.Equal(primera, despues);
        }
    }

    [Fact]
    public async Task La_publicacion_desde_la_consola_se_lee_en_el_historial_de_la_organizacion()
    {
        // LOS DOS LADOS DEL CIRCUITO ESCRIBEN EN LA MISMA BITACORA. Si la consola publicara con una
        // forma distinta a la del resto del circuito, el movimiento llegaría a la ficha de la
        // organización sin estados y sin nombre, como una actualización cualquiera.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        var fila = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == "Mercados" && a.RecordId == registro && a.Action == "publicar")
            .OrderByDescending(a => a.CreatedAt)
            .FirstAsync();

        Assert.Contains("\"evento\":\"decidir\"", fila.NewValuesJson, StringComparison.Ordinal);
        Assert.Contains("\"estado\":\"publicado\"", fila.NewValuesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retirar_del_ecosistema_archiva_con_motivo_y_avisa_a_la_organizacion()
    {
        // ES UN ARCHIVADO, NO UN BORRADO: el registro conserva su historial y su auditoría, y deja
        // de verse en el portal. Y la organización se entera, con el motivo escrito.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" });

        var retiro = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/archivar",
            new ArchivarMercadoSolicitud { Motivo = "Duplicado del mercado del Amazonas." });
        retiro.EnsureSuccessStatusCode();

        var ficha = await consola.GetFromJsonAsync<FichaMercadoAdministrativaDto>($"/api/v1/admin/mercados/{mercadoId}");
        Assert.Equal("archivado", ficha!.Estado);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);

        // El registro sigue ahí: archivar no es borrar.
        Assert.True(await db.Mercados.AsNoTracking().AnyAsync(m => m.Id == mercadoId && m.Activo));

        // EL AVISO DICE LO QUE PASO, y no lo que pasaba antes. El aviso era binario —publicado o
        // ajustes—, así que sobre un mercado archivado habría dicho que le pedían ajustes.
        var aviso = await db.Notifications.AsNoTracking()
            .Where(n => n.ModuloId == "mercados" && n.RecordId == registro)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(aviso);
        Assert.Equal("MercadoArchivado", aviso!.EventType);
        Assert.Contains("Duplicado del mercado del Amazonas.", aviso.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_motivo_no_se_retira_nada_y_sobre_un_borrador_tampoco()
    {
        // LAS DOS MITADES DE LA MISMA REGLA. Sin motivo, dentro de un año nadie recordará por qué
        // se retiró; y retirar del portal algo que nunca estuvo en el portal no es una decisión.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var mercadoId = await MercadoInstitucionalAsync(factory, consola);

        var enBorrador = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/archivar",
            new ArchivarMercadoSolicitud { Motivo = "Da igual: todavía no está publicado." });
        Assert.Equal(HttpStatusCode.Conflict, enBorrador.StatusCode);

        await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercadoId}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "publicado" });

        var sinMotivo = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercadoId}/archivar",
            new ArchivarMercadoSolicitud { Motivo = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var ficha = await consola.GetFromJsonAsync<FichaMercadoAdministrativaDto>($"/api/v1/admin/mercados/{mercadoId}");
        Assert.Equal("publicado", ficha!.Estado);
    }

    [Fact]
    public async Task Al_registrar_se_avisa_de_los_mercados_que_ya_se_llaman_parecido()
    {
        // SE PREGUNTA ANTES DE CREAR, NO DESPUES DE DUPLICAR. Dos registros del mismo mercado se
        // quedan los dos en el portal, cada uno con parte de las ediciones, y separarlos después
        // obliga a decidir cuál es el bueno y a mover lo que cuelga del otro.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        await MercadoInstitucionalAsync(factory, consola);

        // DESDE TRES LETRAS: con una o dos la coincidencia sería con medio catálogo.
        var corto = await consola.GetFromJsonAsync<CoincidenciaDeAltaDto[]>("/api/v1/admin/mercados/coincidencias?nombre=Me");
        Assert.Empty(corto!);

        var coincidencias = await consola.GetFromJsonAsync<CoincidenciaDeAltaDto[]>(
            "/api/v1/admin/mercados/coincidencias?nombre=Musical de la Consola");
        var encontrado = Assert.Single(coincidencias!);
        Assert.Equal("Mercado Musical de la Consola", encontrado.Nombre);
        // CON QUE COMPARAR: quién responde y en qué estado está, que es lo que decide si es el mismo.
        Assert.Equal("Organización responsable del mercado", encontrado.OrganizacionResponsable);
        Assert.Equal("borrador", encontrado.Estado);

        var ninguna = await consola.GetFromJsonAsync<CoincidenciaDeAltaDto[]>(
            "/api/v1/admin/mercados/coincidencias?nombre=Un nombre que no existe");
        Assert.Empty(ninguna!);
    }

    [Fact]
    public async Task Un_mercado_que_no_existe_no_tiene_ficha()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);

        var respuesta = await consola.GetAsync("/api/v1/admin/mercados/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
    [Fact]
    public async Task La_Periodicidad_De_Un_Mercado_Es_Un_Vocabulario_Cerrado_Y_No_Texto_Libre()
    {
        // MUTANTE QUE MATA: dejar pasar cualquier texto, que es lo que hacía hasta el 17 de
        // septiembre de 2026. El cajón de la consola pedía la periodicidad con un campo libre y el
        // servidor no la comprobaba, así que el mismo dato entraba escrito de cuatro maneras.
        await using var factory = new TestWebApplicationFactory();
        var consola = await CmsTestClient.LoginAsync(factory);
        var organizacion = await OrganizacionAsync(factory);

        var inventada = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado con periodicidad inventada",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            OrganizacionId = organizacion,
            Periodicidad = "permanente",
        });
        Assert.Equal(HttpStatusCode.BadRequest, inventada.StatusCode);

        // Y LAS DOS QUE NO DICEN CADA CUANTO OBLIGAN A EXPLICARSE, igual que en un Festival.
        var sinDetalle = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado intermitente sin explicar",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            OrganizacionId = organizacion,
            Periodicidad = "intermitente",
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinDetalle.StatusCode);

        var buena = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", new MercadoUpsertRequest
        {
            Nombre = "Mercado bienal",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            OrganizacionId = organizacion,
            // EN MAYUSCULA A PROPOSITO: lo que se guarda va normalizado, así que «Bienal» y
            // «bienal» son el mismo dato y no dos.
            Periodicidad = "Bienal",
        });
        buena.EnsureSuccessStatusCode();
        Assert.Equal("bienal", (await buena.Content.ReadFromJsonAsync<MercadoDto>())!.Periodicidad);
    }

}
