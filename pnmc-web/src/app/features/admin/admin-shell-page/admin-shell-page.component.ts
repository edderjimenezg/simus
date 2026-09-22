import { Component, OnInit, OnDestroy, inject, signal, computed, effect, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminPrimerIngresoComponent } from '../admin-primer-ingreso/admin-primer-ingreso.component';
import { CajonDeConsultaGuiadaComponent } from '../../../shared/components/consulta-guiada/cajon-de-consulta-guiada.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { FECHA_ADMINISTRATIVA, FECHA_Y_HORA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';
import { ContextoDeConsulta } from '../../../shared/components/consulta-guiada/consulta-guiada.service';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  LucideBell,
  LucideShieldCheck,
  LucideLayoutDashboard,
  LucideInbox,
  LucideSparkles,
  LucideMusic,
  LucideBuilding2,
  LucideStore,
  LucideBookOpen,
  LucideCalendar,
  LucideNewspaper,
  LucideTags,
  LucideGlobe,
  LucideLogOut,
  LucideMail,
  LucideBarChart3,
  LucideFiles,
  LucideFileText,
  LucideUserCog,
  LucideUsers,
  LucideShield,
  LucideExternalLink,
  LucideSearch,
  LucideMenu,
  LucideX,
  LucideChevronDown,
  LucideChevronRight
} from '@lucide/angular';
import { AdminService, MonitorDelSistema, UsuarioAdministrativo } from '../../../core/services/admin.service';
import { SessionService } from '../../../core/services/session.service';
import { SesionExpiradaService } from '../../../core/http/sesion-expirada.service';
import { NavigationService } from '../../../core/services/navigation.service';
import { NotificacionEnVivo, NotificacionesEnVivoService } from '../../../core/services/notificaciones-en-vivo.service';
import { 
  ADMIN_ROLES, 
  getModulesForRoles,
  canRoles, algunoEsRolInterno, rolesDeSesion } from '../domain/admin-config';
import {
  gruposAdministrativosVisibles,
  seccionAdministrativaPorId,
  seccionAdministrativaPorRuta, grupoAdministrativoDeSeccion } from '../domain/navegacion-administrativa';

// Import child components
import { AdminLoginComponent } from '../admin-login/admin-login.component';
import { AdminRecordsPanelComponent } from '../admin-records-panel/admin-records-panel.component';
import { AdminOrganizacionesPanelComponent } from '../admin-organizaciones-panel/admin-organizaciones-panel.component';
import { AdminSolicitudesPanelComponent } from '../admin-solicitudes-panel/admin-solicitudes-panel.component';
import { BandejaDeTrabajoService } from '../admin-solicitudes-panel/bandeja-de-trabajo.service';
import { AdminUsersPanelComponent } from '../admin-users-panel/admin-users-panel.component';
import { AdminSystemPanelComponent } from '../admin-system-panel/admin-system-panel.component';
import { AdminGestionSitioPanelComponent } from '../admin-gestion-sitio-panel/admin-gestion-sitio-panel.component';
import { AdminCatalogoEditorialPanelComponent } from '../admin-catalogo-editorial-panel/admin-catalogo-editorial-panel.component';
import { AdminNoticiasPanelComponent } from '../admin-noticias-panel/admin-noticias-panel.component';
import { AdminAgendaPanelComponent } from '../admin-agenda-panel/admin-agenda-panel.component';
import { AdminMercadosPanelComponent } from '../admin-mercados-panel/admin-mercados-panel.component';
import { AdminCategoriasPanelComponent } from '../admin-categorias-panel/admin-categorias-panel.component';
import { GestionDelBancoComponent } from '../../../shared/components/banco-de-archivos/gestion-del-banco.component';
import { AdminAltaFestivalComponent } from '../admin-alta-festival/admin-alta-festival.component';
import { AdminFichaFestivalComponent } from '../admin-ficha-festival/admin-ficha-festival.component';
import { AdminFichaMercadoComponent } from '../admin-ficha-mercado/admin-ficha-mercado.component';
import { AdminBoletinPanelComponent } from '../admin-boletin-panel/admin-boletin-panel.component';
import { AdminAuditoriaPanelComponent } from '../admin-auditoria-panel/admin-auditoria-panel.component';
import { PanelAnalisisAdministrativoComponent } from '../panel-analisis-administrativo/panel-analisis-administrativo.component';
import { AdminPreparacionAsistidaPanelComponent } from '../components/admin-preparacion-asistida-panel/admin-preparacion-asistida-panel.component';
import { CabeceraDePaginaComponent } from '../../../shared/components/ui/cabecera-de-pagina/cabecera-de-pagina.component';

/** La sección de la consola que abre cada módulo que informa el monitor. */
const SECCION_DEL_MODULO: Record<string, string> = { festivals: 'festivales', mercados: 'mercados' };

@Component({
  selector: 'app-admin-shell-page',
  standalone: true,
  imports: [AdminPrimerIngresoComponent, CabeceraDePaginaComponent,
    CajonDeConsultaGuiadaComponent,
    CommonModule,
    BotonComponent,
    DialogoDirective,
    FormsModule,
    RouterLink,
    AdminLoginComponent,
    AdminRecordsPanelComponent,
    AdminOrganizacionesPanelComponent,
    AdminSolicitudesPanelComponent,
    AdminUsersPanelComponent,
    AdminSystemPanelComponent,
    AdminGestionSitioPanelComponent,
    AdminCatalogoEditorialPanelComponent,
    AdminNoticiasPanelComponent,
    AdminAgendaPanelComponent,
    AdminMercadosPanelComponent,
    AdminCategoriasPanelComponent,
    GestionDelBancoComponent,
    AdminAltaFestivalComponent,
    AdminFichaFestivalComponent,
    AdminFichaMercadoComponent,
    AdminBoletinPanelComponent,
    AdminAuditoriaPanelComponent,
    PanelAnalisisAdministrativoComponent,
    AdminPreparacionAsistidaPanelComponent,
    LucideShieldCheck,
    LucideBell,
    LucideLayoutDashboard,
    LucideInbox,
    LucideSparkles,
    LucideMusic,
    LucideBuilding2,
    LucideStore,
    LucideBookOpen,
    LucideCalendar,
    LucideNewspaper,
    LucideTags,
    LucideGlobe,
    LucideLogOut,
    LucideMail,
    LucideBarChart3,
    LucideFiles,
    LucideFileText,
    LucideUserCog,
    LucideUsers,
    LucideShield,
    LucideExternalLink,
    LucideSearch,
      LucideMenu,
    LucideX,
    LucideChevronDown,
    LucideChevronRight
  ],
  templateUrl: './admin-shell-page.component.html',
  styleUrls: [
    './admin-shell-header.component.css',
    './admin-shell-layout.component.css',
    // El Resumen tiene hoja propia: el fichero de maqueta rozaba su presupuesto y, sobre todo,
    // está minificado a mano. Lo nuevo se escribe legible y aparte.
    './admin-shell-resumen.component.css',
    './admin-shell-typography.component.css',
    './admin-shell-notifications.component.css',
  ]
})
export class AdminShellPageComponent implements OnInit, OnDestroy {
  private adminService = inject(AdminService);
  private sessionService = inject(SessionService);
  private navigationService = inject(NavigationService);
  private router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly notificacionesEnVivo = inject(NotificacionesEnVivoService);
  /**
   * LA BANDEJA VIVE EN SU PROPIO PANEL desde —`app-admin-solicitudes-panel`—
   * y su cola, aquí. El armazón la necesita para tres cosas: la cifra de la barra izquierda, el
   * desglose del Resumen operativo, y llevar a alguien a un asunto concreto.
   */
  readonly bandeja = inject(BandejaDeTrabajoService);

