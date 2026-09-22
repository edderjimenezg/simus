using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Los topes y el reconocedor de formato de las imágenes administrables del sitio.
/// <para>
/// Vive en Infrastructure y no en el endpoint porque <b>la siembra también necesita los
/// topes</b>: sembrar una fila con un dato que después nadie pueda volver a guardar deja esa
/// ranura bloqueada sin que nada avise. Es la misma razón por la que
/// <see cref="ContratoDeEquipoWeb"/> está aquí y no en <c>EquipoWebEndpoints</c>.
/// </para>
/// <para>
/// <b>LO QUE ESTA CLASE NO HACE, dicho explícitamente:</b> no reencoda. Un JPEG con EXIF de GPS
/// o con perfil ICC se guarda tal cual. El panel lo borra al reencodar en <c>canvas</c>, pero eso
/// es una cortesía del cliente, no una garantía del servidor.
/// </para>
/// </summary>
public static class MediosWebContrato
{
    /// <summary>
    /// Tope por archivo: 2 MiB.
    /// <para>
    /// El fichero más grande que hay hoy en el sitio pesa 227.579 bytes
    /// (<c>public/assets/branding/pnmc-blanco.png</c>, medido). Un hero de 1600 px en WebP q80
    /// ronda 250-300 KB. Dos MiB es nueve veces el mayor archivo real: deja pasar un PNG mal
    /// exportado y no deja pasar un vídeo.
    /// </para>
    /// <para>
    /// El mismo número está en <c>CK_MediosWeb_BorradorTope</c> y <c>CK_MediosWeb_PublicadoTope</c>.
    /// Si se cambia aquí hay que cambiarlo allí: la prueba de esquema lo mide.
    /// </para>
    /// </summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Tope de la miniatura: 24 KiB. Es un WebP de 320 px que produce el navegador. La cuadrícula
    /// del panel pinta dieciséis a la vez: 16 × 24 KiB son 384 KiB, frente a los ~4 MB que
    /// costaría pintarla con los originales.
    /// </summary>
    public const int MaxThumbnailBytes = 24 * 1024;

    /// <summary>Tope del texto alternativo. Mismo ancho que la columna.</summary>
    public const int MaxAltLength = 300;

    /// <summary>
    /// Lado máximo, en píxeles. Junto con <see cref="MaxPixels"/> cierra la bomba de
    /// descompresión sin decodificar un píxel: un PNG de 4 KB que declara 50.000 × 50.000 se
    /// rechaza antes de reservar nada.
    /// </summary>
    public const int MaxDimension = 12_000;

    /// <summary>Área máxima. 50 megapíxeles: más que cualquier fotografía de este sitio.</summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>
    /// Los tres formatos, y ni uno más.
    /// <para>
    /// <b>SVG queda fuera y no es una omisión.</b> Un SVG es un documento XML que puede llevar
    /// <c>&lt;script&gt;</c>, y esta ruta lo serviría desde el mismo origen que el sitio. Un SVG
    /// publicado sería XSS almacenado con permiso de webmaster.
    /// </para>
    /// <para>
    /// Son exactamente los tres de <see cref="ContratoDeEquipoWeb"/>, para que el panel no tenga que
    /// aprender un segundo vocabulario según qué esté subiendo.
    /// </para>
    /// </summary>
    public static readonly string[] TiposPermitidos = ["image/webp", "image/png", "image/jpeg"];

    /// <summary>Los dos usos posibles de una ranura. Coincide con <c>CK_MediosWeb_Uso</c>.</summary>
    public static readonly string[] UsosPermitidos = ["fondo", "logotipo"];

    /// <summary>Lo que devuelve el reconocedor: el tipo REAL y las dimensiones que declara la cabecera.</summary>
    public readonly record struct Formato(string Mime, int Width, int Height);

    /// <summary>
    /// SHA-256 en hexadecimal minúsculo.
    /// <para>
    /// Es a la vez el ETag, el <c>?v</c> de la URL pública y la prueba de que el archivo no
    /// cambió. SHA1 y MD5 <b>no compilan</b> en este repositorio: CA5350 y CA5351 están en
    /// <c>WarningsAsErrors</c> (<c>Directory.Build.props:126</c>).
    /// </para>
    /// </summary>
    public static string Huella(ReadOnlySpan<byte> datos)
        => Convert.ToHexStringLower(SHA256.HashData(datos));

