using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>Una publicación editorial ya normalizada, tal como se guarda en la fila del lote.</summary>
public sealed record DatosDePublicacion(
    string Codigo,
    string Titulo,
    string? Subtitulo,
    string? SerieOColeccion,
    string? Resumen,
    string? TipoPublicacion,
    string? Idioma,
    int? AnioInicio,
    int? AnioFin,
    string? Paginas,
    string? Duracion);

/// <summary>
/// Catálogo Editorial: el dominio con dos estados que no significan lo mismo.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNA PUBLICACION TIENE DOS ESTADOS INDEPENDIENTES</b> y una revisión anterior los separó a propósito:
/// <c>EstadoCatalogacion</c> dice si la ficha está bien hecha —calidad— y <c>EstadoPublicacion</c>
/// dice si se ve —visibilidad—. Una ficha puede estar impecable y no publicada, o publicada y
/// pendiente de revisar. La importación los pone los dos en su valor más prudente:
/// <c>pendiente_revision</c> y <c>borrador</c>.
/// </para>
/// <para>
/// <b>LA CAPACIDAD DECLARA EL DE VISIBILIDAD</b>, que es el que la segunda regla vigila: lo que no
/// puede pasar es que un archivo publique. El de catalogación no decide visibilidad y por eso no es
/// el que se declara.
/// </para>
/// <para>
/// <b>EL CODIGO NO SE INVENTA: SE EXIGE.</b> A diferencia de una noticia o un evento, cuya dirección
/// sale del título, el código de una publicación es su signatura en el catálogo y lo asigna quien
/// cataloga. Derivarlo del título produciría signaturas falsas que después habría que corregir a
/// mano una por una.
/// </para>
/// <para>
/// <b>LOS DESCRIPTORES SIGUEN SIENDO TEXTO LIBRE</b> —tipo de publicación, idioma— porque el corte
/// 05A dejó sin aprobar los vocabularios editoriales y eso no ha cambiado. Cerrarlos aquí
/// convertiría en regla unos valores que nadie ha decidido.
/// </para>
/// </remarks>
public sealed class DominioDeCatalogoEditorial : IDominioDeImportacion
{
    private static readonly CampoDeImportacionDto[] CamposDeclarados =
    [
        new("codigo", "Código de catálogo", true),
        new("titulo", "Título", true),
        new("subtitulo", "Subtítulo", false),
        new("serie", "Serie o colección", false),
        new("resumen", "Resumen", false),
        new("tipoPublicacion", "Tipo de publicación", false),
        new("idioma", "Idioma", false),
        new("anioInicio", "Año", false),
        new("anioFin", "Año de cierre", false),
        new("paginas", "Páginas", false),
        new("duracion", "Duración", false),
    ];

    public CapacidadDeImportacion Capacidad { get; } = new(
        "catalogo-editorial",
        "Catálogo Editorial",
        "borrador",
        "Una publicación importada entra en borrador y pendiente de revisión: la ficha llega sin "
            + "que nadie haya comprobado sus datos bibliográficos ni sus derechos, y publicarla "
            + "sería ponerla en el portal sin saber si se puede. Se publica cuando alguien la revise.",
        Modulos.CatalogoEditorial,
        "PublicacionesEditoriales",
        CamposDeclarados);