  // Shell Session Signals
  session = this.sessionService.currentUser;
  isAuthenticated = this.sessionService.isAuthenticated;
  sessionState = signal<string>('checking'); // 'checking' | 'ready'

  // Panel State
  activeSection = signal<string>('monitor');

  /**
   * Cajon de navegacion en pantallas pequenas.
   *
   * La barra lateral era `w-60 shrink-0` sin ninguna variante responsive: 240 px
   * fijos en cualquier pantalla. Medido en un movil de 360 px, dejaba **120 px
   * de ancho util** para los diez paneles — el titulo «Gestion de usuarios» se
   * partia en tres lineas y las tablas quedaban cortadas. Por debajo de `lg` la
   * barra pasa a ser un cajon deslizante y esta senal decide si esta abierto.
   */
  sidebarAbierto = signal<boolean>(false);

  alternarSidebar(abierto: boolean): void {
    this.sidebarAbierto.set(abierto);
  }

  /** Navegar cierra el cajon: en movil taparia el panel recien elegido. */
  irASeccion(id: string): void {
    // Solicitudes deja de ser una pantalla separada: desde esta base pertenece
    // al trabajo institucional junto con revisiones y decisiones.
    if (id === 'governance') id = 'solicitudes';
    this.activeSection.set(id);
    // La primera entrega abre Registros directamente en Festivales: es el tipo
    // prioritario y evita heredar la última pestaña de un módulo distinto.
    if (id === 'ecosistema') this.selectedModuleId.set('festivals');
    this.sidebarAbierto.set(false);
    const seccion = seccionAdministrativaPorId(id);
    if (seccion && this.router.url.startsWith('/administracion')) {
      void this.router.navigateByUrl(`/administracion/${seccion.ruta}`);
    }
  }

  /**
   * Aviso de sesion caducada.
   *
   * Lo levanta `sesionExpiradaInterceptor` al recibir un 401 en una ruta con
   * sesion. Antes esto no existia: el 401 solo escribia «Error de conexion» en
   * un texto de 9,3 px de la barra lateral, `isAuthenticated()` seguia en
   * `true`, y la persona seguia dentro pulsando botones que fallaban.
   */
  readonly sesionExpirada = inject(SesionExpiradaService);
  avisoDeCaducidad = signal<string | null>(null);

  private readonly vigilarCaducidad = effect(() => {
    if (!this.sesionExpirada.expirada()) return;

    // Cerrar de verdad: sin esto la consola sigue pintada sobre una sesion que
    // el servidor ya no reconoce.
    this.sessionService.setSession(null);
    this.sidebarAbierto.set(false);
    this.avisoDeCaducidad.set(
      'Tu sesión caducó mientras trabajabas. Vuelve a entrar para continuar. ' +
        'Los cambios que no hubieras guardado se han perdido.',
    );
    this.sesionExpirada.limpiar();
  });
  selectedModuleId = signal<string>('festivals');
  stats = signal<Record<string, number>>({});
  schemaOnline = signal<boolean>(false);
  apiStatus = signal<string>('Conexión con el servidor sin verificar todavía.');
  divipola = signal<any>({});
  monitor = signal<MonitorDelSistema | null>(null);
  adminNotifications = signal<NotificacionEnVivo[]>([]);
  adminNotificationsOpen = signal(false);
  organizationsTotal = signal<number | null>(null);
  administrativeActivity = signal<any[]>([]);

  readonly unreadNotifications = computed(() => this.adminNotifications().filter(item => !item.readAt).length);
  readonly readNotifications = computed(() => this.adminNotifications().filter(item => !!item.readAt).length);

  detalleDeRevisionDeFestival(notification: NotificacionEnVivo): { organizacion: string; festival: string } | null {
    if (!['FestivalEnviadoARevision', 'FestivalRecibidoParaRevision'].includes(String(notification?.eventType ?? ''))) {
      return null;
    }

    try {
      const metadata = JSON.parse(String(notification?.metadataJson ?? ''));
      const organizacion = String(metadata?.OrganizacionNombre ?? '').trim();
      const festival = String(metadata?.FestivalNombre ?? '').trim();
      if (organizacion && festival) return { organizacion, festival };
    } catch { /* Los avisos antiguos se resuelven desde su texto. */ }

    const coincidencia = String(notification?.body ?? '').match(
      /^La organización [“"](.+?)[”"] envió a revisión para publicación el Festival [“"](.+?)[”"]\.$/,
    );
    return coincidencia ? { organizacion: coincidencia[1], festival: coincidencia[2] } : null;
  }

  /**
   * Lo que espera una decisión, en una sola lista.
   *
   * POR QUE ESTO SUSTITUYE A DOS BLOQUES. El Resumen enseñaba cuatro indicadores arriba y, justo
   * debajo, una lista que repetía TRES DE ESOS CUATRO NÚMEROS —«1 registro pendiente» y «Revisión
   * de registros: 1 pendientes»—; los seis controles llevaban además al mismo sitio. Un número
   * repetido no informa dos veces: hace dudar de si son dos cosas distintas.
   *
   * LAS FILAS EN CERO NO SON BOTONES, y es deliberado. Llevar a alguien a una bandeja vacía es
   * prometerle trabajo que no existe. Se quedan visibles —saber que no hay nada pendiente ES
   * información— pero atenuadas y sin acción.
   */
  readonly pendientesDeDecision = computed(() => [
    { id: 'revision', etiqueta: 'Revisión de registros y propuestas', total: this.bandeja.revisiones().length },
    { id: 'solicitudes', etiqueta: 'Solicitudes, reclamaciones y vinculaciones', total: this.bandeja.solicitudesActivas().length },
    { id: 'retiros', etiqueta: 'Solicitudes de retiro o eliminación', total: this.bandeja.retiros().length },
    // Los hallazgos del sistema entraron a la bandeja: si el Resumen
    // no los cuenta, dice que no hay nada que decidir mientras la bandeja tiene filas esperando.
    { id: 'hallazgos', etiqueta: 'Posibles duplicados y alertas de calidad', total: this.bandeja.colaCompleta().filter(f => f.kind === 'duplicado' || f.kind === 'alerta').length },
  ]);

  /** Cuántas decisiones esperan en total. Cero significa que no hay nada que hacer, no un fallo. */
  readonly totalPendienteDeDecision = computed(() =>
    this.pendientesDeDecision().reduce((suma, fila) => suma + fila.total, 0));

  /**
   * Lo que de verdad espera una decisión.
   *
   * <b>POR QUE SE SEPARA.</b> El bloque prioritario pintaba una fila completa por cada tipo de
   * trámite, tuviera pendientes o no. Con nueve revisiones y cero solicitudes y cero retiros, la
   * pantalla de entrada dedicaba <b>dos tercios de su bloque más importante a decir que no hay nada
   * que hacer</b>, con el mismo peso visual que lo que sí lo requería. Aquí solo entra lo que pide
   * acción; lo demás se resume en una línea.
   */
  readonly esperandoDecision = computed(() => this.pendientesDeDecision().filter(f => f.total > 0));

  /**
   * Lo que está al día, dicho en una línea y no en tres filas.
   *
   * SE SIGUE DICIENDO, y a propósito: callar los tipos que están en cero dejaría la duda de si no
   * hay pendientes o si esa bandeja no existe. Pero ocupa un renglón, no un bloque.
   */
  /** Los dos formatos acordados, expuestos para la plantilla. Ver `domain/formatos-de-fecha.ts`. */
  protected readonly FECHA_ADMINISTRATIVA = FECHA_ADMINISTRATIVA;
  protected readonly FECHA_Y_HORA_ADMINISTRATIVA = FECHA_Y_HORA_ADMINISTRATIVA;

