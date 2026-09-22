import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PageHeroComponent } from './page-hero.component';

/**
 * EL ENCABEZADO COMPACTO NO PUEDE APLASTAR SU PROPIO CONTENIDO.
 *
 * está definido mirando /ecosistema: «se ve muy
 * pegado todo, se siente muy pegado». Medido ese día a 1280×639 con el navegador:
 *
 * | ruta        | caja útil | contenido | desborde |
 * |-------------|-----------|-----------|----------|
 * | /ecosistema | 112px     | 198px     | 86px     |
 * | /pnmc       | 112px     | 172px     | 60px     |
 * | /ejes       | 112px     | 172px     | 60px     |
 *
 * La causa: `h-[24rem] lg:h-[40vh]` fija el alto, y `pt-28` —los 112px que reserva la barra
 * fija— sale de dentro de ese alto en vez de sumarse. Con `flex items-center` el bloque se
 * centraba en los 256px del encabezado y el título arrancaba en y=69, es decir 43px DENTRO de la
 * franja de la barra. Las tres páginas que usan el modo compacto lo sufrían.
 *
 * ESTA PRUEBA MIDE, NO LEE CLASES. Se comprueba que la caja de contenido cabe en el hueco que
 * dejan los rellenos. Un `h-` fijo la pone en rojo; un `min-h-` la deja pasar. Las clases se
 * comprueban además, porque la medida sola no distingue «cabe porque el alto crece» de «cabe
 * porque alguien quitó el relleno superior», y quitar el relleno devolvería el solape con la
 * barra por otro camino.
 */
describe('PageHeroComponent · el encabezado compacto crece con su texto', () => {
  let fixture: ComponentFixture<PageHeroComponent>;

  /**
   * Texto largo Y ANCHO DEL ANFITRIÓN FIJADO, las dos cosas.
   *
   * LO PIDIÓ UN MUTANTE QUE SOBREVIVIÓ. Con la descripción de una página real —unos 370
   * caracteres— el contenido medía ~180px en el marco de Karma y cabía en los 240px que deja el
   * alto fijo: devolver `min-h-` a `h-` no ponía esta prueba en rojo, solo la de las clases. La
   * medida no medía nada.
   *
   * Con el anfitrión a 360px la descripción envuelve en unas veinte líneas, y el contenido supera
   * cualquier hueco que el alto fijo pueda dejar —384px de `h-[24rem]` o el `40vh` del marco, el
   * que resulte mayor—, así que la prueba deja de depender de cuán alto abra Karma el navegador.
   */
  const DESCRIPCION_LARGA = [
    'Un espacio público para conocer, consultar y fortalecer la información sobre las músicas,',
    'sus procesos y quienes las hacen posibles en Colombia. Esta lectura reúne escuelas,',
    'escenarios, festivales, mercados, redes de documentación y lutería, cada uno con su',
    'directorio de consulta pública y una lectura territorial compartida que conecta las',
    'categorías con su presencia geográfica en el Mapa Ecosistémico del Plan. No reemplaza la',
    'experiencia del territorio: la conecta, y por eso el mapa ofrece la visión integral mientras',
    'los directorios especializados permiten profundizar en cada proceso, en cada municipio y en',
    'cada práctica musical que las comunidades sostienen a lo largo y ancho del país.',
  ].join(' ');

  /** 360px: el ancho al que la descripción envuelve lo suficiente para que la medida sea útil. */
  const ANCHO_ESTRECHO = '360px';

  const medir = () => {
    const header = (fixture.nativeElement as HTMLElement).querySelector('header')!;
    const contenido = header.querySelector<HTMLElement>('.relative.z-20')!;
    const estilo = getComputedStyle(header);
    const util = header.clientHeight - parseFloat(estilo.paddingTop) - parseFloat(estilo.paddingBottom);
    return { header, util: Math.round(util), contenido: Math.round(contenido.getBoundingClientRect().height), estilo };
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PageHeroComponent] }).compileComponents();
    fixture = TestBed.createComponent(PageHeroComponent);
  });

  it('el contenido cabe dentro de los rellenos, no debajo de la barra', () => {
    const componente = fixture.componentInstance;
    (fixture.nativeElement as HTMLElement).style.width = ANCHO_ESTRECHO;
    componente.title = 'Ecosistema';
    componente.titleAccent = 'que conecta el territorio';
    componente.description = DESCRIPCION_LARGA;
    fixture.detectChanges();

    const { util, contenido } = medir();

    expect(contenido).withContext(`el texto de prueba mide ${contenido}px y tiene que desbordar el alto fijo`).toBeGreaterThan(400);
    expect(util).withContext(`la caja útil (${util}px) no alcanza para el contenido (${contenido}px)`)
      .toBeGreaterThanOrEqual(contenido);
  });

  it('reserva arriba el alto de la barra fija y no lo recorta', () => {
    fixture.componentInstance.title = 'Ecosistema';
    fixture.detectChanges();

    // `pt-28` son 7rem = 112px, que es lo que mide la barra de navegación fija.
    expect(parseFloat(medir().estilo.paddingTop)).toBe(112);
  });

  it('el alto compacto es un mínimo y el de pantalla completa sigue siendo exacto', () => {
    fixture.componentInstance.title = 'Ecosistema';
    fixture.detectChanges();
    const clases = medir().header.classList;

    expect(clases).toContain('min-h-[24rem]');
    expect(clases).toContain('lg:min-h-[40vh]');
    expect(clases).not.toContain('h-[24rem]');
    expect(clases).not.toContain('lg:h-[40vh]');

    fixture.componentInstance.fullScreen = true;
    fixture.detectChanges();
    expect(medir().header.classList).toContain('h-[100svh]');
  });

  it('el modo «solo retorno» también crece con su contenido', () => {
    const componente = fixture.componentInstance;
    componente.backOnly = true;
    componente.title = 'Noticias';
    fixture.detectChanges();
    const clases = medir().header.classList;

    expect(clases).toContain('min-h-[20rem]');
    expect(clases).not.toContain('h-[20rem]');
  });
});
