using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>
/// Todo lo que es igual en cualquier importación, escrito una vez.
/// </summary>
/// <remarks>
/// <b>LO QUE VIVE AQUI</b> es el circuito: validar el sobre del archivo, calcular la huella del
/// plan, guardar el lote con sus filas y hallazgos, construir la respuesta y decidir la retención.
/// Nada de esto depende de si lo que llega son Festivales, organizaciones o publicaciones.
/// <b>LO QUE NO VIVE AQUI</b> es qué campos tiene cada cosa, cómo se normaliza y cómo se escribe:
/// eso lo pone el dominio a través de <see cref="IDominioDeImportacion"/>.
/// </remarks>
public static class NucleoDeImportacion
{
    /// <summary>La versión del contrato de archivo que esta API acepta.</summary>
    /// <remarks>
    /// ES UNA SOLA PARA TODOS LOS DOMINIOS: describe la forma del sobre —nombre, formato, huella y
    /// filas con sus campos—, no los campos de cada dominio, que ya viajan declarados aparte.
    /// </remarks>
    public const int VersionContrato = 1;

    /// <summary>Cuántas filas admite un archivo.</summary>
    /// <remarks>
    /// QUINIENTAS, y el límite es de la capacidad y no del dominio: lo que protege es la
    /// transacción y la memoria del servidor, que no distinguen qué se está importando.
    /// </remarks>
    public const int MaximoFilas = 500;

    /// <summary>
    /// Separadores de campo y de fila al construir la huella.
    /// </summary>
    /// <remarks>
    /// SON LOS SEPARADORES DE UNIDAD Y DE REGISTRO DE ASCII, que no aparecen en ningún dato escrito
    /// por una persona: sin ellos, dos planes distintos podrían producir la misma cadena y por tanto
    /// la misma huella, que es justo lo que la huella viene a impedir.
    /// </remarks>
    private const char SeparadorDeCampo = '\u001F';
    private const char SeparadorDeFila = '\u001E';

