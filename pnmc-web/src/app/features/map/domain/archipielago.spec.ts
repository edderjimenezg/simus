import * as MapDomain from './map-domain';

/**
 * El archipiélago de San Andrés se dibuja aumentado a propósito: a escala nacional
 * San Andrés mide 12,7 km y Santa Catalina 1,5 km, así que sin aumentar no se ven
 * ni se pueden pulsar. `buildScaledFeature` es quien lo aumenta.
 *
 * Aumentar no es mover. Esta prueba fija esa frontera, porque se cruzó: la versión
 * anterior calculaba UN solo centro para los seis polígonos y escalaba cada vértice
 * respecto de él, con lo que multiplicaba por 8,5 también la DISTANCIA entre las
 * islas. Medido contra el TopoJSON que sirve el API:
 * San Andrés bajaba 381 km y aparecía pintada encima de Panamá, y Providencia subía
 * 370 km hasta mar abierto. El conjunto pasaba de ocupar 0,91 grados de latitud a
 * ocupar 7,77.
 *
 * Las cifras del accidente geográfico salen de
 * `GET /api/v1/mapa/cartografia/departamentos`, departamento 88.
 */

/** Los seis polígonos del departamento 88, cada uno como su rectángulo envolvente real. */
const ARCHIPIELAGO_REAL = {
  type: 'Feature',
  properties: { dpto_ccdgo: '88' },
  geometry: {
    type: 'MultiPolygon',
    coordinates: [
      rectangulo(-81.689, 12.550, 0.0013, 0.0015), // cayo
      rectangulo(-81.689, 12.553, 0.0003, 0.0004), // cayo
      rectangulo(-81.711, 12.538, 0.0485, 0.1146), // San Andrés
      rectangulo(-81.690, 12.600, 0.0017, 0.0021), // cayo
      rectangulo(-81.373, 13.353, 0.0472, 0.0657), // Providencia
      rectangulo(-81.375, 13.388, 0.0124, 0.0132), // Santa Catalina
    ],
  },
};

function rectangulo(cx: number, cy: number, ancho: number, alto: number): number[][][] {
  const x1 = cx - ancho / 2;
  const x2 = cx + ancho / 2;
  const y1 = cy - alto / 2;
  const y2 = cy + alto / 2;
  return [[[x1, y1], [x2, y1], [x2, y2], [x1, y2], [x1, y1]]];
}

function envolvente(coordenadas: any): { mnx: number; mxx: number; mny: number; mxy: number } {
  const puntos: number[][] = [];
  const recorrer = (nodo: any): void => {
    if (!Array.isArray(nodo)) return;
    if (typeof nodo[0] === 'number') {
      puntos.push(nodo as number[]);
      return;
    }
    nodo.forEach(recorrer);
  };
  recorrer(coordenadas);
  return {
    mnx: Math.min(...puntos.map((p) => p[0])),
    mxx: Math.max(...puntos.map((p) => p[0])),
    mny: Math.min(...puntos.map((p) => p[1])),
    mxy: Math.max(...puntos.map((p) => p[1])),
  };
}

describe('buildScaledFeature · el archipiélago se agranda, no se desparrama', () => {
  const ESCALA = 8.5;

  it('deja cada isla en su sitio: el centro de cada polígono no se mueve', () => {
    const escalado = MapDomain.buildScaledFeature(ARCHIPIELAGO_REAL, ESCALA);

    ARCHIPIELAGO_REAL.geometry.coordinates.forEach((poligonoReal, i) => {
      const antes = envolvente(poligonoReal);
      const despues = envolvente(escalado.geometry.coordinates[i]);

      const centroAntes = [(antes.mnx + antes.mxx) / 2, (antes.mny + antes.mxy) / 2];
      const centroDespues = [(despues.mnx + despues.mxx) / 2, (despues.mny + despues.mxy) / 2];

      expect(centroDespues[0]).toBeCloseTo(centroAntes[0], 6);
      expect(centroDespues[1]).toBeCloseTo(centroAntes[1], 6);
    });
  });

  it('agranda cada isla por el factor pedido, que es para lo que existe', () => {
    const escalado = MapDomain.buildScaledFeature(ARCHIPIELAGO_REAL, ESCALA);

    ARCHIPIELAGO_REAL.geometry.coordinates.forEach((poligonoReal, i) => {
      const antes = envolvente(poligonoReal);
      const despues = envolvente(escalado.geometry.coordinates[i]);

      expect(despues.mxx - despues.mnx).toBeCloseTo((antes.mxx - antes.mnx) * ESCALA, 6);
      expect(despues.mxy - despues.mny).toBeCloseTo((antes.mxy - antes.mny) * ESCALA, 6);
    });
  });

  it('ningún trozo del archipiélago aterriza sobre Panamá ni en mar abierto', () => {
    const escalado = MapDomain.buildScaledFeature(ARCHIPIELAGO_REAL, ESCALA);
    const total = envolvente(escalado.geometry.coordinates);

    // El archipiélago real vive entre 12,48N y 13,39N. Aumentadas las islas 8,5 veces
    // sobre su propio centro, el conjunto no puede pasar de estos limites. Panama esta
    // a 9N: la version anterior llegaba a 9,05N.
    expect(total.mny).toBeGreaterThan(12.0);
    expect(total.mxy).toBeLessThan(14.0);
    expect(total.mnx).toBeGreaterThan(-82.2);
    expect(total.mxx).toBeLessThan(-81.0);
  });

  it('no separa los dos grupos: el alto total sigue siendo el del archipiélago', () => {
    const real = envolvente(ARCHIPIELAGO_REAL.geometry.coordinates);
    const escalado = envolvente(MapDomain.buildScaledFeature(ARCHIPIELAGO_REAL, ESCALA).geometry.coordinates);

    const altoReal = real.mxy - real.mny;
    const altoEscalado = escalado.mxy - escalado.mny;

    // Crece, porque las islas crecen, pero no en proporcion a la escala: la separacion
    // entre San Andres y Providencia —0,81 grados— tiene que quedarse como esta.
    expect(altoEscalado).toBeGreaterThan(altoReal);
    expect(altoEscalado).toBeLessThan(altoReal * 2);
  });

  it('devuelve null si no le dan geometría', () => {
    expect(MapDomain.buildScaledFeature(null)).toBeNull();
    expect(MapDomain.buildScaledFeature({ type: 'Feature' })).toBeNull();
  });
});
