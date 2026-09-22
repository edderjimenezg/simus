let runtimeDivipolaByDepartment: Record<string, any> = {};
let runtimeDepartmentNameByCode: Record<string, string> = {};
let runtimeDepartmentCodeByName: Record<string, string> = {};

export interface DepartmentSummary {
  festivalCount: number;
  schoolCount: number;
  marketCount: number;
  redesCount: number;
  lutierCount: number;
  totalStudents: number;
  totalTeachers: number;
  totalInstruments: number;
  totalGroups: number;
  totalMarketProjects: number;
  totalMarketBuyers: number;
  totalRecords: number;
}

export interface ChoroplethStep {
  min: number;
  max: number;
  color: string;
  opacity: number;
  label: string;
}

const getRuntimeDivipolaByDepartment = (): Record<string, any> => runtimeDivipolaByDepartment;

const setRuntimeDivipolaByDepartment = (nextValue: any): void => {
  runtimeDivipolaByDepartment = nextValue && typeof nextValue === 'object'
    ? nextValue
    : {};
};

const normalizeDepartmentCode = (value: any = ''): string => {
  const digits = String(value ?? '').replace(/\D+/g, '');
  if (!digits) return '';
  return digits.padStart(2, '0').slice(-2);
};

const normalizeMunicipalityCode = (value: any = ''): string => {
  const digits = String(value ?? '').replace(/\D+/g, '');
  if (!digits) return '';
  return digits.padStart(5, '0').slice(-5);
};

const setRuntimeDepartmentCatalog = (features: any[] = []): void => {
  const departmentNameByCode: Record<string, string> = {};
  const departmentCodeByNormalizedName: Record<string, string> = {};

  (Array.isArray(features) ? features : []).forEach((feature) => {
    const properties = feature?.properties || {};
    const departmentCode = normalizeDepartmentCode(
      properties.departmentCode || properties.dpto_ccdgo
    );
    const departmentName = String(
      properties.departmentName || properties.dpto_cnmbr || ''
    ).trim();

    if (!departmentCode || !departmentName) return;

    departmentNameByCode[departmentCode] = departmentName;
    departmentCodeByNormalizedName[normalizeDepartmentName(departmentName)] = departmentCode;
  });

  Object.keys(getRuntimeDivipolaByDepartment()).forEach((departmentName) => {
    const normalizedName = normalizeDepartmentName(departmentName);
    const knownCode = departmentCodeByNormalizedName[normalizedName];
    if (!knownCode) return;
    if (!departmentNameByCode[knownCode]) {
      departmentNameByCode[knownCode] = departmentName;
    }
  });

  runtimeDepartmentNameByCode = departmentNameByCode;
  // El camino inverso se calculaba aqui y se tiraba. Hace falta: el desplegable del
  // geovisor trabaja con NOMBRES y el fragmento municipal del API se pide por CODIGO.
  runtimeDepartmentCodeByName = departmentCodeByNormalizedName;
};

const getDepartmentNameByCode = (departmentCode = ''): string => (
  runtimeDepartmentNameByCode[normalizeDepartmentCode(departmentCode)] || ''
);

/**
 * Codigo DIVIPOLA de dos digitos a partir del nombre del departamento.
 *
 * Hace falta porque los dos extremos hablan idiomas distintos: el desplegable del panel
 * y `departamentoElegido()` manejan NOMBRES —«ANTIOQUIA»—, y el fragmento municipal se pide a
 * `GET /api/v1/mapa/cartografia/departamentos/{codigo}/municipios`, que quiere «05». El
 * mapa se queda sin municipios si nadie traduce.
 *
 * Se compara por nombre normalizado, no literal: el catalogo trae «ARCHIPIELAGO DE SAN
 * ANDRES, PROVIDENCIA Y SANTA CATALINA» y en pantalla se escribe de otras maneras.
 */
const getDepartmentCodeByName = (departmentName = ''): string => {
  const normalized = normalizeDepartmentName(departmentName);
  return normalized ? (runtimeDepartmentCodeByName[normalized] || '') : '';
};

const getSortedDepartmentNames = (): string[] => (
  Object.keys(getRuntimeDivipolaByDepartment())
    .sort((left, right) => left.localeCompare(right, 'es-CO'))
);

const GEOJSON_DEPARTMENT_NAME_KEYS = ['departmentName', 'dpto_cnmbr', 'DPTO_CNMBR', 'dpt', 'NOMBRE_DPT', 'name'];
const GEOJSON_DEPARTMENT_CODE_KEYS = ['departmentCode', 'dpto_ccdgo', 'DPTO_CCDGO', 'code'];
const FESTIVAL_COUNTS_CACHE_KEY = 'pnmc-festival-counts-cache-v1';
const SCHOOL_COUNTS_CACHE_KEY = 'pnmc-school-counts-cache-v1';
const MARKET_COUNTS_CACHE_KEY = 'pnmc-market-counts-cache-v1';
const NAVBAR_SCROLL_OFFSET = 112;
const ARCHIPELAGO_NORMALIZED_NAME = 'SAN ANDRES Y PROVIDENCIA';
const ARCHIPELAGO_VISUAL_SCALE = 8.5;
const METRIC_FORMATTER = new Intl.NumberFormat('es-CO');
const DEPARTMENT_NAME_ALIASES: Record<string, string> = {
  'ARCHIPIELAGO DE SAN ANDRES PROVIDENCIA Y SANTA CATALINA': 'SAN ANDRES Y PROVIDENCIA',
  'SAN ANDRES PROVIDENCIA Y SANTA CATALINA': 'SAN ANDRES Y PROVIDENCIA',
  'SAN ANDRES Y PROVIDENCIA Y SANTA CATALINA': 'SAN ANDRES Y PROVIDENCIA',
  'SANTAFE DE BOGOTA': 'BOGOTA',
  'BOGOTA D C': 'BOGOTA',
  'BOGOTA D.C': 'BOGOTA',
  'BOGOTA, D.C.': 'BOGOTA',
  'DISTRITO CAPITAL DE BOGOTA': 'BOGOTA',
  'CUNDINAMRCA': 'CUNDINAMARCA',
};

/**
 * Rampa de color por capa: cinco escalones, de menos a mas procesos.
 *
 * Cada rampa es de UN SOLO TONO y baja de luminosidad monotonamente —la unica
 * forma de que "mas oscuro" signifique "mas" sin que haya que consultar la
 * leyenda—. El ultimo escalon es exactamente el acento de la capa en
 * `LAYER_ACCENTS`, asi que el mapa a tope de densidad y el punto del panel son
 * el mismo color.
 *
 * Los seis acentos se sustituyeron el 25 ago 2026 porque los anteriores no se
 * distinguian entre si. Medido con el validador de paleta sobre TODOS los pares
 * —lo que exige un mapa, donde cualquier capa puede compararse con cualquier
 * otra—, la paleta vieja daba: Redes contra General ΔE 1.1 simulando
 * deuteranopia, y Lutieres contra General ΔE 4.9 EN VISION NORMAL. Es decir que
 * cambiar de capa no cambiaba visiblemente el mapa. Los nuevos dan ΔE 11.5 con
 * daltonismo y 16.2 en vision normal, con todos por encima de 3:1 de contraste.
 *
 * Cada capa conserva su familia de tono —verde sigue verde, ambar sigue ambar—
 * para que quien ya usaba el geovisor no tenga que reaprender nada; lo que
 * cambia es el escalon, no el color que la capa "es".
 *
 *
 * POR QUE LAS RAMPAS SON ESTRECHAS. Antes iban de casi blanco al acento, y por
 * eso los seis primeros escalones eran el mismo color: ΔE 2.5 en vision normal
 * entre la capa mas clara y la siguiente. Como casi todos los departamentos
 * caen en el primer o segundo escalon —hay entre 1 y 5 procesos por
 * departamento—, el mapa se veia IGUAL en las seis capas. Cambiar de capa no
 * cambiaba nada.
 *
 * Ahora cada rampa arranca 0,20 de luminosidad por encima de su acento, no en
 * blanco. Comprobado: entre capas, el peor escalon pasa de ΔE 2.5 a 12.5 en vision
 * normal y de 0.7 a 8.2 simulando daltonismo. Y la lectura de densidad NO
 * empeora: escalones contiguos ΔE 5.0 frente a los 3.8-6.0 de antes, y del
 * primero al ultimo 20.5.
 *
 * El escalon mas claro baja de 3:1 de contraste contra el fondo. Es a
 * proposito y esta permitido en una rampa secuencial: ese escalon significa
 * "casi nada" y puede acercarse a la superficie. Lo que no puede es acercarse
 * al escalon de otra capa, y eso es lo que se acaba de arreglar.
 *
 * Si se toca un acento hay que regenerar aqui los cinco escalones y volver a
 * pasar el validador. Un acento nuevo con la rampa vieja rompe las dos cosas a
 * la vez: la lectura de densidad y la correspondencia con el panel.
 */
const MAP_LAYER_CHOROPLETH_STEPS: Record<string, ChoroplethStep[]> = {
  General: [
    { min: 1, max: 5, color: '#799f6c', opacity: 0.6, label: '1 a 5' },
    { min: 6, max: 10, color: '#659156', opacity: 0.65, label: '6 a 10' },
    { min: 11, max: 15, color: '#528440', opacity: 0.7, label: '11 a 15' },
    { min: 16, max: 20, color: '#3e7628', opacity: 0.75, label: '16 a 20' },
    { min: 21, max: Infinity, color: '#296904', opacity: 0.8, label: '20 o más' },
  ],
  Festivales: [
    { min: 1, max: 5, color: '#aa7fbd', opacity: 0.6, label: '1 a 5' },
    { min: 6, max: 10, color: '#9e6cb4', opacity: 0.65, label: '6 a 10' },
    { min: 11, max: 15, color: '#9259aa', opacity: 0.7, label: '11 a 15' },
    { min: 16, max: 20, color: '#8645a1', opacity: 0.75, label: '16 a 20' },
    { min: 21, max: Infinity, color: '#7a2f97', opacity: 0.8, label: '20 o más' },
  ],
  'Escuelas de Música': [
    { min: 1, max: 5, color: '#9ea9ff', opacity: 0.6, label: '1 a 5' },
    { min: 6, max: 10, color: '#8d97fb', opacity: 0.65, label: '6 a 10' },
    { min: 11, max: 15, color: '#7c84f6', opacity: 0.7, label: '11 a 15' },
    { min: 16, max: 20, color: '#6c71f1', opacity: 0.75, label: '16 a 20' },
    { min: 21, max: Infinity, color: '#5e5ceb', opacity: 0.8, label: '20 o más' },
  ],
  'Mercados Musicales': [
    { min: 1, max: 5, color: '#f5c895', opacity: 0.6, label: '1 a 5' },
    { min: 6, max: 10, color: '#eab67a', opacity: 0.65, label: '6 a 10' },
    { min: 11, max: 15, color: '#dfa45e', opacity: 0.7, label: '11 a 15' },
    { min: 16, max: 20, color: '#d4923f', opacity: 0.75, label: '16 a 20' },
    { min: 21, max: Infinity, color: '#c88011', opacity: 0.8, label: '20 o más' },
  ],
  'Redes de Documentación': [
    { min: 1, max: 2, color: '#f896ab', opacity: 0.6, label: '1 a 2' },
    { min: 3, max: 5, color: '#f07f9a', opacity: 0.65, label: '3 a 5' },
    { min: 6, max: 10, color: '#e76889', opacity: 0.7, label: '6 a 10' },
    { min: 11, max: 15, color: '#dd4e78', opacity: 0.75, label: '11 a 15' },
    { min: 16, max: Infinity, color: '#d33068', opacity: 0.8, label: '16 o más' },
  ],
  'Lutieres': [
    { min: 1, max: 2, color: '#98d7e5', opacity: 0.6, label: '1 a 2' },
    { min: 3, max: 5, color: '#7ec8d9', opacity: 0.65, label: '3 a 5' },
    { min: 6, max: 10, color: '#63bacd', opacity: 0.7, label: '6 a 10' },
    { min: 11, max: 15, color: '#43abc1', opacity: 0.75, label: '11 a 15' },
    { min: 16, max: Infinity, color: '#109db5', opacity: 0.8, label: '16 o más' },
  ],
};

const EMPTY_DEPARTMENT_SUMMARY: DepartmentSummary = {
  festivalCount: 0,
  schoolCount: 0,
  marketCount: 0,
  redesCount: 0,
  lutierCount: 0,
  totalStudents: 0,
  totalTeachers: 0,
  totalInstruments: 0,
  totalGroups: 0,
  totalMarketProjects: 0,
  totalMarketBuyers: 0,
  totalRecords: 0,
};

