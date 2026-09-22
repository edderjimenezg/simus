import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MapDataService } from '../../../../core/services/map-data.service';
import { RecorridoGuiadoService } from '../../../../core/services/recorrido-guiado.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { DEFAULT_TEXTS, WEB_TEXT_GROUPS } from '../../../../core/cms/registro-de-textos-web';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';

/**
 * El tutorial del geovisor: catorce textos cuyo público es, por definición, el
 * que menos sabe usar la herramienta, y que hasta ahora vivían fijos en un
 * arreglo de TypeScript.
 *
 * Se prueba aparte del resto de la página a propósito: aquello mira el modelo
 * de datos del mapa y esto mira de dónde sale la prosa. Mezclarlos haría que un
 * cambio en Leaflet tumbara la prueba del texto y al revés.
 */
describe('MapaEcosistemicoPageComponent · tutorial editable', () => {
  const CLAVES = WEB_TEXT_GROUPS.find((g) => g.id === 'map_tutorial')!.fields.map((f) => f.key);

  const bundleVacio = {
    cartografia: { departments: { type: 'FeatureCollection', features: [] }, municipalities: { type: 'FeatureCollection', features: [] } },
    baseCounts: {}, festivalRecords: [], schoolRecords: [], marketRecords: [],
    redesRecords: [], luthierRecords: [], festivalCounts: {}, schoolCounts: {}, marketCounts: {},
  };

  const montar = (publicados: Record<string, string> = {}) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundleVacio) } },
      ],
    });

    const textos = TestBed.inject(TextosWebService);
    spyOn(textos, 'getWebText').and.callFake((clave: string) => publicados[clave] ?? DEFAULT_TEXTS[clave] ?? '');

    return TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
  };

  it('declara los dieciocho pasos, con título y texto cada uno', () => {
    // ERAN SEIS Y DESCRIBIAN UN GEOVISOR QUE YA NO EXISTIA; pasaron a nueve, y el dueño del
    // proyecto pidió más detalle: un paso por herramienta —los tres modos de dibujo por separado,
    // las cinco decisiones del gráfico, la tabla, el directorio y la agenda—, porque nombrar tres
    // modos no es lo mismo que enseñarlos.
    expect(CLAVES.length).toBe(36);
  });

  it('cada paso toma su texto del panel', () => {
    const publicados = Object.fromEntries(CLAVES.map((k) => [k, `<<${k}>>`]));
    const componente = montar(publicados);
    const pasos = componente.TUTORIAL_STEPS();

    expect(pasos.length).toBe(18);
    expect(pasos.map((p) => p.title)).toEqual(
      CLAVES.filter((k) => k.endsWith('_title')).map((k) => `<<${k}>>`),
    );
    expect(pasos.map((p) => p.description)).toEqual(CLAVES.filter((k) => k.endsWith('_desc')).map((k) => `<<${k}>>`));
  });

  it('los iconos y el orden NO salen del panel', () => {
    // El reparto que hay que preservar: si los iconos se mudaran al CMS, una
    // errata al editar dejaría el paso sin icono, y la plantilla los resuelve
    // por nombre exacto.
    const componente = montar({ map_tutorial_1_title: 'Cualquier otro título' });

    expect(componente.TUTORIAL_STEPS().map((p) => p.iconClass)).toEqual([
      'Globe', 'Layers3', 'Search', 'Aperture',
      'Shapes', 'Shapes', 'Shapes',
      'BookOpen', 'Map', 'LayoutGrid',
      'LayoutGrid', 'BarChart3', 'Shapes', 'PieChart', 'BarChart3',
      'Table', 'List', 'CalendarDays',
    ]);
  });

  it('la bienvenida ya no toma prestada la entradilla del mapa', () => {
    // Antes la plantilla sustituía la descripción del paso 1 por
    // `map_description`, la introducción del geovisor, porque era la única
    // clave editable a mano. Con eso, corregir la entradilla del mapa cambiaba
    // en silencio la bienvenida del tutorial: una clave haciendo dos trabajos.
    const componente = montar({
      map_description: 'ENTRADILLA DEL MAPA',
      map_tutorial_1_desc: 'BIENVENIDA DEL TUTORIAL',
    });

    expect(componente.TUTORIAL_STEPS()[0].description).toBe('BIENVENIDA DEL TUTORIAL');
  });

  it('cada paso deja nítido su apartado y desenfoca los demás', () => {
    // ES EL NUDO DEL RECORRIDO. Antes se atenuaba por COLUMNA —`control`, `centro`, `lectura`— y
    // eso bastaba cuando cada columna era un panel con una sola cosa dentro. Hoy la izquierda tiene
    // tres apartados distintos: hablar del lente iluminando la columna entera dejaba nítidos los
    // filtros y la leyenda, que ese paso no explica.
    const componente = montar();
    componente.tutorialAbierto.set(true);

    // Paso 4: el lente. Su columna abierta, él con anillo, sus vecinos borrosos.
    componente.handleGoToStep(3);
    expect(componente.enfoqueDelTutorial('lente')).toContain('ring-4');
    expect(componente.enfoqueDelTutorial('izquierda')).toBe('');
    expect(componente.enfoqueDelTutorial('filtros')).toContain('blur');
    expect(componente.enfoqueDelTutorial('leyenda')).toContain('blur');
    expect(componente.enfoqueDelTutorial('derecha')).toContain('blur');

    // Paso 2: las capas, que viven en la otra columna.
    componente.handleGoToStep(1);
    expect(componente.enfoqueDelTutorial('capas')).toContain('ring-4');
    expect(componente.enfoqueDelTutorial('derecha')).toBe('');
    expect(componente.enfoqueDelTutorial('listado')).toContain('blur');
    expect(componente.enfoqueDelTutorial('lente')).toContain('blur');
  });

  it('cerrado no atenúa nada', () => {
    const componente = montar();
    componente.tutorialAbierto.set(false);

    for (const clave of ['izquierda', 'lente', 'capas', 'lienzo', 'vistas']) {
      expect(componente.enfoqueDelTutorial(clave)).toBe('');
    }
  });

  it('los tres modos de dibujo son tres pasos, y cada uno pone el suyo', () => {
    // NOMBRAR TRES MODOS NO ES ENSEÑARLOS. Cada paso deja PUESTO el modo del que habla, así que se
    // ve el mapa cambiar de naturaleza al pasar de uno al siguiente.
    const componente = montar();
    const puestos: string[] = [];

    for (const paso of [4, 5, 6]) {
      componente.handleGoToStep(paso);
      puestos.push(componente.modoDeDibujo());
    }

    expect(puestos).toEqual(['cobertura', 'practicas_territorios', 'calor']);
  });

  it('los cinco pasos del gráfico dejan puesta la combinación que explican', () => {
    const componente = montar();

    componente.handleGoToStep(11);
    expect(componente.vistaCentral()).toBe('grafico');
    expect(componente.formaDelGrafico()).toBe('departamentos');
    expect(componente.figuraDeDepartamentos()).toBe('barras');

    componente.handleGoToStep(12);
    expect(componente.figuraDeDepartamentos()).toBe('treemap');

    componente.handleGoToStep(13);
    expect(componente.formaDelGrafico()).toBe('composicion');
    expect(componente.dimensionDeComposicion()).toBe('territorios-sonoros');
    expect(componente.figuraDeComposicion()).toBe('barras');

    componente.handleGoToStep(14);
    expect(componente.dimensionDeComposicion()).toBe('practicas');
    expect(componente.figuraDeComposicion()).toBe('waffle');

    componente.handleGoToStep(15);
    expect(componente.vistaCentral()).toBe('tabla');
  });

  it('el paso del dibujo pide el modo por su identificador real', () => {
    // ESTO YA FALLO UNA VEZ. El recorrido escribía `modoDeDibujo` directamente y con el nombre
    // «simbolos», que no existe: el identificador es `practicas_territorios`. El modo quedaba en un
    // valor que ninguna rama de dibujo reconoce y el paso que explica cómo se dibuja enseñaba un
    // lienzo vacío, sin error y sin aviso.
    const componente = montar();
    componente.handleGoToStep(4);

    expect(componente.modosDeDibujo.map((m) => m.id)).toContain(componente.modoDeDibujo());
  });

  it('el paso visible sigue al índice del tutorial', () => {
    const componente = montar();

    expect(componente.pasoTutorial().title).toBe(DEFAULT_TEXTS['map_tutorial_1_title']);
    componente.handleGoToStep(3);
    expect(componente.pasoTutorial().title).toBe(DEFAULT_TEXTS['map_tutorial_4_title']);
    componente.handleGoToStep(17);
    expect(componente.pasoTutorial().title).toBe(DEFAULT_TEXTS['map_tutorial_18_title']);
  });
});

