/**
 * Registro unico del contenido editable del sitio publico.
 *
 * Antes existian tres listas paralelas de las mismas claves — los textos por
 * defecto, el catalogo de etiquetas/limites y la agrupacion del panel — que
 * habia que mantener sincronizadas a mano. Aqui cada clave se declara una sola
 * vez y las otras tres vistas se derivan. Agregar contenido editable es agregar
 * un field() al grupo que corresponda.
 *
 * Este archivo es datos puros: no importa Angular ni toca almacenamiento, para
 * que pueda alimentar tambien la semilla de la base de datos.
 */

export interface DefinicionDeCampoDeTexto {
  key: string;
  label: string;
  limit: number;
  defaultValue: string;
}

export interface DefinicionDeGrupoDeTexto {
  id: string;
  label: string;
  section: string;
  fields: DefinicionDeCampoDeTexto[];
}

/** Una clave del catalogo, aplanada con la seccion de su grupo. */
export interface DescriptorDeClaveDeTexto {
  key: string;
  label: string;
  section: string;
  limit: number;
}

/** Seccion del panel: el orden y la etiqueta corta no se derivan del contenido. */
export interface DefinicionDeSeccionDeTexto {
  section: string;
  pill: string;
}

const field = (key: string, label: string, limit: number, defaultValue: string): DefinicionDeCampoDeTexto => ({
  key,
  label,
  limit,
  defaultValue,
});

const timelineDefaults = [
  ['2003-2006', 'Creación e Institucionalización', 'La aprobación del CONPES 3409 de 2006 formalizó el PNMC y consolidó las Escuelas Municipales de Música (EMM) como espacios centrales para la formación musical colectiva. Este periodo estableció los cimientos del programa: acceso, democratización, convivencia y fortalecimiento de las músicas locales. Se amplió la dotación instrumental, se fortalecieron los equipos territoriales y se empezó a articular una red nacional de formación basada en la práctica comunitaria.'],
  ['2007-2014', 'Territorialización y saberes', 'Durante esta etapa se profundizó en la institucionalización territorial, con énfasis en la formación de formadores, la cualificación de músicos en ejercicio y el impulso a las músicas tradicionales. Se promovió la descentralización, se consolidaron procesos comunitarios sostenidos y se promovió el reconocimiento de músicos empíricos y sabedores. Este periodo marcó un avance significativo en la diversidad musical, al visibilizar prácticas propias de cada región y promover su circulación.'],
  ['2015-2018', 'Profesionalización y SIMUS', 'En estos años se desarrollaron las líneas estratégicas de Musicalización de la Ciudadanía y Estructuración del Campo Profesional de la Música, orientadas a fortalecer la formación integral y la profesionalización del sector. Se creó el Sistema de Información de la Música (SIMUS), herramienta clave para la toma de decisiones y la caracterización del ecosistema musical. Además, se implementaron nuevas estrategias de circulación y se amplió la presencia del PNMC en festivales, mercados y espacios de movilidad artística.'],
  ['2018-2022', 'Evaluación y Consolidación', 'El Departamento Nacional de Planeación (DNP) realizó una evaluación integral del PNMC, destacando su impacto en la formación musical, el fortalecimiento del tejido social y la dignificación del trabajo artístico. A partir de esta evaluación se identificaron retos y oportunidades, como mejorar la articulación interinstitucional, fortalecer SIMUS, ampliar la presencia del PNMC en educación superior, incentivar economías creativas en los territorios y mejorar las condiciones laborales de los músicos y formadores.'],
  ['2023-2025', 'Actualización y Proyección 2035', 'En un ejercicio nacional sin precedentes, el Ministerio de las Culturas abrió espacios de participación a través de 34 Encuentros Territoriales, mesas sectoriales, la Mesa Nacional Vinculante y el VII Congreso Nacional de Música. Estas iniciativas permitieron recoger las necesidades, visiones y apuestas del sector musical en todo el país y dieron origen al PNMC 2025-2035, “Huellas y apuestas de la diversidad sonora”. Este nuevo Plan articula la música con la vida, el diálogo intercultural, la bioculturalidad, la equidad, la sostenibilidad y la gobernanza participativa, proyectando un ecosistema musical diverso, justo y sostenible para la próxima década.'],
];

const normativeDefaults = [
  ['1997', 'Ley 397 de 1997', 'La Ley General de Cultura establece los principios, objetivos y mecanismos para proteger, fomentar y difundir la cultura en Colombia. Reconoce la diversidad cultural como fundamento de la identidad nacional y define la cultura como derecho. En su estructura se incluyen disposiciones para el fomento de las artes, la formación artística y la protección del patrimonio cultural, elementos esenciales para el desarrollo del PNMC. Actualizada por la Ley 1185 de 2008.'],
  ['2006', 'CONPES 3409 de 2006', 'Este documento aprobó la política del Plan Nacional de Música para la Convivencia, institucionalizando las Escuelas Municipales de Música y definiendo estrategias para mejorar la formación musical, la dotación instrumental y la gestión cultural en los territorios. Fue la base técnica y financiera que permitió consolidar el PNMC como política pública estable.'],
  ['2011', 'Ley 1493 de 2011', 'La Ley de Espectáculos Públicos regula la organización de espectáculos públicos de las artes escénicas y promueve la circulación artística en condiciones más equitativas. Aunque su alcance es más amplio que la música, ha tenido un impacto directo en la infraestructura cultural y en la movilidad de artistas y agrupaciones musicales en el país, facilitando escenarios más dignos y accesibles.'],
  ['2018', 'Decreto 2120 de 2018', 'Este decreto reglamenta la organización, funcionamiento y articulación de los subsistemas que integran el Sistema Nacional de Cultura. Para el PNMC es clave porque define los espacios de participación ciudadana, la gobernanza territorial y las responsabilidades institucionales en procesos formativos y comunitarios, incluyendo los vinculados a las músicas del país.'],
  ['2024-2038', 'Plan Nacional de Cultura', 'El nuevo PNC establece la visión cultural del país para los próximos 14 años. Define la cultura como eje del cuidado de la vida, la diversidad y la paz, y orienta las políticas del Ministerio de las Culturas, las Artes y los Saberes. El PNMC 2025-2035 se enmarca plenamente en esta estrategia, en sus componentes institucional y subsectorial, articulando lineamientos de diversidad sonora, ecosistemas culturales, gobernanza y sostenibilidad.'],
  ['2025', 'Ley 2555 de 2025', 'La Ley Artes al Aula convierte la educación artística en un mandato para todas las instituciones educativas oficiales del país. Reconoce las artes, incluida la música, como un derecho cultural fundamental y exige su incorporación transversal en los procesos pedagógicos, fortaleciendo competencias creativas, socioemocionales y ciudadanas. Impulsa la formación docente en pedagogías artísticas, promueve la articulación entre escuela, comunidad y territorio, y orienta la implementación desde el SINEFAC, facilitando la coordinación entre los sectores de educación y cultura.'],
];

export const TEAM_DEFAULTS: string[][] = [
  ['Coordinación Grupo de Música', 'Jorge Enrique Sossa Santos', 'jsossa@mincultura.gov.co'],
  ['Apoyo a la coordinación', 'Dora Carolina Rojas Rivera', 'drojas@mincultura.gov.co'],
  ['Líder Componente: Formación', 'Diego Rodríguez', 'drodriguezc@mincultura.gov.co'],
  ['Líder Componente: Investigación', 'Raúl Hernán Daza', 'rdaza@mincultura.gov.co'],
  ['Líder Componente: Circulación', 'Carolina Ruiz Barragán', 'druizb@mincultura.gov.co'],
  ['Líder Componente: Dotación', 'Guadalupe Gil', 'ggil@mincultura.gov.co'],
  ['Líder Componente: Creación', 'Isabel Durán', 'iduranp@mincultura.gov.co'],
  ['Líder Componente: Gobernanza', '', ''],
  ['Líder Componente: Información', 'Yazmín López', 'ylopez@mincultura.gov.co'],
  ['Líder Componente: Comunicación', 'Shirley Giomar Gómez', 'sgomezc@mincultura.gov.co'],
];

