/*
 * El mapa de calor del geovisor, escrito a mano sobre un `<canvas>`.
 *
 * PORTADO DEL DESARROLLO DE SEPTIEMBRE, cuando el catálogo territorial
 * dejó por fin de venir sin coordenadas —el importador DIVIPOLA pedía `returnGeometry=false` y no
 * pedía el centroide, así que los 1.122 municipios llegaban sin situar y ninguna vista de puntos
 * tenía nada que dibujar—. Se conserva el razonamiento del original porque sigue siendo válido y
 * porque documenta tres alternativas que ya se probaron y se descartaron; se traen al proyecto los
 * nombres y el tono de esta base, no los del origen.
 *
 * POR QUE NO SE AÑADE `leaflet.heat`. Es la biblioteca de siempre para esto y son 4 KB, así que la
 * tentación es clara. No se descarta por purismo, sino por tres motivos concretos: no se publica
 * desde 2019 y no trae tipos; su rampa por omisión es el arcoíris azul-verde-amarillo-rojo, que
 * este proyecto no puede usar —ver más abajo—; y normaliza por el máximo observado, que es justo
 * la decisión que aquí hay que tomar de otra manera.
 *
 * COMO SE PINTA, que es el algoritmo de siempre y conviene tenerlo escrito:
 *
 *   1. Cada punto dibuja un degradado radial BLANCO sobre transparente en modo `lighter`: donde se
 *      solapan, la opacidad se suma.
 *   2. Se lee el canal alfa. Ese canal ES la densidad.
 *   3. Cada píxel se colorea con la rampa según su alfa, y se vuelca.
 *
 * EL RADIO ES CONSTANTE EN PIXELES Y LA LEYENDA DICE A CUANTOS KILOMETROS EQUIVALE. Es la única de
 * las tres combinaciones posibles que sirve, y las otras dos se probaron:
 *
 *   · Radio en METROS. Es lo más honesto sobre el papel y es inservible en pantalla: a escala
 *     nacional 25 km son unos 5 px, y la mancha ocupaba el 1,3 % del lienzo. Un mapa de calor que
 *     no se ve no es un mapa de calor.
 *   · Radio en píxeles y leyenda con cifras absolutas, que es lo que hace `leaflet.heat`. Se ve
 *     bien y miente: «8 procesos cerca» son 8 procesos en 100 km a escala nacional y 8 procesos en
 *     1,5 km dentro de un departamento, y la leyenda dice lo mismo en los dos casos.
 *   · Radio en píxeles Y LA LEYENDA DICIENDO CUANTOS KILOMETROS MIDE EN ESTA VISTA. La superficie
 *     se ve siempre, la cifra sigue siendo cierta, y al acercarse cambia el rótulo —«a menos de
 *     12 km» pasa a «a menos de 1,5 km»—, que es exactamente lo que ha cambiado.
 *
 * Por eso la capa avisa de su escala con `alCambiarEscala` en cada repintado: la leyenda no puede
 * calcularla por su cuenta sin repetir la proyección, y dos cálculos del mismo número en dos
 * ficheros acaban separándose.
 *
 * LA RAMPA ES DE UN SOLO TONO Y NO ES EL ARCOIRIS. El arcoíris es la convención popular del mapa
 * de calor y es una mala escala: el ojo ve saltos donde el dato es continuo —la frontera del
 * amarillo salta a la vista y no significa nada— y se vuelve ilegible en escala de grises y para
 * quien no distingue rojo y verde. Se usa una rampa secuencial del color activo, que además ata el
 * calor a la misma clave de color que el resto del geovisor.
 *
 * LA RAMPA SE PUEDE CONTAR, y por eso la leyenda lleva cifras. El alfa de cada píxel es la SUMA de
 * los aportes que lo alcanzan, así que con un techo fijo el tono dice cuántos procesos hay a menos
 * del radio. Normalizar por el máximo observado convertiría el tono más oscuro en «el máximo de lo
 * que hay ahora»: cambiaría al tocar un filtro y ninguna cifra sería cierta dos veces seguidas.
 *
 * POR QUE ESTE FICHERO ES LARGO. Son tres piezas que no se pueden separar sin romper lo único que
 * las hace correctas: la tabla de la rampa, la capa que la aplica y `escalonesDeCalor`, que produce
 * los rótulos LEYENDO ESA MISMA tabla. Sacar los rótulos a otro sitio es como se llega a que el
 * techo suba y la leyenda siga diciendo la cifra vieja.
 */
import * as L from 'leaflet';

