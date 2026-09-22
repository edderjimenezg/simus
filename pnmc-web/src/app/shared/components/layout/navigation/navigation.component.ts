import { Component, ElementRef, Input, inject, computed, signal, HostListener } from '@angular/core';
import { CATEGORIAS_ECOSISTEMA } from '../../../../core/services/categorias-ecosistema.config';
import { CommonModule } from '@angular/common';
import { NavigationService, PAGE_IDS } from '../../../../core/services/navigation.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { ExternalSessionService } from '../../../../core/services/external-session.service';
import { ejesDataGlobal } from '../../../../core/services/ejes-data.config';
import { 
  LucideChevronDown, 
  LucideMenu, 
  LucideX, 
  LucideArrowUpRight,
  LucideLogIn,
  LucideUserPlus
} from '@lucide/angular';

/** Una entrada del desplegable del Ecosistema: el proceso, su línea y a dónde lleva si lleva. */
interface EntradaDelMenuDelEcosistema {
  label: string;
  page: string | null;
  detail: string;
}

/**
 * El rótulo de una línea con que el menú anuncia cada proceso.
 *
 * SON MAS CORTOS QUE LA DESCRIPCION DEL CATALOGO a propósito: en el catálogo la descripción explica
 * el proceso, y aquí solo tiene que caber bajo el nombre sin partirse en tres líneas. Un proceso sin
 * entrada aquí cae a la descripción del catálogo, que siempre existe.
 */
const DETALLES_DEL_MENU: Record<string, string> = {
  'Escuelas de música': 'Formación musical, cobertura e indicadores.',
  'Escenarios': 'Infraestructura para la música.',
  'Festivales': 'Circulación y celebración territorial.',
  'Mercados musicales': 'Nodos de intercambio y circulación.',
  'Redes y documentación': 'Memoria, investigación y archivos.',
  'Lutería': 'Saberes, oficios e instrumentos.',
};

@Component({
  selector: 'app-navigation',
  standalone: true,
  imports: [
    CommonModule,
    LucideChevronDown,
    LucideMenu,
    LucideX,
    LucideArrowUpRight,
    LucideLogIn,
    LucideUserPlus
  ],
  templateUrl: './navigation.component.html',
  styleUrls: ['./navigation.component.css']
})
export class NavigationComponent {

