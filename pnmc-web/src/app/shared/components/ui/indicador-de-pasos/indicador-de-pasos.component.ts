import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface PasoDelIndicador {
  id: number;
  titulo: string;
}

export type EstadoDelPaso = 'completado' | 'activo' | 'pendiente';

/**
 * El stepper numerado -círculos + línea de progreso- de los asistentes por pasos del panel.
 *
 * EXTRAÍDO DE `AsistenteDeFestivalComponent` EL 1 DE SEPTIEMBRE DE 2026, cuando el alta de una
 * organización pasó a necesitar el mismo indicador: ninguno de los dos formularios sabe nada del
 * otro, solo comparten esta pieza visual.
 */
@Component({
  selector: 'app-indicador-de-pasos',
  standalone: true,
  imports: [CommonModule],
  template: `
    <ol class="flex items-start" [attr.aria-label]="etiqueta">
      @for (paso of pasos; track paso.id; let ultimo = $last) {
        <li class="flex" [class.flex-1]="!ultimo">
          <button type="button"
                  (click)="irAlPaso.emit(paso.id)"
                  [attr.aria-current]="pasoActivo === paso.id ? 'step' : null"
                  class="group flex shrink-0 flex-col items-center gap-2 focus-visible:outline-none">
            <span [class]="estadoDelPaso(paso.id) === 'completado'
                    ? 'bg-verde-texto text-white'
                    : estadoDelPaso(paso.id) === 'activo'
                      ? 'bg-morado text-white ring-4 ring-morado/15'
                      : 'border-2 border-slate-300 bg-white text-slate-400 group-hover:border-morado group-hover:text-morado'"
                  class="flex h-9 w-9 items-center justify-center rounded-full font-alternate text-sm font-bold transition">
              @if (estadoDelPaso(paso.id) === 'completado') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"
                     stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" class="h-4 w-4">
                  <path d="M20 6 9 17l-5-5"/>
                </svg>
              } @else {
                {{ paso.id }}
              }
            </span>
            <span [class]="estadoDelPaso(paso.id) === 'pendiente' ? 'text-slate-500' : 'text-morado'"
                  class="max-w-[6.5rem] text-center font-alternate text-dato font-bold uppercase leading-tight tracking-widest transition">
              {{ paso.titulo }}
            </span>
          </button>

          @if (!ultimo) {
            <span [class]="estadoDelPaso(paso.id) === 'completado' ? 'bg-verde-texto' : 'bg-slate-200'"
                  class="mt-[17px] h-0.5 flex-1 self-start transition"></span>
          }
        </li>
      }
    </ol>
  `,
})
export class IndicadorDePasosComponent {
  @Input({ required: true }) pasos: readonly PasoDelIndicador[] = [];
  @Input({ required: true }) pasoActivo = 1;
  @Input() etiqueta = 'Pasos';
  @Output() irAlPaso = new EventEmitter<number>();

  estadoDelPaso(id: number): EstadoDelPaso {
    if (id < this.pasoActivo) return 'completado';
    if (id === this.pasoActivo) return 'activo';
    return 'pendiente';
  }
}
