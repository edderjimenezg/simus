import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TextosWebService } from '../../core/services/textos-web.service';
import { NavigationService } from '../../core/services/navigation.service';
import { IMAGENES_DEL_BLOQUE } from '../../core/cms/registro-de-imagenes-web';
import { SobreElPnmcPageComponent } from './pages/sobre-el-pnmc-page/sobre-el-pnmc-page.component';

/*
  QUE /pnmc PIDA SUS ONCE FOTOS, EN EL ORDEN DE LAS TARJETAS.

  Once ranuras entraron: cinco hitos y seis normas.
  Se resuelven con claves ARMADAS EN TIEMPO DE EJECUCIÓN —`about_hito_${n}`, `about_norma_${n}`—,
  que es una forma que el compilador no ve.

  El modo de fallar es el mismo que en la portada y por eso la prueba tiene la misma forma: un
  índice corrido pinta LA FOTO DE OTRO HITO, que es una imagen válida en el sitio equivocado.
  Nadie lo nota mirando la página, y `cms:huerfanas` tampoco, porque la clave existe.

  Y una segunda cosa, que aquí importa más que en la portada: que los ocho campos que leen el CMS
  sean SEÑALES. Eran campos llanos resueltos al construir el componente, y con eso el borrador que
  el panel manda por `postMessage` no movía nada en la previsualización.
*/
describe('/pnmc · las once fotos y los textos que se repintan', () => {
  /*
    EL DOBLE GUARDA LOS TEXTOS EN UNA SEÑAL, y eso no es un detalle de montaje: es la mitad de lo
    que esta prueba mide. `TextosWebService` guarda lo publicado en señales
    (`textos-web.service.ts`), así que un `computed` que lo lea se recalcula solo cuando llega
    contenido nuevo. Con un objeto llano el doble no notificaría a nadie, el `computed` devolvería
    siempre su primer valor, y la prueba diría que el componente está congelado aunque no lo esté.
  */
  class TextosEspia {
    readonly imagenesPedidas: string[] = [];
    readonly textos = signal<Record<string, string>>({});

    publicar(clave: string, valor: string): void {
      this.textos.update((previo) => ({ ...previo, [clave]: valor }));
    }

    getWebImage(clave: string): string {
      this.imagenesPedidas.push(clave);
      return `url::${clave}`;
    }

    getWebImageAlt(clave: string): string { return `alt::${clave}`; }
    getWebText(clave: string): string { return this.textos()[clave] ?? `texto::${clave}`; }
    getWebTeamMembers(): unknown[] { return []; }
    pendingLocalContent() { return { textKeys: 0, teamMembers: 0 }; }
  }

  let espia: TextosEspia;
  let componente: SobreElPnmcPageComponent;

  beforeEach(() => {
    espia = new TextosEspia();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SobreElPnmcPageComponent],
      providers: [
        { provide: TextosWebService, useValue: espia },
        { provide: NavigationService, useValue: { navigate: () => { /* no navega en la prueba */ } } },
      ],
    });
    const fixture = TestBed.createComponent(SobreElPnmcPageComponent);
    fixture.detectChanges();
    componente = fixture.componentInstance;
  });

  it('los cinco hitos piden sus cinco fotos, en el orden de las tarjetas', () => {
    expect(componente.timelineEvents().map((h) => h.img))
      .toEqual(IMAGENES_DEL_BLOQUE['about_timeline'].map((k) => `url::${k}`));
  });

  it('las seis normas piden sus seis fotos, en el orden de las tarjetas', () => {
    expect(componente.normativeStages().map((n) => n.img))
      .toEqual(IMAGENES_DEL_BLOQUE['about_normative'].map((k) => `url::${k}`));
  });

  it('la portada de la página pide su ranura y no la de otra sección', () => {
    // `hero_ejes` y `hero_ecosistema` vivían en el mismo grupo que ésta y declaradas en esta
    // sección. Si alguien las devolviera aquí, la portada de /ejes se editaría desde Sobre PNMC.
    expect(componente.imagen('hero_sobre_pnmc')).toBe('url::hero_sobre_pnmc');
    expect(IMAGENES_DEL_BLOQUE['about_hero_presentation']).toEqual(['hero_sobre_pnmc']);
  });

  it('las once son las once del catálogo, sin repetir ninguna', () => {
    const fotos = [
      ...componente.timelineEvents().map((h) => h.img),
      ...componente.normativeStages().map((n) => n.img),
    ];
    expect(fotos.length).toBe(11);
    expect(new Set(fotos).size).toBe(11);
  });

  it('los textos se vuelven a leer cuando el CMS cambia, no se congelan al construir', () => {
    // ESTA ES LA MITAD QUE VALE. Eran campos llanos: `timelineEvents = [...].map(...)` resuelto
    // una vez. En el sitio público colaba porque el arranque espera al servidor antes de pintar,
    // pero el borrador de la previsualización llega DESPUÉS de montar la página, y un campo
    // llano ya no vuelve a mirar. Editar un hito no movía nada en el marco del panel.
    expect(componente.timelineEvents()[0].title).toBe('texto::about_timeline_1_title');

    espia.publicar('about_timeline_1_title', 'Sin publicar todavía');
    expect(componente.timelineEvents()[0].title).toBe('Sin publicar todavía');

    espia.publicar('about_actors_sector_items', ['Uno', 'Dos', 'Tres'].join(String.fromCharCode(10)));
    expect(componente.sectorActors()).toEqual(['Uno', 'Dos', 'Tres']);
  });
});
