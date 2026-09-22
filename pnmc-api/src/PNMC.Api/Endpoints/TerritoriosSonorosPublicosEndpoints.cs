using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>Consulta pública de la capa conceptual. No inventa contenido: expone los espacios ya creados.</summary>
public static class TerritoriosSonorosPublicosEndpoints
{
    public static RouteGroupBuilder MapTerritoriosSonorosPublicosEndpoints(this RouteGroupBuilder group)
    {
        var publico = group.MapGroup("/publico/territorios-sonoros").WithTags("territorios-sonoros");
        publico.MapGet("", async (PnmcDbContext db, CancellationToken ct) =>
            Results.Ok(await Consulta(db).ToListAsync(ct))).AllowAnonymous();
        publico.MapGet("/{slug}", async (string slug, PnmcDbContext db, CancellationToken ct) =>
        {
            var ficha = (await Consulta(db).ToListAsync(ct)).SingleOrDefault(x => x.Slug == slug);
            return ficha is null ? Results.NotFound() : Results.Ok(ficha);
        }).AllowAnonymous();
        return group;
    }

    private static IQueryable<FichaTerritorioSonoroPublica> Consulta(PnmcDbContext db) =>
        from territorio in db.TerritoriosSonoros.AsNoTracking()
        join ficha in db.FichasConceptualesTerritoriosSonoros.AsNoTracking() on territorio.Id equals ficha.TerritorioSonoroId into fichas
        from ficha in fichas.DefaultIfEmpty()
        orderby territorio.Orden
        select new FichaTerritorioSonoroPublica(territorio.Id, territorio.Nombre, territorio.Slug, territorio.Orden,
            ficha == null ? null : ficha.DefinicionBreve, ficha == null ? null : ficha.DefinicionAmpliada,
            ficha == null ? null : ficha.DescripcionConceptual, ficha == null ? null : ficha.Caracteristicas,
            ficha == null ? null : ficha.RelacionTerritorial, ficha == null ? null : ficha.Contextos,
            ficha == null ? null : ficha.Ejemplos, ficha == null ? null : ficha.Fuentes,
            ficha == null ? null : ficha.RecursoVisualUrl, ficha == null ? null : ficha.TextoAlternativoRecurso);
}

public sealed record FichaTerritorioSonoroPublica(int Id, string Nombre, string Slug, int Orden,
    string? DefinicionBreve, string? DefinicionAmpliada, string? DescripcionConceptual,
    string? Caracteristicas, string? RelacionTerritorial, string? Contextos, string? Ejemplos,
    string? Fuentes, string? RecursoVisualUrl, string? TextoAlternativoRecurso);
