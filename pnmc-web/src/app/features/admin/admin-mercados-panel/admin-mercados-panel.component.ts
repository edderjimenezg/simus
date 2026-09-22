import { CommonModule } from '@angular/common';
import { SelectorMultipleComponent } from '../../../shared/components/ui/selector-multiple/selector-multiple.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { ConmutadorDeOjoComponent } from '../../../shared/components/ui/conmutador-de-ojo/conmutador-de-ojo.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { LucideDownload, LucidePlus, LucideRefreshCw } from '@lucide/angular';
import { descargarPlantillaDeImportacion } from '../../../shared/utils/plantilla-de-importacion';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { AltaAdministrativaService, CoincidenciaDeAlta } from '../../../core/services/alta-administrativa.service';
import { CampoDeLaFichaDeMercado, SECCIONES_DE_LA_FICHA_DE_MERCADO } from '../../../core/services/campos-de-la-ficha-de-mercado';
import { CatalogService, TerritorioConCodigo } from '../../../core/services/catalog.service';
import { AdminService, OrganizacionAdministrativa } from '../../../core/services/admin.service';
import { PERIODICIDADES, pideDetalle } from '../../../core/vocabularios/periodicidad';
import { etiquetaDeCobertura } from '../../../core/vocabularios/cobertura';
import { PASOS_DE_UN_ALTA } from '../../../core/vocabularios/pasos-de-un-alta';
import { AsistenteDeAltaComponent } from '../../../shared/components/ui/asistente-de-alta/asistente-de-alta.component';
import {
  CatalogosDeMercado,
  EdicionDeMercado,
  EdicionDeMercadoParaGuardar,
  ETIQUETAS_ESTADO_EDICION,
  ETIQUETAS_VISIBILIDAD_EDICION,
  ETIQUETAS_ESTADO_MERCADO,
  FestivalElegible,
  Mercado,
  MercadoParaGuardar,
  MercadosService,
  ObservacionParaGuardar,
} from '../../../core/services/mercados.service';

/** Lo que el formulario tiene en la mano mientras se escribe. */
interface FormularioDeMercado extends MercadoParaGuardar {
  id: number | null;
}

/**
 * Los Mercados Musicales en la consola.
 *
 * <b>UN MERCADO ES UN PROCESO, NO UNA SECCION DE FESTIVALES.</b> Tiene su registro, su ficha, sus
 * estados, su territorio y su organización responsable, igual que un festival, y reutiliza sus
 * piezas en vez de duplicarlas: la misma barra de lista, la misma tabla que ordena en el servidor,
 * el mismo indicador de estado y el mismo catálogo territorial.
 *
 * <b>LA PREGUNTA DEL FESTIVAL ES LO UNICO PROPIO.</b> Un mercado puede ocurrir por su cuenta o en
 * el marco de un festival, y cuando ocurre dentro de uno eso es una RELACION entre dos registros,
 * no un nombre escrito a mano. Por eso el selector solo aparece si la respuesta es que sí, solo
 * lista festivales de la misma organización, y dice qué hacer cuando esa organización todavía no
 * tiene ninguno registrado.
 */
@Component({
  selector: 'app-admin-mercados-panel',
  standalone: true,
  imports: [
    SelectorMultipleComponent, DialogoDirective, BarraDeListaComponent,
    CommonModule,
    FormsModule,
    BotonComponent,
    BuscadorDeListaComponent,
    CabeceraDeTablaComponent,
    EstadoDeListaComponent,
    ListaSinFilasComponent,
    FiltroDesplegableComponent,
    IndicadorDeEstadoComponent,
    NombrePropioPipe,
    PanelLateralComponent,
    MenuDeAccionesComponent,
    ConmutadorDeOjoComponent,
    HistorialDeRegistroComponent,
    DialogoDePrevisualizacionComponent,
    ConfirmacionComponent,
    AsistenteDeAltaComponent,
    LucideDownload,
    LucidePlus,
    LucideRefreshCw,
  ],
  templateUrl: './admin-mercados-panel.component.html',
})
export class AdminMercadosPanelComponent {
  private readonly mercados = inject(MercadosService);
  private readonly alta = inject(AltaAdministrativaService);
  private readonly catalogo = inject(CatalogService);
  private readonly admin = inject(AdminService);

  readonly enabled = input(false);

  readonly lista = signal<Mercado[]>([]);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  /** El vocabulario de periodicidad, el mismo que el alta y que el asistente. */
  readonly PERIODICIDADES = PERIODICIDADES;
  readonly pideDetalle = pideDetalle;

  readonly busqueda = signal('');
  readonly filtroEstado = signal('todos');

  /**
   * El ojo de borradores, como en Festivales: los oculta el SERVIDOR, sobre el total.
   * Un borrador no es trabajo pendiente del Programa y estorba en la lista de lo que sí lo es.
   */
  readonly mostrarBorradores = signal(true);

  /**
   * Pide abrir la ficha del mercado, que la pinta el armazón de la consola.
   *
   * <b>LA PINTA EL ARMAZON Y NO ESTE PANEL</b>, igual que la de un Festival: la ficha necesita
   * saber si quien mira puede publicar y tiene que poder llevar a la organización responsable, que
   * vive en otra sección. Las dos cosas las sabe el armazón, no la lista.
   */
  readonly fichaMercado = output<string>();

  /** El mercado cuyo historial se está leyendo, y el que se está previsualizando en el directorio. */
  readonly historialAbierto = signal<Mercado | null>(null);
  readonly previsualizando = signal<Mercado | null>(null);
  readonly generandoPlantilla = signal(false);
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.mercados;

  readonly catalogos = signal<CatalogosDeMercado>({ alcances: [], modalidades: [], practicasMusicales: [], territoriosSonoros: [] });
  readonly territorios = signal<TerritorioConCodigo[]>([]);
  readonly organizaciones = signal<OrganizacionAdministrativa[]>([]);
  readonly festivales = signal<FestivalElegible[]>([]);