const SCHOOL_FIELD_MAP: Record<string, string[]> = {
  id: ['ID escuela'],
  referenceYear: ['Año de referencia', 'Ano de referencia'],
  status: ['Estado'],
  departmentCode: ['departmentCode', 'Codigo Departamento', 'Código Departamento', 'Departamento Código Divipola'],
  municipalityCode: ['municipalityCode', 'Código Divipola', 'Codigo Divipola'],
  department: ['Departamento'],
  municipality: ['Municipio'],
  divipola: ['Código Divipola', 'Codigo Divipola'],
  name: ['Nombre de la escuela'],
  address: ['Dirección de la escuela', 'Direccion de la escuela'],
  schoolType: ['Tipo de escuela'],
  category: ['Categoría', 'Categoria'],
  categorizationDate: ['Fecha de categorización', 'Fecha de categorizacion'],
  legalCreation: ['Escuela creada legalmente'],
  legalPersonhood: ['Tiene personería jurídica', 'Tiene personeria jurídica', 'Tiene personeria juridica'],
  nature: ['Naturaleza'],
  dependsOnEntity: ['Depende de otra entidad'],
  parentEntity: ['Entidad de la que depende'],
  directorName: ['Nombre del director o coordinador'],
  directorContact: ['Celular o contacto del director'],
  contactEmail: ['Correo institucional o de contacto'],
  workSite: ['Sede de trabajo'],
  internetAccess: ['Tiene acceso a internet'],
  instruments: ['Cantidad total de instrumentos'],
  teachers: ['Cantidad total de docentes vinculados'],
  students: ['Cantidad total de alumnos'],
  groups: ['Cantidad de agrupaciones vigentes'],
  practices: ['Prácticas musicales', 'Practicas musicales'],
  workshops: ['Talleres independientes'],
  communityOrganization: ['Cuenta con organización comunitaria', 'Cuenta con organizacion comunitaria'],
  linkedSonorousTerritories: ['Territorios sonoros', 'Territorios Sonoros', 'linkedSonorousTerritories'],
  observations: ['Observaciones'],
};

const SCHOOL_PUBLICATION_POLICY = {
  public: [
    'status',
    'department',
    'municipality',
    'divipola',
    'name',
    'schoolType',
    'category',
    'legalCreation',
    'legalPersonhood',
    'nature',
    'dependsOnEntity',
    'parentEntity',
    'workSite',
    'internetAccess',
    'instruments',
    'teachers',
    'students',
    'groups',
    'practices',
    'workshops',
    'communityOrganization',
  ],
  private: [
    'id',
    'referenceYear',
    'address',
    'categorizationDate',
    'directorName',
    'directorContact',
    'contactEmail',
    'observations',
  ],
};

const MARKET_FIELD_MAP: Record<string, string[]> = {
  name: ['\uFEFFMarca temporal', 'Marca temporal', 'Nombre del mercado', 'Mercado', 'name', 'nombre'],
  departmentCode: ['departmentCode', 'Codigo Departamento', 'Código Departamento'],
  municipalityCode: ['municipalityCode', 'Código Divipola', 'Codigo Divipola'],
  department: ['Departamento donde se realiza', 'Departamento', 'departamento'],
  municipality: ['Ciudad donde se realiza', 'Ciudad', 'Municipio', 'municipio'],
  createdYear: ['Año de creación del mercado ', 'Año de creación del mercado', 'Ano de creación del mercado', 'Ano de creacion del mercado'],
  versions: ['Número de versiones realizadas ', 'Número de versiones realizadas', 'Numero de versiones realizadas'],
  periodicity: ['Periodicidad del mercado ', 'Periodicidad del mercado'],
  editionDate2026: ['Fecha de realización del mercado para el 2026', 'Fecha de realizacion del mercado para el 2026'],
  responsibleEntity: ['¿Cuál es la entidad, organización o corporación responsable del mercado? ', '¿Cuál es la entidad, organización o corporación responsable del mercado?'],
  organizationType: ['Tipo de organización', 'Tipo de organizacion'],
  legalFormalStatus: ['¿Esta entidad u organización cuenta con constitución legal formal? ', '¿Esta entidad u organización cuenta con constitución legal formal?'],
  legalEntityNit: ['Si le respuesta anterior fue Sí, señale: Nombre de la entidad u organización y NIT'],
  linkedFestival: ['¿El mercado se realiza en el marco de algún Festival?', '¿El mercado se realiza en el marco de algún Festival? '],
  festivalName: ['Si le respuesta anterior fue Sí, señale:  \nNombre del festival o evento ', 'Si le respuesta anterior fue Sí, señale: Nombre del festival o evento'],
  festivalVersions: ['¿Cuántas versiones lleva el festival? ', '¿Cuántas versiones lleva el festival?'],
  festivalDates: ['Fechas aproximadas del festival cada año'],
  fundingSources: ['¿Cuáles son las principales fuentes de financiación del mercado o festival?\n(marque las que correspondan) ', '¿Cuáles son las principales fuentes de financiación del mercado o festival?'],
  publicBudgetShare: ['Aproximadamente, ¿Qué porcentaje del presupuesto proviene de recursos públicos? ', 'Aproximadamente, ¿Qué porcentaje del presupuesto proviene de recursos públicos?'],
  curationModel: ['¿Cómo se define la curaduría o selección de artistas y proyectos musicales del mercado y/o festival? ', '¿Cómo se define la curaduría o selección de artistas y proyectos musicales del mercado y/o festival?'],
  openCall: ['¿El mercado cuenta con convocatoria abierta para artistas o proyectos musicales? ', '¿El mercado cuenta con convocatoria abierta para artistas o proyectos musicales?'],
  averageProjects: ['¿Cuántos proyectos musicales participan en promedio en cada edición? ', '¿Cuántos proyectos musicales participan en promedio en cada edición?'],
  averageBuyers: ['¿Cuántos bookers, programadores o compradores participan en promedio en cada edición del mercado? ', '¿Cuántos bookers, programadores o compradores participan en promedio en cada edición del mercado?'],
  buyerSpaces: ['¿De qué tipo de espacios provienen principalmente estos agentes? ', '¿De qué tipo de espacios provienen principalmente estos agentes?'],
  buyerStrategies: ['¿Qué estrategias utiliza el mercado para asegurar la participación de bookers o compradores?  (ejemplo: invitaciones directas, alianzas, bolsas de viaje, acuerdos con festivales, etc.) ', '¿Qué estrategias utiliza el mercado para asegurar la participación de bookers o compradores?'],
  preAgreements: ['¿Los bookers invitados participan con compromisos de programación o compra previamente establecidos? ', '¿Los bookers invitados participan con compromisos de programación o compra previamente establecidos?'],
  circulationMechanisms: ['¿Qué tipo de compromisos o mecanismos se utilizan para promover la compra o circulación de artistas? ', '¿Qué tipo de compromisos o mecanismos se utilizan para promover la compra o circulación de artistas?'],
  publicArticulations: ['¿Qué articulaciones ha tenido su mercado con entidades públicas?\n(Ejemplo: ministerios, gobernaciones, alcaldías, institutos culturales) ', '¿Qué articulaciones ha tenido su mercado con entidades públicas?'],
  partnerNetworks: ['¿Con qué otras organizaciones o redes culturales trabaja actualmente? ', '¿Con qué otras organizaciones o redes culturales trabaja actualmente?'],
  pnmcConnections: ['¿Los Encuentros de Mercados realizados en marzo y noviembre de 2025, en el marco de la estrategia de articulación del Plan Nacional de Música para la Convivencia (PNMC), dieron lugar a nuevos vínculos o colaboraciones con otros mercados, circuitos o...'],
  pnmcConnectionsDetail: ['Si en la pregunta anterior respondió “Sí”, por favor describa el vínculo o colaboración generada, indicando: ¿con qué mercado, festival o circuito se estableció la articulación?, ¿qué artistas o proyectos musicales estuvieron involucrados?, ¿en qué...'],
  collaborationPotential: ['  ¿Qué tipo de acciones o colaboraciones considera posibles desarrollar con otros mercados de su territorio o región?  ', '¿Qué tipo de acciones o colaboraciones considera posibles desarrollar con otros mercados de su territorio o región?'],
  territorialImpact: ['¿Cuáles considera que son los principales aportes de su mercado a la circulación musical en su territorio? ', '¿Cuáles considera que son los principales aportes de su mercado a la circulación musical en su territorio?'],
  practices: ['Prácticas musicales', 'Practicas musicales'],
  linkedSonorousTerritories: ['Territorios sonoros', 'Territorios Sonoros'],
  websiteUrl: ['sitio_web', 'Sitio Web', 'websiteUrl', 'website', 'link'],
};

const MARKET_PUBLICATION_POLICY = {
  public: [
    'name',
    'department',
    'municipality',
    'createdYear',
    'versions',
    'periodicity',
    'editionDate2026',
    'responsibleEntity',
    'organizationType',
    'legalFormalStatus',
    'linkedFestival',
    'festivalName',
    'festivalVersions',
    'festivalDates',
    'fundingSources',
    'publicBudgetShare',
    'curationModel',
    'openCall',
    'averageProjects',
    'averageBuyers',
    'buyerSpaces',
    'buyerStrategies',
    'preAgreements',
    'circulationMechanisms',
    'publicArticulations',
    'partnerNetworks',
    'pnmcConnections',
    'pnmcConnectionsDetail',
    'collaborationPotential',
    'territorialImpact',
  ],
  private: [
    'legalEntityNit',
  ],
};

/**
 * Con qué se juntan las clasificaciones de un registro cuando declara varias.
 *
 * <b>NO PUEDE SER LA COMA, Y ESO COSTO UN DEFECTO QUE SOLO SE VIO CON DATOS REALES.</b> El servicio
 * del mapa unía los territorios sonoros con `', '` y el geovisor los volvía a separar por `,`. Con
 * tres registros de prueba de nombre corto funcionaba; con el catálogo real del Plan, no: siete de
 * sus catorce territorios llevan coma en el nombre —«Cantos, Pitos y Tambores», «Flautas, Cuerdas y
 * Tambores Sureños», «Músicas Urbanas, Alternativas e Independientes - MUAI»— y cada uno se partía
 * en dos grupos inventados. El lente contaba dieciocho territorios sobre un catálogo de catorce, y
 * ninguno de los partidos existía.
 *
 * <b>SE USA EL SEPARADOR DE UNIDAD (U+001F), que es para esto.</b> No es un carácter que alguien
 * pueda escribir en el nombre de un territorio sonoro, así que la ambigüedad no vuelve por mucho
 * que crezca el catálogo. Donde se enseña a una persona se sustituye por « · », que es decisión de
 * presentación y no de dato.
 */
const SEPARADOR_DE_CLASIFICACION = '\u001F';

/** Las clasificaciones de un registro, ya separadas. */
const separarClasificacion = (valor: string | null | undefined): string[] =>
  (valor ?? '').split(SEPARADOR_DE_CLASIFICACION).map(item => item.trim()).filter(Boolean);

/** Las clasificaciones tal como se enseñan en una ficha. */
const clasificacionLegible = (valor: string | null | undefined): string =>
  separarClasificacion(valor).join(' · ');

/**
 * El polígono departamental cuando el mapa NO codifica nada en el área.
 *
 * <b>ES REFERENCIA, NO DATO.</b> En los modos de punto el color del área tiene que callarse para
 * que hable el símbolo; pero el contorno debe seguir ahí, porque un símbolo sin territorio debajo
 * no se puede situar. Relleno casi transparente y borde visible: se ve dónde cae cada punto y no
 * compite con él.
 */
const FONDO_NEUTRO_DEL_DEPARTAMENTO = {
  fillColor: '#f1f5f9',
  fillOpacity: 0.55,
  color: '#cbd5e1',
  weight: 1,
  opacity: 1,
};

const normalizeDepartmentName = (str: string): string => {
  const normalized = str?.toUpperCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\./g, ' ')
    .replace(/,/g, ' ')
    .replace(/\s+D\s+C\s*/g, ' ')
    .replace(/\s+/g, ' ')
    .trim() || '';

  return DEPARTMENT_NAME_ALIASES[normalized] || normalized;
};

const normalizeMunicipalityName = (str = ''): string => String(str || '')
  .toUpperCase()
  .normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '')
  .replace(/\./g, ' ')
  .replace(/,/g, ' ')
  .replace(/\s+/g, ' ')
  .trim();

const sortUniqueByLocale = (values: string[] = []): string[] => (
  [...new Set(
    (Array.isArray(values) ? values : [])
      .map((value) => String(value || '').trim())
      .filter(Boolean)
  )].sort((left, right) => left.localeCompare(right, 'es-CO'))
);

