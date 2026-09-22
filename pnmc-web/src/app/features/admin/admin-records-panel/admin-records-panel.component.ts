import { nombrePropio } from '../../../shared/texto/nombre-propio';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { Component, Input, Output, EventEmitter, inject, signal, computed, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { HistorialDeRegistroComponent, VersionDelRegistro } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FormsModule } from '@angular/forms';
import { ConmutadorDeOjoComponent } from '../../../shared/components/ui/conmutador-de-ojo/conmutador-de-ojo.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { LucideRefreshCw, LucideDownload, LucidePlus, LucideX } from '@lucide/angular';
import { AdminService, CatalogoDeFestival, FichaDeFestivalEnRevision } from '../../../core/services/admin.service';
import { descargarPlantillaDeImportacion } from '../../../shared/utils/plantilla-de-importacion';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { 
  etiquetaDeEstado, 
  ADMIN_COVERAGE_LEVELS, 
  AdminModule, 
  AdminField 
} from '../domain/admin-config';

// Synonyms & helpers for excel generation & parsing
const CONTACT_FIELD_NAMES = new Set(['contactEmail', 'contactPhone', 'organizerEmail', 'organizerPhone', 'responsibleEntityEmail', 'responsibleEntityPhone', 'contactName', 'directorName', 'organizer', 'responsibleEntity', 'responsibleEntityDisplayName']);
const LINK_FIELD_NAMES = new Set(['websiteUrl', 'instagramUrl', 'facebookUrl', 'otherUrl', 'organizerWebsiteUrl', 'responsibleEntityWebsiteUrl', 'infoUrl', 'externalUrl', 'embedUrl']);
const TERRITORY_FIELD_NAMES = new Set(['coverageLevel', 'department', 'municipality', 'specificLocation', 'addressText', 'latitude', 'longitude', 'zone', 'territorialScope', 'location']);
const METRIC_FIELD_NAMES = new Set(['versionsCount', 'editionsCount', 'trainingCapacity', 'students', 'activeGroupsCount', 'sortOrder', 'festivalId', 'associatedFestivalId', 'festivalDisplayName', 'associatedFestivalDisplayName', 'scopeType', 'marketMode', 'periodicity']);
const CONTROL_FIELD_NAMES = new Set(['id', 'status']);

// LA TABLA DE PRIORIDADES SE FUE AL SERVIDOR —`PrioridadDeEstado` en
// `AdminDataEndpoints`—. Aquí ordenaba la página ya traída: cien filas de hasta quinientas, con la
// tabla afirmando «primero lo que espera revisión» mientras lo que esperaba podía no haber venido.

const titleCaseEs = (value = '') => String(value || '')
  .toLocaleLowerCase('es-CO')
  .replace(/(^|[\s(/-])([\p{L}])/gu, (_, prefix, letter) => `${prefix}${letter.toLocaleUpperCase('es-CO')}`)
  .replace(/\bD\.c\./giu, 'D.C.');



interface MetadatosDeRegistro extends Record<string, unknown> {
  name?: string;
  title?: string;
  department?: string;
  municipality?: string;
  organizationName?: string;
  organizer?: string;
  responsibleName?: string;
  description?: string;
  editionsCount?: number;
  latestEditionYear?: number;
  contactEmail?: string;
  hasActiveRequest?: boolean;
  activeRequestType?: string;
  activeProposalStatus?: string;
  administrationType?: string;
  institutionalDraftProposalId?: number | string;
}

interface RegistroAdministrativo extends Record<string, unknown> {
  id: string;
  title?: string;
  name?: string;
  status: string;
  department?: string;
  municipality?: string;
  coverageLevel?: string;
  owner?: string;
  updatedAt?: string;
  metadata?: MetadatosDeRegistro;
}

interface FacetaAdministrativa {
  codigo: string;
  etiqueta: string;
  total: number;
}

interface ElementoDeRevisionLocal extends Record<string, unknown> {
  id: string;
  moduleId: string;
  title: string;
  owner: string;
  status: string;
  updatedAt: string;
}

const mensajeDeError = (error: unknown, respaldo: string): string => {
  if (error instanceof Error && error.message) return error.message;
  if (typeof error === 'object' && error !== null && 'message' in error) {
    return String((error as { message: unknown }).message || respaldo);
  }
  return respaldo;
};

@Component({
  selector: 'app-admin-records-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent,DialogoDirective, FiltroDesplegableComponent, CabeceraDeTablaComponent, 
    BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent, NombrePropioPipe, 
    CommonModule,
    FormsModule,
    BuscadorDeListaComponent,
    ConmutadorDeOjoComponent,
    LucideRefreshCw,
    LucideDownload,
    LucidePlus,

    LucideX,

    IndicadorDeEstadoComponent,
    MenuDeAccionesComponent,
    HistorialDeRegistroComponent,
    DialogoDePrevisualizacionComponent,
    ConfirmacionComponent,
  ],
  templateUrl: './admin-records-panel.component.html',
  styleUrls: ['./admin-records-panel.component.css']
})
export class AdminRecordsPanelComponent implements OnChanges {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private adminService = inject(AdminService);

  @Input() module!: AdminModule;

