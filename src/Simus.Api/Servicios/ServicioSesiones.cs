using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Simus.Api.Servicios;

public sealed class ServicioSesiones(OpcionesSesion opciones)
{
    public async Task<Guid?> ValidarAsync(SqlConnection conexion, string secreto, CancellationToken cancelacion)
    {
        var ahora = DateTime.UtcNow;
        const string sql = """
            UPDATE identidad.Sesiones SET FechaUltimaActividad=@ahora, FechaVencimientoInactividad=@vence
            OUTPUT INSERTED.IdPersona
            WHERE HashSecreto=@hash AND FechaRevocacion IS NULL AND FechaVencimientoInactividad>@ahora AND FechaVencimientoMaximo>@ahora;
            """;
        await using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@hash", SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secreto)));
        comando.Parameters.AddWithValue("@ahora", ahora);
        comando.Parameters.AddWithValue("@vence", ahora.AddMinutes(opciones.MinutosInactividad));
        var resultado = await comando.ExecuteScalarAsync(cancelacion);
        return resultado is Guid idPersona ? idPersona : null;
    }

    public async Task CerrarAsync(SqlConnection conexion, string secreto, CancellationToken cancelacion)
    {
        const string sql = "UPDATE identidad.Sesiones SET FechaRevocacion=SYSUTCDATETIME() WHERE HashSecreto=@hash AND FechaRevocacion IS NULL;";
        await using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@hash", SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secreto)));
        await comando.ExecuteNonQueryAsync(cancelacion);
    }

    public async Task<(string Secreto, DateTime VenceEn)> CrearAsync(SqlConnection conexion, Guid idPersona, CancellationToken cancelacion)
    {
        var secreto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var ahora = DateTime.UtcNow;
        var venceInactividad = ahora.AddMinutes(opciones.MinutosInactividad);
        var venceMaximo = ahora.AddHours(opciones.HorasMaximas);
        const string sql = """
            INSERT INTO identidad.Sesiones (Id,IdPersona,HashSecreto,FechaInicio,FechaUltimaActividad,FechaVencimientoInactividad,FechaVencimientoMaximo)
            VALUES (@id,@persona,@hash,@ahora,@ahora,@inactividad,@maximo);
            """;
        await using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", Guid.NewGuid());
        comando.Parameters.AddWithValue("@persona", idPersona);
        comando.Parameters.AddWithValue("@hash", SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secreto)));
        comando.Parameters.AddWithValue("@ahora", ahora);
        comando.Parameters.AddWithValue("@inactividad", venceInactividad);
        comando.Parameters.AddWithValue("@maximo", venceMaximo);
        await comando.ExecuteNonQueryAsync(cancelacion);
        return (secreto, venceInactividad < venceMaximo ? venceInactividad : venceMaximo);
    }
}
