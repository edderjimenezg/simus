import { ComponentFixture, TestBed, fakeAsync, flush, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AsistenteDeFestivalComponent } from './asistente-de-festival.component';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';
import { ExternalSessionService } from '../../core/services/external-session.service';
import {
  CatalogoDelFestival,
  CatalogosDelFestival,
  FalloDelServidor,
  FestivalDeLaOrganizacion,
  GuardarFestivalSolicitud,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  ResponsableOrganizacion,
  TipoDocumento,
  UbicacionDivipola,
} from './panel-organizacion.api';

/**
 * El asistente de alta de Festival, en /ecosistema/mi-panel/ecosistema/nuevo.
 *
 * <b>REVIERTE, SOLO PARA CREAR, LA DECISIÓN DEL 29 DE AGOSTO DE 2026.</b> Ver el comentario de
 * `asistente-de-festival.component.ts`. Estas pruebas cubren los tres pasos, la validación
 * agregada del paso 4 -las mismas dos reglas de `FichaFestivalComponent.validar()`, no unas
 * nuevas-, y que guardar crea el Festival y nada más.
 */
const PRACTICA: CatalogoDelFestival = { id: 1, nombre: 'Música tradicional' };
const TERRITORIO: CatalogoDelFestival = { id: 2, nombre: 'Pacífico' };

const CATALOGOS: CatalogosDelFestival = {
  practicasMusicales: [PRACTICA], territoriosSonoros: [TERRITORIO], tipologias: [],
  expresionesArtisticas: [], fuentesFinanciacion: [], modalidadesParticipacion: [],
  naturalezasEntidad: [], tiposIngreso: [], tiposOrganizador: [], zonasUrbanoRural: [],
  titulacionesColectivas: [], regionesOcad: [],
};

const DIVIPOLA: UbicacionDivipola[] = [
  { departmentCode: '05', departmentName: 'Antioquia', municipalityCode: '05001', municipalityName: 'Medellín' },
];

class ApiFalso extends PanelOrganizacionApi {
  festivalCreado: GuardarFestivalSolicitud | null = null;
  falloAlCrear: FalloDelServidor | null = null;

  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> { return of(CATALOGOS); }
  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> { return of(DIVIPOLA); }

  override crearFestival(_organizacionId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    this.festivalCreado = solicitud;
    if (this.falloAlCrear) return throwError(() => this.falloAlCrear);
    return of({ id: '501', nombre: solicitud.nombre, estado: 'Borrador' });
  }

