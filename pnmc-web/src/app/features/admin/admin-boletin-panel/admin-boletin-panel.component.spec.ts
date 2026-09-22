import { appConfig } from '../../../app.config';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AdminBoletinPanelComponent } from './admin-boletin-panel.component';
import { ADMIN_MODULES } from '../domain/admin-config';

/*
  LO QUE SE AFIRMA AQUI: que el panel de Comunicaciones lee la lista real del API, que sus cifras
  no se inventan, que la baja no borra, y que el CSV —el puente hacia el mailing— sale de una
  descarga y no de un texto pegado en pantalla.
*/
describe('AdminBoletinPanelComponent', () => {
  // LAS CIFRAS Y FECHAS SE ESCRIBEN EN es-CO, y el locale lo registra `app.config.ts` al cargarse.
  // Nombrar `appConfig` obliga a cargar ese módulo; sin esto la prueba solo pasa cuando corre junto
  // a otra que lo cargue, que es una dependencia del orden de ejecución y no de lo que se prueba.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  let fixture: ComponentFixture<AdminBoletinPanelComponent>;
  let http: HttpTestingController;

  const LISTADO = {
    items: [
      { id: 7, correo: 'ana@pnmc.test', origen: 'portada', estado: 'activa', fechaAlta: '2026-08-28T10:00:00', fechaBaja: null, autorizacionTexto: 'Autorizo…' },
      { id: 8, correo: 'luis@pnmc.test', origen: 'portada', estado: 'baja', fechaAlta: '2026-08-20T10:00:00', fechaBaja: '2026-08-27T10:00:00', autorizacionTexto: 'Autorizo…' },
    ],
    total: 2,
    activas: 1,
    limit: 25,
    offset: 0,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminBoletinPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminBoletinPanelComponent);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const buscar = <T extends HTMLElement>(id: string) =>
    (fixture.nativeElement as HTMLElement).querySelector<T>(`[data-testid="${id}"]`);
  const todos = (id: string) =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(`[data-testid="${id}"]`));

  /**
   * Abre el menu de la primera fila y elige «Dar de baja».
   *
   * La accion vive en el menu de la fila desde, como en el resto de la
   * consola. Se hace por el DOM real -disparador y opcion- y no llamando al metodo, para que la
   * prueba siga vigilando que la opcion existe y esta enganchada.
   */
  function darDeBajaLaPrimeraFila(): void {
    const fila = todos('boletin-panel-fila')[0];
    fila.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();
    const opcion = Array.from(fila.querySelectorAll<HTMLButtonElement>('[data-opcion-accion]'))
      .find(boton => (boton.textContent || '').includes('Dar de baja'))!;
    opcion.click();
    fixture.detectChanges();
  }

  /** El botón que confirma, dentro del diálogo del proyecto. */
  function confirmarLaBaja(): void {
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="confirmar-baja-de-boletin"]')!.click();
    fixture.detectChanges();
  }

  function arrancar(respuesta: object = LISTADO): void {
    fixture.detectChanges();
    const peticion = http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/');
    peticion.flush(respuesta);
    fixture.detectChanges();
  }

  it('consulta la lista al abrirse y pinta una fila por suscripcion', () => {
    fixture.detectChanges();

    const peticion = http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/');
    expect(peticion.request.method).toBe('GET');
    expect(peticion.request.params.get('limit')).toBe('25');
    expect(peticion.request.params.get('offset')).toBe('0');

    peticion.flush(LISTADO);
    fixture.detectChanges();

    const filas = todos('boletin-panel-fila');
    expect(filas.length).toBe(2);
    expect(filas[0].textContent).toContain('ana@pnmc.test');
    expect(filas[1].textContent).toContain('luis@pnmc.test');
  });

  it('las tres cifras son las del servidor, no un recuento de lo que cabe en pantalla', () => {
    // SI SE CONTARAN LAS FILAS VISIBLES, con 25 por pagina el panel diria «25 suscripciones»
    // teniendo mil. Las cifras vienen de `total` y `activas`, que el API calcula sobre la
    // consulta entera.
    arrancar();

    expect(buscar('boletin-panel-total')!.textContent!.trim()).toBe('2');
    expect(buscar('boletin-panel-activas')!.textContent!.trim()).toBe('1');
    expect(buscar('boletin-panel-bajas')!.textContent!.trim()).toBe('1');
  });

  it('con el filtro puesto en activas no enseña un numero de bajas que no puede saber', () => {
    // CON EL FILTRO EN «activa», `total` ya son solo las activas y la resta daria cero. Cero
    // bajas es una afirmacion falsa, asi que se escribe una raya.
    arrancar();

    // EL FILTRO DEJO DE SER UN `<select>`, para que su panel lo dibuje
    // la consola y no el sistema operativo. Lo que se vigila —que el estado viaje al servidor— no
    // cambia; cambia por dónde se pulsa.
    const raiz = fixture.nativeElement as HTMLElement;
    raiz.querySelector<HTMLButtonElement>('[data-filtro="boletin-panel-estado"]')!.click();
    fixture.detectChanges();
    Array.from(raiz.querySelectorAll<HTMLButtonElement>('.filtro__opcion'))
      .find(b => (b.textContent || '').trim().startsWith('Activas'))!.click();
    fixture.detectChanges();

    const peticion = http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/');
    expect(peticion.request.params.get('estado')).toBe('activa');
    peticion.flush({ ...LISTADO, items: [LISTADO.items[0]], total: 1, activas: 1 });
    fixture.detectChanges();

    expect(buscar('boletin-panel-bajas')!.textContent!.trim()).toBe('—');
  });

  it('solo ofrece dar de baja a quien esta activa', () => {
    // A quien ya esta de baja no se le puede volver a dar de baja: su fila no ofrece nada.
    //
    // LA ACCION VIVE EN EL MENU DE LA FILA desde, como en el resto de
    // la consola: era un boton suelto que solo aparecia en unas filas, asi que la ultima columna
    // cambiaba de ancho de una fila a la siguiente.
    arrancar();

    const filas = todos('boletin-panel-fila');
    expect(filas[0].querySelector('[data-disparador-acciones]')).not.toBeNull();
    expect(filas[1].querySelector('[data-disparador-acciones]')).toBeNull();
  });

  it('la baja se pide al API y despues vuelve a consultar la lista', () => {
    arrancar();

    darDeBajaLaPrimeraFila();
    // PIDE ANTES DE HACER: elegir la acción solo abre la confirmación.
    http.expectNone('/api/v1/admin/comunicaciones/boletin/7/baja');
    confirmarLaBaja();

    const baja = http.expectOne('/api/v1/admin/comunicaciones/boletin/7/baja');
    expect(baja.request.method).toBe('POST');
    baja.flush({ message: 'Suscripcion dada de baja.' });
    fixture.detectChanges();

    // RECONSULTA. Sin ella la fila seguiria diciendo «activa» y quien mira creeria que no paso
    // nada, o peor, volveria a pulsar.
    http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/').flush(LISTADO);
    fixture.detectChanges();

    expect(buscar('boletin-panel-aviso')!.textContent).toContain('dada de baja');
  });

  it('si se cancela la confirmacion no se pide nada', () => {
    arrancar();

    darDeBajaLaPrimeraFila();
    fixture.componentInstance.cerrarLaConfirmacion();
    fixture.detectChanges();

    http.expectNone('/api/v1/admin/comunicaciones/boletin/7/baja');
    expect(fixture.nativeElement.querySelector('[data-testid="confirmar-baja-de-boletin"]')).toBeNull();
  });

  it('el CSV se descarga del API con el filtro puesto, y no se fabrica en el navegador', () => {
    // ES EL PUENTE HACIA EL MAILING. Que lo genere el servidor importa: alli estan el BOM y el
    // escapado de formulas, y una copia hecha aqui se saltaria los dos.
    //
    // EL CLIC DEL ENLACE SE INTERCEPTA, Y NO ES COSMETICA. Sin esto la prueba descarga un fichero
    // DE VERDAD: el componente crea un `<a download>` y lo pulsa, y el Chrome que corre Karma
    // obedece y escribe en la carpeta de Descargas de quien ejecuta las pruebas. El dueño del
    // proyecto se encontro DOCE «boletin-pnmc (N).csv» de siete bytes sin saber de donde salian;
    // el metadato de procedencia de macOS los delato: `http://localhost:9876/context.html`, que es
    // el iframe de Karma, y los siete bytes son el `correo\n` que esta misma linea simula mas
    // abajo. Cada corrida de `ng test` dejaba uno.
    //
    // SE ESPIA EL PROTOTIPO Y NO `createElement`, porque lo que hay que impedir es el efecto —que
    // el navegador descargue— y no la construccion del enlace, que es justo lo que se quiere medir.
    const pulsar = spyOn(HTMLAnchorElement.prototype, 'click');
    arrancar();

    // EL BUSCADOR AVISA AL DEJAR DE ESCRIBIR, no en cada tecla: sin eso, teclear «ana» mandaría
    // tres consultas. Intro adelanta la espera, y es lo que usa esta prueba para no depender del
    // reloj. Antes había además un botón «Buscar» al lado, que se retiró por redundante.
    const campo = buscar<HTMLInputElement>('boletin-panel-busqueda')!;
    campo.value = 'ana';
    campo.dispatchEvent(new Event('input'));
    campo.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    // BUSCAR VUELVE A PEDIR LA LISTA al servidor —la búsqueda no se resuelve aquí—, y esa consulta
    // hay que atenderla antes de mirar la descarga.
    http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/').flush(LISTADO);
    fixture.detectChanges();

    buscar<HTMLButtonElement>('boletin-panel-csv')!.click();
    fixture.detectChanges();

    const descarga = http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/export.csv');
    expect(descarga.request.method).toBe('GET');
    expect(descarga.request.params.get('q')).toBe('ana');
    expect(descarga.request.responseType).toBe('blob');

    descarga.flush(new Blob(['correo\n'], { type: 'text/csv' }));
    fixture.detectChanges();

    expect(buscar('boletin-panel-aviso')!.textContent).toContain('descargada');
    // El enlace se pulsó —el camino entero se recorrió— pero el navegador no escribió nada.
    expect(pulsar).toHaveBeenCalledTimes(1);
  });

  it('si el API falla lo dice en pantalla y no deja la tabla mintiendo', () => {
    fixture.detectChanges();
    http.expectOne(req => req.url === '/api/v1/admin/comunicaciones/boletin/')
      .flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(buscar('boletin-panel-error')).not.toBeNull();
    expect(todos('boletin-panel-fila').length).toBe(0);
  });

  it('el identificador de la pestana no choca con ningun modulo', () => {
    // `selectedModuleId` ES UNA SOLA SENAL PARA TODAS LAS AREAS, y `getSelectedModule` resuelve
    // por ese identificador. Si algun dia naciera un modulo llamado «boletin», pulsar la pestana
    // abriria el panel generico de ese modulo y no este, o al reves: dos pantallas peleandose por
    // el mismo nombre. Es la misma cautela que ya lleva escrita `PESTANA_ORGANIZACIONES`.
    expect(ADMIN_MODULES.some(modulo => modulo.id === 'boletin')).toBe(false);
  });

  it('con la lista vacia lo dice, en vez de dejar un hueco', () => {
    arrancar({ items: [], total: 0, activas: 0, limit: 25, offset: 0 });

    expect(buscar('boletin-panel-vacio')).not.toBeNull();
    expect(buscar('boletin-panel-paginacion')).toBeNull();
  });
});
