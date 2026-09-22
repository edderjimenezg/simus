using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// ¿Una base construida SOLO con los guiones de <c>pnmc-database/schema/</c> tiene todo lo
/// que el modelo de EF espera encontrar?
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. El 23 de agosto de 2026 se midio que trece tablas y cinco columnas
/// de <c>PNMC_LOCAL</c> no las creaba ningun guion: las anadia el arranque del API con
/// DDL embebido, y solo con <c>Database:EnsureSupportTables=true</c>. Produccion lo pone
/// en <c>false</c>. Sobre una base hecha con guiones y ese perfil, el primer login
/// institucional moria con «Invalid column name 'Telefono'» y el CMS arrancaba sin tablas.
/// </para>
/// <para>
/// Y NINGUNA PRUEBA PODIA VERLO. La suite corre sobre SQLite, fabricada desde el propio
/// modelo —por definicion identica a el—, y el arnes de SQL Server arranca la aplicacion
/// antes de la primera prueba, con lo que el bootstrapper reparaba la divergencia justo
/// a tiempo. El arnes escondia exactamente el defecto que habia que medir.
/// </para>
/// <para>
/// LO QUE HACE. Crea una base desechable, le aplica UNICAMENTE <c>schema/*.sql</c> —sin
/// siembra y sin arrancar el API—, y recorre el modelo de EF comprobando que cada tabla
/// mapeada exista y que cada columna mapeada exista en ella. Un solo fallo lista TODO lo
/// que falta, no lo primero que encuentra: la lista es el trabajo pendiente.
/// </para>
/// <para>
/// Corre solo con el carril de SQL Server encendido (<c>PNMC_PRUEBAS_SQLSERVER=1</c>),
/// como las demas de ese carril. Se comprobo que muerde: sin
/// <c>V20260823_02__objetos_del_arranque.sql</c> falla listando las trece tablas.
/// </para>
/// </remarks>
public sealed class ParidadEsquemaSinArranqueTests
{
    private static readonly Regex SeparadorDeLotes = new(
        @"^[\t ]*GO[\t ]*(?:--[^\r\n]*)?\r?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [HechoSqlServer]
    public async Task Toda_Tabla_Y_Columna_Que_EF_Mapea_Existe_En_Una_Base_Hecha_Solo_Con_Guiones()
    {
        var cadenaMaestra = ArnesSqlServer.CadenaMaestra();
        var nombreBase = ArnesSqlServer.NuevoNombreDeBase();
        ArnesSqlServer.ExigirNombreDesechable(nombreBase);
        var cadena = ArnesSqlServer.CadenaHacia(cadenaMaestra, nombreBase);

        await EjecutarAsync(cadenaMaestra, $"CREATE DATABASE [{nombreBase}];");
        try
        {
            await AplicarSoloEsquemaAsync(cadena);

            var faltantes = await ColumnasQueFaltanAsync(cadena);

            Assert.True(
                faltantes.Count == 0,
                "Objetos que el modelo de EF mapea y que NINGUN guion de schema/ crea " +
                "(el arranque del API los anadia en silencio; en Produccion no corre):\n  "
                + string.Join("\n  ", faltantes));
        }
        finally
        {
            await EjecutarAsync(
                cadenaMaestra,
                $"IF DB_ID(N'{nombreBase}') IS NOT NULL BEGIN ALTER DATABASE [{nombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombreBase}]; END");
        }
    }


    [Fact]
    public async Task El_Bootstrapper_No_Contiene_DDL_De_SQL_Server()
    {
        var codigo = await File.ReadAllTextAsync(LocalizarBootstrapper());

        Assert.DoesNotContain("OBJECT_ID(", codigo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COL_LENGTH(", codigo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE [", codigo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE [", codigo, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocalizarBootstrapper()
    {
        var raiz = Directory.GetParent(LocalizarDirectorioDeEsquema())!.Parent!.FullName;
        var ruta = Path.Combine(raiz, "pnmc-api", "src", "PNMC.Infrastructure", "Data", "DatabaseBootstrapper.cs");
        Assert.True(File.Exists(ruta), $"No se encontro DatabaseBootstrapper.cs en {ruta}.");
        return ruta;
    }

    private static async Task<List<string>> ColumnasQueFaltanAsync(string cadena)
    {
        // Lo que hay de verdad en la base recien construida.
        var columnasReales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var conexion = new SqlConnection(cadena))
        {
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS";
            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                columnasReales.Add($"{lector.GetString(0)}.{lector.GetString(1)}");
            }
        }
        var tablasReales = new HashSet<string>(
            columnasReales.Select(c => c.Split('.')[0]), StringComparer.OrdinalIgnoreCase);

        // Lo que el modelo de EF da por hecho. Se construye el contexto contra SQL Server
        // para que el modelo sea el de produccion, no el de SQLite.
        var opciones = new DbContextOptionsBuilder<PnmcDbContext>().UseSqlServer(cadena).Options;
        await using var db = new PnmcDbContext(opciones);

        var faltantes = new List<string>();
        foreach (var entidad in db.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName();
            if (tabla is null)
            {
                continue;
            }

            if (!tablasReales.Contains(tabla))
            {
                faltantes.Add($"TABLA {tabla} (entidad {entidad.ClrType.Name})");
                continue;
            }

            var identificador = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());
            foreach (var propiedad in entidad.GetProperties())
            {
                var columna = propiedad.GetColumnName(identificador);
                if (columna is not null && !columnasReales.Contains($"{tabla}.{columna}"))
                {
                    faltantes.Add($"COLUMNA {tabla}.{columna} (propiedad {entidad.ClrType.Name}.{propiedad.Name})");
                }
            }
        }

        return faltantes;
    }

    private static async Task AplicarSoloEsquemaAsync(string cadena)
    {
        var directorio = LocalizarDirectorioDeEsquema();
        var guiones = Directory
            .GetFiles(directorio, "*.sql")
            .OrderBy(ruta => Path.GetFileName(ruta), StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(guiones);

        foreach (var guion in guiones)
        {
            var texto = await File.ReadAllTextAsync(guion);
            var numero = 0;
            foreach (var lote in SeparadorDeLotes.Split(texto))
            {
                numero++;
                if (string.IsNullOrWhiteSpace(lote))
                {
                    continue;
                }

                try
                {
                    await EjecutarAsync(cadena, lote);
                }
                catch (SqlException excepcion)
                {
                    // Decir QUE guion y QUE lote: «Incorrect syntax near '.'» a secas no localiza nada.
                    throw new InvalidOperationException(
                        $"Fallo el lote {numero} de {Path.GetFileName(guion)}: {excepcion.Message}", excepcion);
                }
            }
        }
    }

    /// <summary>
    /// Ver <see cref="RutasDelRepositorio"/>. Antes se subia desde <c>AppContext.BaseDirectory</c>,
    /// lo que hacia imposible ejecutar estas pruebas con la salida redirigida a un temporal.
    /// </summary>
    private static string LocalizarDirectorioDeEsquema() => RutasDelRepositorio.Esquema();

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "La entrada son lotes de los guiones de esquema versionados, nunca datos de una solicitud.")]
    private static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandTimeout = 180;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
