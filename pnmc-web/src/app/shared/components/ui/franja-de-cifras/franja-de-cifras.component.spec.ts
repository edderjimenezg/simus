import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { appConfig } from '../../../../app.config';
import { CifraDeTablero, FranjaDeCifrasComponent } from './franja-de-cifras.component';

@Component({
  standalone: true,
  imports: [FranjaDeCifrasComponent],
  template: `<app-franja-de-cifras [cifras]="cifras" [conFilete]="false" (elegir)="elegida = $event" />`,
})
class AnfitrionComponent {
  cifras: CifraDeTablero[] = [];
  elegida = '';
}

/**
 * La franja de cifras de un tablero: una pieza, no tres dibujos.
 *
 * Lo que se pinza aquí es lo que las tres pantallas hacían distinto: cómo se escribe un número,
 * qué pasa con un cero y con un dato que no existe, y que una cifra accionable sea un botón.
 */
describe('FranjaDeCifrasComponent', () => {
  // LAS CIFRAS SE ESCRIBEN EN es-CO, y el locale lo registra `app.config.ts` al cargarse. Nombrar
  // `appConfig` obliga a cargar ese módulo; sin esto la prueba solo pasa cuando corre junto a
  // otra que lo cargue, que es una dependencia del orden de ejecución y no de lo que se prueba.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  let fixture: ComponentFixture<AnfitrionComponent>;
  let anfitrion: AnfitrionComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [AnfitrionComponent] });
    fixture = TestBed.createComponent(AnfitrionComponent);
    anfitrion = fixture.componentInstance;
  });

  const cifra = (id: string) => fixture.nativeElement.querySelector(`[data-cifra="${id}"] .franja-de-cifras__cifra`) as HTMLElement;

  it('escribe los números con separador de miles, apaga el cero y pone raya donde no hay dato', () => {
    anfitrion.cifras = [
      { id: 'grande', cifra: 12345, rotulo: 'Registros' },
      { id: 'cero', cifra: 0, rotulo: 'Duplicados' },
      { id: 'nada', cifra: null, rotulo: 'Usuarios' },
    ];
    fixture.detectChanges();

    expect(cifra('grande').textContent?.trim()).toBe('12.345');
    expect(cifra('cero').classList).toContain('es-cero');
    expect(cifra('nada').textContent?.trim()).toBe('—');
  });

  it('un texto se dibuja más pequeño y con su tono, no como un número', () => {
    anfitrion.cifras = [{ id: 'api', cifra: 'Disponible', rotulo: 'Esquema', tono: 'correcto', detalle: 'El servidor responde' }];
    fixture.detectChanges();

    expect(cifra('api').classList).toContain('es-texto');
    expect(cifra('api').classList).toContain('es-correcto');
    expect(fixture.nativeElement.querySelector('.franja-de-cifras__detalle').textContent).toContain('El servidor responde');
  });

  it('una cifra accionable es un botón y emite su id; una que no lo es, no', () => {
    anfitrion.cifras = [
      { id: 'organizaciones', cifra: 45, rotulo: 'Organizaciones', accionable: true },
      { id: 'quieta', cifra: 3, rotulo: 'Quieta' },
    ];
    fixture.detectChanges();

    const boton = fixture.nativeElement.querySelector('[data-cifra="organizaciones"]') as HTMLElement;
    expect(boton.tagName).toBe('BUTTON');
    expect(fixture.nativeElement.querySelector('[data-cifra="quieta"]').tagName).toBe('DIV');
    boton.click();
    expect(anfitrion.elegida).toBe('organizaciones');
  });
});
