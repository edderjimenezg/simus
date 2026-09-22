import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NotFoundPageComponent } from './not-found-page.component';
import { TextosWebService } from '../../core/services/textos-web.service';
import { DEFAULT_TEXTS, WEB_TEXT_GROUPS } from '../../core/cms/registro-de-textos-web';

/**
 * La página de error es la única del sitio cuyo texto puede volverse falso sin
 * que nadie la toque: cuando una sección se renombra o se retira, el 404 sigue
 * diciendo lo de siempre. Acaba de pasar con `/simus`.
 *
 * Por eso sus cinco textos entran al panel, y por eso esta prueba mira el DOM y
 * no el componente: resolver la clave no es pintarla.
 */
describe('NotFoundPageComponent · textos del CMS', () => {
  const CLAVES = WEB_TEXT_GROUPS
    .find((g) => g.id === 'general_404')!
    .fields.map((f) => f.key);

  const montar = async (publicados: Record<string, string>) => {
    await TestBed.configureTestingModule({
      imports: [NotFoundPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    const textos = TestBed.inject(TextosWebService);
    spyOn(textos, 'getWebText').and.callFake((clave: string) => publicados[clave] ?? DEFAULT_TEXTS[clave] ?? '');

    const fixture: ComponentFixture<NotFoundPageComponent> = TestBed.createComponent(NotFoundPageComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('los cinco textos publicados llegan a la pantalla', async () => {
    const publicados = Object.fromEntries(CLAVES.map((k) => [k, `<<${k}>>`]));
    const fixture = await montar(publicados);
    const texto: string = fixture.nativeElement.textContent;

    expect(CLAVES.length).toBe(5);
    expect(CLAVES.filter((k) => !texto.includes(`<<${k}>>`))).toEqual([]);
  });

  it('sin nada publicado conserva su texto de fábrica', async () => {
    const fixture = await montar({});
    const texto: string = fixture.nativeElement.textContent;

    expect(texto).toContain(DEFAULT_TEXTS['notfound_desc']);
  });

  it('el encabezado sigue siendo un h1, aunque el texto lo mande el panel', async () => {
    // Un 404 sin encabezado de nivel 1 deja a quien navega con lector de
    // pantalla sin saber dónde ha caído. El texto es editable; la jerarquía no.
    const fixture = await montar({ notfound_title: 'Otro título' });
    const h1: HTMLElement | null = fixture.nativeElement.querySelector('h1');

    expect(h1).not.toBeNull();
    expect(h1!.textContent!.trim()).toBe('Otro título');
  });

  it('los dos botones conservan su destino aunque cambie el rótulo', async () => {
    // El reparto de siempre: el panel dice cómo se llama el botón, el código
    // dice adónde lleva. Si el destino se mudara al panel, una edición de texto
    // podría dejar sin salida la única página desde la que alguien está perdido.
    const fixture = await montar({ notfound_cta_home: 'Al inicio', notfound_cta_map: 'Al mapa' });
    const navegacion = fixture.componentInstance['navigationService'];
    const navegar = spyOn(navegacion, 'navigate');

    const botones: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));
    expect(botones.map((b) => b.textContent!.trim())).toEqual(['Al inicio', 'Al mapa']);

    botones[0].click();
    botones[1].click();
    expect(navegar.calls.allArgs()).toEqual([['home'], ['mapa']]);
  });
});
