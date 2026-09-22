using System.Text;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// EL RECONOCEDOR DE FORMATO, que es la única cosa que decide si unos bytes entran al sitio.
/// <para>
/// La pregunta que responden estas pruebas no es «¿acepta un PNG?». Es «¿qué hace falta para
/// colar algo que no es una imagen?». Por eso la mitad de los casos son archivos que se parecen
/// a una imagen sin serlo: una firma pegada delante de basura, un ejecutable renombrado, un SVG
/// con un <c>onload</c>.
/// </para>
/// <para>
/// Las cuatro imágenes de referencia son REALES: producidas con Pillow, 64 × 40 píxeles, y
/// pegadas aquí en base64. No se generan en la prueba a propósito. Un PNG generado por el mismo
/// código que lo valida —calculando su propio CRC— pasaría aunque el CRC estuviera mal
/// calculado en los dos sitios, y la prueba no mediría nada.
/// </para>
/// </summary>
public sealed class MediosWebContratoTests
{
    private const int AnchoDeReferencia = 64;
    private const int AltoDeReferencia = 40;

    // PNG, 140 bytes, RGB de 8 bits sin entrelazar.
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAAAoCAIAAADBrGu+AAAAU0lEQVR4nO3RgQkAMQCDwBTkZ/7xO4YUPLJA8Gw/28v79jQ6IKMC" +
        "MiogowIyKiCjAjIqIKMCMiogowIyKiCjAjIqIKMCMiogowIyKiCjAjIqMNcFqA4C/LjkybcAAAAASUVORK5CYII=";

    // JPEG, 895 bytes, con su APP0 de JFIF y su marcador de cierre FF D9.
    private const string Jpeg =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYn" +
        "KSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgo" +
        "KCgoKCgoKCj/wAARCAAoAEADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUF" +
        "BAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVW" +
        "V1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi" +
        "4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
        "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVm" +
        "Z2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq" +
        "8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD59hsvarkNl7VsxWXtV2Ky9q+unijgwuN8zGhsvarsVl7VsxWXtVyKy9q4p4o+kwuN8zHi" +
        "svarkVl7VsxWXtV2Ky9q454o+lwuN8zGisvarsVl7VsxWXtVyKy9q454o+kwuN8zjIrL2q5FZe1bMNl7VdhsvaieKP5jwuN8zGhs" +
        "varsNl7VsxWXtV2Ky9q454o+lwuN8zGisvarsVl7VsRWXtV2Ky9q454o+lwuN8zGisvarsVl7VsxWXtV2Ky9q4qmKPpMLjfM4uKy" +
        "9quxWXtRRW86kj+a8JVkXIrL2q7FZe1FFcU6kj6TCVZF2Ky9quxWXtRRXHUqSPpcJVkXIrL2q7FZe1FFcc6kj6TCVZH/2Q==";

    // WebP con pérdida (chunk "VP8 ").
    private const string Webp =
        "UklGRtoAAABXRUJQVlA4IM4AAABwBwCdASpAACgAPmkmkEWxIiGb/HQBEAaEswDCANhE8iVfkcGu53BPWHQtwuKGJxo4YPR6GpEA" +
        "r3E1TX7F10z1IQxTgAD+/aR29YoEpxsGqc//LErWKYv9a3Kv6y06hvv0VNfV8O00Q9PN2/gW3stPQHINRyQves5e4vXZN+Ea3ib6" +
        "P4+1ytx+LnxU225S1FGU9ZAgK64eZ4VqmaCJAgZIwew1EKPmJtIzlTO9srrw4IOkVsKu3Zs/wSbH4skFLxSWN8Scghj9iHr9BpYA" +
        "AA==";

    // WebP sin pérdida (chunk "VP8L"). Codifica las dimensiones de otra forma, así que es un
    // camino distinto del reconocedor y no una repetición del caso anterior.
    private const string WebpSinPerdida =
        "UklGRjIAAABXRUJQVlA4TCYAAAAvP8AJALkyRPQ/dhHR/wCRtk3R/fsefwgkbfG2f/BiAtDVB6j/Lw==";

