using PNMC.Api.ConsultaGuiada;
using Xunit;
using Xunit.Abstractions;

namespace PNMC.Api.Tests;

/// <summary>
/// Preguntas escritas como las escribe la gente, con la consulta que deberían alcanzar.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES UN INSTRUMENTO DE MEDIDA, NO UNA PUERTA MAS.</b> Hasta lo único
/// que comprobaba el intérprete eran sus doce sugerencias —las preguntas que el propio asistente
/// ofrece escritas—, y esas las resuelve por construcción. No había forma de responder «¿entiende
/// mejor que ayer?» con un número, así que cualquier cambio en las pistas se defendía con una
/// opinión.
/// </para>
/// <para>
/// <b>FUNCIONA COMO UN TRINQUETE:</b> declara el suelo medido y falla si se baja de ahí. Subirlo es
/// deliberado —se edita <see cref="SueloDeAciertos"/> y se justifica en la revisión—, igual que los
/// techos de `pnmc-web/trinquete`. Y cuando falla, <b>escribe qué preguntas falló y con qué
/// contestó</b>: una cifra sin el desglose no dice dónde mirar.
/// </para>
/// <para>
/// <b>LAS PREGUNTAS SON DE VERDAD.</b> Sin tildes, con erratas, con abreviaturas y con territorio
/// dentro, que es como se escribe en un buscador. Un banco escrito con la formulación que el
/// catálogo espera mediría el catálogo contra sí mismo.
/// </para>
/// </remarks>
public sealed class BancoDePreguntasTests(ITestOutputHelper salida)
{
    /// <summary>
    /// Cuántas del banco tiene que acertar el intérprete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SOLO SUBE. Si un cambio en las pistas hace que acierte más, se sube aquí y queda escrito
    /// cuánto mejoró; si hace que acierte menos, la prueba lo dice antes de que llegue a producción.
    /// </para>
    /// <para>
    /// <b>29 al medirlo por primera vez</b> (17 de septiembre de 2026, 80 %), con el intérprete que
    /// buscaba subcadenas y se quedaba con la primera consulta que coincidiera. <b>33</b> (91 %) ese
    /// mismo día, al comparar por palabras con raíz y erratas de una letra, puntuar la especificidad
    /// en vez de fiarlo al orden del catálogo, y declarar el vocabulario que faltaba. <b>57 de 61</b>
    /// (93 %) ese mismo día, al leer el sitio y el año que la pregunta nombra y enseñárselos a las
    /// consultas, que ahora saben acotarse. <b>76 de 79</b> (96 %) al leer también el estado, que era
    /// lo único que ninguna consulta veía: todas partían de la lectura pública y los quince
    /// borradores y los ocho Festivales en revisión de la base eran invisibles para el asistente.
    /// <b>77 de 80</b> al medir por primera vez el espacio externo, que hasta entonces solo tenía
    /// comprobadas sus cuatro sugerencias. <b>80 de 83</b> al entender los estados negados —«sin
    /// publicar», «que no estén archivados»—, que es como se pregunta por lo que falta por hacer.
    /// </para>
    /// <para>
    /// EL BANCO CRECIO CON EL SUELO, y eso importa para leer la cifra: con las veinticuatro
    /// preguntas que se añadieron el intérprete acertaba las cincuenta y dos primeras enteras, así
    /// que se escribieron nueve más <b>sin mirar si acertaba</b>. Seis las resolvió sin que nadie
    /// hubiera declarado esa forma de preguntar; tres no, y siguen aquí sin arreglar a propósito:
    /// un banco al cien por cien no mide nada.
    /// </para>
    /// <para>
    /// LAS TRES QUE FALLAN, FALLAN DICIENDO QUE NO ENCONTRARON CONSULTA, que es como el intérprete
    /// declara que debe fallar. Ninguna contesta otra cosa.
    /// </para>
    /// <para>
    /// EL BANCO SE ENDURECIO OTRA VEZ AL ARREGLARLO. En el corte del estado se resolvieron dos de
    /// las cuatro que fallaban, así que se escribieron ocho preguntas nuevas sin mirar si acertaba:
    /// seis las resolvió, una destapó un hueco de verdad —nadie contestaba «cuántos hay sin
    /// publicar»— y otra sigue fallando. Arreglar sin endurecer sube el porcentaje sin que el
    /// intérprete entienda más.
    /// </para>
    /// </remarks>
    private const int SueloDeAciertos = 81;

