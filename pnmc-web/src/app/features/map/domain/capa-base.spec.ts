import * as MapDomain from './map-domain';

/**
 * La capa base del geovisor, y por qué está vigilada.
 *
 * Hasta la capa base era CARTO, escrita a pelo dentro del
 * componente. CARTO pasó a exigir clave y lo hizo SIN romper nada: la teja 5/9/14 de
 * `light_nolabels` sigue respondiendo 200, con `image/png` y 5.251 bytes —los mismos
 * sin clave, con clave inválida y por la ruta `rastertiles`—, solo que con
 * «API KEY REQUIRED» estampado DENTRO del PNG. Ningún código de estado, ninguna
 * cabecera y ningún tipo de contenido lo delataban.
 *
 * De ahí sale la forma de este módulo: el proveedor es un dato en una tabla, no una
 * línea dentro de `montarElMapaUnaVez`. Lo que estas pruebas fijan es esa forma —que
 * cambiar de proveedor sea cambiar un valor— y las dos cosas que la licencia y el
 * diseño exigen de cualquier proveedor que entre en la tabla.
 */
describe('Capa base · el proveedor es un dato, no una línea de código', () => {
  describe('resolveBasemapProvider', () => {
    it('devuelve el proveedor pedido por su identificador', () => {
      expect(MapDomain.resolveBasemapProvider('openstreetmap').id).toBe('openstreetmap');
      expect(MapDomain.resolveBasemapProvider('ninguna').id).toBe('ninguna');
    });

    it('ante un identificador que no existe cae en el de por defecto, no deja el mapa sin fondo', () => {
      for (const malo of ['carto', '', '   ', null, undefined]) {
        expect(MapDomain.resolveBasemapProvider(malo as any).id).toBe(MapDomain.DEFAULT_BASEMAP_ID);
      }
    });

    it('el proveedor por defecto existe de verdad en la tabla', () => {
      expect(MapDomain.BASEMAP_PROVIDERS.some((p) => p.id === MapDomain.DEFAULT_BASEMAP_ID)).toBeTrue();
    });
  });

  describe('lo que se le exige a cualquier proveedor de la tabla', () => {
    it('todos declaran atribución visible, salvo el que no pinta nada', () => {
      // OpenFreeMap, OpenMapTiles y OpenStreetMap exigen atribución. Un proveedor
      // nuevo sin atribución entraría incumpliendo su propia licencia.
      for (const proveedor of MapDomain.BASEMAP_PROVIDERS) {
        if (proveedor.kind === 'none') {
          expect(proveedor.attribution).toBe('');
        } else {
          expect(proveedor.attribution.length).toBeGreaterThan(0);
          expect(proveedor.attribution).toContain('OpenStreetMap');
        }
      }
    });

    it('ninguno pide clave: no hay una sola URL con parámetro de autenticación', () => {
      // Esta es la prueba que no existía y por eso CARTO vivió sellado.
      for (const proveedor of MapDomain.BASEMAP_PROVIDERS) {
        expect(proveedor.url).not.toContain('key=');
        expect(proveedor.url).not.toContain('token=');
        expect(proveedor.url).not.toContain('apikey');
      }
    });

    it('el que no tiene rótulos propios deja que el geovisor dibuje los suyos', () => {
      const porDefecto = MapDomain.resolveBasemapProvider(MapDomain.DEFAULT_BASEMAP_ID);
      expect(porDefecto.hasOwnLabels).toBeFalse();
    });

    it('el raster trae los marcadores de teja que Leaflet necesita', () => {
      for (const proveedor of MapDomain.BASEMAP_PROVIDERS.filter((p) => p.kind === 'raster')) {
        expect(proveedor.url).toContain('{z}');
        expect(proveedor.url).toContain('{x}');
        expect(proveedor.url).toContain('{y}');
      }
    });

    it('el vectorial apunta a un JSON de estilo, no a una plantilla de teja', () => {
      for (const proveedor of MapDomain.BASEMAP_PROVIDERS.filter((p) => p.kind === 'vector')) {
        expect(proveedor.url).not.toContain('{z}');
        expect(proveedor.url.startsWith('https://')).toBeTrue();
      }
    });
  });

  describe('isLabelLayer', () => {
    it('reconoce una capa de rótulos por lo que la hace rotular, no por su nombre', () => {
      // Se mira `text-field` y no el identificador: los nombres los pone el estilo y
      // cambian entre versiones; `text-field` es lo que hace aparecer la palabra.
      expect(MapDomain.isLabelLayer({ id: 'label_country_1', layout: { 'text-field': '{name}' } })).toBeTrue();
      expect(MapDomain.isLabelLayer({ id: 'se_llama_raro', layout: { 'text-field': '{name:es}' } })).toBeTrue();
    });

    it('no confunde con una capa de relleno que se llame «label»', () => {
      expect(MapDomain.isLabelLayer({ id: 'label_background', type: 'fill' })).toBeFalse();
      expect(MapDomain.isLabelLayer({ id: 'water', layout: { visibility: 'visible' } })).toBeFalse();
    });

    it('aguanta lo que le echen sin reventar', () => {
      expect(MapDomain.isLabelLayer(null)).toBeFalse();
      expect(MapDomain.isLabelLayer(undefined)).toBeFalse();
      expect(MapDomain.isLabelLayer({})).toBeFalse();
    });
  });
});
