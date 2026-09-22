import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ExternalAccessPageComponent } from './external-access-page.component';
import { TextosWebService } from '../../core/services/textos-web.service';
import { DEFAULT_TEXTS, WEB_TEXT_KEY_INDEX } from '../../core/cms/registro-de-textos-web';

/**
 * Lo que defiende esta prueba es la promesa entera del CMS aplicada a
 * /registro: que lo que una editora publica sea lo que ve el visitante, y que
 * no publicar nada no deje la página con huecos.
 *
 * Los consentimientos juridicos salen de los documentos vigentes entregados
 * por el API, no del catalogo editorial.
 */
describe('ExternalAccessPageComponent · textos del CMS', () => {
  // Las siete claves de las dos tarjetas de eleccion se fueron con las tarjetas: el alta dejo de
  // ser una eleccion entre cuenta personal y organizacion. Queda la entradilla del formulario.
  const CLAVES_DE_LA_PORTADA = [
    'access_eyebrow',
    'access_title',
    'access_intro',
    'access_register_intro',
    'access_footer_note',
  ];

  const montar = async (publicados: Record<string, string>) => {
    await TestBed.configureTestingModule({
      imports: [ExternalAccessPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    // Se replica el contrato del servicio, no su implementación: lo publicado
    // manda —incluida una cadena vacía, que es una publicación deliberada— y,
    // si la clave no viaja en el diccionario, entra el compilado. De ahí `??`
    // y no `||`; esa distinción se prueba donde vive, en
    // `textos-web.service.spec.ts` (PNMC-040).
    const textos = TestBed.inject(TextosWebService);
    spyOn(textos, 'getWebText').and.callFake((clave: string) => publicados[clave] ?? DEFAULT_TEXTS[clave] ?? '');

    const fixture: ComponentFixture<ExternalAccessPageComponent> = TestBed.createComponent(ExternalAccessPageComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('todas las claves que pide la portada existen en el registro del panel', () => {
    const desconocidas = CLAVES_DE_LA_PORTADA
      .filter((clave) => !WEB_TEXT_KEY_INDEX.has(clave));

    expect(desconocidas).toEqual([]);
  });

  it('sin nada publicado la página muestra su texto compilado', async () => {
    const fixture = await montar({});
    const texto: string = fixture.nativeElement.textContent;

    expect(texto).toContain(DEFAULT_TEXTS['access_title']);
    expect(texto).toContain(DEFAULT_TEXTS['access_register_intro']);
  });

  it('lo publicado reemplaza el texto compilado en la pantalla', async () => {
    const publicados = Object.fromEntries(CLAVES_DE_LA_PORTADA.map((clave) => [clave, `<<${clave}>>`]));
    const fixture = await montar(publicados);
    const texto: string = fixture.nativeElement.textContent;

    const invisibles = CLAVES_DE_LA_PORTADA.filter((clave) => !texto.includes(`<<${clave}>>`));
    expect(invisibles).toEqual([]);
  });

  it('los consentimientos muestran el TEXTO que sirve el servidor, no un enlace a él', async () => {
    // ESTE ES EL CAMBIO QUE IMPORTA. Antes la pantalla componía su propia frase —«Acepto los
    // {título} {versión}»— y lo único que viajaba era un enlace a un PDF alojado en
    // mincultura.gov.co: la evidencia de lo aceptado vivía en un servidor ajeno que puede cambiar
    // el documento sin dejar rastro aquí. Ahora se muestra el texto que el servidor copiará dentro
    // de la autorización, que es lo que la Ley 1581 obliga a poder demostrar.
    const fixture = await montar({});
    fixture.componentInstance.politicasDelRegistro.set([
      { clave: 'terminos', version: '2026-09-12', titulo: 'Términos vigentes', texto: 'ESTE ES EL TEXTO DE LOS TERMINOS', urlOficial: '/legal/terminos-v3', referenciaOficial: 'PL-GSI-001 v0', obligatoria: true },
      { clave: 'boletin', version: '2026-09-12', titulo: 'Boletín del PNMC', texto: 'ESTE ES EL TEXTO DEL BOLETIN', urlOficial: null, referenciaOficial: null, obligatoria: false },
    ]);
    fixture.componentInstance.pasoDeRegistro.set(3);
    fixture.detectChanges();

    const texto: string = fixture.nativeElement.textContent;
    expect(texto).toContain('ESTE ES EL TEXTO DE LOS TERMINOS');
    expect(texto).toContain('ESTE ES EL TEXTO DEL BOLETIN');
    expect(texto).toContain('2026-09-12');

    // Y LO OPCIONAL SE DICE. Que el boletín se pueda dejar sin marcar no se deduce de que no lleve
    // asterisco: se escribe.
    expect(texto).toContain('Opcional');
  });
});

/**
 * Cada pestaña, con su propia dirección.
 *
 * Hasta «Ingresar» y «Registrarse» solo cambiaban `view()`: la barra
 * de direcciones se quedaba diciendo lo mismo al pasar de una a otra, así que ningún estado se
 * podía compartir, guardar en marcadores ni recorrer con «Atrás». Lo pidió la dirección de producto.
 */
describe('ExternalAccessPageComponent · el modo de acceso en la URL', () => {
  let fixture: ComponentFixture<ExternalAccessPageComponent>;
  let componente: ExternalAccessPageComponent;
  let navegar: jasmine.Spy;

  /** `modoAcceso` reproduce la ruta explícita que montó la pantalla. */
  const montar = (modoAcceso: 'ingresar' | 'registro') => {

    TestBed.configureTestingModule({
      imports: [ExternalAccessPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({}), data: { modoAcceso } },
            queryParamMap: of(convertToParamMap({})),
          },
        },
      ],
    });

    fixture = TestBed.createComponent(ExternalAccessPageComponent);
    navegar = spyOn(TestBed.inject(Router), 'navigate');
    fixture.detectChanges();
    componente = fixture.componentInstance;
  };

  it('«Registrarse» abre la ruta pública de registro', () => {
    montar('ingresar');

    componente.openRegistrationForm();

    expect(componente.view()).toBe('register');
    expect(navegar).toHaveBeenCalledWith(['/registro']);
  });

  it('«Ingresar» abre la ruta pública de acceso', () => {
    montar('registro');

    componente.openExternalLogin();

    expect(componente.view()).toBe('login');
    expect(navegar).toHaveBeenCalledWith(['/ingresar']);
  });

  it('la dirección de acceso determina la pestaña visible al cargar', () => {
    montar('ingresar');
    expect(componente.view()).toBe('login');
    TestBed.resetTestingModule();
    montar('registro');
    expect(componente.view()).toBe('register');
  });

  it('la contraseña nace tapada y el control la muestra u oculta', () => {
    montar('ingresar');

    expect(componente.mostrarContrasena()).toBeFalse();
    componente.alternarMostrarContrasena();
    expect(componente.mostrarContrasena()).toBeTrue();
    componente.alternarMostrarContrasena();
    expect(componente.mostrarContrasena()).toBeFalse();
  });

  it('integra el control de visibilidad en el campo, sin una casilla separada', () => {
    montar('ingresar');
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector<HTMLInputElement>('#acceso-contrasena')?.type).toBe('password');
    expect(raiz.querySelector<HTMLButtonElement>('[aria-label="Mostrar contraseña"]')).not.toBeNull();
    expect(raiz.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it('no revela si una cuenta existe cuando el acceso falla', () => {
    montar('ingresar');
    (componente as any).fallarInicioSesion({ status: 401, message: 'La cuenta no existe.' });

    expect(componente.error()).toBe('El correo electrónico o la contraseña no son correctos.');
  });
});
