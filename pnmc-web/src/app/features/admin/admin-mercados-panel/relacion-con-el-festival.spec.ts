import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminMercadosPanelComponent } from './admin-mercados-panel.component';

/**
 * Un mercado se relaciona con un festival de verdad, o no se relaciona.
 *
 * <b>EL DEFECTO QUE TRAEN A IMPEDIR.</b> La tabla heredada de la entrega de septiembre guardaba el
 * festival asociado DOS veces —`idfestivalasociado` con clave ajena y `nombrefestivalasociado` como
 * texto suelto—, sin nada que obligara a que coincidieran; y su formulario ofrecía solo el texto,
 * de modo que el directorio público mostraba nombres de festivales que el sistema no sabía
 * localizar. El criterio es este: «no debe ser un campo
 * de texto libre; debe establecer una relación real entre registros del sistema».
 *
 * <b>LO QUE FIJAN.</b> Que los candidatos se piden por ORGANIZACION, que no se piden mientras no
 * haya una elegida, que responder que no borra el festival que hubiera puesto, y que ordenar la
 * tabla se lo pide al servidor.
 */
describe('la relación de un mercado con un festival', () => {
  let fixture: ComponentFixture<AdminMercadosPanelComponent>;
  let componente: AdminMercadosPanelComponent;
  let http: HttpTestingController;

  const PAGINA_VACIA = { items: [], total: 0, limit: 20, offset: 0 };

  function direccionDeLaLista(): string {
    const peticion = http.expectOne(r => r.url.includes('/institucional/mercados') && !r.url.includes('festivales-elegibles'));
    const url = peticion.request.urlWithParams;
    peticion.flush(PAGINA_VACIA);
    return url;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminMercadosPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(AdminMercadosPanelComponent);
    componente = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  it('sin organización elegida no pregunta por festivales', () => {
    // NO HAY UN CONJUNTO DE FESTIVALES «DE NADIE»: preguntar sin organización devolvería la lista
    // entera del país, que es exactamente lo que la regla de pertenencia impide.
    componente.nuevo();
    fixture.detectChanges();

    http.expectNone(r => r.url.includes('festivales-elegibles'));
  });

  it('elegir la organización pide SUS festivales', () => {
    componente.nuevo();
    fixture.detectChanges();
    componente.campo('organizacionId', 42);
    // EL EFECTO QUE PIDE LOS FESTIVALES CORRE EN LA DETECCION DE CAMBIOS, no al asignar la señal.
    fixture.detectChanges();

    const peticion = http.expectOne(r => r.url.includes('festivales-elegibles'));
    expect(peticion.request.urlWithParams).toContain('organizacion=42');
    peticion.flush([]);
  });

  it('responder que no borra el festival que se hubiera elegido', () => {
    // DEJARLO GUARDADO SERIA UNA RELACION QUE LA FICHA NO ENSEÑA y que nadie sabe que existe. El
    // servidor la rechaza y el CHECK de la base también, pero enterarse al guardar es peor.
    componente.nuevo();
    componente.cambiarSiEstaEnUnFestival(true);
    componente.campo('festivalId', 7);
    expect(componente.formulario()?.festivalId).toBe(7);

    componente.cambiarSiEstaEnUnFestival(false);

    expect(componente.formulario()?.seRealizaEnElMarcoDeUnFestival).toBeFalse();
    expect(componente.formulario()?.festivalId).toBeNull();
  });

  it('pasar a cobertura nacional limpia el territorio que deja de tener sentido', () => {
    // SIN ESTO EL GUARDADO FALLA POR ALGO QUE YA NO SE VE: el campo desaparece de la pantalla pero
    // su valor sigue en el formulario, y el CHECK lo rechaza señalando algo que no está.
    componente.nuevo();
    componente.cambiarDepartamento('05');
    componente.campo('codigoMunicipio', '05001');

    componente.cambiarCobertura('nacional');

    expect(componente.formulario()?.codigoDepartamento).toBeNull();
    expect(componente.formulario()?.codigoMunicipio).toBeNull();
  });

  it('cambiar de departamento suelta el municipio del anterior', () => {
    componente.nuevo();
    componente.cambiarDepartamento('05');
    componente.campo('codigoMunicipio', '05001');

    componente.cambiarDepartamento('08');

    expect(componente.formulario()?.codigoMunicipio).toBeNull();
  });

  it('ordenar una columna se lo pide al servidor y vuelve a la primera página', () => {
    // ORDENAR EN MEMORIA ES EL DEFECTO QUE EL PROYECTO YA CORRIGIO EN AGENDA: con veinte filas por
    // página, reordenar el array traído baraja esas veinte y deja el resto donde estaba.
    componente.pagina.set(4);
    componente.ordenarPor('estado');

    expect(componente.pagina()).toBe(1);
    const url = direccionDeLaLista();
    expect(url).toContain('orden=estado');
    expect(url).toContain('pagina=1');
  });

  it('un solo menú de acciones por fila, con lo que aplica al estado', () => {
    // MERCADOS NACIO CON DOS BOTONES SUELTOS y quedó definido el 15 de septiembre
    // de 2026: Festivales tiene un menú único. Lo que no aplica al estado no se pinta apagado: no
    // se pinta.
    const base = { id: 1, nombre: 'M', estadoRegistro: 'borrador' } as unknown as Parameters<typeof componente.accionesDe>[0];

    const enBorrador = componente.accionesDe(base).map(a => a.id);
    const enRevision = componente.accionesDe({ ...base, estadoRegistro: 'en_revision' }).map(a => a.id);
    const publicado = componente.accionesDe({ ...base, estadoRegistro: 'publicado' }).map(a => a.id);

    // «Abrir ficha» VA PRIMERA Y EN TODOS LOS ESTADOS: ver qué es el registro precede a cambiarlo,
    // y desde es lo único desde donde un mercado se publica, se retira
    // o se archiva, igual que en Festivales.
    expect(enBorrador).toEqual(['ficha', 'editar', 'previsualizar', 'historial']);
    // PUBLICADO NO SE PREVISUALIZA, SE ABRE: previsualizar algo que cualquiera ve ya en el portal
    // es ofrecer un ensayo de lo que está en pie. La regla es la de Festivales.
    expect(publicado).toContain('ver');
    expect(publicado).not.toContain('previsualizar');

    // RETIRAR SOLO APLICA A LO PUBLICADO, y va la última. Ofrecerlo sobre un borrador es ofrecer
    // quitar del portal algo que nunca estuvo en el portal.
    expect(publicado[publicado.length - 1]).toBe('eliminar');
    expect(enBorrador).not.toContain('eliminar');
    expect(enRevision).not.toContain('eliminar');
    expect(enRevision[0]).toBe('revisar');
    expect(enRevision).toContain('ficha');
    expect(enRevision).not.toContain('ediciones');
    expect(publicado).toContain('ediciones');
    expect(publicado).not.toContain('revisar');
  });

  it('retirar del ecosistema exige motivo y archiva, no borra', () => {
    // MUTANTE QUE MATA: mandar la petición con el motivo vacío. El servidor la rechaza y quien la
    // pulsó no entiende por qué, cuando el propio botón podía no dejarle llegar ahí.
    const mercado = { id: 9, nombre: 'Mercado publicado', estadoRegistro: 'publicado' } as unknown as Parameters<typeof componente.pedirElRetiro>[0];
    componente.pedirElRetiro(mercado);

    componente.retirarDelEcosistema('   ');
    http.expectNone('/api/v1/institucional/mercados/9/archivar');

    componente.retirarDelEcosistema('  Duplicado del mercado 4.  ');
    const peticion = http.expectOne('/api/v1/institucional/mercados/9/archivar');
    // EL MOTIVO VIAJA SIN ESPACIOS SOBRANTES: es lo que va a quedar escrito en la bitácora.
    expect(peticion.request.body).toEqual({ motivo: 'Duplicado del mercado 4.' });
    peticion.flush({ id: 9, estadoRegistro: 'archivado' });

    expect(componente.retiroPedido()).toBeNull();
    expect(componente.aviso()).toContain('retirado del ecosistema');
  });

  it('si el servidor niega el retiro, el diálogo se queda y lo dice', () => {
    // Un fallo no puede cerrar el formulario: quien escribió el motivo lo perdería y no sabría
    // si llegó a hacerse o no.
    const mercado = { id: 9, nombre: 'Mercado publicado', estadoRegistro: 'publicado' } as unknown as Parameters<typeof componente.pedirElRetiro>[0];
    componente.pedirElRetiro(mercado);
    componente.retirarDelEcosistema('Duplicado.');

    http.expectOne('/api/v1/institucional/mercados/9/archivar')
      .flush({ message: 'Solo un mercado publicado puede retirarse del ecosistema desde aquí.' }, { status: 409, statusText: 'Conflict' });

    expect(componente.retiroPedido()).not.toBeNull();
    expect(componente.errorDelRetiro()).toContain('publicado');
  });

  it('el ciclo de una edición se ofrece según dónde esté, y publicar tiene su propia puerta', () => {
    // MUTANTE QUE MATA: dejar solo «Editar», que era lo único que había. Una edición se creaba y se
    // quedaba en borrador para siempre, sin forma de publicarla, retirarla ni archivarla.
    const edicion = { id: 7, anio: 2026, nombre: null, estado: 'programada', estadoVisibilidad: 'borrador' } as unknown as Parameters<typeof componente.accionesDeLaEdicion>[0];

    expect(componente.accionesDeLaEdicion(edicion).map(a => a.id)).toEqual(['editar', 'publicar', 'archivar', 'eliminar']);
    expect(componente.accionesDeLaEdicion({ ...edicion, estadoVisibilidad: 'publicado' }).map(a => a.id))
      .toEqual(['editar', 'despublicar', 'archivar']);
    // ARCHIVADA NO OFRECE NADA: reabrir lo archivado sin dejar constancia es peor que no poder.
    expect(componente.accionesDeLaEdicion({ ...edicion, estadoVisibilidad: 'archivado' })).toEqual([]);

    componente.viendoEdiciones.set({ id: 9, nombre: 'Mercado' } as unknown as Parameters<typeof componente.accionesDe>[0]);
    componente.ejecutarAccionDeEdicion(edicion, 'publicar');

    const peticion = http.expectOne('/api/v1/institucional/mercados/9/ediciones/7/publicar');
    expect(peticion.request.method).toBe('POST');
    peticion.flush({ ...edicion, estadoVisibilidad: 'publicado' });
  });

  it('eliminar una edición pregunta antes, y solo borra al confirmar', () => {
    const edicion = { id: 7, anio: 2026, nombre: null, estado: 'programada', estadoVisibilidad: 'borrador' } as unknown as Parameters<typeof componente.accionesDeLaEdicion>[0];
    componente.viendoEdiciones.set({ id: 9, nombre: 'Mercado' } as unknown as Parameters<typeof componente.accionesDe>[0]);

    componente.ejecutarAccionDeEdicion(edicion, 'eliminar');
    http.expectNone('/api/v1/institucional/mercados/9/ediciones/7');
    expect(componente.edicionPorEliminar()).not.toBeNull();

    componente.eliminarLaEdicion();
    const peticion = http.expectOne('/api/v1/institucional/mercados/9/ediciones/7');
    expect(peticion.request.method).toBe('DELETE');
    peticion.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('al crear se pregunta por los mercados que ya se llaman parecido; con menos de tres letras, no', async () => {
    // MUTANTE QUE MATA: preguntar con una o dos letras. La coincidencia sería con medio catálogo y
    // el aviso dejaría de significar nada. Se espera al temporizador de verdad —la búsqueda va
    // detrás de un retardo— en vez de simular el reloj, porque la respuesta llega por una promesa
    // y `jasmine.clock` no adelanta microtareas.
    componente.nuevo();
    componente.campo('nombre', 'Mercado del Sur');
    await new Promise(listo => setTimeout(listo, 700));

    const peticion = http.expectOne(r => r.url.includes('/admin/mercados/coincidencias'));
    expect(decodeURIComponent(peticion.request.urlWithParams)).toContain('Mercado del Sur');
    peticion.flush([{ id: '9', nombre: 'Mercado del Sur', estado: 'publicado', organizacionResponsable: 'Otra fundación' }]);
    await new Promise(listo => setTimeout(listo, 0));

    expect(componente.coincidencias().length).toBe(1);

    componente.campo('nombre', 'Me');
    await new Promise(listo => setTimeout(listo, 700));
    http.expectNone(r => r.url.includes('/admin/mercados/coincidencias'));
    expect(componente.coincidencias()).toEqual([]);
  });

  it('sobre un mercado que ya existe no se busca: encontrarse a sí mismo no es un aviso', async () => {
    componente.editar({
      id: 9, nombre: 'Mercado del Sur', estadoRegistro: 'borrador',
      practicasMusicales: [], territoriosSonoros: [],
    } as unknown as Parameters<typeof componente.editar>[0]);
    componente.campo('nombre', 'Mercado del Sur y algo más');
    await new Promise(listo => setTimeout(listo, 700));

    http.expectNone(r => r.url.includes('/admin/mercados/coincidencias'));
  });

  it('la devolución se hace campo por campo, y no se envía sin señalar ninguno', () => {
    // MUTANTE QUE MATA: dejar el párrafo de antes. Todo lo que hubiera que decir sobre treinta
    // campos cabía en un texto suelto, y a la organización le llegaba un aviso sin decir a qué
    // campo se refería cada frase.
    componente.abrirLaDecision({ id: 9, nombre: 'Mercado del Sur', estadoRegistro: 'en_revision' } as unknown as Parameters<typeof componente.abrirLaDecision>[0]);
    http.expectOne('/api/v1/institucional/mercados/9/revision')
      .flush({ id: 0, moduloId: 'mercados', registroId: '9', registroNombre: 'Mercado del Sur', estado: 'borrador', observacionGeneral: null, revisorNombre: null, destinatarioNombre: null, organizacionNombre: null, fechaActualizacion: null, fechaEnvio: null, observaciones: [] });

    // SIN NINGUN CAMPO SEÑALADO NO SE MANDA NADA: devolver sin decir qué corregir deja a la
    // organización con el mercado en las manos y ninguna instrucción.
    componente.enviarLaRevision();
    http.expectNone('/api/v1/institucional/mercados/9/revision/enviar');
    expect(componente.errorDeLaDecision()).toContain('al menos un campo');

    componente.escribirNota('correoMercado', '  Ese correo rebota.  ');
    expect(componente.camposSenalados()).toBe(1);
    componente.enviarLaRevision();

    const peticion = http.expectOne('/api/v1/institucional/mercados/9/revision/enviar');
    const cuerpo = peticion.request.body as { observaciones: Record<string, unknown>[] };
    const nota = cuerpo.observaciones[0];
    expect(nota['campoId']).toBe('correoMercado');
    // EL ROTULO Y LA SECCION VIAJAN CON LA NOTA: quien la recibe tiene que encontrar el campo.
    expect(nota['campoEtiqueta']).toBe('Correo de contacto');
    expect(nota['seccionId']).toBe('contacto');
    expect(nota['nota']).toBe('Ese correo rebota.');
    peticion.flush({ id: 5, moduloId: 'mercados', registroId: '9', registroNombre: 'Mercado del Sur', estado: 'enviada', observacionGeneral: null, revisorNombre: null, destinatarioNombre: null, organizacionNombre: null, fechaActualizacion: null, fechaEnvio: '2026-09-16T00:00:00Z', observaciones: [] });
    http.expectOne(r => r.url.includes('/institucional/mercados') && !r.url.includes('revision')).flush(PAGINA_VACIA);

    expect(componente.decidiendo()).toBeNull();
  });

  it('el borrador de la devolución se recupera al reabrir: revisar treinta campos no se hace de una sentada', () => {
    componente.abrirLaDecision({ id: 9, nombre: 'Mercado del Sur', estadoRegistro: 'en_revision' } as unknown as Parameters<typeof componente.abrirLaDecision>[0]);

    http.expectOne('/api/v1/institucional/mercados/9/revision').flush({
      id: 5, moduloId: 'mercados', registroId: '9', registroNombre: 'Mercado del Sur', estado: 'borrador',
      observacionGeneral: 'Faltan dos cosas.', revisorNombre: null, destinatarioNombre: null,
      organizacionNombre: null, fechaActualizacion: null, fechaEnvio: null,
      observaciones: [{
        id: 31, ambito: 'principal', subregistroId: null, seccionId: 'contacto', campoId: 'correoMercado',
        campoEtiqueta: 'Correo de contacto', valorObservado: null, nota: 'Ese correo rebota.',
        estado: 'pendiente', fechaAtencion: null,
      }],
    });

    expect(componente.motivoDeLosAjustes()).toBe('Faltan dos cosas.');
    expect(componente.notaDelCampo('correoMercado')).toBe('Ese correo rebota.');
    expect(componente.camposSenalados()).toBe(1);
  });

  it('el ojo de borradores se lo pide al servidor, no filtra la página', () => {
    componente.mostrarBorradores.set(false);
    void componente.cargar();

    expect(direccionDeLaLista()).toContain('incluirBorradores=false');
  });

  it('con los borradores visibles no se manda nada: el servidor los incluye por omisión', () => {
    componente.mostrarBorradores.set(true);
    void componente.cargar();

    expect(direccionDeLaLista()).not.toContain('incluirBorradores');
  });

  afterEach(() => {
    http.match(() => true).forEach(p => p.flush({ items: [], total: 0, alcances: [], modalidades: [] }));
    http.verify();
  });
});
