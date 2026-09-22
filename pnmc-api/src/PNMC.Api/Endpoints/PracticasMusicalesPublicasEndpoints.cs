using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;
namespace PNMC.Api.Endpoints;

public static class PracticasMusicalesPublicasEndpoints
{
    public static RouteGroupBuilder MapPracticasMusicalesPublicasEndpoints(this RouteGroupBuilder group)
    {
        var publico = group.MapGroup("/publico/practicas-musicales").WithTags("practicas-musicales");
        publico.MapGet("", async (PnmcDbContext db, CancellationToken ct) => Results.Ok(await Consulta(db).ToListAsync(ct))).AllowAnonymous();
        publico.MapGet("/{slug}", async (string slug, PnmcDbContext db, CancellationToken ct) =>
        { var ficha = (await Consulta(db).ToListAsync(ct)).SingleOrDefault(x => x.Slug == slug); return ficha is null ? Results.NotFound() : Results.Ok(ficha); }).AllowAnonymous();
        return group;
    }
    private static IQueryable<FichaPracticaMusicalPublica> Consulta(PnmcDbContext db) =>
        from practica in db.PracticasMusicales.AsNoTracking()
        join ficha in db.FichasConceptualesPracticasMusicales.AsNoTracking() on practica.Id equals ficha.PracticaMusicalId into fichas
        from ficha in fichas.DefaultIfEmpty()
        orderby practica.Orden
        select new FichaPracticaMusicalPublica(practica.Id, practica.Nombre, practica.Slug, practica.Orden,
            ficha == null ? null : ficha.DefinicionBreve, ficha == null ? null : ficha.DefinicionAmpliada,
            ficha == null ? null : ficha.DescripcionConceptual, ficha == null ? null : ficha.Caracteristicas,
            ficha == null ? null : ficha.Contextos, ficha == null ? null : ficha.Ejemplos, ficha == null ? null : ficha.Fuentes,
            ficha == null ? null : ficha.RecursoVisualUrl, ficha == null ? null : ficha.TextoAlternativoRecurso);
}
public sealed record FichaPracticaMusicalPublica(int Id, string Nombre, string Slug, int Orden, string? DefinicionBreve,
    string? DefinicionAmpliada, string? DescripcionConceptual, string? Caracteristicas, string? Contextos, string? Ejemplos,
    string? Fuentes, string? RecursoVisualUrl, string? TextoAlternativoRecurso);
