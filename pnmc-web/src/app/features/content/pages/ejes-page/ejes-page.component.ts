import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideArrowUpRight, LucideChevronDown, LucidePlay, LucideTarget } from '@lucide/angular';
import { resolveEjes } from '../../../../core/cms/resolve-ejes';
import { EjeGroup, ejesDataGlobal } from '../../../../core/services/ejes-data.config';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { PageHeroComponent } from '../../../../shared/components/ui/page-hero/page-hero.component';

@Component({
  selector: 'app-ejes-page',
  standalone: true,
  imports: [
    CommonModule,
    PageHeroComponent,
    RouterLink,
    LucideArrowUpRight,
    LucideChevronDown,
    LucidePlay,
    LucideTarget,
  ],
  templateUrl: './ejes-page.component.html',
})
export class EjesPageComponent {
  private readonly webTexts = inject(TextosWebService);

  /**
   * Los textos publicados desde el panel mandan sobre los compilados. Es un
   * `computed` sobre señales del servicio: cuando llega el contenido del
   * servidor, la página se repinta sola.
   */
  readonly ejes = computed(() =>
    resolveEjes(
      ejesDataGlobal,
      (clave) => this.webTexts.getWebText(clave),
      (clave) => this.webTexts.getWebImage(clave),
    ),
  );

  /** Portada de la pagina, administrable. Ver `registro-de-imagenes-web.ts`. */
  imagen(clave: string): string {
    return this.webTexts.getWebImage(clave);
  }

  /** Textos del encabezado, administrables. Ver el grupo `ejes_hero` del registro. */
  texto(clave: string): string {
    return this.webTexts.getWebText(clave);
  }

  readonly expandedComponents = signal<Record<string, number | null>>(
    Object.fromEntries(ejesDataGlobal.map(eje => [eje.id, null])),
  );
  private readonly navigation = inject(NavigationService);

  isExpanded(eje: EjeGroup, index: number): boolean {
    return this.expandedComponents()[eje.id] === index;
  }

  toggleComponent(eje: EjeGroup, index: number): void {
    this.expandedComponents.update(expanded => ({
      ...expanded,
      [eje.id]: expanded[eje.id] === index ? null : index,
    }));
  }

  goHome(): void {
    this.navigation.navigate('home');
  }
}