  public navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);

  /**
   * EL BOTON DE ENTRAR Y EL PANEL SON EL MISMO SITIO, y solo uno se pinta.
   *
   * Quien ya entro no necesita una puerta: necesita saber con que organizacion esta dentro y como
   * salir. Mostrar «Iniciar sesion» a alguien que acaba de iniciarla es decirle que no lo consiguio.
   */
  readonly sesionExterna = inject(ExternalSessionService);

  @Input() scrolled = false;
  @Input() forceSolid = false;

  // Reactivo: Observamos signals del servicio
  activePage = this.navigationService.activePage;
  mobileMenuOpen = this.navigationService.mobileMenuOpen;
  activeNavDropdown = this.navigationService.activeNavDropdown;
  activeEjeMenuId = this.navigationService.activeEjeMenuId;

  // Enlaces de navegación traducidos dinámicamente.
  //
  // Aquí había un `map` que le devolvía al enlace del mapa el nombre fijo
  // «Ecosistema», pisando lo que dijera el panel. Era un parche contra la
  // deriva del catálogo, que sembraba `nav_mapa` como «Mapa Ecosistémico»
  // mientras el sitio compilaba «Ecosistema»: sin el parche, sembrar una base
  // nueva cambiaba el menú de todo el portal. La deriva se corrigió en el
  // origen —el registro ya dice «Ecosistema» y un paso de CI vigila que el
  // catálogo no se separe—, así que el parche sobraba y dejaba la clave muda:
  // la editora la cambiaba, publicaba, y el menú seguía igual.
  resolvedNavigationLinks = computed(() => this.navigationService.getResolvedNavigationLinks());

  primaryNavigationLinks = computed(() => {
    return this.resolvedNavigationLinks().filter(
      (link) => ![PAGE_IDS.ecosistema, PAGE_IDS.mapa].includes(link.id)
    );
  });

  featuredNavigationLinks = computed(() => {
    return this.resolvedNavigationLinks().filter(
      (link) => [PAGE_IDS.ecosistema, PAGE_IDS.mapa].includes(link.id)
    );
  });

  isDropdownLink(linkId: string): boolean {
    return ['ejes', PAGE_IDS.ecosistema].includes(linkId);
  }

  // Sección expandida dentro del menú móvil (acordeón de submenús)
  mobileExpandedSection = signal<string | null>(null);

  toggleMobileSection(linkId: string): void {
    this.mobileExpandedSection.update((current) => (current === linkId ? null : linkId));
  }

  /**
   * Sub-elementos del acordeón móvil. HOY SOLO LOS EJES.
   *
   * Tuvo también una rama para el Ecosistema, y era código muerto: la plantilla móvil recorre
   * `ecosystemMenuItems` por su cuenta desde que la lista dejó de estar agrupada, así que esa rama
   * —y la copia plana `ecosystemDropdownItems` que la alimentaba— no las llamaba nadie. Se fueron
   * con el mismo corte que dejó el menú colgando del catálogo: dos copias menos que mantener.
   */
  mobileSubItems(linkId: string): { label: string; action: () => void }[] {
    if (linkId === 'ejes') {
      return this.ejeNavigationGroups().map((group) => ({
        label: group.name,
        action: () => this.onNavigateToPageSection('ejes', group.sectionId),
      }));
    }
    return [];
  }

  // Apertura del mega-menú por foco de teclado (mouse sigue usando hover)
  onDropdownFocusIn(linkId: string): void {
    if (this.isDropdownLink(linkId)) {
      this.setActiveNavDropdown(linkId);
    }
  }

  onDropdownFocusOut(linkId: string, event: FocusEvent): void {
    if (!this.isDropdownLink(linkId)) return;
    const next = event.relatedTarget as Node | null;
    const current = event.currentTarget as HTMLElement | null;
    // Solo cerrar si el foco abandona por completo el contenedor del dropdown
    if (!next || !current || !current.contains(next)) {
      if (this.activeNavDropdown() === linkId) {
        this.setActiveNavDropdown(null);
      }
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.activeNavDropdown()) {
      this.setActiveNavDropdown(null);
    }
    if (this.mobileMenuOpen()) {
      this.navigationService.setMobileMenuOpen(false);
    }
  }

  private readonly hostElement = inject(ElementRef);

  /**
   * CIERRA TAMBIEN AL HACER CLIC FUERA, no solo con Escape o al salir el foco con el teclado.
   *
   * `alSalirElFocoDelMenuDeCuenta` (mas abajo) cubre el tabulador, pero un clic del raton sobre
   * una zona sin foco propio -el fondo de la pagina, un parrafo- no mueve `document.activeElement`
   * a ningun sitio: el `focusout` nunca llega y el menu se queda abierto. Este listener es el
   * complemento para el raton, no un reemplazo: los dos coexisten porque cierran por dos rutas
   * distintas.
   */
  @HostListener('document:pointerdown', ['$event'])
  onDocumentPointerDown(event: PointerEvent): void {
    if (!this.activeNavDropdown()) return;
    const objetivo = event.target as Node | null;
    if (!objetivo || !this.hostElement.nativeElement.contains(objetivo)) {
      this.setActiveNavDropdown(null);
    }
  }

  /**
   * Los seis procesos del Ecosistema en el menú, CON EL DESTINO QUE DICE EL CATALOGO.
   *
   * SON SEIS Y NO OCHO. Agrupaciones y Agentes salieron del menú porque no tienen tabla ni
   * endpoint: su entrada llevaba a una pantalla de «próximamente». La razón está escrita en
   * `categorias-ecosistema.config.ts`, que marca con `proceso` los que sí son procesos del
   * Ecosistema, y es la misma lista que muestra la rejilla de la portada.
   *
   * ANTES ESTO ERAN TRES GRUPOS CON TÍTULO, Y EL TÍTULO NO SE PINTABA EN NINGÚN SITIO. Existían
   * para partir el panel en tres columnas y servían de clave de `track`, nada más: quien leyera
   * «Personas y procesos» en el código buscaría en vano ese rótulo en la pantalla. Ahora la
   * lista es plana y las columnas las hace la rejilla, que es de quien son.
   *
   * <b>Y ANTES TAMBIEN DECLARABA SUS PROPIAS DIRECCIONES.</b> Ninguna de las cinco que no son
   * Festivales existía en el enrutador: `ecosistema/mercados-musicales`, `escuelas`, `escenarios`,
   * `redes-documentacion` y `luteria` caían en «no encontrado». se detectó
   * entrando por la de mercados, que además SÍ tiene directorio desde
   * —el enrutador la había registrado con el nombre corto, `ecosistema/mercados`—. Un menú con su
   * propia lista de destinos es una segunda copia que diverge en cuanto una ruta cambia de nombre.
   *
   * <b>AHORA EL DESTINO SALE DE `CATEGORIAS_ECOSISTEMA`</b>, la lista única de las tres superficies
   * que ofrecen el Ecosistema. Un proceso sin directorio público no tiene `route`: aquí eso
   * significa que la entrada se ve —saber que el proceso existe es información que la portada del
   * Ecosistema también da— pero no se dibuja como un control, porque no lleva a ningún sitio.
   *
   * El rótulo de una línea se queda aquí porque es copia de esta pantalla: el catálogo describe el
   * proceso, el menú lo anuncia en una línea.
   */
  readonly ecosystemMenuItems: EntradaDelMenuDelEcosistema[] = CATEGORIAS_ECOSISTEMA
    .filter(categoria => categoria.proceso)
    .map(categoria => ({
      label: categoria.title,
      page: categoria.route ?? null,
      detail: DETALLES_DEL_MENU[categoria.title] ?? categoria.description,
    }));

  // Mapeo dinámico de ejes para la barra de navegación con traducción del CMS
  ejeNavigationGroups = computed(() => {
    return ejesDataGlobal.map((group, idx) => {
      const name = this.webTexts.getWebText(`eje0${idx + 1}_title`);

      return {
        id: group.id,
        name,
        sectionId: idx === 0 ? 'musica-para-la-vida' : idx === 1 ? 'oficios-y-practicas' : 'gobernanza',
        components: group.components.map((comp, cIdx) => ({
          id: comp.id,
          name: this.webTexts.getWebText(`eje0${idx + 1}_c${cIdx + 1}_title`)
        }))
      };
    });
  });

  // Eje sobre el que está el cursor en el menú (null = ninguno, no se muestran componentes)
  hoveredEjeGroup = computed(() => {
    const hoveredId = this.activeEjeMenuId();
    if (!hoveredId) return null;
    return this.ejeNavigationGroups().find((g) => g.id === hoveredId) || null;
  });

  clearEjeMenuHover(): void {
    this.setActiveEjeMenuId(null);
  }

  isEjesRelatedPage = computed(() => {
    const page = this.activePage();
    return page === PAGE_IDS.ejes || page.startsWith('comp-');
  });

  isActiveLink(linkId: string): boolean {
    if (linkId === 'ejes') {
      return this.isEjesRelatedPage();
    }
    return this.activePage() === linkId;
  }

  /**
   * DONDE ACABA LA BARRA Y EMPIEZA LA PAGINA.
   *
   * En estado solido el borde inferior era `border-white/5` —blanco al 5 % sobre morado—: contra un
   * fondo claro como el del panel de la organizacion no se distingue de la propia barra, y la
   * pantalla parecia empezar sin borde. Lo señalo criterio: «persiste la
   * falta de delimitacion entre la web y la barra de navegacion».
   *
   * SE RESUELVE CON EL VERDE DE LA MARCA Y NO CON UN GRIS: una linea gris sobre morado se lee como
   * suciedad; el verde al 30 % se lee como el remate de la barra. La sombra se refuerza para que el
   * corte exista tambien cuando la pagina de debajo es blanca.
   */
  getNavClass(): string {
    const isSolid = this.forceSolid || this.scrolled || this.mobileMenuOpen();
    return isSolid
      ? 'py-4 bg-[#291242] backdrop-blur-md shadow-[0_6px_20px_rgba(41,18,66,0.28)] border-b-2 border-[#00DA5E]/30'
      : 'py-8 bg-transparent';
  }

  getPrimaryLinkClass(linkId: string): string {
    const active = this.isActiveLink(linkId) || this.activeNavDropdown() === linkId;
    return active ? 'text-[#00DA5E]' : 'text-white/72 hover:text-[#00DA5E]';
  }

  /**
   * LOS DOS ENLACES DESTACADOS NO SE PINTAN IGUAL, IGUAL QUE EN EL REPOSITORIO DE
   * REFERENCIA. «Ecosistema» es el boton solido verde -la llamada a la accion
   * principal del header, equivalente al boton «SIMUS» de la referencia- y «Mapa»
   * es el boton translucido con borde, un acceso secundario.
   */
  getFeaturedLinkClass(linkId: string): string {
    const active = this.isActiveLink(linkId);
    if (linkId === PAGE_IDS.ecosistema) {
      return active
        ? 'rounded-xl px-5 py-3 bg-[#8BF784] text-[#291242] focus-visible:ring-[#00DA5E]'
        : 'rounded-xl px-5 py-3 bg-[#00DA5E] text-[#291242] hover:bg-[#8BF784] focus-visible:ring-[#00DA5E]';
    }
    return active
      ? 'rounded-xl px-5 py-3 bg-white text-[#291242] focus-visible:ring-white'
      : 'rounded-xl px-5 py-3 border border-white/25 bg-white/10 text-white hover:border-white/40 hover:bg-white/20 focus-visible:ring-white';
  }

  getEjeGroupClass(groupId: string): string {
    const isHovered = this.activeEjeMenuId() === groupId;
    return isHovered
      ? 'border-[#00DA5E] bg-slate-50/70'
      : 'border-transparent hover:bg-slate-50/40';
  }

  getMobileLinkClass(linkId: string): string {
    return this.isActiveLink(linkId) 
      ? 'text-[#00DA5E]' 
      : 'text-white/60 hover:text-[#00DA5E]';
  }

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }
  /**
   * Imagen editable. Devuelve la publicada en el CMS o, si no hay ninguna, la
   * compilada del registro: nunca cadena vacia, porque un `src` vacio hace que
   * el navegador vuelva a pedir la pagina. Ver `registro-de-imagenes-web.ts`.
   */
  imagen(clave: string): string {
    return this.webTexts.getWebImage(clave);
  }

  /** Texto alternativo de esa misma ranura. */
  imagenAlt(clave: string): string {
    return this.webTexts.getWebImageAlt(clave);
  }

  onPageChange(pageId: string): void {
    this.navigationService.navigate(pageId);
  }

  /**
   * «ECOSISTEMA» ES UN ENLACE, NO SOLO UN DESPLEGABLE.
   *
   * Pulsarlo abría y cerraba el panel y no llevaba a ninguna parte: para entrar a la portada de la
   * sección había que abrir el menú y bajar hasta una tarjeta final llamada «Ver el ecosistema
   * completo». El criterio es este: «debe ser cliqueable para optimizar la
   * navegación y limpiar el menú adyacente». Son la misma frase: el botón se lleva el destino y esa
   * tarjeta sobra.
   *
   * EL DESPLEGABLE NO SE PIERDE. Sigue abriéndose al pasar el ratón y, para quien navega con
   * teclado, al recibir el foco —`onDropdownFocusIn`—, que es como se abría antes de que el clic
   * hiciera nada. Lo que cambia es qué hace el clic, no cómo se ven los seis procesos.
   */
  onFeaturedLinkClick(linkId: string): void {
    this.onPageChange(linkId);
  }

  /**
   * EL MENU DE LA CUENTA EXTERNA.
   *
   * Se apoya en `activeNavDropdown`, el mismo mecanismo que los desplegables de «Ejes» y
   * «Ecosistema», y no en una señal propia. Motivo: con dos mecanismos, abrir este menu dejaria
   * el otro desplegable abierto detras, y `@HostListener('document:keydown.escape')` solo cerraria
   * uno de los dos. Compartiendo señal, abrir uno cierra el otro sin escribir una linea para ello.
   */
  readonly MENU_DE_CUENTA = 'menu-de-cuenta';

  readonly menuDeCuentaAbierto = computed(() => this.activeNavDropdown() === this.MENU_DE_CUENTA);

  alternarMenuDeCuenta(): void {
    this.setActiveNavDropdown(this.menuDeCuentaAbierto() ? null : this.MENU_DE_CUENTA);
  }

  /**
   * EL MENU DE ACCESO, PARA QUIEN NO TIENE SESION.
   *
   * EL BOTON ABRIA UNA SOLA PUERTA. Llevaba directo a la pantalla de acceso, que abre en la
   * pestaña de ingresar: quien venia a registrar su organizacion aterrizaba en un formulario de
   * correo y contraseña que todavia no tiene, y tenia que encontrar la otra pestaña. El usuario lo
   * pidio: «el boton de inicio de sesion solo tiene una funcion, se sugiere
   * ampliarlo hacia ambas posibilidades».
   *
   * COMPARTE SEÑAL con el menu de cuenta y con los desplegables de la barra, por el mismo motivo
   * que aquel: abrir uno cierra los demas, y `Escape` los cierra todos sin una linea mas.
   */
  readonly MENU_DE_ACCESO = 'menu-de-acceso';

  readonly menuDeAccesoAbierto = computed(() => this.activeNavDropdown() === this.MENU_DE_ACCESO);

  alternarMenuDeAcceso(): void {
    this.setActiveNavDropdown(this.menuDeAccesoAbierto() ? null : this.MENU_DE_ACCESO);
  }

  /**
   * Las dos puertas del acceso externo.
   *
   * Son dos rutas públicas explícitas: una para acceso y otra para registro. No se usan
   * parámetros de modo, que escondían dos estados distintos detrás de una misma dirección.
   */
  irAIngresar(): void {
    this.setActiveNavDropdown(null);
    this.onNavigateToPath('ingresar');
  }

  irARegistrarse(): void {
    this.setActiveNavDropdown(null);
    this.navigationService.routerNavigate('registro');
  }

  /**
   * Cierra al salir el foco del menu, no al salir el raton: un menu que se cierra con el raton
   * pero no con el tabulador deja al teclado dentro de algo que ya no se ve.
   */
  alSalirElFocoDelMenuDeCuenta(event: FocusEvent): void {
    const siguiente = event.relatedTarget as Node | null;
    const contenedor = event.currentTarget as HTMLElement | null;
    if (!siguiente || !contenedor || !contenedor.contains(siguiente)) {
      this.setActiveNavDropdown(null);
    }
  }

  /**
   * Al panel de la organizacion, que tiene direccion propia desde.
   *
   * ANTES LLEVABA A `ecosistema/ingresar`, la pantalla de acceso, que con sesion abierta cambiaba
   * su vista interna y se quedaba en esa direccion. Es decir: «Mi panel» llevaba a una URL que
   * dice «ingresar» a alguien que ya entro.
   */
  irAMiPanel(): void {
    this.onNavigateToPath('gestion');
  }

  /**
   * Cierra la sesion y devuelve al inicio.
   *
   * SE VUELVE AL INICIO A PROPOSITO: si se cierra sesion estando en una pantalla que exige sesion
   * —el panel de la organizacion, el formulario del Festival—, quedarse alli deja a la vista un
   * formulario que ya no puede enviarse.
   */
  cerrarSesionExterna(): void {
    this.setActiveNavDropdown(null);
    this.sesionExterna.salir().subscribe(() => {
      this.navigationService.setMobileMenuOpen(false);
      this.navigationService.routerNavigate('');
    });
  }

  /**
   * ACEPTA `null` PORQUE EL MENU DEL ECOSISTEMA TIENE ENTRADAS SIN DESTINO: los procesos que
   * todavía no tienen directorio público. El botón ya viene desactivado, así que esto no debería
   * llegar a ejecutarse; la guarda está para que un destino que falte no acabe navegando a la
   * cadena vacía —que el enrutador resuelve como la portada— y parezca que el enlace «funciona».
   */
  onNavigateToPath(path: string | null): void {
    if (!path) return;
    this.navigationService.setActiveNavDropdown(null);
    this.navigationService.setMobileMenuOpen(false);
    this.navigationService.routerNavigate(path);
  }

  onNavigateToPageSection(pageId: string, sectionId: string): void {
    this.navigationService.setActiveNavDropdown(null);
    this.navigationService.setMobileMenuOpen(false);
    this.navigationService.navigate(pageId);
    
    // Simular scroll demorado hacia la sección en la página destino
    setTimeout(() => {
      const targetElement = document.getElementById(sectionId);
      if (targetElement) {
        const offset = 112; // NAVBAR_SCROLL_OFFSET
        const elementPosition = targetElement.getBoundingClientRect().top + window.pageYOffset;
        window.scrollTo({
          top: elementPosition - offset,
          behavior: 'smooth'
        });
      }
    }, 220);
  }

  onNavigateToComponentFromMenu(componentId: string): void {
    this.navigationService.navigateComponent(componentId);
  }

  toggleMobileMenu(): void {
    this.navigationService.setMobileMenuOpen(!this.mobileMenuOpen());
  }

  setActiveNavDropdown(dropdown: string | null): void {
    this.navigationService.setActiveNavDropdown(dropdown);
  }

  setActiveEjeMenuId(ejeId: string | null): void {
    this.navigationService.setActiveEjeMenuId(ejeId);
  }
}
