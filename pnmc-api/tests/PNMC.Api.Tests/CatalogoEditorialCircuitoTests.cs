using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El circuito del Catálogo Editorial, de borrador a portal.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN, y es lo que distingue este catálogo de la tabla plana que se
/// retiró: que una ficha llegue al portal sin cumplir las cuatro condiciones de una revisión anterior
/// —validada, publicada, con fuente y con derechos—. Las cuatro se comprueban por separado
/// porque cada una se puede olvidar por separado.
/// </para>
/// <para>
/// SE PRUEBA CONTRA EL API REAL, no contra el comprobador del contrato. `CatalogoEditorialContratoTests`
/// ya fija que las reglas están bien escritas; aquí se fija que además están ENCHUFADAS, que es
/// la diferencia entre una puerta y el dibujo de una puerta.
/// </para>
/// </summary>
public sealed class CatalogoEditorialCircuitoTests
{
    private const string Consola = "/api/v1/institucional/catalogo-editorial";
    private const string Publico = "/api/v1/publico/catalogo-editorial";

    private static object FichaNueva(string codigo) => new
    {
        codigo,
        titulo = "Acento: arreglos para banda-escuela",
        subtitulo = "Volumen I",
        anioInicio = 2016,
        anioFin = 2016,
        idioma = "es",
    };

    [Fact]
    public async Task La_ficha_se_crea_con_sus_creditos_identificadores_accesos_y_tipologia()
    {
        // POR QUE ESTA PRUEBA EXISTE. El formulario de alta pedía doce campos para un registro de
        // más de treinta, y las cuatro listas que hacen catalográfica a una ficha —créditos con su
        // papel, identificadores con su cualificador, vías de consulta y facetas de tipología— no se
        // podían escribir desde la consola: se cargaron con el acervo y quedaron congeladas.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, new
        {
            codigo = "ED-COMPLETA-1",
            titulo = "Ficha con todo",
            designacionVolumen = "Volumen III",
            formato = "Físico",
            notaFecha = "Fecha aproximada",
            ambitoTexto = "Nacional, con énfasis en la región Andina",
            categoriaSecundaria = "Orquesta",
            creditos = new[]
            {
                new { nombre = "Ana Restrepo", tipo = "persona", rolCodigo = "aut", rolEtiqueta = "Autora", principal = true, orden = 0 },
                new { nombre = "Ministerio de Prueba", tipo = "entidad", rolCodigo = "pbl", rolEtiqueta = "Editorial", principal = false, orden = 1 },
            },
            identificadores = new[]
            {
                new { esquema = "ISBN", codigo = "978-958-000-111-2", cualificador = "EPUB", orden = 0 },
            },
            accesos = new[]
            {
                new { tipo = "enlace", url = "https://www.mincultura.gov.co/guia.pdf", ubicacionFisica = (string?)null, etiqueta = (string?)null, nota = (string?)null, orden = 0 },
                new { tipo = "ubicacion", url = (string?)null, ubicacionFisica = "Biblioteca Nacional", etiqueta = (string?)null, nota = (string?)null, orden = 1 },
            },
        });

        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Volumen III", ficha.GetProperty("designacionVolumen").GetString());
        Assert.Equal("Físico", ficha.GetProperty("formato").GetString());
        Assert.Equal("Fecha aproximada", ficha.GetProperty("notaFecha").GetString());
        Assert.Equal(2, ficha.GetProperty("creditos").GetArrayLength());
        Assert.Equal(1, ficha.GetProperty("identificadores").GetArrayLength());
        Assert.Equal(2, ficha.GetProperty("accesos").GetArrayLength());

