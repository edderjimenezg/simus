export interface AdminRole {
  id: string;
  label: string;
  shortLabel: string;
  description: string;
  capabilities: string[];
  adminAssignable?: boolean;
}

export const ADMIN_ROLES: Record<string, AdminRole> = {
  webmaster: {
    id: 'webmaster',
    label: 'Webmaster',
    shortLabel: 'Webmaster',
    description: 'Control total de la consola: usuarios, módulos, datos, configuración, revisión, publicación y mantenimiento.',
    capabilities: ['read', 'read_privileged', 'create', 'edit', 'solicitudes', 'request_changes', 'reject', 'approve', 'publish', 'archive', 'import', 'export', 'use_assistant', 'extract_from_document', 'manage_global_users', 'manage_site_texts', 'manage_system', 'view_audit'],
  },
  gestor_interno: {
    id: 'gestor_interno',
    label: 'Gestor interno',
    shortLabel: 'Gestor',
    description: 'Segundo nivel de administración. Gestiona módulos, revisa, aprueba y acompaña información de su componente, sin administrar usuarios ni sistema.',
    capabilities: ['read', 'read_privileged', 'create', 'edit', 'solicitudes', 'request_changes', 'reject', 'approve', 'import', 'export', 'use_assistant'],
  },
  externo: {
    id: 'externo',
    label: 'Usuario externo',
    shortLabel: 'Externo',
    description: 'Persona del ecosistema. Es a la vez la persona registrada y quien representa a un actor —agrupación, escuela de música, agente, escenario—: los distingue el vínculo con su entidad, no el rol. No accede a la consola interna.',
    capabilities: ['create_public_submission', 'create_own_entity', 'edit_own_records', 'submit_review'],
  },
};

/**
 * Roles INTERNOS del Ministerio. Es la lista que decide qué pantalla se abre al iniciar
 * sesión, y está escrita como lista blanca a propósito.
 *
 * Hasta la decisión se tomaba al revés: se enumeraban los roles NO
 * internos y la consola completa quedaba como caso por defecto. Con seis roles en el catálogo
 * funcionaba por casualidad; el día que apareciera un rol nuevo y nadie tocara esa línea, ese
 * rol abriría la consola interna entera y solo vería 403 en cada llamada — una consola rota y
 * ningún aviso.
 *
 * El equivalente en el API es `Permisos.RolesInternos`. Las dos listas deben decir lo mismo.
 */
export const ROLES_INTERNOS: readonly string[] = ['webmaster', 'gestor_interno'];

/** ¿Este rol pertenece al Ministerio? Lo que no está nombrado, no es interno. */
export function esRolInterno(roleId: string | null | undefined): boolean {
  return !!roleId && ROLES_INTERNOS.includes(roleId);
}

/**
 * Los roles de una sesión, normalizados y sin repetidos.
 *
 * DESDE LA TRANSICION DE LA MIGRACIÓN DE SIMUS una persona puede tener varios roles, y el API los
 * envía en `user.roles`. El campo `user.role` sigue viniendo —es el principal por precedencia— y
 * este ayudante lo usa como respaldo, de modo que una respuesta anterior al cambio se sigue
 * entendiendo. Devolver una lista vacía cuando no hay sesión no es un descuido: con la lista
 * vacía, `algunoEsRolInterno` dice que no y el SPA cae del lado seguro.
 */
export function rolesDeSesion(sesion: { role?: string | null; roles?: readonly string[] | null } | null | undefined): string[] {
  if (!sesion) { return []; }

  const crudos = (sesion.roles && sesion.roles.length > 0) ? sesion.roles : (sesion.role ? [sesion.role] : []);

  return Array.from(new Set(
    crudos.map((rol) => (rol ?? '').trim().toLowerCase()).filter((rol) => rol.length > 0)
  ));
}

/** ¿Alguno de estos roles pertenece al Ministerio? */
export function algunoEsRolInterno(roles: readonly string[]): boolean {
  return roles.some((rol) => esRolInterno(rol));
}

