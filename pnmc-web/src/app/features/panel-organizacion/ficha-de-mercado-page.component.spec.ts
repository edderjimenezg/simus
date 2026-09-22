import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { appConfig } from '../../app.config';
import { FichaDeMercadoPageComponent } from './ficha-de-mercado-page.component';
import {
  EntradaHistorialMercado,
  MercadoDeLaOrganizacion,
  PanelOrganizacionApi,
} from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

/**
 * La ficha de un mercado en el panel de su organización.
 *
 * <b>LO QUE SE FIJA AQUI ES LA PARIDAD CON LA FICHA DE UN FESTIVAL</b>, que la dirección de producto
 * pidió: «la navegación, edición, publicación, etc. de un mercado debe
 * ser igual en cuanto sea posible a la de un festival, todo está muy diferente». Se comprueban las
 * cuatro cosas que lo hacían distinto: que haya cuatro secciones y no una página plana, que las
 * migas nombren el proceso concreto y vuelvan a su lista, que el historial exista y se pida solo
 * cuando se abre, y que la acción que se ofrece dependa del estado.
 */
describe('FichaDeMercadoPageComponent', () => {
  let fixture: ComponentFixture<FichaDeMercadoPageComponent>;
  let componente: FichaDeMercadoPageComponent;
  let historialPedido: number;
  let falloDelHistorial: unknown = null;
  let retirado: string | null;
  let solicitado: { mercadoId: string; justificacion: string } | null;
  let contactoGuardado: { mercadoId: string; solicitud: Record<string, unknown> } | null;
  let navegadoA: unknown[] | null;
  let marcado: { observacionId: number; atendida: boolean } | null;
  let cambios: Record<string, unknown>[];
  let propuestaViva: Record<string, unknown>;
  let propuestaGuardada: { mercadoId: string; motivo: string | null; campos: { campoId: string; valorPropuesto: string | null }[] } | null;
  let propuestaEnviada: string | null;
  let propuestaAbandonada: string | null;

  const mercado: MercadoDeLaOrganizacion = {
    id: 4, nombre: 'Mercado Musical del Amazonas', descripcion: 'Rueda de negocios del sur.',
    alcanceId: 1, alcance: 'Nacional', modalidadId: 2, modalidad: 'Presencial',
    periodicidad: 'anual', periodicidadDetalle: null,
    correoMercado: 'contacto@mercado.test', telefonoMercado: null, sitioWebMercado: null,
    instagramMercado: null, facebookMercado: null, otroEnlaceMercado: null, observacionesContacto: null,
    nivelCobertura: 'departamental', codigoDepartamento: '91', nombreDepartamento: 'Amazonas',
    codigoMunicipio: null, nombreMunicipio: null, lugarEspecifico: null,
    seRealizaEnElMarcoDeUnFestival: true, festivalId: 12, festivalNombre: 'Festival de Trova',
    organizacionId: 3104, organizacionNombre: 'Corporación Musical de Amazonas',
    estadoRegistro: 'borrador', numeroDeEdiciones: 1,
    fechaCreacion: '2026-09-01T00:00:00Z', fechaActualizacion: null, fechaPublicacion: null,
    practicasMusicales: [], territoriosSonoros: [],
  };

  const movimientos: EntradaHistorialMercado[] = [
    { modulo: 'Mercado', accion: 'MercadoEnviadoARevision', estadoAnterior: 'borrador', estadoNuevo: 'en_revision', comentario: null, fecha: '2026-09-10T12:00:00Z' },
    { modulo: 'Mercado', accion: 'MercadoCreado', estadoAnterior: null, estadoNuevo: null, comentario: null, fecha: '2026-09-01T09:00:00Z' },
  ];

  // `formatDate` necesita los datos de es-CO, y los registra `appConfig`.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  /** Un cambio pedido, que es el caso que importa: con campo, nota y el valor que tenía. */
  const UN_CAMBIO_PEDIDO = {
    id: 31, ambito: 'principal', subregistroId: null, seccionId: 'contacto',
    campoId: 'correoMercado', campoEtiqueta: 'Correo de contacto',
    valorObservado: 'contacto@mercado.test', nota: 'Ese correo rebota.',
    estado: 'pendiente', fechaAtencion: null,
  };

  /** Sin propuesta: el servidor devuelve una carcasa con `id` en cero, no un nulo. */
  const SIN_PROPUESTA = {
    id: 0, moduloId: 'mercados', registroId: '4', nombreDelRegistro: 'Mercado Musical del Amazonas',
    estado: 'borrador', motivo: null, organizacionNombre: null, proponenteNombre: null,
    decideNombre: null, motivoDeLaDecision: null, fechaEnvio: null, fechaDecision: null, campos: [],
  };

  function montar(
    estadoRegistro = 'borrador',
    cambiosPedidos: Record<string, unknown>[] = [UN_CAMBIO_PEDIDO],
    propuesta: Record<string, unknown> = SIN_PROPUESTA,
  ): void {
    historialPedido = 0;
    retirado = null; solicitado = null; contactoGuardado = null; navegadoA = null; marcado = null;
    cambios = cambiosPedidos;
    propuestaGuardada = null; propuestaEnviada = null; propuestaAbandonada = null;
    propuestaViva = propuesta;
    const parametros = new BehaviorSubject(convertToParamMap({}));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FichaDeMercadoPageComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionStore, useValue: {
          organizacionId: signal('3104'),
          organizacionElegida: () => ({ id: '3104', name: 'Corporación Musical de Amazonas' }),
        } },
        { provide: PanelOrganizacionApi, useValue: {
          obtenerMercado: () => of({ ...mercado, estadoRegistro }),
          obtenerCatalogosDeMercado: () => of({ alcances: [], modalidades: [], practicasMusicales: [], territoriosSonoros: [] }),
          obtenerUbicaciones: () => of([]),
          obtenerFestivalesElegibles: () => of([]),
          obtenerEdicionesDeMercado: () => of([]),
          obtenerHistorialMercado: () => {
            historialPedido += 1;
            return falloDelHistorial ? throwError(() => falloDelHistorial) : of(movimientos);
          },
          retirarMercado: (mercadoId: string) => { retirado = mercadoId; return of(undefined); },
          cambiosPedidosDeMercado: () => of(cambios),
          propuestaDeMercado: () => of(propuestaViva),
          guardarPropuestaDeMercado: (mercadoId: string, motivo: string | null, campos: { campoId: string; valorPropuesto: string | null }[]) => {
            propuestaGuardada = { mercadoId, motivo, campos };
            return of({ ...propuestaViva, id: 77, estado: 'borrador', motivo, campos: campos.map((c, i) => ({
              id: i + 1, seccionId: 'contacto', campoId: c.campoId, campoEtiqueta: c.campoId,
              valorAnterior: null, valorPropuesto: c.valorPropuesto,
            })) });
          },
          enviarPropuestaDeMercado: (mercadoId: string) => {
            propuestaEnviada = mercadoId;
            return of({ ...propuestaViva, id: 77, estado: 'en_revision' });
          },
          abandonarPropuestaDeMercado: (mercadoId: string) => { propuestaAbandonada = mercadoId; return of(undefined); },
          atenderCambioPedidoDeMercado: (observacionId: number, atendida: boolean) => {
            marcado = { observacionId, atendida };
            return of({ ...cambios[0], estado: atendida ? 'atendida' : 'pendiente', fechaAtencion: atendida ? '2026-09-16T00:00:00Z' : null });
          },
          solicitarRetiroDeMercado: (mercadoId: string, justificacion: string) => {
            solicitado = { mercadoId, justificacion };
            return of(undefined);
          },
          actualizarContactoDeMercado: (mercadoId: string, solicitud: Record<string, unknown>) => {
            contactoGuardado = { mercadoId, solicitud };
            return of({ ...mercado, estadoRegistro, ...solicitud });
          },
        } },
        { provide: ActivatedRoute, useValue: {
          snapshot: { paramMap: convertToParamMap({ id: '4' }) },
          paramMap: parametros.asObservable(),
          queryParamMap: parametros.asObservable(),
        } },
      ],
    });
    // SE VIGILA EL ROUTER REAL, no se sustituye: `routerLink` lo necesita para resolver los `href`,
    // y un doble sin `createUrlTree` deja la plantilla sin enlaces que comprobar.
    spyOn(TestBed.inject(Router), 'navigate').and.callFake((destino: unknown[]) => {
      navegadoA = destino;
      return Promise.resolve(true);
    });
    fixture = TestBed.createComponent(FichaDeMercadoPageComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('se recorre por secciones, y son las mismas cuatro que la ficha de un Festival', () => {
    // MUTANTE QUE MATA: volver a la página plana. La lista de secciones por nombre y en orden,
    // porque cuatro secciones equivocadas también son cuatro.
    montar();

    expect(componente.secciones.map(seccion => seccion.etiqueta)).toEqual([
      'Vista general', 'Información', 'Ediciones', 'Historial',
    ]);
    expect(componente.seccionActiva()).toBe('resumen');
  });

  it('las migas nombran el mercado y vuelven a la lista de mercados, no a «Procesos»', () => {
    // La regla del proyecto: las rutas y las migas nombran la entidad concreta que se gestiona.
    montar();
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-migas-nombre]')?.textContent?.trim()).toBe(mercado.nombre);
    const destinos = Array.from(raiz.querySelectorAll('nav[aria-label="Ubicación dentro de Gestión"] a'))
      .map(enlace => enlace.getAttribute('href'));
    expect(destinos).toContain('/gestion/procesos/mercados');
  });

  it('el historial se pide al entrar en su sección, y una sola vez', () => {
    // MUTANTE QUE MATA: pedirlo al abrir la ficha —cien movimientos que casi nadie mira— o
    // repetir la llamada cada vez que se vuelve a la pestaña.
    montar();
    expect(historialPedido).toBe(0);

    componente.irASeccion('historial');
    expect(historialPedido).toBe(1);
    expect(componente.historial().length).toBe(2);

    componente.irASeccion('resumen');
    componente.irASeccion('historial');
    expect(historialPedido).toBe(1);
  });

  it('un movimiento se lee con palabras, no con el nombre del evento auditado', () => {
    montar();

    expect(componente.etiquetaAccionHistorial('MercadoEnviadoARevision')).toBe('Enviado a revisión');
    expect(componente.etiquetaAccionHistorial('MercadoPublicado')).toBe('Publicado');
    // Un evento que todavía no se ha traducido no se enseña en crudo.
    expect(componente.etiquetaAccionHistorial('MercadoLoQueSea')).toBe('Actualización registrada');
  });

  it('en revisión no se ofrece editar: se dice en qué punto está el trámite', () => {
    // MUTANTE QUE MATA: pintar el botón apagado. Quien lo ve se queda buscando por qué.
    montar('en_revision');
    const raiz = fixture.nativeElement as HTMLElement;

    expect(componente.sePuedeEditar()).toBeFalse();
    expect(raiz.querySelector('[data-editar-mercado]')).toBeNull();
    expect(raiz.querySelector('[data-enviar-a-revision]')).toBeNull();
    expect(raiz.textContent).toContain('La información está siendo revisada');
  });

  it('publicado ofrece el directorio público y no el formulario', () => {
    montar('publicado');
    const raiz = fixture.nativeElement as HTMLElement;

    const enlace = raiz.querySelector('[data-ver-en-el-directorio]');
    expect(enlace?.getAttribute('href')).toBe('/ecosistema/mercados-musicales/4');
    expect(raiz.querySelector('[data-editar-mercado]')).toBeNull();
  });

  it('deshacer un envío devuelve el mercado a borrador y no lo saca de la ficha', () => {
    // MUTANTE QUE MATA: tratar las dos como la misma. Deshacer un envío conserva el trabajo —el
    // mercado vuelve a borrador y se corrige— y abandonar un borrador lo archiva. Decirlo mal en
    // el diálogo hace que quien pulsa crea que va a perder lo escrito, o que no lo va a perder.
    montar('en_revision');
    componente.pedirConfirmacionDelRetiro();
    expect(componente.estaEnBorrador()).toBeFalse();

    componente.retirarElMercado();

    expect(retirado).toBe('4');
    expect(componente.mercado()?.estadoRegistro).toBe('borrador');
    expect(navegadoA).toBeNull();
    expect(componente.aviso()).toContain('volvió a borrador');
  });

  it('abandonar un borrador vuelve a la lista: la ficha de lo archivado no tiene nada que ofrecer', () => {
    montar('borrador');
    componente.pedirConfirmacionDelRetiro();
    componente.retirarElMercado();

    expect(retirado).toBe('4');
    expect(navegadoA).toEqual(['/gestion/procesos/mercados']);
  });

  it('lo publicado no se retira: se pide, y con justificación', () => {
    // MUTANTE QUE MATA: mandar la solicitud sin justificación. El servidor la rechaza y quien la
    // pidió no entiende por qué; y quien la revisa no tendría con qué decidir.
    montar('publicado');
    componente.pedirElRetiroAlPrograma();

    componente.enviarLaSolicitudDeRetiro('   ');
    expect(solicitado).toBeNull();

    // El motivo lo recorta ya `app-confirmacion`; aquí se comprueba que la pantalla no lo pierde.
    componente.enviarLaSolicitudDeRetiro('El mercado dejó de realizarse.');

    expect(solicitado).toEqual({ mercadoId: '4', justificacion: 'El mercado dejó de realizarse.' });
    expect(componente.solicitudDeRetiro()).toBeNull();
    // Y NO SE OFRECE DOS VECES: dos solicitudes vivas son dos decisiones que pueden contradecirse.
    expect(componente.retiroSolicitado()).toBeTrue();
  });

  it('el contacto se corrige sobre un mercado publicado, y se abre con lo que ya hay', () => {
    // ABRIR VACIO BORRARIA EL CONTACTO al guardar un cambio de teléfono, que es justo el caso que
    // esta pantalla existe para resolver.
    montar('publicado');
    componente.empezarACorregirElContacto();

    expect(componente.contactoEnEdicion()?.correoMercado).toBe('contacto@mercado.test');

    componente.campoDelContacto('telefonoMercado', '3001234567');
    componente.guardarElContacto();

    expect(contactoGuardado?.mercadoId).toBe('4');
    expect(contactoGuardado?.solicitud['telefonoMercado']).toBe('3001234567');
    expect(contactoGuardado?.solicitud['correoMercado']).toBe('contacto@mercado.test');
    expect(componente.contactoEnEdicion()).toBeNull();
  });

  it('un borrador no ofrece solicitar retiro ni editar contacto: no hay nada publicado', () => {
    montar('borrador');
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-solicitar-retiro]')).toBeNull();
    expect(raiz.querySelector('[data-editar-contacto]')).toBeNull();
    expect(raiz.querySelector('[data-retirar-borrador]')).not.toBeNull();
  });

  it('los cambios pedidos se ven con el campo, la nota y lo que decía', () => {
    // MUTANTE QUE MATA: enseñar solo la nota. «Ese correo rebota» sin el campo ni el valor obliga a
    // adivinar cuál de los dos correos —el del mercado o el de la organización— y qué decía antes.
    montar('ajustes_solicitados');
    const raiz = fixture.nativeElement as HTMLElement;
    const bloque = raiz.querySelector('[data-cambios-pedidos]');

    expect(bloque).not.toBeNull();
    expect(bloque!.textContent).toContain('Correo de contacto');
    expect(bloque!.textContent).toContain('Ese correo rebota');
    expect(bloque!.textContent).toContain('contacto@mercado.test');
    expect(componente.cambiosPendientes()).toBe(1);
  });

  it('marcar y desmarcar un cambio: equivocarse no puede ser definitivo', () => {
    montar('ajustes_solicitados');

    componente.alternarCambio(componente.cambiosPedidos()[0]);
    expect(marcado).toEqual({ observacionId: 31, atendida: true });
    expect(componente.cambiosPedidos()[0].estado).toBe('atendida');
    expect(componente.cambiosPendientes()).toBe(0);

    componente.alternarCambio(componente.cambiosPedidos()[0]);
    expect(marcado).toEqual({ observacionId: 31, atendida: false });
    expect(componente.cambiosPedidos()[0].estado).toBe('pendiente');
  });

  it('sin cambios pedidos no se pinta el bloque: no hay nada que corregir', () => {
    montar('borrador', []);

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-cambios-pedidos]')).toBeNull();
  });

  it('si el historial falla lo dice, y deja volver a pedirlo', () => {
    // Un fallo no es «no hay movimientos»: decir que está vacío cuando no se pudo preguntar es
    // afirmar algo que no se sabe.
    falloDelHistorial = { message: 'No fue posible consultar el historial del mercado' };
    montar();
    componente.irASeccion('historial');

    expect(componente.historial().length).toBe(0);
    expect(componente.errorHistorial()).toContain('No fue posible');

    falloDelHistorial = null;
    componente.cargarHistorial();
    expect(componente.historial().length).toBe(2);
  });

  // ── La propuesta de cambio sobre lo publicado ───────────────────────────────────────────────

  it('sobre un mercado publicado se ofrece PROPONER, no editar', () => {
    // MUTANTE QUE MATA: ofrecer «Editar» sobre lo publicado. Editar en caliente es exactamente lo
    // que este circuito existe para impedir: lo que la gente está leyendo no cambia sin decisión.
    montar('publicado');
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-proponer-cambio]')).not.toBeNull();
    expect(raiz.querySelector('[data-guardar-ficha-mercado]')).toBeNull();
  });

  it('lo que se propone es la DIFERENCIA con la ficha publicada, no la ficha entera', () => {
    montar('publicado');

    componente.empezarAProponer();
    componente.campo('correoMercado', 'nuevo@mercado.test');
    componente.guardarLaPropuesta('El anterior rebota.');

    // Un solo campo, y el que cambió: mandar los diecinueve haría que el Programa tuviera que
    // averiguar cuáles miró la organización y cuáles simplemente venían.
    expect(propuestaGuardada?.campos.length).toBe(1);
    expect(propuestaGuardada?.campos[0]).toEqual({ campoId: 'correoMercado', valorPropuesto: 'nuevo@mercado.test' });
    expect(propuestaGuardada?.motivo).toBe('El anterior rebota.');
  });

  it('guardar la propuesta no toca la ficha publicada, y la pantalla lo dice', () => {
    montar('publicado');

    componente.empezarAProponer();
    componente.campo('correoMercado', 'nuevo@mercado.test');
    componente.guardarLaPropuesta('');
    fixture.detectChanges();

    expect(componente.mercado()?.correoMercado).toBe('contacto@mercado.test');
    expect(componente.aviso()).toContain('Todavía no la ve el Programa');
  });

  it('una propuesta en revisión no se puede seguir editando ni descartar', () => {
    montar('publicado', [], { ...SIN_PROPUESTA, id: 77, estado: 'en_revision', motivo: 'Cambió el correo.',
      fechaEnvio: '2026-09-17T00:00:00Z',
      campos: [{ id: 1, seccionId: 'contacto', campoId: 'correoMercado', campoEtiqueta: 'Correo de contacto',
                 valorAnterior: 'contacto@mercado.test', valorPropuesto: 'nuevo@mercado.test' }] });
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-propuesta-en-revision]')).not.toBeNull();
    expect(raiz.querySelector('[data-seguir-editando-propuesta]')).toBeNull();
    expect(raiz.querySelector('[data-abandonar-propuesta]')).toBeNull();
    expect(raiz.querySelector('[data-proponer-cambio]')).toBeNull();
  });

  it('un borrador sin enviar se puede mandar, seguir editando o descartar', () => {
    montar('publicado', [], { ...SIN_PROPUESTA, id: 77,
      campos: [{ id: 1, seccionId: 'contacto', campoId: 'correoMercado', campoEtiqueta: 'Correo de contacto',
                 valorAnterior: null, valorPropuesto: 'nuevo@mercado.test' }] });
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('[data-propuesta-en-borrador]')).not.toBeNull();
    raiz.querySelector<HTMLButtonElement>('[data-enviar-propuesta]')!.click();

    expect(propuestaEnviada).toBe('4');
  });

  it('descartar el borrador lo retira y deja dicho que la ficha publicada no cambió', () => {
    montar('publicado', [], { ...SIN_PROPUESTA, id: 77,
      campos: [{ id: 1, seccionId: 'contacto', campoId: 'correoMercado', campoEtiqueta: 'Correo de contacto',
                 valorAnterior: null, valorPropuesto: 'nuevo@mercado.test' }] });
    const raiz = fixture.nativeElement as HTMLElement;

    raiz.querySelector<HTMLButtonElement>('[data-abandonar-propuesta]')!.click();
    fixture.detectChanges();

    // MUTANTE QUE MATA: dejar la propuesta en pantalla tras descartarla. Y el aviso tiene que
    // decir lo que NO pasó: quien descarta necesita saber que no tocó la ficha publicada.
    expect(propuestaAbandonada).toBe('4');
    expect(componente.propuesta()).toBeNull();
    expect(componente.aviso()).toContain('La ficha publicada no cambió');
  });
});
