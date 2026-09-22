/**
 * Utilidades para servir la variante correcta de una imagen.
 *
 * `tools/optimize-images.mjs` genera, junto a cada foto de `public/`, un hermano
 * `<nombre>.thumb.webp` de 400 px. Sirve para los recuadros pequeños: la tira de
 * miniaturas del visor mide 96 x 64 px y hasta ahora descargaba la foto completa.
 *
 * Las rutas de los álbumes las entrega la API, no el código, de modo que no se
 * puede dar por hecho que toda imagen tenga miniatura. Por eso la sustitución es
 * optimista y va acompañada de `fallbackToOriginal` en el evento `error`.
 */

/** Ruta de la miniatura hermana. Devuelve la original si no aplica. */
export function thumbnailFor(src: string | null | undefined): string {
  if (!src) { return ''; }
  // Solo las imágenes propias (rutas absolutas del sitio) tienen miniatura;
  // las externas y las incrustadas en base64 se dejan intactas.
  if (!src.startsWith('/')) { return src; }
  return src.replace(/\.(jpe?g|png)(\?.*)?$/i, '.thumb.webp$2');
}

/**
 * Repone la imagen original cuando la miniatura no existe —por ejemplo una foto
 * subida después de la última optimización—. La marca evita reintentar en bucle
 * si tampoco carga la original.
 */
export function fallbackToOriginal(event: Event, original: string | null | undefined): void {
  const image = event.target as HTMLImageElement | null;
  if (!image || !original || image.dataset['thumbFallback'] === 'done') { return; }
  image.dataset['thumbFallback'] = 'done';
  image.src = original;
}
