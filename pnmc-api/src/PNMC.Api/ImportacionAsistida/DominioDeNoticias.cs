using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>Una noticia ya normalizada, tal como se guarda en la fila del lote.</summary>
public sealed record DatosDeNoticia(
    string Titulo,
    string Slug,
    string Resumen,
    string? Cuerpo,
    string? AutoriaNombre,
    DateOnly? FechaPublicacion);

/// <summary>
/// Noticias: el tercer dominio, y el primero que no costó un núcleo.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE DEMUESTRA.</b> El Bloque 5b dejó dicho que sumar un dominio cuesta «un fichero de
/// reglas y dos líneas». Esto lo comprueba: no hay aquí nada de transacción, ni de reserva contra
/// confirmaciones simultáneas, ni de idempotencia, ni de bitácora, ni de retención. Todo eso es del
/// núcleo. Aquí solo están las reglas de una noticia.
/// </para>
/// <para>
/// <b>UNA NOTICIA IMPORTADA NACE EN BORRADOR, NUNCA PUBLICADA.</b> Es la segunda regla de la
/// capacidad —«no publicar automáticamente información obtenida por Importación Asistida»— y aquí
/// pesa más que en ningún otro dominio: una noticia publicada es texto que aparece en el portal con
/// la voz del Programa. De un archivo de doscientas filas saldrían doscientas publicaciones que
/// nadie leyó.
/// </para>
/// <para>
/// <b>LA COINCIDENCIA SE MIDE POR LA DIRECCION, NO POR EL TITULO.</b> El slug es lo único único en
/// la tabla, y dos noticias pueden llamarse igual legítimamente —«Convocatoria 2026» de dos
/// programas distintos—. Comparar por título rechazaría filas buenas; comparar por dirección
/// rechaza exactamente lo que la base rechazaría.
/// </para>
/// <para>
/// <b>NO IMPORTA ETIQUETAS NI CATEGORIA.</b> Las dos viven en tablas propias con sus reglas
/// —`EtiquetasNoticia`, `Categorias`— y adivinarlas desde una columna de texto crearía términos
/// sueltos que nadie administra. La noticia entra sin ellas y se clasifican al revisarla, que es
/// cuando alguien la está mirando de todas formas.
/// </para>
/// </remarks>
public sealed class DominioDeNoticias : IDominioDeImportacion
{
    private static readonly CampoDeImportacionDto[] CamposDeclarados =
    [
        new("titulo", "Título", true),
        new("resumen", "Resumen", true),
        new("cuerpo", "Cuerpo", false),
        new("autoria", "Autoría", false),
        new("fechaPublicacion", "Fecha de publicación", false),
        new("slug", "Dirección (slug)", false),
    ];

    public CapacidadDeImportacion Capacidad { get; } = new(
        "noticias",
        "Noticias",
        "borrador",
        "Una noticia importada entra en borrador, nunca publicada: es texto que aparecería en el "
            + "portal con la voz del Programa, y de un archivo de doscientas filas saldrían "
            + "doscientas publicaciones que nadie leyó. Se publica cuando alguien la revise.",
        Modulos.Noticias,
        "Noticias",
        CamposDeclarados);

