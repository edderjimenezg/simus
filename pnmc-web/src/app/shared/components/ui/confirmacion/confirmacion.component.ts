import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, output, signal } from '@angular/core';

import { DialogoDirective } from '../../../directives/dialogo.directive';
import { BotonComponent } from '../boton/boton.component';

/**
 * Confirmar algo que no se deshace, en el lenguaje de la consola.
 *
 * <b>POR QUE EXISTE, CON LO QUE SE CONTO.</b> El barrido encontró
 * <b>ocho `window.confirm` y dos `window.prompt`</b> repartidos por cinco pantallas, y otras cuatro
 * pantallas que ya habían escrito su propio diálogo de confirmación a mano. Catorce sitios, dos
 * lenguajes: el del sistema operativo —tipografía ajena, botones «Aceptar/Cancelar» que nadie eligió,
 * sin foco atrapado, bloqueable por el navegador sin aviso y fuera del alcance de cualquier prueba— y
 * el del proyecto, reescrito distinto cada vez.
 *
 * <b>LO QUE UNA CONFIRMACION TIENE QUE DECIR</b>, y que un `window.confirm` no puede:
 *
 * <list type="number">
 *   <item><b>Qué va a pasar</b>, en el título, con el verbo de la acción.</item>
 *   <item><b>Qué consecuencia tiene</b> —qué se pierde, qué queda, si se puede revertir— en prosa.</item>
 *   <item><b>Por qué</b>, cuando la decisión viaja al historial de otra persona: entonces el motivo
 *   es un campo del diálogo y no un segundo `window.prompt` encadenado al primero.</item>
 *   <item><b>Qué dijo el servidor</b> si se negó, sin cerrar el diálogo ni perder lo escrito.</item>
 * </list>
 *
 * <b>NO DECIDE CUANDO SE ABRE.</b> Eso lo sigue haciendo la pantalla con su `@if`, igual que el
 * resto de diálogos del proyecto: así el ciclo de vida de la directiva `appDialogo` —foco dentro,
 * foco retenido, Esc, fondo bloqueado— coincide exactamente con el del diálogo.
 *
 * @example
 * &#64;if (aDesactivar(); as usuario) {
 *   <app-confirmacion
 *     titulo="Desactivar la cuenta"
 *     [detalle]="usuario.email + ' perderá el acceso administrativo en cuanto confirmes.'"
 *     accion="Desactivar"
 *     tono="destructiva"
 *     (confirmar)="desactivarDeVerdad(usuario)"
 *     (cancelar)="aDesactivar.set(null)" />
 * }
 */
@Component({
  selector: 'app-confirmacion',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DialogoDirective, BotonComponent],
  template: `
    <div class="dialogo-velo" [class.dialogo-velo--encima]="encima()" role="dialog" aria-modal="true"
      [attr.aria-labelledby]="idDelTitulo" appDialogo (cerrar)="cancelar.emit()">
      <form class="dialogo dialogo--hoja confirmacion" (submit)="aceptar($event)">
        <h3 [id]="idDelTitulo" class="dialogo__titulo">{{ titulo() }}</h3>

        @if (detalle()) { <p class="confirmacion__detalle">{{ detalle() }}</p> }

        <!-- Lo que la pantalla quiera añadir: la fila afectada, una lista de lo que se pierde. -->
        <ng-content />

        @if (pideMotivo()) {
          <label class="campo confirmacion__motivo" [attr.for]="idDelMotivo">
            <span class="campo__rotulo">{{ rotuloDelMotivo() }}</span>
            <textarea [id]="idDelMotivo" rows="3" [attr.required]="motivoObligatorio() ? '' : null"
              [attr.aria-describedby]="ayudaDelMotivo() ? idDeLaAyuda : null"
              [value]="motivo()" (input)="motivo.set($any($event.target).value)"></textarea>
            @if (ayudaDelMotivo()) { <small [id]="idDeLaAyuda" class="campo__ayuda">{{ ayudaDelMotivo() }}</small> }
          </label>
        }

        @if (error()) { <p role="alert" class="confirmacion__error">{{ error() }}</p> }

        <div class="dialogo__acciones">
          <app-boton importancia="secundaria" (accion)="cancelar.emit()">{{ cancelacion() }}</app-boton>
          <app-boton tipo="submit" [importancia]="tono()" [ocupado]="ocupado()" [deshabilitado]="faltaElMotivo()"
            [identificador]="identificador()">{{ accion() }}</app-boton>
        </div>
      </form>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .confirmacion__detalle { margin: .45rem 0 0; color: var(--color-prosa); font-size: var(--text-cuerpo); line-height: 1.55; }
    .confirmacion__motivo { margin-top: 1.1rem; }
    .confirmacion__error { margin: .9rem 0 0; color: #b42318; font-size: var(--text-dato); font-weight: 700; }
  `],
})
export class ConfirmacionComponent {
  /** El verbo de lo que va a pasar: «Eliminar la cuenta», no «¿Estás seguro?». */
  readonly titulo = input.required<string>();