    /// <summary>
    /// La huella que identifica exactamente lo que se previsualizó.
    /// </summary>
    /// <remarks>
    /// <b>INCLUYE EL CONTENIDO NORMALIZADO, no solo el archivo.</b> Dos archivos idénticos pueden
    /// planearse distinto si DIVIPOLA cambió o si entremedias apareció un registro coincidente:
    /// confirmar contra una huella vieja escribiría algo que nadie revisó.
    /// </remarks>
    public static string HuellaDelPlan(string huellaArchivo, IReadOnlyList<FilaPlaneada> filas)
    {
        var material = new StringBuilder(huellaArchivo).Append(SeparadorDeCampo).Append(VersionContrato);
        foreach (var fila in filas)
        {
            material.Append(SeparadorDeFila).Append(fila.NumeroFila).Append(SeparadorDeCampo)
                .Append(fila.Resultado).Append(SeparadorDeCampo)
                .Append(fila.ContenidoNormalizadoJson).Append(SeparadorDeCampo)
                .Append(fila.RegistroCoincidenteId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()))).ToLowerInvariant();
    }

    /// <summary>Comprueba el sobre del archivo. No mira los campos: eso es del dominio.</summary>
    public static Dictionary<string, string[]> ValidarSobre(PrevisualizarImportacionSolicitud solicitud)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nombre = Path.GetFileName(solicitud.NombreArchivo?.Trim());
        var formato = solicitud.Formato?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 255)
            errores["nombreArchivo"] = ["Indica un nombre de archivo de hasta 255 caracteres."];
        if (formato is not ("csv" or "xlsx"))
            errores["formato"] = ["El formato debe ser csv o xlsx."];
        if (formato is not null && nombre is not null && !nombre.EndsWith($".{formato}", StringComparison.OrdinalIgnoreCase))
            errores["nombreArchivo"] = ["La extensión del archivo no coincide con el formato declarado."];
        if (string.IsNullOrWhiteSpace(solicitud.HuellaArchivo) || !EsHuella(solicitud.HuellaArchivo))
            errores["huellaArchivo"] = ["La huella SHA-256 debe tener 64 caracteres hexadecimales."];
        if (solicitud.VersionContrato != VersionContrato)
            errores["versionContrato"] = [$"Esta API acepta la versión {VersionContrato}."];

        if (solicitud.Filas is null || solicitud.Filas.Count == 0 || solicitud.Filas.Count > MaximoFilas)
        {
            errores["filas"] = [$"El archivo debe contener entre 1 y {MaximoFilas} filas."];
        }
        else if (solicitud.Filas.Any(fila => fila.NumeroFila < 2)
            || solicitud.Filas.Select(fila => fila.NumeroFila).Distinct().Count() != solicitud.Filas.Count)
        {
            // A PARTIR DE 2 PORQUE LA 1 ES LA DE ENCABEZADOS: el número es el de la hoja original,
            // que es por donde quien preparó el archivo va a buscar lo que hay que corregir.
            errores["filas"] = ["Cada fila debe conservar un número de origen único a partir de 2."];
        }

        return errores;
    }

    /// <summary>
    /// Cómo se escribe y se lee el contenido normalizado de una fila.
    /// </summary>
    /// <remarks>
    /// <b>LO FIJA EL NUCLEO Y NO CADA DOMINIO.</b> El JSON lo escribe el dominio y lo leen tres
    /// sitios —el propio dominio, la proyección para mostrar y el depurador de retención—; si cada
    /// uno asumiera una convención distinta, el depurador buscaría <c>correoContacto</c> en un JSON
    /// que guarda <c>CorreoContacto</c> y retiraría el dato de nadie, en silencio y de noche.
    /// <b>SE LEE SIN DISTINGUIR MAYUSCULAS</b> porque en la base ya hay filas escritas con la
    /// convención anterior, y una carga guardada antes de hoy tiene que seguir siendo legible.
    /// </remarks>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Si una cadena es una huella SHA-256 en hexadecimal.</summary>
    public static bool EsHuella(string? valor) =>
        valor is { Length: 64 }
        && valor.All(caracter => char.IsAsciiDigit(caracter) || (caracter >= 'a' && caracter <= 'f'));

    /// <summary>
    /// Descarta los campos que el dominio no declara.
    /// </summary>
    /// <remarks>
    /// UN DICCIONARIO ABIERTO SERIA UNA FORMA ELEGANTE DE ACEPTAR BASURA: sin este filtro, el
    /// contenido de una columna sobrante acabaría guardado en la traza sin que nadie lo revisara.
    /// </remarks>
    public static IReadOnlyList<FilaDeArchivoDto> SoloCamposDeclarados(
        IReadOnlyList<FilaDeArchivoDto> filas, CapacidadDeImportacion capacidad)
    {
        var declarados = capacidad.Campos.Select(campo => campo.Nombre).ToHashSet(StringComparer.Ordinal);
        return filas.Select(fila => new FilaDeArchivoDto
        {
            NumeroFila = fila.NumeroFila,
            Valores = fila.Valores
                .Where(par => declarados.Contains(par.Key))
                .ToDictionary(par => par.Key, par => par.Value, StringComparer.Ordinal),
        }).ToList();
    }

    // ---------- La respuesta -----------------------------------------------------------------

    public static async Task<ImportacionDto> ConstruirDtoAsync(
        LoteImportacionRow lote,
        IDominioDeImportacion dominio,
        PnmcDbContext db,
        int usuarioActualId,
        int diasRetencion,
        CancellationToken ct)
    {
        var filas = await db.FilasImportacion.AsNoTracking()
            .Where(item => item.LoteImportacionId == lote.Id)
            .OrderBy(item => item.NumeroFila)
            .ToListAsync(ct);
        var ids = filas.Select(item => item.Id).ToArray();
        var hallazgos = await db.HallazgosImportacion.AsNoTracking()
            .Where(item => ids.Contains(item.FilaImportacionId))
            .OrderBy(item => item.Id)
            .ToListAsync(ct);
        var porFila = hallazgos
            .GroupBy(item => item.FilaImportacionId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<HallazgoDeImportacionDto>)grupo
                    .Select(item => new HallazgoDeImportacionDto(item.Severidad, item.Codigo, item.Campo, item.Mensaje))
                    .ToList());

        var filasDto = filas.Select(item => new FilaDeImportacionDto(
            item.Id, item.NumeroFila, item.Resultado, item.PuedeImportarse,
            item.RegistroCoincidenteId, item.RegistroCreadoId,
            // EL NUCLEO NO INTERPRETA EL JSON: se lo pide al dominio, que es quien lo escribió.
            dominio.ParaMostrar(item.ContenidoNormalizadoJson),
            porFila.GetValueOrDefault(item.Id, []))).ToList();

        return new ImportacionDto(
            lote.Id, lote.ModuloId, lote.NombreArchivo, lote.Formato, lote.HuellaArchivo, lote.HuellaPlan,
            lote.VersionContrato, lote.Estado, lote.FechaPrevisualizacion, lote.FechaAplicacion,
            FechaExpiracion(lote, diasRetencion), lote.FechaDepuracion, EstadoRetencion(lote),
            lote.UsuarioId == usuarioActualId,
            lote.UsuarioId == usuarioActualId && lote.Estado == ReglasDeImportacion.Previsualizado,
            lote.TotalFilas, lote.FilasImportables, lote.FilasRechazadas, lote.FilasAplicadas, lote.FilasExcluidas,
            filasDto,
            Aviso(lote, dominio.Capacidad));
    }

    public static ResumenDeImportacionDto Resumen(LoteImportacionRow lote, int usuarioActualId, int diasRetencion) => new(
        lote.Id, lote.ModuloId, lote.NombreArchivo, lote.Formato, lote.Estado, lote.FechaPrevisualizacion,
        lote.FechaAplicacion, FechaExpiracion(lote, diasRetencion), lote.FechaDepuracion, EstadoRetencion(lote),
        lote.UsuarioId == usuarioActualId,
        lote.UsuarioId == usuarioActualId && lote.Estado == ReglasDeImportacion.Previsualizado,
        lote.TotalFilas, lote.FilasImportables, lote.FilasRechazadas, lote.FilasAplicadas, lote.FilasExcluidas);

    public static string PoliticaRetencion(int dias) =>
        $"Las previsualizaciones conservan detalle durante {dias} días. Al expirar se retiran filas y datos normalizados. " +
        "Los lotes aplicados minimizan correo y teléfono duplicados y conservan evidencia hasta que la TRD institucional defina su disposición final.";

    /// <summary>Lo que la pantalla dice sobre este lote. Lo escribe la capacidad, no el dominio.</summary>
    private static string Aviso(LoteImportacionRow lote, CapacidadDeImportacion capacidad) =>
        lote.Estado == ReglasDeImportacion.Expirado
            ? "La previsualización cumplió su plazo operativo. Sus filas y datos normalizados fueron retirados; se conserva únicamente la cabecera trazable."
            : $"La importación solo crea registros en estado «{capacidad.EstadoAlImportar}» bajo responsabilidad institucional. "
                + "Nunca publica ni modifica registros existentes.";

    private static DateTime? FechaExpiracion(LoteImportacionRow lote, int diasRetencion) =>
        lote.Estado is ReglasDeImportacion.Previsualizado or ReglasDeImportacion.Expirado
            ? lote.FechaPrevisualizacion.AddDays(diasRetencion)
            : null;

    private static string EstadoRetencion(LoteImportacionRow lote) => lote.Estado switch
    {
        ReglasDeImportacion.Expirado => "detalle_depurado",
        ReglasDeImportacion.Aplicado when lote.FechaDepuracion is not null => "datos_duplicados_minimizados",
        ReglasDeImportacion.Aplicado => "pendiente_minimizacion",
        ReglasDeImportacion.Previsualizado => "detalle_temporal",
        _ => "en_proceso",
    };
}
