import { Routes, UrlMatchResult, UrlSegment } from '@angular/router';
import { sesionExternaGuard } from './core/guards/sesion-externa.guard';
import { PanelOrganizacionApi } from './features/panel-organizacion/panel-organizacion.api';
import { PanelOrganizacionService } from './features/panel-organizacion/panel-organizacion.service';
import { PanelOrganizacionStore } from './features/panel-organizacion/panel-organizacion.store';

/**
 * Empareja `/administracion` y `/administracion/<seccion>` con una unica entrada de ruta.
 *
 * Devuelve la seccion como parametro `seccion`, igual que hacia `administracion/:seccion`, para
 * que nada de lo que lee `paramMap` tenga que cambiar.
 */
export function emparejaLaConsolaAdministrativa(segmentos: UrlSegment[]): UrlMatchResult | null {
  if (segmentos.length === 0 || segmentos[0].path !== 'administracion') { return null; }
  if (segmentos.length === 1) { return { consumed: segmentos }; }
  if (segmentos.length === 2) { return { consumed: segmentos, posParams: { seccion: segmentos[1] } }; }
  return null;
}

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent),
  },
  {
    path: 'pnmc',
    loadComponent: () => import('./features/pnmc/pages/sobre-el-pnmc-page/sobre-el-pnmc-page.component').then(m => m.SobreElPnmcPageComponent),
  },
  {
    path: 'ejes',
    loadComponent: () => import('./features/content/pages/ejes-page/ejes-page.component').then(m => m.EjesPageComponent),
  },
  {
    path: 'ejes/:componentSlug',
    loadComponent: () => import('./features/content/pages/component-detail-page/component-detail-page.component').then(m => m.ComponentDetailPageComponent),
  },
  {
    path: 'noticias',
    loadComponent: () => import('./features/news/pages/noticias-page/noticias-page.component').then(m => m.NoticiasPageComponent),
  },
  {
    // LA DIRECCION Y NO EL IDENTIFICADOR: la calcula el servidor del título al crear la noticia y
    // después no cambia, así que un enlace compartido sigue funcionando aunque se corrija una tilde.
    path: 'noticias/:slug',
    loadComponent: () => import('./features/news/pages/noticia-detalle-page/noticia-detalle-page.component').then(m => m.NoticiaDetallePageComponent),
  },
  {
    path: 'agenda',
    loadComponent: () => import('./features/agenda/pages/agenda-page/agenda-page.component').then(m => m.AgendaPageComponent),
  },
  {
    path: 'editorial',
    loadComponent: () => import('./features/editorial/pages/editorial-page/editorial-page.component').then(m => m.EditorialPageComponent),
    /*
      LA FICHA ES UNA RUTA HIJA, Y ESO NO ES UN DETALLE DE ESTILO.
      ==========================================================
      CADA OBRA TIENE SU DIRECCION: la signatura es como se cita una publicación, así que citarla
      tiene que llevar a su ficha, `/editorial/PNMC-ED-019`.

      PERO DECLARARLA COMO UNA SEGUNDA RUTA HERMANA LA CONVERTIA EN OTRA PANTALLA. Angular reutiliza
      el componente cuando se navega DENTRO de la misma definición de ruta, y lo DESTRUYE Y VUELVE A
      CREAR cuando se pasa de una a otra. Medido con el catálogo real:
      abrir una ficha volvía a pedir el acervo entero al API y a descargar VEINTIUNA portadas, y lo
      mismo al abrir la siguiente. En pantalla eso se ve como está descrito así: «cada vez que abro algún elemento se queda como sincronizando archivos y se recarga
      completamente».

      COMO HIJA SIN COMPONENTE, la página no se desmonta: solo cambia el parámetro. La dirección es
      la misma y el catálogo se queda donde estaba, con su desplazamiento y sus filtros.
    */
    children: [
      { path: ':codigo', children: [] },
    ],
  },

  {
    path: 'galeria',
    loadComponent: () => import('./features/gallery/pages/galeria-page/galeria-page.component').then(m => m.GaleriaPageComponent),
  },
  {
    // LA SUBPAGINA VA ANTES QUE LA DEL MAPA: Angular resuelve por orden y `mapa-ecosistemico`
    // coincidiría primero si estuviera arriba, dejando esta ruta inalcanzable.
    path: 'mapa-ecosistemico/sobre-el-mapa',
    loadComponent: () => import('./features/map/pages/sobre-el-mapa-page/sobre-el-mapa-page.component').then(m => m.SobreElMapaPageComponent),
  },
  {
    path: 'mapa-ecosistemico',
    loadComponent: () => import('./features/map/pages/mapa-ecosistemico-page/mapa-ecosistemico-page.component').then(m => m.MapaEcosistemicoPageComponent),
  },
  {
    path: 'ecosistema/festivales/:festivalId',
    loadComponent: () => import('./features/ecosistema/pages/festival-publico-detalle-page/festival-publico-detalle-page.component').then(m => m.FestivalPublicoDetallePageComponent),
  },
  {
    path: 'ecosistema/festivales',
    loadComponent: () => import('./features/ecosistema/pages/festivales-publicos-page/festivales-publicos-page.component').then(m => m.FestivalesPublicosPageComponent),
  },
  // MERCADOS MUSICALES, al mismo nivel que Festivales: son dos procesos del Ecosistema y la ruta
  // lo dice. `ecosistema/mercados-musicales` y `ecosistema/festivales` son hermanas.
  // EL PROCESO SE LLAMA «MERCADOS MUSICALES» Y LA RUTA TAMBIEN.
  //
  // Se registró como `ecosistema/mercados` y el resto del proyecto ya
  // había fijado el nombre completo: el menú del portal enlazaba `ecosistema/mercados-musicales`
  // —que no existía, así que caía en «no encontrado»— y el propio catálogo de categorías lo
  // declaraba así en su comentario, junto a `practicas-musicales` y `territorios-sonoros`, que son
  // sus hermanas. Lo reportó la dirección de producto entrando por esa dirección.
  //
  // La forma corta se conserva como REDIRECCION porque se publicó: los enlaces de la ficha de un
  // festival y los que alguien haya copiado siguen llegando.
  {
    path: 'ecosistema/mercados-musicales/:mercadoId',
    loadComponent: () => import('./features/ecosistema/pages/mercado-publico-detalle-page/mercado-publico-detalle-page.component').then(m => m.MercadoPublicoDetallePageComponent),
  },
  { path: 'ecosistema/mercados-musicales', loadComponent: () => import('./features/ecosistema/pages/mercados-publicos-page/mercados-publicos-page.component').then(m => m.MercadosPublicosPageComponent) },
  { path: 'ecosistema/mercados/:mercadoId', redirectTo: 'ecosistema/mercados-musicales/:mercadoId' },
  { path: 'ecosistema/mercados', pathMatch: 'full', redirectTo: 'ecosistema/mercados-musicales' },
  { path: 'ecosistema/territorios-sonoros', loadComponent: () => import('./features/ecosistema/pages/territorios-sonoros-page/territorios-sonoros-page.component').then(m => m.TerritoriosSonorosPageComponent) },
  { path: 'ecosistema/territorios-sonoros/:slug', loadComponent: () => import('./features/ecosistema/pages/territorio-sonoro-detalle-page/territorio-sonoro-detalle-page.component').then(m => m.TerritorioSonoroDetallePageComponent) },
  { path: 'ecosistema/practicas-musicales', loadComponent: () => import('./features/ecosistema/pages/practicas-musicales-page/practicas-musicales-page.component').then(m => m.PracticasMusicalesPageComponent) },
  { path: 'ecosistema/practicas-musicales/:slug', loadComponent: () => import('./features/ecosistema/pages/practica-musical-detalle-page/practica-musical-detalle-page.component').then(m => m.PracticaMusicalDetallePageComponent) },
  {
    path: 'registro',
    loadComponent: () => import('./features/external-access/external-access-page.component').then(m => m.ExternalAccessPageComponent),
    // Ruta pública propia para crear el acceso de una organización.
    data: { modoAcceso: 'registro' },
  },
  {
    path: 'ingresar',
    loadComponent: () => import('./features/external-access/external-access-page.component').then(m => m.ExternalAccessPageComponent),
    data: { modoAcceso: 'ingresar' },
  },
  // Gestión es el espacio privado de las organizaciones y tiene secciones enlazables.
  {
    path: 'gestion',
    canActivate: [sesionExternaGuard],
    loadComponent: () => import('./features/panel-organizacion/panel-organizacion-page.component').then(m => m.PanelOrganizacionPageComponent),
    // EL PANEL SE CONSTRUYO CONTRA UNA CLASE ABSTRACTA para poder probarse sin red, y sin
    // este `provide` la ruta revienta con NullInjectorError. Es tambien lo que faltaba el
    // 27 de agosto: el componente estaba escrito y la ruta seguia cargando la pantalla
    // vieja, asi que en pantalla no cambiaba nada y nada fallaba.
    providers: [
      { provide: PanelOrganizacionApi, useExisting: PanelOrganizacionService },
      PanelOrganizacionStore,
    ],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'resumen' },
      {
        path: 'resumen',
        loadComponent: () => import('./features/panel-organizacion/panel-resumen.component').then(m => m.PanelResumenComponent),
      },
      {
        path: 'organizacion',
        loadComponent: () => import('./features/panel-organizacion/seccion-organizacion.component').then(m => m.SeccionOrganizacionComponent),
      },
      {
        path: 'responsable',
        loadComponent: () => import('./features/panel-organizacion/seccion-responsable.component').then(m => m.SeccionResponsableComponent),
      },
      {
        // MIS ARCHIVOS. El mensaje de cuota pedía «retira material que ya no uses» y no había
        // ninguna forma de retirarlo ni de ver qué ocupaba el espacio.
        path: 'archivos',
        loadComponent: () => import('./features/panel-organizacion/seccion-archivos/seccion-archivos.component').then(m => m.SeccionArchivosComponent),
      },
      {
        path: 'procesos/festivales',
        children: [
          { path: '', loadComponent: () => import('./features/panel-organizacion/seccion-ecosistema.component').then(m => m.SeccionEcosistemaComponent) },
          { path: 'nuevo', loadComponent: () => import('./features/panel-organizacion/asistente-de-festival.component').then(m => m.AsistenteDeFestivalComponent) },
          { path: ':id', loadComponent: () => import('./features/panel-organizacion/ficha-de-festival-page.component').then(m => m.FichaDeFestivalPageComponent) },
        ],
      },
      {
        // MERCADOS CUELGA DE `procesos`, COMO FESTIVALES. Son dos procesos del Ecosistema al mismo
        // nivel, y la ruta lo dice: `procesos/mercados` y `procesos/festivales` son hermanas.
        path: 'procesos/mercados',
        children: [
          // LA LISTA VA PRIMERO Y FALTABA. `procesos/festivales` tiene su ruta vacía desde el
          // principio; mercados no la tenía, así que la dirección del proceso —y la miga que
          // vuelve a ella desde la ficha— no llevaban a ningún sitio.
          { path: '', loadComponent: () => import('./features/panel-organizacion/seccion-mercados.component').then(m => m.SeccionMercadosComponent) },
          { path: 'nuevo', loadComponent: () => import('./features/panel-organizacion/asistente-de-mercado.component').then(m => m.AsistenteDeMercadoComponent) },
          { path: ':id', loadComponent: () => import('./features/panel-organizacion/ficha-de-mercado-page.component').then(m => m.FichaDeMercadoPageComponent) },
        ],
      },
      {
        path: 'procesos',
        loadComponent: () => import('./features/panel-organizacion/procesos-gestion-page.component').then(m => m.ProcesosGestionPageComponent),
      },
      {
        // EVENTOS DE LOS PROCESOS. Una organización no publica eventos sueltos en la agenda del
        // Programa: publica eventos de sus procesos, y si no tiene ninguno publicado, la pantalla
        // no ofrece el formulario y lo explica.
        path: 'eventos',
        loadComponent: () => import('./features/panel-organizacion/panel-eventos.component').then(m => m.PanelEventosComponent),
      },
      {
        path: 'solicitudes',
        loadComponent: () => import('./features/panel-organizacion/seccion-solicitudes.component').then(m => m.SeccionSolicitudesComponent),
      },
      {
        path: 'cuenta-seguridad',
        loadComponent: () => import('./features/panel-organizacion/panel-cuenta-seguridad.component').then(m => m.PanelCuentaSeguridadComponent),
      },
    ],
  },
  {
    // ANONIMA PORQUE LA LEY LO OBLIGA. El art. 12 de la Ley 1581 de 2012 exige informar al titular
    // de las finalidades y de sus derechos ANTES de pedirle la autorización, y quien va a
    // registrarse todavía no tiene cuenta.
    path: 'politicas/:clave',
    loadComponent: () => import('./features/politicas/politica-page.component').then(m => m.PoliticaPageComponent),
    title: 'Políticas de datos · SIMUS',
  },
  {
    path: 'ecosistema',
    loadComponent: () => import('./features/ecosistema/pages/ecosistema-home-page/ecosistema-home-page.component').then(m => m.EcosistemaHomePageComponent),
  },
  {
    // ANONIMA A PROPOSITO: el enlace se abre desde el buzón, donde por definición no hay sesión.
    path: 'ecosistema/confirmar-correo',
    loadComponent: () => import('./features/ecosistema/pages/confirmar-correo-page/confirmar-correo-page.component').then(m => m.ConfirmarCorreoPageComponent),
  },
  {
    path: 'estrategia/circulacion',
    loadComponent: () => import('./features/content/pages/strategy-page/strategy-page.component').then(m => m.StrategyPageComponent),
    data: { strategy: 'circulacion' },
  },
  {
    path: 'estrategia/investigacion',
    loadComponent: () => import('./features/content/pages/strategy-page/strategy-page.component').then(m => m.StrategyPageComponent),
    data: { strategy: 'investigacion' },
  },
  {
    // UNA SOLA ENTRADA DE RUTA PARA LA CONSOLA, Y ESO ES LO QUE ARREGLA. Antes habia dos
    // —`administracion` y `administracion/:seccion`— apuntando al mismo componente, y el
    // enrutador de Angular reutiliza la pantalla solo cuando la entrada de ruta es LA MISMA
    // (`shouldReuseRoute` compara `routeConfig` por identidad). Al entrar, `handleLogin` navega de
    // la primera a la segunda: el armazon se destruia y se volvia a construir con la sesion recien
    // abierta, lo que cancelaba la consulta de notificaciones en vuelo y recargaba la consola
    // entera. Comprobado: `ngOnDestroy` y `constructor` en el mismo
    // milisegundo, y un `net::ERR_ABORTED` sobre `/api/v1/notificaciones`.
    //
    // Se resuelve con un emparejador y no con una redireccion de `administracion` a
    // `administracion/resumen`, porque esa redireccion dejaria el formulario de acceso pintado
    // bajo una direccion que anuncia una seccion —el mismo defecto que se corrigio en el panel
    // externo, documentado en `sesion-externa.guard.spec.ts`—.
    matcher: emparejaLaConsolaAdministrativa,
    loadComponent: () => import('./features/admin/admin-shell-page/admin-shell-page.component').then(m => m.AdminShellPageComponent),
  },
  // `/colaboradores` SE RETIRO. Cargaba el MISMO armazon que
  // `/admin` en modo externo, es decir: era una segunda puerta de entrada para gente de
  // fuera, en paralelo a `/ecosistema/ingresar`. Dos puertas al mismo sitio se separan sin
  // que nadie lo note, y el error de acceso que sigue se lee como «mi contrasena no sirve».
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found-page.component').then(m => m.NotFoundPageComponent),
  },
];
