import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminWebTextsPanelComponent } from './admin-web-texts-panel.component';
import { AdminGroup, ApiOutcome, ContenidoWebApiService, EntradaDeHistorialDeContenidoWeb } from '../../../core/services/contenido-web-api.service';
import { WEB_TEXT_KEY_INDEX } from '../../../core/services/textos-web.service';

/**
 * El corte de la Fase 3: el panel escribe en el servidor, no en el navegador.
 *
 * Estas pruebas fijan lo que no se puede volver a perder: que el guardado viaje
 * a la API, y que un fallo del servidor NUNCA se reporte como exito —que fue el
 * defecto original de este panel—.
 */
class ApiDoble {
  grupo: AdminGroup = {
    groupId: 'home_hero',
    groupLabel: 'Hero',
    section: 'Home',
    fields: [
      {
        key: 'home_title', label: 'Título', limit: 200, draft: 'Borrador del servidor',
        published: null, state: 'no_publicado', version: 7, updatedBy: 'Alguien', updatedAt: '',
      },
      // PUBLICADO EN BLANCO, Y NO ES UN CASO RARO: es lo que queda cuando alguien
      // borra un texto y lo publica. Distingue `??` de `||` en todo el panel, y
      // sin este campo en el doble los dos operadores se comportan igual aqui.
      {
        key: 'home_tag', label: 'Antetítulo', limit: 120, draft: '',
        published: '', state: 'publicado', version: 3, updatedBy: 'Alguien', updatedAt: '',
      },
      // Publicado CON texto: es el campo sobre el que se mide PNMC-040, porque
      // vaciarlo tiene que seguir contando como cambio pendiente.
      {
        key: 'home_description', label: 'Descripción', limit: 300, draft: 'Lo que está publicado',
        published: 'Lo que está publicado', state: 'publicado', version: 5, updatedBy: 'Alguien', updatedAt: '',
      },
    ],
  };

  guardados: { groupId: string; fields: { key: string; content: string }[]; publish: boolean }[] = [];
  respuestaGuardado: ApiOutcome<{ groupId: string; changed: number; published: boolean }> =
    { ok: true, data: { groupId: 'home_hero', changed: 1, published: false } };

  async getGroup(groupId: string): Promise<ApiOutcome<AdminGroup>> {
    return { ok: true, data: { ...this.grupo, groupId } };
  }

  async saveGroup(groupId: string, fields: { key: string; content: string }[], publish: boolean) {
    this.guardados.push({ groupId, fields, publish });
    return this.respuestaGuardado;
  }

  historial: Record<string, EntradaDeHistorialDeContenidoWeb[]> = {};

  async getGroupHistory(groupId: string): Promise<ApiOutcome<{ groupId: string; byKey: Record<string, EntradaDeHistorialDeContenidoWeb[]> }>> {
    return { ok: true, data: { groupId, byKey: this.historial } };
  }

  async getTeam(): Promise<ApiOutcome<never>> { return { ok: false, error: 'sin equipo en esta prueba' }; }
  async saveTeam(): Promise<ApiOutcome<never>> { return { ok: false, error: 'sin equipo en esta prueba' }; }
}

