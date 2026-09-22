import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Hero compacto sin foto (usado por fichas de detalle: componente, subpáginas
 * de ecosistema, etc.). Solo breadcrumb + título + subtítulo opcional, sobre
 * fondo morado plano — sin degradado ni imagen de fondo.
 */
@Component({
  selector: 'app-compact-hero',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="bg-[#291242] pb-12 pt-32 text-white">
      <div [class]="'mx-auto px-6 ' + maxWidthClass">
        <nav aria-label="Ruta de navegación" class="mb-6">
          <ol class="flex items-center gap-2 font-alternate text-[0.66rem] font-bold uppercase tracking-widest">
            <li>
              <button
                type="button"
                (click)="regresoSolicitado.emit()"
                class="cursor-pointer rounded-sm border-0 bg-transparent p-0 uppercase text-[#00DA5E]/75 transition-colors hover:text-[#8BF784] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#00DA5E] focus-visible:ring-offset-2 focus-visible:ring-offset-[#291242]"
              >{{ backLabel }}</button>
            </li>
            @if (current) {
              <li class="text-[#00DA5E]/35" aria-hidden="true">/</li>
              <li class="max-w-[16rem] truncate text-[#00DA5E]" aria-current="page">{{ current }}</li>
            }
          </ol>
        </nav>
        <h1 class="font-alternate text-4xl font-black uppercase leading-tight md:text-5xl">{{ title }}</h1>
        @if (subtitle) {
          <p class="mt-1.5 text-[0.7rem] font-bold uppercase tracking-[0.22em] text-white/40">{{ subtitle }}</p>
        }
      </div>
    </section>
  `,
})
export class CompactHeroComponent {
  @Input() backLabel = 'Inicio';
  @Input() current = '';
  @Input() title = '';
  @Input() subtitle = '';
  @Input() maxWidthClass = 'max-w-7xl';

  @Output() regresoSolicitado = new EventEmitter<void>();
}
