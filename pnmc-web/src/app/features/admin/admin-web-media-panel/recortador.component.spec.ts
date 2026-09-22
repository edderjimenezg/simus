import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RecortadorComponent } from './recortador.component';

/*
  EL RECORTADOR, la parte que la aritmética no cubre.

  `recorte.spec.ts` ya prueba el encaje, los topes y el recorte de píxeles. Aquí se prueba lo que
  solo se ve montando el componente: que lo que la persona VE mientras decide sea lo que el sitio
  va a mostrar, y que lo que no se puede hacer no se ofrezca.

  Las tres propiedades que defiende:
   - una imagen que no alcanza para el hueco NO se puede guardar, y se dice por qué con el número;
   - la previsualización lleva el mismo filtro de color que el sitio, o la persona elige a ciegas;
   - donde el marco no es exacto, se dice, en vez de fingir que lo es.
*/
describe('RecortadorComponent · lo que la persona ve al decidir', () => {
  let fixture: ComponentFixture<RecortadorComponent>;
  let componente: RecortadorComponent;

  /** Un PNG real, del tamaño que se pida, como File. */
  async function archivoDe(w: number, h: number): Promise<File> {
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    const x = c.getContext('2d')!;
    x.fillStyle = '#c1440e';
    x.fillRect(0, 0, w, h);
    const blob = await new Promise<Blob | null>(ok => c.toBlob(ok, 'image/png'));
    return new File([blob!], 'prueba.png', { type: 'image/png' });
  }

  async function montar(entradas: Record<string, unknown>): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({ imports: [RecortadorComponent] }).compileComponents();
    fixture = TestBed.createComponent(RecortadorComponent);
    componente = fixture.componentInstance;
    for (const [k, v] of Object.entries(entradas)) {
      fixture.componentRef.setInput(k, v);
    }
    fixture.detectChanges();
    // La imagen se lee en una microtarea; hay que dejarla terminar antes de mirar nada.
    await fixture.whenStable();
    await new Promise(r => setTimeout(r, 60));
    fixture.detectChanges();
  }

  function elemento(testid: string): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testid}"]`);
  }

  it('bloquea una imagen más pequeña que el hueco y dice los dos números', async () => {
    // Es el caso vivo que motivó la decisión: `home_hero_4` pedía 687 px de ancho para un hueco
    // medido de 1670. Se ampliaba 2,43 veces y nada avisaba.
    await montar({
      archivo: await archivoDe(687, 430),
      marco: { w: 1670, h: 1044 },
    });

    expect(componente.puedeGuardar()).toBeFalse();
    expect((elemento('confirmar-recorte') as HTMLButtonElement).disabled).toBeTrue();

    const aviso = elemento('aviso-del-recorte')?.textContent ?? '';
    expect(aviso).toContain('687');
    expect(aviso).toContain('1670');
  });

  it('deja guardar cuando la imagen alcanza', async () => {
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 438, h: 318 },
    });

    expect(componente.puedeGuardar()).toBeTrue();
    expect((elemento('confirmar-recorte') as HTMLButtonElement).disabled).toBeFalse();
  });

  it('pinta la previsualización con el mismo filtro que el sitio', async () => {
    // La portada de página sale con `grayscale(1)` al 28 % de opacidad. Si aquí se viera a todo
    // color, la persona elegiría una imagen que en el sitio se ve de otra manera. Los diez
    // tratamientos del sitio se midieron con el navegador y ninguno es neutro.
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 1440, h: 324 },
      filtro: 'grayscale(1)',
      opacidad: 0.28,
    });

    const img = (fixture.nativeElement as HTMLElement).querySelector('img') as HTMLImageElement;
    expect(img).not.toBeNull();
    expect(img.style.filter).toBe('grayscale(1)');
    expect(Number(img.style.opacity)).toBeCloseTo(0.28, 2);
  });

  it('el escenario se pinta sobre el color que el sitio tiene debajo', async () => {
    // La portada del Home va al 30 % de opacidad sobre `#291242`. Sobre el gris oscuro que
    // traía el escenario por defecto, la misma foto se veía gris; en el sitio se ve morada.
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 1670, h: 1044 },
      filtro: 'grayscale(1)',
      opacidad: 0.3,
      fondo: '#291242',
    });

    expect(elemento('escenario')?.style.background).toContain('rgb(41, 18, 66)');
  });

  it('el escenario tiene la relación del marco, no la de la imagen', async () => {
    // La persona coloca la foto dentro de un hueco que ya existe. Si el escenario tomara la
    // relación de la foto, estaría enmarcando otra cosa.
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 1440, h: 324 },
    });

    expect(elemento('escenario')?.style.aspectRatio).toBe('1440 / 324');
  });

  it('avisa de que el marco cambia cuando la ranura es de altura variable', async () => {
    // Las portadas de página van de 4,00 a 4,44 según el alto de la ventana, porque
    // `page-hero.component.html:7` declara `lg:h-[40vh]`. Prometer un recorte exacto sería mentir.
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 1440, h: 324 },
      clase: 'variable',
    });

    expect(elemento('aviso-del-recorte')?.textContent).toContain('cambia de alto');
    expect(elemento('franja-plegada')).toBeNull();
  });

  it('dibuja la franja del estado plegado solo en las ranuras de dos marcos', async () => {
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 700, h: 398 },
      clase: 'dos-marcos',
      relacionPlegada: 0.29,
    });

    const franja = elemento('franja-plegada');
    expect(franja).not.toBeNull();
    // 0,29 / 1,758 = 16,5 % del ancho.
    expect(parseFloat(franja!.style.width)).toBeCloseTo(16.5, 0);
  });

  it('dibuja dónde el sitio pinta texto encima', async () => {
    // El banner lleva el texto en x 61 %–97 %, medido. Sin verlo, la persona centra la cara
    // debajo del titular.
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 1440, h: 520 },
      zonaDeTexto: { x: 61, y: 26, w: 36, h: 56 },
    });

    const zona = elemento('zona-de-texto');
    expect(zona).not.toBeNull();
    expect(parseFloat(zona!.style.left)).toBeCloseTo(61, 0);

    // Y SE DICE QUÉ ES. Un rectángulo punteado sin explicación es un adorno: hay que decir que
    // ahí va el titular, o la persona centra la cara justo debajo de él.
    expect(elemento('leyenda-zona-de-texto')?.textContent).toContain('titular');
  });

  it('sin zona de texto no se pinta ni el recuadro ni su leyenda', async () => {
    await montar({ archivo: await archivoDe(1600, 1070), marco: { w: 438, h: 318 } });

    expect(elemento('zona-de-texto')).toBeNull();
    expect(elemento('leyenda-zona-de-texto')).toBeNull();
  });

  it('entrega un WebP con las medidas que anuncia y su miniatura', async () => {
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 438, h: 318 },
    });

    const anunciado = componente.salida();
    // El panel muestra ese número antes de guardar; si el archivo saliera con otro, la cifra de
    // la pantalla sería decorativa.
    expect(elemento('medidas-de-salida')?.textContent).toContain(String(anunciado.w));

    const entregado = await new Promise<{ archivo: Blob; miniatura: Blob | null; ancho: number }>(ok => {
      componente.listo.subscribe(ok);
      void componente.confirmar();
    });

    expect(entregado.archivo.type).toBe('image/webp');
    expect(entregado.ancho).toBe(anunciado.w);

    const salida = await createImageBitmap(entregado.archivo);
    expect(salida.width).toBe(anunciado.w);
    expect(salida.height).toBe(anunciado.h);

    expect(entregado.miniatura).not.toBeNull();
    const mini = await createImageBitmap(entregado.miniatura!);
    expect(mini.width).toBe(320);
  });

  it('reencuadrar devuelve el recorte al encaje inicial', async () => {
    await montar({
      archivo: await archivoDe(1600, 1070),
      marco: { w: 438, h: 318 },
    });

    componente.cambiarNivel(3);
    expect(componente.recorte().w).toBeLessThan(componente.minimo().w);

    componente.reencuadrar();
    expect(componente.recorte()).toEqual(componente.minimo());
  });
});
