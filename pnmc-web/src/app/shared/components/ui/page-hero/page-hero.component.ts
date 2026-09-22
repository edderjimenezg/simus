import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TwoToneLineTitleComponent } from '../two-tone-line-title/two-tone-line-title.component';
import { HeroEyebrowComponent } from '../hero-eyebrow/hero-eyebrow.component';
import { LucideChevronDown } from '@lucide/angular';

@Component({
  selector: 'app-page-hero',
  standalone: true,
  imports: [
    CommonModule,
    TwoToneLineTitleComponent,
    HeroEyebrowComponent,
    LucideChevronDown
  ],
  templateUrl: './page-hero.component.html',
  styles: []
})
export class PageHeroComponent {
  @Input() title = '';
  @Input() titleAccent = '';
  @Input() description = '';
  @Input() bgImage = '';
  @Input() childrenPosition = 'default';
  @Input() fullScreen = false;
  @Input() backOnly = false;
  @Input() bgImageClassName = '';
  @Input() titleClassName = '';
  @Input() titleTone = 'default';
  @Input() tag = '';
  @Input() compactNews = false;
  /** Etiqueta del primer nivel del breadcrumb (destino del botón de retorno). */
  @Input() backLabel = 'Inicio';
  /** Indica si la página proyecta contenido en el slot [visual]; si no, el texto usa el ancho completo. */
  @Input() hasVisual = false;

  @Output() regresoSolicitado = new EventEmitter<void>();

  get hasBackListener(): boolean {
    return this.regresoSolicitado.observed;
  }

  /** Segundo nivel del breadcrumb: la página actual. */
  get breadcrumbCurrent(): string {
    return this.tag || this.title || '';
  }

  getFullTitle(): string {
    return [this.title, this.titleAccent].filter(Boolean).join(' ');
  }

  getTitleClassName(): string {
    const sizeClass = this.fullScreen
      ? 'text-4xl sm:text-5xl lg:text-7xl'
      : 'text-3xl sm:text-4xl lg:text-6xl whitespace-nowrap';
    return `${sizeClass} text-white font-gregor leading-[1.1] uppercase tracking-tighter drop-shadow-xl ${this.titleClassName}`.trim();
  }

  scrollDown() {
    // Buscar la altura del viewport actual y bajar
    if (typeof window !== 'undefined') {
      window.scrollTo({
        top: window.innerHeight - 100,
        behavior: 'smooth'
      });
    }
  }
}
