import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { SeccionEcosistemaComponent } from './seccion-ecosistema.component';

describe('SeccionEcosistemaComponent', () => {
  let fixture: ComponentFixture<SeccionEcosistemaComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeccionEcosistemaComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: { obtenerFestivales: () => of([]) } },
        { provide: PanelOrganizacionStore, useValue: { organizacionId: () => 'organizacion-1' } },
        provideRouter([]),
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SeccionEcosistemaComponent);
    fixture.detectChanges();
  });

  it('muestra solo la gestión de Festivales y enlaza al catálogo de procesos', () => {
    const contenido = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(contenido).toContain('Festivales');
    expect(contenido).toContain('Consulta los procesos disponibles');
    expect(contenido).not.toContain('Otros tipos de registro');
  });
});
