import { TestBed } from '@angular/core/testing';
import { TextosWebService, WEB_CONTENT_BACKUP_VERSION, DEFAULT_TEXTS } from './textos-web.service';
import { ContenidoWebApiService, PublicWebContent, PublicWebImages, PublicWebTeam } from './contenido-web-api.service';

/**
 * Doble del servicio de API. La Fase 3 movio la fuente del sitio publico a la
 * red, asi que estas pruebas fijan como se comporta el servicio ante lo que el
 * servidor conteste —incluido que no conteste—.
 */
class ApiDoble {
  textos: PublicWebContent | null = null;
  equipo: PublicWebTeam | null = null;
  llamadas = 0;

  async getPublicTexts(): Promise<PublicWebContent | null> {
    this.llamadas++;
    return this.textos;
  }

  async getPublicTeam(): Promise<PublicWebTeam | null> {
    return this.equipo;
  }

  /**
   * El manifiesto de imagenes. Se anadio, cuando
   * `loadPublishedContent` paso a pedir tres cosas en vez de dos.
   *
   * SIN ESTE METODO, LAS ONCE PRUEBAS DE ESTE FICHERO MUEREN A LA VEZ con
   * «getPublicImages is not a function», y ninguna por su propio motivo. Un
   * doble que no sigue la superficie del servicio real deja de medir el servicio
   * y pasa a medir el doble.
   */
  imagenes: PublicWebImages | null = null;

  async getPublicImages(): Promise<PublicWebImages | null> {
    return this.imagenes;
  }
}

function crear(api: ApiDoble): TextosWebService {
  TestBed.configureTestingModule({
    providers: [
      TextosWebService,
      { provide: ContenidoWebApiService, useValue: api },
    ],
  });
  return TestBed.inject(TextosWebService);
}

describe('TextosWebService · el sitio publico lee de la API', () => {
  let api: ApiDoble;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    api = new ApiDoble();
  });

  it('sirve el texto publicado por el servidor', async () => {
    api.textos = { texts: { home_title: 'Titulo del servidor' }, count: 1 };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebText('home_title')).toBe('Titulo del servidor');
  });

  it('sirve el texto compilado cuando el servidor no publico esa clave', async () => {
    api.textos = { texts: {}, count: 0 };
    const service = crear(api);

    await service.loadPublishedContent();

    // No queda en blanco: cae al valor de fabrica.
    expect(service.getWebText('home_title').length).toBeGreaterThan(0);
  });

  it('publicar una cadena vacia BORRA el texto del sitio (PNMC-040)', async () => {
    // Este es el hallazgo. Con `||` la cadena vacia era falsa y el sitio volvia
    // a servir el valor compilado: borrar no borraba, y el panel confirmaba que si.
    //
    // La premisa que sostenia la conducta anterior —«el servidor devuelve '' por
    // dos motivos que no se pueden separar»— es falsa contra este servidor:
    // `GET /api/v1/contenido-web` filtra `Publicado != null`
    // (ContenidoWebEndpoints.cs), asi que una clave sin publicar NO aparece en
    // el diccionario. Cadena vacia significa una sola cosa: alguien la publico asi.
    api.textos = { texts: { access_footer_note: '' }, count: 1 };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebText('access_footer_note')).toBe('');
  });

  it('distingue clave publicada en blanco de clave no publicada', async () => {
    // Las dos mitades del contrato, en una sola prueba: ausente cae al compilado,
    // presente y vacia se respeta. Si alguien vuelve a poner `||` en getWebText,
    // la primera expectativa sigue verde y la segunda se pone roja.
    api.textos = { texts: { access_terms_label: '' }, count: 1 };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebText('home_title')).toBe(DEFAULT_TEXTS['home_title']);
    expect(service.getWebText('access_terms_label')).toBe('');
  });

  it('retirar una clave la devuelve al texto compilado', async () => {
    // Retirar pone `Publicado = null` (ContenidoWebEndpoints.cs) y entonces la
    // clave desaparece del diccionario. Retirar y publicar en blanco son dos
    // acciones distintas con dos resultados distintos, y asi debe seguir.
    api.textos = { texts: {}, count: 0 };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebText('access_footer_note')).toBe(DEFAULT_TEXTS['access_footer_note']);
  });

  it('no deja la pagina en blanco si la API no responde', async () => {
    api.textos = null;
    api.equipo = null;
    const service = crear(api);

    const ok = await service.loadPublishedContent();

    expect(ok).toBeFalse();
    expect(service.serverReachable()).toBeFalse();
    expect(service.getWebText('home_title').length).toBeGreaterThan(0);
    expect(service.getWebTeamMembers().length).toBeGreaterThan(0);
  });

  it('ignora lo que quedo en localStorage: ya no alimenta el sitio', async () => {
    // Antes de la Fase 3 esto habria salido en la pagina. Era una verdad que
    // solo veia el editor que la escribio.
    localStorage.setItem('pnmc_web_texts', JSON.stringify({
      home_title: { content: 'Escrito en el navegador', publishedContent: 'Escrito en el navegador', status: 'publicado', updatedAt: '', updatedBy: '' },
    }));
    api.textos = { texts: {}, count: 0 };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebText('home_title')).not.toBe('Escrito en el navegador');
  });

  it('no vuelve a pedir el contenido en cada lectura', async () => {
    api.textos = { texts: { home_title: 'Una sola vez' }, count: 1 };
    const service = crear(api);
    await service.loadPublishedContent();

    for (let i = 0; i < 50; i++) {
      service.getWebText('home_title');
    }

    expect(api.llamadas).toBe(1);
  });
});

