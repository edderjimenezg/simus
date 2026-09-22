/**
 * El recorte contra el marco de la ranura.
 *
 * QUÉ PROBLEMA RESUELVE, con el número que lo hizo necesario: la portada de «Sobre el PNMC» tiene
 * un hueco de relación 4,44 y hasta hoy cualquier imagen entraba centrada por `object-cover`. Subir
 * una fotografía vertical dejaba una franja horizontal del centro y nadie lo veía venir. Aquí la
 * persona elige qué franja.
 *
 * TODO ESTO ES ARITMÉTICA PURA, sin DOM salvo en `recortar`. Es lo que permite probar el encaje,
 * los topes y el acercamiento sin montar un componente ni un navegador, y dejar para las pruebas
 * de navegador solo lo que de verdad necesita píxeles.
 *
 * EL RECTÁNGULO VA EN COORDENADAS NORMALIZADAS (0..1) sobre la imagen de origen, no en píxeles.
 * Así el mismo rectángulo sirve para la vista previa a 400 px de ancho y para el recorte final a
 * 2880, y no hay dos números que puedan discrepar.
 */

/** Un rectángulo normalizado sobre la imagen de origen. */
export interface Recorte {
  x: number;
  y: number;
  w: number;
  h: number;
}

/** Medidas en píxeles. */
export interface Medida {
  w: number;
  h: number;
}

/**
 * Los tres tipos de marco que el sitio tiene de verdad, medidos con el navegador.
 *
 * - `fija`: la relación está escrita en el CSS y no cambia. Solo «Huella y evolución», con
 *   `aspect-[16/6.5]`. El recorte es exacto.
 * - `variable`: la relación depende del alto de la ventana. Las portadas de página van de 4,00 a
 *   4,44 porque `page-hero.component.html:7` declara `lg:h-[40vh]`. Se recorta a la MÁS ANCHA y el
 *   `object-cover` del sitio ajusta el resto: sobra alto, nunca falta.
 * - `dos-marcos`: la misma imagen se pinta en dos relaciones a la vez. Los hitos van a 1,76
 *   abiertos y a 0,29 plegados. Manda el abierto, que es el que el visitante lee.
 * - `encaje`: se pinta con `object-contain` y no se recorta nada; solo se reduce de tamaño.
 */
export type ClaseDeMarco = 'fija' | 'variable' | 'dos-marcos' | 'encaje';

/** Área máxima de salida: dos veces el hueco, para pantallas de alta densidad. */
export const DENSIDAD = 2;

/**
 * Ancho máximo de salida.
 *
 * No es un número nuevo: es el `MAX_WIDTH` que `tools/optimize-images.mjs:31` ya impone a todo
 * `public/`. El sitio tiene un solo techo de resolución y este lo respeta en vez de estrenar otro.
 */
export const ANCHO_MAXIMO = 2880;

/** Calidad de codificación. Igual que `JPEG_QUALITY` de `tools/optimize-images.mjs:34`. */
export const CALIDAD = 0.82;

/**
 * El recorte de partida: el rectángulo más grande con la relación del marco que cabe en la
 * imagen, centrado. Es exactamente lo que hace `object-fit: cover`, así que abrir el editor y no
 * tocar nada deja la imagen como el sitio la habría pintado sola.
 */
export function encajeInicial(natural: Medida, marco: Medida): Recorte {
  const relacionMarco = marco.w / marco.h;
  const relacionImagen = natural.w / natural.h;

  if (relacionImagen > relacionMarco) {
    // La imagen es más ancha que el marco: sobra a los lados.
    const w = relacionMarco / relacionImagen;
    return { x: (1 - w) / 2, y: 0, w, h: 1 };
  }

  // La imagen es más alta: sobra arriba y abajo.
  const h = relacionImagen / relacionMarco;
  return { x: 0, y: (1 - h) / 2, w: 1, h };
}

