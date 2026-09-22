using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Lo que el documento <b>enseña</b>, no solo lo que dice de sí mismo.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTA ES LA SEGUNDA CAPA Y LA QUE MAS DA.</b> <see cref="ExtractorDeDocumentos"/> lee el
/// diccionario de metadatos, que muchos PDF traen vacío o con basura. Aquí se lee el TEXTO de las
/// primeras páginas, con el tamaño de letra de cada palabra, y de ahí sale casi todo lo que una
/// ficha necesita.
/// </para>
/// <para>
/// <b>EL TITULO ES EL BLOQUE DE LETRA MAS GRANDE DE LA PRIMERA PAGINA, y eso está medido.</b> Sobre
/// documentos reales del proyecto: en «Informe Final de Entrega» el bloque de 22 pt es exactamente el
/// título y el de 14 pt el subtítulo; en «Entorno Virtual PNMC» el de 18 pt es el título. No hace
/// falta ningún modelo para eso: hace falta mirar el tamaño de la letra, que es justo lo que hace
/// quien mira una portada.
/// </para>
/// <para>
/// <b>EL CASO NEGATIVO TAMBIEN ESTA MEDIDO, Y HAY QUE DECIRLO.</b> Un PDF cuya primera página es una
/// imagen —una portada escaneada— no devuelve ni una palabra. Para esos, esta capa no puede hacer
/// nada y hace falta OCR. Se detecta y se declara en <see cref="LecturaDeDocumento.PaginaSinTexto"/>
/// en vez de devolver un resultado vacío que parezca un documento sin título.
/// </para>
/// <para>
/// <b>NADA DE LO QUE SALE DE AQUI SE GUARDA SOLO.</b> Es una PROPUESTA, y cada pieza viaja con su
/// origen para que quien cataloga sepa de dónde salió y decida. Es la restricción del proyecto sobre
/// la importación asistida, y aquí se cumple por construcción: esta clase no escribe en ninguna tabla.
/// </para>
/// </remarks>
public static class LecturaDelDocumento
{
    /// <summary>Un bloque de texto de la portada, con el tamaño de letra que lo distingue.</summary>
    /// <param name="AlturaRelativa">Dónde está en la página, de 0 (pie) a 1 (cabecera).</param>
    public sealed record BloqueDePortada(double Tamano, double AlturaRelativa, string Texto);

    /// <summary>Lo leído del documento, con el origen de cada pieza.</summary>
    /// <param name="PaginaSinTexto">
    /// La primera página no tiene texto extraíble: es una imagen. Sin OCR no hay nada que leer.
    /// </param>
    public sealed record LecturaDeDocumento(
        int Paginas,
        IReadOnlyList<BloqueDePortada> Portada,
        IReadOnlyList<string> Isbn,
        IReadOnlyList<string> Ismn,
        IReadOnlyList<int> Anios,
        string TextoDeLasPrimerasPaginas,
        bool PaginaSinTexto,
        /// <summary>
        /// Lo leído salió de MIRAR la página, no de su capa de texto.
        /// </summary>
        /// <remarks>
        /// VIAJA HASTA LA PANTALLA A PROPOSITO. Es el mismo principio que rige la procedencia de los
        /// registros: quien cataloga tiene derecho a saber de dónde salió cada dato y con qué
        /// fiabilidad. Un título reconocido ópticamente merece una segunda mirada que uno extraído de
        /// la capa de texto no necesita, y esconder esa diferencia sería presentar como equivalentes
        /// dos cosas que no lo son.
        /// </remarks>
        bool LeidoConReconocimientoOptico,
        /// <summary>Qué es cada bloque de la portada, ya decidido. Ver <see cref="AnalisisDeLaPortada"/>.</summary>
        AnalisisDeLaPortada.Analisis Analisis);

