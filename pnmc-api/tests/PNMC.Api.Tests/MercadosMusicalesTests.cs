using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Mercados musicales: el proceso existe, y su relación con un festival es una relación de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS EXISTEN PARA IMPEDIR.</b> Que la relación Mercado ↔ Festival vuelva a ser
/// lo que era en la tabla heredada: un nombre escrito a mano junto a una clave ajena que nadie
/// obligaba a coincidir con él. Aquí no se mira ninguna pantalla; se piden las rutas y se comprueba
/// qué contesta el servidor, porque un selector acotado en el formulario es una comodidad y la regla
/// solo existe cuando la aplica quien recibe la petición.
/// </para>
/// <para>
/// <b>EL ESTADO DEL FESTIVAL NO EXCLUYE.</b> La dirección está definido con esas palabras:
/// «podrá seleccionarse aunque se encuentre en estado borrador, en revisión o publicado. La
/// condición fundamental es que exista como registro y pertenezca a la misma organización».
/// </para>
/// </remarks>
public sealed class MercadosMusicalesTests
{
    private static async Task<HttpClient> ConsolaAsync(TestWebApplicationFactory factory) =>
        await CmsTestClient.LoginAsync(factory);

    /// <summary>Siembra una organización y, opcionalmente, un festival suyo en el estado que se pida.</summary>
    private static (int Organizacion, int Festival) Sembrar(
        TestWebApplicationFactory factory, string nombre, string estadoDelFestival = "borrador")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = nombre,
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.EntityProfiles.Add(organizacion);
        db.SaveChanges();

        var festival = new FestivalRow
        {
            Name = "Festival de " + nombre,
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = estadoDelFestival,
            OrganizacionPrincipalId = organizacion.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.FestivalRecords.Add(festival);
        db.SaveChanges();

        return (organizacion.Id, festival.Id);
    }

    private static MercadoUpsertRequest UnMercado(int organizacion) => new()
    {
        Nombre = "Mercado de prueba",
        NivelCobertura = "municipal",
        CodigoDepartamento = "05",
        CodigoMunicipio = "05001",
        OrganizacionId = organizacion,
    };

    [Fact]
    public async Task Sin_el_modulo_activado_la_ruta_no_responde()
    {
        await using var factory = new TestWebApplicationFactory();
        var gestor = await CuentaSinModulosAsync(factory);

        var respuesta = await gestor.GetAsync("/api/v1/institucional/mercados");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_lista_responde_y_arranca_vacia()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);

        var respuesta = await consola.GetAsync("/api/v1/institucional/mercados");
        respuesta.EnsureSuccessStatusCode();
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, pagina.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Los_vocabularios_salen_del_catalogo_y_no_del_formulario()
    {
        // ALCANCE Y MODALIDAD ERAN TEXTO LIBRE en la tabla heredada, de modo que «Nacional»,
        // «nacional» y «NACIONAL» eran tres alcances distintos y nadie podía contar por alcance.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);

        var catalogos = await consola.GetFromJsonAsync<CatalogosDeMercadoDto>("/api/v1/institucional/mercados/catalogos");

        Assert.NotNull(catalogos);
        Assert.Contains(catalogos.Alcances, a => a.Slug == "internacional");
        Assert.Contains(catalogos.Modalidades, m => m.Slug == "mixta");
    }

