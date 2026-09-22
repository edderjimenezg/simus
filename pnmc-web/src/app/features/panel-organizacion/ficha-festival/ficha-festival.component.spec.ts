import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { PracticasMusicalesService } from '../../../core/services/practicas-musicales.service';
import { TerritoriosSonorosService } from '../../../core/services/territorios-sonoros.service';
import { FichaFestivalComponent } from './ficha-festival.component';
import { PASOS_DE_LA_FICHA, SECCIONES_DE_LA_FICHA, pasoDeLaSeccion } from './secciones-de-la-ficha';
import {
  CatalogosDelFestival,
  FestivalDeLaOrganizacion,
  GuardarFestivalSolicitud,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  PropuestaCambioFestival,
  ResponsableOrganizacion,
  TipoDocumento,
  UbicacionDivipola,
} from '../panel-organizacion.api';

/*
  QUÉ SE INSTRUMENTA AQUÍ.

  Este diálogo es, desde, el ÚNICO formulario del Festival: el alta y la
  ficha completa, unificadas. Lo pidió el usuario ese día: «esa ficha completa la vamos a unificar
  cuando le demos crear festival… la idea es que ese formulario lo seccionemos, por secciones con
  toda la información… solo son obligatorios campos generales para que el festival exista».

  Y desde LEERLA Y EDITARLA SON LA MISMA PANTALLA, también a petición del
  usuario: «quiero que mantengamos la misma vista para editar la edición del festival y para la
  ficha; si le doy editar, me habilita los campos del festival… cuando se crea un festival también
  mantengamos esta estructura, así homogenizamos». Sus doce secciones van repartidas en DOS BLOQUES
  —dos pestañas— por un encargo del mismo día: «en todo el formulario debemos identificar la
  diferencia entre la información de la edición o ediciones, y distinguir eso… primero se pregunte
  toda la información básica en los primeros módulos, y después, si se desea, se creen botones para
  añadir una edición de festival».

  LAS CINCO COSAS QUE NO PUEDEN FALLAR:

  1) LOS RÓTULOS SON LOS DEL VOLCADO DE SIMUS. Es la corrección pedida con nombre y
     apellido: «a los nombres de los campos no los cambies como vi que hiciste, en por ejemplo
     "fuente de financiamiento"; déjalo tal cual». Un rótulo traducido a gusto —«Fuente principal»
     por ID_FUENTE_FINANCIACION— no falla en ninguna prueba de comportamiento y hace que la pantalla
     y la base hablen de cosas distintas. La única excepción es «edición» en lugar de «versión», y
     también la pidió él.

  2) CADA CAMPO SE VE DE QUÉ TABLA ES. Hay dos «Correo de contacto», dos «Página web» y dos listas
     de prácticas musicales: una pareja en `dbo.Festivales` y la otra en `dbo.VersionesFestival`. Un
     formulario que no lo diga hace que se escriba en la equivocada, y eso no lo detecta ninguna
     prueba de guardado, porque las dos peticiones salen bien.

  3) SOLO LOS DATOS GENERALES SON OBLIGATORIOS. Guardar tiene que funcionar desde el primer paso,
     sin recorrer los doce.

  4) EL ALTA SON DOS PETICIONES Y EN ORDEN: primero la cabecera, después la edición. Si la segunda
     falla, la primera ya ocurrió, y el mensaje tiene que decirlo o quien guarda vuelve a pulsar y
     crea un segundo Festival.

  5) QUE LO QUE SE TECLEA SEA LO QUE VIAJA, Y A DONDE VA. Las cinco listas y las tres tablas se
     pierden con un descuido invisible: un `id` que sale como cadena vacía en vez de NULL, una lista
     copiada por referencia, o una lista leída del modelo equivocado. En pantalla todo se ve igual.

  SE ENTRA POR LA PANTALLA, no llamando a los métodos del componente.
*/

const FESTIVAL: FestivalDeLaOrganizacion = {
  id: '91',
  nombre: 'Festival de Música del Pacífico',
  descripcion: 'Cuatro días de bandas y chirimías.',
  estado: 'Borrador',
  correoContacto: 'festival@fmv.org',
  telefonoCelular: '3001234567',
  instagram: '@festival',
  facebook: 'fb/festival',
  paginaWeb: 'https://festival.org',
  otroEnlace: null,
  observacionesContacto: 'Llamar en la mañana.',
  periodicidad: 'Anual',
  nivelCobertura: 'municipal',
  codigoDepartamento: '05',
  codigoMunicipio: '05001',
  practicasMusicales: [{ id: 3, nombre: 'Banda' }],
  territoriosSonoros: [{ id: 2, nombre: 'Andes' }],
};

/** La propuesta de cambios activa de un Festival Publicado, tal como la devuelve el servidor. */
function propuesta(cambios: Partial<PropuestaCambioFestival> = {}): PropuestaCambioFestival {
  return {
    id: '801',
    festivalOrigenId: '91',
    versionOrigenId: '501',
    estado: 'Borrador',
    nombre: FESTIVAL.nombre,
    descripcion: FESTIVAL.descripcion,
    nivelCobertura: FESTIVAL.nivelCobertura!,
    codigoDepartamento: FESTIVAL.codigoDepartamento,
    codigoMunicipio: FESTIVAL.codigoMunicipio,
    periodicidad: FESTIVAL.periodicidad,
    correoContacto: FESTIVAL.correoContacto,
    practicasMusicales: FESTIVAL.practicasMusicales ?? [],
    territoriosSonoros: FESTIVAL.territoriosSonoros ?? [],
    instagram: FESTIVAL.instagram,
    facebook: FESTIVAL.facebook,
    paginaWeb: FESTIVAL.paginaWeb,
    otroEnlace: FESTIVAL.otroEnlace,
    telefonoCelular: FESTIVAL.telefonoCelular,
    observacionesContacto: FESTIVAL.observacionesContacto,
    ...cambios,
  };
}

/**
 * Los doce catálogos de `GET /api/v1/externo/catalogos/festival`.
 *
 * Dos opciones por catálogo y no una: con una sola, «se eligió la correcta» y «se eligió la única»
 * son la misma afirmación.
 */
