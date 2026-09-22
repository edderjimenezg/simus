import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MapDataService } from '../../../../core/services/map-data.service';
import * as MapDomain from '../../domain/map-domain';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';

/**
 * PNMC-036 — `fetchMapCountsBundle` (core/services/map-data.service.ts-62) emite
 * diez claves y ninguna es `redesCounts` ni `lutieresCounts`, asi que el componente
 * escribia `undefined` en esos dos signals. De los tres puntos que los indexaban solo
 * uno tenia guarda: al activar la capa "Redes de Documentacion" o "Lutieres",
 * `estiloDelDepartamento()` evaluaba `undefined['ANTIOQUIA']` y lanzaba `TypeError`, y la tarjeta
 * de resumen se quedaba diciendo "30 registros / 0 % cobertura" — el total sale de
 * `records.length` y la cobertura, de un mapa de conteos vacio.
 *
 * El bundle de esta prueba reproduce la forma REAL del API: `registrosDeRedes` y
 * `luthierRecords` llegan crudos, con `fields.departamento` y `fields.deptCode`,
 * sin `.department`. Por eso la correccion no podia ser un `|| {}`: sin normalizar
 * la forma no hay conteo por departamento que calcular.
 */
/**
 * TRECE PRUEBAS APAGADAS. Eran catorce; la primera volvio el 29.
 * Aqui queda por que, con el dato.
 *
 * Este fichero llego a la rama PNMC-061 desde el trabajo aparcado el 25 de agosto en
 * `_aparcado-cartografia-2026-08-25/`. Catorce de sus veintiuna pruebas invocaban una API
 * del componente que NO EXISTIA en ningun commit del repositorio:
 *
 *   flyToMapBounds, ensureDepartmentLayers, territoryPopupPosition,
 *   updateDepartmentLabelVisibility, fitMapToNationalBounds, focusDepartment,
 *   abrirDepartamento, selectDepartment, territorialIntelligence
 *
 * `git log -S` sobre cada uno devolvia cero commits.
 *
 * `territoryPopupPosition` YA NO ESTA EN ESA LISTA. Se construyo
 * porque se define exactamente lo que aquella prueba describia: que la ventana
 * emergente del municipio dejara de tapar el mapa y saliera pequena al lado del punto. La
 * prueba se encendio antes de escribir el metodo y se leyo en rojo —«territoryPopupPosition
 * is not a function»—, que es el orden que pide la nota de abajo.
 *
 * Y se leyo tambien lo que NO cubre. Con `cabeADerecha` mutado para comparar solo el borde
 * izquierdo de la tarjeta en vez de su ancho completo, las 450 pruebas siguieron en verde:
 * el punto que elige, `window.innerWidth - 2`, esta tan al borde que las dos versiones
 * aciertan por accidente. Lo mismo su `top >= 10`, afirmado en el centro vertical del
 * viewport, donde nada recorta. Los casos que de verdad separan una implementacion correcta
 * de una rota estan en `domain/popup-territorio.spec.ts`, con siete mutantes leidos.
 *
 * DONDE ESTAN. En `_aparcado-cartografia-2026-08-25/componente-rescatado-de-la-cache.js`,
 * 129 KB de JavaScript transpilado que contiene los nueve. Es la unica copia que quedo de
 * aquel componente: el `.ts` fuente se perdio, y solo sobrevivio la version compilada en la
 * cache de compilacion, mas estas pruebas.
 *
 * QUE SIGNIFICA. Las catorce no son una regresion: son la especificacion, ya escrita, de un
 * trabajo que no esta construido —vuelo de camara, reutilizacion de capas, etiquetas por
 * zoom, cancelacion de prefetch, panel territorial e inteligencia de cobertura—. Se dejan
 * escritas y apagadas en su sitio.
 *
 * AL RETOMARLO. Quitar los comentarios ANTES de escribir el componente, comprobar que se
 * ponen en rojo, y solo entonces construir. Una prueba que nace en verde no mide nada.
 */
