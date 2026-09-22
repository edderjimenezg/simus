import { Component, DestroyRef, ElementRef, HostListener, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CajonDeConsultaGuiadaComponent } from '../../shared/components/consulta-guiada/cajon-de-consulta-guiada.component';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ExternalSessionService } from '../../core/services/external-session.service';
import { destinoExternoDe, NotificacionesEnVivoService } from '../../core/services/notificaciones-en-vivo.service';
import { FalloDelServidor, NotificacionDelPanel, PanelOrganizacionApi, PerfilOrganizacion } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';
import { nombrePropio } from '../../shared/texto/nombre-propio';

/** Un enlace de la navegación del panel. */
export interface EnlacePanel {
  ruta: string;
  etiqueta: string;
  icono: string;
}

interface ContextoDeSeccion {
  descripcion: string;
}

/**
 * Los enlaces del panel, en el orden en que se leen de arriba abajo.
 *
 * SON ENLACES DE VERDAD DESDE EL 1 DE SEPTIEMBRE DE 2026, no botones de pestaña sobre
 * `?pestana=`: cada uno tiene su propia dirección, se puede enlazar y «atrás» funciona como en
 * cualquier otro sitio. La persona responsable se integra en «Datos de la organización»: es un
 * segundo formulario, pero no una segunda sección de navegación global.
 */
export const ENLACES_DEL_PANEL: readonly EnlacePanel[] = [
  { ruta: 'resumen', etiqueta: 'Resumen', icono: '⌂' },
  { ruta: 'procesos', etiqueta: 'Procesos', icono: '◇' },
  // EVENTOS VA DESPUES DE PROCESOS Y NO ANTES, porque depende de ellos: un evento se anuncia
  // siempre dentro de un proceso, y sin ninguno publicado esta sección no ofrece el formulario.
  { ruta: 'eventos', etiqueta: 'Eventos', icono: '◷' },
  { ruta: 'solicitudes', etiqueta: 'Solicitudes', icono: '✦' },
  { ruta: 'organizacion', etiqueta: 'Datos de la organización', icono: '◫' },
  // MIS ARCHIVOS VA DESPUES DE LOS DATOS y antes de la cuenta: es material de la organización, no
  // una preferencia de la cuenta. Aquí se ve lo que ocupa el espacio y se retira lo que ya no usa
  // ninguna ficha, que es lo que el aviso de cuota venía pidiendo sin dar con qué.
  { ruta: 'archivos', etiqueta: 'Mis archivos', icono: '⛁' },
  { ruta: 'cuenta-seguridad', etiqueta: 'Cuenta y seguridad', icono: '⚙' },
];

/**
 * El armazón de gestión de la organización externa, en /gestion.
 *
 * <b>REDISEÑO DEL 1 DE SEPTIEMBRE DE 2026.</b> Hasta entonces esta clase tenía casi 800 líneas: la
 * navegación por pestañas, la carga del perfil, la lista de Festivales, las notificaciones, el
 * diálogo de la ficha y el álgebra de sus reglas de bloqueo, todo junto porque todo vivía detrás
 * de una sola pestaña o de otra. El criterio es este: «mezcla contexto de sesión,
 * edición de organización, avisos, procesos, formularios y ficha de Festival en una misma
 * jerarquía visual» — la sensación de «cajas dentro de cajas».
 *
 * <b>Lo que queda aquí y nada más.</b> La sesión, el selector de organización, el resumen de
 * lectura de la columna izquierda, la campana de avisos, y el `<router-outlet>` de las seis
 * secciones. Los Festivales, la ficha y sus reglas de bloqueo se mudaron a
 * `SeccionEcosistemaComponent`, que es la única que los usa.
 *
 * <b>Por qué el panel va sobre fondo claro.</b> El criterio es este: la
 * barra de navegación es `fixed` y solo se pinta sólida en las páginas que lo declaran en
 * `app.component.ts`. La página tiene identificador propio —`PAGE_IDS.miPanel`— y el contenido
 * arranca a `pt-20`, igual que Editorial y Agenda.
 *
 * <b>La sesión.</b> La deniega `sesionExternaGuard` en la ruta padre. La comprobación de aquí no
 * sustituye a esa: evita pintar un panel a medias mientras `refrescar()` está en vuelo.
 */
@Component({
  selector: 'app-panel-organizacion-page',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, CajonDeConsultaGuiadaComponent],
  templateUrl: './panel-organizacion-page.component.html',
})
export class PanelOrganizacionPageComponent implements OnInit {
  private readonly sesionExterna = inject(ExternalSessionService);
  private readonly api = inject(PanelOrganizacionApi);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly notificacionesEnVivo = inject(NotificacionesEnVivoService);
  readonly store = inject(PanelOrganizacionStore);
  private readonly confirmacion = inject(ConfirmacionDeCorreoService);
  private notificacionesActivadas = false;