  readonly alDia = computed(() => this.pendientesDeDecision().filter(f => f.total === 0));

  /**
   * De qué son las decisiones pendientes.
   *
   * <b>«NUEVE» NO ES UN DATO UTIL POR SI SOLO.</b> El resumen decía «9 · Revisión de registros y
   * propuestas» y ahí terminaba: para saber si eran nueve festivales nuevos o nueve eliminaciones
   * —que no se atienden igual ni corren la misma prisa— había que abrir la bandeja. La cola de
   * trabajo ya traía el tipo de cada elemento y nadie lo estaba usando en esta pantalla.
   */
  readonly desgloseDeDecisiones = computed(() => {
    // SOBRE LA COLA ENTERA Y NO SOBRE LA FILTRADA. El Resumen dice qué hay pendiente en toda la
    // consola; leer la cola ya filtrada del panel lo ataba al filtro que la BANDEJA tuviera puesto, en otra
    // sección. Se destapó al añadir el atajo «Posibles duplicados», que deja la bandeja filtrada:
    // al volver al Resumen, el desglose decía que lo único pendiente eran duplicados.
    const cuenta = new Map<string, number>();
    for (const item of this.bandeja.colaCompleta()) {
      const tipo = item.type || 'Sin clasificar';
      cuenta.set(tipo, (cuenta.get(tipo) ?? 0) + 1);
    }
    const total = this.bandeja.colaCompleta().length || 1;
    return [...cuenta.entries()]
      .map(([tipo, cuantas]) => ({
        tipo,
        total: cuantas,
        // LA PROPORCION, PARA DIBUJARLA. Una lista de números dice cuántos hay de cada tipo; una
        // barra dice de un vistazo cuál domina, que es lo que se pregunta al abrir la consola.
        parte: Math.round((cuantas / total) * 100),
      }))
      .sort((a, b) => b.total - a.total);
  });

  /**
   * De qué está hecho el acervo: cuántos procesos hay en cada estado.
   *
   * <b>ES LA PREGUNTA QUE UN RESUMEN TIENE QUE RESPONDER.</b> «186 festivales» no dice si el trabajo
   * está hecho: ciento sesenta publicados con dieciséis en borrador es una situación sana, y ciento
   * ochenta y seis en borrador sería una parálisis. La franja enseñaba solo el total.
   *
   * EL DATO YA ESTABA CARGADO Y SIN USAR. El monitor devuelve la composición por estado de cada
   * módulo —`modules[].statuses`— y esta pantalla solo leía el total. No hace falta pedir nada nuevo.
   *
   * SE ORDENA POR TAMAÑO y se calcula la parte de cada estado sobre el total, para poder dibujarlo
   * como una sola barra apilada: lo que se compara es la proporción entre estados, no cifras sueltas.
   */
  readonly composicionDelAcervo = computed(() => {
    // UN MODULO POR BARRA, Y NO SOLO EL PRIMERO. Leía `modules[0]` —Festivales— y con Mercados en el
    // monitor eso habría dejado el segundo módulo del Ecosistema fuera del Resumen. Un módulo del
    // Ecosistema se conecta en todas las superficies donde está Festivales (15 de septiembre de 2026).
    const modulos = (this.monitor()?.modules ?? []).map(modulo => {
      const estados = modulo.statuses ?? [];
      const total = estados.reduce((suma, e) => suma + (e.total ?? 0), 0);
      return {
        id: modulo.id,
        rotulo: modulo.label,
        seccion: SECCION_DEL_MODULO[modulo.id] ?? null,
        total,
        partes: total === 0 ? [] : [...estados]
          .filter(e => (e.total ?? 0) > 0)
          .sort((a, b) => b.total - a.total)
          .map(e => ({ ...e, parte: Math.round((e.total / total) * 1000) / 10 })),
      };
    });
    return { total: modulos.reduce((suma, modulo) => suma + modulo.total, 0), modulos };
  });

  /**
   * Qué se ha estado haciendo, por tipo de acción.
   *
   * La bitácora ya viaja a esta pantalla y solo se usaba como lista. Agrupada por acción dice en qué
   * se va el trabajo administrativo —revisar, publicar, entrar— que es una lectura distinta de
   * «quién hizo qué a las 01:40».
   */
  readonly actividadPorTipo = computed(() => {
    const cuenta = new Map<string, number>();
    for (const item of this.administrativeActivity()) {
      const accion: string = item?.accionEtiqueta ?? item?.accion ?? 'Otra actividad';
      cuenta.set(accion, (cuenta.get(accion) ?? 0) + 1);
    }
    const total = [...cuenta.values()].reduce((a, b) => a + b, 0);
    if (total === 0) { return { total: 0, partes: [] as { accion: string; total: number; parte: number }[] }; }
    return {
      total,
      partes: [...cuenta.entries()]
        .map(([accion, cuantas]) => ({ accion, total: cuantas, parte: Math.round((cuantas / total) * 100) }))
        .sort((a, b) => b.total - a.total)
        .slice(0, 5),
    };
  });

  /**
   * La barra apilada, dicha con palabras.
   *
   * Una proporción dibujada no es legible para todo el mundo, y aquí transporta lo que de verdad
   * responde la pantalla: qué parte de lo registrado está publicada y qué parte sigue atascada.
   */
  readonly descripcionDeLaComposicion = computed(() =>
    this.composicionDelAcervo().modulos
      .map(modulo => `${modulo.rotulo}: ` + modulo.partes.map(parte => `${parte.label} ${parte.total} (${parte.parte} %)`).join(', '))
      .join('; '));

  /**
   * El estado técnico, pieza a pieza y listo para dibujar.
   *
   * <b>«DISPONIBLE» NO ES UN ESTADO, ES UN RESUMEN DE TRES.</b> El monitor comprueba por separado la
   * API, la base de datos y el frontend, y devuelve además la latencia de la llamada. La consola
   * reducía todo eso a una palabra, así que un sistema en pie pero lento y uno en pie y ágil se veían
   * igual, y una base de datos caída con la API respondiendo no se distinguía de nada.
   *
   * LA LATENCIA SE CLASIFICA CONTRA UN UMBRAL EXPLICITO. Un número de milisegundos no dice si está
   * bien: hacen falta tramos. Por debajo de 150 ms una consola se siente inmediata; hasta 600 se
   * nota pero se trabaja; por encima, estorba.
   */
  readonly estadoTecnico = computed(() => {
    const m = this.monitor();
    if (!m) { return null; }

    const latencia: number | null = m.api?.latencyMs ?? null;
    const tramoDeLatencia =
      latencia === null ? 'desconocida'
      : latencia < 150 ? 'agil'
      : latencia < 600 ? 'aceptable'
      : 'lenta';

    // La parte de la barra se calcula contra un techo de 600 ms, que es donde deja de ser cómodo.
    const parteDeLatencia = latencia === null ? 0 : Math.min(100, Math.round((latencia / 600) * 100));

    const piezas = [
      { id: 'api', rotulo: 'API', bien: m.api?.status === 'ok', detalle: m.api?.status ?? 'sin dato' },
      {
        id: 'base',
        rotulo: 'Base de datos',
        bien: m.database?.status === 'ok' && m.database?.canConnect === true,
        detalle: m.database?.canConnect ? 'Conectada' : 'Sin conexión',
      },
      {
        id: 'web',
        rotulo: 'Frontend',
        bien: m.web?.status === 'client-loaded',
        // Lo dice el propio monitor: el frontend se verifica desde el navegador, no desde la API.
        detalle: m.web?.status === 'client-loaded' ? 'Cargado en el navegador' : (m.web?.status ?? 'sin dato'),
      },
    ];

    return {
      piezas,
      enPie: piezas.filter(p => p.bien).length,
      total: piezas.length,
      latencia, tramoDeLatencia, parteDeLatencia,
      entorno: m.environment ?? null,
      comprobadoEn: m.checkedAt ?? null,
    };
  });

