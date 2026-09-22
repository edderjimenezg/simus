using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Qué campos del perfil público de un Festival se pueden proponer, cómo se leen y cómo se escriben.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES EL HERMANO DE <see cref="CamposProponiblesDeMercado"/>, Y POR EL MISMO MOTIVO.</b> La tabla
/// de propuestas es genérica y guarda pares «campo, valor» como texto; si la correspondencia entre
/// esos nombres y las columnas la pusiera el cliente, quien propone elegiría qué columna escribir.
/// La lista cerrada está aquí: un campo que no esté no se guarda, no se compara y no se aplica.
/// </para>
/// <para>
/// <b>SON EXACTAMENTE LOS DIECISEIS QUE TENIA LA SOMBRA</b> —`PropuestasCambioFestival`—, más las
/// dos listas de catálogo. Ni uno más: ampliar lo que se puede proponer es una decisión de producto,
/// no un efecto colateral de cambiar dónde se guarda.
/// </para>
/// <para>
/// <b>SE LEEN Y SE ESCRIBEN SOBRE UNA VERSION, NO SOBRE EL FESTIVAL.</b> Es la diferencia con
/// Mercados: lo que el público lee de un Festival es su <c>VersionFestival</c> vigente, así que una
/// propuesta aprobada no reescribe el registro —crea la versión siguiente— y el «antes» de la
/// comparación es el de la versión sobre la que se propuso.
/// </para>
/// </remarks>
internal static class CamposProponiblesDeFestival
{
    /// <summary>Cómo se lee y cómo se escribe un campo proponible del perfil público.</summary>
    internal sealed record Campo(
        string SeccionId,
        string Etiqueta,
        Func<VersionFestivalRow, string?> Leer,
        Action<VersionFestivalRow, string?>? Escribir);

    /// <summary>Las dos listas de catálogo, que no son columnas de la versión.</summary>
    internal const string PracticasMusicales = "practicasMusicales";
    internal const string TerritoriosSonoros = "territoriosSonoros";

    internal static readonly IReadOnlyDictionary<string, Campo> Todos = new Dictionary<string, Campo>(StringComparer.Ordinal)
    {
        ["nombre"] = new("generales", "Nombre del festival",
            v => v.Nombre, (v, x) => v.Nombre = x ?? string.Empty),

        ["descripcion"] = new("generales", "Descripción",
            v => v.Descripcion, (v, x) => v.Descripcion = x),

        ["periodicidad"] = new("generales", "Periodicidad",
            v => v.Periodicidad, (v, x) => v.Periodicidad = x),

        ["periodicidadDetalle"] = new("generales", "Detalle de la periodicidad",
            v => v.PeriodicidadDetalle, (v, x) => v.PeriodicidadDetalle = x),

        ["nivelCobertura"] = new("territorio", "Alcance territorial",
            v => v.NivelCobertura, (v, x) => v.NivelCobertura = x ?? string.Empty),

        ["codigoDepartamento"] = new("territorio", "Departamento",
            v => v.CodigoDepartamento, (v, x) => v.CodigoDepartamento = x),

        ["codigoMunicipio"] = new("territorio", "Municipio",
            v => v.CodigoMunicipio, (v, x) => v.CodigoMunicipio = x),

        ["correoContacto"] = new("contacto", "Correo de contacto",
            v => v.CorreoContacto, (v, x) => v.CorreoContacto = x),

        ["telefonoContacto"] = new("contacto", "Teléfono de contacto",
            v => v.TelefonoContacto, (v, x) => v.TelefonoContacto = x),

        ["instagram"] = new("contacto", "Instagram",
            v => v.Instagram, (v, x) => v.Instagram = x),

        ["facebook"] = new("contacto", "Facebook",
            v => v.Facebook, (v, x) => v.Facebook = x),

        ["sitioWeb"] = new("contacto", "Sitio web",
            v => v.SitioWeb, (v, x) => v.SitioWeb = x),

        ["otroEnlace"] = new("contacto", "Otro enlace",
            v => v.OtroEnlace, (v, x) => v.OtroEnlace = x),

        ["observacionesContacto"] = new("contacto", "Observaciones de contacto",
            v => v.ObservacionesContacto, (v, x) => v.ObservacionesContacto = x),

        ["director"] = new("organizador", "Director o directora",
            v => v.Director, (v, x) => v.Director = x),

        ["tipoOrganizadorId"] = new("organizador", "Tipo de organización",
            v => Numero(v.TipoOrganizadorId), (v, x) => v.TipoOrganizadorId = Entero(x)),

        [PracticasMusicales] = new("practicas", "Prácticas musicales", _ => null, Escribir: null),
        [TerritoriosSonoros] = new("practicas", "Territorios sonoros", _ => null, Escribir: null),
    };

    /// <summary>El valor de un campo en una versión concreta, incluidas las dos listas.</summary>
    internal static async Task<string?> ValorEnLaVersionAsync(
        PnmcDbContext db, VersionFestivalRow version, string campoId, CancellationToken ct)
    {
        if (campoId == PracticasMusicales)
        {
            var ids = await db.ValoresAsync<PracticaMusicalDeRegistroRow>(
                Modulos.VersionesDeFestival, version.Id, ct);
            return Lista(ids);
        }

        if (campoId == TerritoriosSonoros)
        {
            var ids = await db.ValoresAsync<TerritorioSonoroDeRegistroRow>(
                Modulos.VersionesDeFestival, version.Id, ct);
            return Lista(ids);
        }

        return Todos.TryGetValue(campoId, out var campo) ? campo.Leer(version) : null;
    }

    /// <summary>
    /// Escribe en la versión NUEVA las prácticas o los territorios que dice el valor propuesto.
    /// </summary>
    /// <remarks>
    /// SE ESCRIBE LA LISTA ENTERA, no se añade: una propuesta sobre las prácticas dice cuáles quedan,
    /// y sumar las nuevas a las heredadas haría imposible quitar una.
    /// </remarks>
    internal static void EscribirLista(
        PnmcDbContext db, int versionId, string campoId, string? valor, DateTime ahora)
    {
        var pedidos = Identificadores(valor);

        if (campoId == PracticasMusicales)
        {
            db.Agregar<PracticaMusicalDeRegistroRow>(
                Modulos.VersionesDeFestival, versionId, pedidos, ahora);
            return;
        }

        if (campoId == TerritoriosSonoros)
        {
            db.Agregar<TerritorioSonoroDeRegistroRow>(
                Modulos.VersionesDeFestival, versionId, pedidos, ahora);
        }
    }

    internal static List<int> Identificadores(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(trozo => int.TryParse(trozo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Order()
                .ToList();

    internal static string? Lista(List<int> ids) => ids.Count == 0 ? null : string.Join(',', ids.Order());

    private static string? Numero(int? valor) =>
        valor is null ? null : valor.Value.ToString(CultureInfo.InvariantCulture);

    private static int? Entero(string? valor) =>
        int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