/**
 * Devuelve el rectángulo dentro de los límites de la imagen, conservando su tamaño.
 *
 * MUEVE ANTES DE ENCOGER, y ese orden es el que hace que arrastrar se sienta bien: al llegar al
 * borde el recorte se para, no se deforma. Solo si el rectángulo es más grande que la imagen —lo
 * que no puede ocurrir si se respeta `acercamientoMinimo`— se recorta su tamaño.
 */
export function acotar(recorte: Recorte): Recorte {
  const w = Math.min(1, recorte.w);
  const h = Math.min(1, recorte.h);
  return {
    w,
    h,
    x: Math.min(Math.max(recorte.x, 0), 1 - w),
    y: Math.min(Math.max(recorte.y, 0), 1 - h),
  };
}

/**
 * Aplica un factor de acercamiento manteniendo fijo el punto que la persona señala.
 *
 * EL ACERCAMIENTO MÍNIMO ES LA COBERTURA, y esa es la decisión que convierte «el archivo es más
 * pequeño que el hueco» de aviso en imposible. Con el mínimo en el encaje, el recorte nunca puede
 * ser mayor que la imagen, así que la imagen nunca se amplía. El caso vivo que lo motiva:
 * `home_hero_4` pedía a unsplash una imagen de 687 px de ancho para un hueco de 1670, se ampliaba
 * 2,43 veces, y no fallaba nada.
 */
export function acercar(
  recorte: Recorte,
  factor: number,
  centro: { x: number; y: number },
  minimo: Recorte,
): Recorte {
  // El tope de alejamiento es el encaje: más allá habría que ampliar la imagen.
  const escala = Math.min(Math.max(factor, recorte.w === 0 ? 1 : 0.01), 100);
  let w = recorte.w / escala;
  let h = recorte.h / escala;

  if (w > minimo.w || h > minimo.h) {
    w = minimo.w;
    h = minimo.h;
  }

  // El punto señalado se queda donde estaba: sin esto, acercar salta al centro.
  const x = centro.x - ((centro.x - recorte.x) * w) / recorte.w;
  const y = centro.y - ((centro.y - recorte.y) * h) / recorte.h;

  return acotar({ x, y, w, h });
}

/**
 * Cuántos píxeles tiene que producir el recorte.
 *
 * Tres topes a la vez, y el más pequeño manda:
 *  1. dos veces el hueco, que es lo que pide una pantalla de alta densidad;
 *  2. lo que la imagen de origen realmente tiene dentro del recorte, para no ampliar;
 *  3. el techo de {@link ANCHO_MAXIMO}, que es el del resto del sitio.
 *
 * El segundo es el que evita el defecto de tomar una imagen de 687 px y escribir un archivo de
 * 2880 px que no aporta un solo píxel de detalle y pesa cuatro veces más.
 */
export function salidaDe(marco: Medida, natural: Medida, recorte: Recorte): Medida {
  const disponibles = natural.w * recorte.w;
  const ancho = Math.max(1, Math.round(Math.min(marco.w * DENSIDAD, disponibles, ANCHO_MAXIMO)));
  const alto = Math.max(1, Math.round((ancho * marco.h) / marco.w));
  return { w: ancho, h: alto };
}

/**
 * ¿Alcanza esta imagen para llenar el marco a densidad 1x?
 *
 * Se pregunta ANTES de dejar subir. Es el bloqueo que el usuario eligió sobre el aviso: un aviso
 * que se puede ignorar habría dejado pasar exactamente el caso de `home_hero_4`.
 */
export function alcanzaParaElMarco(natural: Medida, marco: Medida): boolean {
  const encaje = encajeInicial(natural, marco);

  // SE COMPRUEBA SOLO EL ANCHO, y no es un descuido. Aquí había un `&& natural.h * encaje.h >=
  // marco.h` y un mutante que lo borraba SOBREVIVIÓ a las veintiuna pruebas. Al mirar por qué, la
  // segunda mitad resultó ser la primera escrita de otra forma: el encaje ya impone la relación
  // del marco, así que `natural.h * encaje.h` vale exactamente `natural.w * encaje.w / relación`
  // y comparar contra `marco.h` es comparar contra `marco.w / relación`. La misma desigualdad.
  //
  // Se deja una sola, con la explicación: dos comprobaciones equivalentes no son el doble de
  // seguras, son una de ellas y una copia que nadie puede poner en rojo.
  return natural.w * encaje.w >= marco.w;
}

