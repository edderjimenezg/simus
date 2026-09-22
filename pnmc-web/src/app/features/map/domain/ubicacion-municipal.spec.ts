import * as MapDomain from './map-domain';

/**
 * DÓNDE SE DIBUJA CADA PROCESO EN EL MODO DE PRÁCTICAS E INFLUENCIA.
 *
 * POR QUE EXISTE, Y ES UN DATO FALSO EN PANTALLA. `thematicPoints` no usaba
 * ninguna posición: calculaba una. Tomaba el centroide del departamento y le sumaba un
 * desplazamiento en espiral cuyo ángulo y radio salían del ÍNDICE DEL REGISTRO EN EL
 * ARRAY (`const angle = (index * 0.72) % (2 * Math.PI)`). Reordenar los datos movía los
 * puntos por el mapa. El mapa afirmaba que un proceso estaba en un sitio donde no está.
 *
 * La regla del proyecto —«el front no inventa datos», documentada en docs/11-operacion-y-mantenimiento.md— tiene una
 * sola excepción, y es esta: lo que está roto AHORA se arregla al encontrarlo.
 *
 * LO QUE SE MIDIÓ ANTES DE DISEÑAR, contra el API en 8081:
 *
 *   · 155 procesos publicados, repartidos en 33 municipios.
 *   · Los 155 traen `municipalityCode`.
 *   · 60 traen además `latitude`/`longitude`… y los 60 son EXACTAMENTE el punto de
 *     referencia de su municipio, con tolerancia 1e-5. Ni uno tiene posición propia.
 *   · `/api/v1/publico/divipola` es anónima y devuelve los 1.122 municipios con
 *     coordenada.
 *
 * De ahí la forma de estas funciones: la resolución honesta del dato de hoy es EL
 * MUNICIPIO, y el código tiene que poder decirlo en vez de fingir un punto exacto.
 * `punto_propio` existe para el día que un registro traiga su sitio de verdad, y hoy
 * devuelve cero, que es lo que corresponde.
 */