    [Fact]
    public async Task Un_mercado_independiente_se_crea_sin_festival()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del mercado independiente");

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", UnMercado(organizacion));

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.NotNull(mercado);
        Assert.False(mercado.SeRealizaEnElMarcoDeUnFestival);
        Assert.Null(mercado.FestivalId);
        // NACE EN BORRADOR, como cualquier registro del Ecosistema: publicar es una decisión aparte.
        Assert.Equal("borrador", mercado.EstadoRegistro);
    }

    [Fact]
    public async Task El_numero_de_ediciones_se_cuenta_y_no_se_escribe()
    {
        // LA TABLA HEREDADA LO GUARDABA EN UNA COLUMNA que alguien tenía que mantener a mano
        // mientras la lista de ediciones decía otra cosa. Lo derivable no se pregunta.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del recuento");

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", UnMercado(organizacion));
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.NotNull(mercado);
        Assert.Equal(0, mercado.NumeroDeEdiciones);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.EdicionesMercado.Add(new EdicionMercadoRow { MercadoId = mercado.Id, Anio = 2025, FechaCreacion = DateTime.UtcNow });
            db.EdicionesMercado.Add(new EdicionMercadoRow { MercadoId = mercado.Id, Anio = 2026, FechaCreacion = DateTime.UtcNow });
            db.SaveChanges();
        }

        var releido = await consola.GetFromJsonAsync<MercadoDto>($"/api/v1/institucional/mercados/{mercado.Id}");
        Assert.NotNull(releido);
        Assert.Equal(2, releido.NumeroDeEdiciones);
    }

    [Fact]
    public async Task Decir_que_si_al_festival_y_no_elegirlo_no_se_guarda()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del festival sin elegir");

        var peticion = UnMercado(organizacion);
        peticion.SeRealizaEnElMarcoDeUnFestival = true;

        var respuesta = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("elige cuál", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Elegir_un_festival_diciendo_que_no_tampoco()
    {
        // DEJARLO GUARDADO SERIA UNA RELACION QUE LA FICHA NO ENSEÑA y que nadie sabe que existe.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, festival) = Sembrar(factory, "Organización del festival sobrante");

        var peticion = UnMercado(organizacion);
        peticion.SeRealizaEnElMarcoDeUnFestival = false;
        peticion.FestivalId = festival;

        var respuesta = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_festival_de_otra_organizacion_se_rechaza()
    {
        // ESTA ES LA REGLA QUE NO PUEDE VIVIR EN UN CHECK: compara dos filas de dos tablas
        // distintas. Sin ella, el selector acotado de la pantalla sería decoración y bastaría con
        // mandar otro identificador a mano.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacionDelMercado, _) = Sembrar(factory, "Organización dueña del mercado");
        var (_, festivalAjeno) = Sembrar(factory, "Organización dueña del festival");

        var peticion = UnMercado(organizacionDelMercado);
        peticion.SeRealizaEnElMarcoDeUnFestival = true;
        peticion.FestivalId = festivalAjeno;

        var respuesta = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("otra organización", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_festival_en_borrador_de_la_misma_organizacion_si_se_puede_elegir()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, festival) = Sembrar(factory, "Organización con festival en borrador", "borrador");

        var peticion = UnMercado(organizacion);
        peticion.SeRealizaEnElMarcoDeUnFestival = true;
        peticion.FestivalId = festival;

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.NotNull(mercado);
        Assert.Equal(festival, mercado.FestivalId);
        // EL NOMBRE ES DERIVADO: se lee del festival relacionado, no de una segunda columna.
        Assert.Equal("Festival de Organización con festival en borrador", mercado.FestivalNombre);
    }

    [Fact]
    public async Task Los_elegibles_son_los_de_esa_organizacion_sea_cual_sea_su_estado()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (mia, festivalPropio) = Sembrar(factory, "Organización que pregunta", "en_revision");
        var (_, festivalAjeno) = Sembrar(factory, "Organización ajena", "publicado");

        var elegibles = await consola.GetFromJsonAsync<List<FestivalElegibleDto>>(
            $"/api/v1/institucional/mercados/festivales-elegibles?organizacion={mia}");

        Assert.NotNull(elegibles);
        Assert.Contains(elegibles, f => f.Id == festivalPropio);
        Assert.DoesNotContain(elegibles, f => f.Id == festivalAjeno);
    }

    [Fact]
    public async Task Un_territorio_que_no_corresponde_con_la_cobertura_no_se_guarda()
    {
        // EL CHECK DE LA BASE LO RECHAZARIA CON UN 500 SIN EXPLICACION. Aquí sale como un 400 sobre
        // el campo que hay que corregir, que es la diferencia entre un error y un misterio.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del territorio incoherente");

        var peticion = UnMercado(organizacion);
        peticion.NivelCobertura = "nacional";   // Nacional exige departamento y municipio nulos…
        // …y estos siguen puestos.

        var respuesta = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ─────────────────────────── La paridad con Festivales ───────────────────────────

    [Fact]
    public async Task El_ojo_de_borradores_los_quita_en_el_servidor()
    {
        // SOBRE EL TOTAL Y NO SOBRE LA PAGINA: es la regla que Agenda tuvo que corregir. Se pide
        // `incluirBorradores=false` y la respuesta no trae ninguno, sea cual sea la página.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del ojo");

        var enBorrador = UnMercado(organizacion);
        enBorrador.Nombre = "Mercado que sigue en borrador";
        (await consola.PostAsJsonAsync("/api/v1/institucional/mercados", enBorrador)).EnsureSuccessStatusCode();
        await MercadoPublicadoAsync(factory, consola, "Organización del ojo, publicado");

        var conTodos = await consola.GetAsync("/api/v1/institucional/mercados");
        var sinBorradores = await consola.GetAsync("/api/v1/institucional/mercados?incluirBorradores=false");

        Assert.Contains("Mercado que sigue en borrador", await conTodos.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("Mercado que sigue en borrador", await sinBorradores.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_previsualizacion_entrega_un_borrador_a_quien_tiene_consola_y_solo_a_ese()
    {
        // COMO SE VERA ANTES DE PUBLICARLO. La ruta pública no entrega borradores; esta sí, porque
        // quien la pide tiene sesión de consola y el directorio la inserta para enseñar cómo
        // quedará. Sin sesión, no existe.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización de la previsualización");

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", UnMercado(organizacion));
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var conConsola = await consola.GetAsync($"/api/v1/admin/previsualizacion/mercados/{mercado!.Id}");
        var anonimo = await factory.CreateClient().GetAsync($"/api/v1/admin/previsualizacion/mercados/{mercado.Id}");
        var inexistente = await consola.GetAsync("/api/v1/admin/previsualizacion/mercados/999999");

        Assert.Equal(HttpStatusCode.OK, conConsola.StatusCode);
        Assert.Equal("borrador", (await conConsola.Content.ReadFromJsonAsync<MercadoDto>())!.EstadoRegistro);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    // ─────────────────────────── Las ediciones ───────────────────────────

    /// <summary>Publica un mercado y devuelve su identificador, listo para tener ediciones.</summary>
    private static async Task<int> MercadoPublicadoAsync(
        TestWebApplicationFactory factory, HttpClient consola, string nombre)
    {
        var (organizacion, _) = Sembrar(factory, nombre);
        var peticion = UnMercado(organizacion);
        peticion.Nombre = "Mercado de " + nombre;

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Mercados.FirstAsync(m => m.Id == mercado!.Id);
        fila.EstadoRegistro = "publicado";
        await db.SaveChangesAsync();

        return mercado!.Id;
    }

    private static EdicionDeMercadoUpsertRequest UnaEdicion(int anio = 2026) => new()
    {
        Anio = anio,
        Nombre = $"Edición {anio}",
        Estado = "programada",
        EstadoVisibilidad = "borrador",
    };

    [Fact]
    public async Task Un_mercado_sin_publicar_todavia_no_tiene_ediciones()
    {
        // ANUNCIAR LA REALIZACION DE UN PROCESO QUE EL PROGRAMA NO HA APROBADO sería publicar por la
        // puerta de atrás lo que el circuito de revisión existe para decidir.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del mercado en borrador");

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", UnMercado(organizacion));
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var intento = await consola.PostAsJsonAsync(
            $"/api/v1/institucional/mercados/{mercado!.Id}/ediciones", UnaEdicion());

        Assert.Equal(HttpStatusCode.Conflict, intento.StatusCode);
    }

    [Fact]
    public async Task Dos_ediciones_del_mismo_mercado_conviven_sin_pisarse()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización de dos ediciones");

        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2025)))
            .EnsureSuccessStatusCode();
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026)))
            .EnsureSuccessStatusCode();

        var lista = await consola.GetFromJsonAsync<List<EdicionDeMercadoDto>>(
            $"/api/v1/institucional/mercados/{mercado}/ediciones");

        Assert.NotNull(lista);
        // LO MAS RECIENTE PRIMERO: quien abre la lista quiere la última.
        Assert.Equal([2026, 2025], lista!.Select(e => e.Anio).ToList());
    }

    [Fact]
    public async Task El_ciclo_de_visibilidad_de_una_edicion_va_y_vuelve_pero_archivar_no_se_deshace()
    {
        // LOS DOS EJES NO SE MEZCLAN: publicar decide si el portal la enseña; el estado de la
        // realización —programada, realizada— describe el acontecimiento y no se toca aquí.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización del ciclo de ediciones");

        var creada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026));
        creada.EnsureSuccessStatusCode();
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();

        var publicada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion!.Id}/publicar", new { });
        publicada.EnsureSuccessStatusCode();
        var trasPublicar = await publicada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();
        Assert.Equal("publicado", trasPublicar!.EstadoVisibilidad);
        Assert.Equal("programada", trasPublicar.Estado);

        // Publicar dos veces no es una acción: es un clic repetido.
        var repetida = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}/publicar", new { });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        var retirada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}/despublicar", new { });
        retirada.EnsureSuccessStatusCode();
        Assert.Equal("borrador", (await retirada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>())!.EstadoVisibilidad);

        var archivada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}/archivar", new { });
        archivada.EnsureSuccessStatusCode();
        Assert.Equal("archivado", (await archivada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>())!.EstadoVisibilidad);

        // ARCHIVAR NO SE DESHACE POR AQUI: reabrir una decisión sin dejar constancia de que se
        // reabrió es peor que no poder reabrirla.
        var reabierta = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}/publicar", new { });
        Assert.Equal(HttpStatusCode.Conflict, reabierta.StatusCode);
    }

    [Fact]
    public async Task Lo_que_estuvo_publicado_no_se_borra_aunque_ahora_este_en_borrador()
    {
        // LA PREGUNTA NO ES «¿ESTA PUBLICADA AHORA?» SINO «¿LLEGO A PUBLICARSE?». Una edición
        // despublicada vuelve a borrador y ya la vio cualquiera; borrarla dejaría su rastro en la
        // bitácora apuntando a un registro que ya no existe.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización que quiere borrar lo publicado");

        var creada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026));
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();

        // Un borrador que nunca se anunció sí se borra.
        var otra = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2025));
        var borrador = await otra.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();
        var borrada = await consola.DeleteAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{borrador!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, borrada.StatusCode);

        await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion!.Id}/publicar", new { });
        await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}/despublicar", new { });

        var intento = await consola.DeleteAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion.Id}");
        Assert.Equal(HttpStatusCode.Conflict, intento.StatusCode);

        var siguenSiendoDos = await consola.GetFromJsonAsync<List<EdicionDeMercadoDto>>(
            $"/api/v1/institucional/mercados/{mercado}/ediciones");
        Assert.Single(siguenSiendoDos!);
    }

    [Fact]
    public async Task No_se_publica_la_edicion_de_un_mercado_que_no_esta_publicado()
    {
        // QUEDARIA PUBLICADA Y SIN DONDE VERSE: la ficha pública del mercado no existe todavía.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización que despublica su mercado");

        var creada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026));
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();

        // Se retira el mercado del portal; su edición ya no puede publicarse.
        (await consola.PostAsJsonAsync($"/api/v1/admin/mercados/{mercado}/publicacion",
            new CambioDePublicacionDeFestival { Estado = "borrador" })).EnsureSuccessStatusCode();

        var intento = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones/{edicion!.Id}/publicar", new { });
        Assert.Equal(HttpStatusCode.Conflict, intento.StatusCode);
    }

    [Fact]
    public async Task Un_ano_repetido_se_rechaza_diciendo_cual()
    {
        // LO GARANTIZA `UQ_EdicionesMercado_MercadoAnio` EN LA BASE, y se comprueba antes para poder
        // decirlo con un 409 y no con el choque del índice único, que sale como un 500 sin
        // explicación.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización del año repetido");

        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026)))
            .EnsureSuccessStatusCode();
        var segunda = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", UnaEdicion(2026));
        var cuerpo = await segunda.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Contains("2026", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_dos_ejes_de_estado_de_una_edicion_son_independientes()
    {
        // UN MERCADO CANCELADO QUE SIGUE PUBLICADO ES INFORMACION LEGITIMA, y con un solo eje habría
        // que elegir entre decir que se canceló y decir que se ve.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización de los dos ejes");

        var peticion = UnaEdicion(2026);
        peticion.Estado = "cancelada";
        peticion.EstadoVisibilidad = "publicado";

        var creada = await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", peticion);
        creada.EnsureSuccessStatusCode();
        var edicion = await creada.Content.ReadFromJsonAsync<EdicionDeMercadoDto>();

        Assert.Equal("cancelada", edicion!.Estado);
        Assert.Equal("publicado", edicion.EstadoVisibilidad);
        Assert.NotNull(edicion.FechaPublicacion);
    }

    [Fact]
    public async Task La_consulta_publica_de_ediciones_solo_entrega_las_publicadas()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var mercado = await MercadoPublicadoAsync(factory, consola, "Organización de la vitrina");

        var enBorrador = UnaEdicion(2025);
        enBorrador.Nombre = "Edición que sigue en borrador";
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", enBorrador))
            .EnsureSuccessStatusCode();

        var publicada = UnaEdicion(2026);
        publicada.Nombre = "Edición publicada";
        publicada.EstadoVisibilidad = "publicado";
        (await consola.PostAsJsonAsync($"/api/v1/institucional/mercados/{mercado}/ediciones", publicada))
            .EnsureSuccessStatusCode();

        var anonimo = factory.CreateClient();
        var cuerpo = await (await anonimo.GetAsync($"/api/v1/publico/mercados/{mercado}/ediciones")).Content.ReadAsStringAsync();

        Assert.Contains("Edición publicada", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Edición que sigue en borrador", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_ficha_del_festival_enseña_los_mercados_que_ocurren_en_su_marco()
    {
        // ES LA RELACION VISTA DEL OTRO LADO, y sin ella solo se puede recorrer en un sentido, que
        // es la mitad de una relación.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, festival) = Sembrar(factory, "Organización del festival con mercado", "publicado");

        var peticion = UnMercado(organizacion);
        peticion.Nombre = "Mercado dentro del festival";
        peticion.SeRealizaEnElMarcoDeUnFestival = true;
        peticion.FestivalId = festival;

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        creado.EnsureSuccessStatusCode();
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = await db.Mercados.FirstAsync(m => m.Id == mercado!.Id);
            fila.EstadoRegistro = "publicado";
            await db.SaveChangesAsync();
        }

        var anonimo = factory.CreateClient();
        var cuerpo = await (await anonimo.GetAsync($"/api/v1/publico/festivales/{festival}/mercados")).Content.ReadAsStringAsync();

        Assert.Contains("Mercado dentro del festival", cuerpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// La consulta pública entrega lo publicado, y solo lo publicado.
    /// </summary>
    /// <remarks>
    /// <b>ES LA MITAD QUE SE OLVIDA AL ABRIR UNA RUTA.</b> Comprobar que responde sin sesión es
    /// fácil; lo que hay que fijar es que no se lleve por delante el circuito de revisión. Un
    /// borrador o algo en revisión es conversación entre una organización y el Programa, y
    /// entregarlo aquí lo publicaría por la puerta de atrás.
    /// </remarks>
    [Fact]
    public async Task La_consulta_publica_entrega_lo_publicado_y_nada_mas()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización de la consulta pública");

        var borrador = UnMercado(organizacion);
        borrador.Nombre = "Mercado que sigue en borrador";
        (await consola.PostAsJsonAsync("/api/v1/institucional/mercados", borrador)).EnsureSuccessStatusCode();

        // SIN SESION NINGUNA: es una ruta del sitio abierto.
        var anonimo = factory.CreateClient();
        var respuesta = await anonimo.GetAsync("/api/v1/publico/mercados");
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Mercado que sigue en borrador", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_mercado_sin_publicar_no_se_abre_por_su_direccion()
    {
        // NI ESCRIBIENDO SU IDENTIFICADOR A MANO. Ocultarlo de la lista y servirlo por su dirección
        // sería dejar la puerta cerrada y la ventana abierta.
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización del mercado sin publicar");

        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", UnMercado(organizacion));
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        var anonimo = factory.CreateClient();
        var respuesta = await anonimo.GetAsync($"/api/v1/publico/mercados/{mercado!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>Una cuenta de gestor recién entregada y terminada, pero sin ningún módulo concedido.</summary>
    private static async Task<HttpClient> CuentaSinModulosAsync(TestWebApplicationFactory factory)
    {
        const string clave = "ClaveDePrueba123";
        const string definitiva = "ClaveElegida456";
        var webmaster = await CmsTestClient.LoginAsync(factory);
        var correo = $"gestor.mercados.{Guid.NewGuid():N}@pnmc.local";

        var alta = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Gestor sin mercados",
            Email = correo,
            Roles = ["gestor_interno"],
            Password = clave,
            IsActive = true,
        });
        alta.EnsureSuccessStatusCode();
        var creada = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(
            creada.GetProperty("user").GetProperty("id").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);

        var sesion = await CmsTestClient.LoginAsync(factory, correo, clave);

        // El primer ingreso, que si falta contesta 403 antes de mirar ningún módulo y haría que esta
        // prueba midiera el recorrido de bienvenida en vez del permiso.
        (await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-contrasena",
            new CambioDeContrasenaPropia { Actual = clave, Nueva = definitiva })).EnsureSuccessStatusCode();
        (await sesion.PostAsJsonAsync("/api/v1/admin/auth/mi-perfil", new PerfilPropio
        {
            PrimerNombre = "Gestora",
            PrimerApellido = "Sin Mercados",
            TipoDocumento = "cc",
            Identificacion = "2" + id.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(6, '0'),
        })).EnsureSuccessStatusCode();

        return sesion;
    }

    // ─── Prácticas, territorios y la ficha pública (15 de septiembre de 2026) ───

    [Fact]
    public async Task Las_practicas_y_los_territorios_se_guardan_y_se_leen_del_catalogo_del_ecosistema()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización con prácticas");
        var catalogos = await consola.GetFromJsonAsync<CatalogosDeMercadoDto>("/api/v1/institucional/mercados/catalogos");
        Assert.NotNull(catalogos);
        // EL CATALOGO ES EL DEL ECOSISTEMA: el mismo que llena un Festival.
        Assert.NotEmpty(catalogos!.PracticasMusicales);
        Assert.NotEmpty(catalogos.TerritoriosSonoros);
        var practica = catalogos.PracticasMusicales[0];
        var territorio = catalogos.TerritoriosSonoros[0];

        var peticion = UnMercado(organizacion);
        peticion.PracticasMusicalesIds = [practica.Id, practica.Id];
        peticion.TerritoriosSonorosIds = [territorio.Id];
        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();

        // Se leen con su nombre, y el repetido se guarda una sola vez.
        Assert.Equal([practica.Nombre], mercado!.PracticasMusicales.Select(x => x.Nombre));
        Assert.Equal([territorio.Nombre], mercado.TerritoriosSonoros.Select(x => x.Nombre));

        // Guardar con otra lista deja exactamente esa lista.
        peticion.PracticasMusicalesIds = [];
        var guardado = await consola.PutAsJsonAsync($"/api/v1/institucional/mercados/{mercado.Id}", peticion);
        guardado.EnsureSuccessStatusCode();
        var tras = await guardado.Content.ReadFromJsonAsync<MercadoDto>();
        Assert.Empty(tras!.PracticasMusicales);
        Assert.Single(tras.TerritoriosSonoros);
    }

    [Fact]
    public async Task Una_practica_que_no_existe_en_el_catalogo_no_se_guarda()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, _) = Sembrar(factory, "Organización con práctica inventada");
        var peticion = UnMercado(organizacion);
        peticion.PracticasMusicalesIds = [987654];

        var respuesta = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("practicasMusicalesIds", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_ficha_publica_de_un_mercado_no_lleva_correo_ni_telefono()
    {
        await using var factory = new TestWebApplicationFactory();
        var consola = await ConsolaAsync(factory);
        var (organizacion, festival) = Sembrar(factory, "Organización con mercado publicado", "publicado");
        var peticion = UnMercado(organizacion);
        peticion.CorreoMercado = "correo.que.no.sale@example.com";
        peticion.TelefonoMercado = "3001234567";
        peticion.ObservacionesContacto = "Preguntar por la coordinadora";
        peticion.SitioWebMercado = "https://mercado.example.com";
        peticion.SeRealizaEnElMarcoDeUnFestival = true;
        peticion.FestivalId = festival;
        var creado = await consola.PostAsJsonAsync("/api/v1/institucional/mercados", peticion);
        var mercado = await creado.Content.ReadFromJsonAsync<MercadoDto>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = await db.Mercados.SingleAsync(m => m.Id == mercado!.Id);
            fila.EstadoRegistro = "publicado";
            fila.FechaPublicacion = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var anonimo = factory.CreateClient();

        // SE MIRA EL JSON EN CRUDO, como en la ficha del Festival: un DTO tipado sin el campo
        // pasaria aunque el servidor lo mandara.
        foreach (var ruta in new[] { $"/api/v1/publico/mercados/{mercado!.Id}", "/api/v1/publico/mercados", $"/api/v1/publico/festivales/{festival}/mercados" })
        {
            var respuesta = await anonimo.GetAsync(ruta);
            respuesta.EnsureSuccessStatusCode();
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            Assert.Contains("Mercado de prueba", cuerpo, StringComparison.Ordinal);
            Assert.Contains("https://mercado.example.com", cuerpo, StringComparison.Ordinal);
            Assert.DoesNotContain("correo.que.no.sale", cuerpo, StringComparison.Ordinal);
            Assert.DoesNotContain("3001234567", cuerpo, StringComparison.Ordinal);
            Assert.DoesNotContain("coordinadora", cuerpo, StringComparison.Ordinal);
        }
    }

}