const CATALOGOS: CatalogosDelFestival = {
  practicasMusicales: [{ id: 3, nombre: 'Banda' }, { id: 7, nombre: 'Coro' }],
  territoriosSonoros: [{ id: 2, nombre: 'Andes' }, { id: 5, nombre: 'Pacífico' }],
  tipologias: [{ id: 11, nombre: 'Festival de música' }, { id: 12, nombre: 'Mercado' }],
  expresionesArtisticas: [{ id: 21, nombre: 'Danza' }, { id: 22, nombre: 'Teatro' }],
  fuentesFinanciacion: [{ id: 31, nombre: 'Recursos propios' }, { id: 32, nombre: 'Estampilla' }],
  modalidadesParticipacion: [{ id: 41, nombre: 'Concurso' }, { id: 42, nombre: 'Muestra' }],
  naturalezasEntidad: [{ id: 51, nombre: 'Pública' }, { id: 52, nombre: 'Privada' }],
  tiposIngreso: [{ id: 61, nombre: 'Gratuito' }, { id: 62, nombre: 'Con boleta' }],
  tiposOrganizador: [],
  zonasUrbanoRural: [{ id: 81, nombre: 'Urbana' }, { id: 82, nombre: 'Rural' }],
  titulacionesColectivas: [{ id: 91, nombre: 'Ninguna' }, { id: 92, nombre: 'Consejo comunitario' }],
  regionesOcad: [{ id: 101, nombre: 'Pacífico' }],
};

/** Dos departamentos con dos municipios cada uno: menos no permite ver que el municipio se limpia. */
const DIVIPOLA: UbicacionDivipola[] = [
  { departmentCode: '05', departmentName: 'ANTIOQUIA', municipalityCode: '05001', municipalityName: 'MEDELLÍN' },
  { departmentCode: '05', departmentName: 'ANTIOQUIA', municipalityCode: '05088', municipalityName: 'BELLO' },
  { departmentCode: '91', departmentName: 'AMAZONAS', municipalityCode: '91001', municipalityName: 'LETICIA' },
  { departmentCode: '91', departmentName: 'AMAZONAS', municipalityCode: '91263', municipalityName: 'EL ENCANTO' },
];

// AQUI ESTABAN `RESUMEN` Y `ficha()`, los dobles de una versión de `dbo.VersionesFestival`. La
// pestaña que los pintaba se retiró y con ella 49 de las 91 pruebas
// de este fichero: las tarjetas de edición, los ocho pasos de la edición, el alta encadenada, lo
// que viajaba en la segunda petición y la navegación entre pestañas. Lo que queda cubre lo que la
// ficha hace hoy: el Festival, su propuesta de cambios y su formulario.

class ApiFalso extends PanelOrganizacionApi {
  festivalesCreados: { organizacionId: string; solicitud: GuardarFestivalSolicitud }[] = [];
  festivalesGuardados: { festivalId: string; solicitud: GuardarFestivalSolicitud }[] = [];
  /** El orden en que salieron las peticiones. El alta depende de que la cabecera vaya primero. */
  orden: string[] = [];

  falloAlGuardarFestival: unknown = null;
  catalogosFallan = false;

  /** La propuesta activa que devuelve el servidor. Cambia entre pruebas con `propuesta({...})`. */
  propuestaQueDevuelve: PropuestaCambioFestival = propuesta();
  vecesQueIniciaPropuesta = 0;
  propuestasGuardadas: { festivalId: string; solicitud: GuardarFestivalSolicitud }[] = [];

  override obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> {
    if (this.catalogosFallan) return throwError(() => ({ message: 'sin catálogos' }));
    return of(CATALOGOS);
  }

  override obtenerUbicaciones(): Observable<UbicacionDivipola[]> {
    return of(DIVIPOLA);
  }



  override crearFestival(organizacionId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    this.orden.push('crearFestival');
    this.festivalesCreados.push({ organizacionId, solicitud });
    if (this.falloAlGuardarFestival) return throwError(() => this.falloAlGuardarFestival);
    return of({ ...FESTIVAL, id: '95', nombre: solicitud.nombre });
  }

  override guardarFestival(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    this.orden.push('guardarFestival');
    this.festivalesGuardados.push({ festivalId, solicitud });
    if (this.falloAlGuardarFestival) return throwError(() => this.falloAlGuardarFestival);
    return of({ ...FESTIVAL, nombre: solicitud.nombre });
  }



  override obtenerPerfil(): Observable<PerfilOrganizacion> {
    throw new Error('El perfil de la organización no es de esta pantalla.');
  }

  override guardarPerfil(): Observable<PerfilOrganizacion> {
    throw new Error('El perfil de la organización no es de esta pantalla.');
  }

  override obtenerResponsable(): Observable<ResponsableOrganizacion> {
    throw new Error('La persona responsable no es de esta pantalla.');
  }

  override guardarResponsable(): Observable<ResponsableOrganizacion> {
    throw new Error('La persona responsable no es de esta pantalla.');
  }

  override obtenerTiposDocumento(): Observable<TipoDocumento[]> {
    throw new Error('Los tipos de documento no son de esta pantalla.');
  }

  override obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> {
    throw new Error('La lista de Festivales la trae el panel.');
  }

  override enviarFestivalARevision(): Observable<FestivalDeLaOrganizacion> {
    throw new Error('El envío a revisión no es de esta pantalla.');
  }

  override enviarPropuestaARevision(): Observable<unknown> {
    throw new Error('La propuesta de cambios no es de esta pantalla.');
  }

  override iniciarPropuesta(): Observable<PropuestaCambioFestival> {
    this.vecesQueIniciaPropuesta += 1;
    return of(this.propuestaQueDevuelve);
  }

  override obtenerPropuestaActiva(): Observable<PropuestaCambioFestival> {
    return of(this.propuestaQueDevuelve);
  }