/** Un punto que aporta densidad. Un proceso, no un municipio. */
export interface PuntoDeCalor {
  readonly lat: number;
  readonly lng: number;
}

/**
 * Una serie del calor: los puntos de un grupo y el color con el que se pinta.
 *
 * <b>EL CALOR TAMBIEN HABLA EL IDIOMA DEL LENTE.</b> Las coropletas y los símbolos ya toman su
 * color del territorio sonoro o de la práctica; el calor se quedaba en el verde de la capa, así
 * que era el único de los tres modos que no decía CUAL. Con una serie por grupo, cada zona del
 * país se tiñe del territorio que predomina en ella y las tres formas de dibujar el mapa usan por
 * fin la misma clave de color.
 */
export interface SerieDeCalor {
  readonly color: string;
  readonly puntos: readonly PuntoDeCalor[];
}

export interface OpcionesDeCalor {
  /** Radio de influencia de cada proceso, en píxeles CSS. */
  readonly radioEnPixeles: number;
  /** Color base de la rampa cuando hay una sola serie, en `#rrggbb`. */
  readonly color: string;
  /**
   * Cuántos procesos solapados llegan al tono más oscuro.
   *
   * FIJO Y NO «EL MAXIMO OBSERVADO». Con el máximo observado, el tono más oscuro significa una
   * cosa distinta cada vez que se toca un filtro, y dos capturas de la misma pantalla dejan de ser
   * comparables. Con un techo fijo el color significa siempre lo mismo y la leyenda puede decirlo.
   */
  readonly techoDeSolape: number;
  /** Se llama en cada repintado con lo que el radio mide EN EL SUELO, en metros. */
  readonly alCambiarEscala?: (radioEnMetros: number) => void;
}

/**
 * Cuántos pasos tiene la rampa como mucho. Cinco, como los escalones del coroplético.
 *
 * <b>PERO NUNCA MAS QUE EL TECHO</b>, y esto costó una leyenda que decía mentiras. Con el techo de
 * solape en cuatro y cinco pasos fijos, los límites de cada banda se calculaban redondeando quintos
 * de cuatro y salían desordenados: la leyenda rotulaba «3 a 2 procesos cerca» —un rango invertido—
 * y repetía el «3» en dos escalones seguidos. No es un defecto de redondeo que se pueda tapar
 * ajustando la fórmula: cinco bandas de enteros no caben en un recorrido de cuatro enteros.
 *
 * Los pasos se derivan ahora del techo, y los derivan A LA VEZ la tabla de color y los rótulos, que
 * es lo que impide que vuelvan a separarse.
 */
const PASOS_MAXIMOS_DE_LA_RAMPA = 5;

/** Cuántos pasos usa de verdad la rampa para un techo dado. */
const pasosDeLaRampa = (techoDeSolape: number): number =>
  Math.max(1, Math.min(PASOS_MAXIMOS_DE_LA_RAMPA, Math.floor(techoDeSolape)));

/** Radio de la Tierra en metros, para pasar de píxeles del mapa a metros de suelo. */
const RADIO_TERRESTRE = 6378137;

/** El morado del proyecto, por si llega un color ilegible: nunca un tono al azar. */
const COLOR_DE_RESPALDO: [number, number, number] = [41, 18, 66];

function componentes(color: string): [number, number, number] {
  const limpio = color.replace('#', '');
  const entero = Number.parseInt(
    limpio.length === 3 ? limpio.split('').map((letra) => letra + letra).join('') : limpio,
    16,
  );
  if (!Number.isFinite(entero)) return COLOR_DE_RESPALDO;
  return [(entero >> 16) & 255, (entero >> 8) & 255, entero & 255];
}

/**
 * La tabla de 256 entradas que traduce alfa a color.
 *
 * SE PRECALCULA UNA VEZ POR PINTADO Y NO POR PIXEL. Un lienzo de 1.280x600 son 768.000 píxeles;
 * resolver la rampa dentro de ese bucle multiplica por cuatro el coste del volcado, que es la
 * parte cara.
 */
