import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ExternalAccessPageComponent } from './external-access-page.component';

/**
 * El alta de una organizacion, que desde el 25 ago 2026 es UN SOLO FORMULARIO.
 *
 * <b>Que se vigila aqui, y por que cada cosa.</b>
 *
 * 1. QUE NO VUELVAN LAS PANTALLAS QUE SE FUERON. Eran cuatro —elegir entre cuenta personal y
 *    organizacion, los datos de la organizacion aparte, una tercera de «persona responsable» que
 *    no pedia ni un dato, y la verificacion por correo—. Ninguna rompe nada si reaparece: solo
 *    alargan el recorrido, que es como llegaron a ser siete pasos.
 *
 * 2. QUE LA SEDE SE PREGUNTE EN EL PASO 1 SIN DECLARAR ALCANCE. La ubicación principal y el
 *    alcance de los procesos son conceptos distintos.
 *
 * 3. QUE LA IDENTIDAD SEGMENTADA VIAJE. Es la mitad de la promesa que la pantalla lleva haciendo desde agosto
 *    —«identificaremos a la persona responsable»— y la que estuvo sin cumplirse: la tabla que la
 *    guarda tenia cero filas.
 *
 * 4. QUE EL CORREO SIGA SIENDO UNO. Eran dos campos —contacto de la organizacion y acceso— y son
 *    la misma cosa: el institucional. El motivo que los separaba —«publicaria la credencial en la
 *    ficha del Festival»— se comprobo endpoint por endpoint y era falso: ninguna ruta anonima
 *    devuelve el correo de la entidad, y lo que se publica es Festivales.CorreoContacto, otro
 *    campo. Si alguien vuelve a añadir el segundo, estas dos pruebas lo dicen.
 */
