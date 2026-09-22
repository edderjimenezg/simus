import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  LucideArrowUpRight, 
  LucideCalendar, 
  LucideChevronLeft, 
  LucideChevronRight, 
  LucideClock, 
  LucideGraduationCap, 
  LucideList, 
  LucideMegaphone, 
  LucideMusic2, 
  LucidePartyPopper, 
  LucideSearch, 
  LucideSparkles, 
  LucideUsers2, 
  LucideX, 
  LucideMapPin, 
  LucidePlus 
} from '@lucide/angular';
import { NavigationService } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { CatalogService } from '../../../../core/services/catalog.service';
import { EventoDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import { TerritorioConCodigo } from '../../../../core/services/catalog.service';
import { SectionHeaderComponent } from '../../../../shared/components/ui/section-header/section-header.component';
import { 
  LoadingStateComponent, 
  ErrorStateComponent, 
  EmptyStateComponent 
} from '../../../../shared/components/ui/remote-state/remote-state.component';
import { 
  sortUniqueByLocale,
  normalizeDepartmentName,
  normalizeMunicipalityName,
  getDepartmentSelectionValue
} from '../../../map/domain/map-domain';
import { buildAgendaEventIcs } from '../../domain/agenda-ics';
import {
  contarFiltrosPuestos,
  enlazarFiltrosConLaUrl,
  filtroDePagina,
  filtroDeTexto,
  filtroDeVista,
  limpiarFiltros,
} from '../../../../shared/utils/filtros-en-la-url';

@Component({
  selector: 'app-agenda-page',
  standalone: true,
  imports: [
    CommonModule,
    SectionHeaderComponent,
    LoadingStateComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    LucideArrowUpRight,
    LucideCalendar,
    LucideChevronLeft,
    LucideChevronRight,
    LucideClock,
    LucideGraduationCap,
    LucideMegaphone,
    LucideMusic2,
    LucidePartyPopper,
    LucideSearch,
    LucideSparkles,
    LucideUsers2,
    LucideX,
    LucideList,
    LucideMapPin,
    LucidePlus
  ],
  templateUrl: './agenda-page.component.html'
})
export class AgendaPageComponent implements OnInit {
  private navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);
  private catalogService = inject(CatalogService);

  openIndex = signal<number>(-1);
  currentPage = signal<number>(1);
  /**
   * CÓMO SE ACOTA EL TIEMPO: un día, un mes o un tramo entre dos fechas.
   *
   * Eran dos posiciones —día y mes— y ninguna respondía «qué hay entre el 20 de septiembre y el 5
   * de octubre», que es como se planea un viaje o una programación. El tramo se pidió el 14 de
   * septiembre de 2026: «una mejor manera para filtrar una fecha exacta, un mes, como rango de
   * tiempo en la búsqueda».
   */
  /** Las tres posiciones del selector de fecha, en su orden y con su rótulo editable. */
  readonly MODOS_DE_FECHA = [
    { id: 'exact' as const, clave: 'agenda_filter_date_exact', rotulo: 'Día' },
    { id: 'month' as const, clave: 'agenda_filter_date_month', rotulo: 'Mes' },
    { id: 'range' as const, clave: 'agenda_filter_date_range', rotulo: 'Rango' },
  ];

  dateMode = signal<'exact' | 'month' | 'range'>('exact');
  fechaDesde = signal<string>('');
  fechaHasta = signal<string>('');

  /**
   * LA BÚSQUEDA ESCRITA, QUE NO EXISTÍA.
   *
   * La Agenda solo se podía acotar por listas: departamento, municipio, categoría y fecha. Quien
   * busca «marimba» o el nombre de un festival no tenía por dónde entrar, mientras que el Catálogo
   * Editorial sí tiene buscador. Mira título, descripción, lugar, categoría y organizador.
   */
  busqueda = signal<string>('');
  viewMode = signal<'list' | 'calendar'>('list');
  selectedDept = signal<string>('');
  selectedMunicipality = signal<string>('');
  selectedExactDate = signal<string>('');
  selectedMonthFilter = signal<string>('');
  selectedCategory = signal<string>('Todos');

  isLoading = false;
  isRefreshing = false;
  isError = false;
  error: { message?: string } | null = null;

// TIPADO Y NO `any`, que es lo unico que se toca del codigo portado ademas de la conexion.
// El diseno viene de un desarrollo mas laxo; poner el tipo no cambia un pixel y es lo que
// permite que el trinquete de tipado siga bajando en vez de subir.
  agendaData = signal<EventoDeDiseno[]>([]);

  Math = Math;

  padDay(d: string | number): string {
    return String(d ?? '').padStart(2, '0');
  }

  /**
   * El catálogo territorial, leído de la fuente confirmada del proyecto.
   *
   * <b>ANTES SALIA DE UNA TABLA EN MEMORIA Y ESTABA SIEMPRE VACIO.</b> El diseño portado leía
   * `MapDomain.getSortedDepartmentNames()`, que devuelve las claves de una tabla que rellena EL
   * GEOVISOR al cargarse. Quien entraba directo a `/agenda` no había pasado por el mapa, así que
   * la tabla era `{}` y el desplegable de departamento no ofrecía ninguna opción. El dato existía
   * y era correcto; lo que fallaba era depender de que otra pantalla lo hubiera traído antes.
   *
   * Ahora se pide a `/publico/divipola`, que es la fuente del proyecto, desde esta misma pantalla.
   */
  private readonly territorios = signal<TerritorioConCodigo[]>([]);

  departments = computed(() => this.territorios().map((territorio) => territorio.nombre));

  cities = computed(() => {
    const departamento = this.selectedDept();
    if (!departamento) return [];
    const encontrado = this.territorios().find((territorio) => territorio.nombre === departamento);
    return sortUniqueByLocale((encontrado?.municipios ?? []).map((municipio) => municipio.nombre));
  });

  uniqueMonths = computed(() => {
    const months = this.agendaData().map(item => {
      if (!item.dateObj) return null;
      const year = item.dateObj.getFullYear();
      const monthIndex = item.dateObj.getMonth();
      const label = item.dateObj.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' });
      const value = `${year}-${String(monthIndex + 1).padStart(2, '0')}`;
      return { value, label };
    }).filter(Boolean) as { value: string, label: string }[];

    return Array.from(new Map(months.map(m => [m.value, m])).values())
      .sort((a, b) => a.value.localeCompare(b.value));
  });

  activityCategories = computed(() => {
    const cats = [...new Set(this.agendaData().map(item => item.cat).filter(Boolean))];
    return ['Todos', ...cats];
  });

  filteredAgendaItems = computed(() => {
    const data = this.agendaData();
    const dept = this.selectedDept();
    const municipality = this.selectedMunicipality();
    const exactDate = this.selectedExactDate();
    const monthFilter = this.selectedMonthFilter();
    const category = this.selectedCategory();
    const dMode = this.dateMode();
    const desde = this.fechaDesde();
    const hasta = this.fechaHasta();
    const texto = this.normalizarTexto(this.busqueda());

    const selectedDepartmentNormalized = normalizeDepartmentName(getDepartmentSelectionValue(dept));
    const selectedMunicipalityNormalized = normalizeMunicipalityName(municipality);

    return data.filter((item) => {
      // 1. Filter by location
      const locationTokens = String(item?.l || '')
        .split(',')
        .map((token) => token.trim())
        .filter(Boolean);
      const itemMunicipality = item?.municipality || locationTokens[0] || '';
      const itemDepartment = item?.department || locationTokens[1] || '';

      const matchesDepartment = !selectedDepartmentNormalized
        || normalizeDepartmentName(itemDepartment) === selectedDepartmentNormalized;
      const matchesMunicipality = !selectedMunicipalityNormalized
        || normalizeMunicipalityName(itemMunicipality) === selectedMunicipalityNormalized;

      if (!matchesDepartment || !matchesMunicipality) return false;

      // 2. Filter by activity type
      const matchesCategory = category === 'Todos' || item.cat === category;
      if (!matchesCategory) return false;

      // 2 bis. Búsqueda escrita: sin tildes y en minúsculas, sobre lo que alguien recordaría de un
      // evento —su nombre, dónde es, de qué tipo y quién lo organiza—.
      if (texto) {
        const donde = [item.t, item.desc, item.l, item.exactLocation, item.cat, item.organizer]
          .map(valor => this.normalizarTexto(String(valor ?? '')))
          .join(' ');
        if (!donde.includes(texto)) return false;
      }

      // 3. Filter by date / month selection
      if (dMode === 'range') {
        // UN EVENTO ENTRA SI SE SOLAPA CON EL TRAMO, no solo si empieza dentro: un festival de una
        // semana que arranca el día antes del «desde» sigue ocurriendo durante el tramo.
        if (!desde && !hasta) return true;
        if (!item.dateObj) return false;
        const inicio = item.dateObj;
        const fin = item.fechaFin ? new Date(item.fechaFin.slice(0, 10) + 'T23:59:59') : inicio;
        if (desde && fin < new Date(desde + 'T00:00:00')) return false;
        if (hasta && inicio > new Date(hasta + 'T23:59:59')) return false;
        return true;
      }
      if (dMode === 'exact') {
        if (!exactDate) return true;
        const filterDateObj = new Date(exactDate + 'T00:00:00');
        return item.dateObj && 
               item.dateObj.getFullYear() === filterDateObj.getFullYear() &&
               item.dateObj.getMonth() === filterDateObj.getMonth() &&
               item.dateObj.getDate() === filterDateObj.getDate();
      } else {
        if (dMode === 'month' && monthFilter) {
          const itemYear = item.dateObj.getFullYear();
          const itemMonthIndex = item.dateObj.getMonth();
          const itemMonthStr = `${itemYear}-${String(itemMonthIndex + 1).padStart(2, '0')}`;
          if (itemMonthStr !== monthFilter) return false;
        }
        return true;
      }
    });
  });

  ITEMS_PER_PAGE = 12;

  totalPages = computed(() => {
    const count = this.filteredAgendaItems().length;
    return Math.max(1, Math.ceil(count / this.ITEMS_PER_PAGE));
  });

  paginatedAgendaItems = computed(() => {
    const startIndex = (this.currentPage() - 1) * this.ITEMS_PER_PAGE;
    return this.filteredAgendaItems().slice(startIndex, startIndex + this.ITEMS_PER_PAGE);
  });

  /**
   * Los filtros de la Agenda, declarados una vez y enganchados a la URL.
   *
   * <b>ERA LA PANTALLA CON MAS FILTROS Y LA UNICA SIN FORMA DE LIMPIARLOS.</b> Cinco —territorio,
   * municipio, fecha exacta, mes y categoría— y ninguno se podía compartir, ni sobrevivía a una
   * recarga, ni tenía un «Limpiar filtros»: había que devolver los cinco desplegables a mano.
   *
   * <b>`dateMode` NO ES UN FILTRO Y POR ESO NO ESTA AQUI.</b> Decide cuál de los dos filtros de
   * fecha está activo, y viaja implícito en cuál de los dos parámetros trae la URL.
   */
  readonly filtros = [
    filtroDeTexto('departamento', this.selectedDept),
    filtroDeTexto('municipio', this.selectedMunicipality),
    filtroDeTexto('fecha', this.selectedExactDate),
    filtroDeTexto('mes', this.selectedMonthFilter),
    filtroDeTexto('desde', this.fechaDesde),
    filtroDeTexto('hasta', this.fechaHasta),
    filtroDeTexto('q', this.busqueda),
    filtroDeTexto('categoria', this.selectedCategory, 'Todos'),
    filtroDeVista('vista', this.viewMode, 'list'),
    filtroDePagina(this.currentPage),
  ];

  /** Cuántos filtros hay puestos, para poder ofrecer «Limpiar filtros» solo cuando haya alguno. */
  readonly filtrosPuestos = computed(() => contarFiltrosPuestos(this.filtros));

  constructor() {
    enlazarFiltrosConLaUrl(this.filtros);
    // SI LA URL TRAIA UN MES, EL MODO DE FECHA ES «MES». El modo no se guarda: se deduce de cuál
    // de los dos filtros vino, que es lo que evita que la pantalla abra en «fecha exacta»
    // enseñando resultados que vienen de un filtro por mes.
    if (this.selectedMonthFilter()) this.dateMode.set('month');
  }

  /** Devuelve los filtros a su estado de partida y vuelve al principio de la lista. */
  limpiarTodosLosFiltros() {
    limpiarFiltros(this.filtros);
    this.dateMode.set('exact');
    this.openIndex.set(-1);
    this.scrollToFeed();
  }

  ngOnInit() {
    // SIN CATALOGO LA AGENDA SE SIGUE LEYENDO: los filtros territoriales no aparecen y ya está.
    // Es lo que la gente viene a hacer aquí, y no puede depender de una segunda petición.
    this.catalogService.fetchDivipolaConCodigos().subscribe({
      next: (territorios) => this.territorios.set(territorios),
      error: () => this.territorios.set([]),
    });

    this.loadAgendaData();
  }

  loadAgendaData(isRefresh = false) {
    if (isRefresh) {
      this.isRefreshing = true;
    } else {
      this.isLoading = true;
    }
    this.isError = false;
    this.error = null;

    // CONEXION REAL. `fetchAgendaEvents` lee `/publico/agenda` y traduce en
    // `adaptadores-del-diseno`; el orden por fecha lo mantiene esta pantalla, como el diseno.
    this.catalogService.fetchAgendaEvents().subscribe({
      next: (res) => {
        const sorted = [...(res.items || [])]
          .sort((a, b) => a.dateObj.getTime() - b.dateObj.getTime());
        this.agendaData.set(sorted);
        this.isLoading = false;
        this.isRefreshing = false;

        // Check for navigation target
        const initialId = this.navigationService.selectedAgendaEventId();
        if (initialId) {
          const targetIndex = this.filteredAgendaItems().findIndex(item => item.id === initialId);
          if (targetIndex !== -1) {
            setTimeout(() => {
              this.viewMode.set('list');
              this.openIndex.set(targetIndex);
              const targetElement = document.getElementById(`agenda-item-${initialId}`);
              if (targetElement) {
                targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
              }
            }, 150);
          }
        }
      },
      error: (err) => {
        this.error = err;
        this.isError = true;
        this.isLoading = false;
        this.isRefreshing = false;
      }
    });
  }

  retry() {
    this.loadAgendaData(false);
  }

  getWebText(key: string, fallback = ''): string {
    return this.webTexts.getWebText(key) || fallback;
  }

  setDateMode(mode: 'exact' | 'month' | 'range') {
    this.dateMode.set(mode);
    this.resetPaginationAndSelection();
  }

  cambiarBusqueda(evento: Event): void {
    this.busqueda.set((evento.target as HTMLInputElement).value);
    this.resetPaginationAndSelection();
  }

  cambiarFechaDesde(evento: Event): void {
    this.fechaDesde.set((evento.target as HTMLInputElement).value);
    this.resetPaginationAndSelection();
  }

  cambiarFechaHasta(evento: Event): void {
    this.fechaHasta.set((evento.target as HTMLInputElement).value);
    this.resetPaginationAndSelection();
  }

  /** Sin tildes y en minúsculas: quien busca escribe rápido, y exigir acentos es un examen. */
  private normalizarTexto(valor: string): string {
    return (valor ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim().toLowerCase();
  }

  /**
   * El icono de cada tipo de actividad.
   *
   * SE ELIGE POR EL NOMBRE porque la categoría llega como texto del registro —«Festivales»,
   * «Formacion», «Encuentros»— y no como un catálogo con icono, que es lo que sí tiene el Catálogo
   * Editorial. Se normaliza antes de comparar para que «Formación» y «Formacion» caigan en el
   * mismo sitio. Lo que no esté en la tabla se dibuja con el icono genérico de actividad.
   */
  iconoDeCategoria(categoria: string): string {
    const nombre = this.normalizarTexto(categoria);
    if (nombre === 'todos') return 'todos';
    if (nombre.includes('festival')) return 'festival';
    if (nombre.includes('formacion') || nombre.includes('taller') || nombre.includes('curso')) return 'formacion';
    if (nombre.includes('encuentro') || nombre.includes('foro')) return 'encuentro';
    if (nombre.includes('concierto') || nombre.includes('concurso')) return 'concierto';
    if (nombre.includes('convocatoria')) return 'convocatoria';
    return 'otra';
  }

  /** Cuántos eventos hay en cada tipo, con el resto de filtros ya aplicados menos el propio tipo. */
  cuantosEnCategoria(categoria: string): number {
    if (categoria === 'Todos') { return this.agendaData().length; }
    return this.agendaData().filter(item => item.cat === categoria).length;
  }

  setSelectedDept(event: Event) {
    const input = event.target as HTMLSelectElement;
    this.selectedDept.set(input.value);
    this.selectedMunicipality.set('');
    this.resetPaginationAndSelection();
  }

  setSelectedMunicipality(event: Event) {
    const input = event.target as HTMLSelectElement;
    this.selectedMunicipality.set(input.value);
    this.resetPaginationAndSelection();
  }

  setSelectedExactDate(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedExactDate.set(input.value);
    this.resetPaginationAndSelection();
  }

  setSelectedMonthFilter(event: Event) {
    const input = event.target as HTMLSelectElement;
    this.selectedMonthFilter.set(input.value);
    this.resetPaginationAndSelection();
  }

  setSelectedCategory(cat: string) {
    this.selectedCategory.set(cat);
    this.resetPaginationAndSelection();
  }

  toggleOpenIndex(index: number) {
    this.openIndex.set(this.openIndex() === index ? -1 : index);
  }

  setViewMode(mode: 'list' | 'calendar') {
    this.viewMode.set(mode);
    this.openIndex.set(-1);
  }

  setCurrentPage(page: number) {
    this.currentPage.set(page);
    this.openIndex.set(-1);
    this.scrollToFeed();
  }

  prevPage() {
    this.currentPage.set(Math.max(1, this.currentPage() - 1));
    this.openIndex.set(-1);
    this.scrollToFeed();
  }

  nextPage() {
    this.currentPage.set(Math.min(this.totalPages(), this.currentPage() + 1));
    this.openIndex.set(-1);
    this.scrollToFeed();
  }

  clearFilters() {
    this.dateMode.set('exact');
    this.selectedDept.set('');
    this.selectedMunicipality.set('');
    this.selectedExactDate.set('');
    this.selectedMonthFilter.set('');
    this.fechaDesde.set('');
    this.fechaHasta.set('');
    this.busqueda.set('');
    this.selectedCategory.set('Todos');
    this.resetPaginationAndSelection();
  }

  handleAddToCalendar(event: Event, item: EventoDeDiseno) {
    event.stopPropagation();
    const icsContent = buildAgendaEventIcs(item);
    const blob = new Blob([icsContent], { type: 'text/calendar;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    const safeTitle = (item.t || 'evento-pnmc').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

    link.href = url;
    link.download = `${safeTitle || 'evento-pnmc'}.ics`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  handleInfoLink(event: Event, item: EventoDeDiseno) {
    event.stopPropagation();
    if (item.link && item.link !== '#') {
      window.open(item.link, '_blank', 'noopener,noreferrer');
    }
  }

  private resetPaginationAndSelection() {
    this.currentPage.set(1);
    this.openIndex.set(-1);
  }

  private scrollToFeed() {
    setTimeout(() => {
      const element = document.getElementById('agenda-lista-eventos');
      if (element) {
        const yOffset = -112;
        const y = element.getBoundingClientRect().top + window.scrollY + yOffset;
        window.scrollTo({ top: y, behavior: 'smooth' });
      }
    }, 50);
  }
}