function tablaDeLaRampa(color: string, pasos: number): Uint8ClampedArray {
  const [rojo, verde, azul] = componentes(color);
  const tabla = new Uint8ClampedArray(256 * 4);

  for (let alfa = 0; alfa < 256; alfa += 1) {
    // ESCALONADA EN CINCO PASOS Y NO CONTINUA: son los mismos cinco escalones que enseña la
    // leyenda, y una rampa continua con una leyenda de cinco cuadros sería una leyenda que no
    // describe lo que hay en pantalla.
    const paso = Math.min(pasos, Math.ceil((alfa / 255) * pasos));
    const indice = alfa * 4;
    if (paso === 0) {
      tabla[indice + 3] = 0;
      continue;
    }
    const fuerza = paso / pasos;
    /*
      Del claro al saturado: se mezcla con blanco en los pasos bajos y sube la opacidad con la
      densidad, para que el escalón más bajo se lea sobre el fondo sin taparlo.

      LOS DOS COEFICIENTES SE AJUSTARON CONTRA DATOS REALES, y hacía falta. Con 0,85 de blanqueo y
      un alfa mínimo de 90, el primer escalón quedaba en un verde casi blanco al 35 % sobre un
      fondo claro: a escala nacional —donde la mayoría de los municipios no llega a solaparse con
      ningún otro— eso era casi todo el mapa, y el mapa de calor se veía vacío. Comprobado el 12 de
      septiembre de 2026 con 123 municipios sembrados: la mancha existía y no se distinguía del
      fondo. Con 0,55 y 110 el escalón bajo se ve sin taparlo y la rampa conserva sus cinco pasos
      distinguibles, que es lo que la leyenda promete.
    */
    const mezcla = 1 - fuerza;
    tabla[indice] = rojo + (255 - rojo) * mezcla * 0.55;
    tabla[indice + 1] = verde + (255 - verde) * mezcla * 0.55;
    tabla[indice + 2] = azul + (255 - azul) * mezcla * 0.55;
    tabla[indice + 3] = Math.round(110 + fuerza * 145);
  }
  return tabla;
}

/** Un escalón de la leyenda del calor. */
export interface EscalonDeCalor {
  readonly etiqueta: string;
  readonly color: string;
  /** El primer valor del escalón, suelto, para rotular sólo los extremos de la barra. */
  readonly desde: number;
}

/**
 * Los rótulos de los cinco escalones.
 *
 * SALEN DE AQUI Y NO DE LA PLANTILLA para que no puedan separarse del techo con el que se pinta:
 * si el techo sube y la leyenda sigue diciendo la cifra vieja, la leyenda miente y nadie lo nota.
 */
export function escalonesDeCalor(techoDeSolape: number, color: string): EscalonDeCalor[] {
  const pasos = pasosDeLaRampa(techoDeSolape);
  const tabla = tablaDeLaRampa(color, pasos);
  return Array.from({ length: pasos }, (_, indice) => {
    const paso = indice + 1;
    const alfa = Math.min(255, Math.round((paso / pasos) * 255));
    // LOS LIMITES SE CALCULAN SOBRE LOS PASOS QUE DE VERDAD HAY, no sobre cinco fijos: es lo que
    // garantiza que `hasta` nunca quede por debajo de `desde` ni se repita una banda.
    const desde = Math.round(((paso - 1) / pasos) * techoDeSolape) + 1;
    const hasta = Math.max(desde, Math.round((paso / pasos) * techoDeSolape));
    const base = alfa * 4;
    const etiqueta = paso === pasos
      ? `${desde} o más`
      : desde === hasta ? String(desde) : `${desde} a ${hasta}`;
    return {
      etiqueta,
      color: `rgba(${tabla[base]}, ${tabla[base + 1]}, ${tabla[base + 2]}, ${(tabla[base + 3] ?? 0) / 255})`,
      desde,
    };
  });
}

/**
 * La capa. Se añade y se quita como cualquier otra de Leaflet.
 *
 * VA EN EL PANEL `overlayPane` Y NO EN UNO PROPIO. Ahí vive el `<svg>` de la cartografía, así que
 * el calor queda ENCIMA del coroplético y DEBAJO de los marcadores, que es el orden que hace falta:
 * un mapa de calor que tapara los puntos dejaría sin poder pulsar las fichas.
 */
export class CapaDeCalor extends L.Layer {
  private lienzo: HTMLCanvasElement | null = null;
  /**
   * El lienzo auxiliar donde se acumula la densidad de UNA serie antes de leerla.
   *
   * SE REUTILIZA ENTRE SERIES Y ENTRE REPINTADOS. Crear uno por serie y por pintado dejaría al
   * recolector de basura un lienzo de varios megabytes por cada arrastre del mapa.
   */
  private auxiliar: HTMLCanvasElement | null = null;
  private series: readonly SerieDeCalor[];
  private opciones: OpcionesDeCalor;

  constructor(series: readonly SerieDeCalor[], opciones: OpcionesDeCalor) {
    super();
    this.series = series;
    this.opciones = opciones;
  }

