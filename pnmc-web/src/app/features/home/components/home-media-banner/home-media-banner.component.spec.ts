import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavigationService, PAGE_IDS } from '../../../../core/services/navigation.service';
import { HomeMediaBannerComponent } from './home-media-banner.component';

/**
 * El botón del banner lleva a donde dice su rótulo.
 *
 * LO EL MODELO. «Ser parte del ecosistema» navegaba al mapa y, medio segundo más tarde,
 * buscaba en el documento un botón con `[data-open-participation="true"]` y lo pulsaba por su
 * cuenta. Si el mapa tardaba más de 500 ms en pintar, ese botón no existía todavía y no pasaba
 * nada: la persona se quedaba en el mapa sin entender por qué. Sin error y sin traza.
 */
describe('HomeMediaBannerComponent', () => {
  let fixture: ComponentFixture<HomeMediaBannerComponent>;
  let destinos: string[];

  beforeEach(async () => {
    destinos = [];

    await TestBed.configureTestingModule({
      imports: [HomeMediaBannerComponent],
      providers: [
        {
          provide: NavigationService,
          useValue: {
            navigate: (pageId: string) => destinos.push(pageId),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomeMediaBannerComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    // El banner arranca un setInterval en ngOnInit: sin esto queda corriendo entre pruebas.
    fixture.destroy();
  });

  it('«Ser parte del ecosistema» lleva al registro, no al mapa', () => {
    const banner = fixture.componentInstance;
    const diapositiva = banner.slides().find(item => item.cta === 'Ser parte del ecosistema');

    expect(diapositiva).toBeDefined();

    banner.onAction(diapositiva!.actionId);

    expect(destinos).toEqual([PAGE_IDS.registro]);
  });

  it('no pulsa botones de otra pantalla por su cuenta', () => {
    // EL CANARIO DEL TRUCO RETIRADO. Una llamada a `onAction` no debe tocar el documento
    // buscando controles ajenos: si vuelve a hacerlo, el destino deja de estar en el código y
    // pasa a depender de que otra pantalla termine de pintar a tiempo.
    const espia = spyOn(document, 'querySelector').and.callThrough();

    fixture.componentInstance.onAction(PAGE_IDS.registro);

    expect(espia).not.toHaveBeenCalled();
  });

  it('cada diapositiva navega a su propio destino', () => {
    const banner = fixture.componentInstance;

    banner.slides().forEach(diapositiva => banner.onAction(diapositiva.actionId));

    expect(destinos).toEqual(banner.slides().map(diapositiva => diapositiva.actionId));
  });
});
