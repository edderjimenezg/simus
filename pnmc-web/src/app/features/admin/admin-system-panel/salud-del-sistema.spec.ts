import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminSystemPanelComponent } from './admin-system-panel.component';
// LA LOCALIZACION SE REGISTRA AQUI PORQUE ESTA PANTALLA FORMATEA CIFRAS Y FECHAS EN es-CO. El
// registro real es un efecto de módulo de `app.config.ts`, que una prueba aislada no carga: sin
// esto, la suite pasa entera y esta sola falla con «Missing locale data», que es la peor forma de
// fallar —depende de qué otras pruebas corrieron antes—.
import { appConfig } from '../../../app.config';

@Component({
  standalone: true,
  imports: [AdminSystemPanelComponent],
  template: `
    <app-admin-system-panel
      [schemaOnline]="enLinea()"
      [divipola]="divipola()"
      [monitor]="null" />
  `,
})
class Anfitrion {
  readonly enLinea = signal(true);
  readonly divipola = signal<Record<string, string[]>>({
    'HUILA': ['Neiva', 'Pitalito', 'Garzón', 'La Plata', 'Campoalegre', 'Timaná'],
    'AMAZONAS': ['Leticia', 'Puerto Nariño'],
    'BOYACÁ': ['Tunja', 'Duitama', 'Sogamoso'],
  });
}

/**
 * «Salud del sistema».
 *
 * <b>LO QUE FIJAN.</b> Que la cifra dice la verdad sobre el estado del servidor —«No conectado» no
 * es lo mismo que cero—, que el territorio se cuenta sumando y no suponiendo, y que la muestra de
 * municipios de una fila es <b>texto</b> y no una fila de píldoras: eran más de ciento cincuenta
 * cápsulas grises en una pantalla, y ninguna era un objeto ni una acción.
 */
describe('salud del sistema', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let componente: AdminSystemPanelComponent;

  beforeEach(async () => {
    expect(appConfig.providers.length).toBeGreaterThan(0);
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    fixture.detectChanges();
    componente = fixture.debugElement.children[0].componentInstance as AdminSystemPanelComponent;
  });

  it('cuenta el territorio cargado sumando los municipios de cada departamento', () => {
    expect(componente.departamentos().length).toBe(3);
    expect(componente.totalDeMunicipios()).toBe(11);
  });

  it('la cifra del esquema dice si responde, no un número', () => {
    const esquema = () => componente.cifrasDelSistema().find(c => c.id === 'esquema')!;
    expect(esquema().cifra).toBe('Disponible');
    expect(esquema().tono).toBe('correcto');

    fixture.componentInstance.enLinea.set(false);
    fixture.detectChanges();

    // MUTANTE QUE MATA: enseñar un 0. «No conectado» y «cero departamentos» son dos cosas
    // distintas, y confundirlas hace creer que la base está vacía cuando lo que pasa es que no
    // respondió.
    expect(esquema().cifra).toBe('No conectado');
    expect(esquema().tono).toBe('aviso');
  });

  it('buscar filtra sobre el total, no sobre lo que se ve', () => {
    componente.busqueda.set('ama');
    expect(componente.departamentosVisibles().map(d => d.nombre)).toEqual(['AMAZONAS']);

    componente.busqueda.set('');
    expect(componente.departamentosVisibles().length).toBe(3);
  });

  it('ordenar por cuántos municipios ordena por la cifra, no por el nombre', () => {
    componente.orden.alternar('cuantos');
    const ascendente = componente.departamentosVisibles().map(d => d.municipios.length);
    expect(ascendente).toEqual([...ascendente].sort((a, b) => a - b));

    componente.orden.alternar('cuantos');
    const descendente = componente.departamentosVisibles().map(d => d.municipios.length);
    expect(descendente).toEqual([...descendente].sort((a, b) => b - a));
  });

  it('la muestra de municipios es una frase, no una fila de píldoras', () => {
    const muestra = componente.muestraDeMunicipios(['NEIVA', 'PITALITO', 'GARZÓN', 'LA PLATA', 'CAMPOALEGRE', 'TIMANÁ']);

    // Cinco, separados por comas, y con el nombre escrito como se escribe un nombre propio.
    expect(muestra).toBe('Neiva, Pitalito, Garzón, La Plata, Campoalegre');
    expect(muestra).not.toContain('TIMANÁ');
  });
});
