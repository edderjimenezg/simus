import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { FichaDeFestivalPageComponent } from './ficha-de-festival-page.component';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { CambiosPedidosApi } from '../../core/revision-de-campos/cambios-pedidos.api';
import { ObservacionDeCampo, RevisionDeCampos } from '../../core/revision-de-campos/revision-de-campos';
import {
  CatalogosDelFestival,
  EntradaHistorialFestival,
  FalloDelServidor,
  FestivalDeLaOrganizacion,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  ResponsableOrganizacion,
  TipoDocumento,
  UbicacionDivipola,
} from './panel-organizacion.api';

/**
 * Pruebas de composición de `/gestion/procesos/festivales/{id}`. El formulario canónico conserva
 * sus pruebas propias; aquí se verifica la navegación contextual y su integración con el registro.
 */
const CATALOGOS_VACIOS: CatalogosDelFestival = {
  practicasMusicales: [], territoriosSonoros: [], tipologias: [], expresionesArtisticas: [],
  fuentesFinanciacion: [], modalidadesParticipacion: [], naturalezasEntidad: [], tiposIngreso: [],
  tiposOrganizador: [], zonasUrbanoRural: [], titulacionesColectivas: [], regionesOcad: [],
};

class ApiFalso extends PanelOrganizacionApi {
  festivales: FestivalDeLaOrganizacion[] = [];
  falloFestivales: FalloDelServidor | null = null;
  enviosARevision = 0;

  override obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> {
    if (this.falloFestivales) return throwError(() => this.falloFestivales);
    return of(this.festivales);
  }

  // `app-ficha-festival` monta de verdad dentro de «Información»/«Ediciones»: catálogos y
  // ubicaciones vacíos, no lanzar, para que su `ngOnInit` no reviente la prueba.
  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> { return of(CATALOGOS_VACIOS); }
  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> { return of([]); }
  override obtenerHistorialFestival(): Observable<EntradaHistorialFestival[]> { return of([]); }

  override obtenerPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de otra ruta.'); }
  override guardarPerfil(): Observable<PerfilOrganizacion> { throw new Error('El perfil es de otra ruta.'); }
  override obtenerResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override guardarResponsable(): Observable<ResponsableOrganizacion> { throw new Error('La persona responsable es de otra ruta.'); }
  override obtenerTiposDocumento(): Observable<TipoDocumento[]> { throw new Error('Los tipos de documento son de otra ruta.'); }
  override enviarFestivalARevision(festivalId: string): Observable<FestivalDeLaOrganizacion> {
    this.enviosARevision++;
    const actual = this.festivales.find(item => item.id === festivalId)!;
    const enviado = { ...actual, estado: 'EnRevision', cambiosPedidos: 0 };
    this.festivales = this.festivales.map(item => item.id === festivalId ? enviado : item);
    return of(enviado);
  }
  override enviarPropuestaARevision(): Observable<unknown> { throw new Error('Enviar la propuesta es de otra ruta.'); }
  override iniciarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override obtenerPropuestaActiva(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override guardarPropuesta(): Observable<never> { throw new Error('Las propuestas de cambio son de otra ruta.'); }
  override obtenerNotificaciones(): Observable<PaginaDeNotificaciones> { throw new Error('Las notificaciones son de otra ruta.'); }
  override marcarNotificacionLeida(): Observable<NotificacionDelPanel> { throw new Error('Las notificaciones son de otra ruta.'); }
  override crearFestival(): Observable<FestivalDeLaOrganizacion> { throw new Error('El alta es de otra ruta.'); }
  override guardarFestival(): Observable<FestivalDeLaOrganizacion> { throw new Error('No se ejerce en estas pruebas.'); }
}

class CambiosPedidosFalso {
  respuesta: RevisionDeCampos | null = null;
  fallo: FalloDelServidor | null = null;
  atendidas: { id: number; atendida: boolean }[] = [];

  obtener(): Observable<RevisionDeCampos> {
    if (this.fallo) return throwError(() => this.fallo);
    return of(this.respuesta as RevisionDeCampos);
  }

