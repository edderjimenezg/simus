import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';

import { DialogoDirective } from '../../../directives/dialogo.directive';
import { BotonComponent } from '../boton/boton.component';

/**
 * Un diálogo que pide un dato.
 *
 * <b>ES EL TERCERO DE LOS TRES.</b> La consola abre ventanas para tres cosas distintas y cada una
 * tiene ya su pieza: <b>confirmar</b> algo que no se deshace es `app-confirmacion`; <b>registrar</b>
 * un proceso es `app-asistente-de-alta`; y <b>pedir un dato</b> —el nombre de una categoría, el
 * asunto de un mensaje, qué dirección se comprobó, qué acto se ejecuta sobre una organización— era
 * lo único que seguía escribiéndose a mano en cada pantalla.
 *
 * <b>LO QUE DIVERGIA, MEDIDO EL 17 DE SEPTIEMBRE DE 2026:</b> seis firmas distintas de pie entre los
 * diálogos de hoja de la consola —tres con `.dialogo__acciones`, tres con su propio `flex`—, cada
 * uno con su `aria-labelledby` inventado a mano, y <b>ninguno con un sitio donde enseñar la negativa
 * del servidor</b>: cuando el guardado fallaba, el mensaje aparecía en una franja de la pantalla de
 * detrás, que el velo oscurece.
 *
 * <b>EL VERBO NO CAMBIA AL GUARDAR.</b> «Guardar» no se convierte en «Guardando…»: el botón del
 * proyecto ya muestra su propio indicador sin cambiar de ancho, y cambiar el texto mueve el botón
 * justo cuando la persona acaba de apuntar ahí.
 *
 * @example
 * &#64;if (formulario(); as f) {
 *   <app-dialogo-de-formulario titulo="Nueva categoría" verbo="Guardar"
 *     [guardando]="guardando()" [error]="error()"
 *     (guardar)="guardar()" (cerrar)="cerrarFormulario()">
 *     <label class="campo"><span>Nombre</span><input [ngModel]="f.nombre" … /></label>
 *   </app-dialogo-de-formulario>
 * }
 */
@Component({
  selector: 'app-dialogo-de-formulario',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DialogoDirective, BotonComponent],
  template: `
    <div class="dialogo-velo" [class.dialogo-velo--encima]="encima()" role="dialog" aria-modal="true"
      [attr.aria-labelledby]="idDelTitulo" appDialogo (cerrar)="cerrar.emit()">
      <form class="dialogo dialogo--hoja formulario" (submit)="enviar($event)">
        <h3 [id]="idDelTitulo" class="dialogo__titulo">{{ titulo() }}</h3>
        @if (proposito()) { <p class="formulario__proposito">{{ proposito() }}</p> }

        <div class="formulario__campos">
          <ng-content />
        </div>

        @if (error()) { <p class="formulario__error" role="alert">{{ error() }}</p> }

        <div class="dialogo__acciones">
          <app-boton importancia="secundaria" (accion)="cerrar.emit()">{{ cancelacion() }}</app-boton>
          <app-boton tipo="submit" [importancia]="tono()" [ocupado]="guardando()"
            [deshabilitado]="!puedeGuardar()" [identificador]="identificador()">{{ verbo() }}</app-boton>
        </div>
      </form>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .formulario__proposito { margin: .5rem 0 0; color: var(--color-prosa); font-size: var(--text-dato); line-height: 1.55; }
    .formulario__campos { display: grid; gap: 1rem; margin-top: 1.25rem; }
    .formulario__error { margin: 1rem 0 0; color: #b42318; font-size: var(--text-dato); font-weight: 700; }
  `],
})
export class DialogoDeFormularioComponent {
  /** Qué se está pidiendo: «Nueva categoría», «Escribirle a la organización». */
  readonly titulo = input.required<string>();

  /** Para qué sirve y qué NO hace. Es donde se dice la frontera, antes de pulsar. */
  readonly proposito = input('');

  /** El verbo que guarda. No cambia mientras se guarda: el botón ya lo indica. */
  readonly verbo = input('Guardar');
  readonly cancelacion = input('Cancelar');
  readonly tono = input<'principal' | 'confirmar' | 'destructiva'>('principal');

  readonly guardando = input(false, { transform: booleanAttribute });

  /** Falso mientras falte algo que la propia pantalla sabe comprobar. */
  readonly puedeGuardar = input(true, { transform: booleanAttribute });

  /** La negativa del servidor, DENTRO del diálogo: detrás está el velo. */
  readonly error = input<string | null>(null);

  /** Se abre sobre otra capa y tiene que ir por encima. */
  readonly encima = input(false, { transform: booleanAttribute });

  readonly identificador = input<string | null>(null);

  readonly guardar = output<void>();
  readonly cerrar = output<void>();

  /** Único por instancia: puede haber dos diálogos vivos, y el rótulo tiene que apuntar al suyo. */
  private static contador = 0;
  readonly idDelTitulo = `formulario-titulo-${++DialogoDeFormularioComponent.contador}`;

  enviar(evento: Event): void {
    evento.preventDefault();
    if (this.guardando() || !this.puedeGuardar()) { return; }
    this.guardar.emit();
  }
}