/**
 * ¿Qué franja del ancho ve el estado plegado de un acordeón?
 *
 * Los hitos y las normativas pintan la misma imagen a 1,76 abierta y a 0,29 plegada. Recortando a
 * la abierta, el estado plegado toma una franja central que vale la razón entre las dos
 * relaciones: 0,29 / 1,76 = 16,5 % del ancho. El recortador la dibuja para que la persona sepa
 * qué parte se ve siempre.
 */
export function franjaCentral(relacionAbierta: number, relacionPlegada: number): number {
  if (relacionAbierta <= 0) { return 1; }
  return Math.min(1, relacionPlegada / relacionAbierta);
}

/**
 * Recorta y codifica. Es lo único de este fichero que toca el DOM.
 *
 * REDUCE POR MITADES cuando la escala baja de la mitad, y no es un adorno: un solo
 * `drawImage` de 4500 px a 309 deja el borde de un logotipo blando, porque el navegador
 * promedia una muestra por píxel de destino. Es el mismo defecto que tienen hoy las miniaturas de
 * `miniatura.ts`, que hacen la reducción en un paso.
 */
export async function recortar(
  origen: ImageBitmap | HTMLImageElement,
  recorte: Recorte,
  salida: Medida,
  opciones: { calidad?: number; grises?: boolean } = {},
): Promise<Blob | null> {
  const anchoOrigen = 'naturalWidth' in origen ? origen.naturalWidth : origen.width;
  const altoOrigen = 'naturalHeight' in origen ? origen.naturalHeight : origen.height;

  const sx = Math.round(recorte.x * anchoOrigen);
  const sy = Math.round(recorte.y * altoOrigen);
  const sw = Math.max(1, Math.round(recorte.w * anchoOrigen));
  const sh = Math.max(1, Math.round(recorte.h * altoOrigen));

  // Primer lienzo: solo el trozo elegido, a su tamaño original.
  let lienzo = document.createElement('canvas');
  lienzo.width = sw;
  lienzo.height = sh;
  const cx = lienzo.getContext('2d');
  if (!cx) { return null; }
  cx.imageSmoothingEnabled = true;
  cx.imageSmoothingQuality = 'high';
  cx.drawImage(origen as CanvasImageSource, sx, sy, sw, sh, 0, 0, sw, sh);

  // Reducción por mitades hasta quedar a menos del doble del destino.
  while (lienzo.width > salida.w * 2 && lienzo.width > 2) {
    const medio = document.createElement('canvas');
    medio.width = Math.max(1, Math.floor(lienzo.width / 2));
    medio.height = Math.max(1, Math.floor(lienzo.height / 2));
    const mx = medio.getContext('2d');
    if (!mx) { return null; }
    mx.imageSmoothingEnabled = true;
    mx.imageSmoothingQuality = 'high';
    mx.drawImage(lienzo, 0, 0, medio.width, medio.height);
    lienzo = medio;
  }

  const final = document.createElement('canvas');
  final.width = salida.w;
  final.height = salida.h;
  const fx = final.getContext('2d');
  if (!fx) { return null; }
  fx.imageSmoothingEnabled = true;
  fx.imageSmoothingQuality = 'high';
  if (opciones.grises) {
    // Las ranuras que el sitio pinta con `grayscale(1)` se guardan en gris: pesa un 56 % menos y
    // el visitante ve exactamente lo mismo, porque el CSS lo iba a desaturar igual.
    fx.filter = 'grayscale(1)';
  }
  fx.drawImage(lienzo, 0, 0, final.width, final.height);

  return new Promise(resolver =>
    final.toBlob(resolver, 'image/webp', opciones.calidad ?? CALIDAD));
}
