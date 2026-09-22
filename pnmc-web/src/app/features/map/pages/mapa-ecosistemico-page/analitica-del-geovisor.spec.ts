import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MapDataService } from '../../../../core/services/map-data.service';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';
import { SEPARADOR_DE_CLASIFICACION } from '../../domain/map-domain';

/** Un registro que declara varias clasificaciones, unidas como las une el servicio del mapa. */
const varias = (...valores: string[]) => valores.join(SEPARADOR_DE_CLASIFICACION);

/**
 * La vista de gráfico: cinco lecturas, y ninguna repite lo que ya está en pantalla.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> La vista tenía tres formas y una de ellas había dejado de
 * decir nada: «Reparto por capa» repartía el total entre las cinco capas del geovisor, de las que
 * cuatro corresponden a tablas que retiró `V20260904_01`. Llegaban siempre a cero, el filtro de
 * «mayor que cero» las descartaba, y lo que se pintaba era una barra apilada de UN segmento al
 * 100 %. Nada estaba roto y nada avisaba: simplemente el eje por el que se repartía había dejado de
 * existir. Eso es lo que estas pruebas vigilan, cada una sobre la lectura que la sustituye.
 *
 * <b>SIN `detectChanges()`</b>, que es la convención de este mapa: renderizar monta Leaflet de
 * verdad y sus escuchas sobre `document` sobreviven a la suite y rompen otra distinta. Se mira el
 * modelo de la página.
 *
 * EL JUEGO DE DATOS, que es lo que hace legibles las cifras de abajo:
 *   f1  ANTIOQUIA · Medellín    · Joropo                          · Música de cuerdas
 *   f2  ANTIOQUIA · Medellín    · Joropo + «Cantos, Pitos y Tambores» · (no declara)
 *   f3  ATLANTICO · Barranquilla· (no declara)                    · (no declara)
 * Tres festivales, dos departamentos, dos municipios. La suma por territorios sonoros da CUATRO
 * sobre TRES fichas, que es justo el solape que hay que saber contar.
 *
 * EL SEGUNDO TERRITORIO DE f2 LLEVA COMA A PROPOSITO. Es el nombre real de uno de los catorce
 * territorios del Plan, y es lo que destapó el defecto: el servicio unía con `', '` y el geovisor
 * separaba por `,`, así que siete de los catorce se partían en grupos que no existen. Con tres
 * registros de nombre corto nunca se vio.
 */
