using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

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

app.Run();

public sealed record RespuestaSalud(string Api, string BaseDatos);
public sealed record DisponibilidadRegistro(bool RegistroDisponible, IReadOnlyList<string> Impedimentos);

public partial class Program;
