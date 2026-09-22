import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MapDataService } from '../../../../core/services/map-data.service';
import { MAP_PANEL_IDS } from '../../config/mapLayersConfig';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';

/**
 * EL TABLERO DEL GEOVISOR TRAS LOS SIETE AJUSTES DEL 29 DE AGOSTO DE 2026.
 *
 * Ese día se cambiaron siete cosas del tablero a petición del usuario, y ninguna de
 * ellas tenía una sola prueba mirándola: las 459 existentes siguieron en verde con
 * la pestaña «Capas» borrada, con el paso 2 del tutorial señalando un panel vacío y
 * con «Limpiar» sin nada que limpiar. Verde no significaba correcto; significaba que
 * nadie miraba.
 *
 * Aquí se mide lo que aquellos cambios decidieron, y solo eso. Lo que es estilo
 * —pastillas centradas, la mano del mapa, el anillo de cobertura— no se prueba
 * aquí: se ve. Lo que se prueba es lo que puede quedarse callado y roto.
 *
 * EL JUEGO DE DATOS, QUE ES LO QUE HACE LEGIBLES LAS CIFRAS DE ABAJO:
 *   ANTIOQUIA        4 redes (una de ellas SIN municipio)
 *   ATLÁNTICO        1 red
 *   BOLÍVAR          2 lutieres
 *   VALLE DEL CAUCA  nada
 * Cuatro departamentos en la cartografía, tres con registros, siete registros en dos
 * capas.
 *
 * EL REGISTRO SIN MUNICIPIO NO ES DECORADO. Con todos los registros teniendo
 * municipio, leer `record.municipality` y partir la cadena `meta` por su separador
 * dan exactamente lo mismo, y la prueba que dice medir eso pasa con las dos. Se
 * comprobó: con el municipio sacado de `meta.split(' · ')[0]`
 * las 483 pruebas siguieron en verde. Ese registro es lo único que las separa.
 */
