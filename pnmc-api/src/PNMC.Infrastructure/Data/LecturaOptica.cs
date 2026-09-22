using System.Globalization;
using PDFtoImage;
using RapidOcrNet;
using SkiaSharp;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Lee una página que no tiene texto: la mira.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE PROBLEMA RESUELVE.</b> Un PDF puede no tener capa de texto —porque es un escaneo, o porque
/// su tipografía viene sin tabla <c>ToUnicode</c>— y entonces la lectura devolvía la mano vacía y
/// marcaba «requiere OCR» sin poder hacer nada más. Probado con documentos reales, ahí caía un libro
/// entero: «Poemas Humanos», de César Vallejo, del que solo se podía extraer <c>0þí´Ñ Q</c>.
/// </para>
/// <para>
/// <b>QUE MODELO Y POR QUE ESE.</b> PP-OCRv5 de PaddleOCR, en formato ONNX, ejecutado <b>dentro del
/// propio proceso .NET</b>. Licencia <b>Apache 2.0</b> en el modelo y en la biblioteca, que para una
/// entidad pública no es un detalle: los modelos de análisis de documentos más citados no sirven aquí
/// —LayoutLMv3 es CC-BY-NC-SA y prohíbe el uso comercial, y los de la familia YOLO de Ultralytics son
/// AGPL-3.0 y contaminarían todo el servicio que los invoque—.
/// </para>
/// <para>
/// <b>NO HAY PROCESO APARTE NI DEPENDENCIA DE PYTHON.</b> Era la condición de la decisión: los modelos
/// viajan dentro del paquete y se ejecutan con ONNX Runtime, así que desplegar esto es desplegar la
/// API. Un servicio de modelo multimodal —para leer un afiche de la Agenda, donde la información no
/// está jerarquizada por tamaño— sí traería esa dependencia, y por eso se dejó como decisión aparte.
/// </para>
/// <para>
/// <b>SOLO SE USA CUANDO NO HAY TEXTO.</b> Reconocer cuesta entre 175 y 520 ms por página —medido—, y
/// un PDF con capa de texto ya da un resultado mejor y gratis. Esto es la red de abajo, no el camino
/// principal.
/// </para>
/// </remarks>
public static class LecturaOptica
{
    /// <summary>Lo que se pudo ver en las páginas, listo para el mismo análisis que el texto.</summary>
    public sealed record Vista(IReadOnlyList<AnalisisDeLaPortada.Bloque> Portada, string Texto);

    /// <summary>
    /// A cuántos puntos por pulgada se rasteriza antes de mirar.
    /// </summary>
    /// <remarks>
    /// DOSCIENTOS, NO LOS 110 DE LA PORTADA. La miniatura de portada se rasteriza a 110 ppp porque se
    /// va a enseñar en pantalla; el reconocimiento necesita más resolución para separar las letras. Y
    /// no 300, porque el coste crece con el cuadrado y a 200 ya se leen bien los cuerpos pequeños.
    /// </remarks>
    private const int PuntosPorPulgada = 200;

    /// <summary>
    /// Cuántas páginas se reconocen.
    /// </summary>
    /// <remarks>
    /// TRES. La primera es la cubierta; detrás viene la página de créditos, que es donde viven el ISBN
    /// y el año. Reconocer las seis que lee la vía de texto costaría segundos sin encontrar nada más,
    /// porque en un libro escaneado el resto son páginas de contenido.
    /// </remarks>
    private const int PaginasQueSeReconocen = 3;

    /// <summary>
    /// Por debajo de esta confianza, lo reconocido no se propone.
    /// </summary>
    /// <remarks>
    /// El reconocedor devuelve una puntuación por carácter. Un sello, una firma manuscrita o el ruido
    /// del escaneo producen bloques con puntuaciones bajas, y proponerlos sería volver al problema que
    /// <see cref="TextoLegible"/> vino a resolver: basura que alguien tiene que detectar y borrar.
    /// </remarks>
    private const double ConfianzaMinima = 0.60;

