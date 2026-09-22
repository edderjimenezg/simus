/**
 * Superpone los textos del CMS sobre las páginas de estrategia.
 *
 * «Estrategias» era la única pestaña del panel **100 % cosmética**: sus 7 campos
 * no los leía ningún componente. Pero el diagnóstico correcto no era «7 claves
 * huérfanas» sino algo más incómodo: **el panel se construyó para una página que
 * no existía**. `/estrategia/circulacion` era un marcador de posición —un
 * encabezado, un párrafo y tres pilares, escritos por quien programó la página—
 * mientras la copia institucional de Celebra la Música (presentación, propósito
 * y la edición del año) esperaba en el CMS sin que nadie la renderizara.
 *
 * Al conectarla, la página pasa a mostrar el texto del Ministerio en lugar del
 * de relleno. Es un cambio visible y deliberado: entre una copia institucional
 * que nadie ve y una de relleno que sí se ve, la primera es la que debe estar en
 * el portal, y a partir de ahora se corrige desde el panel y no desplegando.
 *
 * OJO CON LAS FECHAS: el texto sembrado habla de «En 2025… durante 29 días». Era
 * la edición vigente cuando se escribió el catálogo. Ya no lo es, y esta función
 * no lo disimula: el sitio dice lo que diga el panel. Actualizarlo es ahora un
 * trabajo de dos minutos para el equipo de contenidos, que es exactamente el
 * punto de todo este rescate.
 *
 * Un valor publicado en blanco se respeta tal cual —el contrato de la Fase 3
 * distingue «publicado vacío» de «sin publicar» (PNMC-040)—; la plantilla se
 * encarga de no pintar un título vacío en vez de rellenarlo con otra cosa.
 */

/** Los dos bloques que el panel gobierna hoy. `null` cuando no gobierna ninguno. */
export interface StrategyNarrative {
  sectionTitle: string;
  mission: string;
  /** Presentación, articulación y cierre de la edición vigente, en ese orden. */
  edition: string[];
}

export interface StrategyContent {
  eyebrow: string;
  title: string;
  description: string;
  image: string;
  pillars: string[];
  text: string;
  narrative: StrategyNarrative | null;
}

export interface StrategyCompiled extends Omit<StrategyContent, 'narrative'> {
  /**
   * Prefijo de sus claves en el panel, o `null` si el panel no ofrece ninguna.
   *
   * Las dos estrategias lo tienen desde. Territorios Sonoros estuvo en
   * `null` hasta ese día, y eso dejaba su FOTO administrable sin ningún bloque del panel donde
   * editarse: el editor de imágenes vive dentro del bloque de los textos que la acompañan.
   */
  cmsPrefix: string | null;
  /**
   * Si el panel gobierna además el relato de tres partes —título de sección, misión y los tres
   * párrafos de la edición—.
   *
   * <b>Territorios Sonoros está en `false` y eso NO es un cableado a medias.</b> Su página no
   * pinta ese bloque, así que declarar sus siete claves obligaría a escribir copia institucional
   * que hoy no existe, solo para tener los campos llenos. Con `false`, el panel gobierna las dos
   * frases que la página sí muestra y no ofrece las que no.
   */
  tieneRelato: boolean;
  /** Clave de `registro-de-imagenes-web.ts` para la imagen de esta estrategia. */
  imageKey: string;
}

export const ESTRATEGIAS_COMPILADAS: Record<'circulacion' | 'investigacion', StrategyCompiled> = {
  circulacion: {
    eyebrow: 'Estrategia de circulación',
    title: 'Celebra la Música',
    description: 'Un movimiento nacional que articula escenarios, agentes y comunidades para hacer visible la diversidad sonora del país.',
    image: 'https://images.unsplash.com/photo-1501386761578-eac5c94b800a?q=80&w=1600&auto=format&fit=crop',
    pillars: ['Circulación nacional', 'Encuentro comunitario', 'Diversidad sonora'],
    text: 'Celebra la Música promueve encuentros, conciertos y procesos de circulación que conectan las músicas de Colombia con públicos, territorios y nuevas oportunidades de colaboración.',
    cmsPrefix: 'strategy_celebra',
    tieneRelato: true,
    imageKey: 'estrategia_celebra_media',
  },
  investigacion: {
    eyebrow: 'Estrategia de investigación',
    title: 'Territorios Sonoros',
    description: 'Una ruta para reconocer, documentar y fortalecer la diversidad musical desde los territorios de Colombia.',
    image: 'https://images.unsplash.com/photo-1511379938547-c1f69419868d?q=80&w=1600&auto=format&fit=crop',
    pillars: ['Investigación situada', 'Memoria y documentación', 'Conocimiento colaborativo'],
    text: 'Territorios Sonoros conecta comunidades, investigadores, portadores de saberes e instituciones para comprender las prácticas musicales en su contexto y convertir ese conocimiento en acciones de política pública.',
    cmsPrefix: 'strategy_territorios',
    tieneRelato: false,
    imageKey: 'estrategia_territorios_media',
  },
};

export function resolveStrategy(
  compilada: StrategyCompiled,
  leer: (clave: string) => string,
  /**
   * Lector de IMAGENES, opcional. La clave sale del prefijo de la estrategia y
   * no del `cmsPrefix`, porque `investigacion` no tiene prefijo de textos y su
   * imagen si es administrable: atarla al `cmsPrefix` habria dejado a Territorios
   * Sonoros fuera del panel sin que nada lo dijera.
   */
  leerImagen?: (clave: string) => string,
): StrategyContent {
  const { cmsPrefix, tieneRelato, ...resto } = compilada;
  const imagen = leerImagen ? leerImagen(compilada.imageKey) : resto.image;
  if (!cmsPrefix) {
    return { ...resto, image: imagen, pillars: [...resto.pillars], narrative: null };
  }

  const clave = (sufijo: string) => leer(`${cmsPrefix}_${sufijo}`);
  return {
    ...resto,
    image: imagen,
    pillars: [...resto.pillars],
    description: clave('hero_desc'),
    text: clave('intro'),
    // SIN RELATO SIGUE SIENDO `null`, no un relato de campos vacíos. La plantilla decide con
    // `@if (strategy().narrative)`: devolver un objeto con tres cadenas vacías pintaría el
    // bloque entero en blanco, con sus títulos y sus separadores, sobre la página pública.
    narrative: tieneRelato
      ? {
          sectionTitle: clave('section_title'),
          mission: clave('mission'),
          edition: [clave('edition_intro'), clave('edition_vision'), clave('edition_closing')],
        }
      : null,
  };
}