    /// <summary>
    /// El banco institucional: la pregunta tal cual se escribiría, y qué debería contestar.
    /// </summary>
    public static readonly (string Pregunta, string Esperada)[] Institucional =
    [
        // ---------- Formuladas como las ofrece el asistente ----------
        ("¿Cómo está la plataforma hoy?", CatalogoDeConsultas.Resumen),
        ("¿Cuántos festivales hay registrados?", CatalogoDeConsultas.Resumen),
        ("cuantos festivales tenemos", CatalogoDeConsultas.Resumen),
        ("dame el panorama general", CatalogoDeConsultas.Resumen),
        ("¿Qué departamentos tienen más Festivales?", CatalogoDeConsultas.Departamentos),
        ("distribución de festivales por departamento", CatalogoDeConsultas.Departamentos),
        ("distribución de festivales por departamento en el Huila", CatalogoDeConsultas.Departamentos),
        ("en qué departamentos hay más presencia", CatalogoDeConsultas.Departamentos),
        ("¿Qué municipios concentran más festivales?", CatalogoDeConsultas.Municipios),
        ("municipios con mayor número de festivales", CatalogoDeConsultas.Municipios),
        ("¿Cuántos municipios tienen festival?", CatalogoDeConsultas.Cobertura),
        ("¿Qué cobertura territorial tenemos?", CatalogoDeConsultas.Cobertura),
        ("¿Dónde no hay festivales?", CatalogoDeConsultas.SinFestival),
        ("qué territorios están sin cobertura", CatalogoDeConsultas.SinFestival),
        ("¿Cuántos mercados musicales hay?", CatalogoDeConsultas.Mercados),
        ("¿Cuántos mercados están publicados?", CatalogoDeConsultas.Mercados),
        ("resumen de mercados", CatalogoDeConsultas.Mercados),
        ("¿En qué departamentos hay mercados?", CatalogoDeConsultas.MercadosPorDepartamento),
        ("mercados musicales por territorio", CatalogoDeConsultas.MercadosPorDepartamento),
        ("¿Qué está pendiente de revisar?", CatalogoDeConsultas.Pendientes),
        ("¿Cuántas solicitudes hay en la bandeja?", CatalogoDeConsultas.Pendientes),
        ("qué necesita atención", CatalogoDeConsultas.Pendientes),
        ("qué me falta por revisar hoy", CatalogoDeConsultas.Pendientes),
        ("¿Qué información está incompleta?", CatalogoDeConsultas.Calidad),
        ("qué festivales tienen datos faltantes", CatalogoDeConsultas.Calidad),
        ("¿Qué prácticas musicales hay?", CatalogoDeConsultas.Tematica),
        ("distribución por práctica musical", CatalogoDeConsultas.Tematica),
        ("¿qué periodicidad tienen los festivales?", CatalogoDeConsultas.Tematica),
        ("¿qué puedo preguntarte?", CatalogoDeConsultas.Ayuda),
        ("cuales son los municipios con mas festivales", CatalogoDeConsultas.Municipios),

        // ---------- Las que fallaban antes de medir, y qué las arregló ----------
        // UNA ERRATA: la resuelve la tolerancia de una letra.
        ("cuantos festivles hay", CatalogoDeConsultas.Resumen),
        // UNA ABREVIATURA: la resuelve el diccionario corto de «x» por «por».
        ("festivales x departamento", CatalogoDeConsultas.Departamentos),
        // VOCABULARIO QUE NADIE HABIA ESCRITO: el resumen cuenta organizaciones desde siempre, pero
        // ninguna pregunta que las nombrara lo alcanzaba.
        ("cuantas organizaciones hay registradas", CatalogoDeConsultas.Resumen),
        ("hay festivales sin organización responsable?", CatalogoDeConsultas.Calidad),
        // UN PLURAL: ya no hace falta declararlo aparte desde que se compara por palabras.
        ("¿qué prácticas musicales tienen los festivales?", CatalogoDeConsultas.Tematica),
        // ---------- Con el sitio y el año dentro de la pregunta ----------
        //
        // LAS TRES PRIMERAS ERAN LOS TRES FALLOS QUE QUEDABAN, y las
        // tres son la misma carencia: el intérprete leía el asunto y no leía las entidades.
        //
        // LA CONSULTA QUE SE ESPERA DE LAS DOS PRIMERAS CAMBIO ESE MISMO DIA, y conviene decir por
        // qué para que no parezca que se movió la diana. Se escribieron esperando «Festivales por
        // departamento», dando por hecho que la forma de no contestar del país entero era enrutar la
        // pregunta a la consulta territorial. Al implementarlo se vio que eso rompía «¿qué
        // información falta en los festivales del Huila?», que también lleva un sitio dentro y no es
        // una pregunta sobre distribución. Lo que fallaba no era el enrutado sino que el resumen no
        // sabía acotarse; una vez que sabe, «cuántos festivales hay en el Huila» se responde con el
        // resumen del Huila, que es exactamente lo que se preguntó. La diana sigue siendo la misma:
        // que la respuesta no sea la del país entero.
        ("¿Cuántos festivales hay publicados en el Huila?", CatalogoDeConsultas.Resumen),
        ("cuántos festivales hay en Nariño", CatalogoDeConsultas.Resumen),
        // SIGUE FALLANDO Y SE QUEDA ASI. Se intentó arreglarla declarando «festivales publicados»
        // como vocabulario del resumen —lo es: el resumen tiene esa fila—, y la prueba de
        // referencia lo cazó en el acto: «¿qué departamentos tienen más Festivales publicados?»
        // pasaba a contestar el resumen, porque una frase de dos palabras corrientes puntúa más que
        // «departamento» más «más». Arreglar una pregunta rompiendo otra no es arreglarla, y el
        // sistema no sabe cuándo se publicó un Festival: no hay fecha de publicación que consultar.
        ("festivales publicados este año", CatalogoDeConsultas.Resumen),
        // EL SITIO NO SE COME EL ASUNTO: la pregunta sigue siendo la suya, acotada.
        ("¿qué municipios del Valle del Cauca tienen más festivales?", CatalogoDeConsultas.Municipios),
        ("¿qué prácticas musicales hay en Bogotá?", CatalogoDeConsultas.Tematica),
        ("¿en qué municipios del Tolima no hay festivales?", CatalogoDeConsultas.SinFestival),
        ("¿cuántos mercados musicales hay en Antioquia?", CatalogoDeConsultas.MercadosPorDepartamento),
        ("qué cobertura tenemos en Santander", CatalogoDeConsultas.Cobertura),
        ("¿qué información falta en los festivales del Huila?", CatalogoDeConsultas.Calidad),
        // UN MUNICIPIO, NO UN DEPARTAMENTO.
        ("¿cuántos festivales hay en Ibagué?", CatalogoDeConsultas.Resumen),
        // CUENTA ORGANIZACIONES, QUE TAMBIEN TIENEN SEDE.
        ("cuantas organizaciones hay en Antioquia", CatalogoDeConsultas.Resumen),
        // LO QUE NO SE ACOTA SIGUE LLEGANDO A SU CONSULTA, y la respuesta dirá que no pudo acotar:
        // las bandejas institucionales no tienen territorio.
        ("¿qué está pendiente en el Huila?", CatalogoDeConsultas.Pendientes),
        // «PAIS» NO ES UN TERRITORIO DE DIVIPOLA, y no debe convertirse en uno por parecerlo.
        ("¿cuántos festivales hay en el país?", CatalogoDeConsultas.Resumen),
        // UN NOMBRE QUE ESTA EN DOS DEPARTAMENTOS: contesta lo general y dice por qué no acotó.
        ("cuántos festivales hay en San Andrés", CatalogoDeConsultas.Resumen),
        // EL AÑO: la única consulta que sabe lo que es.
        ("¿cuántas ediciones hubo en 2024?", CatalogoDeConsultas.Ediciones),
        ("ediciones por año", CatalogoDeConsultas.Ediciones),
        ("ediciones del festival en 2019", CatalogoDeConsultas.Ediciones),
        ("el año pasado cuántas ediciones hubo", CatalogoDeConsultas.Ediciones),

        // ---------- Escritas para que falle ----------
        //
        // SE AÑADEN SIN MIRAR SI ACIERTA. Un banco que acierta todo deja de ser un instrumento y
        // pasa a ser un decorado: mide el catálogo contra sí mismo. Estas nueve son formas que
        // nadie ha declarado en ninguna pista y que cualquiera escribiría, y entran para que el
        // suelo vuelva a estar por debajo del techo y haya adónde subir.
        ("¿qué festivales hay en el Huila?", CatalogoDeConsultas.Resumen),
        ("cuantos festivales se publicaron en 2025", CatalogoDeConsultas.Resumen),
        ("¿cuál es el departamento con más festivales?", CatalogoDeConsultas.Departamentos),
        ("dame los municipios sin festivales del Huila", CatalogoDeConsultas.SinFestival),
        ("¿hay mercados en Pitalito?", CatalogoDeConsultas.MercadosPorDepartamento),
        ("necesito saber cuántas organizaciones están registradas", CatalogoDeConsultas.Resumen),
        ("¿qué tan completa está la información de los festivales de Nariño?", CatalogoDeConsultas.Calidad),
        ("festivales por municipio", CatalogoDeConsultas.Municipios),
        ("qué departamentos no tienen festivales", CatalogoDeConsultas.SinFestival),

        // ---------- Con el estado dentro de la pregunta ----------
        //
        // ES LO UNICO QUE NINGUNA CONSULTA VEIA. Las demás parten de la lectura pública, así que los
        // quince borradores y los ocho Festivales en revisión de la base eran invisibles para el
        // asistente. «Cuántos festivales hay en revisión» llega al resumen y no a la consulta de
        // estados, y está bien: el resumen sabe acotarse por estado y devuelve exactamente la cifra
        // que se pidió; la consulta de estados es para cuando se quiere el reparto entero.
        ("¿cuántos festivales hay en revisión?", CatalogoDeConsultas.Resumen),
        ("¿cuántos festivales hay en revisión en el Huila?", CatalogoDeConsultas.Resumen),
        ("¿en qué estado están los festivales?", CatalogoDeConsultas.Estados),
        ("cuántos borradores hay", CatalogoDeConsultas.Estados),
        ("¿qué festivales están archivados?", CatalogoDeConsultas.Estados),
        ("festivales con ajustes solicitados", CatalogoDeConsultas.Estados),
        ("¿hay festivales rechazados en Nariño?", CatalogoDeConsultas.Estados),
        // LA FRONTERA ENTRE PROCESOS: «borrador» pesa más que «mercado», así que sin declararlo la
        // consulta de Festivales por estado contestaría una pregunta sobre mercados.
        ("¿cuántos mercados hay en borrador?", CatalogoDeConsultas.Mercados),
        ("mercados musicales archivados", CatalogoDeConsultas.Mercados),
        ("¿qué le falta a los festivales en revisión?", CatalogoDeConsultas.Calidad),

        // ---------- Segunda tanda escrita para que falle ----------
        //
        // SE AÑADEN OTRA VEZ SIN MIRAR SI ACIERTA, por la misma razón que las nueve anteriores, y
        // porque en esta versión se arreglaron dos de las que fallaban: si el banco solo se arregla y
        // nunca se endurece, el porcentaje sube sin que el intérprete entienda más.
        // LA ESPERABA EN «FESTIVALES POR ESTADO» Y LLEGA AL RESUMEN, y el arreglo no fue cambiar
        // el enrutado sino que el resumen diera la cifra: ahora tiene una fila «Festivales sin
        // publicar», que es la resta que antes había que hacer a mano leyendo dos filas. La pregunta
        // se contesta, que es lo que el banco mide.
        ("¿cuántos festivales hay sin publicar?", CatalogoDeConsultas.Resumen),
        ("dame el reparto de festivales por estado", CatalogoDeConsultas.Estados),
        ("cuántas ediciones se hicieron el año pasado", CatalogoDeConsultas.Ediciones),
        ("¿qué organizaciones hay en el Valle del Cauca?", CatalogoDeConsultas.Resumen),
        ("necesito el listado de municipios sin festival del Tolima", CatalogoDeConsultas.SinFestival),
        ("cuántos festivales en revisión tiene Antioquia", CatalogoDeConsultas.Resumen),
        ("¿cuál es la periodicidad más común?", CatalogoDeConsultas.Tematica),
        ("festivales que no tienen contacto", CatalogoDeConsultas.Calidad),

        // ---------- Estados negados ----------
        //
        // LO QUE FALTA POR HACER SE PREGUNTA EN NEGATIVO, y esa es toda la razón de que exista la
        // negación: «sin publicar» es la forma corriente de preguntar cuánto trabajo queda.
        ("festivales que no están archivados", CatalogoDeConsultas.Estados),
        ("¿qué festivales no están publicados?", CatalogoDeConsultas.Estados),
        ("¿cuántos mercados no están publicados?", CatalogoDeConsultas.Mercados),
        // LAS EDICIONES DE MERCADO TAMBIEN TIENEN AÑO, y la consulta de Ediciones las reparte por
        // año y las separa de las de Festival; el resumen de mercados solo da su total.
        ("¿cuántas ediciones de mercado hubo en 2024?", CatalogoDeConsultas.Ediciones),
    ];