    /// <summary>
    /// Decide si estos bytes son una imagen de un formato admitido y, de paso, cuánto mide.
    /// <para>
    /// Devuelve <c>null</c> si no lo son. <b>No decodifica ni un píxel:</b> lee la cabecera, que
    /// es a la vez la comprobación de coherencia y la fuente de las dimensiones. El API nunca
    /// decodifica la imagen, así que una bomba de descompresión no tiene dónde explotar.
    /// </para>
    /// <para>
    /// <b>El ContentType que declara el cliente no entra aquí</b>, y ese es el punto. Lo escribe
    /// quien sube. El tipo que se guarda y se sirve es el que decide esta función.
    /// </para>
    /// </summary>
    /// <summary>
    /// Tope de un documento: 80 MiB.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NO ES EL TOPE DE UNA IMAGEN Y NO DEBE SERLO. Los dos MiB de <see cref="MaxBytes"/> se
    /// razonaron sobre lo que pesa un hero en WebP; una guía de iniciación al fagot escaneada pesa
    /// diez veces eso y no por estar mal exportada.
    /// </para>
    /// <para>
    /// <b>EMPEZO EN 20 MiB Y SE QUEDO CORTO A LA PRIMERA.</b> Un método de teoría del jazz de 208
    /// páginas pesa 37 MB —medido—, y ese es exactamente el tipo de publicación que este catálogo
    /// existe para registrar. Un tope que rechaza el caso normal del acervo no protege: estorba.
    /// Ochenta cubre un libro entero escaneado y sigue dejando fuera un vídeo.
    /// </para>
    /// <para>
    /// EL DOCUMENTO SE LEE EN MEMORIA, así que este número también es cuánta memoria puede pedir una
    /// petición. Es asumible porque la carga la hace una persona desde la consola institucional, de
    /// una en una; si algún día se importa por lotes, habrá que pasar a leerlo desde disco.
    /// </para>
    /// </remarks>
    public const int MaxBytesDocumento = 80 * 1024 * 1024;

    /// <summary>
    /// ¿Es un PDF de verdad?
    /// </summary>
    /// <remarks>
    /// <para>
    /// SE COMPRUEBAN LAS DOS PUNTAS, no solo la cabecera. `%PDF-` se pega delante de cualquier cosa
    /// en un segundo, que es exactamente el ataque del que protege reconocer por firma. Un PDF
    /// válido termina además en `%%EOF` —la norma permite hasta 1024 bytes de basura después, así
    /// que se busca en la cola— y declara una versión en la propia cabecera.
    /// </para>
    /// <para>
    /// NO SE PARSEA EL DOCUMENTO. Comprobar la estructura interna exigiría un lector de PDF en el
    /// servidor, que es superficie de ataque nueva para una garantía que no se necesita: el fichero
    /// se guarda y se sirve, no se interpreta. Lo que esta comprobación impide es que un ejecutable
    /// renombrado entre al banco, no que un PDF esté mal formado.
    /// </para>
    /// </remarks>
    public static bool EsPdf(ReadOnlySpan<byte> datos)
    {
        ReadOnlySpan<byte> cabecera = "%PDF-"u8;
        if (datos.Length < 32 || !datos[..5].SequenceEqual(cabecera))
        {
            return false;
        }

        // La versión: un dígito, un punto y un dígito. `%PDF-1.7`, `%PDF-2.0`.
        if (!char.IsAsciiDigit((char)datos[5]) || datos[6] != (byte)'.' || !char.IsAsciiDigit((char)datos[7]))
        {
            return false;
        }

        ReadOnlySpan<byte> fin = "%%EOF"u8;
        var cola = datos[^Math.Min(datos.Length, 2048)..];
        for (var i = 0; i + fin.Length <= cola.Length; i++)
        {
            if (cola.Slice(i, fin.Length).SequenceEqual(fin))
            {
                return true;
            }
        }

        return false;
    }

    public static Formato? Reconocer(ReadOnlySpan<byte> datos)
    {
        var formato = ReconocerPng(datos) ?? ReconocerJpeg(datos) ?? ReconocerWebp(datos);
        if (formato is null)
        {
            return null;
        }

        var (_, ancho, alto) = formato.Value;
        if (ancho < 1 || alto < 1 || ancho > MaxDimension || alto > MaxDimension)
        {
            return null;
        }

        return (long)ancho * alto > MaxPixels ? null : formato;
    }