describe('Tablero del geovisor · los siete ajustes', () => {
  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const rawRecord = (id: string, name: string, deptCode: string, deptName: string, extra: Record<string, unknown> = {}) => ({
    id,
    fields: {
      name,
      departamento: deptName,
      deptCode,
      municipio: 'Medellin',
      divipola: '05001',
      descripcion: 'Registro de prueba',
      ...extra,
    },
  });

  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [
        departmentFeature('05', 'ANTIOQUIA'),
        departmentFeature('08', 'ATLANTICO'),
        departmentFeature('13', 'BOLIVAR'),
        departmentFeature('76', 'VALLE DEL CAUCA'),
      ],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [],
    schoolRecords: [],
    marketRecords: [],
    redesRecords: [
      rawRecord('r1', 'Centro Uno', '05', 'Antioquia', { centerType: 'Archivo' }),
      rawRecord('r2', 'Centro Dos', '05', 'Antioquia', { centerType: 'Archivo' }),
      rawRecord('r3', 'Centro Tres', '05', 'Antioquia', { centerType: 'Biblioteca' }),
      rawRecord('r4', 'Centro Cuatro', '08', 'Atlantico', { centerType: 'Archivo' }),
      // SIN MUNICIPIO, y a proposito. Ver la prueba «un registro sin municipio».
      rawRecord('r5', 'Centro Sin Sede', '05', 'Antioquia', { centerType: 'Fonoteca', municipio: '' }),
    ],
    luthierRecords: [
      rawRecord('l1', 'Taller Uno', '13', 'Bolivar', { oficio: 'Cuerdas' }),
      rawRecord('l2', 'Taller Dos', '13', 'Bolivar', { oficio: 'Percusion' }),
    ],
    festivalCounts: {},
    schoolCounts: {},
    marketCounts: {},
  };

  let component: MapaEcosistemicoPageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundle) } },
      ],
    });

    // Sin `detectChanges()`: se mira el modelo de la página, no Leaflet.
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  // -------------------------------------------------------------------------
  // 1. La pestaña «Capas» se fue del panel de control
  // -------------------------------------------------------------------------
  describe('el panel de control ya no tiene pestaña de Capas', () => {
    it('nunca deja la pestaña seleccionada en la que se borró', () => {
      // Si `activeControlTab` devolviera `layers`, el panel se quedaría sin ningún
      // `@if` que le corresponda: en blanco, sin error y sin nada que pulsar.
      for (const panel of [null, MAP_PANEL_IDS.layers, MAP_PANEL_IDS.tutorial, MAP_PANEL_IDS.export, 'inventado']) {
        component.panelActivo.set(panel as any);
        expect(component.activeControlTab()).withContext(String(panel)).not.toBe(MAP_PANEL_IDS.layers);
      }
    });

    it('cae en Territorio, que es la pestaña que quedó primera', () => {
      component.panelActivo.set(null);
      expect(component.activeControlTab()).toBe(MAP_PANEL_IDS.filters);

      component.panelActivo.set(MAP_PANEL_IDS.layers);
      expect(component.activeControlTab()).toBe(MAP_PANEL_IDS.filters);
    });

    it('respeta las dos pestañas que sí existen', () => {
      component.panelActivo.set(MAP_PANEL_IDS.insights);
      expect(component.activeControlTab()).toBe(MAP_PANEL_IDS.insights);

      component.panelActivo.set(MAP_PANEL_IDS.filters);
      expect(component.activeControlTab()).toBe(MAP_PANEL_IDS.filters);
    });
  });

  // -------------------------------------------------------------------------
  // 2. El recorrido señala las capas donde las capas estén
  // -------------------------------------------------------------------------
  //
  // AQUI HABIA TRES PRUEBAS DEL MODELO DE ZONAS, y el modelo de zonas se retiró: atenuaba por
  // columna —`barra`, `control`, `centro`, `lectura`— y una de esas cuatro, `barra`, llevaba desde
  // el 29 de agosto sin existir en la plantilla. El recorrido pasó a señalar apartado por apartado
  // y su comportamiento se comprueba entero en `mapa-tutorial.spec.ts`; repetirlo aquí dejaría dos
  // sitios donde arreglar lo mismo.
  //
  // LO QUE ESTE FICHERO SI TIENE QUE SEGUIR VIGILANDO es lo que motivó aquellas pruebas: que el
  // paso de las capas señale donde las capas estén de verdad. Se han movido dos veces —de la barra
  // superior a la columna izquierda, y de ahí a la derecha— y las dos veces el recorrido se quedó
  // apuntando al sitio anterior.
  describe('el recorrido y las capas', () => {
    it('el paso de las capas destaca el apartado de las capas', () => {
      component.tutorialAbierto.set(true);
      component.pasoDelTutorial.set(1);

      expect(component.enfoqueDelTutorial('capas')).toContain('ring');
      expect(component.enfoqueDelTutorial('derecha')).toBe('');
    });

    it('ningún paso destaca un apartado que la plantilla no dibuja', () => {
      // Las claves que el componente puede destacar tienen que existir como marca en la plantilla.
      // Es la comprobación que habría evitado que `barra` sobreviviera dos semanas a su elemento.
      const dibujados = [
        'izquierda', 'centro', 'derecha', 'grafico',
        'filtros', 'lente', 'leyenda', 'dibujo', 'lienzo', 'vistas', 'capas', 'listado', 'fichas',
        'lecturas', 'figura-departamentos', 'dimension', 'figura-composicion',
      ];

      for (const paso of component.PASOS_DEL_TUTORIAL) {
        if (paso.destacado) {
          expect(dibujados).withContext(`destacado «${paso.destacado}»`).toContain(paso.destacado);
        }
        for (const abierto of paso.abiertos) {
          expect(dibujados).withContext(`abierto «${abierto}»`).toContain(abierto);
        }
      }
    });
  });

  // -------------------------------------------------------------------------
  // 6. «Limpiar» solo se enciende cuando hay algo que limpiar
  // -------------------------------------------------------------------------
  describe('filtrosActivos y limpiarFiltros', () => {
    it('en una vista recién abierta no hay nada que limpiar', () => {
      expect(component.filtrosActivos()).toEqual([]);
    });

    it('reconoce cada uno de los filtros por separado', () => {
      component.departamentoElegido.set('Antioquia');
      expect(component.filtrosActivos()).toContain('Departamento');

      component.limpiarFiltros();
      component.capaActiva.set('Redes de Documentación');
      expect(component.filtrosActivos()).toContain('Capa');

      component.limpiarFiltros();
      component.busquedaDelDirectorio.set('centro');
      expect(component.filtrosActivos()).toContain('Búsqueda');
    });

    it('el territorio sonoro y la práctica YA NO son filtros: son el lente', () => {
      // EL CAMBIO DE FONDO: «una cosa es cómo se representa el mapa,
      // y otra las gafas que me quiero poner para verlo». Un filtro RESTA —dejaba fuera lo no
      // clasificado, sin decirlo— y un lente REAGRUPA: lo enseña todo, ordenado por otro criterio.
      component.lenteDeLectura.set('territorios-sonoros');

      // MUTANTE QUE MATA: volver a contarlos como filtro. «Limpiar (1)» sobre un mapa que no ha
      // dejado nada fuera, y un usuario buscando qué le quitaron.
      expect(component.filtrosActivos()).not.toContain('Territorio sonoro');
      expect(component.filtrosActivos()).not.toContain('Práctica');
      expect(component.filtrosActivos()).toEqual([]);
    });

    it('una búsqueda de solo espacios no cuenta como filtro', () => {
      // Escribir y borrar deja la cadena vacía; escribir y borrar dejando un espacio
      // dejaba el botón encendido sin nada que quitar.
      component.busquedaDelDirectorio.set('   ');
      expect(component.filtrosActivos()).toEqual([]);
    });

    it('limpiarFiltros quita TODOS los que filtrosActivos sabe contar', () => {
      // Las dos listas tienen que moverse juntas: si se añade un filtro a una y no
      // a la otra, el botón se queda encendido después de pulsarlo.
      component.departamentoElegido.set('Antioquia');
      component.capaActiva.set('Lutieres');
      component.busquedaDelDirectorio.set('taller');
      expect(component.filtrosActivos().length).toBe(3);

      // EL LENTE NO ES UN FILTRO y por eso no cuenta aquí. Pero «Limpiar» SI lo devuelve a
      // territorial: quien pulsa «Limpiar» espera volver al mapa de partida, no a uno con otras
      // gafas puestas. Son dos cosas distintas y las dos tienen que ser ciertas.
      component.lenteDeLectura.set('practicas');
      component.grupoResaltado.set('Bambuco');
      expect(component.filtrosActivos().length).toBe(3);

      component.limpiarFiltros();

      expect(component.filtrosActivos()).toEqual([]);
      expect(component.lenteDeLectura()).toBe('territorial');
      // «Ningún grupo resaltado» se escribe con la cadena vacía y no con `null`, para que el
      // enlazador de filtros con la URL —el mismo que sirve a las otras cuatro lecturas públicas—
      // lo acepte sin una afirmación de tipo. Un nombre de grupo nunca es cadena vacía.
      expect(component.grupoResaltado()).toBe('');
    });
  });

  // -------------------------------------------------------------------------
  // 4. La vista de gráfico: tres formas y sus indicadores
  // -------------------------------------------------------------------------
  describe('departamentosSinRegistros · cuánto país falta', () => {
    it('nombra los departamentos de la cartografía que no tienen ni un registro', () => {
      const vacios = component.departamentosSinRegistros().map((d: any) => d.etiqueta);

      expect(vacios).toEqual(['VALLE DEL CAUCA']);
    });

    it('no puede salir de la lista de departamentos CON registros', () => {
      // Si se leyera `resumenPorDepartamento` en vez de `cartografia`, esta lista
      // saldría siempre vacía: el resumen solo trae a los que ya aparecen en datos.
      const vacios = component.departamentosSinRegistros();

      expect(vacios.length).toBeGreaterThan(0);
      expect(vacios.every((d: any) => d.etiqueta !== 'ANTIOQUIA')).toBeTrue();
    });

    it('cuadra con la cobertura: los que faltan más los que hay son el total', () => {
      const cobertura = component.coberturaTerritorial();

      expect(cobertura.conRegistros).toBe(3);
      expect(cobertura.total).toBe(4);
      expect(component.departamentosSinRegistros().length).toBe(cobertura.total - cobertura.conRegistros);
    });
  });

  describe('indicadoresDelGrafico · la cifra es también el selector', () => {
    it('los indicadores corresponden a las lecturas, y «el cruce» sólo aparece con un lente puesto', () => {
      // Sin lente no hay grupos que cruzar: la matriz sería el ranking de departamentos escrito de
      // otra forma. MUTANTE QUE MATA: ofrecer el indicador igualmente. Llevaría a una lectura vacía,
      // y un indicador que no lleva a nada enseña a desconfiar de los otros cinco.
      expect(component.indicadoresDelGrafico().map((i) => i.id))
        .toEqual(['departamentos', 'composicion', 'municipios', 'calendario', 'cobertura', 'sin-situar']);

      component.lenteDeLectura.set('territorios-sonoros');

      expect(component.indicadoresDelGrafico().map((i) => i.id))
        .toEqual(['departamentos', 'composicion', 'cruce', 'municipios', 'calendario', 'cobertura', 'sin-situar']);
    });

    it('quitarse el lente mientras se mira el cruce no deja la vista en blanco', () => {
      component.lenteDeLectura.set('practicas');
      component.elegirLectura('cruce');

      component.lenteDeLectura.set('territorial');
      component.asegurarLecturaValida();

      // MUTANTE QUE MATA: no devolver la lectura. El indicador se retira de la franja y el `@if` de
      // abajo deja de cumplirse: quedan las cifras y nada debajo, sin nada encendido con lo que salir.
      expect(component.formaDelGrafico()).toBe('composicion');
    });

    it('cada id vale para escribir en formaDelGrafico', () => {
      // Si un id dejara de coincidir, pulsar ese indicador dejaría la vista sin
      // ninguna lectura dibujada: los cinco `@if` fallarían a la vez y la pantalla
      // quedaría con las cifras y nada debajo.
      for (const indicador of component.indicadoresDelGrafico()) {
        component.formaDelGrafico.set(indicador.id);
        expect(component.formaDelGrafico()).toBe(indicador.id);
      }
    });

    it('el indicador de cobertura dice el porcentaje que calcula coberturaTerritorial', () => {
      const cobertura = component.indicadoresDelGrafico().find((i) => i.id === 'cobertura');

      expect(cobertura?.valor).toBe('75 %');
      expect(cobertura?.pie).toBe('3 de 4 departamentos');
    });
  });

  // -------------------------------------------------------------------------
  // 5. La vista de tabla es un listado con las variables en columnas
  // -------------------------------------------------------------------------
  describe('filasDelListado · una variable por columna', () => {
    it('saca el municipio del registro, y lo deja vacío cuando el registro no lo trae', () => {
      // ESTA ES LA PRUEBA QUE OBLIGA A LEER EL CAMPO. `meta` es una cadena ya unida
      // donde el municipio va primero CUANDO EXISTE; en «Centro Sin Sede» no existe y
      // el primer trozo es el tipo de centro. Partir la cadena pondría «Fonoteca» en
      // la columna Municipio, que es un dato falso con aspecto de dato bueno.
      component.seleccionarCapa('Redes de Documentación');
      const filas = component.filasDelListado();
      const sinSede = filas.find((f: any) => f.name === 'Centro Sin Sede');
      const conSede = filas.find((f: any) => f.name === 'Centro Uno');

      expect(conSede?.municipio).toBe('Medellin');
      expect(sinSede).withContext('el registro sin municipio tiene que estar en la lista').toBeTruthy();
      expect(sinSede?.municipio).toBe('');
      expect(sinSede?.detalle).toBe('Fonoteca');
    });

    it('no repite el municipio en la columna de datos principales', () => {
      // El municipio venía primero dentro de `meta`; dejarlo ahí lo pintaba dos
      // veces en la misma fila, en dos columnas contiguas.
      //
      // SE FILTRAN LAS FILAS SIN MUNICIPIO, y no por comodidad: `'algo'.includes('')`
      // es `true` para cualquier cadena, así que la fila de «Centro Sin Sede» hacía
      // fallar esta prueba diga lo que diga el código. Medido
      // al añadir ese registro al juego de datos.
      const conMunicipio = component.filasDelListado().filter((f: any) => f.municipio);

      expect(conMunicipio.length).toBeGreaterThan(0);
      expect(conMunicipio.every((f: any) => !f.detalle.includes(f.municipio))).toBeTrue();
    });

    it('conserva lo demás de meta, que es lo que la columna tiene que decir', () => {
      component.seleccionarCapa('Redes de Documentación');
      const fila = component.filasDelListado().find((f: any) => f.name === 'Centro Uno');

      expect(fila?.detalle).toBe('Archivo');
    });

    it('respeta el paginado: no lista más de lo que el botón «ver más» ha pedido', () => {
      component.limiteDelDirectorio.set(2);

      expect(component.filasDelListado().length).toBe(2);
    });
  });
});
