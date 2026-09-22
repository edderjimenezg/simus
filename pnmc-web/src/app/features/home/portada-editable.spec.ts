import { TestBed } from '@angular/core/testing';
import { TextosWebService } from '../../core/services/textos-web.service';
import { ContenidoWebApiService } from '../../core/services/contenido-web-api.service';
import { IMAGENES_DEL_BLOQUE } from '../../core/cms/registro-de-imagenes-web';
import { PNMCPreviewSectionComponent } from './components/pnmc-preview-section/pnmc-preview-section.component';
import { HomeStrategiesSectionComponent } from './components/home-strategies-section/home-strategies-section.component';
import { HomeMediaBannerComponent } from './components/home-media-banner/home-media-banner.component';

/*
  QUE CADA BLOQUE DE LA PORTADA PIDA SUS PROPIAS RANURAS.

  El 30 de agosto de 2026 entraron dieciocho ranuras de imagen en la portada. Todas se resuelven
  con claves ARMADAS EN TIEMPO DE EJECUCIÓN —`home_ruta_${i + 1}`, `home_eco_${i + 1}`— o con
  literales repartidos por tres componentes. Ninguna de esas dos formas la ve el compilador.

  El modo de fallar es silencioso y caro: una clave con el índice corrido pinta LA FOTO DE OTRA
  TARJETA, que es una imagen perfectamente válida en el sitio equivocado. Nadie lo nota mirando
  la página, y `cms:huerfanas` tampoco, porque la clave existe y alguien la lee.

  Lo que se hace aquí es escuchar QUÉ CLAVES PIDE cada componente y compararlas, una a una y en
  orden, con lo que el registro dice que le toca a ese bloque.
*/
describe('portada · cada bloque pide las ranuras que el registro le asigna', () => {
  /** Doble que apunta cada clave pedida y devuelve algo reconocible. */
  class TextosEspia {
    readonly imagenesPedidas: string[] = [];
    readonly textosPedidos: string[] = [];

    getWebImage(clave: string): string {
      this.imagenesPedidas.push(clave);
      return `url::${clave}`;
    }

    getWebImageAlt(clave: string): string { return `alt::${clave}`; }

    getWebText(clave: string): string {
      this.textosPedidos.push(clave);
      return `texto::${clave}`;
    }

    getWebTeamMembers(): unknown[] { return []; }
    pendingLocalContent() { return { textKeys: 0, teamMembers: 0 }; }
  }

  let espia: TextosEspia;

  function montar<T>(componente: new (...args: never[]) => T): T {
    espia = new TextosEspia();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [componente as never],
      providers: [
        { provide: TextosWebService, useValue: espia },
        { provide: ContenidoWebApiService, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(componente as never);
    fixture.detectChanges();
    return fixture.componentInstance as T;
  }

  it('la sección de identidad pide su única foto', () => {
    const c = montar(PNMCPreviewSectionComponent);
    expect(c.artistsImage()).toBe('url::home_identidad_media');
    expect(espia.imagenesPedidas).toEqual(IMAGENES_DEL_BLOQUE['home_about']);
  });

  it('las ocho rutas piden sus ocho fotos, en el orden de las tarjetas', () => {
    // POR POSICIÓN, no por el id de la estrategia: es lo mismo que hacía el
    // `IMAGENES_DE_GALERIA[n]` que sustituyeron. Un índice corrido pondría la foto de
    // «Territorios Sonoros» en la tarjeta de «Celebra la Música» sin que nada lo dijera.
    const c = montar(HomeStrategiesSectionComponent);
    const tarjetas = c.strategies();

    expect(tarjetas.length).toBe(8);
    expect(tarjetas.map(t => t.img))
      .toEqual(IMAGENES_DEL_BLOQUE['home_strategies_cards'].map(k => `url::${k}`));
  });

  it('el banner pide sus tres fondos y sus doce textos, y NO el destino', () => {
    const c = montar(HomeMediaBannerComponent);
    const diapositivas = c.slides();

    expect(diapositivas.map(d => d.url))
      .toEqual(IMAGENES_DEL_BLOQUE['home_banner'].map(k => `url::${k}`));

    // Los doce textos: etiqueta, título, descripción y botón de cada una.
    expect(espia.textosPedidos.length).toBe(12);
    for (const n of [1, 2, 3]) {
      for (const campo of ['tag', 'title', 'desc', 'cta']) {
        expect(espia.textosPedidos).toContain(`home_banner${n}_${campo}`);
      }
    }

    // EL DESTINO NO SALE DEL CMS. Si alguien lo abriera, un editor podría renombrar el botón y
    // mandar el clic a otra página sin enterarse. Los tres destinos siguen siendo literales.
    expect(espia.textosPedidos.some(k => k.includes('action'))).toBeFalse();
    expect(diapositivas.map(d => d.actionId))
      .toEqual(['registro', 'estrategia-circulacion', 'estrategia-investigacion']);
  });
});
