import { Component, OnInit, OnDestroy, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationService, PAGE_IDS } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { LucideArrowRight } from '@lucide/angular';

interface Slide {
  url: string;
  tag: string;
  title: string;
  desc: string;
  cta: string;
  actionId: string;
}

@Component({
  selector: 'app-home-media-banner',
  standalone: true,
  imports: [CommonModule, LucideArrowRight],
  templateUrl: './home-media-banner.component.html',
  styles: []
})
export class HomeMediaBannerComponent implements OnInit, OnDestroy {
  private navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);

  activeIndex = signal<number>(0);
  progress = signal<number>(0);

  private intervalId: any = null;

  /**
   * LAS TRES DIAPOSITIVAS, con su texto y su fondo del CMS desde.
   *
   * Antes eran un arreglo literal: doce cadenas y tres `HOME_BANNER_IMAGES[n]` escritos en este
   * fichero. Cambiar «Se parte del ecosistema» obligaba a tocar el codigo.
   *
   * `computed` Y NO UN CAMPO, por lo mismo que en la portada del Home: `getWebText` y
   * `getWebImage` leen señales, y un arreglo construido una vez en el constructor se quedaria
   * con lo que hubiera antes de que llegara el manifiesto —y nunca veria un borrador de la
   * previsualizacion del panel—.
   *
   * `actionId` NO SALE DEL CMS y no debe salir: es el destino al que lleva el boton, no su
   * rotulo. Un editor que renombre «Explorar estrategia» no esta pidiendo que el boton lleve a
   * otra pagina, y al reves seria peor: un destino editable es un enlace roto a un clic.
   */
  readonly slides = computed<Slide[]>(() => [
    {
      url: this.webTexts.getWebImage('home_banner_1'),
      tag: this.webTexts.getWebText('home_banner1_tag'),
      title: this.webTexts.getWebText('home_banner1_title'),
      desc: this.webTexts.getWebText('home_banner1_desc'),
      cta: this.webTexts.getWebText('home_banner1_cta'),
      actionId: PAGE_IDS.registro,
    },
    {
      url: this.webTexts.getWebImage('home_banner_2'),
      tag: this.webTexts.getWebText('home_banner2_tag'),
      title: this.webTexts.getWebText('home_banner2_title'),
      desc: this.webTexts.getWebText('home_banner2_desc'),
      cta: this.webTexts.getWebText('home_banner2_cta'),
      actionId: 'estrategia-circulacion',
    },
    {
      url: this.webTexts.getWebImage('home_banner_3'),
      tag: this.webTexts.getWebText('home_banner3_tag'),
      title: this.webTexts.getWebText('home_banner3_title'),
      desc: this.webTexts.getWebText('home_banner3_desc'),
      cta: this.webTexts.getWebText('home_banner3_cta'),
      actionId: 'estrategia-investigacion',
    },
  ]);

  ngOnInit() {
    this.startBannerLoop();
  }

  ngOnDestroy() {
    this.stopBannerLoop();
  }

  startBannerLoop() {
    this.stopBannerLoop();
    this.intervalId = setInterval(() => {
      const current = this.progress();
      if (current >= 100) {
        this.activeIndex.set((this.activeIndex() + 1) % this.slides().length);
        this.progress.set(0);
      } else {
        this.progress.set(current + 1);
      }
    }, 60);
  }

  stopBannerLoop() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  selectSlide(index: number) {
    this.activeIndex.set(index);
    this.progress.set(0);
    this.startBannerLoop(); // reinicia intervalo
  }

  /**
   * Cada botón del banner navega a su destino, y nada más.
   *
   * LO EL MODELOY POR QUÉ SE FUE. «Ser parte del ecosistema» llevaba al mapa y luego,
   * medio segundo después, buscaba en el documento un botón con
   * `[data-open-participation="true"]` y lo pulsaba por su cuenta. Tres cosas mal en una: el
   * destino no era el que anuncia el rótulo; si el mapa tardaba más de 500 ms en pintar, el
   * botón no existía todavía y no pasaba nada —sin error, sin aviso—; y renombrar ese atributo
   * en el mapa rompía esta pantalla sin que nada lo relacionara.
   *
   * Ahora va a `/registro`, que es la pantalla de registro e inicio de sesión, y es lo que el
   * rótulo promete.
   */
  onAction(actionId: string) {
    this.navigationService.navigate(actionId);
  }
}