describe('AdminWebTextsPanelComponent · el panel escribe en el servidor', () => {
  let fixture: ComponentFixture<AdminWebTextsPanelComponent>;
  let component: AdminWebTextsPanelComponent;
  let api: ApiDoble;

  beforeEach(async () => {
    localStorage.clear();
    api = new ApiDoble();

    await TestBed.configureTestingModule({
      imports: [AdminWebTextsPanelComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebTextsPanelComponent);
    component = fixture.componentInstance;
    component._enabled.set(true);
    // EL MARCO SE APAGA EN LAS PRUEBAS. Carga una ruta de la aplicacion, y en
    // Karma esas rutas las sirve el propio Karma con su pagina de contexto: el
    // marco montaba la bateria de pruebas dentro de la bateria, en bucle, hasta
    // que Chrome se desconectaba.
    component.vistaEncendida.set(false);

    component.selectedGroup.set('home_hero');
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => localStorage.clear());

  it('carga el borrador del servidor, no el del navegador', async () => {
    localStorage.setItem('pnmc_web_texts', JSON.stringify({
      home_title: { content: 'Basura vieja del navegador', status: 'borrador', updatedAt: '', updatedBy: '' },
    }));
    await component.reloadCurrentGroup();

    expect(component.formData()['home_title']).toBe('Borrador del servidor');
  });

  it('envía el grupo a la API al guardar', async () => {
    component.handleInputChange('home_title', 'Texto nuevo');
    component.handleSaveAllGroup(false);
    await fixture.whenStable();

    expect(api.guardados.length).toBe(1);
    expect(api.guardados[0].groupId).toBe('home_hero');
    expect(api.guardados[0].publish).toBeFalse();
    expect(api.guardados[0].fields).toContain(
      jasmine.objectContaining({ key: 'home_title', content: 'Texto nuevo' }));
  });

  it('distingue guardar de publicar', async () => {
    component.handleSaveAllGroup(true);
    await fixture.whenStable();

    expect(api.guardados[0].publish).toBeTrue();
  });

  it('NO reporta éxito cuando el servidor rechaza el guardado', async () => {
    // El defecto original del panel: confirmaba un guardado que nunca ocurrio.
    api.respuestaGuardado = { ok: false, error: 'El servidor rechazó el guardado.' };

    component.handleSaveAllGroup(false);
    await fixture.whenStable();

    expect(component.saveStatus()?.type).toBe('error');
  });

  it('ante un conflicto dice que hay que recargar, no que se guardó', async () => {
    api.respuestaGuardado = { ok: false, conflict: true, error: 'Otra persona modificó este contenido.' };

    component.handleSaveAllGroup(false);
    await fixture.whenStable();

    expect(component.saveStatus()?.type).toBe('error');
    expect(component.saveStatus()?.message).toContain('Recargar del servidor');
  });

  it('nombra el campo que se pasa del límite y por cuánto', async () => {
    // Decir «uno o mas campos exceden el limite» obliga a buscarlos a mano entre
    // los 27 de un grupo, y en la revision del corte eso se repite 26 veces.
    // El limite sale del registro real, no de un numero escrito aqui: si alguien
    // lo cambia en el catalogo, esta prueba lo sigue en vez de mentir.
    const { limit: limite, label: etiqueta } = WEB_TEXT_KEY_INDEX.get('home_title')!;
    const exceso = 17;
    component.handleInputChange('home_title', 'x'.repeat(limite + exceso));
    component.handleSaveAllGroup(false);
    await fixture.whenStable();

    const mensaje = component.saveStatus()?.message ?? '';
    expect(component.saveStatus()?.type).toBe('error');
    expect(mensaje).toContain(etiqueta);
    expect(mensaje).toContain(`sobra ${exceso} caracteres`);
    expect(mensaje).toContain(`${limite + exceso}/${limite}`);
    // Y nada viajó al servidor.
    expect(api.guardados.length).toBe(0);
  });

  it('lo pendiente se mide contra lo PUBLICADO, no contra el borrador', () => {
    // La distincion entera de la pastilla. `home_title` llega del servidor con
    // borrador «Borrador del servidor» y publicado `null`: el visitante todavia
    // ve el texto de fabrica, asi que ese campo ESTA pendiente aunque la editora
    // no haya tocado nada en esta sesion.
    //
    // Comparando contra el borrador en vez de contra lo publicado, el campo
    // saldria igual a si mismo y la pastilla diria «Igual a lo que estás
    // editando» mientras el sitio ensena otra cosa.
    expect(component.formData()['home_title']).toBe('Borrador del servidor');
    expect(component.camposFueraDeLaVista()).toContain('home_title');
  });

  it('un texto publicado en blanco no cuenta como pendiente', () => {
    // La otra mitad, y la que separa `??` de `||`. `home_tag` llega con borrador
    // y publicado los dos vacios: no hay nada pendiente. Con `||`, el vacio
    // publicado caeria al valor de fabrica, el campo se leeria como distinto y
    // la pastilla avisaria de un cambio que nadie hizo.
    expect(component.formData()['home_tag']).toBe('');
    expect(component.camposFueraDeLaVista()).not.toContain('home_tag');
  });

  it('vaciar un campo cuenta como cambio pendiente (PNMC-040)', () => {
    // ESTA PRUEBA LEIA EL TEXTO DE LA PREVISUALIZACION, y ya no puede: la vista
    // es un <iframe> y su contenido no es texto del documento del panel. La
    // situacion que vigila es la misma y sigue siendo la mitad visible de
    // PNMC-040 —vaciar un campo tiene que verse como vacio, no volver al texto
    // compilado—, medida ahora sobre `camposFueraDeLaVista()`, que es lo que
    // decide el aviso ambar y lo que el marco recibe por mensaje.
    //
    // La otra mitad de PNMC-040, la del servicio, sigue intacta en
    // textos-web.service.spec.ts.
    // Se mide sobre `home_description`, que llega del servidor con texto
    // publicado: es la unica forma de que vaciarlo signifique algo. Con un campo
    // publicado en blanco, vaciarlo lo devuelve a su estado publicado y no habria
    // nada que afirmar.
    component.handleInputChange('home_description', 'Texto de prueba');
    fixture.detectChanges();
    expect(component.camposFueraDeLaVista()).toContain('home_description');

    // Y al vaciarlo sigue contando como pendiente: lo publicado NO esta vacio,
    // asi que el borrado esta sin publicar y el editor tiene que verlo.
    component.handleInputChange('home_description', '');
    fixture.detectChanges();
    expect(component.camposFueraDeLaVista()).toContain('home_description');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="aviso-borrador"]')).not.toBeNull();
  });

  it('muestra el historial que devuelve el servidor', async () => {
    api.historial = {
      home_title: [
        { action: 'retirado', value: null, user: 'Ana', at: '2026-08-21T10:00:00Z' },
        { action: 'publicado', value: 'Lo que se publicó', user: 'Ana', at: '2026-08-20T10:00:00Z' },
      ],
    };
    await component.reloadCurrentGroup();

    const historial = component.getHistory('home_title');
    expect(historial.length).toBe(2);
    // Del más reciente al más antiguo, sin invertir en la plantilla.
    expect(component.describeHistoryAction(historial[0])).toBe('Retirado del sitio');
    expect(component.describeHistoryAction(historial[1])).toBe('Publicado en el sitio');
    // Retirar no deja texto que restaurar.
    expect(historial[0].value).toBeNull();
  });

  // EL AVISO DE RESCATE DEL NAVEGADOR YA NO SE PRUEBA AQUI. Se mudo al area «Respaldos» de
  // Gestion del sitio en una revisión anterior, y con el sus dos pruebas: ver
  // `admin-gestion-sitio-panel/admin-respaldos-sitio.component.spec.ts`, que cubre el caso
  // con contenido sin rescatar y el caso del navegador limpio.

  it('avisa que un borrador todavía no se ve en el sitio', async () => {
    component.handleSaveAllGroup(false);
    await fixture.whenStable();

    expect(component.saveStatus()?.type).toBe('success');
    expect(component.saveStatus()?.message).toContain('no se ve en el sitio');
  });
});
