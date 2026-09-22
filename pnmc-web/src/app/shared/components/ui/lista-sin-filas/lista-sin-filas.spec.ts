import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ListaSinFilasComponent } from './lista-sin-filas.component';

@Component({
  standalone: true,
  imports: [ListaSinFilasComponent],
  template: `
    <app-lista-sin-filas
      entidad="mercados musicales"
      [hayFiltros]="hayFiltros()"
      [ambito]="ambito()"
      comoLlegan="Los registra una organización desde su espacio."
      (limpiar)="limpiezas = limpiezas + 1">
      <button type="button" data-alta>Nuevo mercado</button>
    </app-lista-sin-filas>
  `,
})
class Anfitrion {
  readonly hayFiltros = signal(false);
  readonly ambito = signal<string | null>(null);
  limpiezas = 0;
}

describe('ListaSinFilasComponent', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let anfitrion: Anfitrion;

  const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const botonDeLimpiar = () =>
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="limpiar-filtros"]');

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('sin filtros dice que todavía no hay registros y ofrece el alta, no un botón de limpiar', () => {
    expect(texto()).toContain('Todavía no hay mercados musicales');
    expect(texto()).toContain('Los registra una organización desde su espacio.');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-alta]')).not.toBeNull();
    expect(botonDeLimpiar()).toBeNull();
  });

  it('con filtros dice que el filtro esconde el resto y ofrece quitarlo', () => {
    anfitrion.hayFiltros.set(true);
    fixture.detectChanges();

    expect(texto()).toContain('No hay mercados musicales que coincidan con estos filtros');
    expect(texto()).not.toContain('Todavía no hay mercados musicales');
    expect(botonDeLimpiar()).not.toBeNull();
  });

  it('el botón de limpiar avisa a quien la usa', () => {
    anfitrion.hayFiltros.set(true);
    fixture.detectChanges();

    botonDeLimpiar()!.click();

    expect(anfitrion.limpiezas).toBe(1);
  });

  it('el alta proyectada no se cuela en el caso filtrado: ahí la salida es quitar el filtro', () => {
    anfitrion.hayFiltros.set(true);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-alta]')).toBeNull();
  });

  it('una pestaña no se confunde con un filtro: nombra dónde no hay, sin decir que no hay ninguno', () => {
    anfitrion.ambito.set('en «Formación»');
    fixture.detectChanges();

    expect(texto()).toContain('No hay mercados musicales en «Formación»');
    expect(texto()).not.toContain('Todavía no hay mercados musicales');
    expect(botonDeLimpiar()).toBeNull();
  });

  it('el filtro manda sobre la pestaña: si hay filtros puestos, la salida es quitarlos', () => {
    anfitrion.ambito.set('en «Formación»');
    anfitrion.hayFiltros.set(true);
    fixture.detectChanges();

    expect(texto()).toContain('No hay mercados musicales que coincidan con estos filtros');
    expect(botonDeLimpiar()).not.toBeNull();
  });
});