export interface AdminStatusConfig {
  label: string;
  variant: string;
}

/**
 * El vocabulario de estados, y es EL de la base: las ocho filas de `dbo.EstadosContenido`.
 *
 * NO SE INVENTAN CODIGOS AQUI. Hasta cuatro pantallas usaban
 * `en_evaluacion`, que no está en esa tabla ni aparece en `pnmc-api/src`. Lo que producía no era un
 * texto feo: la cola de revisión filtraba por él y dejaba fuera todo lo que de verdad estaba en
 * revisión, y el desplegable de estado de cada fila lo enviaba al API, que responde
 * `400 El estado no existe en EstadosContenido`. Un botón roto que parecía funcionar.
 *
 * `registrada` ESTA EN LA LISTA aunque no sea del circuito editorial: es el estado con el que nace
 * una organización dada de alta desde el sitio, y sin él se pintaba en crudo.
 */
export const ADMIN_STATUS: Record<string, AdminStatusConfig> = {
  borrador: { label: 'Borrador', variant: 'neutral' },
  registrada: { label: 'Registrada', variant: 'info' },
  en_revision: { label: 'En revisión', variant: 'warning' },
  ajustes_solicitados: { label: 'Ajustes solicitados', variant: 'warning' },
  aprobado: { label: 'Aprobado', variant: 'success' },
  publicado: { label: 'Publicado', variant: 'info' },
  rechazado: { label: 'Rechazado', variant: 'danger' },
  archivado: { label: 'Archivado', variant: 'neutral' },
};

/**
 * Los códigos que NO son estados de contenido: trámites, severidades y niveles de coincidencia.
 *
 * <b>VAN APARTE DE `ADMIN_STATUS` A PROPOSITO.</b> Aquella tabla es el espejo exacto de
 * `dbo.EstadosContenido` y una prueba lo comprueba fila a fila; meter aquí «pendiente» o
 * «aclaracion_enviada» rompería esa garantía, que existe porque un código inventado —`en_evaluacion`
 * -- ya se coló una vez y nadie lo vio. Son dos vocabularios distintos con la misma pregunta
 * delante: cómo se llama esto para quien lo lee.
 */
export const ETIQUETAS_DE_TRAMITE: Readonly<Record<string, string>> = {
  pendiente: 'Pendiente',
  enviada: 'Enviada',
  aclaracion_enviada: 'Aclaración enviada',
  aclaracion_solicitada: 'Aclaración solicitada',
  aprobada: 'Aprobada',
  rechazada: 'Rechazada',
  abierta: 'Abierta',
  resuelta: 'Resuelta',
  resuelto: 'Resuelto',
  descartado: 'Descartado',
  descartada: 'Descartada',
  fusionado: 'Marcado para fusión',
  activa: 'Activa',
  inactiva: 'Inactiva',
  pendiente_de_confirmacion: 'Pendiente de confirmación',
  eliminada: 'Eliminada',
  alta: 'Alta',
  media: 'Media',
  baja: 'Baja',
};

/**
 * El nombre de un código, para quien lo lee.
 *
 * <b>NUNCA DEVUELVE EL CODIGO CRUDO.</b> Busca primero en los estados de contenido, después en los
 * de trámite, y si no está en ninguno lo humaniza —quita los guiones bajos y pone la primera en
 * mayúscula— en vez de sacar `aclaracion_enviada` a la pantalla. Lo pidió la dirección de producto el
 * 14 de septiembre de 2026 al encontrarlos en las listas: «los estados aparecen con nombres como
 * en_revision, no están optimizados para frontend».
 *
 * El respaldo importa más que las dos tablas: garantiza que un código nuevo del servidor —uno que
 * nadie se acordó de añadir aquí— se lea igual de bien el día que aparezca.
 */
