using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

const string SchemaVersion = "001_identity";
var connectionArgument = args.SkipWhile(value => value != "--connection").Skip(1).FirstOrDefault();
if (string.IsNullOrWhiteSpace(connectionArgument))
{
    Console.Error.WriteLine("Uso: dotnet run --project src/Simus.Database.Setup -- --connection '<cadena de conexión>'");
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
await EnsureSchemaAsync(database);
await BootstrapWebmasterAsync(database);
Console.WriteLine("Esquema SIMUS preparado sin datos demostrativos.");
return 0;

static async Task EnsureSchemaAsync(SqlConnection connection)
{
    await using var versionCheck = new SqlCommand("SELECT COUNT(*) FROM core.SchemaVersions WHERE Version = @version;", connection);
    versionCheck.Parameters.AddWithValue("@version", SchemaVersion);
    if (Convert.ToInt32(await versionCheck.ExecuteScalarAsync()) > 0) return;

    var scriptPath = Path.Combine(AppContext.BaseDirectory, "Schema", "001_identity.sql");
    var script = await File.ReadAllTextAsync(scriptPath);
    await using var transaction = await connection.BeginTransactionAsync();
    await using var command = new SqlCommand(script, connection, (SqlTransaction)transaction);
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}

static async Task BootstrapWebmasterAsync(SqlConnection connection)
{
    var email = Environment.GetEnvironmentVariable("SIMUS_BOOTSTRAP_WEBMASTER_EMAIL")?.Trim().ToLowerInvariant();
    var password = Environment.GetEnvironmentVariable("SIMUS_BOOTSTRAP_WEBMASTER_PASSWORD");
    if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("El aprovisionamiento requiere correo y contraseña de al menos 12 caracteres.");

    await using var transaction = await connection.BeginTransactionAsync();
    const string alreadyBootstrapped = "SELECT COUNT(*) FROM core.BootstrapEvents WHERE EventCode = 'first_webmaster';";
    await using var check = new SqlCommand(alreadyBootstrapped, connection, (SqlTransaction)transaction);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) { await transaction.CommitAsync(); return; }

    var personId = Guid.NewGuid();
    var hasher = new PasswordHasher<object>();
    var hash = hasher.HashPassword(new object(), password);
    const string insert = """
        INSERT INTO identity.Persons (Id, GivenName, FirstSurname, IdentityTypeCode, IdentityNumberNormalized, EmailNormalized, PasswordHash, AccountState, EmailVerificationState, CreatedAt, UpdatedAt)
        VALUES (@id, N'Webmaster', N'SIMUS', N'CONFIG', @email, @email, @hash, N'active', N'not_configured', SYSUTCDATETIME(), SYSUTCDATETIME());
        INSERT INTO identity.PersonRoles (PersonId, RoleCode, GrantedAt) VALUES (@id, N'webmaster', SYSUTCDATETIME());
        INSERT INTO core.BootstrapEvents (EventCode, CompletedAt) VALUES (N'first_webmaster', SYSUTCDATETIME());
        """;
    await using var command = new SqlCommand(insert, connection, (SqlTransaction)transaction);
    command.Parameters.AddWithValue("@id", personId);
    command.Parameters.AddWithValue("@email", email);
    command.Parameters.AddWithValue("@hash", hash);
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}
