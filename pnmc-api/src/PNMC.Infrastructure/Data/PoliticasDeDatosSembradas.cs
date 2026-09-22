namespace PNMC.Infrastructure.Data;

/// <summary>
/// Los tres textos de consentimiento vigentes, en un solo sitio.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTEN DOS COPIAS Y COMO SE EVITA QUE DISCREPEN.</b> Estos textos tienen que estar
/// en la base antes de que nadie pueda aceptarlos, y a la base se llega por dos caminos que no se
/// pueden fundir: las bases reales pasan por DbUp y reciben los textos de
/// <c>V20260912_07__politicas_y_autorizaciones_de_datos.sql</c>; las de prueba se levantan desde el
/// modelo de EF y los reciben de aqui. SQL no puede leer C# ni C# ejecutarse dentro de DbUp.
/// </para>
/// <para>
/// La duplicacion es inevitable; la divergencia no. <c>TextosDePoliticaCoincidenConLaMigracion</c>
/// lee el guion del disco y compara los tres textos caracter a caracter. Si alguien cambia uno de
/// los dos lados, esa prueba se pone roja en la misma edicion.
/// </para>
/// <para>
/// <b>CAMBIAR UN TEXTO ES PUBLICAR UNA VERSION, NO EDITAR ESTA.</b> Las autorizaciones ya
/// otorgadas guardan su propia copia y no leen de aqui, asi que editar esta cadena no altera lo que
/// dice la evidencia de nadie. Pero si alteraria lo que la pantalla muestra como version
/// «2026-09-12», que dejaria de ser esa version. Una redaccion nueva entra con fecha nueva, por
/// migracion, y esta pasa a <c>Vigente = 0</c>.
/// </para>
/// </remarks>
public static class PoliticasDeDatosSembradas
{
    /// <summary>La fecha en que empezaron a mostrarse las tres redacciones vigentes.</summary>
    public const string VersionVigente = "2026-09-12";

    public const string UrlTratamiento =
        "https://mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/"
        + "PL-GSI-002_PoliticaTratamientodeDatosPersonales_V0_LF_2024_03-12-2024.pdf";

    public const string UrlTerminos =
        "https://www.mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/"
        + "PL-GSI-001_PoliticaGeneraldeSeguridadyPrivacidaddelaInformacion.pdf";

    public const string TituloTratamiento = "Autorización de tratamiento de datos personales";
    public const string TituloTerminos = "Términos de uso del Sistema de Información de la Música";
    public const string TituloBoletin = "Autorización para recibir el boletín del PNMC";

    /// <summary>
    /// Las cuatro cosas que la Ley 1581 art. 12 obliga a informar antes de pedir autorizacion: que
    /// tratamiento y con que finalidad, si el suministro es facultativo, que derechos tiene el
    /// titular y quien es el responsable. En ese orden.
    /// </summary>
    public const string TextoTratamiento =
        """
        Autorizo al Ministerio de las Culturas, las Artes y los Saberes —responsable del tratamiento— a recolectar, almacenar, usar y actualizar los datos personales que entrego en este formulario: mi nombre, mi tipo y número de documento, mi teléfono y mi correo electrónico.

        La finalidad es administrar mi cuenta en el Sistema de Información de la Música, identificarme como persona responsable de la organización que registro, y comunicarme lo relativo a los procesos que esa organización inscriba en el Plan Nacional de Música para la Convivencia. Mis datos no se usarán para ninguna otra finalidad ni se entregarán a terceros sin una nueva autorización.

        Entregar estos datos es voluntario. No estoy obligado a responder preguntas sobre datos sensibles ni sobre menores de edad, y este formulario no las hace.

        Conozco que, conforme al artículo 8 de la Ley 1581 de 2012, puedo conocer, actualizar y rectificar mis datos, solicitar prueba de esta autorización, ser informado sobre el uso que se les ha dado, presentar quejas ante la Superintendencia de Industria y Comercio, y revocar esta autorización. Puedo ejercer estos derechos desde la sección «Mis autorizaciones» de mi cuenta o por los canales que indica la política institucional de tratamiento de datos personales del Ministerio.
        """;

    /// <summary>
    /// Los terminos NO son una autorizacion de datos: son la aceptacion de unas condiciones de uso.
    /// Se guardan en el mismo registro porque la pregunta que hay que poder contestar es la misma
    /// —que acepto esta persona, cuando y con que texto delante— y tenerlos en dos sitios fue
    /// justamente el problema anterior.
    /// </summary>
    public const string TextoTerminos =
        """
        Acepto usar el Sistema de Información de la Música del Plan Nacional de Música para la Convivencia para registrar y administrar procesos musicales reales, y respondo por la veracidad de la información que inscriba en él.

        Entiendo que la información que registre sobre una organización o un proceso musical es revisada por el equipo del Plan antes de publicarse, que puede devolverse para ajustes, y que su publicación no implica aval, financiación ni vínculo contractual con el Ministerio de las Culturas, las Artes y los Saberes.

        Entiendo que la información publicada queda visible para cualquier persona que consulte el portal, y que soy responsable de no incluir en ella datos personales de terceros sin su autorización.

        Este sistema se rige por la política general de seguridad y privacidad de la información del Ministerio, que puedo consultar en el enlace de esta página.
        """;

    /// <summary>
    /// El boletin pide una finalidad y solo una, y lo dice. Es la diferencia entre una autorizacion
    /// determinada —lo que pide la Ley 1581 art. 9— y una casilla que autoriza «comunicaciones».
    /// </summary>
    public const string TextoBoletin =
        """
        Autorizo al Ministerio de las Culturas, las Artes y los Saberes —responsable del tratamiento— a usar mi correo electrónico con la única finalidad de enviarme información del Plan Nacional de Música para la Convivencia: convocatorias, publicaciones y agenda.

        Mi correo no se usará para ninguna otra finalidad, no se entregará a terceros y no se cruzará con otra información mía.

        Suscribirme es voluntario. Conforme al artículo 8 de la Ley 1581 de 2012 puedo conocer, actualizar y rectificar mi dato, solicitar prueba de esta autorización y revocarla en cualquier momento, desde el enlace que acompaña a cada envío o escribiendo por los canales del Ministerio.
        """;
}
