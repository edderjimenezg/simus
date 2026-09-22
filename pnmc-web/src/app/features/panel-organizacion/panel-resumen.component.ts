import { Component, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FestivalDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';
import { estadoDelFestival } from './estados-del-festival';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { AvisoDeCorreoSinConfirmarComponent } from './aviso-de-correo-sin-confirmar.component';
import { InvitacionACompletarComponent } from './invitacion-a-completar.component';
import { PerfilOrganizacion } from './panel-organizacion.api';
import { ExternalSessionService } from '../../core/services/external-session.service';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';

/**
 * La ruta «Resumen», en /gestion/resumen: la vista de aterrizaje de Gestión.
 *
 * <b>NUEVA EN EL REDISEÑO DEL 1 DE SEPTIEMBRE DE 2026.</b> Antes no existía como sección propia: su
 * contenido -organización activa, estado del registro- vivía fijo en la columna izquierda, y quien
 * entraba al panel caía directo en «Organización» con catorce campos de lectura. El dueño del
 * proyecto pidió una vista inicial que resuma en vez de desplegar: organización activa, estado,
 * pendientes, avisos y procesos registrados, sin formularios.
 *
 * <b>Pide sus propios datos</b>, como el resto de las secciones desde el rediseño: no hay ningún
 * estado de Festival o notificaciones compartido en {@link PanelOrganizacionStore} -solo qué
 * organización está abierta- así que esta pantalla llama a {@link PanelOrganizacionApi} igual que
 * lo haría si viviera sola.
 */
@Component({
  selector: 'app-panel-resumen',
  standalone: true,
  imports: [CommonModule, RouterLink, AvisoDeCorreoSinConfirmarComponent, InvitacionACompletarComponent],
  templateUrl: './panel-resumen.component.html',
})
export class PanelResumenComponent {
  private readonly api = inject(PanelOrganizacionApi);
  private readonly store = inject(PanelOrganizacionStore);
  private readonly sesion = inject(ExternalSessionService);
  private readonly confirmacion = inject(ConfirmacionDeCorreoService);

  /**
   * El perfil, para saber qué información falta (§15.2 y §15.3).
   *
   * <b>SE PIDE AQUI Y NO EN LA SECCION «ORGANIZACION».</b> El Resumen es la vista de aterrizaje: si
   * la invitación a completar viviera dentro del formulario, solo la vería quien ya decidió ir a
   * rellenarlo. La pregunta «¿qué me falta?» hay que responderla donde se entra.
   */
  readonly perfil = signal<PerfilOrganizacion | null>(null);

  /** Si esta es la primera vez que la cuenta entra. Lo trae la respuesta del ingreso. */
  readonly primerIngreso = computed(() => this.sesion.actual()?.esPrimerIngreso === true);

  /** Cómo se llama quien entró, para saludarle en la bienvenida. */
  readonly nombreDeQuienEntro = computed(() => this.sesion.actual()?.fullName ?? '');

  /**
   * Si el correo ya está comprobado.
   *
   * Lo lee del mismo servicio que pinta el aviso de arriba, y no de una consulta propia: dos
   * lecturas del mismo hecho es como se llega a que la misma pantalla diga las dos cosas.
   */
  readonly correoConfirmado = computed(() => this.confirmacion.estado()?.correoConfirmado === true);

  readonly organizacionId = this.store.organizacionId;

  readonly festivales = signal<FestivalDeLaOrganizacion[]>([]);
  readonly cargandoFestivales = signal(false);

  /**
   * Los seis contadores de la franja de indicadores, separados uno por uno.
   *
   * ANTES «PENDIENTES» JUNTABA EnRevision Y AjustesSolicitados en un solo número. Se define
   * distinguirlos: «en revisión» es lo que espera una decisión del PNMC
   * -la organización ya no tiene nada que hacer-; «requiere tu atención» es lo que el PNMC devolvió
   * con ajustes -la organización sí tiene algo que hacer-. Confundir los dos le decía a la persona
   * que actuara sobre algo que en realidad estaba esperando del otro lado.
   */
  readonly enRevision = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'EnRevision').length,
  );

  readonly requierenAtencion = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'AjustesSolicitados').length,
  );

  readonly borradoresSinEnviar = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'Borrador').length,
  );

  readonly publicados = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'Publicado').length,
  );

  /** Los dos únicos estados que requieren una acción de la organización hoy. */
  readonly conAjustes = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'AjustesSolicitados'),
  );

  readonly borradores = computed(
    () => this.festivales().filter(festival => estadoDelFestival(festival.estado) === 'Borrador'),
  );

  constructor() {
    effect(() => {
      const id = this.organizacionId();
      if (!id) return;
      this.cargarFestivales(id);
      this.cargarPerfil(id);
    });
  }

  private cargarFestivales(organizacionId: string): void {
    this.cargandoFestivales.set(true);
    this.api.obtenerFestivales(organizacionId).subscribe({
      next: festivales => {
        this.cargandoFestivales.set(false);
        this.festivales.set(festivales);
      },
      error: () => this.cargandoFestivales.set(false),
    });
  }

  /**
   * Lee el perfil para saber qué falta.
   *
   * SI FALLA, NO SE INVITA A NADA. Dejar el perfil en `null` hace que la invitación no se pinte, y
   * eso es lo correcto: un cartel levantado por un fallo de red diría que falta información que
   * quizá ya está puesta, y mandaría a rellenar campos llenos.
   */
  private cargarPerfil(organizacionId: string): void {
    this.api.obtenerPerfil(organizacionId).subscribe({
      next: perfil => this.perfil.set(perfil),
      error: () => this.perfil.set(null),
    });
  }

}
