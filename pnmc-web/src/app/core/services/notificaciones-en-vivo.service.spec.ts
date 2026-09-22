import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  INTERVALO_NOTIFICACIONES_MS,
  NotificacionEnVivo,
  NotificacionesEnVivoService,
  destinoExternoDe,
} from './notificaciones-en-vivo.service';

const AJUSTE: NotificacionEnVivo = {
  id: '1', recipientUserId: '9', recipientEmail: 'persona@example.org',
  eventType: 'FestivalCambiosPedidosPorCampo', channel: 'internal',
  title: 'Sugerencias de ajuste', body: 'Revisa tres campos.', status: 'enviada',
  moduleId: 'festivales', recordId: '92', createdAt: '2026-09-09T12:00:00Z',
  sentAt: '2026-09-09T12:00:00Z', readAt: null,
};

describe('NotificacionesEnVivoService', () => {
  let servicio: NotificacionesEnVivoService;
  let http: HttpTestingController;

  beforeEach(() => {
    spyOnProperty(document, 'hidden', 'get').and.returnValue(false);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    servicio = TestBed.inject(NotificacionesEnVivoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('consulta al entrar y vuelve a consultar sin recargar la página', fakeAsync(() => {
    const recibidas: string[][] = [];
    const suscripcion = servicio.observar('externo').subscribe(pagina => recibidas.push(pagina.items.map(item => item.id)));

    tick();
    http.expectOne('/api/v1/notificaciones?ambito=externo&limite=50').flush({ items: [], limit: 50, offset: 0, total: 0 });
    tick(INTERVALO_NOTIFICACIONES_MS);
    http.expectOne('/api/v1/notificaciones?ambito=externo&limite=50').flush({ items: [AJUSTE], limit: 50, offset: 0, total: 1 });

    expect(recibidas).toEqual([[], ['1']]);
    suscripcion.unsubscribe();
  }));

  it('no solapa lecturas lentas y conserva la última lista ante un fallo transitorio', fakeAsync(() => {
    const recibidas: string[][] = [];
    const suscripcion = servicio.observar('institucional').subscribe(pagina => recibidas.push(pagina.items.map(item => item.id)));
    tick();
    const primera = http.expectOne('/api/v1/notificaciones?ambito=institucional&limite=50');

    tick(INTERVALO_NOTIFICACIONES_MS * 2);
    http.expectNone('/api/v1/notificaciones?ambito=institucional&limite=50');
    primera.flush({ items: [AJUSTE], limit: 50, offset: 0, total: 1 });
    tick(INTERVALO_NOTIFICACIONES_MS);
    http.expectOne('/api/v1/notificaciones?ambito=institucional&limite=50').flush({}, { status: 503, statusText: 'Unavailable' });

    expect(recibidas).toEqual([['1']]);
    suscripcion.unsubscribe();
  }));

  it('actualiza inmediatamente cuando se solicita o la ventana recupera el foco', fakeAsync(() => {
    let emisiones = 0;
    const suscripcion = servicio.observar('externo').subscribe(() => emisiones++);
    tick();
    http.expectOne('/api/v1/notificaciones?ambito=externo&limite=50').flush({ items: [], limit: 50, offset: 0, total: 0 });

    servicio.actualizarAhora();
    http.expectOne('/api/v1/notificaciones?ambito=externo&limite=50').flush({ items: [], limit: 50, offset: 0, total: 0 });
    window.dispatchEvent(new Event('focus'));
    http.expectOne('/api/v1/notificaciones?ambito=externo&limite=50').flush({ items: [], limit: 50, offset: 0, total: 0 });

    expect(emisiones).toBe(1);
    suscripcion.unsubscribe();
  }));
});

describe('destinoExternoDe', () => {
  it('lleva la sugerencia por campos al inspector del Festival', () => {
    expect(destinoExternoDe(AJUSTE)).toEqual({
      ruta: ['/gestion/procesos/festivales', '92'],
      parametros: { seccion: 'informacion', ajustes: '1' },
      accion: 'Resolver ajustes',
    });
  });

  it('lleva una aclaración y una conciliación a la solicitud concreta', () => {
    const aclarar = destinoExternoDe({ ...AJUSTE, eventType: 'ReclamacionRequiereAclaracion', recordId: '17' });
    const conciliar = destinoExternoDe({ ...AJUSTE, eventType: 'ReclamacionAprobadaPendienteConciliacion', recordId: '17' });

    expect(aclarar?.parametros).toEqual({ solicitud: '17', accion: 'aclarar' });
    expect(conciliar?.parametros).toEqual({ solicitud: '17', accion: 'conciliar' });
  });

  it('no convierte un aviso informativo en una acción', () => {
    expect(destinoExternoDe({ ...AJUSTE, eventType: 'FestivalPublicado' })).toBeNull();
  });
});
