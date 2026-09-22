import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminCategoriasPanelComponent } from './admin-categorias-panel.component';
import { CategoriaDeContenido, CategoriasDeContenidoService } from '../../../core/services/categorias-de-contenido.service';
import { ProyectosTransversalesService } from '../../../core/services/proyectos-transversales.service';

/**
 * Categorías y proyectos del Programa.
 *
 * <b>POR QUE EXISTEN AHORA.</b> El panel no tenía ninguna prueba, y en el corte v172 sus tres
 * diálogos —nueva categoría, fusión y proyecto— pasaron a `app-dialogo-de-formulario`.
 *
 * <b>LO QUE FIJAN, Y POR QUE IMPORTA.</b> La fusión es la operación delicada del panel: mueve el
 * contenido de una categoría a otra y retira la primera. Estas pruebas fijan que el destino se
 * elige y no se supone, que una categoría no puede fusionarse consigo misma, que se puede elegir un
 * destino de OTRO módulo —que es como una categoría propia se convierte en común sin perder lo
 * publicado— y que el aviso nombra las dos.
 */
describe('categorías y proyectos del Programa', () => {
  let fixture: ComponentFixture<AdminCategoriasPanelComponent>;
  let componente: AdminCategoriasPanelComponent;
  let fusionado: { id: number; destinoId: number } | null;

  const CATEGORIAS: CategoriaDeContenido[] = [
    { id: 1, codigoModulo: 'noticias', nombreCategoria: 'Convocatorias', slug: 'convocatorias', descripcion: null, ordenVisualizacion: 1, contenidosQueLaUsan: 4 },
    { id: 2, codigoModulo: 'noticias', nombreCategoria: 'Convocatoria', slug: 'convocatoria', descripcion: null, ordenVisualizacion: 2, contenidosQueLaUsan: 1 },
    { id: 3, codigoModulo: 'agenda', nombreCategoria: 'Talleres', slug: 'talleres', descripcion: 'Formación', ordenVisualizacion: 1, contenidosQueLaUsan: 7 },
  ];

  const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(async () => {
    fusionado = null;
    const categorias: Partial<CategoriasDeContenidoService> = {
      listar: (async () => ({ ok: true, data: { items: CATEGORIAS } })) as CategoriasDeContenidoService['listar'],
      fusionar: (async (id: number, destinoId: number) => { fusionado = { id, destinoId }; return { ok: true }; }) as CategoriasDeContenidoService['fusionar'],
    };
    const proyectos: Partial<ProyectosTransversalesService> = {
      listar: (async () => ({ ok: true, data: { items: [] } })) as ProyectosTransversalesService['listar'],
    };

    await TestBed.configureTestingModule({
      imports: [AdminCategoriasPanelComponent],
      providers: [
        { provide: CategoriasDeContenidoService, useValue: categorias },
        { provide: ProyectosTransversalesService, useValue: proyectos },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminCategoriasPanelComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('agrupa las categorías por el módulo donde se ofrecen', () => {
    expect(componente.total()).toBe(3);
    const porModulo = componente.grupos().map(g => `${g.codigo}:${g.categorias.length}`);
    expect(porModulo).toContain('noticias:2');
    expect(porModulo).toContain('agenda:1');
  });

  it('la fusión no ofrece la propia categoría como destino, y sí las de otros módulos', () => {
    componente.pedirFusion(CATEGORIAS[1]);

    const destinos = componente.destinosPosibles().map(c => c.id);
    // MUTANTE QUE MATA: dejar que una categoría se fusione consigo misma. El servidor lo rechazaría,
    // pero el desplegable no debería ni ofrecerlo.
    expect(destinos).not.toContain(2);
    // Y SE PUEDE ELEGIR UNA DE OTRO MODULO: así es como una categoría propia se vuelve común.
    expect(destinos).toContain(3);
  });

  it('sin destino elegido no se fusiona nada', async () => {
    componente.pedirFusion(CATEGORIAS[1]);

    await componente.fusionar();

    expect(fusionado).toBeNull();
    expect(componente.fusionando()).not.toBeNull();
  });

  it('con destino, fusiona y el aviso nombra las dos categorías', async () => {
    componente.pedirFusion(CATEGORIAS[1]);
    componente.destinoDeFusion.set(1);

    await componente.fusionar();

    expect(fusionado).toEqual({ id: 2, destinoId: 1 });
    expect(componente.aviso()).toContain('«Convocatoria»');
    expect(componente.aviso()).toContain('«Convocatorias»');
    expect(componente.fusionando()).toBeNull();
  });

  it('el formulario de una categoría nueva nace en el módulo desde el que se pidió', () => {
    componente.nueva('editorial');

    expect(componente.formulario()).toEqual({ id: null, codigoModulo: 'editorial', nombreCategoria: '', descripcion: '' });
    fixture.detectChanges();
    expect(texto()).toContain('Nueva categoría');
  });

  it('editar abre el formulario con lo que la categoría ya tiene', () => {
    componente.editar(CATEGORIAS[2]);

    // MUTANTE QUE MATA: abrirlo vacío. Guardar entonces borraría la descripción sin que nadie lo
    // pidiera, que es el mismo defecto que ya se corrigió en las ediciones de un mercado.
    expect(componente.formulario()?.nombreCategoria).toBe('Talleres');
    expect(componente.formulario()?.descripcion).toBe('Formación');
  });

  it('una categoría sin nombre no se guarda, y se dice por qué', async () => {
    componente.nueva('noticias');

    await componente.guardar();

    expect(componente.error()).toBe('Escribe el nombre de la categoría.');
    expect(componente.formulario()).not.toBeNull();
  });
});
