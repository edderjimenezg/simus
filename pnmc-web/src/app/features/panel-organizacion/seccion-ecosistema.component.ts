import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FalloDelServidor, FestivalDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';
import { SeccionFestivalesComponent } from './seccion-festivales.component';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { estadoDelFestival } from './estados-del-festival';
import { ConfirmacionComponent } from '../../shared/components/ui/confirmacion/confirmacion.component';

/**
 * Lista de Festivales de la organización, dentro de `/gestion/procesos/festivales`.
 *
 * La elección de tipo de proceso vive en `/gestion/procesos`. Esta ruta no vuelve a
 * decidir entre procesos ni conserva parámetros de URL para ello: solo presenta el
 * único flujo externo completo disponible hoy, el de Festivales.
 */
@Component({
  selector: 'app-seccion-ecosistema',
  standalone: true,
  imports: [CommonModule, RouterLink, SeccionFestivalesComponent, ConfirmacionComponent],
  templateUrl: './seccion-ecosistema.component.html',
})
export class SeccionEcosistemaComponent implements OnInit {
  private readonly api = inject(PanelOrganizacionApi);
  private readonly router = inject(Router);
  private readonly store = inject(PanelOrganizacionStore);

  readonly organizacionId = this.store.organizacionId;
  readonly festivales = signal<FestivalDeLaOrganizacion[]>([]);
  readonly cargandoFestivales = signal(false);
  readonly errorFestivales = signal('');
  readonly errorAccionFestival = signal('');
  readonly mensajeFestivales = signal('');
  readonly festivalOcupado = signal(false);
  readonly pendientes = computed(() => this.festivales().filter(festival => {
    const estado = estadoDelFestival(festival.estado);
    return estado === 'EnRevision' || estado === 'AjustesSolicitados';
  }).length);
  readonly borradores = computed(() => this.festivales().filter(festival =>
    estadoDelFestival(festival.estado) === 'Borrador').length);

  ngOnInit(): void {
    this.cargarFestivales();
  }

  cargarFestivales(organizacionId = this.organizacionId()): void {
    if (!organizacionId) return;
    this.festivales.set([]);
    this.errorFestivales.set('');
    this.cargandoFestivales.set(true);
    this.api.obtenerFestivales(organizacionId).subscribe({
      next: festivales => {
        this.cargandoFestivales.set(false);
        this.festivales.set(festivales);
      },
      error: (fallo: FalloDelServidor) => {
        this.cargandoFestivales.set(false);
        this.errorFestivales.set(fallo?.message ?? 'No fue posible consultar los Festivales de la organización');
      },
    });
  }

  alEnviarARevision(festival: FestivalDeLaOrganizacion): void {
    if (this.festivalOcupado()) return;
    this.festivalOcupado.set(true);
    this.errorFestivales.set('');
    this.errorAccionFestival.set('');
    this.mensajeFestivales.set('');
    this.api.enviarFestivalARevision(festival.id).subscribe({
      next: () => {
        this.festivalOcupado.set(false);
        this.mensajeFestivales.set(`«${festival.nombre}» quedó en revisión del equipo del PNMC.`);
        this.cargarFestivales();
      },
      error: (fallo: FalloDelServidor) => {
        this.festivalOcupado.set(false);
        this.errorAccionFestival.set(fallo?.message ?? 'No fue posible enviar el Festival a revisión');
      },
    });
  }

  alEnviarPropuesta(festival: FestivalDeLaOrganizacion): void {
    if (this.festivalOcupado()) return;
    this.festivalOcupado.set(true);
    this.errorFestivales.set('');
    this.errorAccionFestival.set('');
    this.mensajeFestivales.set('');
    this.api.enviarPropuestaARevision(festival.id).subscribe({
      next: () => {
        this.festivalOcupado.set(false);
        this.mensajeFestivales.set(`La propuesta de cambios de «${festival.nombre}» quedó en revisión.`);
        this.cargarFestivales();
      },
      error: (fallo: FalloDelServidor) => {
        this.festivalOcupado.set(false);
        this.errorAccionFestival.set(fallo?.message ?? 'No fue posible enviar la propuesta a revisión');
      },
    });
  }

  abrirFichaDelFestival(festival: FestivalDeLaOrganizacion): void {
    this.router.navigate(['/gestion/procesos/festivales', festival.id]);
  }

  editarFichaDelFestival(festival: FestivalDeLaOrganizacion): void {
    if (estadoDelFestival(festival.estado) === 'AjustesSolicitados') {
      this.router.navigate(['/gestion/procesos/festivales', festival.id], {
        queryParams: { seccion: 'informacion', ajustes: '1' },
      });
      return;
    }
    this.router.navigate(['/gestion/procesos/festivales', festival.id], {
      queryParams: { seccion: 'informacion', editar: '1' },
    });
  }

