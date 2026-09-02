using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class ImportadorDivipola
{
    private const string Version = "2025";
    private const string UrlServicio = "https://geoportal.dane.gov.co/mparcgis/rest/services/Divipola/Serv_DIVIPOLA_MGN_2025/FeatureServer";

    public static async Task ImportarAsync(SqlConnection conexion, CancellationToken cancelacion = default)
    {
        const string consultaExistente = "SELECT COUNT(*) FROM territorio.Departamentos;";
        await using (var comandoExistente = new SqlCommand(consultaExistente, conexion))
        {
            if (Convert.ToInt32(await comandoExistente.ExecuteScalarAsync(cancelacion)) > 0)
                throw new InvalidOperationException("El catálogo territorial ya contiene datos. No se reemplaza automáticamente.");
        }

        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        var departamentos = await ConsultarAsync(cliente, 319, "DPTO_CCDGO,DPTO_CNMBRE", cancelacion);
        var municipios = await ConsultarAsync(cliente, 317, "DPTO_CCDGO,MPIO_CCDGO,MPIO_CNMBRE", cancelacion);
        if (departamentos.Count == 0 || municipios.Count == 0)
            throw new InvalidOperationException("DANE no devolvió departamentos y municipios para DIVIPOLA MGN 2025.");

        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        await InsertarDepartamentosAsync(conexion, (SqlTransaction)transaccion, departamentos, cancelacion);
        await InsertarMunicipiosAsync(conexion, (SqlTransaction)transaccion, municipios, cancelacion);
        const string registrarFuente = "INSERT INTO territorio.FuentesCatalogo (Codigo,Version,UrlOrigen,FechaIncorporacion) VALUES (N'divipola_mgn',@version,@url,SYSUTCDATETIME());";
        await using var comandoFuente = new SqlCommand(registrarFuente, conexion, (SqlTransaction)transaccion);
        comandoFuente.Parameters.AddWithValue("@version", Version);
        comandoFuente.Parameters.AddWithValue("@url", UrlServicio);
        await comandoFuente.ExecuteNonQueryAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        Console.WriteLine($"DIVIPOLA MGN {Version} incorporada: {departamentos.Count} departamentos y {municipios.Count} municipios.");
    }

    public static async Task ImportarSiEstaVacioAsync(SqlConnection conexion, CancellationToken cancelacion = default)
    {
        const string consultaExistente = "SELECT COUNT(*) FROM territorio.Departamentos;";
        await using var comandoExistente = new SqlCommand(consultaExistente, conexion);
        if (Convert.ToInt32(await comandoExistente.ExecuteScalarAsync(cancelacion)) > 0)
        {
            Console.WriteLine("DIVIPOLA ya está incorporada; se conserva sin reemplazos.");
            return;
        }
        await ImportarAsync(conexion, cancelacion);
    }

    private static async Task<List<JsonElement>> ConsultarAsync(HttpClient cliente, int capa, string campos, CancellationToken cancelacion)
    {
        var url = $"{UrlServicio}/{capa}/query?where=1%3D1&outFields={Uri.EscapeDataString(campos)}&returnGeometry=false&f=json";
        using var respuesta = await cliente.GetAsync(url, cancelacion);
        respuesta.EnsureSuccessStatusCode();
        await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancelacion);
        using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancelacion);
        if (!documento.RootElement.TryGetProperty("features", out var elementos)) return [];
        return elementos.EnumerateArray().Select(elemento => elemento.GetProperty("attributes").Clone()).ToList();
    }

    private static async Task InsertarDepartamentosAsync(SqlConnection conexion, SqlTransaction transaccion, IEnumerable<JsonElement> departamentos, CancellationToken cancelacion)
    {
        await using var comando = new SqlCommand("INSERT INTO territorio.Departamentos (Codigo,Nombre) VALUES (@codigo,@nombre);", conexion, transaccion);
        var codigo = comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10);
        var nombre = comando.Parameters.Add("@nombre", System.Data.SqlDbType.NVarChar, 160);
        foreach (var departamento in departamentos)
        {
            codigo.Value = departamento.GetProperty("DPTO_CCDGO").GetString() ?? throw new InvalidOperationException("DIVIPOLA no informó código de departamento.");
            nombre.Value = departamento.GetProperty("DPTO_CNMBRE").GetString() ?? throw new InvalidOperationException("DIVIPOLA no informó nombre de departamento.");
            await comando.ExecuteNonQueryAsync(cancelacion);
        }
    }

    private static async Task InsertarMunicipiosAsync(SqlConnection conexion, SqlTransaction transaccion, IEnumerable<JsonElement> municipios, CancellationToken cancelacion)
    {
        await using var comando = new SqlCommand("INSERT INTO territorio.Municipios (Codigo,CodigoDepartamento,Nombre) VALUES (@codigo,@departamento,@nombre);", conexion, transaccion);
        var codigo = comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10);
        var departamento = comando.Parameters.Add("@departamento", System.Data.SqlDbType.NVarChar, 10);
        var nombre = comando.Parameters.Add("@nombre", System.Data.SqlDbType.NVarChar, 160);
        foreach (var municipio in municipios)
        {
            codigo.Value = municipio.GetProperty("MPIO_CCDGO").GetString() ?? throw new InvalidOperationException("DIVIPOLA no informó código de municipio.");
            departamento.Value = municipio.GetProperty("DPTO_CCDGO").GetString() ?? throw new InvalidOperationException("DIVIPOLA no informó departamento de municipio.");
            nombre.Value = municipio.GetProperty("MPIO_CNMBRE").GetString() ?? throw new InvalidOperationException("DIVIPOLA no informó nombre de municipio.");
            await comando.ExecuteNonQueryAsync(cancelacion);
        }
    }
}
