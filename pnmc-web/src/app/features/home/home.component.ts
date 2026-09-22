import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LucideArrowRight } from '@lucide/angular';
import { NavigationService } from '../../core/services/navigation.service';
import { TextosWebService } from '../../core/services/textos-web.service';
import { PageHeroComponent } from '../../shared/components/ui/page-hero/page-hero.component';
import { TagComponent } from '../../shared/components/ui/tag/tag.component';
import { FooterComponent } from '../../shared/components/layout/footer/footer.component';

import { PNMCPreviewSectionComponent } from './components/pnmc-preview-section/pnmc-preview-section.component';
import { HomeStrategiesSectionComponent } from './components/home-strategies-section/home-strategies-section.component';
import { HomeMediaBannerComponent } from './components/home-media-banner/home-media-banner.component';
import { MapaEcosistemicoPreviewComponent } from './components/mapa-ecosistemico-preview/mapa-ecosistemico-preview.component';
import { NoticiasAgendaPreviewComponent } from './components/noticias-agenda-preview/noticias-agenda-preview.component';
import { BoletinFormComponent } from './components/boletin-form/boletin-form.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    PageHeroComponent,
    TagComponent,
    FooterComponent,
    PNMCPreviewSectionComponent,
    HomeStrategiesSectionComponent,
    HomeMediaBannerComponent,
    MapaEcosistemicoPreviewComponent,
    NoticiasAgendaPreviewComponent,
    BoletinFormComponent,
    LucideArrowRight
  ],
  templateUrl: './home.component.html',
  styles: []
})
export class HomeComponent {
  navigationService = inject(NavigationService);
  webTexts = inject(TextosWebService);

  scrollTargetElement: HTMLElement | null = null;

  /**
   * Las CUATRO ranuras de portada del Home, en el orden del registro. La pagina
   * elige una al azar en cada visita, que es lo que hacia antes; lo que cambia
   * es de donde salen las URLs.
   *
   * SE RESUELVEN AQUI Y NO EN `catalogo-de-imagenes-de-galeria.ts`. Ese fichero se evalua
   * al cargar el modulo, antes de que exista un inyector y antes de que el
   * manifiesto haya llegado: llamar alli al servicio capturaria un valor una vez
   * y no lo actualizaria nunca. El sintoma seria «publique y no cambio», que es
   * el defecto de fondo que origino toda la instrumentacion del CMS.
   */
  private readonly clavesDePortada = ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'];

  /**
   * CUAL DE LAS CUATRO, elegido UNA VEZ por visita.
   *
   * El sorteo va aqui y no dentro del `computed` a proposito: un `Math.random()` dentro se
   * volveria a tirar en cada recalculo y la portada cambiaria sola mientras alguien mira la
   * pagina.
   */
  private readonly portadaDeLaVisita = Math.floor(Math.random() * 4);

  /**
   * La URL de la portada, RECALCULADA cuando cambia lo que la decide.
   *
   * Era un campo llano asignado en `ngOnInit`, y eso lo dejaba congelado en el valor que
   * `getWebImage` devolviera en ese instante. Dos consecuencias, y la segunda es la que se vio
   *: si el manifiesto llega despues del primer pintado, la portada se
   * queda en la compilada; y en la previsualizacion del panel, una portada recien subida no
   * aparecia nunca, porque el puente la entrega por un mensaje posterior a `ngOnInit`.
   *
   * `getWebImage` lee el `signal` del manifiesto, asi que como `computed` reacciona a los dos.
   */
  readonly homeHeroBgImage = computed(
    () => this.webTexts.getWebImage(this.clavesDePortada[this.portadaDeLaVisita]),
  );

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }

  navigateTo(pageId: string) {
    this.navigationService.navigate(pageId);
  }

  navigateToArticle(article: any) {
    this.navigationService.navigateToArticle(article);
  }

  navigateToAgendaEvent(eventId: string) {
    this.navigationService.navigateToAgendaEvent(eventId);
  }

  navigateToMapLayer(layerName: string) {
    this.navigationService.navigateToMapLayer(layerName);
  }

  registerScrollTarget(element: HTMLElement) {
    this.scrollTargetElement = element;
  }
}
