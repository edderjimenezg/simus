using PNMC.Domain.Entities;

namespace PNMC.Api.Security;

/// <summary>
/// A qué módulo de la consola pertenece cada aviso del buzón institucional.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL BUZON TIENE QUE DECIR LO MISMO QUE LA BARRA.</b> Lo pidió la dirección de producto el 15 de
/// septiembre de 2026, al fijar los permisos por cuenta: «es importante que el apartado de
/// notificaciones por supuesto también corresponda con los módulos que tienen activados en cada
/// caso». Una consola que oculta «Catálogo Editorial» y sigue avisando de lo que pasa dentro
/// enseña trabajo al que no se puede entrar: el aviso lleva a una puerta cerrada con 403.
/// </para>
/// <para>
/// <b>SE MAPEA A DONDE SE ACTUA, NO A DONDE NACE EL DATO.</b> Un festival que llega a revisión
/// nace en el Ecosistema, pero quien lo atiende lo hace en «Solicitudes y revisiones», y ahí es
/// donde lo abre el propio buzón. Mapearlo a «ecosistema» escondería trabajo vivo a una cuenta de
/// revisión que sí puede resolverlo. Por eso la columna de la derecha es el módulo que hay que
/// tener abierto para hacer algo con el aviso.
/// </para>
/// <para>
/// <b>LO DESCONOCIDO SE MUESTRA.</b> Un aviso sin módulo, o con uno que esta tabla no conoce, se
/// entrega igual. La alternativa —callar lo que no se sabe clasificar— convierte cada aviso nuevo
/// que alguien escriba mal en un mensaje que nadie recibe y que nadie echa de menos, que es
/// exactamente el fallo que no se detecta nunca.
/// </para>
/// </remarks>
public static class ModuloDeUnAviso
{
    private static readonly Dictionary<string, string> Correspondencias = new(StringComparer.OrdinalIgnoreCase)
    {
        // El circuito de revisión del Ecosistema se atiende entero desde Solicitudes y revisiones.
        [Modulos.Festivales] = ModulosDeLaConsola.Solicitudes,
        [Modulos.VersionesDeFestival] = ModulosDeLaConsola.Solicitudes,
        [Modulos.EdicionesDeFestival] = ModulosDeLaConsola.Solicitudes,
        [Modulos.PropuestasDeCambioDeFestival] = ModulosDeLaConsola.Solicitudes,
        ["reclamaciones-administracion"] = ModulosDeLaConsola.Solicitudes,

        // MERCADOS FALTABA, Y ESO ERA UN AGUJERO. Un aviso cuyo módulo no está en esta tabla se
        // entrega SIEMPRE —es la regla deliberada para no callar lo que no se sabe clasificar—, de
        // modo que los avisos de Mercados llegaban a cuentas que no tienen el módulo activado. La
        // auditoría encontró trece ya escritos en la base. Es
        // exactamente lo contrario de lo que se pidió el 15 de septiembre: «es importante que el
        // apartado de notificaciones por supuesto también corresponda con los módulos que tienen
        // activados en cada caso».
        [Modulos.Mercados] = "mercados",
        [Modulos.EdicionesDeMercado] = "mercados",

        // Estos sí se trabajan en su propio módulo.
        [Modulos.Organizaciones] = "organizaciones",
        [Modulos.CatalogoEditorial] = "catalogo-editorial",
        [Modulos.Agenda] = "agenda",
        [Modulos.Noticias] = "noticias",
        ["banco-de-archivos"] = "banco-de-archivos",
        ["boletin"] = "boletin",
        ["usuarios"] = "usuarios",
    };

    /// <summary>
    /// El módulo de consola que hay que tener abierto para atender ese aviso, o <c>null</c> si el
    /// aviso no pertenece a ninguno y por tanto se entrega siempre.
    /// </summary>
    public static string? ConsolaDe(string? moduloDelAviso) =>
        !string.IsNullOrWhiteSpace(moduloDelAviso)
        && Correspondencias.TryGetValue(moduloDelAviso.Trim(), out var modulo)
            ? modulo
            : null;

    /// <summary>
    /// Los identificadores de aviso que esa cuenta NO debe recibir, dados los módulos que tiene.
    /// </summary>
    /// <remarks>
    /// Se devuelve la lista de lo vetado, y no la de lo permitido, porque es la que sabe filtrar la
    /// consulta sin dejar fuera lo desconocido: <c>ModuloId is null or not in (vetados)</c>.
    /// </remarks>
    public static List<string> VetadosPara(IReadOnlyCollection<string> modulosDeLaCuenta)
    {
        ArgumentNullException.ThrowIfNull(modulosDeLaCuenta);
        var tiene = new HashSet<string>(modulosDeLaCuenta, StringComparer.OrdinalIgnoreCase);
        return Correspondencias
            .Where(par => !tiene.Contains(par.Value))
            .Select(par => par.Key)
            .ToList();
    }
}
