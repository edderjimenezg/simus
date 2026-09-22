import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { StrategyPageComponent } from './strategy-page.component';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { DEFAULT_TEXTS } from '../../../../core/cms/registro-de-textos-web';

/**
 * Resolver la clave no es lo mismo que pintarla.
 *
 * Los dos botones del encabezado del Home se resolvían bien y aun así no
 * llegaban a la página: Angular descartaba en silencio el contenido proyectado.
 * Por eso esta prueba mira el DOM y no el objeto que devuelve la función.
 */
describe('StrategyPageComponent · textos del CMS', () => {
  const montar = async (tipo: string, publicados: Record<string, string>) => {
    await TestBed.configureTestingModule({
      imports: [StrategyPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { strategy: tipo } } } },
      ],
    }).compileComponents();

    const textos = TestBed.inject(TextosWebService);
    spyOn(textos, 'getWebText').and.callFake((clave: string) => publicados[clave] ?? DEFAULT_TEXTS[clave] ?? '');

    const fixture: ComponentFixture<StrategyPageComponent> = TestBed.createComponent(StrategyPageComponent);
    fixture.detectChanges();
    return fixture;
  };

  const CLAVES = [
    'strategy_celebra_hero_desc',
    'strategy_celebra_intro',
    'strategy_celebra_section_title',
    'strategy_celebra_mission',
    'strategy_celebra_edition_intro',
    'strategy_celebra_edition_vision',
    'strategy_celebra_edition_closing',
  ];

  it('los 7 textos publicados llegan a la pantalla', async () => {
    const publicados = Object.fromEntries(CLAVES.map((k) => [k, `<<${k}>>`]));
    const fixture = await montar('circulacion', publicados);
    const texto: string = fixture.nativeElement.textContent;

    expect(CLAVES.filter((k) => !texto.includes(`<<${k}>>`))).toEqual([]);
  });

  it('sin nada publicado muestra la copia institucional sembrada', async () => {
    const fixture = await montar('circulacion', {});
    const texto: string = fixture.nativeElement.textContent;

    expect(texto).toContain(DEFAULT_TEXTS['strategy_celebra_mission']);
    expect(texto).toContain(DEFAULT_TEXTS['strategy_celebra_edition_closing']);
  });

  it('un campo publicado en blanco no deja un título vacío en la página', async () => {
    const fixture = await montar('circulacion', { strategy_celebra_section_title: '' });

    const titulos: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('h2'));
    expect(titulos.every((h) => h.textContent!.trim().length > 0)).toBeTrue();
  });

  it('Territorios Sonoros sigue con su texto compilado y sin bloque narrativo', async () => {
    const fixture = await montar('investigacion', {});
    const texto: string = fixture.nativeElement.textContent;

    expect(texto).toContain('Territorios Sonoros');
    expect(texto).not.toContain(DEFAULT_TEXTS['strategy_celebra_mission']);
  });
});
