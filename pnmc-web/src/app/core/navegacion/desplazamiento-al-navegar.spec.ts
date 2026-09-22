import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { identidadDePantalla } from './desplazamiento-al-navegar';

/**
 * Cuándo una navegación cuenta como «cambiar de pantalla».
 *
 * <b>SE PRUEBA LA DECISION, QUE ES DONDE ESTUVO EL FALLO LAS DOS VECES.</b> La suscripción que la
 * aplica son tres líneas —si el evento trae posición se restaura, si trae ancla se va al ancla, y
 * si no, se sube solo cuando la pantalla cambió— y depende de que el enrutador arranque su
 * `RouterScroller`, que TestBed no monta. Lo que sí se puede fijar aquí, y es lo que se rompió, es
 * la identidad: qué cuenta como pantalla distinta.
 *
 * <b>LAS DOS MITADES QUE SE ROMPIERON POR SEPARADO.</b> Primero, que un detalle en una ruta hija
 * SIN componente no es una pantalla nueva: la ficha del Catálogo Editorial devolvía el mosaico al
 * principio cada vez que se abría algo. Después, al arreglarlo, que una sección que viaja como
 * PARAMETRO de una ruta que sí monta componente SI lo es: la consola se quedaba a mitad de la lista
 * anterior al cambiar de sección, con 1 200 px de desplazamiento puestos.
 *
 * El comportamiento completo —que la página no se mueva y que al cambiar de pantalla suba— se
 * verificó en navegador contra `PNMC_LOCAL`.
 */
@Component({ standalone: true, template: 'catálogo' })
class CatalogoDePrueba {}

@Component({ standalone: true, template: 'consola' })
class ConsolaDePrueba {}

@Component({ standalone: true, template: 'noticias' })
class NoticiasDePrueba {}

describe('qué cuenta como cambiar de pantalla', () => {
  let router: Router;

  const identidadDe = async (url: string) => {
    await router.navigateByUrl(url);
    return identidadDePantalla(router.routerState.snapshot.root);
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          // La ficha del catálogo: una hija SIN componente, que existe para dar dirección a una obra.
          { path: 'editorial', component: CatalogoDePrueba, children: [{ path: ':codigo', children: [] }] },
          // La consola: la sección viaja como parámetro de una ruta que SI monta pantalla.
          { path: 'administracion/:seccion', component: ConsolaDePrueba },
          { path: 'noticias', component: NoticiasDePrueba },
        ]),
      ],
    });
    router = TestBed.inject(Router);
  });

  it('abrir una ficha del catálogo no es cambiar de pantalla', async () => {
    const listado = await identidadDe('/editorial');
    const conFicha = await identidadDe('/editorial/PNMC-ED-019');

    // SI ESTO SE ROMPE, el mosaico vuelve al principio cada vez que se abre una obra.
    expect(conFicha).toBe(listado);
  });

  it('abrir otra ficha distinta tampoco lo es', async () => {
    const una = await identidadDe('/editorial/PNMC-ED-019');
    const otra = await identidadDe('/editorial/PNMC-ED-103');

    expect(otra).toBe(una);
  });

  it('ir a otro módulo sí lo es', async () => {
    const catalogo = await identidadDe('/editorial');
    const noticias = await identidadDe('/noticias');

    expect(noticias).not.toBe(catalogo);
  });

  it('cambiar de sección en la consola sí lo es, aunque sea el mismo componente', async () => {
    const catalogo = await identidadDe('/administracion/catalogo-editorial');
    const usuarios = await identidadDe('/administracion/usuarios');

    // LA SECCION ES UN PARAMETRO DE UNA RUTA QUE MONTA PANTALLA. Mirar solo la ruta —que es lo que
    // hacía la primera versión— dejaba la consola donde estuviera al cambiar de sección.
    expect(usuarios).not.toBe(catalogo);
  });
});
