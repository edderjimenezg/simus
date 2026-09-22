import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { FichaFestivalComponent } from './ficha-festival.component';
import { RevisionDeCamposStore } from '../../../core/revision-de-campos/revision-de-campos.store';
import { ObservacionDeCampo, RevisionDeCampos } from '../../../core/revision-de-campos/revision-de-campos';
import {
  CatalogosDelFestival,
  FestivalDeLaOrganizacion,
  PanelOrganizacionApi,
  UbicacionDivipola,
} from '../panel-organizacion.api';

/*
  QUÉ SE INSTRUMENTA AQUÍ.

  La misma ficha, en los dos papeles del circuito de devolución que pidió el usuario el 29 de agosto
  de 2026: «en todos los campos poder pedir cambios puntuales sobre alguno de los campos […] y que
  al devolverlo le llegue al usuario para hacer esos cambios sobre el festival […] que sea por
  secciones, con la nota que diga qué debe cambiar en cada campo».

  LAS CUATRO COSAS QUE NO PUEDEN FALLAR:

  1) EN MODO REVISIÓN, CADA CAMPO OFRECE PEDIR UN CAMBIO. Son cuarenta y siete y los pinta un solo
     componente; que uno se quede sin control no falla, se queda sin control.

  2) LA NOTA SE VE PEGADA A SU CAMPO. Leer «escribe el nombre completo» sin ver a qué campo se
     refiere obliga a adivinar, y con cuarenta y siete campos eso es lo primero que se pierde.

  3) CON LOS NUEVE PASOS PLEGADOS, EL NÚMERO DEL ENCABEZADO ES LO ÚNICO QUE DICE DÓNDE MIRAR. «El
     estado natural son todos cerrados», del mismo usuario: sin el distintivo hay que desplegarlos
     uno a uno.

  4) AL EDITAR, LA NOTA NO CABE DEBAJO DEL CAMPO —ahí hay un `<input>`—, así que va al principio del
     paso, con el rótulo delante. Es la mitad que ve quien corrige.

  SE ENTRA POR LA PANTALLA. El almacén se siembra como lo sembraría el panel, y todo lo demás son
  clics.
*/

const FESTIVAL: FestivalDeLaOrganizacion = {
  id: '91',
  nombre: 'Festival de Música del Pacífico',
  descripcion: 'Cuatro días de bandas y chirimías.',
  // AJUSTES SOLICITADOS Y NO «EnRevision», y el cambio es. Con
  // «EnRevision» el Festival NO es editable —lo dice el servidor, que contesta 409 al PUT fuera de
  // Borrador y AjustesSolicitados—, y sin embargo la ficha ofrecía «Editar el Festival» porque
  // `puedeEditar()` miraba si la EDICION abierta admitía cambios. Al retirarse esa mitad,
  // `puedeEditar()` pasó a mirar solo la cabecera y el botón dejó de pintarse: la prueba se puso
  // en rojo y lo que destapó es que el montaje describía un caso que no existe. Atender ajustes
  // ocurre cuando el Ministerio ya devolvió el Festival, que es AjustesSolicitados.
  estado: 'AjustesSolicitados',
  periodicidad: 'Anual',
  nivelCobertura: 'municipal',
  codigoDepartamento: '05',
  codigoMunicipio: '05001',
  correoContacto: 'festival@fmv.org',
  practicasMusicales: [{ id: 3, nombre: 'Banda' }],
  territoriosSonoros: [{ id: 2, nombre: 'Andes' }],
};