    private static byte[] Bytes(string base64) => Convert.FromBase64String(base64);

    [Theory]
    [InlineData(Png, "image/png")]
    [InlineData(Jpeg, "image/jpeg")]
    [InlineData(Webp, "image/webp")]
    [InlineData(WebpSinPerdida, "image/webp")]
    public void ReconoceLosTresFormatosYSusMedidas(string base64, string tipoEsperado)
    {
        var formato = MediosWebContrato.Reconocer(Bytes(base64));

        Assert.NotNull(formato);
        Assert.Equal(tipoEsperado, formato.Value.Mime);

        // LAS MEDIDAS SE AFIRMAN, no solo el tipo. Sin esto, un reconocedor que devolviera
        // siempre (tipo, 0, 0) pasaría, y el panel mostraría «0 × 0» en las 44 ranuras.
        Assert.Equal(AnchoDeReferencia, formato.Value.Width);
        Assert.Equal(AltoDeReferencia, formato.Value.Height);
    }

    /// <summary>
    /// Un ejecutable de Windows renombrado a <c>.png</c>. Es el caso más simple y el que separa
    /// «miro la extensión» de «miro los bytes».
    /// </summary>
    [Fact]
    public void UnEjecutableRenombradoNoEsUnaImagen()
    {
        var pe = new byte[512];
        pe[0] = 0x4D; // M
        pe[1] = 0x5A; // Z
        pe[2] = 0x90;

        Assert.Null(MediosWebContrato.Reconocer(pe));
    }

    /// <summary>
    /// LA PRUEBA QUE MIDE LA DIFERENCIA entre mirar la firma y comprobar la estructura.
    /// <para>
    /// Los ocho bytes exactos de la firma PNG, la longitud correcta del chunk, el literal
    /// «IHDR», unas dimensiones plausibles… y basura a partir de ahí. Todo eso se copia de un
    /// PNG cualquiera en un segundo. Lo que no se copia es el CRC32, porque cubre justo los
    /// trece bytes que se acaban de manipular.
    /// </para>
    /// </summary>
    [Fact]
    public void UnPngConLaCabezaCopiadaYElCrcMalNoEsUnaImagen()
    {
        var real = Bytes(Png);
        var falso = new byte[real.Length];
        Array.Copy(real, falso, real.Length);

        // Se cambia el ancho de 64 a 65 y no se toca el CRC: es exactamente lo que haría quien
        // fabrica una cabecera a mano.
        falso[19] = 65;

        Assert.NotNull(MediosWebContrato.Reconocer(real));
        Assert.Null(MediosWebContrato.Reconocer(falso));
    }

    [Fact]
    public void UnaFirmaPngSeguidaDeBasuraNoEsUnaImagen()
    {
        var basura = new byte[4096];
        ReadOnlySpan<byte> firma = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        firma.CopyTo(basura);
        for (var i = 8; i < basura.Length; i++)
        {
            basura[i] = (byte)(i * 7 % 251);
        }

        Assert.Null(MediosWebContrato.Reconocer(basura));
    }