  override onAdd(mapa: L.Map): this {
    const lienzo = L.DomUtil.create('canvas', 'capa-de-calor');
    lienzo.style.position = 'absolute';
    lienzo.style.pointerEvents = 'none';
    // El lienzo no aporta información que no esté en la leyenda y en la tabla, y un `<canvas>` sin
    // texto alternativo es ruido para un lector de pantalla.
    lienzo.setAttribute('aria-hidden', 'true');
    this.lienzo = lienzo;
    mapa.getPanes().overlayPane.appendChild(lienzo);

    mapa.on('moveend zoomend resize', this.redibujar, this);
    // `zoomanim` MUEVE EL LIENZO CON LA ANIMACION. Sin esto, durante el medio segundo que dura el
    // zoom animado la mancha se queda quieta mientras el mapa se mueve debajo, y se ve saltar. No
    // se repinta —serían sesenta repintados por segundo—: se traslada y se escala el lienzo ya
    // pintado, que es lo que hace Leaflet con sus propias capas.
    mapa.on('zoomanim', this.animarZoom, this);
    this.redibujar();
    return this;
  }

  override onRemove(mapa: L.Map): this {
    mapa.off('moveend zoomend resize', this.redibujar, this);
    mapa.off('zoomanim', this.animarZoom, this);
    this.lienzo?.remove();
    this.lienzo = null;
    return this;
  }

  /** Cambia los datos sin quitar y volver a poner la capa. */
  actualizar(series: readonly SerieDeCalor[], opciones: OpcionesDeCalor): void {
    this.series = series;
    this.opciones = opciones;
    this.redibujar();
  }

  private animarZoom(evento: L.ZoomAnimEvent): void {
    const mapa = this._map as L.Map | undefined;
    const lienzo = this.lienzo;
    if (!mapa || !lienzo) return;

    // LOS DOS METODOS SON INTERNOS DE LEAFLET Y NO ESTAN EN `@types/leaflet`. Se declaran aquí con
    // su forma exacta en vez de silenciar la comprobación: así el día que Leaflet cambie la firma
    // esto deja de compilar, en vez de dejar de funcionar. Son los mismos que usa
    // `L.ImageOverlay._animateZoom`, que es de donde sale este cálculo.
    const interno = mapa as unknown as {
      _getCenterOffset: (centro: L.LatLng) => L.Point;
      _getMapPanePos: () => L.Point;
    };
    const escala = mapa.getZoomScale(evento.zoom, mapa.getZoom());
    const desplazamiento = interno
      ._getCenterOffset(evento.center)
      .multiplyBy(-escala)
      .subtract(interno._getMapPanePos());
    L.DomUtil.setTransform(lienzo, desplazamiento, escala);
  }

