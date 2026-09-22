import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { ContentWrapperComponent } from '../../../../shared/components/ui/content-wrapper/content-wrapper.component';
import { LucideChevronLeft, LucideChevronRight } from '@lucide/angular';
import { STRATEGIES_DATA } from '../../../../core/services/strategies-data.config';

@Component({
  selector: 'app-home-strategies-section',
  standalone: true,
  imports: [
    CommonModule,
    ContentWrapperComponent,
    LucideChevronLeft,
    LucideChevronRight
  ],
  templateUrl: './home-strategies-section.component.html',
  styles: [
    `
      /*
        El paso del carrusel tiene que coincidir con el ancho de tarjeta, y ese
        ancho ahora es responsive (w-full / sm:w-1/2 / lg:w-1/3). Se resuelve con
        una variable: la plantilla solo publica el indice y aqui se decide cuanto
        vale un paso en cada tamano. Antes el 33,333% estaba escrito a mano en la
        plantilla, lo que obligaba a mostrar tres tarjetas siempre — en un movil
        de 360 px eso daba tarjetas de 89 px.
      */
      .pista-estrategias {
        transform: translateX(calc(var(--indice, 0) * -100%));
      }
      @media (min-width: 640px) {
        .pista-estrategias {
          transform: translateX(calc(var(--indice, 0) * -50%));
        }
      }
      @media (min-width: 1024px) {
        .pista-estrategias {
          transform: translateX(calc(var(--indice, 0) * -33.333333%));
        }
      }
    `,
  ],
})
export class HomeStrategiesSectionComponent implements OnInit, OnDestroy {
  private navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);

  startIndex = signal<number>(0);
  transitionEnabled = signal<boolean>(true);
  isPaused = signal<boolean>(false);

  private autoScrollIntervalId: any = null;

  // Carga reactiva de textos dinámicos desde CMS
  strategies = computed(() => {
    const keysMap: Record<string, string> = {
      'celebra-la-musica': 'celebra',
      'territorios-sonoros': 'territorios',
      'congreso-nacional': 'congreso',
      'tempos-memorias': 'tempos',
      'voces-saberes': 'voces',
      'red-jazz': 'jazz',
      'mercados-musicales': 'mercados',
      'mesas-participacion': 'mesas'
    };

    return STRATEGIES_DATA.map((card, indice) => {
      const shortKey = keysMap[card.id];
      // LA IMAGEN SE RESUELVE SIEMPRE, tenga o no clave de texto: son dos registros distintos y
      // `home_ruta_N` va por POSICION, igual que iba el `IMAGENES_DE_GALERIA[n]` que sustituye.
      // Si un dia se reordena `STRATEGIES_DATA`, las fotos se reordenan con las tarjetas; lo que
      // no puede pasar es que una tarjeta se quede sin foto por no tener clave de texto.
      const img = this.webTexts.getWebImage(`home_ruta_${indice + 1}`);
      if (shortKey) {
        return {
          ...card,
          img,
          tag: this.webTexts.getWebText(`strat_${shortKey}_tag`),
          title: this.webTexts.getWebText(`strat_${shortKey}_title`),
          desc: this.webTexts.getWebText(`strat_${shortKey}_desc`)
        };
      }
      return { ...card, img };
    });
  });

  totalCards = computed(() => this.strategies().length);

  // Extendemos el array para un bucle sin fin (clonando las primeras 3 cartas)
  extendedCards = computed(() => {
    const data = this.strategies();
    return [...data, ...data.slice(0, 3)];
  });

  ngOnInit() {
    this.startAutoScroll();
  }

  ngOnDestroy() {
    this.stopAutoScroll();
  }

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }

  startAutoScroll() {
    this.stopAutoScroll();
    this.autoScrollIntervalId = setInterval(() => {
      if (this.isPaused()) return;
      this.handleNext();
    }, 6000);
  }

  stopAutoScroll() {
    if (this.autoScrollIntervalId) {
      clearInterval(this.autoScrollIntervalId);
      this.autoScrollIntervalId = null;
    }
  }

  handlePrev() {
    this.startAutoScroll(); // reinicia intervalo
    if (!this.transitionEnabled()) return;

    const prevIndex = this.startIndex();
    const total = this.totalCards();

    if (prevIndex === 0) {
      this.transitionEnabled.set(false);
      this.startIndex.set(total);
      setTimeout(() => {
        this.transitionEnabled.set(true);
        this.startIndex.set(total - 1);
      }, 20);
    } else {
      this.startIndex.set(prevIndex - 1);
    }
  }

  handleNext() {
    if (!this.transitionEnabled()) return;

    const prevIndex = this.startIndex();
    const total = this.totalCards();

    if (prevIndex >= total) {
      this.transitionEnabled.set(false);
      this.startIndex.set(0);
      setTimeout(() => {
        this.transitionEnabled.set(true);
        this.startIndex.set(1);
      }, 20);
    } else {
      this.startIndex.set(prevIndex + 1);
    }
  }

  handleTransitionEnd() {
    const idx = this.startIndex();
    const total = this.totalCards();

    if (idx >= total) {
      this.transitionEnabled.set(false);
      this.startIndex.set(0);
      setTimeout(() => {
        this.transitionEnabled.set(true);
      }, 50);
    }
  }

  goToIndex(index: number) {
    this.startAutoScroll();
    this.startIndex.set(index);
  }

  onCardNavigate(navigatePath: string) {
    if (navigatePath.startsWith('comp-')) {
      const compId = navigatePath.substring(5);
      this.navigationService.navigateComponent(compId);
    } else {
      this.navigationService.navigate(navigatePath);
    }
  }

  setPaused(paused: boolean) {
    this.isPaused.set(paused);
  }
}
