import { Component, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LucideArrowRight } from '@lucide/angular';
import { FestivalesPublicosService } from '../../../../core/services/festivales-publicos.service';
import { NavigationService } from '../../../../core/services/navigation.service';
import { ContentWrapperComponent } from '../../../../shared/components/ui/content-wrapper/content-wrapper.component';
import { SectionHeaderComponent } from '../../../../shared/components/ui/section-header/section-header.component';
import { LoadingStateComponent, ErrorStateComponent } from '../../../../shared/components/ui/remote-state/remote-state.component';
import { CATEGORIAS_ECOSISTEMA, CategoriaEcosistema } from '../../../../core/services/categorias-ecosistema.config';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import * as MapDomain from '../../../map/domain/map-domain';

type TarjetaCategoriaEcosistema = CategoriaEcosistema & { img: string };

/**
 * AQUI ESTUVO `HOME_COUNT_KEYS`, un mapa de icono a clave de recuento, y pintaba dos cifras
 * falsas. Traducía categorías futuras a claves que el mapa llenaba con registros de otro módulo,
 * de modo que una tarjeta podía mostrar una cifra perteneciente a un proceso distinto.
 *
 * La cifra sale ahora de `category.countKey`, que es la unica declaracion de donde viene el dato
 * y que deja Escenarios sin clave a proposito. Sin clave no hay numero: la tarjeta calla.
 */

@Component({
  selector: 'app-mapa-ecosistemico-preview',
  standalone: true,
  imports: [
    CommonModule,
    LucideArrowRight,
    ContentWrapperComponent,
    SectionHeaderComponent,
    LoadingStateComponent,
    ErrorStateComponent
  ],
  templateUrl: './mapa-ecosistemico-preview.component.html'
})
export class MapaEcosistemicoPreviewComponent implements OnInit {
  @Output() navigateToMapLayer = new EventEmitter<string>();

  /**
   * EL MISMO BLOQUE EN DOS PAGINAS, Y POR ESO ESTAS DOS ENTRADAS.
   *
   * Lo pidio la direccion de producto sobre la rejilla de /ecosistema: «este
   * es el del home […] debes traerlo aqui, si se cambia algo del home se cambia aqui». Lo que
   * tiene que ser identico es la rejilla de seis: foto, rotulo y cifra. Lo que cambia es el
   * mobiliario de cada pagina.
   *
   * `mostrarEncabezado`: el titulo y el boton «Ver en el mapa» del bloque. En /ecosistema sobran
   * porque la seccion ya trae su propio titulo desde el CMS (`ecosistema_processes_*`) y el mapa
   * tiene su propia tarjeta mas arriba.
   *
   * `mostrarSalida`: el boton «Ver ecosistema» del pie. En /ecosistema seria un enlace a la
   * pagina en la que ya estas.
   *
   * Las dos por defecto en `true`: el Inicio no cambia.
   */
  @Input() mostrarEncabezado = true;
  @Input() mostrarSalida = true;

  /**
   * El ancla del bloque. `mapa-home` es la del Inicio, y era el valor unico hasta que /ecosistema
   * empezo a montar este mismo componente: la portada del Ecosistema pintaba un `id="mapa-home"`
   * que no es de esa pagina y al que nadie apunta desde alli.
   */
  @Input() anclaId = 'mapa-home';

  private readonly navigation = inject(NavigationService);
  private readonly festivalesPublicos = inject(FestivalesPublicosService);

  private readonly webTexts = inject(TextosWebService);

