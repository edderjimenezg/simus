namespace PNMC.Contracts;

/// <summary>Una pregunta que el catálogo sabe resolver.</summary>
public sealed record ConsultaOfrecidaDto(string Id, string Rotulo, string Descripcion);

/// <summary>
/// Qué puede responder la Consulta Guiada para quien pregunta, y desde dónde.
/// </summary>
/// <remarks>
/// <b>EL CATALOGO DEPENDE DE QUIEN PREGUNTA.</b> Un funcionario del Programa ve las consultas
/// institucionales; una organización, las suyas. No es la misma lista filtrada en pantalla: es lo
/// que el servidor está dispuesto a responderle, y por eso viaja resuelto desde el servidor.
/// </remarks>
public sealed record EstadoDeConsultaGuiadaDto(
    bool Disponible,
    bool ModeloLocalDisponible,
    string Modo,
    string Mensaje,
    IReadOnlyList<ConsultaOfrecidaDto> Consultas,
    IReadOnlyList<string> Sugerencias);

/// <summary>
/// Desde dónde se pregunta, para que la respuesta hable de lo que la persona está mirando.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE.</b> Hasta la Consulta Guiada vivía en una sola
/// pantalla y respondía siempre de toda la operación. Preguntar «¿qué está incompleto?» mientras se
/// mira un Festival concreto devolvía el conteo de los seiscientos, que no es la pregunta que se
/// hizo.
/// </para>
/// <para>
/// <b>EL CONTEXTO NO AMPLIA PERMISOS, LOS ACOTA.</b> El servidor comprueba que quien pregunta pueda
/// ver eso: pedir el contexto de una organización ajena no abre sus datos, los cierra con un 403.
/// </para>
/// <para>
/// <b>Y SI LA CONSULTA NO SE PUEDE ACOTAR, SE DICE.</b> La respuesta siempre declara sobre qué se
/// calculó. Responder de toda la operación una pregunta hecha desde dentro de un Festival, sin
/// avisar, es peor que no acotar: parece una respuesta sobre ese Festival.
/// </para>
/// </remarks>
public sealed class ContextoDeConsultaDto
{
    /// <summary>La sección desde la que se pregunta, tal como la nombra la navegación.</summary>
    public string? Seccion { get; set; }

    /// <summary>La organización sobre la que se pregunta, si la pantalla está dentro de una.</summary>
    public int? OrganizacionId { get; set; }

    /// <summary>El Festival sobre el que se pregunta, si la pantalla está dentro de uno.</summary>
    public int? FestivalId { get; set; }
}

/// <summary>Entrada de la Consulta Guiada. El servidor recorta e interpreta esta pregunta.</summary>
public sealed class PreguntaDeConsultaGuiadaDto
{
    public string? Pregunta { get; set; }

    /// <summary>Desde dónde se pregunta. Opcional: sin él la respuesta es de toda la operación.</summary>
    public ContextoDeConsultaDto? Contexto { get; set; }
}

/// <summary>Tabla agregada que prueba de dónde salió una respuesta.</summary>
public sealed record TablaDeConsultaDto(
    string Titulo,
    string Fuente,
    string Alcance,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas);

/// <summary>Respuesta verificable de la Consulta Guiada.</summary>
/// <remarks>
/// <c>Contexto</c> DICE SOBRE QUE SE CALCULO —«Toda la operación», «Festival de la Candelaria»— y no
/// es decorativo: es la única forma de que quien lee la cifra sepa si responde a lo que preguntó.
/// </remarks>
public sealed record RespuestaDeConsultaGuiadaDto(
    string Respuesta,
    string ConsultaElegida,
    string ResueltoPor,
    DateTime GeneradoEn,
    TablaDeConsultaDto? Consulta,
    string Contexto);

/// <summary>Indicador agregado del tablero de análisis.</summary>
public sealed record IndicadorAnalisisAdministrativoDto(string Id, string Rotulo, int Total, string Alcance);

/// <summary>
/// Fotografía operacional que se puede mostrar aunque no haya clasificador local.
/// </summary>
/// <remarks>
/// SIGUE SIENDO ADMINISTRATIVO Y POR ESO CONSERVA EL NOMBRE: el tablero mira toda la operación del
/// Programa, no es una capacidad transversal como la Consulta Guiada.
/// </remarks>
public sealed record TableroAnalisisAdministrativoDto(
    DateTime GeneradoEn,
    IReadOnlyList<IndicadorAnalisisAdministrativoDto> Indicadores,
    IReadOnlyList<IReadOnlyList<string>> EstadosFestival,
    /// <summary>Mercados musicales por estado. Entro: un modulo del Ecosistema se ve en el tablero como Festivales.</summary>
    IReadOnlyList<IReadOnlyList<string>> EstadosMercado,
    IReadOnlyList<IReadOnlyList<string>> DepartamentosPrincipales);
