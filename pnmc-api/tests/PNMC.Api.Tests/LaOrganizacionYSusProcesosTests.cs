using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La organización es un actor, y cerrarla tiene que resolver qué pasa con sus procesos.
/// </summary>
/// <remarks>
/// <para>
/// <b>DE DÓNDE SALEN ESTAS PRUEBAS.</b> Al contrastar el modelo de organizaciones del desarrollo de
/// septiembre con el de esta base aparecieron dos cosas mal planteadas que sobrevivían. La primera:
/// <c>Entidades.EstadoRegistro</c> usaba el vocabulario de un <i>contenido publicable</i> —ocho
/// estados, seis inertes— sobre un <i>actor</i> que no se publica ni se aprueba. La segunda, más
/// cara: cerrar una organización le cortaba el acceso —eso ya lo arregló el corte anterior— pero
/// dejaba sus Festivales publicados en la página pública sin nadie que pudiera editarlos, recibir
/// propuestas de cambio ni reclamarlos, porque solo se puede reclamar lo que está en custodia del
/// Programa.
/// </para>
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN: que el estado de una organización vuelva a prometer lo que no hay,
/// y que un cierre vuelva a producir procesos huérfanos en silencio.
/// </para>
/// </remarks>
public sealed class LaOrganizacionYSusProcesosTests
{
    private const string Clave = "ClaveExterna123";

    // ---------- El ciclo de vida ---------------------------------------------------------------

    [Fact]
    public async Task Un_estado_de_contenido_ya_no_vale_para_una_organizacion()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (_, organizacionId) = await OrganizacionAsync(factory, "estado.contenido@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        var respuesta = await consola.PostAsJsonAsync(
            $"/api/v1/admin/organizaciones/{organizacionId}/estado", new { estado = "publicado" });

        // NO EXISTE PAGINA PUBLICA DE ORGANIZACION que «publicado» publique. Una organización no se
        // publica ni se aprueba: se registra, se activa, se desactiva o se elimina.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Nace_pendiente_de_confirmacion_y_no_registra_procesos()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "sin.confirmar@example.com");

        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival sin correo comprobado",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });

