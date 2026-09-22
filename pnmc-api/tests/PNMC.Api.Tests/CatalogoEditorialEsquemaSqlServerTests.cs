using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El Catálogo Editorial contra SQL Server de verdad.
///
/// <para>
/// <b>POR QUE ESTE FICHERO EXISTE, y la lección que lo trajo.</b> El resto de la suite corre sobre
/// SQLite fabricado desde el modelo de EF, y SQLite no tiene las CHECK de SQL Server. Con las
/// pruebas en verde, la primera llamada real al API devolvió un 500: la bitácora rechazó el
/// registro porque <c>ISJSON</c> no acepta un escalar —<c>"Acento"</c> es JSON válido para
/// cualquier analizador y no para esa comprobación— y porque el verbo de la acción tiene lista
/// blanca. Ninguna de las dos cosas puede fallar en SQLite.
/// </para>
/// <para>
/// Lo que se mide aquí son las reglas que SOLO existen en la base: el destino único de un acceso,
/// las listas cerradas de estados y el formato que exige la bitácora.
/// </para>
/// </summary>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CatalogoEditorialEsquemaSqlServerTests
{
    private readonly SqlServerFixture _base;

    public CatalogoEditorialEsquemaSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Solo ejecuta sentencias constantes definidas en esta prueba de restricciones SQL.")]
    private async Task<string?> RestriccionQueRechazaAsync(string sql)
    {
        try
        {
            await using var conexion = new SqlConnection(_base.CadenaDeConexion);
            await conexion.OpenAsync();
            await using var orden = conexion.CreateCommand();
            orden.CommandText = sql;
            await orden.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqlException excepcion)
        {
            var mensaje = excepcion.Message;
            var inicio = mensaje.IndexOf("CK_", StringComparison.Ordinal);
            if (inicio < 0) return $"(otra causa) {mensaje}";
            var fin = mensaje.IndexOf('"', inicio);
            return fin < 0 ? mensaje[inicio..] : mensaje[inicio..fin];
        }
    }

    private static string InsertarPublicacion(string codigo, string catalogacion = "pendiente_revision", string publicacion = "borrador") =>
        $"INSERT INTO dbo.PublicacionesEditoriales (Codigo, Titulo, EstadoCatalogacion, EstadoPublicacion) " +
        $"VALUES (N'{codigo}', N'Ficha de prueba', N'{catalogacion}', N'{publicacion}');";

    [Fact]
    public async Task Un_estado_de_catalogacion_inventado_lo_rechaza_la_base()
    {
        var restriccion = await RestriccionQueRechazaAsync(InsertarPublicacion("CK-CAT-01", catalogacion: "en_evaluacion"));

        Assert.Equal("CK_PublicacionesEditoriales_Catalogacion", restriccion);
    }

    [Fact]
    public async Task Un_estado_de_publicacion_inventado_lo_rechaza_la_base()
    {
        var restriccion = await RestriccionQueRechazaAsync(InsertarPublicacion("CK-PUB-01", publicacion: "visible"));

        Assert.Equal("CK_PublicacionesEditoriales_Publicacion", restriccion);
    }

    [Fact]
    public async Task El_tipo_de_agente_va_en_minuscula_y_la_base_rechaza_el_capitalizado()
    {
        // POR QUE ESTA PRUEBA EXISTE. El adaptador del portal comparaba el tipo de agente contra
        // «Entidad» capitalizado, un valor que ni el API emite ni la base admite. La comparación no
        // se cumplía nunca, así que la autoría corporativa jamás se separó de la personal y la fila
        // «Autor corporativo» de la ficha pública no llegó a verse en ninguna publicación. Nada
        // falló: simplemente faltaba un dato en pantalla. Aquí queda escrito cuál es el valor bueno.
        var restriccion = await RestriccionQueRechazaAsync(
            $"INSERT INTO dbo.AgentesEditoriales (Codigo, Tipo, NombrePreferido) " +
            $"VALUES (N'CK-AGE-{Guid.NewGuid():N}', N'Entidad', N'Ministerio de Prueba');");

        Assert.Equal("CK_AgentesEditoriales_Tipo", restriccion);
    }

    [Fact]
    public async Task La_base_admite_los_tipos_de_agente_que_declara_el_contrato()
    {
        // Y EL OTRO LADO DE LA MISMA MONEDA: que el vocabulario del contrato entre de verdad. Con
        // solo la prueba de rechazo, un contrato que se quedara corto seguiría pareciendo sano.
        foreach (var tipo in CatalogoEditorialContrato.TiposAgente)
        {
            var restriccion = await RestriccionQueRechazaAsync(
                $"INSERT INTO dbo.AgentesEditoriales (Codigo, Tipo, NombrePreferido) " +
                $"VALUES (N'CK-AGE-{Guid.NewGuid():N}', N'{tipo}', N'Agente de prueba {tipo}');");

            Assert.Null(restriccion);
        }
    }

    [Fact]
    public async Task El_anio_final_no_puede_ser_anterior_al_inicial()
    {
        var restriccion = await RestriccionQueRechazaAsync(
            "INSERT INTO dbo.PublicacionesEditoriales (Codigo, Titulo, AnioInicio, AnioFin) " +
            "VALUES (N'CK-ANIO-01', N'Ficha de prueba', 2020, 2015);");

        Assert.Equal("CK_PublicacionesEditoriales_Anios", restriccion);
    }

    [Fact]
    public async Task Un_acceso_con_dos_destinos_lo_rechaza_la_base()
    {
        // LA MISMA REGLA QUE COMPRUEBA `TieneDestinoValido` EN EL CONTRATO, impuesta también aquí.
        // Si solo viviera en el código, la primera importación que escriba directo la saltaría.
        await RestriccionQueRechazaAsync(InsertarPublicacion("CK-ACC-01"));

        var restriccion = await RestriccionQueRechazaAsync(
            "INSERT INTO dbo.AccesosEditoriales (PublicacionEditorialId, Tipo, Url, UbicacionFisica) " +
            "SELECT IdPublicacionEditorial, N'enlace', N'https://www.mincultura.gov.co/x', N'Centro de documentación' " +
            "FROM dbo.PublicacionesEditoriales WHERE Codigo = N'CK-ACC-01';");

        Assert.Equal("CK_AccesosEditoriales_UnSoloDestino", restriccion);
    }

    [Fact]
    public async Task Un_acceso_de_tipo_archivo_sin_archivo_lo_rechaza_la_base()
    {
        await RestriccionQueRechazaAsync(InsertarPublicacion("CK-ACC-02"));

        var restriccion = await RestriccionQueRechazaAsync(
            "INSERT INTO dbo.AccesosEditoriales (PublicacionEditorialId, Tipo, Url) " +
            "SELECT IdPublicacionEditorial, N'archivo', N'https://www.mincultura.gov.co/x' " +
            "FROM dbo.PublicacionesEditoriales WHERE Codigo = N'CK-ACC-02';");

        Assert.Equal("CK_AccesosEditoriales_UnSoloDestino", restriccion);
    }

    [Fact]
    public async Task La_bitacora_rechaza_un_valor_que_no_sea_objeto_JSON()
    {
        // ESTA ES LA PRUEBA QUE FALTABA. Un escalar pasa por JSON en cualquier analizador y no
        // pasa `ISJSON`, así que auditar con `JsonSerializer.Serialize(titulo)` reventaba el
        // INSERT entero con un 500 que no mencionaba el catálogo.
        var restriccion = await RestriccionQueRechazaAsync(
            "INSERT INTO dbo.BitacoraAuditoria (IdUsuario, TablaAfectada, IdRegistroAfectado, Accion, ValoresNuevos) " +
            "VALUES (NULL, N'PublicacionesEditoriales', N'1', N'crear', N'\"Acento\"');");

        Assert.Equal("CK_BitacoraAuditoria_ValoresNuevos_JSON", restriccion);
    }

    [Fact]
    public async Task La_bitacora_acepta_los_verbos_que_usa_el_catalogo_editorial()
    {
        // Los cuatro que escribe `CatalogoEditorialEndpoints`. Si alguien añade una operación con
        // un verbo nuevo, esta prueba es la que dice que la lista blanca no lo admite.
        foreach (var verbo in new[] { "crear", "actualizar", "publicar", "archivar" })
        {
            var restriccion = await RestriccionQueRechazaAsync(
                "INSERT INTO dbo.BitacoraAuditoria (IdUsuario, TablaAfectada, IdRegistroAfectado, Accion, ValoresNuevos) " +
                $"VALUES (NULL, N'PublicacionesEditoriales', N'1', N'{verbo}', N'{{\"estadoPublicacion\":\"publicado\"}}');");

            Assert.Null(restriccion);
        }
    }
}