        // EL AGENTE SE CREA A PARTIR DEL NOMBRE, y con su tipo: la entidad no puede acabar contada
        // como persona, que es el defecto que tuvo el portal durante todo el módulo.
        var creditos = ficha.GetProperty("creditos").EnumerateArray().ToList();
        Assert.Contains(creditos, c => c.GetProperty("agenteTipo").GetString() == "entidad");
        Assert.Contains(creditos, c => c.GetProperty("agenteTipo").GetString() == "persona");
        Assert.Contains(creditos, c => c.GetProperty("rolEtiqueta").GetString() == "Autora");
    }

    [Fact]
    public async Task Guardar_sin_enviar_las_listas_no_las_borra()
    {
        // AUSENTE NO ES VACIO. Sin esta regla, guardar un cambio de título desde cualquier
        // formulario que no reenvíe los créditos dejaría la ficha sin autoría, y en silencio.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, new
        {
            codigo = "ED-COMPLETA-2",
            titulo = "Ficha con créditos",
            creditos = new[] { new { nombre = "Ana Restrepo", tipo = "persona", rolCodigo = "aut", rolEtiqueta = "Autora", principal = true, orden = 0 } },
        });
        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();
        var id = ficha.GetProperty("id").GetInt64();

        var guardada = await client.PutAsJsonAsync($"{Consola}/{id}", new
        {
            titulo = "Otro título",
            version = ficha.GetProperty("version").GetInt32(),
        });

        Assert.Equal(HttpStatusCode.OK, guardada.StatusCode);
        var despues = await guardada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Otro título", despues.GetProperty("titulo").GetString());
        Assert.Equal(1, despues.GetProperty("creditos").GetArrayLength());
    }

    [Fact]
    public async Task Enviar_una_lista_vacia_si_la_retira()
    {
        // Y LA OTRA MITAD DE LA REGLA: sin esto no habría forma de quitar el último crédito de una
        // ficha desde la consola.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, new
        {
            codigo = "ED-COMPLETA-3",
            titulo = "Ficha con créditos",
            creditos = new[] { new { nombre = "Ana Restrepo", tipo = "persona", rolCodigo = "aut", rolEtiqueta = "Autora", principal = true, orden = 0 } },
        });
        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();

        var guardada = await client.PutAsJsonAsync($"{Consola}/{ficha.GetProperty("id").GetInt64()}", new
        {
            titulo = "Ficha con créditos",
            version = ficha.GetProperty("version").GetInt32(),
            creditos = Array.Empty<object>(),
        });

        var despues = await guardada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, despues.GetProperty("creditos").GetArrayLength());
    }

    [Fact]
    public async Task El_codigo_lo_asigna_el_sistema_por_orden_de_registro()
    {
        // PEDIRLO A MANO TENIA DOS COSTES: había que saberse cuál fue el último —«¿voy por el 171 o
        // por el 172?»— y dos personas catalogando a la vez chocaban sin enterarse hasta guardar.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var primera = await client.PostAsJsonAsync(Consola, new { titulo = "Sin código" });
        var segunda = await client.PostAsJsonAsync(Consola, new { titulo = "Tampoco" });

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        var uno = (await primera.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString();
        var dos = (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

        Assert.Equal("PNMC-ED-001", uno);
        Assert.Equal("PNMC-ED-002", dos);
    }

    [Fact]
    public async Task Un_codigo_escrito_a_mano_se_sigue_aceptando()
    {
        // UNA FICHA QUE SE REPONE TIENE QUE PODER RECUPERAR EL SUYO. Que el sistema lo asigne por
        // omisión no puede significar que nadie pueda volver a poner el código que ya tenía.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, new { codigo = "PNMC-ED-099", titulo = "Repuesta" });

        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PNMC-ED-099", ficha.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task La_seccion_se_deriva_de_la_ruta_y_no_se_guarda_lo_que_venga()
    {
        // MEDIDO SOBRE EL ACERVO: el primer tramo de la ruta coincide con la sección en las 170
        // fichas que tienen las dos, y hay 22 rutas para 9 secciones. Pedir las dos por separado
        // —la sección en un desplegable y la ruta escrita a mano— deja que discrepen, y entonces hay
        // dos verdades y ninguna manda.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, new
        {
            titulo = "Una guía",
            rutaSeccion = "Formación > Pedagogía Instrumental > Guías y Cuadernos de instrumento",
            // Se manda una sección DISTINTA a propósito: la ruta tiene que ganar.
            seccionPrincipal = "Repertorio",
        });

        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Formación", ficha.GetProperty("seccionPrincipal").GetString());
    }

    [Fact]
    public async Task Las_ubicaciones_del_acervo_se_ofrecen_con_su_seccion_resuelta()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, new { titulo = "Una guía", rutaSeccion = "Formación > Pedagogía Instrumental" });

        var listas = await (await client.GetAsync($"{Consola}/vocabularios")).Content.ReadFromJsonAsync<JsonElement>();
        var rutas = listas.GetProperty("rutas").EnumerateArray().ToList();

        Assert.Contains(rutas, r => r.GetProperty("ruta").GetString() == "Formación > Pedagogía Instrumental"
                                    && r.GetProperty("seccion").GetString() == "Formación");
    }

    [Fact]
    public async Task Los_idiomas_se_listan_uno_a_uno_y_no_como_combinaciones()
    {
        // EL CAMPO GUARDA COMBINACIONES —`es`, `es ; en`, `es ; (lengua nativa)`— y el desplegable
        // las enseñaba tal cual: tres opciones que parecían tres españoles distintos. Lo señaló el
        // dirección de producto.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, new { titulo = "Bilingüe", idioma = "es ; en" });
        await client.PostAsJsonAsync(Consola, new { titulo = "Con lengua nativa", idioma = "es ; (lengua nativa)" });

        var listas = await (await client.GetAsync($"{Consola}/vocabularios")).Content.ReadFromJsonAsync<JsonElement>();
        var idiomas = listas.GetProperty("idiomas").EnumerateArray().ToList();

        // Ni una sola opción contiene el separador: son idiomas, no combinaciones.
        Assert.DoesNotContain(idiomas, i => i.GetProperty("codigo").GetString()!.Contains(';', StringComparison.Ordinal));
        Assert.Contains(idiomas, i => i.GetProperty("codigo").GetString() == "es" && i.GetProperty("usos").GetInt32() == 2);
        Assert.Contains(idiomas, i => i.GetProperty("codigo").GetString() == "en");
        // Lo que la fuente no identifica se dice, no se inventa.
        Assert.Contains(idiomas, i => i.GetProperty("pendienteDePrecisar").GetBoolean());
    }

    [Fact]
    public async Task Los_vocabularios_salen_del_acervo_y_no_de_una_lista_fija()
    {
        // LAS LISTAS DEL FORMULARIO SE LEEN DEL PROPIO CATALOGO, igual que DIVIPOLA se lee de su
        // fuente. Una lista escrita a mano en el código se desincroniza en cuanto alguien cataloga
        // algo nuevo, y entonces el formulario impide escribir lo que la ficha de al lado ya dice.
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, new
        {
            codigo = "ED-VOCAB-1",
            titulo = "Ficha para el vocabulario",
            tipoPublicacion = "Libro y CD",
            ambito = "Andina",
            formato = "Mixto",
        });

        var respuesta = await client.GetAsync($"{Consola}/vocabularios");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var listas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var tipos = listas.GetProperty("tiposDePublicacion").EnumerateArray()
            .Select(x => x.GetProperty("valor").GetString()).ToList();
        Assert.Contains("Libro y CD", tipos);
        Assert.Contains("Andina", listas.GetProperty("ambitos").EnumerateArray().Select(x => x.GetProperty("valor").GetString()));
        // El vocabulario de tipo de agente sí es fijo: lo declara el contrato y lo custodia el CHECK.
        Assert.Equal(["persona", "entidad"], listas.GetProperty("tiposDeAgente").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task El_catalogo_interno_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(Consola)).StatusCode);
    }

    [Fact]
    public async Task La_consulta_publica_es_anonima_y_empieza_vacia()
    {
        await using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        var respuesta = await anonimo.GetAsync(Publico);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        // NO SE SIEMBRA NADA: Una revisión anterior lo decidió y esta prueba lo sostiene. Un catálogo que
        // nace con filas de demostración acaba enseñándolas en producción.
        Assert.Equal(0, pagina.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Una_ficha_nace_en_borrador_y_pendiente_de_revision()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, FichaNueva("ED-001"));

        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("borrador", ficha.GetProperty("estadoPublicacion").GetString());
        Assert.Equal("pendiente_revision", ficha.GetProperty("estadoCatalogacion").GetString());
        Assert.Equal(1, ficha.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task El_codigo_no_se_repite()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, FichaNueva("ED-002"));
        var repetida = await client.PostAsJsonAsync(Consola, FichaNueva("ED-002"));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task Guardar_con_una_version_vieja_se_rechaza_y_no_escribe()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-003"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var primera = await client.PutAsJsonAsync($"{Consola}/{id}", new { titulo = "Primer cambio", version = 1 });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        // La segunda cita la versión que ya quedó atrás: es la persona que abrió la ficha antes.
        var segunda = await client.PutAsJsonAsync($"{Consola}/{id}", new { titulo = "Cambio que pisaría", version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);

        var vigente = await (await client.GetAsync($"{Consola}/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Primer cambio", vigente.GetProperty("titulo").GetString());
    }

    [Theory]
    [InlineData(false, false, false, "la ficha no está validada")]
    [InlineData(true, false, false, "no tiene ninguna fuente")]
    [InlineData(true, true, false, "los derechos no autorizan")]
    public async Task Publicar_se_rechaza_mientras_falte_cualquiera_de_las_condiciones(
        bool validada, bool conFuente, bool conDerechos, string motivoEsperado)
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-004"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        if (validada) await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });
        if (conFuente) await AgregarFuenteAsync(factory, id);
        if (conDerechos) await AutorizarFichaAsync(factory, id);

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });

        Assert.Equal(HttpStatusCode.BadRequest, publicar.StatusCode);
        Assert.Contains(motivoEsperado, await publicar.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_las_cuatro_condiciones_la_ficha_se_publica_y_aparece_en_el_portal()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-005"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });
        await AgregarFuenteAsync(factory, id);
        await AutorizarFichaAsync(factory, id);

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });
        Assert.Equal(HttpStatusCode.OK, publicar.StatusCode);

        var anonimo = factory.CreateClient();
        var pagina = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, pagina.GetProperty("total").GetInt32());

        var ficha = await anonimo.GetAsync($"{Publico}/ED-005");
        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
    }

    [Fact]
    public async Task Retirar_la_saca_del_portal_sin_borrarla_de_la_consola()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-006"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });
        await AgregarFuenteAsync(factory, id);
        await AutorizarFichaAsync(factory, id);
        await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });

        await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "retirado" });

        var anonimo = factory.CreateClient();
        // RETIRAR ES UN ESTADO, NO UN BORRADO: desaparece del portal y sigue entera en la consola.
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"{Publico}/ED-006")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Consola}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Una_ficha_validada_puede_quedarse_sin_publicar()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-007"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var catalogada = await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });

        var ficha = await catalogada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validada", ficha.GetProperty("estadoCatalogacion").GetString());
        // VALIDAR NO PUBLICA. Son dos decisiones y esta prueba es la que impide que se fundan.
        Assert.Equal("borrador", ficha.GetProperty("estadoPublicacion").GetString());
    }

    [Fact]
    public async Task Un_estado_que_no_existe_en_el_contrato_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-008"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var respuesta = await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "en_evaluacion" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("editorial/thumbs/PNMC-ED-100.png", "/editorial/thumbs/PNMC-ED-100.png")]
    [InlineData("/editorial/thumbs/PNMC-ED-100.png", "/editorial/thumbs/PNMC-ED-100.png")]
    [InlineData("  editorial/thumbs/PNMC-ED-100.png  ", "/editorial/thumbs/PNMC-ED-100.png")]
    [InlineData("https://www.mincultura.gov.co/portada.png", "https://www.mincultura.gov.co/portada.png")]
    public async Task La_ruta_de_la_portada_queda_referida_a_la_raiz_del_sitio(string escrita, string esperada)
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // ESTA PRUEBA NACE DE UN ACIERTO POR CASUALIDAD. Las portadas se veían con la ruta sin
        // barra inicial porque el navegador la resolvía contra `/editorial`; desde otra ruta
        // habría fallado, y la utilidad de miniaturas solo sustituye rutas absolutas, de modo que
        // el mosaico pedía el original entero. Lo que se fija aquí es que la barra no dependa de
        // cómo la teclee quien cataloga, y que una dirección externa no se toque.
        var creada = await client.PostAsJsonAsync(Consola, new
        {
            codigo = $"ED-RUTA-{escrita.GetHashCode(StringComparison.Ordinal):X8}",
            titulo = "Ficha con portada",
            miniaturaRuta = escrita,
        });

        var ficha = await creada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(esperada, ficha.GetProperty("miniaturaRuta").GetString());
    }

    // ─────────────────── Ayudas: escriben lo que aún no tiene pantalla ───────────────────

    private static async Task AgregarFuenteAsync(TestWebApplicationFactory factory, long publicacionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fuente = new FuenteEditorialRow
        {
            Nombre = "Catálogo institucional del Proyecto Editorial",
            Referencia = "Inventario 2026, folio 12",
            FechaConsulta = DateTime.UtcNow,
            VerificadaPor = "Equipo editorial",
            FechaCreacion = DateTime.UtcNow,
        };
        db.FuentesEditoriales.Add(fuente);
        await db.SaveChangesAsync();
        db.PublicacionesEditorialesFuentes.Add(new PublicacionEditorialFuenteRow
        {
            PublicacionEditorialId = publicacionId,
            FuenteEditorialId = fuente.Id,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Lo que el portal NO recibe, campo por campo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL CRITERIO ES EL DE UNA BIBLIOTECA, y lo fijó la dirección de producto de
    /// 2026: se publica lo que sirve para identificar, encontrar y conseguir una obra, y nada del
    /// trabajo de catalogarla. Esta prueba existe porque la frontera se había abierto sin que nadie
    /// lo decidiera: las dos rutas públicas devolvían el DTO de la consola con un interruptor que
    /// solo recortaba los accesos, así que un visitante anónimo recibía el estado de catalogación,
    /// el número de versión, el nombre de quien verificó los derechos y la procedencia del registro.
    /// </para>
    /// <para>
    /// SE ENUMERA LO PROHIBIDO Y NO LO PERMITIDO. Un aserto sobre la lista de campos que SÍ salen
    /// habría que actualizarlo cada vez que se publique uno nuevo, y acabaría actualizándose sin
    /// pensar. Así, añadir un campo público no toca esta prueba, y volver a filtrar uno interno la
    /// pone en rojo, que es justo el caso que hay que atrapar.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_portal_no_recibe_nada_del_trabajo_de_catalogar()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-PUB-1"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });
        await AgregarFuenteAsync(factory, id);
        await AutorizarFichaAsync(factory, id);
        await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });

        var anonimo = factory.CreateClient();
        var ficha = await (await anonimo.GetAsync($"{Publico}/ED-PUB-1")).Content.ReadFromJsonAsync<JsonElement>();

        string[] interno =
        [
            // El flujo de trabajo: es una afirmación del equipo sobre sí mismo.
            "estadoCatalogacion", "estadoPublicacion", "version",
            // La autoevaluación de quien cataloga.
            "confianza", "revisarClasificacion", "revisarCreditos", "notasCatalogacion",
            // Trazabilidad administrativa, incluido el identificador interno.
            "id", "procedencia", "diapositivaOrigen", "camposAdicionales",
            // NOMBRES DE FUNCIONARIOS: una biblioteca publica la licencia, no quién la comprobó.
            "derechos", "fuentes",
        ];
        foreach (var campo in interno)
        {
            Assert.False(ficha.TryGetProperty(campo, out _), $"El portal no debe recibir «{campo}».");
        }

        // Y lo que sí: la ficha bibliográfica sigue completa, empezando por su signatura.
        Assert.Equal("ED-PUB-1", ficha.GetProperty("codigo").GetString());
        Assert.Equal("Acento: arreglos para banda-escuela", ficha.GetProperty("titulo").GetString());
        Assert.Equal("Volumen I", ficha.GetProperty("subtitulo").GetString());
        // LA LICENCIA SI SE PUBLICA, que es lo que dice qué puede hacer quien consulta. Lo que se
        // queda dentro es quién la verificó y cuándo.
        Assert.True(ficha.TryGetProperty("licencia", out _));
    }

    /// <summary>
    /// El identificador se publica; el veredicto sobre su dígito de control, no.
    /// </summary>
    [Fact]
    public async Task El_ISBN_sale_al_portal_pero_su_validacion_se_queda_dentro()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-PUB-2"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada" });
        await AgregarFuenteAsync(factory, id);
        await AutorizarFichaAsync(factory, id);
        await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.IdentificadoresEditoriales.Add(new IdentificadorEditorialRow
            {
                PublicacionEditorialId = id,
                Esquema = "ISBN",
                CodigoRecibido = "978-958-753-005-6",
                Valido = false,
                ObservacionValidacion = "El dígito de control no cuadra.",
            });
            await db.SaveChangesAsync();
        }

        var anonimo = factory.CreateClient();
        var ficha = await (await anonimo.GetAsync($"{Publico}/ED-PUB-2")).Content.ReadFromJsonAsync<JsonElement>();
        var identificador = ficha.GetProperty("identificadores").EnumerateArray().Single();

        Assert.Equal("978-958-753-005-6", identificador.GetProperty("codigo").GetString());
        // CONTROL DE CALIDAD DEL CATALOGO, no dato de la obra: publicar «este ISBN no cuadra» deja
        // a quien consulta con una duda y sin nada con que resolverla.
        Assert.False(identificador.TryGetProperty("valido", out _));
        Assert.False(identificador.TryGetProperty("observacionValidacion", out _));
    }

    /// <summary>
    /// Cada decisión sobre la ficha deja constancia de quién y cuándo.
    /// </summary>
    /// <remarks>
    /// HASTA EL 13 DE SEPTIEMBRE DE 2026 EL CATALOGO NO ESCRIBIA NADA. Validar una ficha,
    /// publicarla o retirarla no dejaba rastro, aunque `dbo.RegistrosRevisionHistorial` —el hilo
    /// que ya usa el circuito de Festivales— existía desde mayo. A la pregunta «quién validó esto
    /// y por qué se retiró» no había respuesta.
    /// </remarks>
    [Fact]
    public async Task Cada_decision_sobre_la_ficha_queda_en_su_historial()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-HIST-1"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        await client.PostAsJsonAsync($"{Consola}/{id}/catalogacion", new { estado = "validada", motivo = "Contrastada con el ejemplar." });
        await AgregarFuenteAsync(factory, id);
        await AutorizarFichaAsync(factory, id);
        await client.PostAsJsonAsync($"{Consola}/{id}/publicacion", new { estado = "publicado" });

        var historial = await (await client.GetAsync($"{Consola}/{id}/historial")).Content.ReadFromJsonAsync<JsonElement>();
        var entradas = historial.EnumerateArray().ToList();

        Assert.Equal(2, entradas.Count);
        // EL MAS RECIENTE PRIMERO: un historial se lee por el final, que es donde está lo que pasó.
        Assert.Equal("PublicacionCambiada", entradas[0].GetProperty("accion").GetString());
        Assert.Equal("publicado", entradas[0].GetProperty("estadoNuevo").GetString());
        Assert.Equal("CatalogacionCambiada", entradas[1].GetProperty("accion").GetString());
        Assert.Equal("validada", entradas[1].GetProperty("estadoNuevo").GetString());
        // EL MOTIVO VIAJA COMO COMENTARIO: es lo que explica la decisión a quien la lea después.
        Assert.Equal("Contrastada con el ejemplar.", entradas[1].GetProperty("comentario").GetString());
        // Y QUIEN DECIDIO, EN LETRA: «usuario 12» obliga a ir a buscar quién es.
        Assert.False(string.IsNullOrWhiteSpace(entradas[1].GetProperty("usuarioNombre").GetString()));
    }

    /// <summary>
    /// Una anotación es una entrada más del mismo hilo, y no mueve ningún estado.
    /// </summary>
    [Fact]
    public async Task Una_anotacion_entra_en_el_hilo_sin_mover_la_ficha()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-HIST-2"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var respuesta = await client.PostAsJsonAsync($"{Consola}/{id}/anotaciones", new { comentario = "Falta el arreglista en los créditos." });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var historial = await (await client.GetAsync($"{Consola}/{id}/historial")).Content.ReadFromJsonAsync<JsonElement>();
        var entrada = historial.EnumerateArray().Single();

        Assert.Equal("Anotacion", entrada.GetProperty("accion").GetString());
        Assert.Equal("Falta el arreglista en los créditos.", entrada.GetProperty("comentario").GetString());
        // NO SE MOVIO NADA: el estado repetido deja dicho en qué situación estaba la ficha al
        // escribirse, que es información y no relleno.
        Assert.Equal("pendiente_revision", entrada.GetProperty("estadoAnterior").GetString());
        Assert.Equal("pendiente_revision", entrada.GetProperty("estadoNuevo").GetString());

        var ficha = await (await client.GetAsync($"{Consola}/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pendiente_revision", ficha.GetProperty("estadoCatalogacion").GetString());
    }

    /// <summary>
    /// Una anotación vacía no entra: no le dice nada a quien la lea después.
    /// </summary>
    [Fact]
    public async Task Una_anotacion_en_blanco_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-HIST-3"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var respuesta = await client.PostAsJsonAsync($"{Consola}/{id}/anotaciones", new { comentario = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// El hilo de trabajo es de la consola: el portal no lo recibe por ninguna vía.
    /// </summary>
    [Fact]
    public async Task El_historial_no_se_sirve_a_quien_no_ha_entrado()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, FichaNueva("ED-HIST-4"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var anonimo = factory.CreateClient();
        var respuesta = await anonimo.GetAsync($"{Consola}/{id}/historial");

        Assert.True(respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    private static async Task AutorizarFichaAsync(TestWebApplicationFactory factory, long publicacionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var ficha = await db.PublicacionesEditoriales.FirstAsync(x => x.Id == publicacionId);
        ficha.DerechosEstado = "verificado";
        ficha.DerechosPermitePublicarFicha = true;
        ficha.DerechosVerificadoPor = "Equipo editorial";
        ficha.DerechosFechaVerificacion = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
