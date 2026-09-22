import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { EdicionesTemporalesComponent } from './ediciones-temporales.component';
import { EdicionDeFestival } from '../../core/services/admin.service';

/**
 * Las ediciones de un Festival y su ciclo.
 *
 * <b>Lo que fija.</b> Las tres cosas que el usuario señaló: que
 * publicar un borrador se encuentre —«no existe una ruta clara para publicarla»—, que las acciones
 * de cada fila salgan de su estado en vez de pintarse todas, y que «Consultar observaciones» no
 * aparezca cuando no hay ninguna.
 */
describe('EdicionesTemporalesComponent', () => {
  let fixture: ComponentFixture<EdicionesTemporalesComponent>;
  let componente: EdicionesTemporalesComponent;
  let http: HttpTestingController;

  const BASE: EdicionDeFestival = {
    id: '9', festivalId: '4', anio: 2026, numeroEdicion: 12, nombre: 'Edición 2026',
    descripcion: 'La del año en curso.', fechaInicio: '2026-10-01', fechaFin: '2026-10-05',
    director: null, estadoVisibilidad: 'borrador', estadoVisibilidadEtiqueta: 'Borrador',
    esEditable: true, estado: 'programada', estadoEtiqueta: 'Programada', cuantasObservaciones: 0,
  };

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const montar = (ediciones: EdicionDeFestival[], festivalPublicado = true): void => {
    fixture = TestBed.createComponent(EdicionesTemporalesComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('festivalId', '4');
    fixture.componentRef.setInput('festivalPublicado', festivalPublicado);
    fixture.detectChanges();
    http.expectOne(p => p.url === '/api/v1/externo/festivales/4/ediciones').flush(ediciones);
    fixture.detectChanges();
  };

  /** Abre el menú de la primera fila si estaba cerrado. Llamarlo dos veces no lo cierra. */
  const abrirMenu = (): void => {
    if (raiz().querySelector('[data-menu-de-acciones]')) return;
    raiz().querySelector<HTMLButtonElement>('[data-disparador-acciones]')?.click();
    fixture.detectChanges();
  };

  /** Lo que ofrece la primera fila, la esperada primero. */
  const accionesVisibles = (): string[] => {
    abrirMenu();
    return Array.from(raiz().querySelectorAll('[data-opcion-accion]')).map(o => o.textContent!.trim());
  };

  /** La acción esperada, que desde vive DENTRO del menú. */
  const accionEsperada = (): HTMLElement | null => {
    abrirMenu();
    return raiz().querySelector<HTMLElement>('[data-accion-principal]');
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EdicionesTemporalesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('un borrador ofrece publicar como acción principal', () => {
    montar([BASE]);

    // ERA EXACTAMENTE LA QUE NO SE ENCONTRABA: «Publicar» era un enlace subrayado del mismo tamaño
    // que los demás, en la última columna. Sigue siendo la esperada y se lee destacada, pero desde
    // vive DENTRO del menú: nueve tablas comparten esta pieza y sacar
    // la principal fuera dejaba la columna de acciones distinta en cada fila.
    expect(accionEsperada()!.textContent!.trim()).toBe('Publicar');
    expect(accionesVisibles()).toEqual(['Publicar', 'Editar', 'Archivar']);
  });

  it('con el Festival sin publicar, publicar se ofrece desactivado y dice por qué', () => {
    montar([BASE], false);

    // AQUI SI SE PUEDE HACER: lo que falta es un requisito del Festival, y decirlo es más útil que
    // hacer desaparecer el botón y dejar a quien administra buscando por qué.
    const principal = accionEsperada() as HTMLButtonElement;
    expect(principal.disabled).toBeTrue();
    expect(principal.getAttribute('title')).toContain('Publica primero el Festival');
  });

  it('una edición publicada ofrece despublicar y no ofrece publicar', () => {
    montar([{ ...BASE, estadoVisibilidad: 'publicada', estadoVisibilidadEtiqueta: 'Publicada', esEditable: false }]);

    const acciones = accionesVisibles();
    // CONSULTAR VA PRIMERO Y ES LA PRINCIPAL: sobre una edición publicada lo que se hace casi
    // siempre es mirarla; editarla exige despublicarla antes, y el servidor lo exige igual.
    expect(acciones).toEqual(['Ver la ficha', 'Despublicar', 'Archivar']);
    expect(acciones).not.toContain('Publicar');
    expect(acciones).not.toContain('Editar');
    // DESPUBLICAR NO ES ARCHIVAR y las dos siguen existiendo: una devuelve a borrador para seguir
    // editando, la otra cierra el ciclo.
    expect(acciones).toContain('Despublicar');
  });

  it('una edición archivada se puede consultar y no ofrece volver a archivarla', () => {
    montar([{ ...BASE, estadoVisibilidad: 'archivada', estadoVisibilidadEtiqueta: 'Archivada', esEditable: false }]);

    // NO PUEDE QUEDARSE SIN NINGUNA ACCION. Hasta esta lista estaba
    // vacía y la fila quedaba con la columna «Acciones» en blanco: el registro seguía ahí y no
    // había forma de volver a verlo. Consultar es lo único que se puede siempre.
    expect(accionesVisibles()).toEqual(['Ver la ficha']);
    expect(accionesVisibles()).not.toContain('Archivar');
  });

  it('consultar abre la misma ficha en modo de solo lectura', () => {
    montar([{ ...BASE, estadoVisibilidad: 'archivada', estadoVisibilidadEtiqueta: 'Archivada', esEditable: false }]);

    (accionEsperada() as HTMLButtonElement).click();
    fixture.detectChanges();

    // ES LA MISMA FICHA Y NO UNA COPIA: lo que cambia es el modo con el que se abre.
    expect(componente.consultando()).toBeTrue();
    expect(componente.edicionAbierta()).toBe(BASE.id);
    expect(raiz().querySelector('app-ficha-edicion-festival')).not.toBeNull();

    // LA FICHA PIDE LO SUYO AL ABRIRSE —sus catálogos, DIVIPOLA y la edición— y hay que contestarle:
    // `http.verify()` del `afterEach` considera una petición sin respuesta un fallo, con razón.
    for (const pendiente of http.match(() => true)) pendiente.flush(pendiente.request.method === 'GET' ? [] : {});
  });

  it('sin observaciones no se ofrece consultarlas', () => {
    montar([BASE]);

    // NO MOSTRAR EL BOTON, no mostrarlo y abrir un recuadro que diga «no hay observaciones»: eso
    // hace perder un clic y deja la duda de si algo falló.
    expect(accionesVisibles().some(a => a.includes('observaci'))).toBeFalse();
  });

  it('con observaciones se ofrece consultarlas, y dice cuántas', () => {
    montar([{ ...BASE, cuantasObservaciones: 3 }]);

    expect(accionesVisibles()).toContain('Ver 3 observaciones');
  });

  it('publicar desde la lista llama a la ruta y recarga', () => {
    montar([BASE]);
    (accionEsperada() as HTMLButtonElement).click();
    fixture.detectChanges();

    http.expectOne(p => p.url === '/api/v1/externo/organizaciones/csrf').flush({ requestToken: 't' });
    http.expectOne(p => p.url === '/api/v1/externo/ediciones/9/publicar')
      .flush({ ...BASE, estadoVisibilidad: 'publicada', estadoVisibilidadEtiqueta: 'Publicada' });
    fixture.detectChanges();

    // SE RECARGA LA LISTA: dejarla con el estado anterior haría dudar de si se publicó.
    http.expectOne(p => p.url === '/api/v1/externo/festivales/4/ediciones').flush([]);
    fixture.detectChanges();
    expect(componente.mensaje()).toContain('quedó publicada');
  });

  it('si publicar falla, se cuenta el motivo del servidor y la lista no cambia', () => {
    montar([BASE]);
    (accionEsperada() as HTMLButtonElement).click();
    fixture.detectChanges();

    http.expectOne(p => p.url === '/api/v1/externo/organizaciones/csrf').flush({ requestToken: 't' });
    http.expectOne(p => p.url === '/api/v1/externo/ediciones/9/publicar').flush(
      { message: 'Publica primero el Festival para poder publicar una de sus ediciones.' },
      { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    // «PUBLICA PRIMERO EL FESTIVAL» Y «CONFIRMA TU CORREO» piden cosas distintas: un «no fue
    // posible» genérico obliga a adivinar cuál de las dos es.
    expect(componente.error()).toContain('Publica primero el Festival');
    expect(componente.ediciones().length).toBe(1);
  });
});