    private static readonly TimeSpan TopeDeBusqueda = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Cuántas páginas se leen para cosechar identificadores y fechas.
    /// </summary>
    /// <remarks>
    /// EN UNA PUBLICACION, EL ISBN Y EL AÑO VIVEN EN LA PAGINA DE CREDITOS, que está justo detrás de
    /// la portada. Leer el libro entero para encontrarlos costaría segundos y no encontraría nada
    /// nuevo; leer solo la primera se los perdería. Seis es el compromiso: cubre portada,
    /// contraportada interior, página de créditos y el índice que a veces se cuela en medio.
    /// </remarks>
    private const int PaginasQueSeLeen = 6;

    public static LecturaDeDocumento Leer(byte[] datos)
    {
        using var documento = PdfDocument.Open(datos);
        var total = documento.NumberOfPages;

        var portada = BloquesDeLaPortada(documento);
        var texto = TextoDeLasPrimeras(documento, Math.Min(total, PaginasQueSeLeen));

        // ── LA RED DE ABAJO: si la página no tiene texto, se mira ───────────────────
        // Solo entonces. Un PDF con capa de texto da un resultado mejor y gratis, y reconocer cuesta
        // entre 175 y 520 ms por página. Ver LecturaOptica para el modelo y su licencia.
        var conOcr = false;
        if (portada.Count == 0)
        {
            var vista = LecturaOptica.Leer(datos, total);
            if (vista is not null && vista.Portada.Count > 0)
            {
                portada = vista.Portada
                    .Select(b => new BloqueDePortada(b.Tamano, b.AlturaRelativa, b.Texto))
                    .ToList();
                // EL TEXTO RECONOCIDO SE SUMA Y NO SUSTITUYE: un documento puede tener capa de texto
                // en unas páginas y no en otras —una cubierta escaneada pegada delante de un interior
                // digital—, y perder lo que sí se pudo extraer sería cambiar un acierto por otro.
                texto = string.Join(' ', new[] { texto, vista.Texto }.Where(x => !string.IsNullOrWhiteSpace(x)));
                conOcr = true;
            }
        }

        // EL RESTO DE PAGINAS ES LO QUE PERMITE RECONOCER UN ENCABEZADO: lo que se repite fuera de la
        // primera página no es el título de nada. Sin esto, el análisis solo puede mirar tamaños.
        var otrasPaginas = total > 1 ? TextoDeOtras(documento, Math.Min(total, PaginasQueSeLeen)) : string.Empty;
        var metadatos = ExtractorDeDocumentos.Leer(datos);

        var analisis = AnalisisDeLaPortada.Analizar(
            portada.Select(b => new AnalisisDeLaPortada.Bloque(b.Tamano, b.AlturaRelativa, b.Texto)).ToList(),
            metadatos.Titulo,
            otrasPaginas,
            texto);

        return new LecturaDeDocumento(
            Paginas: total,
            Portada: portada,
            Isbn: Identificadores(texto, "ISBN", EsIsbnValido),
            Ismn: Identificadores(texto, "ISMN", EsIsmnValido),
            Anios: Anios(texto),
            TextoDeLasPrimerasPaginas: texto,
            PaginaSinTexto: portada.Count == 0,
            LeidoConReconocimientoOptico: conOcr,
            Analisis: analisis);
    }

