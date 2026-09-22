using PNMC.Api.Endpoints;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Nada de lo que el circuito de un mercado escribe en la bitácora puede violar su CHECK.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL DEFECTO QUE TRAJO ESTA PRUEBA, Y POR QUE NO LO VIO NINGUNA OTRA.</b> El primer registro de
/// un mercado desde el panel de una organización murió con un 500 contra
/// <c>CK_BitacoraAuditoria_Accion</c>: el circuito escribía «MercadoCreado» y ese CHECK solo admite
/// trece verbos técnicos. Las veinte pruebas del módulo pasaban porque la base de pruebas la crea EF
/// con <c>EnsureCreated</c>, <b>que levanta las tablas y no sus CHECK</b>; el fallo apareció al
/// recorrerlo en el navegador contra la base real. Es el mismo defecto que el circuito de Festival
/// ya había tenido y corregido con <see cref="AccionesAuditoria"/>.
/// </para>
/// <para>
/// <b>LO QUE FIJA, Y POR QUE ASI.</b> No una lista de eventos —esa se queda atrás en cuanto alguien
/// añade uno— sino la propiedad que de verdad importa: la traducción es TOTAL. Cualquier entrada,
/// conocida o no, sale convertida en uno de los trece verbos admitidos. Con el caso por omisión en
/// su sitio, un evento nuevo sin traducir queda como «actualizar» y con su nombre completo en
/// <c>ValoresNuevos</c>, en vez de tumbar la operación de quien lo hizo.
/// </para>
/// </remarks>
public sealed class VerbosDeAuditoriaDelMercadoTests
{
    /// <summary>Los eventos que el circuito escribe hoy, en los dos canales.</summary>
    public static TheoryData<string> EventosDelCircuito() =>
    [
        "MercadoCreado",
        "MercadoActualizado",
        "MercadoEnviadoARevision",
        "MercadoPublicado",
        "MercadoConAjustesSolicitados",
        "crear",
        "guardar",
        "decidir",
    ];

    [Theory]
    [MemberData(nameof(EventosDelCircuito))]
    public void Todo_evento_del_circuito_sale_como_un_verbo_admitido(string evento)
    {
        var verbo = AccionesAuditoria.DeMercado(evento);

        Assert.Contains(verbo, AccionesAuditoria.Admitidos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UnEventoQueNadieHaEscritoTodavia")]
    public void Un_evento_desconocido_no_tumba_la_operacion(string? evento)
    {
        // NI LANZA NI DEVUELVE EL NOMBRE CRUDO: lo segundo sería volver al 500 del primer día.
        var verbo = AccionesAuditoria.DeMercado(evento);

        Assert.Contains(verbo, AccionesAuditoria.Admitidos);
        Assert.Equal("actualizar", verbo);
    }
}
