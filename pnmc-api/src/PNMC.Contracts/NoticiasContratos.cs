namespace PNMC.Contracts;

/// <summary>
/// Los contratos de Noticias.
/// </summary>
/// <remarks>
/// <para>
/// <b>UN SOLO DTO PARA LAS DOS SUPERFICIES.</b> La consola y el portal leen la misma forma. Lo que
/// cambia no es el DTO sino QUE FILAS se devuelven: el portal solo ve las publicadas con fecha
/// llegada, y esa decisión vive en el servidor, en un único sitio.
/// </para>
/// </remarks>
public sealed record NoticiaDto(
    long Id,
    string Slug,
    string Titulo,
    string Resumen,
    string? Cuerpo,
    DateOnly? FechaPublicacion,
    string? ImagenRuta,
    string? ImagenAlternativa,
    string? AutoriaNombre,
    /// <summary>El archivo del banco vinculado con rol «imagen_principal», si lo hay.</summary>
    int? ImagenArchivoId,
    string? ImagenUrl,
    string? ImagenAlt,
    /// <summary>El identificador de la categoría: es el dato que se guarda y con el que se edita.</summary>
    int? CategoriaId,
    /// <summary>Su nombre, resuelto al servir. Es lo que el portal enseña y por lo que filtra.</summary>
    string? Categoria,
    string Estado,
    /// <summary>
    /// Cómo está la noticia HOY, contando su fecha de aparición.
    /// </summary>
    /// <remarks>
    /// COINCIDE CON <see cref="Estado"/> SALVO EN UN CASO: publicada con fecha futura, que es
    /// «programada». Es lo que las pantallas deben enseñar; <c>Estado</c> sigue siendo el dato que
    /// se guarda y con el que se decide qué acciones caben.
    /// </remarks>
    string EstadoEfectivo,
    int Version,
    IReadOnlyList<string> Etiquetas,
    /// <summary>Las prácticas musicales con las que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    /// <summary>Los territorios sonoros con los que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    /// <summary>
    /// De dónde vino el registro: contexto, entidad que lo aportó y cuenta que lo creó.
    /// </summary>
    /// <remarks>
    /// ES ANULABLE porque los registros anteriores a la tabla de procedencia pueden no tenerla, y
    /// decir «no consta» es más honesto que inventar una.
    /// </remarks>
    ProcedenciaDeRegistroDto? Procedencia,
    /// <summary>
    /// Las iniciativas del Programa a las que pertenece: Celebra la Música y las que vengan.
    /// </summary>
    /// <remarks>
    /// NO ES SU CATEGORIA. La categoría dice de qué trata; esto dice a qué iniciativa pertenece, y
    /// son dos preguntas distintas: un evento puede ser de «Encuentros» y además de Celebra.
    /// </remarks>
    IReadOnlyList<ElementoDeClasificacionDto> ProyectosTransversales,
    DateTime FechaActualizacion);

public sealed record PaginaNoticiasDto(
    IReadOnlyList<NoticiaDto> Items,
    int Pagina,
    int Tamano,
    int Total,
    int TotalPaginas);

/// <summary>
/// Lo que la consola envía al crear o guardar.
/// </summary>
/// <remarks>
/// <c>Version</c> es obligatoria al guardar y se ignora al crear. Citar la versión que se estaba
/// mirando es lo que impide que dos personas con la misma noticia abierta se pisen sin enterarse.
/// </remarks>
public sealed class GuardarNoticiaSolicitud
{
    public string? Slug { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Resumen { get; set; } = string.Empty;
    public string? Cuerpo { get; set; }
    public DateOnly? FechaPublicacion { get; set; }
    public string? ImagenRuta { get; set; }
    public string? ImagenAlternativa { get; set; }
    public string? AutoriaNombre { get; set; }

    /// <summary>
    /// El archivo del banco que hace de imagen principal.
    /// </summary>
    /// <remarks>
    /// AUSENTE Y VACIO NO SIGNIFICAN LO MISMO, y la diferencia es la que evita borrar la imagen de
    /// todas las noticias con el primer cliente que no conozca el campo. La clave ausente no toca
    /// la imagen que hubiera; <see cref="RetirarImagen"/> la retira.
    /// </remarks>
    public int? ImagenArchivoId { get; set; }

    /// <summary>Marca explícita de «quítale la imagen». Un nulo no basta: es ambiguo.</summary>
    public bool RetirarImagen { get; set; }

    public int? CategoriaId { get; set; }
    public IReadOnlyList<string>? Etiquetas { get; set; }

    /// <summary>
    /// Las prácticas musicales y los territorios sonoros con los que se clasifica la noticia.
    /// </summary>
    /// <remarks>
    /// AUSENTE NO ES VACIO: <c>null</c> deja la clasificación como estaba y una lista vacía la
    /// retira. Es la misma regla que ya aplican las etiquetas unas líneas más arriba.
    /// </remarks>
    public IReadOnlyList<int>? PracticasMusicalesIds { get; set; }
    public IReadOnlyList<int>? TerritoriosSonorosIds { get; set; }

    /// <summary>
    /// Las iniciativas del Programa con las que se enlaza.
    /// </summary>
    /// <remarks>
    /// AUSENTE NO ES VACIO, igual que la clasificación: <c>null</c> deja los enlaces como estaban y
    /// una lista vacía los retira.
    /// </remarks>
    public IReadOnlyList<int>? ProyectosTransversalesIds { get; set; }

    public int Version { get; set; }
}

/// <summary>El cambio de estado va por su propia ruta: publicar no es «guardar con otro campo».</summary>
public sealed class CambiarEstadoNoticiaSolicitud
{
    public string Estado { get; set; } = string.Empty;
}

/// <summary>
/// Las reglas de Noticias, en un sitio comprobable sin base de datos.
/// </summary>
public static class ReglasDeNoticias
{
    public static readonly IReadOnlyList<string> Estados = ["borrador", "en_revision", "publicado", "archivado"];

