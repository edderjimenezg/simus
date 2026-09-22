import * as L from 'leaflet';
import { CapaDeCalor, OpcionesDeCalor, SerieDeCalor } from './capa-de-calor';

/**
 * El dibujo temático del geovisor: los símbolos municipales y la superficie de calor.
 *
 * <b>POR QUE VIVE FUERA DEL COMPONENTE.</b> `mapa-ecosistemico-page.component.ts` mezclaba dos
 * responsabilidades que no se parecen en nada: decidir QUE se dibuja —que es reactividad de Angular,
 * señales y computeds— y dibujarlo —que es Leaflet, capas, iconos y globos—. Mezcladas, cualquier
 * cambio en una obligaba a leer la otra, y el fichero pasaba de cinco mil líneas.
 *
 * <b>LA COSTURA ES QUE AQUI NO ENTRA NI UNA SEÑAL.</b> Esta clase recibe datos planos —ya
 * calculados, ya coloreados, ya dimensionados— y devuelve capas de Leaflet. No sabe qué es un lente
 * ni qué modo está activo; sólo sabe pintar lo que le den. Esa frontera es lo que permite cambiar la
 * lógica sin tocar el dibujo y al revés.
 *
 * <b>Y NO GUARDA ESTADO DE NEGOCIO</b>, sólo las capas que ha puesto en el mapa, porque alguien
 * tiene que poder quitarlas.
 */

/** Un símbolo municipal, ya resuelto: dónde va, de qué tamaño y de qué color. */
export interface SimboloMunicipal {
  readonly codigo: string;
  readonly nombre: string;
  readonly lat: number;
  readonly lng: number;
  readonly total: number;
  readonly color: string;
  readonly lado: number;
  readonly atenuado: boolean;
  /** Los primeros nombres de proceso, para el globo. Vienen recortados de fuera. */
  readonly titulos: readonly string[];
  /** Cuántos quedaron fuera de `titulos`. */
  readonly restantes: number;
}

/** Lo que el agrupador necesita saber de cada marcador para componer su burbuja. */
interface OpcionesDeSenalador extends L.MarkerOptions {
  colorDelLente?: string;
}

/** Lo que el dibujo devuelve al pulsar un municipio. */
export type AlPulsarMunicipio = (
  nombre: string,
  codigo: string,
  evento: L.LeafletMouseEvent,
  ancla: { lat: number; lng: number },
) => void;

/** Cómo se dimensiona un símbolo. Se pasa de fuera para que la escala viva en un solo sitio. */
export type LadoDelSimbolo = (total: number, maximo: number) => number;

export class DibujoTematico {
  private grupoDeSenaladores?: L.MarkerClusterGroup;
  private capaDeCalor?: CapaDeCalor;

  /**
   * Los señaladores municipales, agrupados.
   *
   * UNO POR MUNICIPIO Y NO UNO POR PROCESO. Dibujar una marca por proceso apila varias en el mismo
   * píxel: se ve una y se pierden las demás. La cifra dentro del símbolo dice cuántas hay debajo.
   *
   * SE ANCLA POR EL CENTRO Y NO POR UNA PUNTA. Una punta de alfiler señala un píxel del suelo, y el
   * dato tiene resolución de MUNICIPIO: la punta afirmaría una precisión que no existe.
   *
   * LOS `data-*` NO SON DECORACION: son lo que permite al comprobador de navegador verificar, uno
   * por uno, que la posición dibujada es la que el catálogo da para ese municipio.
   */
  dibujarSimbolos(
    mapa: L.Map,
    simbolos: readonly SimboloMunicipal[],
    maximo: number,
    colorDeRespaldo: string,
    ladoDelSimbolo: LadoDelSimbolo,
    alPulsar: AlPulsarMunicipio,
    crearGrupo: (opciones: L.MarkerClusterGroupOptions) => L.MarkerClusterGroup,
  ): void {
    const grupo = this.crearAgrupador(maximo, colorDeRespaldo, ladoDelSimbolo, crearGrupo);

    for (const simbolo of simbolos) {
      const icono = L.divIcon({
        className: 'senalador-municipal-envoltura',
        html:
          `<span class="senalador-municipal" style="--senalador-color: ${simbolo.color}` +
          `; width: ${simbolo.lado}px; height: ${simbolo.lado}px` +
          `; opacity: ${simbolo.atenuado ? 0.25 : 1}"` +
          ` data-municipio="${simbolo.codigo}" data-lat="${simbolo.lat}"` +
          ` data-lng="${simbolo.lng}" data-total="${simbolo.total}">` +
          `<span class="senalador-municipal-cifra">${simbolo.total}</span></span>`,
        iconSize: [simbolo.lado, simbolo.lado],
        iconAnchor: [simbolo.lado / 2, simbolo.lado / 2],
      });

      const marcador = L.marker([simbolo.lat, simbolo.lng], {
        icon: icono,
        keyboard: true,
        title: `${simbolo.nombre}: ${simbolo.total} ${simbolo.total === 1 ? 'proceso' : 'procesos'}`,
        // El total viaja en `alt` porque es la única propiedad de `MarkerOptions` que acepta una
        // cadena libre y sobrevive al agrupador; lo lee la burbuja para sumar procesos y no
        // municipios. El color va en una opción propia: Leaflet conserva las que no conoce, y así no
        // hay que codificarlo dentro de `alt` con un separador que acabaría partiéndose.
        alt: String(simbolo.total),
        colorDelLente: simbolo.color,
      } as OpcionesDeSenalador);

      const nombres = simbolo.titulos.map((titulo) => `<li>${titulo}</li>`).join('');
      marcador.bindTooltip(
        `<div class="municipality-tooltip">`
          + `<p class="tooltip-title">${simbolo.nombre}</p>`
          + `<p class="tooltip-value">${simbolo.total} ${simbolo.total === 1 ? 'proceso' : 'procesos'}</p>`
          + `<ul class="tooltip-lista">${nombres}</ul>`
          + (simbolo.restantes > 0 ? `<p class="tooltip-nota">y ${simbolo.restantes} más</p>` : '')
          + `<p class="tooltip-nota">Punto de referencia del municipio: ubica el municipio, no el proceso.</p>`
          + `</div>`,
        { direction: 'top', className: 'custom-municipality-tooltip', offset: [0, -14] },
      );

      marcador.on('click', (evento: L.LeafletMouseEvent) => {
        L.DomEvent.stopPropagation(evento);
        alPulsar(simbolo.nombre, simbolo.codigo, evento, { lat: simbolo.lat, lng: simbolo.lng });
      });

      grupo.addLayer(marcador);
    }

    grupo.addTo(mapa);
    this.grupoDeSenaladores = grupo;
  }

