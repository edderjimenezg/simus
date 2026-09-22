// `OnDestroy` se retiró con su método vacío: el listener de desplazamiento lo quita Angular
// solo, porque está declarado con `@HostListener` y no a mano.
import { Component, OnInit, inject, signal, computed, HostListener } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { 
  LucideArrowLeft, 
  LucideArrowRight, 
  LucideArrowUpRight, 
  LucideChevronDown, 
  LucideClock, 
  LucideMail, 
  LucideSearch, 
  LucideShare2, 
  LucideBookmark, 
  LucideCheckCircle2, 
  LucideLayoutGrid, 
  LucideList 
} from '@lucide/angular';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { NoticiasService } from '../../../../core/services/noticias.service';
import { BoletinService } from '../../../../core/services/boletin.service';
import { PageHeroComponent } from '../../../../shared/components/ui/page-hero/page-hero.component';
import {
  contarFiltrosPuestos,
  enlazarFiltrosConLaUrl,
  filtroDePagina,
  filtroDeTexto,
  filtroDeVista,
  limpiarFiltros,
} from '../../../../shared/utils/filtros-en-la-url';
import { 
  LoadingStateComponent, 
  ErrorStateComponent, 
  EmptyStateComponent 
} from '../../../../shared/components/ui/remote-state/remote-state.component';
import { 
  getNewsDateKeys, 
  splitHeroHeadline 
} from '../../../../core/services/data-transforms';
import { NoticiaDeDiseno, aNoticiaDeDiseno } from '../../../../core/services/adaptadores-del-diseno';

@Component({
  selector: 'app-noticias-page',
  standalone: true,
  imports: [
    CommonModule,
    PageHeroComponent,
    LoadingStateComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    LucideArrowLeft,
    LucideArrowRight,
    LucideArrowUpRight,
    LucideChevronDown,
    LucideClock,
    LucideMail,
    LucideSearch,
    LucideShare2,
    LucideBookmark,
    LucideCheckCircle2,
    LucideLayoutGrid,
    LucideList
  ],
  templateUrl: './noticias-page.component.html'
})
export class NoticiasPageComponent implements OnInit {
  Math = Math;
  private webTexts = inject(TextosWebService);
  private noticiasApi = inject(NoticiasService);
  private boletin = inject(BoletinService);
  private sanitizer = inject(DomSanitizer);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // Reactivo a la ruta: /noticias/:articleId identifica la noticia abierta,
  // así el artículo es enlazable, compartible y sobrevive a un refresco.
  private readonly routeParamMap = toSignal(this.route.paramMap);
  private readonly articleIdParam = computed(() => this.routeParamMap()?.get('articleId') ?? null);

  readonly localSelectedArticle = computed(() => {
    const id = this.articleIdParam();
    if (!id) return null;
    return this.newsData().find((item) => String(item.id) === id) ?? null;
  });

  newsSearchTerm = signal<string>('');
  newsCategoryFilter = signal<string>('all');
  newsSortOrder = signal<string>('newest');
  currentPage = signal<number>(1);
  viewLayout = signal<'grid' | 'list'>('grid');
  readingProgress = signal<number>(0);

  isLoading = false;
  isRefreshing = false;
  isError = false;
  error: { message?: string } | null = null;

// TIPADO Y NO `any`, que es lo unico que se toca del codigo portado ademas de la conexion.
// El diseno viene de un desarrollo mas laxo; poner el tipo no cambia un pixel y es lo que
// permite que el trinquete de tipado siga bajando en vez de subir.
  newsData = signal<NoticiaDeDiseno[]>([]);

  // Subscription state
  isSubscribed = false;
  subscriberEmail = '';
  subscriptionError = '';

  ITEMS_PER_PAGE = 6;

  // Derivadas usando computed()
  featuredPrimary = computed(() => this.newsData()[0] || null);
  featuredSecondary = computed(() => this.newsData().slice(1, 3));
  newsListPool = computed(() => this.newsData().slice(3));

  newsCategoryOptions = computed(() => {
    const categories = this.newsListPool().map((item) => item.category).filter(Boolean);
    return [...new Set(categories)];
  });