const getDepartmentDisplayName = (value = ''): string => {
  const departmentCode = normalizeDepartmentCode(value);
  if (departmentCode) {
    const fromCode = getDepartmentNameByCode(departmentCode);
    if (fromCode) return fromCode;
  }

  const normalized = normalizeDepartmentName(value);

  if (!normalized) return 'Sin departamento';
  if (normalized === ARCHIPELAGO_NORMALIZED_NAME) {
    return 'Archipiélago de San Andrés, Providencia y Santa Catalina';
  }
  if (normalized === 'BOGOTA') {
    return 'Bogotá D.C.';
  }

  return Object.keys(getRuntimeDivipolaByDepartment()).find(
    (departmentName) => normalizeDepartmentName(departmentName) === normalized
  ) || value;
};

/**
 * El nombre con el que un departamento se ROTULA SOBRE EL MAPA, que no siempre es el
 * mismo con el que se nombra en un desplegable.
 *
 * POR QUE HACE FALTA. `getDepartmentDisplayName` devuelve el nombre completo, y para el
 * archipielago eso es «Archipiélago de San Andrés, Providencia y Santa Catalina»:
 * cincuenta y seis caracteres. Medido en pantalla con la
 * tipografia del rotulo, ocupaba 311 px, casi el triple que el siguiente mas ancho
 * —«NORTE DE SANTANDER», 112 px— y cruzaba media cuenca del Caribe sobre unas islas que
 * a escala nacional miden pocos pixeles.
 *
 * La forma corta no se inventa aqui: es la misma que `getDepartmentSelectionValue`
 * devuelve para este departamento y la misma que el geovisor le pasa al clic. Asi el
 * rotulo dice exactamente lo que se selecciona al pulsarlo.
 *
 * Para los demas departamentos es `getDepartmentDisplayName` sin mas: ninguno tiene un
 * nombre que estorbe.
 */
const getDepartmentLabelName = (value = ''): string => {
  const normalized = normalizeDepartmentName(value);
  if (normalized === ARCHIPELAGO_NORMALIZED_NAME) {
    return 'San Andrés y Providencia';
  }
  return getDepartmentDisplayName(value);
};

// ---------------------------------------------------------------------------
// DONDE SE PUEDE AFIRMAR QUE ESTA UN PROCESO (29 de agosto de 2026)
//
// EL DEFECTO QUE ESTO RETIRA. `thematicPoints`, en el componente, no usaba ninguna
// posicion: la calculaba. Tomaba el centroide del DEPARTAMENTO y le sumaba un
// desplazamiento en espiral cuyo angulo y radio salian del INDICE DEL REGISTRO EN EL
// ARRAY:
//
//     const angle = (index * 0.72) % (2 * Math.PI);
//     const radius = 0.08 + ((index * 0.03) % 0.14);
//     lat = centroid[0] + Math.sin(angle) * radius;
//
// Reordenar los datos movia los puntos por el mapa. El mapa afirmaba que un proceso
// estaba en un sitio donde no esta, que es el caso mas literal de dato falso.
//
// LO QUE SE MIDIO ANTES DE DISENAR ESTO, contra el API en 8081:
//
//   · 155 procesos publicados, repartidos en 33 municipios.
//   · Los 155 traen `municipalityCode`.
//   · 60 traen ademas `latitude`/`longitude`, y LOS 60 SON EXACTAMENTE el punto de
//     referencia de su municipio, con tolerancia 1e-5. Ni uno tiene posicion propia.
//   · `/api/v1/publico/divipola` es anonima y devuelve los 1.122 municipios con
//     coordenada.
//
// De ahi que la resolucion honesta de hoy sea EL MUNICIPIO. `punto_propio` existe para
// el dia que un registro traiga su sitio de verdad; hoy devuelve cero, que es correcto.
// ---------------------------------------------------------------------------

/** Hasta donde llega la certeza sobre la posicion de un proceso. */
type NivelDeUbicacion =
  /** El registro trae una coordenada que se aparta del punto de su municipio. */
  | 'punto_propio'
  /** Se usa el punto de referencia del municipio: ubica el municipio, no el proceso. */
  | 'punto_municipal'
  /** Hay codigo de municipio, pero la tabla de DIVIPOLA no tiene punto para el. */
  | 'municipio_sin_punto'
  /** El registro no dice en que municipio esta. */
  | 'sin_codigo'
  /** No se pudo cargar la tabla de municipios: no es culpa del registro. */
  | 'catalogo_no_disponible';

interface PuntoMunicipal {
  readonly lat: number;
  readonly lng: number;
  readonly nombre: string;
  readonly codigoDepartamento?: string;
}

/** Lo minimo que un registro tiene que traer para intentar situarlo. */
interface RegistroSituable {
  readonly municipalityCode?: string | null;
  readonly latitude?: number | null;
  readonly longitude?: number | null;
}

interface UbicacionDeRegistro {
  readonly nivel: NivelDeUbicacion;
  readonly lat: number | null;
  readonly lng: number | null;
  readonly codigo: string;
}

interface MunicipioConProcesos<T> {
  readonly codigo: string;
  readonly nombre: string;
  readonly lat: number;
  readonly lng: number;
  readonly total: number;
  readonly fichas: readonly T[];
}

/**
 * Las tres maneras de no poder situar un proceso, CONTADAS POR SEPARADO.
 *
 * Fundirlas en «N sin ubicar» las hace inarreglables: falta el dato en el registro,
 * falta el municipio en la tabla, o no se pudo cargar la tabla. Son tres fallos con
 * tres arreglos distintos y tres responsables distintos.
 */
interface ProcesosNoSituados {
  readonly sinCodigo: number;
  readonly municipioSinPunto: number;
  readonly catalogoNoDisponible: number;
}

/**
 * Cuanto se puede apartar una coordenada del punto de su municipio antes de que
 * merezca llamarse propia. 1e-5 grados son unos 1,1 metros: por debajo de eso es el
 * mismo punto guardado dos veces, no una medicion distinta.
 */
const TOLERANCIA_DE_PUNTO_PROPIO = 1e-5;

const resolverUbicacionDeRegistro = (
  registro: RegistroSituable | null | undefined,
  puntosMunicipales: ReadonlyMap<string, PuntoMunicipal> | null | undefined,
): UbicacionDeRegistro => {
  const codigo = normalizeMunicipalityCode(registro?.municipalityCode ?? '');

  // El orden importa: sin tabla no se puede juzgar nada del registro, ni siquiera si su
  // codigo existe. Culparlo de «municipio sin punto» seria acusarlo de un fallo ajeno.
  if (!puntosMunicipales) {
    return { nivel: 'catalogo_no_disponible', lat: null, lng: null, codigo };
  }
  if (!codigo) {
    return { nivel: 'sin_codigo', lat: null, lng: null, codigo: '' };
  }

  const punto = puntosMunicipales.get(codigo);
  if (!punto) {
    return { nivel: 'municipio_sin_punto', lat: null, lng: null, codigo };
  }

  // `latitude`/`longitude` son `decimal?` en el contrato. `Number(null)` es 0, y 0,0 es
  // un punto del oceano frente a Africa: un registro colombiano dibujado ahi. De ahi la
  // comprobacion de nulo ANTES de convertir.
  const lat = registro?.latitude;
  const lng = registro?.longitude;
  const traeCoordenada = typeof lat === 'number' && Number.isFinite(lat)
    && typeof lng === 'number' && Number.isFinite(lng);

  if (traeCoordenada) {
    const seAparta = Math.abs(lat - punto.lat) > TOLERANCIA_DE_PUNTO_PROPIO
      || Math.abs(lng - punto.lng) > TOLERANCIA_DE_PUNTO_PROPIO;
    if (seAparta) {
      return { nivel: 'punto_propio', lat, lng, codigo };
    }
  }

  return { nivel: 'punto_municipal', lat: punto.lat, lng: punto.lng, codigo };
};

/**
 * Agrupa los procesos por municipio: una pastilla por municipio, no una por proceso.
 *
 * POR QUE POR MUNICIPIO. Medido, los 155 procesos publicados
 * viven en 33 municipios, y 30 de esos 33 tienen exactamente 5 registros. Dibujar 155
 * marcas sobre 33 puntos es apilar cinco marcas en el mismo pixel cuatro veces de cada
 * cinco: se ve una y se pierden cuatro.
 *
 * NO ORDENA POR NADA QUE DEPENDA DEL ORDEN DE ENTRADA. Se ordena por codigo, que es
 * estable, para que reordenar los datos no cambie ni una posicion ni el reparto. Era
 * justo lo que la espiral no cumplia.
 */
const agruparProcesosPorMunicipio = <T extends RegistroSituable>(
  registros: readonly T[],
  puntosMunicipales: ReadonlyMap<string, PuntoMunicipal> | null | undefined,
): { municipios: MunicipioConProcesos<T>[]; noSituados: ProcesosNoSituados } => {
  const porCodigo = new Map<string, { punto: PuntoMunicipal; fichas: T[] }>();
  let sinCodigo = 0;
  let municipioSinPunto = 0;
  let catalogoNoDisponible = 0;

  for (const registro of registros || []) {
    const ubicacion = resolverUbicacionDeRegistro(registro, puntosMunicipales);

    if (ubicacion.nivel === 'catalogo_no_disponible') {
      catalogoNoDisponible += 1;
      continue;
    }
    if (ubicacion.nivel === 'sin_codigo') {
      sinCodigo += 1;
      continue;
    }
    if (ubicacion.nivel === 'municipio_sin_punto') {
      municipioSinPunto += 1;
      continue;
    }

    const punto = puntosMunicipales?.get(ubicacion.codigo);
    if (!punto) {
      municipioSinPunto += 1;
      continue;
    }

    const grupo = porCodigo.get(ubicacion.codigo);
    if (grupo) {
      grupo.fichas.push(registro);
    } else {
      porCodigo.set(ubicacion.codigo, { punto, fichas: [registro] });
    }
  }

  const municipios = [...porCodigo.entries()]
    .sort(([uno], [otro]) => uno.localeCompare(otro))
    .map(([codigo, grupo]) => ({
      codigo,
      nombre: grupo.punto.nombre,
      lat: grupo.punto.lat,
      lng: grupo.punto.lng,
      total: grupo.fichas.length,
      fichas: grupo.fichas,
    }));

  return { municipios, noSituados: { sinCodigo, municipioSinPunto, catalogoNoDisponible } };
};

// ---------------------------------------------------------------------------
// EL DIRECTORIO COMO ATAJO DEL MAPA (29 de agosto de 2026)
//
// LO QUE HABIA. Pulsar un registro del directorio abria un modal a pantalla completa
// —`fixed inset-0` con velo y `backdrop-blur`— encima del mapa. Medido en el navegador
// a 1280x639 antes del cambio: al pulsar la primera tarjeta del panel aparecia un
// dialogo que ocupaba los 1280x639 y el mapa dejaba de verse entero. El directorio esta
// PEGADO AL MAPA, en la columna de al lado: usarlo para tapar el mapa es gastar la
// pantalla en negarse a si misma.
//
// LO QUE PIDIO EL USUARIO, literal: «la funcionalidad de este no es para picar y abrir
// la ficha, sino para que se vaya al mapa y se ubique en el municipio, y se abra la
// pestana que se usa dentro del mapa para ubicar, la idea es que sea un directorio de
// navegacion que funciona como atajo en el mapa».
//
// LO QUE SE MIDIO ANTES DE DISENARLO, contra el API en 8081:
//
//   · Los 155 registros publicados traen `municipalityCode` y `departmentName`.
//   · Los 155 codigos estan en `/api/v1/publico/divipola`, que devuelve 1.122
//     municipios con coordenada.
//
// O sea que el atajo puede llevar a su municipio a CUALQUIERA de los 155 registros del
// directorio. Aun asi esta funcion contempla los tres casos en que no podria, porque el
// dia que un registro llegue sin codigo el clic tiene que seguir haciendo algo.
// ---------------------------------------------------------------------------

/** En que va la descarga de la tabla de puntos municipales. */
type EstadoDeLosPuntosMunicipales = 'sin_pedir' | 'cargando' | 'listo' | 'fallo';

/**
 * Acercamiento al que se deja el mapa al navegar a un municipio.
 *
 * NUEVE Y NO SIETE. Los rotulos municipales se encienden POR ENCIMA de
 * `MUNICIPALITY_LABEL_MIN_ZOOM` —`labelVisibilityForZoom` compara con `>`, no con
 * `>=`—, asi que en el umbral exacto el municipio al que se acaba de viajar se queda
 * sin nombre en pantalla. Nueve lo nombra y todavia deja ver a sus vecinos, que es lo
 * que hace falta para saber donde se esta.
 */
const ZOOM_DE_MUNICIPIO = 9;