describe('ExternalAccessPageComponent · alta de organización', () => {
  let fixture: ComponentFixture<ExternalAccessPageComponent>;
  let componente: ExternalAccessPageComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExternalAccessPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(ExternalAccessPageComponent);
    componente = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // El sondeo de sesion del arranque: sin atenderlo, el verificador se queja al cerrar.
    http.match(peticion => peticion.url.includes('/externo/auth/me')).forEach(p => p.flush({}, { status: 401, statusText: 'Unauthorized' }));
    // El paso 1 del alta pide el DIVIPOLA por su ruta pública canónica antes de tener sesión.
    // para la sede obligatoria. Ver el comentario de `alta` en
    // el componente y el de `cargarDivipolaPublica()` en `admin.service.ts`.
    http.match(peticion => peticion.url.includes('/publico/divipola')).forEach(p => p.flush([]));
    http.match(peticion => peticion.url.includes('/externo/auth/register-preparation')).forEach(p => p.flush({
      registrationAvailable: true,
      legalDocuments: [
        { code: 'terminos_uso', title: 'Términos de uso', version: 'v1', publicUrl: 'https://example.test/terms' },
        { code: 'tratamiento_datos', title: 'Tratamiento de datos', version: 'v1', publicUrl: 'https://example.test/data' },
      ],
      impediments: [],
    }));
  });

  afterEach(() => http.verify());

  const rellenar = () => {
    componente.alta.organizationName = 'Fundación Sonidos del Río';
    componente.alta.headquartersDepartmentCode = '05';
    componente.alta.headquartersMunicipalityCode = '05001';
    componente.alta.firstName = 'Marta';
    componente.alta.secondName = 'Lucía';
    componente.alta.firstSurname = 'Responsable';
    componente.alta.secondSurname = 'Prueba';
    componente.alta.documentType = 'cc';
    componente.alta.documentNumber = '1.020.304.050';
    componente.alta.phone = '3001234567';
    componente.alta.email = 'marta@sonidosdelrio.org';
    componente.alta.password = 'Clave Segura 123';
    componente.alta.confirmPassword = 'Clave Segura 123';
    sembrarPoliticas();
    componente.politicasAceptadas.set({ tratamiento: true, terminos: true });
  };

  /**
   * Las tres finalidades vigentes, como las sirve el servidor.
   *
   * LAS PONE EL SERVIDOR Y NO LA PANTALLA, que es el cambio entero: antes eran dos casillas
   * escritas a mano en la plantilla, y el día que apareciera una tercera finalidad habría que venir
   * a escribirla. `obligatoria` también lo decide el servidor.
   */
  const sembrarPoliticas = (): void => {
    componente.politicasDelRegistro.set([
      { clave: 'tratamiento', version: '2026-09-12', titulo: 'Autorización de tratamiento de datos personales', texto: 'Autorizo el tratamiento…', urlOficial: null, referenciaOficial: null, obligatoria: true },
      { clave: 'terminos', version: '2026-09-12', titulo: 'Términos de uso', texto: 'Acepto usar el sistema…', urlOficial: null, referenciaOficial: null, obligatoria: true },
      { clave: 'boletin', version: '2026-09-12', titulo: 'Boletín del PNMC', texto: 'Autorizo el envío…', urlOficial: null, referenciaOficial: null, obligatoria: false },
    ]);
  };

  // ---------- 1. El recorrido ------------------------------------------------

  it('abre directamente en el formulario, sin pantalla de elección', () => {
    expect(componente.view()).toBe('register');
    const texto: string = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Crear mi cuenta');
    expect(texto).not.toContain('Registro personal');
  });

  it('no dibuja ninguna pantalla de verificación de correo', () => {
    const texto: string = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Verifica tu correo');
    expect(texto).not.toContain('código');
  });

  it('pide la sede obligatoria sin pedir alcance territorial', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const nombres = Array.from(raiz.querySelectorAll('input, select'))
      .map(control => control.getAttribute('name') ?? '');

    expect(nombres).not.toContain('coverageLevel');
    componente.pasoDeRegistro.set(2);
    fixture.detectChanges();
    const nombresPasoDos = Array.from(raiz.querySelectorAll('input, select')).map(control => control.getAttribute('name') ?? '');
    expect(nombresPasoDos).toContain('headquartersDepartmentCode');
    expect(nombresPasoDos).toContain('headquartersMunicipalityCode');
  });

  it('pide un solo correo electrónico: también es el de acceso', () => {
    componente.pasoDeRegistro.set(1);
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    const correos = Array.from(raiz.querySelectorAll('input[type="email"]'))
      .map(control => control.getAttribute('name'));

    expect(correos).toEqual(['email']);
  });

  // ---------- 2. Lo que se envía --------------------------------------------

  it('manda organización, persona responsable y acceso en una sola petición', () => {
    rellenar();
    componente.registrar();

    const peticion = http.expectOne('/api/v1/externo/auth/register');
    const cuerpo = peticion.request.body;

    expect(cuerpo.organizationName).toBe('Fundación Sonidos del Río');
    expect(cuerpo.firstName).toBe('Marta');
    expect(cuerpo.secondName).toBe('Lucía');
    expect(cuerpo.firstSurname).toBe('Responsable');
    expect(cuerpo.secondSurname).toBe('Prueba');
    expect(cuerpo.documentType).toBe('cc');
    expect(cuerpo.documentNumber).toBe('1.020.304.050');
    expect(cuerpo.email).toBe('marta@sonidosdelrio.org');
    expect(cuerpo.politicasAceptadas).toEqual(['tratamiento', 'terminos']);

    expect(cuerpo.headquartersDepartmentCode).toBe('05');
    expect(cuerpo.headquartersMunicipalityCode).toBe('05001');
    expect(cuerpo.coverageLevel).toBeUndefined();

    // Y el segundo correo tampoco: el contrato del API ya no tiene ese campo.
    expect(cuerpo.organizationContactEmail).toBeUndefined();

    peticion.flush({ userId: '9', email: cuerpo.email, accountStatus: 'activo', organizationId: '4', organizationName: cuerpo.organizationName });

    // Y se entra en el mismo gesto, sin pantalla intermedia.
    http.expectOne('/api/v1/externo/auth/login').flush({ email: cuerpo.email, accountStatus: 'activo' });
    http.match(peticion => peticion.method === 'GET').forEach(pendiente => pendiente.flush([]));
  });

  // ---------- 3. Lo que se detiene antes de enviar ---------------------------

  it('no envía nada si falta el número de documento', () => {
    rellenar();
    componente.alta.documentNumber = '';
    componente.registrar();

    http.expectNone('/api/v1/externo/auth/register');
    expect(componente.erroresAlta()['documentNumber']).toContain('documento');
  });

  it('no envía nada si las contraseñas no coinciden', () => {
    rellenar();
    componente.alta.confirmPassword = 'OtraDistinta123';
    componente.registrar();

    http.expectNone('/api/v1/externo/auth/register');
    expect(componente.erroresAlta()['confirmPassword']).toContain('coinciden');
  });

  it('marca el correo inválido junto al campo y no pierde el valor escrito', () => {
    rellenar();
    componente.alta.email = 'correo-invalido';
    componente.registrar();

    http.expectNone('/api/v1/externo/auth/register');
    expect(componente.erroresAlta()['email']).toBe('Ingresa un correo electrónico válido.');
    expect(componente.alta.email).toBe('correo-invalido');
  });

  // ---------- 4. El consentimiento se ve y se puede pulsar --------------------

  /**
   * Las dos casillas eran `input type=checkbox` sin una sola clase de tema sobre un panel oscuro.
   * Esta prueba no juzga el aspecto —eso no lo puede hacer una prueba unitaria— sino lo unico
   * comprobable y lo que de verdad estaba roto: que cada casilla va DENTRO de su etiqueta, que es
   * lo que hace que pulsar el texto la marque, y que lleva el color de acento en vez del azul del
   * navegador.
   */
  it('cada consentimiento es una etiqueta pulsable con la casilla dentro', () => {
    sembrarPoliticas();
    componente.pasoDeRegistro.set(3);
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    const casillas = Array.from(raiz.querySelectorAll('input[type="checkbox"]'));

    // UNA POR FINALIDAD, y las finalidades las dice el servidor: eran dos fijas escritas en la
    // plantilla y ahora son tantas como políticas vigentes haya.
    expect(casillas.length).toBe(3);
    for (const casilla of casillas) {
      expect(casilla.closest('label')).withContext('la casilla tiene que ir dentro de su etiqueta').not.toBeNull();
      expect(casilla.className).toContain('accent-[#00A849]');
    }
  });

  it('mantiene las casillas compactas para que el texto permanezca dentro de su tarjeta', () => {
    sembrarPoliticas();
    componente.pasoDeRegistro.set(3);
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    const casillas = Array.from(raiz.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'));

    expect(casillas.length).toBe(3);
    for (const casilla of casillas) {
      const ancho = Number.parseFloat(getComputedStyle(casilla).width);

      expect(ancho)
        .withContext('una casilla de consentimiento no puede heredar el ancho completo de los campos de texto')
        .toBeLessThanOrEqual(24);
    }
  });

  it('no envía nada si falta una autorización obligatoria', () => {
    rellenar();
    componente.politicasAceptadas.set({ terminos: true });
    componente.registrar();

    http.expectNone('/api/v1/externo/auth/register');
    expect(componente.erroresAlta()['politicasAceptadas']).toBeTruthy();
  });

  it('el boletín es opcional: sin marcarlo, el alta sale igual', () => {
    // ES LO QUE HACE QUE LA FINALIDAD SIGNIFIQUE ALGO. Si hubiera que marcarlo para continuar no
    // sería una autorización libre para un fin determinado —Ley 1581 art. 9— sino un peaje.
    rellenar();
    componente.registrar();

    const peticion = http.expectOne('/api/v1/externo/auth/register');
    expect(peticion.request.body.politicasAceptadas).toEqual(['tratamiento', 'terminos']);

    // EL ALTA ENCADENA EL INGRESO. Es lo que hace la pantalla al terminar —registrar y entrar— y
    // si no se atiende aquí, `http.verify()` la cuenta como petición abierta.
    peticion.flush({});
    http.expectOne('/api/v1/externo/auth/login').flush({});
  });
});