  private redibujar(): void {
    const mapa = this._map as L.Map | undefined;
    const lienzo = this.lienzo;
    if (!mapa || !lienzo) return;

    const tamano = mapa.getSize();
    L.DomUtil.setPosition(lienzo, mapa.containerPointToLayerPoint([0, 0]));

    /*
      SE PINTA A UN PIXEL CSS Y NO A LA DENSIDAD DE LA PANTALLA, y es una decisión y no un descuido.

      El calor es una superficie DIFUSA: no tiene bordes ni texto, así que el detalle de una
      pantalla retina no se aprecia. Lo que sí se aprecia es el coste: cada serie exige leer el
      lienzo entero con `getImageData`, y a doble densidad eso son cuatro veces más píxeles. Con
      catorce territorios sonoros —el caso normal del lente— la diferencia es entre repintar al
      soltar el ratón y bloquear la pestaña medio segundo en cada arrastre.
    */
    const ancho = Math.max(1, Math.round(tamano.x));
    const alto = Math.max(1, Math.round(tamano.y));
    lienzo.width = ancho;
    lienzo.height = alto;
    lienzo.style.width = `${tamano.x}px`;
    lienzo.style.height = `${tamano.y}px`;

    const contexto = lienzo.getContext('2d', { willReadFrequently: true });
    if (!contexto) return;
    contexto.clearRect(0, 0, ancho, alto);

    const series = this.series.filter((serie) => serie.puntos.length > 0);
    if (series.length === 0) return;

    // CUANTO MIDE EL RADIO EN EL SUELO, que es lo que la leyenda necesita. Mercator estira las
    // distancias con el coseno de la latitud, así que el mismo píxel vale menos metros en La
    // Guajira que en el Amazonas. Colombia va de 4°S a 13°N y la diferencia es de un 2 %: pequeña,
    // pero de las que se corrigen con una línea y luego no hay que explicar.
    const centro = mapa.getCenter();
    const metrosPorPixel =
      (Math.cos((centro.lat * Math.PI) / 180) * 2 * Math.PI * RADIO_TERRESTRE) /
      (256 * Math.pow(2, mapa.getZoom()));
    const radio = this.opciones.radioEnPixeles;
    this.opciones.alCambiarEscala?.(radio * metrosPorPixel);

    const auxiliar = this.prepararAuxiliar(ancho, alto);
    const contextoAuxiliar = auxiliar?.getContext('2d', { willReadFrequently: true });
    if (!auxiliar || !contextoAuxiliar) return;

    /*
      GANADOR POR PIXEL, Y NO MEZCLA DE COLORES.

      Con catorce series translúcidas superpuestas, las zonas donde coinciden tres o cuatro dan un
      pardo del que no se puede leer ningún territorio: la mezcla de muchos tintes siempre tiende al
      gris. Se colorea por el grupo que MAS densidad tiene en cada píxel y la intensidad la da la
      densidad TOTAL, que es la misma semántica que el coroplético cualitativo —el color dice cuál,
      el tono dice cuánto— pero con fronteras blandas y orgánicas en vez de los límites
      administrativos. Que era el punto: un territorio sonoro no se detiene donde acaba un
      departamento.
    */
    const pixeles = ancho * alto;
    const total = new Float32Array(pixeles);
    const mejor = new Float32Array(pixeles);
    const ganador = new Uint8Array(pixeles);

    const alfaPorPunto = 1 / Math.max(1, this.opciones.techoDeSolape);

    for (let indiceSerie = 0; indiceSerie < series.length; indiceSerie += 1) {
      contextoAuxiliar.clearRect(0, 0, ancho, alto);
      contextoAuxiliar.globalCompositeOperation = 'lighter';
      for (const punto of series[indiceSerie].puntos) {
        const proyectado = mapa.latLngToContainerPoint([punto.lat, punto.lng]);
        const x = proyectado.x;
        const y = proyectado.y;
        if (x < -radio || y < -radio || x > ancho + radio || y > alto + radio) continue;

        const degradado = contextoAuxiliar.createRadialGradient(x, y, 0, x, y, radio);
        degradado.addColorStop(0, `rgba(255,255,255,${alfaPorPunto})`);
        degradado.addColorStop(1, 'rgba(255,255,255,0)');
        contextoAuxiliar.fillStyle = degradado;
        contextoAuxiliar.beginPath();
        contextoAuxiliar.arc(x, y, radio, 0, Math.PI * 2);
        contextoAuxiliar.fill();
      }
      contextoAuxiliar.globalCompositeOperation = 'source-over';

      const densidad = contextoAuxiliar.getImageData(0, 0, ancho, alto).data;
      for (let pixel = 0; pixel < pixeles; pixel += 1) {
        const alfa = densidad[pixel * 4 + 3];
        if (alfa === 0) continue;
        total[pixel] += alfa;
        if (alfa > mejor[pixel]) {
          mejor[pixel] = alfa;
          ganador[pixel] = indiceSerie;
        }
      }
    }

    // Una tabla de rampa por serie, calculada una vez y no por píxel.
    const pasos = pasosDeLaRampa(this.opciones.techoDeSolape);
    const tablas = series.map((serie) => tablaDeLaRampa(serie.color, pasos));

    const imagen = contexto.createImageData(ancho, alto);
    const salida = imagen.data;
    for (let pixel = 0; pixel < pixeles; pixel += 1) {
      const acumulado = total[pixel];
      if (acumulado === 0) continue;
      const base = Math.min(255, Math.round(acumulado)) * 4;
      const tabla = tablas[ganador[pixel]];
      const destino = pixel * 4;
      salida[destino] = tabla[base] ?? 0;
      salida[destino + 1] = tabla[base + 1] ?? 0;
      salida[destino + 2] = tabla[base + 2] ?? 0;
      salida[destino + 3] = tabla[base + 3] ?? 0;
    }
    contexto.putImageData(imagen, 0, 0);
  }

  private prepararAuxiliar(ancho: number, alto: number): HTMLCanvasElement | null {
    if (typeof document === 'undefined') return null;
    const auxiliar = this.auxiliar ?? document.createElement('canvas');
    this.auxiliar = auxiliar;
    if (auxiliar.width !== ancho) auxiliar.width = ancho;
    if (auxiliar.height !== alto) auxiliar.height = alto;
    return auxiliar;
  }
}
