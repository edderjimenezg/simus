using PNMC.Contracts;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>
/// Qué puede importar SIMUS por archivo, y en qué estado nace lo que llega así.
/// </summary>
/// <param name="Dominio">El identificador que viaja en la ruta: <c>festivales</c>.</param>
/// <param name="Etiqueta">Cómo se llama en pantalla.</param>
/// <param name="EstadoAlImportar">
/// El estado en el que nace un registro importado.
/// </param>
/// <param name="PorQueEseEstado">
/// La razón, escrita para quien la lea dentro de un año. No es documentación de cortesía: es lo que
/// impide que alguien «suba» este estado sin darse cuenta de lo que estaba protegiendo.
/// </param>
/// <param name="TipoDeRegistro">
/// Cómo se llama este registro en la bitácora de procedencia —<c>festival</c>, <c>organizacion</c>—.
/// </param>
/// <remarks>
/// <b>NO ES EL IDENTIFICADOR DEL DOMINIO.</b> El dominio es el plural de la ruta —<c>festivales</c>—
/// y el tipo de registro es el singular que ya usaba el registro de procedencia mucho antes de que
/// existiera la importación. Confundirlos escribe una procedencia que la base rechaza, y lo hace
/// dentro de la transacción de confirmación: la carga entera se cae sin escribir nada.
/// </remarks>
/// <param name="TablaDeAuditoria">
/// La tabla con la que se anota en la bitácora el registro creado —<c>Festivales</c>—.
/// </param>
/// <remarks>
/// <b>LA BITACORA APUNTA AL REGISTRO, NO AL LOTE.</b> El historial de un Festival se lee en su
/// propia ficha; anotarlo contra <c>LotesImportacion</c> dejaría ese Festival sin rastro de cómo
/// apareció, justo para quien lo esté mirando.
/// </remarks>
/// <param name="Campos">Los campos que el dominio sabe leer de un archivo.</param>
public sealed record CapacidadDeImportacion(
    string ModuloId,
    string Etiqueta,
    string EstadoAlImportar,
    string PorQueEseEstado,
    string TipoDeRegistro,
    string TablaDeAuditoria,
    IReadOnlyList<CampoDeImportacionDto> Campos);

/// <summary>
/// Lo que la Importación Asistida promete, escrito una vez y para todos los dominios.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE ESTE FICHERO.</b> Hasta la importación era «la
/// pantalla de Festivales»: sus reglas vivían repartidas en comentarios dentro de un endpoint de
/// casi quinientas líneas, y la más importante —que nada importado se publica solo— era una sola
/// asignación, <c>StatusCode = Borrador</c>, sin nada que impidiera cambiarla. El plan de
/// consolidación la declaró <b>capacidad transversal</b>; una capacidad necesita un contrato, y
/// este es el contrato.
/// </para>
/// <para>
/// <b>LAS CUATRO REGLAS, y ninguna es negociable por un dominio nuevo:</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>Previsualizar no escribe en el destino.</b> Se guarda el plan —el lote, sus filas y sus
///     hallazgos— para poder decidir después sobre exactamente lo mismo que se vio, pero ni un
///     Festival ni una organización existen hasta que alguien confirma.
///   </item>
///   <item>
///     <b>Nada importado se publica ni se activa solo.</b> Cada dominio declara en qué estado nace
///     lo suyo y ese estado tiene que exigir una decisión humana posterior.
///     <c>ImportacionAsistidaTests</c> lo comprueba dominio por dominio: una capacidad que depende
///     de que nadie toque una línea no es una garantía.
///   </item>
///   <item>
///     <b>Todo lo importado dice que llegó en una carga.</b> La procedencia se anota como
///     <c>importacion</c>, que no es lo mismo que <c>administrativo</c> aunque lo ejecute un
///     funcionario: distinguirlo es lo que permite responder después «esto lo escribió alguien»
///     frente a «esto llegó en un archivo».
///   </item>
///   <item>
///     <b>Una confirmación por plan.</b> El lote guarda la huella de lo que se previsualizó y la
///     clave de idempotencia de quien confirmó; si el mundo cambió entremedias, se rechaza y se
///     vuelve a previsualizar en vez de escribir sobre un plan que ya no describe la realidad.
///   </item>
/// </list>
/// </remarks>
public static class ReglasDeImportacion
{
    /// <summary>El plan está guardado y nadie ha decidido nada todavía.</summary>
    public const string Previsualizado = "previsualizado";

    /// <summary>Reserva momentánea mientras se escribe. Impide dos confirmaciones a la vez.</summary>
    public const string Aplicando = "aplicando";

    /// <summary>Se escribió lo que se decidió escribir.</summary>
    public const string Aplicado = "aplicado";

    /// <summary>Pasó su plazo de retención sin que nadie decidiera.</summary>
    public const string Expirado = "expirado";

    /// <summary>Los estados que un lote puede tener.</summary>
    public static readonly string[] EstadosDeLote = [Previsualizado, Aplicando, Aplicado, Expirado];

    /// <summary>
    /// Si ese estado deja el registro a la vista del público sin que nadie decida.
    /// </summary>
    /// <remarks>
    /// LA LISTA ES DE ESTADOS PUBLICOS Y NO DE ESTADOS PERMITIDOS, a propósito: un dominio nuevo con
    /// un vocabulario propio no queda bloqueado por no estar en una lista blanca, pero tampoco puede
    /// colar «publicado» ni «activa» por la puerta de atrás.
    /// </remarks>
    public static bool PublicaSinDecision(string estado) =>
        estado is "publicado" or "publicada" or "aprobado" or "activa" or "activo" or "validada";
}
