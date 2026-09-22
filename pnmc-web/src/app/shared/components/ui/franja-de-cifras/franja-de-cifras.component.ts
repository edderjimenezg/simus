import { formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * Una cifra de un tablero.
 *
 * `cifra` admite un número —se escribe con separador de miles en es-CO—, un texto —«Disponible»,
 * que se dibuja más pequeño porque no es un número— o `null`, que se escribe como raya: no se
 * inventa un cero cuando no hay dato.
 */
export interface CifraDeTablero {
  readonly id: string;
  readonly cifra: number | string | null;
  /** Lo que la cifra cuenta: «Organizaciones», «Festivales publicados». */
  readonly rotulo: string;
  /** El alcance, si hace falta decirlo: «Todos los estados», «Lectura pública vigente». */
  readonly detalle?: string;
  /**
   * El tono. Sin tono, un cero se apaga solo —no es una alarma, es que no hay nada— y el resto va
   * en el color de la consola. `correcto` y `aviso` son para lecturas de estado, no para conteos.
   */
  readonly tono?: 'correcto' | 'aviso';
  /** Si al pulsarla se va a algún sitio: sale como botón y emite su `id`. */
  readonly accionable?: boolean;
}

/**
 * La franja de cifras de un tablero: la cifra protagonista, el rótulo debajo y, si hace falta, el
 * alcance. Separadas por espacio, no por marcos ni tarjetas.
 *
 * <b>ERA TRES DIBUJOS DEL MISMO BLOQUE.</b> El Resumen operativo la tenía con clases propias en su
 * hoja; Salud del sistema la componía con utilidades sueltas y tres divisores; Análisis metía cada
 * indicador en su tarjeta con borde y sombra. Tres tableros de la misma consola, tres franjas
 * distintas. Desde los tres usan esta pieza, que fija el aspecto —los
 * estilos viven en `styles.css`, `.franja-de-cifras`— y deja al tablero solo los datos.
 *
 * <b>UNA CIFRA ACCIONABLE ES UN BOTON</b>, no un texto con cursor: es la regla del proyecto de que una
 * acción parece una acción. El Resumen las usa para ir a Organizaciones, a Usuarios y a la bandeja.
 */
@Component({
  selector: 'app-franja-de-cifras',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [':host { display: block; }'],
  template: `
    <ul class="franja-de-cifras" [class.con-filete]="conFilete" data-franja-de-cifras>
      @for (dato of cifras; track dato.id) {
        <li class="contents">
          @if (dato.accionable) {
            <button type="button" class="franja-de-cifras__dato" [attr.data-cifra]="dato.id" (click)="elegir.emit(dato.id)">
              <strong class="franja-de-cifras__cifra" [class]="clasesDe(dato)">{{ textoDe(dato) }}</strong>
              <span class="franja-de-cifras__rotulo">{{ dato.rotulo }}</span>
              @if (dato.detalle) { <span class="franja-de-cifras__detalle">{{ dato.detalle }}</span> }
            </button>
          } @else {
            <div class="franja-de-cifras__dato" [attr.data-cifra]="dato.id">
              <strong class="franja-de-cifras__cifra" [class]="clasesDe(dato)">{{ textoDe(dato) }}</strong>
              <span class="franja-de-cifras__rotulo">{{ dato.rotulo }}</span>
              @if (dato.detalle) { <span class="franja-de-cifras__detalle">{{ dato.detalle }}</span> }
            </div>
          }
        </li>
      }
    </ul>
  `,
})
export class FranjaDeCifrasComponent {
  @Input({ required: true }) cifras: readonly CifraDeTablero[] = [];
  /** Con una línea arriba que la despega del bloque anterior. Sin ella cuando abre la pantalla. */
  @Input() conFilete = true;
  @Output() elegir = new EventEmitter<string>();

  textoDe(dato: CifraDeTablero): string {
    if (dato.cifra === null || dato.cifra === undefined) { return '—'; }
    return typeof dato.cifra === 'number' ? formatNumber(dato.cifra, 'es-CO') : dato.cifra;
  }

  clasesDe(dato: CifraDeTablero): string {
    const clases = ['franja-de-cifras__cifra'];
    if (typeof dato.cifra === 'string') { clases.push('es-texto'); }
    if (dato.tono) { clases.push('es-' + dato.tono); }
    else if (dato.cifra === 0) { clases.push('es-cero'); }
    return clases.join(' ');
  }
}
