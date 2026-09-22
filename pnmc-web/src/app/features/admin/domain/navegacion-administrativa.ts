export type IdSeccionAdministrativa =
  | 'monitor'
  | 'solicitudes'
  | 'calidad'
  | 'ecosistema'
  | 'mercados'
  | 'organizaciones'
  | 'catalogo-editorial'
  | 'agenda'
  | 'noticias'
  | 'banco-de-archivos'
  | 'categorias'
  | 'gestion-sitio'
  | 'boletin'
  | 'analisis'
  | 'auditoria'
  | 'usuarios'
  | 'sistema';

export interface SeccionAdministrativa {
  id: IdSeccionAdministrativa;
  ruta: string;
  titulo: string;
  descripcion: string;
  icono?: string;
  rolesPermitidos?: readonly string[];
  /**
   * Rutas con las que esta sección se publicó antes y que deben seguir abriéndola.
   *
   * NO ES DECORATIVO: cuando una ruta desaparece, `seccionAdministrativaPorRuta` no la
   * encuentra y el armazón cae en «Resumen». Es decir, un enlace guardado no daría error
   * —daría OTRA pantalla, en silencio—, que es la forma más cara de romper un enlace.
   */
  rutasAnteriores?: readonly string[];
}

export interface GrupoAdministrativo {
  id: 'bandeja' | 'ecosistema' | 'publicaciones' | 'sitio' | 'gobierno';
  titulo: string;
  secciones: readonly SeccionAdministrativa[];
}

/**
 * La arquitectura visible del Espacio de Gestión Administrativa reorganizada en cinco
 * familias de negocio cultural:
 * 1. Bandeja de Trabajo: Triage y decisiones inmediatas de solicitudes y calidad.
 * 2. Ecosistema Musical: Actores y procesos territoriales (Festivales, Organizaciones).
 * 3. Publicaciones y Mediateca: Catálogo Editorial, Agenda, Noticias, Categorías y Banco de archivos.
 * 4. Sitio Web y Comunicación: CMS de páginas del portal, medios y boletín.
 * 5. Gobierno y Control: Indicadores de análisis, auditoría legal y configuración del sistema.
 */