  // ─────────────────────────── Gestión de la cuenta ───────────────────────────

  /**
   * El menú de la persona que está dentro.
   *
   * <b>ERAN TRES ELEMENTOS SUELTOS EN LA CABECERA</b> —iniciales, nombre y una salida siempre
   * visible—, y no había ningún sitio donde gestionar la propia cuenta. Ahora es un solo control:
   * quién soy, y debajo lo que puedo hacer conmigo mismo.
   */
  readonly menuDeCuentaAbierto = signal(false);

  /** El formulario de perfil está abierto. */
  readonly editandoPerfil = signal(false);
  readonly guardandoPerfil = signal(false);
  readonly errorDePerfil = signal<string | null>(null);

  /**
   * Lo que se está editando del propio perfil.
   *
   * <b>EL ENDPOINT Y EL SERVICIO YA EXISTIAN Y NADIE LOS LLAMABA.</b> `PUT /admin/auth/profile`
   * acepta nombre, correo, teléfono y contraseña, y `AdminService.actualizarPerfil` lo envuelve desde
   * hace tiempo: no había pantalla. Es el mismo defecto de «escrito y no enganchado» que ya apareció
   * con los iconos de la navegación.
   */
  /**
   * Lo que se está editando del propio perfil.
   *
   * <b>NO HAY CAMPO DE TELEFONO, Y ES DELIBERADO.</b> El endpoint lo acepta, pero la sesión que
   * tiene esta pantalla solo trae identificador, nombre, correo y rol: no hay forma de precargar el
   * teléfono guardado. Un campo vacío que se envía es un campo que BORRA lo que hubiera, y perder un
   * dato por guardar el nombre es el peor final posible para un formulario de perfil. Se añadirá
   * cuando la sesión lo traiga.
   */
  readonly perfil = signal({ fullName: '', email: '', password: '' });

  /** Lo que se le dice a la persona cuando termina de guardar. */
  readonly avisoDePerfil = signal<string | null>(null);

  abrirMenuDeCuenta(): void { this.menuDeCuentaAbierto.update(v => !v); }
  cerrarMenuDeCuenta(): void { this.menuDeCuentaAbierto.set(false); }

  abrirPerfil(): void {
    const s = this.session();
    this.perfil.set({
      fullName: s?.fullName ?? '',
      email: s?.email ?? '',
      // LA CONTRASEÑA SIEMPRE ARRANCA VACIA: no se precarga ni se enseña la que hay, y si se deja en
      // blanco no se envía. Cambiarla es una decisión explícita, no un efecto de guardar el nombre.
      password: '',
    });
    this.errorDePerfil.set(null);
    this.avisoDePerfil.set(null);
    this.menuDeCuentaAbierto.set(false);
    this.editandoPerfil.set(true);
  }

  cambiarPerfil(campo: 'fullName' | 'email' | 'password', valor: string): void {
    this.perfil.update(p => ({ ...p, [campo]: valor }));
  }

  guardarPerfil(): void {
    if (this.guardandoPerfil()) { return; }
    const p = this.perfil();
    if (!p.fullName.trim()) { this.errorDePerfil.set('El nombre no puede quedar vacío.'); return; }
    if (!p.email.trim()) { this.errorDePerfil.set('El correo no puede quedar vacío.'); return; }

    this.guardandoPerfil.set(true);
    this.errorDePerfil.set(null);

    // Solo viaja lo que se escribió: una contraseña en blanco NO es «ponme la contraseña vacía».
    const cuerpo: Record<string, string> = { fullName: p.fullName.trim(), email: p.email.trim() };
    if (p.password.trim().length > 0) { cuerpo['password'] = p.password; }

    this.adminService.actualizarPerfil(cuerpo).subscribe({
      next: () => {
        this.guardandoPerfil.set(false);
        this.editandoPerfil.set(false);
        // LA CABECERA TIENE QUE REFLEJARLO EN EL ACTO: si el nombre sigue siendo el viejo arriba,
        // quien acaba de cambiarlo no sabe si se guardó.
        const actual = this.session();
        if (actual) { this.sessionService.setSession({ ...actual, fullName: cuerpo['fullName'], email: cuerpo['email'] }); }
        this.avisoDePerfil.set(
          cuerpo['password'] ? 'Perfil y contraseña actualizados.' : 'Perfil actualizado.');
      },
      error: (e: { message?: string }) => {
        this.guardandoPerfil.set(false);
        this.errorDePerfil.set(e?.message ?? 'No fue posible actualizar el perfil.');
      },
    });
  }

  /** Los totales del monitor, que traían usuarios y entidades sin que nadie los leyera. */
  readonly totalesDelSistema = computed(() => this.monitor()?.totals ?? null);

  /** La latencia de la API, para que «Disponible» diga algo más que sí o no. */
  readonly latenciaApi = computed<number | null>(() => this.monitor()?.api?.latencyMs ?? null);

  /**
   * Cuánto lleva esperando lo más antiguo de la bandeja.
   *
   * ES LA PREGUNTA QUE UN RESUMEN TIENE QUE RESPONDER y que no respondía. Nueve pendientes de hoy y
   * nueve pendientes de hace tres semanas son situaciones distintas, y la pantalla de entrada las
   * enseñaba igual. Devuelve null cuando no hay nada esperando: no se inventa un cero.
   */
  readonly esperaMasLarga = computed(() => {
    const fechas = this.bandeja.colaCompleta()
      .map(item => item.createdAt)
      .filter((f): f is string => typeof f === 'string' && f.length > 0)
      .map(f => new Date(f))
      .filter(f => !Number.isNaN(f.getTime()));
    if (fechas.length === 0) { return null; }
    const masAntigua = new Date(Math.min(...fechas.map(f => f.getTime())));
    const dias = Math.floor((Date.now() - masAntigua.getTime()) / 86400000);
    return { desde: masAntigua, dias };
  });

  /**
   * La actividad reciente, con las repeticiones seguidas agrupadas.
   *
   * <b>POR QUE.</b> La bitácora registra cada entrada en sesión, y quien administra entra varias
   * veces al día. Medido en el Resumen: <b>seis filas idénticas</b>
   * —«Webmaster PNMC · Inició sesión · Usuarios y accesos»— con minutos de diferencia, ocupando
   * casi la mitad de la pantalla de entrada. Seis renglones para decir una cosa.
   *
   * <b>NO SE OCULTA NADA:</b> se agrupa lo consecutivo e idéntico y se dice cuántas veces fue y
   * entre qué horas, que es más información en menos sitio. La auditoría completa sigue estando a
   * un clic, y ahí no se agrupa nada.
   *
   * SOLO LO CONSECUTIVO, a propósito: si entre dos entradas en sesión hubo una publicación, se
   * cortan los grupos y el orden del relato se conserva.
   */
  readonly actividadAgrupada = computed(() => {
    // El tipo del elemento se infiere de la señal; no se anota para no introducir un `any` nuevo.
    const grupos: { clave: string; item: (typeof entradas)[number]; veces: number; desde: string; hasta: string }[] = [];
    const entradas = this.administrativeActivity();
    for (const item of entradas) {
      const autor: string = item?.autor?.nombre ?? '';
      const accion: string = item?.accionEtiqueta ?? item?.accion ?? '';
      const clave = autor + '|' + accion + '|' + this.sobreQue(item);
      const cuando: string = item?.fecha ?? item?.createdAt ?? '';
      const ultimo = grupos[grupos.length - 1];
      if (ultimo && ultimo.clave === clave) {
        ultimo.veces++;
        ultimo.desde = cuando;   // la lista llega de más reciente a más antigua
      } else {
        grupos.push({ clave, item, veces: 1, desde: cuando, hasta: cuando });
      }
    }
    return grupos;
  });