  /**
   * En qué punto está la consulta de los festivales de esa organización.
   *
   * <b>UN FALLO NO ES UNA RESPUESTA.</b> Convertir el error en una lista vacía hacía que la pantalla
   * afirmara «esta organización todavía no tiene ningún festival» sin haber podido preguntarlo, y
   * eso manda a registrar de nuevo algo que ya existe.
   */
  readonly consultaDeFestivales = signal<'sin_preguntar' | 'preguntando' | 'respondida' | 'fallida'>('sin_preguntar');

  readonly formulario = signal<FormularioDeMercado | null>(null);

  /** El mercado sobre el que se está decidiendo, y el motivo que se escribe al pedir ajustes. */
  /** El mercado cuyas ediciones se están mirando, y la edición que se está escribiendo. */
  readonly viendoEdiciones = signal<Mercado | null>(null);
  readonly ediciones = signal<EdicionDeMercado[]>([]);
  readonly cargandoEdiciones = signal(false);
  readonly formularioDeEdicion = signal<(EdicionDeMercadoParaGuardar & { id: number | null }) | null>(null);
  readonly guardandoEdicion = signal(false);
  readonly errorDeLaEdicion = signal<string | null>(null);

  readonly decidiendo = signal<Mercado | null>(null);
  readonly motivoDeLosAjustes = signal('');
  readonly errorDeLaDecision = signal<string | null>(null);
  readonly guardando = signal(false);
  readonly errorDelFormulario = signal<string | null>(null);