/**
 * Cerrar el tutorial devuelve el mapa donde estaba, y no a cero.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> `cerrarTutorial` llevaba un bloque rotulado «Reset
 * filters» que ponía modo, capa, departamento y lente en sus valores de fábrica. Tenía sentido
 * cuando cerrar el tutorial sólo podía significar «deshaz lo que el tour tocó»; dejó de tenerlo en
 * cuanto el tutorial se abre solo una vez al día y el mapa aprendió a guardar su estado en la
 * dirección. Quien llegaba con un departamento elegido, o abriendo un enlace compartido, lo perdía
 * todo al cerrar, sin aviso y sin forma de recuperarlo salvo rehacerlo a mano.
 */
describe('el tutorial toma prestado el mapa y lo devuelve', () => {
  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [{
        type: 'Feature',
        properties: { departmentCode: '05', departmentName: 'ANTIOQUIA' },
        geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
      }],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [], agendaRecords: [],
    schoolRecords: [], marketRecords: [], redesRecords: [], luthierRecords: [],
    festivalCounts: {}, agendaCounts: {}, schoolCounts: {}, marketCounts: {},
  };

  let component: MapaEcosistemicoPageComponent;

  beforeEach(() => {
    localStorage.removeItem('pnmc_last_tutorial_date');
    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundle) } },
      ],
    });
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  it('restaura lo que había puesto antes de abrirlo', () => {
    component.departamentoElegido.set('ANTIOQUIA');
    component.lenteDeLectura.set('territorios-sonoros');
    component.modoDeDibujo.set('calor');

    // POR `abrirElRecorrido` Y NO POR EL BOTON DE AYUDA: desde ese
    // botón abre el modo EXPLORAR, que no toca el mapa —solo señala lo que tiene el puntero
    // encima— y por tanto no tiene nada que devolver. Quien toma prestado el mapa es el recorrido.
    component.abrirElRecorrido();
    // El recorrido deja el mapa limpio para enseñar sobre él: eso sigue siendo correcto.
    expect(component.departamentoElegido()).toBe('Nacional');

    component.cerrarTutorial();

    // MUTANTE QUE MATA: el bloque «Reset filters». Devolvía estos tres a fábrica y descartaba,
    // sin decirlo, todo lo que la persona había elegido antes de pedir ayuda.
    expect(component.departamentoElegido()).toBe('ANTIOQUIA');
    expect(component.lenteDeLectura()).toBe('territorios-sonoros');
    expect(component.modoDeDibujo()).toBe('calor');
  });

  it('el botón de ayuda abre el modo explorar, y ese no toca el mapa', () => {
    // LA OTRA FORMA DE PREGUNTAR. Quien llega al botón de ayuda ya está en el mapa y tiene UNA
    // duda: pasar por dieciocho pasos para resolverla es el camino largo, y además le desharía lo
    // que tenía puesto. El modo explorar solo señala.
    component.departamentoElegido.set('ANTIOQUIA');
    component.lenteDeLectura.set('territorios-sonoros');

    component.alternarPanel('tutorial');

    expect(component.modoExplorar()).toBeTrue();
    expect(component.tutorialAbierto()).toBeFalse();
    expect(component.departamentoElegido()).toBe('ANTIOQUIA');
    expect(component.lenteDeLectura()).toBe('territorios-sonoros');
  });

  it('salir del mapa apaga lo que el recorrido dejó encendido', () => {
    // SALIR CON EL RECORRIDO ABIERTO DEJABA EL SITIO ENTERO SIN EL BOTON FLOTANTE. Ese botón se
    // aparta mientras dura un recorrido, y la señal que lo dice es de la aplicación, no del mapa:
    // si el mapa se destruye sin apagarla, se queda encendida en todas las demás páginas.
    const recorrido = TestBed.inject(RecorridoGuiadoService);
    component.abrirElRecorrido();
    expect(recorrido.hayRecorrido()).toBeTrue();

    component.ngOnDestroy();

    expect(recorrido.hayRecorrido()).toBeFalse();
  });

  it('en modo explorar lo señalado no se levanta sobre sus hermanos', () => {
    // ESTO PASO Y SE VE. Lo destacado llevaba `z-[9998]`, que en el recorrido hace falta para
    // quedar por encima del velo blanco. En modo explorar no hay velo, y al señalar el MAPA el
    // lienzo subía sobre los controles flotantes que son hermanos suyos —los tres modos de dibujo
    // y el conmutador de vistas, a `z-950`—: los botones seguían ahí, con opacidad 1, y el mapa los
    // tapaba. Era justo el apartado que más se señala, y señalarlo escondía dos de las
    // herramientas que este modo existe para explicar.
    component.abrirModoExplorar();
    component.senalarApartado('lienzo');

    const clases = component.enfoqueDelTutorial('lienzo');
    expect(clases).toContain('ring-4');
    expect(clases).not.toContain('z-[');
  });

  it('en modo explorar no se desenfoca nada: solo se ilumina lo señalado', () => {
    // BUSCAR CON EL RESTO BORROSO ES IMPOSIBLE, porque lo que hay que encontrar es justo lo que
    // todavía no se está señalando. Es la diferencia con el recorrido, donde sí se atenúa.
    component.abrirModoExplorar();
    component.senalarApartado('lente');

    expect(component.enfoqueDelTutorial('lente')).toContain('ring-4');
    expect(component.enfoqueDelTutorial('filtros')).toBe('');
    expect(component.enfoqueDelTutorial('capas')).toBe('');
  });

  it('cerrarlo dos veces no pisa lo ya devuelto', () => {
    component.departamentoElegido.set('ANTIOQUIA');
    component.alternarPanel('tutorial');
    component.cerrarTutorial();

    // Cerrar con el aspa y cerrar con Escape son dos caminos al mismo método.
    component.departamentoElegido.set('Nacional');
    component.cerrarTutorial();

    expect(component.departamentoElegido()).toBe('Nacional');
  });

  it('no se abre solo encima de quien llega con una consulta hecha', () => {
    component.departamentoElegido.set('ANTIOQUIA');

    component.loadTutorialAuto();

    // MUTANTE QUE MATA: abrirlo igualmente. Tapaba justo lo que la persona venía a ver, y al
    // cerrarlo se lo borraba.
    expect(component.tutorialAbierto()).toBeFalse();
  });

  it('sí se abre solo cuando se llega al mapa sin nada puesto', () => {
    component.loadTutorialAuto();

    expect(component.tutorialAbierto()).toBeTrue();
  });
});
