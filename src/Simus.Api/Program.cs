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

app.Run();

public sealed record RespuestaSalud(string Api, string BaseDatos);

public partial class Program;
