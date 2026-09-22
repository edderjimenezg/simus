using PNMC.Api.Endpoints;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-060 — La base admite <c>agrupacion</c> como tipo de entidad.
/// </summary>
/// <remarks>
/// <para>
/// La agrupacion es el creador externo del piloto Festival. <c>CK_Entidades_Tipo</c> es una
/// lista cerrada y hasta <c>V20260822_01__agrupaciones.sql</c> no incluia ese valor. Sin este guion, dar de alta una agrupacion habria fallado contra SQL Server
/// exactamente igual que fallaba dar de alta una organizacion antes de añadir el estado
/// <c>registrada</c>: con un 500 y sin que ninguna prueba de la suite se enterara.
/// </para>
/// <para>
/// POR QUE VIVE EN LA VIA DE SQL SERVER. Porque <c>CK_Entidades_Tipo</c> <b>no existe</b> en
/// la base de la suite: EF la fabrica desde el modelo y el modelo no declara esa
/// restriccion. Una prueba de SQLite escribiendo <c>agrupacion</c> pasaria en verde tanto
/// con el guion como sin el, es decir no probaria nada. Su gemela declarativa —que las
/// constantes del codigo coinciden con la lista de la base— si corre siempre, en
/// <see cref="Pnmc059VocabularioEstadosTests"/>.
/// </para>
/// <para>
/// Omitida salvo que se encienda <c>PNMC_PRUEBAS_SQLSERVER=1</c>. Ver <see cref="ArnesSqlServer"/>.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class Pnmc060AgrupacionesSqlServerTests
{
    private readonly SqlServerFixture _base;

    public Pnmc060AgrupacionesSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    /// <summary>
    /// La restriccion real acepta el unico actor declarado y rechaza todo lo demas.
    /// </summary>
    /// <remarks>
    /// Las dos mitades importan. Comprobar solo que <c>agrupacion</c> entra dejaria pasar una
    /// restriccion borrada por accidente: si <c>CK_Entidades_Tipo</c> desapareciera, la
    /// primera mitad seguiria verde y la tabla habria dejado de proteger nada. Por eso se
    /// comprueba tambien que un tipo inventado se rechaza.
    /// </remarks>
    [HechoSqlServer]
    public async Task La_Base_Acepta_Solo_Organizacion_Y_Rechaza_Los_Demas()
    {
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_Entidades_Tipo';")));

        var (departamento, municipio) = await TerritorioAsync();

        foreach (var tipo in TiposDeEntidad.Admitidos)
        {
            var aceptado = await IntentarAltaAsync(tipo, departamento, municipio);
            Assert.True(aceptado, $"CK_Entidades_Tipo rechazo '{tipo}', que el codigo declara admitido.");
        }

        var rechazado = await IntentarAltaAsync("agrupacion", departamento, municipio);
        Assert.False(rechazado,
            "CK_Entidades_Tipo acepto 'agrupacion', que el usuario retiro el 12 de septiembre de "
            + "2026. Si la restriccion se borro, la tabla dejo de proteger el vocabulario.");

        // Y RECHAZA LOS SIETE DE SEPTIEMBRE. `festival` es el que mas importa: mientras la tabla de
        // actores lo admitiera, una fila podia decir que es un festival y ser administrada como si
        // fuera la organizacion que lo organiza. En esta base un Festival es dbo.Festivales.
        foreach (var tipoDeProceso in new[] { "festival", "escuela_musica", "mercado_musical", "espacio", "lutier" })
        {
            Assert.False(
                await IntentarAltaAsync(tipoDeProceso, departamento, municipio),
                $"CK_Entidades_Tipo acepto '{tipoDeProceso}', que nombra un proceso y no un actor.");
        }
    }

    /// <summary>
    /// Inserta y deshace. Devuelve si la base admitio el valor.
    /// </summary>
    /// <remarks>
    /// El <c>ROLLBACK</c> no es cortesia: sin el, esta prueba dejaria nueve entidades
    /// sembradas que otra prueba de la misma coleccion podria encontrar y dar por suyas.
    /// </remarks>
    private async Task<bool> IntentarAltaAsync(string tipo, string departamento, string municipio)
    {
        var resultado = Convert.ToString(await _base.EscalarAsync($"""
            SET QUOTED_IDENTIFIER ON;
            BEGIN TRY
                BEGIN TRAN;
                INSERT INTO dbo.Entidades
                    (TipoEntidad, Nombre, CodigoDepartamentoSede, CodigoMunicipioSede,
                     EstadoRegistro, Activo, IdUsuarioCreador, FechaCreacion)
                VALUES
                    (N'{tipo}', N'Sonda de tipos', N'{departamento}', N'{municipio}',
                     N'{EstadosDeOrganizacion.Activa}', 1, 1, SYSUTCDATETIME());
                ROLLBACK;
                SELECT 'ACEPTADO';
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK;
                SELECT 'RECHAZADO';
            END CATCH;
            """));

        return resultado == "ACEPTADO";
    }

    private async Task<(string Departamento, string Municipio)> TerritorioAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP (1) CodigoDepartamento + N'|' + CodigoMunicipio FROM dbo.Divipola "
            + "ORDER BY CodigoDepartamento, CodigoMunicipio;"));
        Assert.False(string.IsNullOrWhiteSpace(par),
            "dbo.Divipola esta vacia: comprueba que el arnes aplico la siembra de referencia.");
        var partes = par!.Split('|');
        return (partes[0], partes[1]);
    }
}
