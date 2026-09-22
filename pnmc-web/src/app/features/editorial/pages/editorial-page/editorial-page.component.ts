import { AfterViewChecked, Component, ElementRef, OnInit, ViewChild, computed, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  LucideBookOpen, 
  LucideBoxes, 
  LucideBuilding2, 
  LucideDisc, 
  LucideFileVideo, 
  LucideInfo, 
  LucideLandmark, 
  LucideLibrary, 
  LucideMusic2, 
  LucideSearch,
  LucideChevronDown,
  LucideChevronUp,
  LucideChevronLeft,
  LucideChevronRight,
  LucideArrowUpRight,
  LucideX,
  LucideCalendar,
  LucideAlertCircle,
  LucideLayoutGrid,
  LucideList,
  LucideUserCircle2,
  LucideLoader2
} from '@lucide/angular';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { CatalogService } from '../../../../core/services/catalog.service';
import { CatalogoEditorialService } from '../../../../core/services/catalogo-editorial.service';
import { SectionHeaderComponent } from '../../../../shared/components/ui/section-header/section-header.component';
import { FichaEditorialComponent } from '../../components/ficha-editorial/ficha-editorial.component';
import { 
  extractEditorialYears, 
  getEditorialSectionIconName 
} from '../../../../core/services/data-transforms';
import { RecursoEditorialDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import {
  contarFiltrosPuestos,
  enlazarFiltrosConLaUrl,
  filtroDePagina,
  filtroDeTexto,
  filtroDeVista,
  limpiarFiltros,
} from '../../../../shared/utils/filtros-en-la-url';

@Component({
  selector: 'app-editorial-page',
  standalone: true,
  imports: [
    CommonModule,
    SectionHeaderComponent,
    FichaEditorialComponent,
    LucideBookOpen, 
    LucideBoxes, 
    LucideBuilding2, 
    LucideDisc, 
    LucideFileVideo, 
    LucideInfo, 
    LucideLandmark, 
    LucideLibrary, 
    LucideMusic2, 
    LucideSearch,
    LucideChevronDown,
    LucideChevronUp,
    LucideChevronLeft,
    LucideChevronRight,
    LucideArrowUpRight,
    LucideX,
    LucideCalendar,
    LucideAlertCircle,
    LucideLayoutGrid,
    LucideList,
    LucideUserCircle2,
    LucideLoader2
  ],
  templateUrl: './editorial-page.component.html'
})
export class EditorialPageComponent implements AfterViewChecked, OnInit {
  private navigationService = inject(NavigationService);
  private readonly ruta = inject(ActivatedRoute);
  private readonly enrutador = inject(Router);
  private webTexts = inject(TextosWebService);
  private catalogService = inject(CatalogService);
  private readonly catalogoEditorial = inject(CatalogoEditorialService);

  selectedMosaicItem = signal<RecursoEditorialDeDiseno | null>(null);
  activeTab = signal<string>('all');
  searchTerm = signal<string>('');
  advancedTitleSearch = signal<string>('');
  advancedAuthorSearch = signal<string>('');
  advancedKeywordSearch = signal<string>('');
  selectedYearFilter = signal<string>('');
  showAdvancedSearch = signal<boolean>(false);
  editorialSortOrder = signal<string>('az');
  selectedKeyword = signal<string>('');
  hoveredId = signal<string | null>(null);
  viewMode = signal<'mosaic' | 'table'>('mosaic');
  expandedId = signal<string | null>(null);
  currentPage = signal<number>(1);
// TIPADO Y NO `any`, que es lo unico que se toca del codigo portado ademas de la conexion.
// El diseno viene de un desarrollo mas laxo; poner el tipo no cambia un pixel y es lo que
// permite que el trinquete de tipado siga bajando en vez de subir.
  resources = signal<RecursoEditorialDeDiseno[]>([]);

  /**
   * La signatura que venía en la dirección, si venía alguna.
   *
   * SE GUARDA APARTE DEL ACERVO porque llegan en momentos distintos: la dirección está desde el
   * primer instante y el catálogo tarda en llegar. Cuando estén los dos, el efecto de abajo abre la
   * ficha; mientras tanto, la pantalla se ve como se ve siempre.
   */
  readonly codigoEnLaUrl = signal<string | null>(null);

  @ViewChild('mosaicDetailDialog') mosaicDetailDialog?: ElementRef<HTMLDialogElement>;

  isLoading = true;
  loadError: string | null = null;

  Math = Math;

  // Computed properties
  categories = computed(() => {
    const list = this.resources();
    const uniqueSections = [...new Set(list.map((r) => r.section).filter(Boolean))];

    return [
      { id: 'all', label: 'Todo el Acervo', iconName: 'library' },
      ...uniqueSections.map((section) => ({
        id: section,
        label: section,
        iconName: getEditorialSectionIconName(section)
      }))
    ];
  });

  sectionScopedResources = computed(() => {
    const list = this.resources();
    const tab = this.activeTab();
    return tab === 'all' ? list : list.filter((r) => r.section === tab);
  });

  popularKeywords = computed(() => {
    const scope = this.sectionScopedResources();
    const counts = new Map<string, number>();

    scope.forEach((resource) => {
      resource.keywords?.forEach((keyword: string) => {
        counts.set(keyword, (counts.get(keyword) || 0) + 1);
      });
    });

    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0], 'es'))
      .slice(0, 8)
      .map(([keyword]) => keyword);
  });

  availableYears = computed(() => {
    const scope = this.sectionScopedResources();
    const years = scope.flatMap((resource) => extractEditorialYears(resource.year));
    return [...new Set(years)].sort((a, b) => b - a);
  });

  filteredResources = computed(() => {
    const scope = this.sectionScopedResources();
    const term = this.searchTerm().trim().toLowerCase();
    const advTitle = this.advancedTitleSearch().trim().toLowerCase();
    const advAuthor = this.advancedAuthorSearch().trim().toLowerCase();
    const advKeyword = this.advancedKeywordSearch().trim().toLowerCase();
    const keyword = this.selectedKeyword().trim().toLowerCase();
    const yearFilter = this.selectedYearFilter();
    const sortOrder = this.editorialSortOrder();

    const matching = scope.filter((resource) => {
      // 1. Keyword
      const matchesKeyword = !keyword
        || resource.keywords?.some((k: string) => k.toLowerCase() === keyword);
      if (!matchesKeyword) return false;

      // 2. Year
      const resourceYears = extractEditorialYears(resource.year).map(String);
      const matchesYear = !yearFilter || resourceYears.includes(yearFilter);
      if (!matchesYear) return false;

      // 3. Advanced title
      const matchesAdvTitle = !advTitle || (resource.title || '').toLowerCase().includes(advTitle);
      if (!matchesAdvTitle) return false;

      // 4. Advanced author
      const authorText = [
        resource.displayAuthor,
        resource.author,
        resource.corporateAuthor,
        resource.additionalCredits
      ].filter(Boolean).join(' ').toLowerCase();
      const matchesAdvAuthor = !advAuthor || authorText.includes(advAuthor);
      if (!matchesAdvAuthor) return false;

      // 5. Advanced keyword/meta
      const metaText = [
        resource.section,
        resource.sectionPath,
        resource.practice,
        resource.category,
        resource.subcategory,
        ...(resource.keywords || [])
      ].filter(Boolean).join(' ').toLowerCase();
      const matchesAdvKeyword = !advKeyword || metaText.includes(advKeyword);
      if (!matchesAdvKeyword) return false;

      // 6. Simple Search
      if (!term) return true;
      const searchable = [
        resource.id,
        resource.title,
        resource.year,
        resource.section,
        resource.sectionPath,
        resource.publicationType,
        resource.practice,
        resource.category,
        resource.subcategory,
        resource.author,
        resource.corporateAuthor,
        resource.additionalCredits,
        resource.regionalScope,
        // LOS ACCESOS SE BUSCAN DESDE SU LISTA, no desde un campo aplanado. Buscar «Biblioteca
        // Nacional» tiene que encontrar la obra que se consulta allí, y esa información vive en
        // `access`; el campo `location` que había aquí era una copia suya unida con «; ».
        ...(resource.access ?? []).map(a => `${a.ubicacionFisica ?? ''} ${a.etiqueta ?? ''} ${a.url ?? ''}`),
        resource.summary,
        ...(resource.keywords || [])
      ].filter(Boolean).join(' ').toLowerCase();

      return searchable.includes(term);
    });

    return [...matching].sort((left, right) => {
      const leftTitle = left.title || left.id || '';
      const rightTitle = right.title || right.id || '';
      const comparison = leftTitle.localeCompare(rightTitle, 'es', { sensitivity: 'base' });
      return sortOrder === 'za' ? comparison * -1 : comparison;
    });
  });

  ITEMS_PER_PAGE = 24;

  totalPages = computed(() => {
    const count = this.filteredResources().length;
    return Math.max(1, Math.ceil(count / this.ITEMS_PER_PAGE));
  });

  paginatedResources = computed(() => {
    const startIndex = (this.currentPage() - 1) * this.ITEMS_PER_PAGE;
    return this.filteredResources().slice(startIndex, startIndex + this.ITEMS_PER_PAGE);
  });

  currentCategoryLabel = computed(() => {
    const tab = this.activeTab();
    return this.categories().find((c) => c.id === tab)?.label || 'Todo el Acervo';
  });

  /**
   * Los filtros del Catálogo, declarados una vez y enganchados a la URL.
   *
   * <b>ERA LA PANTALLA CON LA BUSQUEDA MAS RICA Y LA MENOS COMPARTIBLE.</b> Tiene búsqueda simple,
   * búsqueda avanzada por título, autoría y palabra clave, filtro por año, pestaña de tipo,
   * palabra clave suelta y dos formas de leer el listado. Nada de eso viajaba en la URL: una
   * búsqueda avanzada afinada durante cinco minutos no se podía pegar en un correo, y recargar la
   * perdía entera.
   *
   * <b>`showAdvancedSearch` NO ES UN FILTRO Y POR ESO NO ESTA AQUI.</b> Es si el panel está
   * desplegado; se deduce de que alguno de sus tres campos venga en la URL.
   */
  readonly filtros = [
    filtroDeTexto('q', this.searchTerm),
    filtroDeTexto('titulo', this.advancedTitleSearch),
    filtroDeTexto('autoria', this.advancedAuthorSearch),
    filtroDeTexto('clave', this.advancedKeywordSearch),
    filtroDeTexto('anio', this.selectedYearFilter),
    filtroDeTexto('tipo', this.activeTab, 'all'),
    filtroDeTexto('palabra', this.selectedKeyword),
    filtroDeTexto('orden', this.editorialSortOrder, 'az'),
    filtroDeVista('vista', this.viewMode, 'mosaic'),
    filtroDePagina(this.currentPage),
  ];

  /** Cuántos filtros hay puestos, para rotular «Limpiar» con su número. */
  readonly filtrosPuestos = computed(() => contarFiltrosPuestos(this.filtros));

  constructor() {
    enlazarFiltrosConLaUrl(this.filtros);

    // ABRIR LA FICHA CUANDO ESTEN LAS DOS COSAS: la signatura de la dirección y el acervo cargado.
    effect(() => {
      const codigo = this.codigoEnLaUrl();
      const acervo = this.resources();
      untracked(() => {
        if (!codigo) { this.selectedMosaicItem.set(null); return; }
        const ficha = acervo.find(recurso => recurso.id === codigo);
        // UNA SIGNATURA QUE NO EXISTE NO ROMPE NADA: se queda el catálogo, que es lo que se pidió.
        if (ficha) { this.selectedMosaicItem.set(ficha); }
      });
    });
    // SI LA URL TRAIA UN CAMPO AVANZADO, EL PANEL SE ABRE. Llegar con la búsqueda avanzada puesta
    // y el panel plegado enseñaría resultados acotados sin decir por qué.
    if (this.advancedTitleSearch() || this.advancedAuthorSearch() || this.advancedKeywordSearch()) {
      this.showAdvancedSearch.set(true);
    }
  }

  ngOnInit() {
    this.loadCatalog();

    /*
     * CADA OBRA TIENE SU DIRECCION, y hasta hoy ninguna la tenía.
     *
     * La ficha se abría como un diálogo sin tocar la URL: no se podía enviar el enlace de una
     * publicación, ni volver con el botón de atrás, ni recargar sin perderla. En un catálogo eso no
     * es un detalle: la signatura ES la forma de citar una obra, y citarla tiene que llevar a algún
     * sitio. Ahora `/editorial/PNMC-ED-019` abre esa ficha, y cerrarla vuelve a `/editorial`.
     *
     * SE ESCUCHA EL PARAMETRO Y NO SOLO SE LEE UNA VEZ: así el botón de atrás del navegador cierra
     * la ficha en vez de sacar a la persona del catálogo.
     */
    // EL PARAMETRO VIVE EN LA RUTA HIJA, así que se lee de `firstChild` y no del propio `paramMap`:
    // la página es el padre y el padre no lleva la signatura. Se escucha cada navegación terminada
    // —y se lee una vez al entrar— para que el botón de atrás cierre la ficha en vez de sacar a la
    // persona del catálogo.
    const leerLaSignatura = () =>
      this.codigoEnLaUrl.set(this.ruta.firstChild?.snapshot.paramMap.get('codigo') ?? null);
    leerLaSignatura();
    this.enrutador.events
      .pipe(filter(evento => evento instanceof NavigationEnd))
      .subscribe(() => leerLaSignatura());

    // Check for navigation target (initial selection)
    const initialId = this.navigationService.selectedEditorialResourceId();
    if (initialId) {
      setTimeout(() => {
        this.viewMode.set('table');
        this.expandedId.set(initialId);
        const targetElement = document.getElementById(`resource-row-${initialId}`);
        if (targetElement) {
          targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      }, 300);
    }
  }

  loadCatalog() {
    this.isLoading = true;
    this.loadError = null;

    this.catalogoEditorial.listarParaElPortal().subscribe({
      next: (res) => {
        this.resources.set(Array.isArray(res?.items) ? res.items : []);
        this.isLoading = false;
      },
      error: (err) => {
        this.loadError = err?.message || 'No fue posible cargar el catálogo editorial.';
        this.isLoading = false;
      }
    });
  }

  getWebText(key: string, fallback = ''): string {
    return this.webTexts.getWebText(key) || fallback;
  }

  /**
   * Explorar el acervo desde un dato de la ficha.
   *
   * Cierra la ficha, porque el resultado de explorar es un LISTADO y dejarlo tapado por el diálogo
   * que lo pidió no enseña nada. Y vuelve a la primera página: el filtro nuevo tiene otro reparto.
   */
  explorarDesdeLaFicha(peticion: { campo: 'palabraClave' | 'seccion'; valor: string }) {
    this.closeMosaicDetail();
    this.expandedId.set(null);
    if (peticion.campo === 'palabraClave') {
      this.selectedKeyword.set(peticion.valor);
    } else {
      this.activeTab.set(peticion.valor);
      this.selectedKeyword.set('');
    }
    this.currentPage.set(1);
    this.scrollToFeed();
  }

  setActiveTab(tabId: string) {
    this.activeTab.set(tabId);
    this.selectedKeyword.set('');
    this.currentPage.set(1);
  }

  setSelectedKeyword(keyword: string) {
    this.selectedKeyword.set(this.selectedKeyword() === keyword ? '' : keyword);
    this.currentPage.set(1);
  }

  setSelectedYearFilter(event: Event) {
    const select = event.target as HTMLSelectElement;
    this.selectedYearFilter.set(select.value);
    this.currentPage.set(1);
  }

  setEditorialSortOrder(event: Event) {
    const select = event.target as HTMLSelectElement;
    this.editorialSortOrder.set(select.value);
    this.currentPage.set(1);
  }

  onSearchChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
  }

  toggleAdvancedSearch() {
    this.showAdvancedSearch.set(!this.showAdvancedSearch());
  }

  /**
   * Limpia la búsqueda: los cinco campos que la componen, y nada más.
   *
   * NO TOCA LA PESTAÑA NI EL ORDEN NI LA VISTA, y es deliberado: el botón está dentro del bloque de
   * búsqueda y dice «limpiar la búsqueda», no «empezar de cero». Para eso está `limpiarTodo`.
   */
  clearSearch() {
    this.searchTerm.set('');
    this.advancedTitleSearch.set('');
    this.advancedAuthorSearch.set('');
    this.advancedKeywordSearch.set('');
    this.selectedYearFilter.set('');
    this.currentPage.set(1);
  }

  /** Devuelve TODOS los filtros a su estado de partida, la página incluida. */
  limpiarTodo() {
    limpiarFiltros(this.filtros);
    this.showAdvancedSearch.set(false);
    this.expandedId.set(null);
  }

  setViewMode(mode: 'mosaic' | 'table') {
    this.viewMode.set(mode);
    this.expandedId.set(null);
  }

  toggleExpandedId(itemId: string) {
    const isExpanding = this.expandedId() !== itemId;
    this.expandedId.set(isExpanding ? itemId : null);

    if (isExpanding) {
      setTimeout(() => {
        const element = document.getElementById(`resource-row-${itemId}`);
        if (element) {
          const yOffset = -140;
          const y = element.getBoundingClientRect().top + window.scrollY + yOffset;
          window.scrollTo({ top: y, behavior: 'smooth' });
        }
      }, 100);
    }
  }

  setSelectedMosaicItem(item: RecursoEditorialDeDiseno | null) {
    if (!item) {
      this.closeMosaicDetail();
      return;
    }

    this.selectedMosaicItem.set(item);
    // LOS FILTROS SE CONSERVAN: quien vuelve de una ficha vuelve a su búsqueda, no al acervo entero.
    void this.enrutador.navigate(['/editorial', item.id], { queryParamsHandling: 'preserve' });
  }

  ngAfterViewChecked(): void {
    const dialog = this.mosaicDetailDialog?.nativeElement;
    if (this.selectedMosaicItem() && dialog && !dialog.open) {
      dialog.showModal();
    }
  }

  closeMosaicDetail(event?: Event) {
    event?.preventDefault();
    const dialog = this.mosaicDetailDialog?.nativeElement;
    if (dialog?.open) {
      dialog.close();
    }
    this.selectedMosaicItem.set(null);
    if (this.codigoEnLaUrl()) {
      void this.enrutador.navigate(['/editorial'], { queryParamsHandling: 'preserve' });
    }
  }

  onMosaicDialogClick(event: MouseEvent) {
    if (event.target === event.currentTarget) {
      this.closeMosaicDetail();
    }
  }

  setCurrentPage(page: number) {
    this.currentPage.set(page);
    this.expandedId.set(null);
    this.scrollToFeed();
  }

  prevPage() {
    this.currentPage.set(Math.max(1, this.currentPage() - 1));
    this.expandedId.set(null);
    this.scrollToFeed();
  }

  nextPage() {
    this.currentPage.set(Math.min(this.totalPages(), this.currentPage() + 1));
    this.expandedId.set(null);
    this.scrollToFeed();
  }

  openUrl(url: string) {
    if (url) {
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  }

  splitParagraphs(text = ''): string[] {
    return text.split('\n').filter(Boolean);
  }




  getCategoryCount(catId: string): number {
    if (catId === 'all') return this.resources().length;
    return this.resources().filter((r) => r.section === catId).length;
  }

  private scrollToFeed() {
    setTimeout(() => {
      const element = document.getElementById('editorial-catalogo');
      if (element) {
        element.scrollIntoView({ behavior: 'smooth' });
      }
    }, 50);
  }
}
