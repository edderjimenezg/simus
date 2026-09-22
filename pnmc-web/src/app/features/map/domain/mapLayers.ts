import * as L from 'leaflet';

export const WORLD_COUNTRY_LABELS = [
  { name: 'México', position: [23.6, -102.5] },
  { name: 'Guatemala', position: [15.6, -90.4] },
  { name: 'Panamá', position: [8.8, -80.2] },
  { name: 'Cuba', position: [21.8, -79.5] },
  { name: 'Rep. Dominicana', position: [18.9, -70.3] },
  { name: 'Colombia', position: [4.4, -73.2] },
  { name: 'Venezuela', position: [7.2, -66.2] },
  { name: 'Ecuador', position: [-1.5, -78.2] },
  { name: 'Perú', position: [-9.3, -74.4] },
  { name: 'Brasil', position: [-10.8, -54.2] },
  { name: 'Bolivia', position: [-16.7, -64.6] },
  { name: 'Chile', position: [-30.0, -71.0] },
  { name: 'Paraguay', position: [-23.3, -58.4] },
  { name: 'Argentina', position: [-36.4, -64.2] },
  { name: 'Uruguay', position: [-32.8, -56.0] },
];

export const countryLabelIcon = (name: string) => L.divIcon({
  className: 'country-label-marker',
  html: `<span>${name}</span>`,
});

export const ECOSYSTEM_LAYERS = [
  {
    key: 'General',
    shortLabel: 'General',
    status: 'Activo',
    description: 'Integra una lectura sintética del ecosistema por departamento, sumando escuelas, festivales y mercados visibles.',
    accent: 'from-emerald-100 via-emerald-300 to-emerald-600',
  },
  {
    key: 'Festivales',
    shortLabel: 'Festivales',
    status: 'Activo',
    description: 'Mide presencia territorial, concentración y vacíos de circulación musical.',
    accent: 'from-purple-100 via-purple-300 to-purple-600',
  },
  {
    key: 'Escuelas de Música',
    shortLabel: 'Escuelas',
    status: 'Activo',
    description: 'Lee presencia formativa, capacidad pedagógica e infraestructura básica de las escuelas registradas.',
    accent: 'from-sky-100 via-sky-300 to-sky-600',
  },
  {
    key: 'Redes de Documentación',
    shortLabel: 'Redes Doc.',
    status: 'Activo',
    description: 'Nodos de investigación, archivos y documentación musical territorial.',
    accent: 'from-pink-100 via-pink-300 to-pink-600',
  },
  {
    key: 'Lutieres',
    shortLabel: 'Lutieres',
    status: 'Activo',
    description: 'Constructores, reparadores y saberes de lutería de instrumentos.',
    accent: 'from-cyan-100 via-teal-300 to-teal-600',
  },
  {
    key: 'Mercados Musicales',
    shortLabel: 'Mercados',
    status: 'Activo',
    description: 'Lee nodos de circulación, vitrinas, negocios y articulación sectorial visibles en los mercados musicales registrados.',
    accent: 'from-amber-100 via-amber-300 to-amber-600',
  },
];
