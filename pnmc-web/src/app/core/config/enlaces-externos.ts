/**
 * Los destinos que salen del portal, declarados una sola vez.
 *
 * Son pocos y conviene que sigan siéndolo: cada uno es una salida del dominio
 * propio, y una salida escrita a mano en tres plantillas distintas es una
 * salida que tarde o temprano apunta a tres sitios distintos.
 *
 * No son editables desde el panel, y es deliberado. El resto del texto del
 * sitio sí lo es —para eso existe el CMS—, pero un enlace saliente que se pueda
 * reescribir con sesión de editor deja de ser una corrección de copia y pasa a
 * ser un redireccionamiento del tráfico del Ministerio. Cambiar uno de estos
 * destinos exige tocar el código y pasar por revisión.
 */
export const ENLACES_EXTERNOS = {
  /**
   * SIMUS — Sistema de Información de la Música del Ministerio de las Culturas,
   * las Artes y los Saberes. Es una plataforma independiente de este portal: el
   * PNMC enlaza hacia ella, no la contiene.
   */
  simus: 'https://simus.mincultura.gov.co/',
} as const;

/** Atributos obligatorios de todo enlace que abre pestaña nueva. */
export const DESTINO_NUEVA_PESTANA = {
  target: '_blank',
  rel: 'noopener noreferrer',
} as const;
