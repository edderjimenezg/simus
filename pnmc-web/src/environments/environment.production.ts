export const environment = {
  production: true,
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