  /**
   * El agrupador que junta los señaladores que se pisan.
   *
   * <b>LA BURBUJA DEL GRUPO TAMBIEN ES PROPORCIONAL.</b> Dibujaba todas de 36 px: a escala nacional
   * —la única vista donde se mira el país— casi todo queda agrupado, así que un modo llamado
   * «Símbolos proporcionales» enseñaba decenas de círculos idénticos y la codificación desaparecía
   * justo donde tenía que servir. Una burbuja es un símbolo proporcional como cualquier otro:
   * representa la suma de lo que resume y usa la misma escala, así que al abrirse el área se
   * conserva y la lectura no salta.
   *
   * <b>EL COLOR ES EL MAS FRECUENTE ENTRE SUS HIJOS</b> y no el del primero: un grupo de cinco
   * municipios de los que cuatro son de un territorio es un grupo de ese territorio, y quedarse con
   * el que Leaflet ponga primero haría bailar el color al mover el mapa.
   *
   * `disableClusteringAtZoom: 7` es el mismo umbral con el que aparecen los rótulos municipales: a
   * partir de ahí el mapa nombra municipios uno a uno y agrupar contradiría lo que él mismo dice.
   * `spiderfyOnMaxZoom: false` porque el abanico separa los marcadores de su sitio real.
   */
  private crearAgrupador(
    maximo: number,
    colorDeRespaldo: string,
    ladoDelSimbolo: LadoDelSimbolo,
    crearGrupo: (opciones: L.MarkerClusterGroupOptions) => L.MarkerClusterGroup,
  ): L.MarkerClusterGroup {
    return crearGrupo({
      maxClusterRadius: 28,
      disableClusteringAtZoom: 7,
      showCoverageOnHover: false,
      spiderfyOnMaxZoom: false,
      chunkedLoading: true,
      iconCreateFunction: (grupo) => {
        const hijos = grupo.getAllChildMarkers();
        const total = hijos.reduce((suma, marcador) => suma + Number(marcador.options.alt || 0), 0);

        const votos = new Map<string, number>();
        for (const marcador of hijos) {
          const color = (marcador.options as OpcionesDeSenalador).colorDelLente;
          if (color) votos.set(color, (votos.get(color) ?? 0) + 1);
        }
        let color = colorDeRespaldo;
        let mayor = 0;
        for (const [candidato, cuantos] of votos) {
          if (cuantos > mayor) { color = candidato; mayor = cuantos; }
        }

        const lado = ladoDelSimbolo(total, maximo);
        return L.divIcon({
          className: 'senalador-municipal-envoltura',
          html:
            `<span class="senalador-municipal senalador-municipal-grupo"`
            + ` style="--senalador-color: ${color}; width: ${lado}px; height: ${lado}px"`
            + ` data-grupo="${grupo.getChildCount()}" data-total="${total}">`
            + `<span class="senalador-municipal-cifra">${total}</span></span>`,
          iconSize: [lado, lado],
          iconAnchor: [lado / 2, lado / 2],
        });
      },
    });
  }

  /** La superficie de calor. Se actualiza en vez de recrearse, para no parpadear al repintar. */
  dibujarCalor(mapa: L.Map, series: readonly SerieDeCalor[], opciones: OpcionesDeCalor): void {
    if (this.capaDeCalor) {
      this.capaDeCalor.actualizar(series, opciones);
      return;
    }
    this.capaDeCalor = new CapaDeCalor(series, opciones);
    this.capaDeCalor.addTo(mapa);
  }

  /** Quita los señaladores. El calor se retira aparte porque sobrevive entre repintados. */
  limpiarSimbolos(mapa: L.Map): void {
    if (!this.grupoDeSenaladores) return;
    mapa.removeLayer(this.grupoDeSenaladores);
    this.grupoDeSenaladores = undefined;
  }

  /** Quita la superficie de calor. Sólo al salir del modo: quitarla y reponerla en cada repintado
   *  cuesta una lectura completa del lienzo de más y se nota al arrastrar. */
  limpiarCalor(mapa: L.Map): void {
    if (!this.capaDeCalor) return;
    mapa.removeLayer(this.capaDeCalor);
    this.capaDeCalor = undefined;
  }
}
