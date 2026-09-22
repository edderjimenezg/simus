import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  AdminImage,
  AdminImageGroup,
  AdminImageState,
  ApiOutcome,
  ContenidoWebApiService,
} from '../../../core/services/contenido-web-api.service';
import { AdminWebMediaPanelComponent } from './admin-web-media-panel.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

/*
  EL PANEL DE IMÁGENES.

  Lo que estas pruebas defienden es el reparto de poder, que es lo único que la
  pantalla puede equivocarse de forma cara:

   - la marca institucional ajena no ofrece «subir»;
   - quien no puede publicar no ve los botones que cambian el sitio;
   - «desplegar» solo aparece cuando hay algo distinto que desplegar;
   - una ranura retirada se devuelve con REPUBLICAR y no con publicar, porque si
     no, retirar sería reversible por accidente y dejaría de significar nada.

  Ninguna de las cuatro es la guarda de verdad —el servidor responde 403 y 409
  igual— pero una pantalla que ofrece lo que va a fallar es una pantalla que
  miente, y esa es su propia clase de defecto.
*/
describe('AdminWebMediaPanelComponent · quién puede qué', () => {
  let fixture: ComponentFixture<AdminWebMediaPanelComponent>;
  let componente: AdminWebMediaPanelComponent;
  let api: ApiDoble;

  const TOPES = {
    maxBytes: 2 * 1024 * 1024,
    maxThumbnailBytes: 24 * 1024,
    maxAltLength: 300,
    maxDimension: 12000,
    allowedTypes: ['image/webp', 'image/png', 'image/jpeg'],
  };

  function ranura(parcial: Partial<AdminImage>): AdminImage {
    return {
      key: 'home_hero_1',
      label: 'Portada 1',
      use: 'fondo',
      editable: true,
      alt: 'Portada',
      suggestedWidth: 1600,
      suggestedHeight: 900,
      draft: null,
      published: null,
      state: 'no_publicado',
      version: 1,
      updatedBy: 'Sistema',
      updatedAt: '2026-08-29T00:00:00Z',
      ...parcial,
    };
  }

  const MITAD = { mime: 'image/png', bytes: 1000, width: 1600, height: 900, hash: 'aaa' };

  class ApiDoble {
    grupo: AdminImageGroup = {
      groupId: 'home_media',
      groupLabel: 'Portadas',
      section: 'Home',
      images: [],
      limits: TOPES,
    };

    /** Lo que se llamó, en orden. Es lo que hace medible «usó republicar y no publicar». */
    llamadas: string[] = [];

    async getImageGroup(): Promise<ApiOutcome<AdminImageGroup>> {
      this.llamadas.push('getImageGroup');
      return { ok: true, data: this.grupo };
    }

    async uploadImage(): Promise<ApiOutcome<AdminImageState>> {
      this.llamadas.push('uploadImage');
      return { ok: true, data: { key: 'k', state: 'no_publicado', version: 2, changed: true, hash: 'bbb', mime: 'image/png', width: 1, height: 1, bytes: 1 } };
    }

    async publishImage(): Promise<ApiOutcome<AdminImageState>> {
      this.llamadas.push('publishImage');
      return { ok: true, data: { key: 'k', state: 'publicado', version: 3, changed: true, hash: 'bbb', mime: 'image/png', width: 1, height: 1, bytes: 1 } };
    }

    async republishImage(): Promise<ApiOutcome<AdminImageState>> {
      this.llamadas.push('republishImage');
      return { ok: true, data: { key: 'k', state: 'publicado', version: 3, changed: true, hash: 'bbb', mime: 'image/png', width: 1, height: 1, bytes: 1 } };
    }

    async retireImage(): Promise<ApiOutcome<AdminImageState>> {
      this.llamadas.push('retireImage');
      return { ok: true, data: { key: 'k', state: 'retirado', version: 4, changed: true, hash: 'bbb', mime: null, width: null, height: null, bytes: null } };
    }

    imagePreviewUrl(key: string, estado: string, version: number, miniatura = true): string {
      return `/api/v1/admin/imagenes-web/${key}/preview/${estado}?miniatura=${miniatura}&v=${version}`;
    }
  }

  async function montar(imagenes: AdminImage[], puedePublicar: boolean): Promise<void> {
    // SE REINICIA AQUI Y NO SOLO EN beforeEach: hay una prueba que monta dos
    // veces —el mismo grupo con y sin borrador pendiente— y el segundo montaje
    // moría con «test module has already been instantiated». Comparar los dos
    // casos en la misma prueba es lo que hace legible que la diferencia está en
    // la huella y en nada más.
    TestBed.resetTestingModule();
    api = new ApiDoble();
    api.grupo = { ...api.grupo, images: imagenes };

    await TestBed.configureTestingModule({
      imports: [AdminWebMediaPanelComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebMediaPanelComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('enabled', true);
    fixture.componentRef.setInput('puedePublicar', puedePublicar);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => TestBed.resetTestingModule());

  function elemento(testid: string): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testid}"]`);
  }

  it('una ranura no editable no ofrece subir y dice por qué', async () => {
    await montar([
      ranura({ key: 'marca_gov_co', label: 'Marca GOV.CO', editable: false, use: 'logotipo' }),
    ], true);

    expect(elemento('subir-marca_gov_co')).toBeNull();
    expect(elemento('no-editable-marca_gov_co')?.textContent)
      .withContext('no se explica por qué no se puede reemplazar')
      .toContain('Marca institucional');
  });

  it('quien no puede publicar no ve los botones que cambian el sitio', async () => {
    // gestor_interno: sube borradores y no alcanza el sitio público. El servidor
    // responde 403 igual; esto es no ofrecer lo que va a fallar.
    await montar([
      ranura({ draft: MITAD, published: null, state: 'no_publicado' }),
    ], false);

    expect(elemento('subir-home_hero_1')).withContext('debería poder subir borradores').not.toBeNull();
    expect(elemento('desplegar-home_hero_1')).toBeNull();
    expect(elemento('retirar-home_hero_1')).toBeNull();
  });

  it('«desplegar» solo aparece cuando hay algo distinto que desplegar', async () => {
    // Publicado y borrador con la MISMA huella: no hay nada pendiente.
    await montar([
      ranura({ draft: MITAD, published: MITAD, state: 'publicado' }),
    ], true);
    expect(elemento('desplegar-home_hero_1'))
      .withContext('ofrece desplegar un borrador idéntico al publicado')
      .toBeNull();
    expect(elemento('retirar-home_hero_1')).not.toBeNull();

    // Huellas distintas: sí hay algo pendiente.
    await montar([
      ranura({ draft: { ...MITAD, hash: 'bbb' }, published: MITAD, state: 'publicado' }),
    ], true);
    expect(elemento('desplegar-home_hero_1')).not.toBeNull();
  });

  it('una ranura retirada se devuelve con REPUBLICAR, no con publicar', async () => {
    // Es lo que hace que retirar signifique algo: el servidor responde 409 a un
    // publicar sobre una clave retirada, y ofrecerlo dejaría al editor delante de
    // un botón que no funciona.
    await montar([
      ranura({ draft: MITAD, published: null, state: 'retirado' }),
    ], true);

    const boton = elemento('desplegar-home_hero_1');
    expect(boton).withContext('una ranura retirada no ofrece volver al sitio').not.toBeNull();

    api.llamadas = [];
    await componente.desplegar(componente.imagenes()[0]);

    expect(api.llamadas).toContain('republishImage');
    expect(api.llamadas).not.toContain('publishImage');
  });

  it('un archivo por encima del tope se rechaza antes de subirlo', async () => {
    await montar([ranura({})], true);
    api.llamadas = [];

    const grande = new File([new Uint8Array(10)], 'enorme.png', { type: 'image/png' });
    // El tamaño real del File no se puede fijar; se sustituye la propiedad, que
    // es lo que el componente lee.
    Object.defineProperty(grande, 'size', { value: TOPES.maxBytes + 1 });
    const entrada = document.createElement('input');
    Object.defineProperty(entrada, 'files', { value: [grande] });

    await componente.subir(componente.imagenes()[0], { target: entrada } as unknown as Event);

    // No se llamó a la API: el aviso llega sin haber esperado a que suban dos megas.
    expect(api.llamadas).not.toContain('uploadImage');
    expect(componente.aviso()?.tipo).toBe('error');
  });

  it('hayBorradorSinPublicar compara la huella y no la fecha', async () => {
    await montar([], true);

    expect(componente.hayBorradorSinPublicar(ranura({ draft: null, published: MITAD }))).toBeFalse();
    expect(componente.hayBorradorSinPublicar(ranura({ draft: MITAD, published: MITAD }))).toBeFalse();
    expect(componente.hayBorradorSinPublicar(
      ranura({ draft: { ...MITAD, hash: 'otra' }, published: MITAD }))).toBeTrue();
    // Con borrador y sin nada publicado, hay algo pendiente.
    expect(componente.hayBorradorSinPublicar(ranura({ draft: MITAD, published: null }))).toBeTrue();
  });
});

/*
  ESTE BLOQUE USA EL SERVICIO DE VERDAD, y por eso está separado.

  La afirmación sobre la URL de previsualización vivía arriba, contra el doble, y
  un mutante lo destapó: quitarle la versión a `imagePreviewUrl` dejaba las siete
  pruebas en verde, porque la que la miraba estaba leyendo la copia del doble y no
  el código. Una prueba que mide su propio doble no mide nada.
*/
describe('ContenidoWebApiService · la URL de previsualización', () => {
  let api: ContenidoWebApiService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [ContenidoWebApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ContenidoWebApiService);
  });

  it('lleva la versión, para que publicar no deje la imagen vieja en la cuadrícula', () => {
    // Sin `v`, el navegador sirve la anterior de su caché —la ruta es la misma— y
    // el editor cree que su subida no se guardó.
    const url = api.imagePreviewUrl('home_hero_1', 'publicado', 7);

    expect(url).toContain('v=7');
    expect(api.imagePreviewUrl('home_hero_1', 'publicado', 8)).not.toBe(url);
  });

  it('pide la miniatura por omisión y el original cuando se lo dicen', () => {
    // La cuadrícula pinta dieciséis a la vez: 16 × 24 KiB frente a 16 × 250 KB.
    expect(api.imagePreviewUrl('home_hero_1', 'borrador', 1)).toContain('miniatura=true');
    expect(api.imagePreviewUrl('home_hero_1', 'borrador', 1, false)).toContain('miniatura=false');
  });

  it('escapa la clave en la ruta', () => {
    expect(api.imagePreviewUrl('a/b', 'publicado', 1)).toContain('a%2Fb');
  });
});
