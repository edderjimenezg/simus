import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { CifraDeTablero, FranjaDeCifrasComponent } from '../../../shared/components/ui/franja-de-cifras/franja-de-cifras.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { CommonModule } from '@angular/common';
import { Component, Input, effect, inject, input, signal, untracked, computed } from '@angular/core';
import {
  ContenidoWebApiService,
  ResumenDeGrupoDelSitio,
  ResumenDeGestionDelSitio,
} from '../../../core/services/contenido-web-api.service';
import { AdminWebMediaPanelComponent } from '../admin-web-media-panel/admin-web-media-panel.component';
import { AdminWebTextsPanelComponent } from '../admin-web-texts-panel/admin-web-texts-panel.component';
import { AdminRespaldosSitioComponent } from './admin-respaldos-sitio.component';

export type VistaGestionSitio = 'resumen' | 'textos' | 'imagenes' | 'equipo' | 'pendientes' | 'respaldos';

@Component({
  selector: 'app-admin-gestion-sitio-panel',
  standalone: true,
  imports: [BotonComponent, EstadoDeListaComponent, SelectorSegmentadoComponent, FranjaDeCifrasComponent, MenuDeAccionesComponent, 
    CommonModule,
    AdminWebTextsPanelComponent,
    AdminWebMediaPanelComponent,
    AdminRespaldosSitioComponent,
  ],
  templateUrl: './admin-gestion-sitio-panel.component.html',
})
export class AdminGestionSitioPanelComponent {
  private readonly api = inject(ContenidoWebApiService);

  @Input() session: { fullName?: string } | null = null;
  readonly enabled = input(true);
  readonly puedePublicar = input(false);

  readonly vista = signal<VistaGestionSitio>('resumen');
  readonly resumen = signal<ResumenDeGestionDelSitio | null>(null);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);

  readonly opciones: readonly { id: VistaGestionSitio; etiqueta: string; descripcion: string }[] = [
    { id: 'resumen', etiqueta: 'Resumen', descripcion: 'Estado general del sitio' },
    { id: 'textos', etiqueta: 'Textos y páginas', descripcion: 'Edición con vista previa' },
    { id: 'imagenes', etiqueta: 'Imágenes del sitio', descripcion: 'Archivos y recortes' },
    { id: 'equipo', etiqueta: 'Equipo', descripcion: 'Personas y datos públicos' },
    { id: 'pendientes', etiqueta: 'Cambios pendientes', descripcion: 'Borradores sin publicar' },
    { id: 'respaldos', etiqueta: 'Respaldos', descripcion: 'Copias e importación' },
  ];

  /**
   * Las seis áreas como posiciones de un selector, con los cambios pendientes en la suya.
   *
   * ERAN SEIS TARJETAS con borde y sombra, y la elegida rellena de morado. La descripción de cada
   * una —«Edición con vista previa», «Archivos y recortes»— cabía en la tarjeta pero no en una
   * posición de selector, y tampoco hacía falta: el rótulo ya dice a dónde lleva, y lo que había
   * que saber de verdad —cuántos cambios esperan— no se veía en ninguna de las seis.
   */
  /** Un pendiente no se edita ni se decide desde aquí: se abre en su área. */
  readonly ABRIR: AccionDeRegistro[] = [{ id: 'abrir', etiqueta: 'Abrir', tono: 'principal' }];

  /** Las cuatro cifras del resumen, en la franja compartida; cada una lleva a su área. */
  readonly cifrasDelSitio = computed<CifraDeTablero[]>(() => {
    const estado = this.resumen();
    if (!estado) { return []; }
    return [
      { id: 'textos', cifra: estado.texts.total, rotulo: 'Textos y páginas', detalle: `${estado.texts.published} publicados · ${estado.texts.pending} pendientes`, accionable: true },
      { id: 'imagenes', cifra: estado.images.total, rotulo: 'Imágenes del sitio', detalle: `${estado.images.published} publicadas · ${estado.images.pending} pendientes`, accionable: true },
      { id: 'equipo', cifra: estado.team.members, rotulo: 'Equipo', detalle: estado.team.pending ? 'Nómina con cambios pendientes' : estado.team.published ? 'Nómina publicada' : 'Se muestra la nómina de fábrica', accionable: true },
      // Guardar borrador no cambia el portal: es el criterio de trabajo, y va donde se decide.
      { id: 'pendientes', cifra: estado.pendingTotal, rotulo: 'Cambios pendientes', detalle: 'Borradores distintos de lo visible en el portal. Guardar borrador no cambia el portal.', tono: estado.pendingTotal > 0 ? 'aviso' : undefined, accionable: true },
    ];
  });

  readonly areasDelSitio = computed<readonly OpcionSegmentada[]>(() => {
    const pendientes = this.resumen()?.pendingTotal ?? 0;
    return this.opciones.map(opcion => (
      opcion.id === 'pendientes' && pendientes > 0
        ? { id: opcion.id, etiqueta: opcion.etiqueta, conteo: pendientes }
        : { id: opcion.id, etiqueta: opcion.etiqueta }
    ));
  });

  constructor() {
    // `untracked` NO ES DECORATIVO AQUÍ, y esto costó un bucle infinito de peticiones.
    //
    // `cargarResumen()` empieza SÍNCRONAMENTE: antes de su primer `await` lee `cargando()`
    // —el cerrojo de reentrada— y lo escribe. Llamarla desde el cuerpo del efecto hacía que
    // el efecto se suscribiera a `cargando`, es decir, a una señal que él mismo cambia. El
    // resultado era un ciclo cerrado: el efecto pedía el resumen, `cargando` cambiaba, el
    // cambio volvía a disparar el efecto, y el panel se quedaba para siempre en «Cargando
    // el estado del sitio…» mientras golpeaba el API sin parar.
    //
    // Con `untracked` el efecto depende SOLO de `enabled()`, que es lo único a lo que debe
    // reaccionar: que el área se encienda.
    effect(() => {
      if (!this.enabled()) { return; }
      untracked(() => { void this.cargarResumen(); });
    });
  }

  async seleccionar(vista: VistaGestionSitio): Promise<void> {
    this.vista.set(vista);
    if (vista === 'resumen' || vista === 'pendientes') {
      await this.cargarResumen();
    }
  }

  async cargarResumen(): Promise<void> {
    if (this.cargando()) { return; }
    this.cargando.set(true);
    this.error.set(null);
    const resultado = await this.api.getSiteManagementSummary();
    this.cargando.set(false);

    if (!resultado.ok || !resultado.data) {
      this.error.set(resultado.error ?? 'No fue posible leer el estado de Gestión del sitio.');
      return;
    }
    this.resumen.set(resultado.data);
  }

  gruposPendientes(grupos: readonly ResumenDeGrupoDelSitio[]): ResumenDeGrupoDelSitio[] {
    return grupos.filter(grupo => grupo.pending > 0);
  }
}
