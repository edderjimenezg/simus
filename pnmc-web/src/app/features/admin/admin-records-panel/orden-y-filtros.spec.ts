import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminRecordsPanelComponent } from './admin-records-panel.component';
import { AdminService } from '../../../core/services/admin.service';
import { SessionService } from '../../../core/services/session.service';
import { ADMIN_MODULES } from '../domain/admin-config';

/**
 * El orden y los filtros de la tabla de los seis procesos.
 *
 * <b>El defecto que estas pruebas fijan.</b> Hasta estas cabeceras
 * ordenaban en el navegador. La ruta devuelve una página de como mucho cien filas de hasta
 * quinientas: la cabecera decía «Nombre ↑» y lo que ordenaba eran las cien traídas, así que el
 * registro alfabéticamente primero podía estar en la fila ciento uno y no aparecer nunca arriba.
 *
 * <b>Lo que se comprueba aquí y lo que no.</b> Aquí, que la pantalla PIDE el orden y los filtros, y
 * que pinta la flecha con lo que el servidor contestó. Que el servidor ordene bien lo comprueba
 * las pruebas de la API — sobre ciento veinte registros,
 * que es donde la diferencia se ve.
 */
describe('AdminRecordsPanelComponent · orden y filtros', () => {
  let fixture: ComponentFixture<AdminRecordsPanelComponent>;
  let comp: AdminRecordsPanelComponent;
  let pedido: any = null;
  let respuesta: any;

  const RESPUESTA = {
    items: [
      { id: '1', title: 'Escuela A', status: 'en_revision', department: 'ANTIOQUIA', municipality: 'MEDELLÍN', updatedAt: '2026-08-25' },
      { id: '2', title: 'Escuela B', status: 'publicado', department: 'ANTIOQUIA', municipality: 'MEDELLÍN', updatedAt: '2026-08-24' },
    ],
    limit: 100,
    offset: 0,
    total: 2,
    estados: [
      { codigo: 'publicado', etiqueta: 'Publicado', total: 1 },
      { codigo: 'en_revision', etiqueta: 'En revision', total: 1 },
    ],
    territorios: [
      { codigo: 'ANTIOQUIA', etiqueta: 'ANTIOQUIA', total: 2 },
      { codigo: 'sin_territorio', etiqueta: 'Sin territorio', total: 0 },
    ],
    orden: 'prioridad',
    direccion: 'asc',
  };

  beforeEach(async () => {
    pedido = null;
    respuesta = RESPUESTA;

    const adminStub = {
      cargarRegistrosDeLaConsola: (p: any) => {
        pedido = p;
        return of(respuesta);
      },
      cargarEsquemaDeLaBase: () => of({}),
    };

    await TestBed.configureTestingModule({
      imports: [AdminRecordsPanelComponent],
      providers: [
        { provide: AdminService, useValue: adminStub },
        { provide: SessionService, useValue: { session: () => null } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRecordsPanelComponent);
    comp = fixture.componentInstance;
    comp.module = ADMIN_MODULES.find((m) => m.id === 'musicSchools') ?? ADMIN_MODULES[0];
    comp.roleId = 'webmaster';
    comp.divipola = {};
    fixture.detectChanges();
    // `loadRecords` lo dispara `ngOnChanges`, que no salta al asignar la entrada a mano.
    comp.loadRecords();
    fixture.detectChanges();
  });

  const raiz = () => fixture.nativeElement as HTMLElement;
  const cabecera = (id: string) => raiz().querySelector(`[data-ordenar="${id}"]`)!.closest('th')!;

  it('pulsar una cabecera le pide ese orden al servidor, y no reordena lo que ya tiene', () => {
    raiz().querySelector<HTMLButtonElement>('[data-ordenar="titulo"]')!.click();

    expect(pedido.orden).toBe('titulo');
    expect(pedido.direccion).toBe('asc');
  });

  it('pulsar dos veces la misma cabecera le da la vuelta, y pasar a otra vuelve a ascendente', () => {
    const pulsar = (id: string) => raiz().querySelector<HTMLButtonElement>(`[data-ordenar="${id}"]`)!.click();

    pulsar('titulo');
    expect(pedido.direccion).toBe('asc');
    pulsar('titulo');
    expect(pedido.direccion).toBe('desc');

    // Cambiar de columna NO conserva el «desc»: nadie pide una columna nueva para verla del revés.
    pulsar('territorio');
    expect(pedido.orden).toBe('territorio');
    expect(pedido.direccion).toBe('asc');
  });

  it('la flecha y el aria-sort marcan la columna que el servidor ordenó, no la que se pidió', () => {
    // Si se pide una columna que el servidor no admite, este se cae al orden por omisión. Pintando
    // la flecha con lo pedido quedaría una flecha sobre una cabecera que no ordenó nada.
    respuesta = { ...RESPUESTA, orden: 'actualizacion', direccion: 'desc' };
    raiz().querySelector<HTMLButtonElement>('[data-ordenar="titulo"]')!.click();
    fixture.detectChanges();

    expect(comp.ordenPedido()).toBe('titulo');
    expect(comp.ordenAplicado()).toBe('actualizacion');
    // La flecha la dibuja la pieza compartida de tablas desde, y este
    // panel la sincroniza con lo que el servidor DIJO que ordenó, no con lo que se le pidió.
    expect(comp.orden.flechaDe('titulo')).toBe('↕');
    expect(comp.orden.flechaDe('actualizacion')).toBe('↓');
    expect(cabecera('titulo').getAttribute('aria-sort')).toBe('none');
    expect(cabecera('actualizacion').getAttribute('aria-sort')).toBe('descending');
  });

  it('cada cabecera que ordena es un botón, para que el tabulador y el Enter también ordenen', () => {
    // Antes eran `th` con un `click` encima: la tabla quedaba ordenable solo con ratón.
    const conOrden = Array.from(raiz().querySelectorAll('thead th')).filter((th) => th.querySelector('[data-ordenar]'));

    expect(conOrden.length).toBe(4);
    expect(conOrden.every((th) => th.getAttribute('scope') === 'col')).toBeTrue();
    expect(conOrden.every((th) => th.querySelector('button[type="button"]') !== null)).toBeTrue();
  });

  /**
   * Abre un filtro y elige una de sus opciones, por el DOM.
   *
   * LOS FILTROS DEJARON DE SER `<select>`: el panel de un desplegable
   * nativo lo dibuja el sistema operativo y con los 32 departamentos ocupaba media pantalla, sin
   * forma de acotarlo ni de darle buscador. Lo que estas pruebas vigilan —qué viaja al servidor—
   * no cambia; cambia por dónde se pulsa.
   */
  function elegirEnElFiltro(cual: string, opcion: string): void {
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    const boton = Array.from(raiz().querySelectorAll<HTMLButtonElement>('.filtro__opcion'))
      .find((b) => (b.textContent || '').replace(/\s+/g, ' ').trim().startsWith(opcion));
    if (!boton) { throw new Error(`No hay opción «${opcion}» en el filtro «${cual}».`); }
    boton.click();
    fixture.detectChanges();
  }

  /** Lo que ofrece un filtro, abierto. */
  function opcionesDelFiltro(cual: string): string[] {
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    const textos = Array.from(raiz().querySelectorAll<HTMLButtonElement>('.filtro__opcion'))
      .map((b) => (b.textContent || '').replace(/\s+/g, ' ').trim());
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    return textos;
  }

  it('el filtro de estado viaja al servidor, y «todos» es la ausencia de filtro', () => {
    // El recuento va aparte del rótulo, no entre paréntesis: es una columna propia del panel, y
    // por eso el texto plano de la opción sale pegado.
    expect(opcionesDelFiltro('estado')).toEqual([
      'Todos los estados2',
      'Publicado1',
      'En revision1',
    ]);

    elegirEnElFiltro('estado', 'En revision');
    expect(pedido.estado).toBe('en_revision');

    elegirEnElFiltro('estado', 'Todos los estados');
    expect(pedido.estado).toBeUndefined();
  });

  it('el filtro de territorio viaja al servidor con el nombre del departamento', () => {
    elegirEnElFiltro('territorio', 'Antioquia');

    expect(pedido.departamento).toBe('ANTIOQUIA');
    expect(comp.departamentoActivo()).toBe('ANTIOQUIA');
  });

  it('el departamento elegido sigue en el desplegable aunque se quede sin filas', () => {
    respuesta = { ...RESPUESTA, items: [], total: 0, territorios: [{ codigo: 'sin_territorio', etiqueta: 'Sin territorio', total: 0 }] };
    elegirEnElFiltro('territorio', 'Antioquia');

    expect(comp.opcionesDeTerritorio().map((o: any) => o.codigo)).toContain('ANTIOQUIA');
  });

  it('el tercer golpe en una cabecera devuelve a «Pendientes primero»', () => {
    // <b>ESTO LO HACIA UN BOTON DE LA BARRA Y AHORA LO HACE LA CABECERA.</b> «Pendientes primero»
    // vivía junto a los filtros, con su mismo aspecto, aunque no filtra nada: ordena. El dueño del
    // proyecto pidió «buscar una alternativa», y la alternativa es
    // volver por donde se salió. La flecha vuelve a «↕» en las cuatro columnas, que es la verdad:
    // la lista no está ordenada por ninguna de ellas.
    const cabecera = () => raiz().querySelector<HTMLButtonElement>('[data-ordenar="titulo"]')!;

    cabecera().click();
    expect(pedido.orden).toBe('titulo');
    expect(pedido.direccion).toBe('asc');

    cabecera().click();
    expect(pedido.direccion).toBe('desc');

    cabecera().click();
    expect(pedido.orden).toBe('prioridad');
    expect(pedido.direccion).toBe('asc');
  });

  it('cambiar de módulo limpia el filtro, que puede no existir en el módulo nuevo', () => {
    // El estado «en revisión» de Festivales puede no existir en Escuelas, y el departamento
    // tampoco: arrastrarlos dejaba la tabla nueva vacía sin decir por qué.
    elegirEnElFiltro('estado', 'En revision');
    expect(comp.estadoActivo()).toBe('en_revision');

    comp.module = ADMIN_MODULES.find((m) => m.id === 'festivals') ?? ADMIN_MODULES[0];
    comp.resetStateAndLoad();

    expect(comp.estadoActivo()).toBe('todos');
    expect(comp.departamentoActivo()).toBe('todos');
    expect(comp.ordenPedido()).toBe('prioridad');
    expect(pedido.estado).toBeUndefined();
  });
});
