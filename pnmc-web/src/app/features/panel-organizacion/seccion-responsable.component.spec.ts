import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { ExternalSessionService } from '../../core/services/external-session.service';
import { SeccionResponsableComponent } from './seccion-responsable.component';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import {
  CatalogosDelFestival,
  FalloDelServidor,
  FestivalDeLaOrganizacion,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  ResponsableOrganizacion,
  ResponsableOrganizacionSolicitud,
  TipoDocumento,
  UbicacionDivipola,
} from './panel-organizacion.api';

/**
 * La ruta «Persona responsable», en /ecosistema/mi-panel/responsable.
 *
 * <b>SEPARADA DE `SeccionOrganizacionComponent`.</b> Este fichero hereda
 * las pruebas que hasta entonces vivían en `seccion-organizacion.component.spec.ts` bajo el bloque
 * «9. La persona responsable», adaptadas a la nueva alimentación: la sección inyecta
 * `PanelOrganizacionStore` para `organizacionId` y pide su propia fila a `PanelOrganizacionApi`, ya
 * no recibe nada por `@Input`.
 *
 * SIN HTTP. Se inyecta un doble de `PanelOrganizacionApi`.
 * EL `tick()` NO ES ADORNO: los campos llevan `ngModel` y `NgModel` aplaza a una microtarea el
 * volcado del modelo al elemento.
 */

/**
 * La fila de `dbo.EntidadesResponsable`.
 *
 * `responsableTipoDocumento` va en MAYÚSCULA porque así están las filas reales de la base, mientras
 * que `dbo.TiposDocumento` guarda 'cc'. El desajuste es real y lo resuelve
 * `sembrarFormularioResponsable` bajando a minúscula.
 *
 * `responsableDesde` va SIN la `Z` a propósito: con marca de UTC, el `DatePipe` la pasa a la zona
 * del navegador y la prueba diría un día distinto según dónde corra.
 */
const RESPONSABLE: ResponsableOrganizacion = {
  idEntidad: '117',
  responsableNombre: 'Ana Restrepo',
  responsableTipoDocumento: 'CC',
  responsableTipoDocumentoEtiqueta: 'Cédula de ciudadanía',
  responsableNumeroDocumento: '43987654',
  responsableCorreo: 'ana@fmv.org',
  responsableTelefono: '3009998877',
  responsableDesde: '2026-02-01T00:00:00',
  responsableAutorizacionDatos: true,
};

const TIPOS_DOCUMENTO: TipoDocumento[] = [
  { codigo: 'cc', nombre: 'Cédula de ciudadanía' },
  { codigo: 'ce', nombre: 'Cédula de extranjería' },
];

const sesionFalsa = {
  organizaciones: signal([
    { id: '117', name: 'Fundación Musical del Valle', role: 'administrador' },
  ]),
};

/**
 * El doble del API. Solo lo que esta ruta toca desde el rediseño: la persona responsable y los
 * tipos de documento. Las demás llamadas lanzan, con el mismo criterio que el resto de secciones.
 */
class ApiFalso extends PanelOrganizacionApi {
  responsableGuardado: ResponsableOrganizacionSolicitud | null = null;
  falloAlGuardarResponsable: FalloDelServidor | null = null;
  responsableInicial: ResponsableOrganizacion = RESPONSABLE;
  falloDelResponsable: FalloDelServidor | null = null;

  override obtenerResponsable(): Observable<ResponsableOrganizacion> {
    if (this.falloDelResponsable) return throwError(() => this.falloDelResponsable);
    return of(this.responsableInicial);
  }

  override guardarResponsable(_organizacionId: string, solicitud: ResponsableOrganizacionSolicitud): Observable<ResponsableOrganizacion> {
    this.responsableGuardado = solicitud;
    if (this.falloAlGuardarResponsable) return throwError(() => this.falloAlGuardarResponsable);
    return of({
      ...this.responsableInicial,
      responsableNombre: solicitud.responsableNombre,
      responsableTipoDocumento: solicitud.responsableTipoDocumento,
      responsableNumeroDocumento: solicitud.responsableNumeroDocumento,
      responsableTelefono: solicitud.responsableTelefono,
    });
  }

  override obtenerTiposDocumento(): Observable<TipoDocumento[]> {
    return of(TIPOS_DOCUMENTO);
  }

  override obtenerPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de Organización.'); }
  override guardarPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de Organización.'); }

  override obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> {
    throw new Error('Los Festivales son de otra ruta.');
  }

  override enviarFestivalARevision(): Observable<FestivalDeLaOrganizacion> {
    throw new Error('Los Festivales son de otra ruta.');
  }