  /**
   * Sobre qué recayó una acción de la bitácora.
   *
   * NO SE REPITE AL AUTOR. Al entrar en sesión, el registro afectado ES la propia persona, así que
   * la línea se leía «Webmaster PNMC · Inició sesión» con «Webmaster PNMC» otra vez debajo. Cuando
   * coinciden se enseña el área —«Usuarios y accesos»—, que sí añade algo.
   */
  sobreQue(item: { nombreRegistro?: string | null; grupoEtiqueta?: string | null; autor?: { nombre?: string } | null }): string {
    const registro = (item?.nombreRegistro ?? '').trim();
    const autor = (item?.autor?.nombre ?? '').trim();
    const area = (item?.grupoEtiqueta ?? '').trim();
    if (registro && registro !== autor) { return registro; }
    return area || 'Registro institucional';
  }

  /**
   * «IMPORTAR FESTIVALES», DENTRO DEL MÓDULO Y NO EN UN ÍTEM DE NAVEGACIÓN APARTE. El usuario lo
   * pidió: «Ecosistema → Festivales → Importar Festivales». La consola
   * ya tenía dos caminos aparentes de carga masiva. El genérico se retiró porque llamaba una ruta
   * inexistente; este interruptor abre el único circuito gobernado para Festivales: prepara,
   * previsualiza y solo tras confirmación crea borradores sin tocar registros existentes.
   */
  mostrarPreparacionDeArchivo = signal(false);

  /** Si la sección de Organizaciones está enseñando la preparación de un archivo. */
  mostrarImportacionDeOrganizaciones = signal(false);

  /**
   * Ir a la bandeja YA FILTRADA por un tipo de asunto.
   *
   * La cifra «Posibles duplicados» del Resumen operativo llevaba a la sección «Calidad y
   * coincidencias», que se retiró al unificar la bandeja. Llevar a la bandeja entera sería peor
   * que antes: la cifra prometía esos duplicados y no una cola de cincuenta asuntos.
   */
  irALaBandeja(filtro: string): void {
    this.bandeja.cambiarFiltro(filtro);
    this.irASeccion('solicitudes');
  }

  openFestivalRequests(recordId?: string): void {
    this.irASeccion('solicitudes');
    if (recordId) this.bandeja.pedirTramiteDeFestival(recordId);
  }

  /** El indicador manual no se activa durante el sondeo automático. */
  refrescandoPanel = signal<boolean>(false);

  /** Avisos del panel externo cuando una sesión no pertenece al equipo institucional. */
  notifications = signal<any[]>([]);

  // Timer reference
  private tickIntervalId: any = null;
  private suscripcionNotificaciones: Subscription | null = null;
  private suscripcionRuta: Subscription | null = null;
  private notificacionesInicializadas = false;

  // Icon references
  LucideShieldCheck = LucideShieldCheck;

  /**
   * El titulo de la seccion abierta. Sale de una sola constante: antes estaba escrito dos veces
   * —diez `@if` en la barra superior y un `<h2>` en el cuerpo— y las dos copias no coincidian.
   */
  tituloDeSeccion = computed(() => seccionAdministrativaPorId(this.activeSection())?.titulo ?? 'Gestión administrativa');
  /** La familia de la sección abierta: la miga de la cabecera de página. */
  contextoDeGrupo = computed(() => grupoAdministrativoDeSeccion(this.activeSection())?.titulo ?? 'Gestión administrativa');

  /**
   * Desde dónde se está preguntando, para que la respuesta hable de lo que hay en pantalla.
   *
   * HOY SOLO VIAJA LA SECCION, y es deliberado: la consola abre las fichas dentro de la sección, no
   * en una ruta propia, así que el identificador del Festival o de la organización no está aquí. El
   * contrato ya admite los dos -`ContextoDeConsulta`- y el servidor ya sabe acotarse a ellos; lo que
   * falta es que la ficha abierta los declare, y eso pertenece al bloque de navegación.
   */
  contextoDeLaConsulta = computed<ContextoDeConsulta>(() => ({
    seccion: this.tituloDeSeccion(),
    // Y LO QUE HAY ABIERTO, cuando hay algo abierto. El contrato admitía estos dos desde el bloque
    // de la Consulta Guiada y el servidor ya sabía acotarse a ellos; lo que faltaba era que la
    // consola los declarara, porque abre sus fichas dentro de la sección y no en una ruta propia.
    festivalId: this.fichaDeFestival() ? Number(this.fichaDeFestival()) : undefined,
    organizacionId: this.organizacionPedida() ? Number(this.organizacionPedida()) : undefined,
  }));

  descripcionDeBase(): string {
    return seccionAdministrativaPorId(this.activeSection())?.descripcion
      ?? 'La sección solicitada no forma parte del espacio administrativo vigente.';
  }

  /**
   * Si HAY sesión, no CUÁL es.
   *
   * El efecto de abajo leía el objeto entero, así que cualquier cambio suyo —y al entrar cambia dos
   * veces: primero con lo que devuelve el login y después con lo que confirma `/auth/me`— volvía a
   * ejecutarlo. Cada ejecución rehacía el sondeo: `startPolling()` llama a `stopPolling()`, cancela
   * la petición de notificaciones en vuelo y abre otra. Medido
   * recorriendo la consola: dos `GET /api/v1/notificaciones` abortadas seguidas, en cada entrada.
   *
   * Y no era solo ruido: `refreshAdminBackend()` recargaba con ello los datos de toda la consola
   * por un cambio que no afectaba a ninguno.
   */
  private readonly haySesion = computed(() => this.session() !== null);

  /**
   * Si esta cuenta todavía tiene pendiente su recorrido de bienvenida.
   *
   * <b>LO DICE EL SERVIDOR EN LA PROPIA SESION</b> y no se deduce de nada: las dos marcas viajan en
   * el inicio de sesión y en la sonda, justo para que la consola sepa qué dibujar sin una segunda
   * consulta que la haría parpadear entre el formulario y el panel.
   *
   * SE COMPARA CONTRA `=== false` Y NO CON UN NEGADO: una sesión anterior a este campo llega sin él,
   * y `!undefined` mandaría a toda cuenta vieja a completar un perfil que ya tiene.
   */
  readonly faltaElPrimerIngreso = computed(() => {
    const cuenta = this.session();
    if (!cuenta) { return false; }
    return cuenta.debeCambiarContrasena === true || cuenta.perfilCompletado === false;
  });

  /**
   * El recorrido terminó: se relee la sesión para que la consola arranque con la cuenta al día.
   *
   * SE RELEE EN VEZ DE DAR POR HECHO. El nombre completo lo compuso el servidor a partir de las
   * cuatro partes, y la cabecera lo enseña: inventarlo aquí sería una segunda forma de componerlo.
   */
  primerIngresoTerminado(): void {
    this.sessionService.checkSession().subscribe({
      next: () => this.sessionState.set('ready'),
      error: () => this.sessionState.set('ready'),
    });
  }