        // LA PUERTA SE MOVIO ANTES. Hasta se podía crear el registro y el
        // bloqueo aparecía al enviarlo a revisión; el plan de consolidación lo endureció —«sin correo
        // confirmado no se crean nuevos procesos»— porque descubrir el muro después de escribir el
        // Festival entero es peor que encontrarlo al principio.
        Assert.Equal(HttpStatusCode.Conflict, creado.StatusCode);
    }

    [Fact]
    public async Task El_bloqueo_dice_que_falta_por_que_que_hacer_y_que_se_habilita()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "explicame@example.com");

        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival que no llega a existir",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });

        // «NO TIENE PERMISOS» ES LA RESPUESTA QUE NO SIRVE: quien la lee sí administra la
        // organización, y se quedaría buscando un rol que no le falta. Las cuatro piezas viajan en
        // campos separados para que la pantalla no tenga que trocear una frase.
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("correo_sin_confirmar", cuerpo.GetProperty("motivo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("queFalta").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("porQue").GetString()));
        Assert.Contains("explicame@example.com", cuerpo.GetProperty("queHacer").GetString(), StringComparison.Ordinal);
        Assert.Contains("registrar Festivales", cuerpo.GetProperty("queSeHabilita").GetString(), StringComparison.Ordinal);
        Assert.Equal("explicame@example.com", cuerpo.GetProperty("correoDestino").GetString());
    }

    [Fact]
    public async Task Corregir_el_correo_manda_el_enlace_a_la_direccion_nueva_y_vence_el_anterior()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, _) = await OrganizacionAsync(factory, "erata@example.com");
        var viejoTestigo = await TestigoDeConfirmacionAsync(factory, "erata@example.com");

        var corregido = await cliente.PutAsJsonAsync("/api/v1/externo/cuenta/correo", new { correo = "errata@example.com" });
        corregido.EnsureSuccessStatusCode();

        // SIN ESTA RUTA UNA LETRA DE MAS DEJA LA CUENTA MUERTA: el correo es único y es el de la
        // organización, así que ni el restablecimiento de contraseña llega a ninguna parte.
        var testigoNuevo = await TestigoDeConfirmacionAsync(factory, "errata@example.com");
        Assert.NotEqual(viejoTestigo, testigoNuevo);

        // Y EL ENLACE QUE FUE A LA DIRECCION EQUIVOCADA DEJA DE SERVIR EN EL MISMO MOMENTO.
        var conElViejo = await cliente.PostAsJsonAsync("/api/v1/externo/auth/confirmar-correo", new { testigo = viejoTestigo });
        Assert.Equal(HttpStatusCode.BadRequest, conElViejo.StatusCode);

        var conElNuevo = await cliente.PostAsJsonAsync("/api/v1/externo/auth/confirmar-correo", new { testigo = testigoNuevo });
        conElNuevo.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Confirmar_el_correo_activa_la_organizacion_y_le_abre_la_entrega()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "confirma@example.com");

        var testigo = await TestigoDeConfirmacionAsync(factory, "confirma@example.com");
        var confirmada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/confirmar-correo", new { testigo });
        confirmada.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);
            // CONFIRMAR EL CORREO ES LO QUE ACTIVA LA ORGANIZACION: no hace falta que nadie de la
            // consola la toque. Es la única transición que la propia organización puede provocar.
            Assert.Equal("activa", organizacion.StatusCode);
            Assert.True(organizacion.IsActive);
        }

        // Y CON EL CORREO COMPROBADO, EL CAMINO ENTERO SE ABRE DE UNA VEZ: registrar el proceso y
        // entregárselo al Programa. Las dos puertas son la misma función y la misma condición.
        var festivalId = await FestivalAsync(cliente, organizacionId);
        var envio = await EnviarAsync(cliente, $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
    }

    [Fact]
    public async Task El_enlace_de_confirmacion_queda_en_la_cola_de_salida_sin_darse_por_enviado()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        await OrganizacionAsync(factory, "cola.de.salida@example.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var salida = await db.Notifications.AsNoTracking()
            .SingleAsync(n => n.RecipientEmail == "cola.de.salida@example.com" && n.Channel == "email");

        // NO HAY PROVEEDOR DE CORREO TODAVIA, y el sistema lo dice en vez de fingirlo: el mensaje
        // existe, con su enlace, y su estado es `pendiente` con `SentAt` en nulo. Marcarlo «enviada»
        // afirmaría una entrega que nadie hizo, y el día que se conecte el proveedor estas filas son
        // exactamente la cola que hay que drenar.
        Assert.Equal("pendiente", salida.Status);
        Assert.Null(salida.SentAt);
        Assert.Contains("/ecosistema/confirmar-correo?testigo=", salida.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_enlace_vencido_lo_dice_y_no_confirma()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, _) = await OrganizacionAsync(factory, "vencido@example.com");
        var testigo = await TestigoDeConfirmacionAsync(factory, "vencido@example.com");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = await db.ConfirmacionesDeCorreo.SingleAsync();
            fila.FechaExpiracion = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/externo/auth/confirmar-correo", new { testigo });

        // «VENCIO» Y «NO EXISTE» PIDEN DOS COSAS DISTINTAS a quien abre el enlace —pedir otro, o
        // revisar de dónde salió—, y un «no válido» genérico obliga a adivinar cuál de las dos es.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("venció", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reenviar_invalida_el_enlace_anterior()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, _) = await OrganizacionAsync(factory, "reenvio@example.com");
        var primero = await TestigoDeConfirmacionAsync(factory, "reenvio@example.com");

        (await EnviarAsync(cliente, "/api/v1/externo/cuenta/reenviar-confirmacion", new { })).EnsureSuccessStatusCode();

        var conElViejo = await cliente.PostAsJsonAsync("/api/v1/externo/auth/confirmar-correo", new { testigo = primero });

        // SI «REENVIAR» DEJARA VIVO EL ANTERIOR, cambiar de correo no cerraría el acceso del
        // anterior, que es la mitad del problema que esto viene a resolver.
        Assert.Equal(HttpStatusCode.BadRequest, conElViejo.StatusCode);
    }

    [Fact]
    public async Task Desactivarla_le_corta_el_acceso_y_reactivarla_se_lo_devuelve()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "vaiven@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        (await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "inactiva",
            motivo = "Suspendida mientras se aclara su documentación.",
        })).EnsureSuccessStatusCode();

        var bloqueada = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival tras la baja",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);

        (await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado",
            new { estado = "activa" })).EnsureSuccessStatusCode();

        // INACTIVA ES REVERSIBLE Y ESA ES SU RAZON DE SER: la organización conserva su ficha, sus
        // procesos y sus cuentas, y volver no cuesta más que un acto de la consola.
        var devuelta = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival tras reactivar",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.Created, devuelta.StatusCode);
    }

    [Fact]
    public async Task El_estado_y_la_vigencia_no_pueden_discrepar()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (_, organizacionId) = await OrganizacionAsync(factory, "coherente@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        (await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "inactiva",
            motivo = "Prueba de coherencia.",
        })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);

        // UN SOLO EJE. `Activo` ya no es una decisión aparte: se deriva del estado, y la base lo
        // impone con CK_Entidades_VigenciaCoherente. Mientras pudieran discrepar existía una
        // organización «eliminada» que seguía entrando, y nadie la encontraba porque cada pantalla
        // miraba una de las dos columnas.
        Assert.Equal("inactiva", organizacion.StatusCode);
        Assert.False(organizacion.IsActive);
    }

    // ---------- Cerrarla resuelve sus procesos ------------------------------------------------

    [Fact]
    public async Task Eliminarla_con_un_festival_publicado_se_rechaza_y_lo_nombra()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "cierre.publicado@example.com");
        var festivalId = await FestivalAsync(cliente, organizacionId);
        await PublicarAsync(factory, festivalId);

        var consola = await CmsTestClient.LoginAsync(factory);
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "eliminada",
            motivo = "La organización se disolvió.",
        });

        // UN FESTIVAL PUBLICADO SIN ADMINISTRADORA VIVA es un registro que sigue en la página pública
        // y que ya nadie puede editar ni reclamar. Se rechaza, y se dice cuál.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var nombrados = cuerpo.GetProperty("procesos").EnumerateArray()
            .Select(proceso => proceso.GetProperty("id").GetString())
            .ToList();
        Assert.Contains(festivalId.ToString(CultureInfo.InvariantCulture), nombrados);
    }

    [Fact]
    public async Task Archivar_sus_procesos_los_saca_del_sitio_publico_sin_cambiar_de_custodia()
    {
        // LA SEGUNDA SALIDA, Y NO ES LA MISMA QUE LIBERAR. Liberar dice «este proceso sigue vivo y
        // ahora responde el Programa»; archivar dice «este proceso terminó con la organización».
        // Hasta solo existía la primera, así que cerrar una fundación
        // disuelta dejaba su festival publicado a nombre del Programa esperando a que alguien lo
        // reclamara. La dirección de producto pidió las dos: «la opción de también eliminar los
        // procesos registrados o liberarlos».
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "cierre.archiva@example.com");
        var festivalId = await FestivalAsync(cliente, organizacionId);
        await PublicarAsync(factory, festivalId);

        var consola = await CmsTestClient.LoginAsync(factory);
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "eliminada",
            motivo = "La fundación se disolvió y el festival no vuelve a celebrarse.",
            queHacerConLosProcesos = "archivar",
        });

        respuesta.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
        var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);

        Assert.Equal("archivado", festival.StatusCode);
        Assert.Equal("eliminada", organizacion.StatusCode);

        // LA CUSTODIA NO CAMBIA DE MANOS: el proceso sigue siendo de quien lo registró, que es lo
        // que hay que poder responder años después. Archivar no es un traspaso.
        Assert.Equal(organizacionId, festival.OrganizacionPrincipalId);
        Assert.False(await db.AdministrationTransfers.AnyAsync(
            x => x.CanonicalRecordId == festivalId.ToString(CultureInfo.InvariantCulture)));

        // Y NO BORRA NADA: el registro sigue ahí con su bitácora.
        Assert.True(await db.AuditLogs.AnyAsync(
            x => x.TableName == "Festivales"
                && x.RecordId == festivalId.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Liberar_sus_procesos_deja_cerrarla_y_los_devuelve_al_programa()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "cierre.libera@example.com");
        var festivalId = await FestivalAsync(cliente, organizacionId);
        await PublicarAsync(factory, festivalId);

        var consola = await CmsTestClient.LoginAsync(factory);
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "eliminada",
            motivo = "La organización se disolvió; sus procesos vuelven al Programa.",
            queHacerConLosProcesos = "liberar",
        });

        respuesta.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var institucional = await db.EntityProfiles.Where(x => x.IsInstitutional).Select(x => x.Id).SingleAsync();
        var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
        var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);

        // LA CUSTODIA VUELVE AL PROGRAMA, que es donde otra organización puede reclamarla. El
        // mecanismo ya existía; lo que faltaba era obligar a usarlo antes de cerrar.
        Assert.Equal(institucional, festival.OrganizacionPrincipalId);
        Assert.Equal("eliminada", organizacion.StatusCode);
        Assert.False(organizacion.IsActive);

        // Y DEJA RASTRO EN EL HISTORIAL DE ADMINISTRACION, no solo en la bitácora: sin esa fila, la
        // ficha del Festival enseñaría quién lo reclamó después y no cómo llegó a estar libre.
        Assert.True(await db.AdministrationTransfers.AnyAsync(
            x => x.CanonicalRecordId == festivalId.ToString(CultureInfo.InvariantCulture)
                && x.PreviousOrganizationId == organizacionId
                && x.NewOrganizationId == institucional));
    }

    [Fact]
    public async Task Un_festival_en_borrador_no_bloquea_el_cierre()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "cierre.borrador@example.com");
        await FestivalAsync(cliente, organizacionId);

        var consola = await CmsTestClient.LoginAsync(factory);
        var respuesta = await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "inactiva",
            motivo = "La organización pidió su baja antes de publicar nada.",
        });

        // UN BORRADOR NO ES PUBLICO NI TIENE NADA PENDIENTE DEL PROGRAMA: no deja a nadie sin
        // respuesta. Bloquear el cierre por él sería obligar a liberar lo que nadie ha visto.
        respuesta.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task La_ficha_dice_en_que_estado_esta_cada_proceso()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "ficha.procesos@example.com");
        var festivalId = await FestivalAsync(cliente, organizacionId);
        await PublicarAsync(factory, festivalId);

        var consola = await CmsTestClient.LoginAsync(factory);
        var ficha = await consola.GetFromJsonAsync<JsonElement>($"/api/v1/admin/organizaciones/{organizacionId}");

        // «3 PROCESOS» NO DICE NADA. Quien va a archivar una organización necesita saber si alguno de
        // ellos está publicado, y la ficha enseñaba solo una lista de nombres.
        var proceso = ficha.GetProperty("festivales").EnumerateArray().Single();
        Assert.Equal("publicado", proceso.GetProperty("estado").GetString());
        Assert.True(proceso.GetProperty("dejaHuerfano").GetBoolean());
        Assert.Equal(1, ficha.GetProperty("organizacion").GetProperty("procesos").GetProperty("dependientes").GetInt32());
    }

    // ---------- La confirmación que hace el Programa --------------------------------------------

    [Fact]
    public async Task La_consola_confirma_el_correo_con_motivo_y_eso_activa_la_organizacion()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "por.telefono@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        var respuesta = await consola.PostAsJsonAsync(
            $"/api/v1/admin/organizaciones/{organizacionId}/confirmar-correo",
            new { motivo = "Verificado por teléfono con la directora el 12 de septiembre." });

        respuesta.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var cuenta = await db.Users.SingleAsync(x => x.Email == "por.telefono@example.com");
            var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);

            Assert.True(cuenta.CorreoConfirmado);
            Assert.Equal("activa", organizacion.StatusCode);

            // EL MOTIVO ES LA MITAD DEL ACTO, y sin él en la bitácora dentro de seis meses esto solo
            // diría que alguien lo dio por bueno. Es lo que separa una comprobación real de un clic.
            var rastro = await db.AuditLogs.AsNoTracking()
                .Where(x => x.TableName == "Usuarios" && x.RecordId == cuenta.Id.ToString(CultureInfo.InvariantCulture))
                .OrderByDescending(x => x.Id)
                .FirstAsync();
            // SE LEE EL CAMPO, NO LA CADENA: el serializador escapa los acentos, y buscar la frase
            // en crudo comprobaría cómo se escribe el JSON en vez de qué guarda.
            var valores = JsonDocument.Parse(rastro.NewValuesJson!).RootElement;
            Assert.Equal(
                "Verificado por teléfono con la directora el 12 de septiembre.",
                valores.GetProperty("motivo").GetString());
            Assert.Equal("consola", valores.GetProperty("comprobadoPor").GetString());
            Assert.NotEqual(0, rastro.UserId);
        }

        // Y LA PUERTA SE ABRE DE VERDAD: no es un sello, es la misma condición que mira el registro.
        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival de la que llamó por teléfono",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
    }

    [Fact]
    public async Task La_consola_no_confirma_sin_decir_como_lo_comprobo()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (_, organizacionId) = await OrganizacionAsync(factory, "sin.motivo@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        var respuesta = await consola.PostAsJsonAsync(
            $"/api/v1/admin/organizaciones/{organizacionId}/confirmar-correo", new { motivo = "ok" });

        // UN BYPASS SE DISTINGUE DE UN ACTO REGISTRADO EN QUE EL ACTO DICE POR QUE. Sin motivo esto
        // sería exactamente el «mecanismo permanente de simulación» que el plan prohíbe.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Activar_a_mano_sin_correo_comprobado_se_rechaza_y_dice_por_donde()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (_, organizacionId) = await OrganizacionAsync(factory, "atajo@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        var respuesta = await consola.PostAsJsonAsync(
            $"/api/v1/admin/organizaciones/{organizacionId}/estado", new { estado = "activa" });

        // «ACTIVAR» NO PUEDE SER LA PUERTA DE ATRAS DE LA CONFIRMACION: dejaría el sello diciendo
        // «activa» con la dirección de contacto sin comprobar, que es justo lo que la regla evita.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reactivar_una_inactiva_sin_correo_comprobado_la_devuelve_a_pendiente()
    {
        await using var factory = new TestWebApplicationFactory { ConfirmaElCorreoAlRegistrar = false };
        var (_, organizacionId) = await OrganizacionAsync(factory, "vuelve.a.pendiente@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        (await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "inactiva",
            motivo = "Suspendida mientras se aclara su documentación.",
        })).EnsureSuccessStatusCode();

        var respuesta = await consola.PostAsJsonAsync(
            $"/api/v1/admin/organizaciones/{organizacionId}/estado", new { estado = "activa" });

        // DESHACER UN CIERRE NO PUEDE QUEDAR BLOQUEADO. Se le devuelve el acceso y cae en el estado
        // que le corresponde por su correo, que no es «activa»: es «pendiente de confirmación».
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pendiente_de_confirmacion", cuerpo.GetProperty("estado").GetString());
        Assert.True(cuerpo.GetProperty("activa").GetBoolean());
    }

    // ---------- Andamio -----------------------------------------------------------------------

    /// <summary>Lee el testigo del enlace que quedó en la cola de salida.</summary>
    /// <remarks>
    /// SE LEE DEL MENSAJE Y NO DE LA BASE DE TESTIGOS, que guarda el hash: es lo mismo que haría
    /// quien abre su correo, y de paso comprueba que el enlace que se manda es el que sirve.
    /// </remarks>
    private static async Task<string> TestigoDeConfirmacionAsync(TestWebApplicationFactory factory, string correo)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var mensaje = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientEmail == correo && n.Channel == "email")
            .OrderByDescending(n => n.Id)
            .FirstAsync();
        var marca = "?testigo=";
        var desde = mensaje.Body.IndexOf(marca, StringComparison.Ordinal) + marca.Length;
        var hasta = mensaje.Body.IndexOf('\n', desde);
        return mensaje.Body[desde..(hasta < 0 ? mensaje.Body.Length : hasta)].Trim();
    }

    private static async Task<int> FestivalAsync(HttpClient cliente, int organizacionId)
    {
        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival de la organización",
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        creado.EnsureSuccessStatusCode();
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        return int.Parse(cuerpo.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
    }

    private static async Task PublicarAsync(TestWebApplicationFactory factory, int festivalId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
        festival.StatusCode = "publicado";
        await db.SaveChangesAsync();
    }

    private static async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(
        TestWebApplicationFactory factory, string correo)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de " + correo,
            FullName = "Responsable de la organización",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        var id = int.Parse(mias[0].GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
        return (client, id);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string ruta, object cuerpo)
    {
        var testigo = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        return await client.SendAsync(mensaje);
    }
}
