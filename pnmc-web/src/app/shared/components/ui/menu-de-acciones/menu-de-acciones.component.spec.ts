import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AccionDeRegistro, MenuDeAccionesComponent } from './menu-de-acciones.component';

/**
 * El menú de acciones de una fila.
 *
 * <b>Lo que fija.</b> Que la acción principal salga fuera del desplegable —lo más frecuente no
 * cuesta dos clics—, que las irreversibles queden separadas al final, que elegir una la emita y
 * cierre el menú, y que el teclado no se quede atrapado dentro: sin esa última mitad, cambiar seis
 * botones por un desplegable mejoraría la pantalla para unos y la cerraría para otros.
 */
describe('MenuDeAccionesComponent', () => {
  let fixture: ComponentFixture<MenuDeAccionesComponent>;
  let componente: MenuDeAccionesComponent;

  const ACCIONES: AccionDeRegistro[] = [
    { id: 'publicar', etiqueta: 'Publicar', tono: 'principal' },
    { id: 'editar', etiqueta: 'Editar' },
    { id: 'historial', etiqueta: 'Historial' },
    { id: 'archivar', etiqueta: 'Archivar', tono: 'peligro' },
  ];

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MenuDeAccionesComponent] }).compileComponents();
    fixture = TestBed.createComponent(MenuDeAccionesComponent);
    componente = fixture.componentInstance;
    componente.acciones = ACCIONES;
    componente.descripcion = 'Festival del Agua';
    fixture.detectChanges();
  });

  it('cerrado, la fila enseña un solo control y ninguna acción', () => {
    // FIJABA LO CONTRARIO HASTA EL 14 DE SEPTIEMBRE DE 2026: que «Publicar» se viera fuera del
    // menú, «porque lo más frecuente no debería costar dos clics». Con nueve tablas usando esta
    // pieza, ese segundo botón dejaba la columna de acciones distinta en cada fila —unas con dos
    // controles y otras con uno, con anchos que cambiaban según el verbo— y la dirección de producto
    // pidió uniformarla. La acción esperada no desaparece: es la primera del menú y se lee
    // destacada, y eso lo fija la prueba de abajo.
    expect(raiz().querySelector('[data-accion-principal]')).toBeNull();
    expect(raiz().querySelector('[data-menu-de-acciones]')).toBeNull();
    expect(raiz().querySelectorAll('button').length).toBe(1);
  });

  it('el disparador dice de qué registro son las acciones', () => {
    // «Acciones» repetido treinta veces en una tabla no orienta a quien no ve la pantalla.
    const disparador = raiz().querySelector('[data-disparador-acciones]')!;

    expect(disparador.getAttribute('aria-label')).toBe('Acciones de Festival del Agua');
    expect(disparador.getAttribute('aria-haspopup')).toBe('menu');
    expect(disparador.getAttribute('aria-expanded')).toBe('false');
  });

  it('abre el desplegable con el resto de acciones y deja las irreversibles al final', () => {
    raiz().querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();

    const opciones = Array.from(raiz().querySelectorAll('[data-opcion-accion]'))
      .map(o => (o.textContent ?? '').trim());

    // LA ESPERADA VA PRIMERA Y SEPARADA; «Archivar», irreversible, la última y también separada.
    expect(opciones).toEqual(['Publicar', 'Editar', 'Historial', 'Archivar']);
    expect(raiz().querySelector('[data-accion-principal]')!.textContent!.trim()).toBe('Publicar');
    expect(raiz().querySelectorAll('[data-menu-de-acciones] hr').length).toBe(2);
    expect(raiz().querySelector('[data-disparador-acciones]')!.getAttribute('aria-expanded')).toBe('true');
  });

  it('elegir una acción la emite y cierra el menú', () => {
    const elegidas: string[] = [];
    componente.elegida.subscribe(id => elegidas.push(id));

    raiz().querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();
    Array.from(raiz().querySelectorAll<HTMLButtonElement>('[data-opcion-accion]'))
      .find(o => o.textContent!.includes('Editar'))!.click();
    fixture.detectChanges();

    expect(elegidas).toEqual(['editar']);
    // DEJARLO ABIERTO taparía el resultado de lo que se acaba de pedir.
    expect(raiz().querySelector('[data-menu-de-acciones]')).toBeNull();
  });

  it('una acción deshabilitada no se emite y explica por qué', () => {
    componente.acciones = [{ id: 'publicar', etiqueta: 'Publicar', deshabilitada: true, motivo: 'Guardando…' }];
    fixture.detectChanges();
    const elegidas: string[] = [];
    componente.elegida.subscribe(id => elegidas.push(id));

    raiz().querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();
    const opcion = raiz().querySelector<HTMLButtonElement>('[data-opcion-accion]')!;

    expect(opcion.disabled).toBeTrue();
    expect(opcion.getAttribute('title')).toBe('Guardando…');
    opcion.click();
    expect(elegidas).toEqual([]);
  });

  it('elegir una acción devuelve el foco al disparador ANTES de emitir, para que la ventana que abra sepa a quién volver', () => {
    // La opción pulsada desaparece con el desplegable. Si el foco se queda en ella, la ventana que
    // la acción abre toma como «quien la abrió» un elemento que ya no existe y, al cerrarse, el
    // foco cae al cuerpo de la página. Medido con el historial.
    let activoAlEmitir: Element | null = null;
    componente.elegida.subscribe(() => { activoAlEmitir = document.activeElement; });
    componente.alternar();
    fixture.detectChanges();

    componente.elegir({ id: 'historial', etiqueta: 'Historial' });

    expect(activoAlEmitir).toBe(fixture.nativeElement.querySelector('[data-disparador-acciones]'));
  });

  it('escape cierra el menú y devuelve el foco al disparador', () => {
    const disparador = raiz().querySelector<HTMLButtonElement>('[data-disparador-acciones]')!;
    disparador.click();
    fixture.detectChanges();

    componente.alPulsarEscape(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    // SIN ESTO EL TECLADO SE QUEDA ATRAPADO: el menú se cierra y el foco vuelve de donde salió.
    expect(raiz().querySelector('[data-menu-de-acciones]')).toBeNull();
    expect(document.activeElement).toBe(disparador);
  });

  it('con una sola acción sigue habiendo menú, y sin ninguna no hay control', () => {
    // ANTES, UNA FILA CON SOLO LA ESPERADA NO PINTABA DISPARADOR porque esa acción salía fuera.
    // Ahora todo vive dentro, así que una sola acción también necesita su menú: sin él la fila se
    // quedaría sin ninguna manera de actuar.
    componente.acciones = [{ id: 'publicar', etiqueta: 'Publicar', tono: 'principal' }];
    fixture.detectChanges();
    expect(raiz().querySelector('[data-disparador-acciones]')).not.toBeNull();

    // UN DESPLEGABLE VACIO ES UNA PROMESA INCUMPLIDA: si no hay nada dentro, no hay botón.
    componente.acciones = [];
    fixture.detectChanges();
    expect(raiz().querySelector('[data-disparador-acciones]')).toBeNull();
  });
});
