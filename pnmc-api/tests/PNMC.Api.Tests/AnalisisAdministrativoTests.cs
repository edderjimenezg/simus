using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.ConsultaGuiada;
using PNMC.Api.Endpoints;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class AnalisisAdministrativoTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AnalisisAdministrativoTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Las_tres_rutas_exigen_sesion_institucional()
    {
        using var anonimo = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/admin/analisis/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/admin/analisis/tablero")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar", new PreguntaDeConsultaGuiadaDto { Pregunta = "resumen" })).StatusCode);
    }

    [Theory]
    [InlineData(CmsTestClient.WebmasterEmail, CmsTestClient.WebmasterPassword)]
    [InlineData("gestor@pnmc.local", "pnmc-gestor")]
    public async Task Los_dos_roles_internos_ven_el_catalogo(string correo, string clave)
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, correo, clave, withCsrf: false);

        var estado = await cliente.GetFromJsonAsync<EstadoDeConsultaGuiadaDto>("/api/v1/admin/analisis/estado");

        Assert.NotNull(estado);
        Assert.True(estado.Disponible);
        Assert.False(estado.ModeloLocalDisponible);
        Assert.Equal("deterministico", estado.Modo);
        // ONCE CONSULTAS MAS LA AYUDA: desde «ayuda» es una entrada del
        // catálogo como las demás y por tanto se anuncia, en vez de ser una rama escondida del
        // clasificador que solo encontraba quien ya sabía que existía. Las dos de Mercados entraron
        //: un módulo del Ecosistema también se consulta. «Ediciones por
        // año» y «Festivales por estado» entraron, con la lectura del
        // año y del estado dentro de la pregunta. La segunda es la única que ve lo que no está
        // publicado: las demás parten de la lectura pública.
        Assert.Equal(13, estado.Consultas.Count);
        Assert.Contains(estado.Consultas, consulta => consulta.Id == "ediciones_por_anio");
        Assert.Contains(estado.Consultas, consulta => consulta.Id == "festivales_por_estado");
        Assert.Contains(estado.Consultas, consulta => consulta.Id == "resumen_de_mercados");
        Assert.Contains(estado.Consultas, consulta => consulta.Id == "mercados_por_departamento");
        Assert.DoesNotContain(estado.Consultas, consulta => consulta.Id.Contains("news", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(estado.Consultas, consulta => consulta.Id.Contains("agenda", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_tablero_se_construye_con_datos_reales_aunque_no_haya_modelo()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var tablero = await cliente.GetFromJsonAsync<TableroAnalisisAdministrativoDto>("/api/v1/admin/analisis/tablero");

        Assert.NotNull(tablero);
        Assert.Contains(tablero.Indicadores, indicador => indicador.Id == "festivales" && indicador.Total == 1);
        Assert.Contains(tablero.Indicadores, indicador => indicador.Id == "organizaciones" && indicador.Total == 1);
        Assert.True(tablero.GeneradoEn > DateTime.UtcNow.AddMinutes(-1));
    }

    [Theory]
    [InlineData("¿Cómo está la plataforma hoy?", "resumen_administrativo")]
    [InlineData("¿Qué departamentos tienen más Festivales publicados?", "festivales_por_departamento")]
    [InlineData("¿Cuáles son los municipios con más festivales?", "municipios_con_mas_festivales")]
    [InlineData("¿Qué cobertura territorial tienen los Festivales?", "cobertura_de_festivales")]
    [InlineData("¿Dónde no hay Festivales publicados?", "territorios_sin_festival")]
    [InlineData("¿Qué prácticas musicales aparecen con mayor frecuencia?", "distribucion_tematica")]
    [InlineData("¿Cuántas solicitudes necesitan atención?", "pendientes_de_gestion")]
    [InlineData("¿Qué información está incompleta?", "calidad_de_datos")]
    public async Task Cada_pregunta_de_referencia_elige_una_consulta_cerrada(string pregunta, string esperada)
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuestaHttp = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = pregunta });
        respuestaHttp.EnsureSuccessStatusCode();
        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(respuesta);
        Assert.Equal(esperada, respuesta.ConsultaElegida);
        Assert.Equal("regla", respuesta.ResueltoPor);
        Assert.NotNull(respuesta.Consulta);
        Assert.False(string.IsNullOrWhiteSpace(respuesta.Consulta.Fuente));
        Assert.False(string.IsNullOrWhiteSpace(respuesta.Consulta.Alcance));
    }

    /// <summary>
    /// Nombrar un departamento acota la respuesta de verdad, y no solo la frase.
    /// </summary>
    /// <remarks>
    /// <b>ES LA PRUEBA DEL BLOQUE, Y MIRA LA TABLA Y NO EL IDENTIFICADOR.</b> Que el intérprete
    /// elija el resumen ya lo comprueba el banco de preguntas. Lo que aquí se fija es lo otro: que
    /// la tabla que llega, su título y su alcance hablen de Antioquia. Sin esto, una respuesta
    /// nacional presentada bajo una pregunta sobre un departamento seguiría pasando todas las
    /// puertas, que es exactamente el defecto que el bloque vino a corregir.
    /// </remarks>
    [Fact]
    public async Task Nombrar_un_departamento_acota_la_tabla_que_llega()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuestaHttp = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = "¿cuántos festivales hay en Antioquia?" });
        respuestaHttp.EnsureSuccessStatusCode();
        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(respuesta);
        Assert.Equal("resumen_administrativo", respuesta.ConsultaElegida);
        Assert.NotNull(respuesta.Consulta);
        Assert.Contains("Antioquia", respuesta.Consulta.Titulo, StringComparison.Ordinal);
        Assert.Contains("Antioquia", respuesta.Consulta.Alcance, StringComparison.Ordinal);
        Assert.Contains("Antioquia", respuesta.Respuesta, StringComparison.Ordinal);

        // Y EL ALCANCE QUE SE ENSEÑA ARRIBA DICE LO MISMO QUE LA TABLA. Al verlo en el navegador, la
        // respuesta llegaba titulada «Resumen de Antioquia» y justo encima decía «Calculado sobre:
        // Toda la operación»: una pantalla desmintiendo su propia tabla. El campo lo rellena ahora
        // la consulta que filtró, que es la única que sabe qué recortó de verdad.
        Assert.Equal("Antioquia", respuesta.Contexto, StringComparer.Ordinal);

        // LA TABLA NACIONAL TIENE SEIS FILAS y la acotada tres: las suscripciones al Boletín y los
        // asuntos pendientes no tienen territorio y se caen en vez de colarse como si fueran de allí.
        Assert.Equal(3, respuesta.Consulta.Filas.Count);
        Assert.DoesNotContain(respuesta.Consulta.Filas, fila => fila[0].Contains("Suscripciones", StringComparison.Ordinal));
    }

    /// <summary>
    /// Lo que se entendió y no se pudo aplicar se dice, en vez de callarse.
    /// </summary>
    /// <remarks>
    /// QUIEN ESCRIBE «EN 2024» Y RECIBE UNA CIFRA SIN ADVERTENCIA concluye que es la de 2024. Los
    /// pendientes viven en bandejas institucionales y no tienen año ni territorio; la respuesta
    /// tiene que decirlo.
    /// </remarks>
    [Fact]
    public async Task Un_ano_que_la_consulta_no_sabe_usar_se_declara_en_la_respuesta()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuestaHttp = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = "¿qué solicitudes quedaron pendientes en 2024?" });
        respuestaHttp.EnsureSuccessStatusCode();
        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(respuesta);
        Assert.Equal("pendientes_de_gestion", respuesta.ConsultaElegida);
        Assert.Contains("no se acota al año 2024", respuesta.Respuesta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lo que no está publicado deja de ser invisible para el asistente.
    /// </summary>
    /// <remarks>
    /// <b>ERA EL HUECO MAS GRANDE DEL CATALOGO.</b> Todas las consultas de Festivales parten de la
    /// lectura pública, así que un Festival en revisión no aparecía en ninguna respuesta: preguntar
    /// «¿cuántos hay en revisión?» devolvía el resumen general y quien lo leía no tenía forma de
    /// saber que la cifra que buscaba no estaba ahí.
    /// </remarks>
    [Fact]
    public async Task El_estado_de_la_pregunta_acota_y_ve_lo_que_no_esta_publicado()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.FestivalRecords.Add(new FestivalRow
            {
                Name = "Festival esperando revisión", CoverageLevel = "municipal", DepartmentCode = "05",
                MunicipalityCode = "05001", StatusCode = EstadosFestival.EnRevision, 
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿cuántos festivales hay en revisión?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(cuerpo);
        Assert.Contains("En revisión", cuerpo!.Respuesta, StringComparison.Ordinal);
        Assert.Contains("1 Festival en estado", cuerpo.Respuesta, StringComparison.Ordinal);
        Assert.NotNull(cuerpo.Consulta);
        Assert.Single(cuerpo.Consulta!.Filas);
        Assert.Contains("incluye lo que no está publicado", cuerpo.Consulta.Alcance, StringComparison.Ordinal);
    }

    /// <summary>
    /// «¿Cuántas ediciones hubo?» no dice de qué proceso, y la respuesta cuenta los dos.
    /// </summary>
    /// <remarks>
    /// <b>CONTAR SOLO LAS DE FESTIVAL ERA DAR MEDIA RESPUESTA</b> con la autoridad de una tabla
    /// completa. Van en columnas separadas porque son dos cosas distintas —una Edición de mercado no
    /// es una Edición de Festival— y sumarlas escondería cuál es cuál.
    /// </remarks>
    [Fact]
    public async Task Las_ediciones_por_ano_cuentan_las_de_Festival_y_las_de_mercado()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var entidad = new EntityProfileRow
            {
                EntityType = "organizacion", Name = "Organización de las ediciones", StatusCode = "activa",
                IsActive = true, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow,
            };
            db.EntityProfiles.Add(entidad);
            await db.SaveChangesAsync();

            var festival = new FestivalRow
            {
                Name = "Festival con edición", CoverageLevel = "municipal", DepartmentCode = "05",
                MunicipalityCode = "05001", StatusCode = EstadosFestival.Publicado,
                OrganizacionPrincipalId = entidad.Id, CreatedAt = DateTime.UtcNow,
            };
            db.FestivalRecords.Add(festival);
            var mercado = new MercadoRow
            {
                Nombre = "Mercado con edición", NivelCobertura = "municipal", CodigoDepartamento = "05",
                CodigoMunicipio = "05001", OrganizacionPrincipalId = entidad.Id, EstadoRegistro = "publicado",
                Activo = true, IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            };
            db.Mercados.Add(mercado);
            await db.SaveChangesAsync();

            db.EdicionesFestival.Add(new EdicionFestivalRow { FestivalId = festival.Id, Anio = 2024 });
            db.EdicionesMercado.Add(new EdicionMercadoRow
            {
                MercadoId = mercado.Id, Anio = 2024, IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿cuántas ediciones hubo en 2024?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(cuerpo);
        Assert.Equal("ediciones_por_anio", cuerpo!.ConsultaElegida);
        Assert.Contains("1 de Festival y 1 de mercado musical", cuerpo.Respuesta, StringComparison.Ordinal);
        Assert.NotNull(cuerpo.Consulta);
        Assert.Equal(["Año", "De Festival", "De mercado"], cuerpo.Consulta!.Columnas);
        Assert.Equal(["2024", "1", "1"], cuerpo.Consulta.Filas.Single());
    }

    /// <summary>
    /// Lo que falta por hacer se pregunta en negativo, y se cuenta al revés.
    /// </summary>
    /// <remarks>
    /// <b>ES LA PREGUNTA QUE MIDE EL TRABAJO PENDIENTE</b>, y para contestarla hicieron falta dos
    /// cosas a la vez: que «publicar» y «publicado» fueran la misma palabra para el reconocedor de
    /// raíces, y que una negación pegada al estado lo invirtiera. Sin las dos, «sin publicar» no
    /// alcanzaba ningún estado.
    /// </remarks>
    [Fact]
    public async Task Un_estado_negado_cuenta_lo_que_se_quedo_fuera()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.FestivalRecords.Add(new FestivalRow
            {
                Name = "Borrador que no se ha enviado", CoverageLevel = "municipal", DepartmentCode = "05",
                MunicipalityCode = "05001", StatusCode = EstadosFestival.Borrador, 
                CreatedAt = DateTime.UtcNow,
            });
            db.FestivalRecords.Add(new FestivalRow
            {
                Name = "Publicado y visible", CoverageLevel = "municipal", DepartmentCode = "05",
                MunicipalityCode = "05001", StatusCode = EstadosFestival.Publicado, 
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿qué festivales no están publicados?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(cuerpo);
        Assert.Equal("festivales_por_estado", cuerpo!.ConsultaElegida);
        Assert.Contains("no están en estado «Publicado»", cuerpo.Respuesta, StringComparison.Ordinal);
        // EL DE LA SEMILLA DE LA FABRICA NO TIENE ESTADO, así que cuenta como no publicado: son dos.
        Assert.Contains("2 Festivales", cuerpo.Respuesta, StringComparison.Ordinal);
    }

    /// <summary>
    /// «Borrador» pesa más que «mercado», y aun así la pregunta es de mercados.
    /// </summary>
    /// <remarks>
    /// SIN LA FRONTERA DECLARADA, esta pregunta recibía una cifra de Festivales. Es la clase de
    /// error más cara del asistente: la respuesta llega con su tabla y su fuente, y nada en ella
    /// dice que está contando otra cosa.
    /// </remarks>
    [Fact]
    public async Task Una_pregunta_de_mercados_con_un_estado_dentro_sigue_siendo_de_mercados()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var entidad = new EntityProfileRow
            {
                EntityType = "organizacion", Name = "Organización de mercados", StatusCode = "activa",
                IsActive = true, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow,
            };
            db.EntityProfiles.Add(entidad);
            await db.SaveChangesAsync();
            db.Mercados.Add(new MercadoRow
            {
                Nombre = "Mercado en borrador", NivelCobertura = "municipal", CodigoDepartamento = "05",
                CodigoMunicipio = "05001", OrganizacionPrincipalId = entidad.Id, EstadoRegistro = "borrador",
                Activo = true, IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿cuántos mercados hay en borrador?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(cuerpo);
        Assert.Equal("resumen_de_mercados", cuerpo!.ConsultaElegida);
        Assert.Contains("1 mercado musical en estado «Borrador»", cuerpo.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_pregunta_fuera_del_catalogo_no_inventa_una_consulta()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuestaHttp = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = "Escribe una noticia sobre el próximo concierto" });
        respuestaHttp.EnsureSuccessStatusCode();
        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(respuesta);
        Assert.Equal(string.Empty, respuesta.ConsultaElegida);
        Assert.Equal("sin_coincidencia", respuesta.ResueltoPor);
        Assert.Null(respuesta.Consulta);
    }

    [Fact]
    public async Task La_respuesta_no_filtra_los_datos_personales_sembrados()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuestaHttp = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = "¿Qué información está incompleta?" });
        respuestaHttp.EnsureSuccessStatusCode();
        var json = await respuestaHttp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("test@pnmc.local", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gestor@pnmc.local", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Usuario Prueba", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correoElectronico", json, StringComparison.OrdinalIgnoreCase);

        using var documento = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, documento.RootElement.ValueKind);
    }

    [Fact]
    public async Task La_pregunta_vacia_o_demasiado_larga_se_rechaza_antes_de_consultar()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var vacia = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = "   " });
        var larga = await cliente.PostAsJsonAsync(
            "/api/v1/admin/analisis/consultar",
            new PreguntaDeConsultaGuiadaDto { Pregunta = new string('a', 1_001) });

        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, larga.StatusCode);
    }

    // ─── Mercados en la Consulta Guiada (15 de septiembre de 2026) ───

    /// <summary>
    /// El asistente sabe de Mercados, y sus dos preguntas no se las lleva Festivales.
    /// </summary>
    /// <remarks>
    /// <b>EL ORDEN DEL CATALOGO ES LO QUE SE PRUEBA.</b> «¿En qué departamentos hay mercados?»
    /// comparte la palabra «departament» con «Festivales por departamento», que estaba antes: sin
    /// poner las de Mercados delante, la pregunta se respondía con los Festivales y nadie lo notaba
    /// porque la respuesta era una tabla plausible.
    /// </remarks>
    [Theory]
    [InlineData("¿Cuántos mercados musicales hay publicados?", "resumen_de_mercados")]
    [InlineData("¿En qué departamentos hay mercados musicales?", "mercados_por_departamento")]
    [InlineData("¿Qué departamentos tienen más Festivales publicados?", "festivales_por_departamento")]
    public async Task Las_preguntas_sobre_mercados_eligen_la_consulta_de_mercados(string pregunta, string esperada)
    {
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(AmbitoDeConsulta.Institucional);

        var elegida = await interprete.InterpretarAsync(
            pregunta, disponibles, EntidadesDeLaPregunta.Ninguna, CancellationToken.None);

        Assert.NotNull(elegida);
        Assert.Equal(esperada, elegida!.Consulta.Id);
    }

    [Fact]
    public async Task El_resumen_de_mercados_cuenta_los_publicados_y_los_que_van_dentro_de_un_festival()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        int organizacion, festival;
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var entidad = new EntityProfileRow
            {
                EntityType = "organizacion", Name = "Organización de la consulta guiada", CoverageLevel = "municipal",
                DepartmentCode = "05", MunicipalityCode = "05001", StatusCode = "activa", IsActive = true,
                CreatedByUserId = 1, CreatedAt = DateTime.UtcNow,
            };
            db.EntityProfiles.Add(entidad);
            await db.SaveChangesAsync();
            organizacion = entidad.Id;
            var fila = new FestivalRow
            {
                Name = "Festival de la consulta guiada", CoverageLevel = "municipal", DepartmentCode = "05",
                MunicipalityCode = "05001", StatusCode = EstadosFestival.Publicado, OrganizacionPrincipalId = organizacion,
                CreatedAt = DateTime.UtcNow,
            };
            db.FestivalRecords.Add(fila);
            await db.SaveChangesAsync();
            festival = fila.Id;

            db.Mercados.Add(new MercadoRow
            {
                Nombre = "Mercado publicado en festival", NivelCobertura = "municipal", CodigoDepartamento = "05",
                CodigoMunicipio = "05001", OrganizacionPrincipalId = organizacion, EstadoRegistro = "publicado",
                Activo = true, SeRealizaEnElMarcoDeUnFestival = true, FestivalId = festival,
                IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            db.Mercados.Add(new MercadoRow
            {
                Nombre = "Mercado en borrador", NivelCobertura = "municipal", CodigoDepartamento = "05",
                CodigoMunicipio = "05001", OrganizacionPrincipalId = organizacion, EstadoRegistro = "borrador",
                Activo = true, IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿Cuántos mercados musicales hay publicados?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.NotNull(cuerpo);
        Assert.Equal("resumen_de_mercados", cuerpo!.ConsultaElegida);
        // EL BORRADOR CUENTA COMO REGISTRADO Y NO COMO PUBLICADO: son dos cifras distintas y la
        // respuesta las dice por separado, como hace el resumen de Festivales.
        Assert.Contains("2 mercados musicales registrados", cuerpo.Respuesta, StringComparison.Ordinal);
        // LA CIFRA MANDA SOBRE EL VERBO: «1 publicados» y «1 se realizan» se leen como un error del
        // sistema y hacen dudar del resto de la respuesta.
        Assert.Contains("1 publicado.", cuerpo.Respuesta, StringComparison.Ordinal);
        Assert.Contains("1 se realiza en el marco de un festival", cuerpo.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("1 publicados", cuerpo.Respuesta, StringComparison.Ordinal);
        // Y la fuente se declara, como toda respuesta de datos del asistente.
        Assert.Contains("dbo.Mercados", cuerpo.Consulta!.Fuente, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_mercados_por_departamento_solo_cuentan_los_publicados()
    {
        await using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        using (var alcance = factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var entidad = new EntityProfileRow
            {
                EntityType = "organizacion", Name = "Organización territorial", CoverageLevel = "municipal",
                DepartmentCode = "91", MunicipalityCode = "91001", StatusCode = "activa", IsActive = true,
                CreatedByUserId = 1, CreatedAt = DateTime.UtcNow,
            };
            db.EntityProfiles.Add(entidad);
            await db.SaveChangesAsync();
            db.Mercados.Add(new MercadoRow
            {
                Nombre = "Mercado del Amazonas", NivelCobertura = "departamental", CodigoDepartamento = "91",
                OrganizacionPrincipalId = entidad.Id, EstadoRegistro = "publicado", Activo = true,
                IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            db.Mercados.Add(new MercadoRow
            {
                Nombre = "Mercado sin publicar del Amazonas", NivelCobertura = "departamental", CodigoDepartamento = "91",
                OrganizacionPrincipalId = entidad.Id, EstadoRegistro = "en_revision", Activo = true,
                IdUsuarioCreador = 1, FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/analisis/consultar",
            new { pregunta = "¿En qué departamentos hay mercados musicales?" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDeConsultaGuiadaDto>();

        Assert.Equal("mercados_por_departamento", cuerpo!.ConsultaElegida);
        // MUTANTE QUE MATA: contar todos los activos. El que está en revisión no es público todavía,
        // y la lectura territorial del asistente es la misma que la del portal.
        //
        // SE MIRA LA UNICA FILA Y SU CIFRA, NO EL NOMBRE DEL DEPARTAMENTO: en la base de prueba
        // DIVIPOLA puede no estar sembrada, y entonces la fila se rotula con el código «91», que es
        // el respaldo honesto. Lo que esta prueba vigila es el conteo, no el rótulo.
        var fila = Assert.Single(cuerpo.Consulta!.Filas);
        Assert.Equal("1", fila[1]);
    }

}
