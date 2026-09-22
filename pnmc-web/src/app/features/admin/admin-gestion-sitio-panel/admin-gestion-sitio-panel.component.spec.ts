import { appConfig } from '../../../app.config';
import { TestBed } from '@angular/core/testing';
import { AdminGestionSitioPanelComponent } from './admin-gestion-sitio-panel.component';
import { ContenidoWebApiService } from '../../../core/services/contenido-web-api.service';

/**
 * El armazón de Gestión del sitio ().
 *
 * <b>El defecto que estas pruebas fijan, y que era real.</b> El efecto de arranque llamaba a
 * `cargarResumen()` desde su propio cuerpo. Esa función empieza SÍNCRONAMENTE leyendo y
 * escribiendo `cargando`, así que el efecto acababa suscrito a una señal que él mismo cambia:
 * pedía el resumen, `cargando` cambiaba, el cambio volvía a disparar el efecto, y así sin
 * final. En pantalla se veía como un «Cargando el estado del sitio…» permanente mientras el
 * API recibía peticiones sin parar.
 *
 * Por eso la primera prueba cuenta LLAMADAS y no mira la pantalla: el síntoma visible era un
 * texto de carga, pero el defecto es el número de peticiones, y es lo único que al medirlo
 * distingue «tarda» de «no para nunca».
 */
class ApiDoble {
  llamadas = 0;
  respuesta: { ok: boolean; data?: unknown; error?: string } = {
    ok: true,
    data: {
      generatedAt: '2026-09-11T00:00:00Z',
      pendingTotal: 0,
      texts: { total: 26, published: 26, pending: 0, retired: 0, groups: [] },
      images: { total: 13, published: 13, pending: 0, retired: 0, groups: [] },
      team: { members: 4, published: true, pending: false, updatedAt: null },
    },
  };

  async getSiteManagementSummary() {
    this.llamadas += 1;
    return this.respuesta;
  }
}

describe('AdminGestionSitioPanelComponent', () => {
  // LAS CIFRAS Y FECHAS SE ESCRIBEN EN es-CO, y el locale lo registra `app.config.ts` al cargarse.
  // Nombrar `appConfig` obliga a cargar ese módulo; sin esto la prueba solo pasa cuando corre junto
  // a otra que lo cargue, que es una dependencia del orden de ejecución y no de lo que se prueba.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  let api: ApiDoble;

  async function montar() {
    api = new ApiDoble();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminGestionSitioPanelComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: api }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminGestionSitioPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('pide el resumen UNA sola vez al abrirse, no en bucle', async () => {
    await montar();

    expect(api.llamadas).toBe(1);
  });

  it('deja de anunciar carga cuando el resumen llega', async () => {
    const fixture = await montar();

    expect(fixture.componentInstance.cargando()).toBeFalse();
    expect(fixture.componentInstance.resumen()).not.toBeNull();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).not.toContain('Cargando el estado del sitio');
    expect(texto).toContain('Circuito de publicación');
  });

  it('muestra las seis áreas previstas de una revisión anterior', async () => {
    const fixture = await montar();

    const areas = fixture.componentInstance.opciones.map(o => o.etiqueta);
    expect(areas).toEqual([
      'Resumen',
      'Textos y páginas',
      'Imágenes del sitio',
      'Equipo',
      'Cambios pendientes',
      'Respaldos',
    ]);
  });

  it('si el servidor falla lo dice y ofrece reintentar, sin quedarse cargando', async () => {
    api = new ApiDoble();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminGestionSitioPanelComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: api }],
    }).compileComponents();
    api.respuesta = { ok: false, error: 'No fue posible leer el estado.' };

    const fixture = TestBed.createComponent(AdminGestionSitioPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.cargando()).toBeFalse();
    expect(fixture.componentInstance.error()).toBe('No fue posible leer el estado.');
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Reintentar');
  });

  it('cambiar de área no dispara una carga por cada clic en las que no la necesitan', async () => {
    const fixture = await montar();

    // Textos, imágenes y respaldos se sostienen solos: no dependen del resumen.
    await fixture.componentInstance.seleccionar('textos');
    await fixture.componentInstance.seleccionar('imagenes');
    await fixture.componentInstance.seleccionar('respaldos');

    expect(api.llamadas).toBe(1);

    // «Cambios pendientes» sí relee, porque su contenido ES el resumen.
    await fixture.componentInstance.seleccionar('pendientes');
    expect(api.llamadas).toBe(2);
  });
});