const CATALOGOS: CatalogosDelFestival = {
  practicasMusicales: [{ id: 3, nombre: 'Banda' }, { id: 7, nombre: 'Coro' }],
  territoriosSonoros: [{ id: 2, nombre: 'Andes' }, { id: 5, nombre: 'Pacífico' }],
  tipologias: [{ id: 11, nombre: 'Festival de música' }],
  expresionesArtisticas: [{ id: 21, nombre: 'Danza' }],
  fuentesFinanciacion: [{ id: 31, nombre: 'Recursos propios' }],
  modalidadesParticipacion: [{ id: 41, nombre: 'Concurso' }],
  naturalezasEntidad: [{ id: 51, nombre: 'Pública' }],
  tiposIngreso: [{ id: 61, nombre: 'Gratuito' }],
  tiposOrganizador: [{ id: 71, nombre: 'Alcaldía' }],
  zonasUrbanoRural: [{ id: 81, nombre: 'Urbana' }],
  titulacionesColectivas: [{ id: 91, nombre: 'Ninguna' }],
  regionesOcad: [{ id: 101, nombre: 'Pacífico' }],
};

const DIVIPOLA: UbicacionDivipola[] = [
  { departmentCode: '05', departmentName: 'ANTIOQUIA', municipalityCode: '05001', municipalityName: 'MEDELLÍN' },
  { departmentCode: '05', departmentName: 'ANTIOQUIA', municipalityCode: '05088', municipalityName: 'BELLO' },
];

// AQUI ESTABAN `RESUMENES` Y `fichaDeVersion()`, los dobles de las dos ediciones que esta ficha
// abría en su segunda pestaña. La pestaña se retiró y con ella las
// cinco pruebas que la ejercían: la nota atada a la edición abierta, el distintivo por edición, el
// vaciado al añadir una edición y las dos del recuento de ajustes en otra edición.