describe('TextosWebService · la nomina distingue vacia de no publicada', () => {
  let api: ApiDoble;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    api = new ApiDoble();
    api.textos = { texts: {}, count: 0 };
  });

  it('usa la nomina compilada cuando no hay ninguna publicada', async () => {
    api.equipo = { published: false, members: null };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebTeamMembers().length).toBe(9);
  });

  it('respeta una nomina publicada vacia: es una decision, no una ausencia', async () => {
    api.equipo = { published: true, members: [] };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebTeamMembers()).toEqual([]);
  });

  it('sirve la nomina publicada', async () => {
    api.equipo = {
      published: true,
      members: [{ id: 'team-1', group: 'coordination', role: 'Cargo', name: 'Persona', email: '', photo: '' }],
    };
    const service = crear(api);

    await service.loadPublishedContent();

    expect(service.getWebTeamMembers().map(m => m.name)).toEqual(['Persona']);
  });
});

describe('TextosWebService · rescate de lo que quedo en el navegador', () => {
  let api: ApiDoble;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    api = new ApiDoble();
  });

  it('exporta el contenido historico con su version de formato', () => {
    localStorage.setItem('pnmc_web_texts', JSON.stringify({
      home_title: { content: 'Pendiente de rescatar', status: 'borrador', updatedAt: '', updatedBy: '' },
    }));
    const service = crear(api);

    const backup = service.exportContent('Pruebas');

    expect(backup.app).toBe('pnmc-cms');
    expect(backup.schemaVersion).toBe(WEB_CONTENT_BACKUP_VERSION);
    expect(backup.exportedBy).toBe('Pruebas');
    const textos = backup.stores['pnmc_web_texts'] as Record<string, { content: string }>;
    expect(textos['home_title'].content).toBe('Pendiente de rescatar');
  });

  it('exporta el bloque de equipo como nulo si este navegador nunca lo edito', () => {
    const service = crear(api);

    // Rellenarlo con la nomina de fabrica seria fabricar un dato que este
    // navegador nunca tuvo, y la importacion lo tomaria por trabajo de alguien.
    expect(service.exportContent().stores['pnmc_web_team_members']).toBeNull();
  });

  it('cuenta lo que queda pendiente de rescatar', () => {
    localStorage.setItem('pnmc_web_texts', JSON.stringify({ a: {}, b: {} }));
    localStorage.setItem('pnmc_web_team_members', JSON.stringify({
      content: [{ id: 'team-1' }], status: 'borrador', updatedAt: '', updatedBy: '',
    }));
    const service = crear(api);

    expect(service.pendingLocalContent()).toEqual({ textKeys: 2, teamMembers: 1 });
  });

  it('trata un almacen corrupto como vacio en vez de romperse', () => {
    localStorage.setItem('pnmc_web_texts', 'no es json');
    const service = crear(api);

    expect(service.pendingLocalContent().textKeys).toBe(0);
  });
});
