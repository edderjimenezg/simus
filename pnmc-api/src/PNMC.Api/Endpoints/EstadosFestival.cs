namespace PNMC.Api.Endpoints;

/// <summary>
/// Vocabulario unico de estados del circuito Festival: como se guardan en la base
/// y como viajan por el contrato del canal externo.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE ESTE FICHERO. El circuito escribia los estados en PascalCase
/// (<c>Borrador</c>, <c>EnRevision</c>, <c>AjustesSolicitados</c>...) y
/// <c>Festivales.EstadoRegistro</c> tiene una clave foranea contra
/// <c>EstadosContenido</c>, cuyos siete codigos son minuscula con guion bajo. Medido
/// contra <c>PNMC_LOCAL</c>, un <c>UPDATE</c> por cada
/// nombre:
/// </para>
/// <code>
///           Borrador  ->  ACEPTADO
///         EnRevision  ->  RECHAZADO POR LA FK
/// AjustesSolicitados  ->  RECHAZADO POR LA FK
///          Rechazado  ->  ACEPTADO
///          Publicado  ->  ACEPTADO
/// </code>
/// <para>
/// Tres pasan y dos no, y la razon de que pasen es peor que la de que fallen: la base
/// esta en <c>SQL_Latin1_General_CP1_CI_AS</c>, insensible a mayusculas, asi que
/// <c>Borrador</c> casa con <c>borrador</c> <b>por accidente de configuracion</b>. Los
/// dos que fallan no fallan por mayusculas: <c>EnRevision</c> y <c>en_revision</c> son
/// cadenas distintas, con guion bajo de por medio, y ninguna collation los une.
/// </para>
/// <para>
/// CONSECUENCIA MEDIDA: la cola de revision institucional esta vacia y no por falta de
/// uso. <c>enviar a revision</c> y <c>solicitar ajustes</c> son <b>los dos unicos
/// estados imposibles de escribir</b>, es decir exactamente los dos que meten un
/// Festival en la bandeja del funcionario. Nada puede entrar en ella.
/// </para>
/// <para>
/// POR QUE LAS PRUEBAS NO LO VEIAN. La tabla <c>Festivales</c> de la suite la crea EF
/// desde el modelo, y el mapeo de <c>FestivalRow</c> no declara ninguna relacion con
/// <c>EstadosContenido</c>: en SQLite no hay clave foranea que violar. Un defecto que
/// las pruebas no pueden ver por construccion. Ver <c>Pnmc059VocabularioEstadosTests</c>,
/// que lo comprueba sin depender del motor.
/// </para>
/// <para>
/// POR QUE SOBREVIVE EL NOMBRE DE CONTRATO. El canal externo lleva desde el principio
/// <c>Borrador</c>/<c>EnRevision</c>/... en el JSON, y el front-end compara contra esas
/// cadenas —por ejemplo <c>external-access-page.component.html</c> pinta el aviso de
/// «en revision» con <c>festivalBorrador.estado === 'EnRevision'</c>—. Cambiar tambien
/// el contrato apagaria ese aviso <b>en silencio</b>: sin error, sin 400, simplemente
/// una pantalla que deja de avisar. Asi que el almacenamiento se corrige hoy y el
/// contrato se conserva; la traduccion vive aqui, en un solo sitio, y esta cubierta en
/// las dos direcciones.
/// </para>
/// </remarks>
public static class EstadosFestival
{
    /// <summary>Codigos reales de <c>EstadosContenido</c>. Minuscula obligatoria:
    /// <c>CK_EstadosContenido_CodigoEstado_Formato</c> exige <c>LOWER(CodigoEstado)</c>.</summary>
    public const string Borrador = "borrador";
    public const string EnRevision = "en_revision";
    public const string AjustesSolicitados = "ajustes_solicitados";

