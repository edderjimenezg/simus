import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminAgendaPanelComponent } from './admin-agenda-panel.component';

/**
 * La Agenda ordena sus 471 eventos, no las doce filas que tiene delante.
 *
 * <b>EL DEFECTO QUE TRAJO ESTAS PRUEBAS.</b> Las cabeceras ordenaban el array cargado. Con doce
 * filas por página y cuarenta páginas, pulsar «Estado» reordenaba esas doce y dejaba las otras
 * 459 donde estaban: en la primera página no aparecía nunca un publicado por muchos clics que se
 * dieran, porque los publicados vivían de la página cuatro en adelante. La dirección de producto lo
 * reportó: «debe ser de todos, no solo de los de la página visible».
 *
 * <b>LO QUE FIJAN.</b> Que la columna viaja en la petición, que el sentido viaja con ella, que
 * ordenar devuelve a la primera página y que sin columna elegida no se manda orden ninguna —para
 * que el servidor responda con el orden de trabajo, sin publicar primero—.
 */
describe('la Agenda ordena en el servidor', () => {
  let componente: AdminAgendaPanelComponent;
  let http: HttpTestingController;

  /** Una página vacía: lo que se mira es la dirección de la petición, no lo que devuelve. */
  const PAGINA_VACIA = { items: [], pagina: 1, tamano: 12, total: 0, totalPaginas: 1 };

  function direccionDeLaPeticion(): string {
    const peticion = http.expectOne(r => r.url.includes('/institucional/agenda'));
    const url = peticion.request.urlWithParams;
    peticion.flush(PAGINA_VACIA);
    return url;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminAgendaPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    componente = TestBed.createComponent(AdminAgendaPanelComponent).componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  it('abre sin columna elegida, para que mande el orden de trabajo del servidor', () => {
    void componente.cargar();

    const url = direccionDeLaPeticion();
    expect(componente.orden.columna()).toBe('');
    expect(url).not.toContain('orden=');
    expect(url).not.toContain('direccion=');
  });

  it('manda la columna y el sentido al pulsar una cabecera', () => {
    componente.ordenarPor('estado');

    expect(direccionDeLaPeticion()).toContain('orden=estado');
  });

  it('el segundo golpe en la misma columna invierte el sentido', () => {
    componente.ordenarPor('actividad');
    expect(direccionDeLaPeticion()).toContain('direccion=asc');

    componente.ordenarPor('actividad');
    expect(direccionDeLaPeticion()).toContain('direccion=desc');
  });

  it('ordenar devuelve a la primera página', () => {
    // La página treinta de un orden no contiene lo mismo que la página treinta del otro.
    componente.totalDePaginas.set(40);
    componente.irALaPagina(30);
    direccionDeLaPeticion();
    expect(componente.pagina()).toBe(30);

    componente.ordenarPor('cuando');

    expect(componente.pagina()).toBe(1);
    expect(direccionDeLaPeticion()).toContain('pagina=1');
  });

  it('no reordena nada en el navegador: pinta la página como vino', () => {
    // EL SERVIDOR YA LA ORDENO. Volver a ordenarla aquí solo puede desordenarla: aquí hay doce
    // filas y allí 471, así que cualquier criterio local contradiría al que las eligió.
    const comoVino = [
      { id: 3, titulo: 'Zamba' },
      { id: 1, titulo: 'Abrazo' },
    ] as unknown as Parameters<typeof componente.eventos.set>[0];
    componente.eventos.set(comoVino);

    expect(componente.eventosVisibles().map(e => e.id)).toEqual([3, 1]);
  });

  afterEach(() => {
    // La carga de categorías es otra petición y no es lo que aquí se mira.
    http.match(() => true).forEach(p => p.flush({ items: [] }));
    http.verify();
  });
});
