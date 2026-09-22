import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ClasificacionDeContenidoService,
  ElementoDeClasificacion,
} from '../../../../core/services/clasificacion-de-contenido.service';

/**
 * La clasificación opcional por prácticas musicales y territorios sonoros.
 *
 * <b>CASILLAS Y NO UN DESPLEGABLE MULTIPLE.</b> Los dos catálogos son cortos y se leen de una
 * ojeada; un `<select multiple>` obliga a mantener pulsada una tecla para elegir dos cosas, que es
 * la interacción que más gente desconoce de todo el formulario. Con casillas, lo elegido se ve sin
 * abrir nada.
 *
 * <b>ES OPCIONAL Y LO DICE.</b> No clasificar es una respuesta válida —un comunicado
 * administrativo no pertenece a ninguna práctica—, y una sección que no explica eso invita a
 * marcar algo por si acaso, que ensucia el dato para siempre.
 */
@Component({
  selector: 'app-selector-de-clasificacion',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="grid gap-5 sm:grid-cols-2">
      <fieldset class="rounded-xl border border-slate-200 p-4">
        <legend class="px-1 text-dato font-black uppercase tracking-wider text-slate-500">Prácticas musicales</legend>
        @if (practicas().length === 0) {
          <p class="text-dato text-slate-400">El catálogo de prácticas no está disponible ahora mismo.</p>
        } @else {
          <ul class="space-y-1.5">
            @for (practica of practicas(); track practica.id) {
              <li>
                <label class="flex items-start gap-2 text-xs text-slate-700">
                  <input type="checkbox" class="mt-0.5 accent-morado"
                         [checked]="practicasElegidas.includes(practica.id)"
                         (change)="alternar('practica', practica.id)" />
                  <span>{{ practica.nombre }}</span>
                </label>
              </li>
            }
          </ul>
        }
      </fieldset>

      <fieldset class="rounded-xl border border-slate-200 p-4">
        <legend class="px-1 text-dato font-black uppercase tracking-wider text-slate-500">Territorios sonoros</legend>
        @if (territorios().length === 0) {
          <p class="text-dato text-slate-400">El catálogo de territorios no está disponible ahora mismo.</p>
        } @else {
          <ul class="space-y-1.5">
            @for (territorio of territorios(); track territorio.id) {
              <li>
                <label class="flex items-start gap-2 text-xs text-slate-700">
                  <input type="checkbox" class="mt-0.5 accent-morado"
                         [checked]="territoriosElegidos.includes(territorio.id)"
                         (change)="alternar('territorio', territorio.id)" />
                  <span>{{ territorio.nombre }}</span>
                </label>
              </li>
            }
          </ul>
        }
      </fieldset>
    </div>

    <p class="mt-2 text-dato leading-relaxed text-slate-400">
      Es opcional: sirve para que el portal pueda cruzar este contenido con las fichas de prácticas
      y territorios. Si no corresponde a ninguno, déjalo sin marcar.
    </p>
  `,
})
export class SelectorDeClasificacionComponent implements OnInit {
  private readonly api = inject(ClasificacionDeContenidoService);

  @Input() practicasElegidas: readonly number[] = [];
  @Input() territoriosElegidos: readonly number[] = [];
  @Output() practicasElegidasChange = new EventEmitter<number[]>();
  @Output() territoriosElegidosChange = new EventEmitter<number[]>();

  readonly practicas = signal<ElementoDeClasificacion[]>([]);
  readonly territorios = signal<ElementoDeClasificacion[]>([]);

  async ngOnInit(): Promise<void> {
    this.practicas.set(await this.api.practicasMusicales());
    this.territorios.set(await this.api.territoriosSonoros());
  }

  alternar(catalogo: 'practica' | 'territorio', id: number): void {
    const actuales = catalogo === 'practica' ? this.practicasElegidas : this.territoriosElegidos;
    const siguientes = actuales.includes(id)
      ? actuales.filter(x => x !== id)
      : [...actuales, id];

    // SE EMITE ORDENADO para que dos fichas con la misma clasificación produzcan el mismo cuerpo:
    // así el borrador automático no detecta un cambio donde solo hubo otro orden de clics.
    const ordenados = [...siguientes].sort((a, b) => a - b);
    if (catalogo === 'practica') {
      this.practicasElegidasChange.emit(ordenados);
    } else {
      this.territoriosElegidosChange.emit(ordenados);
    }
  }
}
