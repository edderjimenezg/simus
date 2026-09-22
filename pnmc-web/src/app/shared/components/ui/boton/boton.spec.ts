import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BotonComponent } from './boton.component';

@Component({
  standalone: true,
  imports: [BotonComponent],
  template: `
    <app-boton
      [importancia]="importancia()"
      [tamano]="tamano()"
      [ocupado]="ocupado()"
      [deshabilitado]="deshabilitado()"
      [etiqueta]="etiqueta()"
      (accion)="veces = veces + 1">Guardar</app-boton>
  `,
})
class Anfitrion {
  readonly importancia = signal<'principal' | 'secundaria' | 'destructiva'>('principal');
  readonly tamano = signal<'menudo' | 'normal' | 'comodo'>('normal');
  readonly ocupado = signal(false);
  readonly deshabilitado = signal(false);
  readonly etiqueta = signal<string | null>(null);
  veces = 0;
}

describe('BotonComponent', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let anfitrion: Anfitrion;
  const boton = () => fixture.nativeElement.querySelector('button') as HTMLButtonElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('es de tipo button, para no enviar el formulario que lo contiene', () => {
    // Sin type explícito, un <button> dentro de un <form> lo envía. El trinquete
    // `botones_de_accion_sin_tipo` está en 0 y esto lo mantiene sin vigilancia manual.
    expect(boton().getAttribute('type')).toBe('button');
  });

  it('los tres tamaños declaran un alto mínimo: WCAG 2.2 AA pide 24x24', () => {
    for (const tamano of ['menudo', 'normal', 'comodo'] as const) {
      anfitrion.tamano.set(tamano);
      fixture.detectChanges();
      expect(boton().className).toContain('min-h-control');
    }
  });

  it('mientras está ocupado no vuelve a emitir: el doble clic no crea dos registros', () => {
    boton().click();
    expect(anfitrion.veces).toBe(1);

    anfitrion.ocupado.set(true);
    fixture.detectChanges();

    // Se pulsa el método directamente además del clic: `disabled` se puede quitar desde fuera, y
    // entre que la petición sale y el estado cambia hay una ventana donde el segundo clic entra.
    boton().click();
    fixture.debugElement.children[0].componentInstance.pulsar();
    expect(anfitrion.veces).toBe(1);
  });

  it('anuncia que está ocupado para quien usa un lector de pantalla', () => {
    anfitrion.ocupado.set(true);
    fixture.detectChanges();
    expect(boton().getAttribute('aria-busy')).toBe('true');
  });

  it('deshabilitado no emite', () => {
    anfitrion.deshabilitado.set(true);
    fixture.detectChanges();
    boton().click();
    expect(anfitrion.veces).toBe(0);
  });

  it('la etiqueta da nombre accesible al botón que solo lleva icono', () => {
    anfitrion.etiqueta.set('Eliminar la ficha');
    fixture.detectChanges();
    expect(boton().getAttribute('aria-label')).toBe('Eliminar la ficha');
  });

  it('cada importancia produce una forma distinta, y la destructiva se distingue', () => {
    const formas = new Set<string>();
    for (const importancia of ['principal', 'secundaria', 'destructiva'] as const) {
      anfitrion.importancia.set(importancia);
      fixture.detectChanges();
      formas.add(boton().className);
    }
    expect(formas.size).toBe(3);

    anfitrion.importancia.set('destructiva');
    fixture.detectChanges();
    // No basta con que sea distinta: tiene que leerse como destructiva.
    expect(boton().className).toContain('red');
  });

  it('repone un foco visible, que fue justo uno de los hallazgos de la auditoría', () => {
    expect(boton().className).toContain('focus-visible:ring');
  });
});
