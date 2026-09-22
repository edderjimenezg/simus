namespace PNMC.Api.ConsultaGuiada;

/// <summary>Qué consulta eligió el intérprete, y cómo llegó a ella.</summary>
/// <param name="ResueltoPor">
/// <c>regla</c> o <c>modelo_local</c>. Viaja hasta la pantalla porque quien lee una cifra tiene
/// derecho a saber si la eligió una regla revisable o un modelo.
/// </param>
public sealed record ConsultaElegida(ConsultaDelCatalogo Consulta, string ResueltoPor);

/*
   POR QUE NO HAY UNA RAMA DE «¿CUAL DE LAS DOS?».

   Se comprobó contra el banco de preguntas: preguntar cuando
   dos consultas puntúan parecido COSTABA UN ACIERTO y no arreglaba ninguno de los fallos. Con once
   consultas los empates casi no ocurren, y el único que ocurre —«resumen de mercados», donde empatan
   el resumen de mercados y el resumen general— es un caso en el que preguntar es peor que contestar:
   la pregunta se lee sin esfuerzo, y el catálogo ya declara que lo específico va antes que lo
   general.

   Se probaron tres umbrales —85 %, 95 % y solo empate— y los tres dieron lo mismo: 28 aciertos en
   vez de 29. Vuelve a merecer la pena cuando el catálogo crezca o cuando se extraigan entidades de
   la pregunta, porque entonces «festivales en el Huila» sí tendrá dos lecturas de verdad.
*/

/// <summary>
/// Quien decide qué consulta del catálogo responde a una pregunta escrita.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES LA COSTURA, Y ESA ES TODA SU RAZON DE SER.</b> El 12 de septiembre de 2026 se decidió
/// «determinista ahora, con costura para modelo». Lo determinista es
/// <see cref="InterpreteDeterministico"/>; el día que se mida un modelo local, se registra otra
/// implementación y no cambia ni una línea de las consultas, los permisos o las pantallas.
/// </para>
/// <para>
/// <b>EL INTERPRETE ELIGE, NO RESPONDE.</b> Devuelve una entrada del catálogo y nada más: no
/// redacta cifras, no ve la base y no puede inventar una consulta que no exista. Es la garantía
/// que hace que enchufar un modelo aquí siga siendo seguro —lo peor que puede hacer un modelo mal
/// calibrado es elegir la consulta equivocada, que se ve, y no fabricar un dato, que no se ve—.
/// </para>
/// <para>
/// <b>RECIBE LAS CONSULTAS QUE PUEDE ELEGIR</b>, ya filtradas por ámbito. Así una implementación
/// futura no tiene que conocer el modelo de permisos para no equivocarse: lo que no está en la
/// lista no se puede elegir.
/// </para>
/// </remarks>
public interface IInterpreteDePregunta
{
    /// <summary>Elige una consulta entre las disponibles, o <c>null</c> si ninguna responde.</summary>
    /// <param name="entidades">
    /// El sitio y el año que la pregunta nombra, ya resueltos contra DIVIPOLA. Se le entregan
    /// resueltos a propósito: reconocer territorios es un trabajo determinista con una fuente
    /// única, y no algo que cada implementación deba rehacer —ni algo que un modelo deba adivinar—.
    /// </param>
    Task<ConsultaElegida?> InterpretarAsync(
        string pregunta,
        IReadOnlyList<ConsultaDelCatalogo> disponibles,
        EntidadesDeLaPregunta entidades,
        CancellationToken ct);

    /// <summary>Si hay un modelo detrás. La pantalla lo dice y no lo adivina.</summary>
    bool ModeloLocalDisponible { get; }

    /// <summary>Cómo se llama el modo, para el contrato: <c>deterministico</c> o <c>modelo_local</c>.</summary>
    string Modo { get; }

    /// <summary>Qué hay detrás, en una frase que se le puede enseñar a quien pregunta.</summary>
    string Mensaje { get; }
}

/// <summary>
/// El intérprete por reglas: pistas escritas, revisables y sin modelo.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES DELIBERADAMENTE CONSERVADOR.</b> Prefiere no encontrar consulta a encontrar una parecida.
/// Una respuesta aproximada presentada con su tabla y su fuente es más dañina que un «no sé»: la
/// tabla le da autoridad a una cifra que no contesta lo que se preguntó.
/// </para>
/// <para>
/// GANA LA PRIMERA QUE COINCIDE, en el orden del catálogo. Ver <see cref="CatalogoDeConsultas"/>.
/// </para>
/// </remarks>
public sealed class InterpreteDeterministico : IInterpreteDePregunta
{
    public bool ModeloLocalDisponible => false;

    public string Modo => "deterministico";

    public string Mensaje =>
        "Las respuestas usan reglas y consultas agregadas aprobadas. No hay un modelo local habilitado.";

    public Task<ConsultaElegida?> InterpretarAsync(
        string pregunta,
        IReadOnlyList<ConsultaDelCatalogo> disponibles,
        EntidadesDeLaPregunta entidades,
        CancellationToken ct)
    {
        var palabras = CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta));

        // EL SITIO NO ELIGE CONSULTA, SOLO CAMBIA EL ALCANCE. Se probó lo contrario el 17 de
        // septiembre de 2026 —apartar de la competencia a las consultas que no saben acotarse— y
        // rompía más de lo que arreglaba: «¿qué información falta en los festivales del Huila?» y
        // «¿qué está pendiente en el Huila?» dejaban de llegar a su consulta y acababan en la tabla
        // de Festivales por municipio, que contesta otra pregunta. El defecto de fondo no estaba en
        // quién elegía sino en que las consultas no sabían acotarse; se arregló donde estaba.

        // GANA LA QUE MEJOR PUNTUA, NO LA PRIMERA. El orden del catálogo deja de decidir las
        // respuestas y pasa a ser solo el desempate: con la misma puntuación gana la de más arriba,
        // que es exactamente lo que hacía antes. Así añadir una consulta no puede robarle las
        // preguntas a otra sin que nadie lo vea, que es lo que ya había obligado a colocar a mano
        // «mercados por departamento» delante de «festivales por departamento».
        //
        // `OrderByDescending` es estable en .NET, así que ese desempate sale gratis.
        var mejor = disponibles
            .Select(consulta => (Consulta: consulta, Puntos: consulta.Puntuar(palabras, entidades)))
            .Where(par => par.Puntos > 0)
            .OrderByDescending(par => par.Puntos)
            .Select(par => par.Consulta)
            .FirstOrDefault();

        return Task.FromResult(mejor is null ? null : new ConsultaElegida(mejor, "regla"));
    }
}
