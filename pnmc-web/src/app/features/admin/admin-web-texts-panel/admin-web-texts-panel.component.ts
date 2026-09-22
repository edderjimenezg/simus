import { Component, Input, signal, computed, inject, effect, input, viewChild, ElementRef } from '@angular/core';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { 
  LucideCheckCircle2,
  LucideAlertCircle,
  LucideUpload,
  LucideTrash2,
  LucideUserRound,
  LucidePlus,
  LucideChevronDown,
  LucideExternalLink
} from '@lucide/angular';
import {
  DEFAULT_TEXTS,
  WEB_TEXT_GROUPS,
  WEB_TEXT_KEY_INDEX,
  WEB_TEXT_SECTIONS,
  MiembroDelEquipoWeb,
  TextosWebService,
} from '../../../core/services/textos-web.service';
import { TopesDeLaNomina, ContenidoWebApiService, EntradaDeHistorialDeContenidoWeb } from '../../../core/services/contenido-web-api.service';
import { IMAGENES_DEL_BLOQUE } from '../../../core/cms/registro-de-imagenes-web';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import {
  BloqueDeImagenesComponent,
  ImagenEnVivo,
} from '../admin-web-media-panel/bloque-de-imagenes.component';

/**
 * La pagina publica donde se ve cada seccion del panel.
 *
 * La previsualizacion de la derecha dibuja el contenido a mano y solo del grupo
 * abierto: sirve para ver una frase mientras se escribe, no para ver como queda
 * la pagina. «Ir a la pagina» abre lo real.
 *
 * La regla es por seccion y no por grupo porque una seccion del panel es
 * exactamente una pagina del portal. Los grupos son partes de esa pagina.
 */
const PAGINA_DE_LA_SECCION: Record<string, string> = {
  'Home': '/',
  'Sobre PNMC': '/pnmc',
  'Mapa Ecosistémico': '/mapa-ecosistemico',
  'Ejes': '/ejes',
  'Estrategias': '/estrategia/circulacion',
  'Ecosistema': '/ecosistema',
  'Acceso externo': '/registro',
  // Cabecera y pie salen en todas las paginas; la portada vale como cualquiera.
  'Navegación y Footer': '/',
};

/**
 * Los grupos cuyo contenido NO se ve en la pagina de su seccion.
 *
 * Se nombran uno por uno a proposito: una excepcion declarada es revisable, y
 * la alternativa —adivinar la ruta desde el id del grupo— produciria enlaces
 * plausibles que llevan a una pagina donde el texto no aparece.
 */
const PAGINA_DEL_GRUPO: Record<string, string> = {
  // La pagina de error no tiene ruta propia: se llega a ella justamente por no
  // acertar ninguna. Cualquier ruta inexistente sirve, y esta lo dice en voz alta.
  general_404: '/esta-ruta-no-existe',

  // El grupo contiene solo textos del directorio de festivales, no de la portada
  // general del Ecosistema.
  ecosistema_directorios: '/ecosistema/festivales',

  // La ficha de componente vive en `/ejes/:componente`: el segmento público es estable y legible.
  // pagina que abrir. Se usa el primer componente del primer eje, que existe en
  // `ejes-data.config.ts`. Es una eleccion, no una solucion: los textos de este grupo son los
  // mismos en las once fichas, asi que cualquiera sirve para verlos.
  component_detail: '/ejes/apropiacion-y-derechos',

  // «Estrategias» tiene dos paginas y la regla por seccion apunta a la de Celebra la Musica.
  // Sin esta linea, el boton «Ir a la pagina» de Territorios Sonoros abria la estrategia de al
  // lado: la editora publicaba, miraba, y no veia su texto.
  strategy_territorios_details: '/estrategia/investigacion',
};

/** Un sitio concreto de una pagina donde mirar el contenido de un grupo. */
export interface EncuadreDePrevisualizacion {
  /** Rotulo de la pastilla cuando el grupo tiene mas de un encuadre. */
  etiqueta: string;
  ruta: string;
  /**
   * Selector CSS del bloque a encuadrar dentro de esa pagina. Cadena vacia
   * significa «la pagina entera desde arriba»: se usa cuando el contenido del
   * grupo esta repartido y no hay un bloque que lo contenga.
   */
  ancla: string;
  /**
   * Lo que hay que pulsar para que el bloque exista, en orden.
   *
   * <b>ERA UN SOLO SELECTOR Y SE QUEDO CORTO.</b> El recorrido guiado del geovisor pasó a estar a
   * dos clics —el botón de ayuda abre el modo explorar, y desde su barra se entra al recorrido—,
   * así que con un único disparador la previsualización encuadraba una pantalla donde el diálogo
   * todavía no existía. Una cadena suelta sigue valiendo: es el caso de un solo paso.
   */
  preparar?: string | readonly string[];
  nota?: string;
}

/**
 * DONDE MIRAR CADA GRUPO. Es la tabla que sostiene la previsualizacion.
 *
 * Hasta la previsualizacion era un dibujo: HTML escrito
 * a mano dentro del panel que imitaba ocho secciones. Los otros veintiocho
 * grupos veian un recuadro generico. Ademas de no parecerse, el dibujo envejecia
 * solo: cambiar la portada no cambiaba su copia.
 *
 * Ahora se carga la pagina real en un marco de 1440x810 y se desplaza hasta
 * aqui. La tabla es explicita y no derivada del id del grupo, por el mismo
 * motivo que `PAGINA_DEL_GRUPO`: adivinar produce encuadres plausibles que
 * llevan a un sitio donde el texto no esta, y eso se lee como «el panel no
 * funciona».
 *
 * LAS ANCLAS SON ids O data-testid QUE EXISTEN EN LA PLANTILLA, nunca
 * `section:nth-of-type(N)`: la posicion se rompe en silencio cuando alguien
 * inserta una seccion. Las ocho de `/ecosistema` se anadieron a proposito para
 * esta tabla, porque esa pagina no tenia ni un id.
 */
