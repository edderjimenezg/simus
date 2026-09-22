namespace PNMC.Domain.Entities;

public sealed class ContentStatusRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class DivipolaLocationRow
{
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string MunicipalityCode { get; set; } = string.Empty;
    public string MunicipalityName { get; set; } = string.Empty;
    public string? LocationType { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
}

/// <summary>
/// El texto versionado que una persona acepta, tal como lo sirve el servidor.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL TEXTO VIVE AQUI Y NO EN LA PANTALLA.</b> Si el formulario llevara dentro la frase del
/// consentimiento, lo que la persona lee y lo que el servidor guarda como evidencia serian dos
/// cosas distintas que nadie compara nunca. Un cambio de redaccion en el HTML no dejaria rastro en
/// ninguna fila.
/// </para>
/// <para>
/// <b>NO ES LA POLITICA COMPLETA DEL MINISTERIO.</b> <see cref="Texto"/> es el consentimiento
/// informado que aparece en pantalla —que se recoge, para que, quien responde, que derechos tiene
/// el titular (Ley 1581 art. 12)—. El documento institucional entero es del Ministerio y se enlaza
/// en <see cref="UrlOficial"/>.
/// </para>
/// </remarks>
public sealed class PoliticaDeDatosRow
{
    public int Id { get; set; }