describe('la analítica del geovisor', () => {
  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const festival = (
    id: string, name: string, deptName: string, municipio: string,
    territorios: string, practicas: string,
  ) => ({
    id,
    fields: {
      name, dpt: deptName, departamento: deptName, municipio,
      desc: 'Un festival de prueba', organizador: 'Fundación de prueba', sitio_web: '',
      coverageLevel: 'municipal',
      'Prácticas musicales': practicas,
      'Territorios sonoros': territorios,
    },
  });

  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [
        departmentFeature('05', 'ANTIOQUIA'),
        departmentFeature('08', 'ATLANTICO'),
        departmentFeature('76', 'VALLE DEL CAUCA'),
      ],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [
      festival('f1', 'Festival del Río', 'Antioquia', 'Medellin', 'Joropo', 'Música de cuerdas'),
      festival('f2', 'Festival de la Montaña', 'Antioquia', 'Medellin',
        varias('Joropo', 'Cantos, Pitos y Tambores'), ''),
      festival('f3', 'Festival del Mar', 'Atlantico', 'Barranquilla', '', ''),
    ],
    agendaRecords: [],
    schoolRecords: [], marketRecords: [], redesRecords: [], luthierRecords: [],
    festivalCounts: {}, agendaCounts: {}, schoolCounts: {}, marketCounts: {},
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
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
    component.capaActiva.set('Festivales');
  });

  describe('composición · de qué está hecho el ecosistema', () => {
    it('reparte por lo que los registros DECLARAN, no por capas que ya no existen', () => {
      const composicion = component.composicionDelEcosistema();

      // MUTANTE QUE MATA: leer el género o la descripción, que es lo que hacía la versión anterior
      // de la clasificación. Ninguno de los tres festivales lleva «Joropo» en su descripción.
      expect(composicion.filas.map((fila) => fila.nombre))
        .toEqual(['Joropo', 'Cantos, Pitos y Tambores', 'Sin clasificar']);
      expect(composicion.filas.map((fila) => fila.total)).toEqual([2, 1, 1]);
    });

    it('un territorio cuyo nombre lleva coma NO se parte en dos', () => {
      const nombres = component.composicionDelEcosistema().filas.map((fila) => fila.nombre);

      // MUTANTE QUE MATA: volver a separar por `,`. «Cantos, Pitos y Tambores» se convertiría en
      // «Cantos» y «Pitos y Tambores», dos grupos que no están en el catálogo, y el lente contaría
      // dieciocho territorios sobre los catorce que el Plan declara. Con datos de prueba de nombre
      // corto esto pasa inadvertido; con el catálogo real rompe la lectura entera.
      expect(nombres).toContain('Cantos, Pitos y Tambores');
      expect(nombres).not.toContain('Cantos');
      expect(nombres).not.toContain('Pitos y Tambores');
    });

    it('«Sin clasificar» va al final aunque empatara: es la ausencia de dato, no un grupo más', () => {
      const nombres = component.composicionDelEcosistema().filas.map((fila) => fila.nombre);

      expect(nombres[nombres.length - 1]).toBe('Sin clasificar');
    });

    it('avisa de que la suma pasa del total, porque un registro cuenta en todos los que declara', () => {
      const composicion = component.composicionDelEcosistema();

      // f2 declara dos territorios sonoros: 2 + 1 + 1 = 4 sobre 3 fichas.
      expect(composicion.fichas).toBe(3);
      expect(composicion.suma).toBe(4);

      // MUTANTE QUE MATA: no avisar. Quien sume las filas y no le cuadre con el total no sabe si el
      // error es suyo o del tablero.
      expect(composicion.solapan).toBeTrue();
    });

    it('la cuota se calcula sobre las fichas y no sobre la suma de declaraciones', () => {
      const joropo = component.composicionDelEcosistema().filas.find((fila) => fila.nombre === 'Joropo');

      // MUTANTE QUE MATA: dividir por `suma`. Daría 50 % en vez de 67 %, y la frase que el número
      // sostiene —«dos de cada tres festivales son de Joropo»— dejaría de ser cierta.
      expect(joropo?.cuota).toBe(67);
    });

    it('la dimensión se elige aparte del lente: mirar prácticas no cambia el color del mapa', () => {
      component.lenteDeLectura.set('territorios-sonoros');
      component.dimensionDeComposicion.set('practicas');

      expect(component.composicionDelEcosistema().filas.map((fila) => fila.nombre))
        .toEqual(['Música de cuerdas', 'Sin clasificar']);

      // MUTANTE QUE MATA: atar la dimensión al lente. Obligaría a deshacer una elección para poder
      // hacer la otra, y el mapa cambiaría de color al mirar una tabla.
      expect(component.lenteDeLectura()).toBe('territorios-sonoros');
    });

    it('el indicador no llama «más declarado» a lo que nadie declaró', () => {
      component.dimensionDeComposicion.set('practicas');
      component.departamentoElegido.set('ATLANTICO');

      const indicador = component.indicadoresDelGrafico().find((item) => item.id === 'composicion');

      // El único festival del Atlántico no declara práctica: el primero de la lista es «Sin
      // clasificar», y rotularlo «Más declarado» invertiría lo que dice el dato.
      expect(indicador?.valor).toBe('Sin clasificar');
      expect(indicador?.etiqueta).toBe('Mayoría sin clasificar');
    });
  });

  describe('municipios · el ranking largo', () => {
    it('cuenta por municipio y desambigua por departamento', () => {
      const filas = component.rankingMunicipal();

      expect(filas.map((fila) => `${fila.municipio} (${fila.departamento}): ${fila.total}`))
        .toEqual(['Medellin (ANTIOQUIA): 2', 'Barranquilla (ATLANTICO): 1']);
    });

    it('no depende del catálogo de coordenadas: sale completo aunque no se pueda situar nada', () => {
      // MUTANTE QUE MATA: derivarlo de `procesosPorMunicipio`, que necesita el catálogo. Sin él la
      // lista saldría vacía y parecería que ninguna ficha declara municipio, que es otra cosa.
      expect(component.puntosMunicipales()).toBeNull();
      expect(component.rankingMunicipal().length).toBe(2);
    });
  });

  describe('sin situar · calidad del dato, con la causa y su responsable', () => {
    it('separa las tres causas en vez de fundirlas en «N sin ubicar»', () => {
      const calidad = component.procesosSinSituar();

      // Sin catálogo cargado, las tres fichas caen en la causa del CATALOGO y no en la de la ficha.
      // MUTANTE QUE MATA: contarlas como «la ficha no dice el municipio». Mandaría a corregir tres
      // fichas que están bien.
      expect(calidad.causas.map((causa) => causa.clave)).toEqual(['catalogo']);
      expect(calidad.causas[0].cifra).toBe(3);
      expect(calidad.sinSituar).toBe(3);
    });

    it('sólo enumera las causas que de verdad tienen cifra', () => {
      // MUTANTE QUE MATA: listar las tres siempre. Dos tarjetas en cero al lado de una con cifra
      // hacen pensar que hay tres problemas donde hay uno.
      expect(component.procesosSinSituar().causas.every((causa) => causa.cifra > 0)).toBeTrue();
    });
  });

  describe('los indicadores son también el selector', () => {
    it('cada id vale para elegir su lectura', () => {
      for (const indicador of component.indicadoresDelGrafico()) {
        component.elegirLectura(indicador.id);
        expect(component.formaDelGrafico()).toBe(indicador.id);
      }
    });

    it('abrir «se pueden situar» pide el catálogo que esa lectura va a juzgar', () => {
      expect(component.estadoPuntosMunicipales()).toBe('sin_pedir');

      component.elegirLectura('sin-situar');

      // MUTANTE QUE MATA: dejar la lectura escribiendo sólo la señal. Era el defecto real: la
      // única lectura SOBRE el catálogo municipal era la única que no lo pedía, así que abría,
      // lo encontraba en nulo y concluía «no se pudo cargar» sobre algo que nadie intentó cargar.
      expect(component.estadoPuntosMunicipales()).not.toBe('sin_pedir');
    });
  });

  describe('controles que no eligen nada', () => {
    it('con dos capas consultables, «General» vuelve a ser una elección distinta', () => {
      // ESTA PRUEBA FIJABA LO CONTRARIO, y tenía razón mientras Festivales era la única capa: con
      // una sola, «General» y esa capa enseñaban la misma cifra, los mismos registros y el mismo
      // color, y pulsar una u otra no cambiaba nada. El 15 de septiembre de 2026 Mercados Musicales
      // se encendió, y `capaUnica()` pasa a `null` sola: era justo lo que el código preveía —«el
      // día que vuelva una segunda se enciende sola sin tocar nada»—.
      //
      // MUTANTE QUE MATA: volver a apagar «General» con dos capas. Mirar el conjunto sí es una
      // elección distinta en cuanto hay más de un proceso que mirar.
      expect(component.capasConConteo().length).toBe(2);
      expect(component.capaUnica()).toBeNull();
    });

    it('el municipio no se ofrece cuando el departamento abierto no tiene ninguno con registros', () => {
      component.departamentoElegido.set('VALLE DEL CAUCA');

      // La plantilla condiciona el desplegable a esta lista: vacía, no hay nada entre qué elegir y
      // un «Todos» solitario sólo invita a desplegar algo que no ofrece nada.
      expect(component.municipiosConRegistros()).toEqual([]);
    });
  });

  describe('lo que se dice cuando no hay resultados', () => {
    it('nombra el filtro que vacía el listado, y no una categoría que no se puede cambiar', () => {
      component.departamentoElegido.set('VALLE DEL CAUCA');

      // MUTANTE QUE MATA: el consejo fijo «Intenta cambiando la categoría o ajustando los términos
      // de búsqueda». Con una sola capa no hay categoría que cambiar y sin nada escrito no hay
      // términos que ajustar: mandaba a tocar dos controles que no aplicaban y dejaba sin nombrar
      // el filtro que de verdad estaba vaciando el listado.
      expect(component.consejoSinResultados()).toContain('departamento');
    });

    it('si hay búsqueda, la búsqueda va primero: es lo último que se tocó', () => {
      component.departamentoElegido.set('VALLE DEL CAUCA');
      component.busquedaDelDirectorio.set('gaita');

      expect(component.consejoSinResultados()).toContain('«gaita»');
    });

    it('sin ningún filtro puesto no sugiere maniobras: dice que no hay registros', () => {
      expect(component.consejoSinResultados()).toBe('Todavía no hay registros publicados en esta lectura.');
    });
  });
});
