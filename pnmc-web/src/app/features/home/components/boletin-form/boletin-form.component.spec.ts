import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { BoletinFormComponent } from './boletin-form.component';

/*
  QUE AFIRMA ESTA PRUEBA. Que pulsar «Registrarme» hace algo, y que ese algo pasa por una
  autorizacion expresa. El defecto que se esta arreglando es literal: hasta
  el boton no tenia manejador, asi que se escribia un correo, se pulsaba, y no salia ni una
  peticion ni un aviso. La primera prueba de aqui es la que se habria puesto en rojo entonces.

  SE USA `HttpTestingController` Y NO UN DOBLE DEL SERVICIO. Un servicio falso comprobaria que el
  componente llama a un metodo; lo que hace falta comprobar es que sale una peticion HTTP a la ruta
  correcta y con el cuerpo correcto, porque el defecto era justamente que no salia ninguna.
*/
describe('BoletinFormComponent', () => {
  let fixture: ComponentFixture<BoletinFormComponent>;
  let http: HttpTestingController;

  const POLITICA = { version: '2026-08-28', texto: 'Autorizo el tratamiento de mi correo para recibir el boletin.' };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BoletinFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(BoletinFormComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const raiz = () => fixture.nativeElement as HTMLElement;
  const buscar = <T extends HTMLElement>(id: string) => raiz().querySelector<T>(`[data-testid="${id}"]`);

  function escribir(correo: string): void {
    const campo = buscar<HTMLInputElement>('boletin-correo')!;
    campo.value = correo;
    campo.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function pulsarRegistrarme(): void {
    buscar<HTMLButtonElement>('boletin-enviar')!.click();
    fixture.detectChanges();
  }

  /** Abre el modal y responde la politica, que es el punto de partida de casi todo. */
  function abrirConPolitica(correo = 'persona@pnmc.test'): void {
    escribir(correo);
    pulsarRegistrarme();
    http.expectOne('/api/v1/publico/boletin/politica').flush(POLITICA);
    fixture.detectChanges();
  }

  it('con un correo valido pide la politica y abre la autorizacion, sin dar de alta nada todavia', () => {
    escribir('persona@pnmc.test');
    pulsarRegistrarme();

    // Lo primero que sale es la politica. NO el alta: mientras la persona no acepte, no se envia
    // su correo a ninguna parte.
    const peticion = http.expectOne('/api/v1/publico/boletin/politica');
    expect(peticion.request.method).toBe('GET');
    http.expectNone('/api/v1/publico/boletin/suscripciones');

    peticion.flush(POLITICA);
    fixture.detectChanges();

    expect(buscar('boletin-modal')).not.toBeNull();
    expect(buscar('boletin-politica-texto')!.textContent).toContain(POLITICA.texto);
    expect(buscar('boletin-politica-version')!.textContent).toContain('2026-08-28');
  });

  it('el texto de la autorizacion es el que sirve el servidor, no uno escrito en la plantilla', () => {
    // Si la plantilla llevara su propia copia, esta prueba pasaria igual con cualquier respuesta.
    // Se responde un texto distinto del real a proposito: tiene que aparecer ESE.
    escribir('persona@pnmc.test');
    pulsarRegistrarme();
    http.expectOne('/api/v1/publico/boletin/politica').flush({ version: '9999-01-01', texto: 'TEXTO QUE SOLO EXISTE EN ESTA PRUEBA' });
    fixture.detectChanges();

    expect(buscar('boletin-politica-texto')!.textContent).toContain('TEXTO QUE SOLO EXISTE EN ESTA PRUEBA');
    expect(buscar('boletin-politica-version')!.textContent).toContain('9999-01-01');
  });

  it('con un correo invalido avisa en pantalla y no llama al servidor', () => {
    escribir('esto-no-es-un-correo');
    pulsarRegistrarme();

    // NINGUNA PETICION. Es la mitad que importa: un aviso que ademas hubiera disparado la llamada
    // no habria evitado nada.
    http.expectNone('/api/v1/publico/boletin/politica');
    http.expectNone('/api/v1/publico/boletin/suscripciones');

    expect(buscar('boletin-modal')).toBeNull();
    expect(buscar('boletin-error')!.textContent).toContain('correo electrónico válido');
  });

  it('el aviso se anuncia solo a un lector de pantalla', () => {
    // Quien no ve la pantalla no se entera de que aparecio un texto. Sin `aria-live`, para esa
    // persona el boton sigue sin hacer nada, que es exactamente el defecto que se esta cerrando.
    const aviso = buscar('boletin-aviso')!;
    expect(aviso.getAttribute('aria-live')).toBe('polite');
    expect(aviso.getAttribute('role')).toBe('status');
  });

  it('la casilla nace desmarcada y sin ella no se puede confirmar', () => {
    abrirConPolitica();

    const casilla = buscar<HTMLInputElement>('boletin-casilla')!;
    const confirmar = buscar<HTMLButtonElement>('boletin-confirmar')!;

    // PREMARCARLA SERIA CONSENTIMIENTO POR OMISION, y la Ley 1581 de 2012 pide un acto propio.
    expect(casilla.checked).toBe(false);
    expect(confirmar.disabled).toBe(true);

    confirmar.click();
    fixture.detectChanges();
    http.expectNone('/api/v1/publico/boletin/suscripciones');
  });

  it('al aceptar da de alta el correo con la autorizacion, y lo dice', fakeAsync(() => {
    abrirConPolitica('Persona@PNMC.test');

    const casilla = buscar<HTMLInputElement>('boletin-casilla')!;
    casilla.click();
    fixture.detectChanges();

    const confirmar = buscar<HTMLButtonElement>('boletin-confirmar')!;
    expect(confirmar.disabled).toBe(false);
    confirmar.click();
    fixture.detectChanges();

    const alta = http.expectOne('/api/v1/publico/boletin/suscripciones');
    expect(alta.request.method).toBe('POST');
    expect(alta.request.body).toEqual({
      correo: 'Persona@PNMC.test',
      autorizaTratamiento: true,
      origen: 'portada',
    });

    alta.flush({ message: 'Listo. Vas a recibir la informacion del Plan en ese correo.' }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();

    // El modal se cierra, el aviso sale y el campo queda vacio para que nadie crea que hay algo
    // sin enviar.
    expect(buscar('boletin-modal')).toBeNull();
    expect(buscar('boletin-exito')!.textContent).toContain('Vas a recibir');

    // `tick()` HACE FALTA AQUI Y NO ES ADORNO. `ngModel` escribe en el elemento a traves de una
    // microtarea, asi que sin agotarla el campo seguiria enseñando el texto viejo aunque la senal
    // ya estuviera vacia. Se mide el elemento y no la senal: lo que la persona ve es el elemento.
    tick();
    expect(buscar<HTMLInputElement>('boletin-correo')!.value).toBe('');
  }));

  it('cancelar cierra sin dar de alta', () => {
    abrirConPolitica();

    buscar<HTMLButtonElement>('boletin-cancelar')!.click();
    fixture.detectChanges();

    http.expectNone('/api/v1/publico/boletin/suscripciones');
    expect(buscar('boletin-modal')).toBeNull();
  });

  it('si el servidor rechaza el alta lo dice con su motivo y no cierra el modal', () => {
    abrirConPolitica();
    buscar<HTMLInputElement>('boletin-casilla')!.click();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('boletin-confirmar')!.click();
    fixture.detectChanges();

    http.expectOne('/api/v1/publico/boletin/suscripciones').flush(
      { message: 'El correo electronico no es valido.' },
      { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    // SIGUE ABIERTO: cerrarlo dejaria a la persona sin ver por que fallo y con el correo perdido.
    expect(buscar('boletin-modal')).not.toBeNull();
    expect(buscar('boletin-modal-error')!.textContent).toContain('El correo electronico no es valido.');
  });

  it('si se pulsa demasiadas veces explica que hay que esperar', () => {
    // 429 es el unico error que se arregla solo con tiempo. Decir «error» a secas invita a volver
    // a pulsar, que es justo lo que mantiene cerrado el cupo de `boletin-alta`.
    abrirConPolitica();
    buscar<HTMLInputElement>('boletin-casilla')!.click();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('boletin-confirmar')!.click();
    fixture.detectChanges();

    http.expectOne('/api/v1/publico/boletin/suscripciones').flush(
      { message: 'demasiadas' }, { status: 429, statusText: 'Too Many Requests' });
    fixture.detectChanges();

    expect(buscar('boletin-modal-error')!.textContent).toContain('Espera un minuto');
  });

  it('si la politica no carga, no deja aceptar y ofrece reintentar', () => {
    // ACEPTAR ALGO QUE NO SE HA PODIDO LEER NO ES ACEPTAR. Sin el texto delante, el boton de
    // confirmar ni siquiera existe.
    escribir('persona@pnmc.test');
    pulsarRegistrarme();
    http.expectOne('/api/v1/publico/boletin/politica').flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(buscar('boletin-casilla')).toBeNull();
    expect(buscar<HTMLButtonElement>('boletin-confirmar')!.disabled).toBe(true);
    expect(buscar('boletin-politica-fallo')!.textContent).toContain('política de tratamiento');

    buscar<HTMLButtonElement>('boletin-politica-reintentar')!.click();
    fixture.detectChanges();
    http.expectOne('/api/v1/publico/boletin/politica').flush(POLITICA);
    fixture.detectChanges();

    expect(buscar('boletin-casilla')).not.toBeNull();
  });

  it('sin politica a la vista no se puede confirmar, aunque la autorizacion figurara marcada', () => {
    // ESTA PRUEBA NACIO DE UN MUTANTE QUE SOBREVIVIO. Al quitar `politica() !== null` de
    // `puedeConfirmar()` no caia ninguna prueba, y el motivo era que ninguna llegaba a esa linea:
    // por la pantalla, la casilla solo existe cuando ya hay texto, asi que `autoriza()` nunca
    // podia ser cierto con la politica sin cargar.
    //
    // SE ENTRA POR EL COMPONENTE Y NO POR LA PANTALLA, a proposito. La clausula es una guarda de
    // segunda linea: cubre el estado que la plantilla de hoy no permite, y que permitiria
    // cualquier reordenacion futura del modal. Medirla por el unico camino que la alcanza es lo
    // que impide que se borre por «no la usa nadie».
    escribir('persona@pnmc.test');
    pulsarRegistrarme();
    http.expectOne('/api/v1/publico/boletin/politica').flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    fixture.componentInstance.alternarAutorizacion(true);
    fixture.detectChanges();

    expect(fixture.componentInstance.autoriza()).toBe(true);
    expect(buscar<HTMLButtonElement>('boletin-confirmar')!.disabled).toBe(true);

    buscar<HTMLButtonElement>('boletin-confirmar')!.click();
    fixture.detectChanges();
    http.expectNone('/api/v1/publico/boletin/suscripciones');
  });

  it('el dialogo se anuncia como dialogo modal', fakeAsync(() => {
    abrirConPolitica();
    tick();

    const dialogo = buscar('boletin-modal')!;
    expect(dialogo.getAttribute('role')).toBe('dialog');
    expect(dialogo.getAttribute('aria-modal')).toBe('true');
    expect(dialogo.getAttribute('aria-labelledby')).toBe('boletin-modal-titulo');
    expect(document.getElementById('boletin-modal-titulo')).not.toBeNull();
  }));
});
