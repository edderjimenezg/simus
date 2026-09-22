import { Observable } from 'rxjs';
import {
  NotificacionEnVivo,
  PaginaDeNotificacionesEnVivo,
} from '../../core/services/notificaciones-en-vivo.service';
// LOS TIPOS DE LA PROPUESTA SON LOS MISMOS EN LAS DOS PUERTAS, y por eso se importan en vez de
// volver a declararse aquí: el Programa y la organización miran el mismo expediente desde lados
// distintos, y dos copias de la misma forma se separan en cuanto una cambie.
import { PropuestaDeCambioDeMercado } from '../../core/services/mercados.service';

/**
 * El contrato del panel de la organización externa: los tipos que viajan por el cable y la clase
 * abstracta que declara las llamadas.
 *
 * <b>Por qué existe este fichero.</b> Hasta todo esto vivía dentro de
 * `seccion-organizacion.component.ts`, es decir dentro de UNA de las secciones del panel. Cuando
 * la pestaña de Ecosistema y la de Solicitudes necesitaron las mismas llamadas, importarlas
 * habría significado que dos secciones dependieran de una tercera para hablar con el servidor.
 * El contrato se saca a su propio fichero y cada sección lo importa de aquí.
 *
 * <b>Por qué una clase abstracta y no el servicio.</b> El panel se prueba sin red: la prueba
 * inyecta un doble de {@link PanelOrganizacionApi} y no toca HTTP. La implementación de verdad
 * está en `panel-organizacion.service.ts` y la registra la ruta con
 * `{ provide: PanelOrganizacionApi, useExisting: PanelOrganizacionService }`.
 */


/**
 * Un mercado musical de la organización, tal como lo devuelve el espacio externo.
 *
 * <b>ES EL MISMO DTO QUE LEE LA CONSOLA.</b> Los dos canales componen la tarjeta con el mismo
 * ayudante del servidor, así que la organización y el Programa ven exactamente los mismos datos
 * del mismo registro. Un segundo tipo «para el panel» habría divergido en el primer campo nuevo.
 */
export interface MercadoDeLaOrganizacion {
  id: number;
  nombre: string;
  descripcion: string | null;
  alcanceId: number | null;
  alcance: string | null;
  modalidadId: number | null;
  modalidad: string | null;
  periodicidad: string | null;
  periodicidadDetalle: string | null;
  correoMercado: string | null;
  telefonoMercado: string | null;
  sitioWebMercado: string | null;
  instagramMercado: string | null;
  facebookMercado: string | null;
  otroEnlaceMercado: string | null;
  observacionesContacto: string | null;
  nivelCobertura: string;
  codigoDepartamento: string | null;
  nombreDepartamento: string | null;
  codigoMunicipio: string | null;
  nombreMunicipio: string | null;
  lugarEspecifico: string | null;
  seRealizaEnElMarcoDeUnFestival: boolean;
  festivalId: number | null;
  festivalNombre: string | null;
  organizacionId: number;
  organizacionNombre: string | null;
  estadoRegistro: string;
  numeroDeEdiciones: number;
  fechaCreacion: string;
  fechaActualizacion: string | null;
  fechaPublicacion: string | null;
  practicasMusicales: CatalogoDeMercadoExterno[];
  territoriosSonoros: CatalogoDeMercadoExterno[];
}

/** Un valor de los catálogos compartidos del Ecosistema —prácticas musicales, territorios sonoros—. */
export interface CatalogoDeMercadoExterno { id: number; nombre: string; }

/** Lo que el panel manda para crear o guardar un mercado. */
export interface GuardarMercadoSolicitud {
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
  nombre: string;
  descripcion: string | null;
  alcanceId: number | null;
  modalidadId: number | null;
  periodicidad: string | null;
  periodicidadDetalle: string | null;
  correoMercado: string | null;
  telefonoMercado: string | null;
  sitioWebMercado: string | null;
  instagramMercado: string | null;
  facebookMercado: string | null;
  otroEnlaceMercado: string | null;
  observacionesContacto: string | null;
  nivelCobertura: string;
  codigoDepartamento: string | null;
  codigoMunicipio: string | null;
  lugarEspecifico: string | null;
  seRealizaEnElMarcoDeUnFestival: boolean;
  festivalId: number | null;
  organizacionId: number;
}

export interface OpcionDeMercadoExterna { id: number; nombre: string; slug: string; }

export interface CatalogosDeMercadoExternos {
  alcances: OpcionDeMercadoExterna[];
  modalidades: OpcionDeMercadoExterna[];
  practicasMusicales: CatalogoDeMercadoExterno[];
  territoriosSonoros: CatalogoDeMercadoExterno[];
}

/**
 * Una realización concreta de un mercado, tal como la ve su organización.
 *
 * ES EL MISMO DTO QUE LEE LA CONSOLA (`EdicionDeMercadoDto`): los dos canales lo componen con el
 * mismo ayudante del servidor.
 */