  readonly enlaces = ENLACES_DEL_PANEL;

  readonly sesion = this.sesionExterna.actual;
  readonly organizaciones = this.store.organizaciones;
  readonly hayVariasOrganizaciones = this.store.hayVariasOrganizaciones;
  readonly organizacionId = this.store.organizacionId;

  readonly perfil = signal<PerfilOrganizacion | null>(null);
  readonly cargandoPerfil = signal(false);
  readonly errorPerfil = signal('');

  readonly notificaciones = signal<NotificacionDelPanel[]>([]);

  /** Mientras la sesión no ha resuelto no se pinta ni el panel ni el aviso de que no hay sesión. */
  readonly comprobandoSesion = signal(true);

  /**
   * Los avisos sin leer. Es lo que cuenta la campana.
   *
   * Se sigue pidiendo aquí, y no solo en Resumen o en Solicitudes, porque la campana vive en la
   * cabecera del armazón y se ve desde cualquier sección abierta.
   */
  readonly avisosSinLeer = computed(() => this.notificaciones().filter(aviso => !aviso.readAt).length);
  readonly avisosLeidos = computed(() => this.notificaciones().filter(aviso => !!aviso.readAt).length);

  /**
   * La campana abre un panel propio, no navega a Solicitudes.
   *
   * <b>SON DOS COSAS DISTINTAS.</b> El criterio es este: «el botón de
   * avisos debería permitir consultar directamente las notificaciones, mientras que las
   * solicitudes deberían mantenerse como un módulo independiente». Solicitudes es lo que la
   * organización envió y en qué punto está -un cajón de trabajo propio-; los avisos son lo que el
   * circuito de revisión fue escribiendo -un registro de sucesos-. Antes compartían una sola
   * puerta y la campana llevaba a la pestaña equivocada para «solo quiero ver qué dice el aviso».
   */
  readonly avisosAbiertos = signal(false);
  readonly rutaActual = signal(this.router.url);
  readonly gestionandoFestival = computed(() => /^\/gestion\/procesos\/festivales\/[^/?]+/.test(this.rutaActual()));
  /** Explica la pestaña activa una vez, en el armazón, sin repetir su nombre dentro de cada vista. */
  readonly contextoDeSeccion = computed<ContextoDeSeccion | null>(() => {
    const ruta = this.rutaActual().split('?')[0];
    if (ruta === '/gestion/resumen') return { descripcion: 'Consulta las prioridades y el estado general de los procesos de esta organización.' };
    if (ruta === '/gestion/procesos') return { descripcion: 'Registra, consulta y continúa los procesos que administra esta organización.' };
    if (ruta === '/gestion/solicitudes') return { descripcion: 'Consulta los ajustes de revisión pendientes y los trámites administrativos de esta organización.' };
    if (ruta === '/gestion/organizacion') return { descripcion: 'Actualiza la información pública y la persona responsable de la organización.' };
    if (ruta === '/gestion/archivos') return { descripcion: 'Las imágenes que has adjuntado a tus procesos, y el espacio que ocupan.' };
    if (ruta === '/gestion/cuenta-seguridad') return { descripcion: 'Administra el acceso y las medidas de seguridad de tu cuenta.' };
    return null;
  });

  alternarAvisos(): void {
    this.avisosAbiertos.update(abierto => !abierto);
  }

  /** Cierra el panel al hacer clic fuera. Mismo mecanismo que el menú de cuenta de la barra. */
  @HostListener('document:pointerdown', ['$event'])
  alHacerClicFuera(evento: PointerEvent): void {
    if (!this.avisosAbiertos()) return;
    const objetivo = evento.target as Node | null;
    if (!objetivo || !this.host.nativeElement.querySelector('[data-panel-avisos]')?.contains(objetivo)) {
      this.avisosAbiertos.set(false);
    }
  }

  marcarLeida(notificacion: NotificacionDelPanel): void {
    this.api.marcarNotificacionLeida(notificacion.id).subscribe({
      next: actualizada => {
        this.notificaciones.update(lista =>
          lista.map(item => (item.id === actualizada.id ? actualizada : item)),
        );
      },
      error: () => undefined,
    });
  }

  marcarTodasLeidas(): void {
    const pendientes = this.notificaciones().filter(notificacion => !notificacion.readAt);
    if (!pendientes.length) return;
    forkJoin(pendientes.map(notificacion => this.api.marcarNotificacionLeida(notificacion.id))).subscribe({
      next: actualizadas => this.notificaciones.update(lista => lista.map(item => actualizadas.find(actualizada => actualizada.id === item.id) ?? item)),
      error: () => undefined,
    });
  }