  // Constructor
  constructor() {
    // Arrancar o parar el sondeo, solo cuando la sesión aparece o desaparece.
    //
    // `untracked` NO ES DECORATIVO. Un efecto depende de TODAS las señales que se lean mientras
    // corre, incluidas las que lee lo que llama: `refreshAdminBackend()` recorre media consola, y
    // con ello el efecto quedaba suscrito a señales que él mismo acababa provocando que cambiaran.
    // Resultado medido, con la sesión ya abierta y sin que nadie
    // tocara nada: el efecto se ejecutaba una segunda vez, `startPolling()` volvía a empezar y
    // cancelaba la consulta de notificaciones en vuelo —el segundo `net::ERR_ABORTED`—.
    // Lo único que debe despertar a este efecto es que la sesión aparezca o desaparezca.
    effect(() => {
      const hayQueSondear = this.haySesion();
      untracked(() => {
        if (hayQueSondear) {
          this.refreshAdminBackend();
          this.startPolling();
          // Y QUE PUEDE ABRIR ESTA CUENTA, con lo que se dibuja la barra izquierda. Va aquí y no
          // en `ngOnInit` porque el armazón se monta en la misma ruta que la pantalla de acceso:
          // allí no hay cookie todavía, el servidor respondía 401 y la barra salía SIN FILTRAR.
          // La sesión «apareciendo» es exactamente el momento en que se puede preguntar.
          this.pedirMisModulos();
        } else {
          this.stopPolling();
          this.modulosDeMiCuenta.set(null);
          this.bandeja.vaciar();
        }
      });
    });

    // Una URL profunda tampoco puede abrir en el navegador una sección que el rol no ve.
    effect(() => {
      if (!this.session()) return;
      const seccion = seccionAdministrativaPorId(this.activeSection());
      if (!seccion?.rolesPermitidos || this.roles.some(rol => seccion.rolesPermitidos?.includes(rol))) return;
      this.activeSection.set('monitor');
      if (this.router.url.startsWith('/administracion')) {
        void this.router.navigateByUrl('/administracion/resumen');
      }
    });
  }

  ngOnInit() {
    this.suscripcionRuta = this.ruta.paramMap.subscribe(parametros => {
      const ruta = parametros.get('seccion');
      if (!ruta) {
        this.activeSection.set('monitor');
        return;
      }
      const seccion = seccionAdministrativaPorRuta(ruta);
      if (!seccion) {
        // LA URL Y LA PANTALLA TIENEN QUE DECIR LO MISMO. Una dirección que no existe —una sección
        // renombrada, un marcador viejo, una errata— pintaba el Resumen y dejaba la barra diciendo
        // `/administracion/festivals`: quien la copiaba se llevaba una dirección rota que parecía
        // funcionar. Ahora se corrige la dirección, que es lo que el visitante puede volver a usar.
        this.activeSection.set('monitor');
        void this.router.navigateByUrl('/administracion/resumen', { replaceUrl: true });
        return;
      }
      this.activeSection.set(seccion.id);
    });
    this.sessionService.checkSession().subscribe({
      next: () => {
        this.sessionState.set('ready');
      },
      error: () => {
        this.sessionState.set('ready');
      }
    });
  }

  /**
   * Pregunta qué módulos puede abrir esta cuenta, con los que se dibuja la barra izquierda.
   *
   * <b>DESPUES DE CONFIRMAR LA SESION Y NO ANTES.</b> El armazón se monta en la misma ruta que la
   * pantalla de acceso, así que pedirlo en `ngOnInit` lo pedía sin cookie: el servidor respondía
   * 401, el respaldo dejaba la lista en `null` y la barra salía SIN FILTRAR. Comprobado el 15 de
   * septiembre de 2026: a una cuenta a la que se le acababa de quitar «Agenda» le seguía saliendo
   * en el menú, aunque la ruta ya le respondía 403.
   *
   * <b>SI FALLA, LA BARRA SALE ENTERA.</b> Es mejor ofrecer de más y que el servidor cierre con un
   * 403 explicado, que esconder secciones por un fallo de red y hacer creer que la cuenta no las
   * tiene.
   */
  private pedirMisModulos(): void {
    this.adminService.cargarMisModulos().subscribe({
      next: mios => this.modulosDeMiCuenta.set(mios.modulos),
      error: () => this.modulosDeMiCuenta.set(null),
    });
  }

  ngOnDestroy() {
    this.stopPolling();
    this.suscripcionRuta?.unsubscribe();
  }

  startPolling() {
    this.stopPolling();
    this.suscripcionNotificaciones = this.notificacionesEnVivo.observar('institucional', 20).subscribe(pagina => {
      const idsAnteriores = new Set(this.adminNotifications().map(item => String(item.id)));
      const nuevas = pagina.items.filter(item => !idsAnteriores.has(String(item.id)));
      this.adminNotifications.set(pagina.items);
      if (this.notificacionesInicializadas
          && nuevas.some(item => item.eventType === 'FestivalRecibidoParaRevision')) {
        this.bandeja.cargarRevisiones();
      }
      this.notificacionesInicializadas = true;
    });
    this.tickIntervalId = setInterval(() => {
      this.refreshAdminBackend();
    }, 10000);
  }

  stopPolling() {
    this.suscripcionNotificaciones?.unsubscribe();
    this.suscripcionNotificaciones = null;
    this.notificacionesInicializadas = false;
    if (this.tickIntervalId) {
      clearInterval(this.tickIntervalId);
      this.tickIntervalId = null;
    }
  }

  // Active Role and Capabilities

  /**
   * TODOS los roles de la sesión. Desde la transicion de la migración de SIMUS una persona puede
   * tener varios, y decidir por el primero haría que la consola abriera o cerrara pantallas
   * según el orden en que llegaran.
   *
   * Cae en la lista VACÍA cuando no hay sesión, no en un rol por omisión: con la lista vacía
   * `algunoEsRolInterno` dice que no y el SPA abre el panel de colaborador, que es el lado
   * seguro del error. Ver `isCollaboratorRole`.
   */
  get roles(): string[] {
    return rolesDeSesion(this.session());
  }

  /**
   * El rol PRINCIPAL, solo para lo que necesita uno: la etiqueta que se pinta y el tour.
   *
   * No decide permisos. Todo lo que sea abrir o cerrar algo pregunta por `roles`, que es el
   * conjunto entero.
   */
  get roleId(): string {
    const roles = this.roles;
    if (roles.includes('webmaster')) { return 'webmaster'; }
    if (roles.includes('gestor_interno')) { return 'gestor_interno'; }
    return roles[0] || 'gestor';
  }

  /**
   * ¿Se le abre el panel de colaborador en vez de la consola interna?
   *
   * ESTÁ ESCRITO COMO NEGACIÓN DE UNA LISTA BLANCA, y ese es el arreglo. Antes enumeraba a los
   * NO internos —`['externo', 'aliado_admin', 'aliado_editor', 'aliado_lector', 'gestor']`— y
   * dejaba la consola completa como caso por defecto. Esta es la ÚNICA bifurcación del SPA
   * entre las dos pantallas, y así escrita convertía cada rol nuevo en un usuario interno por
   * omisión: abriría la consola entera y solo recibiría 403 en cada llamada.
   *
   * Nótese además que `roleId` cae en `'gestor'` cuando no hay sesión: con la lista invertida,
   * un fallo al leer la sesión abría la consola interna. Ahora abre el panel de colaborador,
   * que es el lado seguro del error.
   */
  get isCollaboratorRole(): boolean {
    return !algunoEsRolInterno(this.roles);
  }

  get modules(): any[] {
    return getModulesForRoles(this.roles);
  }

  get canApprove(): boolean {
    return canRoles(this.roles, 'approve');
  }

  /**
   * ¿Esta sesión puede sacar contenido al portal público?
   *
   * HASTA NADIE SE LO PREGUNTABA. El armazón montaba el editor de textos sin
   * pasarle `puedePublicar`, así que se quedaba en su valor por omisión —`false`— y los
   * controles de publicación de las imágenes incrustadas no aparecían para NADIE, ni siquiera
   * para un webmaster. No era una decisión de permisos: era un input que nadie conectó.
   *
   * El servidor ya exigía la capacidad; lo que faltaba era que la interfaz la reflejara.
   */
  get puedePublicarSitio(): boolean {
    return canRoles(this.roles, 'publish');
  }

