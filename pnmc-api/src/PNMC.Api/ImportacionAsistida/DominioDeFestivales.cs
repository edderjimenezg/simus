using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>El dato de un Festival ya normalizado, tal como se guarda en la fila del lote.</summary>
/// <remarks>
/// ES LA FORMA DEL DOMINIO, NO DEL NUCLEO. El núcleo guarda esto como JSON opaco; solo este fichero
/// sabe leerlo. Por eso puede llevar los códigos territoriales ya resueltos y también sus nombres,
/// que es lo que permite enseñar la previsualización sin volver a consultar DIVIPOLA.
/// </remarks>
public sealed record DatosDeFestival(
    string Nombre,
    string? Descripcion,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? Periodicidad,
    string? PeriodicidadDetalle,
    string? CorreoContacto,
    string? TelefonoContacto,
    string? SitioWeb,
    string? Instagram,
    string? Facebook,
    string? OtroEnlace);

/// <summary>
/// Festivales: el primer dominio importable, y el que estrenó la capacidad.
/// </summary>
/// <remarks>
/// <para>
/// <b>AQUI SOLO ESTA LO QUE ES DE FESTIVALES.</b> Validar el sobre, guardar el lote, reservar contra
/// confirmaciones simultáneas, la idempotencia, la bitácora, la retención y la forma de la respuesta
/// son del núcleo —<see cref="NucleoDeImportacion"/>—. Este fichero solo sabe qué campos tiene un
/// Festival, cómo se normalizan, cuándo una fila coincide con uno que ya existe y cómo se escribe.
/// </para>
/// <para>
/// <b>UN FESTIVAL IMPORTADO NACE EN BORRADOR</b> bajo la custodia del Programa. No se publica, y la
/// prueba de la capacidad lo comprueba sin que este fichero tenga que acordarse.
/// </para>
/// </remarks>
public sealed class DominioDeFestivales : IDominioDeImportacion
{
    /// <summary>Los campos que este dominio lee de un archivo.</summary>
    /// <remarks>
    /// LOS NOMBRES SON LOS DEL CONTRATO, no los del archivo: la consola empareja los encabezados de
    /// la hoja con estos campos antes de enviar nada, y el servidor descarta lo que no reconoce.
    /// </remarks>
    private static readonly CampoDeImportacionDto[] CamposDeclarados =
    [
        new("nombre", "Nombre del Festival", true),
        new("descripcion", "Descripción", false),
        new("nivelCobertura", "Nivel de cobertura", true),
        new("departamento", "Departamento", false),
        new("municipio", "Municipio", false),
        new("periodicidad", "Periodicidad", false),
        new("periodicidadDetalle", "Detalle de la periodicidad", false),
        new("correoContacto", "Correo de contacto", false),
        new("telefonoContacto", "Teléfono de contacto", false),
        new("sitioWeb", "Sitio web", false),
        new("instagram", "Instagram", false),
        new("facebook", "Facebook", false),
        new("otroEnlace", "Otro enlace", false),
    ];

    public CapacidadDeImportacion Capacidad { get; } = new(
        "festivales",
        "Festivales",
        EstadosFestival.Borrador,
        "Un Festival importado entra al circuito por donde entra cualquier otro: como borrador bajo "
            + "la custodia del Programa. Publicarlo sigue exigiendo revisión, igual que si lo hubiera "
            + "escrito alguien a mano.",
        Modulos.Festivales,
        "Festivales",
        CamposDeclarados);

