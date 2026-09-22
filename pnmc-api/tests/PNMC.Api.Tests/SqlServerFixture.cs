using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-014a. Arnes paralelo y opcional: monta la API sobre una base de SQL Server
/// nueva y desechable, construida con los guiones de esquema reales del repositorio.
///
/// <para>
/// CICLO DE VIDA DE LA BASE. Se crea una base con nombre unico
/// (<c>PNMC_PRUEBAS_&lt;fecha&gt;_&lt;azar&gt;</c>), se le aplican en orden los guiones de
/// <c>pnmc-database/schema/*.sql</c>, y al terminar la corrida se borra. El borrado va
/// en <c>DisposeAsync</c> y, si falla, la prueba se pone roja con el nombre de la base
/// a mano: una base huerfana en el servidor del dueno es peor que no tener la prueba,
/// asi que este arnes prefiere gritar antes que dejarla ahi callando.
/// </para>
///
/// <para>
/// ADEMAS BARRE HUERFANAS. Un <c>dotnet test</c> matado a media corrida no llega a
/// <c>DisposeAsync</c>. Por eso, al arrancar, se borran las bases con el prefijo de la
/// via cuya fecha de creacion pasa de dos horas. El filtro por prefijo y la validacion
/// de <see cref="ArnesSqlServer.ExigirNombreDesechable"/> hacen que
/// <c>PNMC_LOCAL</c> —la base de desarrollo del dueno— quede fuera de alcance por
/// construccion.
/// </para>
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// DIVERGENCIAS CONOCIDAS ENTRE EL ESQUEMA REAL Y EL MODELO DE EF.
    ///
    /// <para>
    /// Cada linea de aqui seria un DEFECTO PENDIENTE, no una comodidad del arnes: una columna
    /// que el modelo de EF mapea y que ninguna de las dos fuentes de esquema reales crea sobre
    /// una base nueva. Sin el remiendo, cualquier consulta de EF sobre esa tabla falla contra
    /// SQL Server con "Invalid column name".
    /// </para>
    ///
    /// <para>
    /// HOY ESTA VACIA, y la unica que hubo merece quedar contada porque casi se lleva por
    /// delante todas las pruebas de SQL Server. Era <c>EntidadesAliadas.LogoUrl</c>: el modelo
    /// la mapeaba y ninguna fuente de esquema la creaba, asi que el remiendo hacia
    /// <c>IF COL_LENGTH(N'dbo.EntidadesAliadas', N'LogoUrl') IS NULL ALTER TABLE ... ADD</c>.
    /// <b><c>COL_LENGTH</c> devuelve NULL tambien cuando la tabla no existe.</b> En cuanto la
    /// retirada de aliados (22 ago 2026) dejo de crearla, ese <c>ALTER</c> habria pasado la
    /// guarda, fallado, y tumbado el montaje del fixture entero —MuestraSqlServerTests,
    /// Pnmc059 y Pnmc060 incluidas—, con un error que no menciona a los aliados por ningun
    /// lado.
    /// </para>
    ///
    /// <para>
    /// LECCION PARA LA PROXIMA LINEA QUE SE AÑADA AQUI: guardar el remiendo tambien por
    /// <c>OBJECT_ID(...) IS NOT NULL</c>, no solo por <c>COL_LENGTH</c>. Una guarda que no
    /// distingue «la columna falta» de «la tabla no esta» convierte una retirada limpia en
    /// una caida del arnes.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Descripcion, string Sql)> DivergenciasReconciliadas = [];

    /// <summary>
    /// Separador de lotes de sqlcmd. El <c>\r?</c> del final no es decorativo: los
    /// guiones del repositorio terminan las lineas en CRLF y, en modo multilinea, el
    /// ancla <c>$</c> de .NET casa justo antes del <c>\n</c> —el <c>\r</c> queda por
    /// medio—. Sin ese <c>\r?</c> el patron no casa ni una sola vez, los lotes se envian
    /// pegados y SQL Server responde "CREATE TRIGGER must be the first statement in a
    /// query batch": un fallo que apunta al guion y no al separador.
    /// </summary>
    private static readonly Regex SeparadorDeLotes = new(
        @"^[\t ]*GO[\t ]*(?:--[^\r\n]*)?\r?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private SqlServerWebApplicationFactory? _factory;

    /// <summary>Nombre de la base desechable de esta corrida.</summary>
    public string NombreDeBase { get; private set; } = string.Empty;

    /// <summary>Cadena de conexion a la base desechable de esta corrida.</summary>
    public string CadenaDeConexion { get; private set; } = string.Empty;

    /// <summary>Guiones de esquema aplicados, en el orden en que se aplicaron.</summary>
    public IReadOnlyList<string> GuionesAplicados { get; private set; } = [];

    /// <summary>Guiones de siembra de referencia aplicados, en orden.</summary>
    public IReadOnlyList<string> SiembraAplicada { get; private set; } = [];

    public IServiceProvider Services => Fabrica.Services;

    public HttpClient CrearCliente() => Fabrica.CreateClient();

    private SqlServerWebApplicationFactory Fabrica => _factory
        ?? throw new InvalidOperationException(
            "La via de SQL Server esta apagada. " + ArnesSqlServer.MotivoDeOmision);

    /// <summary>
    /// Consulta escalar directa contra la base desechable, sin pasar por EF. Sirve para
    /// preguntarle al motor por su propio catalogo (<c>sys.triggers</c>,
    /// <c>sys.procedures</c>, <c>sys.check_constraints</c>), que es justamente lo que el
    /// modelo de EF no sabe y por tanto lo que el arnes de SQLite no puede comprobar.
    /// </summary>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "El arnés solo recibe SQL constante de pruebas versionadas; no procesa entrada externa.")]
    public async Task<object?> EscalarAsync(string sql)
    {
        await using var conexion = new SqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandTimeout = 60;
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return valor == DBNull.Value ? null : valor;
    }

    public async Task InitializeAsync()
    {
        // Cuando la via esta apagada, el arnes no toca la red ni el disco. xunit
        // construye el fixture de coleccion aunque todas sus pruebas esten omitidas;
        // si esto intentase conectar, un portatil sin Docker veria la coleccion entera
        // en error y la suite dejaria de estar verde por una via que nadie pidio.
        if (!ArnesSqlServer.Habilitado)
        {
            return;
        }

        var cadenaMaestra = ArnesSqlServer.CadenaMaestra();

        await BarrerBasesHuerfanasAsync(cadenaMaestra);

        NombreDeBase = ArnesSqlServer.NuevoNombreDeBase();
        ArnesSqlServer.ExigirNombreDesechable(NombreDeBase);

        await EjecutarAsync(cadenaMaestra, $"CREATE DATABASE [{NombreDeBase}];");

        CadenaDeConexion = ArnesSqlServer.CadenaHacia(cadenaMaestra, NombreDeBase);

        try
        {
            await AplicarEsquemaRealAsync();
            await AplicarSiembraDeReferenciaAsync();
            await ReconciliarDivergenciasAsync();

            _factory = new SqlServerWebApplicationFactory(CadenaDeConexion);

            // Forzar el arranque de la aplicacion aqui, y no en la primera prueba, para
            // que la rama de SQL Server de DatabaseBootstrapper (tablas de soporte +
            // siembra del CMS) quede ejecutada y cualquier fallo suyo se lea en el
            // montaje del fixture y no disfrazado de aserto roto.
            using var cliente = _factory.CreateClient();
            var sonda = await cliente.GetAsync("/health/live");
            sonda.EnsureSuccessStatusCode();
        }
        catch (Exception fallo)
        {
            // Si el montaje falla a medias, la base ya existe: hay que borrarla igual.
            // El fallo original se conserva pase lo que pase con el borrado; perderlo
            // detras de un "no pude borrar" dejaria la causa real sin diagnosticar.
            try
            {
                await DisposeAsync();
            }
            catch (Exception falloAlBorrar)
            {
                throw new AggregateException(fallo, falloAlBorrar);
            }

            throw;
        }
    }

    public async Task DisposeAsync()
    {
        // El borrado va en el finally: si desechar la fabrica falla, la base ya existe y
        // seguiria existiendo. Dejarla atras porque se rompio un paso anterior es
        // exactamente la huerfana que este arnes no puede permitirse.
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(NombreDeBase))
            {
                var nombre = NombreDeBase;
                NombreDeBase = string.Empty;
                CadenaDeConexion = string.Empty;

                // Las conexiones de EF viven en el pozo estatico de ADO.NET y no mueren
                // al desechar la fabrica. Sin vaciarlo, el DROP choca con "database is
                // currently in use" y la base queda huerfana.
                SqlConnection.ClearAllPools();

                await BorrarBaseAsync(ArnesSqlServer.CadenaMaestra(), nombre, esObligatorio: true);
            }
        }
    }

    private async Task AplicarEsquemaRealAsync()
    {
        var directorio = LocalizarDirectorioDeEsquema();
        var guiones = Directory
            .GetFiles(directorio, "*.sql")
            .OrderBy(ruta => Path.GetFileName(ruta), StringComparer.Ordinal)
            .ToList();

        if (guiones.Count == 0)
        {
            throw new InvalidOperationException($"No hay guiones de esquema en '{directorio}'.");
        }

        var aplicados = new List<string>();
        foreach (var guion in guiones)
        {
            var texto = await File.ReadAllTextAsync(guion);

            // Los guiones traen separadores GO, que son de sqlcmd y no de T-SQL: si se
            // envian tal cual, SQL Server responde "Incorrect syntax near 'GO'". Y no se
            // pueden juntar todos los lotes en uno: CREATE TRIGGER y CREATE PROCEDURE
            // exigen ser el primer enunciado de su lote.
            foreach (var lote in SeparadorDeLotes.Split(texto))
            {
                if (string.IsNullOrWhiteSpace(lote))
                {
                    continue;
                }

                await EjecutarAsync(CadenaDeConexion, lote);
            }

            aplicados.Add(Path.GetFileName(guion));
        }

        GuionesAplicados = aplicados;
    }

    private async Task ReconciliarDivergenciasAsync()
    {
        foreach (var (_, sql) in DivergenciasReconciliadas)
        {
            await EjecutarAsync(CadenaDeConexion, sql);
        }
    }

    /// <summary>
    /// Nombres de los guiones de <c>pnmc-database/seed</c> que traen DATOS DE REFERENCIA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los otros cuatro (<c>04__contenidos_modulos</c>, <c>05__articulacion_lectura_comun</c>,
    /// <c>06__datos_prueba_amplios</c>, <c>07__datos_moderacion_consola</c>) son datos de
    /// DEMOSTRACION: los 30 Festivales, las 30 Escuelas, los 150 registros del ecosistema. Una
    /// base institucional recien creada tiene lo primero y no lo segundo, y meter aqui las
    /// filas de muestra haria que cualquier prueba pudiera pasar por accidente, encontrando
    /// datos que ella no sembro.
    /// </para>
    /// </remarks>
    private static readonly string[] SiembraDeReferencia =
    [
        "V20260519_01__maestras_estaticas_seed.sql",
        "V20260519_02__divipola_seed.sql",
    ];

    /// <summary>
    /// Aplica la siembra de referencia sobre la base desechable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POR QUE HACIA FALTA. Los guiones de <c>schema</c> CREAN <c>Divipola</c>,
    /// <c>EstadosContenido</c> y <c>Roles</c>, pero no las pueblan. Y la siembra equivalente
    /// del codigo, <c>DatabaseBootstrapper.EnsureLocalDevelopmentSeedAsync</c>, cuelga de la
    /// rama <c>else</c> del arranque: <b>solo corre cuando el proveedor NO es SQL Server</b>.
    /// El resultado era una base desechable con las tablas vacias, en la que cualquier alta
    /// moria contra una clave foranea —medido: <c>FK_Entidades_EstadosContenido</c> al crear
    /// una organizacion— por una carencia del arnes y no del producto.
    /// </para>
    /// <para>
    /// Sin esto, el arnés solo servía para comprobar la FORMA del esquema. Con esto sirve para
    /// recorrer circuitos completos, que es donde estan los defectos que SQLite no ve.
    /// </para>
    /// </remarks>
    private async Task AplicarSiembraDeReferenciaAsync()
    {
        var directorio = LocalizarDirectorioDeSiembra();
        if (directorio is null)
        {
            return;
        }

        var aplicados = new List<string>();
        foreach (var nombre in SiembraDeReferencia)
        {
            var ruta = Path.Combine(directorio, nombre);
            if (!File.Exists(ruta))
            {
                continue;
            }

            var texto = await File.ReadAllTextAsync(ruta);
            foreach (var lote in SeparadorDeLotes.Split(texto))
            {
                if (string.IsNullOrWhiteSpace(lote))
                {
                    continue;
                }

                await EjecutarAsync(CadenaDeConexion, lote);
            }

            aplicados.Add(nombre);
        }

        SiembraAplicada = aplicados;
    }

    /// <summary>
    /// Localiza <c>pnmc-database/seed</c>. Devuelve <c>null</c> si no aparece, en vez de
    /// lanzar: la ausencia de siembra deja las pruebas fallando con su propio mensaje, que es
    /// mas util que un fallo de arranque del arnes.
    /// </summary>
    private static string? LocalizarDirectorioDeSiembra()
    {
        var esquema = LocalizarDirectorioDeEsquema();
        var hermano = Path.Combine(Path.GetDirectoryName(esquema.TrimEnd(Path.DirectorySeparatorChar))!, "seed");
        return Directory.Exists(hermano) ? hermano : null;
    }

    /// <summary>
    /// Busca <c>pnmc-database/schema</c> subiendo desde el directorio del ensamblado de
    /// pruebas. No se usa una ruta relativa fija porque la profundidad de
    /// <c>bin/Debug/net10.0</c> cambia entre configuraciones y entre maquinas.
    /// </summary>
    private static string LocalizarDirectorioDeEsquema()
    {
        var indicada = Environment.GetEnvironmentVariable(ArnesSqlServer.VariableDeEsquema);
        if (!string.IsNullOrWhiteSpace(indicada))
        {
            if (!Directory.Exists(indicada))
            {
                throw new DirectoryNotFoundException(
                    $"{ArnesSqlServer.VariableDeEsquema} apunta a '{indicada}', que no existe.");
            }

            return indicada;
        }

        // DESDE LAS FUENTES PRIMERO, no desde el binario. Subir desde AppContext.BaseDirectory
        // funciona con la salida por omision —que cuelga del arbol— y deja de funcionar en cuanto
        // alguien la redirige con `-p:BaseOutputPath=<temporal>/`, que es como conviene ejecutar
        // cuando hay varias sesiones sobre el mismo arbol para no pelearse por los DLL con un API
        // en marcha. Desde ese temporal no hay repositorio por encima, asi que este metodo lanzaba
        // y con el se caia el CARRIL ENTERO de SQL Server: no una prueba, todas las que dependen
        // de este fixture. Ver RutasDelRepositorio para el porque completo.
        var desdeFuentes = RutasDelRepositorio.Esquema();
        if (Directory.Exists(desdeFuentes))
        {
            return desdeFuentes;
        }

        DirectoryInfo? actual = new(AppContext.BaseDirectory);
        for (var salto = 0; salto < 12 && actual is not null; salto++, actual = actual.Parent)
        {
            var candidato = Path.Combine(actual.FullName, "pnmc-database", "schema");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new DirectoryNotFoundException(
            "No se encontro 'pnmc-database/schema' subiendo desde " + AppContext.BaseDirectory
            + $". Indicalo con {ArnesSqlServer.VariableDeEsquema}.");
    }

    private static async Task BarrerBasesHuerfanasAsync(string cadenaMaestra)
    {
        List<string> huerfanas = [];

        await using (var conexion = new SqlConnection(cadenaMaestra))
        {
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandTimeout = 60;

            // El guion bajo es comodin en LIKE: hay que escaparlo con [_] o el filtro
            // acabaria abarcando nombres que no son de esta via.
            comando.CommandText = """
                SELECT name
                FROM sys.databases
                WHERE name LIKE 'PNMC[_]PRUEBAS[_]%'
                  AND create_date < DATEADD(hour, -2, GETDATE());
                """;

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                huerfanas.Add(lector.GetString(0));
            }
        }

        foreach (var huerfana in huerfanas)
        {
            // El filtro LIKE es mas ancho que el nombre que esta via sabe generar. Un
            // nombre con caracteres raros no se borra ni hace fallar el barrido: se deja
            // como esta. Reventar aqui dejaria la via inservible por una base ajena.
            try
            {
                ArnesSqlServer.ExigirNombreDesechable(huerfana);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            // Barrido de cortesia: si otra corrida la esta usando, se deja estar.
            await BorrarBaseAsync(cadenaMaestra, huerfana, esObligatorio: false);
        }
    }

    private static async Task BorrarBaseAsync(string cadenaMaestra, string nombreBase, bool esObligatorio)
    {
        ArnesSqlServer.ExigirNombreDesechable(nombreBase);

        // SINGLE_USER WITH ROLLBACK IMMEDIATE echa a quien quede conectado; el DROP va
        // en el mismo lote y en la misma sesion para que nadie ocupe la unica plaza
        // libre entre una cosa y la otra.
        var sql = $"""
            IF DB_ID(N'{nombreBase}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{nombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{nombreBase}];
            END;
            """;

        Exception? ultimoFallo = null;
        for (var intento = 1; intento <= 3; intento++)
        {
            try
            {
                await EjecutarAsync(cadenaMaestra, sql, segundos: 60);
                return;
            }
            catch (Exception fallo)
            {
                ultimoFallo = fallo;
                SqlConnection.ClearAllPools();
                await Task.Delay(TimeSpan.FromSeconds(intento));
            }
        }

        if (esObligatorio)
        {
            throw new InvalidOperationException(
                $"No se pudo borrar la base desechable '{nombreBase}'. Quedaria huerfana en el "
                + "servidor. Borrala a mano con: ALTER DATABASE ["
                + nombreBase + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["
                + nombreBase + "];",
                ultimoFallo);
        }
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Ejecuta únicamente guiones DbUp versionados y nombres de bases generados y validados por el arnés.")]
    private static async Task EjecutarAsync(string cadena, string sql, int segundos = 180)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandTimeout = segundos;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Coleccion de xunit de la via de SQL Server: una sola base desechable compartida por
/// todas sus pruebas, que corren en serie dentro de la coleccion.
/// </summary>
[CollectionDefinition(ColeccionSqlServer.Nombre)]
public sealed class ColeccionSqlServer : ICollectionFixture<SqlServerFixture>
{
    public const string Nombre = "sqlserver-real";
}
