import * as MapDomain from './map-domain';

/**
 * Donde se coloca la tarjeta de procesos de un territorio.
 *
 * POR QUE EXISTE. Pulsar un municipio abria un modal de 560 px centrado sobre un
 * velo con desenfoque: para leer los procesos de Amalfi habia que perder de vista Amalfi
 * y el mapa entero. El criterio es este: «la ventana emergente
 * cuando de click no debe quitar el mapa, debe salir mas pequena alli justo al lado».
 *
 * POR QUE ESTE FICHERO EXISTE, HABIENDO YA UNA PRUEBA EN EL COMPONENTE. La prueba
 * `ubica la tarjeta al lado del punto y cambia de lado antes de salir del viewport`
 * —heredada del trabajo aparcado el 25 de agosto— NO comprueba la segunda mitad de su
 * propio titulo. Elige el punto `window.innerWidth - 2`, tan pegado al borde que la
 * tarjeta se va a la izquierda tanto si se compara su ancho completo como si se compara
 * solo su borde izquierdo. Comprobado: con `cabeADerecha` mutado a mirar solo el borde
 * izquierdo, las 450 pruebas siguieron en verde.
 *
 * Lo mismo pasa con `top`: afirma `>= 10` en el centro vertical del viewport, donde
 * cualquier implementacion lo cumple sin recortar nada.
 *
 * Aqui se miden los casos donde las dos versiones se separan.
 */
describe('territoryPopupPlacement · la tarjeta sale al lado y entera', () => {
  const ANCHO = MapDomain.TERRITORY_POPUP_WIDTH;
  const ALTO = MapDomain.TERRITORY_POPUP_HEIGHT;
  const HUECO = MapDomain.TERRITORY_POPUP_GAP;
  const MARGEN = MapDomain.TERRITORY_POPUP_MARGIN;

  const W = 1400;
  const H = 900;

  it('por defecto sale a la derecha del punto, separada por el hueco exacto', () => {
    const p = MapDomain.territoryPopupPlacement(220, 450, W, H);

    expect(p.side).toBe('right');
    expect(p.left).toBe(220 + HUECO);
  });

  it('cambia de lado cuando NO CABE ENTERA, no cuando ya se ha salido', () => {
    // Este es el caso que la prueba del componente no llega a tocar. En x = W - 100 el
    // borde izquierdo de la tarjeta cabria de sobra —quedan 86 px—, pero la tarjeta mide
    // 232 y se saldria 246 px por la derecha. Comparar solo el borde izquierdo la deja
    // medio fuera de pantalla en todo el flanco oriental del mapa.
    const x = W - 100;
    expect(x + HUECO).toBeLessThanOrEqual(W - MARGEN);          // el borde izquierdo cabe
    expect(x + HUECO + ANCHO).toBeGreaterThan(W - MARGEN);      // la tarjeta entera no

    const p = MapDomain.territoryPopupPlacement(x, 450, W, H);

    expect(p.side).toBe('left');
    expect(p.left + ANCHO).toBeLessThanOrEqual(x - HUECO);
  });

  it('el ultimo punto que todavia cabe a la derecha se queda a la derecha', () => {
    // La frontera, por el otro lado: un pixel mas y se va a la izquierda. Sin esta, un
    // `cabeADerecha` siempre falso —que mandaria todo a la izquierda— pasaria la anterior.
    const x = W - MARGEN - ANCHO - HUECO;

    expect(MapDomain.territoryPopupPlacement(x, 450, W, H).side).toBe('right');
    expect(MapDomain.territoryPopupPlacement(x + 1, 450, W, H).side).toBe('left');
  });

  it('no se sale por arriba aunque el punto este pegado al borde superior', () => {
    // Centrar la tarjeta en un punto a 5 px del techo la pondria en -138.
    const p = MapDomain.territoryPopupPlacement(700, 5, W, H);

    expect(p.top).toBe(MARGEN);
  });

  it('no se sale por abajo aunque el punto este pegado al borde inferior', () => {
    const p = MapDomain.territoryPopupPlacement(700, H - 5, W, H);

    expect(p.top).toBe(H - ALTO - MARGEN);
    expect(p.top + ALTO).toBeLessThanOrEqual(H - MARGEN);
  });

  it('no se sale por la izquierda cuando el punto esta en el borde occidental', () => {
    // En x = 0 cabe a la derecha, asi que este caso solo se da si el viewport es
    // estrecho: entonces `side` es 'left' y el calculo daria -246.
    const estrecho = MapDomain.territoryPopupPlacement(0, 450, 240, H);

    expect(estrecho.left).toBeGreaterThanOrEqual(MARGEN);
  });

  it('en cualquier punto del viewport la tarjeta cabe entera dentro', () => {
    // El barrido es la unica forma de afirmar «nunca se sale» sin elegir a mano los
    // puntos que convienen.
    for (let x = 0; x <= W; x += 25) {
      for (let y = 0; y <= H; y += 25) {
        const p = MapDomain.territoryPopupPlacement(x, y, W, H);
        expect(p.left).withContext(`x=${x} y=${y}`).toBeGreaterThanOrEqual(MARGEN);
        expect(p.left + ANCHO).withContext(`x=${x} y=${y}`).toBeLessThanOrEqual(W - MARGEN);
        expect(p.top).withContext(`x=${x} y=${y}`).toBeGreaterThanOrEqual(MARGEN);
        expect(p.top + ALTO).withContext(`x=${x} y=${y}`).toBeLessThanOrEqual(H - MARGEN);
      }
    }
  });

  it('nunca tapa el punto que se acaba de pulsar', () => {
    // La queja original en una linea: la ventana no debe quitar el mapa. Mientras haya
    // sitio a un lado u otro, el punto queda FUERA del rectangulo de la tarjeta.
    for (let x = 300; x <= W - 300; x += 25) {
      const p = MapDomain.territoryPopupPlacement(x, 450, W, H);
      const tapaElPunto = x >= p.left && x <= p.left + ANCHO;
      expect(tapaElPunto).withContext(`x=${x}, tarjeta en ${p.left}`).toBeFalse();
    }
  });

  it('un punto que no es un numero cae en el centro y no en NaN', () => {
    // `clientX` puede no existir: la tarjeta tambien se abre desde `repositionTerritoryPopup`
    // sin evento de raton. Un NaN aqui saldria como `left: NaN` en el estilo y la tarjeta
    // se pintaria en la esquina superior izquierda, encima del panel de control.
    for (const malo of [NaN, undefined, null, 'x']) {
      const p = MapDomain.territoryPopupPlacement(malo as any, malo as any, W, H);
      expect(Number.isFinite(p.left)).withContext(String(malo)).toBeTrue();
      expect(Number.isFinite(p.top)).withContext(String(malo)).toBeTrue();
    }
  });
});
