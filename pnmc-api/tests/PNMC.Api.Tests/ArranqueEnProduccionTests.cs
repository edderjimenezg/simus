using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Lo que el perfil de Produccion tiene que NEGARSE a hacer al arrancar.
/// </summary>
/// <remarks>
/// <para>
/// Dos defectos, los dos de la familia «arranca y calla»:
/// </para>
/// <list type="number">
/// <item>
/// <c>appsettings.json</c> traia los origenes CORS de desarrollo y Produccion los
/// heredaba, porque las listas de configuracion se fusionan por indice y un archivo
/// de perfil no puede vaciar la del base. Un despliegue en Azure habria salido con
/// un origen local y <c>AllowCredentials</c>.
/// </item>
/// <item>
/// <c>ContinueOnStartupFailure</c> valia <c>true</c> por omision y Produccion no lo
/// sobreescribia: sin base, el proceso seguia sirviendo 500 con las sondas en verde.
/// </item>
/// </list>
/// <para>
/// Estas pruebas levantan el API con el perfil <c>Production</c> de verdad —sus
/// <c>appsettings</c> incluidos— y comprueban que cada ausencia tumba el arranque con
/// un mensaje que dice que falta, en vez de taparse con una politica vacia o un
/// modo degradado.
/// </para>
/// </remarks>
public sealed class ArranqueEnProduccionTests
{
    private sealed class FabricaDeProduccion(IReadOnlyDictionary<string, string?> configuracion) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            // SE DECLARA A PROPOSITO PARA QUE NO ESTORBE. Desde el 24 ago 2026 el arranque exige
            // saber si hay un proxy delante, y esa comprobacion corre ANTES que las de este
            // fichero. Sin esta linea, las tres pruebas de abajo seguirian en rojo —es decir,
            // seguirian «pasando»— pero por un motivo que no es el que dicen medir, que es la
            // forma mas facil de convertir una prueba en decoracion.
            builder.UseSetting(PNMC.Api.Security.SeguridadDeTransporte.ClaveDetrasDeProxy, "true");
            // UseSetting y no ConfigureAppConfiguration: con el alojamiento minimo, la
            // configuracion de la fabrica llega tarde para el registro de servicios de
            // Program.cs, mientras que los ajustes del host se leen desde el principio.
            foreach (var (clave, valor) in configuracion)
            {
                builder.UseSetting(clave, valor);
            }
        }
    }

    // Una cadena valida en forma pero que no puede conectar: el puerto 1 y un segundo de espera.
    // Va por AZURE_SQL_CONNECTION_STRING y no por ConnectionStrings:SqlServer porque
    // appsettings.Production.json declara esa ultima en blanco y pisaria el valor en memoria.
    private const string CadenaInalcanzable =
        "Server=127.0.0.1,1;Database=PNMC_NO_EXISTE;User Id=nadie;Password=nada;Connect Timeout=1;TrustServerCertificate=True;Encrypt=False";

    /// <summary>
    /// La configuracion que de verdad se despliega, compuesta como la compone el anfitrion.
    /// </summary>
    /// <remarks>
    /// Se leen los ficheros del proyecto y no los del directorio de salida: lo que interesa es lo
    /// que hay escrito en el repositorio, que es lo que viaja al servidor.
    /// </remarks>
    private static IConfigurationRoot ConfiguracionDeProduccion()
    {
        var carpeta = Path.Combine(RutasDelRepositorio.Raiz(), "pnmc-api", "src", "PNMC.Api");
        Assert.True(
            File.Exists(Path.Combine(carpeta, "appsettings.Production.json")),
            $"No se encontro appsettings.Production.json en {carpeta}. Sin el fichero, estas dos pruebas no miden nada.");

        return new ConfigurationBuilder()
            .SetBasePath(carpeta)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: false)
            .Build();
    }

    [Fact]
    public void Produccion_No_Siembra_Las_Cuentas_De_Arranque()
    {
        // SON CINCO CUENTAS CON LA CONTRASENA LITERAL «admin», y dos de ellas son webmaster y
        // gestor interno. Sembrarlas en un sitio publico es entregar la consola. Hoy el valor sale
        // de appsettings.json —que trae false— y solo appsettings.Local.json lo enciende; esta
        // prueba fija que el perfil de Produccion no lo herede encendido el dia que alguien mueva
        // el valor base.
        var configuracion = ConfiguracionDeProduccion();

        Assert.False(configuracion.GetValue<bool>("Database:SeedBootstrapUsers"));
    }

    [Fact]
    public void El_Arranque_No_Tiene_Un_Interruptor_Para_Aplicar_DDL()
    {
        // El esquema lo aplica DbUp desde la tubería de despliegue y deja registro en
        // dbo.SchemaVersions. La ausencia de la propiedad impide reactivar accidentalmente
        // el antiguo segundo camino mediante configuración.
        Assert.Null(typeof(DatabaseOptions).GetProperty("EnsureSupportTables"));
    }

    [Fact]
    public void Sin_Origenes_CORS_Produccion_No_Arranca_Y_Dice_Por_Que()
    {
        var configuracion = new Dictionary<string, string?>
        {
            ["AZURE_SQL_CONNECTION_STRING"] = CadenaInalcanzable,
        };
        using var fabrica = new FabricaDeProduccion(configuracion);

        var excepcion = Record.Exception(() => fabrica.CreateClient());

        Assert.NotNull(excepcion);
        Assert.Contains("Cors:AllowedOrigins", AplanarMensajes(excepcion!), StringComparison.Ordinal);
    }

    [Fact]
    public void Produccion_No_Hereda_Los_Origenes_De_Desarrollo()
    {
        // Si el appsettings.json base volviera a traer localhost:4300, este arranque
        // dejaria de fallar por CORS y la prueba de arriba se pondria en rojo; esta
        // fija ademas que el motivo del fallo NO es otro (la base), para que la de
        // arriba no pase en verde por la razon equivocada.
        var configuracion = new Dictionary<string, string?>
        {
            ["AZURE_SQL_CONNECTION_STRING"] = CadenaInalcanzable,
        };
        using var fabrica = new FabricaDeProduccion(configuracion);

        var excepcion = Record.Exception(() => fabrica.CreateClient());

        Assert.NotNull(excepcion);
        Assert.DoesNotContain("localhost:4300", AplanarMensajes(excepcion!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Con_Origenes_Pero_Sin_Base_Produccion_Tampoco_Arranca()
    {
        // La otra bandera: con ContinueOnStartupFailure en false, un arranque sin base
        // tiene que morir, no quedarse sirviendo 500 en «modo degradado».
        var configuracion = new Dictionary<string, string?>
        {
            ["AZURE_SQL_CONNECTION_STRING"] = CadenaInalcanzable,
            ["Cors:AllowedOrigins:0"] = "https://pnmc.ejemplo.gov.co",
        };
        using var fabrica = new FabricaDeProduccion(configuracion);

        var excepcion = Record.Exception(() => fabrica.CreateClient());

        Assert.NotNull(excepcion);
        Assert.DoesNotContain("Cors:AllowedOrigins", AplanarMensajes(excepcion!), StringComparison.Ordinal);
    }

    private static string AplanarMensajes(Exception excepcion)
    {
        var mensajes = new List<string>();
        for (Exception? actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            mensajes.Add(actual.Message);
        }
        return string.Join(" | ", mensajes);
    }
}
