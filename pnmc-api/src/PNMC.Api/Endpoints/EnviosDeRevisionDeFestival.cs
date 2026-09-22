using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Captura y compara los envíos del registro permanente de un Festival.
/// </summary>
/// <remarks>
/// La instantánea vive fuera de <c>VersionesFestival</c>: aquella tabla conserva versiones del
/// perfil público; esta evidencia pertenece al trámite institucional. Se guarda en
/// <c>EnviosDeRevision</c>, que es de todos los procesos; lo propio de Festival es saber QUE
/// dieciséis campos componen la instantánea, y eso es lo que hace esta clase.
/// </remarks>
internal static class EnviosDeRevisionDeFestival
{
    /// <summary>El módulo con el que estos envíos se guardan y se recuperan.</summary>
    internal const string Modulo = Modulos.Festivales;

    private sealed record Campo(string CampoId, string SeccionId, string CampoEtiqueta, string? Valor);
    private sealed record Instantanea(int Esquema, IReadOnlyList<Campo> Campos);

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static async Task RegistrarAsync(
        PnmcDbContext dbContext,
        FestivalRow festival,
        int usuarioRemitenteId,
        string estadoAnterior,
        DateTime fecha,
        CancellationToken cancellationToken)
    {
        if (festival.OrganizacionPrincipalId is not int organizacionId)
            throw new InvalidOperationException("No se puede registrar un envío sin organización responsable.");

        var registroId = festival.Id.ToString(CultureInfo.InvariantCulture);
        var numeroEnvio = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
            .CountAsync(item => item.ModuloId == Modulo
                && item.RegistroId == registroId
                && item.Accion == "FestivalEnviadoARevision", cancellationToken) + 1;

        // LA REVISION VIVA, EN LA TABLA GENERICA. Desde las revisiones
        // de Festival viven en `RevisionesDeRegistro`, identificadas por módulo + registro como las
        // de cualquier otro proceso.
        var revisionOrigenId = await dbContext.RevisionesDeRegistro.AsNoTracking()
            .Where(item => item.ModuloId == Modulo && item.RegistroId == registroId && item.Estado == "enviada")
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var instantanea = await TomarAsync(dbContext, festival, cancellationToken);
        dbContext.EnviosDeRevision.Add(new EnvioDeRevisionRow
        {
            ModuloId = Modulo,
            RegistroId = registroId,
            NumeroEnvio = numeroEnvio,
            UsuarioRemitenteId = usuarioRemitenteId,
            OrganizacionId = organizacionId,
            RevisionOrigenId = revisionOrigenId,
            EstadoAnterior = EstadosFestival.DesdeContrato(estadoAnterior) ?? estadoAnterior,
            DatosJson = JsonSerializer.Serialize(instantanea, OpcionesJson),
            FechaEnvio = fecha,
        });
    }

    public static async Task<ComparacionEnviosFestivalDto?> CompararActualAsync(
        PnmcDbContext dbContext,
        int festivalId,
        CancellationToken cancellationToken)
    {
        var clave = festivalId.ToString(CultureInfo.InvariantCulture);
        var envios = await dbContext.EnviosDeRevision.AsNoTracking()
            .Where(item => item.ModuloId == Modulo && item.RegistroId == clave)
            .OrderByDescending(item => item.NumeroEnvio)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (envios.Count == 0) return null;

        var actual = envios[0];
        var camposActuales = Leer(actual.DatosJson);
        var anterior = envios.Count > 1 ? envios[1] : null;
        var camposAnteriores = anterior is null ? new Dictionary<string, Campo>(StringComparer.Ordinal) : Leer(anterior.DatosJson);
        var comparacionParcial = false;

        var ajustes = actual.RevisionOrigenId is long revisionId
            ? await dbContext.RevisionesDeRegistroObservaciones.AsNoTracking()
                .Where(item => item.IdRevision == revisionId)
                .Select(item => new { item.CampoId, item.SeccionId, item.CampoEtiqueta, item.ValorObservado })
                .ToListAsync(cancellationToken)
            : [];
        var camposAjustados = ajustes.Select(item => item.CampoId).ToHashSet(StringComparer.Ordinal);

        // Los Festivales que ya habían sido revisados cuando se instaló esta tabla no tienen una
        // instantánea anterior. Sus observaciones sí copiaron el valor revisado campo por campo.
        // Se recupera únicamente esa evidencia y se declara comparación parcial: nunca se inventan
        // los demás valores del envío anterior.
        if (anterior is null && actual.NumeroEnvio > 1 && ajustes.Count > 0)
        {
            comparacionParcial = true;
            foreach (var ajuste in ajustes)
            {
                camposAnteriores[ajuste.CampoId] = new Campo(
                    ajuste.CampoId, ajuste.SeccionId, ajuste.CampoEtiqueta, ajuste.ValorObservado);
            }
        }

        var cambios = new List<CambioEntreEnviosFestivalDto>();
        foreach (var campoActual in camposActuales.Values)
        {
            if (!camposAnteriores.TryGetValue(campoActual.CampoId, out var campoAnterior)) continue;
            if (Iguales(campoAnterior.Valor, campoActual.Valor)) continue;
            cambios.Add(new CambioEntreEnviosFestivalDto(
                campoActual.CampoId,
                campoActual.SeccionId,
                campoActual.CampoEtiqueta,
                campoAnterior.Valor,
                campoActual.Valor,
                camposAjustados.Contains(campoActual.CampoId)));
        }

        var esPrimerEnvio = actual.NumeroEnvio == 1;
        var ajustesAtendidos = cambios.Count(item => item.RespondeAjusteSugerido);
        return new ComparacionEnviosFestivalDto(
            actual.NumeroEnvio,
            actual.FechaEnvio,
            anterior?.NumeroEnvio ?? (actual.NumeroEnvio > 1 ? actual.NumeroEnvio - 1 : null),
            anterior?.FechaEnvio,
            esPrimerEnvio,
            comparacionParcial,
            camposActuales.Count,
            cambios.Count,
            ajustesAtendidos,
            cambios.Count - ajustesAtendidos,
            cambios.OrderBy(item => item.SeccionId).ThenBy(item => item.CampoEtiqueta).ToList());
    }

