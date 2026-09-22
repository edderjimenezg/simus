using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PNMC.Api.Observability;

/// <summary>
/// PNMC-039. La puerta que impide que un registro se convierta en una base de datos personales.
/// <para>
/// EL RIESGO CONCRETO, NO LA HIPOTESIS: el catalogo publico sirve datos de contacto en siete
/// lecturas anonimas (ver la nota de FestivalesPublicosEndpoints) y la nomina del equipo en una
/// octava. En cuanto se empiece a registrar lo que pasa por esas rutas, cada linea que lleve un
/// correo crea una copia de ese dato en un sitio nuevo — el fichero del contenedor, la consola
/// del servidor, el agregador de registros del dia de manana — donde no hay control de acceso,
/// ni retencion, ni nadie respondiendo por el. Un registro es una base de datos que nadie declara.
/// </para>
/// <para>
/// POR QUE ESTO ES UNA PUERTA Y NO UN CONSEJO. Lo facil habria sido escribir en la guia "no
/// registre correos" y confiar. Eso no es una puerta: la primera persona que escriba
/// <c>LogInformation("login de {Correo}", request.Email)</c> a las once de la noche la abre sin
/// enterarse, y nada se pone rojo. Aqui el saneado corre en el ultimo punto por el que pasa toda
/// linea, ya serializada — <see cref="RegistradorEstructurado"/> —, de modo que da igual por
/// donde entro el dato: plantilla, argumento, ambito o texto de excepcion. La demostracion de
/// que la puerta rechaza de verdad esta en <c>Pnmc039RegistroSinDatosPersonalesTests</c>, en la
/// prueba que registra un correo en crudo bajo una clave deliberadamente inocente y comprueba
/// que no sale.
/// </para>
/// <para>
/// DOS CAPAS, PORQUE UNA SOLA NO LLEGA:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Por nombre de campo</b> (<see cref="Enmascarar(string?, object?)"/>): cualquier propiedad
/// de registro estructurado cuyo nombre suene a dato personal se sustituye por su seudonimo,
/// aunque su valor no tenga forma reconocible. Es la unica capa capaz de atrapar una cedula
/// suelta o un telefono de diez digitos sin separadores, que como texto son indistinguibles de
/// un identificador cualquiera.
/// </description></item>
/// <item><description>
/// <b>Por forma del dato</b> (<see cref="Sanear(string)"/>): sobre la linea ya serializada, para
/// lo que se colo por una clave que nadie clasifico — o dentro del mensaje de una excepcion, que
/// no tiene claves. Aqui solo entran patrones INEQUIVOCOS; ver la nota de falsos positivos abajo.
/// </description></item>
/// </list>
/// <para>
/// FALSOS POSITIVOS: LA RAZON DE QUE EL PATRON DE TELEFONO EXIJA SEPARADOR. La tentacion es
/// enmascarar todo numero de diez digitos que empiece por 3. No se hace, y el motivo importa: el
/// identificador de correlacion es un GUID en hexadecimal de 32 caracteres, y aproximadamente uno
/// de cada quinientos empieza o acaba por diez digitos, el primero de ellos un 3. Ese patron
/// habria destrozado el identificador de correlacion de una peticion de cada quinientas — justo
/// el campo que existe para poder seguir una peticion — y de forma intermitente, que es la peor
/// manera de romper algo. Por eso el patron exige un separador (espacio, punto o guion) o el
/// prefijo +57, cosas que un hexadecimal no puede contener. El telefono escrito de corrido queda
/// cubierto por la capa de nombre de campo, y esa limitacion esta escrita en la guia en vez de
/// tapada. La prueba <c>El_saneado_no_toca_un_identificador_de_correlacion</c> fija la frontera.
/// </para>
/// <para>
/// SEUDONIMO Y NO BORRADO. Un correo no se sustituye por "***" sino por
/// <c>[correo:#a1b2c3d4]</c>. Borrar impide lo unico para lo que sirve un registro: saber que las
/// catorce peticiones fallidas de las 03:00 venian de la misma cuenta. La huella es un HMAC-SHA256
/// con una sal aleatoria por proceso: dentro de una misma ejecucion dos apariciones del mismo
/// correo coinciden y se pueden correlacionar; entre reinicios no, y desde el registro no se puede
/// volver al correo ni probando la lista de correos del pais, porque la sal no esta escrita en
/// ningun sitio. Ese olvido al reiniciar es deliberado y es el lado conservador del intercambio.
/// </para>
/// <para>
/// LO QUE ESTO NO ES. No es control de acceso al registro, ni politica de retencion, ni cifrado
/// del fichero. Reduce el dano de que un registro se filtre; no autoriza a guardarlo para siempre.
/// </para>
/// </summary>
public static class RedaccionDatosPersonales
{
    /// <summary>Clase de dato que decide como se enmascara un valor.</summary>
    public enum ClaseDato
    {
        /// <summary>Nada que ocultar: se registra tal cual.</summary>
        Publico,
        Correo,
        Documento,
        Telefono,
        Persona,
        Direccion,

