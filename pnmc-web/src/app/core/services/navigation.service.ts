import { Injectable, signal, computed, inject } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { filter } from 'rxjs/operators';
import { TextosWebService } from './textos-web.service';
import { ejesDataGlobal } from './ejes-data.config';

export const PAGE_IDS = {
  home: 'home',
  pnmc: 'pnmc',
  ejes: 'ejes',
  editorial: 'editorial',
  galeria: 'galeria',
  noticias: 'noticias',
  agenda: 'agenda',
  mapa: 'mapa',
  ecosistema: 'ecosistema',
  /**
   * El panel de la organizacion externa. Tiene identificador propio y no se resuelve como
   * `ecosistema` por un motivo que se ve en pantalla: `app.component.ts` decide por este valor si
   * la barra de navegacion se pinta solida. `/ecosistema` conserva su encabezado fotografico y
   * necesita la barra transparente; el panel es una pantalla de trabajo sobre fondo claro, y con
   * la barra transparente su logotipo blanco se solapaba con el titulo. Comprobado el 27 de agosto
   * de 2026 sobre /gestion.
   */
  miPanel: 'gestion',
  // AQUI ESTUVO `simus`. Existia porque el menu listaba SIMUS como un destino
  // mas, sin entrada en PAGE_PATHS para que `navigate('simus')` no llevara a
  // ninguna parte dentro del sitio. El menu ya no lo lista —ese sitio lo ocupa
  // «Ver Mapa»— y el identificador se quedo sin un solo uso.
  //
  // Las rutas `/simus/*` de `app.routes.ts-112` siguen redirigiendo, y
  // `getPageIdFromPath` sigue reconociendolas: son cadenas literales, no este
  // identificador. El enlace saliente vive en `ENLACES_EXTERNOS.simus`.
  mapaParticipa: 'mapa-participa',
  admin: 'admin',
  registro: 'registro',
  estrategiaCirculacion: 'estrategia-circulacion',
  estrategiaInvestigacion: 'estrategia-investigacion',
  noEncontrado: 'no-encontrado',
};

export const COMPONENT_PAGE_PREFIX = 'comp-';

