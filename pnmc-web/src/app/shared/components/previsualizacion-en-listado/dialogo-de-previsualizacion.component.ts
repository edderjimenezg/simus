import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject, signal } from '@angular/core';
import { LucideX } from '@lucide/angular';
import { DialogoDirective } from '../../directives/dialogo.directive';
import { ModuloPrevisualizable } from '../../../core/services/previsualizacion-en-listado.service';
import { PrevisualizacionEnListadoComponent } from './previsualizacion-en-listado.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../ui/selector-segmentado/selector-segmentado.component';

/**
 * El diálogo que enseña la previsualización, compartido por los tres paneles de contenido.
 *
 * <b>ES UN ENVOLTORIO Y ESO ES TODO LO QUE HACE.</b> Existe para que Noticias, Agenda y Catálogo
 * Editorial no escriban tres veces el mismo modal —con sus tres formas distintas de cerrar, de
 * devolver el foco y de titularse—, que es como se llega a que uno de los tres no responda a
 * Escape y nadie se dé cuenta hasta que alguien lo reporte.
 */
@Component({
  selector: 'app-dialogo-de-previsualizacion',
  standalone: true,
  imports: [CommonModule, LucideX, DialogoDirective, PrevisualizacionEnListadoComponent, SelectorSegmentadoComponent],
  template: `
    <div class="fixed inset-0 z-[3100] grid place-items-center bg-slate-900/40 p-4">
      <button type="button" (click)="cerrar.emit()" tabindex="-1" aria-hidden="true"
        class="absolute inset-0 cursor-default"></button>
      <section #panel tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="previsualizacion-titulo"
        appDialogo (cerrar)="cerrar.emit()"
        data-dialogo-previsualizacion
        class="relative flex max-h-[92dvh] w-full max-w-5xl flex-col overflow-hidden rounded-2xl bg-white p-5 shadow-xl focus:outline-none">
        <div class="mb-3 flex items-start justify-between gap-4">
          <div>
            <p class="font-alternate text-dato font-bold uppercase tracking-widest text-rotulo">Previsualización</p>
            <h3 id="previsualizacion-titulo" class="mt-0.5 font-alternate text-titulo font-bold text-valor">{{ nombre || 'Antes de publicar' }}</h3>
          </div>
          <button type="button" (click)="cerrar.emit()" data-cerrar-previsualizacion
            class="rounded-lg border border-filete p-2 text-prosa transition hover:border-morado hover:text-morado focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado">
            <svg lucideX [size]="16" aria-hidden="true"></svg>
            <span class="sr-only">Cerrar la previsualización</span>
          </button>
        </div>

        <!--
          DOS PREGUNTAS DISTINTAS, DOS VISTAS.

          «Cómo se lee» es lo que se pregunta antes de publicar algo, y era lo único que este
          diálogo NO enseñaba: solo tenía la tarjeta del listado. Lo pidió la dirección de producto el
          15 de septiembre de 2026 —«que muestre cómo se ve la noticia o el evento como tal»— y por
          eso la pieza va primero y es la que se abre.

          «Cómo se ve en su listado» se queda, porque responde a otra cosa igual de real: si el
          título cabe recortado y si la portada no deja un hueco en la parrilla.
        -->
        <app-selector-segmentado
          [opciones]="VISTAS"
          [elegida]="queSeMira()"
          [conFilete]="false"
          etiquetaDelGrupo="Qué previsualizar"
          (elegir)="queSeMira.set($any($event))" />

        <div class="mt-4 min-h-0 flex-1 overflow-y-auto">
          @if (queSeMira() === 'pieza') {
            <ng-content />
          } @else {
            <app-previsualizacion-en-listado
              [modulo]="modulo" [id]="id" [nombre]="nombre"
              [estadoEtiqueta]="estadoEtiqueta" [yaEsPublico]="yaEsPublico" />
          }
        </div>
      </section>
    </div>
  `,
})
export class DialogoDePrevisualizacionComponent {
  @Input({ required: true }) modulo: ModuloPrevisualizable = 'noticias';
  @Input({ required: true }) id: string | number = 0;
  @Input() nombre = '';
  @Input() estadoEtiqueta = '';
  @Input() yaEsPublico = false;

  @Output() cerrar = new EventEmitter<void>();

  /**
   * Qué se está previsualizando: la pieza o su tarjeta en el listado.
   *
   * ARRANCA EN LA PIEZA. Es lo que se pregunta antes de publicar —cómo se lee— y era justo lo que
   * este diálogo no enseñaba.
   */
  readonly queSeMira = signal<'pieza' | 'listado'>('pieza');

  readonly VISTAS: readonly OpcionSegmentada[] = [
    { id: 'pieza', etiqueta: 'Cómo se lee' },
    { id: 'listado', etiqueta: 'Cómo se ve en el listado' },
  ];

  @ViewChild('panel') panel?: ElementRef<HTMLElement>;
  @ViewChild(PrevisualizacionEnListadoComponent) vista?: PrevisualizacionEnListadoComponent;

  private readonly anfitrion = inject(ElementRef<HTMLElement>);

  ngAfterViewInit(): void {
    // EL FOCO ENTRA AL DIALOGO, o quien navega con teclado abre un modal al que no puede llegar.
    this.panel?.nativeElement.focus();
    // Y LA MEDIDA SE TOMA DESPUES DE PINTAR: antes, el hueco todavía no tiene ancho y el marco se
    // quedaría con la escala de un panel de cero píxeles.
    queueMicrotask(() => this.vista?.medir());
  }

  @HostListener('document:keydown.escape')
  alEscapar(): void {
    this.cerrar.emit();
  }

  /** Se usa desde las pruebas para comprobar que el diálogo está montado en el documento. */
  get elemento(): HTMLElement {
    return this.anfitrion.nativeElement as HTMLElement;
  }
}