interface DestinoDeNavegacion {
  /**
   * Que encuadra la camara.
   *
   * `ninguna` NO ES «no hay nada que hacer»: es «no muevas nada todavia», y eso es una
   * decision, no una omision. Ver la nota de `resolverDestinoDeNavegacion`.
   */
  readonly camara: 'municipio' | 'departamento' | 'ninguna';
  /** Punto del municipio, o null si no se conoce. Nunca una coordenada inventada. */
  readonly lat: number | null;
  readonly lng: number | null;
  /** Acercamiento para el caso `municipio`. */
  readonly zoom: number;
  /** Si la tarjeta de procesos se abre ya, o se espera a saber donde esta el municipio. */
  readonly tarjeta: 'abrir' | 'esperar';
}

/**
 * Adonde lleva pulsar un registro del directorio.
 *
 * EL ORDEN DE LAS TRES GUARDAS ES LA FUNCION. Sin codigo no hay nada que esperar:
 * ninguna descarga va a producir el punto de un municipio que el registro no nombra, y
 * poner esa guarda despues de la de «cargando» deja el clic colgado para siempre. Con
 * la descarga en curso si se espera, porque abrir la tarjeta en ese instante la deja
 * sin ancla —flotando en el centro de la pantalla y sin seguir al territorio cuando el
 * mapa se mueve—. Y si la descarga fallo o el municipio no esta en la tabla se abre
 * igual: un clic que no hace nada es peor que una tarjeta sin ancla.
 *
 * MIENTRAS SE ESPERA, LA CAMARA NO SE MUEVE, Y ESO SE MIDIO. La version anterior
 * encuadraba el departamento durante la espera y saltaba al municipio al llegar la
 * tabla: dos movimientos, y el segundo perdia contra el primero una de cada dos veces.
 * Instrumentando el efecto se vio el mecanismo: `fitBounds`
 * animado no empieza a animar en el acto, se programa para el siguiente fotograma, asi
 * que el `setView` del municipio se ejecutaba ANTES y el encuadre del departamento
 * llegaba despues y lo borraba. Las corridas buenas tenian tres pases del efecto —el
 * tercero volvia a poner el municipio— y las malas solo dos. Un solo movimiento, al
 * destino final, no tiene con quien competir.
 */
const resolverDestinoDeNavegacion = (
  codigoDeMunicipio: string,
  puntosMunicipales: ReadonlyMap<string, PuntoMunicipal> | null | undefined,
  estadoDeLosPuntos: EstadoDeLosPuntosMunicipales,
): DestinoDeNavegacion => {
  const sinPunto = { lat: null, lng: null, zoom: ZOOM_DE_MUNICIPIO } as const;

  const codigo = normalizeMunicipalityCode(codigoDeMunicipio || '');
  if (!codigo) return { ...sinPunto, camara: 'departamento', tarjeta: 'abrir' };

  const punto = puntosMunicipales?.get(codigo);
  if (punto) {
    return {
      camara: 'municipio',
      lat: punto.lat,
      lng: punto.lng,
      zoom: ZOOM_DE_MUNICIPIO,
      tarjeta: 'abrir',
    };
  }

  if (estadoDeLosPuntos === 'cargando') return { ...sinPunto, camara: 'ninguna', tarjeta: 'esperar' };

  return { ...sinPunto, camara: 'departamento', tarjeta: 'abrir' };
};

const getDepartmentSelectionValue = (value = ''): string => {
  const departmentCode = normalizeDepartmentCode(value);
  if (departmentCode) {
    const fromCode = getDepartmentNameByCode(departmentCode);
    if (fromCode) return fromCode;
  }

  const normalized = normalizeDepartmentName(value);

  if (!normalized) return value;
  if (normalized === ARCHIPELAGO_NORMALIZED_NAME) {
    return 'San Andrés y Providencia';
  }
  if (normalized === 'BOGOTA') {
    return 'Bogotá D.C.';
  }

  return Object.keys(getRuntimeDivipolaByDepartment()).find(
    (departmentName) => normalizeDepartmentName(departmentName) === normalized
  ) || value;
};

const resolveDepartmentDivipolaKey = (value = ''): string => {
  const grouped = getRuntimeDivipolaByDepartment();

  if (!value) return '';
  if (Array.isArray(grouped[value])) return value;

  const selectionValue = getDepartmentSelectionValue(value);
  if (selectionValue && Array.isArray(grouped[selectionValue])) return selectionValue;

  const normalized = normalizeDepartmentName(selectionValue || value);
  if (!normalized) return '';

  return Object.keys(grouped).find(
    (departmentName) => normalizeDepartmentName(departmentName) === normalized
  ) || '';
};

const municipalityExistsInList = (municipality = '', municipalityList: string[] = []): boolean => {
  const normalizedTarget = normalizeMunicipalityName(municipality);
  if (!normalizedTarget) return false;

  return (Array.isArray(municipalityList) ? municipalityList : []).some(
    (item) => normalizeMunicipalityName(item) === normalizedTarget
  );
};

const getFeatureDepartmentName = (feature: any): string => {
  if (!feature?.properties) return 'Sin nombre';
  const departmentCode = getFeatureDepartmentCode(feature);
  const fromCode = departmentCode ? getDepartmentNameByCode(departmentCode) : '';
  if (fromCode) return fromCode;

  return GEOJSON_DEPARTMENT_NAME_KEYS.map((key) => feature.properties[key]).find(Boolean) || 'Sin nombre';
};

const getFeatureDepartmentCode = (feature: any): string => {
  if (!feature?.properties) return '';

  const rawCode = GEOJSON_DEPARTMENT_CODE_KEYS
    .map((key) => feature.properties[key])
    .find((value) => value !== undefined && value !== null && String(value).trim() !== '');

  return normalizeDepartmentCode(rawCode || '');
};

const getFeatureDepartmentNormalizedName = (feature: any): string => {
  const featureDepartmentCode = getFeatureDepartmentCode(feature);
  if (featureDepartmentCode) {
    const mappedName = getDepartmentNameByCode(featureDepartmentCode);
    if (mappedName) {
      return normalizeDepartmentName(mappedName);
    }
  }

  return normalizeDepartmentName(getFeatureDepartmentName(feature));
};

const mapCoordinatesDeep = (coordinates: any, transformPoint: (pt: [number, number]) => [number, number]): any => {
  if (!Array.isArray(coordinates)) return coordinates;
  if (typeof coordinates[0] === 'number') {
    return transformPoint(coordinates as [number, number]);
  }
  return coordinates.map((value) => mapCoordinatesDeep(value, transformPoint));
};

const getCoordinatesBounds = (coordinates: any, bounds: any = {
  minLng: Infinity,
  maxLng: -Infinity,
  minLat: Infinity,
  maxLat: -Infinity,
}): any => {
  if (!Array.isArray(coordinates)) return bounds;

  if (typeof coordinates[0] === 'number') {
    const [lng, lat] = coordinates;
    return {
      minLng: Math.min(bounds.minLng, lng),
      maxLng: Math.max(bounds.maxLng, lng),
      minLat: Math.min(bounds.minLat, lat),
      maxLat: Math.max(bounds.maxLat, lat),
    };
  }

  return coordinates.reduce(
    (acc, value) => getCoordinatesBounds(value, acc),
    bounds
  );
};

/** Agranda un grupo de coordenadas respecto de su propio centro. */
const scaleAroundOwnCenter = (coordinates: any, scale: number): any => {
  const bounds = getCoordinatesBounds(coordinates);
  const centerLng = (bounds.minLng + bounds.maxLng) / 2;
  const centerLat = (bounds.minLat + bounds.maxLat) / 2;

  return mapCoordinatesDeep(coordinates, ([lng, lat]) => ([
    centerLng + (lng - centerLng) * scale,
    centerLat + (lat - centerLat) * scale,
  ]));
};

/**
 * Agranda las islas de un accidente geografico para que se vean a escala nacional.
 * San Andres mide 12,7 km y Santa Catalina 1,5 km: sin aumentar no se ven ni se pueden
 * pulsar.
 *
 * CADA POLIGONO SE AGRANDA RESPECTO DE SU PROPIO CENTRO, y esa es la linea que importa.
 * Antes se calculaba UN centro para los seis poligonos del departamento 88 y se escalaba
 * cada vertice respecto de el, con lo que se multiplicaba por 8,5 tambien la DISTANCIA
 * entre las islas. Medido contra `GET /api/v1/mapa/cartografia/departamentos` el 28 de agosto
 * de 2026: San Andres bajaba 381 km y se pintaba encima de Panama, Providencia subia
 * 370 km hasta mar abierto, y el conjunto pasaba de 0,91 grados de latitud a 7,77.
 * Lo vigila `archipielago.spec.ts`.
 */
const buildScaledFeature = (feature: any, scale: number = ARCHIPELAGO_VISUAL_SCALE): any => {
  if (!feature?.geometry?.coordinates) return null;

  const { type, coordinates } = feature.geometry;
  const scaled = type === 'MultiPolygon'
    ? coordinates.map((polygon: any) => scaleAroundOwnCenter(polygon, scale))
    : scaleAroundOwnCenter(coordinates, scale);

  return {
    ...feature,
    geometry: {
      ...feature.geometry,
      coordinates: scaled,
    },
  };
};


/* ------------------------------------------------------------------------------------
 * CAPA BASE DEL GEOVISOR
 * ----------------------------------------------------------------------------------*/

/**
 * Por que hay una tabla de proveedores y no una URL suelta en el componente.
 *
 * Hasta la capa base era CARTO, escrita a pelo dentro de
 * `montarElMapaUnaVez`. CARTO paso a exigir clave, y lo hizo SIN romper nada: sus tejas
 * siguen respondiendo 200, con el mismo tipo de contenido y el mismo peso —5.251 bytes
 * la teja 5/9/14 de `light_nolabels`, identica sin clave y con clave invalida—, pero
 * con «API KEY REQUIRED» estampado DENTRO del PNG. Ninguna prueba podia detectarlo y
 * nadie lo vio hasta que aparecio en pantalla.
 *
 * La leccion no es «cambiar de proveedor», es que el proveedor sea un dato y no una
 * linea de codigo. Cambiar de uno a otro es hoy cambiar `environment.basemap`.
 */
type BasemapKind = 'vector' | 'raster' | 'none';

interface BasemapProvider {
  /** Valor que se escribe en `environment.basemap`. */
  readonly id: string;
  readonly kind: BasemapKind;
  /** JSON de estilo si es vectorial; plantilla de teja si es raster. */
  readonly url: string;
  /** Texto que tiene que quedar VISIBLE en el mapa. Lo exigen las tres licencias. */
  readonly attribution: string;
  /**
   * Si la capa rotula paises por su cuenta. Cuando es `true` el componente NO dibuja
   * `WORLD_COUNTRY_LABELS`, porque saldrian los nombres por duplicado.
   */
  readonly hasOwnLabels: boolean;
}

const BASEMAP_PROVIDERS: readonly BasemapProvider[] = [
  {
    // Abierto de cabo a rabo, sin clave, sin limites y autoalojable: si cambian de
    // politica, los datos se sirven desde un servidor propio y el mapa no se entera.
    // El estilo Positron trae rotulos de pais, ciudad, via y agua; el componente los
    // retira al cargar —ver `isLabelLayer`— para conservar la base lavada sin texto.
    // MAPLIBRE-GL VA FIJADO A ~5.24.0 EN package.json, Y NO ES POR CAPRICHO.
    // `@maplibre/maplibre-gl-leaflet` 0.1.4 declara admitir `maplibre-gl ^6.0.0`, pero
    // no funciona con ella: el puente lee `this._glMap.transform` y hace
    // `Object.getPrototypeOf(transform)` (linea 118 de su dist), y en la rama 6 esa
    // propiedad ya no esta. El resultado no es un error visible sino algo peor: el mapa
    // se crea, el estilo se descarga y se aplica —55 capas—, el lienzo existe, y NO SE
    // PIDE NI UNA TEJA. En pantalla se ve el color de fondo del estilo y parece un mapa
    // vacio. Medido con 6.6.0 y con 6.4.1, en headless y en
    // navegador con GPU: cero tejas. Con 5.24.0: seis tejas en la primera vista.
    // Lo vigila `npm run mapa:capa-base`. Antes de subir maplibre-gl, ejecutarlo.
    id: 'openfreemap-positron',
    kind: 'vector',
    url: 'https://tiles.openfreemap.org/styles/positron',
    attribution: '&copy; <a href="https://openfreemap.org">OpenFreeMap</a> &middot; &copy; <a href="https://www.openmaptiles.org/">OpenMapTiles</a> &middot; Datos de <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    hasOwnLabels: false,
  },
  {
    // Comunitario y sin clave, pero la politica de tejas de la OSMF es para uso
    // moderado, no para servir produccion. Queda como reserva sin dependencias.
    id: 'openstreetmap',
    kind: 'raster',
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    hasOwnLabels: true,
  },
  {
    // Sin capa base: solo los poligonos de los departamentos sobre fondo plano.
    id: 'ninguna',
    kind: 'none',
    url: '',
    attribution: '',
    hasOwnLabels: false,
  },
];