export const ENCUADRES: Record<string, EncuadreDePrevisualizacion[]> = {
  // Sobre PNMC. Su grupo de encabezado toca dos sitios: el hero de arriba y el
  // bloque de presentacion. Con un solo encuadre, `about_hero_tag`, `_title`,
  // `_accent` y `about_description` quedaban fuera de la vista.
  about_hero_presentation: [
    { etiqueta: 'Encabezado', ruta: '/pnmc', ancla: 'app-page-hero' },
    { etiqueta: 'Presentación', ruta: '/pnmc', ancla: '#pnmc-presentacion' },
  ],
  about_objectives: [{ etiqueta: 'Objetivos', ruta: '/pnmc', ancla: '#pnmc-objetivos' }],
  about_approaches: [{ etiqueta: 'Enfoques', ruta: '/pnmc', ancla: '[data-testid="approaches-banner"]' }],
  about_actors: [{ etiqueta: 'Actores', ruta: '/pnmc', ancla: '#pnmc-actores' }],
  about_timeline: [{ etiqueta: 'Hitos', ruta: '/pnmc', ancla: '#pnmc-hitos' }],
  about_normative: [{ etiqueta: 'Marco normativo', ruta: '/pnmc', ancla: '#pnmc-marco' }],
  about_team: [{ etiqueta: 'Equipo', ruta: '/pnmc', ancla: '#pnmc-equipo' }],

  // Home.
  home_hero: [{
    etiqueta: 'Portada',
    ruta: '/',
    ancla: 'app-page-hero',
    // El hero de la portada va con [fullScreen]="true" (home.component.html:10)
    // y `page-hero` le aplica `h-[100svh]`: ocupa el alto entero del marco. Es
    // el unico grupo del catalogo donde no se puede ver un trozo de lo
    // siguiente, y no es un defecto del encuadre sino de la pagina.
    nota: 'El encabezado ocupa la pantalla completa: no queda sitio para la sección siguiente.',
  }],
  home_about: [{ etiqueta: 'Identidad', ruta: '/', ancla: 'app-pnmc-preview-section' }],
  home_ejes: [{ etiqueta: 'Ejes', ruta: '/', ancla: 'app-pnmc-preview-section' }],
  home_bulletin: [{ etiqueta: 'Boletín', ruta: '/', ancla: '[data-testid="home-bulletin-banner"]' }],
  home_strategies_title: [{ etiqueta: 'Carrusel', ruta: '/', ancla: 'app-home-strategies-section' }],
  home_banner: [{ etiqueta: 'Banner', ruta: '/', ancla: 'app-home-media-banner' }],
  // `#mapa-home` es el id del `app-content-wrapper` del bloque
  // (mapa-ecosistemico-preview.component.html:1), no un `nth-of-type`.
  home_ecosistema: [{ etiqueta: 'Ecosistema', ruta: '/', ancla: '#mapa-home' }],
  home_strategies_cards: [{ etiqueta: 'Tarjetas', ruta: '/', ancla: 'app-home-strategies-section' }],

  // El tutorial del geovisor no esta en la pagina hasta que se abre. El boton
  // que lo abre es `handleTogglePanel`, es decir un INTERRUPTOR: pulsarlo dos
  // veces lo cierra. Por eso `preparar` se pulsa una sola vez por carga.
  // El ancla se afina con el `aria-label` porque `/mapa` tiene dos dialogos:
  // este y el de «Procesos registrados en...» (:844).
  // DOS CLICS DESDE EL 13 DE SEPTIEMBRE DE 2026: el botón de ayuda abre el modo explorar —que es
  // lo que se quiere al tener UNA duda— y el recorrido completo se entra desde su barra.
  map_tutorial: [{
    etiqueta: 'Recorrido',
    ruta: '/mapa-ecosistemico',
    ancla: '[role="dialog"][aria-label="Tutorial del geovisor ecosistémico"]',
    preparar: ['[aria-label="Abrir el tutorial del geovisor"]', '[data-abrir-el-recorrido]'],
    nota: 'El recorrido se abre solo para la previsualización.',
  }],

  map_explorar: [{
    etiqueta: 'Explorar',
    ruta: '/mapa-ecosistemico',
    ancla: '[data-barra-de-explorar]',
    preparar: '[aria-label="Abrir el tutorial del geovisor"]',
    nota: 'El modo explorar se abre solo para la previsualización. Los textos de cada apartado se ven al señalarlo.',
  }],

  // El encabezado de /ejes. Sus cuatro textos estuvieron escritos a mano en la plantilla hasta
  //, mientras la foto de esa misma portada ya era administrable.
  ejes_hero: [{ etiqueta: 'Portada', ruta: '/ejes', ancla: 'app-page-hero' }],

  // La ficha de componente. Se encuadra desde arriba: sus cinco claves estan repartidas entre
  // dos bloques que van al final de la pagina y el mensaje de componente inexistente, que solo
  // aparece con un identificador que no existe.
  component_detail: [{
    etiqueta: 'Ficha',
    ruta: '/ejes/apropiacion-y-derechos',
    ancla: '',
    nota: 'Se muestra el primer componente: los textos de este bloque son los mismos en las once fichas.',
  }],

  // Ejes. Cada eje tiene su seccion en /ejes; los rotulos se repiten en el menu
  // desplegable de la portada, que es el segundo encuadre.
  eje1_details: [
    { etiqueta: 'En /ejes', ruta: '/ejes', ancla: '#musica-para-la-vida' },
    { etiqueta: 'En la portada', ruta: '/', ancla: 'app-pnmc-preview-section' },
  ],
  eje2_details: [
    { etiqueta: 'En /ejes', ruta: '/ejes', ancla: '#oficios-y-practicas' },
    { etiqueta: 'En la portada', ruta: '/', ancla: 'app-pnmc-preview-section' },
  ],
  eje3_details: [
    { etiqueta: 'En /ejes', ruta: '/ejes', ancla: '#gobernanza' },
    { etiqueta: 'En la portada', ruta: '/', ancla: 'app-pnmc-preview-section' },
  ],

  strategy_celebra_details: [{ etiqueta: 'Celebra la Música', ruta: '/estrategia/circulacion', ancla: '' }],
  strategy_territorios_details: [{ etiqueta: 'Territorios Sonoros', ruta: '/estrategia/investigacion', ancla: '' }],
  access_external_portal: [{ etiqueta: 'Acceso', ruta: '/registro', ancla: '' }],

  // Ecosistema.
  ecosistema_hero: [{ etiqueta: 'Encabezado', ruta: '/ecosistema', ancla: 'app-page-hero' }],
  ecosistema_about: [{ etiqueta: 'Qué reúne', ruta: '/ecosistema', ancla: '#eco-que-reune' }],
  ecosistema_explore: [{ etiqueta: 'Consulta', ruta: '/ecosistema', ancla: '#eco-explorar' }],
  ecosistema_map: [{ etiqueta: 'Vista integrada', ruta: '/ecosistema', ancla: '#eco-mapa' }],
  ecosistema_categories: [{ etiqueta: 'Procesos', ruta: '/ecosistema', ancla: '#eco-procesos' }],
  ecosistema_participate: [{ etiqueta: 'Participar', ruta: '/ecosistema', ancla: '#eco-participar' }],
  ecosistema_directorios: [{ etiqueta: 'Festivales', ruta: '/ecosistema/festivales', ancla: '' }],

  general_404: [{ etiqueta: 'Página de error', ruta: '/esta-ruta-no-existe', ancla: '' }],
  // VA SOBRE LA PORTADA, igual que `PAGINA_DE_LA_SECCION`, y conviene dejar
  // escrito por que, porque el codigo invita a concluir lo contrario:
  // `showGlobalFooter()` (app.component.ts) devuelve false para la portada, y
  // de ahi se deduce facilmente que en `/` no hay pie. Si lo hay: la portada
  // pinta el suyo propio en home.component.html:227, y esa exclusion existe
  // justamente para que no salgan dos.
  general_nav_footer: [
    { etiqueta: 'Menú', ruta: '/', ancla: 'app-navigation' },
    { etiqueta: 'Pie de página', ruta: '/', ancla: 'app-footer' },
  ],
};

/**
 * LA SECCION QUE ABRE EL PANEL, Y EL PRIMER BLOQUE DE ESA SECCION.
 *
 * Se derivan del registro y no se escriben a mano. Hasta eran tres
 * literales en tres sitios —`selectedSection`, `selectedGroup` y `bloqueAbierto`—, atados solo
 * por que alguien se acordara; ya se rompio una vez, con el acordeon estrenandose plegado.
 */
export const SECCION_INICIAL = WEB_TEXT_SECTIONS[0].section;

/** El id del primer grupo de una seccion, o cadena vacia si la seccion no tiene ninguno. */
export function primerGrupoDe(seccion: string): string {
  return WEB_TEXT_GROUPS.find((grupo) => grupo.section === seccion)?.id ?? '';
}

/** Ancho logico del marco: un PC de escritorio, que es lo que se pidio ver. */
export const ANCHO_LOGICO = 1440;
/** Alto logico. 1440x810 es 16:9, que es lo que hace la vista apaisada. */
export const ALTO_LOGICO = 810;

/**
 * LOS TRES TAMANOS EN QUE SE PUEDE MIRAR LA PAGINA.
 *
 * <b>Por que hay tres y no solo el escritorio.</b> Medido sobre una
 * pantalla de 1440: con el estudio a dos columnas, el hueco de la previsualizacion da 613 px, y
 * 1440 logicos ahi se reducen a escala 0,40 —un texto de 16 px se pinta a 6,4 y no se lee—. A
 * 110 % de zoom baja a 0,35. El problema no es el diseno de dos columnas: es que 1440 no cabe.
 *
 * Con «Tableta» esos mismos 613 px dan 0,60, y con «Telefono» 1,00. Ademas resuelve algo que no
 * se podia hacer de ninguna manera: mirar como queda la portada en un telefono.
 *
 * Los tres tamanos son reales: 1440x810 es el portatil comun; 1024x768, una tableta apaisada;
 * 390x844, un telefono de gama media. No son numeros redondos elegidos por bonitos.
 */