export function etiquetaDeEstado(codigo: string | null | undefined): string {
  const clave = (codigo ?? '').trim();
  if (!clave) return 'Sin estado';
  const conocido = ADMIN_STATUS[clave]?.label ?? ETIQUETAS_DE_TRAMITE[clave];
  if (conocido) return conocido;
  const humano = clave
    .replace(/[_-]+/g, ' ')
    // TAMBIEN SE PARTE EL CAMELLO. El respaldo solo separaba por guiones, así que un código en
    // PascalCase salía entero: «AdministracionControl» se leía tal cual en el filtro de grupos de
    // Auditoría, medido. Se corta ANTES de cada mayúscula que sigue a
    // una minúscula o a un dígito, que es lo que distingue «AdministracionControl» de «PNMC»: las
    // siglas no llevan minúscula delante y por eso sobreviven enteras.
    .replace(/([a-z\d])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim();
  const [primera, ...resto] = humano.split(' ');
  // Solo la primera palabra conserva su mayúscula inicial: «Administracion Control» sería un
  // título, y esto es una etiqueta de dato.
  const cola = resto.map(palabra => (palabra === palabra.toLocaleUpperCase('es-CO') ? palabra : palabra.toLocaleLowerCase('es-CO')));
  const frase = [primera, ...cola].join(' ');
  return frase.charAt(0).toLocaleUpperCase('es-CO') + frase.slice(1);
}
/**
 * Los estados en que un registro está esperando a que alguien haga algo.
 *
 * VIVE AQUI PORQUE ESTABA ESCRITO TRES VECES —la cola de revisión, el recuento de pendientes del
 * armazón y el agregado de procesos externos—, y las tres decían `en_evaluacion`. Una lista
 * repetida se corrige en dos sitios y se olvida en el tercero; ahí es donde el defecto sobrevive.
 *
 * NO SIRVE PARA EL DISTINTIVO DE «REVISIÓN»: para eso está {@link ESTADOS_QUE_ESPERAN_A_LA_CONSOLA}.
 * Esta lista responde «¿queda algo por hacer con este registro?», sin decir de quién es el turno.
 */
export const ESTADOS_CON_TRABAJO_PENDIENTE: readonly string[] = [
  'borrador',
  'en_revision',
  'ajustes_solicitados',
];

/**
 * Los estados en que el turno es DE LA CONSOLA, y no de la organización.
 *
 * EL DISTINTIVO DE «REVISIÓN» DECIA 99 y solo había 14 Festivales esperando decisión. Contaba con
 * {@link ESTADOS_CON_TRABAJO_PENDIENTE}, que incluye `borrador` y `ajustes_solicitados`: medido en
 * `PNMC_LOCAL`, **76 borradores + 14 en revisión + 9 con ajustes = 99**,
 * exactamente el número del distintivo. Los 76 borradores son Festivales que la organización no ha
 * enviado —la consola no puede ni verlos— y los 9 con ajustes están de vuelta con quien los
 * escribió. Un contador que suma trabajo ajeno no se puede vaciar, y un contador que no se puede
 * vaciar se deja de mirar.
 *
 * `en_revision` es el único estado en que la consola tiene algo que decidir.
 */
export const ESTADOS_QUE_ESPERAN_A_LA_CONSOLA: readonly string[] = ['en_revision'];

export interface AdminArea {
  id: string;
  label: string;
  description: string;
}

export const ADMIN_AREAS: Record<string, AdminArea> = {
  ecosystem: {
    id: 'ecosistema',
    label: 'Ecosistema',
    description: 'Las organizaciones del ecosistema y los seis procesos de los que responden: festivales, escuelas de música, mercados musicales, redes de documentación, lutería y escenarios.',
  },
  communications: {
    id: 'communications',
    label: 'Comunicaciones y prensa',
    description: 'Agenda, noticias, álbumes, recursos editoriales, piezas visibles en la web pública y la lista de correos del boletín.',
  },
};

const baseAdminFields = [
  { name: 'id', label: 'ID existente', type: 'text', system: true },
  { name: 'status', label: 'Estado', type: 'status', defaultValue: 'borrador' },
];

// EL VOCABULARIO DE COBERTURA VIVE EN `core/vocabularios/cobertura`, no aquí: las páginas públicas
// también lo necesitan y no deben importar del espacio administrativo. Se reexporta con su nombre
// de siempre para no tocar a quien ya lo usaba.
export { COBERTURAS as ADMIN_COVERAGE_LEVELS } from '../../../core/vocabularios/cobertura';

const territoryFields = [
  { name: 'coverageLevel', label: 'Cobertura', type: 'coverage', defaultValue: 'municipal' },
  { name: 'department', label: 'Departamento', type: 'department', required: true },
  { name: 'municipality', label: 'Municipio', type: 'municipality' },
];

export interface AdminEntityType {
  id: string;
  label: string;
  description: string;
}

export const ADMIN_ENTITY_TYPES: AdminEntityType[] = [
  { id: 'organizacion', label: 'Organización', description: 'Entidad jurídica, red, fundación, corporación, asociación o institución responsable de procesos.' },
  { id: 'escuela_musica', label: 'Escuela de música', description: 'Escuela, proceso formativo o programa de práctica musical.' },
  { id: 'lutier', label: 'Lutier', description: 'Lutier independiente, taller o colectivo de lutería.' },
  { id: 'festival', label: 'Festival', description: 'Festival, encuentro o circuito musical administrado por una entidad.' },
  { id: 'mercado_musical', label: 'Mercado musical', description: 'Mercado, rueda, vitrina o plataforma de intermediación musical.' },
  { id: 'espacio', label: 'Espacio', description: 'Sala, teatro, casa cultural, estudio o infraestructura musical.' },
  { id: 'colectivo', label: 'Colectivo', description: 'Agrupación, colectivo creativo o proceso autogestionado.' },
  { id: 'individuo', label: 'Individuo', description: 'Persona natural vinculada al ecosistema musical.' },
];

export interface AdminField {
  name: string;
  label: string;
  type: string;
  required?: boolean;
  system?: boolean;
  defaultValue?: any;
  rows?: number;
  wide?: boolean;
  options?: { value: string; label: string }[];
  step?: string;
}

export interface AdminModule {
  id: string;
  area: string;
  label: string;
  /*
   * AQUI ESTABAN `singular` y `femenino`. Solo los leia el titulo del modal de alta —«NUEVA
   * NOTICIA» en vez de «NUEVO REGISTRO»—, y ese modal se retiro con el formulario CRUD heredado.
   * Se van con el: un campo que nadie lee es una promesa de que alguien lo usa.
   */
  table: string;
  /*
   * AQUI HABIA UN CAMPO `endpoint`, y los NUEVE modulos lo tenian mal.
   *
   * Declaraban rutas del estilo `/admin/data/map/festivals` o `/admin/data/agenda/events`.
   * Comprobadas contra el API en marcha el 11 sep 2026: las nueve responden 404. La lectura
   * real nunca uso este campo —va por `/admin/data/records/{modulo}`, que si existe—; lo usaba
   * solo el guardado del formulario heredado, que se retira en este mismo corte.
   *
   * No se sustituye por la ruta buena a proposito: no hay ninguna operacion de escritura que
   * necesite componerla. Cuando la haya, se declara donde se use.
   */
  description: string;
  allowedRoles: string[];
  required: string[];
  fields: AdminField[];
}

export const ADMIN_MODULES: AdminModule[] = [
  {
    id: 'festivals',
    area: 'ecosistema',
    label: 'Festivales',
    table: 'Festivales',
    description: 'Festivales, encuentros y circuitos musicales registrados para el mapa.',
    allowedRoles: ['webmaster', 'gestor_interno'],
    required: ['name', 'department'],
    fields: [
      ...baseAdminFields,
      { name: 'name', label: 'Nombre del festival', type: 'text', required: true },
      { name: 'versionsCount', label: 'Número de versiones', type: 'number' },
      { name: 'lastEditionDate', label: 'Fecha última versión', type: 'date' },
      { name: 'description', label: 'Descripción', type: 'textarea', rows: 4, wide: true },
      { name: 'periodicity', label: 'Periodicidad', type: 'text' },
      { name: 'periodicityDetail', label: 'Detalle de periodicidad', type: 'textarea', rows: 2, wide: true },
      { name: 'organizer', label: 'Organizador', type: 'text' },
      { name: 'organizerEmail', label: 'Correo organizador', type: 'email' },
      { name: 'organizerPhone', label: 'Teléfono organizador', type: 'text' },
      { name: 'organizerWebsiteUrl', label: 'Sitio web organizador', type: 'url' },
      { name: 'contactEmail', label: 'Correo festival', type: 'email' },
      { name: 'contactPhone', label: 'Teléfono festival', type: 'text' },
      { name: 'websiteUrl', label: 'Sitio web festival', type: 'url' },
      { name: 'instagramUrl', label: 'Instagram', type: 'url' },
      { name: 'facebookUrl', label: 'Facebook', type: 'url' },
      { name: 'otherUrl', label: 'Otro enlace', type: 'url' },
      { name: 'hasCurrentYearEdition', label: 'Tiene versión vigente este año', type: 'checkbox' },
      { name: 'currentYearEditionStatus', label: 'Estado versión actual', type: 'text' },
      { name: 'currentYearStartDate', label: 'Inicio versión actual', type: 'date' },
      { name: 'currentYearEndDate', label: 'Fin versión actual', type: 'date' },
      ...territoryFields,
    ],
  },
];

/**
 * Los módulos que abre este conjunto de roles: la UNIÓN de lo que abre cada uno.
 *
 * La unión, y no «el rol más alto», porque es lo que significa tener dos roles: sumar lo que cada
 * uno abre. Es la misma semántica que el API aplica en `Permisos.EsFuncionario` y en la matriz de
 * cambio de estado de `AdminDataEndpoints`. Un conjunto vacío no abre nada, que es lo correcto
 * para una sesión que no se pudo leer.
 */
/*
 * AQUI HABIA UN SEGUNDO FILTRO, `MODULOS_ADMINISTRATIVOS_OPERATIVOS`, y era un parche.
 *
 * `ADMIN_MODULES` declaraba nueve modulos y este conjunto dejaba pasar uno. Los otros ocho
 * —escuelas, mercados, redes, escenarios, luteria, agenda, noticias y galeria— seguian ahi con
 * sus campos, su tabla y su endpoint, describiendo tablas retiradas y rutas inexistentes: 255
 * lineas que ninguna pantalla podia alcanzar y que cualquier lectura del inventario tenia que
 * aprender a ignorar.
 *
 * Ahora la lista dice la verdad por si sola: si un modulo esta en `ADMIN_MODULES`, tiene
 * circuito. Cuando cada proceso vuelva con su modelo y su revision propios, entra aqui y se le
 * ve; mientras tanto no figura. Un filtro que tape la mitad de una lista es como se llega a que
 * la lista deje de significar nada.
 */
export const getModulesForRoles = (roles: readonly string[]): AdminModule[] => ADMIN_MODULES.filter((module) => (
  roles.some((roleId) => module.allowedRoles.includes(roleId))
));

export const getModulesForRole = (roleId: string): AdminModule[] => getModulesForRoles([roleId]);

export const getModulesByAreaForRole = (roleId: string, areaId: string): AdminModule[] => getModulesForRole(roleId).filter((module) => (
  module.area === areaId
));

/** ¿Alguno de estos roles trae esta capacidad? La unión, igual que arriba. */
export const canRoles = (roles: readonly string[], capability: string): boolean => (
  roles.some((roleId) => ADMIN_ROLES[roleId]?.capabilities.includes(capability) ?? false)
);

export const canRole = (roleId: string, capability: string): boolean => canRoles([roleId], capability);