export const WEB_TEXT_GROUPS: DefinicionDeGrupoDeTexto[] = [
  {
    id: 'about_hero_presentation', label: 'Hero y Presentación', section: 'Sobre PNMC', fields: [
      field('about_hero_tag', 'Hero - Etiqueta', 50, 'Sobre el PNMC'),
      field('about_hero_title', 'Hero - Título', 80, 'Sobre el'),
      field('about_hero_accent', 'Hero - Título acentuado', 50, 'PNMC'),
      field('about_description', 'Hero - Descripción', 300, 'Reconocemos la música como una expresión transformadora para el cuidado de la vida.'),
      field('about_presentation_bg', 'Presentación - Palabra de fondo', 50, 'POLÍTICA'),
      field('about_presentation_title', 'Presentación - Título', 80, 'Presentación'),
      field('about_presentation_intro_1', 'Presentación - Párrafo 1', 700, 'La música nos conecta y nos permite expresar quiénes somos. En Colombia, nuestra diversidad sonora fortalece los vínculos comunitarios y refleja la riqueza cultural de cada región. Desde hace 20 años, el Plan Nacional de Música para la Convivencia (PNMC) protege y proyecta nuestra pluralidad musical.'),
      field('about_presentation_intro_2', 'Presentación - Párrafo 2', 700, 'Entendemos la música como un derecho cultural y una herramienta fundamental para la equidad y la construcción de paz. Nuestra nueva hoja de ruta, “Huellas y apuestas de la diversidad sonora”, nace de un proceso participativo sin precedentes en todo el país. Es una política renovada, inclusiva y descentralizada para los retos del futuro.'),
      field('about_base_tag', 'Apuesta base - Etiqueta', 80, 'Apuesta Base'),
      field('about_base_quote', 'Apuesta base - Texto', 500, 'Consolidar la equidad de condiciones y oportunidades en el campo musical, promoviendo la participación y el ejercicio pleno de los derechos culturales.'),
      field('about_base_objective_label', 'Apuesta base - Etiqueta objetivo', 80, 'Objetivo General'),
      field('about_base_plan_label', 'Apuesta base - Etiqueta plan', 100, 'Plan Nacional de Música'),
      field('about_collective_tag', 'Construcción colectiva - Etiqueta', 80, 'Construcción Colectiva'),
      field('about_collective_text', 'Construcción colectiva - Texto', 500, 'El PNMC 2025–2035, Huellas y apuestas de la diversidad sonora, nace de un proceso participativo sin precedentes, recogiendo voces a través de encuentros territoriales en todo el país.'),
    ],
  },
  {
    id: 'about_objectives', label: 'Objetivos', section: 'Sobre PNMC', fields: [
      field('about_objectives_bg', 'Objetivos - Palabra de fondo', 50, 'ESTRATEGIA'),
      field('about_objectives_title', 'Objetivos - Título', 80, 'Objetivos de Eje'),
      field('about_objective_1_axis', 'Objetivo 1 - Nombre del eje', 200, 'MÚSICA PARA LA VIDA, EL DIÁLOGO INTERCULTURAL Y LA DIVERSIDAD BIOCULTURAL'),
      field('about_objective_1_title', 'Objetivo 1 - Título', 100, 'Formación y Cuidado'),
      field('about_objective_1_desc', 'Objetivo 1 - Descripción', 500, 'Promover procesos de formación y práctica musical que fortalezcan la relación ética, sensible y sostenible entre las personas y sus territorios, como medio para el cuidado biocultural y la paz.'),
      field('about_objective_1_button', 'Objetivo 1 - Botón', 40, 'Explorar eje'),
      field('about_objective_2_axis', 'Objetivo 2 - Nombre del eje', 200, 'FORTALECIMIENTO DE LAS PRÁCTICAS, EXPRESIONES Y OFICIOS DE LA MÚSICA'),
      field('about_objective_2_title', 'Objetivo 2 - Título', 100, 'Desarrollo Integral'),
      field('about_objective_2_desc', 'Objetivo 2 - Descripción', 500, 'Cualificar procesos de creación, producción y circulación. Fomentar la formalización de oficios y saberes para lograr el reconocimiento profesional y la equidad en todas las regiones.'),
      field('about_objective_2_button', 'Objetivo 2 - Botón', 40, 'Explorar eje'),
      field('about_objective_3_axis', 'Objetivo 3 - Nombre del eje', 200, 'GOBERNANZA MUSICAL E INTEGRACIÓN CULTURAL E INTERSECTORIAL'),
      field('about_objective_3_title', 'Objetivo 3 - Título', 100, 'Gobernanza y Gestión'),
      field('about_objective_3_desc', 'Objetivo 3 - Descripción', 500, 'Impulsar la legitimidad y articulación de los mecanismos de organización del sector con el Estado, orientada a la sostenibilidad cultural y la dignificación de los oficios musicales.'),
      field('about_objective_3_button', 'Objetivo 3 - Botón', 40, 'Explorar eje'),
    ],
  },
  {
    id: 'about_approaches', label: 'Enfoques transversales', section: 'Sobre PNMC', fields: [
      field('about_approaches_tag', 'Enfoques - Etiqueta', 60, 'TRANSVERSALIDAD'),
      field('about_approaches_title', 'Enfoques - Título', 100, 'Enfoques del Sistema'),
      field('about_approaches_desc', 'Enfoques - Descripción', 400, 'Consideramos las particularidades sociales y geográficas para garantizar un acceso equitativo a la cultura.'),
      field('about_approach_1_title', 'Enfoque 1 - Título', 80, 'Biocultural'),
      field('about_approach_1_desc', 'Enfoque 1 - Descripción', 350, 'Relación música-entorno natural. Integración de saberes ancestrales y preservación sonora ambiental.'),
      field('about_approach_2_title', 'Enfoque 2 - Título', 80, 'Poblacional'),
      field('about_approach_2_desc', 'Enfoque 2 - Descripción', 350, 'Equidad para infancia, mujeres, diversidades de género, pueblos étnicos y personas con capacidades diversas.'),
      field('about_approach_3_title', 'Enfoque 3 - Título', 80, 'Territorial'),
      field('about_approach_3_desc', 'Enfoque 3 - Descripción', 350, 'Estrategias diferenciadas según geografía, infraestructura local y financiamiento por contextos.'),
    ],
  },
  {
    id: 'about_actors', label: 'Actores del Plan', section: 'Sobre PNMC', fields: [
      field('about_actors_bg', 'Actores - Palabra de fondo', 50, 'ECOSISTEMA'),
      field('about_actors_title', 'Actores - Título', 80, 'Actores del Plan'),
      field('about_actors_tag', 'Actores - Etiqueta', 80, 'Ecosistema humano'),
      field('about_actors_heading', 'Actores - Encabezado', 120, 'Tres capas de articulación'),
      field('about_actors_desc', 'Actores - Descripción', 500, 'El plan conecta sector musical, institucionalidad y sociedad civil en una misma arquitectura de colaboración para la formación, circulación, sostenibilidad y gobernanza de la música.'),
      field('about_actors_sector_title', 'Actores sectoriales - Título', 100, 'Agentes Sectoriales'),
      field('about_actors_sector_items', 'Actores sectoriales - Lista (una línea por elemento)', 700, 'Creadores y compositores\nIntérpretes y agrupaciones\nDocentes y formadores\nInvestigadores\nProductores y técnicos\nGestores y promotores\nConstructores de instrumentos'),
      field('about_actors_institutional_title', 'Actores institucionales - Título', 100, 'Agentes Institucionales'),
      field('about_actors_institutional_items', 'Actores institucionales - Lista (una línea por elemento)', 700, 'Gobiernos locales\nEscuelas de Música\nCasas de la Cultura\nMinisterios nacionales\nAliados del Estado\nCooperación Ibermúsicas'),
      field('about_actors_civil_title', 'Sociedad civil - Título', 100, 'Sociedad Civil'),
      field('about_actors_civil_items', 'Sociedad civil - Lista (una línea por elemento)', 700, 'Asociaciones de músicos\nOrganizaciones sin ánimo de lucro\nAsociaciones de padres\nCabildos indígenas\nConsejos afrodescendientes'),
    ],
  },
  {
    id: 'about_timeline', label: 'Hitos', section: 'Sobre PNMC', fields: [
      field('about_timeline_bg', 'Hitos - Palabra de fondo', 50, 'EVOLUCIÓN'),
      field('about_timeline_title', 'Hitos - Título', 80, 'Hitos del PNMC'),
      field('about_timeline_intro', 'Hitos - Introducción', 700, 'Desde hace más de medio siglo, Colombia ha construido una política musical que reconoce la música como un derecho cultural y puente de convivencia. El PNMC, creado en 2003, es el resultado de una trayectoria que inició con Colcultura en 1968, garantizando hoy que todas las personas puedan vivir plenamente la música como experiencia y bien común.'),
      ...timelineDefaults.flatMap((stage, index) => [
        field(`about_timeline_${index + 1}_year`, `Hito ${index + 1} - Periodo`, 30, stage[0]),
        field(`about_timeline_${index + 1}_title`, `Hito ${index + 1} - Título`, 120, stage[1]),
        field(`about_timeline_${index + 1}_desc`, `Hito ${index + 1} - Descripción`, 1400, stage[2]),
      ]),
    ],
  },
  {
    id: 'about_normative', label: 'Marco normativo', section: 'Sobre PNMC', fields: [
      field('about_normative_bg', 'Marco normativo - Palabra de fondo', 50, 'MARCO'),
      field('about_normative_title', 'Marco normativo - Título', 80, 'Marco Normativo'),
      field('about_normative_intro', 'Marco normativo - Introducción', 700, 'El PNMC se sustenta en una trayectoria normativa que ha consolidado la cultura y la música como derechos, políticas públicas y herramientas de transformación territorial. Este marco articula leyes, decretos, documentos de política y planes nacionales que orientan la formación, la circulación, la participación y la gobernanza cultural en Colombia.'),
      ...normativeDefaults.flatMap((stage, index) => [
        field(`about_normative_${index + 1}_year`, `Norma ${index + 1} - Año`, 30, stage[0]),
        field(`about_normative_${index + 1}_title`, `Norma ${index + 1} - Título`, 120, stage[1]),
        field(`about_normative_${index + 1}_desc`, `Norma ${index + 1} - Descripción`, 1400, stage[2]),
      ]),
    ],
  },
  {
    id: 'about_team', label: 'Equipo de Trabajo', section: 'Sobre PNMC', fields: [
      field('about_team_bg', 'Palabra de fondo', 50, 'EQUIPO'),
      field('about_team_title', 'Título', 80, 'Equipo de Trabajo'),
      field('about_team_intro', 'Introducción', 700, 'El PNMC se construye y acompaña desde un equipo técnico que articula componentes, seguimiento institucional y trabajo con los territorios. Aquí puedes identificar los referentes del plan por coordinación y componente, con sus canales de contacto institucional.'),
      field('about_team_coordination_label', 'Etiqueta coordinación', 80, 'Coordinación General'),
      field('about_team_leadership_label', 'Etiqueta liderazgos', 100, 'Liderazgos por componente'),
      field('about_team_name_placeholder', 'Nombre pendiente', 80, 'Por definir'),
      field('about_team_email_placeholder', 'Correo pendiente', 80, 'Correo pendiente'),
    ],
  },
  {
    id: 'home_hero', label: 'Encabezado Principal (Hero)', section: 'Home', fields: [
      field('home_tag', 'Etiqueta superior', 100, 'PLAN NACIONAL DE MÚSICA PARA LA CONVIVENCIA 2025—2035'),
      field('home_title', 'Título principal', 120, 'Huellas y Apuestas de la'),
      field('home_title_accent', 'Título acentuado', 80, 'Diversidad Sonora'),
      field('home_description', 'Descripción', 400, 'Un pacto colectivo que reconoce la música como un derecho cultural y un bien común en todo el territorio nacional.'),
    ],
  },
  // Los dos botones del hero —«Sobre el PNMC» y «Explorar Ejes»— fueron
  // editables hasta agosto de 2026. Se retiraron del panel a peticion del
  // equipo: son rotulos de navegacion, no copia. Su texto vive ahora en
  // `home.component.html`, al lado del destino al que llevan, que nunca fue
  // editable. Separar el rotulo del destino invitaba a que alguien renombrara
  // «Explorar Ejes» a otra cosa sin que el boton dejara de ir a /ejes.
  {
    id: 'home_about', label: 'Sección Identidad', section: 'Home', fields: [
      field('home_about_bg_word', 'Palabra de fondo', 50, 'IDENTIDAD'),
      field('home_about_title', 'Título secundario', 100, 'HUELLA Y EVOLUCIÓN'),
      field('home_about_quote', 'Cita del PNMC', 200, 'El PNMC 2025-2035 es una herramienta para que la música sea motor de vida, paz y justicia social.'),
      field('home_about_desc', 'Descripción amplia', 500, 'Desde hace más de dos décadas, el Plan Nacional de Música para la Convivencia (PNMC) promueve la diversidad cultural de Colombia como un pilar para la paz y la equidad.'),
    ],
  },
  {
    id: 'home_ejes', label: 'Estructura Ejes Base', section: 'Home', fields: [
      field('home_ejes_tag', 'Tag superior', 120, 'EL PNMC TIENE UNA ESTRUCTURA ESTRATÉGICA'),
      field('home_ejes_title', 'Título ejes', 120, 'PLANTEADA EN TRES EJES BASE'),
    ],
  },
  {
    id: 'home_bulletin', label: 'Boletín y Redes', section: 'Home', fields: [
      field('home_bulletin_title', 'Boletín - Título del boletín', 80, 'Recibe las Novedades'),
      field('home_bulletin_desc', 'Boletín - Descripción', 200, 'Convocatorias y lanzamientos semanales del PNMC.'),
      field('home_bulletin_placeholder', 'Boletín - Marcador email', 55, 'Ingresa tu correo'),
      field('home_bulletin_btn', 'Boletín - Botón registro', 40, 'Registrarme'),
      field('home_social_title', 'Redes - Título redes', 80, 'Conéctate con el Plan'),
      field('home_social_desc', 'Redes - Subtexto oficial', 120, 'Síguenos en nuestras redes oficiales'),
    ],
  },
  {
    // El banner deslizante de la portada (home.component.html:54). Sus doce textos eran
    // literales del arreglo `slides` del componente hasta.
    //
    // EL DESTINO DE CADA BOTON NO ENTRA. `actionId` lo resuelve `NavigationService`, y abrirlo
    // aqui permitiria renombrar un boton sin cambiar a donde lleva —o al reves—. Es la misma
    // regla que retiro los dos botones del hero.
    id: 'home_banner', label: 'Banner deslizante', section: 'Home', fields: [
      field('home_banner1_tag', 'Diapositiva 1 - Etiqueta', 60, 'Ecosistema musical'),
      field('home_banner1_title', 'Diapositiva 1 - Título', 90, 'Sé parte del ecosistema'),
      field('home_banner1_desc', 'Diapositiva 1 - Descripción', 300, 'Registra tu proceso, organización, festival, mercado, colectivo, espacio o perfil individual y haz parte de esta lectura territorial de la música en Colombia.'),
      field('home_banner1_cta', 'Diapositiva 1 - Botón', 50, 'Ser parte del ecosistema'),
      field('home_banner2_tag', 'Diapositiva 2 - Etiqueta', 60, 'Celebra la Música'),
      field('home_banner2_title', 'Diapositiva 2 - Título', 90, 'Activa la circulación musical en tu territorio'),
      field('home_banner2_desc', 'Diapositiva 2 - Descripción', 300, 'Conoce la estrategia, los recursos y las rutas de participación de Celebra la Música como movimiento nacional de circulación y encuentro.'),
      field('home_banner2_cta', 'Diapositiva 2 - Botón', 50, 'Explorar estrategia'),
      field('home_banner3_tag', 'Diapositiva 3 - Etiqueta', 60, 'Territorios Sonoros'),
      field('home_banner3_title', 'Diapositiva 3 - Título', 90, 'Explora turismo cultural y músicas regionales'),
      field('home_banner3_desc', 'Diapositiva 3 - Descripción', 300, 'Descubre cómo esta línea articula circulación, turismo cultural, saberes locales y experiencias territoriales en torno a la música.'),
      field('home_banner3_cta', 'Diapositiva 3 - Botón', 50, 'Ver territorios sonoros'),
    ],
  },
  {
    // La previsualizacion del ecosistema de la portada (home.component.html:57).
    //
    // NO ESTAN AQUI LOS SEIS ROTULOS DE LAS TARJETAS —«Escuelas de musica», «Festivales»…—, y
    // es deliberado: salen de `categorias-ecosistema.config.ts`, que alimenta ADEMAS la pagina
    // /ecosistema con ocho categorias. Editarlos desde la portada cambiaria las dos paginas a
    // la vez, y eso es una decision de alcance que no toca a este bloque.
    id: 'home_ecosistema', label: 'Ecosistema en la portada', section: 'Home', fields: [
      // «REGISTROS» Y NO «MAPA». Este bloque no previsualiza el mapa: previsualiza los REGISTROS de
      // los seis procesos del Ecosistema, y tiene que seguir dando sus cifras aunque el mapa no
      // esté —regla fijada por la dirección de producto—. La palabra de
      // fondo nombraba la pantalla a la que se puede salir, no lo que el bloque enseña.
      field('home_eco_bg_word', 'Palabra de fondo', 50, 'REGISTROS'),
      field('home_eco_title', 'Título de la sección', 100, 'Ecosistema Musical de Colombia'),
      field('home_eco_intro', 'Introducción', 400, 'Escuelas de música, escenarios, festivales, mercados musicales, redes y documentación, y lutería: los seis procesos del ecosistema musical colombiano, con directorio propio y una lectura territorial compartida.'),
      field('home_eco_simus_tag', 'Bloque morado - Antetítulo', 80, 'Sistema de Información de la Música'),
      field('home_eco_simus_title', 'Bloque morado - Título', 120, 'Toda la música de Colombia, en un solo lugar'),
    ],
  },
  {
    id: 'home_strategies_title', label: 'Rutas de Acción Territorial · cabecera', section: 'Home', fields: [
      field('home_strat_tag', 'Tag superior', 80, 'Procesos destacados'),
      field('home_strat_title', 'Título principal', 120, 'Rutas de Acción Territorial'),
      field('home_strat_desc', 'Introducción', 300, 'Conoce los marcos operativos y pedagógicos que impulsan la formación, investigación, circulación y gobernanza musical en todas las regiones de Colombia.'),
    ],
  },
  {
    id: 'home_strategies_cards', label: 'Rutas de Acción Territorial · 8 tarjetas', section: 'Home', fields: [
      field('strat_celebra_tag', 'Card 1 - Categoría Tag', 60, 'Estrategia de Circulación'),
      field('strat_celebra_title', 'Card 1 - Título', 80, 'Celebra la Música'),
      field('strat_celebra_desc', 'Card 1 - Descripción', 200, 'Activa escenarios, programación y redes territoriales para que los procesos musicales circulen, se conecten y ganen visibilidad.'),
      field('strat_territorios_tag', 'Card 2 - Categoría Tag', 60, 'Estrategia de Investigación'),
      field('strat_territorios_title', 'Card 2 - Título', 80, 'Territorios Sonoros'),
      field('strat_territorios_desc', 'Card 2 - Descripción', 200, 'Impulsa procesos de investigación, cartografía y documentación para reconocer, interpretar y proyectar la diversidad sonora del país.'),
      field('strat_congreso_tag', 'Card 3 - Categoría Tag', 60, 'Estrategia de Gobernanza y Participación'),
      field('strat_congreso_title', 'Card 3 - Título', 80, '8vo Congreso Nacional de Música'),
      field('strat_congreso_desc', 'Card 3 - Descripción', 200, 'Espacio de diálogo académico, social e institucional para consolidar las políticas del sector y fortalecer la gobernanza musical en el país.'),
      field('strat_tempos_tag', 'Card 4 - Categoría Tag', 60, 'Estrategia de Formación'),
      field('strat_tempos_title', 'Card 4 - Título', 80, 'Tempos de Memorias'),
      field('strat_tempos_desc', 'Card 4 - Descripción', 200, 'Laboratorio formativo enfocado en la cualificación de saberes tradicionales, lutería, pedagogía y preservación de patrimonios sonoros locales.'),
      field('strat_voces_tag', 'Card 5 - Categoría Tag', 60, 'Estrategia de Investigación'),
      field('strat_voces_title', 'Card 5 - Título', 80, 'Voces y Saberes'),
      field('strat_voces_desc', 'Card 5 - Descripción', 200, 'Proceso nacional de documentación y registro para catalogar las expresiones orales y la memoria viva de nuestros cantautores y sabedores.'),
      field('strat_jazz_tag', 'Card 6 - Categoría Tag', 60, 'Estrategia de Circulación'),
      field('strat_jazz_title', 'Card 6 - Título', 80, 'Red Nacional de Jazz'),
      field('strat_jazz_desc', 'Card 6 - Descripción', 200, 'Plataforma de circulación colaborativa que conecta festivales, clubes y músicos de jazz en circuitos nacionales y de intercambio.'),
      field('strat_mercados_tag', 'Card 7 - Categoría Tag', 60, 'Estrategia de Circulación'),
      field('strat_mercados_title', 'Card 7 - Título', 80, 'Mercados Musicales de Colombia'),
      field('strat_mercados_desc', 'Card 7 - Descripción', 200, 'Fortalece el encuentro entre programadores, directores y agrupaciones nacionales para dinamizar la circulación nacional e internacional.'),
      field('strat_mesas_tag', 'Card 8 - Categoría Tag', 60, 'Estrategia de Gobernanza y Circulación'),
      field('strat_mesas_title', 'Card 8 - Título', 80, 'Mesas de Participación'),
      field('strat_mesas_desc', 'Card 8 - Descripción', 200, 'Nodos comunitarios de concertación que articulan el tejido asociativo y las veedurías locales del Plan Nacional de Música.'),
    ],
  },
  // Aqui vivia `map_hero`, con una sola clave: `map_description`, la
  // «introduccion del geovisor». Ya no la lee nadie. Su unico uso real era un
  // parche de la plantilla del tutorial —sustituia la descripcion del primer
  // paso, de modo que una misma clave hacia dos trabajos— y ese parche se quito
  // al declarar los 14 campos propios del tutorial.
  //
  // Se retira en vez de conectarla porque /mapa es un geovisor a pantalla
  // completa: no tiene encabezado ni bloque donde quepan 300 caracteres de
  // introduccion. Inventarle uno seria diseno de producto, no correccion de un
  // defecto. Si algun dia se quiere esa entradilla, se vuelve a declarar y se
  // pinta; hasta entonces, ofrecerla en el panel es prometer un cambio que la
  // pagina ignora.
  /*
   * El tutorial del geovisor: catorce textos que solo ve quien no sabe usarlo.
   *
   * Es la única pieza del portal cuyo público es, por definición, el que más
   * ayuda necesita, y era la que menos podía corregirse: siete pasos de prosa
   * dentro de un arreglo de TypeScript. Ahí siguen viviendo el ORDEN y el icono
   * de cada paso —son estructura—; el texto lo manda el panel.
   *
   * Los números («1.», «2.») se quedan DENTRO del texto editable y no se
   * generan alrededor. Es a propósito: el primer paso es una bienvenida y no
   * lleva número, así que numerar automáticamente exigiría una excepción para
   * el primero, y esa excepción se rompe en cuanto alguien reordene los pasos
   * desde el panel.
   */
  {
    id: 'map_tutorial', label: 'Tutorial del geovisor', section: 'Mapa Ecosistémico', fields: [
      // LOS DIECIOCHO PASOS RECORREN LA PANTALLA QUE HAY, herramienta por herramienta. Eran seis y
      // describían un geovisor retirado; pasaron a nueve y el criterio pide más detalle:
      // «explicar las diferentes herramientas, por ejemplo las diferentes vistas rápidamente,
      // coropleta, símbolos proporcionales, mapas de calor… en el gráfico registros por barras por
      // áreas, lo más declarado… la vista de tablas, los filtros territoriales, el directorio, la
      // agenda». Cada control con una decisión propia tiene ahora su paso, y el paso deja PUESTA la
      // situación que explica.

      field('map_tutorial_1_title', 'Paso 1, título', 80, 'Un mapa que se lee de varias formas'),
      field('map_tutorial_1_desc', 'Paso 1, texto', 400, 'Aquí están los procesos musicales del país situados en su territorio. En dieciocho pasos verás qué hace cada control: qué se está mirando, cómo acotarlo, las tres formas de dibujarlo y las tres de leerlo. Puedes salir cuando quieras y volver desde el botón de ayuda.'),

      field('map_tutorial_2_title', 'Paso 2, título', 80, 'Qué se está mirando'),
      field('map_tutorial_2_desc', 'Paso 2, texto', 400, 'Las capas dicen qué tipo de proceso hay en el mapa. Hoy puedes consultar Festivales y Agenda; las otras cuatro están en preparación y aparecen desactivadas, no vacías. La cifra de al lado es cuántos registros tiene la capa puesta.'),

      field('map_tutorial_3_title', 'Paso 3, título', 80, 'Acotar: buscar y territorio'),
      field('map_tutorial_3_desc', 'Paso 3, texto', 400, 'Busca por nombre, municipio o palabra, o elige departamento y municipio. Lo que pongas aparece como una etiqueta arriba de esta columna y puedes quitarlo de uno en uno, sin deshacer el resto.'),

      field('map_tutorial_4_title', 'Paso 4, título', 80, 'El lente: con qué se colorea'),
      field('map_tutorial_4_desc', 'Paso 4, texto', 400, 'Gira el selector para elegir con qué se reparte el color: por territorios sonoros, por prácticas musicales o por división administrativa. El lente no filtra nada; cambia el criterio con el que se pinta lo mismo, y ese criterio viaja a las otras dos vistas.'),

      field('map_tutorial_5_title', 'Paso 5, título', 80, 'Coropleta: el departamento entero, teñido'),
      field('map_tutorial_5_desc', 'Paso 5, texto', 400, 'Cada departamento se colorea según lo que domina en él o cuántos registros tiene. Se lee de un vistazo, pero tiene una trampa conocida: un departamento grande pesa más en la vista aunque tenga menos procesos. Para comparar cantidades, mejor la siguiente.'),

      field('map_tutorial_6_title', 'Paso 6, título', 80, 'Símbolos proporcionales: un círculo por municipio'),
      field('map_tutorial_6_desc', 'Paso 6, texto', 400, 'Un círculo en cada municipio, más grande cuantos más procesos tenga. A diferencia de la coropleta, el tamaño no depende de lo extenso que sea el territorio, así que sirve para comparar de verdad. Los círculos cercanos se agrupan y se separan al acercar.'),

      field('map_tutorial_7_title', 'Paso 7, título', 80, 'Mapa de calor: la práctica que desborda'),
      field('map_tutorial_7_desc', 'Paso 7, texto', 400, 'Una mancha continua que ignora los límites administrativos. Es la forma de ver lo que las otras dos no pueden: una práctica musical no se detiene en la frontera de un departamento, y aquí se ve dónde se concentra de verdad.'),

      field('map_tutorial_8_title', 'Paso 8, título', 80, 'Cómo leer lo que ves'),
      field('map_tutorial_8_desc', 'Paso 8, texto', 400, 'Esta nota explica qué significan los colores y los tamaños del dibujo que esté puesto, y cambia con él. Puedes plegarla cuando ya no la necesites y volver a abrirla en cualquier momento.'),

      field('map_tutorial_9_title', 'Paso 9, título', 80, 'El mapa responde'),
      field('map_tutorial_9_desc', 'Paso 9, texto', 400, 'Pulsa un departamento para entrar en él y ver sus municipios; pulsa un municipio para abrir la lista de sus procesos. Puedes acercar con la rueda y volver al país entero quitando el filtro de departamento.'),

      field('map_tutorial_10_title', 'Paso 10, título', 80, 'Mapa, gráfico o tabla'),
      field('map_tutorial_10_desc', 'Paso 10, texto', 400, 'Los mismos datos en tres presentaciones, y el conmutador está siempre a mano. Sobre el mapa son tres iconos en la esquina para no tapar territorio; en gráfico y tabla sube arriba con sus rótulos, porque ahí la pregunta es cómo se vuelve.'),

      field('map_tutorial_11_title', 'Paso 11, título', 80, 'El gráfico: siete lecturas del mismo conjunto'),
      field('map_tutorial_11_desc', 'Paso 11, texto', 400, 'Cada posición de esta tira es a la vez una cifra y un control: dice el dato y, al pulsarla, cambia lo que se dibuja debajo. Registros, lo más declarado, municipios, calendario, cobertura… ninguna repite lo que otra ya dice.'),

      field('map_tutorial_12_title', 'Paso 12, título', 80, 'Registros por departamento, en barras'),
      field('map_tutorial_12_desc', 'Paso 12, texto', 400, 'El orden y el valor. Las barras están a escala del mayor, con la rejilla en sus cuartos para poder decir «este está por la mitad» sin medir. Pulsa una fila y el mapa se va a ese departamento.'),

      field('map_tutorial_13_title', 'Paso 13, título', 80, 'Los mismos datos, en áreas'),
      field('map_tutorial_13_desc', 'Paso 13, texto', 400, 'Aquí el área de cada bloque es su parte del total: los bloques juntos son el país entero. La barra dice el orden; el área dice la proporción. No son la misma figura con otro aspecto: responden dos preguntas distintas.'),

      field('map_tutorial_14_title', 'Paso 14, título', 80, 'Lo más declarado, y sobre qué'),
      field('map_tutorial_14_desc', 'Paso 14, texto', 400, 'De qué está hecho el ecosistema según lo que los registros declaran. Puedes medirlo por territorios sonoros o por prácticas y géneros: son dos clasificaciones distintas del mismo conjunto. Cada fila lleva el color que el lente le da en el mapa.'),

      field('map_tutorial_15_title', 'Paso 15, título', 80, 'Barras o unidades'),
      field('map_tutorial_15_desc', 'Paso 15, texto', 400, 'Las barras comparan proporciones; las unidades cuentan. En unidades cada cuadro es un registro, así que se puede contar con el dedo: es útil cuando las cifras son pocas y la diferencia entre cuatro y cinco importa.'),

      field('map_tutorial_16_title', 'Paso 16, título', 80, 'La tabla: el conjunto entero'),
      field('map_tutorial_16_desc', 'Paso 16, texto', 400, 'Para recorrer la lista completa, comparar municipios o imprimirla. Ocupa el ancho entero porque aquí lo que se quiere es leer muchas filas seguidas, no buscar una. Desde este mismo control vuelves al mapa.'),

      field('map_tutorial_17_title', 'Paso 17, título', 80, 'El directorio: los registros, uno a uno'),
      field('map_tutorial_17_desc', 'Paso 17, texto', 400, 'Lista lo que hay en el ámbito que tengas puesto y sigue a los filtros: al entrar en un departamento se acota a él. Pulsa «Ubicar en el mapa» en cualquier ficha para llevar el mapa hasta su municipio.'),

      field('map_tutorial_18_title', 'Paso 18, título', 80, 'La agenda: lo que tiene fecha'),
      field('map_tutorial_18_desc', 'Paso 18, texto', 400, 'Los eventos publicados del territorio abierto, con su fecha y su lugar. Aparece cuando tiene sentido —al abrir un departamento— y no al filtrar por práctica musical, porque un evento no se clasifica así.'),
    ],
  },
  {
    // LOS TEXTOS DEL MODO EXPLORAR, que no son los del recorrido y por eso viven aparte.
    //
    // Los del recorrido están escritos como una secuencia —«ahora mira esto», «a diferencia de la
    // anterior»— y fuera de su orden dejan de sostenerse. Además tres pasos del recorrido explican
    // el mismo control de dibujo, uno por modo: al señalarlo habría que elegir cuál de los tres y
    // ninguno responde a «¿qué es esto?». Son dos trabajos y llevan dos textos.
    id: 'map_explorar', label: 'Modo explorar del geovisor', section: 'Mapa Ecosistémico', fields: [

      field('map_explorar_capas', 'Capas', 300, 'Qué tipo de proceso hay en el mapa. Hoy se consultan Festivales y Agenda; las otras cuatro están en preparación y aparecen desactivadas.'),
      field('map_explorar_filtros', 'Filtrar', 300, 'Acota lo que se ve: por nombre, por departamento o por municipio. Lo que pongas aparece como etiqueta y se quita de uno en uno.'),
      field('map_explorar_lente', 'El lente', 300, 'Con qué criterio se reparte el color: territorios sonoros, prácticas musicales o división administrativa. No filtra nada; cambia cómo se pinta lo mismo.'),
      field('map_explorar_dibujo', 'Cómo se dibuja', 300, 'Tres maneras de proyectar lo mismo: coropleta tiñe departamentos enteros, símbolos pone un círculo por municipio, y calor dibuja una mancha que ignora los límites.'),
      field('map_explorar_lienzo', 'El mapa', 300, 'Pulsa un departamento para entrar en él; pulsa un municipio para ver sus procesos. La rueda acerca y aleja.'),
      field('map_explorar_leyenda', 'Cómo leer esta vista', 300, 'Qué significan los colores y los tamaños del dibujo que esté puesto. Cambia con él, y se puede plegar.'),
      field('map_explorar_vistas', 'Mapa, gráfico o tabla', 300, 'Los mismos datos en tres presentaciones. El gráfico ordena y compara; la tabla sirve para recorrer la lista entera o imprimirla.'),
      field('map_explorar_lecturas', 'Las lecturas', 300, 'Siete maneras de leer el mismo conjunto. Cada posición dice su cifra y, al pulsarla, cambia lo que se dibuja debajo.'),
      field('map_explorar_figura', 'La figura', 300, 'Dos dibujos para la misma lectura, que no dicen lo mismo: la barra da el orden y el valor, el área da la parte del todo, las unidades se pueden contar.'),
      field('map_explorar_dimension', 'La dimensión', 300, 'Sobre qué se mide la composición: territorios sonoros o prácticas y géneros. Son dos clasificaciones distintas del mismo conjunto.'),
      field('map_explorar_listado', 'Directorio y agenda', 300, 'El directorio lista lo que hay en el ámbito puesto; la agenda, lo que tiene fecha. Desde cualquier ficha se puede ubicar el proceso en el mapa.'),
    ],
  },
  {
    id: 'ejes_hero', label: 'Encabezado de la página', section: 'Ejes', fields: [
      // LOS CUATRO TEXTOS DE LA PORTADA DE /ejes, que hasta estaban
      // escritos a mano en `ejes-page.component.html:3-6` mientras la FOTO de esa misma portada
      // ya era administrable (`[bgImage]="imagen('hero_ejes')"`, linea 7). Se podia cambiar la
      // imagen del encabezado y no el titulo que va encima.
      //
      // Los valores son EXACTAMENTE los que la plantilla venia mostrando: declarar una clave con
      // otro texto cambia el sitio el dia que entra, y eso no es conectar, es editar.
      field('ejes_hero_tag', 'Antetítulo', 60, 'Ejes'),
      field('ejes_hero_title', 'Título', 40, 'Ejes de'),
      field('ejes_hero_accent', 'Título acentuado', 60, 'Transformación'),
      field('ejes_hero_desc', 'Descripción del encabezado', 300, 'Explora las dimensiones fundamentales del PNMC.'),
    ],
  },
  {
    id: 'eje1_details', label: 'Eje 1 - Música para la Vida', section: 'Ejes', fields: [
      field('eje01_title', 'Título del Eje', 200, 'MÚSICA PARA LA VIDA, EL DIÁLOGO INTERCULTURAL Y LA DIVERSIDAD BIOCULTURAL'),
      field('eje01_desc1', 'Explicación Párrafo 1', 500, 'Este eje promueve el acceso, la apropiación y la práctica musical como derechos culturales fundamentales, entendiendo la música y lo sonoro como bienes comunes que fortalecen identidades, cohesión social y equidad en el país.'),
      field('eje01_desc2', 'Explicación Párrafo 2', 500, 'Desde una perspectiva de diversidad cultural y biocultural, este eje impulsa procesos que reconocen la música como herramienta para el diálogo intercultural, la construcción de paz y la participación ciudadana.'),
      field('eje01_purpose', 'Propósito General', 400, 'Establecer la música como vehículo de inclusión, identidad y reconciliación, garantizando que todas las personas, sin distinción, puedan vivirla plenamente como parte de su vida, su territorio y su comunidad.'),
      field('eje01_c1_title', 'Comp 1: Título', 120, 'Apropiación de la música y de los derechos culturales'),
      field('eje01_c1_desc', 'Comp 1: Detalle', 400, 'Este componente busca fortalecer el vínculo de la ciudadanía con la música como derecho cultural y bien común. Promueve el acceso equitativo, la participación activa y el disfrute de la música en espacios comunitarios, educativos y culturales.'),
      field('eje01_c2_title', 'Comp 2: Título', 120, 'Enfoque poblacional y cultura de paz'),
      field('eje01_c2_desc', 'Comp 2: Detalle', 400, 'Promueve la inclusión de poblaciones históricamente excluidas en el ecosistema musical, reconociendo sus particularidades culturales y garantizando su acceso equitativo a procesos asociados a la música.'),
    ],
  },
  {
    id: 'eje2_details', label: 'Eje 2 - Prácticas y Oficios', section: 'Ejes', fields: [
      field('eje02_title', 'Título del Eje', 200, 'FORTALECIMIENTO DE LAS PRÁCTICAS, EXPRESIONES Y OFICIOS DE LA MÚSICA'),
      field('eje02_desc1', 'Explicación Párrafo 1', 500, 'Este eje busca fortalecer de manera integral el campo musical en Colombia, garantizando mejores condiciones para la formación, la creación, la producción, la investigación, la dotación y la circulación musical en el país.'),
      field('eje02_desc2', 'Explicación Párrafo 2', 500, 'Se destaca la importancia de la memoria, la identidad y la diversidad cultural como bases para la producción artística y para la construcción del presente y el futuro del sector musical.'),
      field('eje02_purpose', 'Propósito General', 400, 'Dignificar y reconocer profesionalmente los oficios y saberes vinculados a la música, promover la equidad de oportunidades y asegurar la sostenibilidad de las diversas expresiones sonoras del territorio.'),
      field('eje02_c1_title', 'Comp 1: Título', 120, 'Formación'),
      field('eje02_c1_desc', 'Comp 1: Detalle', 450, 'Este componente impulsa procesos de cualificación para músicos, sabedores, pedagogos, licenciados, formadores, investigadores, gestores y otros oficios del ecosistema musical. Busca consolidar la educación y formación musical como un pilar del desarrollo cultural y social del país, articulando acciones con el sistema educativo, el SINEFAC y las Escuelas Municipales y Comunitarias de Música.'),
      field('eje02_c2_title', 'Comp 2: Título', 120, 'Creación y producción'),
      field('eje02_c2_desc', 'Comp 2: Detalle', 400, 'Este componente fortalece las condiciones necesarias para la composición, interpretación, experimentación, grabación y producción musical en el país. Promueve estímulos, laboratorios y herramientas técnicas para desarrollar nuevas obras, integrar saberes tradicionales, potenciar la innovación y ampliar la diversidad sonora del territorio.'),
      field('eje02_c3_title', 'Comp 3: Título', 120, 'Circulación'),
      field('eje02_c3_desc', 'Comp 3: Detalle', 500, 'La circulación es un pilar fundamental para el fortalecimiento del ecosistema musical en Colombia, ya que permite la movilidad y visibilización de las músicas y los músicos en distintos escenarios locales, nacionales e internacionales. Este componente busca consolidar redes de colaboración, potenciar festivales y mercados musicales, e integrar a los artistas en diversos circuitos culturales, facilitando el acceso a oportunidades de difusión y profesionalización.'),
      field('eje02_c4_title', 'Comp 4: Título', 120, 'Memoria, investigación y documentación'),
      field('eje02_c4_desc', 'Comp 4: Detalle', 400, 'Este componente impulsa la preservación, investigación y difusión del patrimonio sonoro del país. Articula el conocimiento académico con los saberes comunitarios y ancestrales para documentar repertorios, prácticas y trayectorias musicales.'),
      field('eje02_c5_title', 'Comp 5: Título', 120, 'Información y comunicación'),
      field('eje02_c5_desc', 'Comp 5: Detalle', 500, 'El acceso a información clara, actualizada y estructurada es clave para la toma de decisiones y el diseño de políticas públicas pertinentes. Este componente fortalece la recopilación, sistematización y divulgación de datos del sector musical, promoviendo herramientas como el SIMUS y estrategias de comunicación que permitan a gestores, instituciones, investigadores y ciudadanía comprender y usar la información del ecosistema musical.'),
      field('eje02_c6_title', 'Comp 6: Título', 120, 'Dotación e infraestructura'),
      field('eje02_c6_desc', 'Comp 6: Detalle', 400, 'Este componente garantiza el acceso a instrumentos, herramientas técnicas y espacios adecuados para la formación, creación y circulación musical.'),
    ],
  },
  {
    id: 'eje3_details', label: 'Eje 3 - Gobernanza', section: 'Ejes', fields: [
      field('eje03_title', 'Título del Eje', 200, 'GOBERNANZA MUSICAL E INTEGRACIÓN CULTURAL E INTERSECTORIAL'),
      field('eje03_desc1', 'Explicación Párrafo 1', 500, 'Este eje promueve el fortalecimiento de los mecanismos de organización, participación y articulación del sector musical con y desde el Estado.'),
      field('eje03_desc2', 'Explicación Párrafo 2', 500, 'Consolidando una gobernanza efectiva que garantice la sostenibilidad cultural del ecosistema musical en Colombia.'),
      field('eje03_purpose', 'Propósito General', 400, 'Consolidar una gobernanza sólida y una articulación intersectorial amplia que potencie la capacidad de la música para incidir en la transformación social, la construcción de paz y la reducción de desigualdades.'),
      field('eje03_c1_title', 'Comp 1: Título', 120, 'Participación ciudadana, intersectorialidad y articulación territorial'),
      field('eje03_c1_desc', 'Comp 1: Detalle', 400, 'Este componente busca fortalecer la participación activa del sector musical en la formulación y ejecución de políticas públicas, promoviendo espacios de diálogo, concertación y decisión colectiva como los Comités Departamentales de Música y los Planes Departamentales de Desarrollo Musical.'),
      field('eje03_c2_title', 'Comp 2: Título', 120, 'Sostenibilidad, condiciones laborales y economías de la música'),
      field('eje03_c2_desc', 'Comp 2: Detalle', 450, 'Este componente se centra en mejorar las condiciones laborales y económicas de los actores del ecosistema musical, promoviendo la formalización, la seguridad social, la dignificación del trabajo y el fortalecimiento de capacidades en gestión, producción y emprendimiento.'),
    ],
  },
  {
    id: 'component_detail', label: 'Ficha de componente', section: 'Ejes', fields: [
      /*
        `/ejes/componentes/:id` no tenia ni una clave, y es la pagina donde termina cualquiera que
        pulse un componente en `/ejes`. Entran los dos bloques de relacionados —con su bajada, que
        es la que explica que hace ahi ese listado— y el mensaje de componente inexistente, que es
        lo que ve quien llega con un enlace viejo.

        NO entran los rotulos de navegacion —«Conoce el eje completo», «Explorar editorial»—: son
        destinos, y la regla del proyecto es que el CMS diga como se llama un boton y la
        configuracion adonde lleva.
      */
      field('component_products_title', 'Productos, título', 60, 'Productos relacionados'),
      field('component_products_desc', 'Productos, bajada', 200, 'Material del acervo editorial vinculado a este componente.'),
      field('component_agenda_title', 'Agenda, título', 60, 'Agenda relacionada'),
      field('component_agenda_desc', 'Agenda, bajada', 200, 'Eventos de la agenda vinculados a este componente.'),
      field('component_missing_title', 'Componente inexistente', 80, 'Componente no encontrado'),
    ],
  },
  {
    id: 'strategy_celebra_details', label: 'Celebra la Música', section: 'Estrategias', fields: [
      field('strategy_celebra_hero_desc', 'Descripción Hero', 300, 'Una estrategia nacional que articula territorios, agentes e instituciones para visibilizar la diversidad sonora de Colombia.'),
      field('strategy_celebra_section_title', 'Título sección', 120, 'La celebración de la música'),
      field('strategy_celebra_intro', 'Introducción', 400, 'Como parte del Plan Nacional de Música para la Convivencia del Ministerio de las Culturas del Gobierno de Colombia, Celebra la Música busca que el sonido y la creatividad lleguen a todos los rincones del país, para que cada territorio haga oír su voz.'),
      field('strategy_celebra_mission', 'Misión', 500, 'Su propósito es conectar a artistas, comunidades e instituciones para fortalecer los procesos de formación, creación y circulación musical. Promueve la música como un derecho, un espacio de encuentro y una oportunidad para construir memoria, dignificar el trabajo artístico y enriquecer la vida cultural del país.'),
      field('strategy_celebra_edition_intro', 'Edición Intro', 500, 'En 2025, Celebra la Música se renueva para convertirse en un gran proceso nacional que promueve la circulación musical en el país y que no será solo una jornada conmemorativa, sino un movimiento que, durante 29 días, unirá a los 32 departamentos de Colombia en torno a la diversidad sonora.'),
      field('strategy_celebra_edition_vision', 'Edición Visión', 500, 'Esta edición se articula con el Plan Nacional de Cultura 2024-2038 y el Plan Nacional de Música para la Convivencia 2025-2035, impulsando espacios de formación, creación, circulación y memoria.'),
      field('strategy_celebra_edition_closing', 'Conclusión', 300, 'Celebra la Música 2025 es una apuesta por hacer de la música un camino para la convivencia, la paz y la vida.'),
    ],
  },
  {
    id: 'strategy_territorios_details', label: 'Territorios Sonoros', section: 'Estrategias', fields: [
      /*
        LAS DOS QUE LA PAGINA MUESTRA, Y NI UNA MAS.

        Territorios Sonoros era la unica estrategia sin ninguna clave: `cmsPrefix: null` en
        `resolve-strategy.ts`. Eso dejaba su FOTO administrable sin ningun bloque del acordeon
        donde editarse, porque el editor de imagenes vive dentro del bloque de sus textos.

        Se declaran las dos que la pagina pinta hoy —la bajada del encabezado y el parrafo de
        entrada— con el texto que ya venia mostrando. NO se declara el relato de tres partes que
        si tiene Celebra la Musica: esa pagina no lo pinta, y escribirlo aqui seria inventar
        copia institucional para justificar unos campos. Cuando exista la copia, se anade el
        bloque y se pone `tieneRelato: true`.
      */
      field('strategy_territorios_hero_desc', 'Descripción Hero', 300, 'Una ruta para reconocer, documentar y fortalecer la diversidad musical desde los territorios de Colombia.'),
      field('strategy_territorios_intro', 'Introducción', 400, 'Territorios Sonoros conecta comunidades, investigadores, portadores de saberes e instituciones para comprender las prácticas musicales en su contexto y convertir ese conocimiento en acciones de política pública.'),
    ],
  },
  {
    /**
     * Portada de /registro: lo que lee una persona ANTES de entregar sus datos.
     *
     * La pagina tenia 119 cadenas fijas y cero cobertura del CMS. Aqui no se
     * declaran las 119: los rotulos de formulario y los mensajes de estado son
     * mecanica de la interfaz y llenarian el panel de ruido. Se declara la prosa
     * que un area de comunicaciones o juridica querria reescribir, y sobre todo
     * las dos frases de consentimiento.
     *
     * Que los consentimientos sean editables desde el panel puede sonar
     * arriesgado; lo arriesgado es lo contrario. Hoy corregir «Acepto la
     * politica de tratamiento de datos» exige un despliegue, y el panel ya tiene
     * borrador, publicacion, historial y roles: un gestor no puede publicar.
     * Es mas control del que habia, no menos.
     */
    id: 'access_external_portal', label: 'Portada de acceso externo', section: 'Acceso externo', fields: [
      // El 1 de septiembre de 2026 la columna de marca paso a mostrar un mensaje distinto segun la
      // pestaña activa: estas tres claves quedaron para «Registrarse» -es el mensaje que ya existia
      // y que la dirección de producto confirmo que «puede quedar tal cual»- y las tres siguientes,
      // nuevas, son las de «Ingresar». Antes eran un solo bloque compartido por las dos pestañas.
      field('access_eyebrow', 'Rótulo superior · Registrarse', 40, 'Acceso externo'),
      field('access_title', 'Título · Registrarse', 60, 'Haz parte del ecosistema'),
      field('access_intro', 'Presentación · Registrarse', 220, 'Crea tu cuenta, gestiona organizaciones y mantiene actualizada la información del ecosistema musical.'),
      // Las siete claves de las dos tarjetas de eleccion —«Registro personal» y «Registrar una
      // organizacion»— se retiraron el 25 ago 2026 con las tarjetas: el alta dejo de ser una
      // eleccion entre dos caminos y paso a ser un formulario. Una clave que nadie lee es una
      // casilla que alguien va a editar creyendo que cambia algo.
      field('access_register_intro', 'Alta, entradilla', 240, 'Registra tu organización y la persona que responderá por ella. Con ese mismo correo y contraseña entrarás después.'),
      field('access_footer_note', 'Nota al pie', 200, 'Este es el acceso externo del PNMC. El acceso institucional se mantiene separado.'),
    ],
  },
  /*
   * Ecosistema — la sección que no tenía pestaña.
   *
   * `/ecosistema` y sus directorios son la lectura pública del ecosistema
   * musical del PNMC y llevaban 0 claves: todo su texto vivía fijo en
   * plantillas y arreglos de TypeScript. Es la sección con más prosa
   * institucional del portal —qué reúne, para qué sirve, cómo se navega, cómo
   * se participa— y era la única que exigía un despliegue para corregir una
   * coma.
   *
   * Se llamaba «SIMUS» hasta que se aclaró que el Sistema de Información de la
   * Música es una plataforma EXTERNA (simus.mincultura.gov.co) y no un módulo
   * de este portal. Las claves conservan su forma y su orden; lo que cambió es
   * el prefijo (`simus_` → `ecosistema_`) y toda la copia que se presentaba
   * como si el sistema fuera nuestro. El renombrado de las filas ya sembradas
   * lo hace `RenombradoContenidoWeb` en el arranque del API, sin perder lo que
   * haya escrito una persona.
   *
   * Qué NO entra, y por qué: los once textos de «módulo en construcción» de
   * `/ecosistema/:section` se quedan fijos. Son copia transitoria —desaparecen
   * según cada módulo se publica— y meter veintidós campos de «está en
   * construcción» en el panel es ruido que estorba a quien busca lo editable de
   * verdad. Tampoco entran los rótulos de los botones de volver ni los estados
   * de carga: son mecánica de la interfaz.
   */
  {
    id: 'ecosistema_hero', label: 'Encabezado', section: 'Ecosistema', fields: [
      field('ecosistema_hero_tag', 'Antetítulo', 60, 'Ecosistema musical de Colombia'),
      field('ecosistema_hero_title', 'Título', 40, 'Ecosistema'),
      field('ecosistema_hero_accent', 'Título acentuado', 60, 'que conecta el territorio'),
      field('ecosistema_hero_desc', 'Descripción del encabezado', 300, 'Un espacio público para conocer, consultar y fortalecer la información sobre las músicas, sus procesos y quienes las hacen posibles en Colombia.'),
    ],
  },
  {
    id: 'ecosistema_about', label: 'Qué reúne', section: 'Ecosistema', fields: [
      field('ecosistema_about_tag', 'Qué reúne, antetítulo', 40, 'Consulta pública'),
      field('ecosistema_about_title', 'Qué reúne, título', 120, 'Una puerta de entrada al ecosistema musical.'),
      field('ecosistema_about_p1', 'Qué reúne, párrafo 1', 400, 'SIMUS, el Sistema de Información de la Música, registra, organiza, relaciona y actualiza información estructurada sobre el ecosistema musical colombiano. Reúne agentes, organizaciones, procesos, prácticas, territorios e infraestructuras para hacer visible la riqueza musical del país.'),
      field('ecosistema_about_p2', 'Qué reúne, párrafo 2', 400, 'Escuelas, festivales, mercados, escenarios, documentación, agenda y contenidos editoriales se conectan aquí para facilitar la consulta pública. El Mapa Ecosistémico ofrece una mirada territorial integral; los directorios especializados permiten profundizar en cada registro.'),
    ],
  },
  /*
   * AQUI ESTUVIERON LOS GRUPOS `ecosistema_simus_externo` (4 campos) y `ecosistema_routes`
   * (11 campos), retirados con las dos secciones que los pintaban.
   *
   * La direccion de producto elimino de /ecosistema la tarjeta que enlazaba a SIMUS y el bloque
   * morado «Consulta, participa y gestiona». Sin lector, esas 15 claves quedaban en el panel
   * como campos que se pueden editar y no cambian nada en ninguna pantalla, y `cms:huerfanas`
   * las habria reportado en cada ejecucion.
   *
   * EL ENLACE A SIMUS NO ERA EDITABLE Y SIGUE SIN SERLO: la URL vive en `ENLACES_EXTERNOS.simus`
   * y las rutas `/simus/*` de `app.routes.ts` siguen redirigiendo. Lo que se fue es la copia de
   * la tarjeta, no el destino.
   */
  {
    id: 'ecosistema_explore', label: 'Actualidad y conocimiento', section: 'Ecosistema', fields: [
      field('ecosistema_explore_tag', 'Actualidad y conocimiento - Antetítulo', 40, 'Actualidad y conocimiento'),
      field('ecosistema_explore_title', 'Actualidad y conocimiento - Título', 120, 'Sigue las músicas en movimiento'),
      field('ecosistema_explore_desc', 'Rutas de consulta - Introducción', 300, 'Consulta información disponible, conoce la actualidad musical o recorre las publicaciones del PNMC.'),
      field('ecosistema_explore_map_title', 'Consulta «Mapa» - Título', 60, 'Lee el territorio'),
      field('ecosistema_explore_map_desc', 'Consulta «Mapa» - Descripción', 200, 'Conecta las categorías y su presencia geográfica en el Mapa Ecosistémico.'),
      field('ecosistema_explore_editorial_title', 'Consulta «Editorial» - Título', 60, 'Consulta contenidos'),
      field('ecosistema_explore_editorial_desc', 'Consulta «Editorial» - Descripción', 200, 'Accede a publicaciones, documentos y recursos desde Editorial.'),
      field('ecosistema_explore_news_title', 'Consulta «Noticias» - Título', 60, 'Mantente al día'),
      field('ecosistema_explore_news_desc', 'Consulta «Noticias» - Descripción', 200, 'Conoce noticias y agenda de la actividad musical del país.'),
    ],
  },
  {
    id: 'ecosistema_map', label: 'Guía de exploración', section: 'Ecosistema', fields: [
      field('ecosistema_map_tag', 'Guía de exploración - Antetítulo', 40, 'Guía de exploración'),
      field('ecosistema_map_title', 'Guía de exploración - Título', 120, 'Cómo explorar SIMUS'),
      field('ecosistema_map_desc', 'Botón «Ver en el mapa» - Descripción de apoyo', 300, 'Consulta la distribución territorial de los registros disponibles.'),
      field('ecosistema_map_cta', 'Botón «Ver en el mapa»', 60, 'Ver en el mapa ecosistémico'),
      field('ecosistema_nav_1_title', 'Guía - Paso 1, título', 40, 'Directorios'),
      field('ecosistema_nav_1_desc', 'Guía - Paso 1, texto', 200, 'Consulta registros específicos mediante búsqueda, filtros territoriales, prácticas musicales, lista, mosaico y fichas individuales.'),
      field('ecosistema_nav_2_title', 'Guía - Paso 2, título', 40, 'Mapa Ecosistémico'),
      field('ecosistema_nav_2_desc', 'Guía - Paso 2, texto', 200, 'Observa distribuciones, concentraciones y capas territoriales; desde allí puedes interpretar los datos y volver a cada ficha.'),
      field('ecosistema_nav_3_title', 'Guía - Paso 3, título', 40, 'Contenidos y actualidad'),
      field('ecosistema_nav_3_desc', 'Guía - Paso 3, texto', 200, 'Recorre la agenda, las noticias, la editorial y la galería para conocer la actividad musical y sus contextos.'),
    ],
  },
  {
    // EL GRUPO SE LLAMA COMO LO QUE ENCABEZA, y sus campos ya no repiten ese nombre. Cuando
    // tenia seis campos —tres de «Actores» y tres de «Procesos»— el prefijo distinguia un
    // bloque del otro. Al quedarse en tres, los tres empezaban por «Procesos - » delante del
    // titulo del formulario que ya dice «Procesos»: prefijo constante, que es justo lo que
    // `registro-de-textos-web.spec.ts` prohibe.
    id: 'ecosistema_categories', label: 'Directorios públicos', section: 'Ecosistema', fields: [
      field('ecosistema_processes_tag', 'Antetítulo', 40, 'Directorios públicos'),
      field('ecosistema_processes_title', 'Título', 120, 'Explora los registros'),
      field('ecosistema_processes_desc', 'Introducción', 300, 'Busca, filtra por territorio y consulta fichas individuales de los procesos disponibles.'),
    ],
  },
  {
    id: 'ecosistema_participate', label: 'Ayuda y tutoriales', section: 'Ecosistema', fields: [
      field('ecosistema_join_tag', 'Antetítulo', 60, 'Ayuda y tutoriales'),
      field('ecosistema_join_title', 'Título', 120, 'Acompañamiento para navegar'),
      field('ecosistema_join_desc', 'Texto', 400, 'Este espacio reunirá guías para registrarse, actualizar información, consultar el mapa y utilizar los filtros. Mientras se amplía, puedes revisar la ayuda o contactar al equipo del PNMC.'),
      field('ecosistema_join_cta', 'Botón', 60, 'Ver ayuda y tutoriales'),
    ],
  },
  {
    id: 'ecosistema_directorios', label: 'Festivales publicados', section: 'Ecosistema', fields: [
      field('ecosistema_festivals_title', 'Título del encabezado', 60, 'Festivales'),
      field('ecosistema_festivals_subtitle', 'Subtítulo', 100, 'Procesos de circulación y encuentro'),
      field('ecosistema_festivals_tag', 'Antetítulo', 40, 'Consulta pública'),
      field('ecosistema_festivals_heading', 'Título de la consulta', 120, 'Festivales musicales'),
      // `ecosistema_festivals_desc` se retiró: pintaba un párrafo bajo la
      // barra de herramientas que el repositorio de referencia no tiene, y la dirección de producto
      // pidió quitarlo.
      field('ecosistema_festivals_search_label', 'Rótulo del buscador', 80, 'Buscar Festival, territorio o práctica'),
      field('ecosistema_festivals_empty_title', 'Sin resultados, título', 80, 'No hay Festivales para esta consulta'),
      field('ecosistema_festivals_empty_desc', 'Sin resultados, texto', 200, 'Prueba con otro nombre, territorio o práctica musical.'),
    ],
  },
  /*
   * La página de error: cinco textos que solo lee quien ya se perdió.
   *
   * Entra al panel por la misma razón que el tutorial del mapa —su público es
   * el que más ayuda necesita— y por una propia: es la única página del sitio
   * cuyo texto puede volverse falso sin que nadie toque la página. Cuando una
   * sección se renombra o se retira, el 404 sigue diciendo lo de siempre, y
   * corregirlo pedía un despliegue. Acaba de pasar: `/simus` dejó de existir.
   */
  {
    id: 'general_404', label: 'Página no encontrada (error 404)', section: 'Navegación y Footer', fields: [
      field('notfound_eyebrow', 'Rótulo superior', 40, 'Error 404'),
      field('notfound_title', 'Título', 60, 'Página no encontrada'),
      field('notfound_desc', 'Explicación', 300, 'La página que buscas no existe o fue movida. Verifica la dirección o vuelve al inicio para seguir explorando el Plan Nacional de Música para la Convivencia.'),
      field('notfound_cta_home', 'Botón al inicio', 40, 'Volver al inicio'),
      field('notfound_cta_map', 'Botón al mapa', 40, 'Ir al mapa ecosistémico'),
    ],
  },
  {
    id: 'general_nav_footer', label: 'Enlaces y Contacto', section: 'Navegación y Footer', fields: [
      field('nav_pnmc', 'Menú - Sobre el PNMC', 40, 'Sobre el PNMC'),
      field('nav_ejes', 'Menú - Ejes de Transformación', 40, 'Ejes'),
      field('nav_editorial', 'Menú - Editorial', 40, 'Editorial'),
      field('nav_galeria', 'Menú - Galería', 40, 'Galería'),
      field('nav_noticias', 'Menú - Noticias', 40, 'Noticias'),
      field('nav_agenda', 'Menú - Agenda', 40, 'Agenda'),
      // Se llamó `nav_mapa` mientras el enlace del menú tenía el identificador
      // del mapa. Renombrarla a `nav_ecosistema` es lo que hace que el editor
      // que quiera cambiar cómo se llama «Ecosistema» esté cambiando eso y no
      // el rótulo de otra página. La vieja se retira en el arranque.
      field('nav_ecosistema', 'Menú - Ecosistema', 40, 'Ecosistema'),
      field('nav_components_title', 'Menú - Título de componentes', 60, 'Componentes del eje'),
      field('footer_col2_title', 'Footer - Col 2 Título (Ministerio)', 120, 'Ministerio de las Culturas, las Artes y los Saberes'),
      field('footer_col2_address', 'Footer - Col 2 Dirección', 120, 'Dirección: Calle 9 No. 8 - 31 Bogotá'),
      field('footer_col2_schedule', 'Footer - Col 2 Horario', 150, 'Horario de atención: 8:00 a.m. a 5:00 p.m. jornada continua.'),
      field('footer_col2_phone', 'Footer - Col 2 Teléfono', 60, 'Teléfono: +57 (601) 3424100'),
      field('footer_col2_free_line', 'Footer - Col 2 Línea Gratuita', 60, 'Línea gratuita: 018000 938081'),
      field('footer_col3_title', 'Footer - Col 3 Título (Correspondencia)', 120, 'Contacto Correspondencia'),
      field('footer_col3_address', 'Footer - Col 3 Dirección', 120, 'Dirección: Calle 9 No. 8 - 31 Bogotá'),
      field('footer_col3_schedule', 'Footer - Col 3 Horario', 150, 'Lunes a viernes de 8:00 a.m. a 4:00 p.m. jornada continua'),
      field('footer_col3_email_label', 'Footer - Col 3 Etiqueta Correo', 30, 'Correo:'),
      field('footer_col3_email', 'Footer - Col 3 Correo Servicio', 80, 'servicioalciudadano@mincultura.gov.co'),
      field('footer_col3_email_note', 'Footer - Col 3 Correo Aviso', 150, '(Los correos que se reciban después de las 5:00 p. m., se radicarán el siguiente día hábil)'),
      field('footer_col3_corruption_title', 'Footer - Denuncias Corrupción', 100, 'Registro de denuncias de corrupción:'),
      field('footer_col3_corruption_email', 'Footer - Denuncias Correo', 80, 'soytransparente@mincultura.gov.co'),
      field('footer_col3_legal_title', 'Footer - Notificaciones Título', 100, 'Notificaciones judiciales:'),
      field('footer_col3_legal_email', 'Footer - Notificaciones Correo', 80, 'notificaciones@mincultura.gov.co'),
      field('footer_col4_services_title', 'Footer - Col 4 Servicios Título', 80, 'Servicios a la Ciudadanía'),
      field('footer_col4_about_title', 'Footer - Col 4 Acerca Título', 80, 'Acerca del sitio'),
      // Solo el prefijo. La plantilla añade el año —que se calcula, para que en
      // enero no haya que acordarse— y el nombre del Ministerio. El texto de
      // fábrica llevaba la línea entera, así que el pie renderizaba «Copyright ©
      // 2026 Ministerio de las Culturas 2026 Ministerio de las Culturas» en
      // todas las páginas del portal. La instantánea lo tenía grabado como
      // normal desde que existe la puerta.
      field('footer_credits_text', 'Footer - Créditos Copyright', 120, 'Copyright ©'),
      field('footer_credits_tagline', 'Footer - Lema Institucional', 100, 'Colombia - Potencia de la Vida'),
    ],
  },
];

