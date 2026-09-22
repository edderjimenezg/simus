using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>Un evento de la Agenda ya normalizado, tal como se guarda en la fila del lote.</summary>
public sealed record DatosDeEvento(
    string Titulo,
    string Slug,
    string Descripcion,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Modalidad,
    string? Lugar,
    string? Url,
    string? Organizador,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string NivelCobertura);

/// <summary>
/// Agenda: el dominio donde la base impone más que el endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>LA MODALIDAD DECIDE QUE DATOS SON OBLIGATORIOS, Y NO ES UNA REGLA DE PANTALLA.</b> Un evento
/// presencial sin lugar no se puede anunciar —nadie sabría a dónde ir— y uno virtual sin enlace
/// tampoco. Lo impone <c>CK_EventosAgenda_DatosDeModalidad</c> en la base. Si esta importación no
/// lo comprobara al planear, un archivo de cien filas llegaría al <c>SaveChanges</c> y reventaría
/// ahí, después de la previsualización que dijo que todo estaba bien.
/// </para>
/// <para>
/// <b>EL NIVEL DE COBERTURA VA ATADO A SUS CODIGOS, TAMBIEN POR CHECK.</b> Se deriva del territorio
/// que traiga la fila en vez de pedirlo como columna: un archivo con municipio es municipal, uno
/// con solo departamento es departamental, y uno sin territorio es nacional. Pedirlo aparte
/// permitiría que la hoja dijera «municipal» sin municipio.
/// </para>
/// <para>
/// <b>NACE EN BORRADOR.</b> Un evento publicado convoca gente a un sitio y a una hora; importarlo
/// publicado sería convocar sin que nadie lo hubiera leído.
/// </para>
/// </remarks>
public sealed class DominioDeAgenda : IDominioDeImportacion
{
    private static readonly CampoDeImportacionDto[] CamposDeclarados =
    [
        new("titulo", "Título", true),
        new("descripcion", "Descripción", true),
        new("fechaInicio", "Fecha de inicio", true),
        new("fechaFin", "Fecha de cierre", false),
        new("modalidad", "Modalidad (presencial · virtual · mixta)", true),
        new("lugar", "Lugar", false),
        new("url", "Enlace", false),
        new("organizador", "Organiza", false),
        new("departamento", "Departamento", false),
        new("municipio", "Municipio", false),
        new("slug", "Dirección (slug)", false),
    ];

    /// <summary>Las tres modalidades que la base admite. Fuera de aquí, la fila se rechaza.</summary>
    private static readonly string[] Modalidades = ["presencial", "virtual", "mixta"];

    public CapacidadDeImportacion Capacidad { get; } = new(
        "agenda",
        "Agenda",
        "borrador",
        "Un evento importado entra en borrador, nunca publicado: un evento publicado convoca gente "
            + "a un sitio y a una hora, e importarlo publicado sería convocar sin que nadie lo "
            + "hubiera leído. Se publica cuando alguien lo revise.",
        Modulos.Agenda,
        "EventosAgenda",
        CamposDeclarados);

