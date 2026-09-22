using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PNMC.Infrastructure;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El motor de pruebas no viaja en el despliegue.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddPnmcInfrastructure</c> registraba <c>UseSqlite</c> cuando el entorno se
/// llamaba «Test». Tres líneas que obligaban a que
/// <c>Microsoft.EntityFrameworkCore.Sqlite</c> fuera dependencia de producción:
/// un motor de base de datos completo, con binario nativo propio y su propio
/// historial de vulnerabilidades —la última, corrupción de memoria con 9,8 de
/// gravedad—, publicado en cada despliegue para no ejecutarse jamás.
/// </para>
/// <para>
/// Esta prueba existe porque la corrección es invisible: quitar un paquete no
/// cambia el comportamiento de nada, así que nada avisaría si alguien lo
/// devolviera «para que las pruebas pasen». Aquí eso falla.
/// </para>
/// </remarks>
public sealed class ProduccionSinSqliteTests
{
    private static AssemblyName[] Referencias(Assembly ensamblado) =>
        ensamblado.GetReferencedAssemblies();

    [Fact]
    public void La_Capa_De_Infraestructura_No_Referencia_Sqlite()
    {
        // Mira el ensamblado COMPILADO y no el csproj: el csproj se lee como
        // texto y depende de rutas, y la suite corre con la salida redirigida a
        // un temporal. Esto pregunta lo que de verdad importa —¿hay código de
        // producción que use SQLite?— y no dónde está escrito.
        var referencias = Referencias(typeof(PnmcDbContext).Assembly)
            .Select(nombre => nombre.Name ?? string.Empty)
            .Where(nombre => nombre.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(referencias);
    }

    [Fact]
    public void El_Ensamblado_Del_Api_Tampoco_Lo_Referencia()
    {
        var referencias = Referencias(typeof(Program).Assembly)
            .Select(nombre => nombre.Name ?? string.Empty)
            .Where(nombre => nombre.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(referencias);
    }

    [Fact]
    public void En_Pruebas_No_Se_Registra_Ningun_Proveedor()
    {
        // La otra mitad del cambio: en «Test» la infraestructura no registra
        // proveedor, deja ese hueco para que lo llene el arnés, y —esto es lo
        // que hay que fijar— NO exige la cadena de SQL Server que en pruebas no
        // existe. Si alguien restaurara ese requisito, la suite entera dejaría
        // de arrancar.
        var servicios = new ServiceCollection();
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var excepcion = Record.Exception(() =>
            servicios.AddPnmcInfrastructure(configuracion, new EntornoDePrueba("Test")));

        Assert.Null(excepcion);
        Assert.DoesNotContain(servicios, servicio => servicio.ServiceType == typeof(DbContextOptions<PnmcDbContext>));
    }

    [Fact]
    public void Fuera_De_Pruebas_Sigue_Exigiendo_La_Cadena_De_SQL_Server()
    {
        // El contrapeso del anterior. Sin esta prueba, «no registrar nada en
        // Test» podría degenerar en «no registrar nada nunca», y el servicio
        // arrancaría en producción sin base hasta la primera petición.
        var servicios = new ServiceCollection();
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.Throws<InvalidOperationException>(() =>
            servicios.AddPnmcInfrastructure(configuracion, new EntornoDePrueba("Production")));
    }

    private sealed class EntornoDePrueba : IHostEnvironment
    {
        public EntornoDePrueba(string nombre) => EnvironmentName = nombre;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "PNMC.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
