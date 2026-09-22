import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, formatDate } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LucideRefreshCw } from '@lucide/angular';
import { BoletinService, ListadoDeSuscripciones, SuscripcionDelBoletin } from '../../../core/services/boletin.service';
import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { etiquetaDeEstado } from '../domain/admin-config';
import { FECHA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';

const POR_PAGINA = 25;

/**
 * La lista de correos del boletín, dentro de Comunicaciones.
 *
 * POR QUÉ NO ES UN MÓDULO DE `ADMIN_MODULES` COMO AGENDA O NOTICIAS. Esos módulos comparten un
 * panel genérico de altas, ediciones y borrados sobre una tabla. Aquí nada de eso aplica: nadie
 * da de alta un correo desde la consola —lo hace la persona, desde la portada, y su autorización
 * es lo que da valor legal a la fila—, nadie edita un correo ajeno, y nadie borra: la baja se
 * escribe, no elimina. Montarlo sobre el panel genérico habría pintado un botón «Crear
 * suscripción» y un formulario de edición que el API rechaza.
 *
 * QUÉ ES EL PUENTE HACIA UN MAILING O UN CRM. La exportación a CSV. Es una lista descargable que
 * entra en Mailchimp, Brevo o lo que use el Ministerio, sin credenciales de un tercero guardadas
 * en este sistema y sin sacar datos personales hacia un proveedor que nadie ha aprobado todavía.
 */
@Component({
  selector: 'app-admin-boletin-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent,FiltroDesplegableComponent, CabeceraDeTablaComponent, 
    CommonModule, FormsModule,
    BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent, IndicadorDeEstadoComponent, MenuDeAccionesComponent,
    LucideRefreshCw, BuscadorDeListaComponent, ConfirmacionComponent,
  ],
  templateUrl: './admin-boletin-panel.component.html',
})
export class AdminBoletinPanelComponent implements OnInit {
  private readonly boletin = inject(BoletinService);

  /** El tono y el matiz del indicador, y la etiqueta legible, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;
  readonly etiquetaDeEstado = etiquetaDeEstado;

  readonly cargando = signal(false);
  readonly error = signal('');
  readonly aviso = signal('');

  readonly busqueda = signal('');
  readonly estado = signal('');
  readonly pagina = signal(0);

  readonly listado = signal<ListadoDeSuscripciones | null>(null);

  readonly filas = computed<SuscripcionDelBoletin[]>(() => this.listado()?.items ?? []);

  readonly FILTRO_DE_ESTADO: readonly OpcionDeFiltro[] = [
    { id: '', etiqueta: 'Todos los estados' },
    { id: 'activa', etiqueta: 'Activas' },
    { id: 'baja', etiqueta: 'Dadas de baja' },
  ];

  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'correo', etiqueta: 'Correo' },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'origen', etiqueta: 'Origen' },
    { id: 'alta', etiqueta: 'Alta' },
    { id: 'baja', etiqueta: 'Baja' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  /**
   * Arranca por la fecha de alta, de la más reciente: quien mira la lista busca quién entró.
   *
   * ES EL MISMO ORDEN CON EL QUE RESPONDE EL SERVIDOR cuando no se le pide otro, así que la flecha
   * de la cabecera dice la verdad desde el primer momento.
   */
  readonly orden = new OrdenDeTabla('alta', 'desc');

  /**
   * Pulsar una cabecera: se lo pide al servidor y se vuelve a la primera página.
   *
   * <b>ORDENABA LA PAGINA Y NO EL LISTADO, y este fichero lo decía.</b> Era una limitación asumida
   * —«no hay un criterio de orden en el contrato del API»— y el criterio se añadió el 15 de
   * septiembre de 2026, cuando quedó definido para todas las tablas de la consola:
   * «debe ser de todos, no solo de los de la página visible».
   */
  ordenarPor(columna: string): void {
    this.orden.alternar(columna);
    this.pagina.set(0);
    this.consultar();
  }

  /** Lo que se pinta: la página tal y como la ordenó el servidor. */
  readonly filasVisibles = computed(() => this.filas());
  readonly total = computed(() => this.listado()?.total ?? 0);
  readonly activas = computed(() => this.listado()?.activas ?? 0);

  /**
   * Las bajas se calculan por resta y solo cuando el filtro está en «todos».
   *
   * Con el filtro puesto en «activa», `total` ya son solo las activas y la resta daría cero, que
   * es un número falso. Ante la duda, no se muestra.
   */
  readonly bajas = computed(() => (this.estado() === '' ? Math.max(this.total() - this.activas(), 0) : null));

  readonly hayMas = computed(() => (this.pagina() + 1) * POR_PAGINA < this.total());

  ngOnInit(): void {
    this.consultar();
  }