export interface TamanoDeVista {
  id: 'escritorio' | 'tableta' | 'telefono';
  etiqueta: string;
  ancho: number;
  alto: number;
}

export const TAMANOS_DE_VISTA: TamanoDeVista[] = [
  { id: 'escritorio', etiqueta: 'Escritorio', ancho: ANCHO_LOGICO, alto: ALTO_LOGICO },
  { id: 'tableta', etiqueta: 'Tableta', ancho: 1024, alto: 768 },
  { id: 'telefono', etiqueta: 'Teléfono', ancho: 390, alto: 844 },
];

/**
 * Rutas que el marco puede cargar.
 *
 * Rechaza la consola: un marco que cargue `/admin` monta un segundo panel
 * dentro del primero, con su propia sesion y sus propias peticiones.
 */
export function rutaSegura(ruta: string | null | undefined): string | null {
  if (!ruta || !ruta.startsWith('/')) { return null; }
  if (ruta === '/admin' || ruta.startsWith('/admin/')) { return null; }
  if (ruta === '/administracion' || ruta.startsWith('/administracion/')) { return null; }
  return ruta;
}

/**
 * A que altura poner el marco para ver el bloque MAS su contexto.
 *
 * Funcion pura y exportada para poder probarla sin DOM. Dos casos:
 *  - el bloque cabe en el marco: se centra, y lo que sobra arriba y abajo son
 *    justamente los trozos de la seccion anterior y de la siguiente;
 *  - el bloque es mas alto que el marco: no hay forma de ver las dos vecinas, y
 *    se prefiere ensenar 120 px de la anterior para que se note donde empieza.
 */
export function calcularEncuadre(
  seccion: { top: number; alto: number },
  altoDePagina: number,
  altoDelMarco: number = ALTO_LOGICO,
): number {
  const sobra = altoDelMarco - seccion.alto;
  const deseado = sobra > 0 ? seccion.top - sobra / 2 : seccion.top - 120;
  const maximo = Math.max(0, altoDePagina - altoDelMarco);
  return Math.round(Math.min(Math.max(deseado, 0), maximo));
}

@Component({
  selector: 'app-admin-web-texts-panel',
  standalone: true,
  imports: [
    EstadoDeListaComponent,IndicadorDeEstadoComponent, SelectorSegmentadoComponent, BotonComponent, 
    CommonModule,
    LucideCheckCircle2,
    LucideAlertCircle,
    LucideUpload,
    LucideTrash2,
    LucideUserRound,
    LucidePlus,
    LucideChevronDown,
    LucideExternalLink,
    BloqueDeImagenesComponent
  ],
  templateUrl: './admin-web-texts-panel.component.html',
  styleUrls: ['./admin-web-texts-panel.component.css']
})
export class AdminWebTextsPanelComponent {
  private webTextsService = inject(TextosWebService);
  private api = inject(ContenidoWebApiService);
  private sanitizer = inject(DomSanitizer);

  /**
   * La ficha que identifica a ESTA pestana del panel ante el marco.
   *
   * Viaja en la direccion como `?pnmcVista=` y vuelve en cada mensaje. Sin ella,
   * dos pestanas del panel abiertas a la vez se pisarian los borradores del
   * marco de la otra.
   */
  private readonly fichaDeLaVista = `v${Date.now().toString(36)}`;

  private readonly marcoPrevisualizacion = viewChild<ElementRef<HTMLIFrameElement>>('marcoPrevisualizacion');
  private readonly lienzoPrevisualizacion = viewChild<ElementRef<HTMLElement>>('lienzoPrevisualizacion');

  /** Grupo cargado desde la API, con su version por clave para la concurrencia. */
  private groupVersions = signal<Record<string, number>>({});
  /** Version de la nomina; el servidor la exige y sin ella responde 400. */
  private teamVersion = signal<number | null>(null);

  /**
   * Los topes de la nomina, tal como los publica el servidor.
   *
   * Estos valores son solo el punto de partida hasta que llega la respuesta de
   * `GET /admin/equipo-web`, que los trae. Son los mismos que declara la API para
   * que el formulario no quede mas estrecho que ella mientras carga.
   */
  readonly topesNomina = signal<TopesDeLaNomina>({
    maxMembers: 40,
    maxPhotoChars: 300_000,
    maxSerializedChars: 2_000_000,
    maxNameLength: 120,
    maxRoleLength: 160,
    maxEmailLength: 180,
  });
  loadingGroup = signal(false);
  savingGroup = signal(false);
  /** Grupos ya traidos del servidor, para no repetir la peticion al volver. */
  private loadedGroups = new Set<string>();
  /** Historial por clave del grupo abierto (2E). */
  private historyByKey = signal<Record<string, EntradaDeHistorialDeContenidoWeb[]>>({});

  @Input() set enabled(val: boolean) {
    this._enabled.set(val);
  }
  _enabled = signal<boolean>(false);

  @Input() session: any = null;

  /**
   * Si la sesion puede PUBLICAR. Es mas estrecho que editar, igual que en las imagenes: el
   * servidor responde 403 de todas formas, y que el boton no se pinte es la cortesia de no
   * ofrecer algo que va a fallar.
   */
  readonly puedePublicar = input(false);
  /** Permite reutilizar el mismo editor estable como área de textos o como área exclusiva de equipo. */
  readonly modo = input<'textos' | 'equipo'>('textos');

  // Selected State
  selectedSection = signal<string>(SECCION_INICIAL);
  selectedGroup = signal<string>(primerGrupoDe(SECCION_INICIAL));
  activeEjeSubTab = signal<string>('general');
  saveStatus = signal<{ type: string; message: string } | null>(null);

  // Form State
  formData = signal<Record<string, string>>({});
  originalDetails = signal<Record<string, any>>({});
  openHistoryKeys = signal<Record<string, boolean>>({});
  teamMembers = signal<MiembroDelEquipoWeb[]>([]);
  photoProcessing = signal<string | null>(null);
  teamSettingsOpen = signal(false);

  // AQUI ESTABAN `objectiveIndexes`, `approachIndexes`, `timelineIndexes` y
  // `normativeIndexes`. Solo existian para que el dibujo de la previsualizacion
  // pudiera repetir tarjetas con @for. La previsualizacion ya no dibuja nada:
  // carga la pagina real.

  // --- Previsualizacion en un marco de 1440x810 --------------------------------

  readonly TAMANOS_DE_VISTA = TAMANOS_DE_VISTA;

  /** En que tamano se esta mirando la pagina. Arranca en escritorio, que es lo que se pidio. */
  readonly tamanoDeVista = signal<TamanoDeVista>(TAMANOS_DE_VISTA[0]);

  readonly ANCHO_LOGICO = computed(() => this.tamanoDeVista().ancho);
  readonly ALTO_LOGICO = computed(() => this.tamanoDeVista().alto);

  elegirTamano(tamano: TamanoDeVista): void {
    this.tamanoDeVista.set(tamano);
  }

  /** Cual de los encuadres del grupo se esta mirando. */
  readonly encuadreElegido = signal(0);
  /** Ancho util del hueco donde se pinta el marco; lo fija un ResizeObserver. */
  readonly anchoDisponible = signal(960);
  /** Sube cada vez que hay que forzar una recarga del marco. */
  readonly versionDeLaVista = signal(0);
  /** false mientras no se encuentra el ancla; enciende el aviso. */
  readonly anclaEncontrada = signal(true);
  /** El bloque es mas alto que el marco: no caben las dos vecinas. */
  readonly seccionMasAltaQueLaVentana = signal(false);
  readonly vistaAmpliada = signal(false);

