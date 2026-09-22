namespace PNMC.Api.Endpoints;

/// <summary>
/// Para qué puede este sistema pedir autorización, y cuál de esas autorizaciones se puede retirar
/// sin más.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNA FINALIDAD POR CADA COSA DISTINTA QUE SE HACE CON EL DATO.</b> La Ley 1581 art. 9 y el
/// decreto 1377 de 2013 art. 5 piden autorización <i>para fines determinados</i>. Una casilla única
/// en el alta —que es lo que había— no distingue entre tratar los datos para administrar la cuenta
/// y usar el correo para mandar boletines: son dos cosas, y la persona tiene que poder decir que sí
/// a una y que no a la otra.
/// </para>
/// <para>
/// <b>SON TRES Y NO CINCO.</b> El desarrollo de septiembre declaraba además <c>participacion</c> y
/// <c>directorio</c>. Ninguna de las dos existe en este sistema: no hay pantalla de participación
/// ni directorio público de organizaciones. Pedir autorización para una finalidad que no se ejerce
/// es pedir permiso para nada, y deja al titular con una lista de cosas autorizadas que no se
/// corresponde con lo que de verdad ocurre con sus datos.
/// </para>
/// </remarks>
public static class FinalidadesDeDatos
{
    /// <summary>Administrar la cuenta y la organización de quien se registra.</summary>
    public const string Tratamiento = "tratamiento";

    /// <summary>Las condiciones de uso del sistema. No es una autorización de datos; ver abajo.</summary>
    public const string Terminos = "terminos";

    /// <summary>Usar el correo para enviar el boletín del Plan, y nada más.</summary>
    public const string Boletin = "boletin";

    /// <summary>Las tres, en el orden en que se presentan.</summary>
    public static readonly string[] Todas = [Tratamiento, Terminos, Boletin];

    /// <summary>
    /// Las que el alta de una organización exige. El boletín no está: es voluntario y por eso es
    /// una finalidad aparte y no una línea más del mismo texto.
    /// </summary>
    public static readonly string[] ExigidasEnElAlta = [Tratamiento, Terminos];

    /// <summary>
    /// Si esta finalidad se puede retirar desde la pantalla de la cuenta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL DERECHO A REVOCAR NO ES ABSOLUTO, Y DECIR LO CONTRARIO SERÍA PEOR QUE NO OFRECERLO.</b>
    /// El art. 9 del decreto 1377 de 2013 lo dice con todas las letras: la revocatoria no procede
    /// cuando el titular tiene un deber legal o contractual de permanecer en la base de datos.
    /// Mientras una persona sea la responsable declarada de una organización con procesos inscritos
    /// en el Plan, sus datos de identificación son lo que sostiene ese vínculo: retirarlos dejaría
    /// procesos publicados sin nadie que responda por ellos.
    /// </para>
    /// <para>
    /// El boletín no tiene nada de eso detrás. Es una finalidad voluntaria, separable y sin
    /// consecuencias para nada más, y por tanto se retira cuando la persona quiera y en el acto.
    /// </para>
    /// <para>
    /// <b>LO QUE NO SE HACE ES PONER EL BOTÓN Y QUE NO HAGA NADA.</b> Para
    /// <c>tratamiento</c> y <c>terminos</c> la pantalla no ofrece revocar: explica que retirarlas
    /// equivale a cerrar la cuenta y por dónde se pide. Un botón que se pulsa y contesta que no se
    /// puede es la clase de interfaz que promete lo que no hay.
    /// </para>
    /// </remarks>
    public static bool SePuedeRevocarSolo(string finalidad) =>
        string.Equals(finalidad, Boletin, StringComparison.Ordinal);

    /// <summary>Por qué no se puede retirar sola, para decírselo a quien lo pregunte.</summary>
    public static string MotivoDeNoRevocable(string finalidad) => finalidad switch
    {
        Tratamiento =>
            "Esta autorización sostiene tu cuenta y tu vínculo como persona responsable de la "
            + "organización. Retirarla equivale a cerrar la cuenta, y no puede hacerse mientras la "
            + "organización tenga procesos inscritos en el Plan. Escríbenos para solicitarlo.",
        Terminos =>
            "Los términos de uso no son una autorización de datos: son las condiciones bajo las "
            + "que usas el sistema. Dejar de aceptarlos equivale a cerrar la cuenta. Escríbenos "
            + "para solicitarlo.",
        _ => "Esta autorización no puede retirarse por separado.",
    };

    /// <summary>Si el nombre corresponde a una de las tres finalidades declaradas.</summary>
    public static bool EsConocida(string? finalidad) =>
        finalidad is not null && Array.IndexOf(Todas, finalidad) >= 0;
}