export const PAGE_PATHS: Record<string, string> = {
  [PAGE_IDS.home]: '/',
  [PAGE_IDS.pnmc]: '/pnmc',
  [PAGE_IDS.ejes]: '/ejes',
  [PAGE_IDS.editorial]: '/editorial',
  [PAGE_IDS.galeria]: '/galeria',
  [PAGE_IDS.noticias]: '/noticias',
  [PAGE_IDS.agenda]: '/agenda',
  [PAGE_IDS.mapa]: '/mapa-ecosistemico',
  [PAGE_IDS.ecosistema]: '/ecosistema',
  [PAGE_IDS.miPanel]: '/gestion',
  [PAGE_IDS.mapaParticipa]: '/registro',
  [PAGE_IDS.admin]: '/administracion',
  [PAGE_IDS.registro]: '/registro',
  [PAGE_IDS.estrategiaCirculacion]: '/estrategia/circulacion',
  [PAGE_IDS.estrategiaInvestigacion]: '/estrategia/investigacion',
};

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private router = inject(Router);
  private webTexts = inject(TextosWebService);
  private titleService = inject(Title);

  private readonly baseTitle = 'PNMC — Plan Nacional de Música para la Convivencia';
  private readonly pageTitles: Record<string, string> = {
    [PAGE_IDS.home]: this.baseTitle,
    [PAGE_IDS.pnmc]: 'Sobre el PNMC',
    [PAGE_IDS.ejes]: 'Ejes del Plan',
    [PAGE_IDS.editorial]: 'Editorial',
    [PAGE_IDS.galeria]: 'Galería',
    [PAGE_IDS.noticias]: 'Noticias',
    [PAGE_IDS.agenda]: 'Agenda',
    [PAGE_IDS.mapa]: 'Mapa ecosistémico',
    [PAGE_IDS.ecosistema]: 'Ecosistema musical',
    [PAGE_IDS.miPanel]: 'Gestión de organizaciones',
    [PAGE_IDS.admin]: 'Espacio de Gestión Administrativa',
    [PAGE_IDS.registro]: 'Haz parte del ecosistema',
    [PAGE_IDS.estrategiaCirculacion]: 'Estrategia de circulación',
    [PAGE_IDS.estrategiaInvestigacion]: 'Estrategia de investigación',
    [PAGE_IDS.noEncontrado]: 'Página no encontrada',
  };

  /**
   * Nombre legible de una página, para el anuncio de cambio de ruta.
   *
   * Comparte tabla con el título del documento a propósito: si el anuncio del
   * lector de pantalla y la pestaña del navegador dijeran cosas distintas,
   * habría dos verdades sobre el nombre de la misma página.
   */
  nombreDePagina(pageId: string): string {
    if (pageId === PAGE_IDS.home) return this.baseTitle;
    const titulo = this.pageTitles[pageId];
    if (titulo) return titulo;
    if (pageId.startsWith(COMPONENT_PAGE_PREFIX)) return 'Ejes del Plan';
    return this.baseTitle;
  }

  private updateDocumentTitle(pageId: string): void {
    let title = this.pageTitles[pageId];
    if (!title && pageId.startsWith(COMPONENT_PAGE_PREFIX)) {
      title = 'Ejes del Plan';
    }
    this.titleService.setTitle(
      title && pageId !== PAGE_IDS.home ? `${title} · ${this.baseTitle}` : this.baseTitle,
    );
  }

  private _activePage = signal<string>(PAGE_IDS.home);
  private _mobileMenuOpen = signal<boolean>(false);
  private _activeNavDropdown = signal<string | null>(null);
  private _activeEjeMenuId = signal<string | null>(null);
  private _selectedAgendaEventId = signal<string | null>(null);
  private _selectedEditorialResourceId = signal<string | null>(null);
  private _mapaNavigationRequest = signal<any>(null);

  activePage = computed(() => this._activePage());
  mobileMenuOpen = computed(() => this._mobileMenuOpen());
  activeNavDropdown = computed(() => this._activeNavDropdown());
  activeEjeMenuId = computed(() => this._activeEjeMenuId());
  selectedAgendaEventId = computed(() => this._selectedAgendaEventId());
  selectedEditorialResourceId = computed(() => this._selectedEditorialResourceId());
  mapaNavigationRequest = computed(() => this._mapaNavigationRequest());

  navigationLinks = [
    { name: 'Sobre el PNMC', id: PAGE_IDS.pnmc },
    { name: 'Ejes', id: PAGE_IDS.ejes },
    { name: 'Editorial', id: PAGE_IDS.editorial },
    { name: 'Galería', id: PAGE_IDS.galeria },
    { name: 'Noticias', id: PAGE_IDS.noticias },
    { name: 'Agenda', id: PAGE_IDS.agenda },
    // VA ANTES DE «Ecosistema», Y EL ORDEN DE ESTE ARRAY ES EL ORDEN EN PANTALLA:
    // `featuredNavigationLinks` filtra sin reordenar, asi que mover la entrada
    // aqui es lo unico que mueve el boton en la barra.
    //
    // OCUPA EL SITIO QUE TENIA SIMUS, que sale de la barra. El enlace saliente a
    // simus.mincultura.gov.co sigue publicado en la portada de /ecosistema
    // (`ecosistema-home-page.component.html:18`), asi que el destino no queda
    // huerfano.
    //
    // Su rotulo NO se edita desde el panel, y por eso lleva la marca: en el
    // registro del CMS no hay clave `nav_mapa` —se renombro a `nav_ecosistema`
    // cuando el enlace de al lado cambio de identificador—, y sin la marca
    // `getResolvedNavigationLinks` pediria esa clave, `getWebText` devolveria ''
    // (`textos-web.service.ts`) y el boton se quedaria sin rotulo.
    { name: 'Ver Mapa', id: PAGE_IDS.mapa, rotuloFijo: true },
    // Su identificador es `ecosistema` y no `mapa`, aunque su desplegable
    // ofrezca las dos cosas. Lo fue durante meses, y traia dos consecuencias
    // que no se notaban por separado: la clave del CMS que lo renombra se
    // llamaba `nav_mapa` —un editor que quisiera cambiar como se llama
    // «Ecosistema» estaba renombrando otra cosa sin saberlo— y estar en
    // /ecosistema no encendia su marca de pagina activa.
    //
    // Su desplegable YA NO OFRECE EL MAPA: la tarjeta «Herramienta principal»
    // salio de ahi cuando el mapa gano boton propio a la izquierda. Dentro
    // quedan los seis procesos y «Ver el ecosistema completo».
    { name: 'Ecosistema', id: PAGE_IDS.ecosistema },
  ];

  constructor() {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      const url = event.urlAfterRedirects || event.url || '/';
      const pageId = this.getPageIdFromPath(url);
      this._activePage.set(pageId);
      this.updateDocumentTitle(pageId);
      // EL DESPLAZAMIENTO NO SE DECIDE AQUI, y antes sí: este servicio subía la página al
      // encabezado en CADA navegación, con `scrollTo` en un `requestAnimationFrame`. Era un
      // segundo mecanismo en paralelo al del enrutador, y por eso cambiar la configuración del
      // enrutador no bastaba: abrir una ficha del Catálogo seguía devolviendo el mosaico al
      // principio. La regla —se sube al cambiar de PANTALLA, no al cambiar su estado— vive en
      // `configurarDesplazamientoAlNavegar`, en un solo sitio y con su porqué.
    });
  }

  private normalizePathname(pathname = '/'): string {
    if (!pathname || pathname === '/') return '/';
    return pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  }

  getPageIdFromPath(url = '/'): string {
    const path = url.split('?')[0]; // Ignorar search params
    const normalizedPath = this.normalizePathname(path);

    const pathSegments = normalizedPath.split('/').filter(Boolean);
    if (normalizedPath === '/registro' || normalizedPath === '/ingresar') {
      return PAGE_IDS.registro;
    }
    // `/simus/*` sigue reconociendose porque el router redirige, y entre la
    // navegacion y la redireccion hay un instante en que la URL todavia es la
    // vieja. Sin esta linea ese instante se resolveria como «pagina no
    // encontrada» y el shell parpadearia con el decorado del 404.
    // ANTES DEL CAJON DE `/ecosistema/`, que se lo tragaria. El panel necesita identificador
    // propio para que la barra de navegacion se pinte solida sobre su fondo claro.
    // `startsWith` Y NO SOLO IGUALDAD: el panel paso de una sola ruta a
    // un armazón con hijas reales (`/gestion/resumen`, `/gestion/organizacion`, etc.). Con la
    // igualdad exacta, cualquier ruta hija caia en el cajon general de `/ecosistema/` de la linea de
    // abajo y el panel perdia su identificador propio -la barra volvia a transparentarse sobre su
    // fondo claro, el mismo fallo que motivo darle identificador propio-.
    if (normalizedPath === '/gestion' || normalizedPath.startsWith('/gestion/')) {
      return PAGE_IDS.miPanel;
    }
    if (normalizedPath === '/ecosistema' || normalizedPath.startsWith('/ecosistema/')) {
      return PAGE_IDS.ecosistema;
    }
    if (normalizedPath === '/noticias') {
      return PAGE_IDS.noticias;
    }
    const ejesIndex = pathSegments.findIndex((segment) => segment.toLowerCase() === 'ejes');
    if (ejesIndex !== -1 && pathSegments[ejesIndex + 1]) {
      const component = ejesDataGlobal
        .flatMap((eje) => eje.components)
        .find((item) => item.slug === decodeURIComponent(pathSegments[ejesIndex + 1]));
      if (component) return `${COMPONENT_PAGE_PREFIX}${component.id}`;
    }

    const staticEntries = Object.entries(PAGE_PATHS)
      .filter(([, pagePath]) => pagePath !== '/')
      .sort(([, leftPath], [, rightPath]) => rightPath.length - leftPath.length);

    // La ruta ES la pagina, o es una ficha DENTRO de ella —por ejemplo,
    // `/ecosistema/festivales/abc`—. Antes decia `endsWith`, que no es lo mismo:
    // `/x/admin` «termina en» `/admin`, asi que una direccion inventada se
    // clasificaba como la consola. Y como el decorado del armazon se decide por
    // este identificador, el 404 salia **sin menu y sin pie**: un callejon sin
    // salida del que el visitante no podia moverse. Con `/pnmc/mapa` pasaba a
    // medias —404 con menu pero sin pie—. Comprobado renderizando las tres.
    //
    // Los candidatos vienen ordenados de mas largo a mas corto, asi que la
    // coincidencia mas especifica gana: `/ecosistema/escuelas` antes que
    // `/ecosistema`.
    const staticMatch = staticEntries.find(([, pagePath]) => (
      normalizedPath === pagePath || normalizedPath.startsWith(`${pagePath}/`)
    ));

    // Antes: `return staticMatch?.[0] || PAGE_IDS.home;`
    //
    // Toda ruta desconocida se resolvia como la PORTADA. Y como el decorado del
    // shell se decide por este identificador (app.component.ts), la pagina 404
    // salia con la barra transparente y SIN pie de pagina —el decorado del
    // inicio— y su pestana decia «PNMC — Plan Nacional de Musica para la
    // Convivencia» estando en un error.
    //
    // Solo la raiz es la portada. Cualquier otra cosa sin correspondencia es,
    // por definicion, una ruta que no existe.
    if (staticMatch) return staticMatch[0];
    return normalizedPath === '/' ? PAGE_IDS.home : PAGE_IDS.noEncontrado;
  }

  /**
   * Los enlaces del menu con su rotulo ya resuelto.
   *
   * Los que se editan desde el panel toman su texto del CMS. El de rotulo fijo
   * conserva el suyo: no tiene clave en el catalogo, asi que pedirsela al CMS
   * devolveria cadena vacia y el menu perderia el enlace sin decir por que.
   * Ocurrio el 22 ago 2026 y lo delato `cms:snapshot:check`.
   */
  getResolvedNavigationLinks() {
    return this.navigationLinks.map(link => ({
      ...link,
      name: 'rotuloFijo' in link ? link.name : this.webTexts.getWebText(`nav_${link.id}`)
    }));
  }

  setMobileMenuOpen(open: boolean) {
    this._mobileMenuOpen.set(open);
  }

  setActiveNavDropdown(dropdown: string | null) {
    this._activeNavDropdown.set(dropdown);
  }

  setActiveEjeMenuId(ejeId: string | null) {
    this._activeEjeMenuId.set(ejeId);
  }

  clearSelections() {
    this._selectedAgendaEventId.set(null);
    this._selectedEditorialResourceId.set(null);
    this._mapaNavigationRequest.set(null);
  }

  navigate(pageId: string) {
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    this.clearSelections();
    const path = PAGE_PATHS[pageId] || '/';
    this.router.navigateByUrl(path);
  }

  routerNavigate(path: string) {
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    this.clearSelections();
    this.router.navigateByUrl(`/${path.replace(/^\//, '')}`);
  }

  /**
   * Abrir una noticia concreta desde cualquier parte del sitio.
   *
   * <b>ABRE LA FICHA, NO EL LISTADO.</b> El parámetro se llamaba `_article` y se descartaba: daba
   * igual qué noticia se pulsara, siempre se navegaba a `/noticias`. Mientras el único origen fue
   * un bloque de Inicio sin contenido real, no se notaba; al conectar la portada informativa el 14
   * de septiembre de 2026, pulsar la noticia destacada llevaba al listado y había que volver a
   * buscarla.
   *
   * La dirección es el `slug`, igual que en el listado de Noticias —ver `handleSelectArticle`—,
   * porque la ruta se declara `/noticias/:slug` y la ficha resuelve con `obtenerPublica(slug)`.
   * Sin `slug` se cae al listado, que es lo mejor que se puede hacer sin saber cuál abrir.
   */
  navigateToArticle(article: unknown) {
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    const slug = (article as { slug?: unknown } | null)?.slug;
    if (typeof slug === 'string' && slug.trim()) {
      void this.router.navigate([PAGE_PATHS[PAGE_IDS.noticias], slug]);
      return;
    }
    void this.router.navigateByUrl(PAGE_PATHS[PAGE_IDS.noticias]);
  }

  navigateToAgendaEvent(eventId: string) {
    this._selectedAgendaEventId.set(eventId);
    this._selectedEditorialResourceId.set(null);
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    this.router.navigateByUrl(PAGE_PATHS[PAGE_IDS.agenda]);
  }

  navigateToEditorialResource(resourceId: string) {
    this._selectedEditorialResourceId.set(resourceId);
    this._selectedAgendaEventId.set(null);
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    this.router.navigateByUrl(PAGE_PATHS[PAGE_IDS.editorial]);
  }

  navigateToMapLayer(targetLayer = 'General', options: any = {}) {
    const {
      targetView = 'map',
      scrollToWorkspace = true,
    } = options;

    this._selectedAgendaEventId.set(null);
    this._selectedEditorialResourceId.set(null);
    this._mapaNavigationRequest.set({
      requestId: Date.now(),
      targetLayer,
      targetView,
      scrollToWorkspace,
    });
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    this.router.navigateByUrl(PAGE_PATHS[PAGE_IDS.mapa]);
  }


  navigateComponent(componentId: string) {
    this._mobileMenuOpen.set(false);
    this._activeNavDropdown.set(null);
    const component = ejesDataGlobal
      .flatMap((eje) => eje.components)
      .find((item) => item.id === componentId);
    if (!component) return;
    this.router.navigate(['/ejes', component.slug]);
  }
}