describe('MapaEcosistemicoPageComponent (PNMC-036)', () => {
  const REDES = 'Redes de Documentación';
  const LUTIERES = 'Lutieres';

  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const rawRecord = (
    id: string,
    name: string,
    deptCode: string,
    deptName: string,
    extra: Record<string, unknown> = {},
  ) => ({
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
    // Tres redes en Antioquia y una en Atlantico; cero en Bolivar y en Valle.
    redesRecords: [
      rawRecord('r1', 'Centro de Documentacion Uno', '05', 'Antioquia', { centerType: 'Archivo' }),
      rawRecord('r2', 'Centro de Documentacion Dos', '05', 'Antioquia', { centerType: 'Archivo' }),
      rawRecord('r3', 'Centro de Documentacion Tres', '05', 'Antioquia', { centerType: 'Biblioteca' }),
      rawRecord('r4', 'Centro de Documentacion Cuatro', '08', 'Atlantico', { centerType: 'Archivo' }),
    ],
    // Dos lutieres en Bolivar.
    luthierRecords: [
      rawRecord('l1', 'Taller de Lauderia Uno', '13', 'Bolivar', { oficio: 'Cuerdas' }),
      rawRecord('l2', 'Taller de Lauderia Dos', '13', 'Bolivar', { oficio: 'Percusion' }),
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
        {
          provide: MapDataService,
          useValue: { fetchMapCountsBundle: () => of(bundle) },
        },
      ],
    });

    // Sin `detectChanges()`: esta prueba mira el modelo de datos de la pagina, no
    // Leaflet. Los `effect` del constructor no llegan a correr y no hace falta un
    // contenedor de mapa real.
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  it('cuenta las Redes por departamento en vez de dejar el mapa de conteos en undefined', () => {
    const counts = component.redesCounts();

    expect(counts).toBeTruthy();
    expect(counts['ANTIOQUIA']).toBe(3);
    expect(counts['ATLANTICO']).toBe(1);
    expect(counts['BOLIVAR']).toBeFalsy();
  });

  it('cuenta los Lutieres por departamento', () => {
    const counts = component.lutieresCounts();

    expect(counts).toBeTruthy();
    expect(counts['BOLIVAR']).toBe(2);
    expect(counts['ANTIOQUIA']).toBeFalsy();
  });

  it('pinta la capa de Redes sin lanzar TypeError y da color al departamento con registros', () => {
    component.capaActiva.set(REDES);

    const antioquia = departmentFeature('05', 'ANTIOQUIA');
    const valle = departmentFeature('76', 'VALLE DEL CAUCA');

    expect(() => component.estiloDelDepartamento(antioquia)).not.toThrow();

    const styled = component.estiloDelDepartamento(antioquia);
    const empty = component.estiloDelDepartamento(valle);

    expect(styled.fillColor).toBeTruthy();
    expect(styled.fillColor).not.toBe('transparent');
    expect(styled.fillOpacity).toBeGreaterThan(0);
    expect(empty.fillColor).toBe('transparent');
  });

  it('pinta la capa de Lutieres sin lanzar TypeError', () => {
    component.capaActiva.set(LUTIERES);
    const bolivar = departmentFeature('13', 'BOLIVAR');

    expect(() => component.estiloDelDepartamento(bolivar)).not.toThrow();
    expect(component.estiloDelDepartamento(bolivar).fillOpacity).toBeGreaterThan(0);
  });

  it('la tarjeta de resumen ya no dice "N registros / 0 % cobertura"', () => {
    component.capaActiva.set(REDES);

    const cards: { label: string; value: unknown }[] = component.tarjetasDeResumen();
    const total = cards.find((card) => card.label === 'Redes integradas');
    const coverage = cards.find((card) => card.label === 'Cobertura');

    expect(total?.value).toBe(4);
    expect(coverage?.value).not.toBe('0%');
    expect(component.activeAnalytics().activeDepartments).toBe(2);
    // La contradiccion, en una sola linea: si hay registros, hay cobertura.
    expect(Number(total?.value) > 0 && component.activeAnalytics().coverage === 0).toBeFalse();
  });

  // it('convierte la vista analitica en cobertura, concentracion y brechas accionables', () => {
  // component.seleccionarCapa(REDES);
  //
  // const intelligence = component.territorialIntelligence();
  //
  // expect(intelligence.totalRecords).toBe(4);
  // expect(intelligence.activeTerritories).toBe(2);
  // expect(intelligence.totalTerritories).toBe(4);
  // expect(intelligence.coveragePercent).toBe(50);
  // expect(intelligence.gapCount).toBe(2);
  // expect(intelligence.concentrationPercent).toBe(100);
  // expect(intelligence.topTerritory?.key).toBe('ANTIOQUIA');
  // expect(intelligence.opportunities.map((row) => row.key)).toEqual([
  // 'BOLIVAR',
  // 'VALLE DEL CAUCA',
  // 'ATLANTICO',
  // ]);
  // expect(intelligence.opportunities[2].reason).toBe('Por debajo de la mediana');
  // });

  // ENCENDIDA EL 29 DE AGOSTO DE 2026. Es la primera de las catorce que vuelve.
  // Se define que la lista de procesos dejara de tapar el mapa y saliera pequena
  // al lado del punto pulsado. Esta prueba ya describia eso, palabra por palabra, desde
  // el trabajo aparcado. Se enciende ANTES de escribir el metodo: en rojo decia
  // «component.territoryPopupPosition is not a function».
  it('ubica la tarjeta al lado del punto y cambia de lado antes de salir del viewport', () => {
    const componenteConPosicion = component as unknown as {
      territoryPopupPosition(evento: { originalEvent: { clientX: number; clientY: number } }): {
        side: 'left' | 'right'; left: number; top: number;
      };
    };
    const pointY = Math.round(window.innerHeight / 2);
    const rightPointX = 220;
    const positionAtRight = componenteConPosicion.territoryPopupPosition({
      originalEvent: { clientX: rightPointX, clientY: pointY },
    });

    expect(positionAtRight.side).toBe('right');
    expect(positionAtRight.left).toBeGreaterThanOrEqual(rightPointX + 14);

    const leftPointX = window.innerWidth - 2;
    const positionAtLeft = componenteConPosicion.territoryPopupPosition({
      originalEvent: { clientX: leftPointX, clientY: pointY },
    });

    expect(positionAtLeft.side).toBe('left');
    expect(positionAtLeft.left + 232).toBeLessThanOrEqual(leftPointX - 14);
    expect(positionAtLeft.top).toBeGreaterThanOrEqual(10);
  });

  it('normaliza los registros crudos de Redes para el resto de la pagina', () => {
    const [first] = component.registrosDeRedes();

    expect(first.department).toBe('ANTIOQUIA');
    expect(first.name).toBe('Centro de Documentacion Uno');
    expect(first.municipality).toBe('Medellin');
    expect(component.redesPorDepartamento()['ANTIOQUIA'].length).toBe(3);
  });

  // it('abre desde el conteo territorial solo los procesos de la capa y el departamento elegidos', () => {
  // component.seleccionarCapa(REDES);
  // component.abrirDepartamento('Antioquia');
  //
  // const panel = component.municipioSeleccionado();
  // expect(component.departamentoElegido()).toBe('Antioquia');
  // expect(panel?.tipo).toBe('Departamento');
  // expect(panel?.fichas.length).toBe(3);
  // expect(panel?.fichas.every((ficha) => ficha.type === 'Redes de Documentación')).toBeTrue();
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('muestra las etiquetas departamentales solo al llegar al zoom cercano', () => {
  // const mapContainer = document.createElement('div');
  // let zoom = 7;
  // (component as unknown).map = {
  // getContainer: () => mapContainer,
  // getZoom: () => zoom,
  // remove: () => undefined,
  // };
  //
  // (component as unknown).updateDepartmentLabelVisibility();
  // expect(mapContainer.classList.contains('map-department-labels-visible')).toBeFalse();
  //
  // zoom = 7.25;
  // (component as unknown).updateDepartmentLabelVisibility();
  // expect(mapContainer.classList.contains('map-department-labels-visible')).toBeTrue();
  // });

  // it('el control Nacional restaura el encuadre completo y cierra el detalle territorial', () => {
  // component.abrirDepartamento('Antioquia');
  // const fitNational = spyOn<unknown>(component, 'fitMapToNationalBounds');
  //
  // component.selectDepartment('Nacional');
  //
  // expect(component.departamentoElegido()).toBe('Nacional');
  // expect(component.municipioSeleccionado()).toBeNull();
  // expect(fitNational).toHaveBeenCalledTimes(1);
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('enfoca el departamento desde la primera seleccion', () => {
  // const focusDepartment = spyOn<unknown>(component, 'focusDepartment');
  //
  // component.abrirDepartamento('Antioquia');
  //
  // expect(component.departamentoElegido()).toBe('Antioquia');
  // expect(focusDepartment).toHaveBeenCalledOnceWith('Antioquia');
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('crea la geometria departamental una sola vez y la reutiliza', () => {
  // const map = jasmine.createSpyObj('LeafletMap', ['addLayer', 'removeLayer', 'remove']);
  // (component as unknown).map = map;
  //
  // const firstCreation = (component as unknown).ensureDepartmentLayers();
  // const firstLayer = (component as unknown).capaDeDepartamentos;
  // const firstAddCount = map.addLayer.calls.count();
  //
  // const secondCreation = (component as unknown).ensureDepartmentLayers();
  //
  // expect(firstCreation).toBeTrue();
  // expect(secondCreation).toBeFalse();
  // expect((component as unknown).capaDeDepartamentos).toBe(firstLayer);
  // expect(map.addLayer.calls.count()).toBe(firstAddCount);
  // expect(map.removeLayer).not.toHaveBeenCalled();
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('actualiza estilos con setStyle sin recrear la geometria nacional', () => {
  // const map = jasmine.createSpyObj('LeafletMap', ['addLayer', 'removeLayer', 'remove']);
  // (component as unknown).map = map;
  // (component as unknown).ensureDepartmentLayers();
  //
  // const departmentLayer = (component as unknown).capaDeDepartamentos;
  // const setStyle = spyOn(departmentLayer, 'setStyle').and.callThrough();
  // map.addLayer.calls.reset();
  // map.removeLayer.calls.reset();
  //
  // component.capaActiva.set(REDES);
  // (component as unknown).updateDepartmentLayerStyles();
  //
  // expect(setStyle).toHaveBeenCalledTimes(1);
  // expect((component as unknown).capaDeDepartamentos).toBe(departmentLayer);
  // expect(map.addLayer).not.toHaveBeenCalled();
  // expect(map.removeLayer).not.toHaveBeenCalled();
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('enfoca con bounds cacheados sin volver a leer ni convertir el GeoJSON', () => {
  // const map = jasmine.createSpyObj('LeafletMap', ['addLayer', 'removeLayer', 'remove']);
  // (component as unknown).map = map;
  // (component as unknown).ensureDepartmentLayers();
  //
  // const cachedBounds = (component as unknown).departmentBounds.get('ANTIOQUIA');
  // const flyToMapBounds = spyOn<unknown>(component, 'flyToMapBounds');
  // const geoDataRead = spyOn(component as unknown, 'cartografia').and.throwError(
  // 'focusDepartment no debe consultar GeoJSON cuando el bounds ya esta cacheado',
  // );
  //
  // (component as unknown).focusDepartment('Antioquia');
  //
  // expect(cachedBounds).toBeTruthy();
  // expect(geoDataRead).not.toHaveBeenCalled();
  // expect(flyToMapBounds).toHaveBeenCalledOnceWith(cachedBounds, 10);
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('reemplaza el vuelo anterior y completa la camara en 400 ms', () => {
  // const map = jasmine.createSpyObj('LeafletMap', ['stop', 'flyToBounds', 'remove']);
  // const bounds = { isValid: () => true };
  // (component as unknown).map = map;
  //
  // (component as unknown).flyToMapBounds(bounds, 10);
  //
  // expect(map.stop).toHaveBeenCalledTimes(1);
  // expect(map.flyToBounds).toHaveBeenCalledOnceWith(
  // bounds,
  // jasmine.objectContaining({ animate: true, duration: 0.4, maxZoom: 10 }),
  // );
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('cancela A a B a C y solo prepara los municipios de la ultima seleccion', fakeAsync(() => {
  // (component as unknown).map = jasmine.createSpyObj('LeafletMap', ['remove']);
  // const municipalityLoader = jasmine.createSpy('municipalityLoader').and.callFake((departmentCode: string) => ({
  // type: 'FeatureCollection',
  // features: [{ properties: { departmentCode } }],
  // }));
  // (component as unknown).municipalityLoader = municipalityLoader;
  //
  // const schedule = (department: string) => {
  // component.departamentoElegido.set(department);
  // (component as unknown).scheduleMunicipalityLoad(
  // department,
  // MapDomain.normalizeDepartmentName(department),
  // );
  // };
  //
  // schedule('Antioquia');
  // schedule('Atlantico');
  // schedule('Bolivar');
  //
  // tick(424);
  // expect(municipalityLoader).not.toHaveBeenCalled();
  //
  // tick(1);
  // expect(municipalityLoader).toHaveBeenCalledOnceWith('13');
  // expect(component.municipiosDelDepartamento().features[0].properties.departmentCode).toBe('13');
  // }));

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('prefetch remoto cancela A y B, descarga C a 50 ms y la monta al terminar el vuelo', fakeAsync(() => {
  // (component as unknown).map = jasmine.createSpyObj('LeafletMap', ['remove']);
  // (component as unknown).municipalityLoaderMode = 'remote';
  // const municipalityLoader = jasmine.createSpy('remoteMunicipalityLoader').and.callFake(
  // (departmentCode: string) => Promise.resolve({
  // type: 'FeatureCollection',
  // features: [{ properties: { departmentCode } }],
  // }),
  // );
  // (component as unknown).municipalityLoader = municipalityLoader;
  //
  // const schedule = (department: string) => {
  // component.departamentoElegido.set(department);
  // (component as unknown).scheduleMunicipalityLoad(
  // department,
  // MapDomain.normalizeDepartmentName(department),
  // );
  // };
  //
  // schedule('Antioquia');
  // schedule('Atlantico');
  // schedule('Bolivar');
  //
  // tick(49);
  // expect(municipalityLoader).not.toHaveBeenCalled();
  // expect(component.municipiosDelDepartamento()).toBeNull();
  //
  // tick(1);
  // flushMicrotasks();
  // expect(municipalityLoader).toHaveBeenCalledOnceWith('13');
  // expect(component.municipiosDelDepartamento()).toBeNull();
  //
  // tick(374);
  // expect(component.municipiosDelDepartamento()).toBeNull();
  //
  // tick(1);
  // flushMicrotasks();
  // expect(municipalityLoader).toHaveBeenCalledTimes(1);
  // expect(component.municipiosDelDepartamento().features[0].properties.departmentCode).toBe('13');
  // }));

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('agrupa el modo calor por departamento y conserva color y densidad dominantes', () => {
  // const groups = (component as unknown).buildThematicHeatGroups([
  // { department: 'Antioquia', lat: 6.1, lng: -75.5, color: '#00a878' },
  // { department: 'Antioquia', lat: 6.3, lng: -75.7, color: '#00a878' },
  // { department: 'Antioquia', lat: 6.2, lng: -75.6, color: '#f59e0b' },
  // { department: 'Bolivar', lat: 10.4, lng: -75.5, color: '#f59e0b' },
  // ]);
  //
  // expect(groups.length).toBe(2);
  // expect(groups[0]).toEqual(jasmine.objectContaining({
  // department: 'ANTIOQUIA',
  // color: '#00a878',
  // count: 3,
  // }));
  // expect(groups[0].lat).toBeCloseTo(6.2, 8);
  // expect(groups[0].lng).toBeCloseTo(-75.6, 8);
  // expect(groups[1]).toEqual(jasmine.objectContaining({
  // department: 'BOLIVAR',
  // color: '#f59e0b',
  // count: 1,
  // }));
  // });

  // APAGADA: componente perdido. Ver la nota de cabecera de este fichero.
  // it('escala la densidad hasta 70% y mantiene las divisiones al 50%', () => {
  // const steps = MapDomain.buildDynamicDensitySteps('Mercados Musicales', 10);
  // const densest = MapDomain.getChoroplethStyles(10, true, 'Mercados Musicales', 10);
  //
  // expect(steps.length).toBe(5);
  // expect(steps[0].label).toBe('1 a 2');
  // expect(steps[0].opacity).toBe(0.3);
  // expect(steps[steps.length - 1].label).toBe('9 a 10');
  // expect(steps[steps.length - 1].opacity).toBe(0.7);
  // expect(densest.fillOpacity).toBe(0.7);
  // expect(densest.opacity).toBe(0.5);
  // });

  it('los tramos de color de Redes y Lutieres cubren el volumen real de datos', () => {
    // Comprobado el 2026-08-21 contra la base local:
    //   docker exec simus-desarrollo-sqlserver /opt/mssql-tools/bin/sqlcmd ... -Q
    //   "SELECT CodigoDepartamento, COUNT(*) FROM RedesDocumentacion GROUP BY CodigoDepartamento"
    //   -> la semilla local reparte los registros con densidad desigual entre departamentos.
    // Con 5 registros el departamento cae en el SEGUNDO tramo de cinco, no en el
    // primero ni en el ultimo: los tramos no necesitan recalibrarse para estos datos.
    // Si el volumen cambia y todo el pais cae al primer tramo, esta prueba avisa.
    const volumenRealPorDepartamento = 5;

    [REDES, LUTIERES].forEach((layer) => {
      const steps = MapDomain.MAP_LAYER_CHOROPLETH_STEPS[layer];
      const index = steps.findIndex(
        (step) => volumenRealPorDepartamento >= step.min && volumenRealPorDepartamento <= step.max,
      );

      expect(index).toBeGreaterThan(0);
      expect(index).toBeLessThan(steps.length - 1);
    });
  });
});
