using Microsoft.Data.Sqlite;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Los dos motores NO agrupan igual el mismo texto, y el monitor depende de eso.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE ESTE FICHERO. El 24 ago 2026 el resumen por estado del monitor paso a contar
/// <b>en la base</b> en vez de traerse la columna entera. Al hacerlo aparecio una trampa: agrupar
/// en SQL a secas da resultados <b>distintos en cada motor</b>. SQL Server ordena
/// <c>PNMC_LOCAL</c> con una colacion insensible a la caja, de modo que «PUBLICADO» y «publicado»
/// caen en el mismo grupo; SQLite compara byte a byte y los deja en dos.
/// </para>
/// <para>
/// Esa diferencia es la peor clase de diferencia posible: <b>la suite habria quedado en verde
/// mientras produccion devolvia otro JSON</b>. Por eso <c>ResumenPorCodigoAsync</c> agrupa dos
/// veces —en la base por el codigo crudo, en memoria por el normalizado— y por eso hace falta
/// dejar MEDIDO, y no razonado, que los motores difieren. Una justificacion basada en «SQL Server
/// deberia hacer X» no es evidencia de nada; esta prueba pregunta a cada motor.
/// </para>
/// <para>
/// NO INSERTA NADA. Compara dos literales, asi que no depende de restricciones CHECK, ni de la
/// siembra, ni deja rastro. Lo que mide es la colacion, que es propiedad de la base.
/// </para>
/// </remarks>
public sealed class AgrupacionDeEstadosPorMotorTests
{
    /// <summary>
    /// Cuenta cuantos grupos salen al agrupar dos textos que solo difieren en la caja.
    /// </summary>
    private const string CuantosGrupos = @"
        SELECT COUNT(*) FROM (
            SELECT c FROM (SELECT 'publicado' AS c UNION ALL SELECT 'PUBLICADO' AS c) AS v
            GROUP BY c
        ) AS g;";

    [Collection(ColeccionSqlServer.Nombre)]
    public sealed class ContraSqlServerReal
    {
        private readonly SqlServerFixture _base;

        public ContraSqlServerReal(SqlServerFixture baseDesechable) => _base = baseDesechable;

        /// <summary>
        /// En la base real, agrupar por el codigo crudo FUNDE las dos cajas: un solo grupo.
        /// </summary>
        [HechoSqlServer]
        public async Task La_Base_Real_Funde_Las_Cajas_Al_Agrupar()
        {
            var colacion = Convert.ToString(
                await _base.EscalarAsync("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));"));

            Assert.NotNull(colacion);
            Assert.Contains("_CI", colacion!, StringComparison.OrdinalIgnoreCase);

            var grupos = Convert.ToInt32(await _base.EscalarAsync(CuantosGrupos));

            Assert.Equal(1, grupos);
        }
    }

    /// <summary>
    /// En SQLite —el motor de la mayor parte de la suite— salen DOS grupos. Es el control que
    /// convierte la prueba de arriba en una comparacion y no en un dato suelto.
    /// </summary>
    /// <remarks>
    /// Corre siempre, sin Docker y sin variables de entorno: si alguien apaga el carril de SQL
    /// Server, esta mitad sigue recordando que el problema existe.
    /// </remarks>
    [Fact]
    public void El_Motor_De_La_Suite_Deja_Dos_Grupos()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        using var orden = conexion.CreateCommand();
        orden.CommandText = CuantosGrupos;

        var grupos = Convert.ToInt32(orden.ExecuteScalar());

        Assert.Equal(2, grupos);
    }
}
