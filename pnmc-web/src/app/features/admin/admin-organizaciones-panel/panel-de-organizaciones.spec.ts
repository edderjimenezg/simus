import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminOrganizacionesPanelComponent } from './admin-organizaciones-panel.component';

/**
 * La pestaña «Organizaciones» de la consola, pedida.
 *
 * <b>Lo que se pidió:</b> ver las organizaciones en una tabla como las de los seis procesos, con el
 * nombre del responsable de cada una.
 *
 * <b>Lo que la pantalla tiene que distinguir y no se ve solo:</b> ese nombre puede venir de dos
 * sitios que no valen lo mismo. `EntidadesResponsable` es una persona que dio su nombre y su
 * documento al dar de alta la organización; la cuenta vinculada en `UsuariosEntidades` es una
 * deducción a partir de quién puede administrarla. Pintadas igual, la segunda se leería como la
 * primera.
 */
describe('AdminOrganizacionesPanelComponent · el registro de organizaciones', () => {
  let fixture: ComponentFixture<AdminOrganizacionesPanelComponent>;
  let componente: AdminOrganizacionesPanelComponent;
  let http: HttpTestingController;

  const DECLARADA = {
    id: '117',
    nombre: 'CreacionOrgPrueba',
    nombreLegal: null,
    identificacion: null,
    correoContacto: 'creacionorgprueba@pnmc.test',
    telefono: null,
    estado: 'pendiente_de_confirmacion',
    estadoEtiqueta: 'Pendiente de confirmación',
    activa: true,
    esInstitucional: false,
    territorio: '',
    responsable: {
      nombre: 'Camila Prueba Responsable',
      correo: 'creacionorgprueba@pnmc.test',
      telefono: '3001112233',
      tipoDocumento: 'CC',
      tieneDocumento: true,
      autorizacionDatos: true,
      desde: '2026-08-25T22:29:41',
      rolEntidad: null,
      origen: 'declarado',
    },
    procesos: { festivales: 1, publicados: 1, enCurso: 0, dependientes: 1, total: 1 },
    procedencia: null,
    // PENDIENTE DE CONFIRMACION Y CON UNA CUENTA A LA QUE COMPROBARSELO: es el caso en el que la
    // consola tiene que ofrecer «Confirmar el correo», y el único en el que «Activar» no aparece.
    correoConfirmado: false,
    correosDeCuenta: ['creacionorgprueba@pnmc.test'],
    fechaCreacion: '2026-08-25T22:29:41',
    fechaActualizacion: '2026-08-25T22:29:41',
  };

  const DEDUCIDA = {
    ...DECLARADA,
    id: '101',
    nombre: 'Asociación Musical Sinfónica Opus',
    estado: 'activa',
    estadoEtiqueta: 'Activa',
    territorio: 'MEDELLÍN, ANTIOQUIA',
    responsable: {
      nombre: 'Participante de Prueba 2',
      correo: 'participante.dos@pnmc.local',
      telefono: null,
      tipoDocumento: null,
      tieneDocumento: false,
      autorizacionDatos: false,
      desde: '2026-08-25T20:51:56',
      rolEntidad: 'propietario',
      origen: 'cuenta',
    },
    procesos: { festivales: 0, publicados: 0, enCurso: 0, dependientes: 0, total: 0 },
    correoConfirmado: true,
    correosDeCuenta: ['participante.dos@pnmc.local'],
  };

  const INSTITUCIONAL = {
    ...DECLARADA,
    id: '116',
    nombre: 'Plan Nacional de Música para la Convivencia',
    estado: 'activa',
    estadoEtiqueta: 'Activa',
    esInstitucional: true,
    territorio: '',
    responsable: null,
    procesos: { festivales: 165, publicados: 120, enCurso: 5, dependientes: 125, total: 165 },
    correoConfirmado: true,
    correosDeCuenta: [],
  };

  const RESPUESTA = {
    total: 3,
    pagina: 1,
    tamanoPagina: 100,
    estados: [
      { id: 'activa', etiqueta: 'Activa', total: 2 },
      { id: 'pendiente_de_confirmacion', etiqueta: 'Pendiente de confirmación', total: 1 },
    ],
    items: [DECLARADA, DEDUCIDA, INSTITUCIONAL],
    territorios: [
      { codigo: '05', etiqueta: 'Antioquia', total: 1 },
      { codigo: 'sin_territorio', etiqueta: 'Sin territorio', total: 2 },
    ],
    orden: 'nombre',
    direccion: 'asc',
  };

  const montar = (carga: Record<string, unknown> = RESPUESTA) => {
    fixture = TestBed.createComponent(AdminOrganizacionesPanelComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    http.expectOne(p => p.url.includes('/admin/organizaciones')).flush(carga);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminOrganizacionesPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const raiz = () => fixture.nativeElement as HTMLElement;
  const fila = (id: string) => raiz().querySelector(`[data-organizacion="${id}"]`)!;

  it('cada organización trae el nombre de su responsable', () => {
    montar();

    expect(raiz().querySelectorAll('[data-organizacion]').length).toBe(3);
    expect(fila('117').querySelector('[data-responsable]')!.textContent).toContain('Camila Prueba Responsable');
    expect(fila('101').querySelector('[data-responsable]')!.textContent).toContain('Participante de Prueba 2');
  });

  it('distingue el responsable declarado del deducido de la cuenta', () => {
    // ESTA ES LA MITAD QUE NO SE VE SOLA. Un nombre que salió de `EntidadesResponsable` lo firmó
    // una persona identificada; uno que salió de `UsuariosEntidades` es quien puede administrar la
    // organización, que no es lo mismo. Sin la marca, la segunda se leería como la primera.
    montar();

    expect(fila('117').querySelector('[data-origen]')!.getAttribute('data-origen')).toBe('declarado');
    expect(fila('117').querySelector('[data-origen]')!.textContent).toContain('Declarado');
    expect(fila('101').querySelector('[data-origen]')!.getAttribute('data-origen')).toBe('cuenta');
    expect(fila('101').querySelector('[data-origen]')!.textContent).toContain('Deducido');
  });

  it('una organización sin nadie identificado lo dice, no deja el hueco', () => {
    montar();

    expect(fila('116').querySelector('[data-responsable]')!.textContent).toContain('Nadie identificado');
    expect(fila('116').querySelector('[data-origen]')!.getAttribute('data-origen')).toBe('ninguno');
  });

  it('el documento se anuncia sin decir el número', () => {
    // El número NO viaja desde la API. Aquí se comprueba que la fila con documento lo dice, y que
    // la que no lo tiene no inventa una etiqueta vacía.
    montar();

    expect(fila('117').querySelector('[data-documento]')!.textContent).toContain('CC');
    expect(fila('101').querySelector('[data-documento]')).toBeNull();
  });

  it('cuenta los procesos que sostiene cada organización', () => {
    montar();

    expect(fila('116').querySelector('[data-procesos-total]')!.textContent!.trim()).toBe('165');
    expect(fila('117').querySelector('[data-procesos-total]')!.textContent!.trim()).toBe('1');
    // EL DESGLOSE ES POR ESTADO Y NO POR TIPO DE PROCESO. «165» no dice si son borradores que nadie
    // ha visto o festivales publicados de los que depende media región, y esa es justo la diferencia
    // que necesita quien decide si archivar la organización. Los que están en cero se omiten.
    expect(componente.procesosDe(DECLARADA).map(p => p.etiqueta)).toEqual(['Publicados']);
    expect(componente.procesosDe(DEDUCIDA)).toEqual([]);
    expect(componente.procesosDe(INSTITUCIONAL).map(p => p.etiqueta))
      .toEqual(['Publicados', 'En curso', 'Borradores']);

    // Y LA TABLA AVISA DE CUANTOS DEPENDEN DE ELLA, que es lo que un total no puede decir.
    expect(fila('117').querySelector('[data-procesos-dependientes]')!.textContent)
      .toContain('1 depende de ella');
    expect(fila('116').querySelector('[data-procesos-dependientes]')!.textContent)
      .toContain('125 dependen de ella');
    expect(fila('101').querySelector('[data-procesos-dependientes]')).toBeNull();
  });

  it('marca la entidad institucional, que responde por lo que nadie ha reclamado', () => {
    // Sus 165 procesos son los que ninguna organización del ecosistema ha reclamado todavía. Sin la
    // marca, esa cifra se leería como la de una organización cualquiera.
    montar();

    expect(fila('116').querySelector('[data-institucional]')).not.toBeNull();
    expect(fila('117').querySelector('[data-institucional]')).toBeNull();
  });

  it('dice cuántas filas de la página se apoyan en la cuenta y no en una persona declarada', () => {
    montar();

    const aviso = raiz().querySelector('[data-aviso-respaldo]')!;
    expect(componente.sinResponsableDeclarado()).toBe(2);
    expect(aviso.textContent).toContain('2 de 3');
  });

  it('sin ninguna fila de respaldo, el aviso no aparece', () => {
    montar({ ...RESPUESTA, total: 1, items: [DECLARADA] });

    expect(componente.sinResponsableDeclarado()).toBe(0);
    expect(raiz().querySelector('[data-aviso-respaldo]')).toBeNull();
  });

  it('las pestañas llevan la cifra de TODAS las organizaciones, no la de lo que se ve', () => {
    montar();
    const pestanas = Array.from(raiz().querySelectorAll('[data-filtro-organizacion]'))
      .map(b => (b.textContent ?? '').replace(/\s+/g, ' ').trim());

    expect(pestanas).toContain('Todas 3');
    expect(pestanas.some(p => p.includes('Activa') && p.includes('2'))).toBeTrue();
  });

  it('con una pestaña de estado puesta, «Todas» sigue diciendo cuántas hay en total', () => {
    // DEFECTO ENCONTRADO EL 26 DE AGOSTO AL AÑADIR LOS FILTROS. La cifra salía de `total`, que es el
    // recuento del resultado YA filtrado: con «Pendiente de confirmación» pulsada, la pestaña «Todas» decía 1 al
    // lado de una lista de una fila, y pulsarla mostraba tres. Ahora suma las facetas, que es lo que
    // saldría al quitar ese filtro.
    montar({ ...RESPUESTA, total: 1, items: [DECLARADA] });

    const todas = raiz().querySelector('[data-filtro-organizacion="todos"]')!;
    expect(todas.textContent!.replace(/\s+/g, ' ').trim()).toBe('Todas 3');
    expect(componente.totalDeEstados()).toBe(3);
  });

  it('pulsar una pestaña vuelve a pedir el listado filtrado por ese estado', () => {
    montar();
    raiz().querySelector<HTMLButtonElement>('[data-filtro-organizacion="pendiente_de_confirmacion"]')!.click();

    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('estado=pendiente_de_confirmacion');
    peticion.flush({ ...RESPUESTA, items: [DECLARADA] });
    fixture.detectChanges();

    expect(componente.estadoActivo()).toBe('pendiente_de_confirmacion');
    expect(raiz().querySelectorAll('[data-organizacion]').length).toBe(1);
  });

  it('la búsqueda viaja al servidor y no se filtra en el navegador', () => {
    // SE BUSCA POR `data-testid` Y NO POR UN ATRIBUTO PROPIO: el campo lo pinta ahora
    // `app-buscador-de-lista`, la pieza compartida, que le pone el identificador que se le pasa.
    montar();
    const caja = raiz().querySelector<HTMLInputElement>('[data-testid="buscar-organizacion"]')!;
    caja.value = 'Creacion';
    // INTRO ADELANTA LA ESPERA. El buscador avisa solo al dejar de escribir —300 ms—, e Intro
    // emite en el acto, que es lo que esta prueba usa para no depender del reloj.
    caja.dispatchEvent(new Event('input'));
    caja.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('q=Creacion');
    peticion.flush({ ...RESPUESTA, items: [DECLARADA] });
  });

  it('un 404 dice que hay que reiniciar la API, no un «no fue posible» a secas', () => {
    fixture = TestBed.createComponent(AdminOrganizacionesPanelComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    http.expectOne(p => p.url.includes('/admin/organizaciones'))
      .flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(raiz().querySelector('[data-error-organizaciones]')!.textContent).toContain('Reiníciala');
  });

  // ================================================================================
  // ORDEN Y FILTROS · pedidos
  // ================================================================================

  it('pulsar una cabecera le pide al servidor ese orden, y no reordena lo que ya tiene', () => {
    // EL ORDEN NO PUEDE HACERSE AQUI. El servidor devuelve una pagina de como mucho cien filas de
    // un resultado que puede ser mayor: ordenar en el navegador ordenaria ESA pagina y dejaria
    // fuera lo que no vino, con la tabla afirmando «primero la mas antigua» mientras la mas antigua
    // se quedo en el servidor.
    montar();
    raiz().querySelector<HTMLButtonElement>('[data-ordenar="creacion"]')!.click();

    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('orden=creacion');
    expect(peticion.request.url).toContain('direccion=asc');
    peticion.flush({ ...RESPUESTA, orden: 'creacion', direccion: 'asc' });
  });

  it('pulsar dos veces la misma cabecera le da la vuelta, y pasar a otra vuelve a ascendente', () => {
    montar();

    raiz().querySelector<HTMLButtonElement>('[data-ordenar="creacion"]')!.click();
    http.expectOne(p => p.url.includes('/admin/organizaciones'))
      .flush({ ...RESPUESTA, orden: 'creacion', direccion: 'asc' });
    fixture.detectChanges();

    raiz().querySelector<HTMLButtonElement>('[data-ordenar="creacion"]')!.click();
    const segunda = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(segunda.request.url).toContain('direccion=desc');
    segunda.flush({ ...RESPUESTA, orden: 'creacion', direccion: 'desc' });
    fixture.detectChanges();

    // Cambiar de columna NO conserva el «desc» de la anterior: nadie pide una columna nueva para
    // verla del revés.
    raiz().querySelector<HTMLButtonElement>('[data-ordenar="territorio"]')!.click();
    const tercera = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(tercera.request.url).toContain('orden=territorio');
    expect(tercera.request.url).toContain('direccion=asc');
    tercera.flush({ ...RESPUESTA, orden: 'territorio', direccion: 'asc' });
  });

  it('la flecha marca la columna que el servidor ordeno, no la que se pidio', () => {
    // ESTA ES LA PRUEBA QUE IMPIDE QUE LA TABLA MIENTA. Si se pide una columna que el servidor no
    // admite, este se cae al orden por nombre. Pintando la flecha con lo pedido, quedaria una
    // flecha sobre «Creación» encima de filas ordenadas por nombre.
    montar();
    raiz().querySelector<HTMLButtonElement>('[data-ordenar="creacion"]')!.click();
    http.expectOne(p => p.url.includes('/admin/organizaciones'))
      .flush({ ...RESPUESTA, orden: 'nombre', direccion: 'asc' });
    fixture.detectChanges();

    expect(componente.ordenPedido()).toBe('creacion');
    expect(componente.ordenAplicado()).toBe('nombre');
    // La flecha la dibuja la pieza compartida de tablas desde, y este
    // panel la sincroniza con lo que el servidor DIJO que ordenó, no con lo que se le pidió.
    expect(componente.orden.flechaDe('creacion')).toBe('↕');
    expect(componente.orden.flechaDe('nombre')).toBe('↑');

    const cabecera = (id: string) => raiz().querySelector(`[data-ordenar="${id}"]`)!.closest('th')!;
    expect(cabecera('nombre').getAttribute('aria-sort')).toBe('ascending');
    expect(cabecera('creacion').getAttribute('aria-sort')).toBe('none');
  });

  it('cada cabecera ORDENABLE es un boton, y la de acciones no lo es', () => {
    // Un `th` con un `click` encima deja la tabla ordenable solo con raton, asi que las seis
    // columnas que ordenan llevan boton.
    //
    // ESTA PRUEBA PEDIA BOTON EN LAS SIETE, incluida «Acciones», y eso era justo el defecto: la
    // cabecera se dibujaba como control, con flecha y el titulo «Ordenar por Acciones», y al
    // pulsarla no pasaba nada, porque «acciones» no esta entre los ordenes que el servidor acepta.
    // Una cabecera que ofrece ordenar y no ordena promete algo que detras no existe.
    montar();
    const cabeceras = Array.from(raiz().querySelectorAll('thead th'));

    // SIETE DESDE EL 12 DE SEPTIEMBRE DE 2026: se añadió la columna de acciones, que es donde se
    // cambia el estado de una organización. Antes había que adivinar que la fila se abre al pulsarla
    // y encontrar el botón dentro de la ficha.
    expect(cabeceras.length).toBe(7);
    expect(cabeceras.every(th => th.getAttribute('scope') === 'col')).toBeTrue();

    const ordenables = cabeceras.slice(0, 6);
    expect(ordenables.every(th => th.querySelector('button[type="button"]') !== null)).toBeTrue();

    const acciones = cabeceras[6];
    expect(acciones.textContent!.trim()).toBe('Acciones');
    expect(acciones.querySelector('button')).toBeNull();
  });

  /**
   * Abre un filtro y elige una opción, por el DOM.
   *
   * LOS FILTROS DEJARON DE SER `<select>`: el panel de un desplegable
   * nativo lo dibuja el sistema operativo y con los 31 departamentos ocupaba media pantalla. Lo
   * que estas pruebas vigilan —qué viaja al servidor— no cambia; cambia por dónde se pulsa.
   */
  function elegirEnElFiltro(cual: string, opcion: string): void {
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    const boton = Array.from(raiz().querySelectorAll<HTMLButtonElement>('.filtro__opcion'))
      .find(b => (b.textContent || '').replace(/\s+/g, ' ').trim().startsWith(opcion));
    if (!boton) { throw new Error(`No hay opción «${opcion}» en el filtro «${cual}».`); }
    boton.click();
    fixture.detectChanges();
  }

  function opcionesDelFiltro(cual: string): string[] {
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    const textos = Array.from(raiz().querySelectorAll<HTMLButtonElement>('.filtro__opcion'))
      .map(b => (b.textContent || '').replace(/\s+/g, ' ').trim());
    raiz().querySelector<HTMLButtonElement>(`[data-filtro="${cual}"]`)!.click();
    fixture.detectChanges();
    return textos;
  }

  it('el filtro de territorio viaja al servidor con el codigo del departamento', () => {
    montar();
    // El filtro solo trae los departamentos que tienen alguna, y dice cuantas antes de elegir.
    // El recuento va aparte del rótulo, no entre paréntesis, así que el texto plano sale pegado.
    expect(opcionesDelFiltro('territorio')).toEqual([
      'Todo el país3',
      'Antioquia1',
      'Sin territorio2',
    ]);

    elegirEnElFiltro('territorio', 'Antioquia');

    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('departamento=05');
    peticion.flush({ ...RESPUESTA, total: 1, items: [DEDUCIDA] });
    fixture.detectChanges();

    expect(componente.departamentoActivo()).toBe('05');
  });

  it('«Todo el país» no viaja como filtro: es la ausencia de filtro', () => {
    // ESTA PRUEBA NACIO SIN MEDIR NADA y lo dijo un mutante: comprobaba que la carga inicial no
    // lleva «departamento», pero para entonces no queda ninguna peticion en vuelo y pasaba aunque
    // el servicio mandara «departamento=todos». Ahora se vuelve DESDE un departamento elegido, que
    // es el unico momento en que el parametro podria colarse.
    montar();

    elegirEnElFiltro('territorio', 'Antioquia');
    http.expectOne(p => p.url.includes('departamento=05')).flush(RESPUESTA);
    fixture.detectChanges();

    elegirEnElFiltro('territorio', 'Todo el país');
    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).not.toContain('departamento');
    peticion.flush(RESPUESTA);
  });

  it('el departamento elegido sigue en el desplegable aunque se quede sin filas', () => {
    // Si el filtro de estado o la busqueda dejan ese departamento en cero, el servidor deja de
    // listarlo. Sin esta red, el control se quedaria en blanco mientras la tabla sigue filtrada
    // por el, y no habria manera de saber que hay puesto.
    montar();
    elegirEnElFiltro('territorio', 'Antioquia');

    http.expectOne(p => p.url.includes('/admin/organizaciones')).flush({
      ...RESPUESTA,
      total: 0,
      items: [],
      territorios: [{ codigo: 'sin_territorio', etiqueta: 'Sin territorio', total: 2 }],
    });
    fixture.detectChanges();

    expect(componente.opcionesDeTerritorio().map(o => o.codigo)).toContain('05');
    expect(componente.opcionesDeTerritorio().find(o => o.codigo === '05')!.total).toBe(0);
  });

  it('la fecha de creacion se ve con su hora', () => {
    // Las siete organizaciones de la base local se sembraron el mismo dia: sin la hora, la columna
    // diria siempre lo mismo y el orden por fecha pareceria no hacer nada.
    //
    // EN UNA SOLA LINEA Y EN EL FORMATO UNICO DEL PANEL. Estaban partidas en dos parrafos con dos
    // formatos distintos —«25/08/2026» arriba y «22:29» debajo—, y ese «dd/MM/yyyy» era uno de los
    // diez formatos de fecha que convivian en el Espacio de Gestion Administrativa. Ver
    // `FECHA_Y_HORA_ADMINISTRATIVA` en `features/admin/domain/formatos-de-fecha.ts`.
    montar();
    const celda = fila('117').querySelector('[data-fecha-creacion]')!;

    expect(celda.textContent).toContain('25 ago 2026');
    expect(celda.textContent).toContain('22:29');
  });

  // ================================================================================
  // PAGINACION
  // ================================================================================

  /** Una respuesta de tres páginas con la primera puesta. */
  const TRES_PAGINAS = { ...RESPUESTA, total: 60, pagina: 1, tamanoPagina: 25 };

  it('el pie dice siempre qué rango se está viendo, también cuando cabe todo', () => {
    // UN PIE QUE SOLO APARECE CUANDO HAY VARIAS PAGINAS obliga a deducir del silencio que se está
    // viendo el total, y ese silencio se lee igual que un fallo de carga.
    montar();

    const rango = raiz().querySelector('[data-rango]')!.textContent!.replace(/\s+/g, ' ').trim();
    expect(rango).toBe('1–3 de 3 organizaciones');
    expect(componente.totalDePaginas()).toBe(1);
    // `data-testid` Y NO `data-pagina-siguiente`: los botones de paginación son ahora `app-boton`,
    // que pone el identificador de prueba en el botón real (su anfitrión es `display: contents`).
    // Lo que se vigila —que no haya paginación con una sola página, que «Anterior» esté apagado
    // en la primera y que «Siguiente» pida la página 2— no cambia.
    expect(raiz().querySelector('[data-testid="pagina-siguiente"]')).toBeNull();
  });

  it('sin ninguna organización el pie lo dice, y no muestra un rango vacío', () => {
    montar({ ...RESPUESTA, total: 0, items: [] });

    expect(raiz().querySelector('[data-rango]')!.textContent).toContain('Ninguna organización');
    expect(componente.desde()).toBe(0);
    expect(componente.hasta()).toBe(0);
  });

  it('con varias páginas, «Siguiente» pide la siguiente y «Anterior» empieza apagado', () => {
    montar(TRES_PAGINAS);

    expect(raiz().querySelector('[data-rango]')!.textContent).toContain('página 1 de 3');
    expect(raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-anterior"]')!.disabled).toBeTrue();

    raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-siguiente"]')!.click();
    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('pagina=2');
    peticion.flush({ ...TRES_PAGINAS, pagina: 2 });
    fixture.detectChanges();

    expect(raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-anterior"]')!.disabled).toBeFalse();
  });

  it('en la última página, «Siguiente» está apagado', () => {
    montar({ ...TRES_PAGINAS, pagina: 3 });

    expect(componente.hayPaginaSiguiente()).toBeFalse();
    expect(raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-siguiente"]')!.disabled).toBeTrue();
  });

  it('filtrar por estado vuelve a la primera página', () => {
    // ESTANDO EN LA TERCERA Y FILTRANDO A TRES RESULTADOS, el servidor devuelve una página vacía y
    // la tabla dice «no hay organizaciones para este filtro»: mentira, las hay en otra página.
    montar(TRES_PAGINAS);
    raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-siguiente"]')!.click();
    http.expectOne(p => p.url.includes('pagina=2')).flush({ ...TRES_PAGINAS, pagina: 2 });
    fixture.detectChanges();

    raiz().querySelector<HTMLButtonElement>('[data-filtro-organizacion="pendiente_de_confirmacion"]')!.click();
    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('estado=pendiente_de_confirmacion');
    expect(peticion.request.url).not.toContain('pagina=2');
    peticion.flush({ ...RESPUESTA, items: [DECLARADA] });
  });

  it('ordenar por otra columna vuelve a la primera página', () => {
    // El orden reordena TODO el resultado: la página dos de antes ya no contiene las mismas filas.
    montar(TRES_PAGINAS);
    raiz().querySelector<HTMLButtonElement>('[data-testid="pagina-siguiente"]')!.click();
    http.expectOne(p => p.url.includes('pagina=2')).flush({ ...TRES_PAGINAS, pagina: 2 });
    fixture.detectChanges();

    raiz().querySelector<HTMLButtonElement>('[data-ordenar="creacion"]')!.click();
    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('orden=creacion');
    expect(peticion.request.url).not.toContain('pagina=2');
    peticion.flush({ ...TRES_PAGINAS, orden: 'creacion' });
  });

  it('pedir una página que no existe se queda en la última', () => {
    montar(TRES_PAGINAS);
    componente.irAPagina(99);

    const peticion = http.expectOne(p => p.url.includes('/admin/organizaciones'));
    expect(peticion.request.url).toContain('pagina=3');
    peticion.flush({ ...TRES_PAGINAS, pagina: 3 });
  });

  it('la tabla no atribuye cobertura territorial a la organización', () => {
    // La organización declara una sede. El alcance territorial pertenece a cada proceso y por eso
    // no debe reaparecer en esta tabla como un dato de la entidad.
    montar();

    expect(fila('117').textContent).not.toContain('Cobertura');
    // SE ESPERA «Medellín, Antioquia» Y NO «MEDELLÍN, ANTIOQUIA». La sede llega en mayúsculas
    // sostenidas porque así la publica el DANE en `dbo.Divipola`, y desde el 14 de septiembre de
    // 2026 la interfaz la escribe como se escribe. Lo que esta prueba vigila —que la tabla enseñe
    // la SEDE y no una cobertura territorial atribuida a la organización— no cambia.
    expect(fila('101').textContent).toContain('Medellín, Antioquia');
  });

  it('desde la tabla se puede cambiar el estado de una organización', () => {
    // POR QUE EXISTE: «aparece en la última columna pero no sé cómo cambiarles los estados».
    // Para hacerlo había que adivinar que la fila se abre al pulsarla y encontrar el botón dentro de
    // la ficha. Una acción que hay que descubrir por ensayo y error no existe.
    montar();

    const fila117 = fila('117');
    fila117.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();

    const opciones = Array.from(fila117.querySelectorAll('[data-opcion-accion]'))
      .map(o => o.textContent!.trim());
    // «ACTIVAR» NO APARECE AQUI Y ESO ES CORRECTO: esta organización tiene el correo sin confirmar,
    // el servidor rechazaría activarla, y lo que sí resuelve su caso es confirmarlo. Un menú que
    // ofrece lo que no se puede hacer es la promesa vacía que el plan del 12 de septiembre cerró.
    expect(opciones).toEqual(['Abrir ficha', 'Historial', 'Confirmar el correo', 'Desactivar', 'Eliminar del ecosistema']);
  });

  it('abre la organización que pidió otra pantalla, sin que nadie la busque', () => {
    montar();
    // LA RELACION SE RECORRE ENTERA. Desde la ficha de un Festival, «Ver organización» abría esta
    // sección y ahí se acababa: quien venía de un Festival tenía que buscar a mano, entre todas, la
    // organización cuyo nombre acababa de leer.
    fixture.componentRef.setInput('organizacionPedida', '101');
    fixture.detectChanges();

    http.expectOne(p => p.url === '/api/v1/admin/organizaciones/101').flush({
      organizacion: DEDUCIDA, festivales: [], solicitudes: [], reclamaciones: [],
    });
    http.expectOne(p => p.url.includes('/mensajes')).flush({ items: [], total: 0 });
    fixture.detectChanges();

    expect(componente.fichaAbiertaId()).toBe('101');
  });

  it('los procesos de la ficha se abren, no solo se leen', () => {
    montar();
    raiz().querySelector<HTMLButtonElement>('[data-abrir-ficha-organizacion]')!.click();
    fixture.detectChanges();
    http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117').flush({
      organizacion: DECLARADA,
      festivales: [{ id: '9', nombre: 'Festival de la Fundación', estado: 'publicado', dejaHuerfano: true }],
      solicitudes: [], reclamaciones: [],
    });
    http.expectOne(p => p.url.includes('/mensajes')).flush({ items: [], total: 0 });
    fixture.detectChanges();

    const pedidos: string[] = [];
    componente.verFestival.subscribe(id => pedidos.push(id));
    raiz().querySelector<HTMLButtonElement>('[data-abrir-proceso]')!.click();

    // ESTABA COMO TEXTO PLANO: se podía leer el nombre del Festival y no abrirlo. Una relación que
    // existe en la base y no se puede recorrer en pantalla es una relación que nadie administra.
    expect(pedidos).toEqual(['9']);
  });

  it('con el correo ya confirmado la acción que se ofrece es «Activar», no confirmarlo otra vez', () => {
    montar();

    const fila101 = fila('101');
    fila101.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();

    const opciones = Array.from(fila101.querySelectorAll('[data-opcion-accion]'))
      .map(o => o.textContent!.trim());
    // ESTA YA ESTA ACTIVA Y CON EL CORREO COMPROBADO: ni «Activar» -sería un acto sin efecto- ni
    // «Confirmar el correo» -ya lo está- tienen nada que hacer aquí.
    expect(opciones).toEqual(['Abrir ficha', 'Historial', 'Desactivar', 'Eliminar del ecosistema']);
  });

  it('la entidad institucional solo se puede abrir, no cerrar', () => {
    montar();

    const fila116 = fila('116');
    fila116.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();

    // RESPONDE POR TODO REGISTRO QUE NADIE HAYA RECLAMADO: desactivarla o eliminarla dejaría
    // huérfano medio ecosistema. Ofrecer el acto y rechazarlo después es peor que no ofrecerlo.
    // «HISTORIAL» SI, porque es una lectura: dice quién la tocó y cuándo, y no cierra nada.
    expect(Array.from(fila116.querySelectorAll('[data-opcion-accion]')).map(o => o.textContent!.trim()))
      .toEqual(['Abrir ficha', 'Historial']);
  });

  it('«Historial» abre el mismo panel que las demás pantallas, sin abrir la ficha', () => {
    // ERA LA UNICA DE LAS CUATRO QUE SE LO PINTABA APARTE. Noticias, Agenda y Catálogo abren
    // `app-historial-de-registro`; aquí la ficha traía una lista propia que enseñaba el verbo crudo
    // de la bitácora —«actualizar» en vez de «Actualizó»— y no decía de dónde venía la organización.
    montar();

    const fila117 = fila('117');
    fila117.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();
    Array.from(fila117.querySelectorAll<HTMLButtonElement>('[data-opcion-accion]'))
      .find(o => o.textContent!.trim() === 'Historial')!.click();
    fixture.detectChanges();

    const peticion = http.expectOne(p => p.url.includes('/admin/auditoria'));
    expect(peticion.request.urlWithParams).toContain('tabla=Entidades');
    expect(peticion.request.urlWithParams).toContain('registroId=117');
    peticion.flush({ items: [] });
    fixture.detectChanges();

    // CONSULTAR QUIEN LA TOCO NO ES ABRIR SU EXPEDIENTE: al cerrar el panel hay que seguir en la
    // tabla, no en una ficha que nadie pidió.
    expect(componente.fichaAbiertaId()).toBeNull();
    expect(raiz().textContent).toContain('Historial y procedencia');
  });

  it('elegir una acción de la tabla abre la ficha con el diálogo ya puesto', () => {
    montar();
    const fila117 = fila('117');
    fila117.querySelector<HTMLButtonElement>('[data-disparador-acciones]')!.click();
    fixture.detectChanges();
    Array.from(fila117.querySelectorAll<HTMLButtonElement>('[data-opcion-accion]'))
      .find(o => o.textContent!.includes('Desactivar'))!.click();
    fixture.detectChanges();

    http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117').flush({
      organizacion: DECLARADA,
      festivales: [],
      solicitudes: [],
      reclamaciones: [],
    });
    http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117/mensajes').flush({ items: [], total: 0 });
    fixture.detectChanges();

    // NO ACTUA A CIEGAS: desactivar exige motivo y eliminar puede tener que resolver qué pasa con
    // los procesos. Se abre el diálogo con la acción elegida, no se ejecuta desde el menú.
    const guardar = raiz().querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(guardar.textContent!.trim()).toBe('Desactivar');
  });

  describe('la ficha de una organización', () => {
    // LOS CINCO GRUPOS VACIOS QUE ESTABAN AQUI —escuelas, mercados, redes, lutieres, escenarios—
    // eran del modelo genérico de septiembre y ya no viajan en el contrato: `V20260911_04` retiró
    // esas cinco tablas y Festival es el único proceso habilitado.
    const FICHA = {
      organizacion: DECLARADA,
      festivales: [{ id: '105', nombre: 'Festival Prueba', estado: 'publicado', dejaHuerfano: true }],
      solicitudes: [{ id: '9', recordId: '105', recordName: 'Festival Prueba', status: 'pendiente' }],
      reclamaciones: [{ id: '3', registroCanonicoId: '105', registroNombre: 'Festival Prueba', estado: 'enviada' }],
    };

    /**
     * Responde la consulta de mensajes que acompaña a la ficha.
     *
     * ABRIR LA FICHA PIDE DOS COSAS: sus datos y lo que la consola le ha escrito. Son dos peticiones
     * porque son dos preguntas —quién es y qué le hemos dicho—, y la segunda no debe impedir que la
     * primera se pinte si falla.
     */
    const responderMensajes = () =>
      http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117/mensajes')
        .flush({ items: [], total: 0 });

    it('al hacer clic en una organización se pide y se muestra su ficha', () => {
      montar();

      raiz().querySelector<HTMLButtonElement>('[data-abrir-ficha-organizacion]')!.click();
      fixture.detectChanges();

      const peticion = http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117');
      peticion.flush(FICHA);
      responderMensajes();
      fixture.detectChanges();

      const panel = raiz().querySelector('[data-ficha-organizacion]')!;
      // MUTANTE QUE MATA: mostrar solo el CONTEO de procesos -que ya está en la fila de la tabla-
      // en vez del nombre de cada uno. «Consultar procesos administrados» pide el nombre.
      expect(panel.textContent).toContain('Festival Prueba');
      expect(panel.textContent).toContain('Camila Prueba Responsable');
    });

    it('volver a hacer clic en la misma organización cierra la ficha en vez de pedirla otra vez', () => {
      montar();
      const boton = raiz().querySelector<HTMLButtonElement>('[data-abrir-ficha-organizacion]')!;

      boton.click();
      fixture.detectChanges();
      http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117').flush(FICHA);
      responderMensajes();
      fixture.detectChanges();
      expect(raiz().querySelector('[data-ficha-organizacion]')).not.toBeNull();

      boton.click();
      fixture.detectChanges();

      expect(raiz().querySelector('[data-ficha-organizacion]')).toBeNull();
    });

    it('si el servidor falla al abrir la ficha, se muestra el error y no una ficha vacía', () => {
      montar();

      raiz().querySelector<HTMLButtonElement>('[data-abrir-ficha-organizacion]')!.click();
      fixture.detectChanges();
      http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117')
        .flush({ message: 'No fue posible abrir la ficha de la organización' }, { status: 500, statusText: 'Error' });
      responderMensajes();
      fixture.detectChanges();

      expect(raiz().querySelector('[data-error-ficha]')).not.toBeNull();
    });

    it('cada proceso administrado dice en qué estado está', () => {
      abrirFicha();

      // «FESTIVAL PRUEBA» A SECAS NO DICE SI ESTÁ PUBLICADO. Quien va a archivar la organización
      // necesita esa mitad, y la ficha enseñaba solo una lista de nombres.
      const proceso = raiz().querySelector('[data-proceso-administrado]')!;
      expect(proceso.textContent).toContain('Festival Prueba');
      expect(proceso.textContent).toContain('Publicado');
    });

    it('cerrar la organización con procesos publicados los nombra y ofrece las dos salidas', async () => {
      abrirFicha();
      await intentarCierre();

      // EL 409 NO ES UN ERROR DE QUIEN ADMINISTRA: es el sistema diciendo que falta una decisión.
      // Si la pantalla solo pintara «no fue posible», habría que ir a buscar los procesos a mano.
      const aviso = raiz().querySelector('[data-procesos-bloquean]')!;
      expect(aviso).not.toBeNull();
      expect(aviso.textContent).toContain('Festival Prueba');
      // Y SON DOS, no una. Con solo «liberar», cerrar una fundación disuelta dejaba su festival
      // publicado a nombre del Programa esperando a que alguien lo reclamara.
      expect(raiz().querySelector('[data-testid="liberar-procesos"]')).not.toBeNull();
      expect(raiz().querySelector('[data-testid="archivar-procesos"]')).not.toBeNull();
    });

    it('liberar sus procesos reenvía el cierre con la decisión tomada', async () => {
      abrirFicha();
      await intentarCierre();

      raiz().querySelector<HTMLButtonElement>('[data-testid="liberar-procesos"]')!.click();
      fixture.detectChanges();

      const segunda = http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117/estado');
      expect(segunda.request.body.queHacerConLosProcesos).toBe('liberar');
      expect(segunda.request.body.motivo).toBe('La fundación se disolvió.');
      segunda.flush({ estado: 'archivado', activa: false, queSeHizoConLosProcesos: 'liberar', procesosAfectados: 1, avisados: 0 });
      // `flush` ENTREGA LA RESPUESTA, PERO LA PROMESA SE RESUELVE EN LA SIGUIENTE MICROTAREA.
      // `flush` ENTREGA LA RESPUESTA, PERO LA PROMESA SE RESUELVE EN LA SIGUIENTE MICROTAREA.
      // No se usa `whenStable`: con dos peticiones en vuelo nunca llega a estable.
      await Promise.resolve();
      await Promise.resolve();
      // GUARDAR VUELVE A PEDIR LA FICHA Y LA LISTA: el estado sale en las dos y dejar una con el
      // valor anterior haría dudar de si se guardó. Se atienden aquí para que `http.verify()` no
      // las encuentre abiertas.
      http.match(p => p.url.includes('/admin/organizaciones')).forEach(p => p.flush({}));

      // EL AVISO DICE A DONDE FUERON A PARAR: siguen publicados, esperando a quien los reclame.
      // Se mira la señal y no el DOM porque, al guardar, el panel vuelve a pedir la ficha y la
      // lista, y lo que aquí importa es la frase, no cuándo termina de repintarse el cajón.
      expect(componente.avisoDeAsistencia()).toContain('sigue publicado');
    });

    it('archivar sus procesos reenvía el cierre con la otra decisión', async () => {
      abrirFicha();
      await intentarCierre();

      raiz().querySelector<HTMLButtonElement>('[data-testid="archivar-procesos"]')!.click();
      fixture.detectChanges();

      const segunda = http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117/estado');
      expect(segunda.request.body.queHacerConLosProcesos).toBe('archivar');
      segunda.flush({ estado: 'archivado', activa: false, queSeHizoConLosProcesos: 'archivar', procesosAfectados: 1, avisados: 0 });
      // `flush` ENTREGA LA RESPUESTA, PERO LA PROMESA SE RESUELVE EN LA SIGUIENTE MICROTAREA.
      // No se usa `whenStable`: con dos peticiones en vuelo nunca llega a estable.
      await Promise.resolve();
      await Promise.resolve();
      // GUARDAR VUELVE A PEDIR LA FICHA Y LA LISTA: el estado sale en las dos y dejar una con el
      // valor anterior haría dudar de si se guardó. Se atienden aquí para que `http.verify()` no
      // las encuentre abiertas.
      http.match(p => p.url.includes('/admin/organizaciones')).forEach(p => p.flush({}));

      // NO DICE LO MISMO QUE LIBERAR, porque no pasó lo mismo.
      expect(componente.avisoDeAsistencia()).toContain('salió del sitio público');
    });

    /** Pide archivar la organización y deja que el servidor responda el 409 con sus procesos. */
    async function intentarCierre(): Promise<void> {
      componente.administrarEstado();
      componente.campoDelEstado('estado', 'archivado');
      componente.campoDelEstado('motivo', 'La fundación se disolvió.');
      fixture.detectChanges();

      const enCurso = componente.guardarEstado();
      http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117/estado').flush(
        {
          procesos: [{ id: '105', nombre: 'Festival Prueba', estado: 'publicado' }],
          mensaje: 'Esta organización todavía administra un proceso que quedaría sin nadie que responda por él.',
        },
        { status: 409, statusText: 'Conflict' });
      await enCurso;
      fixture.detectChanges();
    }

    it('desactivar sin decir por qué deja el botón apagado, y el botón se llama como el acto', () => {
      abrirFicha();
      componente.administrarEstado();
      componente.campoDelEstado('estado', 'inactiva');
      fixture.detectChanges();

      // SACARLA DE LA OPERACION SIN DECIR POR QUE deja a quien lo mire dentro de seis meses sin
      // ninguna forma de saberlo, y ya no hay nadie dentro de la organización a quien preguntar.
      const guardar = raiz().querySelector<HTMLButtonElement>('button[type="submit"]')!;
      expect(guardar.disabled).toBeTrue();

      // Y EL BOTON SE LLAMA COMO EL ACTO. «Guardar» no dice qué va a pasar; «Desactivar», sí.
      expect(guardar.textContent!.trim()).toBe('Desactivar');

      componente.campoDelEstado('motivo', 'La fundación se disolvió.');
      fixture.detectChanges();
      expect(guardar.disabled).toBeFalse();
    });

    it('el ciclo de vida no ofrece el estado en el que ya está', () => {
      abrirFicha();
      componente.administrarEstado();
      fixture.detectChanges();

      // LA 117 ESTA «PENDIENTE DE CONFIRMACION» Y SIN CORREO COMPROBADO en el fijador, así que
      // «Activar» no se ofrece: ahí significaría «sáltate la confirmación», y lo que resuelve su
      // caso es «Confirmar el correo», que está fuera de este desplegable.
      const opciones = Array.from(raiz().querySelectorAll<HTMLOptionElement>('#estado-organizacion option'))
        .map(o => o.textContent!.trim());
      expect(opciones).toEqual(['Desactivar', 'Eliminar del ecosistema']);

      // SOBRE UNA CERRADA, «ACTIVAR» SI SE OFRECE aunque el correo siga sin comprobar: ahí significa
      // «devuélvele el acceso», y el servidor la deja en el estado que le corresponde en vez de
      // mentir con «activa». Sin esta rama, deshacer un cierre quedaría sin camino.
      componente.ficha.set({ ...componente.ficha()!, organizacion: { ...componente.ficha()!.organizacion, estado: 'inactiva' } });
      componente.administrarEstado();
      fixture.detectChanges();
      expect(Array.from(raiz().querySelectorAll<HTMLOptionElement>('#estado-organizacion option'))
        .map(o => o.textContent!.trim())).toEqual(['Activar', 'Eliminar del ecosistema']);

      // Y OFRECER «ACTIVAR» A UNA YA ACTIVA ES OFRECER UN ACTO SIN EFECTO: quien lo pulse no sabrá
      // si funcionó.
      componente.ficha.set({ ...componente.ficha()!, organizacion: { ...componente.ficha()!.organizacion, estado: 'activa' } });
      componente.administrarEstado();
      fixture.detectChanges();
      const tras = Array.from(raiz().querySelectorAll<HTMLOptionElement>('#estado-organizacion option'))
        .map(o => o.textContent!.trim());
      expect(tras).toEqual(['Desactivar', 'Eliminar del ecosistema']);
    });

    /** Abre la ficha de la 117 y responde sus dos peticiones. */
    function abrirFicha(): void {
      montar();
      raiz().querySelector<HTMLButtonElement>('[data-abrir-ficha-organizacion]')!.click();
      fixture.detectChanges();
      http.expectOne(p => p.url === '/api/v1/admin/organizaciones/117').flush(FICHA);
      responderMensajes();
      fixture.detectChanges();
    }
  });
});