    public async Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas, string huellaArchivo, PnmcDbContext db, CancellationToken ct)
    {
        var territorios = new TerritoriosDivipola(await db.DivipolaLocations.AsNoTracking().ToListAsync(ct));
        var existentes = await db.EventosAgenda.AsNoTracking()
            .Select(item => new { item.Id, item.Slug })
            .ToListAsync(ct);

        var porSlug = existentes
            .GroupBy(item => item.Slug, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => (long)grupo.First().Id, StringComparer.Ordinal);

        var slugsVistos = new HashSet<string>(StringComparer.Ordinal);
        var planeadas = filas.OrderBy(item => item.NumeroFila)
            .Select(fila => Planear(fila, territorios, porSlug, slugsVistos))
            .ToList();

        return new PlanDeImportacion(NucleoDeImportacion.HuellaDelPlan(huellaArchivo, planeadas), planeadas);
    }

    public async Task<long> AplicarAsync(
        FilaImportacionRow fila, ContextoDeAplicacion contexto, PnmcDbContext db, CancellationToken ct)
    {
        var datos = Leer(fila.ContenidoNormalizadoJson, fila.Id);
        var evento = new EventoAgendaRow
        {
            Slug = datos.Slug,
            Titulo = datos.Titulo,
            Descripcion = datos.Descripcion,
            FechaInicio = datos.FechaInicio,
            FechaFin = datos.FechaFin,
            Modalidad = datos.Modalidad,
            Lugar = datos.Lugar,
            Url = datos.Url,
            Organizador = datos.Organizador,
            CodigoDepartamento = datos.CodigoDepartamento,
            CodigoMunicipio = datos.CodigoMunicipio,
            NivelCobertura = datos.NivelCobertura,
            Estado = Capacidad.EstadoAlImportar,
            Version = 1,
            FechaCreacion = contexto.Ahora,
            FechaActualizacion = contexto.Ahora,
        };
        db.EventosAgenda.Add(evento);
        await db.SaveChangesAsync(ct);
        return evento.Id;
    }

    public IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson)
    {
        var datos = Leer(contenidoNormalizadoJson);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["titulo"] = datos.Titulo,
            ["descripcion"] = datos.Descripcion,
            ["fechaInicio"] = datos.FechaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["fechaFin"] = datos.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["modalidad"] = datos.Modalidad,
            ["lugar"] = datos.Lugar,
            ["url"] = datos.Url,
            ["organizador"] = datos.Organizador,
            ["departamento"] = datos.NombreDepartamento,
            ["municipio"] = datos.NombreMunicipio,
            ["slug"] = datos.Slug,
        };
    }

    public async Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas, PnmcDbContext db, CancellationToken ct)
    {
        var slugs = filas.Select(fila => Leer(fila.ContenidoNormalizadoJson, fila.Id).Slug).ToHashSet(StringComparer.Ordinal);
        if (slugs.Count == 0) return null;

        var apareció = await db.EventosAgenda.AsNoTracking().AnyAsync(item => slugs.Contains(item.Slug), ct);

        return apareció
            ? "Apareció un evento en la misma dirección después de la previsualización. No se escribió nada; vuelve a previsualizar."
            : null;
    }

    // ---------- Las reglas de un evento ------------------------------------------------------

    private static FilaPlaneada Planear(
        FilaDeArchivoDto fila,
        TerritoriosDivipola territorios,
        Dictionary<string, long> porSlug,
        HashSet<string> slugsVistos)
    {
        var hallazgos = new Hallazgos();
        var titulo = NormalizacionDeFilas.Campo(fila, "titulo");
        var descripcion = NormalizacionDeFilas.Campo(fila, "descripcion");
        var lugar = NormalizacionDeFilas.Campo(fila, "lugar");
        var url = NormalizacionDeFilas.Campo(fila, "url");
        var organizador = NormalizacionDeFilas.Campo(fila, "organizador");

        hallazgos.Obligatorio(titulo, "titulo", "titulo_obligatorio", "El evento necesita un título.");
        hallazgos.Obligatorio(descripcion, "descripcion", "descripcion_obligatoria", "El evento necesita una descripción.");
        hallazgos.Maximo(titulo, 300, "titulo");
        hallazgos.Maximo(descripcion, 1000, "descripcion");
        hallazgos.Maximo(lugar, 300, "lugar");
        hallazgos.Maximo(organizador, 200, "organizador");
        if (!string.IsNullOrWhiteSpace(url)) hallazgos.Url(url, "url");

        var modalidad = LeerModalidad(NormalizacionDeFilas.Campo(fila, "modalidad"), hallazgos);

        // LO QUE LA BASE EXIGE, COMPROBADO AQUI. Sin esto, la fila pasaría la previsualización y
        // reventaría en el `SaveChanges`, después de haber dicho que todo estaba bien.
        if (modalidad is "presencial" or "mixta" && string.IsNullOrWhiteSpace(lugar))
        {
            hallazgos.Error("lugar_obligatorio", "lugar",
                "Un evento presencial necesita lugar: sin él nadie sabría a dónde ir.");
        }
        if (modalidad is "virtual" or "mixta" && string.IsNullOrWhiteSpace(url))
        {
            hallazgos.Error("enlace_obligatorio", "url",
                "Un evento virtual necesita enlace: sin él nadie sabría por dónde entrar.");
        }

        var inicio = LeerFecha(NormalizacionDeFilas.Campo(fila, "fechaInicio"), "fechaInicio", hallazgos, obligatoria: true);
        var fin = LeerFecha(NormalizacionDeFilas.Campo(fila, "fechaFin"), "fechaFin", hallazgos, obligatoria: false);
        if (inicio is not null && fin is not null && fin < inicio)
        {
            hallazgos.Error("fechas_invertidas", "fechaFin", "La fecha de cierre es anterior a la de inicio.");
        }

        // EL TERRITORIO ES OPCIONAL AQUI, y eso lo distingue de Organizaciones y Festivales: un
        // evento nacional no tiene municipio, y exigirlo obligaría a inventar uno. Lo que sí se
        // comprueba es que lo que venga sea real: un departamento que no está en DIVIPOLA no es un
        // hueco, es un dato equivocado.
        var departamentoCrudo = NormalizacionDeFilas.Campo(fila, "departamento");
        var municipioCrudo = NormalizacionDeFilas.Campo(fila, "municipio");
        var departamento = territorios.Departamento(departamentoCrudo);
        var municipio = territorios.Municipio(departamento?.Codigo, municipioCrudo);

        if (!string.IsNullOrWhiteSpace(departamentoCrudo) && departamento is null)
            hallazgos.Error("departamento_invalido", "departamento", "El departamento no coincide con DIVIPOLA.");
        if (!string.IsNullOrWhiteSpace(municipioCrudo) && municipio is null)
        {
            hallazgos.Error("municipio_invalido", "municipio",
                "El municipio no coincide con DIVIPOLA dentro del departamento indicado.");
        }

        // EL NIVEL SE DERIVA DEL TERRITORIO QUE VINO, no se pide aparte: pedirlo permitiría que la
        // hoja dijera «municipal» sin municipio, y la base rechaza esa combinación
        // (`CK_EventosAgenda_NivelCobertura`).
        var nivel = municipio is not null ? "municipal"
            : departamento is not null ? "departamental"
            : "nacional";

        var slugPedido = NormalizacionDeFilas.Campo(fila, "slug");
        var slug = ReglasDeAgenda.SlugDesde(slugPedido is { Length: > 0 } ? slugPedido : titulo ?? string.Empty);
        if (slug.Length == 0 && !string.IsNullOrWhiteSpace(titulo))
        {
            hallazgos.Error("titulo_sin_direccion", "titulo", "Del título no sale una dirección utilizable para el portal.");
        }

        long? coincidente = null;
        if (slug.Length > 0)
        {
            if (porSlug.TryGetValue(slug, out var existente))
            {
                coincidente = existente;
                hallazgos.Error("evento_existente", "slug",
                    $"Ya hay un evento en la dirección «{slug}». No se importa para no pisarlo.");
            }
            else if (!slugsVistos.Add(slug))
            {
                hallazgos.Error("duplicado_en_el_archivo", "slug",
                    $"El archivo trae dos filas para la dirección «{slug}».");
            }
        }

        var datos = new DatosDeEvento(
            titulo ?? string.Empty, slug, descripcion ?? string.Empty,
            inicio ?? default, fin, modalidad, lugar, url, organizador,
            departamento?.Codigo, departamento?.Nombre,
            municipio?.Codigo, municipio?.Nombre, nivel);

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
    /// Lee la modalidad contra las tres que la base admite.
    /// </summary>
    /// <remarks>
    /// SE CAE A «presencial» PARA PODER SEGUIR COMPROBANDO, pero con un error apuntado: si
    /// devolviera nulo, las dos reglas de abajo —lugar y enlace— no se podrían evaluar y la fila
    /// saldría con un hallazgo donde debería tener tres.
    /// </remarks>
    private static string LeerModalidad(string? valor, Hallazgos hallazgos)
    {
        var limpio = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (limpio.Length == 0)
        {
            hallazgos.Error("modalidad_obligatoria", "modalidad",
                "Falta la modalidad. Debe ser «presencial», «virtual» o «mixta».");
            return "presencial";
        }
        if (Array.IndexOf(Modalidades, limpio) >= 0) return limpio;

        hallazgos.Error("modalidad_invalida", "modalidad",
            $"«{valor}» no es una modalidad. Debe ser «presencial», «virtual» o «mixta».");
        return "presencial";
    }

    private static DateOnly? LeerFecha(string? valor, string campo, Hallazgos hallazgos, bool obligatoria)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (obligatoria) hallazgos.Error("fecha_obligatoria", campo, "El evento necesita esta fecha.");
            return null;
        }
        if (DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var fecha)) return fecha;
        if (DateOnly.TryParse(valor, new CultureInfo("es-CO"), out fecha)) return fecha;

        hallazgos.Error("fecha_invalida", campo, $"«{valor}» no se entiende como fecha. Usa el formato AAAA-MM-DD.");
        return null;
    }

    private static DatosDeEvento Leer(string json, long? filaId = null) =>
        JsonSerializer.Deserialize<DatosDeEvento>(json, NucleoDeImportacion.Json)
            ?? throw new InvalidOperationException(
                $"La fila {filaId?.ToString(CultureInfo.InvariantCulture) ?? "?"} no tiene contenido legible.");
}
