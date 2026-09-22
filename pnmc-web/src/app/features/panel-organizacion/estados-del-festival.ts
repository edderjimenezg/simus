/**
 * El vocabulario de estados del circuito Festival, en un solo sitio.
 *
 * <b>Por qué se normaliza en vez de comparar literalmente.</b> La plantilla anterior comparaba
 * `festival.estado === 'EnRevision'` contra la cadena exacta. La base guarda los códigos en
 * minúscula con guion bajo —`en_revision`, `ajustes_solicitados`, obligados por
 * `CK_EstadosContenido_CodigoEstado_Formato`— y el API los traduce a PascalCase en
 * `EstadosFestival.HaciaContrato` (pnmc-api/src/PNMC.Api/Endpoints/EstadosFestival.cs). Esa
 * función devuelve el valor TAL CUAL cuando no lo reconoce, así que una fila con un código
 * heredado llega con la grafía de la base. Con la comparación literal, el aviso de «en revisión»
 * y sus botones se apagaban EN SILENCIO: sin error, sin 400, solo una tarjeta que deja de avisar.
 *
 * Aceptar las dos grafías no puede quitar un botón: solo puede reconocer un estado que antes
 * caía al hueco «Desconocido», que es justo el caso que no pinta nada.
 *
 * <b>Por qué es un fichero aparte.</b> Lo necesitan la pestaña de Ecosistema —para saber qué
 * botones admite cada Festival— y la de Solicitudes —para saber cuáles están esperando una
 * decisión del equipo del PNMC—. Dos copias de esta tabla divergen en cuanto una de las dos
 * pantallas cambie.
 */

/** Los cinco estados que recorre un Festival, más el hueco para lo que no se reconozca. */
export type EstadoFestival =
  | 'Borrador'
  | 'EnRevision'
  | 'AjustesSolicitados'
  | 'Rechazado'
  | 'Publicado'
  | 'Desconocido';

/** Los estados de la propuesta de cambios. En femenino: el sujeto es la propuesta. */
export type EstadoPropuesta =
  | 'Borrador'
  | 'EnRevision'
  | 'AjustesSolicitados'
  | 'Rechazada'
  | 'Publicada'
  | 'Desconocido';

const ESTADOS_DEL_FESTIVAL: Readonly<Record<string, EstadoFestival>> = {
  borrador: 'Borrador',
  enrevision: 'EnRevision',
  en_revision: 'EnRevision',
  ajustessolicitados: 'AjustesSolicitados',
  ajustes_solicitados: 'AjustesSolicitados',
  rechazado: 'Rechazado',
  publicado: 'Publicado',
};

/**
 * Estados de la propuesta de cambios. Los escribe
 * `RevisionInstitucionalPropuestasFestivalEndpoints.cs-180` («AjustesSolicitados»,
 * «Rechazada», «Publicada»), `PropuestasCambioFestivalExternosEndpoints.cs` («Borrador») y
 * `:224` («EnRevision»). El femenino es deliberado y no se corrige aquí: la propuesta no es el
 * Festival.
 */
const ESTADOS_DE_LA_PROPUESTA: Readonly<Record<string, EstadoPropuesta>> = {
  borrador: 'Borrador',
  enrevision: 'EnRevision',
  en_revision: 'EnRevision',
  ajustessolicitados: 'AjustesSolicitados',
  ajustes_solicitados: 'AjustesSolicitados',
  rechazada: 'Rechazada',
  rechazado: 'Rechazada',
  publicada: 'Publicada',
  publicado: 'Publicada',
  // EL VOCABULARIO DEL EXPEDIENTE GENERICO, desde: una propuesta
  // aplicada es lo que este panel lleva llamando «Publicada» desde el principio. Se traduce aquí,
  // que es donde ya se traducían las otras cuatro formas de decir lo mismo.
  aplicada: 'Publicada',
};

const ETIQUETAS_DEL_FESTIVAL: Readonly<Record<EstadoFestival, string>> = {
  Borrador: 'Borrador',
  EnRevision: 'En revisión',
  AjustesSolicitados: 'Ajustes solicitados',
  Rechazado: 'Rechazado',
  Publicado: 'Publicado',
  Desconocido: '',
};

const ETIQUETAS_DE_LA_PROPUESTA: Readonly<Record<EstadoPropuesta, string>> = {
  Borrador: 'Borrador',
  EnRevision: 'En revisión',
  AjustesSolicitados: 'Ajustes solicitados',
  Rechazada: 'Rechazada',
  Publicada: 'Publicada',
  Desconocido: '',
};

/** Minúscula y sin espacios sobrantes: la comparación no depende de la grafía recibida. */
export function claveDeEstado(valor: string | null | undefined): string {
  return (valor ?? '').trim().toLowerCase();
}

export function estadoDelFestival(valor: string | null | undefined): EstadoFestival {
  return ESTADOS_DEL_FESTIVAL[claveDeEstado(valor)] ?? 'Desconocido';
}

/**
 * La etiqueta del distintivo. Si el estado no se reconoce se devuelve el valor recibido en crudo
 * y no una cadena vacía: un dato raro es más fácil de diagnosticar que un dato ausente, que es el
 * mismo criterio que sigue `EstadosFestival.HaciaContrato`.
 */
export function etiquetaDelFestival(valor: string | null | undefined): string {
  const estado = estadoDelFestival(valor);
  return estado === 'Desconocido' ? (valor ?? '') : ETIQUETAS_DEL_FESTIVAL[estado];
}

/** `null` cuando no hay propuesta abierta ni cerrada; nunca «Desconocido» por ausencia. */
export function estadoDeLaPropuesta(valor: string | null | undefined): EstadoPropuesta | null {
  if (claveDeEstado(valor) === '') return null;
  return ESTADOS_DE_LA_PROPUESTA[claveDeEstado(valor)] ?? 'Desconocido';
}

export function etiquetaDeLaPropuesta(valor: string | null | undefined): string {
  const estado = estadoDeLaPropuesta(valor);
  if (estado === null) return '';
  return estado === 'Desconocido' ? (valor ?? '') : ETIQUETAS_DE_LA_PROPUESTA[estado];
}