        /// <summary>
        /// Credencial. No se seudonimiza: se borra. Una huella estable de una contrasena sigue
        /// siendo un oraculo con el que comprobar conjeturas.
        /// </summary>
        Secreto
    }

    public const string MarcaCorreo = "[correo:#";
    public const string MarcaDocumento = "[documento:#";
    public const string MarcaTelefono = "[telefono:#";
    public const string MarcaPersona = "[persona:#";
    public const string MarcaDireccion = "[direccion:#";
    public const string MarcaSecreto = "[secreto]";

    /// <summary>
    /// Sal aleatoria por proceso. Ver la nota "SEUDONIMO Y NO BORRADO": es lo que impide que la
    /// huella sea reversible por diccionario, al precio de no correlacionar entre reinicios.
    /// </summary>
    private static readonly byte[] SalDelProceso = RandomNumberGenerator.GetBytes(32);

    private static readonly Regex PatronCorreo = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Exige separador o prefijo +57 a proposito. Ver "FALSOS POSITIVOS" en la nota de la clase.
    private static readonly Regex PatronTelefono = new(
        @"(?<![0-9A-Za-z])(?:\+57[ .\-]?[0-9]{10}|3[0-9]{2}[ .\-][0-9]{3}[ .\-]?[0-9]{4})(?![0-9A-Za-z])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Exige la palabra delante. Un numero suelto de cinco a quince digitos es, casi siempre, un
    // identificador de fila; enmascararlo dejaria ciego al que depura sin proteger a nadie.
    //
    // El (?<!\[) del principio evita morderse la cola: la marca que produce esta misma clase es
    // "[documento:#a1b2c3d4]", y una huella cuyos primeros caracteres salgan todos numericos
    // —una de cada diez— volveria a coincidir con el patron y anidaria una marca dentro de otra.
    // Enmascarar dos veces no filtra nada, pero produce una linea ilegible una vez de cada diez,
    // y una herramienta que a veces escupe basura es una herramienta que se acaba apagando.
    private static readonly Regex PatronDocumentoConPalabra = new(
        @"(?<!\[)(cedula|cédula|documento|identificacion|identificación|nit)([^0-9A-Za-z]{0,4})([0-9]{5,15})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] PalabrasSecreto =
    [
        "password", "contrasena", "pwd", "secret", "token", "authorization", "apikey", "credencial"
    ];

    private static readonly string[] PalabrasCorreo = ["correo", "mail"];

    private static readonly string[] PalabrasDocumento =
    [
        "documento", "cedula", "identificacion", "pasaporte"
    ];

    private static readonly string[] PalabrasTelefono =
    [
        "telefono", "phone", "celular", "movil", "whatsapp"
    ];

    private static readonly string[] PalabrasDireccion = ["direccion", "address", "domicilio"];

    /// <summary>
    /// Coincidencia EXACTA, no por subcadena. "nit" aparece dentro de "unit", "monitor" e
    /// "initial"; "nombre" aparece dentro de "NombreFestival", que es publico y cuyo
    /// enmascaramiento dejaria ciego al que depura sin proteger a ninguna persona.
    /// </summary>
    private static readonly HashSet<string> ClavesExactasDocumento =
        new(StringComparer.Ordinal) { "nit", "nits", "rut", "nui" };

    private static readonly HashSet<string> ClavesExactasPersona = new(StringComparer.Ordinal)
    {
        "fullname",
        "nombrecompleto",
        "nombreyapellidos",
        "nombreyapellido",
        "apellido",
        "apellidos",
        "nombresolicitante",
        "nombrerepresentante",
        "nombrecontacto",
        "personacontacto",
        "representantelegal",
        "autor",
        "updatedby"
    };

    /// <summary>Seudonimo de un correo. Atajo legible para los manejadores.</summary>
    public static string? Correo(string? valor) => Enmascarar("correo", valor);

    /// <summary>Seudonimo de un documento de identidad.</summary>
    public static string? Documento(string? valor) => Enmascarar("documento", valor);

    /// <summary>Seudonimo de un telefono.</summary>
    public static string? Telefono(string? valor) => Enmascarar("telefono", valor);

    /// <summary>Seudonimo del nombre de una persona.</summary>
    public static string? Persona(string? valor) => Enmascarar("nombrecompleto", valor);

    /// <summary>
    /// Capa 1: enmascara por NOMBRE de campo. Devuelve el texto que debe salir al registro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// VALORES NUMERICOS. Un entero bajo una clave que suena a dato personal casi siempre es una
    /// cuenta, no el dato: <c>{FichasConCorreo} = 7</c>. Convertir ese 7 en <c>[correo:#...]</c>
    /// destruiria la metrica sin proteger nada. Por eso los numeros solo se enmascaran cuando la
    /// clase es Documento, Telefono o Secreto, donde el numero SI puede ser el dato. La regla
    /// practica, escrita tambien en la guia: no bautice un contador con una palabra sensible.
    /// </para>
    /// </remarks>
    public static string? Enmascarar(string? clave, object? valor)
    {
        if (valor is null)
        {
            return null;
        }

        var clase = Clasificar(clave);
        if (clase == ClaseDato.Publico)
        {
            return valor.ToString();
        }

        var esNumerico = valor is bool or sbyte or byte or short or ushort or int or uint
            or long or ulong or float or double or decimal;
        if (esNumerico && clase is not (ClaseDato.Documento or ClaseDato.Telefono or ClaseDato.Secreto))
        {
            return Convert.ToString(valor, CultureInfo.InvariantCulture);
        }

        var texto = valor.ToString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        // Sin esto, un valor ya seudonimizado por el manejador se volveria a seudonimizar aqui y
        // dos lineas sobre la misma persona dejarian de coincidir, que es justo lo que el
        // seudonimo existe para permitir.
        if (YaEnmascarado(texto))
        {
            return texto;
        }

        return clase switch
        {
            ClaseDato.Secreto => MarcaSecreto,
            ClaseDato.Correo => MarcaCorreo + Huella(texto) + "]",
            ClaseDato.Documento => MarcaDocumento + Huella(texto) + "]",
            ClaseDato.Telefono => MarcaTelefono + Huella(texto) + "]",
            ClaseDato.Persona => MarcaPersona + Huella(texto) + "]",
            ClaseDato.Direccion => MarcaDireccion + Huella(texto) + "]",
            _ => texto
        };
    }

    /// <summary>
    /// Capa 2: enmascara por FORMA del dato, sobre la linea ya serializada. Ultima red antes de
    /// escribir; ver la nota de la clase sobre por que los patrones son deliberadamente estrechos.
    /// </summary>
    public static string Sanear(string linea)
    {
        if (string.IsNullOrEmpty(linea))
        {
            return linea;
        }

        var salida = linea;

        if (salida.Contains('@'))
        {
            salida = PatronCorreo.Replace(
                salida,
                coincidencia => MarcaCorreo + Huella(coincidencia.Value) + "]");
        }

        salida = PatronTelefono.Replace(
            salida,
            coincidencia => MarcaTelefono + Huella(SoloDigitos(coincidencia.Value)) + "]");

        salida = PatronDocumentoConPalabra.Replace(
            salida,
            coincidencia => coincidencia.Groups[1].Value
                + coincidencia.Groups[2].Value
                + MarcaDocumento + Huella(coincidencia.Groups[3].Value) + "]");

        return salida;
    }

    /// <summary>Que clase de dato representa un nombre de campo. Publico si no se reconoce.</summary>
    public static ClaseDato Clasificar(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return ClaseDato.Publico;
        }

        var normalizada = Normalizar(clave);
        if (normalizada.Length == 0)
        {
            return ClaseDato.Publico;
        }

        // El orden importa: "passwordHash" es secreto antes que cualquier otra cosa, y
        // "correoDeRecuperacionToken" debe salir como secreto y no como correo.
        if (Contiene(normalizada, PalabrasSecreto))
        {
            return ClaseDato.Secreto;
        }

        if (Contiene(normalizada, PalabrasDocumento) || ClavesExactasDocumento.Contains(normalizada))
        {
            return ClaseDato.Documento;
        }

        if (Contiene(normalizada, PalabrasTelefono))
        {
            return ClaseDato.Telefono;
        }

        if (Contiene(normalizada, PalabrasCorreo))
        {
            return ClaseDato.Correo;
        }

        if (Contiene(normalizada, PalabrasDireccion))
        {
            return ClaseDato.Direccion;
        }

        if (ClavesExactasPersona.Contains(normalizada))
        {
            return ClaseDato.Persona;
        }

        return ClaseDato.Publico;
    }

    /// <summary>Si un texto ya lleva una marca de esta clase, no se vuelve a tocar.</summary>
    public static bool YaEnmascarado(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return false;
        }

        return texto.StartsWith(MarcaCorreo, StringComparison.Ordinal)
            || texto.StartsWith(MarcaDocumento, StringComparison.Ordinal)
            || texto.StartsWith(MarcaTelefono, StringComparison.Ordinal)
            || texto.StartsWith(MarcaPersona, StringComparison.Ordinal)
            || texto.StartsWith(MarcaDireccion, StringComparison.Ordinal)
            || string.Equals(texto, MarcaSecreto, StringComparison.Ordinal);
    }

