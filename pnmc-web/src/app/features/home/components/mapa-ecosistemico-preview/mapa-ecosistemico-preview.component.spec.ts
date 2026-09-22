import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { FestivalesPublicosService } from '../../../../core/services/festivales-publicos.service';
import { NavigationService } from '../../../../core/services/navigation.service';
import { CATEGORIAS_ECOSISTEMA } from '../../../../core/services/categorias-ecosistema.config';
import { MapaEcosistemicoPreviewComponent } from './mapa-ecosistemico-preview.component';

describe('MapaEcosistemicoPreviewComponent', () => {
  let fixture: ComponentFixture<MapaEcosistemicoPreviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPreviewComponent],
      providers: [
        provideRouter([]),
        // LA CIFRA VIENE DEL PROCESO, NO DEL MAPA. Este doble era `MapDataService` con su paquete
        // de recuentos; desde la portada pregunta a cada proceso por
        // los suyos, para que siga informando aunque el mapa no esté. Ver `loadMapData()`.
        {
          provide: FestivalesPublicosService,
          useValue: { contarPublicados: () => of(3) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MapaEcosistemicoPreviewComponent);
    fixture.detectChanges();
  });

  it('renders six homogeneous interactive cards with larger titles and no status labels', () => {
    const root = fixture.nativeElement as HTMLElement;
    const images = root.querySelectorAll<HTMLElement>('[data-testid="ecosystem-category-image"]');
    const tints = root.querySelectorAll<HTMLElement>('[data-testid="ecosystem-category-tint"]');
    const titles = root.querySelectorAll<HTMLElement>('[data-testid="ecosystem-category-title"]');
    const counters = root.querySelectorAll<HTMLElement>('[data-testid="ecosystem-category-count"]');
    const grid = root.querySelector<HTMLElement>('[data-testid="ecosystem-category-grid"]');
    const content = root.textContent ?? '';

    expect(images.length).toBe(6);
    images.forEach(image => {
      // 70 y no 30: el filtro se suavizó y la legibilidad pasa a
      // garantizarla el degradado del tercio inferior, no un velo sobre la caja entera.
      expect(image.className).toContain('opacity-70');
      expect(image.className).toContain('group-hover:opacity-100');
    });
    expect(tints.length).toBe(6);
    tints.forEach(tint => expect(tint.className).toContain('group-hover:bg-[#00DA5E]/20'));
    expect(grid?.className).not.toContain('gap-');
    // Tres columnas, no cuatro: con seis tarjetas y sin separacion, cuatro columnas dejarian
    // dos huecos vacios en la ultima fila, dentro del recuadro.
    expect(grid?.className).toContain('md:grid-cols-3');
    expect(titles.length).toBe(6);
    titles.forEach(title => expect(title.className).toContain('text-xl'));
    expect(counters.length).toBe(6);
    // Festivales es el único proceso con directorio y recuento activos en esta versión.
    expect(root.querySelector<HTMLElement>('[data-category="festivals"]')?.textContent?.trim()).toContain('3');
    expect(content).not.toContain('Disponible');
    expect(content).not.toContain('Próximamente');
    expect(content).not.toContain('Próx.');
  });

  it('muestra los seis procesos y ninguno mas', () => {
    // POR NOMBRE Y NO POR CANTIDAD. Contar seis tarjetas no distingue «las seis correctas» de
    // «seis cualesquiera»: un filtro equivocado que dejara Agrupaciones dentro y Escenarios
    // fuera seguiria dando seis. Se comparan los rotulos, y en el orden del catalogo.
    const root = fixture.nativeElement as HTMLElement;
    const rotulos = Array.from(root.querySelectorAll<HTMLElement>('[data-testid="ecosystem-category-title"]'))
      .map(titulo => titulo.textContent?.trim());

    expect(rotulos).toEqual([
      'Escuelas de música',
      'Escenarios',
      'Festivales',
      'Mercados musicales',
      'Redes y documentación',
      'Lutería',
    ]);
  });

  it('deja fuera Agrupaciones y Agentes, que no tienen directorio', () => {
    // Ya no estan ni en el catalogo: se retiraron al unificar las tres
    // superficies del ecosistema —esta rejilla, el desplegable del menu y la portada—, porque no
    // tienen tabla ni endpoint y su tarjeta prometia un directorio inexistente.
    const root = fixture.nativeElement as HTMLElement;
    const content = root.textContent ?? '';

    expect(content).not.toContain('Agrupaciones');
    expect(content).not.toContain('Agentes');
    expect(root.querySelector('[data-category="groups"]')).toBeNull();
    expect(root.querySelector('[data-category="agents"]')).toBeNull();
  });

  it('el catalogo son seis procesos y nada mas', () => {
    // EL CANARIO DE LA PRUEBA ANTERIOR, invertido. Cuando el catalogo tenia ocho, esto exigia que
    // las dos sobrantes siguieran ahi para que el filtro por `proceso` midiera algo. Ahora el
    // catalogo son seis y las que se comprueban son las seis: si alguien reintroduce una
    // categoria sin tabla, esta linea se pone en rojo antes de que llegue a la pantalla.
    expect(CATEGORIAS_ECOSISTEMA.length).toBe(6);
    expect(CATEGORIAS_ECOSISTEMA.filter(categoria => !categoria.proceso)).toEqual([]);
  });

  /**
   * LA TARJETA SIN FUENTE DE RECUENTO NO PINTA CIFRA.
   *
   * Escenarios llegaba aqui con `spaces`, una clave que `loadMapData` rellenaba con
   * `data.luthierRecords`. El 30 de agosto de 2026 la tarjeta anunciaba 31 registros mientras
   * `GET /api/v1/escenarios` respondia `{"items":[],"total":0}`.
   *
   * SE COMPRUEBAN LAS DOS MITADES: que Escenarios y Lutería callen, y que Festivales conserve su
   * cifra porque sí tiene un directorio activo.
   */
  it('Escenarios no toma prestada la cifra de Luteria', () => {
    const root = fixture.nativeElement as HTMLElement;
    const escenarios = root.querySelector<HTMLElement>('[data-category="spaces"]')!;
    const luteria = root.querySelector<HTMLElement>('[data-category="luthier"]')!;
    const festivales = root.querySelector<HTMLElement>('[data-category="festivals"]')!;

    expect(escenarios.hasAttribute('data-sin-recuento')).toBeTrue();
    expect(escenarios.textContent).toContain('sin recuento publicado');
    expect(escenarios.textContent).not.toContain('3');

    expect(luteria.hasAttribute('data-sin-recuento')).toBeTrue();
    expect(luteria.textContent).toContain('sin recuento publicado');

    expect(festivales.hasAttribute('data-sin-recuento')).toBeFalse();
    expect(festivales.textContent?.trim()).toContain('3');
  });

  /**
   * LAS DOS ENTRADAS QUE PERMITEN REUSAR EL BLOQUE EN /ecosistema.
   *
   * Por defecto en `true`, que es como lo monta el Inicio. Si alguien invirtiera el valor por
   * defecto, el Inicio perderia su titulo y su boton sin que ninguna prueba del Inicio lo notara.
   */
  it('el encabezado y la salida se pueden apagar sin tocar el Inicio', () => {
    const componente = fixture.componentInstance;
    expect(componente.mostrarEncabezado).toBeTrue();
    expect(componente.mostrarSalida).toBeTrue();

    componente.mostrarEncabezado = false;
    componente.mostrarSalida = false;
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[data-testid="ecosystem-overview-cta"]')).toBeNull();
    expect(root.textContent).not.toContain('Ver en el mapa');
    // La rejilla, que es lo que se comparte, sigue entera.
    expect(root.querySelectorAll('[data-proceso-tarjeta]').length).toBe(6);
  });

  it('«Ver ecosistema» se pinta sólido y no como contorno translúcido', () => {
    // Estaba en `border-white/20` con texto blanco sobre el bloque #291242: se leía
    // como texto suelto y no como botón. Ahora es blanco sólido con letra #291242.
    // Se comprueban las dos mitades —lo que tiene y lo que ya no— porque añadir
    // `bg-white` sin quitar `text-white` dejaría letra blanca sobre fondo blanco.
    const clases = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="ecosystem-overview-cta"]')!.className;

    expect(clases).toContain('bg-white');
    expect(clases).toContain('text-[#291242]');
    expect(clases).not.toContain('bg-transparent');
    expect(clases).not.toContain('text-white');
  });

  it('ofrece dos salidas distintas: el mapa y la portada del ecosistema', () => {
    // SON DOS DESTINOS, NO UNO REPETIDO. «Ver en el mapa» emite la capa por un @Output que el
    // Home traduce a la ruta del mapa; «Ver ecosistema» navega a la portada. Si algun dia los
    // dos acabaran en el mismo sitio, uno de los botones estaria mintiendo.
    const navegacion = TestBed.inject(NavigationService);
    const enrutar = spyOn(navegacion, 'routerNavigate');
    const capas: string[] = [];
    fixture.componentInstance.navigateToMapLayer.subscribe(capa => capas.push(capa));

    const raiz = fixture.nativeElement as HTMLElement;
    raiz.querySelector<HTMLButtonElement>('[data-testid="ecosystem-overview-cta"]')!.click();

    expect(enrutar).toHaveBeenCalledOnceWith('ecosistema');
    expect(capas).toEqual([]);

    fixture.componentInstance.onNavigateToMapLayer('General');
    expect(capas).toEqual(['General']);
    expect(enrutar).toHaveBeenCalledTimes(1);
  });

  it('removes the participation and external call-to-action buttons', () => {
    const content = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(content).not.toContain('Ser parte del ecosistema');
    expect(content).not.toContain('Ir al ecosistema');
  });
});