    /// <summary>
    /// Qué le falta a una noticia para poder publicarse.
    /// </summary>
    /// <remarks>
    /// DEVUELVE TODO LO QUE FALTA, no lo primero. Quien redacta prefiere una lista a tres intentos
    /// seguidos; es la misma decisión que tomó el Catálogo Editorial con sus cuatro condiciones.
    /// </remarks>
    public static IReadOnlyList<string> LoQueFaltaParaPublicar(
        string? titulo, string? resumen, string? cuerpo, DateOnly? fechaPublicacion)
    {
        var faltas = new List<string>();
        if (string.IsNullOrWhiteSpace(titulo)) faltas.Add("no tiene título");
        if (string.IsNullOrWhiteSpace(resumen)) faltas.Add("no tiene resumen");
        if (string.IsNullOrWhiteSpace(cuerpo)) faltas.Add("no tiene cuerpo");
        if (fechaPublicacion is null) faltas.Add("no tiene fecha de publicación");
        return faltas;
    }

    /// <summary>
    /// Si una noticia es visible en el portal.
    /// </summary>
    /// <remarks>
    /// LA FECHA FUTURA NO SE PUBLICA TODAVIA, y es deliberado: fechar una noticia el lunes y que
    /// aparezca el viernes convierte la fecha en decoración. El portal la enseña cuando llega.
    /// </remarks>
    public static bool EsVisiblePublicamente(string estado, DateOnly? fechaPublicacion, DateOnly hoy) =>
        estado == "publicado" && fechaPublicacion is not null && fechaPublicacion.Value <= hoy;

    /// <summary>El estado que hay que ENSEÑAR hoy, que no siempre es el que está guardado.</summary>
    public const string Programada = "programada";

    /// <summary>
    /// Cómo está la noticia <b>hoy</b>, contando su fecha de aparición.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNA NOTICIA PUBLICADA CON FECHA FUTURA NO ESTA PUBLICADA: está programada.</b> Lo pidió el
    /// usuario. La lectura pública siempre lo hizo bien —no la enseña
    /// hasta su fecha, por <see cref="EsVisiblePublicamente"/>—, pero la consola la etiquetaba
    /// «Publicado», y quien la mirara daría por hecho que ya está en el sitio.
    /// </para>
    /// <para>
    /// <b>EL ESTADO GUARDADO NO SE TOCA.</b> «Publicado con fecha futura» es un dato correcto y
    /// deliberado: es justamente cómo se programa una publicación. Lo que cambia es cómo se nombra
    /// en pantalla, y cuando llega su fecha pasa a «Publicado» sin que nadie haga nada.
    /// </para>
    /// <para>
    /// <b>SE CALCULA EN EL SERVIDOR</b> y viaja en el contrato. Cada pantalla que lo dedujera por su
    /// cuenta sería una copia más que puede quedarse atrás, y además ninguna conoce la fecha del
    /// servidor: un navegador con el reloj adelantado enseñaría «Publicado» un día antes.
    /// </para>
    /// </remarks>
    public static string EstadoEfectivo(string estado, DateOnly? fechaPublicacion, DateOnly hoy) =>
        estado == "publicado" && fechaPublicacion is not null && fechaPublicacion.Value > hoy
            ? Programada
            : estado;

    /// <summary>
    /// La dirección pública a partir del título.
    /// </summary>
    /// <remarks>
    /// SE CALCULA UNA VEZ, AL CREAR, y después no cambia: un enlace compartido no puede dejar de
    /// funcionar porque alguien corrigiera una tilde del título.
    /// </remarks>
    public static string SlugDesde(string titulo)
    {
        var normalizado = (titulo ?? string.Empty).Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var letras = normalizado
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var bruto = new string(letras);
        while (bruto.Contains("--", StringComparison.Ordinal)) bruto = bruto.Replace("--", "-", StringComparison.Ordinal);
        var limpio = bruto.Trim('-');
        return limpio.Length > 160 ? limpio[..160].TrimEnd('-') : limpio;
    }
}