    /// <summary>
    /// Huella estable dentro del proceso e irreversible fuera de el. Ocho caracteres bastan para
    /// distinguir a unas decenas de personas en una investigacion de un dia; no pretende ser una
    /// clave primaria.
    /// </summary>
    private static string Huella(string valor)
    {
        var normalizado = valor.Trim().ToLowerInvariant();
        var mac = HMACSHA256.HashData(SalDelProceso, Encoding.UTF8.GetBytes(normalizado));
        return Convert.ToHexString(mac, 0, 4).ToLowerInvariant();
    }

    private static string SoloDigitos(string valor)
    {
        var constructor = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (caracter >= '0' && caracter <= '9')
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString();
    }

    private static bool Contiene(string normalizada, string[] palabras)
    {
        foreach (var palabra in palabras)
        {
            if (normalizada.Contains(palabra, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Minusculas, sin tildes y sin separadores: "Telefono_Contacto" y "telefonoContacto" tienen
    /// que clasificar igual, porque en este repositorio conviven las dos formas.
    /// </summary>
    private static string Normalizar(string clave)
    {
        var descompuesta = clave.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesta.Length);
        foreach (var caracter in descompuesta)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                constructor.Append(char.ToLowerInvariant(caracter));
            }
        }

        return constructor.ToString();
    }
}
