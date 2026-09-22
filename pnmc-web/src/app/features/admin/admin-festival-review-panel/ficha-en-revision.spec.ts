import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { FichaEnRevisionComponent } from './ficha-en-revision.component';
import { RevisionDeCamposStore } from '../../../core/revision-de-campos/revision-de-campos.store';
import { ComparacionEnviosFestival, GuardarRevisionSolicitud, RevisionDeCampos } from '../../../core/revision-de-campos/revision-de-campos';
import {
  CatalogosDelFestival,
  FestivalDeLaOrganizacion,
  PanelOrganizacionApi,
  UbicacionDivipola,
} from '../../panel-organizacion/panel-organizacion.api';

/*
  QUÉ SE INSTRUMENTA AQUÍ.

  El pie de la revisión: los dos botones que separan «lo tengo a medias» de «devuélveselo». El criterio es este: «ir guardando el borrador, y luego poder enviar la solicitud de
  cambios y que al devolverlo le llegue al usuario para hacer esos cambios sobre el festival».

  LAS TRES COSAS QUE NO PUEDEN FALLAR:

  1) GUARDAR NO ENVÍA. Son dos rutas y dos consecuencias: el borrador solo lo ve el funcionario; el
     envío devuelve el Festival, le manda un aviso a la organización y lo saca de la cola. Que
     «Guardar» llamara a la ruta de envío no se vería en pantalla — se vería en la bandeja de la
     organización, media hora después.

  2) NO SE ENVÍA UNA LISTA VACÍA. Devolver un Festival sin decir qué corregir es el caso que este
     circuito existe para cerrar.

  3) CERRAR CON NOTAS SIN GUARDAR AVISA. El borrador vive en el navegador hasta que se pulsa
     «Guardar»: cerrar lo pierde entero.
*/

const CATALOGOS: CatalogosDelFestival = {
  practicasMusicales: [], territoriosSonoros: [], tipologias: [], expresionesArtisticas: [],
  fuentesFinanciacion: [], modalidadesParticipacion: [], naturalezasEntidad: [], tiposIngreso: [],
  tiposOrganizador: [], zonasUrbanoRural: [], titulacionesColectivas: [], regionesOcad: [],
};

const REVISION_VACIA: RevisionDeCampos = {
  id: 0, moduloId: 'festivales', registroId: '91', registroNombre: 'Festival del Pacífico', estado: 'borrador',
  observacionGeneral: null, revisorNombre: null, destinatarioNombre: null,
  organizacionNombre: null, fechaActualizacion: null, fechaEnvio: null, observaciones: [],
};

/**
 * El doble.
 *
 * <b>EXTIENDE `PanelOrganizacionApi` Y NO `FichaEnRevisionApi`</b>, y no es una preferencia: aquella
 * es abstracta y no inyecta nada, mientras que esta declara `inject(ApiClientService)` como
 * inicializador de campo. `new FichaEnRevisionApi()` fuera de un contexto de inyección muere con
 * NG0203 antes de llegar a ninguna aserción. El componente hace `inject(...) as FichaEnRevisionApi`,
 * así que lo que importa es tener los métodos, no la herencia.
 */
class ApiFalsa extends PanelOrganizacionApi {
  guardados: { festivalId: string; solicitud: GuardarRevisionSolicitud }[] = [];
  enviados: { festivalId: string; solicitud: GuardarRevisionSolicitud }[] = [];
  decisiones: { festivalId: string; accion: 'Publicar' | 'Rechazar'; motivo?: string }[] = [];
  revisionQueDevuelve: RevisionDeCampos = REVISION_VACIA;
  falloAlLeer: unknown = null;
  comparacion: ComparacionEnviosFestival | null = null;

  obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> { return of(CATALOGOS); }
  obtenerUbicaciones(): Observable<UbicacionDivipola[]> { return of([]); }
  obtenerFestivalEnRevision(): Observable<FestivalDeLaOrganizacion> {
    return of({
      id: '91', nombre: 'Festival del Pacífico', descripcion: 'Ampliada', estado: 'EnRevision',
      organizacionPrincipalId: '12', organizacionPrincipalNombre: 'Corporación Pacífico',
      nivelCobertura: 'nacional', periodicidad: 'anual', correoContacto: 'festival@example.com',
      practicasMusicales: [], territoriosSonoros: [],
    });
  }

  obtenerRevision(): Observable<RevisionDeCampos> {
    if (this.falloAlLeer) return throwError(() => this.falloAlLeer);
    return of(this.revisionQueDevuelve);
  }

