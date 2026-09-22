import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AdminService } from '../../../core/services/admin.service';
import { BandejaDeTrabajoService } from '../admin-solicitudes-panel/bandeja-de-trabajo.service';
import { AdminShellPageComponent } from './admin-shell-page.component';

/**
 * EL ARMAZÓN LLEVA A LA BANDEJA; LA BANDEJA VIVE EN SU PANEL.
 *
 * Desde la bandeja de Solicitudes es `AdminSolicitudesPanelComponent`
 * y su cola, `BandejaDeTrabajoService`: sus pruebas están allí (`bandeja-de-trabajo.spec.ts`).
 * Lo que queda aquí es lo que solo el armazón puede hacer: llegar a la bandeja desde un aviso de
 * la campana o desde una cifra del Resumen operativo, ya filtrada y sobre el asunto concreto.
 *
 * Sin sesión a propósito: así el `effect()` que dispara `refreshAdminBackend()` nunca corre y la
 * prueba se queda con el control de la cola.
 */
describe('AdminShellPageComponent · llegar a la bandeja', () => {
  let fixture: ComponentFixture<AdminShellPageComponent>;
  let componente: AdminShellPageComponent;
  let bandeja: BandejaDeTrabajoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminShellPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    fixture = TestBed.createComponent(AdminShellPageComponent);
    componente = fixture.componentInstance;
    bandeja = TestBed.inject(BandejaDeTrabajoService);
    http = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    http.expectOne('/api/v1/admin/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  describe('avisos institucionales accionables', () => {
    it('un Festival recién recibido abre su solicitud de revisión concreta', () => {
      const adminService = TestBed.inject(AdminService);
      spyOn(adminService, 'marcarNotificacionComoLeida').and.returnValue(of({ readAt: '2026-09-09T18:00:00Z' } as any));
      const cargar = spyOn(bandeja, 'cargarRevisiones');
      const aviso = {
        id: '501', recipientUserId: '1', recipientEmail: 'admin@simus.local',
        eventType: 'FestivalRecibidoParaRevision', channel: 'internal',
        title: 'Festival recibido para revisión', body: 'Fundación Prueba envió Festival del Río.',
        status: 'enviada', moduleId: 'festivales', recordId: '77',
        createdAt: '2026-09-09T17:59:00Z', sentAt: null, readAt: null,
      };
      componente.adminNotifications.set([aviso]);
      componente.adminNotificationsOpen.set(true);

      componente.openNotification(aviso);

      expect(componente.activeSection()).toBe('solicitudes');
      // La bandeja vive en su propio panel: el armazón recarga las revisiones y le deja dicho qué
      // festival abrir; el panel lo elige en cuanto está en la cola.
      expect(cargar).toHaveBeenCalled();
      expect(bandeja.tramiteDeFestivalPedido()).toBe('77');
      expect(componente.adminNotificationsOpen()).toBe(false);
      expect(componente.adminNotifications()[0].readAt).toBe('2026-09-09T18:00:00Z');
    });

    it('un aviso informativo se lee sin fingir una solicitud inexistente', () => {
      const adminService = TestBed.inject(AdminService);
      spyOn(adminService, 'marcarNotificacionComoLeida').and.returnValue(of({ readAt: '2026-09-09T18:00:00Z' } as any));
      const cargar = spyOn(bandeja, 'cargarRevisiones');
      const aviso = {
        id: '502', recipientUserId: '1', recipientEmail: 'admin@simus.local',
        eventType: 'FestivalPublicado', channel: 'internal', title: 'Festival publicado',
        body: 'El Festival quedó publicado.', status: 'enviada', moduleId: 'festivales',
        recordId: '77', createdAt: '2026-09-09T17:59:00Z', sentAt: null, readAt: null,
      };

      componente.openNotification(aviso);

      expect(cargar).not.toHaveBeenCalled();
    });
  });

  describe('las cifras del Resumen operativo', () => {
    const duplicado = {
      id: '21', moduleId: 'festivals', sourceRecordId: '23', candidateRecordId: '24',
      similarityLevel: 'alta', similarityScore: 0.95, status: 'pendiente',
      createdAt: '2026-09-10T10:00:00Z',
    };

    it('la cifra de duplicados del Resumen cuenta los que ESPERAN, no los ya decididos', () => {
      // Decía 2 mientras la bandeja, al otro lado del mismo clic, decía 1: leía la lista entera
      // -decididos incluidos- y la bandeja solo los pendientes. Dos números para la misma
      // pregunta obligan a comprobar cuál miente.
      bandeja.duplicados.set([duplicado, { ...duplicado, id: '22', status: 'resuelto' }]);

      expect(bandeja.duplicadosEsperando()).toBe(1);
    });

    it('la cifra de duplicados del Resumen lleva a la bandeja YA filtrada, no a la cola entera', () => {
      componente.irALaBandeja('duplicado');

      expect(bandeja.filtro()).toBe('duplicado');
      expect(componente.activeSection()).toBe('solicitudes');
    });
  });
});
