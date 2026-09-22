import { Component, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { DialogoDirective } from '../../shared/directives/dialogo.directive';
import { NgTemplateOutlet } from '@angular/common';
import { LucideX } from '@lucide/angular';
import { FormsModule } from '@angular/forms';
import { AdminService, AjustesDeEdicion } from '../../core/services/admin.service';
import { BotonComponent } from '../../shared/components/ui/boton/boton.component';
import { CAMPOS_DE_LA_EDICION, campoDeLaEdicion, seccionesDeLaEdicion } from './campos-de-la-edicion';

/** Una observación en construcción, tal como se manda al expediente de la Edición. */
interface NotaDeEdicion {
  seccionId: string;
  campoId: string;
  campoEtiqueta: string;
  valorObservado: string | null;
  nota: string;
}

/**
 * La revisión de una Edición: lo que el PNMC le pide corregir a la organización.
 *
 * <b>SE PIDE EL CAMPO CON UNA LISTA, NO ESCRIBIENDO SU IDENTIFICADOR.</b> Hasta el 14 de septiembre
 * de 2026 cada observación pedía tres cosas: «Identificador del campo», «Etiqueta visible» y la
 * instrucción. Es decir, le pedía a quien revisa que recordara de memoria el nombre interno de un
 * campo del formulario de la organización y que volviera a escribir su rótulo a mano —con lo que
 * dos observaciones sobre el mismo campo podían llegar con dos rótulos distintos—. Ahora se elige
 * el campo de la lista real de la Edición y de ahí salen el identificador, el rótulo y la sección.
 * Es lo que fija el proyecto: lo derivable no se pregunta, y un vocabulario controlado se captura
 * con una lista.
 *
 * <b>PUEDE VIVIR DENTRO DEL PANEL.</b> Con `comoPanel` se monta en la columna de la bandeja en vez
 * de flotar como diálogo, igual que la ficha de Festival en revisión: revisar un trámite no debería
 * tapar la lista de los demás.
 */
@Component({
  selector: 'app-admin-edition-review-panel',
  standalone: true,
  imports: [DialogoDirective, BotonComponent, FormsModule, LucideX, NgTemplateOutlet],
  templateUrl: './admin-edition-review-panel.component.html',
  styles: [`
    /* El mismo rótulo de sección que el resto de la consola administrativa. */
    .seccion-revision {
      margin: 0 0 .45rem;
      color: var(--color-seccion);
      font-size: var(--text-dato);
      font-weight: 900;
      letter-spacing: .08em;
      text-transform: uppercase;
    }
  `],
})
export class AdminEditionReviewPanelComponent implements OnChanges {
  @Input({ required: true }) edicionId!: string;
  @Input() nombre = '';

  /** Vivir dentro de una columna en vez de flotar como diálogo. */
  @Input() comoPanel = false;

  @Output() cerrar = new EventEmitter<void>();
  @Output() enviada = new EventEmitter<void>();

  private readonly api = inject(AdminService);

  readonly revision = signal<AjustesDeEdicion | null>(null);
  readonly error = signal('');
  readonly guardando = signal(false);

  general = '';
  notas: NotaDeEdicion[] = [];

  /** Las secciones de la Edición con sus campos, para el desplegable agrupado. */
  readonly secciones = seccionesDeLaEdicion();

  /** Cuántas observaciones tienen instrucción escrita: es lo que de verdad se enviaría. */
  readonly cuantasEscritas = computed(() => this.notas.filter(nota => nota.nota.trim()).length);

  ngOnChanges(): void {
    this.api.cargarAjustesInstitucionalesDeEdicion(this.edicionId).subscribe({
      next: recibida => {
        this.revision.set(recibida);
        this.general = recibida.observacionGeneral ?? '';
        this.notas = recibida.observaciones.map(observacion => ({ ...observacion }));
      },
      error: fallo => this.error.set(fallo?.message ?? 'No fue posible abrir la revisión.'),
    });
  }

  agregar(): void {
    this.notas.push({ seccionId: '', campoId: '', campoEtiqueta: '', valorObservado: null, nota: '' });
  }

  quitar(indice: number): void {
    this.notas.splice(indice, 1);
  }

  /**
   * Elegir el campo rellena por sí solo su rótulo y su sección.
   *
   * Se guardan los tres —y no solo el identificador— porque es lo que ya espera el servidor y lo
   * que la organización lee: su ficha muestra la observación por el rótulo.
   */
  elegirCampo(nota: NotaDeEdicion, campoId: string): void {
    const campo = campoDeLaEdicion(campoId);
    nota.campoId = campoId;
    nota.campoEtiqueta = campo?.campoEtiqueta ?? '';
    nota.seccionId = campo?.seccionId ?? '';
  }

  /** Un campo ya observado no vuelve a ofrecerse: dos notas sobre el mismo campo se pisan. */
  camposDisponibles(nota: NotaDeEdicion): typeof CAMPOS_DE_LA_EDICION {
    const tomados = new Set(this.notas.filter(otra => otra !== nota).map(otra => otra.campoId));
    return CAMPOS_DE_LA_EDICION.filter(campo => !tomados.has(campo.campoId));
  }

  estaDisponible(nota: NotaDeEdicion, campoId: string): boolean {
    return this.camposDisponibles(nota).some(campo => campo.campoId === campoId);
  }

  guardar(enviar: boolean): void {
    this.error.set('');
    this.guardando.set(true);
    const solicitud = {
      observacionGeneral: this.general || null,
      observaciones: this.notas
        .filter(nota => nota.nota.trim())
        .map(nota => ({
          ...nota,
          campoId: nota.campoId.trim(),
          campoEtiqueta: nota.campoEtiqueta.trim(),
          nota: nota.nota.trim(),
        })),
    };
    this.api.guardarAjustesInstitucionalesDeEdicion(this.edicionId, solicitud, enviar).subscribe({
      next: guardada => {
        this.guardando.set(false);
        this.revision.set(guardada);
        if (enviar) { this.enviada.emit(); }
      },
      error: fallo => {
        this.guardando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible guardar la revisión.');
      },
    });
  }
}
