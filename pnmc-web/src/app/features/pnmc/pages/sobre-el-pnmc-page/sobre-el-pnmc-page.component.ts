import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  LucideArrowRight, 
  LucideZap, 
  LucideBoxes, 
  LucideLandmark, 
  LucideGlobe, 
  LucideUsers2, 
  LucideMap, 
  LucideMusic2, 
  LucideBuilding2, 
  LucideUsers, 
  LucideUserCircle2, 
  LucideMessageCircle 
} from '@lucide/angular';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { PageHeroComponent } from '../../../../shared/components/ui/page-hero/page-hero.component';
import { SectionHeaderComponent } from '../../../../shared/components/ui/section-header/section-header.component';
import { TagComponent } from '../../../../shared/components/ui/tag/tag.component';
import { ContentWrapperComponent } from '../../../../shared/components/ui/content-wrapper/content-wrapper.component';

@Component({
  selector: 'app-sobre-el-pnmc-page',
  standalone: true,
  imports: [
    CommonModule,
    PageHeroComponent,
    SectionHeaderComponent,
    TagComponent,
    ContentWrapperComponent,
    LucideArrowRight, 
    LucideZap, 
    LucideBoxes, 
    LucideLandmark, 
    LucideGlobe, 
    LucideUsers2, 
    LucideMap, 
    LucideMusic2, 
    LucideBuilding2, 
    LucideUsers, 
    LucideUserCircle2, 
    LucideMessageCircle
  ],
  templateUrl: './sobre-el-pnmc-page.component.html'
})
export class SobreElPnmcPageComponent {
  private navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);

  /** Portada de la pagina, administrable. Ver `registro-de-imagenes-web.ts`. */
  imagen(clave: string): string {
    return this.webTexts.getWebImage(clave);
  }

  activeStage = signal<number>(4);
  activeNormativeStage = signal<number>(4);

  /*
    TODO LO QUE LEE EL CMS VA EN `computed`, Y NO ES ESTILO.

    Estos ocho campos eran campos llanos, resueltos UNA VEZ al construir el componente. Con eso
    el sitio publico funcionaba de casualidad —el arranque espera al servidor antes de pintar—,
    pero la previsualizacion del panel no: el borrador viaja por `postMessage` DESPUES de que la
    pagina esta montada, y un campo llano ya no vuelve a mirar. Editar un hito o la lista de
    actores no movia nada en el marco, y se leia como que el panel no funciona.

    Es el mismo defecto que tenia `home.component.ts` con la portada, encontrado el 30 de agosto
    de 2026 y corregido el mismo dia. Aqui son ocho.
  */
  readonly workTeam = computed(() => this.webTexts.getWebTeamMembers());
  readonly coordinationTeam = computed(() => this.workTeam().filter(member => member.group === 'coordination'));
  readonly leadershipTeam = computed(() => this.workTeam().filter(member => member.group === 'leadership'));

  /*
    LAS ONCE FOTOS DE ESTA PAGINA SON RANURAS DEL PANEL desde.

    Antes salian de `IMAGENES_DE_GALERIA` con el indice escrito a mano —[11..15] para los hitos
    y [16, 17, 18, 0, 1, 3] para las normas—. Eran decisiones editoriales de portada elegidas a
    dedo dentro de un fichero de TypeScript: la misma figura que las dieciocho de la portada, que
    entraron al panel el mismo dia.

    La clave se arma con la POSICION de la tarjeta, igual que las Rutas del Home. Un indice
    corrido pinta la foto de otro hito —una imagen valida en el sitio equivocado— y eso no lo ve
    nadie mirando la pagina: lo fija `sobre-pnmc-editable.spec.ts`.
  */
  readonly timelineEvents = computed(() => [0, 1, 2, 3, 4].map((index) => {
    const number = index + 1;
    return {
      id: index,
      year: this.getWebText(`about_timeline_${number}_year`),
      title: this.getWebText(`about_timeline_${number}_title`),
      desc: this.getWebText(`about_timeline_${number}_desc`),
      img: this.imagen(`about_hito_${number}`),
    };
  }));

  readonly normativeStages = computed(() => [0, 1, 2, 3, 4, 5].map((index) => {
    const number = index + 1;
    return {
      id: index,
      year: this.getWebText(`about_normative_${number}_year`),
      title: this.getWebText(`about_normative_${number}_title`),
      desc: this.getWebText(`about_normative_${number}_desc`),
      img: this.imagen(`about_norma_${number}`),
    };
  }));

  readonly sectorActors = computed(() => this.getWebTextLines('about_actors_sector_items'));
  readonly institutionalActors = computed(() => this.getWebTextLines('about_actors_institutional_items'));
  readonly civilActors = computed(() => this.getWebTextLines('about_actors_civil_items'));

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }

  getWebTextLines(key: string): string[] {
    return this.getWebText(key)
      .split(/\r?\n/)
      .map((item) => item.trim())
      .filter(Boolean);
  }

  onBackToHome() {
    this.navigationService.navigate('home');
  }

  navigateToSection(page: string, sectionId: string) {
    this.navigationService.navigate(page);
    setTimeout(() => {
      const el = document.getElementById(sectionId);
      if (el) {
        const yOffset = -100;
        const y = el.getBoundingClientRect().top + window.scrollY + yOffset;
        window.scrollTo({ top: y, behavior: 'smooth' });
      }
    }, 150);
  }
}