  /**
   * ¿La sesión tiene este rol? La plantilla decide con esto, no comparando contra `roleId`.
   *
   * POR QUÉ NO BASTA `roleId === 'webmaster'`. Funciona, pero sólo por una coincidencia: `roleId`
   * devuelve el rol de MAYOR precedencia, así que coincide con «lo tiene» mientras el catálogo sea
   * una jerarquía de tres. El día que aparezca un rol que no encaje en ese orden —la subdivisión
   * fina de funcionarios que el modelo deja para más adelante— esa comparación empezaría a ocultar
   * paneles a quien sí puede abrirlos, y en silencio. Es el mismo hallazgo que se documentó en el
   * API para la matriz de cambio de estado.
   */
  tieneRol(rol: string): boolean {
    return this.roles.includes(rol);
  }

  // Sync Backend Stats
  refreshAdminBackend(manual = false) {
    if (manual) this.refrescandoPanel.set(true);

    this.adminService.cargarMonitorDelSistema().subscribe({
      next: (monitorPayload) => {
        this.monitor.set(monitorPayload);
        this.schemaOnline.set(monitorPayload?.database?.status === 'ok');
        this.stats.set(Object.fromEntries((monitorPayload?.modules || []).map(mod => [mod.id, mod.total])));
        this.apiStatus.set(`Última lectura: ${new Date().toLocaleTimeString('es-CO')} · API ${monitorPayload?.api?.latencyMs || 0} ms`);
        this.refrescandoPanel.set(false);
      },
      error: (err) => {
        this.schemaOnline.set(false);
        this.apiStatus.set(`Error de conexión: ${err.message}`);
        this.refrescandoPanel.set(false);
      }
    });

    this.adminService.cargarDivipolaPorDepartamento().subscribe({
      next: (territories) => {
        this.divipola.set(territories || {});
      }
    });

    this.bandeja.recargar();
    this.loadAdministrativeSummary();
  }

  /** Lo que el Resumen operativo enseña además de la bandeja: cuántas organizaciones hay y qué pasó últimamente. */
  private loadAdministrativeSummary(): void {
    this.adminService.cargarOrganizaciones({ tamano: 1 }).subscribe({
      next: payload => this.organizationsTotal.set(Number(payload?.total ?? payload?.items?.length ?? 0)),
      error: () => this.organizationsTotal.set(null),
    });
    this.adminService.cargarAuditoria({ tamano: 6 }).subscribe({
      next: payload => this.administrativeActivity.set(payload?.items ?? []),
      error: () => this.administrativeActivity.set([]),
    });
  }

  /**
   * Los avisos informativos solo se marcan como leídos. Un aviso de Festival recibido para
   * revisión sí abre el trámite institucional correspondiente, porque representa trabajo activo.
   */
  openNotification(notification: NotificacionEnVivo): void {
    if (!notification.readAt && notification.id) {
      this.adminService.marcarNotificacionComoLeida(String(notification.id)).subscribe({
        next: updated => this.adminNotifications.update(items => items.map(item => item.id === notification.id ? { ...item, ...updated, readAt: updated?.readAt ?? new Date().toISOString() } : item)),
      });
    }
    this.adminNotificationsOpen.set(false);
    if (notification.eventType === 'FestivalRecibidoParaRevision' && notification.recordId) {
      this.irASeccion('solicitudes');
      this.bandeja.cargarRevisiones();
      this.bandeja.pedirTramiteDeFestival(String(notification.recordId));
    }
  }

  markAllNotificationsRead(): void {
    const unread = this.adminNotifications().filter(item => !item.readAt && item.id);
    unread.forEach(item => this.adminService.marcarNotificacionComoLeida(String(item.id)).subscribe({
      next: updated => this.adminNotifications.update(items => items.map(current => current.id === item.id ? { ...current, ...updated, readAt: updated?.readAt ?? new Date().toISOString() } : current)),
    }));
  }

  clearReadNotifications(): void {
    if (!this.readNotifications()) return;
    this.adminService.descartarNotificacionesLeidas().subscribe({
      next: () => this.adminNotifications.update(items => items.filter(item => !item.readAt)),
      error: () => undefined,
    });
  }

  // Authentication Handlers
  handleLogin(user: UsuarioAdministrativo) {
    this.sessionService.setSession(user);
    this.activeSection.set('monitor');
    void this.router.navigateByUrl('/administracion/resumen');
    this.refreshAdminBackend();
  }

  handleLogout() {
    this.sessionService.logout().subscribe({
      next: () => {
        this.router.navigateByUrl('/administracion');
      },
      error: () => {
        this.router.navigateByUrl('/administracion');
      }
    });
  }

  // Sidebar Layout Navigation List
  /** La navegación visible se deriva del catálogo tipado y de la unión de roles de la sesión. */
  // ─────────────────────── Ir a una sección por su nombre ───────────────────────

  /**
   * El buscador de la cabecera.
   *
   * <b>QUE BUSCA, Y POR QUE SOLO ESO.</b> Busca SECCIONES, no registros, y el campo lo dice. La
   * consola declara quince repartidas en cinco familias, y en pantalla pequeña la barra lateral se
   * convierte en una tira de quince pestañas que hay que recorrer de lado. Un buscador de
   * registros necesitaría una consulta transversal que hoy no existe en el API; prometerla aquí
   * sería una caja que parece encontrarlo todo y encuentra una parte.
   *
   * <b>SE BUSCA TAMBIEN POR FAMILIA</b> —«gobierno» encuentra Auditoría— porque quien no recuerda
   * el nombre de una sección suele recordar dónde vive.
   */
  readonly busquedaDeSeccion = signal('');

  readonly seccionesEncontradas = computed(() => {
    const termino = this.normalizar(this.busquedaDeSeccion());
    if (termino.length < 2) { return []; }

    return this.visibleNavSections
      .flatMap(grupo => grupo.items.map(item => ({ ...item, familia: grupo.label })))
      .filter(item =>
        this.normalizar(item.label).includes(termino) || this.normalizar(item.familia).includes(termino))
      .slice(0, 6);
  });

