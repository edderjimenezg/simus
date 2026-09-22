using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El circuito de la Agenda, de borrador a portal.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que se anuncie un evento al que no se puede ir: un presencial sin
/// lugar o un virtual sin enlace. Y que un festival de varios días aparezca como finalizado en su
/// segunda jornada, que es el fallo silencioso de calcular la situación solo contra la fecha de
/// inicio.
/// </para>
/// </summary>
public sealed class AgendaCircuitoTests
{
    private const string Consola = "/api/v1/institucional/agenda";
    private const string Publico = "/api/v1/publico/agenda";

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    private static object EventoNuevo(
        string titulo,
        DateOnly? inicio = null,
        DateOnly? fin = null,
        string modalidad = "presencial",
        string? lugar = "Teatro Municipal de Paipa",
        string? url = null) => new
    {
        titulo,
        descripcion = "Encuentro abierto al público del ecosistema musical.",
        fechaInicio = inicio ?? Hoy,
        fechaFin = fin,
        modalidad,
        lugar,
        url,
        codigoDepartamento = "15",
        etiquetas = new[] { "bandas", "formación" },
    };

    [Fact]
    public async Task La_consola_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Consola)).StatusCode);
    }

    [Fact]
    public async Task El_portal_es_anonimo_y_empieza_vacio()
    {
        await using var factory = new TestWebApplicationFactory();

        var respuesta = await factory.CreateClient().GetAsync(Publico);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, pagina.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Un_evento_nace_en_borrador_con_su_direccion_calculada()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await client.PostAsJsonAsync(Consola, EventoNuevo("Festival de Bandas de Paipa"));

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var evento = await creado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("borrador", evento.GetProperty("estado").GetString());
        Assert.Equal("festival-de-bandas-de-paipa", evento.GetProperty("slug").GetString());
        // EL CODIGO SE GUARDA Y EL NOMBRE SE RESUELVE: `Divipola` es el catálogo canónico.
        Assert.Equal("15", evento.GetProperty("codigoDepartamento").GetString());
    }

    [Fact]
    public async Task Un_presencial_sin_lugar_no_se_publica()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Evento sin sitio", lugar: null))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        Assert.Equal(HttpStatusCode.BadRequest, publicar.StatusCode);
        Assert.Contains("no dice dónde", await publicar.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_virtual_sin_enlace_no_se_publica()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Conversatorio en línea", modalidad: "virtual", lugar: null))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        Assert.Equal(HttpStatusCode.BadRequest, publicar.StatusCode);
        Assert.Contains("no tiene enlace", await publicar.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_mixto_necesita_las_dos_cosas_y_lo_dice_de_una_vez()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Encuentro híbrido", modalidad: "mixta", lugar: null))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        var mensaje = await publicar.Content.ReadAsStringAsync();
        // LAS DOS FALTAS EN UN SOLO MENSAJE: quien elige «mixta» promete ambas formas de asistir.
        Assert.Contains("no dice dónde", mensaje, StringComparison.Ordinal);
        Assert.Contains("no tiene enlace", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_evento_completo_se_publica_y_aparece_en_el_portal()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola, EventoNuevo("Semana de la Música"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();
        var slug = creado.GetProperty("slug").GetString();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" })).StatusCode);

        var anonimo = factory.CreateClient();
        var pagina = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, pagina.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Un_evento_pasado_no_desaparece_del_portal()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Festival del año pasado", inicio: Hoy.AddDays(-400)))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"{Consola}/{creado.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });
        var slug = creado.GetProperty("slug").GetString();

        var anonimo = factory.CreateClient();

        // NO SALE EN «PROXIMOS», que es lo que se pide por omisión…
        var proximos = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, proximos.GetProperty("total").GetInt32());

        // …PERO NO SE HA ESCONDIDO: lo que ya ocurrió sigue siendo información pública.
        var pasados = await (await anonimo.GetAsync($"{Publico}?cuando=pasados")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, pasados.GetProperty("total").GetInt32());
        Assert.Equal("finalizado", pasados.GetProperty("items")[0].GetProperty("situacion").GetString());

        // Y su ficha sigue abierta: un enlace a un evento de hace un año no se rompe.
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Un_evento_de_varios_dias_esta_en_curso_tambien_en_su_segunda_jornada()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // EMPEZO AYER Y ACABA EL DOMINGO. Calcular la situación solo contra la fecha de inicio lo
        // daría por finalizado, y el portal lo sacaría de «próximos» en mitad del festival.
        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Festival de una semana", inicio: Hoy.AddDays(-1), fin: Hoy.AddDays(5)))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"{Consola}/{creado.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var anonimo = factory.CreateClient();
        var pagina = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, pagina.GetProperty("total").GetInt32());
        Assert.Equal("en_curso", pagina.GetProperty("items")[0].GetProperty("situacion").GetString());
    }

    [Fact]
    public async Task Un_evento_no_puede_terminar_antes_de_empezar()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(Consola,
            EventoNuevo("Evento imposible", inicio: Hoy, fin: Hoy.AddDays(-3)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Guardar_con_una_version_vieja_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola, EventoNuevo("Evento versionado"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var primera = await client.PutAsJsonAsync($"{Consola}/{id}",
            new { titulo = "Evento corregido", descripcion = "Descripción.", fechaInicio = Hoy, modalidad = "presencial", lugar = "Paipa", version = 1 });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var segunda = await client.PutAsJsonAsync($"{Consola}/{id}",
            new { titulo = "Evento que pisaría", descripcion = "Descripción.", fechaInicio = Hoy, modalidad = "presencial", lugar = "Paipa", version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task El_portal_filtra_por_departamento()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola, EventoNuevo("Evento en Boyacá"))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"{Consola}/{creado.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var anonimo = factory.CreateClient();
        var enBoyaca = await (await anonimo.GetAsync($"{Publico}?departamento=15")).Content.ReadFromJsonAsync<JsonElement>();
        var enAntioquia = await (await anonimo.GetAsync($"{Publico}?departamento=05")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, enBoyaca.GetProperty("total").GetInt32());
        Assert.Equal(0, enAntioquia.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task El_nivel_de_cobertura_se_deduce_de_los_codigos_y_no_se_pide_aparte()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var nacional = await (await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento nacional", descripcion = "Sin territorio.", fechaInicio = Hoy,
            modalidad = "virtual", url = "https://www.mincultura.gov.co/x",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var departamental = await (await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento departamental", descripcion = "En Boyacá.", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Tunja", codigoDepartamento = "15",
        })).Content.ReadFromJsonAsync<JsonElement>();

        // EL MUNICIPIO TIENE QUE EXISTIR EN DIVIPOLA: con los dos códigos puestos la foránea sí se
        // comprueba —con uno nulo el motor la da por satisfecha— y la base de pruebas nace vacía.
        await SembrarTerritorioAsync(factory, "15", "BOYACÁ", "15516", "PAIPA");

        var municipal = await (await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento municipal", descripcion = "En Paipa.", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Plaza", codigoDepartamento = "15", codigoMunicipio = "15516",
        })).Content.ReadFromJsonAsync<JsonElement>();

        // SON LA MISMA INFORMACION DICHA DOS VECES, y dos formas de decir lo mismo acaban
        // discrepando. La base lo impone además con su CHECK.
        Assert.Equal("nacional", nacional.GetProperty("nivelCobertura").GetString());
        Assert.Equal("departamental", departamental.GetProperty("nivelCobertura").GetString());
        Assert.Equal("municipal", municipal.GetProperty("nivelCobertura").GetString());
    }

    [Fact]
    public async Task Un_evento_de_un_dia_no_puede_acabar_antes_de_empezar()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento imposible", descripcion = "Acaba antes.", fechaInicio = Hoy,
            horaInicio = "19:00:00", horaFin = "17:00:00",
            modalidad = "presencial", lugar = "Teatro",
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task En_un_evento_de_varios_dias_la_hora_de_fin_menor_no_es_un_error()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // ACABAR A LAS 9 DEL ULTIMO DIA ES NORMAL en un festival que empieza a las 19 del primero.
        var respuesta = await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Festival de varios días", descripcion = "Empieza tarde y acaba temprano.",
            fechaInicio = Hoy, fechaFin = Hoy.AddDays(4),
            horaInicio = "19:00:00", horaFin = "09:00:00",
            modalidad = "presencial", lugar = "Plaza",
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_orden_de_visualizacion_empieza_en_uno()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var cero = await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Orden cero", descripcion = "d", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Teatro", ordenVisualizacion = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, cero.StatusCode);
    }

    [Fact]
    public async Task Guardar_sin_mandar_la_imagen_no_se_la_quita_al_evento()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var archivo = await SubirImagenAsync(client);
        var creado = await (await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento con imagen", descripcion = "d", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Teatro", imagenArchivoId = archivo,
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();
        Assert.Equal(archivo, creado.GetProperty("imagenArchivoId").GetInt32());

        // GUARDAR UN SUBCONJUNTO DE CAMPOS sin nombrar la imagen NO la toca. Sin esta distinción,
        // el primer cliente que no conozca el campo le quita la foto a todos los eventos.
        var guardado = await (await client.PutAsJsonAsync($"{Consola}/{id}", new
        {
            titulo = "Evento con imagen, corregido", descripcion = "d", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Teatro", version = 1,
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(archivo, guardado.GetProperty("imagenArchivoId").GetInt32());
    }

    [Fact]
    public async Task Pedir_retirarla_explicitamente_si_se_la_quita()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var archivo = await SubirImagenAsync(client);
        var creado = await (await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Evento que pierde su imagen", descripcion = "d", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Teatro", imagenArchivoId = archivo,
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var guardado = await (await client.PutAsJsonAsync($"{Consola}/{id}", new
        {
            titulo = "Evento que pierde su imagen", descripcion = "d", fechaInicio = Hoy,
            modalidad = "presencial", lugar = "Teatro", version = 1, retirarImagen = true,
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(guardado.GetProperty("imagenArchivoId").ValueKind == JsonValueKind.Null);
    }

    /// <summary>Deja un municipio en el catálogo territorial de la base de pruebas.</summary>
    private static async Task SembrarTerritorioAsync(
        TestWebApplicationFactory factory, string departamento, string nombreDepartamento,
        string municipio, string nombreMunicipio)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        if (await db.DivipolaLocations.AnyAsync(x => x.MunicipalityCode == municipio)) return;

        db.DivipolaLocations.Add(new DivipolaLocationRow
        {
            DepartmentCode = departamento,
            DepartmentName = nombreDepartamento,
            MunicipalityCode = municipio,
            MunicipalityName = nombreMunicipio,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Sube una imagen al banco y devuelve su identificador.</summary>
    private static async Task<int> SubirImagenAsync(HttpClient client)
    {
        var datos = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        using var formulario = new MultipartFormDataContent();
        var parte = new ByteArrayContent(datos);
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        formulario.Add(parte, "file", "evento.png");
        formulario.Add(new StringContent("Cartel del evento"), "alt");

        var respuesta = await client.PostAsync("/api/v1/institucional/archivos", formulario);
        var archivo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return archivo.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Archivar_lo_saca_del_portal_sin_borrarlo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola, EventoNuevo("Evento que se archiva"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();
        var slug = creado.GetProperty("slug").GetString();
        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "archivado" });

        var anonimo = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Consola}/{id}")).StatusCode);
    }
    [Fact]
    public async Task La_consola_puede_pedir_la_lista_sin_lo_archivado()
    {
        // POR QUE HACE FALTA ESTE PARAMETRO. La consola pagina de doce en doce y ordena «sin
        // publicar primero»; con cuatrocientos setenta eventos, el archivo cerrado ocupa sitio en
        // las primeras páginas sin ser trabajo pendiente. Lo pidió la dirección de producto el 15 de
        // septiembre de 2026: «debería haber un botón de mostrar u ocultar archivados».
        //
        // POR OMISION SE INCLUYEN, para no cambiar lo que ya responde a quien no lo pida.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Evento que se archiva"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "archivado" });

        var conTodo = await client.GetFromJsonAsync<JsonElement>(Consola);
        var sinArchivo = await client.GetFromJsonAsync<JsonElement>($"{Consola}?incluirArchivados=false");

        Assert.Equal(1, conTodo.GetProperty("total").GetInt32());
        Assert.Equal(0, sinArchivo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Pedir_el_estado_archivado_los_ensena_aunque_se_pidan_ocultos()
    {
        // LOS DOS FILTROS NO SE CONTRADICEN: quien elige «archivado» en el desplegable de estado
        // quiere verlos, y devolverle una lista vacía porque el conmutador dice lo contrario sería
        // responder a una pregunta que no hizo.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Evento archivado que se busca"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "archivado" });

        var pedido = await client.GetFromJsonAsync<JsonElement>($"{Consola}?estado=archivado&incluirArchivados=false");

        Assert.Equal(1, pedido.GetProperty("total").GetInt32());
    }

    /// <summary>Deja tres eventos: dos sin publicar y uno publicado, con fechas y títulos distintos.</summary>
    /// <remarks>
    /// LOS TITULOS Y LAS FECHAS VAN AL REVES ENTRE SI A PROPOSITO: el primero por título es el
    /// último por fecha. Con los dos criterios alineados, una prueba de orden pasaría aunque el
    /// servidor ordenara siempre por lo mismo.
    /// </remarks>
    private static async Task<HttpClient> TresEventosParaOrdenarAsync(TestWebApplicationFactory factory)
    {
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, EventoNuevo("Zamba de cierre", Hoy.AddDays(1)));
        await client.PostAsJsonAsync(Consola, EventoNuevo("Muestra de bandas", Hoy.AddDays(5)));
        var tercero = await (await client.PostAsJsonAsync(Consola,
            EventoNuevo("Abrazo de tambores", Hoy.AddDays(9)))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"{Consola}/{tercero.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        return client;
    }

    /// <summary>El primer título de la primera página, pidiendo UNA sola fila.</summary>
    /// <remarks>
    /// <b>SE PIDE `tamano=1` Y AHI ESTA TODA LA PRUEBA.</b> Con una fila por página, lo que salga
    /// solo puede venir de haber ordenado los tres eventos en la base: ordenar «la página» cuando
    /// la página tiene un elemento no puede cambiar nada. Es el defecto que se está fijando.
    /// </remarks>
    private static async Task<string?> PrimeroDeLaListaAsync(HttpClient client, string consulta)
    {
        var pagina = await client.GetFromJsonAsync<JsonElement>($"{Consola}?tamano=1&pagina=1&{consulta}");
        return pagina.GetProperty("items")[0].GetProperty("titulo").GetString();
    }

    [Fact]
    public async Task La_consola_ordena_toda_la_agenda_y_no_la_pagina_que_tiene_delante()
    {
        // EL DEFECTO QUE TRAJO ESTA PRUEBA. La tabla ordenaba en el navegador las doce filas que
        // traía la página. Con cuarenta páginas, pulsar «Estado» no subía ningún publicado a la
        // primera página: seguían en la treinta. se detectó el 15 de
        // septiembre de 2026: «debe ser de todos, no solo de los de la página visible, por eso en
        // la página 1 no me aparecía nunca ningún publicado».
        await using var factory = new TestWebApplicationFactory();
        var client = await TresEventosParaOrdenarAsync(factory);

        // Sin orden pedido manda el orden de trabajo: sin publicar primero.
        Assert.Equal("Zamba de cierre", await PrimeroDeLaListaAsync(client, "x=1"));

        // Y pidiendo el estado al revés, el publicado llega a la primera página.
        Assert.Equal("Abrazo de tambores", await PrimeroDeLaListaAsync(client, "orden=estado&direccion=desc"));
    }

    [Theory]
    // «archivado» < «borrador» < «en_revision» < «publicado»: el código cae en el mismo orden que
    // el rótulo que se ve —Archivado, Borrador, En revisión, Publicado—, y por eso se ordena por él.
    [InlineData("actividad", "asc", "Abrazo de tambores")]
    [InlineData("actividad", "desc", "Zamba de cierre")]
    [InlineData("cuando", "asc", "Zamba de cierre")]
    [InlineData("cuando", "desc", "Abrazo de tambores")]
    [InlineData("estado", "asc", "Zamba de cierre")]
    [InlineData("estado", "desc", "Abrazo de tambores")]
    public async Task Cada_columna_de_la_tabla_ordena_en_la_base(string orden, string direccion, string esperado)
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await TresEventosParaOrdenarAsync(factory);

        Assert.Equal(esperado, await PrimeroDeLaListaAsync(client, $"orden={orden}&direccion={direccion}"));
    }

    [Fact]
    public async Task Una_columna_que_no_existe_no_rompe_la_lista()
    {
        // NO ES UN 400: es un enlace guardado, una petición vieja o un nombre mal escrito, y la
        // agenda tiene que seguir respondiendo. Cae al orden de trabajo, sin publicar primero.
        await using var factory = new TestWebApplicationFactory();
        var client = await TresEventosParaOrdenarAsync(factory);

        var respuesta = await client.GetAsync($"{Consola}?orden=columna_inventada&tamano=1&pagina=1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Zamba de cierre", pagina.GetProperty("items")[0].GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task El_orden_no_repite_ni_se_salta_filas_al_paginar()
    {
        // EL DESEMPATE POR IDENTIFICADOR ES LO QUE SE COMPRUEBA. Tres eventos del MISMO día
        // ordenados por fecha empatan; sin un segundo criterio estable, la base puede devolverlos
        // en distinto orden en dos peticiones iguales, y entonces una fila sale en dos páginas y
        // otra no sale en ninguna.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        foreach (var titulo in new[] { "Primero del día", "Segundo del día", "Tercero del día" })
        {
            await client.PostAsJsonAsync(Consola, EventoNuevo(titulo, Hoy));
        }

        var vistos = new List<string?>();
        for (var pagina = 1; pagina <= 3; pagina++)
        {
            var respuesta = await client.GetFromJsonAsync<JsonElement>($"{Consola}?orden=cuando&tamano=1&pagina={pagina}");
            vistos.Add(respuesta.GetProperty("items")[0].GetProperty("titulo").GetString());
        }

        Assert.Equal(3, vistos.Distinct().Count());
    }

}

/// <summary>Las reglas de la Agenda, sin base de datos.</summary>
public sealed class ReglasDeAgendaTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 11);

    [Theory]
    [InlineData(3, null, "proximo")]
    [InlineData(0, null, "en_curso")]
    [InlineData(-1, null, "finalizado")]
    [InlineData(-1, 5, "en_curso")]
    [InlineData(-10, -2, "finalizado")]
    [InlineData(2, 9, "proximo")]
    public void La_situacion_mira_el_ultimo_dia_y_no_solo_el_primero(int diasInicio, int? diasFin, string esperada)
    {
        var inicio = Hoy.AddDays(diasInicio);
        var fin = diasFin is null ? (DateOnly?)null : Hoy.AddDays(diasFin.Value);

        Assert.Equal(esperada, ReglasDeAgenda.SituacionDe(inicio, fin, Hoy));
    }

    [Theory]
    [InlineData("presencial", null, null, 1)]
    [InlineData("presencial", "Teatro", null, 0)]
    [InlineData("virtual", null, null, 1)]
    [InlineData("virtual", null, "https://x.gov.co", 0)]
    [InlineData("mixta", null, null, 2)]
    [InlineData("mixta", "Teatro", null, 1)]
    [InlineData("mixta", "Teatro", "https://x.gov.co", 0)]
    public void La_modalidad_decide_que_datos_son_obligatorios(string modalidad, string? lugar, string? url, int faltasEsperadas)
    {
        var faltas = ReglasDeAgenda.LoQueFaltaParaPublicar("Título", "Descripción", modalidad, lugar, url);

        Assert.Equal(faltasEsperadas, faltas.Count);
    }

    [Fact]
    public void Un_evento_publicado_es_visible_aunque_ya_haya_pasado()
    {
        // NO HAY CORTE POR FECHA, a diferencia de Noticias: lo que ocurrió sigue siendo público.
        Assert.True(ReglasDeAgenda.EsVisiblePublicamente("publicado"));
        Assert.False(ReglasDeAgenda.EsVisiblePublicamente("borrador"));
        Assert.False(ReglasDeAgenda.EsVisiblePublicamente("archivado"));
    }
}