    /// <summary>
    /// Existe en <c>EstadosContenido</c> y NINGUN camino del circuito Festival lo escribe.
    /// </summary>
    /// <remarks>
    /// Comprobado: cero escrituras en todo <c>pnmc-api/src</c> y cero filas
    /// en la base. Un Festival no pasa por «aprobado»: de «en revisión» sale publicado, con ajustes
    /// solicitados o rechazado. Se conserva la constante porque la fila del catálogo existe y este
    /// fichero describe el catálogo, pero no pertenece al recorrido de un Festival y las dos listas
    /// del circuito que lo nombraban tenían una rama muerta cada una.
    /// </remarks>
    public const string Aprobado = "aprobado";

    public const string Publicado = "publicado";
    public const string Archivado = "archivado";
    public const string Rechazado = "rechazado";

    /// <summary>
    /// Estado de una organizacion recien dada de alta por el canal externo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NO pertenece al circuito Festival: es el estado de una Entidad, no de un contenido.
    /// Vive en el mismo catalogo porque <c>Entidades.EstadoRegistro</c> tiene
    /// <c>FK_Entidades_EstadosContenido</c>, es decir comparte la tabla de estados con los
    /// modulos de contenido aunque su ciclo de vida sea otro.
    /// </para>
    /// <para>
    /// POR QUE SE AÑADIO AL CATALOGO EN VEZ DE REUTILIZAR UNO DE LOS SIETE.
    /// <c>ExternalOrganizationEndpoints.cs</c> escribia <c>"registrada"</c> desde el
    /// principio —es el estado funcional de una entidad registrada— y la tabla no lo tenia. Medido
    /// contra <c>PNMC_LOCAL</c>:
    /// </para>
    /// <code>
    /// INSERT Entidades EstadoRegistro='registrada'  ->  RECHAZADO (FK_Entidades_EstadosContenido)
    /// INSERT Entidades EstadoRegistro='borrador'    ->  ACEPTADO
    /// </code>
    /// <para>
    /// Es decir: <b>registrar una organizacion era imposible contra SQL Server</b>, y ese es
    /// el PRIMER paso del circuito entero —antes de crear un Festival, antes de enviarlo a
    /// revision—. Explica sin ambiguedad por que la tabla <c>Entidades</c> tiene cero filas.
    /// </para>
    /// <para>
    /// Los siete codigos existentes describen un ciclo editorial (borrador, en revision,
    /// aprobado, publicado). Una organizacion no pasa por revision: §9 dice expresamente que
    /// no necesita aprobacion ministerial para existir, y §10 que registrarla no la hace
    /// publica. Reutilizar <c>aprobado</c> afirmaria una aprobacion que nadie dio, y
    /// <c>borrador</c> negaria que la organizacion ya puede trabajar. El concepto faltaba y
    /// se añade; es aditivo, cumple <c>CK_EstadosContenido_CodigoEstado_Formato</c> y no
    /// cambia el significado de ningun estado existente.
    /// </para>
    /// </remarks>
    public const string Registrada = "registrada";

    /// <summary>Todos los codigos del catalogo <c>EstadosContenido</c>.</summary>
    public static readonly string[] DelCatalogo =
        [Borrador, EnRevision, AjustesSolicitados, Aprobado, Publicado, Archivado, Rechazado, Registrada];

    /// <summary>
    /// Los cinco estados que recorre un Festival.
    /// </summary>
    /// <remarks>
    /// <c>aprobado</c> queda fuera porque el circuito no define un estado intermedio de aprobación
    /// —y se comprobó que nadie lo escribe—, y <c>archivado</c> porque no es un paso del recorrido
    /// sino su salida: se archiva DESDE cualquiera de los cinco.
    /// </remarks>
    public static readonly string[] DelCircuito =
        [Borrador, EnRevision, AjustesSolicitados, Rechazado, Publicado];

    /// <summary>Estados desde los que la agrupacion todavia puede editar su Festival.</summary>
    public static readonly string[] Editables = [Borrador, AjustesSolicitados];

    private static readonly (string Contrato, string Almacenado)[] Equivalencias =
    [
        ("Borrador", Borrador),
        ("EnRevision", EnRevision),
        ("AjustesSolicitados", AjustesSolicitados),
        ("Aprobado", Aprobado),
        ("Publicado", Publicado),
        ("Archivado", Archivado),
        ("Rechazado", Rechazado),
        ("Registrada", Registrada),
    ];

