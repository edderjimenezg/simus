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
        .AllowAnyMethod()));
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(opciones => opciones.AddFixedWindowLimiter("ingreso", limites =>
{
    limites.PermitLimit = 5;
    limites.Window = TimeSpan.FromMinutes(15);
    limites.QueueLimit = 0;
}));

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

app.MapGet("/api/registro/disponibilidad", async (IConfiguration configuracion, CancellationToken cancelacion) =>
{
    var cadenaConexion = configuracion.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
        return Results.Ok(new DisponibilidadRegistro(false, ["La base de datos aún no está configurada."]));

    try
    {
        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancelacion);
        const string consulta = """
            SELECT (SELECT COUNT(*) FROM legal.Documentos WHERE Codigo IN (N'terminos_uso', N'tratamiento_datos') AND EsVigente = 1),
                   (SELECT COUNT(*) FROM territorio.Departamentos),
                   (SELECT COUNT(*) FROM territorio.Municipios);
            """;
        await using var comando = new SqlCommand(consulta, conexion);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        await lector.ReadAsync(cancelacion);
        var impedimentos = new List<string>();
        if (lector.GetInt32(0) < 2) impedimentos.Add("Faltan los documentos vigentes de términos de uso y tratamiento de datos.");
        if (lector.GetInt32(1) == 0 || lector.GetInt32(2) == 0) impedimentos.Add("Falta incorporar el catálogo oficial de departamentos y municipios.");
        return Results.Ok(new DisponibilidadRegistro(impedimentos.Count == 0, impedimentos));
    }
    catch (SqlException)
    {
        return Results.Ok(new DisponibilidadRegistro(false, ["La base de datos no está disponible temporalmente."]));
    }
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
    contexto.Response.Cookies.Append("simus_sesion", secreto, new CookieOptions { HttpOnly = true, Secure = !app.Environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Expires = venceEn });
    await auditoria.RegistrarAsync(conexion, idPersona, "ingreso_exitoso", contexto.TraceIdentifier, cancelacion);
    return Results.Ok(new { idPersona });
}).RequireRateLimiting("ingreso");

app.Run();

public sealed record RespuestaSalud(string Api, string BaseDatos);
public sealed record DisponibilidadRegistro(bool RegistroDisponible, IReadOnlyList<string> Impedimentos);
public sealed record ErrorApi(string Codigo, string Mensaje, string TrazaId, IReadOnlyDictionary<string, string[]>? Campos = null);
public sealed record SolicitudIngreso(string Correo, string Contrasena);
public sealed class OpcionesSesion
{
    public int MinutosInactividad { get; init; }
    public int HorasMaximas { get; init; }
    public int MinutosAvisoPrevio { get; init; }
}

public partial class Program;
