using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Ningún grupo de rutas se queda escrito y sin registrar.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTO PASO, Y COSTO UNA FUNCIONALIDAD ENTERA.</b>
/// <c>VersionesFestivalExternosEndpoints</c> —569 líneas, cinco rutas y los treinta y un campos del
/// modelo de versión— existía desde y <c>Program.cs</c> no la llamaba
/// nunca. El frontend sí las llamaba: <c>panel-organizacion.service.ts</c> las usa en cuatro
/// sitios, y recibía 404 en las cuatro. El panel de la organización no podía listar, crear ni
/// editar una versión, y de ahí venía la cifra que abrió esta revisión: UNA versión en 181
/// Festivales.
/// </para>
/// <para>
/// <b>POR QUE NINGUNA PRUEBA LO VEIA.</b> Cada grupo se prueba llamando a sus rutas a través del
/// arnés, que levanta la aplicación entera: si el grupo no está registrado, sus pruebas fallarían…
/// pero es que no tenía pruebas. Un fichero sin registrar y sin pruebas no le falla a nadie: solo
/// devuelve 404 a quien lo llame, y eso solo se ve caminando el sitio.
/// </para>
/// <para>
/// <b>ESTA PRUEBA NO LLAMA NINGUNA RUTA.</b> Compara dos listas: los métodos de extensión
/// <c>Map…Endpoints</c> que el ensamblado declara, y los que el texto de <c>Program.cs</c> invoca.
/// Es estructural a propósito —un grupo nuevo entra en la primera lista en cuanto se escribe, sin
/// que nadie tenga que acordarse de añadirlo aquí—.
/// </para>
/// </remarks>
public sealed class TodoGrupoDeRutasEstaRegistradoTests
{
    /// <summary>
    /// Los grupos que NO se registran a propósito, cada uno con su motivo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LA PRIMERA VERSION DE ESTA PRUEBA NO ADMITIA EXCEPCIONES, y esa premisa era falsa.</b> Un
    /// grupo puede estar retirado deliberadamente, y entonces no registrarlo es lo correcto. Con la
    /// lista vacía, la prueba empujaba a registrar lo que una decisión anterior había retirado, que
    /// es exactamente el error que se cometió.
    /// </para>
    /// <para>
    /// LA EXCEPCION SE ESCRIBE AQUI Y NO SE DEDUCE: si alguien retira un grupo, tiene que venir a
    /// decirlo con su motivo. Así un grupo nuevo que se olvide de registrar sigue poniendo la
    /// prueba en rojo, que es para lo que existe.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> RetiradosAProposito = new(StringComparer.Ordinal)
    {
        ["MapVersionesFestivalExternosEndpoints"] =
            "Retirado: transportaba fechas, financiación y otras "
            + "propiedades de una realización, que pertenecen a EdicionesFestival. La tabla "
            + "persiste para historial y propuestas, nunca como formulario externo de una Edición. "
            + "Ver la nota en Program.cs, junto a MapEdicionesFestivalExternosEndpoints.",
    };

