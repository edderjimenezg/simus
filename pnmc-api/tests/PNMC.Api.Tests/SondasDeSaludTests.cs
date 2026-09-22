using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Las sondas de salud tienen que poder ponerse en rojo, y ponerse en rojo POR SEPARADO.
/// </summary>
/// <remarks>
/// <para>
/// Hasta <c>AddHealthChecks()</c> no registraba nada: las dos
/// sondas eran identicas, vacias, y respondian <c>Healthy</c> con la base caida. La unica
/// prueba que las miraba (<c>Health_Endpoints_ReturnOk</c>) afirmaba que ambas devolvian
/// 200 — y con cero comprobaciones registradas era <b>matematicamente incapaz de fallar</b>.
/// Para un orquestador eso significa enrutar trafico a una instancia sin base.
/// </para>
/// <para>
/// Esta clase levanta el API con una cadena de SQL Server que no puede conectar (puerto 1,
/// tiempo de espera de un segundo) y comprueba las dos mitades del contrato: que
/// <c>/health/ready</c> se declara caida, y que <c>/health/live</c> sigue viva, porque
/// liveness no debe depender de la base — si lo hiciera, un hipo de SQL Server haria que
/// el orquestador reiniciara el contenedor en bucle en vez de dejar de enrutarle trafico.
/// </para>
/// <para>
/// El arranque en este escenario falla y, con <c>ContinueOnStartupFailure</c> activo en
/// el perfil Test, el API sigue en modo degradado. Eso es lo que la sonda de readiness
/// tiene que delatar. El mismo escenario con el perfil de Produccion no arranca: alli la
/// bandera va en <c>false</c>.
/// </para>
/// </remarks>
public sealed class SondasDeSaludTests
{
    private sealed class FabricaSinBase : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PnmcDbContext>>();
                services.RemoveAll<PnmcDbContext>();
                services.AddDbContext<PnmcDbContext>(options => options.UseSqlServer(
                    "Server=127.0.0.1,1;Database=PNMC_NO_EXISTE;User Id=nadie;Password=nada;Connect Timeout=1;TrustServerCertificate=True;Encrypt=False"));
            });
        }
    }

    [Fact]
    public async Task Ready_Se_Declara_Caida_Cuando_La_Base_No_Responde()
    {
        await using var fabrica = new FabricaSinBase();
        using var cliente = fabrica.CreateClient();

        var ready = await cliente.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task Live_Sigue_Viva_Aunque_La_Base_No_Responda()
    {
        await using var fabrica = new FabricaSinBase();
        using var cliente = fabrica.CreateClient();

        var live = await cliente.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Ready_Responde_Sana_Con_La_Base_Disponible()
    {
        // La otra mitad: sin esta, un check mal escrito que devolviera Unhealthy siempre
        // dejaria la primera prueba en verde y el despliegue sin trafico.
        await using var fabrica = new TestWebApplicationFactory();
        using var cliente = fabrica.CreateClient();

        var ready = await cliente.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}
