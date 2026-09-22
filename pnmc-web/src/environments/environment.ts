export const environment = {
  production: false,
  /**
   * Vacío a propósito: las llamadas salen relativas al origen del sitio, igual
   * que en producción.
   *
   * Antes apuntaba a un origen absoluto distinto, y eso rompía la sesión: el
   * servidor de desarrollo escucha en `127.0.0.1`, y para el navegador una
   * dirección IP y un nombre de host son **sitios distintos**. La cookie de
   * sesión es `SameSite=Lax`, así que no se guardaba ni se enviaba; el inicio de
   * sesión respondía 200, el panel se pintaba, y cada guardado devolvía 401.
   *
   * El servidor de desarrollo reenvía `/api` a la API real —ver
   * `proxy.conf.json`—, de modo que todo queda en el mismo origen y no hay ni
   * CORS ni cookies entre sitios. Si la API corre en otro puerto, cámbielo allí.
   */
  apiBaseUrl: '',

  /**
   * Proveedor de la capa base del geovisor. Valores admitidos en
   * `map-domain.ts` -> `BASEMAP_PROVIDERS`: 'openfreemap-positron', 'openstreetmap'
   * y 'ninguna'.
   *
   * Se elige OpenFreeMap porque es abierto de cabo a rabo, no pide clave, no tiene
   * limite de peticiones y se puede AUTOALOJAR. Eso ultimo es lo que importa: el 28
   * de agosto de 2026 CARTO empezo a exigir clave y estampo «API KEY REQUIRED» sobre
   * cada teja sin devolver un solo error. Con un proveedor autoalojable esa clase de
   * cambio deja de poder ocurrir sin aviso.
   */
  basemap: 'openfreemap-positron',
};
