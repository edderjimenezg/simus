import { Component, EventEmitter, OnInit, Output, computed, inject, signal } from '@angular/core';
import { LucideArrowRight, LucideArrowUpRight, LucideCalendarDays, LucideChevronRight, LucideMapPin } from '@lucide/angular';
import { AgendaService } from '../../../../core/services/agenda.service';
import { NoticiasService } from '../../../../core/services/noticias.service';
import { EventoDeDiseno, NoticiaDeDiseno, aEventoDeDiseno, aNoticiaDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import { ContentWrapperComponent } from '../../../../shared/components/ui/content-wrapper/content-wrapper.component';
import { SectionHeaderComponent } from '../../../../shared/components/ui/section-header/section-header.component';
import { EmptyStateComponent, ErrorStateComponent, LoadingStateComponent } from '../../../../shared/components/ui/remote-state/remote-state.component';

/**
 * La portada informativa de Inicio: Noticias a la izquierda, Agenda a la derecha.
 *
 * <b>ESTABA DESCONECTADA, Y ESE ERA EL DEFECTO.</b> Hasta este bloque
 * pintaba dos recuadros fijos —«Noticias en preparación», «Aún no hay actividades publicadas»— con
 * un enlace a cada sección. El comentario del componente lo justificaba: «ambas secciones parten
 * vacías hasta contar con sus flujos institucionales de publicación». Esos flujos ya existen: hay
 * panel de Noticias, panel de Agenda y dos rutas públicas, `/publico/noticias` y `/publico/agenda`,
 * que el propio sitio consume en sus páginas. Así que el texto de espera no describía el estado del
 * sistema, solo el de este bloque. El criterio es este: «conecta con noticias y agenda,
 * esa es una previsualización de ambas cosas».
 *
 * <b>LA FORMA ES LA DE EL DISEÑO APROBADO DEL PORTAL</b> —la noticia destacada a lo ancho con su imagen a media caja,
 * dos secundarias debajo, y la agenda como una columna desplazable con el pie que lleva al
 * calendario—, porque es el diseño aprobado del sitio. Lo que NO se copia es su manera de
 * pedir los datos: allí eran dos peticiones anidadas a un `BackendDataService` heredado con
 * campos en inglés. Aquí se usan los servicios del proyecto y los adaptadores que ya traducen
 * `Noticia` y `EventoAgenda` a la forma que el diseño espera —los mismos que usan las páginas de
 * Noticias y de Agenda—, así que la portada y la sección completa no pueden discrepar.
 *
 * <b>SIN ROTACION AUTOMATICA.</b> el diseño aprobado del portal cambiaba el trío destacado cada treinta segundos con un
 * `setInterval`. Se deja fuera: mueve la página bajo el cursor de quien está leyendo, y no hay
 * forma de volver a la noticia que acaba de desaparecer.
 */
@Component({
  selector: 'app-noticias-agenda-preview',
  standalone: true,
  imports: [
    ContentWrapperComponent,
    SectionHeaderComponent,
    LoadingStateComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    LucideArrowRight,
    LucideArrowUpRight,
    LucideCalendarDays,
    LucideChevronRight,
    LucideMapPin,
  ],
  templateUrl: './noticias-agenda-preview.component.html',
})
export class NoticiasAgendaPreviewComponent implements OnInit {
  @Output() navigate = new EventEmitter<string>();
  @Output() navegarANoticia = new EventEmitter<NoticiaDeDiseno>();
  @Output() navegarAEvento = new EventEmitter<string>();

  private readonly noticias = inject(NoticiasService);
  private readonly agenda = inject(AgendaService);

  readonly cargando = signal(true);
  readonly error = signal('');

  readonly destacadas = signal<NoticiaDeDiseno[]>([]);
  readonly eventos = signal<EventoDeDiseno[]>([]);

  /** La tarjeta de agenda sobre la que está el puntero; la primera, mientras no haya ninguna. */
  readonly eventoResaltado = signal(0);

  readonly noticiaPrincipal = computed(() => this.destacadas()[0] ?? null);
  readonly noticiasSecundarias = computed(() => this.destacadas().slice(1, 3));

  ngOnInit(): void {
    void this.cargar();
  }

  /**
   * Las dos listas se piden A LA VEZ y no una dentro de la otra.
   *
   * el diseño aprobado del portal encadenaba la agenda dentro del `next` de las noticias, así que la portada tardaba la
   * suma de las dos y un fallo en la primera dejaba la segunda sin pedir siquiera.
   */
  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set('');

    const [noticias, agenda] = await Promise.all([
      this.noticias.listarPublicas({ tamano: 3, pagina: 1 }),
      this.agenda.listarPublicos({ cuando: 'proximos', tamano: 6, pagina: 1 }),
    ]);

    // `ResultadoNoticia`/`ResultadoAgenda` llevan `ok` y `data` opcional: se comprueban los dos
    // para que el compilador estreche el tipo y no quede un `data!` escondiendo un fallo real.
    if (!noticias.ok || !noticias.data || !agenda.ok || !agenda.data) {
      this.error.set(noticias.error || agenda.error || 'No fue posible cargar la portada informativa.');
      this.cargando.set(false);
      return;
    }

    this.destacadas.set(noticias.data.items.map(aNoticiaDeDiseno));
    this.eventos.set(agenda.data.items.map(aEventoDeDiseno));
    this.eventoResaltado.set(0);
    this.cargando.set(false);
  }

  onNavigate(ruta: 'noticias' | 'agenda'): void {
    this.navigate.emit(ruta);
  }

  abrirNoticia(noticia: NoticiaDeDiseno): void {
    this.navegarANoticia.emit(noticia);
  }

  abrirEvento(id: string): void {
    this.navegarAEvento.emit(id);
  }

  resaltarEvento(indice: number): void {
    this.eventoResaltado.set(indice);
  }
}
