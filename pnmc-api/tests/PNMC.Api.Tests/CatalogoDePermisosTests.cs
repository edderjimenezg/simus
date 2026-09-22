using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El catálogo de permisos existe en la base que fabrica el arnés, y dice <b>exactamente</b> lo
/// mismo que el guion de esquema.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO U8 DEL PLAN DE CONSTRUCCIÓN, cerrado. La vía de SQLite fabrica su base con
/// <c>EnsureCreated</c>, que crea las tablas del modelo y <b>no ejecuta ni una línea de
/// <c>schema/</c></b>. Antes de esto, <c>dbo.Permisos</c> y <c>dbo.RolesPermisos</c> existían
/// vacías en esa vía. El plan lo llama «el peor de los tres casos» y tiene razón: el día que una
/// guarda consulte el catálogo, una prueba de permisos <b>pasaría porque no hay permisos</b>, no
/// porque el reparto sea correcto, y nada se pondría rojo.
/// </para>
/// <para>
/// LA SEGUNDA PRUEBA ES LA QUE DE VERDAD IMPORTA, y no es la de «no está vacío». Sembrar el
/// catálogo en C# crea una <b>segunda copia</b> de una lista que ya vive en el fichero
/// <c>.sql</c>, y dos copias de una lista es como se llega a que una se quede atrás sin que nadie
/// lo note. Aquí se lee el fichero de verdad y se comparan las dos, código a código: si alguien
/// concede un permiso en el guion y no en la referencia de pruebas —o al revés—, esto se pone rojo
/// y nombra la diferencia.
/// </para>
/// <para>
/// SE COMPARA CONTRA EL FICHERO, NO CONTRA LA BASE. Una comparación contra
/// <c>PNMC_LOCAL</c> mediría lo que alguien dejó escrito allí a mano, que es justo lo que un
/// catálogo de permisos no debe hacer. El fichero es la fuente; la base es su consecuencia.
/// </para>
/// <para>
/// HOY NINGUNA GUARDA CONSULTA ESTAS TABLAS. El API sigue decidiendo con listas de roles escritas
/// en el código. Esto es el inventario que hace que el carril rápido sea un espejo fiel del
/// esquema real, para que el día que una guarda empiece a leerlo la suite mida algo.
/// </para>
/// </remarks>
public sealed class CatalogoDePermisosTests : IClassFixture<TestWebApplicationFactory>
{
    private const string GuionDeEsquema = "V20260824_01__usuarios_roles_y_permisos.sql";

    private readonly TestWebApplicationFactory _factory;

    public CatalogoDePermisosTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task La_Base_Del_Arnes_Trae_El_Catalogo_Y_Su_Reparto()
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var permisos = await db.Permisos.AsNoTracking().Select(permiso => permiso.Codigo).ToListAsync();
        var reparto = await db.RolesPermisos.AsNoTracking().CountAsync();

        Assert.NotEmpty(permisos);
        Assert.NotEqual(0, reparto);

        // Y CON LOS NÚMEROS EXACTOS, no solo «algo hay». Un `NotEmpty` a secas seguiría en verde
        // si la siembra dejara un permiso de quince, que es una forma de vacío más difícil de ver.
        //
        // LOS NÚMEROS VAN ESCRITOS, Y NO COMO `Referencia.Permisos.Length`. Se midió: con la
        // longitud de la propia referencia, quitarle una concesión dejaba esta prueba EN VERDE
        // —comparaba 23 contra 23— y solo se ponía roja la que lee el fichero `.sql`. Una
        // comparación de una lista contra su propia longitud no puede detectar que la lista
        // encogió; hace falta un número que no venga de ella.
        //
        // Si el catálogo crece a propósito, estas dos líneas se actualizan a mano y el cambio se
        // ve en la revisión, que es exactamente lo que se quiere de un catálogo de permisos.
        Assert.Equal(15, CatalogoDePermisosDeReferencia.Permisos.Length);
        Assert.Equal(24, CatalogoDePermisosDeReferencia.Reparto.Length);
        Assert.Equal(15, permisos.Count);
        Assert.Equal(24, reparto);