  verEdicionesDelFestival(festival: FestivalDeLaOrganizacion): void {
    this.router.navigate(['/gestion/procesos/festivales', festival.id], {
      queryParams: { seccion: 'ediciones' },
    });
  }

  /**
   * Lo que espera confirmación.
   *
   * <b>SE PREGUNTA EN EL DIALOGO DEL PROYECTO.</b> Retirar usaba `window.confirm` y pedir la
   * eliminación de lo publicado usaba `window.prompt`: la justificación que viaja a la decisión
   * institucional se escribía en una caja de texto del sistema operativo, de una sola línea, sin
   * decir cuántos caracteres caben ni quién la va a leer, y el navegador podía bloquearla sin aviso.
   */
  readonly confirmacion = signal<{ que: 'retirar' | 'solicitar-retiro'; festival: FestivalDeLaOrganizacion } | null>(null);

  /** Si lo que se retira estaba en revisión: cambia el verbo y la consecuencia. */
  readonly enRevision = computed(() => {
    const pendiente = this.confirmacion();
    return !!pendiente && estadoDelFestival(pendiente.festival.estado) === 'EnRevision';
  });

  readonly tituloDeLaConfirmacion = computed(() => {
    const pendiente = this.confirmacion();
    if (!pendiente) return '';
    if (pendiente.que === 'solicitar-retiro') return 'Solicitar la eliminación';
    return this.enRevision() ? 'Retirar de revisión' : 'Eliminar el borrador';
  });

  readonly detalleDeLaConfirmacion = computed(() => {
    const pendiente = this.confirmacion();
    if (!pendiente) return '';
    const nombre = pendiente.festival.nombre;
    if (pendiente.que === 'solicitar-retiro') {
      return `«${nombre}» seguirá publicado hasta que el equipo del PNMC decida. Tu solicitud y su motivo ` +
        'quedan en el historial del Festival.';
    }
    if (this.enRevision()) {
      return `«${nombre}» dejará de estar disponible para la decisión institucional y volverá a borrador. ` +
        'Conserva su historial de auditoría.';
    }
    return `«${nombre}» todavía no está publicado: se retira del panel y conserva su historial de auditoría.`;
  });

  readonly accionDeLaConfirmacion = computed(() => {
    const pendiente = this.confirmacion();
    if (!pendiente) return '';
    if (pendiente.que === 'solicitar-retiro') return 'Enviar la solicitud';
    return this.enRevision() ? 'Retirar de revisión' : 'Eliminar el borrador';
  });

  cerrarLaConfirmacion(): void { this.confirmacion.set(null); }

  confirmarLaDecision(motivo: string): void {
    const pendiente = this.confirmacion();
    if (!pendiente) return;
    this.confirmacion.set(null);
    if (pendiente.que === 'retirar') { this.retirarDeVerdad(pendiente.festival); return; }
    this.solicitarRetiroDeVerdad(pendiente.festival, motivo);
  }

  retirarFestival(festival: FestivalDeLaOrganizacion): void {
    if (this.festivalOcupado()) return;
    this.confirmacion.set({ que: 'retirar', festival });
  }

  private retirarDeVerdad(festival: FestivalDeLaOrganizacion): void {
    const enRevision = estadoDelFestival(festival.estado) === 'EnRevision';
    this.festivalOcupado.set(true);
    this.errorAccionFestival.set('');
    this.mensajeFestivales.set('');
    this.api.retirarFestival(festival.id).subscribe({
      next: () => {
        this.festivalOcupado.set(false);
        this.mensajeFestivales.set(enRevision
          ? `«${festival.nombre}» fue retirado de revisión y volvió a borrador.`
          : `El borrador «${festival.nombre}» fue eliminado.`);
        this.cargarFestivales();
      },
      error: (fallo: FalloDelServidor) => {
        this.festivalOcupado.set(false);
        this.errorAccionFestival.set(fallo?.message ?? 'No fue posible retirar el Festival');
      },
    });
  }

  solicitarRetiroFestival(festival: FestivalDeLaOrganizacion): void {
    if (this.festivalOcupado()) return;
    this.confirmacion.set({ que: 'solicitar-retiro', festival });
  }

  private solicitarRetiroDeVerdad(festival: FestivalDeLaOrganizacion, justificacion: string): void {
    if (!justificacion) return;
    this.festivalOcupado.set(true);
    this.errorAccionFestival.set('');
    this.mensajeFestivales.set('');
    this.api.solicitarRetiroFestival(festival.id, justificacion).subscribe({
      next: () => {
        this.festivalOcupado.set(false);
        this.mensajeFestivales.set(`La solicitud de eliminación de «${festival.nombre}» fue enviada a revisión institucional.`);
        this.cargarFestivales();
      },
      error: (fallo: FalloDelServidor) => {
        this.festivalOcupado.set(false);
        this.errorAccionFestival.set(fallo?.message ?? 'No fue posible enviar la solicitud de eliminación');
      },
    });
  }
}