  /**
   * SI EL MARCO SE MONTA O NO. Encendido en el panel, apagado en las pruebas.
   *
   * No es un interruptor de conveniencia: sin el, las pruebas del panel matan al
   * navegador. El marco carga una ruta de la aplicacion —`/pnmc`, `/`— y en
   * Karma esas rutas las sirve el propio Karma con su pagina de contexto, que es
   * la que carga la bateria de pruebas entera. El marco montaba la bateria dentro
   * de la bateria, en bucle, hasta que Chrome se desconectaba. Se vio como once
   * pruebas agotando su tiempo y un `DISCONNECTED`, no como lo que era.
   *
   * De paso es util en el panel: la previsualizacion carga una segunda copia del
   * sitio, y quien no la necesite puede apagarla.
   */
  readonly vistaEncendida = signal(true);

  readonly encuadresDelGrupo = computed<EncuadreDePrevisualizacion[]>(() => {
    const grupo = this.activeGroupObj();
    return grupo ? ENCUADRES[grupo.id] ?? [] : [];
  });

  readonly encuadreActivo = computed<EncuadreDePrevisualizacion | null>(() => {
    const lista = this.encuadresDelGrupo();
    return lista[this.encuadreElegido()] ?? lista[0] ?? null;
  });

  /**
   * Cuanto se reduce el marco para caber en el hueco.
   *
   * Nunca pasa de 1: ampliar una pagina de 1440 px por encima de su tamano no
   * ensena nada nuevo y desdibuja el texto.
   */
  readonly escala = computed(() => Math.min(1, this.anchoDisponible() / this.ANCHO_LOGICO()));
  readonly anchoPintado = computed(() => Math.round(this.ANCHO_LOGICO() * this.escala()));
  readonly altoPintado = computed(() => Math.round(this.ALTO_LOGICO() * this.escala()));

  /**
   * Los campos que la editora ha tocado y el marco todavia no puede ensenar.
   *
   * Se compara contra lo PUBLICADO, que es lo mismo que sirve `getWebText`
   * (textos-web.service.ts), y con `??` y no `||`: con `||` un campo
   * publicado en blanco caeria al valor de fabrica y vaciar un texto no contaria
   * como cambio. Es el defecto PNMC-040, en el otro lado del espejo.
   */
  readonly camposFueraDeLaVista = computed<string[]>(() => {
    const grupo = this.activeGroupObj();
    if (!grupo) { return []; }
    const borrador = this.formData();
    const detalles = this.originalDetails();
    return grupo.keys.filter((key: string) => {
      const publicado = detalles[key]?.publishedContent ?? DEFAULT_TEXTS[key] ?? '';
      return (borrador[key] ?? '') !== publicado;
    });
  });

  /**
   * La direccion que carga el marco.
   *
   * `?pnmcVista=` no es decorativo: es la llave que el sitio publico exige para
   * aceptar borradores del panel (core/cms/vista-previa-puente.ts). Sin ese
   * parametro, la pagina cargada ignora cualquier mensaje que le llegue.
   * `versionDeLaVista` va en la direccion para poder forzar una recarga.
   */
  readonly urlDelMarco = computed<SafeResourceUrl | null>(() => {
    const encuadre = this.encuadreActivo();
    const ruta = rutaSegura(encuadre?.ruta ?? null);
    if (!ruta || !this.vistaEncendida()) { return null; }
    const separador = ruta.includes('?') ? '&' : '?';
    const url = `${ruta}${separador}pnmcVista=${this.fichaDeLaVista}&v=${this.versionDeLaVista()}`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  });

  /** Ruta desnuda del encuadre activo; sirve para no recargar dentro de la misma pagina. */
  private readonly rutaDelEncuadre = computed(() => rutaSegura(this.encuadreActivo()?.ruta ?? null));

  /** Cuantas ranuras de imagen tienen archivo sin publicar. */
  readonly imagenesSinPublicar = computed(() => this.ranurasSinPublicar().size);

  readonly estadoDeLaVista = computed(() => {
    if (!this.encuadreActivo()) { return 'Esta sección todavía no declara dónde mirarse.'; }
    const campos = this.camposFueraDeLaVista().length;
    const imagenes = this.imagenesSinPublicar();
    if (campos === 0 && imagenes === 0) { return 'Igual a lo que estás editando'; }

    // LAS IMAGENES CUENTAN. Sin ellas, subir una portada y no tocar ningun texto dejaba la
    // pastilla en «Igual a lo que estas editando» mientras el marco pintaba una imagen que el
    // visitante no ve. La pastilla decia que todo estaba publicado y no lo estaba.
    const partes: string[] = [];
    if (campos > 0) { partes.push(`${campos} ${campos === 1 ? 'campo' : 'campos'}`); }
    if (imagenes > 0) { partes.push(`${imagenes} ${imagenes === 1 ? 'imagen' : 'imágenes'}`); }
    return `${partes.join(' y ')} sin publicar`;
  });

  /** Fuerza una recarga del marco: se usa al publicar y con el boton «Actualizar». */
  recargarLaVista(): void {
    this.preparadoEn = null;
    this.versionDeLaVista.update(v => v + 1);
  }

  elegirEncuadre(indice: number): void {
    this.encuadreElegido.set(indice);
  }

  alternarVistaEncendida(): void {
    this.vistaEncendida.update(v => !v);
  }

  alternarAmpliada(): void {
    this.vistaAmpliada.update(v => !v);
  }

  /** La url del marco en la que ya se pulso `preparar`. Evita el segundo clic. */
  private preparadoEn: string | null = null;

  /**
   * Desplaza el marco hasta el bloque del grupo y deja ver sus vecinas.
   *
   * REINTENTA, y no por precaucion: la pagina del marco es la aplicacion entera
   * arrancando —router, peticiones, imagenes—, y el ancla no existe en el
   * instante del evento `load`. Cuarenta intentos cada 150 ms son seis segundos.
   *
   * El clic de `preparar` se da UNA SOLA VEZ POR CARGA. El unico que lo usa hoy
   * es el tutorial del geovisor, y su boton es `handleTogglePanel`: pulsarlo en
   * cada reintento lo abriria y lo cerraria cuarenta veces.
   */
  private encuadrar(marco: HTMLIFrameElement, intentos = 40): void {
    let doc: Document | null;
    try {
      doc = marco.contentDocument;
    } catch {
      // Mismo origen siempre, pero si algun dia deja de serlo el panel no se cae.
      return;
    }
    if (!doc || !doc.body) {
      if (intentos > 0) { setTimeout(() => this.encuadrar(marco, intentos - 1), 150); }
      return;
    }

    const encuadre = this.encuadreActivo();
    if (!encuadre) { return; }

    const urlActual = marco.getAttribute('src');
    if (encuadre.preparar && this.preparadoEn !== urlActual) {
      const pasos = typeof encuadre.preparar === 'string' ? [encuadre.preparar] : encuadre.preparar;
      // SE PULSA EL PRIMERO Y LOS DEMAS EN LA SIGUIENTE VUELTA. Cada clic monta lo que el
      // siguiente necesita encontrar, y eso no ocurre dentro del mismo fotograma: buscarlos todos
      // seguidos daría `null` en el segundo.
      const pendiente = pasos.find(selector => doc.querySelector<HTMLElement>(selector));
      const disparador = pendiente ? doc.querySelector<HTMLElement>(pendiente) : null;
      if (disparador) {
        disparador.click();
        // SOLO SE DA POR PREPARADO CUANDO SE PULSO EL ULTIMO, para que la vuelta siguiente pueda
        // encadenar el que falte.
        if (pendiente === pasos[pasos.length - 1]) this.preparadoEn = urlActual;
      }
    }

    if (!encuadre.ancla) {
      this.anclaEncontrada.set(true);
      this.seccionMasAltaQueLaVentana.set(false);
      marco.contentWindow?.scrollTo(0, 0);
      return;
    }

    const bloque = doc.querySelector<HTMLElement>(encuadre.ancla);
    if (!bloque) {
      if (intentos > 0) {
        setTimeout(() => this.encuadrar(marco, intentos - 1), 150);
      } else {
        this.anclaEncontrada.set(false);
      }
      return;
    }

    this.anclaEncontrada.set(true);
    const caja = bloque.getBoundingClientRect();
    const desplazamientoActual = marco.contentWindow?.scrollY ?? 0;
    const top = caja.top + desplazamientoActual;
    this.seccionMasAltaQueLaVentana.set(caja.height > this.ALTO_LOGICO());
    marco.contentWindow?.scrollTo(
      0,
      calcularEncuadre({ top, alto: caja.height }, doc.body.scrollHeight, this.ALTO_LOGICO()),
    );
  }