describe('Ubicación municipal · dónde se puede afirmar que está un proceso', () => {
  // Puntos reales de `/api/v1/publico/divipola`, copiados tal cual.
  const PUNTOS = new Map<string, { lat: number; lng: number; nombre: string }>([
    ['05001', { lat: 6.28, lng: -75.56, nombre: 'MEDELLÍN' }],
    ['05031', { lat: 6.91, lng: -75.07, nombre: 'AMALFI' }],
    ['76001', { lat: 3.42, lng: -76.52, nombre: 'CALI' }],
  ]);

  const registro = (id: string, municipalityCode: string, extra: Record<string, unknown> = {}) => ({
    id,
    name: 'Proceso ' + id,
    municipalityCode,
    ...extra,
  });

  describe('resolverUbicacionDeRegistro', () => {
    it('sitúa el proceso en el punto de su municipio, sin tocar la coordenada', () => {
      const u = MapDomain.resolverUbicacionDeRegistro(registro('a', '05001'), PUNTOS);

      expect(u.nivel).toBe('punto_municipal');
      expect(u.lat).toBe(6.28);
      expect(u.lng).toBe(-75.56);
    });

    it('no declara «punto propio» cuando la coordenada del registro ES la del municipio', () => {
      // Los 60 registros que hoy traen latitude/longitude traen exactamente esto.
      // Llamarlo «coordenada exacta» sería fingir una precisión que el dato no tiene.
      const u = MapDomain.resolverUbicacionDeRegistro(
        registro('b', '05001', { latitude: 6.28, longitude: -75.56 }),
        PUNTOS,
      );

      expect(u.nivel).toBe('punto_municipal');
    });

    it('declara «punto propio» solo si la coordenada se aparta de verdad del municipio', () => {
      const u = MapDomain.resolverUbicacionDeRegistro(
        registro('c', '05001', { latitude: 6.35, longitude: -75.51 }),
        PUNTOS,
      );

      expect(u.nivel).toBe('punto_propio');
      expect(u.lat).toBe(6.35);
      expect(u.lng).toBe(-75.51);
    });

    it('un nulo en la coordenada no se convierte en el punto cero del golfo de Guinea', () => {
      // `Latitude`/`Longitude` son `decimal?` en el contrato. `Number(null)` es 0, y 0,0
      // es un punto del océano frente a África: un registro colombiano dibujado ahí.
      const u = MapDomain.resolverUbicacionDeRegistro(
        registro('d', '05031', { latitude: null, longitude: null }),
        PUNTOS,
      );

      expect(u.nivel).toBe('punto_municipal');
      expect(u.lat).toBe(6.91);
    });

    it('distingue las tres maneras de no poder situarlo, y no las funde en una', () => {
      // Son fallos distintos y se arreglan en sitios distintos: falta el dato en el
      // registro, falta el municipio en la tabla, o no se pudo cargar la tabla.
      expect(MapDomain.resolverUbicacionDeRegistro(registro('e', ''), PUNTOS).nivel).toBe('sin_codigo');
      expect(MapDomain.resolverUbicacionDeRegistro(registro('f', '99999'), PUNTOS).nivel).toBe('municipio_sin_punto');
      expect(MapDomain.resolverUbicacionDeRegistro(registro('g', '05001'), null).nivel).toBe('catalogo_no_disponible');
    });

    it('nunca cae al centroide del departamento, que es la invención que se retira', () => {
      for (const caso of [registro('h', ''), registro('i', '99999')]) {
        const u = MapDomain.resolverUbicacionDeRegistro(caso, PUNTOS);
        expect(u.lat).withContext(caso.id).toBeNull();
        expect(u.lng).withContext(caso.id).toBeNull();
      }
    });
  });

  describe('agruparProcesosPorMunicipio', () => {
    const REGISTROS = [
      registro('1', '05001'),
      registro('2', '05001'),
      registro('3', '05031'),
      registro('4', '76001'),
      registro('5', ''),
      registro('6', '99999'),
    ];

    it('devuelve un municipio por código, con su punto y su total', () => {
      const { municipios } = MapDomain.agruparProcesosPorMunicipio(REGISTROS, PUNTOS);

      expect(municipios.length).toBe(3);
      const medellin = municipios.find((m) => m.codigo === '05001');
      expect(medellin?.total).toBe(2);
      expect(medellin?.lat).toBe(6.28);
      expect(medellin?.lng).toBe(-75.56);
      expect(medellin?.nombre).toBe('MEDELLÍN');
    });

    it('ni la posición ni el ORDEN de salida dependen del orden de entrada', () => {
      // `angle = (index * 0.72)` hacía que reordenar los datos moviera los puntos por el
      // mapa. Esta prueba es la que aquella implementación no podía pasar.
      //
      // LA SALIDA SE COMPARA SIN VOLVER A ORDENARLA, y esa es la corrección del primer
      // intento: aquella versión hacía `.sort()` sobre las claves antes de comparar, con
      // lo que quitar el `.sort()` del código no la ponía en rojo. Medido con el mutante
      // U4: 513 pruebas en verde con la ordenación borrada. Un orden inestable recrea los
      // marcadores en otra secuencia en cada repintado.
      const alDerecho = MapDomain.agruparProcesosPorMunicipio(REGISTROS, PUNTOS);
      const alReves = MapDomain.agruparProcesosPorMunicipio([...REGISTROS].reverse(), PUNTOS);

      const secuencia = (r: ReturnType<typeof MapDomain.agruparProcesosPorMunicipio>) =>
        r.municipios.map((m) => `${m.codigo}:${m.lat},${m.lng}`).join('|');

      expect(secuencia(alDerecho)).toBe(secuencia(alReves));
      expect(alDerecho.municipios.map((m) => m.codigo)).toEqual(['05001', '05031', '76001']);
    });

    it('cuenta por separado las tres causas de no haber podido situar un proceso', () => {
      const { noSituados } = MapDomain.agruparProcesosPorMunicipio(REGISTROS, PUNTOS);

      expect(noSituados.sinCodigo).toBe(1);
      expect(noSituados.municipioSinPunto).toBe(1);
      expect(noSituados.catalogoNoDisponible).toBe(0);
    });

    it('sin catálogo no sitúa a nadie, y lo dice con su propia causa', () => {
      const { municipios, noSituados } = MapDomain.agruparProcesosPorMunicipio(REGISTROS, null);

      expect(municipios.length).toBe(0);
      expect(noSituados.catalogoNoDisponible).toBe(REGISTROS.length);
      expect(noSituados.sinCodigo).toBe(0);
    });

    it('ningún proceso se pierde ni se cuenta dos veces', () => {
      // La suma de lo dibujado más lo descartado tiene que dar los registros que
      // entraron. Sin esto, la tubería puede tirar registros sin que nadie lo note: es
      // exactamente como el filtro sobre la colección municipal vacía pasó semanas.
      const { municipios, noSituados } = MapDomain.agruparProcesosPorMunicipio(REGISTROS, PUNTOS);
      const dibujados = municipios.reduce((total, m) => total + m.total, 0);
      const descartados = noSituados.sinCodigo + noSituados.municipioSinPunto + noSituados.catalogoNoDisponible;

      expect(dibujados + descartados).toBe(REGISTROS.length);
    });

    it('con la lista vacía no revienta y no inventa municipios', () => {
      const { municipios, noSituados } = MapDomain.agruparProcesosPorMunicipio([], PUNTOS);

      expect(municipios).toEqual([]);
      expect(noSituados.sinCodigo + noSituados.municipioSinPunto + noSituados.catalogoNoDisponible).toBe(0);
    });

    it('guarda las fichas de cada municipio para que el globo pueda nombrarlas', () => {
      const { municipios } = MapDomain.agruparProcesosPorMunicipio(REGISTROS, PUNTOS);
      const medellin = municipios.find((m) => m.codigo === '05001');

      expect(medellin?.fichas.length).toBe(2);
      expect(medellin?.fichas.map((f: { id: string }) => f.id).sort()).toEqual(['1', '2']);
    });
  });
});
