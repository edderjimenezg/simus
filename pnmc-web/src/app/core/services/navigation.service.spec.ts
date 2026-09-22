import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NavigationService, PAGE_IDS } from './navigation.service';

/**
 * De qué página cree el sitio que es cada dirección.
 *
 * No es una curiosidad interna: **el armazón decide por este identificador si
 * dibuja el menú y el pie**. Equivocarlo no produce un error visible, produce
 * una página sin salida.
 *
 * Pasó de verdad. La comparación era `normalizedPath.endsWith(pagePath)`, así
 * que `/x/admin` «terminaba en» `/admin` y se clasificaba como la consola:
 * quien escribiera mal una dirección con esa palabra recibía **el 404 sin menú
 * y sin pie**, sin ningún enlace para moverse. Con `/pnmc/mapa` pasaba a
 * medias: 404 con menú pero sin pie. Ninguna prueba lo veía porque no existía
 * este archivo.
 */
describe('NavigationService · de qué página es cada dirección', () => {
  let servicio: NavigationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    servicio = TestBed.inject(NavigationService);
  });

  // El cast es estructural y no `any` a proposito: si el metodo se renombra o
  // cambia de firma, esto deja de compilar en vez de fallar en tiempo de
  // ejecucion. Sigue alcanzando un privado, que es lo que de verdad hay que
  // quitar: cuando el pageId pase a declararse en la ruta (`data: { pageId }`)
  // esta prueba debera leer datos y no forzar la puerta de un privado. Si el
  // cast sobrevive a aquel cambio, es que el pageId no acabo siendo declarativo.
  const idDe = (ruta: string) =>
    (servicio as unknown as { getPageIdFromPath(r: string): string }).getPageIdFromPath(ruta);

  it('reconoce las páginas por su ruta exacta', () => {
    expect(idDe('/')).toBe(PAGE_IDS.home);
    expect(idDe('/pnmc')).toBe(PAGE_IDS.pnmc);
    expect(idDe('/mapa-ecosistemico')).toBe(PAGE_IDS.mapa);
    expect(idDe('/ecosistema')).toBe(PAGE_IDS.ecosistema);
  });

  it('reconoce una ficha como parte de su sección', () => {
    // `/noticias/12` es la sección de noticias, no una ruta desconocida: el
    // menú y el pie tienen que salir igual que en el listado.
    expect(idDe('/noticias/12')).toBe(PAGE_IDS.noticias);
    expect(idDe('/ecosistema/festivales/abc')).toBe(PAGE_IDS.ecosistema);
  });

  it('prefiere la coincidencia más específica', () => {
    // La ficha pública de Festival empieza por `/ecosistema`, y ambas rutas existen.
    expect(idDe('/ecosistema/festivales')).not.toBe(PAGE_IDS.noEncontrado);
  });

  it('NO confunde una ruta inventada con la página cuyo nombre lleva al final', () => {
    // El defecto que motivó este archivo, y el que de verdad dolía: estas
    // direcciones se clasificaban como la consola, el registro o el mapa —
    // páginas que el armazón dibuja SIN menú y sin pie—, así que el 404 salía
    // sin ninguna salida.
    for (const inventada of ['/x/admin', '/algo/registro', '/otro/colaboradores', '/y/mapa-ecosistemico']) {
      expect(idDe(inventada))
        .withContext(`«${inventada}» debería ser una ruta desconocida`)
        .toBe(PAGE_IDS.noEncontrado);
    }
  });

  it('una ruta inventada DENTRO de una sección hereda su decorado, y eso está bien', () => {
    // `/pnmc/mapa` no existe como ruta, así que Angular dibuja el 404. Este
    // servicio la clasifica como «Sobre el PNMC» porque cuelga de ella, y esa
    // es la respuesta útil: el 404 sale con el menú y el pie de esa sección, en
    // vez de quedarse sin ninguno.
    //
    // LO QUE QUEDA: el título de la pestaña del navegador dirá «Sobre el PNMC»
    // mientras la página muestra un error. Distinguirlo exigiría que este
    // servicio conociera la tabla de rutas del router, y hoy no la conoce.
    expect(idDe('/pnmc/mapa')).toBe(PAGE_IDS.pnmc);
    expect(idDe('/pnmc/lo-que-sea')).toBe(PAGE_IDS.pnmc);
  });

  it('deja el 404 con su decorado propio, que es el que tiene salidas', () => {
    // La consecuencia que importa: si el 404 se clasificara como la consola o
    // como el mapa, el armazón le quitaría el menú, el pie, o los dos.
    const id = idDe('/esta-ruta-no-existe');
    expect(id).toBe(PAGE_IDS.noEncontrado);
    expect(id).not.toBe(PAGE_IDS.admin);
    expect(id).not.toBe(PAGE_IDS.home);
  });
});
