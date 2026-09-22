namespace PNMC.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool SeedBootstrapUsers { get; set; }

    /// <summary>
    /// Si el arranque instala la configuración editorial predeterminada.
    /// El perfil limpio la desactiva: su función es validar una instalación sin
    /// registros iniciales, no convertir textos de muestra en contenido vigente.
    /// </summary>
    public bool SeedWebConfiguration { get; set; } = true;
    public int StartupTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Si el arranque de la base falla, ¿sigue el API sirviendo trafico en modo degradado?
    /// </summary>
    /// <remarks>
    /// Por omision NO. Hasta el valor por omision era <c>true</c>,
    /// y como <c>appsettings.Production.json</c> no lo sobreescribia, en Produccion un
    /// arranque sin base quedaba reducido a un <c>LogWarning</c> y el proceso seguia
    /// respondiendo 500 a todo — con las sondas de salud en verde. Los perfiles de
    /// desarrollo lo encienden explicitamente en su <c>appsettings</c>, que es donde
    /// tiene sentido: alli un hipo de Docker no debe tumbar la sesion de trabajo.
    /// </remarks>
    public bool ContinueOnStartupFailure { get; set; }
}
