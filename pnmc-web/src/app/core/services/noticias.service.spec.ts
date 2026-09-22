import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NoticiasService } from './noticias.service';

/**
 * El servicio de Noticias conserva el motivo del servidor.
 *
 * <b>EL DEFECTO QUE TRAJO ESTAS PRUEBAS.</b> Cuando publicar se rechazaba, el API explicaba TODO
 * lo que faltaba —«no tiene cuerpo; no tiene fecha de publicación»— y la consola enseñaba «Error
 * al enviar datos al backend». El motivo estaba en `ApiError.payload` y el servicio leía un
 * `error` que nunca existió, así que siempre ganaba el respaldo genérico.
 */
describe('NoticiasService conserva lo que dice el servidor', () => {
  let servicio: NoticiasService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    servicio = TestBed.inject(NoticiasService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('enseña el motivo completo cuando publicar se rechaza', async () => {
    const promesa = servicio.cambiarEstado(7, 'publicado');

    const peticion = http.expectOne(r => r.url.includes('/institucional/noticias/7/estado'));
    peticion.flush(
      { estado: ['No se puede publicar: no tiene cuerpo; no tiene fecha de publicación.'] },
      { status: 400, statusText: 'Bad Request' });

    const resultado = await promesa;
    expect(resultado.ok).toBe(false);
    expect(resultado.error).toBe('No se puede publicar: no tiene cuerpo; no tiene fecha de publicación.');
  });

  it('enseña el conflicto de versión tal como lo explica el servidor', async () => {
    const promesa = servicio.guardar(7, { titulo: 'x', resumen: 'y', version: 1 });

    const peticion = http.expectOne(r => r.url.includes('/institucional/noticias/7'));
    peticion.flush(
      { version: ['La noticia cambió mientras la editabas (versión 3).'] },
      { status: 409, statusText: 'Conflict' });

    const resultado = await promesa;
    expect(resultado.error).toBe('La noticia cambió mientras la editabas (versión 3).');
  });

  it('cae al respaldo cuando el servidor no explica nada', async () => {
    const promesa = servicio.listarPublicas();

    const peticion = http.expectOne(r => r.url.includes('/publico/noticias'));
    peticion.flush(null, { status: 500, statusText: 'Server Error' });

    const resultado = await promesa;
    expect(resultado.ok).toBe(false);
    expect(resultado.error).toContain('No fue posible');
  });
});
