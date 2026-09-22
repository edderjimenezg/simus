import { Component, Input, Output, EventEmitter, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ContentWrapperComponent } from '../../../../shared/components/ui/content-wrapper/content-wrapper.component';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { LucideChevronRight, LucideArrowRight } from '@lucide/angular';

@Component({
  selector: 'app-pnmc-preview-section',
  standalone: true,
  imports: [
    CommonModule,
    ContentWrapperComponent,
    LucideChevronRight,
    LucideArrowRight
  ],
  templateUrl: './pnmc-preview-section.component.html',
  styles: []
})
export class PNMCPreviewSectionComponent {
  private webTexts = inject(TextosWebService);

  @Input() scrollTargetElement: HTMLElement | null = null;
  @Output() navegacionSolicitada = new EventEmitter<string>();

  /**
   * La fotografia grande de «Huella y evolucion».
   *
   * Era `IMAGENES_DE_GALERIA[5]`, un indice fijo a mano en `catalogo-de-imagenes-de-galeria.ts`. Desde el
   * 30 de agosto de 2026 es una ranura del CMS y se resuelve como `computed`, no en una
   * asignacion: `getWebImage` lee la señal del manifiesto, y con un campo llano la portada se
   * quedaria con el valor que hubiera en el instante de construir el componente —antes de que
   * llegara el manifiesto, y sin enterarse nunca de un borrador de la previsualizacion—.
   */
  readonly artistsImage = computed(() => this.webTexts.getWebImage('home_identidad_media'));

  readonly artistsAlt = computed(() => this.webTexts.getWebImageAlt('home_identidad_media'));

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }

  navigateToSection(page: string, sectionId: string): void {
    this.navegacionSolicitada.emit(page);
    setTimeout(() => {
      const el = document.getElementById(sectionId);
      if (el) {
        const offset = 112; // NAVBAR_SCROLL_OFFSET
        const elementPosition = el.getBoundingClientRect().top + window.pageYOffset;
        window.scrollTo({
          top: elementPosition - offset,
          behavior: 'smooth'
        });
      }
    }, 150);
  }

  navigateToPage(page: string): void {
    this.navegacionSolicitada.emit(page);
  }
}