export interface EdicionDeMercadoDeLaOrganizacion {
  id: number;
  mercadoId: number;
  anio: number;
  numeroEdicion: number | null;
  nombre: string | null;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  codigoDepartamento: string | null;
  nombreDepartamento: string | null;
  codigoMunicipio: string | null;
  nombreMunicipio: string | null;
  lugarEspecifico: string | null;
  /** El ciclo real del acontecimiento: en_preparacion, programada, realizada, cancelada. */
  estado: string;
  /** Si el público la ve: borrador o publicado. */
  estadoVisibilidad: string;
  fechaCreacion: string;
  fechaActualizacion: string | null;
  fechaPublicacion: string | null;
}

/** Lo que la organización manda para crear o guardar una edición de su mercado. */
export interface GuardarEdicionDeMercadoSolicitud {
  anio: number;
  numeroEdicion: number | null;
  nombre: string | null;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  codigoDepartamento: string | null;
  codigoMunicipio: string | null;
  lugarEspecifico: string | null;
  estado: string;
  estadoVisibilidad: string;
}

/** Un festival de la organización que un mercado puede declarar como marco. */
export interface FestivalElegibleParaMercado {
  id: number;
  nombre: string;
  estadoRegistro: string;
}

/**
 * El perfil de la organización tal como lo devuelve
 * GET /api/v1/externo/organizaciones/{id}/perfil.
 *
 * <b>Por qué está tipado y no como `signal<any>`.</b> La página de acceso externo declara ocho
 * señales `any`; el resultado es que un campo mal escrito —`correoContacto` frente a
 * `contactEmail`— no falla al compilar, falla en pantalla como un hueco en blanco. Estas
 * interfaces son el espejo de los DTO del contrato: si el API cambia un nombre, el fallo aparece
 * en `npm run build` y no en la demostración.
 */
export interface PerfilOrganizacion {
  id: string;
  nombre: string;
  nombreLegal: string | null;
  numeroIdentificacion: string | null;
  tipoIdentificacion: string | null;
  descripcion: string | null;
  correoContacto: string | null;
  telefonoContacto: string | null;
  sitioWeb: string | null;
  facebook: string | null;
  instagram: string | null;
  otroEnlace: string | null;
  direccion: string | null;
  codigoDepartamentoSede?: string | null;
  nombreDepartamentoSede?: string | null;
  codigoMunicipioSede?: string | null;
  nombreMunicipioSede?: string | null;
  estadoRegistro: string;
  estadoRegistroEtiqueta: string;
  fechaActualizacion: string | null;
  /** La foto de perfil, servida por el banco. Nulo significa «sin foto», y entonces van iniciales. */
  fotoUrl?: string | null;
  archivoFotoId?: number | null;
}

/**
 * El cuerpo del PUT del perfil: trece campos y ni uno más.
 *
 * NO HAY `estadoRegistro`, NI `esInstitucional`, NI `activo`, NI `latitud`/`longitud`. Es la
 * primera de las dos barreras contra el cambio de estado: un campo que no existe en el tipo no se
 * rellena por descuido. La segunda barrera está en el servidor, que no asigna esa columna; las
 * transiciones institucionales se gestionan mediante el contrato específico de organizaciones.
 */
export interface PerfilOrganizacionSolicitud {
  nombre: string;
  nombreLegal: string | null;
  numeroIdentificacion: string | null;
  descripcion: string | null;
  correoContacto: string;
  telefonoContacto: string | null;
  sitioWeb: string | null;
  facebook: string | null;
  instagram: string | null;
  otroEnlace: string | null;
  direccion: string | null;
  codigoDepartamentoSede: string;
  codigoMunicipioSede: string;
  /** El archivo del banco que se usa como foto, o `null` para quitarla. */
  archivoFotoId?: number | null;
}

/** La fila de `dbo.EntidadesResponsable`, que es dato personal de un tercero (Ley 1581 de 2012). */
export interface ResponsableOrganizacion {
  idEntidad: string;
  responsableNombre: string;
  responsableTipoDocumento: string;
  responsableTipoDocumentoEtiqueta: string;
  responsableNumeroDocumento: string;
  responsableCorreo: string | null;
  responsableTelefono: string | null;
  responsableDesde: string | null;
  responsableAutorizacionDatos: boolean;
}

/** Los cuatro campos escribibles de la persona responsable. */
export interface ResponsableOrganizacionSolicitud {
  responsableNombre: string;
  responsableTipoDocumento: string;
  responsableNumeroDocumento: string;
  responsableTelefono: string | null;
}

