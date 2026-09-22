namespace PNMC.Contracts;

/// <summary>
/// Una fila del archivo, tal como la consola la leyó: campos y valores, sin forma fija.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE UN DICCIONARIO Y NO TRECE PROPIEDADES.</b> Hasta esta
/// clase se llamaba <c>FilaFuenteImportacionFestivalDto</c> y declaraba una propiedad por cada campo
/// de un Festival. Con eso, importar cualquier otra cosa exigía otro contrato, otro endpoint y otra
/// pantalla: la importación no era una capacidad, era una pantalla de Festivales.
/// </para>
/// <para>
/// <b>LAS CLAVES NO SON LIBRES.</b> Son los campos que el dominio declara en su capacidad —los que
/// devuelve <c>GET /admin/importaciones</c>—, y el servidor descarta las que no reconoce. Un
/// diccionario abierto sería una forma elegante de aceptar basura.
/// </para>
/// </remarks>
public sealed class FilaDeArchivoDto
{
    public int NumeroFila { get; set; }

    /// <summary>Campo declarado por el dominio → valor leído del archivo.</summary>
    public Dictionary<string, string?> Valores { get; set; } = [];
}

/// <summary>Lo que la consola envía para previsualizar un archivo, sea del dominio que sea.</summary>
public sealed class PrevisualizarImportacionSolicitud
{
    public string? NombreArchivo { get; set; }
    public string? Formato { get; set; }
    public string? HuellaArchivo { get; set; }
    public int VersionContrato { get; set; }
    public IReadOnlyList<FilaDeArchivoDto>? Filas { get; set; }
}

/// <summary>Lo que la consola envía para aplicar lo que previsualizó.</summary>
public sealed class ConfirmarImportacionSolicitud
{
    public string? HuellaPlan { get; set; }
    public string? ClaveIdempotencia { get; set; }
    public IReadOnlyList<long>? IdsFilasExcluidas { get; set; }
}

/// <summary>Un campo que un dominio sabe leer de un archivo.</summary>
/// <param name="Nombre">La clave que viaja en <see cref="FilaDeArchivoDto.Valores"/>.</param>
/// <param name="Etiqueta">Cómo se llama en pantalla.</param>
/// <param name="Obligatorio">Si sin él la fila no se puede importar.</param>
public sealed record CampoDeImportacionDto(string Nombre, string Etiqueta, bool Obligatorio);

/// <summary>Qué sabe importar SIMUS, y en qué estado nace lo importado.</summary>
public sealed record DominioImportableDto(
    string Dominio,
    string Etiqueta,
    string EstadoAlImportar,
    string PorQueEseEstado,
    IReadOnlyList<CampoDeImportacionDto> Campos);

/// <summary>Los dominios que hoy tienen circuito de importación.</summary>
public sealed record DominiosImportablesDto(IReadOnlyList<DominioImportableDto> Dominios);

public sealed record HallazgoDeImportacionDto(
    string Severidad,
    string Codigo,
    string? Campo,
    string Mensaje);

/// <summary>Una fila ya planeada, con lo que se va a hacer con ella y por qué.</summary>
/// <param name="Datos">
/// El dato normalizado, proyectado a campo → valor para poder enseñarlo.
/// </param>
/// <remarks>
/// <c>Datos</c> LO PROYECTA EL DOMINIO desde su propia forma guardada. El núcleo nunca interpreta el
/// contenido: lo guarda como JSON opaco y le pide al dominio cómo se enseña. Así un dominio puede
/// guardar lo que necesite —códigos territoriales, identificadores resueltos— sin que el contrato
/// de la capacidad tenga que crecer por cada uno.
/// </remarks>
public sealed record FilaDeImportacionDto(
    long Id,
    int NumeroFila,
    string Resultado,
    bool PuedeImportarse,
    long? RegistroCoincidenteId,
    long? RegistroCreadoId,
    IReadOnlyDictionary<string, string?> Datos,
    IReadOnlyList<HallazgoDeImportacionDto> Hallazgos);

public sealed record ImportacionDto(
    long Id,
    string Dominio,
    string NombreArchivo,
    string Formato,
    string HuellaArchivo,
    string HuellaPlan,
    int VersionContrato,
    string Estado,
    DateTime FechaPrevisualizacion,
    DateTime? FechaAplicacion,
    DateTime? FechaExpiracion,
    DateTime? FechaDepuracion,
    string EstadoRetencion,
    bool EsPropio,
    bool PuedeConfirmar,
    int TotalFilas,
    int FilasImportables,
    int FilasRechazadas,
    int FilasAplicadas,
    int FilasExcluidas,
    IReadOnlyList<FilaDeImportacionDto> Filas,
    string Aviso);

public sealed record ResumenDeImportacionDto(
    long Id,
    string Dominio,
    string NombreArchivo,
    string Formato,
    string Estado,
    DateTime FechaPrevisualizacion,
    DateTime? FechaAplicacion,
    DateTime? FechaExpiracion,
    DateTime? FechaDepuracion,
    string EstadoRetencion,
    bool EsPropio,
    bool PuedeConfirmar,
    int TotalFilas,
    int FilasImportables,
    int FilasRechazadas,
    int FilasAplicadas,
    int FilasExcluidas);

public sealed record PaginaDeImportacionesDto(
    IReadOnlyList<ResumenDeImportacionDto> Items,
    int Pagina,
    int Tamano,
    int Total,
    int TotalPaginas,
    int DiasRetencionPrevisualizacion,
    string PoliticaRetencion);