  /**
   * El MISMO `module`, con el tipo diciendo la verdad.
   *
   * El `@Input` esta declarado con `!` (asercion de no-nulo), pero el shell le
   * pasa `getSelectedModule(areaId)`, que devuelve `undefined` cuando el rol no
   * tiene modulos asignados — el caso del rol `lider`. La plantilla necesita
   * encadenamiento opcional para no reventar, y con un tipo no-nulable el
   * compilador avisa siete veces (NG8102/NG8107) de que ese `?.` sobra.
   *
   * La revision propuso quitar los `?.`. No se hace: reintroduciria el fallo
   * que arreglan. Lo que estaba mal era el TIPO, no la proteccion.
   *
   * Arreglar el `@Input` de raiz obliga a tocar sus diecinueve usos en este
   * fichero; esta vista acota el cambio a la plantilla sin mentir sobre lo que
   * puede llegar.
   */
  protected get moduloOpcional(): AdminModule | undefined {
    return this.module as AdminModule | undefined;
  }
  @Input() roleId = 'webmaster';
  @Input() divipola: Record<string, string[]> = {};
  @Input() session: { fullName?: string; email?: string } | null = null;
  @Output() elementoRevisionLocal = new EventEmitter<ElementoDeRevisionLocal>();
  /** Lleva a la misma bandeja institucional: no crea una segunda bandeja por Festival. */
  @Output() solicitudesFestival = new EventEmitter<string>();
  /**
   * Pide abrir el alta de Festival desde la consola.
   *
   * ANTES ESTE EVENTO LLEVABA A OTRA PANTALLA. El botón decía «Gestionar organizaciones» y
   * navegaba a Organizaciones, porque el alta de un Festival solo existía en el canal externo:
   * todo lo que el Programa conoce y nadie ha reclamado se quedaba sin poder registrarse. Ahora
   * abre el alta administrativa, que usa exactamente las mismas piezas que el asistente externo.
   */
  @Output() registroFestival = new EventEmitter<void>();

  /**
   * Pide abrir la ficha de un Festival concreto.
   *
   * EL FESTIVAL ES LA ENTIDAD PRINCIPAL y sus Ediciones cuelgan de él. La ficha es donde se ven
   * juntas; esta tabla solo sabe listar.
   */
  @Output() fichaFestival = new EventEmitter<string>();


  // Local state
  records = signal<RegistroAdministrativo[]>([]);
  q = signal<string>('');
  message = signal<string>('');

  previewRecord = signal<RegistroAdministrativo | null>(null);
  festivalDetail = signal<FichaDeFestivalEnRevision | null>(null);
  festivalDetailLoading = signal(false);
  creatingInstitutionalDraft = signal(false);
  institutionalDraftOpen = signal(false);
  institutionalDraftId = signal<number | null>(null);
  institutionalDraftTitle = signal('');
  institutionalDraftForm = signal({ nombre: '', descripcion: '', periodicidad: '', correoContacto: '', nivelCobertura: 'municipal', codigoDepartamento: '', codigoMunicipio: '', telefonoCelular: '', instagram: '', facebook: '', paginaWeb: '', otroEnlace: '', observacionesContacto: '', practicasMusicalesIds: [] as number[], territoriosSonorosIds: [] as number[] });
  festivalCatalogs = signal<{ practicasMusicales: CatalogoDeFestival[]; territoriosSonoros: CatalogoDeFestival[] }>({ practicasMusicales: [], territoriosSonoros: [] });
  institutionalDraftSaving = signal(false);