    public async Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas, string huellaArchivo, PnmcDbContext db, CancellationToken ct)
    {
        var territorios = new TerritoriosDivipola(await db.DivipolaLocations.AsNoTracking().ToListAsync(ct));
        var existentes = (await db.FestivalRecords.AsNoTracking()
                .Select(item => new { item.Id, item.Name, item.DepartmentCode, item.MunicipalityCode })
                .ToListAsync(ct))
            .GroupBy(item => Clave(item.Name, item.DepartmentCode, item.MunicipalityCode), StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);

        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var planeadas = filas.OrderBy(item => item.NumeroFila)
            .Select(fila => Planear(fila, territorios, existentes, vistas))
            .ToList();

        return new PlanDeImportacion(NucleoDeImportacion.HuellaDelPlan(huellaArchivo, planeadas), planeadas);
    }

    public async Task<long> AplicarAsync(
        FilaImportacionRow fila, ContextoDeAplicacion contexto, PnmcDbContext db, CancellationToken ct)
    {
        var datos = Leer(fila.ContenidoNormalizadoJson, fila.Id);
        var festival = new FestivalRow
        {
            Name = datos.Nombre,
            Description = datos.Descripcion,
            CoverageLevel = datos.NivelCobertura,
            DepartmentCode = datos.CodigoDepartamento,
            MunicipalityCode = datos.CodigoMunicipio,
            Periodicidad = datos.Periodicidad,
            PeriodicidadDetalle = datos.PeriodicidadDetalle,
            ContactEmail = datos.CorreoContacto,
            ContactPhone = datos.TelefonoContacto,
            WebsiteUrl = datos.SitioWeb,
            InstagramUrl = datos.Instagram,
            FacebookUrl = datos.Facebook,
            OtherUrl = datos.OtroEnlace,
            OrganizacionPrincipalId = contexto.OrganizacionInstitucionalId,
            StatusCode = Capacidad.EstadoAlImportar,
            CreatedAt = contexto.Ahora,
            UpdatedAt = contexto.Ahora,
        };
        db.FestivalRecords.Add(festival);
        await db.SaveChangesAsync(ct);
        return festival.Id;
    }

    public IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson)
    {
        var datos = Leer(contenidoNormalizadoJson);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["nombre"] = datos.Nombre,
            ["descripcion"] = datos.Descripcion,
            ["nivelCobertura"] = datos.NivelCobertura,
            // SE ENSEÑA EL NOMBRE Y SE GUARDA EL CODIGO: quien revisa la previsualización reconoce
            // «Medellín», no «05001», y lo que acaba en la base tiene que ser el código.
            ["departamento"] = datos.NombreDepartamento,
            ["municipio"] = datos.NombreMunicipio,
            ["periodicidad"] = datos.Periodicidad,
            ["periodicidadDetalle"] = datos.PeriodicidadDetalle,
            ["correoContacto"] = datos.CorreoContacto,
            ["telefonoContacto"] = datos.TelefonoContacto,
            ["sitioWeb"] = datos.SitioWeb,
            ["instagram"] = datos.Instagram,
            ["facebook"] = datos.Facebook,
            ["otroEnlace"] = datos.OtroEnlace,
        };
    }

    public async Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas, PnmcDbContext db, CancellationToken ct)
    {
        var claves = filas
            .Select(fila => Leer(fila.ContenidoNormalizadoJson, fila.Id))
            .Select(datos => Clave(datos.Nombre, datos.CodigoDepartamento, datos.CodigoMunicipio))
            .ToHashSet(StringComparer.Ordinal);
        if (claves.Count == 0) return null;

        var vigentes = await db.FestivalRecords.AsNoTracking()
            .Select(item => new { item.Name, item.DepartmentCode, item.MunicipalityCode })
            .ToListAsync(ct);

        return vigentes.Any(item => claves.Contains(Clave(item.Name, item.DepartmentCode, item.MunicipalityCode)))
            ? "Apareció un Festival coincidente después de la previsualización. No se escribió nada; vuelve a previsualizar."
            : null;
    }

    // ---------- Las reglas de Festival ------------------------------------------------------

    private static FilaPlaneada Planear(
        FilaDeArchivoDto fila,
        TerritoriosDivipola territorios,
        Dictionary<string, long> existentes,
        HashSet<string> vistas)
    {
        var hallazgos = new Hallazgos();
        var nombre = NormalizacionDeFilas.Campo(fila, "nombre");
        var descripcion = NormalizacionDeFilas.Campo(fila, "descripcion");
        var nivel = NormalizarNivel(NormalizacionDeFilas.Campo(fila, "nivelCobertura"));
        var departamentoCrudo = NormalizacionDeFilas.Campo(fila, "departamento");
        var municipioCrudo = NormalizacionDeFilas.Campo(fila, "municipio");
        var departamento = territorios.Departamento(departamentoCrudo);
        var municipio = territorios.Municipio(departamento?.Codigo, municipioCrudo);
        var periodicidadCruda = NormalizacionDeFilas.Campo(fila, "periodicidad");
        var periodicidad = NormalizarPeriodicidad(periodicidadCruda);
        var detalle = NormalizacionDeFilas.Campo(fila, "periodicidadDetalle");
        var correoCrudo = NormalizacionDeFilas.Campo(fila, "correoContacto");
        var correo = NormalizacionDeFilas.NormalizarCorreo(correoCrudo);
        var telefonoCrudo = NormalizacionDeFilas.Campo(fila, "telefonoContacto");
        var telefono = NormalizacionDeFilas.NormalizarTelefono(telefonoCrudo);
        var sitio = NormalizacionDeFilas.Campo(fila, "sitioWeb");
        var instagram = NormalizacionDeFilas.Campo(fila, "instagram");
        var facebook = NormalizacionDeFilas.Campo(fila, "facebook");
        var otro = NormalizacionDeFilas.Campo(fila, "otroEnlace");

        hallazgos.Obligatorio(nombre, "nombre", "nombre_requerido", "El nombre del Festival es obligatorio.");
        hallazgos.Maximo(nombre, 220, "nombre");
        hallazgos.Maximo(descripcion, 10_000, "descripcion");
        if (nivel is null)
        {
            hallazgos.Error("nivel_cobertura_invalido", "nivelCobertura",
                "La cobertura debe ser nacional, departamental o municipal.");
        }

        if (nivel == "nacional")
        {
            if (departamento is not null || !string.IsNullOrWhiteSpace(municipioCrudo))
            {
                hallazgos.Info("territorio_omitido_por_cobertura", "nivelCobertura",
                    "La cobertura nacional no conserva departamento ni municipio.");
            }
            departamento = null;
            municipio = null;
        }
        else if (nivel == "departamental")
        {
            if (departamento is null)
                hallazgos.Error("departamento_invalido", "departamento", "El departamento no coincide con DIVIPOLA.");
            if (!string.IsNullOrWhiteSpace(municipioCrudo))
            {
                hallazgos.Info("municipio_omitido_por_cobertura", "municipio",
                    "La cobertura departamental no conserva municipio.");
            }
            municipio = null;
        }
        else if (nivel == "municipal")
        {
            if (departamento is null)
                hallazgos.Error("departamento_invalido", "departamento", "El departamento no coincide con DIVIPOLA.");
            if (municipio is null)
            {
                hallazgos.Error("municipio_invalido", "municipio",
                    "El municipio no coincide con DIVIPOLA dentro del departamento indicado.");
            }
        }

        if (!string.IsNullOrWhiteSpace(periodicidadCruda) && periodicidad is null)
            hallazgos.Error("periodicidad_invalida", "periodicidad", "La periodicidad no pertenece al catálogo vigente.");
        if (PeriodicidadesFestival.RequiereDetalle(periodicidad) && string.IsNullOrWhiteSpace(detalle))
            hallazgos.Error("detalle_periodicidad_requerido", "periodicidadDetalle", "Esta periodicidad requiere una explicación.");
        hallazgos.Maximo(detalle, 600, "periodicidadDetalle");

        hallazgos.Correo(correo, "correoContacto");
        hallazgos.Maximo(telefono, 80, "telefonoContacto");
        hallazgos.Url(sitio, "sitioWeb");
        hallazgos.Url(instagram, "instagram");
        hallazgos.Url(facebook, "facebook");
        hallazgos.Url(otro, "otroEnlace");

        if (!string.Equals(correoCrudo, correo, StringComparison.Ordinal))
            hallazgos.Info("correo_normalizado", "correoContacto", "El correo se normalizó a minúsculas y sin espacios.");
        if (!string.Equals(telefonoCrudo, telefono, StringComparison.Ordinal))
            hallazgos.Info("telefono_normalizado", "telefonoContacto", "El teléfono se normalizó sin separadores de presentación.");

        var datos = new DatosDeFestival(
            nombre ?? string.Empty, descripcion, nivel ?? string.Empty,
            departamento?.Codigo, departamento?.Nombre, municipio?.Codigo, municipio?.Nombre,
            periodicidad, detalle, correo, telefono, sitio, instagram, facebook, otro);

        var clave = Clave(datos.Nombre, datos.CodigoDepartamento, datos.CodigoMunicipio);
        long? existente = null;
        if (nombre is not null && existentes.TryGetValue(clave, out var encontrado))
        {
            existente = encontrado;
            hallazgos.Error("festival_existente", "nombre",
                $"Ya existe un Festival coincidente con identificador {encontrado}; esta importación no lo modifica.");
        }
        if (nombre is not null && !vistas.Add(clave))
            hallazgos.Error("duplicado_en_archivo", "nombre", "Otra fila del archivo representa el mismo Festival y territorio.");

        var importable = hallazgos.SinErrores;
        var resultado = existente is not null ? "coincidencia_existente" : importable ? "crear" : "rechazar";
        return new FilaPlaneada(
            fila.NumeroFila, resultado, importable, existente,
            JsonSerializer.Serialize(datos, NucleoDeImportacion.Json), hallazgos.Lista);
    }

    /// <summary>Qué hace que dos Festivales sean el mismo: su nombre y su territorio.</summary>
    private static string Clave(string? nombre, string? departamento, string? municipio) =>
        $"{NormalizacionDeFilas.Comparable(nombre)}|{departamento ?? string.Empty}|{municipio ?? string.Empty}";

    private static string? NormalizarNivel(string? valor) => NormalizacionDeFilas.Comparable(valor) switch
    {
        "nacional" => "nacional",
        "departamental" or "departamento" => "departamental",
        "municipal" or "municipio" or "local" => "municipal",
        _ => null,
    };

    private static string? NormalizarPeriodicidad(string? valor)
    {
        var normal = NormalizacionDeFilas.Comparable(valor).Replace(' ', '_');
        return normal.Length == 0 ? null : PeriodicidadesFestival.Todas.Contains(normal) ? normal : null;
    }

    private static DatosDeFestival Leer(string json, long filaId = 0) =>
        JsonSerializer.Deserialize<DatosDeFestival>(json, NucleoDeImportacion.Json)
            ?? throw new InvalidOperationException($"La fila {filaId} no contiene el contrato normalizado de Festival.");
}