    private static async Task<Instantanea> TomarAsync(
        PnmcDbContext dbContext,
        FestivalRow festival,
        CancellationToken cancellationToken)
    {
        var practicas = await dbContext.De<PracticaMusicalDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
            .Join(dbContext.PracticasMusicales.AsNoTracking(), relacion => relacion.ValorId, catalogo => catalogo.Id,
                (_, catalogo) => catalogo.Nombre)
            .OrderBy(nombre => nombre)
            .ToListAsync(cancellationToken);
        var territorios = await dbContext.De<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
            .Join(dbContext.TerritoriosSonoros.AsNoTracking(), relacion => relacion.ValorId, catalogo => catalogo.Id,
                (_, catalogo) => catalogo.Nombre)
            .OrderBy(nombre => nombre)
            .ToListAsync(cancellationToken);

        return new Instantanea(1,
        [
            new("festival.nombre", "generales", "Nombre del Festival", Limpiar(festival.Name)),
            new("festival.periodicidad", "generales", "Periodicidad", Limpiar(festival.Periodicidad)),
            new("festival.descripcion", "generales", "Descripción del Festival", Limpiar(festival.Description)),
            new("festival.nivelCobertura", "generales", "Alcance territorial", Limpiar(festival.CoverageLevel)),
            new("festival.codigoDepartamento", "generales", "Departamento", Limpiar(festival.DepartmentCode)),
            new("festival.codigoMunicipio", "generales", "Municipio", Limpiar(festival.MunicipalityCode)),
            new("festival.periodicidadDetalle", "generales", "Detalle de la periodicidad", Limpiar(festival.PeriodicidadDetalle)),
            new("festival.correoContacto", "contacto-festival", "Correo de contacto", Limpiar(festival.ContactEmail)),
            new("festival.telefonoCelular", "contacto-festival", "Teléfono celular", Limpiar(festival.ContactPhone)),
            new("festival.instagram", "contacto-festival", "Instagram", Limpiar(festival.InstagramUrl)),
            new("festival.facebook", "contacto-festival", "Facebook", Limpiar(festival.FacebookUrl)),
            new("festival.paginaWeb", "contacto-festival", "Página web", Limpiar(festival.WebsiteUrl)),
            new("festival.otroEnlace", "contacto-festival", "Otro enlace", Limpiar(festival.OtherUrl)),
            new("festival.observacionesContacto", "contacto-festival", "Observaciones de contacto", Limpiar(festival.ObservacionesContacto)),
            new("festival.practicasMusicales", "musica-festival", "Prácticas musicales", Lista(practicas)),
            new("festival.territoriosSonoros", "musica-festival", "Territorios sonoros", Lista(territorios)),
        ]);
    }

    private static Dictionary<string, Campo> Leer(string json)
    {
        try
        {
            var instantanea = JsonSerializer.Deserialize<Instantanea>(json, OpcionesJson);
            return (instantanea?.Campos ?? []).ToDictionary(item => item.CampoId, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, Campo>(StringComparer.Ordinal);
        }
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    private static string? Lista(List<string> valores) => valores.Count == 0 ? null : string.Join(", ", valores);
    private static bool Iguales(string? izquierda, string? derecha) =>
        string.Equals(Limpiar(izquierda), Limpiar(derecha), StringComparison.Ordinal);
}
