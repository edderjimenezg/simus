using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

var versionesEsquema = new[]
{
    new VersionEsquema("001_identidad", "001_identidad.sql"),
    new VersionEsquema("002_festivales", "002_festivales.sql")
};
var connectionArgument = args.SkipWhile(value => value != "--conexion").Skip(1).FirstOrDefault();
if (string.IsNullOrWhiteSpace(connectionArgument))
{
    Console.Error.WriteLine("Uso: dotnet run --project src/Simus.Preparar.BaseDatos -- --conexion '<cadena de conexión>' [--importar-divipola]");
    return 2;
}

var target = new SqlConnectionStringBuilder(connectionArgument);
var databaseName = string.IsNullOrWhiteSpace(target.InitialCatalog) ? "simus_nucleo" : target.InitialCatalog;
if (!Regex.IsMatch(databaseName, "^[A-Za-z0-9_]+$"))
{
    Console.Error.WriteLine("El nombre de la base solo puede usar letras, números y guion bajo.");
    return 2;
}

var master = new SqlConnectionStringBuilder(connectionArgument) { InitialCatalog = "master" };
await using (var connection = new SqlConnection(master.ConnectionString))
{
    await connection.OpenAsync();
    await using var command = new SqlCommand($"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];", connection);
    await command.ExecuteNonQueryAsync();
}

await using var database = new SqlConnection(target.ConnectionString);
await database.OpenAsync();
await EnsureSchemaAsync(database, versionesEsquema);
if (args.Contains("--importar-divipola", StringComparer.Ordinal)) await ImportadorDivipola.ImportarAsync(database);
await BootstrapWebmasterAsync(database);
Console.WriteLine("Esquema SIMUS preparado sin datos demostrativos.");
return 0;

static async Task EnsureSchemaAsync(SqlConnection connection, IReadOnlyList<VersionEsquema> versiones)
{
    foreach (var version in versiones)
    {
        const string consultaVersion = """
            IF OBJECT_ID(N'nucleo.VersionesEsquema', N'U') IS NULL
                SELECT CAST(0 AS int);
            ELSE
                SELECT COUNT(*) FROM nucleo.VersionesEsquema WHERE Version = @version;
            """;
        await using var versionCheck = new SqlCommand(consultaVersion, connection);
        versionCheck.Parameters.AddWithValue("@version", version.Codigo);
        if (Convert.ToInt32(await versionCheck.ExecuteScalarAsync()) > 0) continue;

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Esquema", version.Archivo);
        var script = await File.ReadAllTextAsync(scriptPath);
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new SqlCommand(script, connection, (SqlTransaction)transaction);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}

static async Task BootstrapWebmasterAsync(SqlConnection connection)
{
    var email = Environment.GetEnvironmentVariable("SIMUS_INICIALIZACION_ADMINISTRADOR_CORREO")?.Trim().ToLowerInvariant();
    var password = Environment.GetEnvironmentVariable("SIMUS_INICIALIZACION_ADMINISTRADOR_CONTRASENA");
    if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("El aprovisionamiento requiere correo y contraseña de al menos 12 caracteres.");

    await using var transaction = await connection.BeginTransactionAsync();
    const string alreadyBootstrapped = "SELECT COUNT(*) FROM nucleo.EventosInicializacion WHERE CodigoEvento = 'primer_administrador_sistema';";
    await using var check = new SqlCommand(alreadyBootstrapped, connection, (SqlTransaction)transaction);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) { await transaction.CommitAsync(); return; }

    var personId = Guid.NewGuid();
    var hasher = new PasswordHasher<object>();
    var hash = hasher.HashPassword(new object(), password);
    const string insert = """
        INSERT INTO identidad.Personas (Id, PrimerNombre, PrimerApellido, CodigoTipoIdentificacion, NumeroIdentificacionNormalizado, CorreoNormalizado, HashContrasena, EstadoCuenta, EstadoVerificacionCorreo, FechaCreacion, FechaActualizacion)
        VALUES (@id, N'Administración', N'SIMUS', N'CONFIG', @email, @email, @hash, N'activa', N'no_configurada', SYSUTCDATETIME(), SYSUTCDATETIME());
        INSERT INTO identidad.PersonasRoles (IdPersona, CodigoRol, FechaAsignacion) VALUES (@id, N'administrador_sistema', SYSUTCDATETIME());
        INSERT INTO nucleo.EventosInicializacion (CodigoEvento, FechaFinalizacion) VALUES (N'primer_administrador_sistema', SYSUTCDATETIME());
        """;
    await using var command = new SqlCommand(insert, connection, (SqlTransaction)transaction);
    command.Parameters.AddWithValue("@id", personId);
    command.Parameters.AddWithValue("@email", email);
    command.Parameters.AddWithValue("@hash", hash);
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}

sealed record VersionEsquema(string Codigo, string Archivo);