  obtenerComparacionEnvios(): Observable<ComparacionEnviosFestival | null> { return of(this.comparacion); }

  guardarBorrador(festivalId: string, solicitud: GuardarRevisionSolicitud): Observable<RevisionDeCampos> {
    this.guardados.push({ festivalId, solicitud });
    return of({ ...this.revisionQueDevuelve, id: 7 });
  }

  enviarSolicitud(festivalId: string, solicitud: GuardarRevisionSolicitud): Observable<RevisionDeCampos> {
    this.enviados.push({ festivalId, solicitud });
    return of({ ...this.revisionQueDevuelve, id: 7, estado: 'enviada', fechaEnvio: '2026-08-29T10:00:00' });
  }

  decidirFestival(festivalId: string, accion: 'Publicar' | 'Rechazar', motivo?: string): Observable<{ id: string; estado: string }> {
    this.decisiones.push({ festivalId, accion, motivo });
    return of({ id: festivalId, estado: accion === 'Publicar' ? 'publicado' : 'rechazado' });
  }

  // Las once que esta pantalla no usa. Lanzan: si alguna se llamara, la prueba lo dice.
  obtenerPerfil(): Observable<never> { throw new Error('no es de esta pantalla'); }
  guardarPerfil(): Observable<never> { throw new Error('no es de esta pantalla'); }
  obtenerResponsable(): Observable<never> { throw new Error('no es de esta pantalla'); }
  guardarResponsable(): Observable<never> { throw new Error('no es de esta pantalla'); }
  obtenerTiposDocumento(): Observable<never> { throw new Error('no es de esta pantalla'); }
  obtenerFestivales(): Observable<never> { throw new Error('no es de esta pantalla'); }
  enviarFestivalARevision(): Observable<never> { throw new Error('no es de esta pantalla'); }
  enviarPropuestaARevision(): Observable<never> { throw new Error('no es de esta pantalla'); }
  iniciarPropuesta(): Observable<never> { throw new Error('no es de esta pantalla'); }
  obtenerPropuestaActiva(): Observable<never> { throw new Error('no es de esta pantalla'); }
  guardarPropuesta(): Observable<never> { throw new Error('no es de esta pantalla'); }
  obtenerNotificaciones(): Observable<never> { throw new Error('no es de esta pantalla'); }
  marcarNotificacionLeida(): Observable<never> { throw new Error('no es de esta pantalla'); }
  crearFestival(): Observable<never> { throw new Error('no es de esta pantalla'); }
  guardarFestival(): Observable<never> { throw new Error('no es de esta pantalla'); }
}

