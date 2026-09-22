import { ChangeDetectionStrategy, Component, ElementRef, HostListener, effect, inject, input, output } from '@angular/core';
import { BotonComponent } from '../boton/boton.component';
import { DialogoDirective } from '../../../directives/dialogo.directive';

/**
 * El cajón lateral de la consola: una ficha que se abre a la derecha sin abandonar la lista.
 *
 * <b>UNA SOLA CASCARA PARA TODAS LAS FICHAS.</b> Cada panel que abría una ficha en un cajón traía
 * su propio velo, su propio ancho, su propio botón de cerrar y su propio encabezado; el de
 * Festivales medía 36rem y cerraba con un botón de 10,88 px, el de Organizaciones ni siquiera era
 * un cajón. Esta pieza fija lo que es común —velo, columna, encabezado con contexto, título y
 * estado, cierre con Escape y con el botón— y deja el contenido a quien la usa.
 *
 * <b>EL FOCO ENTRA Y NO SE QUEDA FUERA.</b> Al abrirse se lleva el foco al cajón, para que quien
 * navega con teclado no siga tabulando por la lista que quedó debajo del velo.
 */
@Component({
  selector: 'app-panel-lateral',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BotonComponent, DialogoDirective],
  styles: [`
    :host { display: contents; }
    .velo { position: fixed; inset: 0; z-index: 50; display: flex; justify-content: flex-end; background: rgba(23, 32, 57, .42); }
    .panel { display: flex; height: 100%; width: 100%; max-width: 40rem; flex-direction: column; background: #fff; box-shadow: -18px 0 40px rgba(16, 24, 40, .18); outline: none; }
    .panel__cabecera { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 1.5rem 1.75rem 1.1rem; border-bottom: 1px solid var(--color-filete); }
    .panel__contexto { margin: 0 0 .35rem; color: var(--color-seccion); font-family: var(--font-alternate); font-size: var(--text-dato); font-weight: 700; letter-spacing: .14em; text-transform: uppercase; }
    .panel__titulo { margin: 0; color: var(--color-valor); font-family: var(--font-alternate); font-size: var(--text-titulo); font-weight: 700; line-height: 1.2; overflow-wrap: anywhere; }
    .panel__bajo-titulo { display: flex; flex-wrap: wrap; align-items: center; gap: .4rem .75rem; margin-top: .5rem; }
    .panel__bajo-titulo:empty { display: none; }
    .panel__cuerpo { flex: 1 1 auto; overflow-y: auto; padding: 1.5rem 1.75rem 2.5rem; }
    @media (max-width: 800px) { .panel { max-width: none; } .panel__cabecera, .panel__cuerpo { padding-inline: 1.1rem; } }
  `],
  template: `
    <div class="velo" (mousedown)="$event.target === $event.currentTarget && cerrar.emit()">
      <!--
        LA DIRECTIVA DE TODA VENTANA: retiene el foco dentro, lo devuelve a quien abrió al cerrar y
        deja el fondo quieto. Escape lo atiende ella cuando el foco está dentro y el oyente del
        documento cuando no —no se pisan: la directiva detiene el evento—.
      -->
      <section class="panel" role="dialog" aria-modal="true" [attr.aria-labelledby]="idDelTitulo()" tabindex="-1"
        appDialogo (cerrar)="cerrar.emit()">
        <header class="panel__cabecera">
          <div class="min-w-0">
            @if (contexto()) { <p class="panel__contexto">{{ contexto() }}</p> }
            <h2 class="panel__titulo" [id]="idDelTitulo()">{{ titulo() }}</h2>
            <div class="panel__bajo-titulo"><ng-content select="[bajo-titulo]" /></div>
          </div>
          <app-boton importancia="secundaria" tamano="menudo" etiqueta="Cerrar la ficha" (accion)="cerrar.emit()">Cerrar</app-boton>
        </header>
        <div class="panel__cuerpo"><ng-content /></div>
      </section>
    </div>
  `,
})
export class PanelLateralComponent {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  readonly titulo = input.required<string>();
  readonly contexto = input<string | null | undefined>(null);
  readonly idDelTitulo = input('panel-lateral-titulo');
  readonly cerrar = output<void>();

  constructor() {
    // El foco entra al abrirse. `effect` en vez de `ngAfterViewInit` porque el cajón se monta
    // con `@if` y lo que importa es el instante en que aparece en el DOM.
    effect(() => {
      const panel = this.host.nativeElement.querySelector<HTMLElement>('.panel');
      panel?.focus({ preventScroll: true });
    });
  }

  @HostListener('document:keydown.escape')
  alPulsarEscape(): void { this.cerrar.emit(); }
}
