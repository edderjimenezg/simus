import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PAGE_IDS } from '../../../../core/services/navigation.service';
import { CATEGORIAS_ECOSISTEMA } from '../../../../core/services/categorias-ecosistema.config';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { DEFAULT_TEXTS } from '../../../../core/cms/registro-de-textos-web';
import { NavigationComponent } from './navigation.component';
import { ExternalSessionService } from '../../../../core/services/external-session.service';

/**
 * LOS DOS BOTONES DE ACCESO NO SE LOCALIZAN IGUAL.
 *
 * El de escritorio es un icono sin texto visible y solo tiene `aria-label`; el
 * del menú móvil sigue siendo un botón rotulado. Buscarlos por `textContent`
 * —como hacían estas pruebas— encontraría hoy uno solo, y la prueba que cuenta
 * dos habría pasado a verificar la mitad sin decirlo.
 *
 * Se busca por NOMBRE ACCESIBLE, que es lo que los dos comparten y lo que un
 * lector de pantalla anuncia. Si mañana el icono pierde su `aria-label`, esta
 * función deja de encontrarlo y las pruebas caen: que es exactamente lo que debe
 * pasar, porque un botón sin nombre accesible no es alcanzable.
 */
/**
 * Los mandos de acceso que hay pintados: el icono de escritorio y los dos botones del menú móvil.
 *
 * SE BUSCAN POR `data-testid` Y NO POR EL ROTULO «Iniciar sesión», que es como se buscaban hasta el
 * 31 de agosto de 2026. Ese día el icono pasó a abrir un menú con dos entradas —«Ingresar» y «Crear
 * cuenta o registrar organización»— y el rótulo dejó de ser una identidad: ahora hay tres mandos y
 * ninguno se llama así.
 */
function botonesDeAcceso(raiz: HTMLElement): HTMLButtonElement[] {
  return Array.from(raiz.querySelectorAll<HTMLButtonElement>(
    '[data-testid="iniciar-sesion"], [data-testid="movil-ingresar"], [data-testid="movil-registrarse"]',
  ));
}

