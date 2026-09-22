import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { PanelResumenComponent } from './panel-resumen.component';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import {
  CatalogosDelFestival,
  FestivalDeLaOrganizacion,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  ResponsableOrganizacion,
  TipoDocumento,
  UbicacionDivipola,
} from './panel-organizacion.api';

/**
 * La ruta «Resumen», en /ecosistema/mi-panel/resumen: la vista de aterrizaje del panel.
 *
 * NUEVA EN EL REDISEÑO DEL 1 DE SEPTIEMBRE DE 2026. Pide su propio perfil, Festivales y
 * notificaciones -no hay estado compartido de eso en {@link PanelOrganizacionStore}, solo qué
 * organización está abierta- así que el doble del API cubre esas tres llamadas y lanza en las
 * demás, con el mismo criterio que el resto de secciones del panel.
 */
const PERFIL: PerfilOrganizacion = {
  id: '117', nombre: 'Fundación Musical del Valle', nombreLegal: null, numeroIdentificacion: '900123456',
  tipoIdentificacion: 'NIT', descripcion: null, correoContacto: 'contacto@fmv.org', telefonoContacto: null,
  sitioWeb: null, facebook: null, instagram: null, otroEnlace: null, direccion: null,
  codigoDepartamentoSede: '05', nombreDepartamentoSede: 'Antioquia',
  codigoMunicipioSede: '05001', nombreMunicipioSede: 'Medellín', estadoRegistro: 'activa',
  estadoRegistroEtiqueta: 'Registrada', fechaActualizacion: null,
};

class ApiFalso extends PanelOrganizacionApi {
  perfil: PerfilOrganizacion = PERFIL;
  festivales: FestivalDeLaOrganizacion[] = [];

  override obtenerPerfil(): Observable<PerfilOrganizacion> { return of(this.perfil); }
  override obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> { return of(this.festivales); }
  override obtenerNotificaciones(): Observable<PaginaDeNotificaciones> { throw new Error('Los avisos viven en la campana.'); }

  override guardarPerfil(): Observable<PerfilOrganizacion> { throw new Error('Resumen no escribe el perfil.'); }
  override obtenerResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override guardarResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override obtenerTiposDocumento(): Observable<TipoDocumento[]> { throw new Error('Los tipos de documento son de otra ruta.'); }
  override enviarFestivalARevision(): Observable<FestivalDeLaOrganizacion> { throw new Error('Enviar a revisión es de otra ruta.'); }
  override enviarPropuestaARevision(): Observable<unknown> { throw new Error('Enviar la propuesta es de otra ruta.'); }
  override iniciarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override obtenerPropuestaActiva(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override guardarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override marcarNotificacionLeida(): Observable<NotificacionDelPanel> { throw new Error('Marcar como leída es de Solicitudes.'); }
  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> { throw new Error('Los catálogos del Festival son de Ecosistema.'); }
  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> { throw new Error('DIVIPOLA es de Organización.'); }
  override crearFestival(): Observable<FestivalDeLaOrganizacion> { throw new Error('El alta de Festival es de Ecosistema.'); }
  override guardarFestival(): Observable<FestivalDeLaOrganizacion> { throw new Error('El alta de Festival es de Ecosistema.'); }
}

describe('PanelResumenComponent', () => {
  let fixture: ComponentFixture<PanelResumenComponent>;
  let componente: PanelResumenComponent;
  let api: ApiFalso;

  function montar(): void {
    TestBed.configureTestingModule({
      imports: [PanelResumenComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: PanelOrganizacionStore, useValue: { organizacionId: signal('117') } },
      ],
    });

    fixture = TestBed.createComponent(PanelResumenComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    api = new ApiFalso();
  });

  it('cuenta los Festivales registrados', () => {
    api.festivales = [
      { id: '1', nombre: 'A', estado: 'Publicado' },
      { id: '2', nombre: 'B', estado: 'Borrador' },
    ];
    montar();

    expect(componente.festivales().length).toBe(2);
  });

  it('separa «en revisión» -espera al PNMC- de «requieren tu atención» -espera a la organización-', () => {
    api.festivales = [
      { id: '1', nombre: 'A', estado: 'EnRevision' },
      { id: '2', nombre: 'B', estado: 'AjustesSolicitados' },
      { id: '3', nombre: 'C', estado: 'Publicado' },
      { id: '4', nombre: 'D', estado: 'Rechazado' },
      { id: '5', nombre: 'E', estado: 'Borrador' },
    ];
    montar();

    // MUTANTE QUE MATA: contar «Borrador» o «Publicado» en cualquiera de los dos. Un borrador
    // todavía no se ha enviado a nadie, y un publicado ya está decidido: ninguno de los dos espera
    // nada, ni del PNMC ni de la organización.
    expect(componente.enRevision()).toBe(1);
    expect(componente.requierenAtencion()).toBe(1);
    expect(componente.borradoresSinEnviar()).toBe(1);
  });

  it('no repite la identidad de la organización -nombre, estado, cobertura, fecha- que ya está en la columna derecha del panel', () => {
    montar();

    // Esta vista es solo cifras de gestión desde; la identidad vive en
    // panel-organizacion-page.component.html, visible en cualquier pestaña, no solo en Resumen.
    expect(raiz().textContent).not.toContain('Fundación Musical del Valle');
  });

  it('un borrador sin enviar ofrece continuar directamente el registro', () => {
    api.festivales = [{ id: '5', nombre: 'E', estado: 'Borrador' }];
    montar();

    expect(raiz().textContent).toContain('Está en borrador y aún no se ha enviado a revisión');
    const enlace = raiz().querySelector('a[href="/gestion/procesos/festivales/5"]');
    expect(enlace?.textContent?.trim()).toBe('Continuar registro');
  });

  it('sin borradores sin enviar no se pinta el aviso', () => {
    api.festivales = [{ id: '1', nombre: 'A', estado: 'Publicado' }];
    montar();

    expect(raiz().textContent).not.toContain('en borrador');
  });

  it('muestra los procesos con ajustes como la prioridad y conduce a resolverlos', () => {
    api.festivales = [{ id: 'ajustes', nombre: 'Festival con ajustes', estado: 'AjustesSolicitados' }];
    montar();

    const enlace = raiz().querySelector<HTMLAnchorElement>('a[href^="/gestion/procesos/festivales/ajustes?"]');
    expect(enlace?.textContent?.trim()).toBe('Resolver ajustes');
    expect(enlace?.href).toContain('seccion=informacion');
    expect(enlace?.href).toContain('ajustes=1');
    expect(raiz().textContent).toContain('Tiene ajustes sugeridos por atender');
  });

  it('no duplica los avisos de la campana dentro del resumen', () => {
    montar();

    expect(raiz().textContent).not.toContain('Actividad reciente');
    expect(raiz().textContent).not.toContain('Avisos sin leer');
  });
});
