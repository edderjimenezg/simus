using Microsoft.Data.SqlClient;
using Xunit;

namespace Simus.Api.Tests;

[CollectionDefinition(Nombre)]
public sealed class ColeccionBaseDatos : ICollectionFixture<BaseDatosPruebasFixture>
{
    public const string Nombre = "BaseDatosPruebas";
}

public sealed class BaseDatosPruebasFixture : IAsyncLifetime
{
    public const string CodigoDepartamentoPrueba = "ZZ";
    public const string CodigoMunicipioPrueba = "ZZZ";

    public bool BaseDatosDisponible { get; private set; }

    public async Task InitializeAsync()
    {
        var cadenaConexion = Environment.GetEnvironmentVariable("ConnectionStrings__Simus");
        if (string.IsNullOrWhiteSpace(cadenaConexion)) return;

        try
        {
            await using var conexion = new SqlConnection(cadenaConexion);
            await conexion.OpenAsync();
            await LimpiarDatosOperativosAsync(conexion);
            await SembrarTerritorioPruebaAsync(conexion);
            BaseDatosDisponible = true;
        }
        catch (SqlException)
        {
            BaseDatosDisponible = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task LimpiarDatosOperativosAsync(SqlConnection conexion)
    {
        const string limpiar = """
            DELETE FROM identidad.EventosAcceso;
            DELETE FROM identidad.Sesiones;
            DELETE FROM legal.Aceptaciones;
            DELETE FROM festivales.Perfiles;
            DELETE FROM festivales.Festivales;
            DELETE FROM organizaciones.Administradores;
            DELETE FROM organizaciones.Organizaciones;
            DELETE FROM identidad.PersonasRoles;
            DELETE FROM identidad.Personas;
            DELETE FROM nucleo.EventosInicializacion;
            """;
        await using var comando = new SqlCommand(limpiar, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task SembrarTerritorioPruebaAsync(SqlConnection conexion)
    {
        const string sembrar = """
            IF NOT EXISTS (SELECT 1 FROM territorio.Departamentos WHERE Codigo=@departamento)
                INSERT INTO territorio.Departamentos (Codigo,Nombre) VALUES (@departamento,N'Departamento de prueba');
            IF NOT EXISTS (SELECT 1 FROM territorio.Municipios WHERE Codigo=@municipio AND CodigoDepartamento=@departamento)
                INSERT INTO territorio.Municipios (Codigo,CodigoDepartamento,Nombre) VALUES (@municipio,@departamento,N'Municipio de prueba');
            """;
        await using var comando = new SqlCommand(sembrar, conexion);
        comando.Parameters.AddWithValue("@departamento", CodigoDepartamentoPrueba);
        comando.Parameters.AddWithValue("@municipio", CodigoMunicipioPrueba);
        await comando.ExecuteNonQueryAsync();
    }
}