    /// <summary>La finalidad que ampara: <c>tratamiento</c>, <c>terminos</c> o <c>boletin</c>.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Fecha en texto (AAAA-MM-DD) de cuando ESTA redaccion empezo a mostrarse.</summary>
    public string Version { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    /// <summary>El PDF institucional al que remite el texto, cuando lo hay.</summary>
    public string? UrlOficial { get; set; }

    /// <summary>El identificador del documento del Ministerio: «PL-GSI-002 v0».</summary>
    public string? ReferenciaOficial { get; set; }

    /// <summary>Una sola por clave, y lo garantiza un indice filtrado en la base.</summary>
    public bool Vigente { get; set; }

    public DateTime FechaPublicacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>
/// Quien autorizo que, con que texto delante, y si lo revoco.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL TEXTO SE COPIA, NO SE REFERENCIA.</b> <see cref="IdPolitica"/> responde «a que fila
/// apunto»; solo <see cref="TextoAceptado"/> responde «que leyo». Cuando la politica cambie —y va
/// a cambiar—, la copia sigue diciendo lo que esta persona acepto aquel dia. Es la columna que
/// justifica la tabla entera y la que cumple el deber de demostrar (Ley 1581 art. 17 lit. f).
/// </para>
/// <para>
/// <b>EL TITULAR PUEDE SER UNA CUENTA O SOLO UN CORREO.</b> Quien se suscribe al boletin desde la
/// portada no tiene cuenta y aun asi es titular de un dato personal y tiene derecho a revocar. La
/// estructura anterior exigia un usuario, y por eso el boletin habia tenido que montarse su propio
/// mecanismo aparte.
/// </para>
/// <para>
/// <b>REVOCAR NO BORRA LA FILA.</b> Borrarla dejaria sin respuesta la pregunta «¿esta persona
/// autorizo alguna vez, y hasta cuando?», que es justo la que hay que poder contestar.
/// </para>
/// </remarks>
public sealed class AutorizacionDeDatosRow
{
    public long Id { get; set; }
    public string Finalidad { get; set; } = string.Empty;
    public int IdPolitica { get; set; }
    public string Version { get; set; } = string.Empty;
    public string TextoAceptado { get; set; } = string.Empty;

    /// <summary>
    /// El texto no se capturo en su momento y se reconstruyo. Solo lo llevan las filas trasladadas
    /// desde la estructura anterior, que no guardaba texto: una evidencia reconstruida no vale lo
    /// mismo que una capturada y el registro no debe presentar las dos con la misma cara.
    /// </summary>
    public bool TextoReconstruido { get; set; }

    public int? IdUsuario { get; set; }
    public string? CorreoTitular { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? ReferenciaId { get; set; }
    public DateTime FechaOtorgada { get; set; }
    public DateTime? FechaRevocacion { get; set; }
    public string? MotivoRevocacion { get; set; }

    /// <summary>Sigue en pie mientras no se haya revocado.</summary>
    public bool EstaVigente => FechaRevocacion is null;
}

public sealed class TagRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class FestivalRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? VersionsCount { get; set; }
    public DateTime? LastEditionDate { get; set; }
    public string? Description { get; set; }
    public string? OrganizerDisplayName { get; set; }
    public string? OrganizerContactEmail { get; set; }
    public string? OrganizerContactPhone { get; set; }
    public string? OrganizerWebsiteUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? OtherUrl { get; set; }
    public string? ContactPhone { get; set; }
    public string CoverageLevel { get; set; } = string.Empty;
    /// <summary>
    /// Codigo DIVIPOLA del departamento, o <c>null</c> en cobertura nacional.
    /// </summary>
    /// <remarks>
    /// ES ANULABLE A PROPOSITO. <c>CK_Festivales_NivelCobertura</c> no comprueba solo que el
    /// nivel sea uno de los tres: comprueba la COHERENCIA entre nivel y territorio —nacional
    /// exige departamento y municipio NULL; departamental, municipio NULL—. Mientras esta
    /// propiedad fue <c>string</c> con <c>= string.Empty</c>, el nivel nacional escribia cadena
    /// vacia, que NO es NULL para la restriccion, y el guardado moria con un 500 sin explicacion.
    /// Un nivel de cobertura entero del formulario era imposible de guardar contra SQL Server.
    /// </remarks>
    public string? DepartmentCode { get; set; }
    public string? MunicipalityCode { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool HasCurrentYearEdition { get; set; }
    public string? CurrentYearEditionStatus { get; set; }
    public DateTime? CurrentYearStartDate { get; set; }
    public DateTime? CurrentYearEndDate { get; set; }
    public int? OrganizacionPrincipalId { get; set; }
    public string? Periodicidad { get; set; }
    public string? PeriodicidadDetalle { get; set; }

    /// <summary>
    /// Lo que el volcado de SIMUS llama <c>OBSERVACIONES_CONTACTO</c> en la cabecera del Festival.
    /// </summary>
    /// <remarks>
    /// LA COLUMNA EXISTIA EN LA BASE DESDE `V20260828_02` Y NO ESTABA MAPEADA, asi que ninguna ruta
    /// podia leerla ni escribirla. `ParidadEsquemaSinArranqueTests` no lo ve: mide que cada columna
    /// MAPEADA exista en la base, no al reves, de modo que una columna sin entidad es invisible
    /// para esa puerta.
    /// </remarks>
    public string? ObservacionesContacto { get; set; }
}

/// <summary>
/// Una categoría temática de contenido.
/// </summary>
/// <remarks>
/// <para>
/// <c>CodigoModulo</c> ADMITE `comun` ADEMAS de `agenda`, `noticias` y `editorial`. Una categoría
/// puede declararse compartida por los tres sin obligar a que todas lo sean: «Bandas» sirve para
/// una noticia, un evento y una publicación, y «Partitura» solo para el catálogo. La decisión se
/// toma categoría por categoría en vez de de una vez para siempre.
/// </para>
/// <para>
/// La tabla existía desde el corte inicial y estaba vacía: nadie la usaba. Los tres módulos
/// guardaban su categoría como texto libre, de modo que nadie podía corregir un nombre mal escrito
/// sin editar cada ficha.
/// </para>
/// </remarks>
public sealed class CategoriaRow
{
    public int Id { get; set; }
    public string CodigoModulo { get; set; } = string.Empty;
    public string NombreCategoria { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int OrdenVisualizacion { get; set; } = 1;
}

/*
  LOS VINCULOS CON EL ECOSISTEMA. Una fila por relación, opcional y múltiple: una noticia puede no
  hablar de ninguna práctica musical, o de tres. Es el mismo patrón que `FestivalPracticaMusicalRow`,
  y se repite tipo a tipo en vez de generalizarlo porque EF necesita un tipo por tabla y porque un
  nombre concreto se lee mejor que uno abstracto en la consulta que lo usa.
*/
public sealed class NoticiaPracticaMusicalRow
{
    public long Id { get; set; }
    public long NoticiaId { get; set; }
    public int PracticaMusicalId { get; set; }
}

public sealed class NoticiaTerritorioSonoroRow
{
    public long Id { get; set; }
    public long NoticiaId { get; set; }
    public int TerritorioSonoroId { get; set; }
}

public sealed class EventoAgendaPracticaMusicalRow
{
    public long Id { get; set; }
    public long EventoAgendaId { get; set; }
    public int PracticaMusicalId { get; set; }
}

public sealed class EventoAgendaTerritorioSonoroRow
{
    public long Id { get; set; }
    public long EventoAgendaId { get; set; }
    public int TerritorioSonoroId { get; set; }
}

public sealed class PublicacionEditorialPracticaMusicalRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public int PracticaMusicalId { get; set; }
}

public sealed class PublicacionEditorialTerritorioSonoroRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public int TerritorioSonoroId { get; set; }
}


public sealed class PracticaMusicalRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
}

public sealed class TerritorioSonoroRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
}

/// <summary>Contenido conceptual único de un Territorio sonoro; la identidad sigue en su catálogo.</summary>
public sealed class FichaConceptualTerritorioSonoroRow
{
    public int TerritorioSonoroId { get; set; }
    public string? DefinicionBreve { get; set; }
    public string? DefinicionAmpliada { get; set; }
    public string? DescripcionConceptual { get; set; }
    public string? Caracteristicas { get; set; }
    public string? RelacionTerritorial { get; set; }
    public string? Contextos { get; set; }
    public string? Ejemplos { get; set; }
    public string? Fuentes { get; set; }
    public string? RecursoVisualUrl { get; set; }
    public string? TextoAlternativoRecurso { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>Contenido conceptual único de una Práctica musical; la identidad sigue en su catálogo.</summary>
public sealed class FichaConceptualPracticaMusicalRow
{
    public int PracticaMusicalId { get; set; }
    public string? DefinicionBreve { get; set; }
    public string? DefinicionAmpliada { get; set; }
    public string? DescripcionConceptual { get; set; }
    public string? Caracteristicas { get; set; }
    public string? Contextos { get; set; }
    public string? Ejemplos { get; set; }
    public string? Fuentes { get; set; }
    public string? RecursoVisualUrl { get; set; }
    public string? TextoAlternativoRecurso { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

// =================================================================================================
// LOS CATALOGOS DEL FESTIVAL QUE VINIERON DE SIMUS
// =================================================================================================
/*
 * POR QUE APARECEN TODOS DE GOLPE, con su fecha. `schema/V20260824_02__festivales_simus.sql` creo
 * estas nueve tablas y las dejo VACIAS a proposito; el 28 de agosto se
 * sembraron sus 54 filas desde `Scripts/script_festivales_simus.sql`. Hasta ese dia ninguna estaba
 * mapeada en EF Core, asi que existian en la base y eran invisibles para el API: `grep` sobre este
 * fichero devolvia cero para las nueve.
 *
 * TODAS TIENEN LA MISMA FORMA -Id y Nombre- Y NO SE FUNDEN EN UNA. Son nueve tablas distintas con
 * nueve claves ajenas distintas; un unico `CatalogoRow` con una columna «tipo» obligaria a que la
 * base dejara de validar que una tipologia no se puede usar donde va una fuente de financiacion, y
 * esa validacion es justo lo que hace que el dato sirva. Es el mismo criterio con el que
 * `PracticaMusicalRow` y `TerritorioSonoroRow` estan separados aqui arriba.
 *
 * NO SE MAPEA `Slug`, `Descripcion` NI `OrdenVisualizacion` todavia: el API solo necesita el
 * identificador y el rotulo para llenar un desplegable. `OrdenVisualizacion` hara falta el dia que
 * el orden del desplegable importe; anadirlo entonces es una linea.
 */

/** `ART_MUS_FESTIVALES_TIPOLOGIA`. Que clase de evento es: fiesta popular, feria, reinado... */
public sealed class TipologiaFestivalRow
{
    public int Id { get; set; }

    /// <summary>
    /// La clave estable del catalogo, la que la siembra fija y el rotulo no.
    /// </summary>
    /// <remarks>
    /// LAS CONDICIONALES DE LA HISTORIA DE USUARIO SE APOYAN AQUI. «Si se elige Otra, el texto que
    /// la explica es obligatorio» necesita reconocer cual de las filas es «Otra», y el
    /// identificador no sirve —lo asigna la siembra y cambia entre bases— ni el nombre visible
    /// tampoco —es un rotulo y se puede reescribir—. El guion de siembra fija estos slugs a
    /// proposito: «'Ninguna' y 'Otra' son centinelas del formulario».
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS`. Que hay ademas de musica: danza, culinaria... */
public sealed class ExpresionArtisticaRow
{
    public int Id { get; set; }

    /// <summary>
    /// La clave estable del catalogo, la que la siembra fija y el rotulo no.
    /// </summary>
    /// <remarks>
    /// LAS CONDICIONALES DE LA HISTORIA DE USUARIO SE APOYAN AQUI. «Si se elige Otra, el texto que
    /// la explica es obligatorio» necesita reconocer cual de las filas es «Otra», y el
    /// identificador no sirve —lo asigna la siembra y cambia entre bases— ni el nombre visible
    /// tampoco —es un rotulo y se puede reescribir—. El guion de siembra fija estos slugs a
    /// proposito: «'Ninguna' y 'Otra' son centinelas del formulario».
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_FUENTE_FINANCIACION`. De donde sale el dinero. */
public sealed class FuenteFinanciacionRow
{
    public int Id { get; set; }

    /// <summary>
    /// La clave estable del catalogo, la que la siembra fija y el rotulo no.
    /// </summary>
    /// <remarks>
    /// LAS CONDICIONALES DE LA HISTORIA DE USUARIO SE APOYAN AQUI. «Si se elige Otra, el texto que
    /// la explica es obligatorio» necesita reconocer cual de las filas es «Otra», y el
    /// identificador no sirve —lo asigna la siembra y cambia entre bases— ni el nombre visible
    /// tampoco —es un rotulo y se puede reescribir—. El guion de siembra fija estos slugs a
    /// proposito: «'Ninguna' y 'Otra' son centinelas del formulario».
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION`. Como entra quien toca: concurso, invitacion... */
public sealed class ModalidadParticipacionRow
{
    public int Id { get; set; }

    /// <summary>
    /// La clave estable del catalogo, la que la siembra fija y el rotulo no.
    /// </summary>
    /// <remarks>
    /// LAS CONDICIONALES DE LA HISTORIA DE USUARIO SE APOYAN AQUI. «Si se elige Otra, el texto que
    /// la explica es obligatorio» necesita reconocer cual de las filas es «Otra», y el
    /// identificador no sirve —lo asigna la siembra y cambia entre bases— ni el nombre visible
    /// tampoco —es un rotulo y se puede reescribir—. El guion de siembra fija estos slugs a
    /// proposito: «'Ninguna' y 'Otra' son centinelas del formulario».
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD`. Publica, privada o mixta; la lleva cada entidad socia. */
public sealed class NaturalezaEntidadRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_TIPOINGRESO`. Como entra el publico: entrada libre, boleteria paga... */
public sealed class TipoIngresoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_TIPO_ORGANIZADOR`. Quien lo organiza: alcaldia, fundacion, museo... */
public sealed class TipoOrganizadorRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_FESTIVALES_ZONA`. Urbana o rural, por cada localizacion de una version. */
public sealed class ZonaUrbanoRuralRow
{
    public int Id { get; set; }

    /// <summary>
    /// La clave estable del catalogo, la que la siembra fija y el rotulo no.
    /// </summary>
    /// <remarks>
    /// LAS CONDICIONALES DE LA HISTORIA DE USUARIO SE APOYAN AQUI. «Si se elige Otra, el texto que
    /// la explica es obligatorio» necesita reconocer cual de las filas es «Otra», y el
    /// identificador no sirve —lo asigna la siembra y cambia entre bases— ni el nombre visible
    /// tampoco —es un rotulo y se puede reescribir—. El guion de siembra fija estos slugs a
    /// proposito: «'Ninguna' y 'Otra' son centinelas del formulario».
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/**
 * `ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA`. Si el lugar es de consejo comunitario, de
 * resguardo indigena, o no aplica.
 *
 * «No aplica» ES UNA FILA Y NO UN NULO, a diferencia de otros catalogos: la relacion es de uno a
 * uno con cada localizacion, asi que sin la fila «no aplica» y «no respondio» serian el mismo NULL,
 * y son dos respuestas distintas.
 */
public sealed class TitulacionColectivaRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/**
 * `ART_MUS_FESTIVALES_REGION_OCAD`. Las seis regiones del Organo Colegiado de Administracion y
 * Decision, que es como se reparten los recursos del Sistema General de Regalias.
 *
 * NO SE DEDUCE DEL DEPARTAMENTO, y por eso hay una tabla y no un `switch`: el reparto es
 * administrativo, no geografico. Antioquia esta en EJE CAFETERO y no en PACIFICO, aunque tenga
 * costa pacifica.
 */
public sealed class RegionOcadRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// El orden del desplegable, que NO es el alfabetico.
    /// </summary>
    /// <remarks>
    /// «Otra» VA LA ULTIMA, y por alfabeto caeria en mitad de la lista. Lo mismo con «Ninguna».
    /// Son las opciones de escape del catalogo y su sitio es el final: quien busca su caso lo busca
    /// entre los concretos, y quien no lo encuentra llega al escape al terminar de mirar.
    /// </remarks>
    public int Orden { get; set; }
}

/** `ART_MUS_ZONAXREGION_OCAD`. Un departamento pertenece a UNA region, y por eso el codigo es la clave. */
public sealed class DepartamentoRegionOcadRow
{
    public string CodigoDepartamento { get; set; } = string.Empty;
    public int RegionOcadId { get; set; }
}

public sealed class VersionFestivalRow
{
    public int Id { get; set; }
    public int FestivalOrigenId { get; set; }
    public int NumeroVersion { get; set; }
    public bool EsVigente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string NivelCobertura { get; set; } = string.Empty;
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Periodicidad { get; set; }
    public string? PeriodicidadDetalle { get; set; }
    public string? CorreoContacto { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public DateTime FechaCreacion { get; set; }

    // BLOQUE DE CONTACTO Y ORGANIZADOR (B6). Viaja en la VERSION y no solo en la cabecera porque
    // la cabecera es el presente y la version es lo publicado. Sin esto, cambiar un telefono en el
    // borrador lo cambiaba en la ficha publicada de inmediato, saltandose el circuito de revision
    // entero — una excepcion silenciosa a la regla que sostiene todo el modelo de versiones.
    public string? TelefonoContacto { get; set; }
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
    public string? SitioWeb { get; set; }
    public string? OtroEnlace { get; set; }
    public string? Director { get; set; }
    public int? TipoOrganizadorId { get; set; }

    // ---------------------------------------------------------------------------------------
    // EL RESTO DEL MODELO DE SIMUS. Mapeado.
    // ---------------------------------------------------------------------------------------
    //
    // DIECISIETE COLUMNAS QUE ESTABAN EN LA BASE Y NO EN EF. Once las anadio
    // `schema/V20260828_02` ese mismo dia; las otras SEIS -las fechas, la tipologia, las dos
    // fuentes de financiacion y la estampilla- llevaban desde el 24 de agosto en
    // `dbo.VersionesFestival` sin entidad que las leyera. `ParidadEsquemaSinArranqueTests` no lo
    // vio nunca porque comprueba en UN SOLO SENTIDO: que exista en la base cada columna que EF
    // mapea. Al reves -una columna de la base sin mapear- no rompe nada y no se nota.

    /// <summary>
    /// Fechas heredadas de la antigua ficha de edición extensa. El perfil público no las usa como
    /// dato propio: se conservarán hasta migrarlas al contrato anual de EdicionesFestival.
    /// </summary>
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    public int? TipologiaFestivalId { get; set; }
    public int? FuenteFinanciacionPrimariaId { get; set; }
    public int? FuenteFinanciacionSecundariaId { get; set; }

    /// <summary>
    /// La estampilla Procultura, el tributo territorial con destinacion a cultura.
    /// </summary>
    /// <remarks>
    /// NULABLE, y la diferencia importa: <c>false</c> es «no la usa» y <c>null</c> es «no
    /// respondio». En el origen hay filas de las dos clases.
    /// </remarks>
    public bool? UsaEstampillaProcultura { get; set; }

    /// <summary>Texto libre del origen (<c>PRACTICAS_MUSICALES_CONGREGA</c>), aparte de la tabla puente.</summary>
    public string? PracticasMusicalesQueCongrega { get; set; }

    // LOS SEIS «OTRA» NO SON RELLENO. Cada catalogo termina en una fila «Otra», y esa fila sola no
    // guarda nada: quien la elige escribe cual. Sin estas columnas, elegir «Otra» seria
    // indistinguible de no elegir, y el catalogo no podria ser cerrado -y por tanto agregable- sin
    // perder el caso que no cabe en el.
    public string? OtraTipologia { get; set; }
    public string? OtraModalidadParticipacion { get; set; }
    public string? OtraExpresionArtistica { get; set; }
    public string? OtraFuenteFinanciacionPrimaria { get; set; }
    public string? OtraFuenteFinanciacionSecundaria { get; set; }
    public string? OtroTipoOrganizador { get; set; }

    /// <summary><c>PERTENECE_ORG_COLETIVA</c> en el origen, con la errata. Nulable: «no respondio» no es «no».</summary>
    public bool? PerteneceAOrganizacionColectiva { get; set; }
    public string? NombreOrganizacionColectiva { get; set; }

    /// <summary>Notas sobre COMO contactar, distintas de la descripcion del Festival.</summary>
    public string? ObservacionesContacto { get; set; }

    /// <summary>Lo que escribe el funcionario al devolver una version. 4000 en el origen; no se recorta.</summary>
    public string? ObservacionesRechazo { get; set; }

    /// <summary>El estado de revision DE ESTA VERSION, no el del Festival.</summary>
    /// <remarks>
    /// <para>
    /// ES LA COLUMNA QUE RESUELVE LA DUDA HISTORICA documentada el 27 de agosto: PNMC tenia dos
    /// modelos para lo mismo —<c>EdicionesFestival</c> y
    /// <c>VersionesFestival</c>— y que habia que decidir cual es «una version del Festival». SIMUS
    /// tiene uno solo, y su version lleva sus fechas, su nombre, sus catalogos Y su propio estado
    /// (<c>ART_MUS_FESTIVALES_VERSION.ID_ESTADO</c>). Esto adopta esa respuesta.
    /// </para>
    /// <para>
    /// Es <c>string</c> y no <c>int</c> porque apunta a <c>dbo.EstadosContenido.CodigoEstado</c>, el
    /// catalogo que manda sobre cualquier constante del front y del API, igual que
    /// <c>FestivalRow.EstadoRegistro</c>. Los seis estados de SIMUS ya estaban ahi —Borrador,
    /// Enviado, Solicitud de aclaraciones, Aprobado, Rechazado, Archivado, contra borrador,
    /// en_revision, ajustes_solicitados, aprobado, rechazado y archivado—: no hubo que anadir uno.
    /// </para>
    /// </remarks>
    public string? EstadoRegistro { get; set; }
}

// -------------------------------------------------------------------------------------------
// LAS CINCO TABLAS PUENTE QUE FALTABAN
// -------------------------------------------------------------------------------------------
// `VersionesFestivalPracticasMusicales` y `VersionesFestivalTerritoriosSonoros` ya estaban
// mapeadas; estas cinco no. Son las que hacen que un Festival pueda tener VARIOS municipios,
// afiche y programa, y entidades aliadas: sin ellas ese dato existe en la base y no hay forma de
// leerlo ni de escribirlo.

/// <summary>
/// Un registro del ecosistema que tiene una organizacion que responde por el.
/// </summary>
/// <remarks>
/// La regla que declara: <b>ningun registro del ecosistema se queda sin nadie que responda por
/// el</b>. Mientras ninguna organizacion de la comunidad lo reclame, responde la institucion.
/// Quien la aplica es <c>PnmcDbContext.SaveChanges</c>, en un solo sitio y no en cada creador.
/// </remarks>
public interface ITieneOrganizacionResponsable
{
    int? OrganizacionResponsableId { get; set; }
}

/// <summary>
/// Lo minimo que comparten los cinco catalogos del ecosistema —escuelas de musica, mercados
/// musicales, redes y documentacion, luteria y escenarios— para poder recorrer el circuito
/// editorial.
/// </summary>
/// <remarks>
/// EXISTE PARA BORRAR TRES COPIAS, NO PARA ANADIR UNA ABSTRACCION. <c>UpdateEcosystemStatusAsync</c>
/// estaba escrito tres veces en <c>AdminDataEndpoints</c>, con el cuerpo identico y solo el tipo de
/// fila cambiando. Escenarios habria sido la cuarta. Con esta interfaz es un unico metodo
/// generico, y el proceso que venga despues no anade nada.
/// </remarks>
public interface IRegistroDeEcosistema
{
    int Id { get; }
    string StatusCode { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Una edicion de un Festival: 2024, 2025, 2026, cada una con su nombre y sus fechas.
/// </summary>
/// <remarks>
/// NO ES <c>dbo.VersionesFestival</c>. Esa tabla es la version del REGISTRO PUBLICADO —la escribe
/// la aprobacion de una propuesta de cambios y el sitio publico lee su fila vigente para sustituir
/// la ficha—, y meter ahi ediciones por anyo cambiaria lo que ve cualquier visitante. El motivo
/// completo esta en <c>pnmc-database/schema/V20260827_01__ediciones_festival.sql</c>.
/// </remarks>
/// <summary>
/// Una fila de <c>dbo.BoletinSuscripciones</c>: un correo que pidio recibir el boletin.
/// </summary>
/// <remarks>
/// <para>
/// LA AUTORIZACION NO ESTA AQUI, Y ESO ES LO QUE CAMBIO. Esta fila llevaba dos columnas propias
/// —<c>AutorizacionOtorgada</c> y <c>AutorizacionTexto</c>— con la idea correcta: la Ley 1581 de
/// 2012 no solo exige autorizacion previa y expresa, exige poder DEMOSTRARLA, y para eso hay que
/// guardar el texto. Lo que fallaba es que esa idea correcta solo la aplicaba el boletin, con el
/// texto escrito como constante de C# y sin guardar la version en ninguna fila.
/// </para>
/// <para>
/// Desde <c>V20260912_07</c> la evidencia vive en <c>dbo.AutorizacionesDatos</c>, que es donde
/// viven las tres del sistema. Esta fila conserva lo que de verdad es suyo: a que correo se envia,
/// desde cuando y si sigue activa. Quien quiera saber que autorizo esa persona pregunta al
/// registro por su correo.
/// </para>
/// <para>
/// NO HAY IP NI AGENTE DE USUARIO, y es deliberado: para una lista de correo no aportan nada y son
/// dato personal adicional que despues hay que declarar, custodiar y borrar.
/// </para>
/// </remarks>
public sealed class BoletinSuscripcionRow
{
    public int Id { get; set; }

    /// <summary>Normalizado a minusculas antes de guardarse. La unicidad la impone la base.</summary>
    public string CorreoElectronico { get; set; } = string.Empty;

    /// <summary>De que formulario vino: «portada», «noticias» o «registro».</summary>
    public string Origen { get; set; } = "portada";

    /// <summary><c>activa</c> o <c>baja</c>. Lo cierra un CHECK en la base.</summary>
    public string Estado { get; set; } = "activa";

    public DateTime FechaAlta { get; set; }

    public DateTime? FechaBaja { get; set; }
}

public sealed class EdicionFestivalRow
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public int? Anio { get; set; }
    public int? NumeroEdicion { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    /// <summary>Director o directora de esta edición, como texto público libre.</summary>
    public string? Director { get; set; }
    public int? TipologiaFestivalId { get; set; }
    public string? OtraTipologia { get; set; }
    public int? FuenteFinanciacionPrimariaId { get; set; }
    public string? OtraFuenteFinanciacionPrimaria { get; set; }
    public int? FuenteFinanciacionSecundariaId { get; set; }
    public string? OtraFuenteFinanciacionSecundaria { get; set; }
    public bool? UsaEstampillaProcultura { get; set; }
    public string? PracticasMusicalesQueCongrega { get; set; }
    public string? OtraModalidadParticipacion { get; set; }
    public string? OtraExpresionArtistica { get; set; }
    /// <summary>Compatibilidad física con el catálogo histórico. No se expone como estado de la Edición.</summary>
    public string EstadoRegistro { get; set; } = "borrador";
    /// <summary>Estado de visibilidad: borrador, publicada o archivada.</summary>
    public string EstadoVisibilidad { get; set; } = "borrador";
    /// <summary>Estado de la realización: preparación, programada, realizada o cancelada.</summary>
    public string Estado { get; set; } = "en_preparacion";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

public sealed class FileRow
{
    public int Id { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public long? FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public string? Credit { get; set; }
    public int UploadedByUserId { get; set; }

    /// <summary>
    /// La organización de la que viene el archivo, o <c>null</c> si lo cargó el Programa.
    /// </summary>
    /// <remarks>
    /// <b>PROCEDENCIA Y CUOTA, QUE SON DOS COSAS.</b> «Quién lo subió» ya estaba en
    /// <see cref="UploadedByUserId"/>; esto es «de qué organización viene», que es distinto y es la
    /// regla transversal del proyecto. Y es además lo único que permite decir «esta organización ya
    /// subió demasiado»: el canal institucional es de confianza y no necesita tope, el externo sí.
    /// Nulo significa institucional, que es lo que de verdad eran todos los archivos hasta que el
    /// banco se abrió al canal externo.
    /// </remarks>
    public int? OrganizacionId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Los bytes viven en la base, como ya hace `MediosWeb`. No se inventa un segundo
    // almacenamiento: el proyecto ya decidio este y ya lo sirve con ETag.
    public byte[]? Content { get; set; }
    /// <summary>Huella del contenido: es como se evita guardar dos veces el mismo archivo.</summary>
    public string? Fingerprint { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>El vinculo entre un evento de la Agenda y un archivo del banco.</summary>
public sealed class EventoAgendaArchivoRow
{
    public long Id { get; set; }
    public long EventoAgendaId { get; set; }
    public int ArchivoId { get; set; }
    /// <summary>`imagen_principal` y, en su dia, los demas. Distinguirlos es de lo que sirve.</summary>
    public string RolArchivo { get; set; } = "imagen_principal";
    public int OrdenVisualizacion { get; set; } = 1;
}

/// <summary>El archivo del banco vinculado a una noticia. Gemela de <see cref="EventoAgendaArchivoRow"/>.</summary>
public sealed class NoticiaArchivoRow
{
    public long Id { get; set; }
    public long NoticiaId { get; set; }
    public int ArchivoId { get; set; }
    /// <summary>`imagen_principal` y, en su día, los demás. Distinguirlos es de lo que sirve.</summary>
    public string RolArchivo { get; set; } = "imagen_principal";
    public int OrdenVisualizacion { get; set; } = 1;
}

/// <summary>
/// Una iniciativa del Programa que atraviesa varios módulos. Celebra la Música es la primera.
/// </summary>
/// <remarks>
/// <para>
/// NO ES UNA CATEGORIA TEMATICA. Una categoría dice DE QUÉ TRATA un contenido —«Convocatorias»,
/// «Bandas»—; un proyecto transversal dice A QUÉ INICIATIVA PERTENECE. Un evento puede ser de la
/// categoría «Encuentros» y además pertenecer a Celebra la Música: mezclarlas obligaría a inventar
/// categorías como «Encuentros de Celebra».
/// </para>
/// <para>
/// SE ENLAZAN EVENTOS DE AGENDA Y NOTICIAS, NO FESTIVALES. Lo decidió la dirección el 11 de
/// septiembre de 2026: un Festival es un proceso del ecosistema con su propia organización
/// responsable; lo que puede pertenecer a una iniciativa del Programa es el contenido que se
/// publica sobre él, no el proceso entero.
/// </para>
/// <para>
/// UN PROYECTO QUE TERMINA SE DESACTIVA, NO SE BORRA: borrarlo dejaría sin explicación el
/// contenido publicado bajo él.
/// </para>
/// </remarks>
public sealed class ProyectoTransversalRow
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int OrdenVisualizacion { get; set; } = 1;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

/// <summary>El vínculo de un evento de Agenda con una iniciativa del Programa.</summary>
public sealed class EventoAgendaProyectoTransversalRow
{
    public long Id { get; set; }
    public long EventoAgendaId { get; set; }
    public int ProyectoTransversalId { get; set; }
}

/// <summary>El vínculo de una noticia con una iniciativa del Programa.</summary>
public sealed class NoticiaProyectoTransversalRow
{
    public long Id { get; set; }
    public long NoticiaId { get; set; }
    public int ProyectoTransversalId { get; set; }
}

/// <summary>
/// De dónde vino un registro: desde qué contexto se incorporó, qué entidad lo aportó y qué cuenta
/// ejecutó la acción.
/// </summary>
/// <remarks>
/// <para>
/// SON TRES COSAS DISTINTAS Y EL SISTEMA SOLO SABIA DOS. <c>OrganizacionResponsableId</c> dice
/// quién RESPONDE por el registro, y <c>dbo.BitacoraAuditoria</c> dice QUÉ PASÓ con él. Faltaba
/// DE DÓNDE VINO: si lo incorporó una organización desde su espacio o un funcionario desde la
/// consola.
/// </para>
/// <para>
/// EL CASO QUE LA OBLIGA: un funcionario del PNMC registra el festival de la Fundación X. La
/// procedencia es el PNMC —lo incorporó el Programa—, pero la organización responsable sigue
/// siendo la Fundación X. Con un solo campo, o se pierde quién lo metió o se le atribuye al
/// Programa un festival que no organiza.
/// </para>
/// <para>
/// EL PAR (Dominio, RegistroId) ES LA CONVENCIÓN QUE YA USA EL PROYECTO en la bitácora y en las
/// reclamaciones. No se inventa otra forma de apuntar a un registro, y así una tabla nueva no
/// necesita columnas nuevas para tener procedencia.
/// </para>
/// </remarks>
public sealed class ProcedenciaDeRegistroRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string ContextoOrigen { get; set; } = string.Empty;
    public int? OrganizacionProcedenciaId { get; set; }
    public int? UsuarioCreadorId { get; set; }
    public DateTime FechaRegistro { get; set; }
}

/// <summary>Un módulo de la consola concedido a una cuenta.</summary>
/// <remarks>
/// <para>
/// <b>LOS PERMISOS DE LA CONSOLA SON POR CUENTA</b>, no por rol: lo decidió la dirección de producto el
/// 15 de septiembre de 2026. La unidad es el módulo que la persona ve en la barra izquierda, con su
/// mismo identificador —ver <c>ModulosDeLaConsola</c>—, para no mantener dos taxonomías.
/// </para>
/// <para>
/// <b>LOS QUE VIENEN SIEMPRE ACTIVADOS NO SE GUARDAN AQUI.</b> «Resumen operativo» y «Solicitudes y
/// revisiones» los tiene toda cuenta por decisión de producto, y guardarlos sería poder borrarlos.
/// </para>
/// </remarks>
/// <summary>Un tipo de documento de identidad del catálogo del país.</summary>
/// <remarks>
/// <b>SE MAPEA PARA RETIRAR UNA COPIA DECLARADA.</b> La tabla existía en la base con sus ocho filas
/// y nadie la leía: el alta externa llevaba las ocho parejas escritas a mano, anotadas como copia y
/// con la instrucción de borrarlas «el día que la tabla se mapee». Ese día es hoy, porque el perfil
/// de una cuenta administrativa necesita el mismo vocabulario y una tercera copia era inaceptable.
/// </remarks>
public sealed class TipoDocumentoRow
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int OrdenVisualizacion { get; set; }
    public bool Activo { get; set; }
}

public sealed class ModuloPorCuentaRow
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public string CodigoModulo { get; set; } = string.Empty;
    public DateTime FechaOtorgado { get; set; }

    /// <summary>Quién lo concedió. Nulo para lo que no concedió una persona.</summary>
    public int? IdUsuarioQueOtorgo { get; set; }
}

public sealed class UserRow
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AccessChannel { get; set; } = "interno";
    public string? ProfileType { get; set; }
    public string? Telefono { get; set; }

    /// <summary>El número de documento de quien usa la cuenta.</summary>
    /// <remarks>
    /// <b>LAS COLUMNAS EXISTIAN Y NADIE LAS LEIA.</b> `Usuarios.Identificacion` y
    /// `Usuarios.CodigoTipoDocumento` estaban en la base desde antes y la entidad no las mapeaba,
    /// así que la consola no tenía dónde pedirlas. Quedó definido el 15 de
    /// septiembre de 2026: «es importante plantear una ruta de modificación de perfil de estos
    /// usuarios administrativos donde podamos pedirles la información básica como nombre completo,
    /// número de identificación, etcétera, porque eso hoy no está planteado».
    /// </remarks>
    public string? Identificacion { get; set; }

    /// <summary>El código del tipo de documento, del catálogo `TiposDocumento`.</summary>
    public string? CodigoTipoDocumento { get; set; }

    /// <summary>El nombre, partido igual que en el alta externa.</summary>
    /// <remarks>
    /// <b>LA MISMA ESTRUCTURA EN TODO EL PROYECTO.</b> Quedó definido el 15 de
    /// septiembre de 2026: «es importante dejar todos los formularios de nombre con la misma
    /// estructura… copiemos esa estructura base de ahí». Un nombre partido en un formulario y entero
    /// en otro obliga a decidir dos veces cómo se guarda y hace imposible comparar personas entre
    /// módulos.
    ///
    /// <b>`FullName` NO SE RETIRA</b>: se compone a partir de estas cuatro partes y lo siguen
    /// leyendo la bitácora, las fichas y media consola. Las cuentas anteriores solo tienen eso.
    /// </remarks>
    public string? PrimerNombre { get; set; }
    public string? SegundoNombre { get; set; }
    public string? PrimerApellido { get; set; }
    public string? SegundoApellido { get; set; }

    /// <summary>La cuenta todavía usa la contraseña que le puso quien la creó.</summary>
    /// <remarks>
    /// MIENTRAS ESTE EN CIERTO, ESA CREDENCIAL LA CONOCEN DOS PERSONAS. El primer ingreso obliga a
    /// cambiarla antes de cualquier otra cosa.
    /// </remarks>
    public bool DebeCambiarContrasena { get; set; }

    /// <summary>La persona ya dijo quién es: nombre y documento.</summary>
    /// <remarks>
    /// SON DOS MARCAS Y NO UNA porque son dos pasos en orden: quien cambió la clave y cerró la
    /// ventana antes de llenar sus datos no tiene que volver a cambiarla.
    ///
    /// <b>NACE EN CIERTO, Y ESO ES DELIBERADO.</b> El recorrido de bienvenida existe porque una
    /// cuenta se ENTREGA, y eso solo pasa por una vía: el alta de la consola, que la pone en falso
    /// expresamente. Todo lo demás que crea una cuenta —los sembradores, los arneses de prueba, lo
    /// que se escriba mañana— produce cuentas técnicas que ya tienen nombre y credenciales.
    ///
    /// Con el valor por omisión al revés, cualquier cuenta creada fuera del alta quedaba encerrada:
    /// medido, el webmaster de las pruebas recibía 403 en cada módulo y
    /// ni siquiera podía conceder permisos para arreglarlo.
    /// </remarks>
    public bool PerfilCompletado { get; set; } = true;
    public bool IsActive { get; set; }

    /// <summary>
    /// Si esta cuenta ha comprobado que el correo con el que entra es suyo.
    /// </summary>
    /// <remarks>
    /// NO BLOQUEA EL ACCESO, BLOQUEA ACTUAR EN NOMBRE DE LA ORGANIZACION. Quien acaba de registrarse
    /// entra y consulta el ecosistema desde el primer minuto; lo que no puede es registrar un
    /// proceso, anunciar un evento, reclamar un Festival, enviarlo a revision ni publicar una
    /// edicion atado a un correo que nadie ha comprobado. La puerta es una sola funcion:
    /// <c>AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync</c>.
    /// </remarks>
    public bool CorreoConfirmado { get; set; }

    public DateTime? FechaConfirmacionCorreo { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Un enlace de confirmacion de correo emitido para una cuenta.
/// </summary>
/// <remarks>
/// UNA FILA POR EMISION, no una columna en la cuenta: reenviar tiene que invalidar el anterior y
/// quedar registrado, y con una sola columna «lo pedi tres veces y no me llego» no se puede
/// responder. Se guarda el HASH del testigo, nunca el testigo: quien pueda leer esta tabla no debe
/// poder confirmar el correo de nadie.
/// </remarks>
public sealed class ConfirmacionDeCorreoRow
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public string CorreoDestino { get; set; } = string.Empty;
    public string HashTestigo { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public DateTime? FechaUso { get; set; }
}

public sealed class RoleRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Una concesion de rol a una persona: la fila de <c>dbo.UsuariosRoles</c>.
/// </summary>
/// <remarks>
/// <para>
/// ES LA FUENTE DE VERDAD DE QUE ROLES TIENE ALGUIEN en el modelo de
/// SIMUS (24 de agosto de 2026). Hasta entonces el rol vivia en la columna escalar
/// <c>Usuarios.IdRol</c>, que solo admite uno.
/// </para>
/// <para>
/// <b>ES LA UNICA FUENTE.</b> La columna escalar
/// <c>Usuarios.IdRol</c> ya no existe: la retiro la seccion 11 de
/// <c>schema/V20260824_01__usuarios_roles_y_permisos.sql</c>, y con ella desaparecio la
/// escritura doble que la transicion mantenia para poder revertir el despliegue.
/// </para>
/// <para>
/// Eso es lo que hace comprobable el invariante que motivo toda la migracion: <b>los roles de
/// una persona son exactamente las filas de esta tabla</b>, sin excepciones y sin una segunda
/// fuente que pueda contradecirla.
/// </para>
/// </remarks>
public sealed class UsuarioRolRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Un permiso del catalogo: la fila de <c>dbo.Permisos</c>.</summary>
/// <remarks>
/// EL CATALOGO EXISTE, PERO TODAVIA NO DECIDE NADA. Ninguna guarda lo consulta: las listas de
/// roles siguen escritas en el codigo (<c>Permisos.cs</c>, <c>ContenidoWebEndpoints.cs</c>...).
/// Se mapea para que la via de SQLite del arnes de pruebas pueda sembrarlo igual que lo siembra
/// el esquema, y asi el dia que una guarda empiece a leerlo, la suite mida algo en vez de pasar
/// sobre una tabla vacia. Es el defecto U8 del plan de construccion.
/// </remarks>
public sealed class PermisoRow
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>El reparto de un permiso a un rol: la fila de <c>dbo.RolesPermisos</c>.</summary>
/// <remarks>Ver la nota de <see cref="PermisoRow"/>: inventario, todavia no guarda.</remarks>
public sealed class RolPermisoRow
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public int PermisoId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditLogRow
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? PreviousValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Cabecera trazable de una importación institucional. Conserva huellas y resultados, no el
/// archivo original. El contenido necesario para confirmar vive por fila y deja de ser una caja
/// negra: cada decisión puede relacionarse con su número de origen.
/// </summary>
public sealed class LoteImportacionRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Formato { get; set; } = string.Empty;
    public string HuellaArchivo { get; set; } = string.Empty;
    public string HuellaPlan { get; set; } = string.Empty;
    public int VersionContrato { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string? ClaveIdempotencia { get; set; }
    public DateTime FechaPrevisualizacion { get; set; }
    public DateTime? FechaAplicacion { get; set; }
    public DateTime? FechaDepuracion { get; set; }
    public int TotalFilas { get; set; }
    public int FilasImportables { get; set; }
    public int FilasRechazadas { get; set; }
    public int FilasAplicadas { get; set; }
    public int FilasExcluidas { get; set; }
}

public sealed class FilaImportacionRow
{
    public long Id { get; set; }
    public long LoteImportacionId { get; set; }
    public int NumeroFila { get; set; }
    public string ContenidoNormalizadoJson { get; set; } = "{}";
    public string Resultado { get; set; } = string.Empty;
    public bool PuedeImportarse { get; set; }

    /// <summary>Con qué registro ya existente coincide esta fila, si coincide con alguno.</summary>
    /// <remarks>
    /// <b>SIN FORANEA, Y NO ES UN DESCUIDO.</b> A qué tabla apunta lo dice el <c>Dominio</c> del
    /// lote, y una foránea no puede apuntar a dos tablas. Hasta esta
    /// columna se llamaba <c>IdFestivalCoincidente</c> y tenía foránea a <c>dbo.Festivales</c>: el
    /// almacén decía ser genérico —el dominio estaba en la cabecera— y no lo era.
    /// </remarks>
    public long? RegistroCoincidenteId { get; set; }

    /// <summary>Qué registro creó esta fila al confirmarse. Mismo criterio que el anterior.</summary>
    public long? RegistroCreadoId { get; set; }
}

public sealed class HallazgoImportacionRow
{
    public long Id { get; set; }
    public long FilaImportacionId { get; set; }
    public string Severidad { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Campo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class DecisionImportacionRow
{
    public long Id { get; set; }
    public long LoteImportacionId { get; set; }
    public long FilaImportacionId { get; set; }
    public int UsuarioId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}

public sealed class HistorialRevisionRegistroRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? Comentario { get; set; }
    public string? MotivoRechazo { get; set; }
    public string? CamposObservados { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime Fecha { get; set; }
    public string? MetadataJson { get; set; }

    // INSTANTANEA (). Se copian AL ESCRIBIR y no se resuelven al leer: la ficha de
    // revision armaba el nombre de la organizacion por JOIN contra el presente, de modo que
    // renombrarla reescribia en silencio todo su historial pasado.
    //
    // Anulables porque el historial anterior a este cambio no los tiene, y no se inventan hacia
    // atras: una fila vacia dice la verdad —«esto no se guardo entonces»—; rellenarla con el
    // valor de hoy seria la falsificacion que este bloque existe para impedir.
    //
    // NO hay numero de documento aqui, y es deliberado: una entrada de historial no lo necesita
    // para ser veraz, y copiarlo en cada cambio de estado multiplicaria la cedula de un ciudadano
    // por todo el historial. El vigente vive una sola vez en EntidadesResponsable.
    public int? OrganizacionId { get; set; }
    public string? OrganizacionNombre { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ActorNombre { get; set; }
}

public sealed class UserVerificationCodeRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class NotificationRow
{
    public long Id { get; set; }
    public int? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AccessScope { get; set; } = "external";
    public string Channel { get; set; } = "internal";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "pendiente";
    public string? ModuloId { get; set; }
    public string? RecordId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

public sealed class RecordLinkRequestRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public int RequestingUserId { get; set; }

    /// <summary>
    /// Entidad del ecosistema en cuyo nombre se pide la vinculacion, o <c>null</c> si quien
    /// pide es una persona registrada que aun no representa a ningun actor. Sustituye a la
    /// antigua <c>EntidadAliadaId</c>: el ancla ya no es una entidad aliada sino una fila de
    /// <c>Entidades</c>, alcanzada por <c>UsuariosEntidades</c>.
    /// </summary>
    public int? EntidadId { get; set; }
    public string RequestedScope { get; set; } = "responsable";
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceText { get; set; }
    public string Status { get; set; } = "pendiente";
    public int? ReviewerUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class RecordDuplicateCandidateRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string SourceRecordId { get; set; } = string.Empty;
    public string CandidateRecordId { get; set; } = string.Empty;
    public string SimilarityLevel { get; set; } = "media";
    public decimal? SimilarityScore { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public string Status { get; set; } = "pendiente";
    public string? Decision { get; set; }
    public string? DecisionComment { get; set; }
    public int? ReviewerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class RecordQualityFlagRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string FlagType { get; set; } = string.Empty;
    public string Severity { get; set; } = "media";
    public string Status { get; set; } = "abierta";
    public string? Detail { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Un borrador automático de un formulario largo, guardado mientras alguien lo llena.
/// </summary>
/// <remarks>
/// <para>
/// SIRVE A LOS DOS LADOS DE LA CASA. Nació para el asistente externo de Festival y desde el 11 de
/// septiembre de 2026 lo usa también la consola institucional: lo que se guarda es lo mismo —un
/// dominio, quién escribe, un JSON y una versión— y una tabla gemela solo daría dos sitios donde
/// resolver el mismo conflicto de versión.
/// </para>
/// <para>
/// <c>OrganizacionId</c> ES OPCIONAL PORQUE QUIEN ADMINISTRA DESDE EL MINISTERIO NO ACTUA EN
/// NOMBRE DE NINGUNA ENTIDAD DEL ECOSISTEMA. Rellenarlo con la organización institucional sería
/// inventar un dato que después acabaría contándose en algún informe de participación.
/// </para>
/// </remarks>
public sealed class BorradorDeProcesoRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public int? OrganizacionId { get; set; }
    public int PersonaId { get; set; }
    public string Estado { get; set; } = "borrador";
    public string DatosJson { get; set; } = "{}";
    public int Version { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

public sealed class AdministrationClaimRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string CanonicalRecordId { get; set; } = string.Empty;
    public int RequestingOrganizationId { get; set; }
    public int RequestingPersonId { get; set; }
    public long? ProcessDraftId { get; set; }
    public string Status { get; set; } = "borrador";
    public string SignalsJson { get; set; } = "[]";
    public string Justification { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public int? DeciderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}

public sealed class AdministrationClaimClarificationRow
{
    public long Id { get; set; }
    public long ClaimId { get; set; }
    public int RequestedById { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int? RespondedById { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public sealed class AdministrationTransferRow
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string CanonicalRecordId { get; set; } = string.Empty;
    public int PreviousOrganizationId { get; set; }
    public int NewOrganizationId { get; set; }

    /// <summary>
    /// La reclamación que produjo la transferencia, si la hubo.
    /// </summary>
    /// <remarks>
    /// ES ANULABLE DESDE EL 12 DE SEPTIEMBRE DE 2026. Una <b>liberación</b> también cambia quién
    /// administra —de la organización a la institución— pero no tiene reclamación detrás: no la
    /// pidió nadie, la decidió el Programa. Dejarla fuera del historial daría un registro con
    /// huecos; inventarle una reclamación vacía ensuciaría la bandeja con solicitudes que nadie hizo.
    /// </remarks>
    public long? ClaimId { get; set; }
    public int DeciderId { get; set; }
    public string? Reason { get; set; }
    public string? EvidenceJson { get; set; }
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public DateTime ExecutedAt { get; set; }
}

public sealed class AdministrationReconciliationRow
{
    public long Id { get; set; }
    public long ClaimId { get; set; }
    public string HistoricalValuesJson { get; set; } = "{}";
    public string DraftValuesJson { get; set; } = "{}";
    public string? SelectionsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class EntityProfileRow
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? IdentificationNumber { get; set; }
    public string? IdentificationType { get; set; }
    public string? LegalName { get; set; }
    public string? Description { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? OtherUrl { get; set; }
    // Compatibilidad exclusiva para constructores de pruebas antiguas. EF no persiste estas
    // propiedades: el modelo vigente de organizaciones solo usa la sede.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CoverageLevel { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? DepartmentCode { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? MunicipalityCode { get; set; }
    /// <summary>
    /// La foto de perfil de la organización, en el banco de archivos.
    /// </summary>
    /// <remarks>
    /// <b>ES PUBLICA</b> —decisión de la dirección de producto— y por eso
    /// apunta al banco, que sirve por <c>/publico/archivos/{id}</c> sin cuenta y con ETag. No se
    /// guarda una RUTA en texto: eso sería confiar en que el fichero siga ahí y en que alguien la
    /// escribió bien. Nulo es lo corriente, y entonces se pintan las iniciales, que siguen siendo
    /// el respaldo de quien no sube foto.
    /// </remarks>
    public int? ArchivoFotoId { get; set; }

    public string? HeadquartersDepartmentCode { get; set; }
    public string? HeadquartersMunicipalityCode { get; set; }
    public string? AddressText { get; set; }

    /// <summary>
    /// Donde esta la organizacion en la escalera institucional.
    /// </summary>
    /// <remarks>
    /// Los cuatro valores validos y su significado viven en <c>EstadosDeOrganizacion</c>. La base
    /// los fija con <c>CK_Entidades_EstadoRegistro</c>. NO son los del circuito editorial: una
    /// organizacion no se publica ni se aprueba, se registra, se verifica o se archiva.
    /// </remarks>
    public string StatusCode { get; set; } = "registrada";
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// La organizacion que responde por un registro mientras ninguna del ecosistema lo reclame.
    /// Solo puede haber una: lo garantiza el indice unico filtrado UQ_Entidades_EsInstitucional.
    /// </summary>
    public bool IsInstitutional { get; set; }

    public int CreatedByUserId { get; set; }
    public int? ResponsibleUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

/// <summary>
/// La persona natural que responde por una organizacion del ecosistema.
/// </summary>
/// <remarks>
/// <para>
/// ESTA CLASE VIVE APARTE DE <see cref="EntityProfileRow"/> A PROPOSITO, y el motivo es de
/// privacidad. <c>Entidades</c> se lee en caminos ANONIMOS —la ficha publica de un festival
/// resuelve sus entidades socias—, asi que el documento de un ciudadano puesto ahi saldria al
/// publico con cualquier <c>SELECT *</c>, cualquier <c>Include</c> de mas o cualquier DTO que se
/// serialice entero. En un satelite 1:1, publicarlo exige escribir un <c>JOIN</c> a proposito.
/// </para>
/// <para>
/// NO LLEVA CREDENCIAL, Y ESO ES UNA DECISION. Nacio el 25 ago 2026 con correo de acceso y hash
/// de contrasena, para que la organizacion entrara por si misma. Se retiraron el mismo dia: el
/// modelo que ya existe —persona con cuenta propia, y vinculo activo en <c>UsuariosEntidades</c>
/// comprobado en CADA peticion— hace lo mismo y mejor, porque una marca en el tiquete se
/// comprueba una vez al entrar y el vinculo se comprueba siempre. Una credencial que sobra no es
/// neutra: un hash que nadie escribe, sentado al lado de la cedula de un ciudadano, es la columna
/// que acaba saliendo el dia que alguien proyecte esta tabla.
/// </para>
/// <para>
/// TODO CAMPO <c>Responsable*</c> ES DATO PERSONAL DE UN TERCERO. Ninguno puede aparecer en una
/// respuesta sin sesion; lo vigila <c>FichaPublicaSinDatosPersonalesTests</c>, que se escribio
/// ANTES que esta clase precisamente para poder ponerse rojo el dia en que alguien la proyecte.
/// </para>
/// </remarks>
public sealed class EntidadResponsableRow
{
    /// <summary>Clave primaria y foranea a la vez: uno a uno con <c>Entidades</c>.</summary>
    public int IdEntidad { get; set; }

    public string ResponsableNombre { get; set; } = string.Empty;
    public string? ResponsablePrimerNombre { get; set; }
    public string? ResponsableSegundoNombre { get; set; }
    public string? ResponsablePrimerApellido { get; set; }
    public string? ResponsableSegundoApellido { get; set; }
    public string ResponsableTipoDocumento { get; set; } = string.Empty;
    public string ResponsableNumeroDocumento { get; set; } = string.Empty;
    public string ResponsableCorreo { get; set; } = string.Empty;
    public string? ResponsableTelefono { get; set; }
    public DateTime ResponsableDesde { get; set; }

    /// <summary>Ley 1581 de 2012: la autorizacion del titular se guarda, no se presume.</summary>
    public bool ResponsableAutorizacionDatos { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

public sealed class UserEntityRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EntityId { get; set; }
    public string EntityRole { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Referencia de procedencia histórica que respalda una coincidencia de Festival.</summary>
public sealed class ReferenciaHistoricaFestivalRow
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }
    public int FestivalId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ParticipationSubmissionRow
{
    public string Reference { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

/// <summary>
/// Un texto editable del sitio público. Una fila por clave del registro del CMS.
/// <para>
/// <c>Draft</c> es lo que ve el editor; <c>Published</c> es lo que ve el visitante.
/// Que <c>Published</c> sea nulo significa que esa clave nunca se publicó y el sitio
/// sirve su texto compilado. Guardar un borrador no toca <c>Published</c>: solo
/// publicar mueve ese valor.
/// </para>
/// </summary>
public sealed class ContenidoWebRow
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int CharacterLimit { get; set; }
    public string Draft { get; set; } = string.Empty;
    public string? Published { get; set; }
    /// <summary>
    /// Cuándo se retiró del sitio público, si se retiró. Nulo en los dos casos
    /// restantes; se distingue de «nunca publicado» porque allí también
    /// <c>Published</c> es nulo, pero esta marca no está puesta.
    /// </summary>
    public DateTime? Retired { get; set; }
    /// <summary>Token de concurrencia: EF lo incluye en el WHERE de cada UPDATE.</summary>
    public int Version { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Una entrada del historial de un texto del sitio (incremento 2E).
/// <para>
/// Guarda <b>solo el valor nuevo</b>, no el anterior. El anterior es el
/// <c>Valor</c> de la entrada previa de esa misma clave, y para la primera es el
/// texto compilado del catálogo: guardarlo otra vez duplicaría el
/// almacenamiento y abriría la puerta a que las dos copias se contradigan.
/// </para>
/// <para>
/// El historial es de <b>textos</b>. La nómina del equipo queda fuera a
/// propósito: es un único JSON con las fotografías incrustadas, y una entrada
/// por cambio guardaría megabytes repetidos cada vez que alguien corrige una
/// tilde.
/// </para>
/// </summary>
public sealed class HistorialDeContenidoWebRow
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>guardado | publicado | retirado | republicado | importado.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// El texto que quedó tras la acción. Nulo cuando la acción no cambió el
    /// texto —retirar quita del sitio, no reescribe el borrador—.
    /// </summary>
    public string? Value { get; set; }

    public string User { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

/// <summary>
/// La nómina del equipo del PNMC, guardada como un único registro.
/// <para>
/// Es una fila y no una tabla de personas a propósito: el panel nunca guarda a
/// una sola persona, guarda la nómina entera —añadir, reordenar y quitar ocurren
/// en el mismo formulario y se publican juntas—. Con una fila por persona harían
/// falta borrado lógico, columna de orden y un estado de publicación por persona
/// para reproducir esa misma operación, y quedaría la posibilidad de publicar
/// media nómina.
/// </para>
/// <para>
/// Las fotografías viajan incrustadas dentro del JSON, junto a la persona a la
/// que pertenecen. Guardarlas en una tabla aparte obligaría a recoger las que
/// quedan sin dueño al quitar a alguien; aquí desaparecen con ella.
/// </para>
/// </summary>
public sealed class EquipoWebRow
{
    /// <summary>
    /// Siempre <see cref="SingletonId"/>. Una restricción CHECK en la tabla lo
    /// impone, para que «hay una sola nómina» sea un hecho de la base y no una
    /// costumbre del código que la escribe.
    /// </summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>JSON con la nómina que ve el editor. Nunca nulo: sin personas es <c>[]</c>.</summary>
    public string Draft { get; set; } = "[]";

    /// <summary>
    /// JSON con la nómina que ve el visitante, o nulo si nunca se publicó. Nulo y
    /// <c>[]</c> significan cosas distintas: «todavía no hay nómina publicada» y
    /// «se publicó una nómina vacía a propósito».
    /// </summary>
    public string? Published { get; set; }

    /// <summary>Token de concurrencia: EF lo incluye en el WHERE de cada UPDATE.</summary>
    public int Version { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Una ranura de imagen administrable del sitio publico (incremento de medios, 29 ago 2026).
/// <para>
/// Una fila por SITIO donde va una imagen, no por archivo. Dos ranuras que hoy comparten el
/// mismo archivo llevan claves distintas a proposito: son decisiones editoriales distintas, y
/// quien cambie la portada de Ejes no quiere cambiar la de Ecosistema.
/// </para>
/// <para>
/// La mitad <c>Draft*</c> es lo que ve el editor; la mitad <c>Published*</c> es lo que ve el
/// visitante. Que <c>PublishedContent</c> sea nulo significa que esa clave no esta en el sitio y
/// el front sirve su imagen compilada. Publicar es copiar las siete columnas de una mitad a la
/// otra; retirar es anular las siete de <c>Published*</c> y conservar el borrador.
/// </para>
/// <para>
/// Los bytes viven en la base y no en disco porque el respaldo de PNMC_LOCAL tiene que seguir
/// bastando para restaurar el sitio: con los bytes fuera, un <c>.bak</c> restaurado sobre un
/// disco vacio deja las filas apuntando a archivos que no existen. El razonamiento completo, con
/// los numeros, esta en <c>pnmc-database/schema/V20260829_01__medios_web.sql</c>.
/// </para>
/// </summary>
public sealed class ImagenWebRow
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>fondo | logotipo. Decide como se encaja: object-cover o object-contain.</summary>
    public string Use { get; set; } = "fondo";
    public int? SuggestedWidth { get; set; }
    public int? SuggestedHeight { get; set; }

    /// <summary>
    /// Falso para la marca institucional ajena —GOV.CO, el sello de Colombia, el Ministerio de
    /// las Culturas—. Que el panel deje reemplazar el escudo nacional no es una funcion. El
    /// CHECK <c>CK_MediosWeb_NoEditableSinBytes</c> lo impone en la base, para que no dependa de
    /// que el API se acuerde de comprobarlo.
    /// </summary>
    public bool Editable { get; set; } = true;

    /// <summary>Texto alternativo. Pertenece a la imagen, no al catalogo de textos.</summary>
    public string AltText { get; set; } = string.Empty;

    public byte[]? DraftContent { get; set; }
    public string? DraftMime { get; set; }
    public int? DraftBytes { get; set; }
    public int? DraftWidth { get; set; }
    public int? DraftHeight { get; set; }
    /// <summary>SHA-256 en hexadecimal minusculo. Es el ETag, el <c>?v</c> y la prueba de que el archivo no cambio.</summary>
    public string? DraftHash { get; set; }
    /// <summary>WebP de 320 px. En columna aparte para que abrir el panel no traiga los originales.</summary>
    public byte[]? DraftThumbnail { get; set; }

    public byte[]? PublishedContent { get; set; }
    public string? PublishedMime { get; set; }
    public int? PublishedBytes { get; set; }
    public int? PublishedWidth { get; set; }
    public int? PublishedHeight { get; set; }
    public string? PublishedHash { get; set; }
    public byte[]? PublishedThumbnail { get; set; }

    /// <summary>
    /// Cuando se retiro del sitio, si se retiro. Nulo en los dos casos restantes; se distingue de
    /// «nunca publicada» porque alli tambien <c>PublishedContent</c> es nulo, pero esta marca no
    /// esta puesta. Igual que en <see cref="ContenidoWebRow.Retired"/>.
    /// </summary>
    public DateTime? Retired { get; set; }

    /// <summary>Token de concurrencia: EF lo incluye en el WHERE de cada UPDATE.</summary>
    public int Version { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Una entrada del historial de una imagen del sitio.
/// <para>
/// NO GUARDA EL ARCHIVO, y es una decision explicita del usuario:
/// «si se remplaza la imagen, desaparece, no se guarda». Guarda quien, cuando, que accion y las
/// SENAS del archivo que estuvo ahi —tipo, peso, medidas y huella—, que es lo que permite leer el
/// registro sin conservar megabytes.
/// </para>
/// <para>
/// LA CONSECUENCIA: no hay «restaurar». Reemplazar una imagen borra la anterior para siempre, y
/// volver atras exige subir el archivo otra vez. La ruta de restaurar se retiro en el mismo cambio,
/// porque una ruta que no puede cumplir lo que promete es peor que no tenerla.
/// </para>
/// <para>
/// Las senas son nulas cuando la accion no trae archivo: retirar quita del sitio y no reescribe
/// el borrador.
/// </para>
/// </summary>
public sealed class HistorialDeImagenWebRow
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>guardado | publicado | retirado | republicado. Sin «importado»: el respaldo JSON del panel no lleva imagenes.</summary>
    public string Action { get; set; } = string.Empty;

    public string? Mime { get; set; }
    public int? Bytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Hash { get; set; }

    public string User { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

/*
 * ───────────────────────── Catálogo Editorial ─────────────────────────
 *
 * La ficha es RELACIONAL. Agentes, créditos, identificadores, accesos, fuentes y programas no
 * viven como cadenas agregadas dentro de la publicación: esa fue la tabla plana
 * `CatalogoEditorial`, retirada en `V20260904_03`, y es lo que impedía preguntar «qué publicó
 * este arreglista» sin leer texto libre.
 */

/// <summary>Una persona o entidad, registrada UNA vez. Las variantes las decide una persona.</summary>
public sealed class AgenteEditorialRow
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tipo { get; set; } = "persona";
    public string NombrePreferido { get; set; } = string.Empty;
    public string? FormaNormalizada { get; set; }
    public string? Seudonimo { get; set; }
    public string? Acronimo { get; set; }
    public bool RequiereRevision { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>Fuente verificable. `Url` admite nulo: hay documentos institucionales sin dirección pública.</summary>
public sealed class FuenteEditorialRow
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string? Url { get; set; }
    public DateTime? FechaFuente { get; set; }
    public DateTime FechaConsulta { get; set; }
    public string VerificadaPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}

public sealed class ProgramaEditorialRow
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public sealed class PublicacionEditorialRow
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Subtitulo { get; set; }
    public string? DesignacionVolumen { get; set; }
    public string? SerieOColeccion { get; set; }
    public string? Resumen { get; set; }
    public string? FechaEdtf { get; set; }
    public int? AnioInicio { get; set; }
    public int? AnioFin { get; set; }
    public string? Idioma { get; set; }

    /*
     * DESCRIPTORES DE PRESENTACION. Texto libre a proposito: Una revisión anterior decidio que los
     * vocabularios editoriales se administraran como catalogos versionados y que sus listas
     * siguen TBC. Cerrarlos aqui convertiria en regla unos valores que nadie ha aprobado.
     */
    public string? TipoPublicacion { get; set; }
    /// <summary>La categoría, ahora una fila de `Categorias` y no texto suelto.</summary>
    public int? CategoriaId { get; set; }
    public string? Ambito { get; set; }

    /// <summary>Ruta al fichero estatico del portal. No es un medio editable del CMS.</summary>
    public string? MiniaturaRuta { get; set; }

    // Los nueve que el diseño aprobado necesita para llenar sus tarjetas de detalle. Texto libre:
    // Una revisión anterior dejó los vocabularios editoriales sin aprobar y eso no ha cambiado.
    public string? SeccionPrincipal { get; set; }
    public string? RutaSeccion { get; set; }
    public string? PracticaMusical { get; set; }
    public string? Subcategoria { get; set; }
    public string? TamanoFormato { get; set; }
    /// <summary>Texto y no número: la fuente trae «48 p.», «2 v.». Normalizar es otra decisión.</summary>
    public string? Paginas { get; set; }
    /// <summary>Texto por el mismo motivo: «ca. 30 min».</summary>
    public string? Duracion { get; set; }
    public string? CamposAdicionales { get; set; }
    public string? TextoPortada { get; set; }

    /// <summary>Calidad de la ficha. NO decide visibilidad.</summary>
    public string EstadoCatalogacion { get; set; } = "pendiente_revision";

    /// <summary>Visibilidad. NO valida la ficha.</summary>
    public string EstadoPublicacion { get; set; } = "borrador";

    /// <summary>Se cita al guardar; si cambió, la escritura se rechaza en vez de pisar trabajo ajeno.</summary>
    public int Version { get; set; } = 1;

    public string DerechosEstado { get; set; } = "pendiente";
    public bool DerechosPermitePublicarFicha { get; set; }
    public bool DerechosPermitePublicarArchivo { get; set; }
    public string? DerechosLicenciaONota { get; set; }
    public long? DerechosFuenteId { get; set; }
    public DateTime? DerechosFechaVerificacion { get; set; }
    public string? DerechosVerificadoPor { get; set; }

    /*
     * LO QUE TRAE EL ACERVO REAL Y ANTES NO CABIA.
     *
     * Las siete salen de la carga de las 171 publicaciones del Proyecto Editorial y se modelan
     * como las modeló el desarrollo de septiembre, que ya hizo esta misma importación. Ninguna es
     * especulativa: la cuenta de cuántas fichas usa cada una está en `V20260913_04`.
     */

    /// <summary>Físico, Digital o Mixto. NO se deduce del soporte: se comprobó y no se sostiene.</summary>
    public string? Formato { get; set; }

    /// <summary>Lo que la fuente escribió cuando no pudo fijar la fecha. Nueve fichas.</summary>
    public string? NotaFecha { get; set; }

    /// <summary>La segunda práctica cuando la obra toca dos. Cincuenta y ocho fichas.</summary>
    public string? CategoriaSecundaria { get; set; }

    /// <summary>El ámbito escrito a mano cuando no entra en el vocabulario. Dieciséis fichas.</summary>
    public string? AmbitoTexto { get; set; }

    /// <summary>La nota de quien catalogó. Dieciocho fichas.</summary>
    public string? NotasCatalogacion { get; set; }

    /// <summary>Cuánta confianza da la ficha de origen: Alta, Media o Baja.</summary>
    public string? Confianza { get; set; }

    /// <summary>Banderas de revisión que trae la fuente, para ordenar la cola de catalogación.</summary>
    public bool RevisarClasificacion { get; set; }
    public bool RevisarCreditos { get; set; }

    /// <summary>De qué lámina del catálogo original salió la ficha. Es su procedencia.</summary>
    public string? DiapositivaOrigen { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>
/// Un término de las cuatro facetas con las que se describe de qué está hecha una publicación.
/// </summary>
/// <remarks>
/// UN SOLO VOCABULARIO PARA LOS CUATRO EJES —tipo de recurso (DCMI), contenido (RDA 336), medio
/// (RDA 337) y soporte (RDA 338)—, porque son cuatro listas con la misma forma. Ver
/// `V20260913_04__tipologia_editorial.sql`.
/// </remarks>
public sealed class TipologiaEditorialRow
{
    public int Id { get; set; }
    public string Eje { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    /// <summary>La norma de la que sale, para poder citarla sin codificarla en la pantalla.</summary>
    public string Norma { get; set; } = string.Empty;
    public int Orden { get; set; }
}

/// <summary>
/// Qué tipología tiene una publicación. Es de muchos a muchos EN LOS CUATRO EJES: 23 de las 171
/// fichas declaran más de un valor en alguno —un libro con su CD, una caja con DVD, CD y cartilla—.
/// </summary>
public sealed class PublicacionEditorialTipologiaRow
{
    public long PublicacionEditorialId { get; set; }
    public int TipologiaEditorialId { get; set; }
}

public sealed class PublicacionEditorialFuenteRow
{
    public long PublicacionEditorialId { get; set; }
    public long FuenteEditorialId { get; set; }
}

public sealed class CreditoEditorialRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public long AgenteEditorialId { get; set; }
    /// <summary>MARC Relators: `aut`, `cmp`, `arr`, `edt`, `pbl`…</summary>
    public string RolCodigo { get; set; } = string.Empty;
    /// <summary>Lo que se presenta. NO se deriva del código: es una decisión editorial.</summary>
    public string RolEtiqueta { get; set; } = string.Empty;
    public bool Principal { get; set; }
    public int Orden { get; set; }
}

public sealed class IdentificadorEditorialRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public string Esquema { get; set; } = "ISBN";
    /// <summary>Tal como llegó. Corregirlo en silencio borraría la prueba de que la fuente venía mal.</summary>
    public string CodigoRecibido { get; set; } = string.Empty;
    public string? Cualificador { get; set; }
    public bool? Valido { get; set; }
    public string? ObservacionValidacion { get; set; }
}

public sealed class AccesoEditorialRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public string Tipo { get; set; } = "enlace";
    public int? ArchivoId { get; set; }
    public string? Url { get; set; }
    public string? UbicacionFisica { get; set; }
    public string? Etiqueta { get; set; }
    public string? Nota { get; set; }
    public int Orden { get; set; }

    /// <summary>Cada acceso lleva SU decisión: una ubicación autorizada puede convivir con un archivo restringido.</summary>
    public string DerechosEstado { get; set; } = "pendiente";
    public bool DerechosPermitePublicarFicha { get; set; }
    public bool DerechosPermitePublicarArchivo { get; set; }
    public string? DerechosLicenciaONota { get; set; }
    public long? DerechosFuenteId { get; set; }
    public DateTime? DerechosFechaVerificacion { get; set; }
    public string? DerechosVerificadoPor { get; set; }
}

public sealed class PublicacionEditorialProgramaRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public long ProgramaEditorialId { get; set; }
    /// <summary>Obligatoria: la pertenencia se afirma con respaldo, no se infiere.</summary>
    public long FuenteEditorialId { get; set; }
    public DateOnly? VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public string VerificadaPor { get; set; } = string.Empty;
}

/// <summary>Un término por fila: buscar sobre una cadena separada por comas no usa índice.</summary>
public sealed class PalabraClaveEditorialRow
{
    public long Id { get; set; }
    public long PublicacionEditorialId { get; set; }
    public string Termino { get; set; } = string.Empty;
}

/// <summary>
/// Una noticia del portal.
/// </summary>
/// <remarks>
/// <para>
/// UNA SOLA COLUMNA DE ESTADO, a diferencia del Catálogo Editorial. Allí hacían falta dos —calidad
/// de la ficha y visibilidad— porque catalogar y publicar son decisiones de equipos distintos. Una
/// noticia no se cataloga: se escribe, se revisa y se publica.
/// </para>
/// <para>
/// <c>FechaPublicacion</c> ES UN DATO, NO UN EFECTO de publicar. Una noticia publicada hoy puede
/// fecharse el lunes, y el orden del portal es cronológico por esa fecha, no por cuándo alguien
/// pulsó el botón. Por eso la base exige que una noticia publicada la tenga.
/// </para>
/// </remarks>
public sealed class NoticiaRow
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Resumen { get; set; } = string.Empty;
    public string? Cuerpo { get; set; }
    public DateOnly? FechaPublicacion { get; set; }
    public string? ImagenRuta { get; set; }
    public string? ImagenAlternativa { get; set; }
    public string? AutoriaNombre { get; set; }
    /// <summary>La categoría, ahora una fila de `Categorias` y no texto suelto.</summary>
    public int? CategoriaId { get; set; }
    public string Estado { get; set; } = "borrador";
    public int Version { get; set; } = 1;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>Una etiqueta de una noticia. Una fila por término, para que filtrar use índice.</summary>
public sealed class EtiquetaNoticiaRow
{
    public long Id { get; set; }
    public long NoticiaId { get; set; }
    public string Termino { get; set; } = string.Empty;
}

/// <summary>
/// Un evento de la Agenda.
/// </summary>
/// <remarks>
/// <para>
/// LA MODALIDAD DECIDE QUE DATOS SON OBLIGATORIOS. Un evento presencial publicado sin lugar no se
/// puede anunciar —nadie sabría a dónde ir— y uno virtual sin enlace tampoco. La base lo impone
/// con <c>CK_EventosAgenda_DatosDeModalidad</c>, no solo el endpoint.
/// </para>
/// <para>
/// <c>FechaFin</c> NULA SIGNIFICA «el mismo día», no «sin definir». Obligarla convertiría cada
/// evento de una tarde en dos campos idénticos.
/// </para>
/// <para>
/// UN EVENTO PASADO NO DESAPARECE del portal, a diferencia de una noticia futura que espera su
/// fecha: lo que ya ocurrió sigue siendo información pública y se presenta como finalizado.
/// </para>
/// </remarks>
public sealed class EventoAgendaRow
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public string Modalidad { get; set; } = "presencial";
    public string? Lugar { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Url { get; set; }
    public string? ImagenRuta { get; set; }
    public string? ImagenAlternativa { get; set; }
    /// <summary>La categoría, ahora una fila de `Categorias` y no texto suelto.</summary>
    public int? CategoriaId { get; set; }
    /// <summary>Quién organiza. La tarjeta del diseño aprobado lo enseña.</summary>
    public string? Organizador { get; set; }
    /// <summary>Texto extenso, aparte del resumen que va en las tarjetas.</summary>
    public string? DescripcionLarga { get; set; }
    public TimeOnly? HoraFin { get; set; }
    /// <summary>nacional · departamental · municipal, atado a sus códigos por CHECK.</summary>
    public string NivelCobertura { get; set; } = "nacional";
    /// <summary>Posición manual en las listas. Es lo que permite destacar a mano.</summary>
    public int? OrdenVisualizacion { get; set; }
    /// <summary>El Festival del que forma parte la actividad, cuando lo hay.</summary>
    public int? FestivalId { get; set; }
    public string Estado { get; set; } = "borrador";
    public int Version { get; set; } = 1;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>Una etiqueta de un evento. Una fila por término, para que filtrar use índice.</summary>
public sealed class EtiquetaEventoAgendaRow
{
    public long Id { get; set; }
    public long EventoAgendaId { get; set; }
    public string Termino { get; set; } = string.Empty;
}

/// <summary>
/// Un mercado musical: un proceso del Ecosistema, al mismo nivel que un Festival.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO GUARDA SU NUMERO DE EDICIONES.</b> La tabla heredada sí lo guardaba, en una columna que
/// alguien tenía que mantener a mano mientras la lista de ediciones decía otra cosa. Aquí se cuenta.
/// </para>
/// <para>
/// <b>NO GUARDA LA EDICION DE ESTE AÑO.</b> La tabla heredada la tenía en cuatro columnas, de modo
/// que escribir la de 2026 borraba la de 2025 sin dejar rastro. Las ediciones son
/// <see cref="EdicionMercadoRow"/>.
/// </para>
/// <para>
/// <b>EL FESTIVAL ASOCIADO ES UNA RELACION, NO UN NOMBRE.</b> La heredada lo guardaba dos veces
/// —clave ajena y texto suelto, sin nada que obligara a que coincidieran—. Aquí solo está la clave:
/// el nombre se lee del festival. La pertenencia a la misma organización la comprueba el servidor,
/// porque es una regla entre dos filas de dos tablas que un CHECK no puede expresar.
/// </para>
/// </remarks>
public sealed class MercadoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public int? AlcanceMercadoId { get; set; }
    public int? ModalidadMercadoId { get; set; }
    public string? Periodicidad { get; set; }
    public string? PeriodicidadDetalle { get; set; }

    public string? CorreoMercado { get; set; }
    public string? TelefonoMercado { get; set; }
    public string? SitioWebMercado { get; set; }
    public string? InstagramMercado { get; set; }
    public string? FacebookMercado { get; set; }
    public string? OtroEnlaceMercado { get; set; }
    public string? ObservacionesContacto { get; set; }

    public string NivelCobertura { get; set; } = string.Empty;

    /// <summary>
    /// Codigo DIVIPOLA del departamento, o <c>null</c> en cobertura nacional.
    /// </summary>
    /// <remarks>
    /// ES ANULABLE A PROPOSITO, por la misma razón que en <see cref="FestivalRow"/>: el CHECK no
    /// comprueba solo que el nivel sea uno de los tres, sino la COHERENCIA entre nivel y territorio.
    /// Una cadena vacía no es NULL para la restricción, y el guardado moriría con un 500.
    /// </remarks>
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? LugarEspecifico { get; set; }

    public bool SeRealizaEnElMarcoDeUnFestival { get; set; }
    public int? FestivalId { get; set; }

    public int OrganizacionPrincipalId { get; set; }
    public string EstadoRegistro { get; set; } = "borrador";
    public bool Activo { get; set; } = true;
    public int? IdUsuarioCreador { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaEnvioARevision { get; set; }
    public DateTime? FechaPublicacion { get; set; }
    public DateTime? FechaArchivado { get; set; }
}

/// <summary>Una realización concreta de un mercado musical.</summary>
/// <remarks>
/// <b>NO TIENE ESTADO DE REGISTRO NI CIRCUITO DE REVISION.</b> Las ediciones de un mercado no pasan
/// por revisión institucional: quedó fijado, y
/// es la única diferencia real de ciclo de vida respecto de la edición de un festival. El control
/// está en la puerta de entrada del mercado. Lo que sí conserva son sus DOS ejes de estado: el ciclo
/// real del acontecimiento y si se enseña o no, que no son lo mismo —un mercado cancelado que sigue
/// publicado es información legítima—.
/// </remarks>
public sealed class EdicionMercadoRow
{
    public int Id { get; set; }
    public int MercadoId { get; set; }
    public int Anio { get; set; }
    public int? NumeroEdicion { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? LugarEspecifico { get; set; }
    public string Estado { get; set; } = "en_preparacion";
    public string EstadoVisibilidad { get; set; } = "borrador";
    public int? IdUsuarioCreador { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaPublicacion { get; set; }
}

/// <summary>Un alcance posible de un mercado musical. Vocabulario controlado.</summary>
public sealed class AlcanceMercadoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int OrdenVisualizacion { get; set; }
}

/// <summary>Una modalidad posible de un mercado musical. Vocabulario controlado.</summary>
public sealed class ModalidadMercadoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int OrdenVisualizacion { get; set; }
}

/// <summary>Una práctica musical que congrega un mercado.</summary>
public sealed class MercadoPracticaMusicalRow
{
    public int Id { get; set; }
    public int MercadoId { get; set; }
    public int PracticaMusicalId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Un territorio sonoro donde ocurre un mercado.</summary>
public sealed class MercadoTerritorioSonoroRow
{
    public int Id { get; set; }
    public int MercadoId { get; set; }
    public int TerritorioSonoroId { get; set; }
    public DateTime FechaCreacion { get; set; }
}
