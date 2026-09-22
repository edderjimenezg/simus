using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Security;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-064 — Toda ruta del circuito institucional lleva puesta la política de funcionario.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE ESTA PRUEBA EXISTE, y por qué no la sustituye ninguna de las que ya había. Una
/// auditoría hizo el experimento: cambiar
/// <c>RevisionInstitucionalFestivalesEndpoints.cs</c> de
/// <c>Permisos.PoliticaFuncionario</c> a <c>InstitutionalPolicy</c> —es decir, volver al agujero
/// que se acababa de cerrar— dejaba <b>la suite entera en verde</b>. Reproducido: 326 pasan, 0
/// fallan.
/// </para>
/// <para>
/// Las dos pruebas que parecían cubrirlo no lo hacían, cada una por su motivo:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>GuardasDeRolTests.Una_persona_del_ecosistema_no_llega_al_circuito_institucional</c>
///     usa un cliente <b>sin sesión</b> —la persona externa ni siquiera obtiene cookie—, así que
///     sus 401 los produce el «no estás autenticado», que las dos políticas comparten. Sigue
///     siendo una prueba correcta de lo que dice medir; simplemente no mide esto.
///   </description></item>
///   <item><description>
///     <c>PuertaInstitucionalTests.La_politica_de_funcionario_exige_exactamente_los_roles_internos</c>
///     compara los roles de la política <b>ya registrada</b> con la lista de la que salieron. Es
///     una tautología sobre el objeto del contenedor: comprueba que la política está bien
///     construida y no comprueba que ninguna ruta la use.
///   </description></item>
/// </list>
/// <para>
/// LA FORMA CORRECTA es un barrido sobre las rutas realmente montadas, no una lista escrita a
/// mano: una ruta nueva bajo <c>/api/v1/institucional/</c> entra sola en el barrido, que es
/// justamente el descuido que se está cerrando. Es el mismo mecanismo de
/// <see cref="AutorizacionPorDefectoTests"/>, mirando la <b>política</b> en vez de contar rutas.
/// </para>
/// <para>
/// POR QUE IMPORTA AUNQUE LOS ALIADOS YA NO EXISTAN. Retirado ese rol, ninguna cuenta no interna
/// obtiene hoy cookie de consola, así que la política parece redundante. No lo es: la lista de
/// roles asignables desde la consola admite <c>externo</c>, y un principal institucional puede
/// acabar con un rol no interno si alguien degrada una cuenta con la sesión viva. La política es
/// lo que decide quién decide, y este barrido es lo que impide que alguien la quite «porque ya
/// no hace falta».
/// </para>
/// </remarks>
public sealed class CircuitoInstitucionalCerradoTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>Prefijo de las rutas que solo un funcionario puede tocar.</summary>
    private const string PrefijoInstitucional = "/api/v1/institucional/";

    /// <summary>
    /// Cuántas rutas debe encontrar el barrido como mínimo.
    /// </summary>
    /// <remarks>
    /// Hoy son 11, repartidas en los tres ficheros del circuito. El suelo no es decorativo: un
    /// barrido que encuentra cero rutas pasa siempre y da una falsa sensación de red. Si esta
    /// cifra baja, alguien retiró rutas del circuito y hay que venir aquí a decirlo.
    /// </remarks>
    private const int RutasDelCircuito = 8;

    private readonly TestWebApplicationFactory _factory;

    public CircuitoInstitucionalCerradoTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Toda_ruta_institucional_exige_la_politica_de_funcionario()
    {
        var rutas = LeerRutasInstitucionales();

        Assert.True(
            rutas.Count >= RutasDelCircuito,
            $"El barrido solo encontró {rutas.Count} rutas bajo {PrefijoInstitucional} y debería "
            + $"encontrar al menos {RutasDelCircuito}. O el circuito perdió rutas, o la lectura de "
            + "EndpointDataSource dejó de funcionar: en cualquiera de los dos casos esta prueba ya "
            + "no vigila nada y hay que arreglarla antes de fiarse de ella.");

        var sinPolitica = rutas
            .Where(ruta => !ExigePoliticaDeFuncionario(ruta))
            .Select(Nombrar)
            .ToList();

        Assert.True(sinPolitica.Count == 0,
            "Estas rutas del circuito institucional no exigen Permisos.PoliticaFuncionario:\n  "
            + string.Join("\n  ", sinPolitica)
            + "\n\nLos tres ficheros del circuito NO comprueban rol en ningún manejador: la política "
            + "aplicada al grupo es su única guarda. Una ruta institucional cerrada solo con "
            + "InstitutionalPolicy significa «hay cookie de consola» y nada más — que es exactamente "
            + "el agujero por el que un administrador de entidad aliada podía publicar o rechazar "
            + "cualquier Festival del país.");
    }

    /// <summary>
    /// Y la política sigue exigiendo rol, no solo sesión. Control del aserto anterior.
    /// </summary>
    /// <remarks>
    /// Sin esto, alguien podría dejar el nombre <c>funcionario-institucional</c> en su sitio y
    /// vaciar la política de contenido: el barrido de arriba seguiría verde sobre una política
    /// que no exige nada. Los dos asertos juntos son los que cierran: el nombre está puesto Y el
    /// nombre significa algo.
    /// </remarks>
    [Fact]
    public async Task La_politica_que_las_rutas_nombran_sigue_exigiendo_rol_interno()
    {
        using var alcance = _factory.Services.CreateScope();
        var proveedor = alcance.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var politica = await proveedor.GetPolicyAsync(Permisos.PoliticaFuncionario);
        Assert.NotNull(politica);
        Assert.Contains(politica!.Requirements, requisito => requisito is RolesAuthorizationRequirement);
    }

    // ---------- Andamio ----------------------------------------------------------

    private List<Endpoint> LeerRutasInstitucionales()
    {
        // Crear el cliente arranca el host; sin eso las fuentes de rutas están vacías.
        using var arranque = _factory.CreateClient();

        return _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(fuente => fuente.Endpoints)
            .Distinct()
            .Where(ruta => (ruta as RouteEndpoint)?.RoutePattern.RawText?
                .StartsWith(PrefijoInstitucional, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
    }

    private static bool ExigePoliticaDeFuncionario(Endpoint ruta)
        => ruta.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(dato => string.Equals(dato.Policy, Permisos.PoliticaFuncionario, StringComparison.Ordinal));

    private static string Nombrar(Endpoint ruta)
        => (ruta as RouteEndpoint)?.RoutePattern.RawText ?? ruta.DisplayName ?? "(sin nombre)";
}
