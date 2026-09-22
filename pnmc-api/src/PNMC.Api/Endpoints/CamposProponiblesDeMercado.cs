using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Qué campos de un mercado se pueden proponer, cómo se leen y cómo se aplican.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL CATALOGO VIVE EN EL SERVIDOR, Y ESA ES LA DEFENSA.</b> La tabla de propuestas es genérica y
/// guarda pares «campo, valor» como texto; si la correspondencia entre esos nombres y las columnas
/// del mercado la pusiera el cliente, quien propone elegiría qué columna escribir. Aquí está la
/// lista cerrada: un campo que no esté no se guarda, no se compara y no se aplica.
/// </para>
/// <para>
/// <b>LOS NOMBRES SON LOS MISMOS QUE LOS DE LA REVISION POR CAMPOS.</b> «correoMercado» es el mismo
/// campo cuando se propone cambiarlo y cuando el Programa pide ajustarlo, así que las dos cosas se
/// pueden enseñar juntas al lado del campo sin traducción en medio.
/// </para>
/// <para>
/// <b>LOS QUE APUNTAN A OTRA TABLA VIAJAN COMO IDENTIFICADORES</b> —el alcance, la modalidad, el
/// festival, y las dos listas de catálogo— y se acompañan de una etiqueta legible que se resuelve al
/// enseñarlos. Guardar el nombre sería guardar algo que puede cambiar en su catálogo; guardar el id
/// es guardar a qué apunta.
/// </para>
/// </remarks>
internal static class CamposProponiblesDeMercado
{
    /// <summary>Cómo se lee y cómo se escribe un campo proponible.</summary>
    /// <param name="SeccionId">Dónde está en la ficha, para agrupar la comparación como se agrupa la pantalla.</param>
    /// <param name="Etiqueta">Su rótulo, el mismo que enseña la ficha.</param>
    /// <param name="Leer">El valor actual del registro, como texto comparable.</param>
    /// <param name="Escribir">
    /// Aplica el valor propuesto sobre la fila. Nulo en los campos que no son columnas de la fila
    /// —las dos listas de catálogo—, que se aplican aparte porque son filas de otra tabla.
    /// </param>
    internal sealed record Campo(
        string SeccionId,
        string Etiqueta,
        Func<MercadoRow, string?> Leer,
        Action<MercadoRow, string?>? Escribir);

    /// <summary>Las dos listas de catálogo, que no son columnas de la fila del mercado.</summary>
    internal const string PracticasMusicales = "practicasMusicales";
    internal const string TerritoriosSonoros = "territoriosSonoros";

    /// <summary>
    /// La lista cerrada, por identificador de campo.
    /// </summary>
    /// <remarks>
    /// <b>NO ESTAN EL ESTADO NI LA ORGANIZACION RESPONSABLE.</b> Publicar, retirar o cambiar de
    /// organización no son «cambios sobre lo publicado»: son decisiones con su propio circuito, y
    /// dejarlas entrar por aquí sería abrir una segunda puerta a lo que aquellas protegen.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, Campo> Todos = new Dictionary<string, Campo>(StringComparer.Ordinal)
    {
        ["nombre"] = new("identificacion", "Nombre del mercado",
            m => m.Nombre,
            (m, v) => m.Nombre = v ?? string.Empty),

        ["descripcion"] = new("identificacion", "Descripción",
            m => m.Descripcion,
            (m, v) => m.Descripcion = v),

        ["alcance"] = new("identificacion", "Alcance",
            m => Numero(m.AlcanceMercadoId),
            (m, v) => m.AlcanceMercadoId = Entero(v)),

        ["modalidad"] = new("identificacion", "Modalidad",
            m => Numero(m.ModalidadMercadoId),
            (m, v) => m.ModalidadMercadoId = Entero(v)),

        ["periodicidad"] = new("identificacion", "Periodicidad",
            m => m.Periodicidad,
            (m, v) => m.Periodicidad = v),

        ["periodicidadDetalle"] = new("identificacion", "Detalle de la periodicidad",
            m => m.PeriodicidadDetalle,
            (m, v) => m.PeriodicidadDetalle = v),

        ["nivelCobertura"] = new("territorio", "Nivel de cobertura",
            m => m.NivelCobertura,
            (m, v) => m.NivelCobertura = v ?? string.Empty),

        ["codigoDepartamento"] = new("territorio", "Departamento",
            m => m.CodigoDepartamento,
            (m, v) => m.CodigoDepartamento = v),

        ["codigoMunicipio"] = new("territorio", "Municipio",
            m => m.CodigoMunicipio,
            (m, v) => m.CodigoMunicipio = v),

        ["lugarEspecifico"] = new("territorio", "Lugar específico",
            m => m.LugarEspecifico,
            (m, v) => m.LugarEspecifico = v),

        ["correoMercado"] = new("contacto", "Correo de contacto",
            m => m.CorreoMercado,
            (m, v) => m.CorreoMercado = v),

        ["telefonoMercado"] = new("contacto", "Teléfono",
            m => m.TelefonoMercado,
            (m, v) => m.TelefonoMercado = v),

        ["sitioWebMercado"] = new("contacto", "Página web",
            m => m.SitioWebMercado,
            (m, v) => m.SitioWebMercado = v),

        ["instagramMercado"] = new("contacto", "Instagram",
            m => m.InstagramMercado,
            (m, v) => m.InstagramMercado = v),

        ["facebookMercado"] = new("contacto", "Facebook",
            m => m.FacebookMercado,
            (m, v) => m.FacebookMercado = v),

        ["otroEnlaceMercado"] = new("contacto", "Otro enlace",
            m => m.OtroEnlaceMercado,
            (m, v) => m.OtroEnlaceMercado = v),

        ["festival"] = new("relaciones", "En el marco de",
            m => Numero(m.FestivalId),
            (m, v) =>
            {
                var festival = Entero(v);
                m.FestivalId = festival;
                // EL SI Y EL CUAL VAN JUNTOS. Dejar el indicador en cierto sin festival, o al revés,
                // deja la ficha diciendo que el mercado ocurre dentro de algo que no nombra.
                m.SeRealizaEnElMarcoDeUnFestival = festival is not null;
            }),

        [PracticasMusicales] = new("relaciones", "Prácticas musicales",
            _ => null, Escribir: null),

        [TerritoriosSonoros] = new("relaciones", "Territorios sonoros",
            _ => null, Escribir: null),
    };

