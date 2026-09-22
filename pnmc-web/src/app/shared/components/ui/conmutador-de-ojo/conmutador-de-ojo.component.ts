import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { LucideEye, LucideEyeOff } from '@lucide/angular';
import { BotonComponent } from '../boton/boton.component';

/**
 * El conmutador que enseña u oculta un grupo de registros dentro de una lista.
 *
 * <b>POR QUE ES UN OJO Y NO UN BOTON MAS.</b> En la barra de una lista conviven dos clases de
 * control que se parecían demasiado: las posiciones EXCLUYENTES de una misma pregunta —Todos,
 * Institucionales, Externos— de las que solo una puede estar puesta, y los interruptores, que se
 * combinan con cualquiera de ellas. Con la misma forma, el interruptor se leía como una cuarta
 * opción del grupo. Quedó definido para la Agenda —«con
 * un icono de vista u ojo que cambie al cliquear, que se sienta diferente»— y lo extendió al resto
 * el mismo día: «lo del icono de ojo también a donde haya opción de ocultar algún estado, como
 * ocultar borradores en festivales».
 *
 * <b>EL ICONO DICE EL ESTADO Y LA PALABRA DICE EL SUJETO.</b> El ojo abierto cuando ese grupo se
 * ve, tachado cuando no; y al lado, de qué grupo se habla. Sin la palabra, el ojo obliga a posarse
 * encima para saber si esconde archivados, borradores o eventos pasados, y en pantalla táctil no
 * hay dónde posarse. Sin el cambio de icono, hay que adivinar si lo que se ve es el estado actual o
 * lo que va a pasar al pulsar.
 */
@Component({
  selector: 'app-conmutador-de-ojo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BotonComponent, LucideEye, LucideEyeOff],
  styles: [':host { display: contents; }'],
  template: `
    <app-boton importancia="terciaria" tamano="menudo" [pulsado]="visible()"
      [identificador]="identificador()"
      [etiqueta]="anuncio()"
      (accion)="alternar.emit(!visible())">
      @if (visible()) {
        <svg lucideEye [size]="16" aria-hidden="true"></svg>
      } @else {
        <svg lucideEyeOff [size]="16" aria-hidden="true"></svg>
      }
      {{ grupo() }}
    </app-boton>
  `,
})
export class ConmutadorDeOjoComponent {
  /** Si ese grupo se está viendo ahora mismo. */
  readonly visible = input.required<boolean>();

  /** El grupo, en una palabra y en plural: «Archivados», «Borradores». Es lo que se lee al lado. */
  readonly grupo = input.required<string>();

  /**
   * De qué son esos registros, para la frase completa: «los eventos archivados».
   *
   * SE DICE ENTERA EN EL NOMBRE ACCESIBLE aunque en pantalla solo quepa una palabra: quien escucha
   * la consola no tiene la columna de al lado para deducir de qué lista se habla.
   */
  readonly deQue = input.required<string>();

  readonly identificador = input<string | null>(null);

  /** El estado pedido: cierto para enseñarlos, falso para ocultarlos. */
  readonly alternar = output<boolean>();

  protected readonly anuncio = computed(() =>
    this.visible()
      ? `Ocultar ${this.deQue()}`
      : `Mostrar también ${this.deQue()}`);
}