  /** Lo llama el `(load)` del marco en la plantilla. */
  alCargarElMarco(evento: Event): void {
    const marco = evento.target as HTMLIFrameElement;
    this.encuadrar(marco);
    // Dos re-encuadres tardios: las imagenes y las fuentes cambian el alto de la
    // pagina despues de `load`, y con ello la posicion del bloque.
    setTimeout(() => this.encuadrar(marco, 0), 600);
    setTimeout(() => this.encuadrar(marco, 0), 1500);
  }
  readonly teamSectionKeys = [
    'about_team_bg',
    'about_team_title',
    'about_team_intro',
    'about_team_coordination_label',
    'about_team_leadership_label',
  ];

  // Icon references
  LucideCheckCircle2 = LucideCheckCircle2;
  LucideAlertCircle = LucideAlertCircle;
  LucideUpload = LucideUpload;
  LucideTrash2 = LucideTrash2;
  LucideUserRound = LucideUserRound;
  LucidePlus = LucidePlus;
  LucideChevronDown = LucideChevronDown;
  LucideExternalLink = LucideExternalLink;

  /**
   * Grupos editables derivados del registro unico. Antes esta lista repetia a
   * mano las 238 claves que ya declaraba el catalogo, y cualquier clave nueva
   * habia que agregarla en los dos lugares.
   */
  GROUPS = WEB_TEXT_GROUPS.map((group) => ({
    id: group.id,
    label: group.label,
    section: group.section,
    keys: group.fields.map((field) => field.key),
  }));

  /** Pestanas horizontales, en el orden de edicion declarado por el registro. */
  readonly sections = WEB_TEXT_SECTIONS;
  /** Las secciones como posiciones del selector segmentado: eran píldoras. */
  readonly seccionesComoOpciones: readonly OpcionSegmentada[] = WEB_TEXT_SECTIONS.map(item => ({ id: item.section, etiqueta: item.pill }));
  readonly ejesComoOpciones = computed<readonly OpcionSegmentada[]>(() => this.ejeSubTabsList().map(tab => ({ id: tab.id, etiqueta: tab.label })));
  readonly encuadresComoOpciones = computed<readonly OpcionSegmentada[]>(() =>
    this.encuadresDelGrupo().map((encuadre, i) => ({ id: String(i), etiqueta: encuadre.etiqueta })));
  /** El parser de plantillas no ve los globales: se exponen para convertir el índice del encuadre. */
  readonly String = String;
  readonly Number = Number;

  // --- El estudio: acordeon a la izquierda, pagina real pegada a la derecha ----

  /*
   * EL ESTUDIO ES EL UNICO DISENO DEL PANEL desde.
   *
   * Aqui vivia `SECCION_DE_ESTUDIO = 'Home'`, la constante que lo dejaba solo en la portada
   * mientras se probaba, y la plantilla llevaba las dos maquetaciones con un `@else`. Al pasar
   * todas las secciones, esa rama quedaba sin ninguna forma de alcanzarse: se retiro entera en
   * vez de dejarla como codigo muerto que aparenta ser una alternativa.
   */

  /**
   * Que bloque del acordeon esta abierto. `null` es «todos plegados», que es un estado util:
   * deja ver los seis rotulos de un vistazo para saltar a otro sin recorrer un formulario.
   *
   * PLEGARLO TODO NO CAMBIA `selectedGroup`, y es deliberado: el marco de la derecha sigue
   * encuadrado donde estaba. Ponerlo a null dejaria la previsualizacion en blanco justo cuando
   * la persona quiere mirarla mientras decide a donde ir.
   *
   * ARRANCA CON EL MISMO GRUPO QUE `selectedGroup`, no en null: al entrar al panel se veian los
   * rotulos plegados y hacia falta un clic que nadie eligio dar. Los dos salen de la misma
   * funcion para que no puedan discrepar; antes eran dos literales y la prueba era lo unico que
   * los ataba.
   */
  readonly bloqueAbierto = signal<string | null>(primerGrupoDe(SECCION_INICIAL));

  /**
   * Las imagenes sin publicar que el marco tiene que pintar, por clave de ranura.
   *
   * Viajan en el mismo mensaje que los textos y por el mismo puente
   * (core/cms/vista-previa-puente.ts). Sin esto, subir una portada y mirar la previsualizacion
   * ensena la portada anterior: el manifiesto publico solo trae lo publicado.
   */
  private readonly imagenesEnVivo = signal<Record<string, string>>({});

  /**
   * Las ranuras que tienen imagen sin publicar. SOLO LA CLAVE REAL, nunca las espejadas.
   *
   * Es otra cosa que `imagenesEnVivo`, y por eso son dos senales: aquella lleva lo que el marco
   * tiene que PINTAR —cuatro claves apuntando al mismo archivo cuando se espeja una portada—, y
   * esta lleva cuantas ranuras estan de verdad sin publicar. Contar las claves de la primera
   * diria «4 imagenes sin publicar» cuando hay una.
   */
  private readonly ranurasSinPublicar = signal<ReadonlySet<string>>(new Set());

  /** Abre un bloque, o lo pliega si ya estaba abierto. */
  alternarBloque(groupId: string): void {
    if (this.bloqueAbierto() === groupId) {
      this.bloqueAbierto.set(null);
      return;
    }
    this.bloqueAbierto.set(groupId);
    // Abrir un bloque es lo que mueve la previsualizacion: `selectedGroup` gobierna el encuadre.
    this.selectedGroup.set(groupId);
  }

  /**
   * LO QUE EL MARCO TIENE QUE RECIBIR para pintar el bloque abierto.
   *
   * Se calcula aparte del efecto que lo envia para poder afirmarlo sin montar un marco: el
   * `<iframe>` de la previsualizacion carga la aplicacion entera, y en Karma la ruta la sirve la
   * propia pagina de contexto de Karma. Aqui se mide QUE se manda; que llegue lo cubre
   * `vista-previa-puente.spec.ts` al otro lado, y el recorrido completo la prueba e2e.
   *
   * SOLO LAS CLAVES DEL GRUPO ABIERTO en los textos, TODAS las imagenes en vivo. No es una
   * incoherencia: los textos del grupo cerrado ya se enviaron cuando estaba abierto y siguen
   * puestos en el marco, mientras que una imagen sin publicar puede ser de otro bloque —las
   * cuatro portadas se espejan entre si— y recortarla por grupo la apagaria.
   */
  readonly borradorParaElMarco = computed(() => {
    const grupo = this.activeGroupObj();
    const textos = this.formData();
    const soloDelGrupo: Record<string, string> = {};
    for (const key of grupo?.keys ?? []) { soloDelGrupo[key] = textos[key] ?? ''; }
    return { textos: soloDelGrupo, imagenes: this.imagenesEnVivo() };
  });

  /** Las ranuras de imagen que se editan dentro de un bloque. Vacio si no tiene ninguna. */
  imagenesDelBloque(groupId: string): string[] {
    return IMAGENES_DEL_BLOQUE[groupId] ?? [];
  }