const DEFAULT_BASEMAP_ID = 'openfreemap-positron';

/**
 * Devuelve el proveedor pedido. Si el identificador no existe cae en el de por defecto
 * en lugar de dejar el mapa sin fondo: una errata en la configuracion no debe apagar
 * la capa base en silencio.
 */
const resolveBasemapProvider = (id: string | null | undefined): BasemapProvider => {
  const buscado = (id ?? '').trim();
  return (
    BASEMAP_PROVIDERS.find((proveedor) => proveedor.id === buscado) ??
    BASEMAP_PROVIDERS.find((proveedor) => proveedor.id === DEFAULT_BASEMAP_ID)!
  );
};

/**
 * Una capa de estilo MapLibre es de rotulos cuando pinta texto. Se mira `text-field`
 * y no el nombre de la capa: los nombres los decide el estilo y cambian entre
 * versiones, `text-field` es lo que hace que aparezca una palabra en pantalla.
 */
const isLabelLayer = (layer: any): boolean => Boolean(layer?.layout?.['text-field']);

/**
 * A partir de que acercamiento se leen los rotulos.
 *
 * No es una preferencia estetica: a escala nacional los 33 nombres de departamento se
 * pisan unos a otros y tapan el coropletico, que es el dato. Y los 1.122 municipios,
 * dibujados a la vez, dejan el mapa ilegible. Cada rotulo aparece cuando su geometria
 * ya ocupa espacio suficiente para leerlo.
 *
 * EL UMBRAL DEPARTAMENTAL ES 5 Y NO 7, y queda dicho por que se cambio. La
 * especificacion que dejo escrita `mapa-ecosistemico-page.component.spec.ts` pedia 7
 * —a 7 ocultos, a 7,25 visibles—, pero el geovisor abre la vista nacional en torno a 5,5:
 * con aquel umbral no se leia un solo nombre de departamento al entrar. Se baja a 5 por
 * decision de producto: los territorios se nombran desde que se
 * ven. Aquella prueba sigue apagada, con el resto de las del componente que se perdio.
 */
const DEPARTMENT_LABEL_MIN_ZOOM = 5;
/**
 * El umbral municipal es el mismo, y no por pereza. Los municipios SOLO se dibujan
 * cuando hay un departamento abierto —a escala nacional no existe ni un poligono
 * municipal en el mapa—, asi que no hay riesgo de 1.122 rotulos a la vez. Poner el
 * umbral mas arriba tenia el efecto contrario al buscado: abrir un departamento encuadra
 * entre 7 y 8,5, de modo que los 125 rotulos de Antioquia se creaban y ninguno se veia.
 * Medido con `npm run mapa:territorios`: 125 creados, 0 visibles.
 *
 * Queda por encima del departamental para que la vista nacional nombre departamentos y
 * la de un departamento abierto nombre sus municipios, sin mezclar las dos escalas.
 */
const MUNICIPALITY_LABEL_MIN_ZOOM = 7;

const labelVisibilityForZoom = (zoom: number): { departments: boolean; municipalities: boolean } => {
  const nivel = Number(zoom);
  if (!Number.isFinite(nivel)) return { departments: false, municipalities: false };
  return {
    departments: nivel > DEPARTMENT_LABEL_MIN_ZOOM,
    municipalities: nivel > MUNICIPALITY_LABEL_MIN_ZOOM,
  };
};

// ---------------------------------------------------------------------------
// DONDE SE COLOCA LA TARJETA DE PROCESOS DE UN TERRITORIO
//
// EL PROBLEMA QUE RESUELVE. Pulsar un municipio abria un modal de 560 px centrado,
// sobre un velo `bg-[#0f172a]/50` con `backdrop-blur-sm`: el mapa desaparecia entero
// detras de la lista de sus propios procesos. No se podia comparar el municipio con su
// vecino, ni ver donde estaba lo que se acababa de pulsar.
//
// La tarjeta ahora sale al lado del punto, y esta funcion decide en que lado y a que
// altura. Es geometria pura —entran cuatro numeros, sale una posicion— justamente para
// poder comprobarla sin navegador, sin Leaflet y sin mapa.
//
// LAS TRES REGLAS, EN ORDEN:
//   1. Por defecto a la derecha del punto, separada por `GAP`.
//   2. Si a la derecha no cabe entera dentro del viewport, pasa a la izquierda. Se
//      compara contra el ancho COMPLETO de la tarjeta: mirar solo el borde izquierdo
//      la dejaba medio fuera de pantalla en el borde oriental del mapa.
//   3. Nunca toca el borde: `MARGIN` la separa de los cuatro lados, y la altura se
//      centra en el punto pero se recorta arriba y abajo.
//
// Las medidas son las del trabajo aparcado, y se conservan
// porque la prueba que las fija —«ubica la tarjeta al lado del punto»— esta escrita
// contra ellas: 232 de ancho y 14 de separacion aparecen literalmente en sus `expect`.
// ---------------------------------------------------------------------------

/** Ancho de la tarjeta. Fijado tambien en la clase `w-[232px]` de la plantilla. */
const TERRITORY_POPUP_WIDTH = 232;
/** Alto de referencia para centrarla en el punto. Es un maximo, no una altura impuesta. */
const TERRITORY_POPUP_HEIGHT = 286;
/** Separacion entre el punto pulsado y la tarjeta, para que no lo tape. */
const TERRITORY_POPUP_GAP = 14;
/** Margen minimo contra cualquier borde del viewport. */
const TERRITORY_POPUP_MARGIN = 10;

interface TerritoryPopupPlacement {
  readonly left: number;
  readonly top: number;
  readonly side: 'left' | 'right';
}

const territoryPopupPlacement = (
  pointX: number,
  pointY: number,
  viewportWidth: number,
  viewportHeight: number,
): TerritoryPopupPlacement => {
  const x = Number.isFinite(Number(pointX)) ? Number(pointX) : viewportWidth / 2;
  const y = Number.isFinite(Number(pointY)) ? Number(pointY) : viewportHeight / 2;

  const cabeADerecha = x + TERRITORY_POPUP_GAP + TERRITORY_POPUP_WIDTH <= viewportWidth - TERRITORY_POPUP_MARGIN;
  const side: 'left' | 'right' = cabeADerecha ? 'right' : 'left';

  const izquierdaSinRecortar = side === 'right'
    ? x + TERRITORY_POPUP_GAP
    : x - TERRITORY_POPUP_GAP - TERRITORY_POPUP_WIDTH;

  const left = Math.max(
    TERRITORY_POPUP_MARGIN,
    Math.min(izquierdaSinRecortar, viewportWidth - TERRITORY_POPUP_WIDTH - TERRITORY_POPUP_MARGIN),
  );
  const top = Math.max(
    TERRITORY_POPUP_MARGIN,
    Math.min(y - TERRITORY_POPUP_HEIGHT / 2, viewportHeight - TERRITORY_POPUP_HEIGHT - TERRITORY_POPUP_MARGIN),
  );

  return { left, top, side };
};

const scrollToElementWithOffset = (element: HTMLElement | null | undefined, offset: number = NAVBAR_SCROLL_OFFSET, behavior: ScrollBehavior = 'smooth'): void => {
  if (!element) return;
  const elementTop = element.getBoundingClientRect().top + window.scrollY;
  window.scrollTo({
    top: Math.max(elementTop - offset, 0),
    behavior,
  });
};

const getBaseDepartmentCounts = (): Record<string, number> => {
  const departmentNames = new Set(Object.keys(getRuntimeDivipolaByDepartment()));
  Object.values(runtimeDepartmentNameByCode).forEach((departmentName) => {
    if (departmentName) departmentNames.add(departmentName);
  });

  const baseCounts = Array.from(departmentNames).reduce((acc: Record<string, number>, deptName) => {
    acc[normalizeDepartmentName(deptName)] = 0;
    return acc;
  }, {});

  return baseCounts;
};

const formatMetricValue = (value: any = 0): string => {
  const numericValue = Number(value) || 0;
  return METRIC_FORMATTER.format(Math.round(numericValue));
};

const sumNumericValues = (values: any[] = []): number => values.reduce((sum, value) => sum + (Number(value) || 0), 0);

const formatDataCellValue = (value: any): string => {
  if (value === undefined || value === null || value === '') return '—';
  return typeof value === 'number' ? formatMetricValue(value) : value;
};

const buildSearchIndexValue = (values: any[] = []): string => normalizeDepartmentName(
  values
    .flatMap((value) => Array.isArray(value) ? value : [value])
    .filter((value) => value !== undefined && value !== null && value !== '')
    .join(' ')
);

const countDistinctValues = (items: any[] = [], selector: (item: any) => any = (item) => item): number => {
  const uniqueValues = new Set<string>();

  items.forEach((item) => {
    const value = selector(item);

    if (value === undefined || value === null || value === '') return;
    uniqueValues.add(String(value).trim());
  });

  return uniqueValues.size;
};

const compareTechnicalValues = (leftValue: any, rightValue: any, direction: 'asc' | 'desc' = 'desc'): number => {
  const leftNumber = typeof leftValue === 'number' ? leftValue : Number.NaN;
  const rightNumber = typeof rightValue === 'number' ? rightValue : Number.NaN;
  const multiplier = direction === 'asc' ? 1 : -1;

  if (Number.isFinite(leftNumber) && Number.isFinite(rightNumber)) {
    return (leftNumber - rightNumber) * multiplier;
  }

  const normalizedLeft = String(leftValue ?? '').toLocaleLowerCase('es-CO');
  const normalizedRight = String(rightValue ?? '').toLocaleLowerCase('es-CO');
  return normalizedLeft.localeCompare(normalizedRight, 'es-CO') * multiplier;
};

const escapeHtml = (value: any = ''): string => String(value ?? '')
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#39;');

const getRecordFieldValue = (recordOrFields: any, fieldNames: string[] = []): any => {
  const fields = recordOrFields?.fields || recordOrFields || {};

  for (const fieldName of fieldNames) {
    const value = fields?.[fieldName];

    if (Array.isArray(value)) {
      const firstValue = value.find(
        (item) => item !== undefined && item !== null && String(item).trim() !== ''
      );
      if (firstValue !== undefined) return firstValue;
      continue;
    }

    if (value !== undefined && value !== null && String(value).trim() !== '') {
      return value;
    }
  }

  return '';
};

const toNumericValue = (value: any): number => {
  if (value === undefined || value === null || value === '') return 0;
  const normalized = Number(String(value).replace(/[^\d.-]/g, ''));
  return Number.isFinite(normalized) ? normalized : 0;
};

const normalizeBooleanishField = (value: any): string => {
  if (value === undefined || value === null || value === '') return '';

  const normalized = String(value)
    .trim()
    .toUpperCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');

  if (['SI', 'TRUE', 'YES'].includes(normalized)) return 'Sí';
  if (['NO', 'FALSE'].includes(normalized)) return 'No';
  return String(value).trim();
};

const normalizeSchoolStatus = (value: any): string => {
  if (value === undefined || value === null || value === '') return '';

  const normalized = String(value)
    .trim()
    .toUpperCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');

  if (normalized.includes('EN PAUSA')) return 'En pausa';
  if (normalized.includes('INACT')) return 'Inactiva';
  if (normalized.includes('ACTIV')) return 'Activa';
  return String(value).trim();
};

const getSchoolField = (record: any, fieldKey: string): any => {
  return getRecordFieldValue(record, SCHOOL_FIELD_MAP[fieldKey] || []);
};

const getMarketField = (record: any, fieldKey: string): any => {
  return getRecordFieldValue(record, MARKET_FIELD_MAP[fieldKey] || []);
};

const extractNumericAverage = (value: any): number => {
  if (value === undefined || value === null || value === '') return 0;

  const matches = String(value).match(/\d+(?:[.,]\d+)?/g) || [];

  if (matches.length === 0) return 0;

  const values = matches
    .map((item) => Number(String(item).replace(',', '.')))
    .filter((item) => Number.isFinite(item));

  if (values.length === 0) return 0;

  return values.reduce((sum, item) => sum + item, 0) / values.length;
};

const resolveDepartmentNameFromCodeOrLabel = (departmentCode = '', departmentLabel = ''): string => {
  const normalizedCode = normalizeDepartmentCode(departmentCode);
  if (normalizedCode) {
    const mappedName = getDepartmentNameByCode(normalizedCode);
    if (mappedName) return mappedName;
  }

  return departmentLabel;
};

const resolveDepartmentNameFromRecord = (record: any, fallbackDepartment = ''): string => {
  const departmentCode = normalizeDepartmentCode(
    record?.fields?.departmentCode
    || record?.fields?.DepartmentCode
    || record?.fields?.dpto_ccdgo
  );

  return resolveDepartmentNameFromCodeOrLabel(departmentCode, fallbackDepartment);
};

