/**
 * Dónde se coloca la tarjeta del recorrido guiado, y hacia dónde apunta su flecha.
 *
 * <b>POR QUE HACIA FALTA.</b> La tarjeta vivía centrada abajo, siempre en el mismo sitio, y el
 * único vínculo entre lo que decía y lo que explicaba era el desenfoque del resto. Con la pantalla
 * llena —tres columnas y una decena de controles— eso no basta: está definido así,
 * «a veces no es tan claro qué es lo que está mostrando». Una tarjeta pegada al elemento, con una
 * flecha que lo señala, no deja lugar a duda.
 *
 * <b>POR QUE ES UN FICHERO APARTE Y SIN SEÑALES.</b> Es aritmética de rectángulos: entra la caja
 * del elemento y la de la ventana, sale una posición. Sin Angular de por medio se puede comprobar
 * con números en vez de montando un componente, que es lo que hace que estas reglas —no salirse de
 * la pantalla, no taparte lo que estás mirando— se puedan fijar en una prueba.
 */

/** Un rectángulo en coordenadas de ventana, como los que da `getBoundingClientRect()`. */
export interface Caja {
  readonly top: number;
  readonly left: number;
  readonly ancho: number;
  readonly alto: number;
}

/** De qué lado del elemento queda la tarjeta; la flecha sale por el lado contrario. */
export type LadoDeLaTarjeta = 'derecha' | 'izquierda' | 'abajo' | 'arriba';

export interface UbicacionDeLaTarjeta {
  readonly top: number;
  readonly left: number;
  readonly lado: LadoDeLaTarjeta;
  /**
   * Dónde va la flecha a lo largo del borde de la tarjeta, en píxeles desde su esquina superior
   * izquierda. Se calcula para que apunte al CENTRO del elemento y no a la esquina de la tarjeta:
   * con elementos altos —una columna entera— la diferencia es de cientos de píxeles.
   */
  readonly flecha: number;
}

/** Cuánto separa la tarjeta del elemento que explica. Deja sitio para la flecha. */
const SEPARACION = 18;

/** Margen mínimo con el borde de la ventana. */
const MARGEN = 12;

/**
 * Coloca la tarjeta junto al elemento.
 *
 * <b>EL ORDEN DE PREFERENCIA NO ES ARBITRARIO.</b> Primero a los lados y después arriba o abajo,
 * porque este tablero es de tres columnas: un elemento de la columna izquierda tiene sitio de sobra
 * a su derecha y casi ninguno debajo. Y entre los dos lados se elige el que tenga más hueco, no
 * siempre el mismo, para que la tarjeta no se salga de la pantalla al explicar la columna derecha.
 *
 * <b>SIEMPRE CABE.</b> Si ningún lado tiene espacio suficiente —una ventana estrecha, un elemento
 * que ocupa casi todo—, se coloca donde más quepa y se sujeta dentro de los márgenes. Una tarjeta
 * medio fuera de la pantalla es peor que una mal colocada.
 */
export function ubicarLaTarjeta(
  elemento: Caja,
  tarjeta: { ancho: number; alto: number },
  /**
   * `topSeguro` es dónde empieza el área utilizable, no dónde empieza la ventana.
   *
   * LA CABECERA DEL SITIO ES `fixed` Y MIDE UNOS 80 px. Sin este dato, la tarjeta de un paso que
   * explica algo de la parte alta —el conmutador de vistas, que sobre el gráfico sube a la
   * esquina— se sujetaba a 12 px del borde y quedaba montada encima de la barra de navegación,
   * pisando el menú del sitio.
   */
  ventana: { ancho: number; alto: number; topSeguro?: number },
): UbicacionDeLaTarjeta {
  const topSeguro = ventana.topSeguro ?? 0;
  const huecoDerecha = ventana.ancho - (elemento.left + elemento.ancho);
  const huecoIzquierda = elemento.left;
  const huecoAbajo = ventana.alto - (elemento.top + elemento.alto);
  const huecoArriba = elemento.top - topSeguro;

  const anchoNecesario = tarjeta.ancho + SEPARACION + MARGEN;
  const altoNecesario = tarjeta.alto + SEPARACION + MARGEN;

  let lado: LadoDeLaTarjeta;
  if (huecoDerecha >= anchoNecesario || huecoIzquierda >= anchoNecesario) {
    lado = huecoDerecha >= huecoIzquierda ? 'derecha' : 'izquierda';
  } else if (huecoAbajo >= altoNecesario || huecoArriba >= altoNecesario) {
    lado = huecoAbajo >= huecoArriba ? 'abajo' : 'arriba';
  } else {
    // NO CABE EN NINGUN LADO. Se elige el mayor de los cuatro y el sujetado hace el resto.
    const mayor = Math.max(huecoDerecha, huecoIzquierda, huecoAbajo, huecoArriba);
    lado = mayor === huecoDerecha ? 'derecha'
      : mayor === huecoIzquierda ? 'izquierda'
      : mayor === huecoAbajo ? 'abajo' : 'arriba';
  }

  const centroY = elemento.top + elemento.alto / 2;
  const centroX = elemento.left + elemento.ancho / 2;

  let top: number;
  let left: number;

  switch (lado) {
    case 'derecha':
      left = elemento.left + elemento.ancho + SEPARACION;
      top = centroY - tarjeta.alto / 2;
      break;
    case 'izquierda':
      left = elemento.left - tarjeta.ancho - SEPARACION;
      top = centroY - tarjeta.alto / 2;
      break;
    case 'abajo':
      left = centroX - tarjeta.ancho / 2;
      top = elemento.top + elemento.alto + SEPARACION;
      break;
    default:
      left = centroX - tarjeta.ancho / 2;
      top = elemento.top - tarjeta.alto - SEPARACION;
      break;
  }

  const topSujeto = sujetar(top, topSeguro + MARGEN, ventana.alto - tarjeta.alto - MARGEN);
  const leftSujeto = sujetar(left, MARGEN, ventana.ancho - tarjeta.ancho - MARGEN);

  // LA FLECHA SIGUE AL ELEMENTO, NO A LA TARJETA. Si la tarjeta se sujetó contra un borde, apuntar
  // a su propio centro señalaría un sitio donde no hay nada; se mide desde el centro real del
  // elemento y se deja un margen para que no salga por la esquina redondeada.
  const flecha = lado === 'derecha' || lado === 'izquierda'
    ? sujetar(centroY - topSujeto, 20, tarjeta.alto - 20)
    : sujetar(centroX - leftSujeto, 20, tarjeta.ancho - 20);

  return { top: topSujeto, left: leftSujeto, lado, flecha };
}

function sujetar(valor: number, minimo: number, maximo: number): number {
  if (maximo < minimo) return minimo;
  return Math.max(minimo, Math.min(maximo, valor));
}