    public async Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas, string huellaArchivo, PnmcDbContext db, CancellationToken ct)
    {
        var existentes = await db.PublicacionesEditoriales.AsNoTracking()
            .Select(item => new { item.Id, item.Codigo })
            .ToListAsync(ct);

        var porCodigo = existentes
            .GroupBy(item => NormalizacionDeFilas.Comparable(item.Codigo), StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);

        var codigosVistos = new HashSet<string>(StringComparer.Ordinal);
        var planeadas = filas.OrderBy(item => item.NumeroFila)
            .Select(fila => Planear(fila, porCodigo, codigosVistos))
            .ToList();

        return new PlanDeImportacion(NucleoDeImportacion.HuellaDelPlan(huellaArchivo, planeadas), planeadas);
    }

    public async Task<long> AplicarAsync(
        FilaImportacionRow fila, ContextoDeAplicacion contexto, PnmcDbContext db, CancellationToken ct)
    {
        var datos = Leer(fila.ContenidoNormalizadoJson, fila.Id);
        var publicacion = new PublicacionEditorialRow
        {
            Codigo = datos.Codigo,
            Titulo = datos.Titulo,
            Subtitulo = datos.Subtitulo,
            SerieOColeccion = datos.SerieOColeccion,
            Resumen = datos.Resumen,
            TipoPublicacion = datos.TipoPublicacion,
            Idioma = datos.Idioma,
            AnioInicio = datos.AnioInicio,
            AnioFin = datos.AnioFin,
            Paginas = datos.Paginas,
            Duracion = datos.Duracion,
            // LOS DOS ESTADOS, CADA UNO EN SU VALOR MAS PRUDENTE. El de visibilidad es el que la
            // capacidad declara y el que la regla vigila; el de catalogación dice que nadie ha
            // comprobado todavía esta ficha, que es exactamente el caso.
            EstadoPublicacion = Capacidad.EstadoAlImportar,
            EstadoCatalogacion = "pendiente_revision",
            // LOS DERECHOS NACEN PENDIENTES Y SIN PERMISO. Importar una ficha no dice nada sobre
            // quién puede publicar su archivo, y dar por bueno el permiso desde una hoja sería
            // afirmar una autorización que nadie firmó.
            DerechosEstado = "pendiente",
            DerechosPermitePublicarFicha = false,
            DerechosPermitePublicarArchivo = false,
            Version = 1,
            FechaCreacion = contexto.Ahora,
            FechaActualizacion = contexto.Ahora,
        };
        db.PublicacionesEditoriales.Add(publicacion);
        await db.SaveChangesAsync(ct);
        return publicacion.Id;
    }

    public IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson)
    {
        var datos = Leer(contenidoNormalizadoJson);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["codigo"] = datos.Codigo,
            ["titulo"] = datos.Titulo,
            ["subtitulo"] = datos.Subtitulo,
            ["serie"] = datos.SerieOColeccion,
            ["resumen"] = datos.Resumen,
            ["tipoPublicacion"] = datos.TipoPublicacion,
            ["idioma"] = datos.Idioma,
            ["anioInicio"] = datos.AnioInicio?.ToString(CultureInfo.InvariantCulture),
            ["anioFin"] = datos.AnioFin?.ToString(CultureInfo.InvariantCulture),
            ["paginas"] = datos.Paginas,
            ["duracion"] = datos.Duracion,
        };
    }

    public async Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas, PnmcDbContext db, CancellationToken ct)
    {
        var codigos = filas
            .Select(fila => Leer(fila.ContenidoNormalizadoJson, fila.Id).Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codigos.Count == 0) return null;

        var vigentes = await db.PublicacionesEditoriales.AsNoTracking()
            .Select(item => item.Codigo)
            .ToListAsync(ct);

        var apareció = vigentes.Any(codigo => codigos.Contains(codigo));

        return apareció
            ? "Apareció una publicación con el mismo código después de la previsualización. No se escribió nada; vuelve a previsualizar."
            : null;
    }

    // ---------- Las reglas de una publicación ------------------------------------------------

    private static FilaPlaneada Planear(
        FilaDeArchivoDto fila,
        Dictionary<string, long> porCodigo,
        HashSet<string> codigosVistos)
    {
        var hallazgos = new Hallazgos();
        var codigo = NormalizacionDeFilas.Campo(fila, "codigo");
        var titulo = NormalizacionDeFilas.Campo(fila, "titulo");
        var subtitulo = NormalizacionDeFilas.Campo(fila, "subtitulo");
        var serie = NormalizacionDeFilas.Campo(fila, "serie");
        var resumen = NormalizacionDeFilas.Campo(fila, "resumen");
        var tipo = NormalizacionDeFilas.Campo(fila, "tipoPublicacion");
        var idioma = NormalizacionDeFilas.Campo(fila, "idioma");
        var paginas = NormalizacionDeFilas.Campo(fila, "paginas");
        var duracion = NormalizacionDeFilas.Campo(fila, "duracion");

        hallazgos.Obligatorio(codigo, "codigo", "codigo_obligatorio",
            "La publicación necesita su código de catálogo. No se deriva del título: es la signatura que asigna quien cataloga.");
        hallazgos.Obligatorio(titulo, "titulo", "titulo_obligatorio", "La publicación necesita un título.");
        hallazgos.Maximo(codigo, 80, "codigo");
        hallazgos.Maximo(titulo, 500, "titulo");
        hallazgos.Maximo(subtitulo, 500, "subtitulo");
        hallazgos.Maximo(serie, 300, "serie");
        hallazgos.Maximo(resumen, 4000, "resumen");
        hallazgos.Maximo(tipo, 120, "tipoPublicacion");
        hallazgos.Maximo(idioma, 80, "idioma");
        hallazgos.Maximo(paginas, 80, "paginas");
        hallazgos.Maximo(duracion, 80, "duracion");

        var anioInicio = LeerAnio(NormalizacionDeFilas.Campo(fila, "anioInicio"), "anioInicio", hallazgos);
        var anioFin = LeerAnio(NormalizacionDeFilas.Campo(fila, "anioFin"), "anioFin", hallazgos);
        if (anioInicio is not null && anioFin is not null && anioFin < anioInicio)
        {
            hallazgos.Error("anios_invertidos", "anioFin", "El año de cierre es anterior al de inicio.");
        }

        long? coincidente = null;
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var clave = NormalizacionDeFilas.Comparable(codigo);
            if (porCodigo.TryGetValue(clave, out var existente))
            {
                coincidente = existente;
                hallazgos.Error("publicacion_existente", "codigo",
                    $"Ya hay una publicación con el código «{codigo}». No se importa para no pisarla.");
            }
            else if (!codigosVistos.Add(clave))
            {
                hallazgos.Error("duplicada_en_el_archivo", "codigo",
                    $"El archivo trae dos filas con el código «{codigo}».");
            }
        }

        var datos = new DatosDePublicacion(
            codigo ?? string.Empty, titulo ?? string.Empty, subtitulo, serie, resumen,
            tipo, idioma, anioInicio, anioFin, paginas, duracion);

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
    /// Lee un año, que es opcional.
    /// </summary>
    /// <remarks>
    /// EL RANGO ES AMPLIO A PROPOSITO. Este catálogo guarda fondos históricos: acotarlo al siglo XX
    /// rechazaría material legítimo. Lo que sí se rechaza es lo que no es un año.
    /// </remarks>
    private static int? LeerAnio(string? valor, string campo, Hallazgos hallazgos)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var anio)
            && anio is >= 1000 and <= 2999)
        {
            return anio;
        }

        hallazgos.Error("anio_invalido", campo, $"«{valor}» no se entiende como año.");
        return null;
    }

    private static DatosDePublicacion Leer(string json, long? filaId = null) =>
        JsonSerializer.Deserialize<DatosDePublicacion>(json, NucleoDeImportacion.Json)
            ?? throw new InvalidOperationException(
                $"La fila {filaId?.ToString(CultureInfo.InvariantCulture) ?? "?"} no tiene contenido legible.");
}