    /// <summary>
    /// Grafias adicionales que se ACEPTAN a la entrada pero nunca se emiten.
    /// </summary>
    /// <remarks>
    /// El ciclo de la propuesta de cambio nombra sus estados en
    /// femenino —<c>Publicada</c>, <c>Rechazada</c>— porque el sujeto es la propuesta y no
    /// el Festival. Ese vocabulario es legitimo y se conserva en
    /// <c>PropuestasCambioFestival.Estado</c>, que no tiene <c>CHECK</c>. Pero la fila que
    /// esa decision escribe en <c>RegistrosRevisionHistorial</c> es institucional y comun a
    /// todos los modulos, y ahi solo caben los siete codigos: escribir <c>Publicada</c>
    /// violaba <c>CK_RegistrosRevisionHistorial_EstadoNuevo</c> y devolvia 500 al panel,
    /// justo lo que midio la sonda de botones el 21 de agosto.
    /// </remarks>
    private static readonly (string Alias, string Almacenado)[] AliasDeEntrada =
    [
        ("Publicada", Publicado),
        ("Rechazada", Rechazado),
        ("Aprobada", Aprobado),
        ("Archivada", Archivado),
    ];

    /// <summary>
    /// Traduce el nombre que viaja en el JSON al codigo que admite la base.
    /// </summary>
    /// <remarks>
    /// Acepta las dos grafias a proposito —lector tolerante—, porque durante la
    /// transicion conviven peticiones antiguas con <c>EnRevision</c> y nuevas con
    /// <c>en_revision</c>. Devuelve <c>null</c> cuando no reconoce el valor, y quien
    /// llama debe tratarlo como peticion invalida: adivinar un estado es peor que
    /// rechazarlo.
    /// </remarks>
    public static string? DesdeContrato(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        foreach (var (contrato, almacenado) in Equivalencias)
        {
            if (string.Equals(limpio, contrato, StringComparison.OrdinalIgnoreCase)
                || string.Equals(limpio, almacenado, StringComparison.OrdinalIgnoreCase))
            {
                return almacenado;
            }
        }

        foreach (var (alias, almacenado) in AliasDeEntrada)
        {
            if (string.Equals(limpio, alias, StringComparison.OrdinalIgnoreCase))
            {
                return almacenado;
            }
        }

        return null;
    }

    /// <summary>
    /// Traduce el codigo almacenado al nombre que espera el canal externo.
    /// </summary>
    /// <remarks>
    /// Si el valor no se reconoce se devuelve tal cual en vez de <c>null</c>: los 30
    /// Festivales historicos de la base tienen <c>publicado</c> y podrian aparecer
    /// otros codigos heredados; borrarlos de la respuesta convertiria un dato raro en
    /// un dato ausente, que es mas dificil de diagnosticar.
    /// </remarks>
    public static string? HaciaContrato(string? almacenado)
    {
        if (string.IsNullOrWhiteSpace(almacenado)) return almacenado;
        var limpio = almacenado.Trim();
        foreach (var (contrato, codigo) in Equivalencias)
        {
            if (string.Equals(limpio, codigo, StringComparison.OrdinalIgnoreCase)
                || string.Equals(limpio, contrato, StringComparison.OrdinalIgnoreCase))
            {
                return contrato;
            }
        }

        return almacenado;
    }

    /// <summary>
    /// Compara un valor almacenado contra un codigo canonico sin depender de la
    /// collation del motor.
    /// </summary>
    /// <remarks>
    /// Se usa en memoria, no dentro de una consulta traducida a SQL. Existe porque la
    /// base local es insensible a mayusculas y SQLite no: una comparacion literal se
    /// comporta distinto en pruebas y en produccion, y esa diferencia es justo la que
    /// escondio este defecto durante meses.
    /// </remarks>
    public static bool Es(string? almacenado, string codigo) =>
        string.Equals(DesdeContrato(almacenado), codigo, StringComparison.Ordinal);
}
