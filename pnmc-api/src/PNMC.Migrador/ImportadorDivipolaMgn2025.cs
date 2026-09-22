using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PNMC.Migrador;

/// <summary>
/// Incorpora el catálogo territorial oficial, sin convertir el DANE en una dependencia del
/// formulario. El acceso remoto existe sólo en esta operación explícita de preparación.
/// </summary>
/// <remarks>
/// <para>
/// <b>SE PIDE TAMBIEN EL CENTROIDE, Y ESA ES LA UNICA COORDENADA ADMISIBLE.</b> Hasta el 12 de
/// septiembre de 2026 la consulta iba con <c>returnGeometry=false</c> y sin centroide, así que
/// <c>dbo.Divipola</c> quedaba con los 1.122 municipios y las 1.122 coordenadas en nulo. El
/// geovisor lo notaba tarde: el modo de símbolos proporcionales no dibujaba un solo círculo y
/// culpaba a las fichas de «no tener municipio», cuando lo que faltaba era el catálogo.
/// </para>
/// <para>
/// <b>NO SE CALCULA AQUI NINGUN CENTROIDE.</b> ArcGIS devuelve el del propio MGN con
/// <c>returnCentroid=true</c>, que es un punto garantizado dentro del polígono oficial y cuesta
/// dos números por municipio en vez de traerse los polígonos enteros. Promediar vértices por
/// nuestra cuenta daría un punto distinto del oficial —y fuera del municipio en los que tienen
/// forma de herradura—, es decir, una coordenada inventada con apariencia de dato.
/// </para>
/// <para>
/// <b>SIN CENTROIDE NO SE ESCRIBE CERO.</b> Un municipio que el servicio no sitúe se queda con la
/// coordenada en nulo: (0, 0) es un punto en el golfo de Guinea y el mapa lo dibujaría.
/// </para>
/// </remarks>
internal static class ImportadorDivipolaMgn2025
{
    private const string Version = "MGN 2025";
    private const string Servicio = "https://geoportal.dane.gov.co/mparcgis/rest/services/Divipola/Serv_DIVIPOLA_MGN_2025/FeatureServer";

    public static async Task ImportarAsync(SqlConnection conexion, CancellationToken cancelacion = default)
    {
        await VerificarEsquemaAsync(conexion, cancelacion);

        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd("SIMUS-Importador-DIVIPOLA/1.0");

        var departamentos = await ConsultarDepartamentosAsync(cliente, cancelacion);
        var municipios = await ConsultarMunicipiosAsync(cliente, departamentos, cancelacion);
        if (departamentos.Count == 0 || municipios.Count != 1122)
            throw new InvalidOperationException($"El DANE no devolvió el corte esperado de DIVIPOLA MGN 2025 (departamentos: {departamentos.Count}; municipios: {municipios.Count}). No se modificó la base.");

        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancelacion);
        try
        {
            foreach (var municipio in municipios)
                await ConciliarMunicipioAsync(conexion, transaccion, municipio, cancelacion);

            await RegistrarFuenteAsync(conexion, transaccion, departamentos.Count, municipios.Count, cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }
        catch
        {
            await transaccion.RollbackAsync(cancelacion);
            throw;
        }

        var situados = municipios.Count(item => item.Latitud is not null && item.Longitud is not null);
        Console.WriteLine($"DIVIPOLA {Version} incorporada: {departamentos.Count} departamentos y {municipios.Count} municipios ({situados} con coordenada).");
        if (situados < municipios.Count)
        {
            // SE AVISA AQUI Y NO EN EL MAPA. Quien corre el importador puede volver a lanzarlo; quien
            // abre el geovisor tres semanas después sólo ve municipios que no salen.
            Console.WriteLine($"Aviso: {municipios.Count - situados} municipio(s) quedaron sin coordenada. El mapa los contará como «sin situar».");
        }
    }

