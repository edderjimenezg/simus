import { ubicarLaTarjeta } from './ubicacion-del-tutorial';

/**
 * Dónde se coloca la tarjeta del recorrido guiado.
 *
 * <b>SE COMPRUEBA CON NUMEROS Y NO MONTANDO EL GEOVISOR.</b> Es aritmética de rectángulos: entra la
 * caja del elemento y la de la ventana, sale una posición. Las reglas que importan —no salirse de
 * la pantalla, no taparse el elemento que explica, no montarse sobre la cabecera— se pueden fijar
 * aquí sin arrancar un mapa.
 */
describe('ubicarLaTarjeta', () => {
  const TARJETA = { ancho: 380, alto: 250 };
  const VENTANA = { ancho: 1440, alto: 900, topSeguro: 88 };

  it('un elemento de la columna izquierda recibe la tarjeta a su derecha', () => {
    // La columna de controles mide 300 px: a su derecha quedan 1.100 y a su izquierda, nada.
    const ubicacion = ubicarLaTarjeta(
      { top: 300, left: 20, ancho: 260, alto: 120 }, TARJETA, VENTANA);

    expect(ubicacion.lado).toBe('derecha');
    expect(ubicacion.left).toBeGreaterThan(280);
  });

  it('un elemento de la columna derecha recibe la tarjeta a su izquierda', () => {
    const ubicacion = ubicarLaTarjeta(
      { top: 160, left: 1120, ancho: 300, alto: 100 }, TARJETA, VENTANA);

    expect(ubicacion.lado).toBe('izquierda');
    expect(ubicacion.left + TARJETA.ancho).toBeLessThanOrEqual(1120);
  });

  it('nunca se sale de la ventana, ni con el elemento pegado a un borde', () => {
    for (const elemento of [
      { top: 0, left: 0, ancho: 40, alto: 40 },
      { top: 860, left: 1400, ancho: 40, alto: 40 },
      { top: 400, left: 700, ancho: 900, alto: 800 },
    ]) {
      const ubicacion = ubicarLaTarjeta(elemento, TARJETA, VENTANA);

      expect(ubicacion.left).withContext('borde izquierdo').toBeGreaterThanOrEqual(0);
      expect(ubicacion.top).withContext('borde superior').toBeGreaterThanOrEqual(0);
      expect(ubicacion.left + TARJETA.ancho).withContext('borde derecho').toBeLessThanOrEqual(VENTANA.ancho);
      expect(ubicacion.top + TARJETA.alto).withContext('borde inferior').toBeLessThanOrEqual(VENTANA.alto);
    }
  });

  it('no se monta sobre la cabecera del sitio', () => {
    // LA CABECERA ES `fixed` Y MIDE UNOS 80 px. El conmutador de vistas sube a la esquina superior
    // sobre el gráfico, y sin área segura la tarjeta se sujetaba a 12 px del borde: quedaba encima
    // del menú del sitio, medido en navegador.
    const ubicacion = ubicarLaTarjeta(
      { top: 100, left: 320, ancho: 250, alto: 44 }, TARJETA, VENTANA);

    expect(ubicacion.top).toBeGreaterThanOrEqual(VENTANA.topSeguro);
  });

  it('la flecha apunta al centro del elemento, no al de la tarjeta', () => {
    // Un elemento ALTO —una columna entera— con la tarjeta sujetada arriba: si la flecha saliera
    // del centro de la tarjeta señalaría un sitio donde no hay nada.
    const elemento = { top: 120, left: 20, ancho: 260, alto: 700 };
    const ubicacion = ubicarLaTarjeta(elemento, TARJETA, VENTANA);

    const centroDelElemento = elemento.top + elemento.alto / 2;
    expect(ubicacion.top + ubicacion.flecha).toBeCloseTo(centroDelElemento, 0);
  });

  it('la flecha se queda dentro de la tarjeta aunque el elemento esté lejos', () => {
    // Con la tarjeta sujetada contra un borde, el centro del elemento puede caer fuera de ella.
    // La flecha se queda dentro: una punta saliendo por la esquina redondeada no se lee.
    const ubicacion = ubicarLaTarjeta(
      { top: 880, left: 20, ancho: 260, alto: 20 }, TARJETA, VENTANA);

    expect(ubicacion.flecha).toBeGreaterThanOrEqual(20);
    expect(ubicacion.flecha).toBeLessThanOrEqual(TARJETA.alto - 20);
  });

  it('en una ventana estrecha se coloca debajo en vez de a un lado', () => {
    // A 390 px no caben 380 de tarjeta más separación a ningún lado de nada.
    const ubicacion = ubicarLaTarjeta(
      { top: 200, left: 16, ancho: 358, alto: 90 },
      { ancho: 340, alto: 230 },
      { ancho: 390, alto: 844, topSeguro: 88 },
    );

    expect(['abajo', 'arriba']).toContain(ubicacion.lado);
    expect(ubicacion.left).toBeGreaterThanOrEqual(0);
    expect(ubicacion.left + 340).toBeLessThanOrEqual(390);
  });
});
