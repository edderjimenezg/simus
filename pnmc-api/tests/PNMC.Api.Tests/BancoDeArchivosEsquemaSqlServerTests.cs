using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El banco de archivos contra SQL Server de verdad.
///
/// <para>
/// <b>POR QUE ESTE FICHERO EXISTE, y la lección que lo trajo.</b> El resto de la suite corre sobre
/// SQLite fabricado desde el modelo de EF, y SQLite no tiene las restricciones que solo declara el
/// esquema. Con las ocho pruebas del banco en verde, la segunda subida real devolvió un 500:
/// <c>UQ_Archivos_RutaAlmacenamiento</c> es única y el código escribía la misma ruta en todas las
/// filas. Una prueba sobre SQLite no puede cazar eso.
/// </para>
/// <para>
/// Es la misma lección que dejó escrita <see cref="CatalogoEditorialEsquemaSqlServerTests"/>, y por
/// eso esta comprobación vive aquí y no allí: lo que se mide son las reglas que SOLO existen en la
/// base.
/// </para>
/// </summary>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class BancoDeArchivosEsquemaSqlServerTests
{
    private readonly SqlServerFixture _base;

    public BancoDeArchivosEsquemaSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

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
            foreach (var prefijo in new[] { "UQ_", "CK_" })
            {
                var inicio = mensaje.IndexOf(prefijo, StringComparison.Ordinal);
                if (inicio < 0) continue;
                // SQL SERVER NO USA LA MISMA COMILLA EN LOS DOS CASOS: cierra el nombre de una
                // UNIQUE con simple y el de una CHECK con doble. Gana la que aparezca antes.
                var simple = mensaje.IndexOf('\'', inicio);
                var doble = mensaje.IndexOf('"', inicio);
                var fin = (simple, doble) switch
                {
                    (< 0, < 0) => -1,
                    (< 0, _) => doble,
                    (_, < 0) => simple,
                    _ => Math.Min(simple, doble),
                };
                return fin < 0 ? mensaje[inicio..] : mensaje[inicio..fin];
            }
            return $"(otra causa) {mensaje}";
        }
    }

    /// <summary>
    /// Inserta un archivo tomando prestado el primer usuario que haya.
    /// </summary>
    /// <remarks>
    /// `IdUsuarioCarga` tiene foránea a `Usuarios` y la base desechable nace con las cuentas de
    /// arranque; fijar un `1` a mano hacía fallar el INSERT por el motivo equivocado y la prueba
    /// medía la foránea en vez de lo que dice medir.
    /// </remarks>
    private static string Insertar(string nombre, string ruta, long peso = 100, string mime = "image/png") =>
        "INSERT INTO dbo.Archivos (NombreOriginal, NombreAlmacenado, Extension, TipoMime, PesoBytes, RutaAlmacenamiento, IdUsuarioCarga, FechaCarga) " +
        $"SELECT N'{nombre}', N'{nombre}', N'.png', N'{mime}', {peso.ToString(System.Globalization.CultureInfo.InvariantCulture)}, N'{ruta}', MIN(IdUsuario), SYSUTCDATETIME() FROM dbo.Usuarios;";

    /// <summary>
    /// Deja una cuenta en la base desechable, que nace sin ninguna.
    /// </summary>
    /// <remarks>
    /// `IdUsuarioCarga` tiene foránea a `Usuarios`, y sin fila el INSERT fallaba por el motivo
    /// equivocado: la prueba medía la foránea en vez de lo que dice medir.
    /// </remarks>
    private async Task SembrarUsuarioAsync() => await RestriccionQueRechazaAsync(
        "IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios) " +
        "INSERT INTO dbo.Usuarios (NombreCompleto, CorreoElectronico, HashContrasena) " +
        "VALUES (N'Prueba de esquema', N'esquema@pnmc.local', N'x');");

    [Fact]
    public async Task Dos_archivos_no_pueden_compartir_la_ruta_de_almacenamiento()
    {
        await SembrarUsuarioAsync();
        await RestriccionQueRechazaAsync(Insertar("uno.png", "/ruta/compartida.png"));

        var restriccion = await RestriccionQueRechazaAsync(Insertar("dos.png", "/ruta/compartida.png"));

        // ESTA ES LA PRUEBA QUE FALTABA: el código escribía la misma ruta en todas las filas y la
        // segunda subida real reventaba con un 500 que no mencionaba el banco.
        Assert.Equal("UQ_Archivos_RutaAlmacenamiento", restriccion);
    }

    [Fact]
    public async Task Una_imagen_por_encima_de_dos_megas_la_rechaza_la_base()
    {
        await SembrarUsuarioAsync();
        // EL TOPE VIVE EN LOS DOS SITIOS: la mitad del código rechaza con un mensaje útil, la de
        // la base impide que una importación que escriba directo meta un fichero de cien megas.
        var restriccion = await RestriccionQueRechazaAsync(
            Insertar("gorda.png", "/ruta/gorda.png", peso: 2 * 1024 * 1024 + 1));

        Assert.Equal("CK_Archivos_PesoPorTipo", restriccion);
    }

    [Fact]
    public async Task Un_documento_admite_veinte_megas()
    {
        await SembrarUsuarioAsync();
        // Diez veces el de una imagen y para un solo tipo: un PDF no se sirve en línea ni se carga
        // entero para reconocerlo.
        var restriccion = await RestriccionQueRechazaAsync(
            Insertar("informe.pdf", "/ruta/informe.pdf", peso: 20 * 1024 * 1024, mime: "application/pdf"));

        Assert.Null(restriccion);
    }

    [Fact]
    public async Task Un_evento_municipal_sin_departamento_lo_rechaza_la_base()
    {
        // La regla que septiembre tenía y nosotros no: el nivel de cobertura va atado a sus
        // códigos, y un municipio suelto no significa nada.
        var restriccion = await RestriccionQueRechazaAsync(
            "INSERT INTO dbo.EventosAgenda (Slug, Titulo, Descripcion, FechaInicio, Modalidad, Lugar, NivelCobertura, CodigoMunicipio) " +
            "VALUES (N'ck-cobertura', N'Evento', N'Descripción', '2026-10-10', N'presencial', N'Teatro', N'municipal', N'15516');");

        Assert.Equal("CK_EventosAgenda_NivelCobertura", restriccion);
    }
}