  limpiarAvisosLeidos(): void {
    if (!this.avisosLeidos()) return;
    this.api.ocultarNotificacionesLeidas().subscribe({
      next: () => this.notificaciones.update(lista => lista.filter(aviso => !aviso.readAt)),
      error: () => undefined,
    });
  }

  /**
   * Solo los avisos que representan una acción pendiente tienen destino. Los avisos puramente
   * informativos conservan su función de registro y no simulan ser enlaces.
   */
  rutaDelAviso(notificacion: NotificacionDelPanel): string[] | null {
    return destinoExternoDe(notificacion)?.ruta.map(segmento => String(segmento)) ?? null;
  }

  parametrosDelAviso(notificacion: NotificacionDelPanel): Record<string, string> | null {
    return destinoExternoDe(notificacion)?.parametros ?? null;
  }

  accionDelAviso(notificacion: NotificacionDelPanel): string {
    return destinoExternoDe(notificacion)?.accion ?? '';
  }

  abrirAviso(notificacion: NotificacionDelPanel): void {
    this.avisosAbiertos.set(false);
    if (!notificacion.readAt) this.marcarLeida(notificacion);
  }

  inicialesOrganizacion(): string {
    const nombre = this.perfil()?.nombre || this.store.organizacionElegida()?.name || '';
    return nombre.split(/\s+/).filter(Boolean).slice(0, 2).map(parte => parte[0]).join('').toUpperCase() || 'OR';
  }

  ubicacionOrganizacion(): string | null {
    const ficha = this.perfil();
    if (!ficha) return null;
    // La fuente territorial guarda los nombres como los publica el DANE —«LA PLATA», «HUILA»— y
    // aquí se enseñan; se escriben como se escriben. Ver `shared/texto/nombre-propio.ts`.
    const ubicacion = [ficha.nombreMunicipioSede, ficha.nombreDepartamentoSede]
      .filter(Boolean).map(nombre => nombrePropio(nombre)).join(', ');
    return ubicacion || null;
  }

  constructor() {
    this.router.events.subscribe(evento => {
      if (evento instanceof NavigationEnd) this.rutaActual.set(evento.urlAfterRedirects);
    });
    // El perfil de la columna izquierda cambia al cambiar de organización.
    effect(() => {
      const id = this.organizacionId();
      if (id) this.cargarPerfil(id);
    });
  }

  ngOnInit(): void {
    // SE PREGUNTA UNA VEZ AL ENTRAR AL PANEL, no en cada pantalla. La confirmación del correo es de
    // la cuenta —quien administra tres organizaciones no tiene tres correos— y desde el 12 de
    // septiembre de 2026 decide qué puede hacer en cuatro pantallas distintas. Resuelta aquí, cada
    // una la lee ya sabida en vez de esperar su propia respuesta de red antes de pintar.
    void this.confirmacion.asegurar();

    if (this.sesionExterna.dentro()) {
      this.comprobandoSesion.set(false);
      this.store.elegirOrganizacionInicial();
      this.activarNotificaciones();
      return;
    }

    // SE USA `refrescar()` Y NO `cargarUnaVez()` A PROPÓSITO: `cargarUnaVez()` no dice cuándo
    // terminó, y aquí distinguir «todavía no se ha consultado» de «no hay sesión» es justo lo que
    // decide entre pintar el panel y mandar a la pantalla de ingreso.
    this.sesionExterna.refrescar().subscribe(sesion => {
      this.comprobandoSesion.set(false);
      if (!sesion) {
        this.router.navigate(['/ingresar']);
        return;
      }
      this.store.elegirOrganizacionInicial();
      this.activarNotificaciones();
    });
  }

  private cargarPerfil(organizacionId: string): void {
    this.perfil.set(null);
    this.errorPerfil.set('');
    this.cargandoPerfil.set(true);
    this.api.obtenerPerfil(organizacionId).subscribe({
      next: perfil => {
        this.cargandoPerfil.set(false);
        this.perfil.set(perfil);
      },
      error: (fallo: FalloDelServidor) => {
        this.cargandoPerfil.set(false);
        this.errorPerfil.set(fallo?.message ?? 'No fue posible consultar los datos de la organización');
      },
    });
  }

  private activarNotificaciones(): void {
    if (this.notificacionesActivadas) return;
    this.notificacionesActivadas = true;
    this.notificacionesEnVivo.observar('externo', 50).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: pagina => this.notificaciones.set(pagina?.items ?? []),
    });
  }

  seleccionarOrganizacion(id: string): void {
    this.store.seleccionarOrganizacion(id);
  }

  cerrarSesion(): void {
    this.sesionExterna.salir().subscribe(() => this.router.navigate(['/']));
  }
}
