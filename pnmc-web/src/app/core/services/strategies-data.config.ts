import { IMAGENES_DE_GALERIA } from './catalogo-de-imagenes-de-galeria';

export interface StrategyCard {
  id: string;
  tag: string;
  title: string;
  desc: string;
  img: string;
  navigatePath: string;
  componentId: string;
  bgGlow: string;
}

/*
  `img` SIGUE VIVO AUNQUE LA PORTADA YA NO LO USE. Desde el carrusel del
  Home resuelve su foto con `getWebImage('home_ruta_N')` y pisa este campo, pero
  `component-detail-page.component.html:166` lo lee TAL CUAL para la ficha de cada componente.
  Borrarlo de aqui por «campo muerto» dejaria esa pagina sin fotos.

  Consecuencia que conviene saber: publicar una foto de ruta desde el panel cambia la portada y
  NO cambia la ficha del componente. Son dos gobiernos para el mismo archivo hasta que la ficha
  tenga sus propias ranuras.
*/
export const STRATEGIES_DATA: StrategyCard[] = [
  {
    id: 'celebra-la-musica',
    tag: 'Estrategia de Circulación',
    title: 'Celebra la Música',
    desc: 'Activa escenarios, programación y redes territoriales para que los procesos musicales circulen, se conecten y ganen visibilidad.',
    img: IMAGENES_DE_GALERIA[2] || '',
    navigatePath: 'estrategia-circulacion',
    componentId: 'comp-c2-3',
    bgGlow: 'bg-[#6100D7]/20'
  },
  {
    id: 'territorios-sonoros',
    tag: 'Estrategia de Investigación',
    title: 'Territorios Sonoros',
    desc: 'Impulsa procesos de investigación, cartografía y documentación para reconocer, interpretar y proyectar la diversidad sonora del país.',
    img: IMAGENES_DE_GALERIA[4] || '',
    navigatePath: 'estrategia-investigacion',
    componentId: 'comp-c2-4',
    bgGlow: 'bg-[#00DA5E]/5'
  },
  {
    id: 'congreso-nacional',
    tag: 'Estrategia de Gobernanza y Participación',
    title: '8vo Congreso Nacional de Música',
    desc: 'Espacio de diálogo académico, social e institucional para consolidar las políticas del sector y fortalecer la gobernanza musical en el país.',
    img: IMAGENES_DE_GALERIA[6] || '',
    navigatePath: 'comp-c3-1',
    componentId: 'comp-c3-1',
    bgGlow: 'bg-[#6100D7]/15'
  },
  {
    id: 'tempos-memorias',
    tag: 'Estrategia de Formación',
    title: 'Tempos de Memorias',
    desc: 'Laboratorio formativo enfocado en la cualificación de saberes tradicionales, lutería, pedagogía y preservación de patrimonios sonoros locales.',
    img: IMAGENES_DE_GALERIA[8] || '',
    navigatePath: 'comp-c2-1',
    componentId: 'comp-c2-1',
    bgGlow: 'bg-[#00DA5E]/10'
  },
  {
    id: 'voces-saberes',
    tag: 'Estrategia de Investigación',
    title: 'Voces y Saberes',
    desc: 'Proceso nacional de documentación y registro para catalogar las expresiones orales y la memoria viva de nuestros cantautores y sabedores.',
    img: IMAGENES_DE_GALERIA[10] || '',
    navigatePath: 'comp-c2-4',
    componentId: 'comp-c2-4',
    bgGlow: 'bg-[#6100D7]/15'
  },
  {
    id: 'red-jazz',
    tag: 'Estrategia de Circulación',
    title: 'Red Nacional de Jazz',
    desc: 'Plataforma de circulación colaborativa que conecta festivales, clubes y músicos de jazz en circuitos nacionales y de intercambio.',
    img: IMAGENES_DE_GALERIA[12] || '',
    navigatePath: 'comp-c2-3',
    componentId: 'comp-c2-3',
    bgGlow: 'bg-[#00DA5E]/5'
  },
  {
    id: 'mercados-musicales',
    tag: 'Estrategia de Circulación',
    title: 'Mercados Musicales de Colombia',
    desc: 'Fortalece el encuentro entre programadores, directores y agrupaciones nacionales para dinamizar la circulación nacional e internacional.',
    img: IMAGENES_DE_GALERIA[1] || '',
    navigatePath: 'comp-c2-3',
    componentId: 'comp-c2-3',
    bgGlow: 'bg-[#6100D7]/15'
  },
  {
    id: 'mesas-participacion',
    tag: 'Estrategia de Gobernanza y Circulación',
    title: 'Mesas de Participación',
    desc: 'Nodos comunitarios de concertación que articulan el tejido asociativo y las veedurías locales del Plan Nacional de Música.',
    img: IMAGENES_DE_GALERIA[3] || '',
    navigatePath: 'comp-c3-1',
    componentId: 'comp-c3-1',
    bgGlow: 'bg-[#00DA5E]/10'
  }
];