  /**
   * Sin tildes y en minúsculas, para que «auditoria» encuentre «Auditoría».
   *
   * Quien busca escribe rápido y sin acentos; exigirlos convierte el buscador en un examen de
   * ortografía.
   */
  private normalizar(valor: string): string {
    return (valor ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim()
      .toLowerCase();
  }

  /** Abre la sección encontrada y deja el campo limpio para la siguiente búsqueda. */
  irASeccionEncontrada(id: string): void {
    this.busquedaDeSeccion.set('');
    this.irASeccion(id);
  }

  /**
   * Los módulos que esta cuenta tiene activados, tal como los dice el servidor.
   *
   * <b>SE PREGUNTAN Y NO SE DEDUCEN.</b> Los permisos se conceden por cuenta y los aplica el
   * servidor; deducirlos aquí del rol daría una barra que no corresponde con lo que las rutas
   * dejan abrir, y esa discrepancia se nota como un 403 al pulsar algo que se veía.
   *
   * MIENTRAS NO HAN LLEGADO, `null`, y la barra se dibuja entera. Esconder secciones durante el
   * primer segundo y sacarlas después es peor que enseñarlas: el menú saltaría en cada carga.
   */
  readonly modulosDeMiCuenta = signal<readonly string[] | null>(null);

  get visibleNavSections() {
    // POR EL CONJUNTO, NO POR EL PRINCIPAL. Con `roleId` a secas, a quien tuviera
    // {gestor_interno, webmaster} se le mostrarían u ocultarían secciones según cuál ganara la
    // precedencia, y no según lo que de verdad puede hacer. La unión es lo que significa tener
    // dos roles.
    //
    // Y ADEMAS DEL ROL, LOS MODULOS ACTIVADOS DE ESTA CUENTA. Son dos preguntas distintas: el rol
    // dice quién eres y el módulo dice qué te han activado.
    const mios = this.modulosDeMiCuenta();
    return gruposAdministrativosVisibles(this.roles)
      .map(grupo => ({
        ...grupo,
        secciones: mios === null ? grupo.secciones : grupo.secciones.filter(s => mios.includes(s.id)),
      }))
      .filter(grupo => grupo.secciones.length > 0)
      .map(grupo => ({
      group: grupo.id,
      label: grupo.titulo,
      items: grupo.secciones.map(seccion => ({
        id: seccion.id,
        label: seccion.titulo,
        icono: seccion.icono,
      })),
    }));
  }

  /** Festivales es el único módulo de datos informado como operativo en esta versión. */
  readonly ECOSYSTEM_MODULE_IDS = ['festivals'];

  /**
   * Las secciones que trabajan sobre una lista se apoyan en un lienzo blanco, como Solicitudes
   * desde su rediseño; el Resumen operativo conserva el fondo gris porque es un tablero y sus
   * paneles entintados necesitan un fondo contra el que leerse.
   */
  readonly SECCIONES_DE_LISTA: readonly string[] = ['solicitudes', 'calidad', 'ecosistema', 'organizaciones'];

  /** El Festival cuya ficha se está mirando, si alguno. */
  readonly fichaDeFestival = signal<string | null>(null);

  /**
   * El mercado cuya ficha está abierta.
   *
   * VIVE AQUI Y NO EN EL PANEL, por lo mismo que la del Festival: la ficha necesita saber si quien
   * mira puede publicar y tiene que poder llevar a la organización responsable, que está en otra
   * sección de la consola. Las dos cosas las sabe el armazón.
   */
  readonly fichaDeMercado = signal<string | null>(null);

  abrirFichaDeMercado(id: string): void { this.fichaDeMercado.set(id); }

  cerrarFichaDeMercado(): void { this.fichaDeMercado.set(null); }

  abrirFichaDeFestival(id: string): void { this.fichaDeFestival.set(id); }

  cerrarFichaDeFestival(): void { this.fichaDeFestival.set(null); }

  /** La organización que se pidió abrir desde otra pantalla, si alguna. */
  readonly organizacionPedida = signal<string | null>(null);

  /**
   * Va de un Festival a la organización que responde por él, y llega hasta ella.
   *
   * <b>LA RELACION SE RECORRE ENTERA.</b> Hasta este método abría la
   * sección de Organizaciones y ahí se acababa: quien venía de un Festival tenía que buscar a mano,
   * entre todas, la organización cuyo nombre acababa de leer. Una relación que existe en la base y
   * no se puede recorrer en pantalla es una relación que nadie puede administrar.
   */
  irALaOrganizacion(organizacionId: number): void {
    this.fichaDeFestival.set(null);
    this.organizacionPedida.set(String(organizacionId));
    this.irASeccion('organizaciones');
  }

  /**
   * Va de una organización a uno de los procesos que administra.
   *
   * ES EL CAMINO DE VUELTA, y hace falta por el mismo motivo: la ficha de la organización listaba
   * sus Festivales como texto, así que se podía leer el nombre y no abrirlo.
   */
  irAlFestival(festivalId: string): void {
    this.organizacionPedida.set(null);
    this.irASeccion('ecosistema');
    this.selectedModuleId.set('festivals');
    this.fichaDeFestival.set(festivalId);
  }

  /** El alta de Festival desde la consola, abierta o no. */
  readonly registrandoFestival = signal(false);

  readonly avisoDeFestival = signal<string | null>(null);

  abrirAltaDeFestival(): void {
    this.avisoDeFestival.set(null);
    this.registrandoFestival.set(true);
  }

  cerrarAltaDeFestival(): void { this.registrandoFestival.set(false); }

  /**
   * Cierra el alta y refresca la lista.
   *
   * SE RECARGA LA SECCION ENTERA y no solo la tabla: el recuento de la cabecera y las facetas de
   * estado también cambian, y dejarlos con la cifra anterior haría dudar de si se guardó.
   */
  festivalRegistrado(festival: { id: string; nombre: string }): void {
    this.registrandoFestival.set(false);
    this.avisoDeFestival.set(`«${festival.nombre}» quedó registrado como borrador, con procedencia del Programa.`);
    this.selectedModuleId.set('festivals');
  }

  modulosDe(ids: readonly string[]): any[] {
    return this.modules.filter(m => ids.includes(m.id));
  }

  moduloActivoDe(ids: readonly string[]) {
    const mods = this.modulosDe(ids);
    return mods.find(m => m.id === this.selectedModuleId()) || mods[0] || null;
  }

  // Visual Helpers
  COLORES_DE_AVATAR = [
    'bg-violet-600', 'bg-indigo-600', 'bg-blue-600',
    'bg-teal-600', 'bg-emerald-600', 'bg-amber-600', 'bg-rose-600',
  ];

  colorDeAvatar(name = ''): string {
    const hash = String(name).split('').reduce((acc, ch) => acc + ch.charCodeAt(0), 0);
    return this.COLORES_DE_AVATAR[hash % this.COLORES_DE_AVATAR.length];
  }

  inicialesDe(name = ''): string {
    const parts = String(name).trim().split(/\s+/);
    if (parts.length === 1 && parts[0]) return parts[0].slice(0, 2).toUpperCase();
    if (parts.length > 1) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return 'U';
  }

  nombreDelRol(role: string): string {
    return ADMIN_ROLES[role]?.shortLabel || role;
  }

  /**
   * Cómo se presenta la condición de quien tiene la sesión.
   *
   * <b>TODOS LOS USUARIOS DE ESTA CONSOLA PERTENECEN AL PROGRAMA</b>, y hasta ahora la pantalla
   * solo decía su rol: «Webmaster» a secas no dice desde dónde actúa esa persona, y es justo lo
   * que hay que poder distinguir de una cuenta del espacio externo.
   *
   * <b>ES DINÁMICO, NO UN TEXTO FIJO.</b> Sale del rol de la sesión, sea cual sea la cuenta:
   * codificar «webmaster PNMC» convertiría un usuario de prueba en una regla.
   */
  condicionInstitucional(role: string): string {
    return `PNMC · ${this.nombreDelRol(role)}`;
  }

  statusPillClass(status: string): string {
    const STYLES: Record<string, string> = {
      borrador:            'text-slate-600 bg-slate-100 border border-slate-200',
      en_revision:         'text-blue-700 bg-blue-50 border border-blue-200',
      ajustes_solicitados: 'text-amber-700 bg-amber-50 border border-amber-200',
      aprobado:            'text-emerald-700 bg-emerald-50 border border-emerald-200',
      publicado:           'text-violet-700 bg-violet-50 border border-violet-200',
      rechazado:           'text-red-700 bg-red-50 border border-red-200',
      archivado:           'text-slate-400 bg-slate-50 border border-slate-100',
    };
    return STYLES[status] || STYLES['borrador'];
  }

  statusLabel(status: string): string {
    const LABELS: Record<string, string> = {
      borrador:            'Borrador',
      en_revision:         'En revisión',
      ajustes_solicitados: 'Ajustes solicitados',
      aprobado:            'Aprobado',
      publicado:           'Publicado',
      rechazado:           'Rechazado',
      archivado:           'Archivado',
    };
    return LABELS[status] || status;
  }
}
