// Los colores de `color` DEBEN coincidir con `LAYER_ACCENTS` del componente del
// geovisor, que es lo que pinta los marcadores del mapa. Cuando discrepaban, la
// leyenda y la lista de capas afirmaban un color que el mapa no usaba: los
// festivales salian verdes en la lista y morados sobre el mapa.
export const MAP_LAYERS_CONFIG = [
  {
    id: 'general',
    layerKey: 'General',
    label: 'Vista general',
    // NOMBRABA CINCO COSAS Y CUATRO NO EXISTEN. Escuelas, mercados, redes de documentación y
    // lutieres son procesos cuyas tablas retiró `V20260904_01`: el subtítulo del mapa afirmaba
    // integrar datos que el sistema no tiene. Dice lo que integra de verdad.
    description: 'Integra los Festivales y la Agenda de eventos en una lectura territorial única.',
    color: '#296904',
    defaultVisible: true,
    iconKey: 'layout-grid',
  },
  {
    id: 'festivales',
    layerKey: 'Festivales',
    label: 'Festivales',
    description: 'Procesos y eventos tipo festival registrados en el ecosistema.',
    color: '#7a2f97',
    defaultVisible: true,
    iconKey: 'party',
  },
  {
    id: 'agenda',
    layerKey: 'Agenda',
    label: 'Agenda',
    description: 'Eventos publicados de la Agenda, situados por el territorio que declaran.',
    // EL MISMO VERDE CON EL QUE EL PORTAL YA NOMBRA LA AGENDA en su navegación. Dos códigos de
    // color para lo mismo obligarían a aprendérselos los dos.
    color: '#00A849',
    defaultVisible: true,
    iconKey: 'calendar',
  },
  {
    id: 'mercados',
    layerKey: 'Mercados Musicales',
    label: 'Mercados',
    description: 'Mercados musicales y espacios de circulación/comercialización.',
    color: '#c88011',
    defaultVisible: true,
    iconKey: 'building',
  },
  {
    id: 'escuelas',
    layerKey: 'Escuelas de Música',
    label: 'Escuelas',
    description: 'Escuelas y procesos formativos musicales registrados.',
    color: '#5e5ceb',
    defaultVisible: true,
    iconKey: 'library',
  },
  {
    id: 'redes',
    layerKey: 'Redes de Documentación',
    label: 'Redes Doc.',
    description: 'Redes de documentación e investigación musical.',
    color: '#d33068',
    defaultVisible: true,
    iconKey: 'book-open',
  },
  {
    id: 'lutieres',
    layerKey: 'Lutieres',
    label: 'Lutieres',
    description: 'Constructores y reparadores de instrumentos musicales.',
    color: '#109db5',
    defaultVisible: true,
    iconKey: 'hammer',
  },
];

export const MAP_PANEL_IDS = {
  layers: 'layers',
  territory: 'territory',
  filters: 'filters',
  insights: 'insights',
  tutorial: 'tutorial',
  export: 'export',
  registration: 'registration',
};
