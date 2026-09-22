using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// LAS RESTRICCIONES DE <c>dbo.MediosWeb</c> MUERDEN DE VERDAD.
/// <para>
/// <b>Por qué esta clase existe y por qué está en este carril.</b> La suite por omisión corre
/// sobre SQLite fabricada desde el modelo de EF (<c>TestWebApplicationFactory</c>,
/// <c>UseSqlite</c> + <c>EnsureCreated</c>). Ninguno de los diecisiete CHECK —trece en la tabla
/// y cuatro en su historial, contados contra <c>sys.check_constraints</c>— de
/// <c>V20260829_01__medios_web.sql</c> existe allí: el modelo mapea columnas, no restricciones.
/// Sin esta clase, la suite estaría verde sobre diecisiete garantías que jamás se ejercitaron, y la
/// primera vez que una de ellas hiciera falta sería en la base real.
/// </para>
/// <para>
/// Es la misma lección que dejó escrita <c>AltaDeUsuarioSqlServerTests</c>: el carril rápido no
/// puede desmentirte sobre restricciones que su base no tiene.
/// </para>
/// <para>
/// SE ESCRIBE POR SQL DIRECTO Y NO POR EL API, a propósito. Lo que se prueba aquí es que la BASE
/// rechaza el dato aunque el API se equivoque —o aunque alguien conecte otro cliente—. Pasar por
/// el API mediría la validación de C#, que ya tiene sus propias pruebas.
/// </para>
/// </summary>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class ImagenesWebEsquemaSqlServerTests
{
    private readonly SqlServerFixture _base;

    public ImagenesWebEsquemaSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    private const string ClaveEditable = "prueba_medios_editable";
    private const string ClaveNoEditable = "prueba_medios_no_editable";

    /// <summary>Ocho bytes que son una firma PNG. Sirven de contenido cualquiera.</summary>
    private const string BytesPng = "0x89504E470D0A1A0A";

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Solo ejecuta sentencias constantes definidas en esta prueba de restricciones SQL.")]
    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = new SqlConnection(_base.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var orden = conexion.CreateCommand();
        orden.CommandText = sql;
        await orden.ExecuteNonQueryAsync();
    }

    /// <summary>Devuelve el nombre del CHECK que rechazó la orden, o null si la aceptó.</summary>
    private async Task<string?> RestriccionQueRechazaAsync(string sql)
    {
        try
        {
            await EjecutarAsync(sql);
            return null;
        }
        catch (SqlException excepcion)
        {
            // SQL Server nombra la restricción dentro del mensaje. Se devuelve el nombre y no un
            // booleano: sin él, una fila rechazada POR OTRA razón contaría como éxito, que es la
            // forma más común de que una prueba de restricciones mienta.
            var mensaje = excepcion.Message;
            var inicio = mensaje.IndexOf("CK_MediosWeb", StringComparison.Ordinal);
            if (inicio < 0)
            {
                return $"(otra causa) {mensaje}";
            }

            var fin = mensaje.IndexOf('"', inicio);
            return fin < 0 ? mensaje[inicio..] : mensaje[inicio..fin];
        }
    }

    private async Task PrepararAsync()
    {
        await EjecutarAsync($"""
            DELETE FROM dbo.MediosWeb WHERE Clave LIKE N'prueba_medios_%';
            INSERT INTO dbo.MediosWeb (Clave, GrupoId, GrupoEtiqueta, Seccion, Etiqueta, Uso, Editable)
            VALUES (N'{ClaveEditable}', N'g', N'G', N'S', N'E', N'fondo', 1),
                   (N'{ClaveNoEditable}', N'g', N'G', N'S', N'E', N'logotipo', 0);
            """);
    }

    private static string ActualizarBorrador(string clave, string extra)
        => $"UPDATE dbo.MediosWeb SET {extra} WHERE Clave = N'{clave}';";

    /// <summary>
    /// El caso legítimo pasa. VA PRIMERO Y NO ES DECORATIVO: si una fila correcta se rechazara,
    /// todos los casos de abajo estarían en verde por el motivo equivocado y la prueba no mediría
    /// las restricciones sino un error de sintaxis.
    /// </summary>
    [HechoSqlServer]
    public async Task UnaFilaCorrectaSeAcepta()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"BorradorContenido = {BytesPng}, BorradorTipo = N'image/png', BorradorBytes = 8, " +
            $"BorradorAncho = 64, BorradorAlto = 40, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Null(resultado);
    }

    /// <summary>
    /// Una clave no editable no guarda bytes, y lo impide la BASE. Es la garantía que no depende
    /// de que el API se acuerde de comprobarlo: reemplazar el escudo nacional sigue siendo
    /// imposible aunque alguien borre la guarda de <c>ImagenesWebEndpoints</c>.
    /// </summary>
    [HechoSqlServer]
    public async Task UnaClaveNoEditableNoPuedeGuardarBytes()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveNoEditable,
            $"BorradorContenido = {BytesPng}, BorradorTipo = N'image/png', BorradorBytes = 8, " +
            $"BorradorAncho = 1, BorradorAlto = 1, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Equal("CK_MediosWeb_NoEditableSinBytes", resultado);
    }

    /// <summary>
    /// Los bytes declarados son los bytes guardados. Sin esto, <c>BorradorBytes</c> es un número
    /// que el panel muestra y que nadie comprueba.
    /// </summary>
    [HechoSqlServer]
    public async Task LosBytesDeclaradosTienenQueCoincidirConLosGuardados()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"BorradorContenido = {BytesPng}, BorradorTipo = N'image/png', BorradorBytes = 99999, " +
            $"BorradorAncho = 1, BorradorAlto = 1, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Equal("CK_MediosWeb_BytesReales", resultado);
    }

    /// <summary>
    /// SVG no entra ni por SQL directo. Un SVG servido desde el mismo origen que el sitio es XSS
    /// almacenado con permiso de webmaster.
    /// </summary>
    [HechoSqlServer]
    public async Task ElTipoSvgNoEntraNiPorSqlDirecto()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            "BorradorContenido = 0x3C737667, BorradorTipo = N'image/svg+xml', BorradorBytes = 4, " +
            "BorradorAncho = 1, BorradorAlto = 1, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Equal("CK_MediosWeb_BorradorTipo", resultado);
    }

    /// <summary>
    /// LA HUELLA EN MAYÚSCULAS SE RECHAZA, y este caso existe para demostrar que el
    /// <c>COLLATE Latin1_General_BIN2</c> del CHECK está puesto.
    /// <para>
    /// La base local es <c>SQL_Latin1_General_CP1_CI_AS</c> —no distingue mayúsculas—, así que sin
    /// el COLLATE el <c>NOT LIKE '%[^0-9a-f]%'</c> acepta la 'A'. Comprobado sembrando una tabla
    /// gemela sin COLLATE: aceptó sesenta y cuatro 'A'. Es decir, el COLLATE es la guarda entera y
    /// el LIKE por sí solo no filtra nada del caso que importa.
    /// </para>
    /// </summary>
    [HechoSqlServer]
    public async Task LaHuellaEnMayusculasSeRechazaGraciasAlCollate()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"BorradorContenido = {BytesPng}, BorradorTipo = N'image/png', BorradorBytes = 8, " +
            "BorradorAncho = 1, BorradorAlto = 1, BorradorHuella = REPLICATE('A', 64)"));

        Assert.Equal("CK_MediosWeb_HuellaHex", resultado);
    }

    /// <summary>
    /// Cada mitad está entera o no está. Sin esto cabe una fila con bytes y sin tipo, y el
    /// endpoint público serviría un <c>Content-Type</c> nulo.
    /// </summary>
    [HechoSqlServer]
    public async Task UnaMitadIncompletaSeRechaza()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"BorradorContenido = {BytesPng}, BorradorBytes = 8"));

        Assert.Equal("CK_MediosWeb_BorradorCompleto", resultado);
    }

    /// <summary>
    /// Publicado y retirado a la vez es imposible. El API lo deriva en código; aquí lo impide
    /// además la base, de modo que el estado no puede quedar en dos sitios a la vez.
    /// </summary>
    [HechoSqlServer]
    public async Task NoSePuedeEstarPublicadoYRetiradoALaVez()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"PublicadoContenido = {BytesPng}, PublicadoTipo = N'image/png', PublicadoBytes = 8, " +
            "PublicadoAncho = 1, PublicadoAlto = 1, PublicadoHuella = REPLICATE('a', 64), " +
            "FechaRetiro = SYSUTCDATETIME()"));

        Assert.Equal("CK_MediosWeb_NoPublicadoYRetirado", resultado);
    }

    /// <summary>
    /// El tope de 2 MiB también en la base. Se comprueba con un valor declarado por encima del
    /// tope, sin mover dos megas por el cable.
    /// </summary>
    [HechoSqlServer]
    public async Task ElTopeDeDosMebibytesTambienLoImponeLaBase()
    {
        await PrepararAsync();

        // Se usa REPLICATE para fabricar 2 MiB + 1 byte sin enviarlos: el CHECK del tope se
        // evalúa sobre BorradorBytes, y CK_MediosWeb_BytesReales obliga a que ese número sea el
        // real, así que hay que construir el contenido de verdad.
        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            "BorradorContenido = CAST(REPLICATE(CAST(0x00 AS varbinary(max)), 2097153) AS varbinary(max)), " +
            "BorradorTipo = N'image/png', BorradorBytes = 2097153, " +
            "BorradorAncho = 1, BorradorAlto = 1, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Equal("CK_MediosWeb_BorradorTope", resultado);
    }

    /// <summary>
    /// La bomba de descompresión, en la base: unas dimensiones imposibles no se guardan aunque el
    /// reconocedor de C# no estuviera.
    /// </summary>
    [HechoSqlServer]
    public async Task UnasDimensionesImposiblesSeRechazan()
    {
        await PrepararAsync();

        var resultado = await RestriccionQueRechazaAsync(ActualizarBorrador(ClaveEditable,
            $"BorradorContenido = {BytesPng}, BorradorTipo = N'image/png', BorradorBytes = 8, " +
            "BorradorAncho = 60000, BorradorAlto = 60000, BorradorHuella = REPLICATE('a', 64)"));

        Assert.Equal("CK_MediosWeb_Dimensiones", resultado);
    }
}
