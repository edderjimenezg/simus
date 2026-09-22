using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-059 — La cola de revision institucional, contra el motor que la mataba.
/// </summary>
/// <remarks>
/// <para>
/// ESTA es la prueba que demuestra la correccion. Su gemela de SQLite
/// (<see cref="Pnmc059VocabularioEstadosTests"/>) comprueba el vocabulario declarado y por
/// eso protege el codigo; pero <b>no puede fallar por el defecto original</b>, porque la
/// base de la suite la fabrica EF desde el modelo y ahi no existen ni
/// <c>FK_Festivales_EstadosContenido</c> ni <c>CK_RegistrosRevisionHistorial_EstadoNuevo</c>
/// ni <c>CK_BitacoraAuditoria_Accion</c>. Sobre SQLite, escribir <c>EnRevision</c> y
/// <c>FestivalEnviadoARevision</c> funciona perfectamente. Por eso el defecto vivio meses
/// con la suite en verde, y por eso <b>tres</b> pruebas llegaron a afirmarlo.
/// </para>
/// <para>
/// Aqui las tres restricciones existen. Medido contra
/// <c>PNMC_LOCAL</c>, antes de tocar nada:
/// </para>
/// <code>
/// UPDATE Festivales SET EstadoRegistro = 'EnRevision'          -> RECHAZADO (FK)
/// UPDATE Festivales SET EstadoRegistro = 'AjustesSolicitados'  -> RECHAZADO (FK)
/// INSERT RegistrosRevisionHistorial EstadoNuevo='EnRevision'   -> RECHAZADO (CHECK)
/// INSERT BitacoraAuditoria Accion='FestivalEnviadoARevision'   -> RECHAZADO (CHECK)
/// </code>
/// <para>
/// Los otros tres estados del circuito —<c>Borrador</c>, <c>Rechazado</c>,
/// <c>Publicado</c>— si pasaban, pero por una razon peor que la de los que fallaban: la
/// base esta en collation <c>SQL_Latin1_General_CP1_CI_AS</c> y casan con los codigos en
/// minuscula <b>por accidente de configuracion</b>. Los dos que fallaban son exactamente
/// los dos que meten un Festival en la bandeja del funcionario.
/// </para>
/// <para>
/// Omitida salvo que se encienda <c>PNMC_PRUEBAS_SQLSERVER=1</c>. Ver <see cref="ArnesSqlServer"/>.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class Pnmc059ColaDeRevisionSqlServerTests
{
    private const string Correo = "agrupacion.cola@example.com";
    private const string Clave = "ClaveExterna123";
    private const string CorreoFuncionario = "funcionario.cola@pnmc.local";
    private const string ClaveFuncionario = "PnmcFuncionario123";
    private const string CorreoArchivo = "agrupacion.archivo@example.com";
    private const string ClaveArchivo = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public Pnmc059ColaDeRevisionSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    /// <summary>
    /// Un Festival enviado a revision aparece en la bandeja institucional, y el funcionario
    /// puede decidir sobre el.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El recorrido completo del circuito vigente contra el esquema real:
    /// persona, organizacion, borrador, envio, bandeja, publicacion. Antes de la correccion
    /// esta prueba no llegaba ni al tercer paso —el <c>SaveChangesAsync</c> del envio moria
    /// contra la clave foranea—, y si alguien lo hubiese silenciado, la bandeja habria
    /// seguido devolviendo una lista vacia para siempre.
    /// </para>
    /// <para>
    /// La asercion sobre <c>DB_NAME()</c> no es adorno: confirma que se esta hablando con la
    /// base desechable de esta corrida y no con <c>PNMC_LOCAL</c>, la de desarrollo del dueño.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task Un_Festival_Enviado_A_Revision_Llega_A_La_Bandeja_Institucional()
    {
        using (var alcance = _base.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", contexto.Database.ProviderName);
        }

        var baseEnUso = Convert.ToString(await _base.EscalarAsync("SELECT DB_NAME();"));
        Assert.Equal(_base.NombreDeBase, baseEnUso);
        Assert.NotEqual(ArnesSqlServer.BaseProhibida, baseEnUso);

        // Linea base honesta: si estas tres restricciones no existieran, la prueba pasaria
        // igual con el defecto dentro y no valdria nada. Se comprueba que estan.
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_Festivales_EstadosContenido';")));
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_RegistrosRevisionHistorial_EstadoNuevo';")));
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_BitacoraAuditoria_Accion';")));

        // El territorio se TOMA de la base desechable en vez de fijarlo a mano. La base de la
        // suite de SQLite viene sembrada por DatabaseBootstrapper y siempre tiene 05/05001;
        // esta se levanta desde los guiones de esquema, cuya siembra de DIVIPOLA depende de
        // que guiones se apliquen. Fijar «05» convertiria un cambio de siembra en un fallo de
        // esta prueba, que no es lo que mide.
        var (departamento, municipio) = await TerritorioRealAsync();

        using var agrupacion = _base.CrearCliente();
        var registro = await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Responsable de la agrupación",
            NumeroDocumento = "1020304051",
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            Phone = "3000000000",
            Email = Correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        var cuenta = await registro.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(cuenta);
        (await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = Correo, Password = Clave })).EnsureSuccessStatusCode();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación de la Cola", correoContacto: "contacto@agrupacion-cola.test", departamento: departamento, municipio: municipio);

        var festival = await CrearAsync<FestivalBorradorDto>(agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
        {
            nombre = "Festival de la Cola Resucitada",
            descripcion = "Festival sembrado para comprobar el circuito contra el motor real.",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
        });

        // ANTES DE LA CORRECCION ESTA LLAMADA MORIA AQUI. El SaveChangesAsync escribia
        // EstadoRegistro='EnRevision' y la clave foranea lo rechazaba; ademas la fila de
        // historial y la de bitacora violaban su propio CHECK cada una por su cuenta.
        var enviar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/enviar-a-revision")
        {
            Content = JsonContent.Create(new { }),
        };
        enviar.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(agrupacion));
        var envio = await agrupacion.SendAsync(enviar);
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);

        // Y ESTA es la comprobacion que da nombre al hallazgo: la bandeja deja de estar vacia.
        await SembrarFuncionarioAsync();
        using var funcionario = _base.CrearCliente();
        (await funcionario.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = CorreoFuncionario, Password = ClaveFuncionario })).EnsureSuccessStatusCode();

        var bandeja = await funcionario.GetFromJsonAsync<List<FestivalRevisionInstitucionalDto>>("/api/v1/institucional/festivales/en-revision");
        Assert.NotNull(bandeja);
        Assert.Contains(bandeja!, item => item.Id == festival.Id);

        // El funcionario decide. Publicar escribe otro estado y otro verbo, y los dos tienen
        // que caber igual.
        var decidir = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festival.Id}/decisiones")
        {
            Content = JsonContent.Create(new { accion = "Publicar" }),
        };
        decidir.Headers.Add("X-CSRF-TOKEN", await CsrfInstitucionalAsync(funcionario));
        Assert.Equal(HttpStatusCode.OK, (await funcionario.SendAsync(decidir)).StatusCode);

        // Lo que quedo escrito, leido del motor real y no del contexto de EF.
        var estado = Convert.ToString(await _base.EscalarAsync(
            $"SELECT EstadoRegistro FROM dbo.Festivales WHERE IdFestival = {int.Parse(festival.Id, System.Globalization.CultureInfo.InvariantCulture)};"));
        Assert.Equal(EstadosFestival.Publicado, estado);

        var estadosDelHistorial = Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT COUNT(*) FROM dbo.RegistrosRevisionHistorial WHERE ModuloId = N'festivales' AND RegistroId = N'{festival.Id}';"));
        Assert.True(estadosDelHistorial >= 2, $"Se esperaban al menos el envio y la decision; hay {estadosDelHistorial}.");

        // La organizacion recibe aviso. Antes NO lo recibia contra este motor, y sin error:
        // el destinatario se resolvia buscando en la bitacora una fila con
        // Action='FestivalEnviadoARevision' que la base nunca dejo escribir, la consulta salia
        // vacia y el metodo se iba por un return silencioso.
        using var alcanceFinal = _base.Services.CreateScope();
        var db = alcanceFinal.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Contains(await db.Notifications.AsNoTracking().ToListAsync(),
            item => item.RecordId == festival.Id && item.EventType == "FestivalPublicado" && item.RecipientEmail == Correo);
    }

    /// <summary>
    /// Un Festival publicado se puede eliminar del ecosistema directamente desde la consola, sin
    /// esperar a que la organización pida su retiro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL DEFECTO QUE ESTO CIERRA. Hasta la única forma de archivar un
    /// Festival publicado era que la organización pidiera su retiro y un funcionario aprobara esa
    /// solicitud (<c>RecordGovernanceEndpoints</c>, <c>festivales_retiro</c>). No había ningún
    /// camino para que la administración lo hiciera por su cuenta -pensando en un Festival
    /// duplicado o que incumple las bases, donde no tiene sentido esperar a que quien lo publicó
    /// pida borrarlo primero-.
    /// </para>
    /// <para>
    /// Contra SQL Server real porque el escrito toca las mismas tres restricciones que
    /// <see cref="Un_Festival_Enviado_A_Revision_Llega_A_La_Bandeja_Institucional"/>: la fila de
    /// <c>RegistrosRevisionHistorial</c> con <c>EstadoNuevo='archivado'</c> y la de
    /// <c>BitacoraAuditoria</c> con <c>Accion='archivar'</c> solo demuestran algo si esos CHECK
    /// existen de verdad.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task Un_Festival_Publicado_Se_Elimina_Del_Ecosistema_Y_Deja_Rastro()
    {
        var (departamento, municipio) = await TerritorioRealAsync();

        using var agrupacion = _base.CrearCliente();
        var registro = await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de prueba archivo",
            FullName = "Responsable de archivo",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            Phone = "3000000001",
            Email = CorreoArchivo,
            Password = ClaveArchivo,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        (await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = CorreoArchivo, Password = ClaveArchivo })).EnsureSuccessStatusCode();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación a archivar", correoContacto: "contacto@agrupacion-archivo.test", departamento: departamento, municipio: municipio);

        var festival = await CrearAsync<FestivalBorradorDto>(agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
        {
            nombre = "Festival A Eliminar",
            descripcion = "Festival sembrado para comprobar el archivado institucional directo.",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
        });

        var enviar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festival.Id}/enviar-a-revision") { Content = JsonContent.Create(new { }) };
        enviar.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(agrupacion));
        Assert.Equal(HttpStatusCode.OK, (await agrupacion.SendAsync(enviar)).StatusCode);

        await SembrarFuncionarioAsync();
        using var funcionario = _base.CrearCliente();
        (await funcionario.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = CorreoFuncionario, Password = ClaveFuncionario })).EnsureSuccessStatusCode();

        var publicar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festival.Id}/decisiones") { Content = JsonContent.Create(new { accion = "Publicar" }) };
        publicar.Headers.Add("X-CSRF-TOKEN", await CsrfInstitucionalAsync(funcionario));
        Assert.Equal(HttpStatusCode.OK, (await funcionario.SendAsync(publicar)).StatusCode);

        // SIN MOTIVO, EL SERVIDOR LO RECHAZA. No basta con «quiero eliminarlo»: hay que decir por qué.
        var sinMotivo = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festival.Id}/archivar") { Content = JsonContent.Create(new { motivo = "" }) };
        sinMotivo.Headers.Add("X-CSRF-TOKEN", await CsrfInstitucionalAsync(funcionario));
        Assert.Equal(HttpStatusCode.BadRequest, (await funcionario.SendAsync(sinMotivo)).StatusCode);

        // CON MOTIVO, SE ELIMINA DEL ECOSISTEMA.
        var archivar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festival.Id}/archivar")
        {
            Content = JsonContent.Create(new { motivo = "Duplicado de otro Festival ya publicado." }),
        };
        archivar.Headers.Add("X-CSRF-TOKEN", await CsrfInstitucionalAsync(funcionario));
        Assert.Equal(HttpStatusCode.OK, (await funcionario.SendAsync(archivar)).StatusCode);

        var estado = Convert.ToString(await _base.EscalarAsync(
            $"SELECT EstadoRegistro FROM dbo.Festivales WHERE IdFestival = {int.Parse(festival.Id, System.Globalization.CultureInfo.InvariantCulture)};"));
        Assert.Equal(EstadosFestival.Archivado, estado);

        // NO SE PUEDE ELIMINAR DOS VECES: la segunda llamada ya no encuentra un Festival publicado.
        var segundaVez = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/institucional/festivales/{festival.Id}/archivar")
        {
            Content = JsonContent.Create(new { motivo = "Segundo intento." }),
        };
        segundaVez.Headers.Add("X-CSRF-TOKEN", await CsrfInstitucionalAsync(funcionario));
        Assert.Equal(HttpStatusCode.Conflict, (await funcionario.SendAsync(segundaVez)).StatusCode);

        using var alcanceFinal = _base.Services.CreateScope();
        var db = alcanceFinal.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Contains(await db.AuditLogs.AsNoTracking().ToListAsync(),
            item => item.RecordId == festival.Id && item.TableName == "Festivales" && item.Action == AccionesAuditoria.Archivar);
        Assert.Contains(await db.HistorialesRevisionRegistros.AsNoTracking().ToListAsync(),
            item => item.RegistroId == festival.Id && item.Accion == "FestivalArchivado" && item.EstadoNuevo == EstadosFestival.Archivado);
        Assert.Contains(await db.Notifications.AsNoTracking().ToListAsync(),
            item => item.RecordId == festival.Id && item.EventType == "FestivalArchivado" && item.RecipientEmail == CorreoArchivo);
    }

    /// <summary>
    /// Crea el funcionario institucional de esta prueba directamente sobre la base.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El arnes de SQLite siembra <c>test@pnmc.local</c> en <c>TestWebApplicationFactory</c>,
    /// pero contra SQL Server no existe equivalente: los usuarios de arranque cuelgan de
    /// <c>Database:SeedBootstrapUsers</c>, que esta apagado, y de
    /// <c>EnsureLocalDevelopmentSeedAsync</c>, que solo corre en la rama que no es SQL Server.
    /// </para>
    /// <para>
    /// Se siembra aqui y no encendiendo <c>SeedBootstrapUsers</c> a proposito: esas cuentas de
    /// arranque traen la contraseña «admin», de cinco caracteres, y encenderlas en el arnes
    /// normalizaria tenerlas. Esta prueba crea la suya, con su propia clave, y no toca la
    /// configuracion del producto.
    /// </para>
    /// </remarks>
    private async Task SembrarFuncionarioAsync()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var idWebmaster = await db.Roles.AsNoTracking()
            .Where(item => item.Name == "webmaster")
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();
        Assert.True(idWebmaster is not null,
            "No hay rol 'webmaster' en la base desechable: la siembra de referencia no se aplico.");

        if (await db.Users.AnyAsync(item => item.Email == CorreoFuncionario))
        {
            return;
        }

        var usuario = new PNMC.Domain.Entities.UserRow
        {
            FullName = "Funcionaria de revisión",
            Email = CorreoFuncionario,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        usuario.PasswordHash = PNMC.Api.Endpoints.AdminAuthEndpoints.HashPassword(usuario, ClaveFuncionario);
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

            // LA FILA DE dbo.UsuariosRoles. `RoleId` de arriba ya no da ningun rol: desde la
            // El rol vive en la tabla de asignacion, y sin esta linea la cuenta existe
            // pero el login la rechaza con el motivo `sin_rol_valido`. Ver RolesEnPruebas.
        await RolesEnPruebas.AsignarPorIdAsync(db, usuario.Id, [idWebmaster!.Value]);
    }

    /// <summary>
    /// Un par departamento/municipio que exista de verdad en la DIVIPOLA de esta base.
    /// </summary>
    /// <remarks>
    /// Falla con un mensaje explicito si la tabla esta vacia, en vez de dejar que el error
    /// aparezca doscientas lineas mas abajo como un 400 de validacion sin contexto.
    /// </remarks>
    /// <summary>
    /// Un par departamento/municipio que exista de verdad en la DIVIPOLA de esta base.
    /// </summary>
    /// <remarks>
    /// No se fija «05/05001» a mano: la base desechable se levanta desde los guiones reales
    /// —esquema y siembra de referencia— y fijar un codigo convertiria cualquier cambio del
    /// catalogo territorial en un fallo de esta prueba, que mide otra cosa. El mensaje de
    /// fallo nombra la causa, para que no aparezca doscientas lineas mas abajo como un 400 de
    /// validacion sin contexto.
    /// </remarks>
    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP (1) CodigoDepartamento + N'|' + CodigoMunicipio FROM dbo.Divipola "
            + "ORDER BY CodigoDepartamento, CodigoMunicipio;"));
        Assert.False(string.IsNullOrWhiteSpace(par),
            "dbo.Divipola esta vacia: sin territorio no se puede crear una organizacion ni un "
            + "Festival. Comprueba que el arnes aplico la siembra de referencia.");
        var partes = par!.Split('|');
        return (partes[0], partes[1]);
    }

    private static async Task<string> CsrfExternoAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>())!.RequestToken;
    }

    private static async Task<string> CsrfInstitucionalAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/institucional/festivales/csrf");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>())!.RequestToken;
    }

    private static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        // Se lee el cuerpo antes de fallar. Un EnsureSuccessStatusCode a secas dice
        // «400 Bad Request» y nada mas, y averiguar por que obliga a instrumentar la prueba
        // a mano: el motivo esta en la respuesta y no cuesta nada mostrarlo.
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"POST {ruta} respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        }
        var creado = await respuesta.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(creado);
        return creado!;
    }
}