    /// <summary>El valor actual de un campo, incluidas las dos listas de catálogo.</summary>
    internal static async Task<string?> ValorActualAsync(
        PnmcDbContext db, MercadoRow mercado, string campoId, CancellationToken ct)
    {
        if (campoId == PracticasMusicales)
        {
            var ids = await db.MercadosPracticasMusicales.AsNoTracking()
                .Where(r => r.MercadoId == mercado.Id).Select(r => r.PracticaMusicalId).ToListAsync(ct);
            return Lista(ids);
        }

        if (campoId == TerritoriosSonoros)
        {
            var ids = await db.MercadosTerritoriosSonoros.AsNoTracking()
                .Where(r => r.MercadoId == mercado.Id).Select(r => r.TerritorioSonoroId).ToListAsync(ct);
            return Lista(ids);
        }

        return Todos.TryGetValue(campoId, out var campo) ? campo.Leer(mercado) : null;
    }

    /// <summary>
    /// Sustituye las prácticas o los territorios del mercado por los que dice el valor propuesto.
    /// </summary>
    /// <remarks>
    /// SE REEMPLAZA LA LISTA ENTERA, no se añade: una propuesta sobre las prácticas dice cuáles
    /// quedan, y sumar las nuevas a las viejas haría imposible quitar una.
    /// </remarks>
    internal static async Task AplicarListaAsync(
        PnmcDbContext db, MercadoRow mercado, string campoId, string? valor, DateTime ahora, CancellationToken ct)
    {
        var pedidos = Identificadores(valor);

        if (campoId == PracticasMusicales)
        {
            var actuales = await db.MercadosPracticasMusicales
                .Where(r => r.MercadoId == mercado.Id).ToListAsync(ct);
            db.MercadosPracticasMusicales.RemoveRange(actuales);
            db.MercadosPracticasMusicales.AddRange(pedidos.Select(id => new MercadoPracticaMusicalRow
            {
                MercadoId = mercado.Id, PracticaMusicalId = id, FechaCreacion = ahora,
            }));
            return;
        }

        if (campoId == TerritoriosSonoros)
        {
            var actuales = await db.MercadosTerritoriosSonoros
                .Where(r => r.MercadoId == mercado.Id).ToListAsync(ct);
            db.MercadosTerritoriosSonoros.RemoveRange(actuales);
            db.MercadosTerritoriosSonoros.AddRange(pedidos.Select(id => new MercadoTerritorioSonoroRow
            {
                MercadoId = mercado.Id, TerritorioSonoroId = id, FechaCreacion = ahora,
            }));
        }
    }

    /// <summary>Los identificadores de una lista, sin repetidos y en orden.</summary>
    internal static List<int> Identificadores(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(trozo => int.TryParse(trozo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Order()
                .ToList();

    private static string? Lista(List<int> ids) => ids.Count == 0 ? null : string.Join(',', ids.Order());

    private static string? Numero(int? valor) =>
        valor is null ? null : valor.Value.ToString(CultureInfo.InvariantCulture);

    private static int? Entero(string? valor) =>
        int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