  filteredListNews = computed(() => {
    const pool = this.newsListPool();
    const term = this.newsSearchTerm().trim().toLowerCase();
    const category = this.newsCategoryFilter();
    const sortOrder = this.newsSortOrder();

    return [...pool]
      .filter((item) => {
        const searchableText = [item.title, item.desc, item.category, item.content]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();

        const matchesSearch = !term || searchableText.includes(term);
        if (!matchesSearch) return false;

        const matchesCategory = category === 'all' || item.category === category;

        return matchesCategory;
      })
      .sort((leftItem, rightItem) => {
        const leftDate = getNewsDateKeys(leftItem.date)?.dateKey || '';
        const rightDate = getNewsDateKeys(rightItem.date)?.dateKey || '';

        if (leftDate === rightDate) {
          return leftItem.title.localeCompare(rightItem.title, 'es', { sensitivity: 'base' });
        }

        return sortOrder === 'oldest'
          ? leftDate.localeCompare(rightDate)
          : rightDate.localeCompare(leftDate);
      });
  });

  totalPages = computed(() => {
    const count = this.filteredListNews().length;
    return Math.max(1, Math.ceil(count / this.ITEMS_PER_PAGE));
  });

  paginatedNews = computed(() => {
    const list = this.filteredListNews();
    const page = Math.min(this.currentPage(), this.totalPages());
    const startIndex = (page - 1) * this.ITEMS_PER_PAGE;
    return list.slice(startIndex, startIndex + this.ITEMS_PER_PAGE);
  });

  selectedArticleHeroCopy = computed(() => {
    const article = this.localSelectedArticle();
    return article ? splitHeroHeadline(article.title || '') : { title: '', titleAccent: '' };
  });

  selectedArticleSafeContent = computed<SafeHtml>(() => {
    const article = this.localSelectedArticle();
    return article?.content
      ? this.sanitizer.bypassSecurityTrustHtml(article.content)
      : '';
  });

  /**
   * Los filtros de esta pantalla, declarados una vez y enganchados a la URL.
   *
   * <b>ANTES NO ESTABAN EN NINGUNA PARTE MAS QUE EN ESTAS SEÑALES.</b> Filtrar por «Convocatorias»
   * y pasarle el enlace a alguien le abría la lista entera; recargar lo perdía; y volver de una
   * noticia al listado también. La noticia abierta sí era enlazable desde hacía cortes —vive en la
   * ruta, `/noticias/:articleId`—, y el filtro que la encontró no.
   *
   * <b>EL VACIO SE DECLARA POR FILTRO.</b> Aquí la categoría vacía se escribe `'all'` y el orden
   * arranca en `'newest'`: unificar esas palabras obligaría a tocar la lógica de filtrado, que es
   * código del diseño aprobado. La pieza compartida admite el vacío de cada uno y deja la URL
   * limpia igual.
   */
  readonly filtros = [
    filtroDeTexto('q', this.newsSearchTerm),
    filtroDeTexto('categoria', this.newsCategoryFilter, 'all'),
    filtroDeTexto('orden', this.newsSortOrder, 'newest'),
    filtroDeVista('vista', this.viewLayout, 'grid'),
    filtroDePagina(this.currentPage),
  ];

  /** Cuántos filtros hay puestos, para rotular «Limpiar filtros» con su número. */
  readonly filtrosPuestos = computed(() => contarFiltrosPuestos(this.filtros));

  constructor() {
    enlazarFiltrosConLaUrl(this.filtros);
  }

  ngOnInit() {
    this.loadNewsData();
  }

  @HostListener('window:scroll', [])
  onWindowScroll() {
    if (!this.localSelectedArticle()) return;
    const scrollHeight = document.documentElement.scrollHeight;
    const clientHeight = document.documentElement.clientHeight;
    const totalHeight = scrollHeight - clientHeight;
    if (totalHeight > 0) {
      const progress = (window.scrollY / totalHeight) * 100;
      this.readingProgress.set(progress);
    }
  }