/** Alcances territoriales de un Festival u otro proceso, nunca de la organización que lo administra. */
export const NIVELES_DE_COBERTURA: readonly { codigo: string; etiqueta: string }[] = [
  { codigo: 'nacional', etiqueta: 'Nacional' },
  { codigo: 'departamental', etiqueta: 'Departamental' },
  { codigo: 'municipal', etiqueta: 'Municipal' },
];

/** Una fila de `dbo.TiposDocumento` con Activo = 1, para el desplegable del documento. */
export interface TipoDocumento {
  codigo: string;
  nombre: string;
}

/** Una fila de GET /api/v1/publico/divipola. */
export interface UbicacionDivipola {
  departmentCode: string;
  departmentName: string;
  municipalityCode: string;
  municipalityName: string;
}

/**
 * Un elemento de los catálogos que el Festival lleva colgados (prácticas musicales,
 * territorios sonoros).
 */
export interface CatalogoDelFestival {
  id: number;
  nombre: string;
}

/**
 * Espejo TypeScript de `FestivalBorradorDto`
 * (pnmc-api/src/PNMC.Contracts/ApiContracts.cs-1210), tal como lo devuelve
 * GET /api/v1/externo/organizaciones/{id}/festivales.
 */
export interface FestivalDeLaOrganizacion {
  id: string;
  nombre: string;
  descripcion?: string | null;
  /** Estado del Festival tal como viaja en el contrato externo: PascalCase. */
  estado: string;
  organizacionPrincipalId?: string;
  organizacionPrincipalNombre?: string;
  nivelCobertura?: string;
  codigoDepartamento?: string | null;
  codigoMunicipio?: string | null;
  periodicidad?: string | null;
  periodicidadDetalle?: string | null;
  correoContacto?: string | null;
  practicasMusicales?: CatalogoDelFestival[];
  territoriosSonoros?: CatalogoDelFestival[];
  /**
   * El bloque de contacto de `ART_MUS_FESTIVALES`.
   *
   * LOS NOMBRES SON LOS DEL VOLCADO: `paginaWeb` y no `sitioWeb`, `telefonoCelular` y no
   * `telefono`. Lo pidió el usuario. Las columnas existían en
   * `dbo.Festivales` desde el primer día y ninguna solicitud las llenaba.
   */
  instagram?: string | null;
  facebook?: string | null;
  paginaWeb?: string | null;
  otroEnlace?: string | null;
  telefonoCelular?: string | null;
  observacionesContacto?: string | null;

  /** Último comentario o motivo de rechazo de la revisión institucional del Festival. */
  observacionRevision?: string | null;
  /** Estado de la propuesta de cambios, si la hay. Nombra sus estados en femenino. */
  estadoPropuesta?: string | null;
  /** Último comentario o motivo de rechazo de la revisión de la propuesta. */
  observacionPropuesta?: string | null;

  /**
   * Cuántos campos tienen un cambio pedido sin atender.
   *
   * VIENE EN LA MISMA RESPUESTA QUE LA LISTA, contado por el servidor. Es lo que decide si la
   * tarjeta pinta el aviso y si abrir la ficha vale la pena una petición más: sin este número, la
   * única forma de saberlo sería preguntar por cada Festival de la lista —cuarenta, en la
   * organización 115—.
   */
  cambiosPedidos?: number;

  /**
   * Territorio en nombre, cuántas ediciones tiene y cuándo se actualizó. Agregados el 1 de
   * septiembre de 2026 para que la tarjeta de «Mis procesos» no tenga que pedir nada más por
   * Festival -mismo criterio que ya evitó el N+1 de `cambiosPedidos`-.
   */
  nombreDepartamento?: string | null;
  nombreMunicipio?: string | null;
  cuantasEdiciones?: number;
  fechaActualizacion?: string | null;
  estadoSolicitudRetiro?: string | null;
}

/**
 * Espejo de `PropuestaCambioFestivalDto`
 * (pnmc-api/src/PNMC.Contracts/ApiContracts.cs-1345), la propuesta de cambios activa de un
 * Festival Publicado.
 */
export interface PropuestaCambioFestival {
  id: string;
  festivalOrigenId: string;
  versionOrigenId: string;
  estado: string;
  nombre: string;
  descripcion?: string | null;
  nivelCobertura: string;
  codigoDepartamento?: string | null;
  codigoMunicipio?: string | null;
  periodicidad?: string | null;
  periodicidadDetalle?: string | null;
  correoContacto?: string | null;
  practicasMusicales: CatalogoDelFestival[];
  territoriosSonoros: CatalogoDelFestival[];
  fechaEnvioRevision?: string | null;
  observacionRevision?: string | null;
  instagram?: string | null;
  facebook?: string | null;
  paginaWeb?: string | null;
  otroEnlace?: string | null;
  telefonoCelular?: string | null;
  observacionesContacto?: string | null;
}