  atender(id: number, atendida: boolean): Observable<ObservacionDeCampo> {
    this.atendidas.push({ id, atendida });
    const nota = this.respuesta?.observaciones.find(item => item.id === id);
    if (!nota) throw new Error('No existe la observación de prueba.');
    const actualizada: ObservacionDeCampo = { ...nota, estado: atendida ? 'atendida' : 'pendiente', fechaAtencion: atendida ? '2026-09-09T18:00:00Z' : null };
    if (this.respuesta) {
      this.respuesta = { ...this.respuesta, observaciones: this.respuesta.observaciones.map(item => item.id === id ? actualizada : item) };
    }
    return of(actualizada);
  }
}

describe('FichaDeFestivalPageComponent', () => {
  let fixture: ComponentFixture<FichaDeFestivalPageComponent>;
  let componente: FichaDeFestivalPageComponent;
  let api: ApiFalso;
  let cambiosPedidos: CambiosPedidosFalso;
  let parametrosDeRuta: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let parametrosDeConsulta: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(() => {
    api = new ApiFalso();
    cambiosPedidos = new CambiosPedidosFalso();
  });

  /**
   * NO REEMPLAZA `api`/`cambiosPedidos`: `beforeEach` ya les dio un valor por omisión, y cada
   * prueba puede personalizarlos -reasignando la misma variable, no creando una nueva- ANTES de
   * llamar a `montar()`. Crearlos aquí dentro pisaría lo que la prueba acaba de configurar.
   */
  function montar(id: string, queryParams: Record<string, string> = {}): void {
    parametrosDeRuta = new BehaviorSubject(convertToParamMap({ id }));
    parametrosDeConsulta = new BehaviorSubject(convertToParamMap(queryParams));
    const store = {
      organizacionId: signal('117'),
      organizacionElegida: () => ({ id: '117', name: 'Organización de prueba' }),
    };
    TestBed.configureTestingModule({
      imports: [FichaDeFestivalPageComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: CambiosPedidosApi, useValue: cambiosPedidos },
        { provide: PanelOrganizacionStore, useValue: store },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id }),
              queryParamMap: convertToParamMap(queryParams),
              url: [{ path: id }],
            },
            paramMap: parametrosDeRuta,
            queryParamMap: parametrosDeConsulta,
          },
        },
      ],
    });
    spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(FichaDeFestivalPageComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('encuentra el Festival por id dentro de la lista de la organización', () => {
    api = new ApiFalso();
    api.festivales = [
      { id: '1', nombre: 'Otro', estado: 'Publicado' },
      { id: '2', nombre: 'Festival de Gaitas', estado: 'Borrador' },
    ];
    montar('2');

    expect(componente.festival()?.nombre).toBe('Festival de Gaitas');
    expect(raiz().textContent).toContain('Festival de Gaitas');
  });

  it('un id que no está en la lista de la organización se dice, no se pinta en blanco', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '1', nombre: 'Otro', estado: 'Publicado' }];
    montar('999');

    expect(componente.festival()).toBeNull();
    expect(raiz().textContent).toContain('No se encontró este Festival');
  });

  it('un fallo al consultar los Festivales se dice', () => {
    api = new ApiFalso();
    api.falloFestivales = { status: 500, message: 'No fue posible consultar los Festivales de la organización' };
    montar('2');

    expect(raiz().textContent).toContain('No fue posible consultar los Festivales de la organización');
  });

  it('sin ?seccion= arranca en Resumen', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado' }];
    montar('2');

    expect(componente.seccionActiva()).toBe('resumen');
  });

  it('expone una sola navegación contextual con las cuatro áreas permanentes', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado' }];
    montar('2');

    const navegacion = raiz().querySelector('nav[aria-label="Secciones del Festival"]');
    expect(navegacion).not.toBeNull();
    expect(navegacion?.textContent).toContain('Vista general');
    expect(navegacion?.textContent).toContain('Información');
    expect(navegacion?.textContent).toContain('Ediciones');
    expect(navegacion?.textContent).toContain('Historial');
    expect(navegacion?.textContent).not.toContain('Ajustes');
    expect(raiz().querySelectorAll('app-ficha-festival').length).toBe(0);
  });

  it('un ?seccion= conocido abre esa sección permanente', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado' }];
    montar('2', { seccion: 'informacion' });

    expect(componente.seccionActiva()).toBe('informacion');
    expect(raiz().querySelector('[aria-current="page"]')?.textContent).toContain('Información');
  });

  it('reacciona a un aviso de ajustes aunque Angular reutilice la ficha ya abierta', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 1 }];
    montar('2', { seccion: 'resumen' });

    parametrosDeConsulta.next(convertToParamMap({ seccion: 'informacion', ajustes: '1' }));
    fixture.detectChanges();

    expect(componente.seccionActiva()).toBe('informacion');
    expect(raiz().querySelector('[aria-current="page"]')?.textContent).toContain('Información');
  });

  it('carga el Festival del aviso aunque Angular reutilice la ruta con otro id', () => {
    api = new ApiFalso();
    api.festivales = [
      { id: '2', nombre: 'Festival inicial', estado: 'Borrador' },
      { id: '3002', nombre: 'Festival del aviso', estado: 'AjustesSolicitados', cambiosPedidos: 1 },
    ];
    montar('2');

    parametrosDeRuta.next(convertToParamMap({ id: '3002' }));
    parametrosDeConsulta.next(convertToParamMap({ seccion: 'informacion', ajustes: '1' }));
    fixture.detectChanges();

    expect(componente.festival()?.id).toBe('3002');
    expect(raiz().textContent).toContain('Festival del aviso');
    expect(componente.seccionActiva()).toBe('informacion');
  });

  it('?mensaje=creado muestra el aviso de que el Festival quedó creado', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Borrador' }];
    montar('2', { mensaje: 'creado' });

    expect(raiz().textContent).toContain('quedó creado como borrador');
  });

  it('«Información» incrusta la ficha forzada en la pestaña «festival», sin su propio tablist', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado' }];
    montar('2', { seccion: 'informacion' });

    const ficha = raiz().querySelector('app-ficha-festival')!;
    expect(ficha).not.toBeNull();
    // Sin `role="tablist"` propio: la pestaña la elige esta página, no el diálogo incrustado.
    expect(ficha.querySelector('[role="tablist"]')).toBeNull();
    // Sin fondo de diálogo: `modoIncrustado` retira el `data-testid` del fondo modal.
    expect(raiz().querySelector('[data-testid="ficha-festival-fondo"]')).toBeNull();
    expect(raiz().querySelector('[data-testid="ficha-festival-incrustada"]')).not.toBeNull();
  });

  it('?editar=1 abre la ficha incrustada directo en edición', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Borrador' }];
    montar('2', { seccion: 'informacion', editar: '1' });

    expect(componente.abrirEnEdicion()).toBeTrue();
  });

  it('conserva en la URL lógica el campo exacto al abrir una corrección', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados' }];
    montar('2', { seccion: 'informacion', editar: '1', campo: 'festival.correo' });

    expect(componente.abrirEnEdicion()).toBeTrue();
    expect(componente.campoInicial()).toBe('festival.correo');
  });

  it('sin cambios pedidos ni ajustes solicitados, la revisión no pide nada al servidor', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado', cambiosPedidos: 0 }];
    spyOn(cambiosPedidos, 'obtener').and.callThrough();
    montar('2');

    expect(cambiosPedidos.obtener).not.toHaveBeenCalled();
    expect(componente.revision.modo()).toBe('lectura');
  });

  it('Información reúne la lectura, las observaciones y la edición en una sola ruta', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 1 }];
    cambiosPedidos.respuesta = {
      id: 1, moduloId: 'festivales', registroId: '2', registroNombre: 'X', estado: 'devuelta', observacionGeneral: null,
      observaciones: [{
        id: 10, ambito: 'principal', subregistroId: null, seccionId: 'generales', campoId: 'festival.nombre',
        campoEtiqueta: 'Nombre del festival', valorObservado: 'X', nota: 'Corrige el nombre.',
        estado: 'pendiente', fechaAtencion: null,
      }],
    } as unknown as RevisionDeCampos;
    montar('2', { seccion: 'informacion', ajustes: '1' });

    expect(componente.seccionActiva()).toBe('informacion');
    expect(componente.inspectorAjustesAbierto()).toBeFalse();
    expect(componente.revision.hayNotas()).toBeTrue();
    expect(raiz().textContent).not.toContain('0 de 1 resueltos');
    expect(raiz().textContent).not.toContain('Corrige el nombre.');
    const indicador = raiz().querySelector<HTMLButtonElement>('button[aria-label="Revisar ajuste sugerido para Nombre del festival"]');
    expect(indicador).not.toBeNull();

    indicador!.click();
    fixture.detectChanges();

    expect(componente.inspectorAjustesAbierto()).toBeTrue();
    expect(raiz().textContent).toContain('Corrige el nombre.');

    const enlace = Array.from(raiz().querySelectorAll('button'))
      .find(b => (b.textContent ?? '').includes('Editar este campo'));
    expect(enlace).withContext('falta la acción directa de edición').toBeTruthy();
    expect(raiz().querySelector('[data-testid="ficha-editar"]'))
      .withContext('el inspector debe ser la única ruta de edición mientras está abierto').toBeNull();
    enlace!.click();
    fixture.detectChanges();
    expect(componente.editandoAjuste()).toBeTrue();
    expect(componente.campoInicial()).toBe('festival.nombre');
    const inspector = raiz().querySelector('#inspector-ajustes')!;
    const editor = inspector.querySelector('[data-testid="editor-contextual-del-ajuste"]')!;
    expect(editor).withContext('el campo debe editarse dentro del inspector').not.toBeNull();
    expect(editor.querySelector('#ficha-nombre-festival')).not.toBeNull();
    expect(editor.querySelector('#ficha-descripcion-festival')?.closest('div')?.classList.contains('hidden'))
      .withContext('los controles ajenos al ajuste no deben ocupar espacio').toBeTrue();
    expect(raiz().querySelectorAll('app-ficha-festival').length)
      .withContext('la lectura principal permanece visible junto al editor contextual').toBe(2);
    expect(raiz().textContent).not.toContain('Ya lo corregí');
  });

  it('seleccionar otra observación no navega ni altera el desplazamiento mediante el Router', () => {
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 2 }];
    cambiosPedidos.respuesta = {
      id: 1, moduloId: 'festivales', registroId: '2', registroNombre: 'X', estado: 'enviada', observacionGeneral: null,
      revisorNombre: null, destinatarioNombre: null, organizacionNombre: null,
      fechaActualizacion: null, fechaEnvio: null,
      observaciones: [
        { id: 10, ambito: 'principal', subregistroId: null, seccionId: 'generales', campoId: 'festival.nombre', campoEtiqueta: 'Nombre', valorObservado: 'X', nota: 'Ajusta el nombre.', estado: 'pendiente', fechaAtencion: null },
        { id: 11, ambito: 'principal', subregistroId: null, seccionId: 'generales', campoId: 'festival.descripcion', campoEtiqueta: 'Descripción', valorObservado: 'Texto', nota: 'Amplía la descripción.', estado: 'pendiente', fechaAtencion: null },
      ],
    };
    montar('2', { seccion: 'informacion', ajustes: '1' });
    const navegar = TestBed.inject(Router).navigate as jasmine.Spy;
    navegar.calls.reset();

    componente.seleccionarAjuste(componente.revision.notas()[1]);
    fixture.detectChanges();

    expect(componente.ajusteSeleccionado()?.id).toBe(11);
    expect(navegar).not.toHaveBeenCalled();
    expect(raiz().querySelector('#inspector-ajustes')?.textContent).toContain('Amplía la descripción.');
  });

  it('el inspector usa anchos flexibles y contiene textos extensos sin desbordar la página', () => {
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 1 }];
    cambiosPedidos.respuesta = {
      id: 1, moduloId: 'festivales', registroId: '2', registroNombre: 'X', estado: 'enviada', observacionGeneral: null,
      revisorNombre: null, destinatarioNombre: null, organizacionNombre: null,
      fechaActualizacion: null, fechaEnvio: null,
      observaciones: [{
        id: 10, ambito: 'principal', subregistroId: null, seccionId: 'contacto-festival', campoId: 'festival.telefonoCelular',
        campoEtiqueta: 'Celular', valorObservado: '3000000000', nota: 'Una observación deliberadamente extensa para comprobar el ajuste del texto dentro del inspector.',
        estado: 'pendiente', fechaAtencion: null,
      }],
    };
    montar('2', { seccion: 'informacion', ajustes: '1' });

    componente.seleccionarAjuste(componente.revision.notas()[0]);
    fixture.detectChanges();

    const inspector = raiz().querySelector('#inspector-ajustes')!;
    expect(inspector.classList.contains('min-w-0')).toBeTrue();
    expect(inspector.classList.contains('max-w-full')).toBeTrue();
    expect(inspector.classList.contains('overflow-x-hidden')).toBeTrue();
    expect(inspector.querySelector('.break-words')).not.toBeNull();
  });

  it('guardar el campo resuelve su observación sin una confirmación manual adicional', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 1 }];
    cambiosPedidos.respuesta = {
      id: 1, moduloId: 'festivales', registroId: '2', registroNombre: 'X', estado: 'enviada', observacionGeneral: null,
      revisorNombre: null, destinatarioNombre: null, organizacionNombre: null,
      fechaActualizacion: null, fechaEnvio: null,
      observaciones: [{
        id: 10, ambito: 'principal', subregistroId: null, seccionId: 'generales', campoId: 'festival.nombre',
        campoEtiqueta: 'Nombre del festival', valorObservado: 'X', nota: 'Corrige el nombre.',
        estado: 'pendiente', fechaAtencion: null,
      }],
    };
    montar('2', { seccion: 'informacion', ajustes: '1', ajuste: '10', editar: '1' });

    componente.alGuardarElAjuste();

    expect(cambiosPedidos.atendidas).toEqual([{ id: 10, atendida: true }]);
    expect(componente.revision.cuantasPendientes()).toBe(0);
    expect(componente.editandoAjuste()).toBeFalse();
    expect(componente.inspectorAjustesAbierto()).toBeFalse();
    expect(componente.mensajeAjuste()).toContain('El ajuste quedó guardado');
  });

  it('al resolver el último ajuste ofrece reenviar o seguir ajustando y permite el reenvío', () => {
    api.festivales = [{ id: '2', nombre: 'X', estado: 'AjustesSolicitados', cambiosPedidos: 1 }];
    cambiosPedidos.respuesta = {
      id: 1, moduloId: 'festivales', registroId: '2', registroNombre: 'X', estado: 'enviada', observacionGeneral: null,
      revisorNombre: null, destinatarioNombre: null, organizacionNombre: null,
      fechaActualizacion: null, fechaEnvio: null,
      observaciones: [{
        id: 10, ambito: 'principal', subregistroId: null, seccionId: 'generales', campoId: 'festival.nombre',
        campoEtiqueta: 'Nombre del Festival', valorObservado: 'X', nota: 'Corrige el nombre.',
        estado: 'pendiente', fechaAtencion: null,
      }],
    };
    montar('2', { seccion: 'informacion', ajustes: '1', ajuste: '10', editar: '1' });

    componente.alGuardarElAjuste();
    fixture.detectChanges();

    const aviso = raiz().querySelector<HTMLElement>('[data-testid="festival-listo-para-reenvio"]');
    expect(aviso?.textContent).toContain('La información está lista para volver a revisión');
    expect(aviso?.textContent).toContain('Seguir ajustando información');
    const reenviar = Array.from(aviso!.querySelectorAll<HTMLButtonElement>('button'))
      .find(boton => boton.textContent?.includes('Volver a enviar a revisión'))!;
    reenviar.click();

    expect(api.enviosARevision).toBe(1);
    expect(componente.festival()?.estado).toBe('EnRevision');
  });

  it('permite atender una orientación general sin inventar una casilla ni un campo', () => {
    api.festivales = [{ id: '92', nombre: 'Festival del Río', estado: 'AjustesSolicitados' }];
    cambiosPedidos.respuesta = {
      id: 5, moduloId: 'festivales', registroId: '92', registroNombre: 'Festival del Río', estado: 'enviada',
      observacionGeneral: 'Revisa la coherencia general de la descripción.', revisorNombre: 'Equipo PNMC',
      destinatarioNombre: null, organizacionNombre: null, fechaActualizacion: null, fechaEnvio: null,
      observaciones: [],
    };
    montar('92', { seccion: 'informacion', ajustes: '1' });

    expect(componente.inspectorAjustesAbierto()).toBeFalse();
    const revisar = Array.from(raiz().querySelectorAll<HTMLButtonElement>('button'))
      .find(boton => boton.textContent?.includes('Revisar orientación'))!;
    revisar.click();
    fixture.detectChanges();

    expect(raiz().textContent).toContain('Revisa la coherencia general de la descripción.');
    expect(raiz().textContent).toContain('Editar información');
    expect(raiz().textContent).not.toContain('Ya lo corregí');

    const editar = Array.from(raiz().querySelectorAll<HTMLButtonElement>('button'))
      .find(boton => boton.textContent?.includes('Editar información'))!;
    editar.click();
    fixture.detectChanges();

    expect(raiz().querySelector('app-ficha-festival')).not.toBeNull();
    expect(raiz().textContent).toContain('Guardar cambios');
  });

  it('«Historial» consulta la trazabilidad y muestra el estado vacío real', () => {
    api = new ApiFalso();
    api.festivales = [{ id: '2', nombre: 'X', estado: 'Publicado' }];
    montar('2', { seccion: 'historial' });

    expect(raiz().textContent).toContain('Aún no hay movimientos registrados');
  });
});