    /// <summary>
    /// Los bloques de la primera página, del texto más grande al más pequeño.
    /// </summary>
    /// <remarks>
    /// SE AGRUPA POR TAMAÑO REDONDEADO A ENTERO. Dentro de un mismo titular, las letras varían unas
    /// décimas de punto; sin redondear, un título de seis palabras salían seis bloques de una.
    /// </remarks>
    private static List<BloqueDePortada> BloquesDeLaPortada(PdfDocument documento)
    {
        if (documento.NumberOfPages < 1) { return []; }

        var pagina = documento.GetPage(1);
        var alto = pagina.Height;
        if (alto <= 0) { return []; }

        // ── Primero, cada LINEA con su cuerpo de letra ──────────────────────────────
        //
        // SE AGRUPA POR TAMAÑO **Y POR LINEA**, no solo por tamaño. Agrupando solo por tamaño, todo el
        // cuerpo de un artículo —que va a 12 pt— quedaba en UN bloque gigante junto al subtítulo, y
        // no había forma de distinguir la línea que sigue al título del párrafo tercero.
        var lineas = pagina.GetWords()
            .Where(palabra => palabra.Letters.Count > 0)
            .GroupBy(palabra => (
                Tamano: Math.Round(palabra.Letters.Average(letra => letra.PointSize), 0),
                Linea: Math.Round(palabra.BoundingBox.Bottom / 6)))
            .Select(grupo => new
            {
                grupo.Key.Tamano,
                Base = grupo.First().BoundingBox.Bottom,
                Texto = Limpiar(string.Join(' ', grupo.Select(palabra => palabra.Text))),
            })
            .Where(linea => linea.Texto.Length > 0)
            .OrderByDescending(linea => linea.Base)
            .ToList();

        // ── Después, las líneas contiguas del MISMO cuerpo se unen ──────────────────
        //
        // UN TITULO LARGO OCUPA VARIAS LINEAS, y partirlo es lo que hacía la versión anterior: de
        // «Aproximación al concepto de gobernanza en Colombia / y algunos apuntes sobre su
        // importancia / en el derecho ambiental» —tres líneas a 26 pt en un artículo real— tomaba la
        // primera por título y la segunda por subtítulo. El criterio es este: «parte el
        // nombre en dos como si fuera título y subtítulo».
        //
        // SE UNEN POR CUERPO Y POR CERCANIA: mismo tamaño de letra y un hueco vertical que no pase de
        // dos veces ese tamaño, que es el interlineado de un titular. Con un hueco mayor ya no es la
        // continuación de la misma frase sino otro bloque de la página.
        var bloques = new List<BloqueDePortada>();
        foreach (var porTamano in lineas.GroupBy(linea => linea.Tamano))
        {
            var separacionMaxima = porTamano.Key * 2.0;
            var enCurso = new List<string>();
            double? baseAnterior = null;
            double baseDelBloque = 0;

            foreach (var linea in porTamano.OrderByDescending(x => x.Base))
            {
                var sigue = baseAnterior is { } previa && previa - linea.Base <= separacionMaxima;
                if (!sigue && enCurso.Count > 0)
                {
                    bloques.Add(new BloqueDePortada(porTamano.Key, baseDelBloque / alto, string.Join(' ', enCurso)));
                    enCurso = [];
                }

                if (enCurso.Count == 0) { baseDelBloque = linea.Base; }
                enCurso.Add(linea.Texto);
                baseAnterior = linea.Base;
            }

            if (enCurso.Count > 0)
            {
                bloques.Add(new BloqueDePortada(porTamano.Key, baseDelBloque / alto, string.Join(' ', enCurso)));
            }
        }

        return bloques
            .Where(bloque => bloque.Texto.Length > 1)
            .OrderByDescending(bloque => bloque.Tamano).ThenByDescending(bloque => bloque.AlturaRelativa)
            .Take(14)
            .ToList();
    }

    /// <summary>El texto de la página 2 en adelante, para reconocer lo que se repite.</summary>
    private static string TextoDeOtras(PdfDocument documento, int hasta)
    {
        var partes = new List<string>();
        for (var numero = 2; numero <= hasta; numero++)
        {
            partes.Add(string.Join(' ', documento.GetPage(numero).GetWords().Select(p => p.Text)));
        }
        return Limpiar(string.Join(' ', partes));
    }

    private static string TextoDeLasPrimeras(PdfDocument documento, int cuantas)
    {
        var partes = new List<string>();
        for (var numero = 1; numero <= cuantas; numero++)
        {
            var palabras = documento.GetPage(numero).GetWords().Select(palabra => palabra.Text);
            partes.Add(string.Join(' ', palabras));
        }
        return Limpiar(string.Join(' ', partes));
    }