    private static async Task VerificarEsquemaAsync(SqlConnection conexion, CancellationToken cancelacion)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'dbo.CatalogosReferenciaFuente', N'U') IS NULL THEN 0 ELSE 1 END;";
        await using var comando = new SqlCommand(sql, conexion);
        if (Convert.ToInt32(await comando.ExecuteScalarAsync(cancelacion)) != 1)
            throw new InvalidOperationException("Falta la tabla de procedencia DIVIPOLA. Ejecuta primero `./scripts/schema-local.sh migrar`.");
    }

    private static async Task<List<Departamento>> ConsultarDepartamentosAsync(HttpClient cliente, CancellationToken cancelacion)
    {
        var rasgos = await ConsultarRasgosAsync(cliente, 319, "1=1", "DPTO_CCDGO,DPTO_CNMBRE", conCentroide: false, cancelacion);
        return rasgos.Select(rasgo => rasgo.GetProperty("attributes")).Select(item => new Departamento(
            Requerido(item, "DPTO_CCDGO"), Requerido(item, "DPTO_CNMBRE"))).DistinctBy(item => item.Codigo).ToList();
    }

    private static async Task<List<Municipio>> ConsultarMunicipiosAsync(HttpClient cliente, IReadOnlyCollection<Departamento> departamentos, CancellationToken cancelacion)
    {
        var idsUrl = $"{Servicio}/317/query?where=1%3D1&returnIdsOnly=true&f=json";
        using var idsRespuesta = await cliente.GetAsync(idsUrl, cancelacion);
        idsRespuesta.EnsureSuccessStatusCode();
        await using var idsContenido = await idsRespuesta.Content.ReadAsStreamAsync(cancelacion);
        using var idsDocumento = await JsonDocument.ParseAsync(idsContenido, cancellationToken: cancelacion);
        if (!idsDocumento.RootElement.TryGetProperty("objectIds", out var ids))
            throw new InvalidOperationException("El servicio MGN 2025 no devolvió identificadores de municipios.");

        var resultado = new List<Municipio>();
        foreach (var grupo in ids.EnumerateArray().Select(item => item.GetInt32()).Chunk(100))
        {
            var donde = $"OBJECTID IN ({string.Join(',', grupo)})";
            var rasgos = await ConsultarRasgosAsync(cliente, 317, donde, "DPTO_CCDGO,MPIO_CCDGO,MPIO_CNMBRE", conCentroide: true, cancelacion);
            resultado.AddRange(rasgos.Select(rasgo =>
            {
                var atributos = rasgo.GetProperty("attributes");
                var (latitud, longitud) = LeerCentroide(rasgo);
                return new Municipio(
                    Requerido(atributos, "DPTO_CCDGO"), Requerido(atributos, "MPIO_CCDGO"), Requerido(atributos, "MPIO_CNMBRE"))
                {
                    Latitud = latitud,
                    Longitud = longitud,
                };
            }));
        }

        var nombresDepartamento = departamentos.ToDictionary(item => item.Codigo, item => item.Nombre, StringComparer.Ordinal);
        return resultado
            .DistinctBy(item => (item.CodigoDepartamento, item.CodigoMunicipal))
            .Select(item => item with
            {
                NombreDepartamento = nombresDepartamento.TryGetValue(item.CodigoDepartamento, out var nombre)
                    ? nombre
                    : throw new InvalidOperationException($"El municipio {item.Nombre} refiere un departamento no informado por DIVIPOLA: {item.CodigoDepartamento}.")
            })
            .ToList();
    }

    /// <summary>
    /// Consulta una capa y devuelve los rasgos completos —atributos y, si se pide, centroide—.
    /// </summary>
    /// <remarks>
    /// <c>outSR=4326</c> va siempre que se pida el centroide: el MGN se publica en coordenadas
    /// planas y sin forzar WGS84 llegarían metros, que puestos en una latitud darían un punto en
    /// mitad del océano sin que nada fallara.
    /// </remarks>
    private static async Task<List<JsonElement>> ConsultarRasgosAsync(HttpClient cliente, int capa, string donde, string campos, bool conCentroide, CancellationToken cancelacion)
    {
        var centroide = conCentroide ? "&returnCentroid=true&outSR=4326" : string.Empty;
        var url = $"{Servicio}/{capa}/query?where={Uri.EscapeDataString(donde)}&outFields={Uri.EscapeDataString(campos)}&returnGeometry=false{centroide}&f=json";
        using var respuesta = await cliente.GetAsync(url, cancelacion);
        respuesta.EnsureSuccessStatusCode();
        await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancelacion);
        using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancelacion);
        if (!documento.RootElement.TryGetProperty("features", out var elementos)) return [];
        return elementos.EnumerateArray().Select(item => item.Clone()).ToList();
    }

    /// <summary>
    /// El centroide del rasgo, redondeado a lo que admite la columna, o nulo si no vino.
    /// </summary>
    /// <remarks>
    /// SE DESCARTA LO QUE NO CAIGA EN COLOMBIA. Un centroide fuera del rectángulo del país delata
    /// una proyección distinta de la pedida, y guardarlo pondría un municipio en otro continente;
    /// es preferible dejarlo sin situar, que el mapa ya sabe contar.
    /// </remarks>
    private static (decimal? Latitud, decimal? Longitud) LeerCentroide(JsonElement rasgo)
    {
        if (!rasgo.TryGetProperty("centroid", out var centroide) || centroide.ValueKind != JsonValueKind.Object)
            return (null, null);
        if (!centroide.TryGetProperty("x", out var x) || !centroide.TryGetProperty("y", out var y))
            return (null, null);
        if (!x.TryGetDouble(out var longitud) || !y.TryGetDouble(out var latitud))
            return (null, null);
        if (!double.IsFinite(longitud) || !double.IsFinite(latitud)) return (null, null);
        if (longitud is < -82 or > -66 || latitud is < -5 or > 14) return (null, null);

        return (Math.Round((decimal)latitud, 6), Math.Round((decimal)longitud, 6));
    }

    private static string Requerido(JsonElement atributos, string campo) =>
        atributos.TryGetProperty(campo, out var valor) && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : throw new InvalidOperationException($"DIVIPOLA no informó el campo obligatorio {campo}.");

    private static async Task ConciliarMunicipioAsync(SqlConnection conexion, SqlTransaction transaccion, Municipio municipio, CancellationToken cancelacion)
    {
        const string sql = """
            MERGE dbo.Divipola AS destino
            USING (SELECT @departamento AS CodigoDepartamento, @municipio AS CodigoMunicipio) AS origen
            ON destino.CodigoDepartamento = origen.CodigoDepartamento AND destino.CodigoMunicipio = origen.CodigoMunicipio
            WHEN MATCHED THEN UPDATE SET NombreDepartamento = @nombreDepartamento, NombreMunicipio = @nombreMunicipio, TipoTerritorio = N'municipio',
                -- COALESCE Y NO ASIGNACION DIRECTA: si un municipio llega sin centroide, se conserva
                -- el que ya hubiera en vez de borrarlo. Reimportar nunca debe dejar la base con menos
                -- datos de los que tenía.
                Latitud = COALESCE(@latitud, destino.Latitud), Longitud = COALESCE(@longitud, destino.Longitud)
            WHEN NOT MATCHED THEN INSERT (CodigoDepartamento, NombreDepartamento, CodigoMunicipio, NombreMunicipio, TipoTerritorio, Latitud, Longitud)
                VALUES (@departamento, @nombreDepartamento, @municipio, @nombreMunicipio, N'municipio', @latitud, @longitud);
            """;
        await using var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.Add("@departamento", SqlDbType.Char, 2).Value = municipio.CodigoDepartamento;
        comando.Parameters.Add("@municipio", SqlDbType.Char, 5).Value = municipio.CodigoCompleto;
        comando.Parameters.Add("@nombreDepartamento", SqlDbType.NVarChar, 120).Value = municipio.NombreDepartamento;
        comando.Parameters.Add("@nombreMunicipio", SqlDbType.NVarChar, 160).Value = municipio.Nombre;
        // PRECISION Y ESCALA EXPLICITAS. Un parámetro decimal sin declararlas hereda escala 0 en
        // SqlClient: la latitud llegaría redondeada al grado entero, que en Colombia son unos 110 km.
        AgregarCoordenada(comando, "@latitud", municipio.Latitud);
        AgregarCoordenada(comando, "@longitud", municipio.Longitud);
        await comando.ExecuteNonQueryAsync(cancelacion);
    }

    private static void AgregarCoordenada(SqlCommand comando, string nombre, decimal? valor)
    {
        var parametro = comando.Parameters.Add(nombre, SqlDbType.Decimal);
        parametro.Precision = 9;
        parametro.Scale = 6;
        parametro.Value = (object?)valor ?? DBNull.Value;
    }

    private static async Task RegistrarFuenteAsync(SqlConnection conexion, SqlTransaction transaccion, int departamentos, int municipios, CancellationToken cancelacion)
    {
        const string sql = """
            MERGE dbo.CatalogosReferenciaFuente AS destino
            USING (SELECT N'DIVIPOLA' AS CodigoCatalogo) AS origen ON destino.CodigoCatalogo = origen.CodigoCatalogo
            WHEN MATCHED THEN UPDATE SET VersionFuente = @version, UrlOrigen = @url, FechaIncorporacion = SYSUTCDATETIME(), CantidadDepartamentos = @departamentos, CantidadMunicipios = @municipios
            WHEN NOT MATCHED THEN INSERT (CodigoCatalogo, VersionFuente, UrlOrigen, FechaIncorporacion, CantidadDepartamentos, CantidadMunicipios)
                VALUES (N'DIVIPOLA', @version, @url, SYSUTCDATETIME(), @departamentos, @municipios);
            """;
        await using var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.AddWithValue("@version", Version);
        comando.Parameters.AddWithValue("@url", Servicio);
        comando.Parameters.AddWithValue("@departamentos", departamentos);
        comando.Parameters.AddWithValue("@municipios", municipios);
        await comando.ExecuteNonQueryAsync(cancelacion);
    }

    private sealed record Departamento(string Codigo, string Nombre);
    private sealed record Municipio(string CodigoDepartamento, string CodigoMunicipal, string Nombre)
    {
        public string CodigoCompleto => CodigoDepartamento + CodigoMunicipal;
        public string NombreDepartamento { get; init; } = string.Empty;
        public decimal? Latitud { get; init; }
        public decimal? Longitud { get; init; }
    }
}