    /// <summary>
    /// PNG.
    /// <para>
    /// Lo que separa un PNG de un ejecutable con la firma pegada delante <b>no es la firma</b>:
    /// se copia en un segundo. Es el <b>CRC32 del chunk IHDR</b>. Una firma se copia; un CRC hay
    /// que acertarlo, y el de IHDR cubre justo los trece bytes que declaran las dimensiones.
    /// </para>
    /// </summary>
    private static Formato? ReconocerPng(ReadOnlySpan<byte> datos)
    {
        ReadOnlySpan<byte> firma = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (datos.Length < 33 || !datos[..8].SequenceEqual(firma))
        {
            return null;
        }

        // El primer chunk de todo PNG es IHDR y mide exactamente 13 bytes de datos.
        if (BinaryPrimitives.ReadUInt32BigEndian(datos[8..12]) != 13)
        {
            return null;
        }

        if (datos[12] != (byte)'I' || datos[13] != (byte)'H' || datos[14] != (byte)'D' || datos[15] != (byte)'R')
        {
            return null;
        }

        var ancho = BinaryPrimitives.ReadUInt32BigEndian(datos[16..20]);
        var alto = BinaryPrimitives.ReadUInt32BigEndian(datos[20..24]);
        if (ancho == 0 || alto == 0 || ancho > int.MaxValue || alto > int.MaxValue)
        {
            return null;
        }

        // Profundidad de bits y tipo de color: las combinaciones legales de la norma. Una
        // combinación imposible es una cabecera fabricada a mano.
        var profundidad = datos[24];
        var tipoDeColor = datos[25];
        var combinacionLegal = tipoDeColor switch
        {
            0 => profundidad is 1 or 2 or 4 or 8 or 16,
            3 => profundidad is 1 or 2 or 4 or 8,
            2 or 4 or 6 => profundidad is 8 or 16,
            _ => false,
        };
        if (!combinacionLegal)
        {
            return null;
        }

        // Compresión 0, filtro 0 y entrelazado 0 o 1 son los únicos valores que la norma define.
        if (datos[26] != 0 || datos[27] != 0 || datos[28] > 1)
        {
            return null;
        }

        // EL CRC. Cubre el nombre del chunk y sus datos, es decir los bytes 12..29.
        var declarado = BinaryPrimitives.ReadUInt32BigEndian(datos[29..33]);
        return Crc32(datos[12..29]) != declarado
            ? null
            : new Formato("image/png", (int)ancho, (int)alto);
    }

    /// <summary>
    /// JPEG.
    /// <para>
    /// No hay una cabecera fija donde leer las dimensiones: hay que <b>recorrer la cadena de
    /// marcadores</b> hasta el SOF, que es donde están. Ese recorrido es a la vez la comprobación
    /// de estructura: un archivo cuyos marcadores no encadenan se sale del recorrido y se
    /// rechaza. Al final se exige el <c>FF D9</c> de cierre, que es lo que distingue un JPEG
    /// completo de uno truncado o de basura con una cabecera delante.
    /// </para>
    /// </summary>
    private static Formato? ReconocerJpeg(ReadOnlySpan<byte> datos)
    {
        if (datos.Length < 4 || datos[0] != 0xFF || datos[1] != 0xD8)
        {
            return null;
        }

        if (datos[^2] != 0xFF || datos[^1] != 0xD9)
        {
            return null;
        }

        var i = 2;
        while (i + 3 < datos.Length)
        {
            if (datos[i] != 0xFF)
            {
                return null;
            }

            // Relleno: entre segmentos puede haber cualquier número de 0xFF seguidos.
            var marcador = datos[i + 1];
            if (marcador == 0xFF)
            {
                i++;
                continue;
            }

            // Marcadores sin carga útil: no llevan longitud, así que no se les puede sumar.
            if (marcador is 0x01 or >= 0xD0 and <= 0xD9)
            {
                i += 2;
                continue;
            }

            var longitud = BinaryPrimitives.ReadUInt16BigEndian(datos[(i + 2)..(i + 4)]);
            if (longitud < 2 || i + 2 + longitud > datos.Length)
            {
                return null;
            }

            // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15. C4 es DHT, C8 es JPG y CC es
            // DAC: NO son SOF, y confundirlos leería las dimensiones de otro sitio.
            var esSof = marcador is >= 0xC0 and <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC);
            if (esSof)
            {
                if (longitud < 7)
                {
                    return null;
                }

                var alto = BinaryPrimitives.ReadUInt16BigEndian(datos[(i + 5)..(i + 7)]);
                var ancho = BinaryPrimitives.ReadUInt16BigEndian(datos[(i + 7)..(i + 9)]);
                return ancho == 0 || alto == 0 ? null : new Formato("image/jpeg", ancho, alto);
            }

            // SOS: a partir de aquí vienen los datos comprimidos, no más marcadores con longitud.
            // Si se llegó hasta aquí sin encontrar un SOF, el archivo no declara dimensiones.
            if (marcador == 0xDA)
            {
                return null;
            }

            i += 2 + longitud;
        }

