using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>El dato de una organización ya normalizado, tal como se guarda en la fila del lote.</summary>
public sealed record DatosDeOrganizacion(
    string Nombre,
    string? NombreLegal,
    string? Identificacion,
    string? TipoIdentificacion,
    string? Descripcion,
    string? CorreoContacto,
    string? TelefonoContacto,
    string? CodigoDepartamentoSede,
    string? NombreDepartamentoSede,
    string? CodigoMunicipioSede,
    string? NombreMunicipioSede,
    string? Direccion,
    string? SitioWeb,
    string? Instagram,
    string? Facebook);

/// <summary>
/// Organizaciones: el segundo dominio, y el que demuestra que el núcleo lo es.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE ESTE Y NO OTRO.</b> La organización es el actor del ecosistema —quien registra y
/// administra los procesos— y el Programa tiene sus directorios en hoja de cálculo. Además su
/// forma no se parece en nada a la de un Festival: identificación tributaria, sede, razón social.
/// Un segundo dominio parecido al primero no habría probado nada.
/// </para>
/// <para>
/// <b>UNA ORGANIZACION IMPORTADA NACE «PENDIENTE DE CONFIRMACION», NUNCA «ACTIVA».</b> Es la misma
/// regla que cerró el bloque de verificación de correo: sin una dirección comprobada, una
/// organización no actúa. Importarla activa afirmaría un contacto que nadie ha verificado, y de un
/// archivo de doscientas filas saldrían doscientas afirmaciones falsas.
/// </para>
/// <para>
/// <b>NO SE LE CREA CUENTA A NADIE.</b> Importar una organización la pone en el directorio; quien
/// responda por ella llegará después y reclamará su administración. Crear cuentas desde un archivo
/// sería repartir credenciales que nadie pidió.
/// </para>
/// </remarks>
public sealed class DominioDeOrganizaciones : IDominioDeImportacion
{
    private static readonly CampoDeImportacionDto[] CamposDeclarados =
    [
        new("nombre", "Nombre de la organización", true),
        new("nombreLegal", "Razón social", false),
        new("identificacion", "NIT o identificación", false),
        new("tipoIdentificacion", "Tipo de identificación", false),
        new("descripcion", "Descripción", false),
        new("correoContacto", "Correo de contacto", false),
        new("telefonoContacto", "Teléfono de contacto", false),
        new("departamentoSede", "Departamento de la sede", true),
        new("municipioSede", "Municipio de la sede", true),
        new("direccion", "Dirección", false),
        new("sitioWeb", "Sitio web", false),
        new("instagram", "Instagram", false),
        new("facebook", "Facebook", false),
    ];

    public CapacidadDeImportacion Capacidad { get; } = new(
        "organizaciones",
        "Organizaciones",
        EstadosDeOrganizacion.PendienteDeConfirmacion,
        "Una organización importada entra al directorio pendiente de confirmación, nunca activa: "
            + "sin una dirección de correo comprobada no puede actuar, y darla por activa afirmaría "
            + "un contacto que nadie ha verificado. Se activa cuando alguien confirme su correo.",
        Modulos.Organizaciones,
        "Entidades",
        CamposDeclarados);