  /**
   * Recoge lo que el editor de imagenes acaba de hacer y lo lleva al marco.
   *
   * `rotanConElla` no es un adorno: la portada del Home elige UNA DE CUATRO AL AZAR en cada
   * visita (home.component.ts). Sin espejar la que se esta cambiando sobre las cuatro
   * claves, la previsualizacion ensenaria la que le toque y la persona no podria mirar la suya.
   * Se dice en pantalla, porque fijar la portada es una mentira piadosa y tiene que verse.
   */
  recibirImagenEnVivo(evento: ImagenEnVivo): void {
    this.ranurasSinPublicar.update(previo => {
      const siguiente = new Set(previo);
      if (evento.url) { siguiente.add(evento.clave); } else { siguiente.delete(evento.clave); }
      return siguiente;
    });

    const claves = evento.rotanConElla.length > 0 ? evento.rotanConElla : [evento.clave];
    this.imagenesEnVivo.update(previo => {
      const siguiente = { ...previo };
      for (const clave of claves) {
        if (evento.url) { siguiente[clave] = evento.url; } else { delete siguiente[clave]; }
      }
      return siguiente;
    });

    // Publicar o retirar cambia el MANIFIESTO, y eso el marco solo lo relee volviendo a cargar
    // la pagina. Subir, no: el borrador viaja por mensaje y se ve al instante.
    if (evento.recargarElSitio) { this.recargarLaVista(); }
  }


  keysList = this.webTextsService.getWebTextsKeysList();

  constructor() {
    // Los valores compilados se ponen de entrada para que el formulario nunca
    // aparezca vacio mientras llega la respuesta del servidor.
    this.seedFormWithDefaults();

    // «Equipo» es un área visible de Gestión del sitio. Al montarla, este mismo editor abre la
    // nómina directamente y no obliga a atravesar Sobre PNMC ni a conocer el nombre del grupo.
    effect(() => {
      if (this.modo() !== 'equipo') { return; }
      this.selectedSection.set('Sobre PNMC');
      this.selectedGroup.set('about_team');
      this.bloqueAbierto.set('about_team');
    });

    // Cada grupo se trae del servidor cuando se abre, no los 26 de golpe: el
    // editor trabaja en uno a la vez y 26 peticiones al abrir el panel serian
    // 25 desperdiciadas.
    effect(() => {
      const groupId = this.selectedGroup();
      this.activeEjeSubTab.set('general');
      void this.loadGroupFromServer(groupId);
    });

    // Al cambiar de grupo se vuelve al primer encuadre: el indice 1 de un grupo
    // de dos no significa nada en el siguiente.
    effect(() => {
      this.selectedGroup();
      this.encuadreElegido.set(0);
    });

    // EL MARCO SE RECARGA CUANDO CAMBIA LA RUTA, NO CUANDO CAMBIA EL GRUPO.
    // Siete grupos comparten `/pnmc` y ocho `/ecosistema`: recargar en cada
    // cambio de grupo tiraria la pagina entera para volver a montarla igual, y
    // el desplazamiento hasta el bloque nuevo tardaria seis segundos en vez de
    // uno. Cuando la ruta no cambia, solo se vuelve a encuadrar.
    effect(() => {
      const ruta = this.rutaDelEncuadre();
      this.encuadreElegido();
      // Cambiar de tamano cambia el alto del marco y con el la posicion del bloque: hay que
      // volver a encuadrar aunque no se haya movido de grupo.
      this.tamanoDeVista();
      if (!ruta) { return; }
      const marco = this.marcoPrevisualizacion()?.nativeElement;
      if (!marco) { return; }
      if (marco.getAttribute('src')?.startsWith(`${ruta}?`) || marco.getAttribute('src')?.startsWith(`${ruta}&`)) {
        this.encuadrar(marco);
      }
    });

    // El ancho del hueco manda sobre la escala. Se observa en vez de leerse una
    // vez porque la barra lateral del panel se pliega y la ventana cambia.
    effect((alLimpiar) => {
      const lienzo = this.lienzoPrevisualizacion()?.nativeElement;
      if (!lienzo || typeof ResizeObserver === 'undefined') { return; }
      const observador = new ResizeObserver(entradas => {
        const ancho = entradas[0]?.contentRect.width ?? 0;
        if (ancho > 0) { this.anchoDisponible.set(ancho); }
      });
      observador.observe(lienzo);
      alLimpiar(() => observador.disconnect());
    });

    // EL BORRADOR VIAJA AL MARCO EN CADA TECLA. Es lo que mantiene «en vivo» la
    // previsualizacion sin tocar `textos-web.service.ts`: el sitio publico solo
    // acepta estos mensajes cuando se cargo con `?pnmcVista=` y son del marco
    // padre. Ver `core/cms/vista-previa-puente.ts`.
    effect(() => {
      const carga = this.borradorParaElMarco();
      const marco = this.marcoPrevisualizacion()?.nativeElement;
      if (!marco?.contentWindow) { return; }
      marco.contentWindow.postMessage(
        { tipo: 'pnmc:borrador', ficha: this.fichaDeLaVista, ...carga },
        window.location.origin,
      );
    });
  }

  /** Rellena el formulario con el texto de fabrica, sin tocar el servidor. */
  private seedFormWithDefaults() {
    const data: Record<string, string> = {};
    this.keysList.forEach(k => {
      data[k.key] = DEFAULT_TEXTS[k.key] || '';
    });
    this.formData.set(data);
    this.teamMembers.set(this.webTextsService.getWebTeamMembers().map(member => ({ ...member })));
  }

  /**
   * Trae del servidor el borrador de un grupo. El grupo `about_team` ademas
   * carga la nomina, que vive en su propia ruta.
   */
  private async loadGroupFromServer(groupId: string, force = false) {
    if (!force && this.loadedGroups.has(groupId)) {
      return;
    }

    this.loadingGroup.set(true);
    try {
      const outcome = await this.api.getGroup(groupId);
      if (!outcome.ok || !outcome.data) {
        this.saveStatus.set({
          type: 'error',
          message: `${outcome.error ?? 'No fue posible cargar este grupo.'} Está viendo los textos de fábrica; no guarde hasta que cargue.`,
        });
        return;
      }

      const data = { ...this.formData() };
      const versions = { ...this.groupVersions() };
      const details: Record<string, any> = { ...this.originalDetails() };

      for (const field of outcome.data.fields) {
        data[field.key] = field.draft;
        versions[field.key] = field.version;
        // El panel muestra estado y autoria; el historial por clave llega en 2E.
        details[field.key] = {
          content: field.draft,
          publishedContent: field.published,
          status: field.state,
          updatedAt: field.updatedAt,
          updatedBy: field.updatedBy,
          history: [],
        };
      }

      this.formData.set(data);
      this.groupVersions.set(versions);
      this.originalDetails.set(details);
      this.loadedGroups.add(groupId);

      if (groupId === 'about_team') {
        await this.loadTeamFromServer();
      }

      // El historial va en su propia peticion, y su fallo no invalida la carga
      // del grupo: sin historial se puede editar; sin borrador, no.
      const historial = await this.api.getGroupHistory(groupId);
      if (historial.ok && historial.data) {
        this.historyByKey.set(historial.data.byKey ?? {});
      }
    } finally {
      this.loadingGroup.set(false);
    }
  }

  private async loadTeamFromServer() {
    const outcome = await this.api.getTeam();
    if (!outcome.ok || !outcome.data) {
      this.saveStatus.set({
        type: 'error',
        message: outcome.error ?? 'No fue posible cargar la nómina del equipo.',
      });
      return;
    }

    this.teamMembers.set(outcome.data.members.map(member => ({ ...member })));
    this.teamVersion.set(outcome.data.version);
    if (outcome.data.limits) {
      this.topesNomina.set(outcome.data.limits);
    }
  }

  /** Recarga el grupo abierto descartando lo que haya en pantalla. */
  async reloadCurrentGroup() {
    await this.loadGroupFromServer(this.selectedGroup(), true);
  }

  handleInputChange(key: string, val: string) {
    this.formData.update(prev => ({
      ...prev,
      [key]: val
    }));
  }

  handleRestoreVersion(key: string, content: string) {
    this.handleInputChange(key, content);
    this.saveStatus.set({
      type: 'info',
      message: 'Versión del historial restaurada en el editor. Recuerde hacer clic en Guardar para conservar los cambios.'
    });
    setTimeout(() => this.saveStatus.set(null), 4000);
  }