  /** Los textos propios de este bloque de la portada. */
  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }

  /*
   * LAS SEIS FOTOS SON `computed` Y LA LISTA TAMBIEN, desde.
   *
   * Era un campo llano con `IMAGENES_DE_GALERIA[6 + index]`. Como en los otros tres bloques
   * de la portada: `getWebImage` lee una señal, y calcularlo una sola vez al construir el
   * componente congela la foto compilada aunque despues llegue el manifiesto o un borrador.
   *
   * LOS SEIS ROTULOS NO SALEN DEL CMS y no es un olvido: son el nombre del proceso, que es lo
   * que `CATEGORIAS_ECOSISTEMA` declara para las tres superficies —esta rejilla, el desplegable
   * del menu y la portada de /ecosistema, que desde monta este mismo
   * componente—. Abrirlos al panel dejaria que una edicion renombrara «Festivales» en las tres
   * a la vez, incluida la ruta que la tarjeta anuncia.
   *
   * EL FILTRO POR `proceso` YA NO DESCARTA NADA: el catalogo son seis y los seis lo son. Se
   * conserva porque es la declaracion de que esta rejilla solo admite procesos con tabla propia,
   * y `mapa-ecosistemico-preview.component.spec.ts` lo comprueba en las dos direcciones.
   */
  readonly categories = computed<TarjetaCategoriaEcosistema[]>(() => CATEGORIAS_ECOSISTEMA
    .filter(category => category.proceso)
    .map((category, index) => ({
      ...category,
      img: this.webTexts.getWebImage(`home_eco_${index + 1}`),
    })));

  isLoading = false;
  isRefreshing = false;
  isError = false;
  error: any = null;

  readonly recordsByType = signal<Record<string, number>>({});

  readonly totalRecords = computed(() => Object.values(this.recordsByType()).reduce((sum, value) => sum + value, 0));

  ngOnInit(): void {
    this.loadMapData();
  }

  /**
   * LAS CIFRAS SALEN DE LOS PROCESOS, NO DEL MAPA.
   *
   * <b>Qué había aquí.</b> `fetchMapCountsBundle()`, que descarga la cartografía de Colombia
   * —513 KB medidos contra el API local—, los registros de Festivales
   * y la agenda entera, y calcula los recuentos POR DEPARTAMENTO que el mapa necesita para
   * pintarse. De todo eso, esta previsualización usaba un número.
   *
   * <b>Por qué se cambia, y no es solo el peso.</b> La dirección de producto fijó la regla ese mismo
   * día: «lo que aparece en el home no es un preview del mapa ecosistémico, es un preview de los
   * registros de ecosistema directamente; si algún día no está el mapa, ese apartado debe seguir
   * dando los datos». Atado al mapa, el día que fallara la cartografía la portada dejaría de decir
   * cuántos Festivales hay registrados.
   *
   * <b>Cómo queda.</b> Cada proceso aporta su propio recuento. Hoy solo Festivales tiene directorio
   * público, así que hay una sola llamada —el listado con la página más pequeña, leyendo `total`—;
   * cuando Escuelas, Escenarios, Mercados, Redes o Lutería abran el suyo, se añaden aquí al lado
   * sin tocar nada del mapa.
   */
  loadMapData(isRefresh = false): void {
    if (isRefresh) {
      this.isRefreshing = true;
    } else {
      this.isLoading = true;
    }
    this.isError = false;
    this.error = null;

    this.festivalesPublicos.contarPublicados().subscribe({
      next: (festivales) => {
        // UNA CLAVE POR PROCESO CON DIRECTORIO ABIERTO. Los cinco restantes no declaran `countKey`
        // en la configuración, así que `count()` devuelve `null` y la tarjeta pinta una raya en vez
        // de tomar prestada la cifra de otro —que es lo que pasaba: Escenarios llegaba con el
        // número de Lutería—.
        this.recordsByType.set({ festivals: festivales });
        this.isLoading = false;
        this.isRefreshing = false;
      },
      error: (err) => {
        this.error = err;
        this.isError = true;
        this.isLoading = false;
        this.isRefreshing = false;
      }
    });
  }

  retryMapData(): void {
    this.loadMapData(false);
  }

  /**
   * La cifra de una tarjeta, o `null` cuando no hay de donde sacarla.
   *
   * `null` Y NO CERO. Cero es una afirmacion —«no hay escenarios registrados»— y aqui la verdad
   * es otra: el paquete de recuentos no trae escenarios, asi que no se sabe. La plantilla pinta
   * una raya.
   */
  count(category: TarjetaCategoriaEcosistema): number | null {
    if (!category.countKey) return null;
    return this.recordsByType()[category.countKey] ?? 0;
  }

  formatMetricValue(value: number): string {
    return MapDomain.formatMetricValue(value);
  }

  openCategory(category: CategoriaEcosistema): void {
    // DOS CONDICIONES, NO UNA, Y LA SEGUNDA LA IMPONE EL TIPO. Antes bastaba con el estado, y
    // las cinco categorías sin pantalla declaraban igualmente una ruta inexistente. Ahora solo
    // tiene ruta la que tiene a dónde ir: marcar una como disponible sin darle pantalla ya no
    // compila, en vez de llevar al comodín de «página no encontrada».
    if (category.status !== 'Disponible' || !category.route) return;
    this.navigation.routerNavigate(category.route);
  }

  /**
   * La portada del Ecosistema: directorios, rutas de consulta y como participar.
   *
   * NO ES EL MISMO DESTINO QUE «Ver en el mapa», que abre el mapa con sus capas. Se separan
   * porque son dos lecturas distintas del mismo ecosistema, y hasta ahora la portada solo se
   * alcanzaba desde el menu de navegacion.
   */
  verEcosistema(): void {
    this.navigation.routerNavigate('ecosistema');
  }

  onNavigateToMapLayer(layer: string): void {
    this.navigateToMapLayer.emit(layer);
  }

}
