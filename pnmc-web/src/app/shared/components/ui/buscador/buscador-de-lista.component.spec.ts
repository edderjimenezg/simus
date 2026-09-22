import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { BuscadorDeListaComponent } from './buscador-de-lista.component';

@Component({
  standalone: true,
  imports: [BuscadorDeListaComponent],
  template: `
    <app-buscador-de-lista identificador="buscar-cosa" etiqueta="Buscar cosas"
      [espera]="espera()" [valor]="valor()" (cambiar)="recibido.set($event); valor.set($event)" />
  `,
})
class Anfitrion {
  readonly valor = signal('');
  readonly espera = signal(300);
  readonly recibido = signal<string | null>(null);
}

/**
 * El buscador del proyecto filtra mientras se escribe y se puede limpiar de un golpe.
 *
 * <b>LO QUE FIJAN ESTAS PRUEBAS.</b> Quedó definido para todo el proyecto el 15 de
 * septiembre de 2026: «que vayan filtrando a medida que vas escribiendo y no hasta darle enter,
 * poner la x para limpiar la barra de búsqueda y eliminar elementos o botones redundantes». Los
 * trece buscadores que había hacían tres cosas distintas y dos llevaban además un botón «Buscar».
 */
describe('el buscador de una lista', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let anfitrion: Anfitrion;

  function campo(): HTMLInputElement {
    return fixture.nativeElement.querySelector('[data-testid="buscar-cosa"]');
  }

  function escribir(texto: string): void {
    campo().value = texto;
    campo().dispatchEvent(new Event('input'));
  }

  function aspa(): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector('.buscador__limpiar');
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Anfitrion] });
    fixture = TestBed.createComponent(Anfitrion);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('avisa al dejar de escribir, sin pulsar Intro', fakeAsync(() => {
    escribir('festival');

    // TODAVIA NO: si avisara en el acto, «festival» mandaría ocho consultas al servidor.
    expect(anfitrion.recibido()).toBeNull();

    tick(300);
    expect(anfitrion.recibido()).toBe('festival');
  }));

  it('una escritura seguida avisa una sola vez y con lo último', fakeAsync(() => {
    escribir('fes');
    tick(100);
    escribir('festi');
    tick(100);
    escribir('festival');
    tick(300);

    expect(anfitrion.recibido()).toBe('festival');
  }));

  it('con espera en cero avisa en el acto, para lo que se filtra en memoria', () => {
    anfitrion.espera.set(0);
    fixture.detectChanges();

    escribir('ana');

    expect(anfitrion.recibido()).toBe('ana');
  });

  it('Intro adelanta la espera en vez de no hacer nada', () => {
    escribir('ana');
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(anfitrion.recibido()).toBe('ana');
  });

  it('el aspa solo existe cuando hay algo que borrar', () => {
    expect(aspa()).toBeNull();

    escribir('ana');
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(aspa()).not.toBeNull();
  });

  it('el aspa vacía el campo, avisa y devuelve el foco', () => {
    escribir('ana');
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    aspa()!.click();
    fixture.detectChanges();

    expect(anfitrion.recibido()).toBe('');
    expect(campo().value).toBe('');
    // QUIEN LIMPIA UNA BUSQUEDA CASI SIEMPRE VA A ESCRIBIR OTRA.
    expect(document.activeElement).toBe(campo());
  });

  it('Escape también lo vacía', () => {
    escribir('ana');
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(anfitrion.recibido()).toBe('');
  });

  it('el aspa dice qué borra, y no solo «borrar»', () => {
    escribir('ana');
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(aspa()!.getAttribute('aria-label')).toBe('Limpiar buscar cosas');
    // EL `title` TAMBIEN: quien ve el aspa y duda no tiene lector de pantalla que se lo diga.
    expect(aspa()!.getAttribute('title')).toBe('Limpiar buscar cosas');
  });
});