describe('NavigationComponent ecosystem navigation', () => {
  let fixture: ComponentFixture<NavigationComponent>;
  let component: NavigationComponent;

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_texts');

    await TestBed.configureTestingModule({
      imports: [NavigationComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(NavigationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => localStorage.removeItem('pnmc_web_texts'));

  it('opens the ecosystem list from the Ecosistema text control', () => {
    expect(component.isDropdownLink(PAGE_IDS.ecosistema)).toBeTrue();
    expect(component.isDropdownLink(PAGE_IDS.mapa)).toBeFalse();
    expect(component.resolvedNavigationLinks().find(link => link.id === PAGE_IDS.ecosistema)?.name).toBe('Ecosistema');

    // EL CLIC YA NO ABRE EL PANEL: NAVEGA. Desde «Ecosistema» es un
    // enlace, y el desplegable se abre al pasar el ratón o al recibir el foco, que es por donde
    // llega quien navega con teclado. Se abre aquí por esa misma puerta.
    component.setActiveNavDropdown(PAGE_IDS.ecosistema);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const content = root.textContent ?? '';
    expect(component.activeNavDropdown()).toBe(PAGE_IDS.ecosistema);
    expect(content).toContain('Ecosistema');
    expect(content).toContain('Escuelas de música');
    expect(content).not.toContain('Personas y procesos');
    expect(content).not.toContain('Circulación y espacios');
    expect(content).not.toContain('Conocimiento y oficios');
    expect(root.querySelector('[data-testid="ecosystem-tool-grid"]')?.className).toContain('grid-cols-2');
    expect(root.querySelectorAll('[data-testid="ecosystem-tool-item"]').length).toBe(6);
    // Los recuadros grises que envolvian cada columna se fueron con los grupos.
    expect(root.querySelectorAll('[data-testid="ecosystem-tool-group"]').length).toBe(0);
    // La tarjeta «Herramienta principal» que llevaba al mapa salió del desplegable: el mapa
    // tiene botón propio en la barra, dos posiciones a la izquierda. Se comprueba por el
    // `data-testid` Y por el rótulo, porque borrar solo el atributo dejaría la tarjeta en
    // pantalla con la prueba en verde.
    expect(root.querySelector('[data-testid="ecosystem-map-tool"]')).toBeNull();
    expect(content).not.toContain('Herramienta principal');
    expect(content).not.toContain('Mapa ecosistémico');
    expect(content).not.toContain('Acerca de SIMUS');
    expect(content).not.toContain('Ayuda y tutoriales');
    expect(content).not.toContain('Ingresar');
    expect(content).not.toContain('Ser parte del SIMUS');
  });

  it('el rótulo del menú lo manda el panel, no el código', async () => {
    // El componente tenía un `map` que le devolvía a este enlace el nombre fijo
    // «Ecosistema» después de haberlo resuelto contra el CMS: la editora lo
    // cambiaba, publicaba, y el menú seguía igual. Aquí se comprueba lo
    // contrario —que ahora manda lo publicado— y también el rótulo del panel
    // de componentes, que antes no lo leía nadie.
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NavigationComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    const textos = TestBed.inject(TextosWebService);
    spyOn(textos, 'getWebText').and.callFake((clave: string) =>
      clave === 'nav_ecosistema' ? 'Mapa Ecosistémico'
        : clave === 'nav_components_title' ? 'Piezas del eje'
          : DEFAULT_TEXTS[clave] ?? '');

    const conCms = TestBed.createComponent(NavigationComponent);
    conCms.detectChanges();

    expect(conCms.componentInstance.resolvedNavigationLinks()
      .find(link => link.id === PAGE_IDS.ecosistema)?.name).toBe('Mapa Ecosistémico');

    const idDelPrimerEje = conCms.componentInstance.ejeNavigationGroups()[0].id;
    conCms.componentInstance.setActiveNavDropdown(PAGE_IDS.ejes);
    conCms.componentInstance.setActiveEjeMenuId(idDelPrimerEje);
    conCms.detectChanges();

    expect((conCms.nativeElement as HTMLElement).textContent).toContain('Piezas del eje');
  });

  it('el botón «Mapa» va al mapa y no abre nada fuera del portal', () => {
    // Este botón ocupa el sitio que tenía SIMUS, que sí salía del portal con
    // `window.open`. El espía sigue puesto para que un regreso de aquel
    // comportamiento —abrir una pestaña nueva desde la barra— ponga la prueba en
    // rojo, no solo el destino equivocado.
    const open = spyOn(window, 'open');
    const navegar = spyOn(component.navigationService, 'navigate');

    component.onFeaturedLinkClick(PAGE_IDS.mapa);

    expect(navegar).toHaveBeenCalledOnceWith(PAGE_IDS.mapa);
    expect(open).not.toHaveBeenCalled();
  });

  it('«Ver Mapa» va antes que «Ecosistema» en la barra', () => {
    // El orden en pantalla es el orden de `navigationLinks`: `featuredNavigationLinks`
    // filtra sin reordenar. Se comprueba sobre el DOM y no sobre el array porque un
    // `flex-row-reverse` en el contenedor invertiría lo que ve el visitante sin tocar
    // el array, y esta prueba seguiría verde.
    const rotulos = Array.from(
      (fixture.nativeElement as HTMLElement)
        .querySelectorAll<HTMLElement>('[data-testid="enlace-destacado"] > button'),
    ).map(boton => (boton.textContent ?? '').trim());

    expect(rotulos).toEqual(['Ver Mapa', 'Ecosistema']);
  });

  it('«Ecosistema» es el único botón sólido verde de la barra, como en el repositorio de referencia', () => {
    // El rótulo y el destino no bastan: lo que distingue a este enlace del resto
    // de la barra es que es un botón sólido, la llamada a la acción principal del
    // header (equivalente al botón «SIMUS» de la referencia).
    const clasesDelEcosistema = component.getFeaturedLinkClass(PAGE_IDS.ecosistema);

    expect(clasesDelEcosistema).toContain('bg-[#00DA5E]');
    expect(clasesDelEcosistema).toContain('text-[#291242]');
    expect(clasesDelEcosistema).toContain('hover:bg-[#8BF784]');
    // Y el de al lado NO lo es: si el caso especial se colara en el resto, la
    // barra entera se llenaría de botones verdes. «Ver Mapa» es un botón
    // secundario, translúcido y con borde.
    const clasesDelMapa = component.getFeaturedLinkClass(PAGE_IDS.mapa);
    expect(clasesDelMapa).toContain('border');
    expect(clasesDelMapa).toContain('bg-white/10');
    expect(clasesDelMapa).not.toContain('bg-[#00DA5E]');
  });

  it('el último enlace conserva su rótulo aunque no tenga clave en el catálogo', () => {
    // El defecto que esto impide, y que ocurrió de verdad el 22 ago 2026 con el
    // enlace que ocupaba este sitio: `getResolvedNavigationLinks()` arma
    // `nav_${link.id}` para cada enlace, y **`nav_simus` nunca existió** en el
    // registro. Mientras hubo un texto de reserva, el `||` lo tapaba; al
    // quitarlo, `getWebText` devolvió cadena vacía y **«SIMUS» desapareció del
    // menú en todas las páginas**. Las otras pruebas que lo tocaban lo
    // localizaban por `window.open` o por `href`: **seguían verdes con el rótulo
    // en blanco**. Esta es la que no.
    //
    // El riesgo es el mismo hoy: `nav_mapa` tampoco existe —se renombró a
    // `nav_ecosistema` (`registro-de-textos-web.ts`)—, así que este enlace vive
    // igual de `rotuloFijo`.
    const mapa = component.resolvedNavigationLinks().find(link => link.id === PAGE_IDS.mapa);

    expect(mapa).withContext('el enlace al mapa desapareció del menú').toBeTruthy();
    expect(mapa!.name).toBe('Ver Mapa');
  });

  it('los rótulos que SÍ vienen del catálogo no llegan nunca vacíos', () => {
    // La otra mitad del mismo riesgo: un enlace cuya clave se renombre a medias
    // —como pasó con `nav_mapa` → `nav_ecosistema`— dejaría su rótulo en blanco
    // sin que nada fallara. El menú es lo primero que ve un visitante.
    const vacios = component.resolvedNavigationLinks()
      .filter(link => !link.name || !link.name.trim())
      .map(link => link.id);

    expect(vacios).toEqual([]);
  });

  it('el menú móvil ya no saca al visitante a SIMUS', () => {
    // El menú móvil tenía una rama propia para SIMUS: un `<a>` con `target`
    // `_blank`. Al quedarse sin rama, el enlace cae en el `@else` común y
    // navega por dentro como los demás. Se comprueba lo uno y lo otro porque
    // borrar la rama sin dejar rótulo dejaría el menú móvil con un hueco.
    component.navigationService.setMobileMenuOpen(true);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('a[href="https://simus.mincultura.gov.co/"]')).toBeNull();
    expect(root.querySelector('a[target="_blank"]')).toBeNull();

    // El rótulo se busca por igualdad exacta y no con `toContain` sobre el texto
    // de la página: el acordeón del ecosistema tiene dentro «Mapa ecosistémico»,
    // y un `toContain('Mapa')` seguiría verde aunque este botón desapareciera.
    const navegar = spyOn(component.navigationService, 'navigate');
    const botonDelMapa = Array.from(root.querySelectorAll('button'))
      .find(boton => (boton.textContent ?? '').trim() === 'Ver Mapa');

    expect(botonDelMapa).withContext('el menú móvil se quedó sin el botón «Mapa»').toBeTruthy();
    botonDelMapa!.click();
    expect(navegar).toHaveBeenCalledOnceWith(PAGE_IDS.mapa);
  });

  it('la puerta a la portada del ecosistema es el propio botón, y ya no una tarjeta del menú', () => {
    // POR QUÉ EXISTIÓ ESTA PRUEBA. Al convertir SIMUS en enlace saliente, `/ecosistema` se quedó
    // sin ningún enlace que llevara a ella: era la página que abría aquel botón. La puerta se puso
    // entonces al final del desplegable, en una tarjeta llamada «Ver el ecosistema completo».
    //
    // POR QUÉ CAMBIA DE FORMA. Se define que «Ecosistema» fuera
    // cliqueable «para optimizar la navegación y limpiar el menú adyacente». Con el botón llevando
    // a la portada, aquella tarjeta era un segundo camino al mismo sitio dentro del mismo menú.
    // La regla que se protege es la misma —que la portada tenga puerta—; lo que cambia es cuál.
    const navegar = spyOn(component.navigationService, 'navigate');

    component.onFeaturedLinkClick(PAGE_IDS.ecosistema);

    expect(navegar).toHaveBeenCalledOnceWith(PAGE_IDS.ecosistema);

    component.setActiveNavDropdown(PAGE_IDS.ecosistema);
    fixture.detectChanges();
    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('[data-testid="ecosystem-overview-link"]'))
      .withContext('la tarjeta duplicaba el destino del botón').toBeNull();
    expect(raiz.textContent ?? '')
      .not.toContain('Ver el ecosistema completo');
  });

  it('el menú ofrece los seis procesos, y no Agrupaciones ni Agentes', () => {
    // POR NOMBRE Y EN ORDEN, no por cantidad: seis entradas equivocadas también son seis.
    // Agrupaciones y Agentes salieron porque no tienen tabla ni endpoint, que es la misma razón
    // por la que `categorias-ecosistema.config.ts` no los marca como `proceso`. La entrada existía
    // y llevaba a una pantalla de «próximamente».
    expect(component.ecosystemMenuItems.map(item => item.label)).toEqual([
      'Escuelas de música',
      'Escenarios',
      'Festivales',
      'Mercados musicales',
      'Redes y documentación',
      'Lutería',
    ]);

    const destinos = component.ecosystemMenuItems.map(item => item.page);
    expect(destinos).not.toContain('ecosistema/agrupaciones');
    expect(destinos).not.toContain('ecosistema/agentes');
  });

  it('el destino de cada proceso lo pone el catálogo, no el menú', () => {
    // EL DEFECTO QUE FIJA. El menú llevaba su propia lista de direcciones y ninguna de las cinco
    // que no eran Festivales existía en el enrutador. La dirección de producto entró por la de
    // mercados —que sí tiene directorio— y aterrizó en «página no encontrada», porque el menú
    // decía `ecosistema/mercados-musicales` y el enrutador conocía `ecosistema/mercados`.
    // Comparar contra `CATEGORIAS_ECOSISTEMA` es lo que impide que vuelvan a separarse.
    const esperado = CATEGORIAS_ECOSISTEMA
      .filter(categoria => categoria.proceso)
      .map(categoria => categoria.route ?? null);

    expect(component.ecosystemMenuItems.map(item => item.page)).toEqual(esperado);
  });

  it('un proceso sin directorio se ve pero no se puede pulsar', () => {
    // NI SE ESCONDE NI ENGAÑA. Saber que el Ecosistema tiene seis procesos es información, y así
    // lo hace también la portada del Ecosistema con sus tarjetas; lo que no puede es parecer un
    // enlace y no llevar a ningún sitio. Se comprueba sobre lo PINTADO, que es donde se pulsa.
    component.setActiveNavDropdown(PAGE_IDS.ecosistema);
    fixture.detectChanges();

    const botones = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[data-testid="ecosystem-tool-item"]'),
    );
    const activos = botones.filter(boton => !boton.disabled).map(boton => boton.textContent?.trim() ?? '');

    expect(botones.length).toBe(6);
    expect(activos.length).toBe(2);
    expect(activos[0]).toContain('Festivales');
    expect(activos[1]).toContain('Mercados musicales');
    expect(botones.filter(boton => boton.disabled).length).toBe(4);
  });

  it('el menú del móvil pinta exactamente los mismos seis', () => {
    // SON DOS PLANTILLAS DISTINTAS con la misma lista, y ese es justo el modo de que se
    // separen: la de escritorio se cambia, la de móvil se olvida, y nadie lo nota porque
    // ninguna prueba abre el menú pequeño. Se abre y se leen los rótulos PINTADOS, no la
    // propiedad del componente: comprobar el dato no dice que la plantilla lo use.
    component.navigationService.setMobileMenuOpen(true);
    component.toggleMobileSection(PAGE_IDS.ecosistema);
    fixture.detectChanges();

    const rotulos = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[data-testid="ecosystem-mobile-item"]'),
    ).map(boton => boton.textContent?.trim());

    // EMPIEZA POR el rótulo y no es igual a él: los cuatro procesos que todavía no tienen
    // directorio llevan además «Próximamente» dentro del mismo botón, igual que en escritorio.
    expect(rotulos.length).toBe(6);
    component.ecosystemMenuItems.forEach((item, indice) => {
      expect(rotulos[indice]).withContext(item.label).toContain(item.label);
    });
  });

  it('ningún destino del menú apunta ya a /simus', () => {
    // `/simus/*` sobrevive como redirección para no romper marcadores, pero un
    // menú que sigue apuntando ahí manda a cada visitante por un salto de más
    // y deja la URL vieja copiándose y compartiéndose indefinidamente.
    const destinos = component.ecosystemMenuItems.map(item => item.page);

    expect(destinos.filter(page => page?.startsWith('simus'))).toEqual([]);
    expect(destinos).toContain('ecosistema/festivales');
  });

  it('en móvil la portada del ecosistema tiene su propia puerta, porque el rótulo despliega', () => {
    // EN ESCRITORIO LA PUERTA ES EL ROTULO, que desde navega al pulsarlo.
    // En móvil ese mismo rótulo abre el acordeón, así que ahí hace falta una entrada propia. Va la
    // PRIMERA y no la última, igual que «Ver todos los ejes» en el acordeón de al lado; la que se
    // retiró era una tarjeta al final que duplicaba el destino del rótulo de escritorio.
    // El menú móvil lo abre el servicio, no el componente: `mobileMenuOpen` es una señal de solo
    // lectura que el componente reexpone.
    const navegar = spyOn(component.navigationService, 'navigate');
    component.toggleMobileMenu();
    component.toggleMobileSection(PAGE_IDS.ecosistema);
    fixture.detectChanges();

    const puerta = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="ecosystem-mobile-portada"]');

    expect(puerta).withContext('el menú móvil se quedó sin enlace a la portada').not.toBeNull();
    puerta?.click();
    expect(navegar).toHaveBeenCalledOnceWith(PAGE_IDS.ecosistema);
  });

  // La version anterior de esta prueba fijaba el destino `admin` y se llamaba «takes the
  // login button to the existing admin login». Cumplia su trabajo: cuando el destino cambio,
  // se puso en rojo y obligo a mirar. Lo que cambio es la decision, no la prueba.
  //
  // POR QUE SE COMPRUEBAN LOS DOS BOTONES Y NO EL PRIMERO. Hay dos «Iniciar sesion» —el de
  // escritorio y el del menu de movil— con el mismo texto y el mismo destino escrito aparte.
  // `find` devolvia solo el primero, de modo que el de movil podia quedarse apuntando a la
  // consola sin que ninguna prueba se enterara.
  it('los mandos de acceso llevan a la entrada del ecosistema, y el de registro por su pestaña', () => {
    // El segundo boton vive dentro del menu de movil, que cuelga de `@if (mobileMenuOpen())`.
    // Sin abrirlo, el fixture solo pinta uno y la prueba examinaria la mitad del asunto —que
    // es exactamente lo que hacia la version anterior sin decirlo.
    component.navigationService.setMobileMenuOpen(true);
    fixture.detectChanges();

    const navigate = spyOn(component.navigationService, 'routerNavigate');
    const raiz = fixture.nativeElement as HTMLElement;

    // TRES MANDOS Y NO DOS desde: el icono de escritorio y, en el menú
    // móvil, «Ingresar» y «Crear cuenta». El icono ABRE UN MENÚ, así que no navega al pulsarlo:
    // navegan las dos entradas de dentro.
    expect(botonesDeAcceso(raiz).length).toBe(3);

    raiz.querySelector<HTMLButtonElement>('[data-testid="movil-ingresar"]')!.click();
    raiz.querySelector<HTMLButtonElement>('[data-testid="movil-registrarse"]')!.click();

    component.irAIngresar();
    component.irARegistrarse();

    expect(navigate.calls.allArgs()).toEqual([
      ['ingresar'],
      ['registro'],
      ['ingresar'],
      ['registro'],
    ]);
  });

  // La guarda de verdad. La prueba de arriba dice a donde VA el boton que hoy existe; esta
  // dice que NINGUN mando del sitio publico lleva a la consola, incluido el que alguien
  // añada mañana. Sin ella, un tercer enlace a `admin` entraria sin ponerse nada en rojo.
  it('ningun mando de la navegacion publica lleva a la consola institucional', () => {
    component.navigationService.setMobileMenuOpen(true);
    fixture.detectChanges();

    const navigate = spyOn(component.navigationService, 'routerNavigate');
    const botones = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));

    for (const boton of botones) {
      boton.click();
    }

    const destinos = navigate.calls.allArgs().map(args => String(args[0]));
    expect(destinos.filter(destino => destino === 'admin' || destino.startsWith('admin/'))).toEqual([]);
  });
});