/** Compatibilidad con el editor de la seccion "Sobre PNMC". */
export const ABOUT_PNMC_TEXT_GROUPS: DefinicionDeGrupoDeTexto[] =
  WEB_TEXT_GROUPS.filter((group) => group.section === 'Sobre PNMC');

export const WEB_TEXT_FIELDS: DefinicionDeCampoDeTexto[] =
  WEB_TEXT_GROUPS.flatMap((group) => group.fields);

/** Texto compilado que se sirve cuando una clave no tiene version publicada. */
export const DEFAULT_TEXTS: Record<string, string> =
  Object.fromEntries(WEB_TEXT_FIELDS.map((f) => [f.key, f.defaultValue]));

export const WEB_TEXT_KEYS_LIST: DescriptorDeClaveDeTexto[] = WEB_TEXT_GROUPS.flatMap((group) =>
  group.fields.map((f) => ({ key: f.key, label: f.label, section: group.section, limit: f.limit })),
);

/** Busqueda por clave; evita el find() lineal que hacia el panel por cada campo. */
export const WEB_TEXT_KEY_INDEX: ReadonlyMap<string, DescriptorDeClaveDeTexto> =
  new Map(WEB_TEXT_KEYS_LIST.map((descriptor) => [descriptor.key, descriptor]));

/**
 * Orden de las pestanas del panel. Se declara explicito porque el orden de
 * lectura no coincide con el orden en que se editan las secciones.
 */
export const WEB_TEXT_SECTIONS: DefinicionDeSeccionDeTexto[] = [
  { section: 'Home', pill: 'Home' },
  { section: 'Sobre PNMC', pill: 'Sobre PNMC' },
  { section: 'Mapa Ecosistémico', pill: 'Mapa' },
  { section: 'Ejes', pill: 'Ejes' },
  { section: 'Estrategias', pill: 'Estrategias' },
  { section: 'Ecosistema', pill: 'Ecosistema' },
  { section: 'Acceso externo', pill: 'Registro' },
  { section: 'Navegación y Footer', pill: 'Navegación y Footer' },
];
