using Microsoft.Extensions.Configuration;

namespace PNMC.Infrastructure.Data;

public static class DatabaseConnectionResolver
{
    public static string ResolveSqlServerConnectionString(IConfiguration configuration)
    {
        var directConnectionString = configuration["AZURE_SQL_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(directConnectionString))
        {
            return directConnectionString.Trim();
        }

        var configuredConnectionString = configuration.GetConnectionString("SqlServer");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString.Trim();
        }

        var server = configuration["AZURE_SQL_SERVER"];
        var database = configuration["AZURE_SQL_DATABASE"];
        var user = configuration["AZURE_SQL_USER"];
        var password = configuration["AZURE_SQL_PASSWORD"];
        var encrypt = configuration["AZURE_SQL_ENCRYPT"] ?? "true";
        var trustServerCertificate = configuration["AZURE_SQL_TRUST_SERVER_CERTIFICATE"] ?? "false";

        if (string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password))
        {
            return string.Empty;
        }

        var normalizedServer = server.Trim();
        if (!normalizedServer.Contains(','))
        {
            normalizedServer = $"{normalizedServer},1433";
        }

        return $"Server=tcp:{normalizedServer};Initial Catalog={database.Trim()};Persist Security Info=False;User ID={user.Trim()};Password={password};MultipleActiveResultSets=False;Encrypt={encrypt};TrustServerCertificate={trustServerCertificate};Connection Timeout=30;";
    }
}