  consultar(): void {
    this.cargando.set(true);
    this.error.set('');

    this.boletin.consultarSuscripciones({
      q: this.busqueda().trim() || undefined,
      estado: this.estado() || undefined,
      orden: this.orden.columna() || undefined,
      direccion: this.orden.columna() ? this.orden.direccion() : undefined,
      limit: POR_PAGINA,
      offset: this.pagina() * POR_PAGINA,
    }).subscribe({
      next: (respuesta) => {
        this.listado.set(respuesta);
        this.cargando.set(false);
      },
      error: (err: any) => {
        this.cargando.set(false);
        this.error.set(err?.message || 'No fue posible consultar las suscripciones al boletín.');
      },
    });
  }

  /** Buscar devuelve a la primera página. Llega con el texto ya esperado por el buscador. */
  buscar(texto: string): void {
    this.busqueda.set(texto);
    this.pagina.set(0);
    this.consultar();
  }

  cambiarEstado(valor: string): void {
    this.estado.set(valor);
    this.pagina.set(0);
    this.consultar();
  }

  /** Si hay búsqueda o estado puestos, y por tanto la lista puede no estar vacía de verdad. */
  readonly hayFiltrosPuestos = computed(() => this.busqueda().trim().length > 0 || this.estado() !== '');

  /** Devuelve la lista completa de suscripciones. */
  limpiarFiltros(): void {
    this.busqueda.set('');
    this.estado.set('');
    this.pagina.set(0);
    this.consultar();
  }

  paginaAnterior(): void {
    if (this.pagina() === 0) return;
    this.pagina.update(valor => valor - 1);
    this.consultar();
  }

  paginaSiguiente(): void {
    if (!this.hayMas()) return;
    this.pagina.update(valor => valor + 1);
    this.consultar();
  }

  /**
   * Lo que se puede hacer sobre una suscripción, para el menú de la fila.
   *
   * ES UNA SOLA ACCION Y AUN ASI PASA POR EL MENU: así la última columna mide lo mismo en todas
   * las filas. Antes, «Dar de baja» solo se pintaba en las activas, y la columna cambiaba de ancho
   * de una fila a la siguiente. Sobre una fila ya dada de baja no se ofrece nada, porque no hay
   * nada que hacer: volver a entrar depende de la persona, no de la consola.
   */
  accionesDe(fila: SuscripcionDelBoletin): AccionDeRegistro[] {
    if (fila.estado !== 'activa') { return []; }
    return [{ id: 'baja', etiqueta: 'Dar de baja', tono: 'peligro' }];
  }

  ejecutarAccion(fila: SuscripcionDelBoletin, accion: string): void {
    if (accion === 'baja') { this.darDeBaja(fila); }
  }

  /**
   * La suscripción que se está a punto de dar de baja.
   *
   * SE PREGUNTA PORQUE ES EL DATO DE OTRA PERSONA y no tiene deshacer en la pantalla: para volver a
   * entrar, esa persona tiene que suscribirse de nuevo. Y se pregunta en el diálogo del proyecto,
   * no en un `confirm()` del navegador, que es lo que había hasta.
   */
  readonly bajaPendiente = signal<SuscripcionDelBoletin | null>(null);

  darDeBaja(fila: SuscripcionDelBoletin): void {
    this.bajaPendiente.set(fila);
  }

  cerrarLaConfirmacion(): void { this.bajaPendiente.set(null); }

  confirmarLaBaja(): void {
    const fila = this.bajaPendiente();
    if (!fila) return;
    this.bajaPendiente.set(null);
    this.error.set('');
    this.boletin.darDeBaja(fila.id).subscribe({
      next: (respuesta) => {
        this.aviso.set(respuesta?.message || 'Suscripción dada de baja.');
        this.consultar();
      },
      error: (err: any) => this.error.set(err?.message || 'No fue posible dar de baja esa suscripción.'),
    });
  }

  descargarCsv(): void {
    this.error.set('');
    this.boletin.descargarCsv({
      q: this.busqueda().trim() || undefined,
      estado: this.estado() || undefined,
    }).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.download = 'boletin-pnmc.csv';
        document.body.appendChild(enlace);
        enlace.click();
        enlace.remove();
        URL.revokeObjectURL(url);
        this.aviso.set('Lista descargada. Se puede importar en la herramienta de envíos.');
      },
      error: () => this.error.set('No fue posible descargar la lista.'),
    });
  }

  /** La fecha del boletín, en el formato único del panel. Las del API llegan en UTC y en ISO. */
  fecha(valor: string | null): string {
    if (!valor) return '—';
    const fecha = new Date(valor);
    return Number.isNaN(fecha.getTime()) ? '—' : formatDate(fecha, FECHA_ADMINISTRATIVA, 'es-CO');
  }
}