describe('FichaEnRevisionComponent · el pie de la revisión institucional', () => {
  let fixture: ComponentFixture<FichaEnRevisionComponent>;
  let api: ApiFalsa;
  let almacen: RevisionDeCamposStore;
  let enviadas: number;

  beforeEach(async () => {
    api = new ApiFalsa();
    await TestBed.configureTestingModule({ imports: [FichaEnRevisionComponent] })
      // LOS PROVEEDORES DEL COMPONENTE MANDAN sobre los del TestBed: sin `overrideComponent`, la
      // prueba pediría al servidor de verdad y no mediría nada.
      .overrideComponent(FichaEnRevisionComponent, {
        set: {
          providers: [
            RevisionDeCamposStore,
            { provide: PanelOrganizacionApi, useValue: api },
          ],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(FichaEnRevisionComponent);
    fixture.componentInstance.festivalId = '91';
    fixture.componentInstance.nombreFestival = 'Festival del Pacífico';
    fixture.componentInstance.nombreOrganizacion = 'Corporación Pacífico';
    enviadas = 0;
    fixture.componentInstance.enviada.subscribe(() => enviadas++);
    almacen = fixture.componentInstance.almacen;
  });

  function raiz(): HTMLElement { return fixture.nativeElement as HTMLElement; }
  function buscar<T extends HTMLElement>(testid: string): T | null {
    return raiz().querySelector<T>(`[data-testid="${testid}"]`);
  }
  function montar(): void {
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
  }
  function anotar(campoId: string, texto: string): void {
    almacen.escribir({
      campoId, seccionId: 'generales', campoEtiqueta: 'Nombre del festival', valorObservado: 'lo que decía',
    }, texto);
    fixture.detectChanges();
  }

  it('sin campos señalados no se puede enviar', fakeAsync(() => {
    montar();

    expect(buscar<HTMLButtonElement>('revision-enviar')!.disabled).toBeTrue();
    expect(buscar('revision-recuento')!.textContent).toContain('Ningún campo señalado');
  }));

  it('identifica el ciclo y marca los campos que cambiaron desde el envío anterior', fakeAsync(() => {
    api.comparacion = {
      numeroEnvio: 2,
      fechaEnvio: '2026-09-09T18:00:00Z',
      numeroEnvioAnterior: 1,
      fechaEnvioAnterior: '2026-09-08T18:00:00Z',
      esPrimerEnvio: false,
      comparacionParcial: false,
      totalCampos: 16,
      camposModificados: 2,
      ajustesSugeridosAtendidos: 1,
      otrosCambios: 1,
      cambios: [
        { campoId: 'festival.nombre', seccionId: 'generales', campoEtiqueta: 'Nombre del Festival', valorAnterior: 'Festival del Pacífico', valorRecibido: 'Festival Pacífico', respondeAjusteSugerido: true },
        { campoId: 'festival.descripcion', seccionId: 'generales', campoEtiqueta: 'Descripción del Festival', valorAnterior: 'Breve', valorRecibido: 'Ampliada', respondeAjusteSugerido: false },
      ],
    };

    montar();

    expect(buscar('contexto-del-envio')?.textContent).toContain('Segundo envío a revisión');
    expect(buscar('contexto-del-envio')?.textContent).toContain('2 campos cambiaron');
    expect(raiz().textContent).toContain('Ampliada');
    expect(raiz().textContent).toContain('anual');
    expect(raiz().textContent).toContain('festival@example.com');
    expect(buscar('cambio-envio-festival.nombre')?.textContent).toContain('Ajuste atendido');
    expect(buscar('cambio-envio-festival.descripcion')?.textContent).toContain('Cambio adicional');
  }));

  it('guarda automáticamente el borrador anotado y NO lo envía', fakeAsync(() => {
    // LA DIFERENCIA QUE ESTA PRUEBA SOSTIENE: son dos rutas y dos consecuencias. Que «Guardar»
    // llamara a la de envío no se vería aquí; se vería en la bandeja de la organización.
    montar();
    anotar('festival.nombre', 'Sin la sigla.');
    buscar<HTMLTextAreaElement>('revision-observacion-general')!.value = 'Dos cosas.';
    buscar<HTMLTextAreaElement>('revision-observacion-general')!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    tick(800);
    fixture.detectChanges();

    expect(api.guardados.length).toBe(1);
    expect(api.enviados.length).withContext('guardar no envía').toBe(0);
    expect(api.guardados[0].festivalId).toBe('91');
    expect(api.guardados[0].solicitud.observacionGeneral).toBe('Dos cosas.');
    expect(api.guardados[0].solicitud.observaciones.map(item => item.campoId)).toEqual(['festival.nombre']);
    expect(buscar('revision-mensaje')).toBeNull();
    expect(buscar('revision-autoguardado')!.getAttribute('title')).toContain('guardado automáticamente');
    expect(enviadas).withContext('la bandeja no se recarga al guardar').toBe(0);
  }));

  it('enviar abre una confirmación integrada y, al confirmarla, manda la lista y avisa a la bandeja', fakeAsync(() => {
    montar();
    anotar('festival.nombre', 'Sin la sigla.');

    buscar<HTMLButtonElement>('revision-enviar')!.click();
    fixture.detectChanges();

    const confirmacion = buscar('revision-confirmacion')!;
    expect(confirmacion.textContent).toContain('Corporación Pacífico');
    expect(confirmacion.textContent).toContain('1 ajuste sugerido');
    expect(api.enviados.length).withContext('abrir la confirmación no envía').toBe(0);

    buscar<HTMLButtonElement>('revision-confirmar-accion')!.click();
    tick();
    fixture.detectChanges();

    expect(api.enviados.length).toBe(1);
    expect(api.enviados[0].solicitud.observaciones.length).toBe(1);
    expect(enviadas).withContext('la bandeja tiene que recargarse: el Festival sale de la cola').toBe(1);
  }));

  it('si se vuelve desde la confirmación integrada, no se manda nada', fakeAsync(() => {
    montar();
    anotar('festival.nombre', 'Sin la sigla.');

    buscar<HTMLButtonElement>('revision-enviar')!.click();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('revision-volver')!.click();
    tick();
    fixture.detectChanges();

    expect(api.enviados.length).toBe(0);
    expect(enviadas).toBe(0);
  }));

  it('publicar confirma dentro de la ficha antes de decidir', fakeAsync(() => {
    montar();

    buscar<HTMLButtonElement>('revision-publicar')!.click();
    fixture.detectChanges();
    expect(buscar('revision-confirmacion')!.textContent).toContain('quedará visible en el portal público');
    expect(api.decisiones).toEqual([]);

    buscar<HTMLButtonElement>('revision-confirmar-accion')!.click();
    tick();

    expect(api.decisiones).toEqual([{ festivalId: '91', accion: 'Publicar', motivo: undefined }]);
  }));

  it('rechazar exige motivo y lo resume antes de decidir', fakeAsync(() => {
    montar();

    buscar<HTMLButtonElement>('revision-rechazar')!.click();
    fixture.detectChanges();
    const motivo = buscar<HTMLTextAreaElement>('revision-motivo-rechazo')!;
    motivo.value = 'No corresponde a un Festival musical.';
    motivo.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    buscar<HTMLButtonElement>('revision-confirmar-rechazo')!.click();
    fixture.detectChanges();
    expect(buscar('revision-confirmacion')!.textContent).toContain('No corresponde a un Festival musical.');
    expect(api.decisiones).toEqual([]);

    buscar<HTMLButtonElement>('revision-confirmar-accion')!.click();
    tick();

    expect(api.decisiones).toEqual([{ festivalId: '91', accion: 'Rechazar', motivo: 'No corresponde a un Festival musical.' }]);
  }));

  it('con la solicitud ya enviada no hay dónde seguir escribiendo', fakeAsync(() => {
    api.revisionQueDevuelve = {
      ...REVISION_VACIA, id: 7, estado: 'enviada', fechaEnvio: '2026-08-29T10:00:00',
      destinatarioNombre: 'Persona de la organización',
      observaciones: [{
        id: 1, ambito: 'principal', subregistroId: null, seccionId: 'generales',
        campoId: 'festival.nombre', campoEtiqueta: 'Nombre del festival',
        valorObservado: null, nota: 'Sin la sigla.', estado: 'pendiente', fechaAtencion: null,
      }],
    };
    montar();

    expect(buscar('revision-guardar')).toBeNull();
    expect(buscar('revision-enviar')).toBeNull();
    expect(buscar('revision-observacion-general')).toBeNull();
    const aviso = buscar('revision-ya-enviada')!;
    expect(aviso.textContent).toContain('Persona de la organización');
    expect(aviso.textContent).toContain('1 de 1 sin atender');
  }));

  it('cerrar con notas sin guardar avisa, y respetarlo no cierra', fakeAsync(() => {
    // EL BORRADOR VIVE EN EL NAVEGADOR hasta que se pulsa «Guardar»: cerrar lo pierde entero.
    montar();
    anotar('festival.nombre', 'Sin la sigla.');
    let cierres = 0;
    fixture.componentInstance.cerrar.subscribe(() => cierres++);

    fixture.componentInstance.intentarCerrar();
    fixture.detectChanges();

    // SE PREGUNTA EN EL DIALOGO DEL PROYECTO, dentro de la propia pantalla, y decir «Seguir
    // revisando» deja la ficha donde estaba con lo señalado intacto.
    expect(fixture.componentInstance.cierreDudoso()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Cerrar sin guardar lo señalado');
    expect(cierres).toBe(0);

    fixture.componentInstance.cierreDudoso.set(false);
    fixture.detectChanges();
    expect(cierres).toBe(0);
  }));

  it('cerrar sin nada sin guardar no pregunta nada', fakeAsync(() => {
    montar();
    let cierres = 0;
    fixture.componentInstance.cerrar.subscribe(() => cierres++);

    fixture.componentInstance.intentarCerrar();
    fixture.detectChanges();

    expect(fixture.componentInstance.cierreDudoso()).toBeFalse();
    expect(cierres).toBe(1);
  }));

  it('si la revisión no se puede leer, la ficha se abre igualmente', fakeAsync(() => {
    // QUE EL CIRCUITO DE NOTAS ESTÉ CAÍDO no puede impedirle al funcionario ver lo que la
    // organización diligenció: es lo que vino a revisar.
    api.falloAlLeer = { message: 'sin revisión' };
    montar();

    expect(buscar('revision-error')!.textContent).toContain('sin revisión');
    expect(buscar('revision-enviar')).withContext('el pie sigue en pie').not.toBeNull();
    expect(almacen.esRevision()).toBeTrue();
  }));
});
