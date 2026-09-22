import { Component, EventEmitter, Input, Output } from '@angular/core';
import { LucideLayoutGrid, LucideList } from '@lucide/angular';
import { FranjaMetricasEcosistemaComponent, MetricaEcosistema } from '../franja-metricas-ecosistema/franja-metricas-ecosistema.component';

/**
 * `listado`/`cuadricula`, no `lista`/`mosaico`: son los mismos dos nombres que ya usaba
 * `FestivalesPublicosPageComponent` (y su prueba) antes de portar este componente. Cambiar el
 * vocabulario aqui habria significado tocar el tipo publico `VistaFestivales` y las pruebas que ya
 * lo comprueban, por una diferencia de nombre sin efecto visible.
 */
export type VistaEcosistema = 'listado' | 'cuadricula';

@Component({
  selector: 'app-barra-exploracion-ecosistema',
  standalone: true,
  imports: [FranjaMetricasEcosistemaComponent, LucideLayoutGrid, LucideList],
  template: `
    <section class="grid gap-4 border-b border-slate-100 pb-7 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
      <app-franja-metricas-ecosistema [metricas]="metricas"></app-franja-metricas-ecosistema>
      <div class="flex shrink-0 items-center self-center justify-self-start rounded-xl border border-slate-200 bg-white p-1 lg:justify-self-end" role="group" aria-label="Modo de visualización">
        <button type="button" (click)="cambioVista.emit('listado')" [attr.aria-pressed]="vista === 'listado'" title="Ver en listado" [class]="'rounded-lg px-3 py-2 cursor-pointer border-0 transition ' + (vista === 'listado' ? 'bg-[#291242] text-white' : 'bg-transparent text-slate-500 hover:bg-slate-50')"><svg lucideList [size]="15"></svg><span class="sr-only">Listado</span></button>
        <button type="button" (click)="cambioVista.emit('cuadricula')" [attr.aria-pressed]="vista === 'cuadricula'" title="Ver en cuadrícula" [class]="'rounded-lg px-3 py-2 cursor-pointer border-0 transition ' + (vista === 'cuadricula' ? 'bg-[#291242] text-white' : 'bg-transparent text-slate-500 hover:bg-slate-50')"><svg lucideLayoutGrid [size]="15"></svg><span class="sr-only">Cuadrícula</span></button>
      </div>
    </section>
  `,
})
export class BarraExploracionEcosistemaComponent {
  @Input() metricas: MetricaEcosistema[] = [];
  @Input() vista: VistaEcosistema = 'listado';
  @Output() cambioVista = new EventEmitter<VistaEcosistema>();
}