/**
 * Una fila de `dbo.Notificaciones` dirigida a quien tiene la sesión abierta, tal como la
 * devuelve GET /api/v1/notificaciones.
 *
 * <b>Esto es el otro extremo del circuito de aprobación.</b> Las escriben cuatro sitios del
 * backend: `FestivalesExternosEndpoints.CrearNotificacionEnvioRevisionAsync` cuando la
 * organización manda su Festival, y `RevisionInstitucionalFestivalesEndpoints` —más sus dos
 * equivalentes de propuestas— cuando el equipo del PNMC decide. Hasta que esta pestaña existió,
 * el sistema registraba el envío como hecho, con fecha, sobre un aviso que nadie podía leer.
 *
 * Los nombres van en camelCase porque así viajan: medido contra la API local el 27 de agosto de
 * 2026 con una sesión externa (HTTP 200, un elemento, `eventType: "FestivalEnviadoARevision"`).
 */
export type NotificacionDelPanel = NotificacionEnVivo;

/** Espejo de `PagedResponse<NotificationDto>`. */
export type PaginaDeNotificaciones = PaginaDeNotificacionesEnVivo;

/**
 * Lo que `ApiClientService` entrega cuando el servidor rechaza.
 *
 * ESTABA ESCRITO COMO `any` EN CINCO SITIOS, y eso es justo lo que mide `trinquete:any`, un
 * techo que solo puede bajar. Con `any`, escribir `fallo.payload.erors` compila y devuelve
 * `undefined`: el formulario se queda sin marcar ni un campo y nadie se entera.
 */
export interface FalloDelServidor {
  /** El codigo HTTP: el panel distingue el 404 de «no hay responsable» del resto. */
  status?: number;
  message?: string;
  payload?: { errors?: Record<string, string[] | string> } | null;
}

export interface EntradaHistorialFestival {
  modulo: 'Festival' | 'Edición'; accion: string; estadoAnterior: string | null; estadoNuevo: string | null;
  comentario: string | null; fecha: string;
}

/**
 * Un cambio que el Programa pidió sobre un campo concreto del mercado.
 *
 * ES EL MISMO TIPO QUE ESCRIBE QUIEN REVISA: el campo, su rótulo, lo que decía cuando se pidió el
 * cambio y la nota. Con dos formas distintas, la segunda se olvida.
 */
export interface ObservacionDeCampoDeMercado {
  id: number;
  ambito: string;
  subregistroId: string | null;
  seccionId: string;
  campoId: string;
  campoEtiqueta: string;
  valorObservado: string | null;
  nota: string;
  /** pendiente | atendida. */
  estado: string;
  fechaAtencion: string | null;
}

/** Los datos públicos de contacto de un mercado, que son lo único corregible una vez publicado. */
export interface ContactoPublicoDeMercadoSolicitud {
  correoMercado: string | null;
  telefonoMercado: string | null;
  sitioWebMercado: string | null;
  instagramMercado: string | null;
  facebookMercado: string | null;
  otroEnlaceMercado: string | null;
  observacionesContacto: string | null;
}

/**
 * Un movimiento de un mercado. LA MISMA FORMA QUE LA DEL FESTIVAL, a propósito: la ficha se lee
 * igual en los dos procesos, y la línea de tiempo es la misma pieza.
 */
export interface EntradaHistorialMercado {
  modulo: 'Mercado' | 'Edición'; accion: string; estadoAnterior: string | null; estadoNuevo: string | null;
  comentario: string | null; fecha: string;
}

/**
 * Las nueve llamadas del panel.
 *
 * QUIEN CABLEE UNA RUTA NUEVA tiene que registrar el proveedor —
 * `{ provide: PanelOrganizacionApi, useExisting: PanelOrganizacionService }`— o la ruta fallará
 * al abrirse con un NullInjectorError. Se prefiere ese fallo ruidoso a una copia local del
 * servicio: dos copias de la misma regla divergen.
 */
export abstract class PanelOrganizacionApi {
  abstract obtenerPerfil(organizacionId: string): Observable<PerfilOrganizacion>;
  abstract guardarPerfil(organizacionId: string, solicitud: PerfilOrganizacionSolicitud): Observable<PerfilOrganizacion>;
  abstract obtenerResponsable(organizacionId: string): Observable<ResponsableOrganizacion>;
  abstract guardarResponsable(organizacionId: string, solicitud: ResponsableOrganizacionSolicitud): Observable<ResponsableOrganizacion>;
  abstract obtenerTiposDocumento(): Observable<TipoDocumento[]>;

  /** GET /api/v1/externo/organizaciones/{id}/festivales — los Festivales de esta organización. */
  abstract obtenerFestivales(organizacionId: string): Observable<FestivalDeLaOrganizacion[]>;
  obtenerHistorialFestival(organizacionId: string, festivalId: string): Observable<EntradaHistorialFestival[]> {
    throw new Error(`El historial del Festival ${festivalId} no está implementado por este adaptador.`);
  }

