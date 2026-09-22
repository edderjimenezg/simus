import { TestBed } from '@angular/core/testing';
import { AdminRespaldosSitioComponent } from './admin-respaldos-sitio.component';
import { ContenidoWebApiService } from '../../../core/services/contenido-web-api.service';

/**
 * El área «Respaldos» de Gestión del sitio ().
 *
 * <b>De dónde viene.</b> Hasta esta versión, exportar e importar vivían dentro del editor de
 * textos, que era la única puerta al CMS. Al darle a Gestión del sitio sus seis áreas, dejar
 * los botones en las dos habría dado DOS caminos a la misma escritura, que es exactamente la
 * clase de duplicidad que este proyecto viene retirando.
 *
 * <b>Qué fijan estas pruebas.</b> El aviso de rescate del navegador —que convierte «averiguar
 * quién usó el panel y desde qué equipo» en algo que cada persona ve sola al entrar— se movió
 * con la lógica, y su cobertura tenía que moverse con él. Estas dos pruebas son las que estaban
 * en `admin-web-texts-panel.api.spec.ts` antes del traslado, reescritas contra el componente
 * nuevo: sin ellas, mover la funcionalidad habría significado perder la prueba en silencio.
 */
class ApiDoble {
  async getServerSnapshot() {
    return { ok: true, data: { textos: {}, equipo: null } };
  }
  async previewImport() {
    return { ok: true, data: null };
  }
  async applyImport() {
    return { ok: true, data: { cambios: 0 } };
  }
}

describe('AdminRespaldosSitioComponent', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  /**
   * El servicio de textos lee `localStorage` UNA SOLA VEZ, al construirse. Reconstruir el
   * TestBed es lo que equivale a recargar la página, que es cuando alguien vería el aviso
   * de verdad; poblar el almacén después de crear el componente no probaría nada.
   */
  async function montar() {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminRespaldosSitioComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: new ApiDoble() }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminRespaldosSitioComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('avisa cuando este navegador guarda contenido sin rescatar', async () => {
    localStorage.setItem('pnmc_web_texts', JSON.stringify({
      home_title: { content: 'Sin rescatar', status: 'borrador', updatedAt: '', updatedBy: '' },
      home_tag: { content: 'Tampoco', status: 'borrador', updatedAt: '', updatedBy: '' },
    }));

    const fixture = await montar();

    expect(fixture.componentInstance.pendienteLocal().textKeys).toBe(2);
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Este navegador conserva');
    expect(texto).toContain('Descargar rescate local');
  });

  it('dice que no hay nada pendiente si este navegador está limpio', async () => {
    const fixture = await montar();

    expect(fixture.componentInstance.pendienteLocal().textKeys).toBe(0);
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('no conserva contenido pendiente');
    // El botón de rescate NO se ofrece cuando no hay nada que rescatar: un botón que
    // descarga un archivo vacío se lee como un fallo de la herramienta.
    expect(texto).not.toContain('Descargar rescate local');
  });
});