    /// <summary>Un reconocedor para todo el proceso: cargar los modelos cuesta unos 70 ms.</summary>
    private static readonly Lazy<RapidOcr?> Reconocedor = new(Preparar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// EL RECONOCEDOR NO ES REENTRANTE, así que se serializan las llamadas. No es un cuello de botella
    /// real: quien carga documentos lo hace de uno en uno desde la consola institucional.
    /// </summary>
    private static readonly Lock Turno = new();

    private static RapidOcr? Preparar()
    {
        try
        {
            // LAS RUTAS SE RESUELVEN CONTRA EL DIRECTORIO DEL BINARIO Y NO CONTRA EL DE TRABAJO. La
            // biblioteca los busca en «models/v5» relativo al directorio de trabajo, que en un
            // servicio alojado no es el de la aplicación; así arrancaba bien en pruebas y fallaba al
            // desplegar, que es la peor forma de fallar.
            var carpeta = Path.Combine(AppContext.BaseDirectory, "models", "v5");
            var ocr = new RapidOcr();
            ocr.InitModels(
                Path.Combine(carpeta, "ch_PP-OCRv5_mobile_det.onnx"),
                Path.Combine(carpeta, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
                Path.Combine(carpeta, "latin_PP-OCRv5_rec_mobile_infer.onnx"),
                Path.Combine(carpeta, "ppocrv5_latin_dict.txt"));
            return ocr;
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            // SIN MODELOS SE SIGUE SIN RECONOCIMIENTO, no se cae la carga. La pantalla dirá que el
            // documento requiere OCR, que es exactamente lo que decía antes de existir esta clase.
            return null;
        }
    }

    /// <summary>¿Está disponible el reconocimiento en este despliegue?</summary>
    public static bool Disponible => Reconocedor.Value is not null;

    /// <summary>
    /// Mira las primeras páginas y devuelve lo mismo que devolvería la lectura de texto.
    /// </summary>
    /// <remarks>
    /// DEVUELVE LA MISMA FORMA A PROPOSITO: bloques con tamaño y altura relativa. Así
    /// <see cref="AnalisisDeLaPortada"/> decide qué es el título exactamente igual, sin saber si lo
    /// que le llega salió de la capa de texto o de mirar la página. Toda la lógica de encabezados,
    /// nombres propios y subtítulos vale igual, y no hay una segunda copia que se quede atrás.
    /// </remarks>
    public static Vista? Leer(byte[] datos, int paginasDelDocumento)
    {
        var ocr = Reconocedor.Value;
        if (ocr is null) { return null; }

        var cuantas = Math.Min(paginasDelDocumento, PaginasQueSeReconocen);
        if (cuantas < 1) { return null; }

        try
        {
            lock (Turno)
            {
                List<AnalisisDeLaPortada.Bloque>? portada = null;
                var texto = new List<string>();

                for (var pagina = 0; pagina < cuantas; pagina++)
                {
                    using var imagen = Conversion.ToImage(datos, page: pagina, options: new(Dpi: PuntosPorPulgada));
                    var visto = ocr.Detect(imagen, RapidOcrOptions.Default);

                    var lineas = Lineas(visto, imagen.Height);
                    texto.AddRange(lineas.Select(l => l.Texto));
                    portada ??= Unir(lineas);
                }

                return portada is null ? null : new Vista(portada, string.Join(' ', texto));
            }
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException and not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Una línea reconocida, ya con sus medidas en la escala de la página.</summary>
    private sealed record Linea(double Tamano, double AlturaRelativa, string Texto);

    /// <summary>
    /// Junta en una línea lo que el reconocedor devolvió como trozos sueltos a la misma altura.
    /// </summary>
    /// <remarks>
    /// CASO REAL: en la cubierta de «Poemas Humanos», «POEMAS» y «HUMANOS» vuelven como dos bloques
    /// distintos porque entre ellos hay un espacio grande de composición. Son una sola línea, y sin
    /// unirlos el título propuesto sería la mitad del título.
    /// </remarks>
    private static List<Linea> Lineas(OcrResult resultado, int altoEnPixeles)
    {
        var alto = (double)altoEnPixeles;
        var piezas = resultado.TextBlocks
            .Where(b => b.CharScores is { Length: > 0 } && b.CharScores.Average() >= ConfianzaMinima)
            .Where(b => !string.IsNullOrWhiteSpace(b.Text))
            .Select(b => new
            {
                b.Text,
                Arriba = (double)b.BoxPoints.Min(p => p.Y),
                Abajo = (double)b.BoxPoints.Max(p => p.Y),
                Izquierda = (double)b.BoxPoints.Min(p => p.X),
            })
            .Select(b => new { b.Text, b.Izquierda, b.Arriba, Alto = b.Abajo - b.Arriba, Centro = (b.Arriba + b.Abajo) / 2 })
            .Where(b => b.Alto > 0)
            .OrderBy(b => b.Centro)
            .ToList();

        var lineas = new List<Linea>();
        var i = 0;
        while (i < piezas.Count)
        {
            var grupo = new List<int> { i };
            var j = i + 1;

            // MISMA LINEA = los centros verticales no se separan más de media altura de letra.
            while (j < piezas.Count && Math.Abs(piezas[j].Centro - piezas[i].Centro) <= piezas[i].Alto * 0.5)
            {
                grupo.Add(j);
                j++;
            }

            var trozos = grupo.Select(k => piezas[k]).OrderBy(p => p.Izquierda).ToList();
            lineas.Add(new Linea(
                Tamano: EnPuntos(trozos.Max(p => p.Alto)),
                AlturaRelativa: 1 - (trozos.Min(p => p.Arriba) / alto),
                Texto: string.Join(' ', trozos.Select(p => p.Text.Trim()))));
            i = j;
        }

        return lineas;
    }

    /// <summary>
    /// Une líneas seguidas del mismo cuerpo: un título largo ocupa varias.
    /// </summary>
    /// <remarks>
    /// ES LA MISMA REGLA QUE APLICA LA LECTURA DE TEXTO a las líneas de un PDF, y por el mismo motivo:
    /// sin ella, «Aproximación al concepto de gobernanza en Colombia / y algunos apuntes sobre su
    /// importancia / en el derecho ambiental» son tres bloques, gana el primero y el resto del título
    /// se pierde o acaba propuesto como subtítulo.
    /// </remarks>
    private static List<AnalisisDeLaPortada.Bloque> Unir(List<Linea> lineas)
    {
        var bloques = new List<AnalisisDeLaPortada.Bloque>();
        var i = 0;
        while (i < lineas.Count)
        {
            var actual = lineas[i];
            var texto = actual.Texto;
            var j = i + 1;

            while (j < lineas.Count
                   && Math.Abs(lineas[j].Tamano - actual.Tamano) <= actual.Tamano * 0.15
                   && actual.AlturaRelativa - lineas[j].AlturaRelativa > 0
                   && actual.AlturaRelativa - lineas[j].AlturaRelativa <= 0.06)
            {
                texto += " " + lineas[j].Texto;
                actual = lineas[j] with { Tamano = actual.Tamano, AlturaRelativa = lineas[j].AlturaRelativa };
                j++;
            }

            bloques.Add(new AnalisisDeLaPortada.Bloque(lineas[i].Tamano, lineas[i].AlturaRelativa, texto));
            i = j;
        }

        return bloques.OrderByDescending(b => b.Tamano).ToList();
    }

    /// <summary>
    /// De píxeles a puntos tipográficos, para que la escala sea la misma que la de la capa de texto.
    /// </summary>
    /// <remarks>
    /// IMPORTA QUE SEA LA MISMA ESCALA: <see cref="AnalisisDeLaPortada"/> compara tamaños entre sí y
    /// también contra umbrales escritos en puntos. Si aquí llegaran píxeles a 200 ppp, todo parecería
    /// tres veces más grande y los umbrales dejarían de significar lo que dicen.
    /// </remarks>
    private static double EnPuntos(double pixeles) =>
        Math.Round(pixeles * 72.0 / PuntosPorPulgada, 0, MidpointRounding.AwayFromZero);

    /// <summary>Para los informes: qué modelo se usó, dicho con su nombre.</summary>
    public static string Modelo => string.Create(CultureInfo.InvariantCulture, $"PP-OCRv5 latin (ONNX, Apache 2.0) a {PuntosPorPulgada} ppp");
}