  /** La consecuencia en prosa: qué se pierde, qué queda y si puede revertirse. */
  readonly detalle = input('');

  /** El texto del botón que confirma. Repite el verbo del título, nunca «Aceptar». */
  readonly accion = input('Continuar');

  /** El texto del botón que se va. «Cancelar» salvo que la pantalla tenga una palabra mejor. */
  readonly cancelacion = input('Cancelar');

  /**
   * La importancia del botón que confirma.
   *
   * POR OMISION ES DESTRUCTIVA porque una confirmación existe justamente cuando algo se pierde. Lo
   * que solo abre un camino —aprobar, publicar— pasa `confirmar` y se distingue de lo que borra.
   */
  readonly tono = input<'destructiva' | 'confirmar' | 'principal'>('destructiva');

  /** Pide un motivo antes de dejar confirmar. */
  readonly pideMotivo = input(false, { transform: booleanAttribute });
  readonly rotuloDelMotivo = input('Por qué');
  readonly ayudaDelMotivo = input('');

  /** Si el motivo es obligatorio. Lo es salvo que la pantalla diga lo contrario. */
  readonly motivoObligatorio = input(true, { transform: booleanAttribute });

  /** Lo que dijo el servidor al negarse. Se muestra sin cerrar ni vaciar lo escrito. */
  readonly error = input('');

  /** Hay una petición en curso: el botón espera y no vuelve a emitir. */
  readonly ocupado = input(false, { transform: booleanAttribute });

  /** Se abre sobre otra capa —una ficha, un cajón— y tiene que ir por encima. */
  readonly encima = input(false, { transform: booleanAttribute });

  /** El `data-testid` del botón que confirma, para que la prueba apunte al control real. */
  readonly identificador = input<string | null>(null);

  /** Lo confirmado, con el motivo escrito —cadena vacía si no se pedía—. */
  readonly confirmar = output<string>();
  readonly cancelar = output<void>();

  readonly motivo = signal('');

  /**
   * IDENTIFICADORES UNICOS POR INSTANCIA. Dos confirmaciones en la misma página —una en la lista y
   * otra dentro de la ficha abierta— compartirían el `for` del rótulo y el `aria-labelledby`, y el
   * lector de pantalla leería el título del diálogo equivocado.
   */
  private static contador = 0;
  private readonly sufijo = `${++ConfirmacionComponent.contador}`;
  readonly idDelTitulo = `confirmacion-titulo-${this.sufijo}`;
  readonly idDelMotivo = `confirmacion-motivo-${this.sufijo}`;
  readonly idDeLaAyuda = `confirmacion-ayuda-${this.sufijo}`;

  readonly faltaElMotivo = computed(() => this.pideMotivo() && this.motivoObligatorio() && !this.motivo().trim());

  aceptar(evento: Event): void {
    evento.preventDefault();
    if (this.ocupado() || this.faltaElMotivo()) { return; }
    this.confirmar.emit(this.motivo().trim());
  }
}
