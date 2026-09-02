using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Simus.Api.Servicios;

var builder = WebApplication.CreateBuilder(args);
var opcionesSesion = builder.Configuration.GetSection("Sesion").Get<OpcionesSesion>()
    ?? throw new InvalidOperationException("Falta la configuración de sesión.");
if (opcionesSesion.MinutosInactividad is < 5 or > 240 || opcionesSesion.HorasMaximas is < 1 or > 24 || opcionesSesion.MinutosAvisoPrevio < 1)
    throw new InvalidOperationException("La configuración de sesión no está dentro de los límites permitidos.");
builder.Services.AddSingleton(opcionesSesion);
builder.Services.AddSingleton<ServicioSesiones>();
builder.Services.AddSingleton<ServicioAuditoriaAcceso>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("ingreso", contexto =>
    {
        var claveParticion = contexto.Connection.RemoteIpAddress?.ToString() ?? "sin_ip";
        return RateLimitPartition.GetFixedWindowLimiter(claveParticion, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();
app.UseExceptionHandler(manejador => manejador.Run(async contexto =>
{
    var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
    var trazaId = contexto.TraceIdentifier;
    app.Logger.LogError(error, "Error no previsto. Traza: {TrazaId}", trazaId);
    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await contexto.Response.WriteAsJsonAsync(new ErrorApi("error_no_previsto", "Ocurrió un error inesperado. Inténtalo nuevamente.", trazaId));
}));
app.Use(async (contexto, siguiente) =>
{
    var inicio = Stopwatch.GetTimestamp();
    await siguiente();
    app.Logger.LogInformation("Solicitud {Metodo} {Ruta} respondió {Estado} en {DuracionMs} ms. Traza: {TrazaId}",
        contexto.Request.Method, contexto.Request.Path, contexto.Response.StatusCode,
        Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, contexto.TraceIdentifier);
});
app.UseCors();
app.UseRateLimiter();

app.MapGet("/api/salud", async (IConfiguration configuracion, CancellationToken cancelacion) =>
{
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
    {
        return Results.Json(new RespuestaSalud("no_disponible", "no_configurada"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancelacion);
        return Results.Ok(new RespuestaSalud("disponible", "disponible"));
    }
    catch (SqlException)
    {
        return Results.Json(new RespuestaSalud("disponible", "no_disponible"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/registro/preparacion", async (IConfiguration configuracion, CancellationToken cancelacion) =>
{
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
        return Results.Ok(new PreparacionRegistro(false, false, [], ["La base de datos aún no está configurada."]));

    try
    {
        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancelacion);
        return Results.Ok(await ObtenerPreparacionRegistroAsync(conexion, cancelacion));
    }
    catch (SqlException)
    {
        return Results.Ok(new PreparacionRegistro(false, false, [], ["La base de datos no está disponible temporalmente."]));
    }
});

app.MapGet("/api/registro/departamentos", async (IConfiguration configuracion, CancellationToken cancelacion) =>
{
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync(cancelacion);
    const string consulta = "SELECT Codigo,Nombre FROM territorio.Departamentos ORDER BY Nombre;";
    await using var comando = new SqlCommand(consulta, conexion);
    await using var lector = await comando.ExecuteReaderAsync(cancelacion);
    var departamentos = new List<TerritorioRegistro>();
    while (await lector.ReadAsync(cancelacion)) departamentos.Add(new TerritorioRegistro(lector.GetString(0), lector.GetString(1)));
    return Results.Ok(departamentos);
});

app.MapGet("/api/registro/departamentos/{codigoDepartamento}/municipios", async (string codigoDepartamento, IConfiguration configuracion, CancellationToken cancelacion) =>
{
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync(cancelacion);
    const string consulta = "SELECT Codigo,Nombre FROM territorio.Municipios WHERE CodigoDepartamento=@departamento ORDER BY Nombre;";
    await using var comando = new SqlCommand(consulta, conexion);
    comando.Parameters.AddWithValue("@departamento", codigoDepartamento.Trim());
    await using var lector = await comando.ExecuteReaderAsync(cancelacion);
    var municipios = new List<TerritorioRegistro>();
    while (await lector.ReadAsync(cancelacion)) municipios.Add(new TerritorioRegistro(lector.GetString(0), lector.GetString(1)));
    return Results.Ok(municipios);
});

app.MapGet("/api/sesion/estado", async (HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, CancellationToken cancelacion) =>
{
    if (!contexto.Request.Cookies.TryGetValue("simus_sesion", out var secreto) || string.IsNullOrWhiteSpace(secreto)) return Results.Unauthorized();
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync(cancelacion);
    var idPersona = await sesiones.ValidarAsync(conexion, secreto, cancelacion);
    return idPersona is null ? Results.Unauthorized() : Results.Ok(new { idPersona });
});

app.MapGet("/api/mi-panel/contexto", async (HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, CancellationToken cancelacion) =>
{
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        const string consulta = """
            SELECT p.PrimerNombre,p.PrimerApellido,p.CorreoNormalizado,p.Telefono,
                   o.Id,o.Nombre,o.NumeroIdentificacion,o.Estado,o.CodigoDepartamento,d.Nombre,o.CodigoMunicipio,m.Nombre,o.FechaActualizacion
            FROM identidad.Personas p
            INNER JOIN organizaciones.Administradores a ON a.IdPersona=p.Id AND a.FechaRetiro IS NULL
            INNER JOIN organizaciones.Organizaciones o ON o.Id=a.IdOrganizacion
            INNER JOIN territorio.Departamentos d ON d.Codigo=o.CodigoDepartamento
            INNER JOIN territorio.Municipios m ON m.Codigo=o.CodigoMunicipio AND m.CodigoDepartamento=o.CodigoDepartamento
            WHERE p.Id=@persona
            ORDER BY o.Nombre;
            """;
        await using var comando = new SqlCommand(consulta, conexionAbierta);
        comando.Parameters.AddWithValue("@persona", idPersona!.Value);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        PersonaPanel? persona = null;
        var organizaciones = new List<OrganizacionPanel>();
        while (await lector.ReadAsync(cancelacion))
        {
            persona ??= new PersonaPanel(lector.GetString(0), lector.GetString(1), lector.GetString(2), lector.IsDBNull(3) ? null : lector.GetString(3));
            organizaciones.Add(new OrganizacionPanel(lector.GetGuid(4), lector.GetString(5), lector.IsDBNull(6) ? null : lector.GetString(6), lector.GetString(7), lector.GetString(8), lector.GetString(9), lector.GetString(10), lector.GetString(11), lector.GetDateTime(12)));
        }
        return persona is null
            ? Results.Unauthorized()
            : Results.Ok(new ContextoPanel(persona, organizaciones));
    }
});

app.MapGet("/api/mi-panel/organizaciones/{idOrganizacion:guid}/administradores", async (Guid idOrganizacion, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, CancellationToken cancelacion) =>
{
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        if (!await PuedeAdministrarOrganizacionAsync(conexionAbierta, idPersona!.Value, idOrganizacion, cancelacion)) return Results.Forbid();
        const string consulta = """
            SELECT p.Id,p.PrimerNombre,p.SegundoNombre,p.PrimerApellido,p.SegundoApellido,p.CorreoNormalizado,p.Telefono,a.FechaAsignacion
            FROM organizaciones.Administradores a
            INNER JOIN identidad.Personas p ON p.Id=a.IdPersona
            WHERE a.IdOrganizacion=@organizacion AND a.FechaRetiro IS NULL
            ORDER BY p.PrimerApellido,p.PrimerNombre;
            """;
        await using var comando = new SqlCommand(consulta, conexionAbierta);
        comando.Parameters.AddWithValue("@organizacion", idOrganizacion);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        var administradores = new List<AdministradorOrganizacionPanel>();
        while (await lector.ReadAsync(cancelacion))
        {
            var nombres = string.Join(" ", new[] { lector.GetString(1), lector.IsDBNull(2) ? null : lector.GetString(2), lector.GetString(3), lector.IsDBNull(4) ? null : lector.GetString(4) }.Where(valor => !string.IsNullOrWhiteSpace(valor)));
            administradores.Add(new AdministradorOrganizacionPanel(lector.GetGuid(0), nombres, lector.GetString(5), lector.IsDBNull(6) ? null : lector.GetString(6), lector.GetDateTime(7)));
        }
        return Results.Ok(administradores);
    }
});

app.MapPatch("/api/mi-panel/organizaciones/{idOrganizacion:guid}", async (Guid idOrganizacion, ActualizacionOrganizacionPanel solicitud, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    var errores = ValidarActualizacionOrganizacion(solicitud);
    if (errores.Count > 0) return Results.UnprocessableEntity(new ErrorApi("campos_invalidos", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        if (!await PuedeAdministrarOrganizacionAsync(conexionAbierta, idPersona!.Value, idOrganizacion, cancelacion)) return Results.Forbid();
        const string territorioExiste = "SELECT COUNT(*) FROM territorio.Municipios WHERE CodigoDepartamento=@departamento AND Codigo=@municipio;";
        await using (var comandoTerritorio = new SqlCommand(territorioExiste, conexionAbierta))
        {
            comandoTerritorio.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
            comandoTerritorio.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
            if (Convert.ToInt32(await comandoTerritorio.ExecuteScalarAsync(cancelacion)) == 0)
            {
                errores["codigoMunicipio"] = ["El municipio no corresponde al departamento seleccionado."];
                return Results.UnprocessableEntity(new ErrorApi("territorio_invalido", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
            }
        }
        const string actualizar = """
            UPDATE organizaciones.Organizaciones
            SET Nombre=@nombre,NumeroIdentificacion=@numero,CodigoDepartamento=@departamento,CodigoMunicipio=@municipio,FechaActualizacion=SYSUTCDATETIME()
            WHERE Id=@organizacion;
            """;
        await using var comando = new SqlCommand(actualizar, conexionAbierta);
        comando.Parameters.AddWithValue("@nombre", solicitud.Nombre.Trim());
        comando.Parameters.AddWithValue("@numero", ComoDbNull(solicitud.NumeroIdentificacion));
        comando.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
        comando.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
        comando.Parameters.AddWithValue("@organizacion", idOrganizacion);
        await comando.ExecuteNonQueryAsync(cancelacion);
        await auditoria.RegistrarAsync(conexionAbierta, idPersona.Value, "organizacion_actualizada", contexto.TraceIdentifier, cancelacion);
        return Results.NoContent();
    }
});

app.MapGet("/api/mi-panel/festivales", async (HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, CancellationToken cancelacion) =>
{
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        const string consulta = """
            SELECT f.Id,p.Id,p.Nombre,p.EstadoEditorial,p.Descripcion,p.CodigoDepartamento,d.Nombre,p.CodigoMunicipio,m.Nombre,p.FechaActualizacion,
                   o.Id,o.Nombre
            FROM festivales.Festivales f
            INNER JOIN organizaciones.Administradores a ON a.IdOrganizacion=f.IdOrganizacionAdministradora AND a.FechaRetiro IS NULL
            INNER JOIN organizaciones.Organizaciones o ON o.Id=f.IdOrganizacionAdministradora
            CROSS APPLY (
                SELECT TOP 1 Id,Nombre,EstadoEditorial,Descripcion,CodigoDepartamento,CodigoMunicipio,FechaActualizacion
                FROM festivales.Perfiles WHERE IdFestival=f.Id ORDER BY NumeroVersion DESC
            ) p
            INNER JOIN territorio.Departamentos d ON d.Codigo=p.CodigoDepartamento
            INNER JOIN territorio.Municipios m ON m.Codigo=p.CodigoMunicipio AND m.CodigoDepartamento=p.CodigoDepartamento
            WHERE a.IdPersona=@persona AND f.EstadoIdentidad=N'activa'
            ORDER BY p.FechaActualizacion DESC,p.Nombre;
            """;
        await using var comando = new SqlCommand(consulta, conexionAbierta);
        comando.Parameters.AddWithValue("@persona", idPersona!.Value);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        var festivales = new List<FestivalPanel>();
        while (await lector.ReadAsync(cancelacion))
        {
            festivales.Add(new FestivalPanel(
                lector.GetGuid(0), lector.GetGuid(1), lector.GetString(2), lector.GetString(3), lector.IsDBNull(4) ? null : lector.GetString(4),
                lector.GetString(5), lector.GetString(6), lector.GetString(7), lector.GetString(8), lector.GetDateTime(9), lector.GetGuid(10), lector.GetString(11)));
        }
        return Results.Ok(festivales);
    }
});

app.MapPost("/api/mi-panel/festivales", async (SolicitudCrearFestival solicitud, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    var errores = ValidarSolicitudFestival(solicitud.Nombre, solicitud.Descripcion, solicitud.CodigoDepartamento, solicitud.CodigoMunicipio);
    if (solicitud.IdOrganizacion == Guid.Empty) errores["idOrganizacion"] = ["Selecciona la organización que administrará el Festival."];
    if (errores.Count > 0) return Results.UnprocessableEntity(new ErrorApi("campos_invalidos", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        if (!await PuedeAdministrarOrganizacionAsync(conexionAbierta, idPersona!.Value, solicitud.IdOrganizacion, cancelacion)) return Results.Forbid();
        if (!await TerritorioExisteAsync(conexionAbierta, solicitud.CodigoDepartamento, solicitud.CodigoMunicipio, cancelacion))
        {
            errores["codigoMunicipio"] = ["El municipio no corresponde al departamento seleccionado."];
            return Results.UnprocessableEntity(new ErrorApi("territorio_invalido", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
        }
        var idFestival = Guid.NewGuid();
        var idPerfil = Guid.NewGuid();
        await using var transaccion = await conexionAbierta.BeginTransactionAsync(cancelacion);
        const string insertar = """
            INSERT INTO festivales.Festivales (Id,IdOrganizacionAdministradora,EstadoIdentidad,FechaCreacion,FechaActualizacion)
            VALUES (@festival,@organizacion,N'activa',SYSUTCDATETIME(),SYSUTCDATETIME());
            INSERT INTO festivales.Perfiles (Id,IdFestival,NumeroVersion,EstadoEditorial,Nombre,Descripcion,CodigoDepartamento,CodigoMunicipio,IdPersonaCreadora,FechaCreacion,FechaActualizacion)
            VALUES (@perfil,@festival,1,N'borrador',@nombre,@descripcion,@departamento,@municipio,@persona,SYSUTCDATETIME(),SYSUTCDATETIME());
            """;
        await using var comando = new SqlCommand(insertar, conexionAbierta, (SqlTransaction)transaccion);
        comando.Parameters.AddWithValue("@festival", idFestival);
        comando.Parameters.AddWithValue("@perfil", idPerfil);
        comando.Parameters.AddWithValue("@organizacion", solicitud.IdOrganizacion);
        comando.Parameters.AddWithValue("@nombre", solicitud.Nombre.Trim());
        comando.Parameters.AddWithValue("@descripcion", ComoDbNull(solicitud.Descripcion));
        comando.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
        comando.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
        comando.Parameters.AddWithValue("@persona", idPersona.Value);
        await comando.ExecuteNonQueryAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        await auditoria.RegistrarAsync(conexionAbierta, idPersona.Value, "festival_borrador_creado", contexto.TraceIdentifier, cancelacion);
        return Results.Created($"/api/mi-panel/festivales/{idFestival}", new { idFestival, idPerfil });
    }
});

app.MapPatch("/api/mi-panel/festivales/{idFestival:guid}/perfil-borrador", async (Guid idFestival, SolicitudActualizarFestival solicitud, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    var errores = ValidarSolicitudFestival(solicitud.Nombre, solicitud.Descripcion, solicitud.CodigoDepartamento, solicitud.CodigoMunicipio);
    if (errores.Count > 0) return Results.UnprocessableEntity(new ErrorApi("campos_invalidos", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    var (conexion, idPersona, error) = await ObtenerConexionAutorizadaAsync(contexto, configuracion, sesiones, cancelacion);
    if (error is not null) return error;
    await using (conexion!)
    {
        var conexionAbierta = conexion!;
        if (!await PuedeAdministrarFestivalAsync(conexionAbierta, idPersona!.Value, idFestival, cancelacion)) return Results.Forbid();
        if (!await TerritorioExisteAsync(conexionAbierta, solicitud.CodigoDepartamento, solicitud.CodigoMunicipio, cancelacion))
        {
            errores["codigoMunicipio"] = ["El municipio no corresponde al departamento seleccionado."];
            return Results.UnprocessableEntity(new ErrorApi("territorio_invalido", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
        }
        const string actualizar = """
            UPDATE festivales.Perfiles SET Nombre=@nombre,Descripcion=@descripcion,CodigoDepartamento=@departamento,CodigoMunicipio=@municipio,FechaActualizacion=SYSUTCDATETIME()
            WHERE IdFestival=@festival AND EstadoEditorial=N'borrador' AND NumeroVersion=(SELECT MAX(NumeroVersion) FROM festivales.Perfiles WHERE IdFestival=@festival);
            UPDATE festivales.Festivales SET FechaActualizacion=SYSUTCDATETIME() WHERE Id=@festival;
            """;
        await using var comando = new SqlCommand(actualizar, conexionAbierta);
        comando.Parameters.AddWithValue("@nombre", solicitud.Nombre.Trim());
        comando.Parameters.AddWithValue("@descripcion", ComoDbNull(solicitud.Descripcion));
        comando.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
        comando.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
        comando.Parameters.AddWithValue("@festival", idFestival);
        var filas = await comando.ExecuteNonQueryAsync(cancelacion);
        if (filas == 1) return Results.Conflict(new ErrorApi("borrador_no_disponible", "Este Festival ya no tiene un borrador disponible para editar.", contexto.TraceIdentifier));
        await auditoria.RegistrarAsync(conexionAbierta, idPersona.Value, "festival_borrador_actualizado", contexto.TraceIdentifier, cancelacion);
        return Results.NoContent();
    }
});

app.MapPost("/api/sesion/cerrar", async (HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    if (contexto.Request.Cookies.TryGetValue("simus_sesion", out var secreto) && !string.IsNullOrWhiteSpace(secreto))
    {
        var cadenaConexion = configuracion.GetConnectionString("Simus");
        if (!string.IsNullOrWhiteSpace(cadenaConexion))
        {
            await using var conexion = new SqlConnection(cadenaConexion);
            await conexion.OpenAsync(cancelacion);
            var idPersona = await sesiones.ValidarAsync(conexion, secreto, cancelacion);
            await sesiones.CerrarAsync(conexion, secreto, cancelacion);
            if (idPersona is not null) await auditoria.RegistrarAsync(conexion, idPersona, "cierre_sesion", contexto.TraceIdentifier, cancelacion);
        }
    }
    contexto.Response.Cookies.Delete("simus_sesion");
    return Results.NoContent();
});

app.MapPost("/api/acceso/ingresar", async (SolicitudIngreso solicitud, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    var errores = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(solicitud.Correo)) errores["correo"] = ["Ingresa tu correo electrónico."];
    if (string.IsNullOrWhiteSpace(solicitud.Contrasena)) errores["contrasena"] = ["Ingresa tu contraseña."];
    if (errores.Count > 0) return Results.UnprocessableEntity(new ErrorApi("campos_invalidos", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync(cancelacion);
    const string sql = "SELECT Id,HashContrasena,EstadoCuenta FROM identidad.Personas WHERE CorreoNormalizado=@correo;";
    await using var comando = new SqlCommand(sql, conexion);
    comando.Parameters.AddWithValue("@correo", solicitud.Correo.Trim().ToLowerInvariant());
    await using var lector = await comando.ExecuteReaderAsync(cancelacion);
    if (!await lector.ReadAsync(cancelacion))
    {
        await lector.CloseAsync();
        await auditoria.RegistrarAsync(conexion, null, "ingreso_rechazado", contexto.TraceIdentifier, cancelacion);
        return Results.Unauthorized();
    }
    var idPersona = lector.GetGuid(0); var hash = lector.GetString(1); var estado = lector.GetString(2);
    await lector.CloseAsync();
    if (estado != "activa" || new PasswordHasher<object>().VerifyHashedPassword(new object(), hash, solicitud.Contrasena) == PasswordVerificationResult.Failed)
    {
        await auditoria.RegistrarAsync(conexion, idPersona, "ingreso_rechazado", contexto.TraceIdentifier, cancelacion);
        return Results.Unauthorized();
    }
    var (secreto, venceEn) = await sesiones.CrearAsync(conexion, idPersona, cancelacion);
    contexto.Response.Cookies.Append("simus_sesion", secreto, OpcionesCookieSesion(app, venceEn));
    await auditoria.RegistrarAsync(conexion, idPersona, "ingreso_exitoso", contexto.TraceIdentifier, cancelacion);
    return Results.Ok(new { idPersona });
}).RequireRateLimiting("ingreso");

app.MapPost("/api/registro", async (SolicitudRegistroExterno solicitud, HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, ServicioAuditoriaAcceso auditoria, CancellationToken cancelacion) =>
{
    var errores = ValidarSolicitudRegistro(solicitud);
    if (errores.Count > 0) return Results.UnprocessableEntity(new ErrorApi("campos_invalidos", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync(cancelacion);
    var preparacion = await ObtenerPreparacionRegistroAsync(conexion, cancelacion);
    if (!preparacion.RegistroDisponible)
        return Results.Conflict(new ErrorApi("registro_no_disponible", "El registro aún no está disponible porque faltan requisitos institucionales.", contexto.TraceIdentifier));

    const string territorioExiste = "SELECT COUNT(*) FROM territorio.Municipios WHERE CodigoDepartamento=@departamento AND Codigo=@municipio;";
    await using (var comandoTerritorio = new SqlCommand(territorioExiste, conexion))
    {
        comandoTerritorio.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
        comandoTerritorio.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
        if (Convert.ToInt32(await comandoTerritorio.ExecuteScalarAsync(cancelacion)) == 0)
        {
            errores["codigoMunicipio"] = ["El municipio no corresponde al departamento seleccionado."];
            return Results.UnprocessableEntity(new ErrorApi("territorio_invalido", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
        }
    }

    var aceptados = (solicitud.CodigosDocumentosAceptados ?? []).ToHashSet(StringComparer.Ordinal);
    var faltantes = preparacion.Documentos.Where(documento => !aceptados.Contains(documento.Codigo)).ToList();
    if (faltantes.Count > 0)
    {
        errores["consentimientos"] = ["Debes aceptar los documentos vigentes para continuar."];
        return Results.UnprocessableEntity(new ErrorApi("consentimientos_pendientes", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    }

    var correo = solicitud.Correo.Trim().ToLowerInvariant();
    var identificacion = solicitud.NumeroIdentificacion.Trim().ToUpperInvariant();
    var tipoIdentificacion = solicitud.CodigoTipoIdentificacion.Trim().ToUpperInvariant();
    var idPersona = Guid.NewGuid();
    var idOrganizacion = Guid.NewGuid();
    try
    {
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        const string insertar = """
            INSERT INTO identidad.Personas (Id,PrimerNombre,SegundoNombre,PrimerApellido,SegundoApellido,CodigoTipoIdentificacion,NumeroIdentificacionNormalizado,CorreoNormalizado,Telefono,HashContrasena,EstadoCuenta,EstadoVerificacionCorreo,FechaCreacion,FechaActualizacion)
            VALUES (@persona,@primerNombre,@segundoNombre,@primerApellido,@segundoApellido,@tipo,@numero,@correo,@telefono,@hash,N'activa',N'no_configurada',SYSUTCDATETIME(),SYSUTCDATETIME());
            INSERT INTO identidad.PersonasRoles (IdPersona,CodigoRol,FechaAsignacion) VALUES (@persona,N'externo',SYSUTCDATETIME());
            INSERT INTO organizaciones.Organizaciones (Id,Nombre,NumeroIdentificacion,CorreoContacto,CodigoDepartamento,CodigoMunicipio,Estado,FechaCreacion,FechaActualizacion)
            VALUES (@organizacion,@nombreOrganizacion,@numeroOrganizacion,NULL,@departamento,@municipio,N'activa',SYSUTCDATETIME(),SYSUTCDATETIME());
            INSERT INTO organizaciones.Administradores (IdOrganizacion,IdPersona,FechaAsignacion) VALUES (@organizacion,@persona,SYSUTCDATETIME());
            """;
        await using var comando = new SqlCommand(insertar, conexion, (SqlTransaction)transaccion);
        comando.Parameters.AddWithValue("@persona", idPersona);
        comando.Parameters.AddWithValue("@organizacion", idOrganizacion);
        comando.Parameters.AddWithValue("@primerNombre", solicitud.PrimerNombre.Trim());
        comando.Parameters.AddWithValue("@segundoNombre", ComoDbNull(solicitud.SegundoNombre));
        comando.Parameters.AddWithValue("@primerApellido", solicitud.PrimerApellido.Trim());
        comando.Parameters.AddWithValue("@segundoApellido", ComoDbNull(solicitud.SegundoApellido));
        comando.Parameters.AddWithValue("@tipo", tipoIdentificacion);
        comando.Parameters.AddWithValue("@numero", identificacion);
        comando.Parameters.AddWithValue("@correo", correo);
        comando.Parameters.AddWithValue("@telefono", ComoDbNull(solicitud.Telefono));
        comando.Parameters.AddWithValue("@hash", new PasswordHasher<object>().HashPassword(new object(), solicitud.Contrasena));
        comando.Parameters.AddWithValue("@nombreOrganizacion", solicitud.NombreOrganizacion.Trim());
        comando.Parameters.AddWithValue("@numeroOrganizacion", ComoDbNull(solicitud.NumeroIdentificacionOrganizacion));
        comando.Parameters.AddWithValue("@departamento", solicitud.CodigoDepartamento.Trim());
        comando.Parameters.AddWithValue("@municipio", solicitud.CodigoMunicipio.Trim());
        await comando.ExecuteNonQueryAsync(cancelacion);

        const string aceptar = "INSERT INTO legal.Aceptaciones (Id,IdPersona,IdDocumento,FechaAceptacion) VALUES (NEWID(),@persona,@documento,SYSUTCDATETIME());";
        foreach (var documento in preparacion.Documentos)
        {
            await using var comandoAceptacion = new SqlCommand(aceptar, conexion, (SqlTransaction)transaccion);
            comandoAceptacion.Parameters.AddWithValue("@persona", idPersona);
            comandoAceptacion.Parameters.AddWithValue("@documento", documento.Id);
            await comandoAceptacion.ExecuteNonQueryAsync(cancelacion);
        }
        await transaccion.CommitAsync(cancelacion);
    }
    catch (SqlException error) when (error.Number is 2601 or 2627)
    {
        var campo = error.Message.Contains("CorreoNormalizado", StringComparison.OrdinalIgnoreCase) ? "correo" : "numeroIdentificacion";
        errores[campo] = [campo == "correo" ? "Ya existe una cuenta con este correo electrónico." : "Ya existe una cuenta con este tipo y número de identificación."];
        return Results.Conflict(new ErrorApi("dato_duplicado", "Revisa los campos indicados.", contexto.TraceIdentifier, errores));
    }

    var (secreto, venceEn) = await sesiones.CrearAsync(conexion, idPersona, cancelacion);
    contexto.Response.Cookies.Append("simus_sesion", secreto, OpcionesCookieSesion(app, venceEn));
    await auditoria.RegistrarAsync(conexion, idPersona, "registro_externo_exitoso", contexto.TraceIdentifier, cancelacion);
    return Results.Ok(new { idPersona, idOrganizacion, venceEn });
}).RequireRateLimiting("ingreso");

static CookieOptions OpcionesCookieSesion(WebApplication aplicacion, DateTime? venceEn = null) => new()
{
    HttpOnly = true,
    Secure = !aplicacion.Environment.IsDevelopment(),
    SameSite = SameSiteMode.Strict,
    Expires = venceEn
};

static object ComoDbNull(string? valor) => string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

static async Task<(SqlConnection? Conexion, Guid? IdPersona, IResult? Error)> ObtenerConexionAutorizadaAsync(HttpContext contexto, IConfiguration configuracion, ServicioSesiones sesiones, CancellationToken cancelacion)
{
    if (!contexto.Request.Cookies.TryGetValue("simus_sesion", out var secreto) || string.IsNullOrWhiteSpace(secreto))
        return (null, null, Results.Unauthorized());
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion)) return (null, null, Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    var conexion = new SqlConnection(cadenaConexion);
    try
    {
        await conexion.OpenAsync(cancelacion);
        var idPersona = await sesiones.ValidarAsync(conexion, secreto, cancelacion);
        if (idPersona is not null) return (conexion, idPersona, null);
        await conexion.DisposeAsync();
        return (null, null, Results.Unauthorized());
    }
    catch
    {
        await conexion.DisposeAsync();
        throw;
    }
}

static async Task<bool> PuedeAdministrarOrganizacionAsync(SqlConnection conexion, Guid idPersona, Guid idOrganizacion, CancellationToken cancelacion)
{
    const string consulta = """
        SELECT COUNT(*) FROM organizaciones.Administradores a
        INNER JOIN organizaciones.Organizaciones o ON o.Id=a.IdOrganizacion
        WHERE a.IdPersona=@persona AND a.IdOrganizacion=@organizacion AND a.FechaRetiro IS NULL AND o.Estado=N'activa';
        """;
    await using var comando = new SqlCommand(consulta, conexion);
    comando.Parameters.AddWithValue("@persona", idPersona);
    comando.Parameters.AddWithValue("@organizacion", idOrganizacion);
    return Convert.ToInt32(await comando.ExecuteScalarAsync(cancelacion)) == 1;
}

static async Task<bool> PuedeAdministrarFestivalAsync(SqlConnection conexion, Guid idPersona, Guid idFestival, CancellationToken cancelacion)
{
    const string consulta = """
        SELECT COUNT(*) FROM festivales.Festivales f
        INNER JOIN organizaciones.Administradores a ON a.IdOrganizacion=f.IdOrganizacionAdministradora AND a.FechaRetiro IS NULL
        WHERE f.Id=@festival AND f.EstadoIdentidad=N'activa' AND a.IdPersona=@persona;
        """;
    await using var comando = new SqlCommand(consulta, conexion);
    comando.Parameters.AddWithValue("@persona", idPersona);
    comando.Parameters.AddWithValue("@festival", idFestival);
    return Convert.ToInt32(await comando.ExecuteScalarAsync(cancelacion)) == 1;
}

static async Task<bool> TerritorioExisteAsync(SqlConnection conexion, string codigoDepartamento, string codigoMunicipio, CancellationToken cancelacion)
{
    const string consulta = "SELECT COUNT(*) FROM territorio.Municipios WHERE CodigoDepartamento=@departamento AND Codigo=@municipio;";
    await using var comando = new SqlCommand(consulta, conexion);
    comando.Parameters.AddWithValue("@departamento", codigoDepartamento.Trim());
    comando.Parameters.AddWithValue("@municipio", codigoMunicipio.Trim());
    return Convert.ToInt32(await comando.ExecuteScalarAsync(cancelacion)) == 1;
}

static Dictionary<string, string[]> ValidarActualizacionOrganizacion(ActualizacionOrganizacionPanel solicitud)
{
    var errores = new Dictionary<string, string[]>();
    ValidarTexto(solicitud.Nombre, "nombre", "Ingresa el nombre de la organización.", 240, errores);
    ValidarTextoOpcional(solicitud.NumeroIdentificacion, "numeroIdentificacion", 80, errores);
    ValidarTexto(solicitud.CodigoDepartamento, "codigoDepartamento", "Selecciona un departamento.", 10, errores);
    ValidarTexto(solicitud.CodigoMunicipio, "codigoMunicipio", "Selecciona un municipio.", 10, errores);
    return errores;
}

static Dictionary<string, string[]> ValidarSolicitudFestival(string? nombre, string? descripcion, string? codigoDepartamento, string? codigoMunicipio)
{
    var errores = new Dictionary<string, string[]>();
    ValidarTexto(nombre, "nombre", "Ingresa el nombre del Festival.", 240, errores);
    ValidarTextoOpcional(descripcion, "descripcion", 8000, errores);
    ValidarTexto(codigoDepartamento, "codigoDepartamento", "Selecciona un departamento.", 10, errores);
    ValidarTexto(codigoMunicipio, "codigoMunicipio", "Selecciona un municipio.", 10, errores);
    return errores;
}

static Dictionary<string, string[]> ValidarSolicitudRegistro(SolicitudRegistroExterno solicitud)
{
    var errores = new Dictionary<string, string[]>();
    ValidarTexto(solicitud.PrimerNombre, "primerNombre", "Ingresa tu primer nombre.", 120, errores);
    ValidarTexto(solicitud.PrimerApellido, "primerApellido", "Ingresa tu primer apellido.", 120, errores);
    ValidarTextoOpcional(solicitud.SegundoNombre, "segundoNombre", 120, errores);
    ValidarTextoOpcional(solicitud.SegundoApellido, "segundoApellido", 120, errores);
    var tiposPermitidos = new[] { "CC", "CE", "PA", "PPT", "DIE", "CD" };
    if (!tiposPermitidos.Contains(solicitud.CodigoTipoIdentificacion?.Trim().ToUpperInvariant())) errores["codigoTipoIdentificacion"] = ["Selecciona un tipo de identificación válido."];
    ValidarTexto(solicitud.NumeroIdentificacion, "numeroIdentificacion", "Ingresa tu número de identificación.", 120, errores);
    if (string.IsNullOrWhiteSpace(solicitud.Correo)) errores["correo"] = ["Ingresa tu correo electrónico."];
    else if (!System.Net.Mail.MailAddress.TryCreate(solicitud.Correo.Trim(), out _)) errores["correo"] = ["Ingresa un correo electrónico válido."];
    ValidarTextoOpcional(solicitud.Telefono, "telefono", 40, errores);
    if (string.IsNullOrWhiteSpace(solicitud.Contrasena)) errores["contrasena"] = ["Crea una contraseña."];
    else if (solicitud.Contrasena.Length < 12) errores["contrasena"] = ["La contraseña debe tener al menos 12 caracteres."];
    ValidarTexto(solicitud.NombreOrganizacion, "nombreOrganizacion", "Ingresa el nombre de la organización.", 240, errores);
    ValidarTextoOpcional(solicitud.NumeroIdentificacionOrganizacion, "numeroIdentificacionOrganizacion", 80, errores);
    ValidarTexto(solicitud.CodigoDepartamento, "codigoDepartamento", "Selecciona un departamento.", 10, errores);
    ValidarTexto(solicitud.CodigoMunicipio, "codigoMunicipio", "Selecciona un municipio.", 10, errores);
    return errores;
}

static void ValidarTexto(string? valor, string campo, string mensajeVacio, int longitudMaxima, IDictionary<string, string[]> errores)
{
    if (string.IsNullOrWhiteSpace(valor)) errores[campo] = [mensajeVacio];
    else if (valor.Trim().Length > longitudMaxima) errores[campo] = [$"Este campo no puede superar {longitudMaxima} caracteres."];
}

static void ValidarTextoOpcional(string? valor, string campo, int longitudMaxima, IDictionary<string, string[]> errores)
{
    if (!string.IsNullOrWhiteSpace(valor) && valor.Trim().Length > longitudMaxima) errores[campo] = [$"Este campo no puede superar {longitudMaxima} caracteres."];
}

static async Task<PreparacionRegistro> ObtenerPreparacionRegistroAsync(SqlConnection conexion, CancellationToken cancelacion)
{
    const string consulta = """
        SELECT Id,Codigo,Version,Titulo,UrlPublica FROM legal.Documentos
        WHERE Codigo IN (N'terminos_uso',N'tratamiento_datos') AND EsVigente=1 ORDER BY Codigo;
        SELECT (SELECT COUNT(*) FROM territorio.Departamentos),(SELECT COUNT(*) FROM territorio.Municipios);
        """;
    await using var comando = new SqlCommand(consulta, conexion);
    await using var lector = await comando.ExecuteReaderAsync(cancelacion);
    var documentos = new List<DocumentoConsentimiento>();
    while (await lector.ReadAsync(cancelacion)) documentos.Add(new DocumentoConsentimiento(lector.GetGuid(0), lector.GetString(1), lector.GetString(3), lector.GetString(2), lector.GetString(4)));
    await lector.NextResultAsync(cancelacion);
    await lector.ReadAsync(cancelacion);
    var territorioDisponible = lector.GetInt32(0) > 0 && lector.GetInt32(1) > 0;
    var impedimentos = new List<string>();
    if (documentos.Count < 2) impedimentos.Add("Faltan los documentos vigentes de términos de uso y tratamiento de datos.");
    if (!territorioDisponible) impedimentos.Add("Falta incorporar el catálogo oficial de departamentos y municipios.");
    return new PreparacionRegistro(impedimentos.Count == 0, territorioDisponible, documentos, impedimentos);
}

app.Run();

public sealed record RespuestaSalud(string Api, string BaseDatos);
public sealed record DocumentoConsentimiento(Guid Id, string Codigo, string Titulo, string Version, string UrlPublica);
public sealed record PreparacionRegistro(bool RegistroDisponible, bool TerritorioDisponible, IReadOnlyList<DocumentoConsentimiento> Documentos, IReadOnlyList<string> Impedimentos);
public sealed record TerritorioRegistro(string Codigo, string Nombre);
public sealed record ErrorApi(string Codigo, string Mensaje, string TrazaId, IReadOnlyDictionary<string, string[]>? Campos = null);
public sealed record SolicitudIngreso(string Correo, string Contrasena);
public sealed record SolicitudRegistroExterno(string PrimerNombre, string? SegundoNombre, string PrimerApellido, string? SegundoApellido, string CodigoTipoIdentificacion, string NumeroIdentificacion, string Correo, string? Telefono, string Contrasena, string NombreOrganizacion, string? NumeroIdentificacionOrganizacion, string CodigoDepartamento, string CodigoMunicipio, IReadOnlyList<string>? CodigosDocumentosAceptados);
public sealed record PersonaPanel(string PrimerNombre, string PrimerApellido, string Correo, string? Telefono);
public sealed record OrganizacionPanel(Guid Id, string Nombre, string? NumeroIdentificacion, string Estado, string CodigoDepartamento, string Departamento, string CodigoMunicipio, string Municipio, DateTime FechaActualizacion);
public sealed record ContextoPanel(PersonaPanel Persona, IReadOnlyList<OrganizacionPanel> Organizaciones);
public sealed record AdministradorOrganizacionPanel(Guid IdPersona, string Nombre, string Correo, string? Telefono, DateTime FechaAsignacion);
public sealed record ActualizacionOrganizacionPanel(string Nombre, string? NumeroIdentificacion, string CodigoDepartamento, string CodigoMunicipio);
public sealed record FestivalPanel(Guid IdFestival, Guid IdPerfil, string Nombre, string EstadoEditorial, string? Descripcion, string CodigoDepartamento, string Departamento, string CodigoMunicipio, string Municipio, DateTime FechaActualizacion, Guid IdOrganizacion, string Organizacion);
public sealed record SolicitudCrearFestival(Guid IdOrganizacion, string Nombre, string? Descripcion, string CodigoDepartamento, string CodigoMunicipio);
public sealed record SolicitudActualizarFestival(string Nombre, string? Descripcion, string CodigoDepartamento, string CodigoMunicipio);
public sealed class OpcionesSesion
{
    public int MinutosInactividad { get; init; }
    public int HorasMaximas { get; init; }
    public int MinutosAvisoPrevio { get; init; }
}

public partial class Program;