/**
 * El panel de la organización en la barra pública.
 *
 * EL DEFECTO QUE ESTO FIJA: la sesión existía en la cookie y en el servidor, pero la barra no la
 * miraba. Una organización entraba, volvía al inicio y se encontraba otra vez el botón «Iniciar
 * sesión»; el único sitio del portal que sabía que había alguien dentro era /registro.
 *
 * SE MIRA EL DOM, NO EL COMPONENTE. Que `dentro()` devuelva true no es que el panel se pinte: los
 * dos bloques —escritorio y móvil— tienen que cambiar los dos, y el móvil vive detrás de un @if.
 */
describe('NavigationComponent · panel de sesión externa', () => {
  let fixture: ComponentFixture<NavigationComponent>;
  let sesion: ExternalSessionService;

  const SESION = {
    userId: '13',
    fullName: 'Camila Prueba Responsable',
    email: 'creacionorgprueba@pnmc.test',
    accountStatus: 'activo',
    organizations: [{ id: '117', name: 'CreacionOrgPrueba', role: 'administrador' }],
  };

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_texts');
    await TestBed.configureTestingModule({
      imports: [NavigationComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(NavigationComponent);
    sesion = TestBed.inject(ExternalSessionService);
    fixture.detectChanges();
  });

  afterEach(() => {
    sesion.establecer(null);
    localStorage.removeItem('pnmc_web_texts');
  });

  it('sin sesión ofrece la puerta, no un panel', () => {
    const raiz = fixture.nativeElement as HTMLElement;

    // El menú móvil está cerrado, así que el único mando pintado es el icono de escritorio.
    expect(botonesDeAcceso(raiz).length).toBe(1);
    expect(raiz.querySelectorAll('[data-panel-sesion-externa]').length).toBe(0);
    // Y el menú de acceso nace cerrado: el icono lo abre, no lo trae abierto.
    expect(raiz.querySelector('[data-testid="menu-de-acceso"]')).toBeNull();
  });

  it('el acceso de escritorio es un icono limpio, sin círculo ni botón verde', () => {
    // No es verde porque «Ecosistema» lo es: dos botones sólidos verdes en la
    // misma barra se leían como el mismo tipo de acción. Y no lleva círculo ni
    // borde, igual que en el repositorio de referencia: es un icono discreto, no
    // un segundo botón. Al perder el texto visible, el nombre accesible pasa a
    // ser lo único que lo nombra.
    const acceso = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="iniciar-sesion"]');

    expect(acceso).withContext('desapareció el acceso a la sesión').toBeTruthy();
    // SE LLAMA «Acceso externo» Y NO «Iniciar sesión» desde: abre un menú
    // con dos puertas, y anunciarlo como «Iniciar sesión» diría la mitad de lo que hace.
    expect(acceso!.getAttribute('aria-label')).toBe('Acceso externo');
    expect(acceso!.getAttribute('aria-haspopup')).toBe('menu');
    expect(acceso!.textContent?.trim()).toBe('');
    expect(acceso!.className).not.toContain('bg-[#00DA5E]');
    expect(acceso!.className).not.toContain('rounded-full');
    expect(acceso!.className).not.toContain('border-white');
    expect(acceso!.querySelector('svg')).withContext('el icono no se pintó').toBeTruthy();
  });

  /**
   * UN CLIC FUERA TAMBIEN LO CIERRA, no solo Escape o el tabulador.
   *
   * El criterio es este: el menú de «Ingresar / Crear cuenta» quedaba
   * abierto al hacer clic en cualquier zona sin foco propio -el fondo de la página, un párrafo-
   * porque `alSalirElFocoDelMenuDeCuenta` solo escucha `focusout`, y un clic ahí no mueve
   * `document.activeElement`. `onDocumentPointerDown` es el complemento para el ratón.
   */
  it('un clic fuera del menú de acceso lo cierra', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const boton = raiz.querySelector<HTMLButtonElement>('[data-testid="iniciar-sesion"]')!;

    boton.click();
    fixture.detectChanges();
    expect(raiz.querySelector('[data-testid="menu-de-acceso"]')).withContext('el menú no se abrió').toBeTruthy();

    document.body.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    fixture.detectChanges();

    expect(raiz.querySelector('[data-testid="menu-de-acceso"]')).withContext('el menú siguió abierto tras el clic fuera').toBeNull();
  });

  it('un clic dentro del menú de acceso no lo cierra', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const boton = raiz.querySelector<HTMLButtonElement>('[data-testid="iniciar-sesion"]')!;

    boton.click();
    fixture.detectChanges();
    const panel = raiz.querySelector<HTMLElement>('[data-testid="menu-de-acceso"]')!;

    panel.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    fixture.detectChanges();

    expect(raiz.querySelector('[data-testid="menu-de-acceso"]')).withContext('el clic dentro cerró el menú').toBeTruthy();
  });

  it('con sesión el botón de entrar deja su sitio al panel de la organización', () => {
    sesion.establecer(SESION);
    fixture.componentInstance.toggleMobileMenu();
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    const paneles = raiz.querySelectorAll('[data-panel-sesion-externa]');

    // Dos: el de escritorio y el del menú móvil. Si solo cambiara uno, la mitad del portal
    // seguiría pidiendo entrar a quien ya entró.
    expect(paneles.length).toBe(2);
    expect(raiz.textContent).toContain('CreacionOrgPrueba');
    expect(raiz.textContent).toContain('Mi panel');
    // Por nombre accesible: el de escritorio ya no tiene texto, así que un
    // `not.toContain` sobre `textContent` seguiría verde aunque el icono se quedara
    // pintado encima del panel de la organización.
    expect(botonesDeAcceso(raiz).length).toBe(0);
  });

  it('«Gestión» lleva a la dirección propia del panel, no a la pantalla de acceso', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();

    const componente = fixture.componentInstance;
    spyOn(componente, 'onNavigateToPath');
    componente.irAMiPanel();

    // ANTES LLEVABA A `ecosistema/ingresar`. Con sesión abierta esa pantalla cambiaba su vista
    // interna y se quedaba en esa dirección: «Mi panel» llevaba a una URL que dice «ingresar» a
    // quien ya entró, no se podía guardar en marcadores y volver atrás devolvía al formulario.
    expect(componente.onNavigateToPath).toHaveBeenCalledWith('gestion');
  });

  // ---------------------------------------------------------------------------------------------
  // El menú de la cuenta
  //
  // El chip de la barra pasó de ser un enlace a ser el disparador de un menú. Antes había DOS
  // botones sueltos compitiendo por sitio en la barra —«Mi panel» y «Salir»—, y cada destino nuevo
  // de la cuenta habría añadido otro.
  // ---------------------------------------------------------------------------------------------

  const raizDe = () => fixture.nativeElement as HTMLElement;
  const disparador = () => raizDe().querySelector<HTMLButtonElement>('[data-menu-de-cuenta]')!;
  const panelDelMenu = () => raizDe().querySelector('[data-menu-de-cuenta-panel]');

  it('el menú nace cerrado y el disparador lo dice', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();

    expect(panelDelMenu()).withContext('el menú no debería estar abierto de entrada').toBeNull();
    expect(disparador().getAttribute('aria-expanded')).toBe('false');
    expect(disparador().getAttribute('aria-haspopup')).toBe('menu');
  });

  it('al pulsar el chip se abre el menú con sus entradas', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();

    disparador().click();
    fixture.detectChanges();

    const panel = panelDelMenu()!;
    expect(panel).withContext('el menú no se abrió').toBeTruthy();
    expect(panel.getAttribute('role')).toBe('menu');
    expect(disparador().getAttribute('aria-expanded')).toBe('true');

    // Quién está dentro: el nombre de la PERSONA, que el chip no puede mostrar porque solo le cabe
    // el de la organización.
    expect(panel.textContent).toContain('Camila Prueba Responsable');
    expect(panel.textContent).toContain('CreacionOrgPrueba');

    const entradas = Array.from(panel.querySelectorAll('[role="menuitem"]'))
      .map((e) => (e.textContent ?? '').trim());
    expect(entradas).toEqual(['Gestión', 'Datos de la organización', 'Cuenta y seguridad', 'Cerrar sesión']);
  });

  it('cada entrada del menú es un botón, para que el tabulador y el Enter también sirvan', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();
    disparador().click();
    fixture.detectChanges();

    const entradas = Array.from(panelDelMenu()!.querySelectorAll('[role="menuitem"]'));
    expect(entradas.length).toBe(4);
    for (const entrada of entradas) {
      expect(entrada.tagName).withContext(`«${entrada.textContent?.trim()}» no es un botón`).toBe('BUTTON');
    }
  });

  it('«Organización» y «Cuenta y seguridad» llevan a sus rutas propias del panel', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();

    const componente = fixture.componentInstance;
    // SIN `.and.callThrough()`: la implementación real también cierra el menú
    // (`setActiveNavDropdown(null)`), así que espiarla sin dejarla correr mantiene el panel abierto
    // para pulsar las dos entradas en el mismo ciclo, sin tener que reabrirlo entre una y otra.
    spyOn(componente, 'onNavigateToPath');

    disparador().click();
    fixture.detectChanges();

    (panelDelMenu()!.querySelector('[data-menu-organizacion]') as HTMLButtonElement).click();
    expect(componente.onNavigateToPath).toHaveBeenCalledWith('gestion/organizacion');

    (panelDelMenu()!.querySelector('[data-menu-cuenta-seguridad]') as HTMLButtonElement).click();
    expect(componente.onNavigateToPath).toHaveBeenCalledWith('gestion/cuenta-seguridad');
  });

  it('Escape cierra el menú', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();
    disparador().click();
    fixture.detectChanges();
    expect(panelDelMenu()).toBeTruthy();

    fixture.componentInstance.onEscape();
    fixture.detectChanges();

    expect(panelDelMenu()).withContext('Escape no cerró el menú').toBeNull();
  });

  it('el segundo clic en el chip vuelve a cerrarlo', () => {
    sesion.establecer(SESION);
    fixture.detectChanges();

    disparador().click();
    fixture.detectChanges();
    expect(panelDelMenu()).toBeTruthy();

    disparador().click();
    fixture.detectChanges();
    expect(panelDelMenu()).toBeNull();
  });

  it('sin sesión no hay menú de cuenta que abrir', () => {
    fixture.detectChanges();

    expect(raizDe().querySelector('[data-menu-de-cuenta]')).toBeNull();
  });
});