    public async Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas, string huellaArchivo, PnmcDbContext db, CancellationToken ct)
    {
        var existentes = await db.Noticias.AsNoTracking()
            .Select(item => new { item.Id, item.Slug })
            .ToListAsync(ct);

        var porSlug = existentes
            .GroupBy(item => item.Slug, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);

        // DENTRO DEL MISMO ARCHIVO TAMBIEN SE COMPRUEBA. Dos filas con la misma dirección son un
        // duplicado del propio archivo: escribir las dos daría un error de clave única a mitad del
        // lote, con la mitad escrita.
        var slugsVistos = new HashSet<string>(StringComparer.Ordinal);
        var planeadas = filas.OrderBy(item => item.NumeroFila)
            .Select(fila => Planear(fila, porSlug, slugsVistos))
            .ToList();

        return new PlanDeImportacion(NucleoDeImportacion.HuellaDelPlan(huellaArchivo, planeadas), planeadas);
    }

    public async Task<long> AplicarAsync(
        FilaImportacionRow fila, ContextoDeAplicacion contexto, PnmcDbContext db, CancellationToken ct)
    {
        var datos = Leer(fila.ContenidoNormalizadoJson, fila.Id);
        var noticia = new NoticiaRow
        {
            Slug = datos.Slug,
            Titulo = datos.Titulo,
            Resumen = datos.Resumen,
            Cuerpo = datos.Cuerpo,
            AutoriaNombre = datos.AutoriaNombre,
            FechaPublicacion = datos.FechaPublicacion,
            Estado = Capacidad.EstadoAlImportar,
            Version = 1,
            FechaCreacion = contexto.Ahora,
            FechaActualizacion = contexto.Ahora,
        };
        db.Noticias.Add(noticia);
        await db.SaveChangesAsync(ct);
        return noticia.Id;
    }

    public IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson)
    {
        var datos = Leer(contenidoNormalizadoJson);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["titulo"] = datos.Titulo,
            ["resumen"] = datos.Resumen,
            ["cuerpo"] = datos.Cuerpo,
            ["autoria"] = datos.AutoriaNombre,
            ["fechaPublicacion"] = datos.FechaPublicacion?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["slug"] = datos.Slug,
        };
    }

    public async Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas, PnmcDbContext db, CancellationToken ct)
    {
        var slugs = filas.Select(fila => Leer(fila.ContenidoNormalizadoJson, fila.Id).Slug).ToHashSet(StringComparer.Ordinal);
        if (slugs.Count == 0) return null;

        var apareció = await db.Noticias.AsNoTracking().AnyAsync(item => slugs.Contains(item.Slug), ct);

        return apareció
            ? "Apareció una noticia en la misma dirección después de la previsualización. No se escribió nada; vuelve a previsualizar."
            : null;
    }

    // ---------- Las reglas de una noticia ----------------------------------------------------

    private static FilaPlaneada Planear(
        FilaDeArchivoDto fila,
        Dictionary<string, long> porSlug,
        HashSet<string> slugsVistos)
    {
        var hallazgos = new Hallazgos();
        var titulo = NormalizacionDeFilas.Campo(fila, "titulo");
        var resumen = NormalizacionDeFilas.Campo(fila, "resumen");
        var cuerpo = NormalizacionDeFilas.Campo(fila, "cuerpo");
        var autoria = NormalizacionDeFilas.Campo(fila, "autoria");

        hallazgos.Obligatorio(titulo, "titulo", "titulo_obligatorio", "La noticia necesita un título.");
        hallazgos.Obligatorio(resumen, "resumen", "resumen_obligatorio", "La noticia necesita un resumen: es lo que se lee en la tarjeta del portal.");
        hallazgos.Maximo(titulo, 300, "titulo");
        hallazgos.Maximo(resumen, 600, "resumen");
        hallazgos.Maximo(autoria, 200, "autoria");

        // LA DIRECCION SE CALCULA CON LA MISMA REGLA QUE USA EL FORMULARIO. Escribir aquí otra
        // normalización daría direcciones distintas para el mismo título según por dónde entrara.
        var slugPedido = NormalizacionDeFilas.Campo(fila, "slug");
        var slug = ReglasDeNoticias.SlugDesde(slugPedido is { Length: > 0 } ? slugPedido : titulo ?? string.Empty);
        if (slug.Length == 0 && !string.IsNullOrWhiteSpace(titulo))
        {
            hallazgos.Error("titulo_sin_direccion", "titulo", "Del título no sale una dirección utilizable para el portal.");
        }

        var fecha = LeerFecha(NormalizacionDeFilas.Campo(fila, "fechaPublicacion"), hallazgos);

        long? coincidente = null;
        if (slug.Length > 0)
        {
            if (porSlug.TryGetValue(slug, out var existente))
            {
                coincidente = existente;
                hallazgos.Error("noticia_existente", "slug",
                    $"Ya hay una noticia en la dirección «{slug}». No se importa para no pisarla.");
            }
            else if (!slugsVistos.Add(slug))
            {
                hallazgos.Error("duplicada_en_el_archivo", "slug",
                    $"El archivo trae dos filas para la dirección «{slug}».");
            }
        }

        var datos = new DatosDeNoticia(titulo ?? string.Empty, slug, resumen ?? string.Empty, cuerpo, autoria, fecha);
        var puede = hallazgos.SinErrores;

        return new FilaPlaneada(
            fila.NumeroFila,
            coincidente is null ? (puede ? "crear" : "rechazar") : "coincidencia_existente",
            puede,
            coincidente,
            JsonSerializer.Serialize(datos, NucleoDeImportacion.Json),
            hallazgos.Lista);
    }

    /// <summary>
    /// Lee la fecha de publicación, que es opcional.
    /// </summary>
    /// <remarks>
    /// UNA FECHA ILEGIBLE ES UN ERROR Y NO UN HUECO. Dejarla en nulo silenciosamente publicaría la
    /// noticia sin fecha y nadie sabría que el archivo traía una: el hallazgo es lo que permite
    /// corregir la hoja antes de confirmar.
    /// </remarks>
    private static DateOnly? LeerFecha(string? valor, Hallazgos hallazgos)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (DateOnly.TryParse(valor, System.Globalization.CultureInfo.InvariantCulture, out var fecha)) return fecha;
        if (DateOnly.TryParse(valor, new System.Globalization.CultureInfo("es-CO"), out fecha)) return fecha;

        hallazgos.Error("fecha_invalida", "fechaPublicacion",
            $"«{valor}» no se entiende como fecha. Usa el formato AAAA-MM-DD.");
        return null;
    }

    private static DatosDeNoticia Leer(string json, long? filaId = null) =>
        JsonSerializer.Deserialize<DatosDeNoticia>(json, NucleoDeImportacion.Json)
            ?? throw new InvalidOperationException(
                $"La fila {filaId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"} no tiene contenido legible.");
}