    /// <summary>
    /// Las filas de DIVIPOLA con las que se mide el banco.
    /// </summary>
    /// <remarks>
    /// <b>ES UN ANDAMIO DE PRUEBA, NO UNA SEGUNDA FUENTE.</b> En producción el vocabulario lo
    /// construye <c>CacheDivipola</c> leyendo <c>dbo.Divipola</c> entera, que es la única fuente del
    /// proyecto. Aquí hacen falta unas cuantas filas para poder preguntar por un sitio concreto sin
    /// levantar base de datos, y son filas reales: los códigos, los nombres y hasta la repetición de
    /// «San Andrés» en dos departamentos están copiados de <c>PNMC_LOCAL</c>.
    /// </remarks>
    private static readonly VocabularioTerritorial.FilaTerritorial[] Territorios =
    [
        new("41", "Huila", "41001", "Neiva"),
        new("41", "Huila", "41551", "Pitalito"),
        new("52", "Nariño", "52001", "Pasto"),
        new("76", "Valle del Cauca", "76001", "Cali"),
        new("11", "Bogotá, D.C.", "11001", "Bogotá, D.C."),
        new("73", "Tolima", "73001", "Ibagué"),
        new("05", "Antioquia", "05001", "Medellín"),
        new("68", "Santander", "68001", "Bucaramanga"),
        // EL MISMO NOMBRE EN DOS DEPARTAMENTOS: así el banco cubre el caso ambiguo de verdad.
        new("68", "Santander", "68669", "San Andrés"),
        new("88", "Archipiélago de San Andrés, Providencia y Santa Catalina", "88001", "San Andrés"),
    ];