  /** POST /api/v1/externo/festivales/{id}/enviar-a-revision — mete el Festival en la cola interna. */
  abstract enviarFestivalARevision(festivalId: string): Observable<FestivalDeLaOrganizacion>;

  /** Retira un borrador o un envío todavía no publicado; no borra el historial de auditoría. */
  retirarFestival(festivalId: string): Observable<void> {
    throw new Error(`Retirar Festival ${festivalId} no está implementado por este adaptador.`);
  }

  solicitarRetiroFestival(festivalId: string, _justificacion: string): Observable<void> {
    throw new Error(`Solicitar retiro del Festival ${festivalId} no está implementado por este adaptador.`);
  }

  /**
   * POST /api/v1/externo/festivales/{id}/propuestas-cambio — crea la propuesta activa, o la
   * devuelve tal cual si ya había una (idempotente, lo decide el servidor).
   */
  abstract iniciarPropuesta(festivalId: string): Observable<PropuestaCambioFestival>;

  /** GET /api/v1/externo/festivales/{id}/propuesta-cambio — la propuesta activa, si hay una. */
  abstract obtenerPropuestaActiva(festivalId: string): Observable<PropuestaCambioFestival>;

  /**
   * PUT /api/v1/externo/festivales/{id}/propuesta-cambio — mismos 15 campos que
   * {@link guardarFestival}, 409 si la propuesta ya no admite cambios.
   */
  abstract guardarPropuesta(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<PropuestaCambioFestival>;

  /** POST /api/v1/externo/festivales/{id}/propuesta-cambio/enviar-a-revision. */
  abstract enviarPropuestaARevision(festivalId: string): Observable<unknown>;

  /** GET /api/v1/notificaciones — los avisos dirigidos a quien tiene la sesión abierta. */
  abstract obtenerNotificaciones(): Observable<PaginaDeNotificaciones>;

  /** POST /api/v1/notificaciones/{id}/lectura. */
  abstract marcarNotificacionLeida(notificacionId: string): Observable<NotificacionDelPanel>;

  /** Oculta del buzón los avisos leídos sin eliminar su trazabilidad institucional. */
  ocultarNotificacionesLeidas(): Observable<{ ocultadas: number }> {
    throw new Error('Limpiar avisos es una acción de la cabecera del panel.');
  }

  // ── Lo que el formulario del Festival necesita para pintarse ───────────────────────────────

  /** GET /api/v1/externo/catalogos/festival — los doce catálogos que llenan el formulario. */
  abstract obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival>;

  /** GET /api/v1/publico/divipola — departamentos y municipios. */
  abstract obtenerUbicaciones(): Observable<UbicacionDivipola[]>;

  // AQUI ESTABAN LAS CUATRO LLAMADAS DE `VersionesFestival` —listar, abrir, crear y guardar—, y
  // las cuatro las pedía una pestaña que ningún montaje del componente llegaba a mostrar. Tres de
  // ellas apuntaban además a rutas retiradas: medido contra la API
  // local el 13 de septiembre, `/externo/festivales/{id}/versiones` y `/externo/versiones/{id}`
  // contestan 404. Retiradas ese mismo día. Las Ediciones se administran desde su propia sección,
  // con `AdminService` contra `/externo/ediciones`.

  /** POST /api/v1/externo/organizaciones/{id}/festivales — la cabecera del Festival. */
  abstract crearFestival(organizacionId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion>;


  /** PUT /api/v1/externo/festivales/{id} — 409 si el Festival ya no admite cambios. */
  abstract guardarFestival(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion>;

  /** PUT /api/v1/externo/festivales/{id}/contacto-publico — solo para un Festival publicado. */
  actualizarContactoPublico(festivalId: string, _solicitud: ContactoPublicoFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    throw new Error(`Actualizar el contacto público del Festival ${festivalId} no está implementado por este adaptador.`);
  }

  // ── Mercados musicales ──────────────────────────────────────────────────────────────────────
  //
  // LLEVAN IMPLEMENTACION POR OMISION QUE LANZA, y no `abstract`, por la misma razón que
  // `obtenerHistorialFestival`: los dobles de prueba implementan esta clase, y un método abstracto
  // nuevo los rompe todos a la vez sin que ninguno tenga nada que ver con mercados.

  /** GET /api/v1/externo/organizaciones/{id}/mercados. */
  obtenerMercados(organizacionId: string): Observable<MercadoDeLaOrganizacion[]> {
    throw new Error(`Los mercados de la organización ${organizacionId} no están implementados por este adaptador.`);
  }

  /** GET /api/v1/externo/mercados/{id}. */
  obtenerMercado(mercadoId: string): Observable<MercadoDeLaOrganizacion> {
    throw new Error(`El mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** GET /api/v1/externo/catalogos/mercado — alcance y modalidad, el mismo vocabulario que la consola. */
  obtenerCatalogosDeMercado(): Observable<CatalogosDeMercadoExternos> {
    throw new Error('Los catálogos de mercado no están implementados por este adaptador.');
  }

  /** GET /api/v1/externo/organizaciones/{id}/festivales-elegibles — acotado por pertenencia, no por estado. */
  obtenerFestivalesElegibles(organizacionId: string): Observable<FestivalElegibleParaMercado[]> {
    throw new Error(`Los festivales elegibles de ${organizacionId} no están implementados por este adaptador.`);
  }

  /** POST /api/v1/externo/organizaciones/{id}/mercados — nace en borrador. */
  crearMercado(organizacionId: string, _solicitud: GuardarMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    throw new Error(`Crear un mercado en ${organizacionId} no está implementado por este adaptador.`);
  }

  /** PUT /api/v1/externo/mercados/{id} — 409 si el mercado ya no admite cambios. */
  guardarMercado(mercadoId: string, _solicitud: GuardarMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    throw new Error(`Guardar el mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** POST /api/v1/externo/mercados/{id}/enviar-a-revision. */
  enviarMercadoARevision(mercadoId: string): Observable<MercadoDeLaOrganizacion> {
    throw new Error(`Enviar a revisión el mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /**
   * DELETE /api/v1/externo/mercados/{id} — deshace un envío o abandona un borrador.
   *
   * NO SON LO MISMO Y EL SERVIDOR NO HACE LO MISMO: retirar de revisión devuelve el mercado a
   * borrador —el trabajo se conserva— y abandonar un borrador lo archiva.
   */
  retirarMercado(mercadoId: string): Observable<void> {
    throw new Error(`Retirar el mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** POST /api/v1/externo/mercados/{id}/solicitudes-retiro — lo publicado lo retira el Programa. */
  solicitarRetiroDeMercado(mercadoId: string, _justificacion: string): Observable<void> {
    throw new Error(`Solicitar el retiro del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** PUT /api/v1/externo/mercados/{id}/contacto-publico — la corrección menor de lo publicado. */
  actualizarContactoDeMercado(mercadoId: string, _solicitud: ContactoPublicoDeMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    throw new Error(`Actualizar el contacto del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /**
   * GET /api/v1/externo/mercados/{id}/cambios-pedidos — lo que el Programa pidió corregir.
   *
   * SOLO LO ENVIADO: un borrador que el funcionario está escribiendo no es una petición, y
   * enseñarlo haría corregir cosas que todavía nadie ha pedido.
   */
  cambiosPedidosDeMercado(mercadoId: string): Observable<ObservacionDeCampoDeMercado[]> {
    throw new Error(`Los cambios pedidos del mercado ${mercadoId} no están implementados por este adaptador.`);
  }

  /** POST /api/v1/externo/mercados/cambios-pedidos/{id}/atender — se marca y se desmarca. */
  atenderCambioPedidoDeMercado(observacionId: number, _atendida: boolean): Observable<ObservacionDeCampoDeMercado> {
    throw new Error(`Atender el cambio ${observacionId} no está implementado por este adaptador.`);
  }

  /**
   * GET /api/v1/externo/mercados/{id}/propuesta — la propuesta de cambio viva, si la hay.
   *
   * <b>CON `id` EN CERO NO HAY NINGUNA.</b> El servidor devuelve una carcasa vacía en vez de un 404,
   * por lo mismo que en los cambios pedidos: la pantalla se pinta igual en los dos casos.
   */
  propuestaDeMercado(mercadoId: string): Observable<PropuestaDeCambioDeMercado> {
    throw new Error(`La propuesta del mercado ${mercadoId} no está implementada por este adaptador.`);
  }

  /** PUT /api/v1/externo/mercados/{id}/propuesta — el borrador entero, con semántica de reemplazo. */
  guardarPropuestaDeMercado(
    mercadoId: string, _motivo: string | null, _campos: { campoId: string; valorPropuesto: string | null }[],
  ): Observable<PropuestaDeCambioDeMercado> {
    throw new Error(`Guardar la propuesta del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** POST /api/v1/externo/mercados/{id}/propuesta/enviar — y aquí se congela el «antes». */
  enviarPropuestaDeMercado(mercadoId: string): Observable<PropuestaDeCambioDeMercado> {
    throw new Error(`Enviar la propuesta del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** DELETE /api/v1/externo/mercados/{id}/propuesta — abandonar el borrador, nunca lo ya enviado. */
  abandonarPropuestaDeMercado(mercadoId: string): Observable<void> {
    throw new Error(`Abandonar la propuesta del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** GET /api/v1/externo/organizaciones/{id}/mercados/{id}/historial — los movimientos del mercado. */
  obtenerHistorialMercado(organizacionId: string, mercadoId: string): Observable<EntradaHistorialMercado[]> {
    throw new Error(`El historial del mercado ${mercadoId} de ${organizacionId} no está implementado por este adaptador.`);
  }

  // ── Las ediciones de un mercado, desde su organización. La ruta existía desde el primer día y
  //    solo la usaba la consola; la pantalla llegó. ──

  /** GET /api/v1/externo/mercados/{id}/ediciones. */
  obtenerEdicionesDeMercado(mercadoId: string): Observable<EdicionDeMercadoDeLaOrganizacion[]> {
    throw new Error(`Las ediciones del mercado ${mercadoId} no están implementadas por este adaptador.`);
  }

  /** POST /api/v1/externo/mercados/{id}/ediciones — 409 si el mercado no está publicado o el año ya existe. */
  crearEdicionDeMercado(mercadoId: string, _solicitud: GuardarEdicionDeMercadoSolicitud): Observable<EdicionDeMercadoDeLaOrganizacion> {
    throw new Error(`Crear una edición del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /**
   * POST /api/v1/externo/mercados/{id}/ediciones/{edicionId}/{publicar|despublicar|archivar}.
   *
   * <b>CADA CAMBIO DE VISIBILIDAD TIENE SU PUERTA, Y NO ES UN GUARDADO.</b> Antes se hacía
   * reenviando la edición entera con otro `estadoVisibilidad`: funcionaba, pero se saltaba las
   * reglas —publicar la edición de un mercado que no está publicado, reabrir una archivada— y
   * quedaba en la bitácora como «guardar», así que el historial no podía decir qué pasó.
   */
  cambiarVisibilidadDeEdicionDeMercado(
    mercadoId: string, edicionId: number, _accion: 'publicar' | 'despublicar' | 'archivar'): Observable<EdicionDeMercadoDeLaOrganizacion> {
    throw new Error(`Cambiar la visibilidad de la edición ${edicionId} no está implementado por este adaptador.`);
  }

  /** DELETE /api/v1/externo/mercados/{id}/ediciones/{edicionId} — solo lo que nunca se publicó. */
  eliminarEdicionDeMercado(mercadoId: string, edicionId: number): Observable<void> {
    throw new Error(`Eliminar la edición ${edicionId} del mercado ${mercadoId} no está implementado por este adaptador.`);
  }

  /** PUT /api/v1/externo/mercados/{id}/ediciones/{edicionId}. */
  guardarEdicionDeMercado(mercadoId: string, edicionId: number, _solicitud: GuardarEdicionDeMercadoSolicitud): Observable<EdicionDeMercadoDeLaOrganizacion> {
    throw new Error(`Guardar la edición ${edicionId} del mercado ${mercadoId} no está implementado por este adaptador.`);
  }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════
// AQUI VIVIA EL CONTRATO DE `VersionesFestival`, Y SE RETIRO EL 13 DE SEPTIEMBRE DE 2026
// ═══════════════════════════════════════════════════════════════════════════════════════════
//
// Eran nueve tipos —`ResumenDeVersion`, `FichaDeLaVersion`, `GuardarVersionSolicitud` y las seis
// formas de sus tablas hijas— y los usaba una sola pantalla: la pestaña «Ediciones» de
// `FichaFestivalComponent`, que ningún montaje del componente llegaba a mostrar.
//
// QUE SIGUE EXISTIENDO Y QUE NO. `dbo.VersionesFestival` sigue ahí y sigue haciendo falta: es el
// historial del perfil público y la base sobre la que se arma una propuesta de cambios. Lo que se
// retira es su FORMULARIO EXTERNO, coherente con la decisión —«la
// tabla persiste para historial y propuestas, nunca como formulario externo de una Edición»—.
//
// DONDE ESTA AHORA LO QUE ESTO EDITABA. En `admin.service.ts`, con `EdicionDeFestival`,
// `EdicionDeFestivalDetalle` y sus tres solicitudes hijas, contra `/externo/ediciones`. Ese
// contrato sí tiene pantalla: `ediciones-temporales.component.ts` la lista y
// `ficha-edicion-festival.component.ts` la edita, con las mismas ocho secciones.

/**
 * Los doce catálogos de GET /api/v1/externo/catalogos/festival.
 *
 * SON DOCE Y VIAJAN JUNTOS. El formulario los necesita TODOS a la vez para poder pintarse: doce
 * peticiones para llenar un formulario son doce veces la latencia y doce sitios donde fallar a
 * medias. Las claves son las del objeto anónimo que devuelve
 * `FestivalesExternosEndpoints.cs-116`; un nombre mal escrito aquí deja un desplegable vacío
 * sin que nada falle, y por eso están tipadas y no leídas como `any`.
 */
export interface CatalogosDelFestival {
  practicasMusicales: CatalogoDelFestival[];
  territoriosSonoros: CatalogoDelFestival[];
  tipologias: CatalogoDelFestival[];
  expresionesArtisticas: CatalogoDelFestival[];
  fuentesFinanciacion: CatalogoDelFestival[];
  modalidadesParticipacion: CatalogoDelFestival[];
  naturalezasEntidad: CatalogoDelFestival[];
  tiposIngreso: CatalogoDelFestival[];
  tiposOrganizador: CatalogoDelFestival[];
  zonasUrbanoRural: CatalogoDelFestival[];
  titulacionesColectivas: CatalogoDelFestival[];
  regionesOcad: CatalogoDelFestival[];
}

/**
 * Lo que se escribe de la CABECERA del Festival: `dbo.Festivales`, que es `ART_MUS_FESTIVALES` del
 * volcado de SIMUS.
 *
 * <b>ES OTRA FILA QUE EL PERFIL VERSIONADO, y por eso otra solicitud.</b> La cabecera identifica
 * el proceso y su organización administradora; el perfil versionado contiene los datos públicos
 * sujetos a revisión. El formulario actual aún agrupa campos heredados que deben migrarse al
 * contrato anual de edición en un bloque posterior; no debe usarse esa convivencia temporal como
 * justificación para volver a duplicar contactos u organización.
 *
 * <b>LO QUE NO LLEVA.</b> No hay `estadoRegistro` ni `activo`: el estado lo mueve la revisión
 * institucional. Es la misma guarda que en {@link PerfilOrganizacionSolicitud}.
 *
 * <b>`nivelCobertura`, `codigoDepartamento`, `codigoMunicipio` y `periodicidad` son nuestros</b>, no
 * del volcado. Los exige `CK_Festivales_NivelCobertura`, que comprueba la coherencia entre el nivel
 * y el territorio: nacional con departamento y municipio nulos, departamental con departamento y
 * sin municipio, municipal con los dos.
 */
export interface GuardarFestivalSolicitud {
  /** Borrador privado que originó el alta. Solo viaja al crear y permite cerrarlo al completar el registro. */
  borradorProcesoId?: string | null;
  /** `NOMBRE_FESTIVAL`. Lo único que el servidor exige para que el Festival exista. */
  nombre: string;
  /** `DESCRIPCION_FESTIVAL`. */
  descripcion: string | null;
  /** `CORREO_CONTACTO` de la cabecera. */
  correoContacto: string | null;
  /** `CELULAR` de la cabecera. */
  telefonoCelular: string | null;
  /** `INSTAGRAM`. */
  instagram: string | null;
  /** `FACEBOOK`. */
  facebook: string | null;
  /** `PAGINA_WEB`. */
  paginaWeb: string | null;
  /** `OTRO_ENLACE`. */
  otroEnlace: string | null;
  /** `OBSERVACIONES_CONTACTO` de la cabecera. */
  observacionesContacto: string | null;

  periodicidad: string | null;
  periodicidadDetalle?: string | null;
  nivelCobertura: string;
  codigoDepartamento: string | null;
  codigoMunicipio: string | null;

  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
}

/** Los seis datos menores que pueden actualizarse en un Festival publicado. */
export interface ContactoPublicoFestivalSolicitud {
  correoContacto: string | null;
  telefonoCelular: string | null;
  instagram: string | null;
  facebook: string | null;
  paginaWeb: string | null;
  otroEnlace: string | null;
}

/**
 * Un proceso de la organización que puede enmarcar un evento.
 *
 * <b>HOY SOLO HAY FESTIVALES.</b> Los demás procesos del ecosistema —mercados, redes, escuelas—
 * todavía no tienen su modelo. `tipo` existe desde ahora para que el día que existan no haya que
 * cambiar ni el contrato ni la pantalla.
 */
export interface ProcesoQueEnmarca {
  id: string;
  tipo: string;
  tipoEtiqueta: string;
  nombre: string;
  territorio: string | null;
}

export interface EventoDeLaOrganizacion {
  id: string;
  titulo: string;
  fechaInicio: string;
  fechaFin: string | null;
  modalidad: string;
  lugar: string | null;
  estado: string;
  estadoEtiqueta: string;
  procesoNombre: string | null;
  fechaActualizacion: string;
}

/**
 * Lo que la organización envía para anunciar un evento.
 *
 * NO LLEVA ESTADO. El evento nace siempre en revisión: quien lo escribe no decide si se publica.
 */
export interface AnunciarEventoSolicitud {
  titulo: string;
  descripcion: string;
  descripcionLarga?: string | null;
  fechaInicio: string;
  fechaFin?: string | null;
  horaInicio?: string | null;
  horaFin?: string | null;
  modalidad: string;
  lugar?: string | null;
  codigoDepartamento?: string | null;
  codigoMunicipio?: string | null;
  url?: string | null;
  /** El proceso en el que se enmarca. Es obligatorio y es la regla entera. */
  festivalId: number;
}
