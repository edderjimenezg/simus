import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ProcesosGestionPageComponent } from './procesos-gestion-page.component';
import { PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

describe('ProcesosGestionPageComponent', () => {
  let fixture: ComponentFixture<ProcesosGestionPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ProcesosGestionPageComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionStore, useValue: { organizacionId: signal('117') } },
        { provide: PanelOrganizacionApi, useValue: { obtenerFestivales: () => of([]), obtenerMercados: () => of([]) } },
      ],
    });
    fixture = TestBed.createComponent(ProcesosGestionPageComponent);
    fixture.detectChanges();
  });

  it('describe Festival sin etiquetas temporales de disponibilidad', () => {
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('información permanente del Festival');
    expect(texto).not.toContain('Disponible ahora');
    expect(texto).not.toContain('Disponible para registrar ahora');
    expect(texto).not.toContain('Aún no disponibles');
  });

  it('Mercado musical ya es un tipo real y no una opción apagada', () => {
    // ESTA PRUEBA ESPERABA CINCO OPCIONES DESHABILITADAS, y tenía razón hasta el 15 de septiembre
    // de 2026: ese día Mercados Musicales dejó de ser un proceso proyectado y pasó a registrarse de
    // verdad, con su asistente, su ficha y su circuito de revisión. Quedan cuatro apagados.
    fixture.componentInstance.opcionesAbiertas.set(true);
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('[data-menu-registrar-mercado]')).not.toBeNull();
    expect(raiz.querySelectorAll('button[role="menuitem"]:disabled').length).toBe(4);
    expect(raiz.textContent).not.toContain('Próximamente');
    expect(raiz.textContent).not.toContain('en construcción');
  });

  it('la lista vacía ofrece los dos tipos que ya se pueden registrar', () => {
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-registrar-mercado]')).not.toBeNull();
    expect(raiz.textContent).toContain('Mercado musical');
  });
});
