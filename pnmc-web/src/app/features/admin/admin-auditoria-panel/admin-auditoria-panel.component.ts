import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { CommonModule, formatDate } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideRefreshCw } from '@lucide/angular';
import { ActuacionDeAuditoria, AdminService, DetalleDeAuditoria } from '../../../core/services/admin.service';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { etiquetaDeEstado } from '../domain/admin-config';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';

/** Un grupo —o un verbo— y cuántas actuaciones tiene en toda la bitácora. */
interface PosicionDeAuditoria {
  id: string;
  etiqueta: string;
  total: number;
}

interface RespuestaDeAuditoria {
  total: number;
  pagina: number;
  tamanoPagina: number;
  grupos: PosicionDeAuditoria[];
  acciones: PosicionDeAuditoria[];
  items: ActuacionDeAuditoria[];
}

/** Las posiciones del filtro de periodo, en días hacia atrás desde ahora; «hoy» es desde la medianoche. */
type Periodo = 'todo' | 'hoy' | '7' | '30';

const TAMANO_DE_PAGINA = 25;

@Component({
  selector: 'app-admin-auditoria-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent, BuscadorDeListaComponent, CabeceraDeTablaComponent, CommonModule, FormsModule, BotonComponent,
    DatoEnLecturaComponent, EstadoDeListaComponent, ListaSinFilasComponent, FiltroDesplegableComponent, MenuDeAccionesComponent, PanelLateralComponent,
    SelectorSegmentadoComponent, LucideRefreshCw,
  ],
  templateUrl: './admin-auditoria-panel.component.html',
})
export class AdminAuditoriaPanelComponent implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly respuesta = signal<RespuestaDeAuditoria | null>(null);
  readonly grupo = signal('todos');
  /** El verbo elegido en la lista, o vacío para todos. Es el código; la lista enseña la etiqueta. */
  readonly accion = signal('');
  /** Lo que se busca: un nombre, un correo o el identificador de un registro. */
  readonly q = signal('');
  readonly periodo = signal<Periodo>('todo');
  readonly pagina = signal(1);

  /**
   * Si hay algo puesto que pueda estar escondiendo actuaciones que sí ocurrieron.
   *
   * La bitácora no se crea desde ninguna pantalla: o hay filtros, o es que en ese periodo nadie
   * hizo nada. Por eso aquí el caso sin filtros no ofrece un alta, solo lo explica.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.q().trim().length > 0
      || this.grupo() !== 'todos'
      || this.accion() !== ''
      || this.periodo() !== 'todo',
  );

  /** Devuelve la bitácora entera y vuelve a preguntar. */
  limpiarFiltros(): void {
    this.q.set('');
    this.grupo.set('todos');
    this.accion.set('');
    this.periodo.set('todo');
    this.pagina.set(1);
    void this.consultar();
  }
  readonly cargando = signal(false);
  readonly error = signal('');

  /** El código nunca sale a pantalla: ver `etiquetaDeEstado`. */
  readonly etiquetaDeEstado = etiquetaDeEstado;

  /**
   * Las columnas de la bitácora.
   *
   * <b>«REGISTRO» NO ORDENA, Y LO DECLARA.</b> El nombre del registro tocado no está en la
   * bitácora: el servidor lo resuelve después de traer la página, con una consulta por cada tabla
   * presente en ella. No hay forma de ordenar por él en la base sin unir todas las tablas del
   * sistema, y ordenar solo la página es el defecto que se está quitando. Una cabecera que ofrece
   * ordenar y ordena otra cosa engaña más que una que no lo ofrece.
   */
  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'fecha', etiqueta: 'Fecha' },
    { id: 'actuacion', etiqueta: 'Actuación' },
    { id: 'registro', etiqueta: 'Registro', ordenable: false },
    { id: 'responsable', etiqueta: 'Responsable' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  /** Una actuación no se edita ni se decide: se abre. */
  readonly ACCIONES_DE_FILA: AccionDeRegistro[] = [{ id: 'detalle', etiqueta: 'Ver qué cambió', tono: 'principal' }];

  readonly OPCIONES_DE_PERIODO: readonly OpcionDeFiltro[] = [
    { id: 'todo', etiqueta: 'Todo el tiempo' },
    { id: 'hoy', etiqueta: 'Hoy' },
    { id: '7', etiqueta: 'Últimos 7 días' },
    { id: '30', etiqueta: 'Últimos 30 días' },
  ];

  /**
   * Los verbos de la bitácora como posiciones de la lista «Acción», con su recuento.
   *
   * VIENEN DEL SERVIDOR, que es quien sabe qué verbos hay y cuántas veces; escribirlos aquí sería
   * una segunda lista esperando a divergir de `CK_BitacoraAuditoria_Accion`.
   */
  readonly accionesDeLaBitacora = computed<readonly OpcionDeFiltro[]>(() => [
    { id: 'todas', etiqueta: 'Todas las acciones' },
    ...(this.respuesta()?.acciones ?? []).map(item => ({ id: item.id, etiqueta: item.etiqueta, conteo: item.total })),
  ]);

  /**
   * El orden de la tabla. Una bitácora se lee por cuándo pasó, de lo último hacia atrás.
   *
   * AQUI SI ARRANCA CON COLUMNA, al revés que Agenda y Noticias: el orden de partida de la bitácora
   * ES el de una columna —la fecha, descendente—, así que la flecha dice la verdad desde el primer
   * momento. Donde no se marca ninguna es donde el orden inicial combina dos datos.
   */
  readonly orden = new OrdenDeTabla('fecha', 'desc');

  /**
   * Pulsar una cabecera: se lo pide al servidor y se vuelve a la primera página.
   *
   * ORDENAR EN MEMORIA ERA EL DEFECTO: la bitácora pagina de veinte en veinte sobre miles de
   * actuaciones, así que ordenar las veinte cargadas no reordena la bitácora.
   */
  ordenarPor(columna: string): void {
    this.orden.alternar(columna);
    this.pagina.set(1);
    this.consultar();
  }

  /** Lo que se pinta: la página tal y como la ordenó el servidor. */
  readonly filasVisibles = computed(() => this.respuesta()?.items ?? []);

  /**
   * Los grupos como posiciones de un selector, «Todos» delante.
   *
   * ERAN OCHO PILDORAS con su borde y su relleno morado. Y sus rótulos vienen del servidor, así que
   * uno sin traducir -«AdministracionControl», medido- llegaba a
   * pantalla tal cual; el respaldo compartido lo escribe legible.
   */
  readonly gruposDeLaBitacora = computed<readonly OpcionSegmentada[]>(() => {
    const datos = this.respuesta();
    return [
      { id: 'todos', etiqueta: 'Todos', conteo: datos?.total ?? 0 },
      ...(datos?.grupos ?? []).map(item => ({
        id: item.id,
        etiqueta: etiquetaDeEstado(item.etiqueta),
        conteo: item.total,
      })),
    ];
  });

  /** Cuántas páginas hay, para decirlo en el pie en vez de solo «página 3». */
  readonly totalDePaginas = computed(() => {
    const datos = this.respuesta();
    if (!datos || datos.total === 0) { return 1; }
    return Math.max(1, Math.ceil(datos.total / (datos.tamanoPagina || TAMANO_DE_PAGINA)));
  });

  ngOnInit(): void {
    this.consultar();
  }

  consultar(): void {
    this.cargando.set(true);
    this.error.set('');
    this.adminService.cargarAuditoria({
      grupo: this.grupo(),
      accion: this.accion().trim() || undefined,
      q: this.q().trim() || undefined,
      desde: this.desde(),
      orden: this.orden.columna() || undefined,
      direccion: this.orden.columna() ? this.orden.direccion() : undefined,
      pagina: this.pagina(),
      tamano: TAMANO_DE_PAGINA,
    }).subscribe({
      next: respuesta => {
        this.respuesta.set(respuesta);
        this.cargando.set(false);
      },
      error: error => {
        this.cargando.set(false);
        this.error.set(error?.message || 'No fue posible consultar la auditoría.');
      },
    });
  }

  /** Cualquier filtro devuelve a la primera página: la que se estaba mirando ya no existe. */
  private filtrar(): void {
    this.pagina.set(1);
    this.consultar();
  }

  /** Llega con el texto ya esperado por el buscador. */
  buscar(texto: string): void {
    this.q.set(texto);
    this.filtrar();
  }

  elegirAccion(id: string): void {
    this.accion.set(id === 'todas' ? '' : id);
    this.filtrar();
  }

  elegirPeriodo(id: string): void {
    this.periodo.set(id as Periodo);
    this.filtrar();
  }

  elegirGrupo(grupo: string): void {
    this.grupo.set(grupo);
    this.filtrar();
  }

  /** Desde cuándo, en ISO, según el periodo elegido. «Hoy» empieza en la medianoche local. */
  private desde(): string | undefined {
    const ahora = new Date();
    switch (this.periodo()) {
      case 'hoy': return new Date(ahora.getFullYear(), ahora.getMonth(), ahora.getDate()).toISOString();
      case '7': return new Date(ahora.getTime() - 7 * 86400000).toISOString();
      case '30': return new Date(ahora.getTime() - 30 * 86400000).toISOString();
      default: return undefined;
    }
  }

  // ─── La actuación entera ───

  readonly detalleAbierto = signal<DetalleDeAuditoria | null>(null);
  readonly errorDelDetalle = signal('');

  abrirDetalle(item: ActuacionDeAuditoria): void {
    this.errorDelDetalle.set('');
    this.adminService.cargarDetalleDeAuditoria(item.id).subscribe({
      next: detalle => this.detalleAbierto.set(detalle),
      error: error => this.errorDelDetalle.set(error?.message || 'No fue posible abrir la actuación.'),
    });
  }

  cerrarDetalle(): void {
    this.detalleAbierto.set(null);
  }

  /**
   * Qué cambió, campo a campo: la unión de lo que había antes y lo que quedó después.
   *
   * SE PINTAN TAMBIEN LOS CAMPOS QUE NO CAMBIAN, y se marcan los que sí: un listado de solo
   * diferencias deja sin responder si el resto se guardó igual o simplemente no se guardó.
   */
  readonly cambios = computed(() => {
    const detalle = this.detalleAbierto();
    if (!detalle) { return []; }
    const campos = new Set([...Object.keys(detalle.valoresAnteriores), ...Object.keys(detalle.valoresNuevos)]);
    return [...campos].map(campo => {
      const antes = detalle.valoresAnteriores[campo] ?? null;
      const despues = detalle.valoresNuevos[campo] ?? null;
      return { campo, antes, despues, cambia: antes !== despues };
    });
  });

  paginaAnterior(): void {
    if (this.pagina() <= 1) return;
    this.pagina.update(pagina => pagina - 1);
    this.consultar();
  }

  paginaSiguiente(): void {
    const respuesta = this.respuesta();
    if (!respuesta || respuesta.pagina * respuesta.tamanoPagina >= respuesta.total) return;
    this.pagina.update(pagina => pagina + 1);
    this.consultar();
  }

  fecha(valor: string): string {
    const fecha = new Date(valor);
    return Number.isNaN(fecha.getTime())
      ? '—'
      : formatDate(fecha, FECHA_Y_HORA_ADMINISTRATIVA, 'es-CO');
  }
}
