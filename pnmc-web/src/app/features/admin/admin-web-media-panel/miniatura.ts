/**
 * Produce la miniatura que el panel pinta en la cuadrícula.
 *
 * POR QUÉ LA HACE EL NAVEGADOR Y NO EL SERVIDOR. El API no decodifica ni un
 * píxel a propósito —es lo que hace que una bomba de descompresión no tenga
 * dónde explotar— y redimensionar exige decodificar. El navegador ya tiene la
 * imagen en memoria porque la persona acaba de elegirla, así que ahí el trabajo
 * ya está pagado.
 *
 * El servidor no se fía de esto: la miniatura pasa por el mismo reconocedor de
 * formato que el archivo principal y se le exige WebP y 24 KiB. Es un dato de
 * cliente y se trata como tal.
 *
 * Es el mismo `canvas` que ya usa `optimizeTeamPhoto` para las fotografías del
 * equipo, con 320 px en vez de 480: la cuadrícula pinta dieciséis a la vez.
 */
export const ANCHO_DE_MINIATURA = 320;

/** Tope del servidor. Se repite aquí para poder avisar antes de subir. */
export const TOPE_DE_MINIATURA = 24 * 1024;

export function construirMiniatura(archivo: File): Promise<Blob | null> {
  return new Promise((resolver) => {
    const lector = new FileReader();
    // NUNCA RECHAZA. Una miniatura es una comodidad: si el navegador no puede
    // producirla —formato que no decodifica, canvas bloqueado—, la subida tiene
    // que seguir. Rechazar aquí convertiría un detalle de la cuadrícula en un
    // impedimento para cambiar la imagen del sitio.
    lector.onerror = () => resolver(null);
    lector.onload = () => {
      const imagen = new Image();
      imagen.onerror = () => resolver(null);
      imagen.onload = () => {
        const escala = Math.min(1, ANCHO_DE_MINIATURA / imagen.width);
        const ancho = Math.max(1, Math.round(imagen.width * escala));
        const alto = Math.max(1, Math.round(imagen.height * escala));
        const lienzo = document.createElement('canvas');
        lienzo.width = ancho;
        lienzo.height = alto;
        const contexto = lienzo.getContext('2d');
        if (!contexto) {
          resolver(null);
          return;
        }
        contexto.drawImage(imagen, 0, 0, ancho, alto);
        lienzo.toBlob(
          (blob) => resolver(blob && blob.size <= TOPE_DE_MINIATURA ? blob : null),
          'image/webp',
          0.8,
        );
      };
      imagen.src = String(lector.result || '');
    };
    lector.readAsDataURL(archivo);
  });
}

/** Formatea un tamaño en bytes para mostrarlo. */
export function pesoLegible(bytes: number | null | undefined): string {
  if (!bytes) { return '—'; }
  if (bytes < 1024) { return `${bytes} B`; }
  if (bytes < 1024 * 1024) { return `${Math.round(bytes / 1024)} KB`; }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** La etiqueta que ve el editor para cada estado. Derivado, nunca almacenado. */
export function etiquetaDeEstado(estado: string): string {
  switch (estado) {
    case 'publicado': return 'En el sitio';
    case 'retirado': return 'Retirada';
    default: return 'Sin publicar';
  }
}

/**
 * La misma imagen de fábrica, pedida al tamaño de una miniatura.
 *
 * <b>POR QUÉ EXISTE, con su dato.</b> Una ranura sin nada publicado enseña en el panel la
 * imagen compilada del registro, que es la que el visitante ve hoy. Cuatro de esas URLs son de
 * unsplash y traen `w=1015` o `w=1470`: el panel del Home descargaba cerca de 800 KB para
 * pintar cuatro recuadros de 320 px de ancho. Reescribir el parámetro los deja en unas decenas
 * de KB y no cambia lo que se ve.
 *
 * <b>Solo toca URLs de unsplash y solo el parámetro `w`.</b> Lo que no reconoce lo devuelve
 * tal cual: inventarle parámetros a un servidor cualquiera produce un 404 y un hueco, que es
 * peor que una imagen grande.
 */
export function versionLigera(url: string, ancho = ANCHO_DE_MINIATURA): string {
  if (!url.startsWith('https://images.unsplash.com/')) { return url; }
  if (!/[?&]w=\d+/.test(url)) { return url; }
  return url.replace(/([?&]w=)\d+/, `$1${ancho}`);
}