  // Sorting
  /**
   * Las columnas por las que esta tabla se deja ordenar, y el nombre que el servidor les da.
   *
   * LA LISTA VIVE AQUI Y NO EN LA PLANTILLA para que la cabecera, la flecha y el `aria-sort` no
   * puedan decir tres cosas distintas: los tres salen de la misma fila.
   */
  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'titulo', etiqueta: 'Nombre / Título' },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'territorio', etiqueta: 'Territorio' },
    { id: 'actualizacion', etiqueta: 'Actualización', clases: 'hidden lg:table-cell' },
  ];

  /**
   * Las columnas que se pintan de verdad, en su orden.
   *
   * «ORGANIZACION» SOLO EXISTE EN FESTIVALES y no se ordena por ella —el API no la admite como
   * criterio—, y «Acciones» nunca se ordena. Antes iban intercaladas a mano dentro del bucle de la
   * cabecera, con la de Organización insertada justo después de «Estado» y un `@if` en medio; al
   * declararlas aquí, la fila de cabeceras es la misma pieza que en las otras nueve tablas.
   */
  readonly columnasVisibles = computed<readonly ColumnaDeTabla[]>(() => {
    const columnas: ColumnaDeTabla[] = [];
    for (const columna of this.COLUMNAS) {
      columnas.push(columna);
      if (this.isFestivalModule && columna.id === 'estado') {
        columnas.push({ id: 'organizacion', etiqueta: 'Organización', ordenable: false, clases: 'hidden xl:table-cell' });
      }
    }
    columnas.push({ id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' });
    return columnas;
  });

  /**
   * El orden que DIBUJA la cabecera: el que el servidor aplicó, no el que se pidió.
   *
   * Si se pide una columna que el API no admite, responde con la suya; pintar lo pedido dejaría
   * una flecha sobre una cabecera que no ordenó nada.
   */
  // El literal y no `this.ORDEN_POR_OMISION`: la constante se declara más abajo en la clase y los
  // inicializadores corren en orden de escritura. Una prueba comprueba que los dos coinciden.

  /** Los estados, en la forma que pide el filtro compartido, con «todos» delante. */
  readonly filtroDeEstado = computed<readonly OpcionDeFiltro[]>(() => [
    { id: 'todos', etiqueta: 'Todos los estados', conteo: this.totalDeEstados() },
    ...this.estados().map(o => ({ id: o.codigo, etiqueta: o.etiqueta, conteo: o.total })),
  ]);

  /**
   * Y los territorios. Son 32: es el filtro que hacía falta acotar.
   *
   * Se apoya en `opcionesDeTerritorio`, que ya añade de vuelta el departamento elegido cuando el
   * servidor deja de listarlo por quedarse sin filas: sin eso, el control se quedaría en blanco
   * mientras la tabla sigue filtrada por él.
   */
  readonly filtroDeTerritorio = computed<readonly OpcionDeFiltro[]>(() => [
    { id: 'todos', etiqueta: 'Todo el país', conteo: this.totalDeTerritorios() },
    // LOS NOMBRES TERRITORIALES NUNCA EN MAYUSCULAS SOSTENIDAS: el servidor manda «ANTIOQUIA» y
    // DIVIPOLA es la fuente, así que la corrección es de presentación. Lo hacía la plantilla con
    // el `pipe`; al dejar de ser un `<select>`, tiene que hacerlo quien arma las opciones.
    ...this.opcionesDeTerritorio().map(o => ({ id: String(o.codigo), etiqueta: nombrePropio(o.etiqueta), conteo: o.total })),
  ]);

  readonly orden = new OrdenDeTabla('prioridad', 'asc', 'Pendientes primero');

  /**
   * El orden con el que se abre la tabla: primero lo que espera a alguien.
   *
   * NO ES NINGUNA DE LAS CUATRO COLUMNAS, y por eso ninguna cabecera sale marcada al entrar. Es
   * deliberado: fingir que la tabla está ordenada por «Estado» cuando lo que hace es poner delante
   * lo urgente sería decir algo que no es.
   */
  readonly ORDEN_POR_OMISION = 'prioridad';

  ordenPedido = signal<string>(this.ORDEN_POR_OMISION);
  direccionPedida = signal<'asc' | 'desc'>('asc');
  estadoActivo = signal<string>('todos');
  departamentoActivo = signal<string>('todos');
  mostrarBorradores = signal(true);

  /** Las facetas y el orden que el SERVIDOR aplicó, tal como vinieron en la respuesta. */
  estados = signal<FacetaAdministrativa[]>([]);
  territorios = signal<FacetaAdministrativa[]>([]);
  ordenAplicado = signal<string>(this.ORDEN_POR_OMISION);
  direccionAplicada = signal<string>('asc');


  // Auditoria del panel, dos defectos corregidos en este fichero:
  // 1) Las acciones que no se pueden deshacer desde la interfaz (publicar,
  //    archivar, rechazar y aprobar sin revisión) ahora
  //    confirman nombrando QUE se va a hacer y SOBRE QUE registro; el resto
  //    (buscar, filtrar, guardar borrador, abrir modales) no confirma nada,
  //    porque una confirmacion de mas ensena a aceptar sin leer.
  // 2) Los botones que disparan una peticion se deshabilitan mientras esta en
  //    vuelo con estas senales, puestas a false en las DOS ramas (next y
  //    error), para que un doble clic no cree registros duplicados.
  cargandoRegistros = signal<boolean>(false);
  generandoPlantilla = signal<boolean>(false);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['module'] && this.module) {
      this.resetStateAndLoad();
    }
  }

  resetStateAndLoad() {
    this.records.set([]);
    // CAMBIAR DE MODULO LIMPIA EL FILTRO. El estado «en revisión» de Festivales puede no existir en
    // otro módulo, y el departamento tampoco: arrastrarlos dejaba la tabla nueva vacía sin decir
    // por qué, con el desplegable señalando algo que ese módulo no tiene.
    //
    // AQUI SE LIMPIABAN ADEMAS TRES ESTADOS DEL FORMULARIO CRUD heredado —los valores del
    // formulario, el modal de edición y el de preparación de ficha—, retirados en una revisión anterior.
    this.estadoActivo.set('todos');
    this.departamentoActivo.set('todos');
    this.mostrarBorradores.set(true);
    this.ordenPedido.set(this.ORDEN_POR_OMISION);
    this.direccionPedida.set('asc');
    this.estados.set([]);
    this.territorios.set([]);
    this.previewRecord.set(null);
    this.loadRecords();
  }


  /**
   * Buscar: guarda el texto y consulta.
   *
   * LLEGA CON EL TEXTO YA ESPERADO por el buscador compartido. Antes había que pulsar Intro, que
   * era además la única forma: no había ni botón.
   */
  buscarRegistros(texto: string): void {
    this.q.set(texto);
    this.loadRecords();
  }

  loadRecords() {
    this.cargandoRegistros.set(true);
    this.message.set('Consultando registros...');
    // EL ORDEN Y LOS FILTROS VIAJAN AL SERVIDOR. Esta ruta devuelve una página de como mucho cien
    // filas de hasta quinientas: ordenar o filtrar aquí trabajaría sobre lo ya traído y dejaría
    // fuera el resto, con la tabla afirmando cosas que no puede saber.
    const peticion: { moduleId: string; [clave: string]: unknown } = {
      moduleId: this.module.id,
      q: this.q(),
      orden: this.ordenPedido(),
      direccion: this.direccionPedida(),
      limit: 100 };
    // «todos» no viaja: el servidor ya trata la ausencia del parámetro como «sin filtro».
    if (this.estadoActivo() !== 'todos') peticion['estado'] = this.estadoActivo();
    if (this.departamentoActivo() !== 'todos') peticion['departamento'] = this.departamentoActivo();
    if (!this.mostrarBorradores()) peticion['incluirBorradores'] = false;

    this.adminService.cargarRegistrosDeLaConsola(peticion).subscribe({
      next: (payload) => {
        this.cargandoRegistros.set(false);
        this.records.set(payload.items || []);
        // EL CODIGO NO SALE AL DESPLEGABLE. El servidor manda la faceta con su etiqueta, pero
        // para los estados manda el código crudo: el filtro de Festivales ofrecía elegir entre
        // «ajustes_solicitados», «en_revision» y «publicado». Medido.
        this.estados.set((payload.estados || []).map((faceta: FacetaAdministrativa) => ({
          ...faceta,
          etiqueta: etiquetaDeEstado(faceta.etiqueta || faceta.codigo),
        })));
        this.territorios.set(payload.territorios || []);
        this.ordenAplicado.set(payload.orden || this.ORDEN_POR_OMISION);
        this.direccionAplicada.set(payload.direccion || 'asc');
        this.orden.fijar(payload.orden || this.ORDEN_POR_OMISION, payload.direccion === 'desc' ? 'desc' : 'asc');
        this.message.set(`${payload.total || 0} registros encontrados.`);
      },
      error: (err) => {
        this.cargandoRegistros.set(false);
        this.records.set([]);
        this.estados.set([]);
        this.territorios.set([]);
        this.message.set(err.message || 'Error al consultar registros.');
      }
    });
  }

  /**
   * Las opciones del desplegable de territorio, con la elegida siempre presente.
   *
   * SI EL DEPARTAMENTO ELEGIDO SE QUEDA SIN FILAS —porque el filtro de estado o la búsqueda las
   * quitaron— el servidor deja de listarlo, y el control se quedaría en blanco mientras la tabla
   * sigue filtrada por él.
   */
  opcionesDeTerritorio = computed<FacetaAdministrativa[]>(() => {
    const opciones = this.territorios();
    const elegido = this.departamentoActivo();
    if (elegido === 'todos' || opciones.some((opcion) => String(opcion?.codigo) === elegido)) {
      return opciones;
    }
    return [{ codigo: elegido, etiqueta: elegido, total: 0 }, ...opciones];
  });

  totalDeEstados = computed<number>(
    () => this.estados().reduce((suma, estado) => suma + (Number(estado?.total) || 0), 0),
  );

  totalDeTerritorios = computed<number>(
    () => this.territorios().reduce((suma, opcion) => suma + (Number(opcion?.total) || 0), 0),
  );

  /**
   * Ordena por una columna, o le da la vuelta si ya estaba puesta.
   *
   * NO HAY TERCER ESTADO «SIN ORDEN» EN LA CABECERA. Antes el tercer clic devolvía la tabla al
   * orden por prioridad, y eso no se veía por ningún sitio: la cabecera se quedaba sin flecha y el
   * orden cambiaba solo. Para volver a lo pendiente-primero está el botón que lo dice con palabras.
   */
  /**
   * Pulsar una cabecera: ascendente, al revés, y de vuelta al orden propio de la lista.
   *
   * SE ALTERNA SOBRE LO PEDIDO, NO SOBRE LO DIBUJADO. Son dos estados distintos: la cabecera
   * enseña el orden que el servidor APLICO -para no mentir cuando rechaza una columna- y el
   * segundo clic tiene que invertir lo que se PIDIO. Mezclarlos hacía que, si el servidor
   * respondía con otro orden, el segundo golpe sobre la misma cabecera volviera a ascendente en
   * vez de dar la vuelta.
   *
   * <b>EL TERCER GOLPE DEVUELVE A «PENDIENTES PRIMERO».</b> Ese orden tenía un botón propio en la
   * barra, con el mismo aspecto que los filtros de al lado aunque no filtra nada. Ahora la vuelta
   * se hace desde la misma cabecera por la que se salió, que es la regla de toda la consola para
   * una tabla cuyo orden de partida no es el de ninguna columna. Ver `OrdenDeTabla.alternar`.
   */
  requestSort(key: string) {
    if (this.ordenPedido() === key) {
      if (this.direccionPedida() === 'asc') {
        this.direccionPedida.set('desc');
      } else {
        this.ordenPedido.set(this.ORDEN_POR_OMISION);
        this.direccionPedida.set('asc');
      }
    } else {
      this.ordenPedido.set(key);
      this.direccionPedida.set('asc');
    }
    this.loadRecords();
  }

  filtrarPorEstado(estado: string) {
    const pedido = estado || 'todos';
    if (this.estadoActivo() === pedido) return;
    this.estadoActivo.set(pedido);
    this.loadRecords();
  }

  filtrarPorTerritorio(departamento: string) {
    const pedido = departamento || 'todos';
    if (this.departamentoActivo() === pedido) return;
    this.departamentoActivo.set(pedido);
    this.loadRecords();
  }

  alternarBorradores(): void {
    this.mostrarBorradores.update(mostrar => !mostrar);
    this.loadRecords();
  }

  /**
   * Si hay algo puesto que pueda estar escondiendo Festivales que sí existen.
   *
   * El ojo de borradores cuenta como filtro: oculta registros reales, y quien mira la tabla vacía
   * con los borradores apagados tiene que saber que esa es la razón y no que no hay nada.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.q().trim().length > 0
      || this.estadoActivo() !== 'todos'
      || this.departamentoActivo() !== 'todos'
      || !this.mostrarBorradores(),
  );

  /** Devuelve la tabla a su estado de partida y vuelve a preguntar. */
  limpiarFiltros(): void {
    this.q.set('');
    this.estadoActivo.set('todos');
    this.departamentoActivo.set('todos');
    this.mostrarBorradores.set(true);
    this.loadRecords();
  }



  /** El estado con su nombre. Ver `etiquetaDeEstado`: ningún código crudo llega a la pantalla. */
  statusText(status: string): string {
    return etiquetaDeEstado(status);
  }

  coverageText(coverage: string): string {
    return ADMIN_COVERAGE_LEVELS[String(coverage || '').trim().toLowerCase() as keyof typeof ADMIN_COVERAGE_LEVELS] || coverage || 'Sin cobertura';
  }

  statusPillClass(status: string): string {
    const STYLES: Record<string, string> = {
      borrador:            'text-slate-600 bg-slate-100 border border-slate-200',
      en_revision:         'text-blue-700 bg-blue-50 border border-blue-200',
      ajustes_solicitados: 'text-amber-700 bg-amber-50 border border-amber-200',
      aprobado:            'text-emerald-700 bg-emerald-50 border border-emerald-200',
      publicado:           'text-violet-700 bg-violet-50 border border-violet-200',
      rechazado:           'text-red-700 bg-red-50 border border-red-200',
      archivado:           'text-slate-400 bg-slate-50 border border-slate-100' };
    return STYLES[status] || STYLES['borrador'];
  }

  get isFestivalModule(): boolean {
    return this.module?.id === 'festivals';
  }

  organizationLabel(record: RegistroAdministrativo): string {
    return record?.metadata?.organizationName || record?.metadata?.organizer || 'Sin organización administradora';
  }

  responsibleLabel(record: RegistroAdministrativo): string {
    return record?.metadata?.responsibleName || 'Responsable sin registrar';
  }

  // ─────────────────────── El menú de acciones de cada Festival ───────────────────────
  //
  // <b>EL MISMO QUE EN LAS OTRAS CUATRO PANTALLAS.</b> Aquí había un `<details>` escrito a mano con
  // los botones dentro, rotulado «Consultar». Cinco pantallas de la consola gestionan registros y
  // esta era la única que no usaba `app-menu-de-acciones`: sin `role="menu"`, sin recorrido con
  // flechas, sin Escape, con un barrido global del DOM para cerrar los demás desplegables y con los
  // colores escritos como literales. El §13 del plan lo dice sin rodeos: una acción equivalente
  // mantiene nombre, ubicación, comportamiento y jerarquía visual.
  //
  // <b>LOS ROTULOS SON LOS COMUNES.</b> «Historial» —no «Historial y trazabilidad»—, «Previsualizar»
  // y «Ver en el portal» con la regla de siempre: nunca las dos a la vez.

  /**
   * Las acciones que este Festival admite hoy.
   *
   * SALEN DE SU ESTADO Y DE SUS SOLICITUDES, igual que en los demás paneles: una acción que el
   * servidor rechazaría no se ofrece desactivada, simplemente no está.
   */
  accionesDe(record: RegistroAdministrativo): AccionDeRegistro[] {
    const publicado = this.esFestivalPublicado(record);
    const acciones: AccionDeRegistro[] = [
      { id: 'ficha', etiqueta: 'Abrir ficha' },
      { id: 'historial', etiqueta: 'Historial' },
      { id: 'procedencia', etiqueta: 'Procedencia y coincidencias' },
    ];

    if (this.isPendingFestivalReview(record)) {
      // LO QUE SE ESPERA QUE SE HAGA CON ESTA FILA, y por eso sale fuera del desplegable.
      acciones.push({ id: 'revisar', etiqueta: 'Revisar Festival', tono: 'principal' });
    }
    if (this.canCreateInstitutionalDraft(record)) {
      acciones.push({
        id: 'crear-propuesta',
        etiqueta: 'Crear propuesta de cambio',
        deshabilitada: this.creatingInstitutionalDraft(),
        motivo: this.creatingInstitutionalDraft() ? 'Se está creando una propuesta.' : undefined });
    }
    if (this.institutionalDraftIdOf(record) !== null) {
      acciones.push({ id: 'continuar-propuesta', etiqueta: 'Continuar propuesta de cambio' });
    }
    if (record.metadata?.hasActiveRequest && !this.isPendingFestivalReview(record)) {
      acciones.push({ id: 'solicitud', etiqueta: this.activeRequestLabel(record) });
    }

    // PREVISUALIZAR MIENTRAS NO SE VE; ABRIRLO CUANDO YA SE VE. Es la regla del Bloque 4, y era la
    // única de las cinco pantallas donde no estaba en la fila.
    if (publicado) {
      acciones.push({ id: 'ver', etiqueta: 'Ver en el portal', enlace: `/ecosistema/festivales/${record.id}`, nuevaPestana: true });
      // ELIMINAR ES LO UNICO QUE NO SE DESHACE, y va separada al final del menú por eso.
      acciones.push({ id: 'eliminar', etiqueta: 'Eliminar del ecosistema', tono: 'peligro' });
    } else {
      acciones.push({ id: 'previsualizar', etiqueta: 'Previsualizar' });
    }

    return acciones;
  }

  /** Ejecuta la acción elegida desde la fila. */
  ejecutarAccion(record: RegistroAdministrativo, accion: string): void {
    switch (accion) {
      case 'ficha': this.fichaFestival.emit(record.id); break;
      case 'historial': this.abrirHistorial(record); break;
      case 'procedencia': this.openPreview(record, 'procedencia'); break;
      case 'revisar': this.solicitudesFestival.emit(record.id); break;
      case 'solicitud': this.solicitudesFestival.emit(record.id); break;
      case 'crear-propuesta': this.createInstitutionalDraft(record); break;
      case 'continuar-propuesta': {
        const propuesta = this.institutionalDraftIdOf(record);
        if (propuesta !== null) this.openInstitutionalDraft(propuesta, record);
        break;
      }
      case 'previsualizar': this.previsualizando.set(record); break;
      case 'eliminar': this.pedirEliminacion(record); break;
    }
  }

  /** El Festival cuyo historial se está mirando, o `null`. */
  readonly historialAbierto = signal<RegistroAdministrativo | null>(null);
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.festivales;

  /**
   * Las versiones publicadas del Festival que se está mirando.
   *
   * <b>TRES ESTADOS Y NO DOS:</b> `undefined` mientras no se ha preguntado, `null` si la consulta
   * falló —que no es lo mismo que no haber ninguna— y la lista traducida cuando llegó. El diálogo
   * pinta cada caso distinto.
   */
  readonly versionesDelHistorial = signal<VersionDelRegistro[] | null | undefined>(undefined);

  /** El Festival que se está mirando como se verá en su listado, o `null`. */
  readonly previsualizando = signal<RegistroAdministrativo | null>(null);

  /**
   * Abre el historial del Festival y pide sus versiones publicadas.
   *
   * <b>LAS VERSIONES SE PIDEN AQUI Y NO EN EL DIALOGO</b>, que es genérico y lo usan varias
   * pantallas: ninguna otra tiene perfiles versionados. Se traducen a la forma mínima que el
   * diálogo entiende —etiqueta, detalle, vigente— para que no tenga que saber qué es una versión de
   * Festival.
   *
   * UN FALLO SE DISTINGUE DE «no hay ninguna»: `null` frente a lista vacía. Decir «todavía no se ha
   * publicado ninguna versión» cuando la consulta se cayó es afirmar algo falso sobre el registro.
   */
  abrirHistorial(record: RegistroAdministrativo): void {
    this.historialAbierto.set(record);
    this.versionesDelHistorial.set(undefined);
    this.adminService.cargarPerfilesVersionadosDeFestival(String(record.id)).subscribe({
      next: versiones => this.versionesDelHistorial.set(versiones.map(v => ({
        id: v.id,
        etiqueta: `Versión ${v.numeroVersion}`
          + (v.nombre?.trim() ? ` · ${v.nombre.trim()}` : '')
          + (v.estadoRegistroEtiqueta ? ` · ${v.estadoRegistroEtiqueta}` : ''),
        detalle: v.fechaInicio || v.fechaFin
          ? [v.fechaInicio, v.fechaFin].filter(Boolean).join(' a ')
          : null,
        vigente: v.esVigente }))),
      error: () => this.versionesDelHistorial.set(null) });
  }

  isPendingFestivalReview(record: RegistroAdministrativo): boolean {
    return String(record?.status || '').toLocaleLowerCase('es-CO').includes('revision');
  }

  /** Solo un Festival PUBLICADO puede eliminarse del ecosistema desde aquí -mismo requisito que valida el servidor-. */
  esFestivalPublicado(record: { status?: string }): boolean {
    return String(record?.status || '').toLocaleLowerCase('es-CO') === 'publicado';
  }

  /**
   * ELIMINAR UN REGISTRO DEL ECOSISTEMA, DIRECTO DESDE LA CONSOLA. El criterio es este: hasta ahora un Festival publicado solo se archivaba si la propia
   * organización pedía su retiro y un funcionario aprobaba esa solicitud. Esto no espera esa
   * solicitud -piénsese en un Festival duplicado o que incumple las bases-.
   *
   * ES UN ARCHIVADO, NO UN BORRADO REAL: el registro conserva su historial y su auditoría, solo
   * deja de estar público. Pide el motivo primero y confirma después -mismo orden que ya usa
   * `accionRapida()` en la bandeja de Solicitudes para sus propias acciones irreversibles-.
   */
  /**
   * El Festival que se está a punto de eliminar del ecosistema, con el motivo que se escribe.
   *
   * <b>ANTES ERAN `window.prompt` Y `window.confirm`.</b> Dos diálogos del navegador, sin estilo,
   * imposibles de recorrer con teclado como el resto de la consola, imposibles de comprobar en una
   * prueba, y bloqueados sin aviso en algunos contextos —lo que dejaba la acción muerta sin decirlo—.
   * Las demás pantallas piden el motivo en un formulario con `role="dialog"`; esta era la excepción,
   * y justo en la única acción de la pantalla que no se deshace.
   */
  readonly eliminacionPedida = signal<RegistroAdministrativo | null>(null);

  pedirEliminacion(record: RegistroAdministrativo): void {
    this.eliminacionPedida.set(record);
  }

  cancelarEliminacion(): void { this.eliminacionPedida.set(null); this.errorDeLaEliminacion.set(''); }

  /**
   * ELIMINAR UN REGISTRO DEL ECOSISTEMA, DIRECTO DESDE LA CONSOLA. El criterio es este: hasta ahora un Festival publicado solo se archivaba si la propia
   * organización pedía su retiro y un funcionario aprobaba esa solicitud. Esto no espera esa
   * solicitud -piénsese en un Festival duplicado o que incumple las bases-.
   *
   * ES UN ARCHIVADO, NO UN BORRADO REAL: el registro conserva su historial y su auditoría, solo
   * deja de estar público. El motivo es obligatorio y por eso el botón no se activa sin él.
   */
  /** Lo que falló al eliminar, para enseñarlo DENTRO del diálogo y no perder el motivo escrito. */
  readonly errorDeLaEliminacion = signal('');

  eliminarFestivalDelEcosistema(motivo: string): void {
    const record = this.eliminacionPedida();
    if (!record || !motivo.trim()) return;
    this.errorDeLaEliminacion.set('');
    const nombre = record?.title || record?.name || 'este Festival';

    this.adminService.archivarFestival(Number(record.id), motivo.trim()).subscribe({
      next: () => {
        this.eliminacionPedida.set(null);
        this.errorDeLaEliminacion.set('');
        this.message.set(`«${nombre}» fue eliminado del ecosistema.`);
        this.loadRecords();
      },
      error: (error: unknown) => {
        // NO SE CIERRA EL DIALOGO. Cerrarlo tiraba el motivo recién escrito, y ante un fallo
        // pasajero obligaba a redactarlo otra vez. El error se enseña dentro, junto al campo,
        // que es como lo resuelve el mismo diálogo en Mercados.
        this.errorDeLaEliminacion.set(mensajeDeError(error, 'No fue posible eliminar el registro del ecosistema.'));
      } });
  }

  activeRequestLabel(record: RegistroAdministrativo): string {
    switch (record?.metadata?.activeRequestType) {
      case 'retiro': return 'Solicitud de retiro activa';
      case 'propuesta': {
        const status = String(record?.metadata?.activeProposalStatus || '').toLocaleLowerCase('es-CO');
        return status === 'borrador' ? 'Propuesta de cambio en borrador' : status === 'ajustessolicitados' ? 'Propuesta con ajustes solicitados' : 'Propuesta de cambio en revisión';
      }
      case 'vinculacion': return 'Solicitud de vinculación activa';
      default: return 'Solicitud activa';
    }
  }

  canCreateInstitutionalDraft(record: RegistroAdministrativo): boolean {
    return ['historico', 'institucional'].includes(String(record?.metadata?.administrationType || ''))
      && !record?.metadata?.hasActiveRequest;
  }

  institutionalDraftIdOf(record: RegistroAdministrativo): number | null {
    const id = Number(record?.metadata?.institutionalDraftProposalId);
    return Number.isInteger(id) && id > 0 ? id : null;
  }

  createInstitutionalDraft(record: RegistroAdministrativo): void {
    if (this.creatingInstitutionalDraft()) return;
    this.creatingInstitutionalDraft.set(true);
    this.adminService.crearBorradorInstitucionalDeFestival(Number(record.id)).subscribe({
      next: result => {
        this.creatingInstitutionalDraft.set(false);
        this.openInstitutionalDraft(Number(result.id), record);
        this.message.set(result?.existente ? 'Se abrió la propuesta de cambio activa.' : 'Propuesta de cambio creada. Completa sus cambios antes de enviarla a revisión.');
        this.loadRecords();
      },
      error: error => {
        this.creatingInstitutionalDraft.set(false);
        this.message.set(error?.message || 'No fue posible crear el borrador institucional.');
      } });
  }

  openInstitutionalDraft(proposalId: number, record: RegistroAdministrativo): void {
    this.institutionalDraftTitle.set(record?.title || record?.name || 'Festival');
    this.institutionalDraftId.set(proposalId);
    this.institutionalDraftOpen.set(true);
    this.adminService.cargarFichaDePropuesta(proposalId).subscribe({
      next: detail => {
        const proposal = detail?.propuesta ?? {};
        this.institutionalDraftForm.set({ nombre: proposal.nombre ?? record?.title ?? '', descripcion: proposal.descripcion ?? '', periodicidad: proposal.periodicidad ?? '', correoContacto: proposal.correoContacto ?? '', nivelCobertura: proposal.nivelCobertura ?? 'municipal', codigoDepartamento: proposal.codigoDepartamento ?? '', codigoMunicipio: proposal.codigoMunicipio ?? '', telefonoCelular: proposal.telefonoContacto ?? '', instagram: proposal.instagram ?? '', facebook: proposal.facebook ?? '', paginaWeb: proposal.paginaWeb ?? '', otroEnlace: proposal.otroEnlace ?? '', observacionesContacto: proposal.observacionesContacto ?? '', practicasMusicalesIds: (proposal.practicasMusicales ?? []).map((item: CatalogoDeFestival) => Number(item.id)), territoriosSonorosIds: (proposal.territoriosSonoros ?? []).map((item: CatalogoDeFestival) => Number(item.id)) });
      },
      error: error => this.message.set(error?.message || 'No fue posible abrir la propuesta de cambio.') });
    this.adminService.cargarCatalogosInstitucionalesDeFestival().subscribe({ next: catalogs => this.festivalCatalogs.set({ practicasMusicales: catalogs?.practicasMusicales ?? [], territoriosSonoros: catalogs?.territoriosSonoros ?? [] }) });
  }

  updateInstitutionalDraftField(field: keyof { nombre: string; descripcion: string; periodicidad: string; correoContacto: string; nivelCobertura: string; codigoDepartamento: string; codigoMunicipio: string; telefonoCelular: string; instagram: string; facebook: string; paginaWeb: string; otroEnlace: string; observacionesContacto: string }, value: string): void {
    this.institutionalDraftForm.update(current => ({ ...current, [field]: value }));
  }

  toggleInstitutionalCatalog(field: 'practicasMusicalesIds' | 'territoriosSonorosIds', id: number, checked: boolean): void {
    this.institutionalDraftForm.update(current => ({ ...current, [field]: checked ? [...new Set([...current[field], id])] : current[field].filter(currentId => currentId !== id) }));
  }

  saveInstitutionalDraft(send = false): void {
    const proposalId = this.institutionalDraftId();
    if (!proposalId || this.institutionalDraftSaving()) return;
    this.institutionalDraftSaving.set(true);
    const finish = (text: string) => { this.institutionalDraftSaving.set(false); this.message.set(text); if (send) this.institutionalDraftOpen.set(false); this.loadRecords(); };
    const fail = (error: unknown) => { this.institutionalDraftSaving.set(false); this.message.set(mensajeDeError(error, 'No fue posible guardar la propuesta de cambio.')); };
    this.adminService.guardarBorradorInstitucionalDeFestival(proposalId, this.institutionalDraftForm()).subscribe({
      next: () => send ? this.adminService.enviarBorradorInstitucionalARevision(proposalId).subscribe({ next: () => finish('Propuesta de cambio enviada a revisión.'), error: fail }) : finish('Propuesta de cambio guardada.'),
      error: fail });
  }

  /*
   * AQUI ESTABAN DOS `@HostListener` Y UN BARRIDO GLOBAL DEL DOM.
   *
   * El desplegable escrito a mano era un `<details>`, que no se cierra solo: hacia falta escuchar
   * cada clic y cada Escape del documento y recorrer `document.querySelectorAll` para quitarles el
   * atributo `open` a los demas. `app-menu-de-acciones` ya resuelve eso dentro de si mismo, y
   * ademas hace lo que aquello no hacia: `role="menu"`, recorrido con flechas y devolver el foco
   * al disparador al cerrar.
   */

  /**
   * Abre la procedencia y las coincidencias de un Festival.
   *
   * <b>YA NO PIDE EL HISTORIAL.</b> Hacia una segunda lectura de la bitacora —`grupo: 'festivales'`,
   * diez lineas— para pintarla con una lista propia. Esa era la TERCERA forma de leer el mismo
   * dato en la consola; ahora la abre `app-historial-de-registro`, igual que en las otras cuatro
   * pantallas. Y tampoco arma ya una segunda ficha: eso lo hace `app-admin-ficha-festival`.
   */
  openPreview(record: RegistroAdministrativo, _vista: 'procedencia' = 'procedencia'): void {
    this.previewRecord.set(record);
    this.festivalDetail.set(null);
    if (!this.isFestivalModule) return;
    this.festivalDetailLoading.set(true);
    this.adminService.cargarFichaInstitucionalDeFestival(Number(record.id)).subscribe({
      next: detail => {
        this.festivalDetailLoading.set(false);
        this.festivalDetail.set(detail);
      },
      error: () => this.festivalDetailLoading.set(false) });
  }

  /*
   * AQUI ESTABA `changeRecordStatus`, el desplegable «Cambiar estado» de cada fila.
   *
   * Solo se pintaba para los modulos genericos, retirados en una revisión anterior. Para un Festival nunca
   * existio, y con razon: su estado lo mueve el circuito de revision institucional —enviar,
   * aprobar, pedir ajustes—, no un desplegable que salta ese circuito desde la tabla.
   */







  // Field grouping logic
  get fieldGroups() {
    const groups: Record<string, AdminField[]> = { control: [], basic: [], contact: [], links: [], location: [], metrics: [] };
    (this.module?.fields || []).forEach((field) => {
      if (CONTROL_FIELD_NAMES.has(field.name)) groups['control'].push(field);
      else if (CONTACT_FIELD_NAMES.has(field.name)) groups['contact'].push(field);
      else if (LINK_FIELD_NAMES.has(field.name)) groups['links'].push(field);
      else if (TERRITORY_FIELD_NAMES.has(field.name)) groups['location'].push(field);
      else if (METRIC_FIELD_NAMES.has(field.name) || field.type === 'number') groups['metrics'].push(field);
      else groups['basic'].push(field);
    });
    return groups;
  }

  get hasTerritory() {
    return (this.module?.fields || []).some((f) => f.name === 'department');
  }





  // --- Excel Templates Generation ---
  async handleDownloadTemplate() {
    this.generandoPlantilla.set(true);
    try {
      this.message.set('Generando plantilla optimizada...');
      
      const fields = this.module.fields.filter(f => f.name !== 'id' && f.name !== 'status');
      const exampleRow = fields.map(f => {
        const name = f.name;
        if (name === 'name' || name === 'title') {
          if (this.module.id === 'festivals') return 'Festival de Música del Pacífico';
          if (this.module.id === 'musicSchools') return 'Escuela de Música y Tradición';
          if (this.module.id === 'musicMarkets') return 'Mercado del Ecosistema de la Música';
          if (this.module.id === 'luteria') return 'Taller del Luthier de Viento';
          if (this.module.id === 'agenda') return 'Concierto de Gala de la Filarmónica';
          if (this.module.id === 'news') return 'Resultados de Convocatoria Nacional';
          return 'Ejemplo de Registro';
        }
        if (name === 'versionsCount' || name === 'editionsCount') return 12;
        if (name === 'lastEditionDate' || name === 'date' || name === 'publishedDate' || name === 'currentYearStartDate' || name === 'currentYearEndDate') return '2026-05-15';
        if (name === 'description' || name === 'summary' || name === 'contentHtml' || name === 'lead') {
          return `Ejemplo de descripción. Por favor, reemplace esta fila completa con sus datos reales.`;
        }
        if (name.toLowerCase().includes('email')) return 'contacto@ejemplo.com';
        if (name.toLowerCase().includes('phone')) return '+57 300 123 4567';
        if (name.toLowerCase().includes('url')) return 'https://www.ejemplo.com';
        if (name === 'department') return 'Nariño';
        if (name === 'municipality') return 'Pasto';
        if (name === 'coverageLevel') return 'Municipal';
        if (name === 'students' || name === 'activeGroupsCount' || name === 'trainingCapacity' || name === 'sortOrder') return 5;
        if (name === 'hasCurrentYearEdition' || name === 'isActiveSchool') return 'Sí';
        if (name === 'directorName' || name === 'contactName') return 'Juan Pérez';
        if (name === 'actorType') return 'individual';
        return 'Dato de Ejemplo';
      });

      // LA PLANTILLA SE CONSTRUYE EN UNA PIEZA COMPARTIDA; aquí solo se dice qué opciones tiene cada
      // campo, que es lo único que depende de este panel.
      const opcionesDe = (field: AdminField): string[] => {
        let opts: string[] = [];
        if (field.options?.length) opts = field.options.map(o => o.label || o.value);
        else if (field.name === 'coverageLevel') opts = Object.values(ADMIN_COVERAGE_LEVELS);
        else if (field.name === 'department') opts = this.departmentsList.map(titleCaseEs);
        else if (field.name === 'municipality') {
          opts = [...new Set(Object.values(this.divipola || {}).flat() as string[])].map(titleCaseEs);
        }
        else if (field.type === 'checkbox') opts = ['Sí', 'No'];

        return opts;
      };
      await descargarPlantillaDeImportacion({
        nombreDelArchivo: `plantilla_${this.module.id}`,
        campos: fields,
        filaDeEjemplo: exampleRow,
        opcionesDe,
      });
      
      this.message.set('Plantilla Excel descargada con listas y validaciones.');
    } catch (error: unknown) {
      this.message.set('Error al descargar plantilla: ' + mensajeDeError(error, 'Error desconocido'));
    } finally {
      this.generandoPlantilla.set(false);
    }
  }

  // --- Preparación local de una ficha ---





  territoryFieldNames = TERRITORY_FIELD_NAMES;

  isValEmpty(val: unknown): boolean {
    return val === undefined || val === null || String(val).trim() === '';
  }

  titleCaseEs(text: string): string {
    if (!text) return '';
    return text.split(' ').map(w => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase()).join(' ');
  }

  /**
   * Los departamentos de DIVIPOLA, en orden alfabético español.
   *
   * SE QUEDA AUNQUE EL FORMULARIO SE HAYA IDO. Nació para el desplegable del formulario CRUD
   * heredado, retirado en una revisión anterior, pero quien lo usa hoy es el generador de la plantilla
   * Excel: escribe con él la lista de validación de la columna «department», y sin esa lista la
   * plantilla deja de proponer valores y empieza a admitir cualquier cosa escrita a mano.
   */
  get departmentsList(): string[] {
    return Object.keys(this.divipola || {}).sort((a, b) => a.localeCompare(b, 'es'));
  }

  formatLocation(dept: string | undefined, muni: string | undefined): string {
    return [dept, muni].filter(val => !!val).join(' / ');
  }

}
