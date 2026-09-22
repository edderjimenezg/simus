/**
 * El vocabulario de la revisión campo por campo, en un solo sitio.
 *
 * <b>POR QUÉ VIVE EN `core` Y NO EN UNA DE LAS DOS PANTALLAS.</b> Lo usan las dos puntas de la
 * misma conversación: el panel institucional, que escribe las notas, y el panel de la organización,
 * que las atiende. Puesto en cualquiera de los dos, el otro tendría que importar de un `features/`
 * ajeno, que es la dependencia que el proyecto evita desde que `seccion-organizacion` tuvo que
 * mudar `NIVELES_DE_COBERTURA` a `panel-organizacion.api.ts`.
 *
 * <b>LOS NOMBRES SON LOS DEL CABLE.</b> `PNMC.Contracts/RevisionDeCamposContratos.cs`. Un campo
 * mal escrito aquí no falla al compilar: falla en pantalla como un hueco en blanco.
 */

/** Los tres estados de una revisión. Los mismos que `CK_RevisionesFestival_Estado`. */
export type EstadoDeLaRevision = 'borrador' | 'enviada' | 'cerrada';

/** Los dos estados de una nota. Los mismos que `CK_RevisionesFestivalObservaciones_Estado`. */
export type EstadoDeLaNota = 'pendiente' | 'atendida';

/**
 * Sobre qué se pide el cambio.
 *
 * NO ES DECORACIÓN: `principal` es la ficha del registro y `subregistro` una de sus realizaciones
 * —en un Festival, una fila de `dbo.VersionesFestival`—. «Correo de contacto» existe en las dos y
 * son dos columnas distintas.
 *
 * <b>ANTES SE LLAMABAN `festival` Y `perfil_versionado`.</b> Se renombraron el 17 de septiembre de
 * 2026, al pasar las revisiones de Festival a las tablas genéricas: son los mismos dos conceptos que
 * ya usaba Mercados, y con dos nombres distintos hacía falta traducir en medio para que dos módulos
 * dijeran lo mismo.
 */
export type AmbitoDelCampo = 'principal' | 'subregistro';

/** Un cambio pedido sobre un campo, tal como viaja en las dos direcciones. */
export interface ObservacionDeCampo {
  /** Cero mientras la nota solo existe en la pantalla del funcionario. */
  id: number;
  ambito: AmbitoDelCampo;
  subregistroId: number | null;
  seccionId: string;
  campoId: string;
  campoEtiqueta: string;
  valorObservado: string | null;
  nota: string;
  estado: EstadoDeLaNota;
  fechaAtencion: string | null;
}

/** El borrador entero: la observación general, las dos puntas y las notas. */
export interface RevisionDeCampos {
  id: number;
  /** El módulo del registro: `festivales`, `mercados`… */
  moduloId: string;
  registroId: string;
  registroNombre: string;
  estado: EstadoDeLaRevision;
  observacionGeneral: string | null;
  /** Quién envía. */
  revisorNombre: string | null;
  /** Quién recibe. */
  destinatarioNombre: string | null;
  organizacionNombre: string | null;
  fechaActualizacion: string | null;
  fechaEnvio: string | null;
  observaciones: ObservacionDeCampo[];
}

export interface CambioEntreEnviosFestival {
  campoId: string;
  seccionId: string;
  campoEtiqueta: string;
  valorAnterior: string | null;
  valorRecibido: string | null;
  respondeAjusteSugerido: boolean;
}

export interface ComparacionEnviosFestival {
  numeroEnvio: number;
  fechaEnvio: string;
  numeroEnvioAnterior: number | null;
  fechaEnvioAnterior: string | null;
  esPrimerEnvio: boolean;
  comparacionParcial: boolean;
  totalCampos: number;
  camposModificados: number;
  ajustesSugeridosAtendidos: number;
  otrosCambios: number;
  cambios: CambioEntreEnviosFestival[];
}

/** El cuerpo del PUT y del POST. Reemplaza la lista entera: lo que no venga se borra. */
export interface GuardarRevisionSolicitud {
  observacionGeneral: string | null;
  observaciones: {
    ambito: AmbitoDelCampo;
    subregistroId: number | null;
    seccionId: string;
    campoId: string;
    campoEtiqueta: string;
    valorObservado: string | null;
    nota: string;
  }[];
}

/** La carcasa que devuelve el servidor cuando el registro todavía no tiene revisión abierta. */
export function revisionVacia(registroId: string, registroNombre = '', moduloId = 'festivales'): RevisionDeCampos {
  return {
    id: 0,
    moduloId,
    registroId,
    registroNombre,
    estado: 'borrador',
    observacionGeneral: null,
    revisorNombre: null,
    destinatarioNombre: null,
    organizacionNombre: null,
    fechaActualizacion: null,
    fechaEnvio: null,
    observaciones: [],
  };
}

/**
 * El ámbito de un campo, deducido de su identificador.
 *
 * <b>ES UNA REGLA Y NO UNA CONVENCIÓN DE NOMBRES.</b> Todo identificador empieza por `festival.` o
 * por `perfilVersionado.`, y de ahí sale a qué tabla apunta la nota. Se deduce en vez de escribirse porque
 * son cuarenta y siete campos en la plantilla: un atributo más por campo es cuarenta y siete
 * ocasiones de poner el ámbito equivocado, y el equivocado NO falla —cuelga la nota de la tabla de
 * al lado, donde nadie la busca—.
 *
 * Un identificador sin prefijo conocido se trata como del Festival y no se inventa un tercero: es
 * el ámbito que no necesita un perfil versionado abierto, así que es el que menos daño hace si alguien se
 * equivoca al escribirlo.
 */
export function ambitoDelCampo(campoId: string): AmbitoDelCampo {
  return campoId.startsWith('perfilVersionado.') ? 'subregistro' : 'principal';
}

/**
 * La llave con la que se busca la nota de un campo.
 *
 * LLEVA EL PERFIL VERSIONADO DENTRO porque el mismo `campoId` se repite en cada perfil: la nota sobre el correo
 * de un perfil no es la de otro. Es la misma llave que impone
 * `UQ_RevisionesFestivalObservaciones_Campo` en la base.
 */
export function llaveDelCampo(campoId: string, subregistroId: number | null): string {
  return ambitoDelCampo(campoId) === 'subregistro'
    ? `subregistro|${subregistroId}|${campoId}`
    : `festival||${campoId}`;
}