const buildFestivalCounts = (records: any[] = []): Record<string, number> => {
  return records.reduce((acc: Record<string, number>, rec) => {
    const deptRaw = rec?.fields?.dpt ?? rec?.fields?.dpto ?? rec?.fields?.departamento ?? rec?.fields?.department;
    const deptName = Array.isArray(deptRaw) ? deptRaw[0] : (deptRaw || 'Desconocido');
    const resolvedDepartmentName = resolveDepartmentNameFromRecord(rec, deptName);
    const normalized = normalizeDepartmentName(resolvedDepartmentName);

    if (!normalized || normalized === 'DESCONOCIDO') return acc;

    acc[normalized] = (acc[normalized] || 0) + 1;
    return acc;
  }, {});
};

const isSchoolRecordVisible = (record: any): boolean => {
  const departmentName = resolveDepartmentNameFromCodeOrLabel(
    getSchoolField(record, 'departmentCode'),
    getSchoolField(record, 'department')
  );
  const department = normalizeDepartmentName(departmentName);
  const name = getSchoolField(record, 'name');
  const status = normalizeSchoolStatus(getSchoolField(record, 'status'));

  if (!department || department === 'DESCONOCIDO') return false;
  if (!name) return false;
  return status !== 'Inactiva';
};

const buildPublicSchoolRecord = (record: any): any => {
  if (!isSchoolRecordVisible(record)) return null;

  const departmentCode = normalizeDepartmentCode(getSchoolField(record, 'departmentCode'));
  const municipalityCode = normalizeMunicipalityCode(
    getSchoolField(record, 'municipalityCode') || getSchoolField(record, 'divipola')
  );
  const departmentName = resolveDepartmentNameFromCodeOrLabel(
    departmentCode,
    getSchoolField(record, 'department')
  );

  return {
    id: record?.id || getSchoolField(record, 'id'),
    status: normalizeSchoolStatus(getSchoolField(record, 'status')),
    departmentCode,
    municipalityCode,
    department: normalizeDepartmentName(departmentName),
    municipality: getSchoolField(record, 'municipality'),
    divipola: municipalityCode || getSchoolField(record, 'divipola'),
    name: getSchoolField(record, 'name'),
    schoolType: getSchoolField(record, 'schoolType'),
    category: getSchoolField(record, 'category'),
    legalCreation: normalizeBooleanishField(getSchoolField(record, 'legalCreation')),
    legalPersonhood: normalizeBooleanishField(getSchoolField(record, 'legalPersonhood')),
    nature: getSchoolField(record, 'nature'),
    dependsOnEntity: normalizeBooleanishField(getSchoolField(record, 'dependsOnEntity')),
    parentEntity: getSchoolField(record, 'parentEntity'),
    workSite: getSchoolField(record, 'workSite'),
    hasInternet: normalizeBooleanishField(getSchoolField(record, 'internetAccess')),
    instruments: toNumericValue(getSchoolField(record, 'instruments')),
    teachers: toNumericValue(getSchoolField(record, 'teachers')),
    students: toNumericValue(getSchoolField(record, 'students')),
    groups: toNumericValue(getSchoolField(record, 'groups')),
    practices: getSchoolField(record, 'practices'),
    linkedSonorousTerritories: getSchoolField(record, 'linkedSonorousTerritories') || '',
    workshops: normalizeBooleanishField(getSchoolField(record, 'workshops')),
    communityOrganization: normalizeBooleanishField(getSchoolField(record, 'communityOrganization')),
    directorName: getSchoolField(record, 'directorName'),
    directorContact: getSchoolField(record, 'directorContact'),
    contactEmail: getSchoolField(record, 'contactEmail'),
    contact: [getSchoolField(record, 'contactEmail'), getSchoolField(record, 'directorContact')].filter(Boolean).join(' · '),
  };
};

const buildMarketName = (record: any): string => {
  return getMarketField(record, 'name')
    || record?.fields?.Market_Name
    || record?.fields?.name
    || record?.fields?.nombre
    || 'Mercado sin nombre';
};

const isMarketRecordVisible = (record: any): boolean => {
  const departmentName = resolveDepartmentNameFromCodeOrLabel(
    getMarketField(record, 'departmentCode'),
    getMarketField(record, 'department')
  );
  const department = normalizeDepartmentName(departmentName);
  const name = buildMarketName(record);

  return Boolean(department && department !== 'DESCONOCIDO' && name);
};

const buildPublicMarketRecord = (record: any): any => {
  if (!isMarketRecordVisible(record)) return null;

  const departmentCode = normalizeDepartmentCode(getMarketField(record, 'departmentCode'));
  const municipalityCode = normalizeMunicipalityCode(getMarketField(record, 'municipalityCode'));
  const departmentName = resolveDepartmentNameFromCodeOrLabel(
    departmentCode,
    getMarketField(record, 'department')
  );

  return {
    id: record?.id || '',
    name: buildMarketName(record),
    departmentCode,
    municipalityCode,
    department: normalizeDepartmentName(departmentName),
    municipality: getMarketField(record, 'municipality'),
    createdYear: getMarketField(record, 'createdYear'),
    versions: getMarketField(record, 'versions'),
    periodicity: getMarketField(record, 'periodicity'),
    editionDate2026: getMarketField(record, 'editionDate2026'),
    responsibleEntity: getMarketField(record, 'responsibleEntity'),
    organizationType: getMarketField(record, 'organizationType'),
    legalFormalStatus: normalizeBooleanishField(getMarketField(record, 'legalFormalStatus')),
    linkedFestival: normalizeBooleanishField(getMarketField(record, 'linkedFestival')),
    festivalName: getMarketField(record, 'festivalName'),
    festivalVersions: getMarketField(record, 'festivalVersions'),
    festivalDates: getMarketField(record, 'festivalDates'),
    fundingSources: getMarketField(record, 'fundingSources'),
    publicBudgetShare: getMarketField(record, 'publicBudgetShare'),
    curationModel: getMarketField(record, 'curationModel'),
    openCall: normalizeBooleanishField(getMarketField(record, 'openCall')),
    averageProjects: extractNumericAverage(getMarketField(record, 'averageProjects')),
    averageProjectsLabel: getMarketField(record, 'averageProjects'),
    averageBuyers: extractNumericAverage(getMarketField(record, 'averageBuyers')),
    averageBuyersLabel: getMarketField(record, 'averageBuyers'),
    buyerSpaces: getMarketField(record, 'buyerSpaces'),
    buyerStrategies: getMarketField(record, 'buyerStrategies'),
    preAgreements: getMarketField(record, 'preAgreements'),
    circulationMechanisms: getMarketField(record, 'circulationMechanisms'),
    publicArticulations: getMarketField(record, 'publicArticulations'),
    partnerNetworks: getMarketField(record, 'partnerNetworks'),
    pnmcConnections: getMarketField(record, 'pnmcConnections'),
    pnmcConnectionsDetail: getMarketField(record, 'pnmcConnectionsDetail'),
    collaborationPotential: getMarketField(record, 'collaborationPotential'),
    territorialImpact: getMarketField(record, 'territorialImpact'),
    practices: getMarketField(record, 'practices') || '',
    linkedSonorousTerritories: getMarketField(record, 'linkedSonorousTerritories') || '',
    websiteUrl: getMarketField(record, 'websiteUrl') || record?.fields?.sitio_web || '',
  };
};

const buildSchoolCounts = (records: any[] = []): Record<string, number> => {
  return records.reduce((acc: Record<string, number>, record) => {
    const normalized = normalizeDepartmentName(record?.department);

    if (!normalized || normalized === 'DESCONOCIDO') return acc;

    acc[normalized] = (acc[normalized] || 0) + 1;
    return acc;
  }, {});
};

const buildMarketCounts = (records: any[] = []): Record<string, number> => {
  return records.reduce((acc: Record<string, number>, record) => {
    const normalized = normalizeDepartmentName(record?.department);

    if (!normalized || normalized === 'DESCONOCIDO') return acc;

    acc[normalized] = (acc[normalized] || 0) + 1;
    return acc;
  }, {});
};

const buildLayerAnalytics = (counts: Record<string, number> = {}, records: any[] = [], departamentoElegido = 'Nacional'): any => {
  const allDepartmentNames = new Set<string>([
    ...Object.keys(getRuntimeDivipolaByDepartment()),
    ...Object.values(runtimeDepartmentNameByCode),
  ]);

  const entries = Object.entries(counts)
    .map(([name, count]) => ({ name, count }))
    .filter((item) => item.count > 0)
    .sort((a, b) => b.count - a.count);

  const totalRecords = records.length || entries.reduce((acc, item) => acc + item.count, 0);
  const activeDepartments = entries.length;
  const totalDepartments = allDepartmentNames.size;
  const departamentoNormalizado = normalizeDepartmentName(departamentoElegido);
  const selectedCount = departamentoElegido === 'Nacional' ? totalRecords : (counts[departamentoNormalizado] || 0);
  const selectedRank = departamentoElegido === 'Nacional' ? null : entries.findIndex((item) => item.name === departamentoNormalizado) + 1;
  const coverage = totalDepartments > 0 ? Math.round((activeDepartments / totalDepartments) * 100) : 0;
  const concentration = totalRecords > 0 && entries.length > 0 ? Math.round((entries[0].count / totalRecords) * 100) : 0;

  return {
    entries,
    topDepartments: entries.slice(0, 6),
    uncoveredDepartments: Array.from(allDepartmentNames)
      .map((name) => normalizeDepartmentName(name))
      .filter((name) => !entries.some((item) => item.name === name))
      .slice(0, 8),
    totalRecords,
    activeDepartments,
    totalDepartments,
    coverage,
    concentration,
    selectedCount,
    selectedRank,
  };
};

const buildSchoolCapacityTotals = (records: any[] = []): any => {
  return records.reduce((acc, record) => {
    acc.totalStudents += record.students || 0;
    acc.totalTeachers += record.teachers || 0;
    acc.totalInstruments += record.instruments || 0;
    acc.totalGroups += record.groups || 0;

    if (record.hasInternet === 'Sí') acc.withInternet += 1;
    if (record.communityOrganization === 'Sí') acc.withCommunityOrganization += 1;
    if (record.status === 'Activa') acc.active += 1;
    if (record.status === 'En pausa') acc.paused += 1;

    return acc;
  }, {
    totalStudents: 0,
    totalTeachers: 0,
    totalInstruments: 0,
    totalGroups: 0,
    withInternet: 0,
    withCommunityOrganization: 0,
    active: 0,
    paused: 0,
  });
};

const buildMarketTotals = (records: any[] = []): any => {
  const totals = records.reduce((acc, record) => {
    acc.totalProjects += record.averageProjects || 0;
    acc.totalBuyers += record.averageBuyers || 0;
    if (record.openCall === 'Sí') acc.openCalls += 1;
    if (record.linkedFestival === 'Sí') acc.linkedToFestival += 1;
    return acc;
  }, {
    totalProjects: 0,
    totalBuyers: 0,
    openCalls: 0,
    linkedToFestival: 0,
  });

  return {
    ...totals,
    totalMarkets: records.length,
    averageProjectsPerMarket: records.length > 0 ? totals.totalProjects / records.length : 0,
    averageBuyersPerMarket: records.length > 0 ? totals.totalBuyers / records.length : 0,
  };
};

