import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BarraDeListaComponent } from './barra-de-lista.component';

/**
 * La barra coloca cada pieza por su tipo, no por el orden en que se escribió.
 *
 * <b>EL DEFECTO QUE TRAJO ESTA PRUEBA.</b> La barra era una convención —las mismas clases en el
 * mismo orden porque alguien las copió bien— y se copió mal tres veces: Noticias, Usuarios y roles y
 * Mercados, cada una reclamada por la dirección de producto. Aquí las piezas se escriben AL REVES y se
 * comprueba que salen en el orden del patrón: buscador, filtros, ojo, acciones al extremo.
 */
@Component({
  standalone: true,
  imports: [BarraDeListaComponent],
  template: `
    <app-barra-de-lista>
      <button extremo type="button">Nuevo</button>
      <app-conmutador-de-ojo></app-conmutador-de-ojo>
      <app-filtro-desplegable></app-filtro-desplegable>
      <app-buscador-de-lista></app-buscador-de-lista>
    </app-barra-de-lista>
  `,
  // Los selectores de las piezas reales se declaran como elementos desconocidos a propósito: lo
  // que se mide es DONDE caen, no qué pintan.
  schemas: [],
})
class AnfitrionDesordenado {}

describe('la barra de lista', () => {
  it('coloca las piezas en el orden del patrón aunque se escriban al revés', async () => {
    await TestBed.configureTestingModule({
      imports: [AnfitrionDesordenado],
      errorOnUnknownElements: false,
    }).compileComponents();
    const fixture = TestBed.createComponent(AnfitrionDesordenado);
    fixture.detectChanges();

    const barra: HTMLElement = fixture.nativeElement.querySelector('[data-barra-de-lista]');
    const orden = [...barra.children].map(e => e.tagName.toLowerCase());

    expect(orden).toEqual([
      'app-buscador-de-lista',
      'app-filtro-desplegable',
      'app-conmutador-de-ojo',
      'div', // el extremo
    ]);
    expect(barra.querySelector('.barra-de-lista__extremo button')?.textContent?.trim()).toBe('Nuevo');
  });

  it('no deja un hueco vacío por lo que no se pasa', async () => {
    @Component({ standalone: true, imports: [BarraDeListaComponent], template: `<app-barra-de-lista><app-buscador-de-lista></app-buscador-de-lista></app-barra-de-lista>` })
    class SoloBuscador {}
    await TestBed.configureTestingModule({ imports: [SoloBuscador], errorOnUnknownElements: false }).compileComponents();
    const fixture = TestBed.createComponent(SoloBuscador);
    fixture.detectChanges();

    const barra: HTMLElement = fixture.nativeElement.querySelector('[data-barra-de-lista]');
    // Solo el buscador y el contenedor del extremo, que vacío no ocupa sitio (flex sin hijos).
    expect([...barra.children].map(e => e.tagName.toLowerCase())).toEqual(['app-buscador-de-lista', 'div']);
    expect(barra.querySelector('.barra-de-lista__extremo')?.children.length).toBe(0);
  });
});