    public async Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas, string huellaArchivo, PnmcDbContext db, CancellationToken ct)
    {
        var territorios = new TerritoriosDivipola(await db.DivipolaLocations.AsNoTracking().ToListAsync(ct));
        var existentes = await db.EntityProfiles.AsNoTracking()
            .Where(item => item.EntityType == "organizacion")
            .Select(item => new { item.Id, item.Name, item.IdentificationNumber })
            .ToListAsync(ct);

        var porNombre = existentes
            .GroupBy(item => NormalizacionDeFilas.Comparable(item.Name), StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);
        var porIdentificacion = existentes
            .Where(item => !string.IsNullOrWhiteSpace(item.IdentificationNumber))
            .GroupBy(item => ClaveDeIdentificacion(item.IdentificationNumber), StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);

        var nombresVistos = new HashSet<string>(StringComparer.Ordinal);
        var identificacionesVistas = new HashSet<string>(StringComparer.Ordinal);
        var planeadas = filas.OrderBy(item => item.NumeroFila)
            .Select(fila => Planear(fila, territorios, porNombre, porIdentificacion, nombresVistos, identificacionesVistas))
            .ToList();

        return new PlanDeImportacion(NucleoDeImportacion.HuellaDelPlan(huellaArchivo, planeadas), planeadas);
    }

    public async Task<long> AplicarAsync(
        FilaImportacionRow fila, ContextoDeAplicacion contexto, PnmcDbContext db, CancellationToken ct)
    {
        var datos = Leer(fila.ContenidoNormalizadoJson, fila.Id);
        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = datos.Nombre,
            LegalName = datos.NombreLegal,
            IdentificationNumber = datos.Identificacion,
            IdentificationType = datos.TipoIdentificacion,
            Description = datos.Descripcion,
            ContactEmail = datos.CorreoContacto,
            ContactPhone = datos.TelefonoContacto,
            HeadquartersDepartmentCode = datos.CodigoDepartamentoSede,
            HeadquartersMunicipalityCode = datos.CodigoMunicipioSede,
            AddressText = datos.Direccion,
            WebsiteUrl = datos.SitioWeb,
            InstagramUrl = datos.Instagram,
            FacebookUrl = datos.Facebook,
            StatusCode = Capacidad.EstadoAlImportar,
            // LA VIGENCIA SE DERIVA DEL ESTADO, nunca se escribe a mano: la base rechaza cualquier
            // combinación en la que discrepen (`CK_Entidades_VigenciaCoherente`).
            IsActive = EstadosDeOrganizacion.VigenciaDe(Capacidad.EstadoAlImportar),
            IsInstitutional = false,
            CreatedAt = contexto.Ahora,
            UpdatedAt = contexto.Ahora,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync(ct);
        return organizacion.Id;
    }

    public IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson)
    {
        var datos = Leer(contenidoNormalizadoJson);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["nombre"] = datos.Nombre,
            ["nombreLegal"] = datos.NombreLegal,
            ["identificacion"] = datos.Identificacion,
            ["tipoIdentificacion"] = datos.TipoIdentificacion,
            ["descripcion"] = datos.Descripcion,
            ["correoContacto"] = datos.CorreoContacto,
            ["telefonoContacto"] = datos.TelefonoContacto,
            ["departamentoSede"] = datos.NombreDepartamentoSede,
            ["municipioSede"] = datos.NombreMunicipioSede,
            ["direccion"] = datos.Direccion,
            ["sitioWeb"] = datos.SitioWeb,
            ["instagram"] = datos.Instagram,
            ["facebook"] = datos.Facebook,
        };
    }

    public async Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas, PnmcDbContext db, CancellationToken ct)
    {
        var datos = filas.Select(fila => Leer(fila.ContenidoNormalizadoJson, fila.Id)).ToList();
        if (datos.Count == 0) return null;

        var nombres = datos.Select(item => NormalizacionDeFilas.Comparable(item.Nombre)).ToHashSet(StringComparer.Ordinal);
        var identificaciones = datos
            .Where(item => !string.IsNullOrWhiteSpace(item.Identificacion))
            .Select(item => ClaveDeIdentificacion(item.Identificacion))
            .ToHashSet(StringComparer.Ordinal);

        var vigentes = await db.EntityProfiles.AsNoTracking()
            .Where(item => item.EntityType == "organizacion")
            .Select(item => new { item.Name, item.IdentificationNumber })
            .ToListAsync(ct);

        var apareció = vigentes.Any(item =>
            nombres.Contains(NormalizacionDeFilas.Comparable(item.Name))
            || (!string.IsNullOrWhiteSpace(item.IdentificationNumber)
                && identificaciones.Contains(ClaveDeIdentificacion(item.IdentificationNumber))));

        return apareció
            ? "Apareció una organización coincidente después de la previsualización. No se escribió nada; vuelve a previsualizar."
            : null;
    }

    // ---------- Las reglas de organización ---------------------------------------------------

    private static FilaPlaneada Planear(
        FilaDeArchivoDto fila,
        TerritoriosDivipola territorios,
        Dictionary<string, long> porNombre,
        Dictionary<string, long> porIdentificacion,
        HashSet<string> nombresVistos,
        HashSet<string> identificacionesVistas)
    {
        var hallazgos = new Hallazgos();
        var nombre = NormalizacionDeFilas.Campo(fila, "nombre");
        var nombreLegal = NormalizacionDeFilas.Campo(fila, "nombreLegal");
        var identificacion = AltaDeOrganizacion.NormalizarIdentificacion(NormalizacionDeFilas.Campo(fila, "identificacion"));
        var tipoIdentificacion = NormalizacionDeFilas.Campo(fila, "tipoIdentificacion");
        var descripcion = NormalizacionDeFilas.Campo(fila, "descripcion");
        var correoCrudo = NormalizacionDeFilas.Campo(fila, "correoContacto");
        var correo = NormalizacionDeFilas.NormalizarCorreo(correoCrudo);
        var telefonoCrudo = NormalizacionDeFilas.Campo(fila, "telefonoContacto");
        var telefono = NormalizacionDeFilas.NormalizarTelefono(telefonoCrudo);
        var departamentoCrudo = NormalizacionDeFilas.Campo(fila, "departamentoSede");
        var municipioCrudo = NormalizacionDeFilas.Campo(fila, "municipioSede");
        var departamento = territorios.Departamento(departamentoCrudo);
        var municipio = territorios.Municipio(departamento?.Codigo, municipioCrudo);
        var direccion = NormalizacionDeFilas.Campo(fila, "direccion");
        var sitio = NormalizacionDeFilas.Campo(fila, "sitioWeb");
        var instagram = NormalizacionDeFilas.Campo(fila, "instagram");
        var facebook = NormalizacionDeFilas.Campo(fila, "facebook");

        hallazgos.Obligatorio(nombre, "nombre", "nombre_requerido", "El nombre de la organización es obligatorio.");
        hallazgos.Maximo(nombre, 220, "nombre");
        hallazgos.Maximo(nombreLegal, 220, "nombreLegal");
        hallazgos.Maximo(descripcion, 10_000, "descripcion");
        hallazgos.Maximo(direccion, 300, "direccion");
        hallazgos.Correo(correo, "correoContacto");
        hallazgos.Maximo(telefono, 80, "telefonoContacto");
        hallazgos.Url(sitio, "sitioWeb");
        hallazgos.Url(instagram, "instagram");
        hallazgos.Url(facebook, "facebook");

        // LA SEDE ES OBLIGATORIA Y COMPLETA. Una organización sin territorio no se puede buscar por
        // donde de verdad se la busca —su región— y acaba fuera de todos los informes.
        if (departamento is null)
            hallazgos.Error("departamento_invalido", "departamentoSede", "El departamento de la sede no coincide con DIVIPOLA.");
        if (municipio is null)
        {
            hallazgos.Error("municipio_invalido", "municipioSede",
                "El municipio de la sede no coincide con DIVIPOLA dentro del departamento indicado.");
        }

        if (identificacion is not null && !AltaDeOrganizacion.PatronDeIdentificacion.IsMatch(identificacion))
        {
            hallazgos.Error("identificacion_invalida", "identificacion",
                "La identificación solo admite letras, números, puntos y guiones, entre 3 y 60 caracteres.");
        }

        if (!string.Equals(correoCrudo, correo, StringComparison.Ordinal))
            hallazgos.Info("correo_normalizado", "correoContacto", "El correo se normalizó a minúsculas y sin espacios.");
        if (!string.Equals(telefonoCrudo, telefono, StringComparison.Ordinal))
            hallazgos.Info("telefono_normalizado", "telefonoContacto", "El teléfono se normalizó sin separadores de presentación.");

        var datos = new DatosDeOrganizacion(
            nombre ?? string.Empty, nombreLegal, identificacion, tipoIdentificacion, descripcion,
            correo, telefono, departamento?.Codigo, departamento?.Nombre,
            municipio?.Codigo, municipio?.Nombre, direccion, sitio, instagram, facebook);

        // DOS FORMAS DE SER LA MISMA ORGANIZACION, y las dos importan: el NIT es la identidad
        // jurídica y el nombre es por el que la busca una persona. Comprobar solo una dejaría
        // entrar la otra mitad de los duplicados.
        long? existente = null;
        var claveNombre = NormalizacionDeFilas.Comparable(datos.Nombre);
        if (nombre is not null && porNombre.TryGetValue(claveNombre, out var porSuNombre))
        {
            existente = porSuNombre;
            hallazgos.Error("organizacion_existente", "nombre",
                $"Ya existe una organización con ese nombre, con identificador {porSuNombre}; esta importación no la modifica.");
        }

        if (identificacion is not null)
        {
            var claveIdentificacion = ClaveDeIdentificacion(identificacion);
            if (porIdentificacion.TryGetValue(claveIdentificacion, out var porSuNit))
            {
                existente ??= porSuNit;
                hallazgos.Error("identificacion_existente", "identificacion",
                    $"Ya existe una organización con esa identificación, con identificador {porSuNit}.");
            }
            if (!identificacionesVistas.Add(claveIdentificacion))
                hallazgos.Error("duplicado_en_archivo", "identificacion", "Otra fila del archivo trae la misma identificación.");
        }

        if (nombre is not null && !nombresVistos.Add(claveNombre))
            hallazgos.Error("duplicado_en_archivo", "nombre", "Otra fila del archivo trae el mismo nombre.");

        var importable = hallazgos.SinErrores;
        var resultado = existente is not null ? "coincidencia_existente" : importable ? "crear" : "rechazar";
        return new FilaPlaneada(
            fila.NumeroFila, resultado, importable, existente,
            JsonSerializer.Serialize(datos, NucleoDeImportacion.Json), hallazgos.Lista);
    }

    /// <summary>
    /// La identificación comparable: solo sus caracteres significativos.
    /// </summary>
    /// <remarks>
    /// «900.123.456-7» y «9001234567» son el mismo NIT escrito por dos personas distintas. Comparar
    /// la cadena tal cual dejaría entrar el mismo duplicado tantas veces como formatos existan.
    /// </remarks>
    private static string ClaveDeIdentificacion(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static DatosDeOrganizacion Leer(string json, long filaId = 0) =>
        JsonSerializer.Deserialize<DatosDeOrganizacion>(json, NucleoDeImportacion.Json)
            ?? throw new InvalidOperationException($"La fila {filaId} no contiene el contrato normalizado de organización.");
}