export const GRUPOS_DE_GESTION_ADMINISTRATIVA: readonly GrupoAdministrativo[] = [
  {
    id: 'bandeja',
    titulo: 'Bandeja de trabajo',
    secciones: [
      {
        id: 'monitor',
        ruta: 'resumen',
        titulo: 'Resumen operativo',
        descripcion: 'Prioridades, alertas inmediatas y pulso general de la gestión.',
        icono: 'LayoutDashboard',
      },
      {
        id: 'solicitudes',
        ruta: 'solicitudes',
        titulo: 'Solicitudes y revisiones',
        // LA DESCRIPCION PROMETIA LO QUE LA PANTALLA NO HACE. Decía «expande una fila para
        // revisarla y decidir sin salir de aquí», y medido: nueve de las diez filas de la bandeja no
        // ofrecen ninguna decisión al desplegarse, porque un registro inicial de festival se revisa
        // en su ficha completa. Ahora lo dice como es.
        descripcion: 'Todo lo que espera una decisión: registros nuevos, cambios, eliminaciones, reclamaciones, vinculaciones, posibles duplicados y alertas de calidad. Unos se deciden aquí mismo; los registros completos se revisan en su ficha.',
        icono: 'Inbox',
        // UNA SOLA BANDEJA, Y POR ESO ESTA SECCION ABSORBE DOS RUTAS ANTIGUAS.
        //
        // «Calidad y coincidencias» -antes «Gobernanza»- era una segunda seccion con cinco
        // pestañas, y tres de ellas -Eliminaciones, Reclamaciones, Vinculaciones- listaban
        // EXACTAMENTE los mismos trámites que esta bandeja, leídos de los mismos dos endpoints,
        // pero con otra interfaz de decisión. Dos caminos al mismo asunto que podían divergir en
        // cuanto uno cambiara. Las otras dos -Duplicados y Alertas- son hallazgos del sistema y
        // entraron aquí como dos posiciones más del filtro.
        //
        // Lo decidió la dirección de producto, tras preguntar si el
        // apartado debía estar aparte o podía ser una categoría más de la vista general.
        rutasAnteriores: ['calidad', 'gobernanza'],
      },
    ],
  },
  {
    id: 'ecosistema',
    titulo: 'Ecosistema musical',
    secciones: [
      {
        id: 'ecosistema',
        ruta: 'festivales',
        titulo: 'Festivales y ediciones',
        descripcion: 'Catálogo de Festivales, historial de versiones e importación asistida.',
        icono: 'Music',
        rutasAnteriores: ['ecosistema'],
      },
      {
        id: 'mercados',
        ruta: 'mercados',
        titulo: 'Mercados musicales',
        descripcion: 'Mercados del ecosistema, sus ediciones y su relación con festivales.',
        icono: 'Store',
      },
      {
        id: 'organizaciones',
        ruta: 'organizaciones',
        titulo: 'Organizaciones y responsables',
        descripcion: 'Directorio de entidades gestoras, directores, personerías y relaciones.',
        icono: 'Building2',
      },
    ],
  },
  {
    id: 'publicaciones',
    titulo: 'Publicaciones y mediateca',
    secciones: [
      {
        id: 'catalogo-editorial',
        ruta: 'catalogo-editorial',
        titulo: 'Catálogo Editorial',
        // LA DESCRIPCION DICE EL CRITERIO, NO REPITE EL TITULO. Decía «Fichas bibliográficas,
        // partituras, métodos pedagógicos…», que es lo mismo que ya dice el nombre de la sección y no
        // ayuda a decidir nada. Esta frase estaba dentro del panel, en un segundo encabezado que
        // repetía el título; al retirar ese encabezado se conserva lo único que el armazón no sabía.
        descripcion: 'Catalogar dice si la ficha está bien hecha. Publicar dice si se ve. Son dos decisiones distintas: una ficha validada puede quedarse sin publicar.',
        icono: 'BookOpen',
      },
      {
        id: 'agenda',
        ruta: 'agenda',
        titulo: 'Agenda y eventos',
        descripcion: 'Convocatorias, eventos y programación de actividades en territorio.',
        icono: 'Calendar',
      },
      {
        id: 'noticias',
        ruta: 'noticias',
        titulo: 'Noticias y prensa',
        descripcion: 'Publicaciones informativas, notas de prensa y comunicados oficiales del PNMC.',
        icono: 'Newspaper',
      },
      {
        id: 'categorias',
        ruta: 'categorias',
        titulo: 'Categorías y proyectos',
        descripcion: 'Vocabulario común de Catálogo Editorial, Agenda y Noticias, y las iniciativas del Programa con las que se enlaza el contenido.',
        icono: 'Tags',
      },
      {
        // EL BANCO ES EL ALMACEN DE LO QUE SE ADJUNTA desde una ficha —el afiche de una Edición, la
        // foto de perfil de una organización, la imagen de una noticia—, y no el archivo
        // fotográfico del Programa. Ese archivo —la Galería— NO ESTA EN LA BARRA: se retiró el 15
        // de septiembre de 2026 porque no tenía panel ni circuito, y una sección que se ofrece y no
        // abre nada es un placeholder. Volverá como bloque propio cuando se defina qué es; su
        // módulo sigue en el catálogo de permisos del servidor para ese día.
        id: 'banco-de-archivos',
        ruta: 'banco-de-archivos',
        titulo: 'Banco de archivos',
        descripcion: 'Las imágenes adjuntas a fichas, con su procedencia, dónde se usan y cuáles no usa nadie.',
        icono: 'Files',
      },
    ],
  },
  {
    id: 'sitio',
    titulo: 'Sitio web y comunicación',
    secciones: [
      {
        id: 'gestion-sitio',
        ruta: 'gestion-del-sitio',
        titulo: 'Páginas y bloques (CMS)',
        descripcion: 'Gestión unificada de textos, imágenes, equipo y diseño del portal ciudadano.',
        icono: 'Globe',
        rutasAnteriores: ['sitio-web'],
      },
      {
        id: 'boletin',
        ruta: 'boletin',
        titulo: 'Boletín informativo',
        descripcion: 'Consulta, bajas y exportación autorizada de suscripciones ciudadanas.',
        icono: 'Mail',
      },
    ],
  },
  {
    id: 'gobierno',
    titulo: 'Gobierno y control',
    secciones: [
      {
        id: 'analisis',
        ruta: 'analisis',
        titulo: 'Análisis y consultas',
        descripcion: 'Indicadores verificables y consultas agregadas sobre la operación vigente.',
        icono: 'BarChart3',
      },
      {
        id: 'auditoria',
        ruta: 'auditoria',
        titulo: 'Auditoría y trazabilidad',
        descripcion: 'Trazabilidad legal paginada de todas las actuaciones administrativas.',
        icono: 'FileText',
      },
      {
        id: 'usuarios',
        ruta: 'usuarios',
        titulo: 'Usuarios y roles',
        descripcion: 'Cuentas institucionales, roles y control de acceso del Ministerio.',
        icono: 'Users',
        rolesPermitidos: ['webmaster'],
      },
      {
        id: 'sistema',
        ruta: 'sistema',
        titulo: 'Salud del sistema',
        descripcion: 'Salud de la API, capacidades informadas y telemetría técnica.',
        icono: 'Shield',
        rolesPermitidos: ['webmaster'],
      },
    ],
  },
] as const;

export const SECCIONES_DE_GESTION_ADMINISTRATIVA = GRUPOS_DE_GESTION_ADMINISTRATIVA.flatMap(
  grupo => grupo.secciones,
);

export function seccionAdministrativaPorId(id: string): SeccionAdministrativa | undefined {
  return SECCIONES_DE_GESTION_ADMINISTRATIVA.find(seccion => seccion.id === id);
}

/** La familia a la que pertenece una sección: es lo que la cabecera de página enseña como miga. */
export function grupoAdministrativoDeSeccion(id: string): GrupoAdministrativo | undefined {
  return GRUPOS_DE_GESTION_ADMINISTRATIVA.find(grupo => grupo.secciones.some(seccion => seccion.id === id));
}

export function seccionAdministrativaPorRuta(ruta: string): SeccionAdministrativa | undefined {
  return SECCIONES_DE_GESTION_ADMINISTRATIVA.find(
    seccion => seccion.ruta === ruta || seccion.rutasAnteriores?.includes(ruta),
  );
}

export function gruposAdministrativosVisibles(roles: readonly string[]): GrupoAdministrativo[] {
  return GRUPOS_DE_GESTION_ADMINISTRATIVA
    .map(grupo => ({
      ...grupo,
      secciones: grupo.secciones.filter(
        seccion => !seccion.rolesPermitidos || roles.some(rol => seccion.rolesPermitidos?.includes(rol)),
      ),
    }))
    .filter(grupo => grupo.secciones.length > 0);
}
