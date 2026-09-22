import * as L from 'leaflet';

/**
 * Tipado minimo de `leaflet.markercluster`, escrito a mano.
 *
 * POR QUE NO SE INSTALA `@types/leaflet.markercluster`. Porque no hace falta para las
 * seis opciones que este proyecto usa, y porque el trinquete de tipado esta en rojo
 * —636 ocurrencias de `any` contra un techo de 518, medido—:
 * el camino corto era un `(L as any).markerClusterGroup(...)`, que suma una ocurrencia
 * a una deuda que ya se pasa de la raya.
 *
 * La biblioteca lleva en `dependencies` desde antes (1.5.3), sus dos hojas de estilo
 * cargadas en `angular.json`, y estaba IMPORTADA en el componente sin un solo consumidor.
 *
 * Se declaran solo las opciones que se pasan. Anadir una obliga a declararla aqui, que
 * es justo lo que se quiere: que el tipo diga la verdad de lo que se usa.
 */
declare module 'leaflet' {
  interface MarkerClusterGroupOptions extends L.LayerOptions {
    /** Radio en pixeles dentro del cual dos marcadores se juntan en uno. */
    maxClusterRadius?: number | ((zoom: number) => number);
    /** A partir de este acercamiento no se agrupa nada. */
    disableClusteringAtZoom?: number;
    /** Dibujar el area que cubre un grupo al pasar el cursor. */
    showCoverageOnHover?: boolean;
    /** Abrir el grupo en abanico al llegar al maximo acercamiento. */
    spiderfyOnMaxZoom?: boolean;
    /** Anadir los marcadores por tandas para no bloquear el hilo. */
    chunkedLoading?: boolean;
    /** El icono del grupo. */
    iconCreateFunction?: (cluster: MarkerCluster) => L.DivIcon;
  }

  interface MarkerCluster extends L.Marker {
    getAllChildMarkers(): L.Marker[];
    getChildCount(): number;
  }

  interface MarkerClusterGroup extends L.FeatureGroup {
    addLayer(layer: L.Layer): this;
    addLayers(layers: L.Layer[]): this;
    clearLayers(): this;
  }

  function markerClusterGroup(options?: MarkerClusterGroupOptions): MarkerClusterGroup;
}
