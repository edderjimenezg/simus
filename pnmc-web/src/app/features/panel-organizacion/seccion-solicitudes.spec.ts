import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { ApiClientService } from '../../core/http/api-client.service';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { PanelOrganizacionApi } from './panel-organizacion.api';
import { SeccionSolicitudesComponent } from './seccion-solicitudes.component';

describe('SeccionSolicitudesComponent', () => {
  let fixture: ComponentFixture<SeccionSolicitudesComponent>;
  let componente: SeccionSolicitudesComponent;
  let get: jasmine.Spy;

  const reclamacion = {
    id: '17', dominio: 'festival', registroCanonicoId: '92', registroNombre: 'Festival del Río',
    estado: 'requiere_aclaracion', justificacion: 'Administración histórica',
    fechaCreacion: '2026-09-09T12:00:00Z', motivoDecision: null,
  };

  function montar(
    filas: unknown[] = [],
    festivales: unknown[] = [],
    parametros: Record<string, string> = {},
  ): void {
    get = jasmine.createSpy('get').and.returnValue(of(filas));
    const parametrosDeConsulta = new BehaviorSubject(convertToParamMap(parametros));
    TestBed.configureTestingModule({
      imports: [SeccionSolicitudesComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionStore, useValue: { organizacionId: signal('117') } },
        { provide: ApiClientService, useValue: { get, post: jasmine.createSpy('post') } },
        { provide: PanelOrganizacionApi, useValue: { obtenerFestivales: () => of(festivales) } },
        { provide: ActivatedRoute, useValue: { queryParamMap: parametrosDeConsulta } },
      ],
    });
    fixture = TestBed.createComponent(SeccionSolicitudesComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('separa las acciones de revisión de los trámites administrativos y no carga notificaciones', () => {
    montar([]);

    expect(get).toHaveBeenCalledWith('/api/v1/externo/organizaciones/117/reclamaciones-administracion', jasmine.any(Object));
    expect(fixture.nativeElement.textContent).toContain('No tienes solicitudes pendientes');
    expect(fixture.nativeElement.textContent).not.toContain('Notificaciones');
    expect(fixture.nativeElement.textContent).not.toContain('enviado a revisión');
  });

  it('incluye los ajustes sugeridos como acciones pendientes con salida directa a la ficha', () => {
    montar([], [{ id: '92', nombre: 'Festival del Río', estado: 'AjustesSolicitados', cambiosPedidos: 2 }]);

    expect(componente.ajustesPendientes().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Ajuste sugerido');
    expect(fixture.nativeElement.textContent).toContain('Tienes 2 ajustes por atender.');
    expect(fixture.nativeElement.textContent).toContain('Resolver ajustes');

    const navegar = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    const boton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(item => item.textContent?.includes('Resolver ajustes'))!;
    boton.click();
    expect(navegar).toHaveBeenCalledWith(['/gestion/procesos/festivales', '92'], {
      queryParams: { seccion: 'informacion', ajustes: '1' },
    });
  });

  it('muestra una aclaración requerida como acción administrativa y enlaza a su Festival', () => {
    montar([reclamacion]);

    expect(fixture.nativeElement.textContent).toContain('Festival del Río');
    expect(fixture.nativeElement.textContent).toContain('Requiere aclaración');
    expect(fixture.nativeElement.textContent).toContain('Responder aclaración');
    expect(fixture.nativeElement.querySelector('a[href="/gestion/procesos/festivales/92"]')).not.toBeNull();
  });

  it('abre la respuesta junto a la solicitud, sin llevarla a otra pantalla', () => {
    montar([reclamacion]);
    const botones = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    const boton = botones.find(item => item.textContent?.includes('Responder aclaración'))!;
    boton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('textarea')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Aclaración para la institución');
  });

  it('un aviso de aclaración abre directamente el trámite señalado por su URL', () => {
    montar([reclamacion], [], { solicitud: '17', accion: 'aclarar' });

    expect(componente.aclaracionAbierta()).toBe('17');
    expect(fixture.nativeElement.querySelector('textarea')).not.toBeNull();
  });

  it('distingue el historial cerrado de los trámites en curso', () => {
    montar([{ ...reclamacion, id: '18', estado: 'rechazada', motivoDecision: 'No corresponde.' }]);

    expect(componente.activas()).toEqual([]);
    expect(componente.cerradas().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Historial');
  });
});