    /// <summary>
    /// Los identificadores del esquema pedido, <b>solo si el dígito de control cuadra</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AQUI NO SE CONJETURA: SE COMPRUEBA. Un ISBN y un ISMN llevan un dígito de control calculado
    /// sobre los demás, así que se puede saber con CERTEZA si una cadena de trece cifras encontrada
    /// en una página es un identificador de verdad o el número de un teléfono. Es la diferencia entre
    /// proponer un dato y proponer ruido.
    /// </para>
    /// <para>
    /// Se acepta con o sin el rótulo delante: muchas páginas de créditos escriben «ISBN: 978-…» y
    /// otras solo el número.
    /// </para>
    /// </remarks>
    private static List<string> Identificadores(string texto, string esquema, Func<string, bool> esValido)
    {
        var patron = new Regex(
            esquema + @"[^0-9]{0,12}((?:97[89][\s\-]?)?[0-9][0-9\s\-]{8,16}[0-9Xx])",
            RegexOptions.IgnoreCase,
            TopeDeBusqueda);

        var encontrados = new List<string>();
        foreach (Match coincidencia in patron.Matches(texto))
        {
            var candidato = coincidencia.Groups[1].Value.Trim();
            var cifras = new string(candidato.Where(c => char.IsAsciiDigit(c) || c is 'X' or 'x').ToArray());
            if (esValido(cifras) && !encontrados.Contains(candidato, StringComparer.OrdinalIgnoreCase))
            {
                encontrados.Add(candidato);
            }
        }
        return encontrados;
    }

    /// <summary>El dígito de control de un ISBN, de 10 o de 13 cifras.</summary>
    public static bool EsIsbnValido(string cifras)
    {
        if (cifras.Length == 13)
        {
            // ISBN-13: pesos alternos 1 y 3; la suma tiene que ser múltiplo de diez.
            var suma = cifras.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
            return cifras.All(char.IsAsciiDigit) && suma % 10 == 0;
        }

        if (cifras.Length == 10)
        {
            // ISBN-10: pesos de 10 a 1, módulo once, y la última posición admite «X» por el diez.
            var suma = 0;
            for (var i = 0; i < 9; i++)
            {
                if (!char.IsAsciiDigit(cifras[i])) { return false; }
                suma += (cifras[i] - '0') * (10 - i);
            }
            var ultima = char.ToUpperInvariant(cifras[9]);
            suma += ultima == 'X' ? 10 : char.IsAsciiDigit(ultima) ? ultima - '0' : -1000;
            return suma % 11 == 0;
        }

        return false;
    }

    /// <summary>
    /// El dígito de control de un ISMN.
    /// </summary>
    /// <remarks>
    /// UN ISMN ES UN EAN-13 QUE EMPIEZA POR 979-0, así que comparte el algoritmo del ISBN-13. Se
    /// comprueba además el prefijo: sin él, cualquier EAN de un producto de supermercado pasaría por
    /// identificador de una partitura.
    /// </remarks>
    public static bool EsIsmnValido(string cifras) =>
        cifras.Length == 13 && cifras.StartsWith("9790", StringComparison.Ordinal) && EsIsbnValido(cifras);

    /// <summary>
    /// Los años que aparecen en el texto, del más repetido al menos.
    /// </summary>
    /// <remarks>
    /// SE ACOTA A UN RANGO CON SENTIDO. Sin acotar, «1er movimiento» y los números de una tabla se
    /// cuelan como años. El acervo real va de 1990 a 2024; se admite desde 1900 para no cerrar la
    /// puerta a una reedición de un facsímil, y hasta el año que viene por los depósitos legales
    /// adelantados.
    /// </remarks>
    private static List<int> Anios(string texto)
    {
        var maximo = DateTime.UtcNow.Year + 1;
        return Regex.Matches(texto, @"\b(1[89]\d{2}|20\d{2})\b", RegexOptions.None, TopeDeBusqueda)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Where(anio => anio >= 1900 && anio <= maximo)
            .GroupBy(anio => anio)
            .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key)
            .Select(g => g.Key)
            .Take(5)
            .ToList();
    }

    private static string Limpiar(string texto) =>
        Regex.Replace(texto ?? string.Empty, @"\s+", " ", RegexOptions.None, TopeDeBusqueda).Trim();
}