        // El reparto apunta a filas que existen: sin esto, quince concesiones podrían señalar a
        // identificadores inventados y la cuenta seguiría cuadrando.
        var huerfanas = await db.RolesPermisos.AsNoTracking()
            .Where(concesion => !db.Permisos.Any(permiso => permiso.Id == concesion.PermisoId)
                || !db.Roles.Any(rol => rol.Id == concesion.RoleId))
            .CountAsync();
        Assert.Equal(0, huerfanas);
    }

    [Fact]
    public void La_Referencia_De_Pruebas_Dice_Lo_Mismo_Que_El_Guion_De_Esquema()
    {
        var ruta = Path.Combine(RutasDelRepositorio.Esquema(), GuionDeEsquema);
        Assert.True(File.Exists(ruta), $"No se encontro el guion de esquema en {ruta}.");

        var guion = File.ReadAllText(ruta);

        var permisosDelGuion = CodigosDeLaSiembraDePermisos(guion);
        var permisosDeReferencia = CatalogoDePermisosDeReferencia.Permisos
            .Select(permiso => permiso.Codigo)
            .OrderBy(codigo => codigo, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(permisosDeReferencia, permisosDelGuion);

        var repartoDelGuion = ConcesionesDelGuion(guion);
        var repartoDeReferencia = CatalogoDePermisosDeReferencia.Reparto
            .Select(concesion => $"{concesion.Rol}|{concesion.Codigo}")
            .OrderBy(linea => linea, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(repartoDeReferencia, repartoDelGuion);
    }

    /// <summary>
    /// Los quince códigos del <c>MERGE dbo.Permisos</c>, leídos del fichero.
    /// </summary>
    /// <remarks>
    /// Se acota al bloque del <c>MERGE</c> y no se barre el fichero entero: los mismos códigos
    /// aparecen más abajo en el reparto y en varios comentarios, y contarlos todos daría una lista
    /// inflada que casaría con cualquier cosa.
    /// </remarks>
    private static List<string> CodigosDeLaSiembraDePermisos(string guion)
    {
        var bloque = Bloque(guion, "MERGE dbo.Permisos AS destino", ") AS origen (CodigoPermiso");

        return Regex.Matches(bloque, @"\(N'(?<codigo>[a-z_]+\.[a-z_]+)',\s*N'")
            .Select(coincidencia => coincidencia.Groups["codigo"].Value)
            .OrderBy(codigo => codigo, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Las concesiones del <c>MERGE dbo.RolesPermisos</c>, como <c>rol|codigo</c>.</summary>
    private static List<string> ConcesionesDelGuion(string guion)
    {
        var bloque = Bloque(guion, "MERGE dbo.RolesPermisos AS destino", ") AS reparto (NombreRol");

        return Regex.Matches(bloque, @"\(N'(?<rol>[a-z_]+)',\s*N'(?<codigo>[a-z_]+\.[a-z_]+)'\)")
            .Select(coincidencia => $"{coincidencia.Groups["rol"].Value}|{coincidencia.Groups["codigo"].Value}")
            .OrderBy(linea => linea, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// El trozo del fichero entre dos marcas, con un mensaje útil si alguna no aparece.
    /// </summary>
    /// <remarks>
    /// Que una marca desaparezca es un resultado, no un accidente que haya que tolerar: significa
    /// que el guion se reescribió y que esta prueba dejó de estar leyendo lo que cree leer. Fallar
    /// aquí es mejor que devolver una lista vacía y comparar dos nadas.
    /// </remarks>
    private static string Bloque(string guion, string desde, string hasta)
    {
        var inicio = guion.IndexOf(desde, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"El guion ya no contiene «{desde}»; esta prueba dejo de leer lo que cree leer.");

        var fin = guion.IndexOf(hasta, inicio, StringComparison.Ordinal);
        Assert.True(fin > inicio, $"El guion ya no contiene «{hasta}» despues de «{desde}».");

        return guion[inicio..fin];
    }
}
