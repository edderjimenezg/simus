import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, model, output } from '@angular/core';

import { DialogoDirective } from '../../../directives/dialogo.directive';
import { BotonComponent } from '../boton/boton.component';
import { IndicadorDePasosComponent, PasoDelIndicador } from '../indicador-de-pasos/indicador-de-pasos.component';

/**
 * El armazón de un alta por pasos.
 *
 * <b>POR QUE EXISTE.</b> El alta de un Festival era un asistente de cinco pasos y la de un Mercado,
 * trece campos de una vez en un solo desplazamiento. Dos formas de registrar dos procesos que el
 * proyecto trata igual, y la dirección de producto ya lo había pedido dos veces: «la navegación,
 * edición, publicación, etc. de un mercado debe ser igual en cuanto sea posible a la de un
 * festival». Esto es lo común de las dos —velo, hoja, cabecera, indicador, error, pie— en un solo
 * sitio, para que la tercera no vuelva a escribirse distinta.
 *
 * <b>LOS CAMPOS NO ENTRAN AQUI.</b> Cada alta proyecta su paso con su propio `&#64;if (paso() === n)`:
 * son sus campos, sus catálogos y sus validaciones. Lo que se comparte es el recorrido, no el
 * contenido.
 *
 * <b>EL PASO ES UN MODELO DE DOS VIAS</b> porque el indicador deja volver a un paso ya recorrido y
 * la pantalla necesita saber en cuál está para pintar el suyo.
 */
@Component({
  selector: 'app-asistente-de-alta',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DialogoDirective, BotonComponent, IndicadorDePasosComponent],
  template: `
    <div class="dialogo-velo dialogo-velo--con-desplazamiento" role="dialog" aria-modal="true"
      [attr.aria-labelledby]="idDelTitulo" appDialogo (cerrar)="cancelar.emit()">
      <div class="dialogo dialogo--hoja asistente">
        <div class="asistente__cabecera">
          <div>
            @if (antetitulo()) { <p class="dialogo__contexto">{{ antetitulo() }}</p> }
            <h3 [id]="idDelTitulo" class="dialogo__titulo">{{ titulo() }}</h3>
            @if (proposito()) { <p class="asistente__proposito">{{ proposito() }}</p> }
          </div>
          <app-boton importancia="terciaria" tamano="menudo" etiqueta="Cerrar sin registrar"
            (accion)="cancelar.emit()">Cerrar</app-boton>
        </div>

        <app-indicador-de-pasos class="asistente__pasos" [pasos]="pasos()" [pasoActivo]="paso()"
          [etiqueta]="'Pasos de ' + titulo().toLowerCase()" (irAlPaso)="paso.set($event)" />

        @if (error()) { <p class="asistente__error" role="alert">{{ error() }}</p> }

        <div class="asistente__cuerpo">
          <ng-content />
        </div>

        <!--
          LO QUE FALTA SE DICE EN EL PASO DE REVISION, no al pulsar el botón. Enterarse de que falta
          un campo cuando ya se creía terminado es lo que convierte un alta en una sucesión de
          intentos; y el botón apagado sin decir por qué es la otra mitad del mismo defecto.
        -->
        @if (enElUltimoPaso() && loQueFalta().length > 0) {
          <p class="asistente__falta" role="status">Falta {{ loQueFalta().join(', ') }}.</p>
        }

        <div class="dialogo__acciones">
          <app-boton importancia="secundaria" (accion)="cancelar.emit()">Cancelar</app-boton>
          @if (paso() > 1) {
            <app-boton importancia="secundaria" (accion)="paso.set(paso() - 1)">Atrás</app-boton>
          }
          @if (!enElUltimoPaso()) {
            <app-boton importancia="principal" (accion)="paso.set(paso() + 1)">Siguiente</app-boton>
          } @else {
            <app-boton importancia="principal" [ocupado]="guardando()" [deshabilitado]="loQueFalta().length > 0"
              [identificador]="identificador()" (accion)="registrar.emit()">{{ verbo() }}</app-boton>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .asistente { max-width: 44rem; }
    .asistente__cabecera { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .asistente__proposito { margin: .5rem 0 0; max-width: 34rem; color: var(--color-prosa); font-size: var(--text-dato); line-height: 1.55; }
    .asistente__pasos { display: block; margin-top: 1.25rem; }
    .asistente__error { margin: 1.25rem 0 0; border: 1px solid #fcd9a4; border-radius: .75rem; background: #fff8ec; padding: .75rem 1rem; color: #8a5300; font-size: var(--text-dato); }
    .asistente__cuerpo { display: grid; gap: 1rem; margin-top: 1.25rem; }
    .asistente__falta { margin: 1rem 0 0; border: 1px solid #fcd9a4; border-radius: .75rem; background: #fff8ec; padding: .75rem 1rem; color: #8a5300; font-size: var(--text-dato); }
  `],
})
export class AsistenteDeAltaComponent {
  /** La familia a la que pertenece lo que se registra: «Ecosistema musical». */
  readonly antetitulo = input('');

  /** El verbo y el registro: «Registrar Festival». */
  readonly titulo = input.required<string>();

  /** Para qué sirve este alta y qué la distingue de la del espacio externo. */
  readonly proposito = input('');

  readonly pasos = input.required<readonly PasoDelIndicador[]>();
  readonly paso = model(1);

  /** Lo que contestó el servidor al negarse. */
  readonly error = input<string | null>(null);

  /** Lo que todavía falta para poder registrar, nombrado campo a campo. */
  readonly loQueFalta = input<readonly string[]>([]);

  readonly guardando = input(false, { transform: booleanAttribute });

  /** El verbo del botón que crea: «Registrar Festival», «Registrar mercado». */
  readonly verbo = input('Registrar');

  /** El `data-testid` del botón que crea. */
  readonly identificador = input<string | null>(null);

  readonly cancelar = output<void>();
  readonly registrar = output<void>();

  /**
   * IDENTIFICADOR UNICO POR INSTANCIA: el alta de una organización se abre DESDE el alta de un
   * Festival, así que hay dos asistentes vivos a la vez y `aria-labelledby` apuntaría al de fuera.
   */
  private static contador = 0;
  readonly idDelTitulo = `asistente-titulo-${++AsistenteDeAltaComponent.contador}`;

  readonly enElUltimoPaso = computed(() => this.paso() >= this.pasos().length);
}