    /// <summary>
    /// SVG. No está entre los tipos permitidos y tampoco lo reconoce el olfateador: las dos
    /// puertas están cerradas, y esta prueba afirma la segunda. Un SVG servido desde el mismo
    /// origen que el sitio es XSS almacenado.
    /// </summary>
    [Fact]
    public void UnSvgNoEsUnaImagenParaEsteContrato()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"></svg>");

        Assert.Null(MediosWebContrato.Reconocer(svg));
        Assert.DoesNotContain("image/svg+xml", MediosWebContrato.TiposPermitidos);
    }

    /// <summary>
    /// Un JPEG al que le falta el cierre <c>FF D9</c>. Es lo que llega cuando una subida se corta
    /// a la mitad, y guardarlo dejaría una portada rota en el sitio sin que nada lo dijera.
    /// </summary>
    [Fact]
    public void UnJpegTruncadoNoEsUnaImagen()
    {
        var real = Bytes(Jpeg);
        var truncado = real[..(real.Length - 40)];

        Assert.Null(MediosWebContrato.Reconocer(truncado));
    }

    /// <summary>
    /// Un WebP cuya longitud RIFF no cuadra con el tamaño real. Es la comprobación que carga
    /// peso en WebP: la firma se copia, pero el entero de la posición 4 depende del archivo
    /// entero.
    /// </summary>
    [Fact]
    public void UnWebpConLaLongitudRiffMalNoEsUnaImagen()
    {
        var real = Bytes(Webp);
        var falso = new byte[real.Length];
        Array.Copy(real, falso, real.Length);
        falso[4] = (byte)(falso[4] + 1);

        Assert.NotNull(MediosWebContrato.Reconocer(real));
        Assert.Null(MediosWebContrato.Reconocer(falso));
    }

    /// <summary>
    /// LA BOMBA DE DESCOMPRESIÓN. Un PNG de pocos bytes que declara 60.000 × 60.000 píxeles: al
    /// decodificarlo pediría más de diez gigabytes. Se rechaza por las dimensiones, sin
    /// decodificar nada, que es la única defensa que no cuesta memoria.
    /// <para>
    /// El CRC se recalcula a propósito, para que el archivo sea un PNG legítimo en todo lo demás
    /// y la prueba mida el tope de dimensiones y no el CRC.
    /// </para>
    /// </summary>
    [Fact]
    public void UnPngQueDeclaraSesentaMilPixelesPorLadoSeRechaza()
    {
        var bomba = ConstruirPngConDimensiones(60_000, 60_000);

        // Primero: el archivo es correcto salvo por el tamaño. Si esta afirmación fallara, la
        // prueba estaría midiendo el CRC y no el tope.
        Assert.NotNull(MediosWebContrato.Reconocer(ConstruirPngConDimensiones(64, 40)));
        Assert.Null(MediosWebContrato.Reconocer(bomba));
    }

    /// <summary>
    /// Un PNG que cabe en el lado máximo por los pelos pero cuya área supera los cincuenta
    /// megapíxeles. Sin el tope de área, 12.000 × 12.000 pasaría: son 144 megapíxeles.
    /// </summary>
    [Fact]
    public void ElTopeDeAreaSeAplicaAunqueCadaLadoQuepa()
    {
        Assert.Null(MediosWebContrato.Reconocer(ConstruirPngConDimensiones(12_000, 12_000)));
        Assert.NotNull(MediosWebContrato.Reconocer(ConstruirPngConDimensiones(4_000, 3_000)));
    }

    /// <summary>
    /// EL CASO QUE FALTABA, y lo destapó un mutante que sobrevivía. Quitar el tope de lado
    /// (<c>MaxDimension</c>) dejaba las once pruebas en verde: la bomba de 60.000 × 60.000 la
    /// rechazaba el tope de ÁREA, no el de lado, así que el tope de lado no lo medía nada.
    /// <para>
    /// Un panorama de 13.000 × 1.000 son trece megapíxeles: pasa el tope de área de sobra y
    /// supera el de lado. Es el único caso que separa las dos guardas, y no es rebuscado: una
    /// panorámica exportada de un móvil llega a esas proporciones.
    /// </para>
    /// </summary>
    [Fact]
    public void UnPanoramaMasAnchoDelTopeSeRechazaAunqueSuAreaQuepa()
    {
        var panorama = ConstruirPngConDimensiones(13_000, 1_000);

        // 13 millones de píxeles: muy por debajo de los cincuenta del tope de área. Si esta
        // prueba se pusiera roja por el área, no estaría midiendo lo que dice medir.
        Assert.True(13_000L * 1_000 < MediosWebContrato.MaxPixels);
        Assert.Null(MediosWebContrato.Reconocer(panorama));
    }

    /// <summary>
    /// OTRO CASO QUE DESTAPÓ UN MUTANTE. Quitar la comprobación de profundidad de bits y tipo de
    /// color no ponía nada en rojo, porque todas las imágenes de referencia son legítimas.
    /// <para>
    /// El tipo de color 7 no existe en la norma PNG. Un archivo que lo declara no salió de
    /// ningún codificador: lo escribió alguien a mano. Aquí el CRC se recalcula a propósito,
    /// para que lo que rechace el archivo sea la combinación imposible y no el CRC.
    /// </para>
    /// </summary>
    [Fact]
    public void UnPngConTipoDeColorInexistenteSeRechaza()
    {
        var imposible = ConstruirPngConDimensiones(64, 40, profundidad: 8, tipoDeColor: 7);
        var legitimo = ConstruirPngConDimensiones(64, 40, profundidad: 8, tipoDeColor: 2);

        Assert.NotNull(MediosWebContrato.Reconocer(legitimo));
        Assert.Null(MediosWebContrato.Reconocer(imposible));

        // Y una combinación que existe por separado pero no junta: color indexado (3) admite
        // hasta 8 bits, nunca 16.
        Assert.Null(MediosWebContrato.Reconocer(ConstruirPngConDimensiones(64, 40, profundidad: 16, tipoDeColor: 3)));
    }

    [Fact]
    public void LaHuellaEsSha256EnHexadecimalMinusculo()
    {
        var huella = MediosWebContrato.Huella(Bytes(Png));

        Assert.Equal(64, huella.Length);
        Assert.Equal(huella.ToLowerInvariant(), huella);
        Assert.All(huella, c => Assert.Contains(c, "0123456789abcdef"));

        // Dos archivos distintos, dos huellas distintas: es lo que hace que el ETag sirva.
        Assert.NotEqual(huella, MediosWebContrato.Huella(Bytes(Jpeg)));
    }

    [Fact]
    public void UnArchivoVacioNoEsUnaImagen()
    {
        Assert.Null(MediosWebContrato.Reconocer(ReadOnlySpan<byte>.Empty));
        Assert.Null(MediosWebContrato.Reconocer(new byte[4]));
    }

    /// <summary>
    /// Construye un PNG mínimo válido —firma, IHDR con su CRC, IEND— de las dimensiones pedidas.
    /// Solo lo usan las dos pruebas de tamaño: en todo lo demás se usan las imágenes reales, para
    /// que el reconocedor no se mida contra un generador escrito con las mismas creencias.
    /// </summary>
    private static byte[] ConstruirPngConDimensiones(uint ancho, uint alto, byte profundidad = 8, byte tipoDeColor = 2)
    {
        var salida = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        salida.AddRange([0x00, 0x00, 0x00, 0x0D]);

        var chunk = new List<byte> { (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        chunk.AddRange([(byte)(ancho >> 24), (byte)(ancho >> 16), (byte)(ancho >> 8), (byte)ancho]);
        chunk.AddRange([(byte)(alto >> 24), (byte)(alto >> 16), (byte)(alto >> 8), (byte)alto]);
        chunk.AddRange([profundidad, tipoDeColor, 0, 0, 0]); // por omisión: 8 bits, RGB, sin entrelazar

        salida.AddRange(chunk);
        var crc = Crc32(CollectionsMarshalToArray(chunk));
        salida.AddRange([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);

        // IEND, para que el archivo esté completo.
        salida.AddRange([0x00, 0x00, 0x00, 0x00, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82]);
        return [.. salida];
    }

    private static byte[] CollectionsMarshalToArray(List<byte> lista) => [.. lista];

    private static uint Crc32(ReadOnlySpan<byte> datos)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in datos)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
