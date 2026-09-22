export interface EjeComponent {
  id: string;
  slug: string;
  name: string;
  details: string;
  fullText: string[];
  /**
   * Etiquetas usadas para vincular productos del catálogo editorial con este
   * componente. Si se omite, el emparejamiento cae en las palabras clave del
   * nombre del componente. Ej.: ['circulación', 'festivales', 'mercados'].
   */
  relatedTags?: string[];
}

export interface EjeGroup {
  id: string;
  title: string;
  axisExplain: string[];
  purpose: string;
  videoImg: string;
  components: EjeComponent[];
}

export const ejesDataGlobal: EjeGroup[] = [
  { 
    id: "01", 
    title: "MÚSICA PARA LA VIDA, EL DIÁLOGO INTERCULTURAL Y LA DIVERSIDAD BIOCULTURAL", 
    axisExplain: [
      "Este eje promueve el acceso, la apropiación y la práctica musical como derechos culturales fundamentales, entendiendo la música y lo sonoro como bienes comunes que fortalecen identidades, cohesión social y equidad en el país.",
      "Desde una perspectiva de diversidad cultural y biocultural, este eje impulsa procesos que reconocen la música como herramienta para el diálogo intercultural, la construcción de paz y la participación ciudadana."
    ],
    purpose: "Establecer la música como vehículo de inclusión, identidad y reconciliación, garantizando que todas las personas, sin distinción, puedan vivirla plenamente como parte de su vida, su territorio y su comunidad.",
    videoImg: "https://images.unsplash.com/photo-1774558396253-be05d7a37d82?q=80&w=1470&auto=format&fit=crop", 
    components: [
      { 
        id: "c1-1", 
        slug: "apropiacion-y-derechos",
        name: "Apropiación de la música y de los derechos culturales", 
        details: "Este componente busca fortalecer el vínculo de la ciudadanía con la música como derecho cultural y bien común.",
        fullText: [
          "Este componente busca fortalecer el vínculo de la ciudadanía con la música como derecho cultural y bien común. Promueve el acceso equitativo, la participación activa y el disfrute de la música en espacios comunitarios, educativos y culturales.",
          "Impulsa la valoración y práctica de la diversidad musical del país como motor de desarrollo social, fortalecimiento del tejido comunitario y construcción de ciudadanía."
        ]
      }, 
      { 
        id: "c1-2", 
        slug: "poblacional-y-paz",
        name: "Enfoque poblacional y cultura de paz", 
        details: "Promueve la inclusión de poblaciones históricamente excluidas en el ecosistema musical.",
        fullText: [
          "Promueve la inclusión de poblaciones históricamente excluidas en el ecosistema musical, reconociendo sus particularidades culturales y garantizando su acceso equitativo a procesos asociados a la música.",
          "Desde un enfoque de derechos y bioculturalidad, posiciona la música como medio para la reconciliación, la memoria histórica y la reconstrucción del tejido social.",
          "Incluye la participación de grupos étnicos, comunidades en situación de vulnerabilidad y otros colectivos poblacionales, así como el desarrollo de programas de formación musical en centros penitenciarios, donde la música se convierte en herramienta de resocialización, expresión creativa y desarrollo personal."
        ]
      }
    ] 
  },
  { 
    id: "02", 
    title: "FORTALECIMIENTO DE LAS PRÁCTICAS, EXPRESIONES Y OFICIOS DE LA MÚSICA", 
    axisExplain: [
      "Este eje busca fortalecer de manera integral el campo musical en Colombia, garantizando mejores condiciones para la formación, la creación, la producción, la investigación, la dotación y la circulación musical en el país.",
      "Se destaca la importancia de la memoria, la identidad y la diversidad cultural como bases para la producción artística y para la construcción del presente y el futuro del sector musical."
    ],
    purpose: "Dignificar and reconocer profesionalmente los oficios y saberes vinculados a la música, promover la equidad de oportunidades y asegurar la sostenibilidad de las diversas expresiones sonoras del territorio.",
    videoImg: "https://images.unsplash.com/photo-1774558396280-c14b21198674?q=80&w=1470&auto=format&fit=crop", 
    components: [
      { 
        id: "c2-1", 
        slug: "formacion",
        name: "Formación", 
        details: "Este componente impulsa procesos de cualificación para músicos, sabedores, pedagogos y otros oficios.",
        fullText: [
          "Este componente impulsa procesos de cualificación para músicos, sabedores, pedagogos, licenciados, formadores, investigadores, gestores y otros oficios del ecosistema musical. Busca consolidar la educación y formación musical como un pilar del desarrollo cultural y social del país, articulando acciones con el sistema educativo, el SINEFAC y las Escuelas Municipales y Comunitarias de Música.",
          "Promueve la estructuración y certificación de la oferta formativa, el fortalecimiento de la formación especializada en producción, gestión y ejecución musical, y la protección y transmisión de saberes tradicionales. Incorpora una perspectiva psicosocial y comunitaria que reconoce la música como herramienta para la creatividad, la cohesión social y la construcción de identidades individuales y colectivas."
        ]
      }, 
      { 
        id: "c2-2", 
        slug: "creacion-y-produccion",
        name: "Creación y producción", 
        details: "Fortalece las condiciones necesarias para la composición, experimentación y grabación musical.",
        fullText: [
          "Este componente fortalece las condiciones necesarias para la composición, interpretación, experimentación, grabación y producción musical en el país. Promueve estímulos, laboratorios y herramientas técnicas para desarrollar nuevas obras, integrar saberes tradicionales, potenciar la innovación y ampliar la diversidad sonora del territorio.",
          "Su objetivo es facilitar procesos creativos sostenibles y ampliar las posibilidades de expresión y producción de los agentes del sector musical."
        ]
      }, 
      { 
        id: "c2-3",
        slug: "circulacion",
        name: "Circulación",
        relatedTags: ["circulación", "festivales", "mercados musicales", "circuitos"],
        details: "Componente orientado a la movilidad, visibilización y articulación de músicos, músicas, festivales, mercados y circuitos culturales en escenarios locales, nacionales e internacionales.",
        fullText: [
          "La circulación es un pilar fundamental para el fortalecimiento del ecosistema musical en Colombia, ya que permite la movilidad y visibilización de las músicas y los músicos en distintos escenarios locales, nacionales e internacionales. Este componente busca consolidar redes de colaboración, potenciar festivales y mercados musicales, e integrar a los artistas en diversos circuitos culturales, facilitando el acceso a oportunidades de difusión y profesionalización.",
          "Las acciones particulares de este componente incluyen la dinamización de espacios para la circulación musical, promoviendo la programación en escenarios convencionales y no convencionales y articulando esfuerzos con el CNA, la Red Nacional de Teatros y Escenarios Públicos y Patrimoniales, el programa Salas Concertadas, Espacios Vivos y otras estrategias territoriales para activar espacios urbanos y rurales con vocación de música en vivo.",
          "También impulsa Territorios Sonoros de Colombia como una estrategia de alcance nacional que articula la circulación musical con el turismo cultural y comunitario. Esta línea fortalece festivales, procesos de lutería y experiencias de formación de públicos, y dialoga con la implementación de los territorios creativos, bioculturales y de los saberes como instrumentos clave de gestión territorial y desarrollo local.",
          "De igual forma, plantea una estrategia integral de circulación musical en el marco del Sistema Nacional de Circulación de las Culturas, las Artes y los Saberes, en consonancia con los lineamientos del artículo 189 de la Ley 2294 de 2023, proyectando la movilidad de músicos, obras, productos y servicios en contextos locales, regionales, nacionales e internacionales.",
          "Finalmente, propone el fomento de redes y circulación colectiva mediante el fortalecimiento de redes de prácticas musicales, encuentros, circuitos colaborativos y plataformas de intercambio que faciliten la movilidad de artistas, agrupaciones y proyectos, optimicen recursos, fortalezcan economías a escala y contribuyan a descentralizar la circulación musical."
        ]
      }, 
      { 
        id: "c2-4", 
        slug: "memoria-investigacion-documentacion",
        name: "Memoria, investigación y documentación", 
        details: "Impulsa la preservación, investigación y difusión del patrimonio sonoro del país.",
        fullText: [
          "Este componente impulsa la preservación, investigación y difusión del patrimonio sonoro del país. Articula el conocimiento académico con los saberes comunitarios y ancestrales para documentar repertorios, prácticas y trayectorias musicales.",
          "Busca fortalecer la memoria musical como elemento esencial para la apropiación cultural, la revitalización de expresiones locales y la transmisión intergeneracional de saberes."
        ]
      }, 
      { 
        id: "c2-5", 
        slug: "informacion-y-comunicacion",
        name: "Información y comunicación", 
        details: "Fortalece la recopilación y divulgación de datos del sector musical nacional.",
        fullText: [
          "El acceso a información clara, actualizada y estructurada es clave para la toma de decisiones y el diseño de políticas públicas pertinentes. Este componente fortalece la recopilación, sistematización y divulgación de datos del sector musical, promoviendo herramientas como el SIMUS y estrategias de comunicación que permitan a gestores, instituciones, investigadores y ciudadanía comprender y usar la información del ecosistema musical."
        ]
      }, 
      { 
        id: "c2-6", 
        slug: "dotacion-e-infraestructura",
        name: "Dotación e infraestructura", 
        details: "Garantiza el acceso a instrumentos, herramientas técnicas y espacios adecuados.",
        fullText: [
          "Este componente garantiza el acceso a instrumentos, herramientas técnicas y espacios adecuados para la formación, creación y circulación musical.",
          "Promueve la dotación de instrumentos en centros de formación, el mejoramiento de infraestructura en Escuelas Municipales y Comunitarias de Música, y la adecuación de espacios para prácticas, ensayos, grabaciones y presentaciones, asegurando condiciones dignas y equitativas en los territorios."
        ]
      }
    ] 
  },
  { 
    id: "03", 
    title: "GOBERNANZA MUSICAL E INTEGRACIÓN CULTURAL E INTERSECTORIAL", 
    axisExplain: [
      "Este eje promueve el fortalecimiento de los mecanismos de organización, participación y articulación del sector musical con y desde el Estado.",
      "Consolidando una gobernanza efectiva que garantice la sostenibilidad cultural del ecosistema musical en Colombia."
    ],
    purpose: "Consolidar una gobernanza sólida y una articulación intersectorial amplia que potencie la capacidad de la música para incidir en la transformación social, la construcción de paz y la reducción de desigualdades.",
    videoImg: "https://images.unsplash.com/photo-1774558396253-be05d7a37d82?q=80&w=1470&auto=format&fit=crop", 
    components: [
      { 
        id: "c3-1", 
        slug: "participacion-y-articulacion",
        name: "Participación ciudadana, intersectorialidad y articulación territorial", 
        details: "Fortalece la participación activa del sector musical en las políticas públicas.",
        fullText: [
          "Este componente busca fortalecer la participación activa del sector musical en la formulación y ejecución de políticas públicas, promoviendo espacios de diálogo, concertación y decisión colectiva como los Comités Departamentales de Música y los Planes Departamentales de Desarrollo Musical.",
          "Además, impulsa la creación de asociaciones, redes y plataformas colaborativas que consolidan el tejido organizativo local y dinamizan los procesos musicales en los territorios. También fomenta la articulación con otros sectores estratégicos como educación, economía, medio ambiente y tecnología, promoviendo coordinaciones mixtas entre Estado y sociedad civil para garantizar políticas inclusivas, sostenibles y que reflejen las necesidades y aspiraciones de las comunidades."
        ]
      }, 
      { 
        id: "c3-2", 
        slug: "sostenibilidad-y-economias-musicales",
        name: "Sostenibilidad, condiciones laborales y economías de la música", 
        details: "Se centra en mejorar las condiciones laborales y económicas de los actores del ecosistema.",
        fullText: [
          "Este componente se centra en mejorar las condiciones laborales y económicas de los actores del ecosistema musical, promoviendo la formalización, la seguridad social, la dignificación del trabajo y el fortalecimiento de capacidades en gestión, producción y emprendimiento.",
          "Busca consolidar economías musicales territoriales a través de procesos de producción, circulación y comercialización que respondan a las dinámicas locales, fomentando modelos sostenibles que involucren al Estado, la sociedad civil y la empresa privada. Su objetivo es garantizar que la música sea cultural, social y económicamente viable, contribuyendo a la autonomía del sector, la reducción de desigualdades y la resiliencia de un ecosistema diverso."
        ]
      }
    ] 
  }
];