    [Fact]
    public void TodoGrupoDeRutasDeclaradoSeRegistraEnProgram()
    {
        var declarados = MetodosDeRegistroDeclarados();
        Assert.NotEmpty(declarados);

        var programa = SinComentarios(LeerPrograma());
        var sinRegistrar = declarados
            .Where(nombre => !Regex.IsMatch(programa, $@"\.{Regex.Escape(nombre)}\s*\("))
            .Where(nombre => !RetiradosAProposito.ContainsKey(nombre))
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            sinRegistrar.Count == 0,
            "Hay grupos de rutas escritos y sin registrar en Program.cs. Sus rutas devuelven 404 a "
            + "quien las llame, y eso solo se ve caminando el sitio. Si alguno está retirado a "
            + "propósito, decláralo en `RetiradosAProposito` con su motivo: "
            + string.Join(", ", sinRegistrar));
    }

    /// <summary>
    /// Lo declarado como retirado existe todavía y sigue sin registrarse.
    /// </summary>
    /// <remarks>
    /// Sin esto, la lista de excepciones se convertiría en el sitio donde se esconden los olvidos:
    /// bastaría añadir un nombre para callar la prueba. Aquí se comprueba lo contrario —que el
    /// grupo existe y que de verdad no está registrado—, así que una entrada que sobra también
    /// pone la prueba en rojo.
    /// </remarks>
    [Fact]
    public void LasExcepcionesDeclaradasSiguenSiendoCiertas()
    {
        var declarados = MetodosDeRegistroDeclarados();
        var programa = SinComentarios(LeerPrograma());

        foreach (var (nombre, motivo) in RetiradosAProposito)
        {
            Assert.True(declarados.Contains(nombre),
                $"«{nombre}» está declarado como retirado y ya no existe en el ensamblado. "
                + "Retira también su entrada. Motivo que tenía: " + motivo);

            Assert.False(Regex.IsMatch(programa, $@"\.{Regex.Escape(nombre)}\s*\("),
                $"«{nombre}» está declarado como retirado y sin embargo Program.cs lo registra. "
                + "O se revierte el registro, o se retira la excepción con su motivo.");
        }
    }

    /// <summary>
    /// Los métodos de extensión que registran un grupo de rutas.
    /// </summary>
    /// <remarks>
    /// Se reconocen por su forma, que es la del proyecto entero: estáticos, públicos, sobre
    /// <see cref="RouteGroupBuilder"/> y con nombre <c>Map…Endpoints</c>. Buscar por nombre a secas
    /// recogería también los <c>Map…</c> de ASP.NET.
    /// </remarks>
    private static List<string> MetodosDeRegistroDeclarados() =>
        typeof(PNMC.Api.Endpoints.EstadosFestival).Assembly.GetTypes()
            .Where(tipo => tipo.IsAbstract && tipo.IsSealed && tipo.IsPublic)
            .SelectMany(tipo => tipo.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(metodo => metodo.Name.StartsWith("Map", StringComparison.Ordinal)
                && metodo.Name.EndsWith("Endpoints", StringComparison.Ordinal)
                && metodo.GetParameters().Length > 0
                && metodo.GetParameters()[0].ParameterType == typeof(RouteGroupBuilder))
            .Select(metodo => metodo.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Quita los comentarios antes de buscar las llamadas.
    /// </summary>
    /// <remarks>
    /// <b>SIN ESTO LA PRUEBA NO SERVIA, Y SE COMPROBO.</b> Al escribirla se verificó comentando el
    /// registro recién añadido: la prueba siguió en verde, porque la llamada seguía ahí —dentro del
    /// comentario— y el patrón la encontraba igual. Una guarda que no detecta el fallo que dice
    /// vigilar es peor que no tenerla: da confianza sin darla.
    /// </remarks>
    private static string SinComentarios(string codigo)
    {
        var sinBloques = Regex.Replace(codigo, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(sinBloques, @"//[^\n]*", string.Empty);
    }

    /// <summary>
    /// El texto de <c>Program.cs</c>, localizado subiendo desde el ejecutable.
    /// </summary>
    /// <remarks>
    /// Se lee el FICHERO y no se inspecciona la aplicación montada porque lo que hay que comprobar
    /// es que alguien escribió la llamada. Una aplicación montada por el arnés respondería que las
    /// rutas existen… solo si el registro está, que es justo lo que se pone en duda.
    /// </remarks>
    private static string LeerPrograma()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var candidato = Path.Combine(actual.FullName, "src", "PNMC.Api", "Program.cs");
            if (File.Exists(candidato)) return File.ReadAllText(candidato);
            actual = actual.Parent;
        }

        throw new FileNotFoundException("No se encontro Program.cs subiendo desde " + AppContext.BaseDirectory);
    }
}
