import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LucideBarChart3, LucideMap, LucideMapPinned, LucideMusic2 } from '@lucide/angular';

export interface MetricaEcosistema {
  label: string;
  value: string | number;
  detail?: string;
}

/**
 * Tira de recuentos de un directorio publico, tomada del repositorio de referencia.
 *
 * Reemplaza al `<dl>` de cuatro celdas que cada directorio dibujaba a mano: mismo dato -label,
 * value, detail-, una sola fuente del estilo.
 */
@Component({
  selector: 'app-franja-metricas-ecosistema',
  standalone: true,
  imports: [CommonModule, LucideBarChart3, LucideMap, LucideMapPinned, LucideMusic2],
  template: `
    <section class="grid w-full grid-cols-2 divide-x divide-y divide-slate-200/80 sm:grid-cols-4 sm:divide-y-0" aria-label="Resumen del directorio">
      @for (metrica of metricas; track metrica.label) {
        <div class="min-w-0 px-5 py-5 sm:px-6">
          <p class="flex items-center gap-2 font-alternate text-[0.56rem] font-bold uppercase tracking-widest text-[#00a849]">
            @switch (iconoPara(metrica.label)) {
              @case ('departamentos') { <svg lucideMap class="h-3.5 w-3.5" aria-hidden="true"></svg> }
              @case ('municipios') { <svg lucideMapPinned class="h-3.5 w-3.5" aria-hidden="true"></svg> }
              @case ('practicas') { <svg lucideMusic2 class="h-3.5 w-3.5" aria-hidden="true"></svg> }
              @default { <svg lucideBarChart3 class="h-3.5 w-3.5" aria-hidden="true"></svg> }
            }
            {{ metrica.label }}
          </p>
          <p class="mt-3 font-alternate text-4xl font-bold leading-none text-[#291242]">{{ metrica.value }}</p>
          @if (metrica.detail) { <p class="mt-2 text-xs leading-snug text-slate-500">{{ metrica.detail }}</p> }
        </div>
      }
    </section>
  `,
})
export class FranjaMetricasEcosistemaComponent {
  @Input() metricas: MetricaEcosistema[] = [];

  iconoPara(label: string): 'resultados' | 'departamentos' | 'municipios' | 'practicas' {
    const etiquetaNormalizada = label.toLocaleLowerCase('es-CO');

    if (etiquetaNormalizada.includes('departamento')) return 'departamentos';
    if (etiquetaNormalizada.includes('municipio')) return 'municipios';
    if (etiquetaNormalizada.includes('práctica') || etiquetaNormalizada.includes('practica')) return 'practicas';

    return 'resultados';
  }
}