        return null;
    }

    /// <summary>
    /// WebP.
    /// <para>
    /// La comprobación que carga peso es la <b>longitud RIFF</b>: el entero de la posición 4 debe
    /// ser exactamente <c>longitud - 8</c>. Un archivo con «RIFF» y «WEBP» pegados delante casi
    /// nunca la acierta, porque depende del tamaño total.
    /// </para>
    /// </summary>
    private static Formato? ReconocerWebp(ReadOnlySpan<byte> datos)
    {
        if (datos.Length < 30)
        {
            return null;
        }

        if (datos[0] != (byte)'R' || datos[1] != (byte)'I' || datos[2] != (byte)'F' || datos[3] != (byte)'F')
        {
            return null;
        }

        if (datos[8] != (byte)'W' || datos[9] != (byte)'E' || datos[10] != (byte)'B' || datos[11] != (byte)'P')
        {
            return null;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(datos[4..8]) != (uint)(datos.Length - 8))
        {
            return null;
        }

        var chunk = datos[12..16];

        // VP8 con espacio: lossy. Las dimensiones están tras el código de arranque 9D 01 2A.
        if (chunk[0] == (byte)'V' && chunk[1] == (byte)'P' && chunk[2] == (byte)'8' && chunk[3] == (byte)' ')
        {
            if (datos.Length < 30 || datos[23] != 0x9D || datos[24] != 0x01 || datos[25] != 0x2A)
            {
                return null;
            }

            var ancho = BinaryPrimitives.ReadUInt16LittleEndian(datos[26..28]) & 0x3FFF;
            var alto = BinaryPrimitives.ReadUInt16LittleEndian(datos[28..30]) & 0x3FFF;
            return ancho == 0 || alto == 0 ? null : new Formato("image/webp", ancho, alto);
        }

        // VP8L: lossless. Catorce bits por lado, menos uno, empaquetados tras la firma 0x2F.
        if (chunk[0] == (byte)'V' && chunk[1] == (byte)'P' && chunk[2] == (byte)'8' && chunk[3] == (byte)'L')
        {
            if (datos.Length < 25 || datos[20] != 0x2F)
            {
                return null;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(datos[21..25]);
            var ancho = (int)(bits & 0x3FFF) + 1;
            var alto = (int)((bits >> 14) & 0x3FFF) + 1;
            return new Formato("image/webp", ancho, alto);
        }

        // VP8X: extendido. Lleva las dimensiones menos uno en veinticuatro bits cada una.
        if (chunk[0] == (byte)'V' && chunk[1] == (byte)'P' && chunk[2] == (byte)'8' && chunk[3] == (byte)'X')
        {
            if (datos.Length < 30)
            {
                return null;
            }

            var ancho = (datos[24] | (datos[25] << 8) | (datos[26] << 16)) + 1;
            var alto = (datos[27] | (datos[28] << 8) | (datos[29] << 16)) + 1;
            return new Formato("image/webp", ancho, alto);
        }

        return null;
    }

    /// <summary>
    /// CRC-32 (IEEE 802.3), el que usa PNG.
    /// <para>
    /// Escrito a mano y no traído de un paquete a propósito: <c>PNMC.Api.csproj:17-20</c> tiene
    /// <c>NuGetAudit</c> en modo <c>all</c> con NU1903 y NU1904 como error, así que una
    /// dependencia nueva es superficie de auditoría permanente por lo que aquí son treinta líneas.
    /// </para>
    /// </summary>
    private static uint Crc32(ReadOnlySpan<byte> datos)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in datos)
        {
            crc = TablaCrc32[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] TablaCrc32 = ConstruirTablaCrc32();

    private static uint[] ConstruirTablaCrc32()
    {
        var tabla = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            tabla[n] = c;
        }

        return tabla;
    }
}
