import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IndicadorDePasosComponent } from './indicador-de-pasos.component';

describe('IndicadorDePasosComponent', () => {
  let fixture: ComponentFixture<IndicadorDePasosComponent>;
  let componente: IndicadorDePasosComponent;

  const PASOS = [
    { id: 1, titulo: 'Uno' },
    { id: 2, titulo: 'Dos' },
    { id: 3, titulo: 'Tres' },
  ];

  function montar(pasoActivo: number): void {
    fixture = TestBed.createComponent(IndicadorDePasosComponent);
    componente = fixture.componentInstance;
    componente.pasos = PASOS;
    componente.pasoActivo = pasoActivo;
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('el paso activo lleva aria-current="step" y ningún otro', () => {
    montar(2);

    const botones = Array.from(raiz().querySelectorAll('button'));
    expect(botones.map(b => b.getAttribute('aria-current'))).toEqual([null, 'step', null]);
  });

  it('los pasos anteriores al activo se marcan completados -con su SVG de visto-, los siguientes no', () => {
    montar(2);

    expect(componente.estadoDelPaso(1)).toBe('completado');
    expect(componente.estadoDelPaso(2)).toBe('activo');
    expect(componente.estadoDelPaso(3)).toBe('pendiente');

    const botones = Array.from(raiz().querySelectorAll('button'));
    expect(botones[0].querySelector('svg')).withContext('el paso completado lleva el visto').not.toBeNull();
    expect(botones[1].querySelector('svg')).withContext('el paso activo muestra su número, no un visto').toBeNull();
    expect(botones[2].querySelector('svg')).withContext('el paso pendiente muestra su número, no un visto').toBeNull();
  });

  it('un clic en un paso emite su id', () => {
    montar(1);
    const emitidos: number[] = [];
    componente.irAlPaso.subscribe((id: number) => emitidos.push(id));

    raiz().querySelectorAll('button')[2].click();

    expect(emitidos).toEqual([3]);
  });
});
