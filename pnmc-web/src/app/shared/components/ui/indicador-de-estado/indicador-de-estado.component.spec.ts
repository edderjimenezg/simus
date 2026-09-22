import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IndicadorDeEstadoComponent } from './indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from './tono-del-estado';

/**
 * El indicador de estado.
 *
 * <b>Lo que fija.</b> Que sustituir las píldoras por iconografía —lo que pidió el usuario el 12 de
 * septiembre de 2026— no pierda el significado por el camino: el texto sigue en el DOM para la
 * tecnología asistiva, aparece al pasar por encima, y el icono no se anuncia dos veces.
 */
describe('IndicadorDeEstadoComponent', () => {
  let fixture: ComponentFixture<IndicadorDeEstadoComponent>;
  let componente: IndicadorDeEstadoComponent;

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [IndicadorDeEstadoComponent] }).compileComponents();
    fixture = TestBed.createComponent(IndicadorDeEstadoComponent);
    componente = fixture.componentInstance;
    componente.etiqueta = 'Publicado';
    componente.tono = 'logrado';
    fixture.detectChanges();
  });

  it('no pinta una píldora: dibuja un icono', () => {
    const indicador = raiz().querySelector('[data-indicador-de-estado]')!;

    // LA PILDORA ERA EL RECUADRO REDONDEADO DE COLOR con el estado escrito dentro. Es justo lo que
    // Se define retirar de todo el diseño.
    expect(indicador.className).not.toContain('rounded-full');
    expect(indicador.querySelector('svg')).not.toBeNull();
  });

  it('el texto sigue disponible para quien no ve la pantalla', () => {
    // CAMBIAR EL TEXTO POR UN ICONO MUDO seria una regresion, no una mejora: empeoraria la pantalla
    // justo para quien mas la necesita.
    const oculto = raiz().querySelector('.sr-only')!;
    expect(oculto.textContent!.trim()).toBe('Publicado');

    // Y EL ICONO NO LO REPITE: si tambien se anunciara, el estado se leeria dos veces seguidas.
    expect(raiz().querySelector('svg')!.getAttribute('aria-hidden')).toBe('true');
  });

  it('al pasar por encima se lee el texto', () => {
    expect(raiz().querySelector('[data-tooltip-de-estado]')).toBeNull();

    raiz().querySelector('[data-indicador-de-estado]')!.dispatchEvent(new MouseEvent('mouseenter'));
    fixture.detectChanges();

    const tooltip = raiz().querySelector('[data-tooltip-de-estado]')!;
    expect(tooltip.textContent!.trim()).toBe('Publicado');
    expect(tooltip.getAttribute('role')).toBe('tooltip');
  });

  it('con texto al lado no se repite en un tooltip', () => {
    componente.conTexto = true;
    fixture.detectChanges();
    raiz().querySelector('[data-indicador-de-estado]')!.dispatchEvent(new MouseEvent('mouseenter'));
    fixture.detectChanges();

    // REPETIR LO QUE SE LEE AL LADO es ruido, no ayuda.
    expect(raiz().textContent).toContain('Publicado');
    expect(raiz().querySelector('[data-tooltip-de-estado]')).toBeNull();
  });

  it('no añade una parada de tabulación', () => {
    // TREINTA FILAS SERIAN TREINTA PARADAS que atravesar para llegar a las acciones. El significado
    // llega igual por la etiqueta oculta.
    expect(raiz().querySelector('[tabindex]')).toBeNull();
  });
});

/**
 * El criterio de tono, que es único para todo el proyecto.
 *
 * <b>Lo que fija.</b> Que el mismo concepto no acabe en verde en una pantalla y en gris en otra, y
 * que los dos vocabularios que conviven en el proyecto —PascalCase en el contrato externo de
 * Festival, minúsculas en la base— se lean los dos.
 */
describe('tonoDelEstado', () => {
  it('lee los dos vocabularios que conviven en el proyecto', () => {
    expect(tonoDelEstado('AjustesSolicitados')).toBe('requiere_accion');
    expect(tonoDelEstado('ajustes_solicitados')).toBe('requiere_accion');
    expect(tonoDelEstado('Publicado')).toBe('logrado');
    expect(tonoDelEstado('publicado')).toBe('logrado');
  });

  it('el mismo concepto tiene el mismo tono aunque se llame distinto', () => {
    // «Publicado» es de un Festival y «activa» de una organización: sujetos distintos, situación
    // equivalente —algo terminado y en pie—.
    expect(tonoDelEstado('activa')).toBe(tonoDelEstado('publicado'));
    expect(tonoDelEstado('publicada')).toBe(tonoDelEstado('publicado'));
  });

  it('un estado desconocido no se pinta como un logro ni como un error', () => {
    expect(tonoDelEstado('lo_que_sea')).toBe('preliminar');
    expect(tonoDelEstado(null)).toBe('preliminar');
  });

  it('inactiva y archivada comparten tono pero no icono', () => {
    // LAS DOS DEJAN EL REGISTRO FUERA, pero una suspende y se deshace y la otra lo guarda. El icono
    // es lo que las separa.
    expect(tonoDelEstado('inactiva')).toBe(tonoDelEstado('archivada'));
    expect(matizDelEstado('inactiva')).toBe('pausa');
    expect(matizDelEstado('archivada')).toBe('archivo');
    expect(matizDelEstado('eliminada')).toBe('eliminado');
  });
});