  override guardarPropuesta(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<PropuestaCambioFestival> {
    this.orden.push('guardarPropuesta');
    this.propuestasGuardadas.push({ festivalId, solicitud });
    return of({ ...this.propuestaQueDevuelve, nombre: solicitud.nombre });
  }

  override obtenerNotificaciones(): Observable<PaginaDeNotificaciones> {
    throw new Error('Las notificaciones no son de esta pantalla.');
  }

  override marcarNotificacionLeida(): Observable<NotificacionDelPanel> {
    throw new Error('Las notificaciones no son de esta pantalla.');
  }
}

describe('FichaFestivalComponent · la misma vista para leer, editar y crear', () => {
  let fixture: ComponentFixture<FichaFestivalComponent>;
  let componente: FichaFestivalComponent;
  let api: ApiFalso;
  let cierres: number;
  let creados: { id: string; nombre: string }[];

  beforeEach(async () => {
    api = new ApiFalso();
    await TestBed.configureTestingModule({
      imports: [FichaFestivalComponent],
      providers: [
        { provide: PanelOrganizacionApi, useValue: api },
        { provide: PracticasMusicalesService, useValue: { listar: () => of([]) } },
        { provide: TerritoriosSonorosService, useValue: { listar: () => of([]) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FichaFestivalComponent);
    componente = fixture.componentInstance;
    cierres = 0;
    creados = [];
    componente.cerrar.subscribe(() => (cierres += 1));
    componente.creado.subscribe(f => creados.push(f));
  });

  afterEach(() => (fixture.nativeElement as HTMLElement)?.remove?.());

  const raiz = () => fixture.nativeElement as HTMLElement;
  const buscar = <T extends HTMLElement>(id: string) => raiz().querySelector<T>(`[data-testid="${id}"]`);
  const texto = () => raiz().textContent ?? '';

  /** Monta el diálogo sobre un Festival que ya existe. */
  function montar(abrirEnEdicion = false): void {
    fixture.componentRef.setInput('festival', FESTIVAL);
    fixture.componentRef.setInput('organizacionId', '117');
    fixture.componentRef.setInput('abrirEnEdicion', abrirEnEdicion);
    fixture.detectChanges();
  }

  /** Monta el diálogo en modo alta: sin Festival todavía. */
  function montarAlta(): void {
    fixture.componentRef.setInput('festival', null);
    fixture.componentRef.setInput('organizacionId', '117');
    fixture.componentRef.setInput('nombreOrganizacion', 'Fundación Musical del Valle');
    fixture.detectChanges();
  }

  /**
   * Monta el diálogo DENTRO del documento.
   *
   * ES OBLIGATORIO PARA MEDIR EL FOCO: `focus()` sobre un elemento que no está en el documento no
   * hace nada, y `document.activeElement` se queda en `<body>`. Con la fixture suelta, las pruebas
   * del foco pasarían igual con el arreglo y sin él.
   */
  function montarEnElDocumento(): void {
    montar();
    document.body.appendChild(fixture.nativeElement);
  }

  function editar(): void {
    buscar<HTMLButtonElement>('ficha-editar')!.click();
    fixture.detectChanges();
  }

  /**
   * Deja a la vista la sección pedida.
   *
   * DESPLIEGA SU PASO, que es lo único que hace falta desde que la ficha tiene un solo lado. Antes
   * abría además la pestaña donde vivía la sección y desplegaba la edición si no lo estaba; las dos
   * cosas se fueron con la pestaña «Ediciones».
   *
   * SIGUE HACIENDO FALTA EL DESPLIEGUE: desde el formulario enseña un paso
   * a la vez —«sería bien ver los círculos con la info todos y poder diligenciarlos, porque todo es
   * extenso», del usuario—, así que los campos de los demás no están en el DOM.
   */
  function irALaSeccion(id: string): void {
    desplegar(pasoDeLaSeccion(id)!.id);
  }

  /** Despliega un paso si estaba plegado. Desde nacen todos cerrados. */
  function desplegar(pasoId: string): void {
    const mando = buscar<HTMLButtonElement>(`abrir-paso-${pasoId}`)!;
    if (mando.getAttribute('aria-expanded') === 'false') {
      mando.click();
      fixture.detectChanges();
    }
  }

  /** Despliega los tres pasos. Lo necesita todo lo que lee la ficha entera. */
  function desplegarTodo(): void {
    for (const paso of PASOS_DE_LA_FICHA) {
      if (buscar(`abrir-paso-${paso.id}`)) desplegar(paso.id);
    }
  }

  /**
   * Escribe en un campo, desplegando su paso si hacía falta.
   *
   * DESPLIEGA SOLO SI EL CAMPO NO ESTÁ, y ese matiz importa: desde la ficha
   * abre con todos los pasos cerrados —«el estado natural son todos cerrados», del usuario—, así que
   * escribir es un gesto de dos partes. Una prueba que quiera medir el estado plegado lo mide antes
   * de llamar aquí.
   */
  function escribir(nombre: string, valor: string): void {
    if (!raiz().querySelector(`[name="${nombre}"]`)) desplegarTodo();
    const campo = raiz().querySelector<HTMLInputElement>(`[name="${nombre}"]`)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function elegir(selector: string, valor: string): void {
    if (!raiz().querySelector(selector)) desplegarTodo();
    const campo = raiz().querySelector<HTMLSelectElement>(selector)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  /** El rótulo visible de un control, buscado por su `name`. */
  function rotuloDe(nombre: string): string {
    const campo = raiz().querySelector<HTMLElement>(`[name="${nombre}"]`);
    if (!campo) return '';
    const id = campo.getAttribute('id');
    if (id) {
      const etiqueta = raiz().querySelector<HTMLLabelElement>(`label[for="${id}"]`);
      if (etiqueta) return (etiqueta.textContent ?? '').replace('*', '').trim();
    }
    return (campo.closest('label')?.textContent ?? '').trim();
  }

  // ───────────────────────── Los rótulos del volcado ─────────────────────────

  it('los rótulos del formulario son los nombres del volcado de SIMUS, no traducciones libres', () => {
    // EL ENCARGO DEL USUARIO, con su ejemplo: «a los nombres de los campos no los cambies como vi
    // que hiciste, en por ejemplo "fuente de financiamiento"; déjalo tal cual». Cada par es
    // columna del volcado → rótulo que debe leerse.
    const esperados: [string, string, string][] = [
      ['generales', 'nombre', 'Nombre del festival'],                                  // NOMBRE_FESTIVAL
      ['generales', 'descripcionFestival', 'Descripción del festival'],                // DESCRIPCION_FESTIVAL
      ['contacto-festival', 'correoContactoFestival', 'Correo de contacto'],           // CORREO_CONTACTO
      ['contacto-festival', 'telefonoCelular', 'Celular'],                             // CELULAR
      ['contacto-festival', 'paginaWeb', 'Página web'],                                // PAGINA_WEB
      ['contacto-festival', 'otroEnlaceFestival', 'Otro enlace'],                      // OTRO_ENLACE
      ['contacto-festival', 'observacionesContactoFestival', 'Observaciones de contacto'], // OBSERVACIONES_CONTACTO
      // LOS DIECISEIS ROTULOS DE LA EDICION —NOMBRE_VERSION, ID_TIPOLOGIA, ID_FUENTE_FINANCIACION,
      // DIRECTOR y los demás— se medían aquí y se fueron con su pestaña el 13 de septiembre de
      // 2026. La misma regla los vigila en su sitio nuevo, `ficha-edicion-festival.component`.
    ];

    montar();
    editar();
    for (const [seccion, nombre, rotulo] of esperados) {
      irALaSeccion(seccion);
      expect(rotuloDe(nombre)).withContext(`${seccion} · ${nombre}`).toBe(rotulo);
    }
  });


  it('las secciones son las tres del Festival, y ninguna de la Edición', () => {
    // ERAN ONCE: tres del Festival y ocho de la Edición, que esta ficha pintaba en una segunda
    // pestaña que nadie podía abrir. Retiradas.
    //
    // MUTANTE QUE MATA: volver a colar aquí un grupo de la Edición. La ficha volvería a pedir por
    // segunda vez datos que se registran en `ficha-edicion-festival.component`.
    expect(SECCIONES_DE_LA_FICHA.map(seccion => seccion.titulo)).toEqual([
      'Datos generales',
      'Contacto del Festival',
      'Prácticas y territorios',
    ]);
  });

  // ───────────────────────── La misma vista para leer y para editar ─────────────────────────

  // `fakeAsync` + `tick()` PORQUE NGMODEL ESCRIBE LA VISTA EN UNA MICROTAREA: el `<input>` se crea
  // vacío y NgModel le pone el valor después. Sin el `tick()` se lee la cadena vacía y la prueba
  // mediría el repintado, no el dato.
  it('«Editar» habilita los campos sin cambiar de pantalla', fakeAsync(() => {
    // EL ENCARGO, TEXTUAL, del usuario: «quiero que mantengamos la misma
    // vista para editar la edición del festival y para la ficha; si le doy editar, me habilita los
    // campos del festival, y así se puede tener una mejor experiencia de usuario».
    //
    // QUÉ MEDÍA ESTO ANTES: nada, porque no existía. «Editar» cambiaba la ficha entera por un
    // formulario de doce pantallas con su propio recorrido, y quien venía de leer tenía que volver
    // a encontrar cada dato.
    montar();

    // AL LEER: el mismo paso, y su valor es texto.
    desplegar('generales');
    expect(buscar('seccion-generales')).not.toBeNull();
    expect(buscar('ficha-lectura-generales')!.textContent).toContain('Nombre del festival');
    expect(raiz().querySelector('[name="nombre"]')).toBeNull();

    editar();
    tick();
    desplegar('generales');
    tick();
    fixture.detectChanges();

    // AL EDITAR: el MISMO paso, en el mismo sitio, y ahora su valor es un campo.
    expect(buscar('seccion-generales')).not.toBeNull();
    expect(buscar('ficha-lectura-generales')).toBeNull();
    expect(raiz().querySelector<HTMLInputElement>('[name="nombre"]')!.value).toBe(FESTIVAL.nombre);
    // Y SIGUE SIENDO LA MISMA PANTALLA: el contenedor del Festival es el que ya estaba.
    expect(buscar('panel-festival')).not.toBeNull();
  }));



  it('la ficha abre con todos los pasos cerrados, al leer y al editar', () => {
    // «EL ESTADO NATURAL SON TODOS CERRADOS», del criterio, sin
    // distinguir modo. Los encabezados plegados son el índice de la ficha: se ven los títulos de un
    // vistazo y se abre lo que interese.
    //
    // MUTANTE QUE MATA: nacer con el primer paso desplegado, que es como estaba una hora antes.
    montar();

    for (const paso of PASOS_DE_LA_FICHA) {
      expect(buscar(`paso-${paso.id}`)).withContext(paso.id).not.toBeNull();
      expect(buscar(`cuerpo-paso-${paso.id}`)).withContext(paso.id).toBeNull();
    }
    expect(buscar('seccion-generales')).toBeNull();

    editar();

    expect(buscar('bloque-generales')).toBeNull();
    expect(buscar('abrir-paso-generales')!.getAttribute('aria-expanded')).toBe('false');
  });

  it('el encabezado abre con un clic y cierra con otro', () => {
    // «El hamburguesa también debe abrir con clic y retraerse igual con clic», del usuario el 29 de
    // agosto de 2026. Hasta ese momento volver a pulsar el abierto no lo cerraba, y no había forma
    // de devolver la ficha a su estado plegado.
    //
    // MUTANTE QUE MATA: que `alternarPaso()` solo abra.
    montar();
    editar();

    buscar<HTMLButtonElement>('abrir-paso-musica-festival')!.click();
    fixture.detectChanges();
    expect(buscar('bloque-musica-festival')).not.toBeNull();
    expect(buscar('abrir-paso-musica-festival')!.getAttribute('aria-expanded')).toBe('true');

    buscar<HTMLButtonElement>('abrir-paso-musica-festival')!.click();
    fixture.detectChanges();
    expect(buscar('bloque-musica-festival')).toBeNull();
    expect(buscar('abrir-paso-musica-festival')!.getAttribute('aria-expanded')).toBe('false');
  });

  it('abrir un paso no cierra los demás', () => {
    // SON ALTERNADORES INDEPENDIENTES, no un acordeón exclusivo: quien necesite comparar dos pasos
    // puede tener los dos abiertos. Es lo que permite «todos cerrados» como estado de partida —con
    // uno exclusivo, «todos cerrados» no sería un estado alcanzable—.
    //
    // MUTANTE QUE MATA: que abrir uno cierre el resto.
    montar();
    editar();

    desplegar('generales');
    desplegar('musica-festival');

    expect(buscar('bloque-generales')).not.toBeNull();
    expect(buscar('bloque-musica-festival')).not.toBeNull();
  });


  it('cada panel dice de quién son sus datos, y ya no nombrando la tabla', () => {
    // POR QUE EXISTIO ESTA PRUEBA. Los dos «Correo de contacto» —el del Festival y el de la
    // edicion— son columnas de tablas distintas y se veian identicos. La respuesta era una pastilla
    // que decia «Dato del Festival» o «Dato de la edicion».
    //
    // POR QUE CAMBIA DE FORMA. El usuario las retiro: «tambien tiene ese
    // tag de DATO DE LA EDICION que no deberia ir». La pregunta la contesta ahora la pestaña
    // activa, que ya estaba ahi y no nombra ninguna tabla. La regla que se protege es la misma:
    // que se sepa en cual de los dos bloques se esta.
    montar();
    editar();
    expect(buscar('panel-insignia')).withContext('la pastilla nombraba la tabla').toBeNull();
    expect(buscar('panel-festival')).not.toBeNull();

    // Y YA NO HAY UN SEGUNDO PANEL DEL QUE DISTINGUIRSE: la pestaña «Ediciones» se retiró el 13 de
    // septiembre de 2026, así que tampoco hay dos lados que confundir. Lo que queda por comprobar
    // es que la pastilla siga sin volver.
    expect(buscar('panel-edicion')).toBeNull();
  });

  it('con la ficha abierta, la página de detrás no se desplaza', () => {
    // LA RUEDA MOVIA EL PANEL DE DEBAJO. El diálogo es `fixed inset-0` con su propio desplazamiento:
    // al llegar a su final, o con el cursor sobre el fondo oscuro, la rueda seguia moviendo el panel
    // de la organizacion y al cerrar la ficha la pagina estaba en otro sitio. Lo señalo el usuario
    //.
    const previo = document.body.style.overflow;
    document.body.style.overflow = 'auto';

    montar();
    expect(document.body.style.overflow).toBe('hidden');

    // Y SE DEVUELVE LO QUE HABIA, no una cadena vacia: el geovisor tambien congela el `body`, y
    // limpiarlo al cerrar esta ficha lo descongelaria a el.
    fixture.destroy();
    expect(document.body.style.overflow).toBe('auto');
    document.body.style.overflow = previo;
  });


  it('un Festival sin ninguna edición se puede editar', fakeAsync(() => {
    // DEFECTO CERRADO EL 29 DE AGOSTO DE 2026. `puedeEditar()` solo miraba `ficha()?.esEditable`, y
    // un Festival recién creado no tiene ninguna edición: el botón «Editar el Festival» no se
    // pintaba, y no había forma de corregir su nombre ni su alcance sin registrarle antes una
    // edición que quizá nadie quería.
    //
    // MUTANTE QUE MATA: quitar la tercera rama de `puedeEditar()`.
    montar();

    expect(buscar('ficha-editar')).not.toBeNull();
    editar();
    tick();
    desplegar('generales');
    tick();
    fixture.detectChanges();

    expect(buscar('bloque-generales')).not.toBeNull();
    expect(raiz().querySelector<HTMLInputElement>('[name="nombre"]')!.value).toBe(FESTIVAL.nombre);
  }));

  it('una cabecera fuera de borrador no ofrece «Editar» aunque no tenga ediciones', () => {
    // LA OTRA MITAD DE LA REGLA, y es la del servidor: el PUT del Festival solo admite Borrador y
    // AjustesSolicitados (`FestivalesExternosEndpoints.cs-483`), y en cualquier otro contesta
    // 409. Sin esta prueba, la rama nueva de `puedeEditar()` abriría el formulario de un Festival
    // publicado para que el guardado muriera contra el servidor.
    fixture.componentRef.setInput('festival', { ...FESTIVAL, estado: 'Publicado' });
    fixture.componentRef.setInput('organizacionId', '117');
    fixture.detectChanges();

    expect(buscar('ficha-editar')).toBeNull();
  });

  // ─────────────── La propuesta de cambios: la unificación ───────────────
  //
  // ANTES «PROPONER CAMBIOS» SOLO NAVEGABA AFUERA, a `/registro?modo=festival` -una pantalla
  // entera distinta, sin relación visual con esta ficha-. El criterio es este: «el botón Editar
  // Festival no debe abrir una ruta paralela, antigua o independiente». Ahora el mismo botón
  // inicia -o recupera- la propuesta de cambios y habilita los mismos campos que ya usa
  // Borrador/AjustesSolicitados, dentro de esta misma pantalla.

  /**
   * UN FESTIVAL PUBLICADO, que es el único estado donde se propone un cambio en vez de editarlo.
   *
   * `puedeEditar()` es hoy `esAlta() || cabeceraEditable()`, y `cabeceraEditable()` es falso fuera
   * de Borrador y AjustesSolicitados —la misma regla del servidor, que contesta 409—, así que el
   * pie cae al `@else if (cabecera())` que ofrece «Proponer cambios».
   */
  function montarPublicado(cambiosEnPropuesta: Partial<PropuestaCambioFestival> = {}): void {
    api.propuestaQueDevuelve = propuesta(cambiosEnPropuesta);
    fixture.componentRef.setInput('festival', { ...FESTIVAL, estado: 'Publicado' });
    fixture.componentRef.setInput('organizacionId', '117');
    fixture.detectChanges();
  }

  it('Publicado, con su edición vigente ya no editable, ofrece «Editar Festival» y no «Editar»', () => {
    // «EDITAR EL FESTIVAL» (`ficha-editar`) Y «EDITAR FESTIVAL»/«PROPONER CAMBIOS»
    // (`ficha-proponer-cambios`) SON DOS BOTONES INDEPENDIENTES: el primero edita la EDICIÓN
    // abierta -su propio `esEditable`-, el segundo la CABECERA -su propio estado-. Los dos pueden
    // coexistir si la edición abierta todavía admite cambios; aquí no, porque es la vigente de un
    // Festival ya publicado, y esa combinación es la que interesa a esta prueba.
    montarPublicado();

    expect(buscar('ficha-editar')).toBeNull();
    expect(buscar('ficha-proponer-cambios')).not.toBeNull();
  });

  it('«Editar Festival» inicia la propuesta, siembra sus campos y habilita el formulario', fakeAsync(() => {
    montarPublicado({ nombre: 'Festival de Música del Pacífico (propuesta)' });

    buscar<HTMLButtonElement>('ficha-proponer-cambios')!.click();
    tick();
    fixture.detectChanges();
    desplegar('generales');
    tick();
    fixture.detectChanges();

    // MUTANTE QUE MATA: sembrar desde `cabecera` en vez de desde la propuesta. Si ya había una
    // propuesta a medio editar, ese avance se perdería cada vez que se vuelve a abrir.
    expect(raiz().querySelector<HTMLInputElement>('[name="nombre"]')!.value)
      .toBe('Festival de Música del Pacífico (propuesta)');
    expect(buscar('ficha-guardar')).not.toBeNull();
  }));

  it('iniciar la propuesta es idempotente: reabrir no crea una segunda', fakeAsync(() => {
    montarPublicado();

    buscar<HTMLButtonElement>('ficha-proponer-cambios')!.click();
    tick();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('ficha-cancelar')!.click();
    tick();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('ficha-proponer-cambios')!.click();
    tick();
    fixture.detectChanges();

    // El servidor decide la idempotencia -`POST .../propuestas-cambio` devuelve la activa si ya
    // hay una-; esta prueba solo vigila que la ficha llame al POST cada vez que se pulsa el botón,
    // sin inventarse un atajo que se salte al servidor.
    expect(api.vecesQueIniciaPropuesta).toBe(2);
  }));

  it('guardar en modo propuesta llama a guardarPropuesta, nunca a guardarFestival', fakeAsync(() => {
    montarPublicado();

    buscar<HTMLButtonElement>('ficha-proponer-cambios')!.click();
    tick();
    fixture.detectChanges();
    desplegar('generales');
    tick();
    raiz().querySelector<HTMLInputElement>('[name="nombre"]')!.value = 'Nombre corregido';
    raiz().querySelector<HTMLInputElement>('[name="nombre"]')!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();

    // MUTANTE QUE MATA: llamar a `guardarFestival` en modo propuesta. El PUT directo del Festival
    // contesta 409 fuera de Borrador/AjustesSolicitados -`FestivalesExternosEndpoints.cs-483`-,
    // así que guardar reventaría contra el servidor sobre un Festival Publicado.
    expect(api.festivalesGuardados).toEqual([]);
    expect(api.propuestasGuardadas.length).toBe(1);
    expect(api.propuestasGuardadas[0].solicitud.nombre).toBe('Nombre corregido');

    // Y LO PUBLICADO NO CAMBIA: la propuesta se guarda aparte, a la espera de que el PNMC la
    // revise. El Festival que esta ficha sigue mostrando no puede adelantarse a esa decisión.
    expect(componente.cabecera()?.nombre).toBe(FESTIVAL.nombre);
    expect(componente.cabecera()?.estado).toBe('Publicado');
  }));

  it('llegar con `abrirEnEdicion` sobre un Publicado abre la propuesta, no la cabecera directa', fakeAsync(() => {
    api.propuestaQueDevuelve = propuesta();
    fixture.componentRef.setInput('festival', { ...FESTIVAL, estado: 'Publicado' });
    fixture.componentRef.setInput('organizacionId', '117');
    fixture.componentRef.setInput('abrirEnEdicion', true);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();

    // MUTANTE QUE MATA: seguir llamando a `editar()` sin más para cualquier estado. `editar()`
    // corta con `!puedeEditar()` en Publicado, y `?editar=1` desde «Editar Festival» en Mis
    // procesos no abriría nada.
    expect(api.vecesQueIniciaPropuesta).toBe(1);
    expect(buscar('ficha-guardar')).not.toBeNull();
  }));

  it('cancelar en modo propuesta no toca lo publicado y devuelve el foco al botón que la abrió', fakeAsync(() => {
    montarPublicado();
    document.body.appendChild(fixture.nativeElement);

    buscar<HTMLButtonElement>('ficha-proponer-cambios')!.click();
    tick();
    fixture.detectChanges();
    buscar<HTMLButtonElement>('ficha-cancelar')!.click();
    fixture.detectChanges();
    // `enfocar()` mueve el foco en un `setTimeout`, no en el mismo tick del clic.
    tick();

    expect(buscar('ficha-guardar')).toBeNull();
    expect(buscar('ficha-proponer-cambios')).not.toBeNull();
    expect(document.activeElement).toBe(buscar('ficha-proponer-cambios'));
    expect(componente.cabecera()?.estado).toBe('Publicado');
  }));

  it('«Editar» sobre un Festival sin ediciones entra directo desde la tarjeta', () => {
    // EL BOTÓN «EDITAR» DE LA TARJETA abre la ficha con `abrirEnEdicion`. Hasta el 29 de agosto de
    // 2026 ese camino pasaba por haber cargado una edición, así que sobre un Festival sin ninguna
    // abría en lectura y ahí se quedaba: el botón no hacía nada visible.
    montar(true);
    desplegar('generales');

    expect(buscar('ficha-formulario')).not.toBeNull();
    expect(buscar('bloque-generales')).not.toBeNull();
  });






  // `fakeAsync` + `tick()` PORQUE NGMODEL ESCRIBE LA VISTA EN UNA MICROTAREA: al volver a la
  // pestaña de la edición el `<input>` se crea vacío y NgModel le pone el valor después. Sin el
  // `tick()` se lee la cadena vacía y la prueba mide el repintado, no el dato.

  // ───────────────────────── Los ocho pasos ─────────────────────────



  it('los pasos cubren los grupos de campos, sin repetir ninguno ni dejarlo fuera', () => {
    // LO QUE ESTO IMPIDE: que un grupo de campos se quede sin paso al reordenarlos y desaparezca de
    // la pantalla sin que nada falle. El formulario seguiría funcionando y esos campos no se
    // podrían escribir.
    const repartidos = PASOS_DE_LA_FICHA.flatMap(paso => paso.secciones);
    expect(repartidos.length).toBe(SECCIONES_DE_LA_FICHA.length);
    expect([...repartidos].sort()).toEqual(SECCIONES_DE_LA_FICHA.map(una => una.id).sort());

    // Y CADA GRUPO QUE UN PASO DECLARA EXISTE: un identificador mal escrito en `secciones` deja
    // ese grupo sin pintar y `seccion()` devuelve el primero, así que el encabezado dice «Datos
    // generales» donde no toca en vez de fallar.
    for (const paso of PASOS_DE_LA_FICHA) {
      for (const id of paso.secciones) {
        expect(SECCIONES_DE_LA_FICHA.some(una => una.id === id))
          .withContext(`${paso.id} · ${id}`).toBeTrue();
      }
    }
  });


  // AQUI HUBO UNA PRUEBA sobre el paso que juntaba «Datos de la edición» con «Tipología» y no
  // repetía el encabezado del grupo. Los dos grupos eran de la Edición, y se fueron con su pestaña
  //. Hoy los tres pasos del Festival llevan un grupo cada uno, así que
  // la condición no tiene dónde aplicarse; volverá a hacer falta si un paso vuelve a juntar dos.


  it('la plantilla pinta los pasos y sus grupos en el orden que declara la lista', () => {
    // <b>ESTO ES LO QUE HACE QUE `secciones` SEA UN DATO Y NO UN ADORNO.</b> La plantilla escribe
    // los doce grupos a mano, uno por uno: nada obliga por sí solo a que el orden del HTML sea el
    // que declara `PASOS_DE_LA_FICHA`. Sin esta prueba, cambiar la lista no cambiaría la pantalla y
    // el fichero de datos envejecería diciendo algo que no es cierto —lo destapó un mutante el 29
    // de agosto de 2026: invertir `['organizador', 'financiacion']` dejaba las 189 en verde—.
    //
    // EL ORDEN ESPERADO SALE DE LA LISTA, no de una copia escrita aquí: una copia sería un tercer
    // sitio donde el orden está dicho, y el que se quedaría atrás.
    montar();

    {
      desplegarTodo();
      const pasos = PASOS_DE_LA_FICHA;

      const pintados = Array.from(raiz().querySelectorAll('[data-testid^="paso-"]'))
        .map(nodo => nodo.getAttribute('data-testid'));
      expect(pintados).toEqual(pasos.map(paso => `paso-${paso.id}`));

      for (const paso of pasos) {
        const dentro = Array.from(
          buscar(`paso-${paso.id}`)!.querySelectorAll('[data-testid^="seccion-"]'))
          .map(nodo => nodo.getAttribute('data-testid'));
        expect(dentro).withContext(paso.id).toEqual(paso.secciones.map(id => `seccion-${id}`));
      }
    }
  });


  // ───────────────────────── Las tarjetas de las ediciones ─────────────────────────








  // ───────────────────────── Guardar y los avisos ─────────────────────────

  it('«Guardar borrador» está disponible desde el primer momento', () => {
    // LA REGLA ANTERIOR SE FUE CON LOS PASOS. «Después del paso 3 podemos dar en guardar borrador,
    // no antes», del usuario, ordenaba un recorrido de doce pantallas. Sin
    // pasos que recorrer no hay nada que ordenar: todo está a la vista desde que se abre.
    montar();
    editar();

    const guardar = buscar('ficha-guardar');
    expect(guardar).not.toBeNull();
    expect(guardar!.textContent!.trim()).toBe('Guardar borrador');

    expect(buscar('ficha-guardar')).not.toBeNull();
  });

  it('el aviso de validación nombra la sección y la despliega', () => {
    // CON LAS SECCIONES PLEGADAS, un «Revisa los campos marcados» a secas señala a algo que no se
    // está viendo: hay que decir en cuál y abrirla.
    //
    // MUTANTE QUE MATA: devolver el aviso genérico sin desplegar el paso.
    montar();
    editar();
    escribir('nombre', '');

    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    fixture.detectChanges();

    expect(buscar('ficha-error')!.textContent).toContain('Datos generales');
    expect(buscar('bloque-generales')).not.toBeNull();
  });

  // ───────────────────────── El alta ─────────────────────────

  it('sin Festival abre directo en el formulario y no pide versiones', () => {
    montarAlta();

    expect(buscar('ficha-formulario')).not.toBeNull();
    // ABRE PLEGADA, como toda la ficha desde: el paso está, sus campos no.
    expect(buscar('paso-generales')).not.toBeNull();
    expect(buscar('bloque-generales')).toBeNull();
    desplegar('generales');
    expect(buscar('bloque-generales')).not.toBeNull();
    expect(buscar('ficha-titulo')!.textContent!.trim()).toBe('Crear un Festival');
  });





  it('cancelar en el alta cierra el diálogo, porque no hay nada a lo que volver', () => {
    montarAlta();
    buscar<HTMLButtonElement>('ficha-cancelar')!.click();

    expect(cierres).toBe(1);
  });

  // ───────────────────────── La edición ─────────────────────────


  // AQUI HUBO UNA PRUEBA que medía que un Festival Publicado no gastara el PUT de la cabecera y sí
  // el de su versión: eran dos estados y dos rutas, y el 409 del primero habría tumbado el segundo.
  // Con una sola petición, el caso se contesta antes: un Publicado no ofrece «Editar» —lo dice
  // `puedeEditar()`, que es `esAlta() || cabeceraEditable()`— sino «Proponer cambios», y eso lo
  // cubren las pruebas de la propuesta.


  // ───────────────────────── Lo que se lee ─────────────────────────







  it('un dato ausente se lee «Sin registrar» y no como un hueco en blanco', () => {
    // EL FESTIVAL DE PRUEBA NO TRAE `observacionesContacto`, y un valor vacío pintado como valor
    // vacío se lee igual que un fallo de carga.
    montar();
    desplegarTodo();

    expect((buscar('panel-festival')!.textContent ?? '').includes('Sin registrar')).toBeTrue();
  });





  // ───────────────────────── Lo que viaja ─────────────────────────



  it('la cabecera viaja con su bloque de contacto entero', fakeAsync(() => {
    montar();
    editar();
    tick();
    // LAS SIETE COLUMNAS DE CONTACTO VIVEN EN SU PROPIO PASO desde: son
    // de `ART_MUS_FESTIVALES` y se confundían con las de la edición, que se llaman casi igual.
    irALaSeccion('contacto-festival');
    escribir('instagramFestival', '@nuevo');
    escribir('telefonoCelular', '3009999999');
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();

    const solicitud = api.festivalesGuardados[0].solicitud;
    expect(solicitud.instagram).toBe('@nuevo');
    expect(solicitud.telefonoCelular).toBe('3009999999');
    expect(solicitud.paginaWeb).toBe('https://festival.org');
    expect(solicitud.observacionesContacto).toBe('Llamar en la mañana.');
  }));

  it('las prácticas y los territorios se preguntan UNA vez, en el Festival', fakeAsync(() => {
    // LAS RETIRÓ EL USUARIO de la edición: «estas prácticas ya las tiene el
    // festival… quítalo porque es información que tiene la entidad padre». Se preguntaban dos veces
    // y nada obligaba a que las dos respuestas coincidieran.
    //
    // LA EDICIÓN SIGUE GUARDÁNDOLAS: sus tablas puente existen y la ruta las espera, así que se
    // mandan las del Festival. Mandarlas vacías borraría sus filas.
    //
    // MUTANTE QUE MATA: que `construirSolicitudDeVersion()` vuelva a leerlas de `formulario`, que
    // ahora nadie escribe y quedaría con lo que trajo la ficha.
    montar();
    editar();
    tick();

    // SE ELIGEN EN «Prácticas y territorios» DEL FESTIVAL Y EN NINGÚN OTRO SITIO. Mientras la
    // ficha tuvo una segunda pestaña, aquí se comprobaba además que la de la Edición no volviera a
    // preguntarlas; esa pestaña ya no existe.
    irALaSeccion('musica-festival');
    // La segunda práctica del catálogo es «Coro», id 7, que la cabecera no tiene.
    buscar('casillas-festival-practicasMusicalesIds')!.querySelectorAll<HTMLInputElement>('input')[1].click();
    fixture.detectChanges();

    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();

    // SE PREGUNTAN EN EL FESTIVAL Y VIAJAN CON EL FESTIVAL. Mientras la ficha guardaba también una
    // versión, esta prueba comprobaba además que las dos peticiones dijeran lo mismo; ahora hay
    // una sola petición y el punto es que la casilla marcada llegue a `dbo.Festivales`.
    expect(api.festivalesGuardados[0].solicitud.practicasMusicalesIds).toEqual([3, 7]);
    expect(api.festivalesGuardados[0].solicitud.territoriosSonorosIds).toEqual([2]);
  }));

  it('guardar el Festival gasta UNA petición, y es la de la cabecera', fakeAsync(() => {
    // <b>ESTA PRUEBA VIGILABA UN DEFECTO QUE YA NO SE PUEDE COMETER, y sigue por lo que mide.</b>
    // Hasta este guardado encadenaba una segunda petición que creaba
    // una VERSIÓN, y su guardia —«solo si hay algo escrito en la edición»— faltó durante un tiempo
    // en la rama de actualización: quien corregía el nombre de un Festival sin ediciones se
    // encontraba una edición que nadie registró. Retirada la segunda petición, lo que queda por
    // comprobar es que sigue habiendo UNA y que es la de `dbo.Festivales`.
    //
    // MUTANTE QUE MATA: volver a encadenar cualquier segunda escritura detrás de la cabecera.
    montar();
    editar();
    tick();
    escribir('nombre', 'Festival con el nombre corregido');
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();
    fixture.detectChanges();

    expect(api.orden).toEqual(['guardarFestival']);
    expect(api.festivalesGuardados[0].solicitud.nombre).toBe('Festival con el nombre corregido');
    expect(buscar('ficha-mensaje')!.textContent).toContain('Los datos del Festival quedaron guardados');
  }));



  it('con alcance nacional el departamento y el municipio viajan en null', fakeAsync(() => {
    montar();
    editar();
    tick();
    elegir('[name="nivelCobertura"]', 'nacional');
    tick();
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();

    // `CK_Festivales_NivelCobertura` exige los dos en NULL con alcance nacional, y `''` no es NULL:
    // el guardado moriría con un 500 que no nombra ningún campo.
    const solicitud = api.festivalesGuardados[0].solicitud;
    expect(solicitud.codigoDepartamento).toBeNull();
    expect(solicitud.codigoMunicipio).toBeNull();
  }));



  // ───────────────────────── Lo que no se manda ─────────────────────────

  it('sin nombre del festival no gasta ninguna petición y lleva a los datos generales', fakeAsync(() => {
    montar();
    editar();
    tick();
    escribir('nombre', '');
    // SE GUARDA DESDE LA OTRA PESTAÑA a propósito: es el caso en que el error no está a la vista.
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();
    fixture.detectChanges();

    expect(api.festivalesGuardados.length).toBe(0);
    // DESPLIEGA LA SECCION DEL ERROR: con las secciones plegadas, «Revisa los campos marcados» sin
    // abrir la que falla señala a algo que no se está viendo.
    expect(buscar('bloque-generales')).not.toBeNull();
    expect(texto()).toContain('Escribe el nombre del festival.');
  }));



  it('los errores del servidor se pintan por campo y sin corchetes', fakeAsync(() => {
    api.falloAlGuardarFestival = {
      status: 400,
      message: 'El Festival no pudo guardarse.',
      payload: { errors: { nombre: ['Ya hay un Festival con ese nombre en el municipio.'] } },
    };
    montar();
    editar();
    tick();
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();
    fixture.detectChanges();

    expect(texto()).toContain('Ya hay un Festival con ese nombre en el municipio.');
    expect(texto()).not.toContain('["Ya hay');
    // Y DESPLIEGA LA SECCION donde vive ese campo, igual que la validación local.
    expect(buscar('bloque-generales')).not.toBeNull();
  }));

  // ───────────────────────── Cerrar y foco ─────────────────────────

  it('la X cierra la ventana, también con el formulario abierto', () => {
    montar();
    editar();
    buscar<HTMLButtonElement>('ficha-cerrar')!.click();

    expect(cierres).toBe(1);
  });

  it('la X tiene nombre accesible propio, porque «×» no se lee', () => {
    montar();

    expect(buscar('ficha-cerrar')!.getAttribute('aria-label')).toBe('Cerrar la ficha del Festival');
  });

  it('Escape cierra mientras se lee y NO mientras se edita', () => {
    // LA TECLA SE PULSA DENTRO DEL DIALOGO, Y AHORA ESO ES LO CORRECTO. Antes la ficha escuchaba
    // `document:keydown.escape`, porque nada garantizaba que el foco estuviera dentro: el foco se
    // quedaba en el botón que la abrió. Desde que la ficha usa `appDialogo`, el foco entra al
    // abrirse y no sale, así que un Escape real siempre nace dentro y sube hasta el diálogo.
    // Escuchar en `document` además cerraba fichas que ni siquiera estaban abiertas.
    const dialogo = () => buscar('ficha-festival-modal')!;
    montar();
    dialogo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(cierres).toBe(1);

    editar();
    dialogo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(cierres).withContext('editando no debe cerrar').toBe(1);
  });

  it('el fondo NO cierra mientras se edita', () => {
    montar();
    editar();
    const fondo = buscar('ficha-festival-fondo')!;
    fondo.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
    fondo.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(cierres).toBe(0);
  });

  it('entrar al formulario deja el foco en el primer encabezado, no en el body', fakeAsync(() => {
    // NO EN UN CAMPO, porque desde no hay ninguno a la vista: la ficha abre
    // plegada. El foco va al mando que despliega el primer paso, así que quien navega con teclado
    // sigue con Enter.
    montarEnElDocumento();
    editar();
    tick();

    expect(document.activeElement).toBe(buscar('abrir-paso-generales'));
  }));

  it('desplegar un paso lleva el foco a su primer campo', fakeAsync(() => {
    // SIN ESTO EL FOCO SE QUEDA EN EL ENCABEZADO y quien navega con teclado tiene que tabular hasta
    // el contenido que acaba de abrir. Al leer no hay campos, así que cae al propio encabezado.
    montarEnElDocumento();
    editar();
    tick();

    buscar<HTMLButtonElement>('abrir-paso-generales')!.click();
    fixture.detectChanges();
    tick();

    expect(document.activeElement).toBe(raiz().querySelector('#ficha-nombre-festival'));
  }));


  // ───────────────────────── Lo que la revisión adversarial encontró ─────────────────────────

  it('el mensaje de error va atado a su campo con aria-describedby', fakeAsync(() => {
    // SIN ESTO un lector de pantalla lee «Nombre del festival, no válido» y no dice qué falta: el
    // texto que lo explica está en un párrafo suelto que nadie le ata al control.
    montar();
    editar();
    tick();
    escribir('nombre', '');
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    tick();
    fixture.detectChanges();

    const campo = raiz().querySelector<HTMLInputElement>('[name="nombre"]')!;
    const idDelError = campo.getAttribute('aria-describedby');
    expect(idDelError).toBe('error-nombre');
    expect(raiz().querySelector(`#${idDelError}`)!.textContent).toContain('Escribe el nombre del festival.');
  }));

  it('tras un guardado fallido el foco va al aviso, y no se queda en el body', fakeAsync(() => {
    // El botón «Guardar» se deshabilita mientras la petición vuela, y el navegador desenfoca todo
    // elemento que pasa a `disabled`: sin mover el foco a mano queda fuera del diálogo.
    api.falloAlGuardarFestival = { status: 500, message: 'El Festival no pudo guardarse.' };
    montarEnElDocumento();
    editar();
    tick();
    buscar<HTMLButtonElement>('ficha-guardar')!.click();
    // SE REPINTA ANTES DE AGOTAR EL TEMPORIZADOR: el aviso lleva `[class.hidden]` mientras no hay
    // error, y `focus()` sobre un elemento con `display:none` no hace nada. En el navegador esto no
    // se nota —zone.js repinta antes de correr el `setTimeout`—, pero aquí el orden lo pone la
    // prueba, y con `tick()` primero mediría el fallo en vez del arreglo.
    fixture.detectChanges();
    tick();

    expect(document.activeElement).toBe(buscar('ficha-error'));
  }));



  it('cancelar devuelve el foco a «Editar el Festival»', fakeAsync(() => {
    // CANCELAR TIENE QUE DEVOLVER EL FOCO A ALGO: el botón de cancelar desaparece con el
    // formulario, y el navegador deja el foco en el `<body>` —fuera del diálogo, a un tabulador
    // desde el principio del documento—.
    montarEnElDocumento();
    editar();
    tick();
    buscar<HTMLButtonElement>('ficha-cancelar')!.click();
    fixture.detectChanges();
    tick();

    expect(document.activeElement).toBe(buscar('ficha-editar'));
  }));


  it('si los catálogos no cargan, la ficha se lee igual', () => {
    api.catalogosFallan = true;
    montar();
    desplegarTodo();

    expect(buscar('ficha-lectura')).not.toBeNull();
    expect(componente.catalogos().tipologias).toEqual([]);
  });
});