  handleSaveAllGroup(publish = true) {
    // El autor ya no viaja en la peticion: el servidor lo toma de la sesion
    // autenticada. Un nombre enviado por el cliente es un dato que el cliente
    // elige, y la autoria de un cambio no puede depender de eso.
    const group = this.GROUPS.find(g => g.id === this.selectedGroup());
    if (!group) return;

    // Se nombran los campos que se pasan y por cuanto. Decir «uno o mas campos
    // exceden el limite» obliga a buscarlos a mano entre los 27 de un grupo, y
    // durante la revision del corte eso se hace 26 veces.
    const excedidos = group.keys
      .map(key => {
        const descriptor = WEB_TEXT_KEY_INDEX.get(key);
        const limit = descriptor?.limit ?? 999;
        const length = (this.formData()[key] || '').length;
        return { label: descriptor?.label ?? key, limit, length, exceso: length - limit };
      })
      .filter(campo => campo.exceso > 0)
      .sort((a, b) => b.exceso - a.exceso);

    if (excedidos.length > 0) {
      const detalle = excedidos
        .slice(0, 3)
        .map(c => `«${c.label}» sobra ${c.exceso} ${c.exceso === 1 ? 'carácter' : 'caracteres'} (${c.length}/${c.limit})`)
        .join('; ');
      const resto = excedidos.length > 3 ? ` y ${excedidos.length - 3} más` : '';

      this.saveStatus.set({
        type: 'error',
        message: `No se guardó nada. Recorte: ${detalle}${resto}.`,
      });
      setTimeout(() => this.saveStatus.set(null), 9000);
      return;
    }

    if (group.id === 'about_team') {
      const problemas = this.teamMembers()
        .map((member, index) => {
          const quien = member.name.trim() || `la persona #${index + 1}`;
          if (!member.name.trim()) { return `${quien}: falta el nombre`; }
          if (!member.role.trim()) { return `${quien}: falta el cargo`; }
          // Los topes salen del servidor, no de aquí. Escritos a mano se
          // quedaron en 120 para los tres mientras la API ya aceptaba 160 en el
          // cargo y 180 en el correo, y un cargo largo no se podía ni teclear.
          const topes = this.topesNomina();
          if (member.name.length > topes.maxNameLength) { return `${quien}: el nombre excede ${topes.maxNameLength} caracteres`; }
          if (member.role.length > topes.maxRoleLength) { return `${quien}: el cargo excede ${topes.maxRoleLength} caracteres`; }
          if (member.email.length > topes.maxEmailLength) { return `${quien}: el correo excede ${topes.maxEmailLength} caracteres`; }
          if (member.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(member.email.trim())) {
            return `${quien}: el correo no tiene un formato válido`;
          }
          return null;
        })
        .filter((problema): problema is string => problema !== null);

      if (problemas.length > 0) {
        this.saveStatus.set({
          type: 'error',
          message: `No se guardó nada. ${problemas.slice(0, 3).join('; ')}`
            + (problemas.length > 3 ? ` y ${problemas.length - 3} más.` : '.'),
        });
        setTimeout(() => this.saveStatus.set(null), 9000);
        return;
      }
    }

    void this.saveGroupToServer(group, publish);
  }

  /**
   * Guarda el grupo contra la API.
   *
   * El resultado del servidor manda: si la peticion falla, el panel dice que
   * fallo. Confirmar un guardado que no ocurrio fue el defecto original de este
   * panel y no se puede reintroducir por la puerta de atras.
   */
  private async saveGroupToServer(group: { id: string; label: string; keys: string[] }, publish: boolean) {
    if (this.savingGroup()) {
      return;
    }

    this.savingGroup.set(true);
    try {
      const fields = group.keys.map(key => ({ key, content: this.formData()[key] || '' }));
      const outcome = await this.api.saveGroup(group.id, fields, publish);

      if (!outcome.ok) {
        this.saveStatus.set({
          type: 'error',
          message: outcome.conflict
            ? `${outcome.error} Use «Recargar del servidor» para traer la versión vigente.`
            : outcome.error ?? 'No fue posible guardar.',
        });
        setTimeout(() => this.saveStatus.set(null), 8000);
        return;
      }

      // La nomina va en su propia ruta y con su propia version.
      if (group.id === 'about_team') {
        const version = this.teamVersion();
        if (version === null) {
          this.saveStatus.set({
            type: 'error',
            message: 'Los textos se guardaron, pero la nómina no está cargada. Recárguela antes de guardar el equipo.',
          });
          setTimeout(() => this.saveStatus.set(null), 8000);
          return;
        }

        const team = await this.api.saveTeam(this.teamMembers(), publish, version);
        if (!team.ok) {
          this.saveStatus.set({
            type: 'error',
            message: `Los textos se guardaron, pero la nómina no: ${team.error}`,
          });
          setTimeout(() => this.saveStatus.set(null), 8000);
          return;
        }
        this.teamVersion.set(team.data?.version ?? version);
      }

      const changed = outcome.data?.changed ?? 0;
      this.saveStatus.set({
        type: 'success',
        message: publish
          ? `«${group.label}» se guardó y publicó. ${changed} ${changed === 1 ? 'campo cambió' : 'campos cambiaron'}.`
          : `«${group.label}» se guardó como borrador. Todavía no se ve en el sitio: hay que publicarlo.`,
      });

      // Se recarga para traer las versiones nuevas: sin eso, el siguiente
      // guardado chocaria contra su propia escritura.
      await this.loadGroupFromServer(group.id, true);

      // Y al PUBLICAR se recarga tambien el marco. El borrador que viaja por
      // mensaje ya se veia; lo que cambia al publicar es lo que sirve el
      // servidor, y eso el marco solo lo relee volviendo a cargar la pagina.
      // Sin esto, la pastilla pasa a «Igual a lo que estás editando» mientras el
      // marco sigue pintando lo de antes.
      if (publish) { this.recargarLaVista(); }
      setTimeout(() => this.saveStatus.set(null), 6000);
    } finally {
      this.savingGroup.set(false);
    }
  }

  // Grupos de la seccion activa. La etiqueta corta de la pestana la resuelve el
  // registro, asi que aqui ya no hace falta traducir "Mapa" a su seccion real.
  filteredGroups = computed(() => {
    if (this.modo() === 'equipo') {
      return this.GROUPS.filter(g => g.id === 'about_team');
    }
    const section = this.selectedSection();
    return this.GROUPS.filter(g => g.section === section);
  });

  activeGroupObj = computed(() => {
    return this.GROUPS.find(g => g.id === this.selectedGroup());
  });

  /**
   * La ruta publica del grupo abierto, o `null` si no se sabe cual es.
   *
   * Devolver `null` es deliberado: sin ruta el boton no se dibuja. Un enlace
   * roto en el panel es peor que la ausencia del enlace, porque una pagina que
   * no muestra el texto se lee como «el cambio no se aplico».
   */
  readonly paginaPublica = computed(() => {
    const grupo = this.activeGroupObj();
    if (!grupo) { return null; }
    return PAGINA_DEL_GRUPO[grupo.id] ?? PAGINA_DE_LA_SECCION[grupo.section] ?? null;
  });

  /**
   * LOS TRES GRUPOS QUE SE PARTEN EN SUBPESTANAS, nombrados uno por uno.
   *
   * Estaba escrito como `section === 'Ejes'`, y era cierto mientras la seccion tuviera
   * exactamente esos tres grupos. El 30 de agosto de 2026 entraron dos mas —el encabezado de la
   * pagina y la ficha de componente— y el filtro los alcanzo: `filteredEjeKeys` conserva, de las
   * claves acabadas en `_title`, SOLO `eje01_title`, asi que `ejes_hero_title` y las tres
   * `component_*_title` DESAPARECIERON del formulario. El campo seguia en el catalogo y en la
   * base; simplemente no habia forma de escribirlo.
   *
   * No es «section» lo que decide si un grupo tiene componentes dentro: es el grupo.
   */
  private static readonly GRUPOS_CON_SUBPESTANAS: Record<string, string> = {
    eje1_details: '01',
    eje2_details: '02',
    eje3_details: '03',
  };