const buildDepartmentSummaryMap = (
  baseCounts: Record<string, number> = {},
  festivalesPorDepartamento: Record<string, any[]> = {},
  escuelasPorDepartamento: Record<string, any[]> = {},
  mercadosPorDepartamento: Record<string, any[]> = {},
  redesPorDepartamento: Record<string, any[]> = {},
  lutierRecordsByDepartment: Record<string, any[]> = {}
): Record<string, DepartmentSummary> => {
  const summaryMap = Object.keys(baseCounts).reduce((acc: Record<string, DepartmentSummary>, departmentName) => {
    acc[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    return acc;
  }, {});

  Object.entries(festivalesPorDepartamento).forEach(([departmentName, records]) => {
    if (!summaryMap[departmentName]) {
      summaryMap[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    }

    summaryMap[departmentName].festivalCount = records.length;
  });

  Object.entries(escuelasPorDepartamento).forEach(([departmentName, records]) => {
    if (!summaryMap[departmentName]) {
      summaryMap[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    }

    const totals = buildSchoolCapacityTotals(records);
    summaryMap[departmentName].schoolCount = records.length;
    summaryMap[departmentName].totalStudents = totals.totalStudents;
    summaryMap[departmentName].totalTeachers = totals.totalTeachers;
    summaryMap[departmentName].totalInstruments = totals.totalInstruments;
    summaryMap[departmentName].totalGroups = totals.totalGroups;
  });

  Object.entries(mercadosPorDepartamento).forEach(([departmentName, records]) => {
    if (!summaryMap[departmentName]) {
      summaryMap[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    }

    const totals = buildMarketTotals(records);
    summaryMap[departmentName].marketCount = records.length;
    summaryMap[departmentName].totalMarketProjects = totals.totalProjects;
    summaryMap[departmentName].totalMarketBuyers = totals.totalBuyers;
  });

  Object.entries(redesPorDepartamento).forEach(([departmentName, records]) => {
    if (!summaryMap[departmentName]) {
      summaryMap[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    }

    summaryMap[departmentName].redesCount = records.length;
  });

  Object.entries(lutierRecordsByDepartment).forEach(([departmentName, records]) => {
    if (!summaryMap[departmentName]) {
      summaryMap[departmentName] = { ...EMPTY_DEPARTMENT_SUMMARY };
    }

    summaryMap[departmentName].lutierCount = records.length;
  });

  Object.values(summaryMap).forEach((summary) => {
    summary.totalRecords =
      summary.festivalCount +
      summary.schoolCount +
      summary.marketCount +
      summary.redesCount +
      summary.lutierCount;
  });

  return summaryMap;
};

const getFestivalRecordName = (record: any): string => {
  return record?.fields?.Festival_Name
    || record?.fields?.name
    || record?.fields?.nombre
    || record?.fields?.title
    || record?.fields?.festival
    || record?.fields?.t
    || 'Registro sin nombre';
};

const buildDepartmentPopupMarkup = ({
  deptName,
  capaActiva,
  stats = EMPTY_DEPARTMENT_SUMMARY,
  embedded = false,
}: {
  deptName: string;
  capaActiva: string;
  stats?: DepartmentSummary;
  embedded?: boolean;
}): string => {
  const isGeneralPopup = capaActiva === 'General';
  const isSchoolsPopup = capaActiva === 'Escuelas de Música';
  const isFestivalsPopup = capaActiva === 'Festivales';
  const isMarketsPopup = capaActiva === 'Mercados Musicales';
  const popupHeadline = capaActiva === 'General'
    ? {
      value: formatMetricValue(stats.totalRecords),
    }
    : isSchoolsPopup
      ? {
        value: formatMetricValue(stats.schoolCount),
      }
      : isFestivalsPopup
        ? {
        value: formatMetricValue(stats.festivalCount),
      }
        : isMarketsPopup
          ? {
            value: formatMetricValue(stats.marketCount),
          }
        : {
          value: 'Próx.',
        };
  const generalDistributionCards = [
    {
      label: 'Festivales',
      value: stats.festivalCount,
      accent: '#00DA5E',
    },
    {
      label: 'Escuelas',
      value: stats.schoolCount,
      accent: '#8BF784',
    },
    {
      label: 'Mercados',
      value: stats.marketCount,
      accent: '#291242',
    },
  ];
  const festivalsShare = stats.totalRecords > 0 ? Math.round((stats.festivalCount / stats.totalRecords) * 100) : 0;
  const schoolsShare = stats.totalRecords > 0 ? Math.round((stats.schoolCount / stats.totalRecords) * 100) : 0;
  const marketsShare = stats.totalRecords > 0 ? Math.round((stats.marketCount / stats.totalRecords) * 100) : 0;
  const wrapperClasses = embedded
    ? 'w-full h-full'
    : 'p-2 min-w-[280px] max-w-[300px]';
  const shellClasses = embedded
    ? 'overflow-hidden bg-transparent h-full flex flex-col'
    : 'overflow-hidden rounded-[1.35rem] border border-slate-200 bg-white shadow-[0_18px_36px_rgba(15,23,42,0.12)]';

  return `
    <div class="${wrapperClasses}">
      <div class="${shellClasses}">
        <div class="bg-[#291242] px-4 py-4">
          <div class="flex items-stretch justify-between gap-4 min-h-[76px]">
            <div class="min-w-0 flex items-end">
              <div class="text-[0.98rem] font-bold uppercase tracking-[0.1em] leading-tight text-white">
                ${escapeHtml(deptName)}
              </div>
            </div>
            <div class="flex-shrink-0 flex flex-col items-end justify-center text-right">
              <div class="text-[0.95rem] font-bold leading-none text-white">${popupHeadline.value}</div>
              <div class="mt-1 text-[0.46rem] font-bold uppercase tracking-[0.18em] text-slate-300">Registros</div>
            </div>
          </div>
        </div>
        ${isGeneralPopup ? `
        <div class="px-4 py-4 bg-white flex-1">
          <div class="rounded-[1.2rem] border border-slate-100 bg-slate-50 overflow-hidden">
            <div class="flex h-1.5 overflow-hidden bg-white">
              <span style="width:${festivalsShare}%;background:#00DA5E;"></span>
              <span style="width:${schoolsShare}%;background:#8BF784;"></span>
              <span style="width:${marketsShare}%;background:#291242;"></span>
            </div>
            <div class="grid grid-cols-3 divide-x divide-slate-200 items-stretch">
              ${generalDistributionCards.map((item) => {
                const share = stats.totalRecords > 0 ? Math.round((item.value / stats.totalRecords) * 100) : 0;

                return `
              <div class="px-3.5 py-3.5">
                <div class="flex items-center gap-2 text-[0.48rem] font-bold uppercase tracking-[0.16em] text-slate-500">
                  <span class="h-2 w-2 rounded-full flex-shrink-0" style="background:${item.accent};"></span>
                  ${item.label}
                </div>
                <div class="mt-3 text-[1.2rem] font-bold leading-none text-[#291242]">${formatMetricValue(item.value)}</div>
                <div class="mt-2 text-[0.42rem] font-bold uppercase tracking-[0.1em] text-slate-400 whitespace-nowrap">${share}% del registro</div>
              </div>
          `;
              }).join('')}
            </div>
          </div>
        </div>
        ` : isSchoolsPopup && embedded ? `
        <div class="px-4 py-4 bg-white flex-1">
          <div class="grid grid-cols-3 gap-3 h-full">
            ${[
              { label: 'Escuelas', value: stats.schoolCount },
              { label: 'Estudiantes', value: stats.totalStudents },
              { label: 'Docentes', value: stats.totalTeachers },
            ].map((item) => `
              <div class="rounded-[1.1rem] border border-slate-100 bg-slate-50 px-3 py-3 flex flex-col justify-between">
                <div class="text-[0.45rem] font-bold uppercase tracking-[0.14em] text-slate-400">${item.label}</div>
                <div class="mt-3 text-[1.05rem] font-bold leading-none text-[#291242]">${formatMetricValue(item.value)}</div>
              </div>
            `).join('')}
          </div>
        </div>
        ` : isFestivalsPopup && embedded ? `
        <div class="px-4 py-4 bg-white flex-1">
          <div class="rounded-[1.2rem] border border-slate-100 bg-slate-50 px-4 py-4 h-full flex items-center justify-between gap-4">
            <div>
              <div class="text-[0.48rem] font-bold uppercase tracking-[0.16em] text-slate-400">Festivales</div>
              <div class="mt-3 text-[1.35rem] font-bold leading-none text-[#291242]">${formatMetricValue(stats.festivalCount)}</div>
            </div>
            <div class="h-10 w-px bg-slate-200"></div>
            <div class="text-right">
              <div class="text-[0.48rem] font-bold uppercase tracking-[0.16em] text-slate-400">Lectura</div>
              <div class="mt-3 text-[0.7rem] font-bold uppercase tracking-[0.08em] text-[#291242]">Circulación visible</div>
            </div>
          </div>
        </div>
        ` : isMarketsPopup && embedded ? `
        <div class="px-4 py-4 bg-white flex-1">
          <div class="grid grid-cols-3 gap-3 h-full">
            ${[
              { label: 'Mercados', value: stats.marketCount },
              { label: 'Proyectos', value: stats.totalMarketProjects },
              { label: 'Bookers', value: stats.totalMarketBuyers },
            ].map((item) => `
              <div class="rounded-[1.1rem] border border-slate-100 bg-slate-50 px-3 py-3 flex flex-col justify-between">
                <div class="text-[0.45rem] font-bold uppercase tracking-[0.14em] text-slate-400">${item.label}</div>
                <div class="mt-3 text-[1.05rem] font-bold leading-none text-[#291242]">${formatMetricValue(item.value)}</div>
              </div>
            `).join('')}
          </div>
        </div>
        ` : ''}
      </div>
    </div>
  `;
};

const buildDynamicDensitySteps = (layerKey: string, maxCount: number): ChoroplethStep[] => {
  const palette = MAP_LAYER_CHOROPLETH_STEPS[layerKey] || MAP_LAYER_CHOROPLETH_STEPS['General'];
  const normalizedMax = Math.max(0, Math.floor(Number(maxCount) || 0));
  if (normalizedMax === 0) return [];

  const bandCount = Math.min(palette.length, normalizedMax);
  return Array.from({ length: bandCount }, (_, index) => {
    const min = Math.floor((index * normalizedMax) / bandCount) + 1;
    const max = Math.floor(((index + 1) * normalizedMax) / bandCount);
    const paletteIndex = bandCount === 1
      ? palette.length - 1
      : Math.round((index * (palette.length - 1)) / (bandCount - 1));
    const paletteStep = palette[paletteIndex];

    return {
      ...paletteStep,
      min,
      max,
      label: min === max ? `${min}` : `${min} a ${max}`,
    };
  });
};

const DEPARTMENT_HIT_AREA_STYLE = {
  fill: true,
  fillColor: '#000000',
  fillOpacity: 0,
  color: 'transparent',
  opacity: 0,
  weight: 2,
  interactive: true,
};

const getChoroplethStyles = (
  count: number,
  isSelected = true,
  layerKey = 'Festivales',
  maxCount = 0,
): any => {
  let fillColor = 'transparent';
  let fillOpacity = 0;
  let strokeColor = '#291242';
  let strokeWeight = 1.2;
  const layerSteps = maxCount > 0
    ? buildDynamicDensitySteps(layerKey, maxCount)
    : (MAP_LAYER_CHOROPLETH_STEPS[layerKey] || MAP_LAYER_CHOROPLETH_STEPS['Festivales']);
  const activeStep = layerSteps.find((step) => count >= step.min && count <= step.max);

  if (activeStep) {
    fillColor = activeStep.color;
    fillOpacity = activeStep.opacity;
  }

  if (!isSelected) {
    fillColor = '#d8d3df';
    fillOpacity = 0.3; // Muted fill for non-selected
    strokeColor = '#291242';
    strokeWeight = 1.0;
  }

  if (isSelected && count > 0) {
    strokeColor = '#291242';
    strokeWeight = 1.5;
  }

  return {
    fillColor,
    fill: true,
    fillOpacity,
    fillRule: 'nonzero',
    color: strokeColor,
    interactive: true,
    weight: strokeWeight,
    opacity: 0.5,
  };
};

const COLOMBIA_DEPARTMENT_CENTROIDS: Record<string, [number, number]> = {
  'antioquia': [6.2442, -75.5812],
  'atlantico': [10.9685, -74.7813],
  'bogota': [4.6097, -74.0817],
  'bolivar': [10.3910, -75.4794],
  'caldas': [5.0689, -75.5174],
  'cauca': [2.4419, -76.6063],
  'cesar': [10.4631, -73.2532],
  'choco': [5.6983, -76.6583],
  'la guajira': [11.5444, -72.9069],
  'meta': [4.1420, -73.6266],
  'narino': [1.2136, -77.2811],
  'valle del cauca': [3.4516, -76.5320],
  'arauca': [7.0903, -70.7616],
  'casanare': [5.3378, -72.3959],
  'cundinamarca': [4.7110, -73.8000],
  'guaviare': [2.5667, -72.6333],
  'huila': [2.5333, -75.6000],
  'norte de santander': [7.9000, -72.5000],
  'putumayo': [1.1500, -76.6500],
  'quindio': [4.5333, -75.6667],
  'risaralda': [5.0689, -75.8000],
  'santander': [7.1254, -73.1198],
  'sucre': [9.3000, -75.4000],
  'tolima': [4.1667, -75.1667],
  'vaupes': [1.2500, -70.5000],
  'vichada': [6.1833, -69.2167],
  'amazonas': [-1.0191, -71.9385],
  'caqueta': [1.6144, -75.6062],
  'guainia': [2.5000, -68.5000],
  'magdalena': [10.4000, -74.2000],
  'san andres': [12.5847, -81.7006],
  'cordoba': [8.7500, -75.8833],
  'boyaca': [5.5500, -73.0000]
};

const cleanTextForMatching = (str: string): string => {
  if (!str) return '';
  return str
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();
};

const matchesSonorousTerritory = (selectedTerritory: string, textToCheck: string): boolean => {
  if (!selectedTerritory || selectedTerritory === 'Todos') return true;
  const selClean = cleanTextForMatching(selectedTerritory);
  const textClean = cleanTextForMatching(textToCheck);
  
  if (textClean.includes(selClean)) return true;

  const keywordsMap: Record<string, string[]> = {
    'Cantos, Pitos y Tambores': ['cantos', 'pitos', 'tambores', 'cumbia', 'gaita', 'caribe'],
    'Canta y Torbellino': ['canta', 'torbellino', 'guabina', 'pasillo', 'andina'],
    'Rajaleña y Cucamba': ['rajalena', 'cucamba', 'huila', 'sampedro', 'bambuco'],
    'Marimba': ['marimba', 'currulao', 'pacifico', 'sur', 'cantos tradicionales'],
    'Flautas, Cuerdas y Tambores Sureños': ['flautas', 'cuerdas', 'tambores', 'surenos', 'sur', 'narino'],
    'Chirimía': ['chirimia', 'choco', 'pacifico norte'],
    'Joropo': ['joropo', 'arpa', 'cuatro', 'maracas', 'llano', 'llanera', 'llanero'],
    'Trova y Parranda': ['trova', 'parranda', 'paisa', 'antioquia'],
    'Amazonas': ['amazonas', 'amazonico', 'indigena'],
    'Insular': ['insular', 'san andres', 'reggae', 'calipso', 'providencia'],
    'Prácticas de Pueblos Indígenas': ['indigena', 'indigenas', 'pueblos originarios', 'nasa', 'wayuu'],
    'Músicas Urbanas, Alternativas e Independientes - MUAI': ['urbana', 'urbanas', 'alternativa', 'independiente', 'muai', 'rock', 'hip hop', 'pop', 'rap'],
    'Comunidades Académicas': ['academica', 'academicas', 'universidad', 'conservatorio'],
    'Rrom': ['rrom', 'gitano', 'gitanos']
  };

  const keywords = keywordsMap[selectedTerritory];
  if (!keywords) return false;
  
  return keywords.some(keyword => textClean.includes(cleanTextForMatching(keyword)));
};

const matchesPracticeMusical = (selectedPractice: string, textToCheck: string): boolean => {
  if (!selectedPractice || selectedPractice === 'Todas') return true;
  const selClean = cleanTextForMatching(selectedPractice);
  const textClean = cleanTextForMatching(textToCheck);
  
  if (textClean.includes(selClean)) return true;

  const keywordsMap: Record<string, string[]> = {
    'Expresiones sonoras de pueblos originarios': ['originarios', 'pueblos', 'indigena', 'indigenas'],
    'Músicas de comunidades negras, afrocolombianas, raizales y palenqueras': ['negras', 'afrocolombianas', 'raizales', 'palenqueras', 'afro', 'raizal', 'palenque'],
    'Músicas campesinas, rurales y de raíz territorial': ['campesina', 'campesino', 'rural', 'carranga', 'carranguera'],
    'Músicas populares tradicionales, regionales y patrimoniales': ['tradicional', 'regional', 'patrimonial', 'tradicionales', 'regionales'],
    'Músicas comunitarias y procesos colectivos de práctica musical': ['comunitaria', 'comunitario', 'colectivo', 'social'],
    'Músicas de frontera, diásporas, migraciones e interculturalidad': ['frontera', 'diaspora', 'migracion', 'intercultural'],
    'Músicas urbanas, alternativas e independientes': ['urbana', 'alternativa', 'independiente', 'rock', 'pop', 'hip hop'],
    'Músicas populares de amplia circulación, tropicales, bailables y comerciales': ['popular', 'tropical', 'bailable', 'comercial', 'salsa', 'merengue'],
    'Músicas vocales, corales y de tradición cantada': ['vocal', 'coral', 'coro', 'canto', 'cantada'],
    'Músicas sinfónicas, bandas, orquestas y grandes formatos instrumentales': ['sinfonica', 'banda', 'orquesta', 'formato'],
    'Bandas de marcha, batucadas, comparsas y colectivos sonoros en movimiento': ['marcha', 'batucada', 'comparsa', 'movimiento'],
    'Músicas académicas, de cámara, contemporáneas, experimentales y de vanguardia': ['academica', 'camara', 'contemporanea', 'experimental', 'vanguardia'],
    'Músicas electrónicas, digitales, producción sonora y nuevas tecnologías': ['electronica', 'digital', 'produccion', 'tecnologia'],
    'Músicas religiosas, rituales, espirituales y devocionales': ['religiosa', 'ritual', 'espiritual', 'devocional', 'sacra'],
    'Músicas para escena, danza, audiovisual e interdisciplinariedad': ['escena', 'danza', 'audiovisual', 'interdisciplinar'],
    'Prácticas sonoras, arte sonoro, archivo, investigación-creación y paisajes sonoros': ['arte sonoro', 'archivo', 'investigacion', 'creacion', 'paisaje']
  };

  const keywords = keywordsMap[selectedPractice];
  if (!keywords) return false;

  return keywords.some(keyword => textClean.includes(cleanTextForMatching(keyword)));
};

export type {
  BasemapProvider,
  BasemapKind,
  TerritoryPopupPlacement,
  NivelDeUbicacion,
  PuntoMunicipal,
  RegistroSituable,
  UbicacionDeRegistro,
  MunicipioConProcesos,
  ProcesosNoSituados,
  EstadoDeLosPuntosMunicipales,
  DestinoDeNavegacion,
};

export {
  FESTIVAL_COUNTS_CACHE_KEY,
  SCHOOL_COUNTS_CACHE_KEY,
  MARKET_COUNTS_CACHE_KEY,
  ARCHIPELAGO_NORMALIZED_NAME,
  METRIC_FORMATTER,
  MAP_LAYER_CHOROPLETH_STEPS,
  EMPTY_DEPARTMENT_SUMMARY,
  SCHOOL_PUBLICATION_POLICY,
  MARKET_PUBLICATION_POLICY,
  BASEMAP_PROVIDERS,
  DEFAULT_BASEMAP_ID,
  resolveBasemapProvider,
  isLabelLayer,
  DEPARTMENT_HIT_AREA_STYLE,
  buildDynamicDensitySteps,
  getRuntimeDivipolaByDepartment,
  setRuntimeDivipolaByDepartment,
  normalizeDepartmentCode,
  normalizeMunicipalityCode,
  setRuntimeDepartmentCatalog,
  getDepartmentNameByCode,
  getDepartmentCodeByName,
  DEPARTMENT_LABEL_MIN_ZOOM,
  MUNICIPALITY_LABEL_MIN_ZOOM,
  labelVisibilityForZoom,
  TERRITORY_POPUP_WIDTH,
  TERRITORY_POPUP_HEIGHT,
  TERRITORY_POPUP_GAP,
  TERRITORY_POPUP_MARGIN,
  territoryPopupPlacement,
  getSortedDepartmentNames,
  FONDO_NEUTRO_DEL_DEPARTAMENTO,
  SEPARADOR_DE_CLASIFICACION,
  separarClasificacion,
  clasificacionLegible,
  normalizeDepartmentName,
  normalizeMunicipalityName,
  sortUniqueByLocale,
  getDepartmentDisplayName,
  getDepartmentLabelName,
  resolverUbicacionDeRegistro,
  agruparProcesosPorMunicipio,
  ZOOM_DE_MUNICIPIO,
  resolverDestinoDeNavegacion,
  getDepartmentSelectionValue,
  resolveDepartmentDivipolaKey,
  municipalityExistsInList,
  getFeatureDepartmentName,
  getFeatureDepartmentCode,
  getFeatureDepartmentNormalizedName,
  scrollToElementWithOffset,
  getBaseDepartmentCounts,
  formatMetricValue,
  sumNumericValues,
  formatDataCellValue,
  buildSearchIndexValue,
  countDistinctValues,
  compareTechnicalValues,
  resolveDepartmentNameFromRecord,
  buildFestivalCounts,
  buildPublicSchoolRecord,
  buildPublicMarketRecord,
  buildSchoolCounts,
  buildMarketCounts,
  buildLayerAnalytics,
  buildSchoolCapacityTotals,
  buildMarketTotals,
  buildDepartmentSummaryMap,
  getFestivalRecordName,
  buildDepartmentPopupMarkup,
  buildScaledFeature,
  getChoroplethStyles,
  COLOMBIA_DEPARTMENT_CENTROIDS,
  cleanTextForMatching,
  matchesSonorousTerritory,
  matchesPracticeMusical,
};

/**
 * El reparto de un treemap, calculado con el algoritmo «squarified».
 *
 * <b>POR QUE UN TREEMAP Y NO MAS BARRAS.</b> Una barra dice el ORDEN y el valor; un treemap dice la
 * PARTE DEL TODO, porque el área de cada bloque es su proporción y todos los bloques juntos son el
 * total. Con veintinueve departamentos, además, la lista de barras es una columna que no cabe y el
 * treemap entra en un rectángulo: se ve de un vistazo que tres departamentos son medio país.
 *
 * <b>«SQUARIFIED» Y NO EL REPARTO INGENUO.</b> Partir el rectángulo siempre en la misma dirección
 * produce bloques larguísimos y finos —«tiras»— cuya área el ojo no sabe comparar: una tira de
 * 200x4 y un cuadrado de 28x28 miden lo mismo y no lo parecen. Este algoritmo elige en cada paso la
 * dirección que deja los bloques más cuadrados posible, que es lo que vuelve comparables las áreas.
 *
 * Está aquí y no en el componente porque es aritmética pura: se puede probar sin montar una
 * pantalla, que es justo lo que un reparto de áreas necesita.
 */
export interface BloqueDeTreemap<T> {
  readonly x: number;
  readonly y: number;
  readonly ancho: number;
  readonly alto: number;
  readonly dato: T;
}

export function repartirEnTreemap<T>(
  valores: readonly { readonly valor: number; readonly dato: T }[],
  ancho: number,
  alto: number,
): BloqueDeTreemap<T>[] {
  const positivos = valores.filter((item) => item.valor > 0);
  if (positivos.length === 0 || ancho <= 0 || alto <= 0) return [];

  const bloques: BloqueDeTreemap<T>[] = [];
  let pendientes = [...positivos].sort((uno, otro) => otro.valor - uno.valor);
  let areaRestante = pendientes.reduce((suma, item) => suma + item.valor, 0);
  let x = 0;
  let y = 0;
  let libreAncho = ancho;
  let libreAlto = alto;

  // GUARDA CONTRA EL BUCLE INFINITO. Si por un redondeo una vuelta no consumiera ninguna fila, el
  // `while` no terminaría nunca y colgaría la pestaña. Con un tope igual al número de elementos,
  // el peor caso es una fila por elemento.
  let vueltas = 0;
  while (pendientes.length > 0 && vueltas <= positivos.length) {
    vueltas += 1;
    const horizontal = libreAncho >= libreAlto;
    const lado = horizontal ? libreAlto : libreAncho;
    const areaLibre = libreAncho * libreAlto;
    if (lado <= 0 || areaLibre <= 0) break;

    // Se añaden elementos a la fila mientras la peor relación de aspecto MEJORE. En cuanto empeora,
    // la fila está completa: es la heurística entera del algoritmo.
    let fila = pendientes.slice(0, 1);
    let mejorRazon = Number.POSITIVE_INFINITY;
    for (let tamano = 1; tamano <= pendientes.length; tamano += 1) {
      const candidata = pendientes.slice(0, tamano);
      const sumaFila = candidata.reduce((suma, item) => suma + item.valor, 0);
      const grosor = (sumaFila * (areaLibre / areaRestante)) / lado;
      if (grosor <= 0) break;
      const razon = candidata.reduce((peor, item) => {
        const largo = (item.valor * (areaLibre / areaRestante)) / grosor;
        return Math.max(peor, Math.max(largo / grosor, grosor / largo));
      }, 0);
      if (razon > mejorRazon) break;
      mejorRazon = razon;
      fila = candidata;
    }

    const sumaFila = fila.reduce((suma, item) => suma + item.valor, 0);
    const grosor = (sumaFila * (areaLibre / areaRestante)) / lado;
    let avance = 0;
    for (const item of fila) {
      const largo = (item.valor * (areaLibre / areaRestante)) / grosor;
      bloques.push(horizontal
        ? { x, y: y + avance, ancho: grosor, alto: largo, dato: item.dato }
        : { x: x + avance, y, ancho: largo, alto: grosor, dato: item.dato });
      avance += largo;
    }

    if (horizontal) { x += grosor; libreAncho -= grosor; }
    else { y += grosor; libreAlto -= grosor; }
    areaRestante -= sumaFila;
    pendientes = pendientes.slice(fila.length);
  }

  return bloques;
}