  private static readonly POR_PAGINA = 20;

  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'nombre', etiqueta: 'Mercado' },
    { id: 'donde', etiqueta: 'Dónde', ordenable: false },
    { id: 'festival', etiqueta: 'En el marco de', ordenable: false },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  readonly FILTRO_DE_ESTADO: readonly OpcionDeFiltro[] = [
    { id: 'todos', etiqueta: 'Todos los estados' },
    { id: 'borrador', etiqueta: 'Borrador' },
    { id: 'en_revision', etiqueta: 'En revisión' },
    { id: 'ajustes_solicitados', etiqueta: 'Ajustes solicitados' },
    { id: 'publicado', etiqueta: 'Publicado' },
    { id: 'archivado', etiqueta: 'Archivado' },
  ];

  readonly NIVELES_DE_COBERTURA: readonly { id: string; etiqueta: string }[] = [
    { id: 'nacional', etiqueta: 'Nacional' },
    { id: 'departamental', etiqueta: 'Departamental' },
    { id: 'municipal', etiqueta: 'Municipal' },
  ];

  /**
   * El orden de la tabla, resuelto en el servidor.
   *
   * <b>SIN ORDEN PROPIO NO HAY TERCER CLIC.</b> La lista no tiene un orden de trabajo distinto del
   * alfabético —no hay «pendientes primero» que recuperar—, así que la cabecera alterna entre
   * ascendente y descendente y ya está.
   */
  readonly orden = new OrdenDeTabla('nombre', 'asc');

  readonly totalDePaginas = computed(() =>
    Math.max(1, Math.ceil(this.total() / AdminMercadosPanelComponent.POR_PAGINA)));

  /**
   * Los festivales que puede elegir el mercado que se está escribiendo.
   *
   * <b>SE PIDEN CUANDO CAMBIA LA ORGANIZACION, Y NO ANTES.</b> Sin organización elegida la
   * pregunta no tiene respuesta posible: no hay un conjunto de festivales «de nadie».
   */
  constructor() {
    effect(() => {
      if (!this.enabled()) return;
      untracked(() => {
        void this.cargar();
        this.cargarLosCatalogos();
      });
    });

    effect(() => {
      this.busqueda();
      this.filtroEstado();
      this.mostrarBorradores();
      if (!untracked(() => this.enabled())) return;
      untracked(() => {
        this.pagina.set(1);
        void this.cargar();
      });
    });

    effect(() => {
      const organizacion = this.formulario()?.organizacionId ?? 0;
      untracked(() => this.cargarFestivalesDe(organizacion));
    });
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    this.mercados.listar({
      q: this.busqueda() || undefined,
      estado: this.filtroEstado(),
      orden: this.orden.columna() || undefined,
      descendente: this.orden.direccion() === 'desc',
      incluirBorradores: this.mostrarBorradores() ? undefined : false,
      pagina: this.pagina(),
      tamano: AdminMercadosPanelComponent.POR_PAGINA,
    }).subscribe({
      next: pagina => {
        this.lista.set(pagina.items ?? []);
        this.total.set(pagina.total ?? 0);
        this.cargando.set(false);
      },
      error: () => {
        this.error.set('No fue posible leer los mercados musicales.');
        this.cargando.set(false);
      },
    });
  }

  private cargarLosCatalogos(): void {
    this.mercados.catalogos().subscribe({
      next: catalogos => this.catalogos.set(catalogos),
      error: () => undefined,
    });
    this.catalogo.fetchDivipolaConCodigos().subscribe({
      next: territorios => this.territorios.set(territorios),
      error: () => undefined,
    });
    // LAS ORGANIZACIONES ACTIVAS, que son las que pueden responder por un proceso. Se piden de una
    // vez y no se paginan aquí: el selector de un formulario no es una lista de trabajo.
    this.admin.cargarOrganizaciones({ estado: 'activa', tamano: 200 }).subscribe({
      next: respuesta => this.organizaciones.set(respuesta.items ?? []),
      error: () => undefined,
    });
  }

  cargarFestivalesDe(organizacion: number): void {
    if (!organizacion) {
      this.festivales.set([]);
      this.consultaDeFestivales.set('sin_preguntar');
      return;
    }
    this.consultaDeFestivales.set('preguntando');
    this.mercados.festivalesElegibles(organizacion).subscribe({
      next: lista => {
        this.festivales.set(lista ?? []);
        this.consultaDeFestivales.set('respondida');
      },
      error: () => {
        this.festivales.set([]);
        this.consultaDeFestivales.set('fallida');
      },
    });
  }

  /** Reintentar, con la organización que hay ahora mismo en el formulario. */
  reintentarLosFestivales(): void {
    this.cargarFestivalesDe(this.formulario()?.organizacionId ?? 0);
  }

  buscar(texto: string): void {
    this.busqueda.set(texto);
  }

  /**
   * Si hay algo puesto que pueda estar escondiendo mercados que sí existen.
   *
   * El ojo de borradores cuenta: oculta registros reales, igual que un filtro, y quien mira una
   * lista vacía con los borradores apagados tiene que saber que esa es la razón.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.busqueda().trim().length > 0 || this.filtroEstado() !== 'todos' || !this.mostrarBorradores(),
  );

  /**
   * Devuelve la lista a su estado de partida.
   *
   * No llama a `cargar()`: el efecto del constructor depende de los tres, vuelve a la primera
   * página y pide la lista. Llamarlo también aquí pediría la misma página dos veces.
   */
  limpiarFiltros(): void {
    this.busqueda.set('');
    this.filtroEstado.set('todos');
    this.mostrarBorradores.set(true);
  }

  ordenarPor(columna: string): void {
    this.orden.alternar(columna);
    this.pagina.set(1);
    void this.cargar();
  }

  irALaPagina(numero: number): void {
    if (numero < 1 || numero > this.totalDePaginas()) return;
    this.pagina.set(numero);
    void this.cargar();
  }

  rotuloDeEstado(estado: string): string {
    return ETIQUETAS_ESTADO_MERCADO[estado] ?? estado;
  }

  tonoDelEstado = tonoDelEstado;
  matizDelEstado = matizDelEstado;

  /** Dónde ocurre el mercado, dicho en una línea y según su nivel de cobertura. */
  donde(mercado: Mercado): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    if (mercado.nivelCobertura === 'departamental') return mercado.nombreDepartamento ?? '—';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || '—';
  }

  municipiosDelDepartamento(): readonly { codigo: string; nombre: string }[] {
    const codigo = this.formulario()?.codigoDepartamento;
    if (!codigo) return [];
    return this.territorios().find(t => t.codigo === codigo)?.municipios ?? [];
  }

  // ─────────────────────────── El formulario ───────────────────────────

  nuevo(): void {
    this.errorDelFormulario.set(null);
    this.coincidencias.set([]);
    this.pasoDelAlta.set(1);
    this.formulario.set({
      id: null,
      nombre: '',
      descripcion: null,
      alcanceId: null,
      modalidadId: null,
      periodicidad: null,
      periodicidadDetalle: null,
      correoMercado: null,
      telefonoMercado: null,
      sitioWebMercado: null,
      instagramMercado: null,
      facebookMercado: null,
      otroEnlaceMercado: null,
      observacionesContacto: null,
      nivelCobertura: 'municipal',
      codigoDepartamento: null,
      codigoMunicipio: null,
      lugarEspecifico: null,
      seRealizaEnElMarcoDeUnFestival: false,
      festivalId: null,
      organizacionId: 0,
      practicasMusicalesIds: [],
      territoriosSonorosIds: [],
    });
  }

  editar(mercado: Mercado): void {
    this.errorDelFormulario.set(null);
    this.formulario.set({
      id: mercado.id,
      nombre: mercado.nombre,
      descripcion: mercado.descripcion,
      alcanceId: mercado.alcanceId,
      modalidadId: mercado.modalidadId,
      periodicidad: mercado.periodicidad,
      periodicidadDetalle: mercado.periodicidadDetalle,
      correoMercado: mercado.correoMercado,
      telefonoMercado: mercado.telefonoMercado,
      sitioWebMercado: mercado.sitioWebMercado,
      instagramMercado: mercado.instagramMercado,
      facebookMercado: mercado.facebookMercado,
      otroEnlaceMercado: mercado.otroEnlaceMercado,
      observacionesContacto: mercado.observacionesContacto,
      nivelCobertura: mercado.nivelCobertura,
      codigoDepartamento: mercado.codigoDepartamento,
      codigoMunicipio: mercado.codigoMunicipio,
      lugarEspecifico: mercado.lugarEspecifico,
      seRealizaEnElMarcoDeUnFestival: mercado.seRealizaEnElMarcoDeUnFestival,
      festivalId: mercado.festivalId,
      organizacionId: mercado.organizacionId,
      // SE ABREN CON LO QUE YA TIENE. Abrirlas vacías haría que guardar un cambio de nombre le
      // borrara las prácticas sin que nadie lo pidiera.
      practicasMusicalesIds: mercado.practicasMusicales.map(x => x.id),
      territoriosSonorosIds: mercado.territoriosSonoros.map(x => x.id),
    });
  }

  cerrarElFormulario(): void {
    this.formulario.set(null);
    this.errorDelFormulario.set(null);
    this.pasoDelAlta.set(1);
  }

  // ───────────────── El alta, que pregunta por pasos como la de un Festival ─────────────────

  readonly pasosDelAlta = PASOS_DE_UN_ALTA;
  readonly pasoDelAlta = signal(1);
  readonly etiquetaDeCobertura = etiquetaDeCobertura;

  /** Cómo se llama la organización elegida, para enseñarla en la revisión sin volver a buscarla. */
  readonly nombreDeLaOrganizacionElegida = computed(() => {
    const id = this.formulario()?.organizacionId;
    if (!id) return '';
    return this.organizaciones().find(o => +o.id === +id)?.nombre ?? '';
  });

  /**
   * Lo que todavía falta para poder registrar, nombrado campo a campo.
   *
   * SE DICE, NO SE DEDUCE DE UN BOTON APAGADO. Un botón deshabilitado sin explicación obliga a
   * recorrer los cinco pasos buscando qué falta, y es exactamente lo que hacía el formulario largo
   * cuando el servidor devolvía el error después de pulsar.
   */
  readonly loQueFaltaParaRegistrar = computed<string[]>(() => {
    const f = this.formulario();
    if (!f) return [];
    const falta: string[] = [];
    if (!f.nombre.trim()) falta.push('el nombre');
    if (!f.organizacionId) falta.push('la organización responsable');
    if (pideDetalle(f.periodicidad) && !(f.periodicidadDetalle ?? '').trim()) falta.push('explicar la periodicidad');
    if (f.nivelCobertura !== 'nacional' && !f.codigoDepartamento) falta.push('el departamento');
    if (f.nivelCobertura === 'municipal' && !f.codigoMunicipio) falta.push('el municipio');
    if (f.seRealizaEnElMarcoDeUnFestival && !f.festivalId) falta.push('el festival en cuyo marco ocurre');
    return falta;
  });

  /** Marca o desmarca un valor de catálogo; la lista la mantiene el formulario, no el control. */
  alternarDeCatalogo(clave: 'practicasMusicalesIds' | 'territoriosSonorosIds', id: number): void {
    this.formulario.update(actual => {
      if (!actual) return actual;
      const lista = actual[clave].includes(id) ? actual[clave].filter(x => x !== id) : [...actual[clave], id];
      return { ...actual, [clave]: lista };
    });
  }

  campo<K extends keyof FormularioDeMercado>(clave: K, valor: FormularioDeMercado[K]): void {
    this.formulario.update(actual => (actual ? { ...actual, [clave]: valor } : actual));
    if (clave === 'nombre') this.buscarCoincidencias();
  }

  // ─────────────────────── Mercados que ya existen con ese nombre ───────────────────────
  //
  // SE PREGUNTA ANTES DE CREAR, NO DESPUES DE DUPLICAR. Dos registros del mismo mercado —uno que
  // incorporó el Programa y otro que registró su organización— no se detectan solos: se quedan los
  // dos en el portal, cada uno con parte de las ediciones. Avisa y no bloquea: hay mercados que de
  // verdad se llaman parecido en departamentos distintos, y quien registra es quien lo sabe.

  readonly coincidencias = signal<CoincidenciaDeAlta[]>([]);
  private temporizadorDeCoincidencias: ReturnType<typeof setTimeout> | null = null;

  private buscarCoincidencias(): void {
    // SOLO AL CREAR: sobre un mercado que ya existe, encontrarse a sí mismo no es un aviso.
    if (this.formulario()?.id !== null) { this.coincidencias.set([]); return; }

    if (this.temporizadorDeCoincidencias) clearTimeout(this.temporizadorDeCoincidencias);
    this.temporizadorDeCoincidencias = setTimeout(async () => {
      const nombre = (this.formulario()?.nombre ?? '').trim();
      this.coincidencias.set(nombre.length < 3 ? [] : await this.alta.coincidenciasDeMercado(nombre));
    }, 600);
  }

  /**
   * Cambiar el nivel de cobertura limpia lo que deja de tener sentido.
   *
   * <b>SIN ESTO, EL GUARDADO FALLA POR ALGO QUE YA NO SE VE.</b> Si alguien elige un municipio y
   * luego pasa a cobertura nacional, el código del municipio sigue en el formulario aunque su campo
   * haya desaparecido, y el CHECK de la base —que exige territorio nulo en nacional— rechaza el
   * guardado señalando un campo que no está en pantalla.
   */
  cambiarCobertura(nivel: string): void {
    this.formulario.update(actual => {
      if (!actual) return actual;
      if (nivel === 'nacional') return { ...actual, nivelCobertura: nivel, codigoDepartamento: null, codigoMunicipio: null };
      if (nivel === 'departamental') return { ...actual, nivelCobertura: nivel, codigoMunicipio: null };
      return { ...actual, nivelCobertura: nivel };
    });
  }

  cambiarDepartamento(codigo: string): void {
    this.formulario.update(actual =>
      actual ? { ...actual, codigoDepartamento: codigo || null, codigoMunicipio: null } : actual);
  }

  /**
   * Responder que no al festival borra el que hubiera elegido.
   *
   * Dejarlo guardado sería una relación que la ficha no enseña y que nadie sabe que existe; el
   * servidor la rechaza y el CHECK de la base también, pero enterarse al guardar es peor.
   */
  cambiarSiEstaEnUnFestival(dentro: boolean): void {
    this.formulario.update(actual =>
      actual ? { ...actual, seRealizaEnElMarcoDeUnFestival: dentro, festivalId: dentro ? actual.festivalId : null } : actual);
  }

  // ─────────────────────────── Un solo menú de acciones por fila ───────────────────────────

  /**
   * Lo que se puede hacer con esta fila, en el orden en que se espera hacerlo.
   *
   * <b>UN SOLO CONTROL POR FILA, COMO EN FESTIVALES.</b> Mercados nació con dos botones sueltos y
   * quedó definido: «festivales tiene un botón
   * particular en acciones y en mercados ahora se montan dos botones». La acción que se espera
   * —«Revisar» sobre lo que está en revisión— sale como principal; lo que no aplica al estado no se
   * pinta, en vez de pintarse apagado.
   */
  accionesDe(mercado: Mercado): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [];
    if (this.sePuedeDecidir(mercado)) acciones.push({ id: 'revisar', etiqueta: 'Revisar', tono: 'principal' });
    // ABRIR FICHA VA PRIMERO, COMO EN FESTIVALES: ver qué es el registro precede a cambiarlo, y es
    // lo único desde donde se puede publicar, retirar o archivar un mercado.
    acciones.push({ id: 'ficha', etiqueta: 'Abrir ficha' });
    acciones.push({ id: 'editar', etiqueta: 'Editar' });
    if (this.tieneEdiciones(mercado)) acciones.push({ id: 'ediciones', etiqueta: 'Ediciones' });
    // PREVISUALIZAR MIENTRAS NO SE VE; ABRIRLO CUANDO YA SE VE. Es la misma regla que en
    // Festivales, y aquí una sola entrada —«Ver en el directorio»— hacía las dos cosas: sobre un
    // mercado publicado abría una previsualización de algo que cualquiera puede ver ya en el
    // portal, y sobre un borrador se llamaba como si llevara al directorio público.
    if (mercado.estadoRegistro === 'publicado') {
      acciones.push({ id: 'ver', etiqueta: 'Ver en el portal', enlace: `/ecosistema/mercados-musicales/${mercado.id}`, nuevaPestana: true });
    } else {
      acciones.push({ id: 'previsualizar', etiqueta: 'Previsualizar' });
    }
    acciones.push({ id: 'historial', etiqueta: 'Historial' });
    // RETIRAR VA LA ULTIMA Y EN ROJO, como en Festivales: es lo único de este menú que la
    // organización no puede deshacer por su cuenta, y solo aplica a lo que está publicado.
    if (mercado.estadoRegistro === 'publicado') {
      acciones.push({ id: 'eliminar', etiqueta: 'Eliminar del ecosistema', tono: 'peligro' });
    }
    return acciones;
  }

  ejecutarAccion(mercado: Mercado, accion: string): void {
    switch (accion) {
      case 'ficha': this.fichaMercado.emit(String(mercado.id)); break;
      case 'revisar': this.abrirLaDecision(mercado); break;
      case 'editar': this.editar(mercado); break;
      case 'ediciones': this.abrirLasEdiciones(mercado); break;
      case 'previsualizar': this.previsualizando.set(mercado); break;
      case 'historial': this.historialAbierto.set(mercado); break;
      case 'eliminar': this.pedirElRetiro(mercado); break;
    }
  }

  // ─────────────────── El ciclo de visibilidad de una edición, en la consola ───────────────────
  //
  // UN SOLO MENU POR FILA, COMO EN LA LISTA DE MERCADOS. Aquí había un botón suelto «Editar» y
  // nada más: una edición se creaba y se guardaba, y se quedaba en borrador para siempre.

  /**
   * Lo que se puede hacer con esta edición, según dónde esté de su ciclo.
   *
   * ARCHIVADA NO OFRECE NADA MAS: reabrir lo archivado sin dejar constancia es peor que no poder
   * reabrirlo. Y ELIMINAR SOLO SOBRE UN BORRADOR: lo que se publicó alguna vez se archiva, porque
   * borrarlo dejaría su rastro en la bitácora apuntando a un registro que ya no existe.
   */
  accionesDeLaEdicion(edicion: EdicionDeMercado): AccionDeRegistro[] {
    if (edicion.estadoVisibilidad === 'archivado') return [];

    const acciones: AccionDeRegistro[] = [{ id: 'editar', etiqueta: 'Editar', tono: 'principal' }];
    if (edicion.estadoVisibilidad === 'publicado') {
      acciones.push({ id: 'despublicar', etiqueta: 'Retirar del portal' });
    } else {
      acciones.push({ id: 'publicar', etiqueta: 'Publicar' });
    }
    acciones.push({ id: 'archivar', etiqueta: 'Archivar' });
    if (edicion.estadoVisibilidad === 'borrador') {
      acciones.push({ id: 'eliminar', etiqueta: 'Eliminar', tono: 'peligro' });
    }
    return acciones;
  }

  ejecutarAccionDeEdicion(edicion: EdicionDeMercado, accion: string): void {
    const mercado = this.viendoEdiciones();
    if (!mercado) return;
    if (accion === 'editar') { this.editarEdicion(edicion); return; }
    if (accion === 'eliminar') { this.edicionPorEliminar.set(edicion); return; }
    if (accion !== 'publicar' && accion !== 'despublicar' && accion !== 'archivar') return;

    const avisos: Record<string, string> = {
      publicar: 'La edición quedó publicada en el portal.',
      despublicar: 'La edición se retiró del portal y volvió a borrador.',
      archivar: 'La edición quedó archivada.',
    };

    this.guardandoEdicion.set(true);
    this.errorDeLaEdicion.set(null);
    this.mercados.cambiarVisibilidadDeEdicion(mercado.id, edicion.id, accion).subscribe({
      next: () => {
        this.guardandoEdicion.set(false);
        this.aviso.set(avisos[accion]);
        this.abrirLasEdiciones(mercado);
      },
      error: (fallo: unknown) => {
        this.guardandoEdicion.set(false);
        this.errorDeLaEdicion.set(this.motivoDe(fallo));
      },
    });
  }

  /** La edición que se está a punto de borrar: se confirma en una ventana, no con `window.confirm`. */
  readonly edicionPorEliminar = signal<EdicionDeMercado | null>(null);

  cancelarLaEliminacionDeLaEdicion(): void { this.edicionPorEliminar.set(null); }

  eliminarLaEdicion(): void {
    const mercado = this.viendoEdiciones();
    const edicion = this.edicionPorEliminar();
    if (!mercado || !edicion) return;

    this.guardandoEdicion.set(true);
    this.errorDeLaEdicion.set(null);
    this.mercados.eliminarEdicion(mercado.id, edicion.id).subscribe({
      next: () => {
        this.guardandoEdicion.set(false);
        this.edicionPorEliminar.set(null);
        this.aviso.set('La edición se eliminó.');
        this.abrirLasEdiciones(mercado);
      },
      error: (fallo: unknown) => {
        this.guardandoEdicion.set(false);
        this.edicionPorEliminar.set(null);
        this.errorDeLaEdicion.set(this.motivoDe(fallo));
      },
    });
  }

  /** Recargar la lista desde el servidor, con los filtros que hay puestos. */
  recargar(): void {
    void this.cargar();
  }

  /**
   * La plantilla de importación de mercados.
   *
   * <b>LA MISMA PIEZA QUE FESTIVALES</b>, con los campos de un mercado. La importación asistida de
   * mercados todavía no existe; la plantilla es la primera mitad de ese bloque y su formato es el
   * que la importación leerá cuando llegue. Se ofrece ya porque el patrón de la consola la incluye y
   * porque quien reúne mercados en una hoja lo hace mucho antes de subirlos.
   */
  async descargarPlantilla(): Promise<void> {
    this.generandoPlantilla.set(true);
    try {
      const campos = [
        { name: 'nombre', label: 'Nombre del mercado', required: true },
        { name: 'descripcion', label: 'Descripción' },
        { name: 'alcance', label: 'Alcance' },
        { name: 'modalidad', label: 'Modalidad' },
        { name: 'periodicidad', label: 'Periodicidad' },
        { name: 'nivelCobertura', label: 'Nivel de cobertura', required: true },
        { name: 'departamento', label: 'Departamento' },
        { name: 'municipio', label: 'Municipio' },
        { name: 'lugarEspecifico', label: 'Lugar específico' },
        { name: 'organizacion', label: 'Organización responsable', required: true },
        { name: 'festival', label: 'Festival en cuyo marco se realiza' },
        { name: 'correoMercado', label: 'Correo de contacto', type: 'Correo' },
        { name: 'telefonoMercado', label: 'Teléfono' },
        { name: 'sitioWebMercado', label: 'Sitio web', type: 'Enlace' },
      ];
      const filaDeEjemplo = [
        'Mercado del Ecosistema de la Música', 'Encuentro anual de circulación y negocios.',
        'Nacional', 'Mixta', 'Anual', 'Municipal', 'Antioquia', 'Medellín', 'Plaza Mayor',
        'Corporación Musical de Antioquia', '', 'contacto@ejemplo.com', '3000000000', 'https://ejemplo.com',
      ];
      const vocabularios: Record<string, readonly string[]> = {
        alcance: this.catalogos().alcances.map(a => a.nombre),
        modalidad: this.catalogos().modalidades.map(m => m.nombre),
        nivelCobertura: this.NIVELES_DE_COBERTURA.map(n => n.etiqueta),
        departamento: this.territorios().map(t => t.nombre),
      };
      await descargarPlantillaDeImportacion({
        nombreDelArchivo: 'plantilla_mercados',
        campos,
        filaDeEjemplo,
        opcionesDe: campo => vocabularios[campo.name] ?? [],
      });
      this.aviso.set('Plantilla Excel descargada con listas y validaciones.');
    } catch {
      this.error.set('No fue posible generar la plantilla.');
    } finally {
      this.generandoPlantilla.set(false);
    }
  }

  // ─────────────────────────── Las ediciones ───────────────────────────

  /**
   * Solo un mercado publicado tiene ediciones.
   *
   * <b>Y NO ES UNA RESTRICCION DE PANTALLA:</b> el servidor contesta 409 a quien lo intente.
   * Anunciar la realización de un proceso que el Programa todavía no ha aprobado sería publicar por
   * la puerta de atrás lo que el circuito de revisión existe para decidir.
   */
  tieneEdiciones(mercado: Mercado): boolean {
    return mercado.estadoRegistro === 'publicado';
  }

  abrirLasEdiciones(mercado: Mercado): void {
    this.viendoEdiciones.set(mercado);
    this.ediciones.set([]);
    this.formularioDeEdicion.set(null);
    this.errorDeLaEdicion.set(null);
    this.cargandoEdiciones.set(true);
    this.mercados.ediciones(mercado.id).subscribe({
      next: lista => {
        this.ediciones.set(lista ?? []);
        this.cargandoEdiciones.set(false);
      },
      error: () => {
        this.ediciones.set([]);
        this.cargandoEdiciones.set(false);
        this.errorDeLaEdicion.set('No fue posible leer las ediciones de este mercado.');
      },
    });
  }

  cerrarLasEdiciones(): void {
    this.viendoEdiciones.set(null);
    this.formularioDeEdicion.set(null);
    this.errorDeLaEdicion.set(null);
  }

  rotuloDeEstadoDeEdicion(estado: string): string {
    return ETIQUETAS_ESTADO_EDICION[estado] ?? estado;
  }

  rotuloDeVisibilidad(visibilidad: string): string {
    return ETIQUETAS_VISIBILIDAD_EDICION[visibilidad] ?? visibilidad;
  }

  nuevaEdicion(): void {
    this.errorDeLaEdicion.set(null);
    // EL AÑO PROPUESTO ES EL SIGUIENTE AL DE LA ULTIMA, y el actual si no hay ninguna: es el que se
    // va a escribir nueve de cada diez veces, y proponerlo ahorra el error más común —repetir un
    // año que ya existe, que el servidor rechaza con un 409—.
    const ultima = this.ediciones()[0]?.anio;
    this.formularioDeEdicion.set({
      id: null,
      anio: ultima ? ultima + 1 : new Date().getFullYear(),
      numeroEdicion: null,
      nombre: null,
      descripcion: null,
      fechaInicio: null,
      fechaFin: null,
      codigoDepartamento: null,
      codigoMunicipio: null,
      lugarEspecifico: null,
      estado: 'en_preparacion',
      estadoVisibilidad: 'borrador',
    });
  }

  editarEdicion(edicion: EdicionDeMercado): void {
    this.errorDeLaEdicion.set(null);
    this.formularioDeEdicion.set({
      id: edicion.id,
      anio: edicion.anio,
      numeroEdicion: edicion.numeroEdicion,
      nombre: edicion.nombre,
      descripcion: edicion.descripcion,
      fechaInicio: edicion.fechaInicio,
      fechaFin: edicion.fechaFin,
      codigoDepartamento: edicion.codigoDepartamento,
      codigoMunicipio: edicion.codigoMunicipio,
      lugarEspecifico: edicion.lugarEspecifico,
      estado: edicion.estado,
      estadoVisibilidad: edicion.estadoVisibilidad,
    });
  }

  campoDeLaEdicion<K extends keyof EdicionDeMercadoParaGuardar>(clave: K, valor: EdicionDeMercadoParaGuardar[K]): void {
    this.formularioDeEdicion.update(actual => (actual ? { ...actual, [clave]: valor } : actual));
  }

  guardarLaEdicion(): void {
    const mercado = this.viendoEdiciones();
    const f = this.formularioDeEdicion();
    if (!mercado || !f) return;

    this.guardandoEdicion.set(true);
    this.errorDeLaEdicion.set(null);
    const { id, ...datos } = f;
    const peticion = id === null
      ? this.mercados.crearEdicion(mercado.id, datos)
      : this.mercados.guardarEdicion(mercado.id, id, datos);

    peticion.subscribe({
      next: () => {
        this.guardandoEdicion.set(false);
        this.formularioDeEdicion.set(null);
        this.abrirLasEdiciones(mercado);
        void this.cargar();
      },
      error: (fallo: unknown) => {
        this.guardandoEdicion.set(false);
        this.errorDeLaEdicion.set(this.motivoDe(fallo));
      },
    });
  }

  // ─────────────────────────── La decisión del Programa ───────────────────────────

  /**
   * Solo se decide sobre lo que está en revisión.
   *
   * Decidir sobre un borrador sería resolver algo que su organización todavía no ha entregado. El
   * servidor lo rechaza igual; aquí ni siquiera se ofrece, que es lo que dice la regla del proyecto
   * sobre acciones contextuales.
   */
  sePuedeDecidir(mercado: Mercado): boolean {
    return mercado.estadoRegistro === 'en_revision';
  }

  /**
   * El mercado que se está a punto de retirar del ecosistema, con el motivo que se escribe.
   *
   * <b>EN UN DIALOGO DE VERDAD, NO EN UN `window.prompt`.</b> Es la regla del proyecto y la razón
   * por la que la misma acción en Festivales dejó de usarlos: sin estilo, fuera del recorrido de
   * teclado de la consola, imposibles de comprobar en una prueba y bloqueados sin aviso en algunos
   * contextos, que deja la acción muerta sin decirlo.
   */
  readonly retiroPedido = signal<Mercado | null>(null);
  readonly retirando = signal(false);
  readonly errorDelRetiro = signal<string | null>(null);

  pedirElRetiro(mercado: Mercado): void {
    this.errorDelRetiro.set(null);
    this.retiroPedido.set(mercado);
  }

  cancelarElRetiro(): void {
    this.retiroPedido.set(null);
    this.errorDelRetiro.set(null);
  }

  /**
   * Retira el mercado del ecosistema. NO LO BORRA: lo archiva, conservando historial y auditoría.
   * El motivo es obligatorio, y por eso el botón no se activa sin él.
   */
  retirarDelEcosistema(motivo: string): void {
    const mercado = this.retiroPedido();
    if (!mercado || !motivo.trim()) return;

    this.retirando.set(true);
    this.errorDelRetiro.set(null);
    this.mercados.archivar(mercado.id, motivo.trim()).subscribe({
      next: () => {
        this.retirando.set(false);
        this.retiroPedido.set(null);
        this.aviso.set(`«${mercado.nombre}» fue retirado del ecosistema.`);
        void this.cargar();
      },
      error: (fallo: unknown) => {
        this.retirando.set(false);
        this.errorDelRetiro.set(this.motivoDe(fallo));
      },
    });
  }

  // ─────────────────── La devolución, campo por campo ───────────────────
  //
  // ANTES ERA UN PARRAFO. Todo lo que hubiera que decir sobre treinta campos cabía en «qué hay que
  // corregir», y a la organización le llegaba un aviso suelto: no había forma de saber a qué campo
  // se refería cada frase, ni de marcar un punto como resuelto, ni de contar cuántos quedaban. Es
  // el mismo circuito que tiene un Festival desde.

  readonly SECCIONES_DE_LA_FICHA = SECCIONES_DE_LA_FICHA_DE_MERCADO;

  /** La nota escrita en cada campo, por identificador de campo. */
  readonly notasPorCampo = signal<Record<string, string>>({});
  readonly cargandoRevision = signal(false);
  readonly revisionEnviada = signal(false);

  abrirLaDecision(mercado: Mercado): void {
    this.motivoDeLosAjustes.set('');
    this.errorDeLaDecision.set(null);
    this.notasPorCampo.set({});
    this.revisionEnviada.set(false);
    this.decidiendo.set(mercado);

    // SE RECUPERA EL BORRADOR QUE HUBIERA. Quien revisa puede dejarlo a medias y volver, y
    // encontrarse la pantalla en blanco le haría escribirlo todo otra vez.
    this.cargandoRevision.set(true);
    this.mercados.revision(mercado.id).subscribe({
      next: revision => {
        this.cargandoRevision.set(false);
        this.motivoDeLosAjustes.set(revision.observacionGeneral ?? '');
        this.revisionEnviada.set(revision.estado === 'enviada');
        const notas: Record<string, string> = {};
        for (const observacion of revision.observaciones) notas[observacion.campoId] = observacion.nota;
        this.notasPorCampo.set(notas);
      },
      error: () => {
        // LEER EL BORRADOR ES UNA AYUDA, NO UN REQUISITO: si falla, se decide igual.
        this.cargandoRevision.set(false);
      },
    });
  }

  cerrarLaDecision(): void {
    this.decidiendo.set(null);
    this.errorDeLaDecision.set(null);
    this.notasPorCampo.set({});
  }

  notaDelCampo(campoId: string): string {
    return this.notasPorCampo()[campoId] ?? '';
  }

  escribirNota(campoId: string, valor: string): void {
    this.notasPorCampo.update(actual => ({ ...actual, [campoId]: valor }));
  }

  /** Lo que el campo dice ahora, que viaja copiado en la nota como evidencia. */
  valorDelCampo(campo: CampoDeLaFichaDeMercado): string | null {
    const mercado = this.decidiendo();
    return mercado ? campo.valor(mercado) : null;
  }

  /** Cuántos campos llevan nota. Es lo que decide si la devolución se puede enviar. */
  readonly camposSenalados = computed(() =>
    Object.values(this.notasPorCampo()).filter(nota => nota.trim().length > 0).length);

  /** Las notas con texto, en la forma que espera el servidor. */
  private notasParaGuardar(): ObservacionParaGuardar[] {
    const notas = this.notasPorCampo();
    return this.SECCIONES_DE_LA_FICHA.flatMap(seccion => seccion.campos
      .filter(campo => (notas[campo.id] ?? '').trim().length > 0)
      .map(campo => ({
        ambito: 'principal',
        subregistroId: null,
        seccionId: seccion.id,
        campoId: campo.id,
        campoEtiqueta: campo.etiqueta,
        valorObservado: this.valorDelCampo(campo),
        nota: notas[campo.id].trim(),
      })));
  }

  guardarElBorradorDeLaRevision(): void {
    const mercado = this.decidiendo();
    if (!mercado) return;

    this.guardando.set(true);
    this.errorDeLaDecision.set(null);
    this.mercados.guardarRevision(mercado.id, this.motivoDeLosAjustes().trim() || null, this.notasParaGuardar()).subscribe({
      next: () => {
        this.guardando.set(false);
        this.aviso.set('El borrador de la solicitud de cambios quedó guardado. Solo lo ves tú.');
      },
      error: (fallo: unknown) => {
        this.guardando.set(false);
        this.errorDeLaDecision.set(this.motivoDe(fallo));
      },
    });
  }

  /**
   * Envía la devolución: el mercado vuelve a su organización con las notas.
   *
   * AL MENOS UN CAMPO SEÑALADO, y esta es la diferencia con publicar. Devolver sin decir qué
   * corregir deja a la organización con el mercado en las manos y ninguna instrucción.
   */
  enviarLaRevision(): void {
    const mercado = this.decidiendo();
    if (!mercado) return;

    if (this.camposSenalados() === 0) {
      this.errorDeLaDecision.set('Señala al menos un campo antes de devolver el mercado.');
      return;
    }

    this.guardando.set(true);
    this.errorDeLaDecision.set(null);
    this.mercados.enviarRevision(mercado.id, this.motivoDeLosAjustes().trim() || null, this.notasParaGuardar()).subscribe({
      next: () => {
        this.guardando.set(false);
        this.decidiendo.set(null);
        this.aviso.set('El mercado volvió a su organización con los cambios pedidos.');
        void this.cargar();
      },
      error: (fallo: unknown) => {
        this.guardando.set(false);
        this.errorDeLaDecision.set(this.motivoDe(fallo));
      },
    });
  }

  decidir(decision: 'publicar' | 'ajustes'): void {
    const mercado = this.decidiendo();
    if (!mercado) return;

    const motivo = this.motivoDeLosAjustes().trim();
    if (decision === 'ajustes' && !motivo) {
      this.errorDeLaDecision.set('Di qué hay que corregir: sin motivo, el mercado vuelve igual.');
      return;
    }

    this.errorDeLaDecision.set(null);
    this.mercados.decidir(mercado.id, decision, motivo || undefined).subscribe({
      next: () => {
        this.decidiendo.set(null);
        this.aviso.set(decision === 'publicar'
          ? 'Mercado publicado en el ecosistema.'
          : 'Se le pidieron ajustes a la organización.');
        void this.cargar();
      },
      error: (fallo: unknown) => this.errorDeLaDecision.set(this.motivoDe(fallo)),
    });
  }

  guardar(): void {
    const f = this.formulario();
    if (!f) return;

    this.guardando.set(true);
    this.errorDelFormulario.set(null);
    const { id, ...datos } = f;
    const peticion = id === null ? this.mercados.crear(datos) : this.mercados.guardar(id, datos);

    peticion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.formulario.set(null);
        this.aviso.set(id === null ? 'Mercado creado en borrador.' : 'Mercado guardado.');
        void this.cargar();
      },
      error: (fallo: unknown) => {
        this.guardando.set(false);
        this.errorDelFormulario.set(this.motivoDe(fallo));
      },
    });
  }

  /**
   * El motivo que devolvió el servidor, y no uno genérico.
   *
   * Las tres reglas del festival y la coherencia del territorio se comprueban en el servidor, y su
   * respuesta dice exactamente cuál falló. Taparla con «no fue posible guardar» obliga a adivinar.
   */
  private motivoDe(fallo: unknown): string {
    const cuerpo = (fallo as { payload?: { errors?: Record<string, string[]> } })?.payload;
    const errores = cuerpo?.errors;
    if (errores) {
      const primero = Object.values(errores).find(mensajes => mensajes?.length);
      if (primero?.[0]) return primero[0];
    }
    const mensaje = (fallo as { payload?: { message?: string } })?.payload?.message;
    return mensaje ?? 'No fue posible guardar el mercado.';
  }
}