  ejeSubTabsList = computed(() => {
    const activeGroup = this.activeGroupObj();
    const ejeId = activeGroup
      ? AdminWebTextsPanelComponent.GRUPOS_CON_SUBPESTANAS[activeGroup.id]
      : undefined;
    if (!activeGroup || !ejeId) return [];

    const tabs = [{ id: 'general', label: 'Información General' }];

    activeGroup.keys.forEach(k => {
      const match = k.match(new RegExp(`eje${ejeId}_c(\\d+)_title`));
      if (match) {
        const compNum = match[1];
        const compTitle = this.formData()[k] || `Componente ${compNum}`;
        tabs.push({
          id: `c${compNum}`,
          label: compTitle.length > 25 ? compTitle.slice(0, 23) + '...' : compTitle
        });
      }
    });

    return tabs;
  });

  filteredEjeKeys = computed(() => {
    const activeGroup = this.activeGroupObj();
    const subTab = this.activeEjeSubTab();
    const ejeId = activeGroup
      ? AdminWebTextsPanelComponent.GRUPOS_CON_SUBPESTANAS[activeGroup.id]
      : undefined;
    if (!activeGroup || !ejeId) return [];

    if (subTab === 'general') {
      return activeGroup.keys.filter(k => k.endsWith('_title') ? k === `eje${ejeId}_title` : !k.includes('_c'));
    } else {
      const compNum = subTab.replace('c', '');
      return activeGroup.keys.filter(k => k.includes(`_c${compNum}_`));
    }
  });

  keysToRender = computed(() => {
    const activeGroup = this.activeGroupObj();
    if (!activeGroup) return [];
    // Solo los tres grupos de eje se reparten en subpestanas. Cualquier otro pinta TODAS sus
    // claves: un campo que el formulario no dibuja es un campo que nadie puede escribir.
    if (AdminWebTextsPanelComponent.GRUPOS_CON_SUBPESTANAS[activeGroup.id]) {
      return this.filteredEjeKeys();
    }
    return activeGroup.keys;
  });

  /** Cambia de seccion y abre su primer grupo editable. */
  selectPill(section: string) {
    this.selectedSection.set(section);
    const firstGroup = this.GROUPS.find(g => g.section === section);
    if (firstGroup) {
      this.selectedGroup.set(firstGroup.id);
      // El acordeon estrena la seccion con su primer bloque abierto. Llegar a una lista de seis
      // rotulos plegados obliga a un clic que nadie eligio dar.
      this.bloqueAbierto.set(firstGroup.id);
    }
  }

  // La plantilla consulta limite y etiqueta por cada campo en cada ciclo de
  // deteccion de cambios. Con el indice del registro es O(1) en vez de recorrer
  // las 238 claves del catalogo cada vez.
  getCharLimit(key: string): number {
    return WEB_TEXT_KEY_INDEX.get(key)?.limit ?? 300;
  }

  getFieldLabel(key: string): string {
    return WEB_TEXT_KEY_INDEX.get(key)?.label ?? key;
  }

  // AQUI ESTABA `getPreviewLines`. Partia un campo en lineas para que el dibujo
  // de la previsualizacion pudiera pintarlas como vinetas. Ya no hay dibujo.

  getMemberInitials(member: MiembroDelEquipoWeb, index: number): string {
    return member.name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part.charAt(0).toUpperCase())
      .join('') || String(index + 1).padStart(2, '0');
  }

  addTeamMember() {
    const id = typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `team-${Date.now()}`;
    this.teamMembers.update(members => [
      ...members,
      { id, group: 'leadership', name: '', role: '', email: '', photo: '' },
    ]);
    setTimeout(() => {
      document.getElementById(`team-member-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  updateTeamMember(id: string, field: 'name' | 'role' | 'email' | 'group', value: string) {
    this.teamMembers.update(members => members.map(member =>
      member.id === id ? { ...member, [field]: value } as MiembroDelEquipoWeb : member
    ));
  }

  removeTeamMember(id: string) {
    this.teamMembers.update(members => members.filter(member => member.id !== id));
    this.saveStatus.set({
      type: 'info',
      message: 'Integrante retirado del editor. Guarde o publique para confirmar el cambio.'
    });
    setTimeout(() => this.saveStatus.set(null), 4000);
  }

  async handleTeamPhotoSelected(event: Event, memberId: string) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.showPhotoError('Formato no compatible. Use una imagen JPG, PNG o WebP.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.showPhotoError('La fotografía supera 10 MB. Seleccione un archivo más liviano.');
      return;
    }

    this.photoProcessing.set(memberId);
    try {
      const optimizedPhoto = await this.optimizeTeamPhoto(file);
      this.teamMembers.update(members => members.map(member =>
        member.id === memberId ? { ...member, photo: optimizedPhoto } : member
      ));
      const memberName = this.teamMembers().find(member => member.id === memberId)?.name;
      this.saveStatus.set({
        type: 'info',
        message: `Fotografía de ${memberName || 'la persona'} lista. Guarde o publique para conservarla.`
      });
      setTimeout(() => this.saveStatus.set(null), 4500);
    } catch {
      this.showPhotoError('No fue posible procesar la fotografía. Pruebe con otro archivo.');
    } finally {
      this.photoProcessing.set(null);
    }
  }

  removeTeamPhoto(memberId: string) {
    this.teamMembers.update(members => members.map(member =>
      member.id === memberId ? { ...member, photo: '' } : member
    ));
    this.saveStatus.set({
      type: 'info',
      message: 'La fotografía se retiró de la tarjeta. Guarde o publique para confirmar el cambio.'
    });
    setTimeout(() => this.saveStatus.set(null), 4000);
  }

  private showPhotoError(message: string) {
    this.saveStatus.set({ type: 'error', message });
    setTimeout(() => this.saveStatus.set(null), 5000);
  }

  private optimizeTeamPhoto(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(new Error('read-error'));
      reader.onload = () => {
        const image = new Image();
        image.onerror = () => reject(new Error('image-error'));
        image.onload = () => {
          const maxWidth = 480;
          const maxHeight = 600;
          const scale = Math.min(1, maxWidth / image.width, maxHeight / image.height);
          const width = Math.max(1, Math.round(image.width * scale));
          const height = Math.max(1, Math.round(image.height * scale));
          const canvas = document.createElement('canvas');
          canvas.width = width;
          canvas.height = height;
          const context = canvas.getContext('2d');
          if (!context) {
            reject(new Error('canvas-error'));
            return;
          }
          context.fillStyle = '#f8fafc';
          context.fillRect(0, 0, width, height);
          context.drawImage(image, 0, 0, width, height);
          resolve(canvas.toDataURL('image/webp', 0.8));
        };
        image.src = String(reader.result || '');
      };
      reader.readAsDataURL(file);
    });
  }

  /**
   * El historial de una clave, que ahora vive en el servidor (2E).
   *
   * Se lee de una senal y no de una peticion, porque la plantilla llama a esto
   * en cada ciclo de deteccion de cambios: una peticion aqui serian cientos.
   * La carga la hace `loadGroupFromServer`, de una vez para todo el grupo.
   */
  getHistory(key: string): EntradaDeHistorialDeContenidoWeb[] {
    return this.historyByKey()[key] ?? [];
  }

  /** Etiqueta legible de cada acción del historial. */
  describeHistoryAction(entry: EntradaDeHistorialDeContenidoWeb): string {
    switch (entry.action) {
      case 'publicado': return 'Publicado en el sitio';
      case 'retirado': return 'Retirado del sitio';
      case 'republicado': return 'Devuelto al sitio';
      case 'importado': return 'Importado desde un respaldo';
      default: return 'Guardado como borrador';
    }
  }

  toggleHistoryCollapse(key: string) {
    this.openHistoryKeys.update(prev => ({
      ...prev,
      [key]: !prev[key]
    }));
  }

  isHistoryOpen(key: string): boolean {
    return !!this.openHistoryKeys()[key];
  }
}
