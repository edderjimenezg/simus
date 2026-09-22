import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { ExternalSessionService, OrganizacionDeSesion } from '../../core/services/external-session.service';

/**
 * Qué organización está abierta en el panel, compartido por todas sus secciones.
 *
 * <b>SOLO ESTO, NO UN «GOD STORE» CON TODO EL ESTADO DEL PANEL.</b> Rediseño del 1 de septiembre de
 * 2026: `/gestion` pasó de una sola página con pestañas por query-param a un armazón con
 * rutas hijas de verdad (Resumen, Organización, Responsable, Ecosistema, Solicitudes, Cuenta y
 * seguridad). El patrón ya establecido en `SeccionOrganizacionComponent` -que nunca recibió
 * `responsable` por `@Input`, siempre lo pidió él mismo con `PanelOrganizacionApi`- es que cada
 * sección se alimenta sola; lo único que de verdad cruza las seis secciones es CUÁL organización
 * está abierta. `perfil`, `festivales`, `notificaciones` los pide cada sección que los necesita,
 * cuando los necesita.
 *
 * <b>Las organizaciones administradas no se piden aquí.</b> `ExternalSessionService.organizaciones`
 * ya las trae en la respuesta de la sesión -no hay una llamada aparte que repetir-; este store solo
 * elige la primera al entrar y recuerda cuál está activa.
 *
 * Provisto en `providers` de la ruta padre `gestion` (`app.routes.ts`): una sola instancia para
 * el armazón y las seis rutas hijas, que muere al salir de Gestión.
 */
@Injectable()
export class PanelOrganizacionStore {
  private readonly sesionExterna = inject(ExternalSessionService);

  readonly organizaciones = this.sesionExterna.organizaciones;

  /** Con una sola organización, un desplegable de una opción es un adorno que pide un clic de más. */
  readonly hayVariasOrganizaciones = computed(() => this.organizaciones().length > 1);

  readonly organizacionId = signal('');

  constructor() {
    /*
     * La sesión es la fuente de verdad de las organizaciones administrables. Si una persona
     * pierde un vínculo mientras tiene Gestión abierto, conservar el id anterior haría que la
     * interfaz intentara trabajar sobre una organización que el servidor ya no autoriza. Se
     * conserva la elección mientras siga siendo válida; de lo contrario se toma la primera que
     * el servidor aún reconoce, o ninguna si la cuenta dejó de administrar organizaciones.
     */
    effect(() => {
      const organizaciones = this.organizaciones();
      const actual = this.organizacionId();
      if (actual && organizaciones.some(organizacion => organizacion.id === actual)) return;
      this.organizacionId.set(organizaciones[0]?.id ?? '');
    });
  }

  organizacionElegida(): OrganizacionDeSesion | null {
    return this.organizaciones().find(item => item.id === this.organizacionId()) ?? null;
  }

  /** Elige la primera organización de la cuenta, si todavía no hay ninguna elegida. */
  elegirOrganizacionInicial(): void {
    if (this.organizacionId()) return;
    const primera = this.organizaciones()[0];
    if (primera) this.organizacionId.set(primera.id);
  }

  seleccionarOrganizacion(id: string): void {
    // El select no es una autorización. Aun así, no se debe aceptar en el estado de la interfaz
    // un id que no vino en la sesión: evita mostrar transitoriamente el contexto equivocado antes
    // de que las verificaciones de cada endpoint respondan 403.
    if (!this.organizaciones().some(organizacion => organizacion.id === id)) return;
    this.organizacionId.set(id);
  }
}
