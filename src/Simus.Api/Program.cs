using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration.GetConnectionString("Simus");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Json(new HealthResponse("unavailable", "not-configured"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return Results.Ok(new HealthResponse("available", "available"));
    }
    catch (SqlException)
    {
        return Results.Json(new HealthResponse("available", "unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public sealed record HealthResponse(string Api, string Database);

public partial class Program;
