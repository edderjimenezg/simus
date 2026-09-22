import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BloqueDeImagenesComponent, ImagenEnVivo } from './bloque-de-imagenes.component';
import {
  AdminImage,
  AdminImageGroup,
  ApiOutcome,
  ContenidoWebApiService,
} from '../../../core/services/contenido-web-api.service';
import { DEFAULT_IMAGE_URLS, WEB_IMAGE_KEYS } from '../../../core/cms/registro-de-imagenes-web';

/*
  LAS IMÁGENES DENTRO DEL BLOQUE DE TEXTO QUE LAS ACOMPAÑA.

  Lo que estas pruebas defienden es lo que separa este editor del panel de imágenes aparte:

   · la tarjeta enseña LO QUE EL VISITANTE VE HOY, incluso cuando no hay nada publicado;
   · el marco al que se recorta sale del registro del front, que es donde se midió;
   · publicar y subir NO son lo mismo para la previsualización, y el componente lo dice;
   · una ranura de marca institucional no ofrece reemplazo.
*/
describe('BloqueDeImagenesComponent · editar la imagen donde está su texto', () => {
  let fixture: ComponentFixture<BloqueDeImagenesComponent>;
  let componente: BloqueDeImagenesComponent;
  let api: ApiDoble;

  const PORTADAS = ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'];

  function ranura(clave: string, extra: Partial<AdminImage> = {}): AdminImage {
    const registro = WEB_IMAGE_KEYS.find(x => x.key === clave)!;
    return {
      key: clave,
      label: registro.label,
      use: registro.use,
      editable: registro.editable,
      alt: registro.alt,
      suggestedWidth: registro.suggestedWidth,
      suggestedHeight: registro.suggestedHeight,
      draft: null,
      published: null,
      state: 'no_publicado',
      version: 1,
      updatedBy: 'Sistema',
      updatedAt: '2026-08-30T00:00:00Z',
      ...extra,
    };
  }

  class ApiDoble {
    imagenes: AdminImage[] = PORTADAS.map(clave => ranura(clave));
    gruposPedidos: string[] = [];
    publicadas: string[] = [];
    retiradas: string[] = [];
    subidas: { key: string; bytes: number }[] = [];

    async getImageGroup(groupId: string): Promise<ApiOutcome<AdminImageGroup>> {
      this.gruposPedidos.push(groupId);
      return {
        ok: true,
        data: {
          groupId,
          groupLabel: 'Portadas rotatorias del Home',
          section: 'Home',
          images: this.imagenes,
          limits: {
            maxBytes: 2 * 1024 * 1024,
            maxThumbnailBytes: 24 * 1024,
            maxAltLength: 300,
            maxDimension: 12_000,
            allowedTypes: ['image/webp', 'image/png', 'image/jpeg'],
          },
        },
      };
    }

    async uploadImage(key: string, archivo: File) {
      this.subidas.push({ key, bytes: archivo.size });
      return { ok: true, data: { key, state: 'no_publicado', version: 2, changed: true, hash: 'aa', mime: 'image/webp', width: 10, height: 10, bytes: archivo.size } };
    }

    async publishImage(key: string) {
      this.publicadas.push(key);
      return { ok: true, data: { key, state: 'publicado', version: 3, changed: true, hash: 'aa', mime: 'image/webp', width: 10, height: 10, bytes: 1 } };
    }

    async republishImage(key: string) { return this.publishImage(key); }

    async retireImage(key: string) {
      this.retiradas.push(key);
      return { ok: true, data: { key, state: 'retirado', version: 4, changed: true, hash: null, mime: null, width: null, height: null, bytes: null } };
    }

    async getImageHistory() { return { ok: true, data: { key: 'x', entries: [] } }; }

    imagePreviewUrl(key: string, estado: string, version: number, miniatura = true): string {
      return `/api/v1/admin/imagenes-web/${key}/preview/${estado}?miniatura=${miniatura}&v=${version}`;
    }
  }

  async function montar(claves = PORTADAS, puedePublicar = true): Promise<void> {
    api = new ApiDoble();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [BloqueDeImagenesComponent],
      providers: [{ provide: ContenidoWebApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(BloqueDeImagenesComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('claves', claves);
    fixture.componentRef.setInput('enabled', true);
    fixture.componentRef.setInput('puedePublicar', puedePublicar);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function raiz(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('pide UNA vez el grupo aunque el bloque tenga cuatro ranuras', async () => {
    // Las cuatro portadas viven en `home_media`. Una petición por tarjeta serían cuatro
    // respuestas idénticas y tres desperdiciadas cada vez que se abre el bloque.
    await montar();
    expect(api.gruposPedidos).toEqual(['home_media']);
    expect(componente.ranuras().length).toBe(4);
  });

  it('sin nada publicado enseña la imagen que el sitio pinta hoy, aligerada', async () => {
    // Hasta aquí había un recuadro gris con la frase «el sitio muestra
    // su imagen original», que obliga a abrir el sitio en otra pestaña para saber cuál es.
    await montar();
    const url = componente.urlEnElSitio(componente.ranuras()[0]);

    expect(url).toContain('images.unsplash.com');
    expect(DEFAULT_IMAGE_URLS['home_hero_1']).toContain('w=1015');
    expect(url)
      .withContext('el panel se descargó la imagen a tamaño completo para un recuadro de 320 px')
      .toContain('w=320');
  });

  it('con algo publicado enseña lo publicado, no la de fábrica', async () => {
    await montar();
    api.imagenes = [ranura('home_hero_1', {
      state: 'publicado',
      published: { mime: 'image/webp', bytes: 40_000, width: 1670, height: 1044, hash: 'abc' },
      version: 7,
    })];
    await componente.recargar();

    expect(componente.urlEnElSitio(componente.ranuras()[0]))
      .toBe('/api/v1/admin/imagenes-web/home_hero_1/preview/publicado?miniatura=true&v=7');
  });

  it('el marco sale del registro del front, no de lo que responda el servidor', async () => {
    // El servidor guarda una copia de las medidas y se refresca al arrancar, así que puede ir
    // por detrás: la base tenía 1600×900 para esta ranura mientras el
    // registro ya decía 1670×1044. Recortar a la copia vieja recorta al marco equivocado.
    await montar();
    api.imagenes = [ranura('home_hero_1', { suggestedWidth: 1600, suggestedHeight: 900 })];
    await componente.recargar();

    expect(componente.marcoDe('home_hero_1')).toEqual({ w: 1670, h: 1044 });
  });

  it('las cuatro portadas rotan entre sí y una ranura suelta no rota', async () => {
    await montar();
    expect(componente.rotanCon('home_hero_1')).toEqual(PORTADAS);

    await montar(['hero_ejes']);
    expect(componente.rotanCon('hero_ejes')).toEqual([]);
  });

  it('las ocho Rutas comparten tratamiento y NO rotan; tampoco las seis del Ecosistema', async () => {
    // EL CASO DE EN MEDIO, que es el que faltaba. Hasta `rotanCon`
    // deducía la rotación comparando la identidad del objeto de presentación, y las ocho Rutas
    // comparten `TARJETA_DE_RUTA`: subir la foto de la Ruta 3 la pintaba en las ocho tarjetas de
    // la previsualización. La prueba de arriba no lo veía porque sus dos casos son los extremos
    // —una ranura que rota Y comparte tratamiento, y otra sin tratamiento declarado—.
    //
    // Cada tarjeta del carrusel tiene su foto y se ven las ocho a la vez. Espejar aquí no es una
    // mentira piadosa: es enseñar siete fotos que el sitio no va a pintar.
    const RUTAS = ['home_ruta_1', 'home_ruta_2', 'home_ruta_3', 'home_ruta_4',
                   'home_ruta_5', 'home_ruta_6', 'home_ruta_7', 'home_ruta_8'];
    await montar(RUTAS);
    for (const clave of RUTAS) {
      expect(componente.rotanCon(clave)).withContext(clave).toEqual([]);
    }

    const ECO = ['home_eco_1', 'home_eco_2', 'home_eco_3', 'home_eco_4', 'home_eco_5', 'home_eco_6'];
    await montar(ECO);
    for (const clave of ECO) {
      expect(componente.rotanCon(clave)).withContext(clave).toEqual([]);
    }

    // Y el banner tampoco: las tres diapositivas se turnan solas, así que espejar la que se está
    // cambiando taparía las otras dos y dejaría el banner sin poder mirarse entero.
    const BANNER = ['home_banner_1', 'home_banner_2', 'home_banner_3'];
    await montar(BANNER);
    for (const clave of BANNER) {
      expect(componente.rotanCon(clave)).withContext(clave).toEqual([]);
    }
  });

  it('espejar nunca sale del bloque que se está mirando', async () => {
    // `ROTAN_JUNTAS` nombra las cuatro portadas, pero un bloque que solo muestre dos no puede
    // anunciar las otras dos: el marco pintaría una ranura que en esta pantalla no se ve, y la
    // persona no tendría forma de deshacerlo.
    await montar(['home_hero_1', 'home_hero_2']);
    expect(componente.rotanCon('home_hero_1')).toEqual(['home_hero_1', 'home_hero_2']);
  });

  it('la previsualización se pinta con el filtro y el fondo del sitio', async () => {
    // La portada va con `grayscale(1)` al 30 % sobre `#291242`. Pintarla a todo color aquí
    // haría elegir bien una imagen que en el sitio se ve de otra manera.
    await montar();
    const img = raiz().querySelector<HTMLImageElement>('[data-testid="ranura-home_hero_1"] img')!;
    expect(img.style.filter).toBe('grayscale(1)');
    expect(Number(img.style.opacity)).toBeCloseTo(0.3, 2);
  });

  it('una ranura de marca institucional no ofrece reemplazo', async () => {
    await montar(['marca_gov_co']);
    api.imagenes = [ranura('marca_gov_co')];
    await componente.recargar();

    expect(componente.ranuras()[0].editable).toBeFalse();
    expect(raiz().querySelector('[data-testid="cambiar-marca_gov_co"]')).toBeNull();
  });

  it('sin permiso para publicar no se pinta ni desplegar ni retirar', async () => {
    await montar(PORTADAS, false);
    api.imagenes = [ranura('home_hero_1', {
      state: 'publicado',
      draft: { mime: 'image/webp', bytes: 10, width: 1, height: 1, hash: 'nueva' },
      published: { mime: 'image/webp', bytes: 10, width: 1, height: 1, hash: 'vieja' },
    })];
    await componente.recargar();

    expect(componente.hayBorradorSinPublicar(componente.ranuras()[0])).toBeTrue();
    expect(raiz().querySelector('[data-testid="desplegar-home_hero_1"]')).toBeNull();
    expect(raiz().querySelector('[data-testid="retirar-home_hero_1"]')).toBeNull();
  });

  it('publicar y retirar piden recargar el marco; cargar la pantalla no', async () => {
    await montar();
    const anuncios: ImagenEnVivo[] = [];
    componente.enVivo.subscribe(e => anuncios.push(e));

    await componente.desplegar(componente.ranuras()[0]);
    await componente.retirar(componente.ranuras()[0]);

    expect(api.publicadas).toEqual(['home_hero_1']);
    expect(api.retiradas).toEqual(['home_hero_1']);

    // Publicar y retirar cambian el manifiesto y piden recarga; los anuncios que dispara volver
    // a leer el grupo, no.
    const conRecarga = anuncios.filter(a => a.recargarElSitio);
    expect(conRecarga.length).toBe(2);
    // Y esos dos espejan sobre las cuatro, que es lo que hace mirable una portada que rota.
    expect(conRecarga[0].rotanConElla).toEqual(PORTADAS);
  });

  it('al cargar anuncia lo que ya había sin publicar, y sin espejarlo', async () => {
    // Un borrador subido en otra sesión existe antes de que esta pantalla se abra. Sin este
    // anuncio, la previsualización pintaba lo publicado mientras la tarjeta de al lado enseñaba
    // otra imagen bajo el rótulo «sin publicar».
    //
    // SIN ESPEJAR, y es la mitad que importa: fijar una portada sobre las cuatro que rotan sirve
    // para poder mirar la que se está cambiando. Al cargar no se está cambiando ninguna, y
    // espejar aquí haría que el sitio se viera con una portada que no es la que le toca.
    await montar();
    api.imagenes = PORTADAS.map(clave => ranura(clave, clave === 'home_hero_2'
      ? { draft: { mime: 'image/webp', bytes: 10, width: 1, height: 1, hash: 'nueva' }, version: 5 }
      : {}));

    const anuncios: ImagenEnVivo[] = [];
    componente.enVivo.subscribe(e => anuncios.push(e));
    await componente.recargar();

    expect(anuncios.map(a => a.clave)).toEqual(PORTADAS);
    expect(anuncios.every(a => a.rotanConElla.length === 0)).toBeTrue();
    expect(anuncios.every(a => !a.recargarElSitio)).toBeTrue();
    expect(anuncios.filter(a => a.url !== null).map(a => a.clave)).toEqual(['home_hero_2']);
  });

  it('al marco le manda el archivo entero, no la miniatura de la tarjeta', async () => {
    // La miniatura mide 320 px de ancho. El marco la estiraría a 1440 como fondo de portada, y
    // la persona juzgaría la nitidez de una imagen que el sitio nunca va a servir así.
    await montar();
    api.imagenes = [ranura('home_hero_1', {
      draft: { mime: 'image/webp', bytes: 10, width: 2400, height: 1500, hash: 'nueva' },
      version: 5,
    })];
    await componente.recargar();

    const imagen = componente.ranuras()[0];
    expect(componente.urlSinPublicar(imagen)).toContain('miniatura=true');
    expect(componente.urlParaElMarco(imagen)).toContain('miniatura=false');
  });

  it('un archivo más pesado que el tope se rechaza antes de decodificarlo', async () => {
    await montar();
    const enorme = new File([new Uint8Array(3 * 1024 * 1024)], 'grande.png', { type: 'image/png' });
    const entrada = { target: { files: [enorme], value: 'x' } } as unknown as Event;

    componente.elegirArchivo(componente.ranuras()[0], entrada);
    fixture.detectChanges();

    expect(componente.ajustando())
      .withContext('se abrió el recortador con un archivo que el servidor va a rechazar')
      .toBeNull();
    expect(componente.aviso()?.tipo).toBe('error');
    expect(componente.aviso()?.texto).toContain('3.0 MB');
  });

  it('un archivo que cabe abre el ajuste y no sube nada todavía', async () => {
    // Subir es lo que hace «Usar esta imagen». Elegir el archivo solo abre el marco.
    await montar();
    const cabe = new File([new Uint8Array(1024)], 'ok.png', { type: 'image/png' });
    componente.elegirArchivo(
      componente.ranuras()[0],
      { target: { files: [cabe], value: 'x' } } as unknown as Event,
    );

    expect(componente.ajustando()?.clave).toBe('home_hero_1');
    expect(api.subidas).toEqual([]);
  });
});
