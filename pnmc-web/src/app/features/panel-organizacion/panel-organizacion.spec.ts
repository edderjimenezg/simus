import { Component, computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { BehaviorSubject, of } from 'rxjs';
import { ExternalSessionService, SesionExterna } from '../../core/services/external-session.service';
import { NotificacionesEnVivoService, PaginaDeNotificacionesEnVivo } from '../../core/services/notificaciones-en-vivo.service';
import { ENLACES_DEL_PANEL, PanelOrganizacionPageComponent } from './panel-organizacion-page.component';
import { NotificacionDelPanel, PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

@Component({ standalone: true, template: 'Vista de prueba' })
class VistaDePruebaComponent {}

const SESION: SesionExterna = {
  userId: '9',
  fullName: 'Ana Restrepo',
  email: 'ana@fmv.org',
  accountStatus: 'activa',
  organizations: [
    { id: '117', name: 'Fundación Musical del Valle', role: 'administrador' },
    { id: '1117', name: 'Corporación Tambó', role: 'administrador' },
  ],
};

const RUTAS: Routes = [{
  path: 'gestion',
  component: PanelOrganizacionPageComponent,
  providers: [PanelOrganizacionStore],
  children: [
    { path: '', pathMatch: 'full', redirectTo: 'resumen' },
    ...ENLACES_DEL_PANEL.map(enlace => ({ path: enlace.ruta, component: VistaDePruebaComponent })),
  ],
}];

class SesionFalsa {
  readonly interna = signal<SesionExterna | null>(null);
  readonly actual = this.interna.asReadonly();
  readonly organizaciones = computed(() => this.interna()?.organizations ?? []);
  respuesta: SesionExterna | null = null;

  dentro(): boolean { return this.interna() !== null; }
  refrescar() { this.interna.set(this.respuesta); return of(this.respuesta); }
  salir() { this.interna.set(null); return of(null); }
}

class NotificacionesEnVivoFalsas {
  readonly pagina = new BehaviorSubject<PaginaDeNotificacionesEnVivo>({ items: [], limit: 50, offset: 0, total: 0 });
  observar() { return this.pagina.asObservable(); }
  actualizarAhora(): void { return undefined; }
}

describe('PanelOrganizacionPageComponent', () => {
  let sesion: SesionFalsa;
  let notificaciones: NotificacionDelPanel[];
  let marcarNotificacionLeida: jasmine.Spy;
  let harness: RouterTestingHarness;
  let notificacionesEnVivo: NotificacionesEnVivoFalsas;

  beforeEach(() => {
    sesion = new SesionFalsa();
    notificaciones = [];
    notificacionesEnVivo = new NotificacionesEnVivoFalsas();
    marcarNotificacionLeida = jasmine.createSpy('marcarNotificacionLeida').and.callFake((id: string) => of({
      ...notificaciones.find(item => item.id === id)!,
      readAt: '2026-09-09T12:30:00Z',
    }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter(RUTAS),
        { provide: ExternalSessionService, useValue: sesion },
        { provide: NotificacionesEnVivoService, useValue: notificacionesEnVivo },
        {
          provide: PanelOrganizacionApi,
          useValue: {
            obtenerPerfil: (id: string) => of({
              id,
              nombre: 'Fundación Musical del Valle',
              nivelCobertura: 'municipal',
              nombreMunicipio: 'Medellín',
              nombreDepartamento: 'Antioquia',
              nombreMunicipioSede: 'Bogotá, D. C.',
              nombreDepartamentoSede: 'Bogotá, D. C.',
              fechaActualizacion: '2026-09-09T00:00:00Z',
            }),
            obtenerNotificaciones: () => of({ items: notificaciones, limit: 50, offset: 0, total: notificaciones.length }),
            marcarNotificacionLeida,
            ocultarNotificacionesLeidas: () => of({ ocultadas: 0 }),
          },
        },
      ],
    });
  });

  async function montar(url = '/gestion/resumen'): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, PanelOrganizacionPageComponent);
    harness.detectChanges();
    return harness.routeNativeElement as HTMLElement;
  }

  it('sin sesión dirige al acceso vigente', async () => {
    sesion.respuesta = null;
    const navegar = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    const raiz = await montar();

    expect(navegar).toHaveBeenCalledWith(['/ingresar']);
    expect(raiz.querySelector('[aria-label="Secciones del panel de la organización"]')).toBeNull();
  });

  it('mantiene las secciones como pestañas enlazables, sin un contenedor desplazable', async () => {
    sesion.respuesta = SESION;
    const raiz = await montar();
    const navegacion = raiz.querySelector<HTMLElement>('[aria-label="Secciones del panel de la organización"]');

    expect(navegacion).not.toBeNull();
    expect(navegacion?.className).not.toContain('overflow');
    expect(navegacion?.querySelectorAll('a').length).toBe(ENLACES_DEL_PANEL.length);
    expect(Array.from(navegacion?.querySelectorAll('a') ?? []).map(enlace => enlace.textContent?.trim())).toEqual([
      // «EVENTOS» VA DESPUES DE «PROCESOS» Y NO ANTES, porque depende de ellos: un evento se
      // anuncia siempre dentro de un proceso, y sin ninguno publicado esa sección no ofrece el
      // formulario. El orden de la navegación cuenta esa dependencia.
      //
      // «MIS ARCHIVOS» VA DESPUES DE LOS DATOS Y ANTES DE LA CUENTA: es material de la
      // organización, no una preferencia de la cuenta.
      'Resumen', 'Procesos', 'Eventos', 'Solicitudes', 'Datos de la organización', 'Mis archivos',
      'Cuenta y seguridad',
    ]);
  });

  it('solo muestra el selector cuando la persona administra más de una organización', async () => {
    sesion.respuesta = SESION;
    const raiz = await montar();

    expect(raiz.querySelector<HTMLSelectElement>('#selector-organizacion')?.options.length).toBe(2);
  });

  it('nunca expone el código técnico de una cobertura pendiente en el encabezado', async () => {
    sesion.respuesta = SESION;
    const raiz = await montar();

    expect(raiz.textContent).not.toContain('sin_definir');
  });

  it('muestra identidad, ubicación real y actualización, sin estados o enlaces que no tienen función', async () => {
    sesion.respuesta = SESION;
    const raiz = await montar();

    expect(raiz.textContent).toContain('Fundación Musical del Valle');
    expect(raiz.textContent).toContain('Bogotá, D. C.');
    expect(raiz.textContent).not.toContain('Medellín, Antioquia');
    expect(raiz.textContent).not.toContain('Organización activa');
    expect(raiz.textContent).toContain('Última actualización:');
    expect(raiz.querySelector('[aria-label="Preparar edición de foto de perfil"]')).toBeNull();
  });

  it('lleva un aviso de ajustes a la ruta directa y conserva los avisos informativos sin enlace', async () => {
    sesion.respuesta = SESION;
    notificaciones = [
      { id: '1', recipientUserId: '9', recipientEmail: 'ana@fmv.org', eventType: 'FestivalCambiosPedidosPorCampo', channel: 'internal', title: 'Festival con ajustes solicitados', body: 'Revisa los ajustes.', status: 'enviada', moduleId: 'festivales', recordId: '92', createdAt: '2026-09-09T12:00:00Z', sentAt: null, readAt: null },
      { id: '2', recipientUserId: '9', recipientEmail: 'ana@fmv.org', eventType: 'FestivalEnviadoARevision', channel: 'internal', title: 'Festival enviado', body: 'La revisión comenzó.', status: 'enviada', moduleId: 'festivales', recordId: '92', createdAt: '2026-09-09T11:00:00Z', sentAt: null, readAt: null },
    ];
    notificacionesEnVivo.pagina.next({ items: notificaciones, limit: 50, offset: 0, total: 2 });
    const raiz = await montar();
    (raiz.querySelector('[data-testid="campana-avisos"]') as HTMLButtonElement).click();
    harness.detectChanges();

    const enlace = raiz.querySelector<HTMLAnchorElement>('a[href^="/gestion/procesos/festivales/92?"]');
    expect(enlace).not.toBeNull();
    expect(enlace?.textContent).toContain('Resolver ajustes');
    expect(enlace?.href).toContain('seccion=informacion');
    expect(enlace?.href).toContain('ajustes=1');
    expect(raiz.querySelectorAll('[data-testid="panel-avisos"] a').length).toBe(1);
  });

  it('incorpora un aviso nuevo mientras la página permanece abierta', async () => {
    sesion.respuesta = SESION;
    const raiz = await montar();
    expect(raiz.querySelector('[data-testid="campana-contador"]')).toBeNull();

    notificacionesEnVivo.pagina.next({
      items: [{ id: '3', recipientUserId: '9', recipientEmail: 'ana@fmv.org', eventType: 'FestivalCambiosPedidosPorCampo', channel: 'internal', title: 'Nuevas sugerencias', body: 'Revisa un campo.', status: 'enviada', moduleId: 'festivales', recordId: '92', createdAt: '2026-09-09T13:00:00Z', sentAt: null, readAt: null }],
      limit: 50, offset: 0, total: 1,
    });
    harness.detectChanges();

    expect(raiz.querySelector('[data-testid="campana-contador"]')?.textContent).toContain('1');
  });
});