  override enviarPropuestaARevision(): Observable<unknown> {
    throw new Error('Las propuestas son de otra ruta.');
  }

  override iniciarPropuesta(): Observable<never> { throw new Error('Las propuestas son de otra ruta.'); }
  override obtenerPropuestaActiva(): Observable<never> { throw new Error('Las propuestas son de otra ruta.'); }
  override guardarPropuesta(): Observable<never> { throw new Error('Las propuestas son de otra ruta.'); }

  override obtenerNotificaciones(): Observable<PaginaDeNotificaciones> {
    throw new Error('Las notificaciones son de otra ruta.');
  }

  override marcarNotificacionLeida(): Observable<NotificacionDelPanel> {
    throw new Error('Las notificaciones son de otra ruta.');
  }

  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> {
    throw new Error('Los catálogos del Festival son de otra ruta.');
  }

  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> {
    throw new Error('DIVIPOLA es de Organización.');
  }

  override crearFestival(): Observable<FestivalDeLaOrganizacion> {
    throw new Error('El alta de Festival es de otra ruta.');
  }

  override guardarFestival(): Observable<FestivalDeLaOrganizacion> {
    throw new Error('El alta de Festival es de otra ruta.');
  }
}

describe('SeccionResponsableComponent · por omisión se lee, se edita bajo petición', () => {
  let fixture: ComponentFixture<SeccionResponsableComponent>;
  let componente: SeccionResponsableComponent;
  let api: ApiFalso;

  const CAMPOS_DEL_RESPONSABLE = [
    'responsable-nombre', 'responsable-tipo-documento', 'responsable-numero-documento',
    'responsable-telefono',
  ];

  function montar(): void {
    api = new ApiFalso();
    TestBed.configureTestingModule({
      imports: [SeccionResponsableComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: ExternalSessionService, useValue: sesionFalsa },
        PanelOrganizacionStore,
      ],
    });
    // Por DI, no con `new`: el constructor del store inyecta `ExternalSessionService`.
    TestBed.inject(PanelOrganizacionStore).organizacionId.set('117');

    fixture = TestBed.createComponent(SeccionResponsableComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const limpiar = (texto: string | null): string => (texto ?? '').replace(/\s+/g, ' ').trim();
  const buscar = <T extends HTMLElement>(testid: string): T | null =>
    raiz().querySelector<T>('[data-testid="' + testid + '"]');
  const porId = <T extends HTMLElement>(id: string): T | null => raiz().querySelector<T>('#' + id);
  const texto = (): string => limpiar(raiz().textContent);
  const formularios = (): HTMLFormElement[] => Array.from(raiz().querySelectorAll('form'));
  const botones = (): HTMLButtonElement[] => Array.from(raiz().querySelectorAll('button'));
  const rotulosDeBotones = (): string[] => botones().map(boton => limpiar(boton.textContent));

  function ficha(testid: string): Record<string, string> {
    const bloque = buscar(testid);
    if (!bloque) throw new Error('No está en pantalla el bloque de lectura «' + testid + '».');
    const salida: Record<string, string> = {};
    for (const fila of Array.from(bloque.querySelectorAll('dl > div'))) {
      const rotulo = fila.querySelector('dt');
      const valor = fila.querySelector('dd');
      if (rotulo && valor) salida[limpiar(rotulo.textContent)] = limpiar(valor.textContent);
    }
    return salida;
  }

  function valoresDelFormulario(ids: string[]): Record<string, string | null> {
    const salida: Record<string, string | null> = {};
    for (const id of ids) {
      const campo = porId<HTMLInputElement>(id);
      salida[id] = campo ? campo.value : null;
    }
    return salida;
  }

  function pulsar(testid: string): void {
    const boton = buscar<HTMLButtonElement>(testid);
    if (!boton) throw new Error('No está en pantalla el botón «' + testid + '».');
    boton.click();
    fixture.detectChanges();
  }

  function pulsarPorTexto(rotulo: string): void {
    const boton = botones().find(candidato => limpiar(candidato.textContent) === rotulo);
    if (!boton) throw new Error('No hay ningún botón rotulado «' + rotulo + '».');
    boton.click();
    fixture.detectChanges();
  }

  function escribir(id: string, valor: string): void {
    const campo = porId<HTMLInputElement>(id);
    if (!campo) throw new Error('No está en pantalla el campo #' + id + '.');
    campo.value = valor;
    campo.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function abrirEdicion(testid: string): void {
    pulsar(testid);
    tick();
  }

  it('al abrirse se lee la persona responsable y se ofrece editarla, sin formulario', () => {
    montar();

    expect(formularios().length).toBe(0);
    for (const id of CAMPOS_DEL_RESPONSABLE) {
      expect(porId(id)).withContext('sigue en pantalla el campo #' + id).toBeNull();
    }
    expect(buscar('responsable-lectura')).not.toBeNull();
    // EL NOMBRE ACCESIBLE, NO EL ROTULO VISIBLE: ver el comentario gemelo en
    // `seccion-organizacion.component.spec.ts`. El botón es ahora una opción junto al título y
    // dice «Editar»; quien lo oye sigue oyendo de qué es.
    expect(rotulosDeBotones()).toEqual(['Editar']);
    expect(botones().map(boton => boton.getAttribute('aria-label'))).toEqual(['Editar la persona responsable']);
  });

  it('la persona responsable se lee por omisión, con la etiqueta del documento', () => {
    // «cc» ES UN CÓDIGO DE `dbo.TiposDocumento`, no algo que se lea. Y los tres datos que el API no
    // deja escribir —correo, fecha y autorización— van en la misma lista con su motivo.
    montar();

    const datos = ficha('responsable-lectura');
    // La fecha se comprueba aparte: el `| date: 'd MMMM y'` va sin locale y hoy el mes sale en
    // inglés. Es un defecto reportado y NO arreglado aquí.
    expect(datos['Responsable desde']).toContain('2026');
    expect(datos['Responsable desde']).not.toBe('Sin registrar');
    delete datos['Responsable desde'];

    expect(datos).toEqual({
      'Nombre completo': 'Ana Restrepo',
      'Documento': 'Cédula de ciudadanía 43987654',
      'Teléfono': '3009998877',
      'Correo de la persona responsable': 'ana@fmv.org',
      'Autorización de tratamiento de datos': 'Otorgada',
    });

    expect(texto()).toContain('Es el correo con el que se inicia sesión');
    expect(texto()).toContain('Fecha desde la que esta persona quedó registrada como responsable');
    expect(texto()).toContain('Para retirarla, escríbele al equipo del PNMC');
  });

  it('sin número de documento la fila lo dice, en vez de dejar la etiqueta suelta', () => {
    api = new ApiFalso();
    api.responsableInicial = {
      ...RESPONSABLE, responsableNumeroDocumento: '', responsableTelefono: null,
      responsableCorreo: null, responsableDesde: null, responsableAutorizacionDatos: false,
    };
    TestBed.configureTestingModule({
      imports: [SeccionResponsableComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: ExternalSessionService, useValue: sesionFalsa },
        PanelOrganizacionStore,
      ],
    });
    TestBed.inject(PanelOrganizacionStore).organizacionId.set('117');
    fixture = TestBed.createComponent(SeccionResponsableComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    const datos = ficha('responsable-lectura');
    expect(datos['Documento']).toBe('Sin registrar');
    expect(datos['Teléfono']).toBe('Sin registrar');
    expect(datos['Correo de la persona responsable']).toBe('Sin registrar');
    expect(datos['Responsable desde']).toBe('Sin registrar');
    expect(datos['Autorización de tratamiento de datos']).toBe('Sin otorgar');
  });

  it('«Editar» abre los cuatro campos rellenos y el tipo en minúscula', fakeAsync(() => {
    // LA BASE GUARDA 'CC' Y EL CATÁLOGO GUARDA 'cc'. Sin bajarlo a minúscula, el `<select>` no
    // encuentra su `<option>` y guardar sin tocarlo enviaría el tipo vacío.
    montar();

    abrirEdicion('responsable-editar');

    expect(buscar('responsable-lectura')).toBeNull();
    expect(valoresDelFormulario(CAMPOS_DEL_RESPONSABLE)).toEqual({
      'responsable-nombre': 'Ana Restrepo',
      'responsable-tipo-documento': 'cc',
      'responsable-numero-documento': '43987654',
      'responsable-telefono': '3009998877',
    });
    expect(api.responsableGuardado).toBeNull();
  }));

  it('cancelar descarta lo tecleado', fakeAsync(() => {
    // Ley 1581 de 2012: un número de documento a medio teclear que sobreviviera al cancelar se
    // leería después como el que hay registrado.
    montar();

    abrirEdicion('responsable-editar');
    escribir('responsable-numero-documento', '11111111');
    pulsar('responsable-cancelar');

    expect(componente.formularioResponsable.responsableNumeroDocumento).toBe('43987654');
    expect(api.responsableGuardado).toBeNull();
    expect(ficha('responsable-lectura')['Documento']).toBe('Cédula de ciudadanía 43987654');

    abrirEdicion('responsable-editar');
    expect(porId<HTMLInputElement>('responsable-numero-documento')!.value).toBe('43987654');
  }));

  it('guardar vuelve a la lectura con el dato nuevo', fakeAsync(() => {
    montar();

    abrirEdicion('responsable-editar');
    escribir('responsable-nombre', 'Beatriz Osorio');
    pulsarPorTexto('Guardar persona responsable');

    expect(api.responsableGuardado!.responsableNombre).toBe('Beatriz Osorio');
    expect(formularios().length).toBe(0);
    expect(ficha('responsable-lectura')['Nombre completo']).toBe('Beatriz Osorio');
    expect(texto()).toContain('Los datos de la persona responsable quedaron guardados.');
  }));

  it('un error al guardar no cierra el formulario', fakeAsync(() => {
    montar();
    api.falloAlGuardarResponsable = {
      status: 422,
      message: 'El documento ya está registrado en otra organización.',
      payload: { errors: { responsableNumeroDocumento: ['Ese documento ya figura en otra ficha.'] } },
    };

    abrirEdicion('responsable-editar');
    escribir('responsable-numero-documento', '43987655');
    pulsarPorTexto('Guardar persona responsable');

    expect(porId<HTMLInputElement>('responsable-numero-documento')!.value).toBe('43987655');
    expect(buscar('responsable-lectura')).toBeNull();
    expect(texto()).toContain('El documento ya está registrado en otra organización.');
    expect(limpiar(porId('responsable-documento-error')!.textContent)).toBe('Ese documento ya figura en otra ficha.');
  }));

  it('sin fila de responsable no se ofrece editar y el aviso se queda en pantalla', () => {
    // HOY LE PASA A 17 DE LAS 19 ENTIDADES: se dieron de alta antes de que el registro pidiera el
    // dato. El 404 no es un fallo.
    api = new ApiFalso();
    api.falloDelResponsable = { status: 404, message: 'Recurso no encontrado' };
    TestBed.configureTestingModule({
      imports: [SeccionResponsableComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: ExternalSessionService, useValue: sesionFalsa },
        PanelOrganizacionStore,
      ],
    });
    TestBed.inject(PanelOrganizacionStore).organizacionId.set('117');
    fixture = TestBed.createComponent(SeccionResponsableComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    expect(componente.sinResponsable()).toBeTrue();
    expect(buscar('responsable-editar')).toBeNull();
    expect(buscar('responsable-lectura')).toBeNull();
    for (const id of CAMPOS_DEL_RESPONSABLE) {
      expect(porId(id)).withContext('sigue en pantalla el campo #' + id).toBeNull();
    }
    expect(texto()).toContain('no tiene una persona responsable registrada');
    expect(rotulosDeBotones()).toEqual([]);
    expect(componente.errorResponsable()).toBe('');
    expect(raiz().querySelector('[role="alert"]')).toBeNull();
  });

  it('un fallo que no es 404 se dice como error y tampoco ofrece editar a ciegas', () => {
    api = new ApiFalso();
    api.falloDelResponsable = { status: 500, message: 'No fue posible consultar la persona responsable' };
    TestBed.configureTestingModule({
      imports: [SeccionResponsableComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: ExternalSessionService, useValue: sesionFalsa },
        PanelOrganizacionStore,
      ],
    });
    TestBed.inject(PanelOrganizacionStore).organizacionId.set('117');
    fixture = TestBed.createComponent(SeccionResponsableComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    expect(componente.sinResponsable()).toBeFalse();
    expect(texto()).toContain('No fue posible consultar la persona responsable');
    expect(buscar('responsable-editar')).toBeNull();
    expect(texto()).toContain('Los datos de la persona responsable todavía no han llegado');
  });

  // ───────────────────────────── El foco en las transiciones ─────────────────────────────

  function montarEnElDocumento(): void {
    montar();
    document.body.appendChild(fixture.nativeElement);
  }

  afterEach(() => {
    fixture?.nativeElement?.remove?.();
  });

  it('«Editar» deja el foco en el primer campo, «Cancelar» lo devuelve al botón', fakeAsync(() => {
    montarEnElDocumento();
    tick();

    pulsar('responsable-editar');
    tick();
    expect(document.activeElement).toBe(porId('responsable-nombre'));

    pulsar('responsable-cancelar');
    tick();
    expect(document.activeElement).toBe(buscar('responsable-editar'));
  }));
});