class ApiFalso extends PanelOrganizacionApi {
  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> { return of(CATALOGOS); }
  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> { return of(DIVIPOLA); }
  override obtenerPerfil(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override guardarPerfil(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override obtenerResponsable(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override guardarResponsable(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override obtenerTiposDocumento(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override obtenerFestivales(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override enviarFestivalARevision(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override enviarPropuestaARevision(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override iniciarPropuesta(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override obtenerPropuestaActiva(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override guardarPropuesta(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override obtenerNotificaciones(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override marcarNotificacionLeida(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override crearFestival(): Observable<never> { throw new Error('no es de esta pantalla'); }
  override guardarFestival(): Observable<never> { throw new Error('no es de esta pantalla'); }
}

function nota(cambios: Partial<ObservacionDeCampo> = {}): ObservacionDeCampo {
  return {
    id: 1, ambito: 'principal', subregistroId: null, seccionId: 'generales',
    campoId: 'festival.nombre', campoEtiqueta: 'Nombre del festival',
    valorObservado: FESTIVAL.nombre, nota: 'Escribe el nombre completo, sin la sigla.',
    estado: 'pendiente', fechaAtencion: null, ...cambios,
  };
}

function revision(observaciones: ObservacionDeCampo[], estado: RevisionDeCampos['estado'] = 'borrador'): RevisionDeCampos {
  return {
    id: 7, moduloId: 'festivales', registroId: '91', registroNombre: FESTIVAL.nombre, estado,
    observacionGeneral: null, revisorNombre: 'Funcionaria PNMC',
    destinatarioNombre: 'Persona de la organización', organizacionNombre: 'Corporación Pacífico',
    fechaActualizacion: null, fechaEnvio: estado === 'borrador' ? null : '2026-08-29T10:00:00',
    observaciones,
  };
}

describe('FichaFestivalComponent · pedir y atender cambios campo por campo', () => {
  let fixture: ComponentFixture<FichaFestivalComponent>;
  let almacen: RevisionDeCamposStore;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FichaFestivalComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: new ApiFalso() },
        RevisionDeCamposStore,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FichaFestivalComponent);
    almacen = TestBed.inject(RevisionDeCamposStore);
    fixture.componentInstance.festival = FESTIVAL;
    fixture.componentInstance.organizacionId = '10';
  });

  function raiz(): HTMLElement { return fixture.nativeElement as HTMLElement; }
  function buscar<T extends HTMLElement>(testid: string): T | null {
    return raiz().querySelector<T>(`[data-testid="${testid}"]`);
  }
  function pulsar(testid: string): void {
    const nodo = buscar<HTMLButtonElement>(testid);
    if (!nodo) throw new Error(`No está en pantalla: ${testid}`);
    nodo.click();
    fixture.detectChanges();
  }
  function montar(modo: 'revision' | 'atencion' | 'lectura', notas: ObservacionDeCampo[], estado: RevisionDeCampos['estado'] = 'borrador'): void {
    almacen.modo.set(modo);
    almacen.sembrar(revision(notas, estado), '91', FESTIVAL.nombre);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
  }
  function desplegar(pasoId: string): void {
    const boton = buscar<HTMLButtonElement>(`abrir-paso-${pasoId}`);
    if (!boton) throw new Error(`No hay paso ${pasoId} en pantalla`);
    if (boton.getAttribute('aria-expanded') === 'false') pulsar(`abrir-paso-${pasoId}`);
  }

  // ═══════════════════════════════════════════════════════════════════════════════════
  // El funcionario: pedir
  // ═══════════════════════════════════════════════════════════════════════════════════

  it('en modo revisión, cada campo ofrece pedir un cambio', fakeAsync(() => {
    montar('revision', []);
    desplegar('generales');

    expect(buscar('pedir-cambio-festival.nombre')).not.toBeNull();
    expect(buscar('pedir-cambio-festival.descripcion')).not.toBeNull();
    expect(buscar('pedir-cambio-festival.periodicidad')).not.toBeNull();
    expect(buscar('pedir-cambio-festival.nivelCobertura')).not.toBeNull();
    // EL TERRITORIO TAMBIÉN, y hasta la lectura ni siquiera lo mostraba:
    // el formulario lo pedía y la ficha no lo enseñaba.
    expect(buscar('pedir-cambio-festival.codigoDepartamento')).not.toBeNull();
    expect(buscar('pedir-cambio-festival.codigoMunicipio')).not.toBeNull();
  }));

  it('sin modo revisión no hay ningún control de pedir cambio', fakeAsync(() => {
    // LA FICHA SIGUE SIENDO LA FICHA. La organización que la abre para leer no ve controles de un
    // circuito que no es suyo.
    montar('lectura', []);
    desplegar('generales');

    expect(buscar('pedir-cambio-festival.nombre')).toBeNull();
    expect(raiz().querySelectorAll('[data-testid^="pedir-cambio-"]').length).toBe(0);
  }));

  it('escribir una nota la deja pegada a su campo, con el valor que había', fakeAsync(() => {
    montar('revision', []);
    desplegar('generales');

    pulsar('pedir-cambio-festival.nombre');
    const caja = buscar<HTMLTextAreaElement>('caja-nota-festival.nombre')!;
    caja.value = 'Escribe el nombre completo, sin la sigla.';
    caja.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    pulsar('guardar-nota-festival.nombre');

    const escrita = buscar('nota-festival.nombre');
    expect(escrita).not.toBeNull();
    expect(escrita!.textContent).toContain('Escribe el nombre completo, sin la sigla.');
    // Y LA CAJA SE CIERRA: dos cajas abiertas invitan a confundirlas.
    expect(buscar('caja-nota-festival.nombre')).toBeNull();

    // LO QUE VIAJA lleva el rótulo y el valor de ENTONCES, que es la evidencia de sobre qué se pidió
    // el cambio: cuando la organización lo corrija, el valor de hoy deja de existir.
    const guardada = almacen.paraGuardar(null).observaciones[0];
    expect(guardada.campoId).toBe('festival.nombre');
    expect(guardada.seccionId).toBe('generales');
    expect(guardada.campoEtiqueta).toBe('Nombre del festival');
    expect(guardada.valorObservado).toBe(FESTIVAL.nombre);
    expect(guardada.ambito).toBe('principal');
    expect(guardada.subregistroId).toBeNull();
  }));


  it('quitar una nota la borra de lo que se va a guardar', fakeAsync(() => {
    montar('revision', [nota()]);
    desplegar('generales');
    expect(buscar('nota-festival.nombre')).not.toBeNull();

    pulsar('quitar-nota-festival.nombre');

    expect(buscar('nota-festival.nombre')).toBeNull();
    expect(almacen.paraGuardar(null).observaciones.length).toBe(0);
  }));

  it('en modo revisión no hay botón de editar el Festival', fakeAsync(() => {
    // UN FUNCIONARIO NO CORRIGE LA FICHA DE UNA ORGANIZACIÓN: pide que la corrijan. El botón
    // llamaría a un PUT que la consola no puede hacer.
    montar('revision', []);
    expect(buscar('ficha-editar')).toBeNull();
    expect(buscar('ficha-guardar')).toBeNull();
  }));

  it('con la solicitud ya enviada el funcionario no puede seguir escribiendo', fakeAsync(() => {
    // LA LISTA ESTÁ EN MANOS DE LA ORGANIZACIÓN: reescribir una nota que la otra parte ya está
    // atendiendo cambiaría el encargo a mitad de camino.
    montar('revision', [nota()], 'enviada');
    desplegar('generales');

    expect(buscar('nota-festival.nombre')).withContext('la nota se sigue viendo').not.toBeNull();
    expect(buscar('quitar-nota-festival.nombre')).toBeNull();
    expect(buscar('editar-nota-festival.nombre')).toBeNull();
    expect(buscar('pedir-cambio-festival.descripcion')).toBeNull();
  }));

  // ═══════════════════════════════════════════════════════════════════════════════════
  // El distintivo del encabezado
  // ═══════════════════════════════════════════════════════════════════════════════════

  it('el encabezado de la revisión continua dice cuántos campos tiene señalados', fakeAsync(() => {
    montar('revision', [
      nota({ id: 1, campoId: 'festival.nombre' }),
      nota({ id: 2, campoId: 'festival.descripcion' }),
      nota({ id: 3, campoId: 'festival.correoContacto', seccionId: 'contacto-festival' }),
    ]);

    expect(buscar('abrir-paso-generales')!.getAttribute('aria-expanded')).toBeNull();
    expect(buscar('insignia-cambios-generales')!.textContent!.trim()).toContain('2');
    expect(buscar('insignia-cambios-contacto-festival')!.textContent!.trim()).toContain('1');
    expect(buscar('insignia-cambios-musica-festival')).withContext('un paso sin notas no lleva distintivo').toBeNull();
  }));

  it('el distintivo no cuenta lo que ya se atendió', fakeAsync(() => {
    montar('atencion', [
      nota({ id: 1, campoId: 'festival.nombre', estado: 'atendida' }),
      nota({ id: 2, campoId: 'festival.descripcion' }),
    ], 'enviada');

    expect(buscar('insignia-cambios-generales')!.textContent!.trim()).toContain('1');
  }));


  // ═══════════════════════════════════════════════════════════════════════════════════
  // La organización: atender
  // ═══════════════════════════════════════════════════════════════════════════════════

  it('al editar, los cambios pedidos salen al principio del paso, con su rótulo', fakeAsync(() => {
    // DEBAJO DEL CAMPO NO CABEN: ahí hay un `<input>`. «Que sea por secciones, con la nota que diga
    // qué debe cambiar en cada campo», del usuario.
    fixture.componentInstance.festival = { ...FESTIVAL, estado: 'AjustesSolicitados' };
    montar('atencion', [
      nota({ id: 1, campoId: 'festival.nombre', nota: 'Sin la sigla.' }),
      nota({ id: 2, campoId: 'festival.periodicidad', campoEtiqueta: 'Periodicidad', nota: 'Di cada cuánto.' }),
      // LA TERCERA ES DE OTRO PASO, y la puso el mutante F12: con las dos
      // primeras en la misma sección, un `cambiosDelPaso()` que devolviera TODAS las notas pasaba
      // la prueba igual. Una lista donde todo cae en el mismo grupo no mide agrupación.
      nota({ id: 3, campoId: 'festival.correoContacto', seccionId: 'contacto-festival',
             campoEtiqueta: 'Correo de contacto', nota: 'Este correo rebota.' }),
    ], 'enviada');

    fixture.componentInstance.editar();
    fixture.detectChanges();
    desplegar('generales');

    const resumen = buscar('cambios-paso-generales');
    expect(resumen).not.toBeNull();
    expect(resumen!.textContent).toContain('Nombre del festival');
    expect(resumen!.textContent).toContain('Sin la sigla.');
    expect(resumen!.textContent).toContain('Periodicidad');
    expect(resumen!.textContent).toContain('Di cada cuánto.');
    expect(resumen!.textContent).not.toContain('Correo de contacto');
    expect(resumen!.textContent).not.toContain('Este correo rebota.');

    // Y LA TERCERA SÍ ESTÁ, en el paso que le toca.
    desplegar('contacto-festival');
    expect(buscar('cambios-paso-contacto-festival')!.textContent).toContain('Este correo rebota.');
  }));

  it('al leer, la nota va debajo de su campo y NO en el resumen del paso', fakeAsync(() => {
    // LAS DOS MITADES DE LA MISMA REGLA: donde cabe pegada al campo, va pegada; donde no, va arriba.
    // Pintar las dos a la vez duplicaría cada nota en pantalla.
    montar('atencion', [nota()], 'enviada');
    desplegar('generales');

    expect(buscar('nota-festival.nombre')).not.toBeNull();
    expect(buscar('cambios-paso-generales')).toBeNull();
  }));

  it('marcar un cambio como atendido avisa a quien tiene que escribirlo en el servidor', fakeAsync(() => {
    // SE ESCRIBE EN EL SERVIDOR ANTES DE PINTARLO. Marcar en pantalla y confiar en que la petición
    // salga bien deja a la organización creyendo que dejó constancia de algo que no quedó escrito.
    const marcados: { id: number; atendida: boolean }[] = [];
    almacen.alAtender = (id, atendida) => {
      marcados.push({ id, atendida });
      return of(nota({ id, estado: 'pendiente' }));
    };

    montar('atencion', [nota({ id: 44 })], 'enviada');
    desplegar('generales');

    const casilla = buscar<HTMLInputElement>('atender-festival.nombre')!;
    casilla.checked = true;
    casilla.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(marcados).toEqual([{ id: 44, atendida: true }]);
    // NO SE PINTA SOLA: el almacén solo cambia cuando el servidor contesta.
    expect(almacen.notas()[0].estado).toBe('pendiente');

    almacen.marcarAtendida(44, true, '2026-08-29T15:00:00');
    fixture.detectChanges();
    expect(buscar('nota-festival.nombre')!.textContent).toContain('Ajuste atendido');
  }));

  it('la casilla vuelve a su sitio si el servidor no acepta la marca', fakeAsync(() => {
    // LO DESTAPÓ EL RECORRIDO DE PUNTA A PUNTA. El `<input>` no está atado
    // a ninguna señal: `[checked]` solo se reescribe cuando el valor ATADO cambia, y al fallar la
    // petición ese valor sigue siendo `pendiente`, así que Angular no vuelve a escribirlo. La
    // casilla se quedaba marcada sobre algo que el servidor nunca recibió — una marca que miente
    // justo en el circuito que existe para dar trazabilidad.
    almacen.alAtender = () => throwError(() => ({ message: 'la sesión caducó' }));

    montar('atencion', [nota({ id: 44 })], 'enviada');
    desplegar('generales');

    const casilla = buscar<HTMLInputElement>('atender-festival.nombre')!;
    casilla.checked = true;
    casilla.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(casilla.checked).withContext('lo que el servidor no aceptó no se pinta como aceptado').toBeFalse();
  }));

  it('en modo atención no se puede quitar ni corregir la nota', fakeAsync(() => {
    // NO ES SU LISTA. La organización atiende lo que le pidieron; borrarlo sería contestar al
    // encargo haciéndolo desaparecer.
    montar('atencion', [nota()], 'enviada');
    desplegar('generales');

    expect(buscar('quitar-nota-festival.nombre')).toBeNull();
    expect(buscar('editar-nota-festival.nombre')).toBeNull();
    expect(buscar('atender-festival.nombre')).not.toBeNull();
  }));

  // ═══════════════════════════════════════════════════════════════════════════════════
  // El recuento de ajustes, arriba del todo (30 de agosto de 2026)
  // ═══════════════════════════════════════════════════════════════════════════════════
  //
  // «Aquí me gustaría ver la cantidad de ajustes, y cuando tenga los ajustes totalmente concluidos,
  // podemos decir todos los ajustes hechos, de naranja a verde, sugerir ya guardar cambios».

  /** El recuento vive siempre en el DOM; `hidden` es lo que decide si se ve. */
  function recuento(): HTMLElement {
    const nodo = buscar('ficha-recuento-ajustes');
    if (!nodo) throw new Error('El recuento de ajustes no está en el DOM');
    return nodo;
  }

  it('con ajustes pendientes el recuento sale en ámbar y dice cuántos van', fakeAsync(() => {
    montar('atencion', [
      nota({ id: 1, campoId: 'festival.nombre', estado: 'atendida' }),
      nota({ id: 2, campoId: 'festival.descripcion' }),
      nota({ id: 3, campoId: 'festival.periodicidad' }),
    ], 'enviada');

    // MUTANTE QUE MATA: contar `cuantasNotas()` en vez de restar las pendientes. El número diría
    // «3 de 3 hechos» con dos sin tocar, que es justo la mentira que este recuento existe para no
    // decir.
    expect(recuento().classList).not.toContain('hidden');
    expect(recuento().textContent).toContain('1 de 3 ajustes hechos');
    expect(recuento().classList).toContain('bg-amber-50');
    expect(recuento().classList).not.toContain('bg-emerald-50');
  }));

  it('con todos marcados el recuento pasa a verde y sugiere guardar', fakeAsync(() => {
    montar('atencion', [
      nota({ id: 1, campoId: 'festival.nombre', estado: 'atendida' }),
      nota({ id: 2, campoId: 'festival.descripcion', estado: 'atendida' }),
    ], 'enviada');

    expect(recuento().classList).toContain('bg-emerald-50');
    expect(buscar('ficha-ajustes-completos')).not.toBeNull();
    expect(recuento().textContent).toContain('Todos los ajustes hechos: 2 de 2');
    // EL COLOR NO VA SOLO: el texto también cambia, porque un cambio de tono no lo percibe quien no
    // distingue esos dos tonos.
    expect(recuento().textContent).toContain('Ya puedes volver a enviar el Festival a revisión');
  }));

  it('el paso de ámbar a verde ocurre en una región viva que ya estaba observada', fakeAsync(() => {
    montar('atencion', [nota({ id: 44, campoId: 'festival.nombre' })], 'enviada');
    expect(recuento().getAttribute('role')).toBe('status');
    expect(recuento().classList).toContain('bg-amber-50');

    almacen.marcarAtendida(44, true, '2026-08-30T10:00:00');
    fixture.detectChanges();

    // MUTANTE QUE MATA: envolver el recuento en un `@if` en vez de esconderlo con `hidden`. La
    // pantalla se vería igual y el lector de pantalla NO anunciaría el cambio: una región viva que
    // se inserta con su contenido dentro no se anuncia.
    expect(recuento().classList).toContain('bg-emerald-50');
    expect(recuento().textContent).toContain('Todos los ajustes hechos');
  }));

  it('al editar, el recuento completo sugiere guardar los cambios', fakeAsync(() => {
    montar('atencion', [nota({ id: 1, campoId: 'festival.nombre', estado: 'atendida' })], 'enviada');
    pulsar('ficha-editar');

    expect(recuento().textContent).toContain('Guarda los cambios');
  }));

  it('sin cambios pedidos y en modo revisión, el recuento no se ve', fakeAsync(() => {
    // EN MODO REVISIÓN NO SE PINTA: ahí el número lo lleva el pie y significa otra cosa —cuántos
    // lleva escritos quien redacta, no cuántos quedan por corregir—. Dos recuentos con el mismo
    // aspecto y distinto sujeto en la misma pantalla se leen como el mismo número.
    montar('revision', [nota({ id: 1, campoId: 'festival.nombre' })]);
    expect(recuento().classList).toContain('hidden');

    montar('lectura', []);
    expect(recuento().classList).toContain('hidden');
  }));




  it('un fallo al traer los cambios pedidos se DICE, no se ve como «no te pidieron nada»', fakeAsync(() => {
    // LA SEÑAL EXISTÍA Y NO LA PINTABA NADIE DE ESTE LADO: `cargarCambiosPedidos()` escribía el
    // fallo en `revision.error` y solo lo leía la consola interna. Para la organización, que el GET
    // fallara se veía como una ficha sin una sola nota y un reenvío apagado sin motivo: los dos
    // síntomas de «no te pidieron nada», y los dos falsos.
    montar('atencion', [], 'enviada');
    almacen.error.set('No fue posible consultar los cambios pedidos');
    fixture.detectChanges();

    const aviso = buscar('ficha-error-revision')!;
    expect(aviso.classList).not.toContain('hidden');
    expect(aviso.getAttribute('role')).toBe('alert');
    expect(aviso.textContent).toContain('No fue posible consultar los cambios pedidos');
  }));

  it('sin fallo, el aviso de error de la revisión está escondido', fakeAsync(() => {
    montar('atencion', [nota()], 'enviada');
    expect(buscar('ficha-error-revision')!.classList).toContain('hidden');
  }));

  it('cada casilla «Ya lo corregí» dice a QUÉ campo pertenece', fakeAsync(() => {
    // Con tres cambios en el mismo paso, las tres casillas se anunciaban «Ya lo corregí» y nada
    // más: marcar la equivocada escribe en el servidor que se corrigió un campo que nadie tocó.
    //
    // MUTANTE QUE MATA: quitar el `<span class="sr-only">` del rótulo.
    montar('atencion', [nota({ id: 1, campoId: 'festival.nombre', campoEtiqueta: 'Nombre del festival' })], 'enviada');
    desplegar('generales');

    const rotulo = buscar<HTMLInputElement>('atender-festival.nombre')!.closest('label');
    expect(rotulo!.textContent).toContain('Ya lo corregí');
    expect(rotulo!.querySelector('.sr-only')!.textContent).toContain('Nombre del festival');
  }));

  it('en modo atención SIN notas todavía cargadas, el recuento no dice «todo hecho»', fakeAsync(() => {
    // ES UN INSTANTE REAL Y NO UN CASO IMPOSIBLE. `cargarCambiosPedidos()` pone el modo en
    // «atencion» y siembra el almacén VACÍO antes de pedir las notas al servidor
    // (`panel-organizacion-page.component.ts`). Sin la condición «y hay al menos una», ese instante
    // pinta un «Todos los ajustes hechos: 0 de 0» en verde sobre un Festival que tiene tres.
    montar('atencion', [], 'enviada');

    expect(recuento().classList).toContain('hidden');
    expect(buscar('ficha-ajustes-completos')).toBeNull();
  }));
});