  override obtenerPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de otra ruta.'); }
  override guardarPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de otra ruta.'); }
  override obtenerResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override guardarResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override obtenerTiposDocumento(): Observable<TipoDocumento[]> { throw new Error('Los tipos de documento son de otra ruta.'); }
  override obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> { throw new Error('La lista es de otra ruta.'); }
  override enviarFestivalARevision(): Observable<FestivalDeLaOrganizacion> { throw new Error('Enviar a revisión es de otra ruta.'); }
  override enviarPropuestaARevision(): Observable<unknown> { throw new Error('Enviar la propuesta es de otra ruta.'); }
  override iniciarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override obtenerPropuestaActiva(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override guardarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override obtenerNotificaciones(): Observable<PaginaDeNotificaciones> { throw new Error('Las notificaciones son de otra ruta.'); }
  override marcarNotificacionLeida(): Observable<NotificacionDelPanel> { throw new Error('Las notificaciones son de otra ruta.'); }
  override guardarFestival(): Observable<FestivalDeLaOrganizacion> { throw new Error('El asistente solo crea, no edita.'); }
}

describe('AsistenteDeFestivalComponent', () => {
  let fixture: ComponentFixture<AsistenteDeFestivalComponent>;
  let componente: AsistenteDeFestivalComponent;
  let api: ApiFalso;
  let navegar: jasmine.Spy;
  let http: HttpTestingController;

  function montar(
    paso: string | null = null,
    borradorRecuperado?: { id: string; datosJson: string; version: number },
    correoConfirmado = true,
  ): void {
    api = new ApiFalso();
    TestBed.configureTestingModule({
      imports: [AsistenteDeFestivalComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PanelOrganizacionApi, useValue: api },
        {
          provide: ExternalSessionService,
          useValue: { organizaciones: () => [{ id: '117', name: 'Organización de prueba', role: 'responsable' }] },
        },
        PanelOrganizacionStore,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(paso ? { paso } : {}) } },
        },
      ],
    });
    TestBed.inject(PanelOrganizacionStore).organizacionId.set('117');
    // EL ESTADO DE LA CONFIRMACION LO CARGA EL PANEL AL ENTRAR, no esta pantalla: aquí se deja ya
    // resuelto, que es como llega en la aplicación real. Desde sin
    // correo confirmado no se registran procesos, y el asistente no ofrece el formulario.
    TestBed.inject(ConfirmacionDeCorreoService).estado.set({
      userId: '9', fullName: 'Responsable', email: 'responsable@example.com',
      accountStatus: 'activo', correoConfirmado,
    });
    navegar = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(AsistenteDeFestivalComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    // `ngOnInit` busca un borrador privado ya guardado (`cargarBorradorPrivado()`); en una fixture
    // recién montada no hay ninguno. Se resuelve aquí mismo, ANTES de que corra código de prueba
    // dentro de `fakeAsync`, porque un XHR real sin responder revienta esa zona con "Cannot make
    // XHRs from within a fake async test".
    http.match(peticion => peticion.url.includes('/festivales/borrador'))
      .forEach(peticion => borradorRecuperado
        ? peticion.flush(borradorRecuperado)
        : peticion.flush(null, { status: 404, statusText: 'Not Found' }));
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const botonPorTexto = (texto: string): HTMLButtonElement | undefined =>
    Array.from(raiz().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === texto);

  it('con el correo sin confirmar no enseña el formulario: explica y ofrece resolverlo', () => {
    montar(null, undefined, false);

    // NO ES UN «NO TIENE PERMISOS»: quien lee esto SI administra su organización. Se le dice qué
    // falta, por qué, qué hacer y qué se habilita, y se le ofrecen las dos salidas en el sitio.
    expect(raiz().querySelector('[data-falta-confirmar]')).not.toBeNull();
    expect(raiz().textContent).not.toContain('Datos generales');
    expect(raiz().querySelector('[data-reenviar-confirmacion]')).not.toBeNull();
    expect(raiz().querySelector('[data-corregir-correo]')).not.toBeNull();
  });

  it('arranca en el paso 1, con los catálogos ya cargados', () => {
    montar();

    expect(componente.pasoActivo()).toBe(1);
    expect(componente.cargando()).toBeFalse();
    expect(raiz().textContent).toContain('Datos generales');
  });

  it('un ?paso= conocido abre ese paso', () => {
    montar('3');

    expect(componente.pasoActivo()).toBe(3);
    expect(raiz().textContent).toContain('Prácticas y territorios sonoros');
  });

  it('«Siguiente» avanza sin exigir ningún campo -la validación es agregada, no por paso-', () => {
    montar();

    botonPorTexto('Siguiente')?.click();
    fixture.detectChanges();

    expect(componente.pasoActivo()).toBe(2);
    expect(componente.errores()).toEqual({});
  });

  it('«Atrás» retrocede', () => {
    montar('2');

    botonPorTexto('Atrás')?.click();
    fixture.detectChanges();

    expect(componente.pasoActivo()).toBe(1);
  });

  it('al cambiar de paso desplaza el encabezado nuevo sin llevar la página al inicio', fakeAsync(() => {
    montar();
    const desplazar = spyOn(HTMLElement.prototype, 'scrollIntoView');

    botonPorTexto('Siguiente')?.click();
    fixture.detectChanges();
    tick();

    expect(componente.pasoActivo()).toBe(2);
    expect(desplazar).toHaveBeenCalledTimes(1);
    expect(desplazar.calls.mostRecent().object).toBe(raiz().querySelector<HTMLElement>('#paso2-titulo')!);
    expect(desplazar.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({ block: 'start' }));
  }));

  it('si hay un error desplaza y enfoca solo el primer campo inválido', fakeAsync(() => {
    montar('4');
    const desplazar = spyOn(HTMLElement.prototype, 'scrollIntoView');
    const enfocar = spyOn(HTMLElement.prototype, 'focus');

    componente.guardarBorrador();
    fixture.detectChanges();
    tick();

    const nombre = raiz().querySelector<HTMLElement>('#asistente-nombre')!;
    expect(componente.pasoActivo()).toBe(1);
    expect(desplazar).toHaveBeenCalledTimes(1);
    expect(desplazar.calls.mostRecent().object).toBe(nombre);
    expect(desplazar.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({ block: 'center' }));
    expect(enfocar.calls.mostRecent().object).toBe(nombre);
    expect(enfocar.calls.mostRecent().args[0]).toEqual({ preventScroll: true });
  }));

  it('el territorio se pide según el nivel de cobertura, misma regla que la ficha', () => {
    montar('2');

    // El nivel por omisión es «municipal» -mismo valor de partida que `FichaFestivalComponent`-,
    // así que los dos campos ya están a la vista al llegar al paso.
    expect(raiz().querySelector('#asistente-departamento')).not.toBeNull();
    expect(raiz().querySelector('#asistente-municipio')).not.toBeNull();

    componente.formulario.nivelCobertura = 'nacional';
    componente.alCambiarNivel();
    fixture.detectChanges();

    // Nacional: ninguno de los dos se pide, y limpia lo que hubiera tecleado antes.
    expect(raiz().querySelector('#asistente-departamento')).toBeNull();
    expect(raiz().querySelector('#asistente-municipio')).toBeNull();
    expect(componente.formulario.codigoDepartamento).toBe('');
    expect(componente.formulario.codigoMunicipio).toBe('');

    componente.formulario.nivelCobertura = 'departamental';
    componente.alCambiarNivel();
    fixture.detectChanges();

    // Departamental: solo el departamento.
    expect(raiz().querySelector('#asistente-departamento')).not.toBeNull();
    expect(raiz().querySelector('#asistente-municipio')).toBeNull();
  });

  it('las casillas del paso 3 alternan sin duplicar ni perder ids', () => {
    montar('3');

    componente.alternarPractica(PRACTICA.id);
    expect(componente.formulario.practicasMusicalesIds).toEqual([PRACTICA.id]);
    componente.alternarPractica(PRACTICA.id);
    expect(componente.formulario.practicasMusicalesIds).toEqual([]);
  });

  it('guardar sin nombre vuelve al paso 1 con el error marcado, sin llamar al servidor', () => {
    montar('4');

    componente.guardarBorrador();
    fixture.detectChanges();

    expect(componente.pasoActivo()).toBe(1);
    expect(componente.errores()['nombre']).toBeTruthy();
    expect(api.festivalCreado).toBeNull();
  });

  it('guardar sin municipio, con nivel municipal, vuelve al paso 2', () => {
    montar('4');
    componente.formulario.nombre = 'Festival de Gaitas';
    componente.formulario.nivelCobertura = 'municipal';
    componente.formulario.codigoDepartamento = '05';

    componente.guardarBorrador();
    fixture.detectChanges();

    expect(componente.pasoActivo()).toBe(2);
    expect(componente.errores()['codigoMunicipio']).toBeTruthy();
  });

  it('guardar bien crea el Festival y navega a la ficha creada', fakeAsync(() => {
    montar('4');
    componente.formulario.nombre = 'Festival de Gaitas';
    componente.formulario.nivelCobertura = 'nacional';
    componente.formulario.practicasMusicalesIds = [PRACTICA.id];

    componente.guardarBorrador();
    tick();

    expect(api.festivalCreado).not.toBeNull();
    expect(api.festivalCreado!.nombre).toBe('Festival de Gaitas');
    expect(api.festivalCreado!.practicasMusicalesIds).toEqual([PRACTICA.id]);
    // Nacional: ni departamento ni municipio viajan, aunque se hubieran tecleado antes.
    expect(api.festivalCreado!.codigoDepartamento).toBeNull();
    expect(api.festivalCreado!.codigoMunicipio).toBeNull();

    expect(navegar).toHaveBeenCalledWith(
      ['/gestion/procesos/festivales'],
    );
  }));

  it('recupera un segundo registro con opcionales nulos y permite guardarlo', fakeAsync(() => {
    montar('4', {
      id: '40002',
      version: 34,
      datosJson: JSON.stringify({
        nombre: 'Segundo Festival',
        descripcion: 'Segundo registro consecutivo',
        periodicidad: 'anual',
        periodicidadDetalle: null,
        nivelCobertura: 'nacional',
        codigoDepartamento: null,
        codigoMunicipio: null,
        correoContacto: null,
        telefonoCelular: null,
        instagram: null,
        facebook: null,
        paginaWeb: null,
        otroEnlace: null,
        observacionesContacto: null,
        practicasMusicalesIds: [1, 1],
        territoriosSonorosIds: [2],
      }),
    });
    tick();
    fixture.detectChanges();

    expect(componente.formulario.otroEnlace).toBe('');
    expect(componente.formulario.correoContacto).toBe('');
    expect(componente.formulario.practicasMusicalesIds).toEqual([1]);
    expect(componente.estadoGuardado()).toBe('guardado');

    botonPorTexto('Guardar borrador')?.click();
    tick();

    expect(api.festivalCreado?.nombre).toBe('Segundo Festival');
    expect(api.festivalCreado?.borradorProcesoId).toBe('40002');
    expect(navegar).toHaveBeenCalledWith(['/gestion/procesos/festivales']);
  }));

  it('un fallo del servidor se dice y no navega a ninguna parte', fakeAsync(() => {
    montar('4');
    componente.formulario.nombre = 'Festival de Gaitas';
    componente.formulario.nivelCobertura = 'nacional';
    api.falloAlCrear = { status: 500, message: 'No fue posible crear el Festival' };

    componente.guardarBorrador();
    tick();

    expect(componente.error()).toBe('No fue posible crear el Festival');
    expect(navegar).not.toHaveBeenCalled();
  }));

  it('«Cancelar» vuelve a la lista de Festivales', () => {
    montar();

    botonPorTexto('Cancelar')?.click();

    expect(navegar).toHaveBeenCalledWith(['/gestion/procesos/festivales']);
  });

  // ───────────────────── Los duplicados que se avisan al registrar ─────────────────────

  /**
   * Siembra una coincidencia y abre el recuadro que las enseña.
   *
   * LA BUSQUEDA LA DISPARA EL FORMULARIO, no la prueba: se rellenan nombre y territorio como lo
   * haría quien registra, y se contesta la petición que sale.
   */
  function coincidenciaDeTipo(tipo: string, organizador: string | null): void {
    componente.formulario.nombre = 'Festival de la Coincidencia';
    componente.formulario.codigoDepartamento = '05';
    componente.formulario.codigoMunicipio = '05001';
    // SE DISPARA COMO EN LA APLICACION: el temporizador de `programarGuardado()`, a 850 ms.
    componente.programarGuardado();
    tick(900);
    // El autoguardado del borrador sale por el mismo gesto y aquí no se mide. Se contesta solo lo
    // que siga vivo: el bucle de autoguardado cancela el envío anterior cuando encadena otro, y
    // responder a una petición cancelada revienta el arnés.
    http.match(p => p.url.includes('/borradores-proceso/')).forEach(p => { if (!p.cancelled) p.flush({ version: 2 }); });

    http.match(p => p.url.includes('/coincidencias-historicas')).filter(p => !p.cancelled).forEach(p => p.flush([{
      festivalId: '77', nombreFestival: 'Festival de la Coincidencia',
      descripcion: null, evidencias: ['Coincide en nombre y territorio.'],
      tipoCoincidencia: tipo, organizadorHistorico: organizador,
    }]));
    // La ficha pública de cada coincidencia se pide aparte; aquí no se mide.
    http.match(p => p.url.includes('/publico/festivales/')).forEach(p => { if (!p.cancelled) p.flush(null, { status: 404, statusText: 'Not Found' }); });
    tick(900);
    componente.abrirModalDeCoincidencias();
    fixture.detectChanges();
  }

  it('un registro histórico sin dueño SÍ ofrece solicitar su administración', fakeAsync(() => {
    montar();
    coincidenciaDeTipo('HistoricoReclamable', null);

    expect(botonPorTexto('Solicitar administración')).withContext('es reclamable').toBeDefined();
    expect(raiz().querySelector('[data-testid="coincidencia-no-reclamable"]')).toBeNull();
    flush();
  }));

  it('un Festival que ya administra otra organización NO ofrece reclamarlo, y dice quién es', fakeAsync(() => {
    // <b>ANTES TODAS LAS COINCIDENCIAS SE PINTABAN IGUAL</b>, con su formulario de «Solicitar
    // administración» debajo. Ese trámite existe para adoptar un registro HISTORICO sin dueño; sobre
    // uno que ya administra alguien, ofrecerlo invita a un camino que no es el suyo. Lo que hace
    // falta ahí es no registrarlo dos veces, y saber con quién hablar.
    //
    // MUTANTE QUE MATA: pintar el formulario sin mirar `tipoCoincidencia`.
    montar();
    coincidenciaDeTipo('YaRegistradoPorOtraOrganizacion', 'Corporación Musical de Antioquia');

    expect(botonPorTexto('Solicitar administración')).withContext('no es reclamable').toBeUndefined();
    const aviso = raiz().querySelector('[data-testid="coincidencia-no-reclamable"]')!;
    expect(aviso).not.toBeNull();
    expect(aviso.textContent).toContain('Corporación Musical de Antioquia');
    flush();
  }));

  it('un Festival propio dice que ya está en el panel, y que se edite en vez de repetirlo', fakeAsync(() => {
    montar();
    coincidenciaDeTipo('YaRegistradoPorTuOrganizacion', null);

    expect(botonPorTexto('Solicitar administración')).toBeUndefined();
    expect(raiz().querySelector('[data-testid="coincidencia-no-reclamable"]')!.textContent)
      .toContain('ya está en tu panel');
    // Y UN BORRADOR SIN FICHA PUBLICA NO SE CUENTA COMO FALLO. El 404 de `/publico/festivales/{id}`
    // es lo normal para algo sin publicar; decir «no fue posible cargar» convierte una situación
    // corriente en un error aparente, y encima donde alguien decide si su registro está duplicado.
    expect(raiz().textContent).toContain('Todavía no tiene ficha pública');
    expect(raiz().textContent).not.toContain('No fue posible cargar el detalle público');
    flush();
  }));

});
