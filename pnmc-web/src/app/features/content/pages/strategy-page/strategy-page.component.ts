import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavigationService, PAGE_IDS } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { ESTRATEGIAS_COMPILADAS, resolveStrategy } from '../../../../core/cms/resolve-strategy';

@Component({
  selector: 'app-strategy-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './strategy-page.component.html',
})
export class StrategyPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly webTexts = inject(TextosWebService);
  readonly navigation = inject(NavigationService);

  // El texto compilado y el mapeo de claves viven en `core/cms/resolve-strategy`
  // para que se puedan probar sin levantar Angular ni el enrutador.
  readonly strategy = computed(() => {
    const tipo = this.route.snapshot.data['strategy'] === 'investigacion' ? 'investigacion' : 'circulacion';
    return resolveStrategy(
      ESTRATEGIAS_COMPILADAS[tipo],
      (clave) => this.webTexts.getWebText(clave),
      (clave) => this.webTexts.getWebImage(clave),
    );
  });

  readonly mapPage = PAGE_IDS.mapa;
}
