import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProyectoTransversal, ProyectosTransversalesService } from '../../../../core/services/proyectos-transversales.service';

/**
 * El enlace opcional con las iniciativas del Programa.
 *
 * <b>ES UNA PREGUNTA DISTINTA DE LA CATEGORÍA, Y POR ESO VA APARTE.</b> La categoría dice de qué
 * trata el contenido; esto dice a qué iniciativa pertenece. Un evento puede ser de «Encuentros» y
 * además de Celebra la Música.
 *
 * <b>SOLO SE OFRECEN LAS INICIATIVAS ABIERTAS.</b> Enlazar contenido nuevo a una que ya terminó es
 * casi siempre un descuido; el servidor lo rechaza y aquí ni siquiera se ofrece.
 *
 * <b>SI NO HAY NINGUNA, NO SE PINTA NADA.</b> Una sección vacía con una explicación de por qué está
 * vacía ocupa más de lo que aporta en un formulario que ya es largo.
 */
@Component({
  selector: 'app-selector-de-proyectos',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (proyectos().length > 0) {
      <fieldset class="rounded-xl border border-slate-200 p-4">
        <legend class="px-1 text-dato font-black uppercase tracking-wider text-slate-500">Proyectos del Programa</legend>
        <p class="mb-3 text-dato leading-relaxed text-slate-500">
          Si este contenido forma parte de una iniciativa del Programa, márcala: su portal podrá
          reunir su propio calendario y su propia sala de prensa sin duplicar nada.
        </p>
        <ul class="space-y-1.5">
          @for (proyecto of proyectos(); track proyecto.id) {
            <li>
              <label class="flex items-start gap-2 text-xs text-slate-700">
                <input type="checkbox" class="mt-0.5 accent-morado"
                       [checked]="elegidos.includes(proyecto.id)"
                       (change)="alternar(proyecto.id)" />
                <span>
                  {{ proyecto.nombre }}
                  @if (proyecto.descripcion) {
                    <small class="mt-0.5 block text-dato leading-relaxed text-slate-400">{{ proyecto.descripcion }}</small>
                  }
                </span>
              </label>
            </li>
          }
        </ul>
      </fieldset>
    }
  `,
})
export class SelectorDeProyectosComponent implements OnInit {
  private readonly api = inject(ProyectosTransversalesService);

  @Input() elegidos: readonly number[] = [];
  @Output() elegidosChange = new EventEmitter<number[]>();

  readonly proyectos = signal<ProyectoTransversal[]>([]);

  async ngOnInit(): Promise<void> {
    this.proyectos.set(await this.api.activosParaFormularios());
  }

  alternar(id: number): void {
    const siguientes = this.elegidos.includes(id)
      ? this.elegidos.filter(x => x !== id)
      : [...this.elegidos, id];
    // ORDENADO, para que dos fichas con los mismos proyectos produzcan el mismo cuerpo y el
    // borrador automático no detecte un cambio donde solo hubo otro orden de clics.
    this.elegidosChange.emit([...siguientes].sort((a, b) => a - b));
  }
}