  loadNewsData(isRefresh = false) {
    if (isRefresh) {
      this.isRefreshing = true;
    } else {
      this.isLoading = true;
    }
    this.isError = false;
    this.error = null;

    // CONEXION REAL, y es lo unico que cambia respecto del diseno aprobado. Antes llamaba a
    // `fetchNewsRecords`, que pasaba por `buildNewsItemFromRecord`: aquella funcion asignaba una
    // IMAGEN ALEATORIA cuando el registro no traia ninguna, que es justo lo que hacia que esta
    // pantalla estuviera falsamente conectada. Aqui una noticia sin imagen se queda sin imagen.
    void this.noticiasApi.listarPublicas({ tamano: 50 }).then((resultado) => {
      if (resultado.ok && resultado.data) {
        this.newsData.set(resultado.data.items.map(aNoticiaDeDiseno));
      } else {
        this.error = { message: resultado.error };
        this.isError = true;
      }
      this.isLoading = false;
      this.isRefreshing = false;
    });
  }

  retry() {
    this.loadNewsData(false);
  }

  getWebText(key: string, fallback = ''): string {
    return this.webTexts.getWebText(key) || fallback;
  }

  /**
   * Abre una noticia del listado.
   *
   * <b>NAVEGA POR EL SLUG Y NO POR EL IDENTIFICADOR, y esto era un enlace roto.</b> La ruta se
   * declara como `/noticias/:slug` y la ficha resuelve con `obtenerPublica(slug)`; el listado
   * navegaba con `article.id`, así que pulsar cualquier noticia llevaba a `/noticias/2` y la ficha
   * contestaba «ESTA NOTICIA NO ESTÁ PUBLICADA» sobre una noticia que sí lo está.
   *
   * <b>NO LO VIO NINGUNA PRUEBA</b> porque las dos mitades funcionan por separado: el listado
   * navega y la ficha abre —comprobado con el slug a mano—. Lo que fallaba era el paso de una a la
   * otra, que es justo lo que ningún unitario mira. Apareció al recorrer el sitio entero como lo
   * recorre alguien.
   *
   * El `slug` viene en `NoticiaDeDiseno` desde el adaptador, así que no hace falta pedir nada más.
   */
  handleSelectArticle(article: NoticiaDeDiseno | null) {
    this.readingProgress.set(0);
    window.scrollTo({ top: 0, behavior: 'instant' });
    if (article?.slug) {
      this.router.navigate(['/noticias', article.slug]);
    } else {
      this.router.navigate(['/noticias']);
    }
  }

  setCategoryFilter(category: string) {
    this.newsCategoryFilter.set(category);
    this.currentPage.set(1);
    this.scrollToFeed();
  }

  setSortOrder(order: string) {
    this.newsSortOrder.set(order);
    this.currentPage.set(1);
    this.scrollToFeed();
  }

  onSearchChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.newsSearchTerm.set(input.value);
    this.currentPage.set(1);
  }

  clearSearch() {
    this.newsSearchTerm.set('');
    this.currentPage.set(1);
  }

  setViewLayout(layout: 'grid' | 'list') {
    this.viewLayout.set(layout);
  }

  setCurrentPage(page: number) {
    this.currentPage.set(page);
    this.scrollToFeed();
  }

  prevPage() {
    this.currentPage.set(Math.max(1, this.currentPage() - 1));
    this.scrollToFeed();
  }

  nextPage() {
    this.currentPage.set(Math.min(this.totalPages(), this.currentPage() + 1));
    this.scrollToFeed();
  }

  /**
   * Acota el listado por la categoría de una tarjeta, o lo deshace si ya era esa.
   *
   * <b>DETIENE LA PROPAGACION, Y ESA ES LA PARTE QUE HAY QUE MIRAR.</b> La tarjeta entera es
   * pulsable —abre la noticia— y esta etiqueta vive dentro. Sin `stopPropagation`, pulsar la
   * categoría filtraría Y abriría la noticia a la vez, que es peor que dejarla como texto muerto.
   *
   * <b>ALTERNA</b>, igual que las etiquetas del ecosistema: pulsar la que ya está filtrando
   * significa «quítamelo».
   */
  filtrarPorCategoria(categoria: string, evento?: Event) {
    evento?.stopPropagation();
    const actual = this.newsCategoryFilter();
    this.newsCategoryFilter.set(actual === categoria ? 'all' : categoria);
    this.currentPage.set(1);
    this.scrollToFeed();
  }

  /** Si el listado ya está acotado por esta categoría. Decide el estilo y el `aria-pressed`. */
  categoriaActiva(categoria: string): boolean {
    return this.newsCategoryFilter() === categoria;
  }

  /**
   * Si filtrar por esta categoría puede dar algún resultado.
   *
   * <b>EL LISTADO EMPIEZA EN LA CUARTA NOTICIA.</b> Las tres primeras son el destacado y el par
   * secundario, y no entran en `newsListPool()` —de donde salen tanto el filtrado como las
   * pestañas de categoría de arriba—. Así que una categoría que SOLO existe en una destacada no
   * tiene pestaña y, si se filtrara por ella, daría cero resultados siempre.
   *
   * Se comprobó en navegador: pulsar «Convocatorias» en la tarjeta destacada dejaba la pantalla en
   * «ninguna noticia coincide con tus filtros». Una acción que solo puede llevar a una vista vacía
   * no se ofrece: es la misma regla que ya rige los menús de la consola.
   */
  categoriaFiltrable(categoria: string): boolean {
    return !!categoria && this.newsCategoryOptions().includes(categoria);
  }

  /** Lo que se le lee a quien no ve la pantalla, y que cambia cuando el filtro ya está puesto. */
  rotuloDeCategoria(categoria: string): string {
    return this.categoriaActiva(categoria)
      ? `Quitar el filtro por la categoría ${categoria}`
      : `Ver solo las noticias de ${categoria}`;
  }

  resetAllFilters() {
    // CADA FILTRO A SU PROPIO VACIO, el declarado arriba. Escribirlos aquí otra vez es donde
    // empiezan a divergir: este método ya se había quedado sin la vista.
    limpiarFiltros(this.filtros);
    this.scrollToFeed();
  }

  onEmailChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.subscriberEmail = input.value;
    this.subscriptionError = '';
  }

  /**
   * Da de alta el correo en el boletín.
   *
   * ANTES ESTE METODO NO LLAMABA A NADA: validaba que hubiera una arroba y ponía `isSubscribed` a
   * cierto. El formulario daba las gracias y el correo no llegaba a ninguna parte, que es la
   * definición exacta de falsamente conectado. Ahora escribe en `/publico/boletin/suscripciones`.
   *
   * EL ORIGEN ES «noticias» Y NO «portada»: la consola necesita distinguir qué pantalla convierte,
   * que es para lo que existe la lista de suscripciones.
   *
   * NO SE DISTINGUE SI EL CORREO YA ESTABA, y es decisión del API: hacerlo convertiría esta ruta
   * anónima en un comprobador abierto de quién está en la lista.
   */
  handleSubscribeSubmit(event: Event) {
    event.preventDefault();
    const correo = (this.subscriberEmail || '').trim();
    if (!correo || !correo.includes('@')) {
      this.subscriptionError = 'Por favor, ingresa una dirección de correo válida.';
      return;
    }

    this.subscriptionError = '';
    this.boletin.suscribir(correo, true, 'noticias').subscribe({
      next: () => {
        this.isSubscribed = true;
        this.subscriberEmail = '';
      },
      error: (error: { payload?: { message?: string } }) => {
        this.subscriptionError = error?.payload?.message
          || 'No fue posible registrar el correo. Inténtalo de nuevo.';
      }
    });
  }

  calculateReadingTime(content = ''): number {
    const wordsPerMinute = 200;
    const wordCount = content.trim().split(/\s+/).length;
    return Math.max(1, Math.ceil(wordCount / wordsPerMinute));
  }

  getCategoryCount(cat: string): number {
    return this.newsListPool().filter(item => item.category === cat).length;
  }

  private scrollToFeed() {
    setTimeout(() => {
      const element = document.getElementById('news-feed-section');
      if (element) {
        const yOffset = -100;
        const y = element.getBoundingClientRect().top + window.scrollY + yOffset;
        window.scrollTo({ top: y, behavior: 'smooth' });
      }
    }, 50);
  }
}