    private static readonly VocabularioTerritorial Vocabulario = VocabularioTerritorial.Construir(Territorios);

    /// <summary>
    /// Los estados con los que se mide el banco, copiados de <c>dbo.EstadosContenido</c>.
    /// </summary>
    /// <remarks>
    /// SOLO LOS DEL CIRCUITO DE UN FESTIVAL. «Registrada», «Activa» y los demás del catálogo no
    /// entran, igual que no entran en producción: son estados de una entidad o de una suscripción.
    /// </remarks>
    private static readonly VocabularioDeEstados Estados = VocabularioDeEstados.Construir(
    [
        new("borrador", "Borrador"),
        new("en_revision", "En revisión"),
        new("ajustes_solicitados", "Ajustes solicitados"),
        new("rechazado", "Rechazado"),
        new("publicado", "Publicado"),
        new("archivado", "Archivado"),
    ]);

    /// <summary>El año con el que se leen «este año» y «el año pasado» en el banco.</summary>
    private const int AnioDeReferencia = 2026;

    private static EntidadesDeLaPregunta EntidadesDe(string pregunta) =>
        ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)),
            Vocabulario,
            AnioDeReferencia,
            Estados);

    /// <summary>
    /// Cuántas del banco de la organización tiene que acertar.
    /// </summary>
    /// <remarks>
    /// <b>ESTE AMBITO NUNCA SE HABIA MEDIDO.</b> Hasta lo único que se
    /// comprobaba de él eran sus cuatro sugerencias, que resuelve por construcción. Una organización
    /// pregunta con las mismas palabras que un funcionario —«¿dónde están mis Festivales en el
    /// Huila?»— y tiene derecho a la misma respuesta acotada.
    /// </remarks>
    /// <remarks>
    /// <b>20 de 21 AL MEDIRLO POR PRIMERA VEZ</b> (17 de septiembre de 2026, 95 %), y la primera
    /// medición ya encontró un defecto que llevaba días en pie: «¿Dónde están mis Festivales?» —el
    /// rótulo de una consulta y una de las cuatro sugerencias que el asistente ofrece escritas—
    /// llegaba a «Cómo van mis procesos». La prueba de las sugerencias no podía verlo: comprobaba
    /// que resolvieran algo, no que resolvieran lo suyo. <b>25 de 26</b> al conectar Mercados
    /// Musicales a este espacio, que era la superficie donde el módulo no había llegado: una
    /// organización que registraba un mercado no podía preguntar por él.
    /// </remarks>
    private const int SueloDeLaOrganizacion = 27;

    /// <summary>El banco del espacio externo: lo que pregunta quien administra su organización.</summary>
    public static readonly (string Pregunta, string Esperada)[] DeOrganizacion =
    [
        ("¿Qué necesita mi atención?", CatalogoDeConsultas.MisPendientes),
        ("qué tengo que hacer", CatalogoDeConsultas.MisPendientes),
        ("¿qué me falta en el Huila?", CatalogoDeConsultas.MisPendientes),
        ("¿qué información me falta?", CatalogoDeConsultas.MiCalidad),
        ("qué información está incompleta en mis festivales", CatalogoDeConsultas.MiCalidad),
        ("¿cómo van mis procesos?", CatalogoDeConsultas.MisProcesos),
        ("¿tengo festivales en borrador?", CatalogoDeConsultas.MisProcesos),
        ("¿cuántas ediciones tengo?", CatalogoDeConsultas.MisProcesos),
        ("mis festivales en Nariño", CatalogoDeConsultas.MisProcesos),
        ("¿dónde están mis festivales?", CatalogoDeConsultas.MiTerritorio),
        ("¿dónde están mis festivales en Nariño?", CatalogoDeConsultas.MiTerritorio),
        ("qué información está incompleta en mis festivales del Huila", CatalogoDeConsultas.MiCalidad),
        ("en qué municipios tengo festivales", CatalogoDeConsultas.MiTerritorio),
        ("¿qué cobertura territorial tengo?", CatalogoDeConsultas.MiTerritorio),
        ("¿qué puedo preguntarte?", CatalogoDeConsultas.Ayuda),

        // ---------- Escritas para que falle, también aquí ----------
        ("¿tengo algo rechazado?", CatalogoDeConsultas.MisProcesos),
        ("cuántos festivales míos están publicados", CatalogoDeConsultas.MisProcesos),
        ("¿me falta algún dato?", CatalogoDeConsultas.MiCalidad),
        ("¿hay algo esperando que yo haga?", CatalogoDeConsultas.MisPendientes),
        ("listado de mis ediciones", CatalogoDeConsultas.MisProcesos),
        ("¿en qué departamentos trabajo?", CatalogoDeConsultas.MiTerritorio),

        // ---------- Mercados, que hasta ahora no existían aquí ----------
        //
        // LA FRONTERA ENTRE PROCESOS, otra vez: «como van» mide ocho caracteres y «mercado» siete,
        // así que sin declararla «¿cómo van mis mercados?» recibía la cifra de los Festivales.
        ("¿cómo van mis mercados?", CatalogoDeConsultas.MisMercados),
        ("¿cuántos mercados musicales tengo?", CatalogoDeConsultas.MisMercados),
        ("mis mercados en Antioquia", CatalogoDeConsultas.MisMercados),
        ("¿tengo mercados en borrador?", CatalogoDeConsultas.MisMercados),
        ("¿en qué municipios tengo mercados?", CatalogoDeConsultas.MisMercados),
        // PREGUNTAR POR UN HUECO NO ES PREGUNTAR POR UN MODULO: las dos van a la misma consulta,
        // que mira los Festivales y los mercados a la vez.
        ("¿qué le falta a mis mercados?", CatalogoDeConsultas.MiCalidad),
        ("¿qué le falta a mis festivales?", CatalogoDeConsultas.MiCalidad),
    ];

    /// <summary>
    /// El espacio externo entiende tanto como la consola, y se acota igual.
    /// </summary>
    [Fact]
    public async Task El_interprete_de_la_organizacion_acierta_al_menos_su_suelo()
    {
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(AmbitoDeConsulta.Organizacion);

        var aciertos = 0;
        var fallos = new List<string>();

        foreach (var (pregunta, esperada) in DeOrganizacion)
        {
            var elegida = await interprete.InterpretarAsync(
                pregunta, disponibles, EntidadesDe(pregunta), CancellationToken.None);
            if (elegida?.Consulta.Id == esperada)
            {
                aciertos++;
            }
            else
            {
                fallos.Add($"«{pregunta}» → esperaba {esperada}, contestó {elegida?.Consulta.Id ?? "(ninguna)"}");
            }
        }

        salida.WriteLine($"Banco de la organización: {aciertos} de {DeOrganizacion.Length} " +
            $"({100 * aciertos / DeOrganizacion.Length} %). Suelo: {SueloDeLaOrganizacion}.");
        foreach (var fallo in fallos) salida.WriteLine("  " + fallo);

        Assert.True(
            aciertos >= SueloDeLaOrganizacion,
            $"El intérprete de la organización bajó a {aciertos} de {DeOrganizacion.Length}; el suelo " +
            $"medido es {SueloDeLaOrganizacion}. Falla en:{Environment.NewLine}{string.Join(Environment.NewLine, fallos)}");
    }

    /// <summary>
    /// Todo recorte territorial que una consulta declara es alcanzable con una pregunta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ES UNA PRUEBA CONTRA EL CODIGO ESCRITO Y NO ENGANCHADO</b>, que es un defecto que este
    /// proyecto ya ha pagado varias veces. Una consulta puede declarar que sabe acotarse por
    /// territorio y tener su rama escrita, probada por dentro y jamás alcanzada, porque ninguna
    /// forma de preguntar llega a ella con un sitio dentro. Desde fuera se ve igual que si no
    /// existiera.
    /// </para>
    /// <para>
    /// <b>NO SE COMPRUEBA AL REVES.</b> Se escribió primero la invariante contraria —«toda pregunta
    /// con un sitio dentro tiene que llegar a una consulta que sepa acotarse»— y es falsa a
    /// propósito: «¿qué está pendiente en el Huila?» debe llegar a los pendientes de gestión, que
    /// viven en bandejas institucionales sin territorio, y decir que no se acotan. Obligarla a
    /// llegar a otra parte sería contestar otra pregunta.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(AmbitoDeConsulta.Institucional)]
    [InlineData(AmbitoDeConsulta.Organizacion)]
    public async Task Cada_consulta_que_dice_acotarse_por_territorio_se_alcanza_con_un_sitio_dentro(AmbitoDeConsulta ambito)
    {
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(ambito);
        var banco = ambito == AmbitoDeConsulta.Institucional ? Institucional : DeOrganizacion;

        var alcanzadas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (pregunta, _) in banco)
        {
            var entidades = EntidadesDe(pregunta);
            if (entidades.Territorio is null) { continue; }

            var elegida = await interprete.InterpretarAsync(pregunta, disponibles, entidades, CancellationToken.None);
            if (elegida is not null) { alcanzadas.Add(elegida.Consulta.Id); }
        }

        var sinAlcanzar = disponibles
            .Where(consulta => consulta.SeAcotaPorTerritorio && !alcanzadas.Contains(consulta.Id))
            .Select(consulta => consulta.Rotulo)
            .ToList();

        Assert.True(
            sinAlcanzar.Count == 0,
            $"Estas consultas de {ambito} dicen acotarse por territorio y ninguna pregunta del banco " +
            $"las alcanza nombrando un sitio: {string.Join(", ", sinAlcanzar)}.");
    }

    [Fact]
    public async Task El_interprete_acierta_al_menos_el_suelo_medido()
    {
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(AmbitoDeConsulta.Institucional);

        var aciertos = 0;
        var fallos = new List<string>();

        foreach (var (pregunta, esperada) in Institucional)
        {
            var elegida = await interprete.InterpretarAsync(
                pregunta, disponibles, EntidadesDe(pregunta), CancellationToken.None);
            if (elegida?.Consulta.Id == esperada)
            {
                aciertos++;
            }
            else
            {
                fallos.Add($"«{pregunta}» → esperaba {esperada}, contestó {elegida?.Consulta.Id ?? "(ninguna)"}");
            }
        }

        salida.WriteLine($"Banco institucional: {aciertos} de {Institucional.Length} " +
            $"({100 * aciertos / Institucional.Length} %). Suelo: {SueloDeAciertos}.");
        foreach (var fallo in fallos) salida.WriteLine("  " + fallo);

        Assert.True(
            aciertos >= SueloDeAciertos,
            $"El intérprete bajó a {aciertos} aciertos de {Institucional.Length}; el suelo medido es " +
            $"{SueloDeAciertos}. Falla en:{Environment.NewLine}{string.Join(Environment.NewLine, fallos)}");
    }

    [Fact]
    public async Task Ninguna_pregunta_del_banco_recibe_una_respuesta_de_otra_cosa_sin_que_se_sepa()
    {
        // LA PEOR RESPUESTA NO ES «NO SE»: es contestar con autoridad a otra pregunta. El intérprete
        // se declara conservador —«prefiere no encontrar consulta a encontrar una parecida»—, y esta
        // prueba cuenta cuántas veces no lo es, para que esa cifra no crezca sin que nadie lo vea.
        var interprete = new InterpreteDeterministico();
        var disponibles = CatalogoDeConsultas.De(AmbitoDeConsulta.Institucional);

        var equivocadas = new List<string>();
        foreach (var (pregunta, esperada) in Institucional)
        {
            var elegida = await interprete.InterpretarAsync(
                pregunta, disponibles, EntidadesDe(pregunta), CancellationToken.None);
            if (elegida is not null && elegida.Consulta.Id != esperada)
            {
                equivocadas.Add($"«{pregunta}» → contestó {elegida.Consulta.Id} en vez de {esperada}");
            }
        }

        salida.WriteLine($"Contesta otra cosa en {equivocadas.Count} de {Institucional.Length}.");
        foreach (var caso in equivocadas) salida.WriteLine("  " + caso);

        // CERO, MEDIDO, Y SOSTENIDO AL CRECER EL BANCO. Eran dos —las dos preguntas con un
        // departamento dentro que recibían el resumen del país entero— y las arregló leer las
        // entidades. Sigue en cero con diecinueve preguntas más, incluidas las que cruzan procesos:
        // «¿cuántos mercados hay en borrador?» no puede acabar en una cifra de Festivales, y sin la
        // frontera declarada acababa. Que esté en cero no significa que el intérprete lo entienda
        // todo: las tres que falla las falla diciendo que no encontró consulta, que es como se
        // declaró que debía fallar.
        Assert.True(equivocadas.Count == 0, string.Join(Environment.NewLine, equivocadas));
    }
}
