import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PrevisualizacionEnListadoService } from './previsualizacion-en-listado.service';

/**
 * La previsualización entra por donde entran los datos del listado.
 *
 * LO QUE ESTAS PRUEBAS IMPIDEN: que una dirección le pida a un listado que previsualice un registro
 * de otro módulo, que un fallo del servidor tumbe el listado entero, y que el registro
 * previsualizado aparezca dos veces cuando ya estaba publicado.
 */
describe('PrevisualizacionEnListadoService', () => {
  let servicio: PrevisualizacionEnListadoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    servicio = TestBed.inject(PrevisualizacionEnListadoService);
    http = TestBed.inject(HttpTestingController);
  });

  it('solo atiende lo suyo: el parámetro lleva el módulo', () => {
    expect(servicio.idPara('noticias', '?pnmcPrevisualizar=noticias:12')).toBe(12);
    // SIN ESTO, `/agenda?pnmcPrevisualizar=noticias:12` le pediría a la Agenda que fuera a buscar
    // una noticia, y el listado enseñaría un registro de otro módulo con su tarjeta equivocada.
    expect(servicio.idPara('agenda', '?pnmcPrevisualizar=noticias:12')).toBeNull();
    expect(servicio.idPara('noticias', '?pnmcPrevisualizar=noticias:abc')).toBeNull();
    expect(servicio.idPara('noticias', '?pnmcPrevisualizar=noticias:-3')).toBeNull();
    expect(servicio.idPara('noticias', '')).toBeNull();
  });

  it('si el servidor no responde, el listado se queda como estaba', async () => {
    const promesa = servicio.obtener<{ id: number }>('noticias', 7);
    // SIN SESION DE CONSOLA EL SERVIDOR RESPONDE 401, que es la guarda de verdad: el borrador no
    // sale del servidor. Aquí lo que se comprueba es que ese 401 no rompe la pantalla pública.
    http.expectOne('/api/v1/admin/previsualizacion/noticias/7')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expectAsync(promesa).toBeResolvedTo(null);
  });

  it('coloca el registro lo más arriba posible y no lo duplica', () => {
    const lista = [{ id: 1 }, { id: 2 }];
    const mismoId = (a: { id: number }, b: { id: number }) => a.id === b.id;

    // EN AGENDA Y CATALOGO la primera posición ya es la tarjeta ordinaria.
    expect(servicio.anteponer(lista, { id: 9 }, mismoId, 'agenda')).toEqual([{ id: 9 }, { id: 1 }, { id: 2 }]);
    // Y SI YA ERA PUBLICO, se sustituye en vez de salir dos veces.
    expect(servicio.anteponer(lista, { id: 2 }, mismoId, 'agenda')).toEqual([{ id: 2 }, { id: 1 }]);
    expect(servicio.anteponer(lista, null, mismoId, 'agenda')).toBe(lista);
  });

  it('en Noticias esquiva las piezas destacadas, que usan otra tarjeta', () => {
    const lista = [{ id: 1 }, { id: 2 }, { id: 3 }, { id: 4 }];
    const mismoId = (a: { id: number }, b: { id: number }) => a.id === b.id;

    // POR QUE EXISTE: colocado el primero, el registro se pintaba con la tarjeta de portada
    // —grande, con imagen a sangre— que es justamente la que no va a tener. La pregunta «¿cómo se
    // verá?» recibía la respuesta de otra tarjeta.
    expect(servicio.anteponer(lista, { id: 9 }, mismoId, 'noticias'))
      .toEqual([{ id: 1 }, { id: 2 }, { id: 3 }, { id: 9 }, { id: 4 }]);

    // CON MENOS DE TRES NOTICIAS no hay destacadas que esquivar, y va al final sin romperse.
    expect(servicio.anteponer([{ id: 1 }], { id: 9 }, mismoId, 'noticias')).toEqual([{ id: 1 }, { id: 9 }]);
  });

  afterEach(() => http.verify());
});
