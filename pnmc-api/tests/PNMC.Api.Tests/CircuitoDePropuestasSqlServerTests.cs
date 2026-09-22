using System.Globalization;
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
/// El circuito de propuestas de cambio de un Festival, de punta a punta, contra SQL Server
/// con la estrategia de reintentos de produccion.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. <c>POST /externo/festivales/{id}/propuestas-cambio</c> y
/// <c>POST /institucional/propuestas-cambio-festival/{id}/decisiones</c> abrian una
/// transaccion con <c>BeginTransactionAsync</c> fuera de la estrategia de ejecucion. Con
/// <c>EnableRetryOnFailure</c> —que es como produccion registra el contexto— EF lo prohibe y
/// el primer <c>SaveChangesAsync</c> lanza: las dos rutas respondian 500 SIEMPRE. Era el
/// mismo bloqueo que se diagnostico en la normalizacion de versiones; lo encontro la
/// documentacion del modulo Festivales leyendo el codigo, no una
/// prueba, porque el arnes de SQL Server no reintentaba y el de SQLite no tiene estrategia.
/// </para>
/// <para>
/// Mutante demostrado antes de dejar esta prueba: con el arnes reintentando y las dos rutas
/// sin la estrategia, el POST de la propuesta responde 500 y la prueba muere en el primer
/// paso del circuito.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CircuitoDePropuestasSqlServerTests
{
    private const string Correo = "agrupacion.propuestas@example.com";
    private const string Clave = "ClaveExterna123";
    private const string CorreoFuncionario = "funcionario.propuestas@pnmc.local";
    private const string ClaveFuncionario = "PnmcFuncionario123";

    private readonly SqlServerFixture _base;

    public CircuitoDePropuestasSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task Una_Propuesta_Se_Crea_Se_Envia_Y_Se_Publica_Como_Nueva_Version_Vigente()
    {
        Assert.Equal(_base.NombreDeBase, Convert.ToString(await _base.EscalarAsync("SELECT DB_NAME();")));

        // Linea base honesta: el arnes tiene que reintentar como produccion, o esta prueba no
        // puede ver el defecto que la justifica.
        using (var alcance = _base.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var estrategia = contexto.Database.CreateExecutionStrategy();
            Assert.True(estrategia.RetriesOnFailure, "El arnes de SQL Server no reintenta: no reproduce la configuracion de produccion.");
        }

        var (departamento, municipio) = await TerritorioRealAsync();

        // 1. Una agrupacion publica un Festival (registro, organizacion, borrador, envio,
        //    decision institucional). Es el mismo tramo que Pnmc059 ya prueba; aqui es el
        //    punto de partida.
        using var agrupacion = _base.CrearCliente();
        var registro = await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Responsable de propuestas",
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
        (await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = Correo, Password = Clave })).EnsureSuccessStatusCode();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación de las Propuestas", correoContacto: "contacto@agrupacion-propuestas.test", departamento: departamento, municipio: municipio);
        var festival = await CrearAsync<FestivalBorradorDto>(agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
        {
            nombre = "Festival con propuesta",
            descripcion = "Festival sembrado para recorrer el circuito de propuestas contra el motor real.",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
        });
        await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/enviar-a-revision", new { });

        await SembrarFuncionarioAsync();
        using var funcionario = _base.CrearCliente();
        (await funcionario.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = CorreoFuncionario, Password = ClaveFuncionario })).EnsureSuccessStatusCode();
        await DecidirAsync(funcionario, $"/api/v1/institucional/festivales/{festival.Id}/decisiones", "/api/v1/institucional/festivales/csrf");

        // 2. LA PROPUESTA. Antes del arreglo este POST respondia 500 contra SQL Server.
        var creada = await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio", new { });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var propuesta = await creada.Content.ReadFromJsonAsync<PropuestaCambioFestivalDto>();
        Assert.NotNull(propuesta);

        var editada = await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio", new
        {
            nombre = "Festival con propuesta, segunda versión",
            descripcion = "Contenido de la nueva versión.",
            periodicidad = "bienal",
            correoContacto = "nueva@festival.test",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        }, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);

        var enviada = await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio/enviar-a-revision", new { });
        Assert.Equal(HttpStatusCode.OK, enviada.StatusCode);

        // 3. LA DECISION. Tambien respondia 500 antes del arreglo.
        var bandeja = await funcionario.GetFromJsonAsync<List<PropuestaCambioFestivalRevisionDto>>("/api/v1/institucional/propuestas-cambio-festival/en-revision");
        Assert.Contains(bandeja!, item => item.Id == propuesta!.Id);
        await DecidirAsync(funcionario, $"/api/v1/institucional/propuestas-cambio-festival/{propuesta!.Id}/decisiones", "/api/v1/institucional/propuestas-cambio-festival/csrf");

        // Lo que quedo escrito, leido del motor: una sola version vigente, la nueva, y la
        // propuesta publicada e inactiva. El indice UX_VersionesFestival_Festival_Vigente —que
        // el guion de esquema omitio en su primera version— es lo que hace que «una sola» sea
        // una garantia de la base y no una costumbre del codigo.
        var festivalId = int.Parse(festival.Id, CultureInfo.InvariantCulture);
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT COUNT(*) FROM dbo.VersionesFestival WHERE FestivalOrigenId = {festivalId} AND EsVigente = 1;")));
        Assert.Equal(2, Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT MAX(NumeroVersion) FROM dbo.VersionesFestival WHERE FestivalOrigenId = {festivalId};")));
        // EL EXPEDIENTE VIVE EN LAS TABLAS GENERICAS desde, y su
        // vocabulario es el de allí: `aplicada` es lo que antes se llamaba `Publicada`.
        Assert.Equal("aplicada", Convert.ToString(await _base.EscalarAsync(
            $"SELECT Estado FROM dbo.PropuestasDeCambio WHERE IdPropuesta = {propuesta.Id};")));
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = N'UX_VersionesFestival_Festival_Vigente';")));

        var publico = await agrupacion.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festival.Id}");
        Assert.Equal("Festival con propuesta, segunda versión", publico!.Nombre);
    }

    /// <summary>
    /// Reproduce el 500 encontrado verificando en vivo el panel de
    /// «Mis procesos»: un Festival histórico con filas en <c>VersionesFestival</c> ya numeradas
    /// pero ninguna marcada vigente -exactamente el estado en que quedaba un Festival publicado
    /// antes de que este circuito existiera- hacía que <c>ObtenerOCrearVersionVigenteAsync</c>
    /// escribiera <c>NumeroVersion = 1</c> a ciegas, chocando con
    /// <c>UQ_VersionesFestival_Festival_Numero</c> y devolviendo 500 en el primer intento de
    /// proponer un cambio.
    /// </summary>
    [HechoSqlServer]
    public async Task Proponer_Cambios_Con_Una_Version_Huerfana_Ya_Numerada_No_Choca_Con_El_Indice_Unico()
    {
        var (departamento, municipio) = await TerritorioRealAsync();

        using var agrupacion = _base.CrearCliente();
        var registro = await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion con version huerfana",
            FullName = "Responsable de la version huérfana",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            Phone = "3000000001",
            Email = "agrupacion.version-huerfana@example.com",
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        (await agrupacion.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = "agrupacion.version-huerfana@example.com", Password = Clave })).EnsureSuccessStatusCode();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación de la versión huérfana", correoContacto: "contacto@version-huerfana.test", departamento: departamento, municipio: municipio);
        var festival = await CrearAsync<FestivalBorradorDto>(agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
        {
            nombre = "Festival con versión huérfana",
            descripcion = "Festival sembrado con una version ya numerada pero sin marcar vigente.",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
        });
        await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/enviar-a-revision", new { });

        await SembrarFuncionarioAsync();
        using var funcionario = _base.CrearCliente();
        (await funcionario.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = CorreoFuncionario, Password = ClaveFuncionario })).EnsureSuccessStatusCode();
        await DecidirAsync(funcionario, $"/api/v1/institucional/festivales/{festival.Id}/decisiones", "/api/v1/institucional/festivales/csrf");

        var festivalId = int.Parse(festival.Id, CultureInfo.InvariantCulture);

        // LA VERSION HUERFANA. Simula un Festival histórico: ya tiene una fila en
        // VersionesFestival con NumeroVersion = 1, pero ninguna vigente -el mismo estado que
        // dejaba, contra la base local, un Festival publicado antes de que este endpoint
        // supiera crear su propia versión de arranque-.
        using (var alcance = _base.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            contexto.VersionesFestival.Add(new PNMC.Domain.Entities.VersionFestivalRow
            {
                FestivalOrigenId = festivalId,
                NumeroVersion = 1,
                EsVigente = false,
                Nombre = "Festival con versión huérfana",
                NivelCobertura = "municipal",
                FechaCreacion = DateTime.UtcNow,
            });
            await contexto.SaveChangesAsync();
        }

        // Antes del arreglo, este POST respondía 500: intentaba escribir NumeroVersion = 1 de
        // nuevo y chocaba con UQ_VersionesFestival_Festival_Numero.
        var creada = await EnviarExternoAsync(agrupacion, $"/api/v1/externo/festivales/{festival.Id}/propuestas-cambio", new { });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);

        Assert.Equal(2, Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT MAX(NumeroVersion) FROM dbo.VersionesFestival WHERE FestivalOrigenId = {festivalId};")));
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT COUNT(*) FROM dbo.VersionesFestival WHERE FestivalOrigenId = {festivalId} AND EsVigente = 1;")));
    }

    private static async Task DecidirAsync(HttpClient funcionario, string ruta, string rutaCsrf)
    {
        var csrf = await funcionario.GetAsync(rutaCsrf);
        csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>())!.RequestToken;
        var decidir = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(new { accion = "Publicar" }) };
        decidir.Headers.Add("X-CSRF-TOKEN", token);
        var respuesta = await funcionario.SendAsync(decidir);
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"POST {ruta} respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        }
    }

    private async Task SembrarFuncionarioAsync()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        if (await db.Users.AnyAsync(item => item.Email == CorreoFuncionario))
        {
            return;
        }

        var idWebmaster = await db.Roles.AsNoTracking().Where(item => item.Name == "webmaster").Select(item => item.Id).FirstAsync();
        var usuario = new PNMC.Domain.Entities.UserRow
        {
            FullName = "Funcionaria de propuestas",
            Email = CorreoFuncionario,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        usuario.PasswordHash = AdminAuthEndpoints.HashPassword(usuario, ClaveFuncionario);
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

            // LA FILA DE dbo.UsuariosRoles. `RoleId` de arriba ya no da ningun rol: desde la
            // El rol vive en la tabla de asignacion, y sin esta linea la cuenta existe
            // pero el login la rechaza con el motivo `sin_rol_valido`. Ver RolesEnPruebas.
        await RolesEnPruebas.AsignarPorIdAsync(db, usuario.Id, [idWebmaster]);
    }

    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP (1) CodigoDepartamento + N'|' + CodigoMunicipio FROM dbo.Divipola ORDER BY CodigoDepartamento, CodigoMunicipio;"));
        Assert.False(string.IsNullOrWhiteSpace(par), "dbo.Divipola esta vacia: el arnes no aplico la siembra de referencia.");
        var partes = par!.Split('|');
        return (partes[0], partes[1]);
    }

    private static async Task<string> CsrfExternoAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>())!.RequestToken;
    }

    private static async Task<HttpResponseMessage> EnviarExternoAsync(HttpClient cliente, string ruta, object cuerpo, HttpMethod? metodo = null)
    {
        var mensaje = new HttpRequestMessage(metodo ?? HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"{mensaje.Method} {ruta} respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        }
        return respuesta;
    }

    private static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var respuesta = await EnviarExternoAsync(cliente, ruta, cuerpo);
        var creado = await respuesta.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(creado);
        return creado!;
    }
}
