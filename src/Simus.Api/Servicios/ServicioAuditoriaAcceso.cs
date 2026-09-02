using Microsoft.Data.SqlClient;

namespace Simus.Api.Servicios;

public sealed class ServicioAuditoriaAcceso
{
    public async Task RegistrarAsync(SqlConnection conexion, Guid? idPersona, string evento, string trazaId, CancellationToken cancelacion)
    {
        const string sql = "INSERT INTO identidad.EventosAcceso (Id,IdPersona,Evento,TrazaId,Fecha) VALUES (@id,@persona,@evento,@traza,SYSUTCDATETIME());";
        await using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", Guid.NewGuid());
        comando.Parameters.AddWithValue("@persona", (object?)idPersona ?? DBNull.Value);
        comando.Parameters.AddWithValue("@evento", evento);
        comando.Parameters.AddWithValue("@traza", trazaId);
        await comando.ExecuteNonQueryAsync(cancelacion);
    }
}
