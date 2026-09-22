import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Una posición del selector. */
export interface OpcionSegmentada {
  readonly id: string;
  readonly etiqueta: string;

  /**
   * Cuántos elementos hay detrás de esta posición, cuando el control filtra una lista.
   *
   * Es opcional a propósito: un selector de modo de vista -«Barras / Áreas»- no tiene nada que
   * contar, y ponerle un número al lado sería inventar un dato.
   */
  readonly conteo?: number;
}

/**
 * Elegir entre dos o tres opciones excluyentes, como un solo objeto con varias posiciones.
 *
 * <b>SUSTITUYE A LAS PILDORAS, Y ESO ES UNA NORMA DEL PROYECTO.</b> El 12 de septiembre de 2026 el
 * está definido de los controles de la vista de gráfico del mapa —«Barras / Áreas»,
 * «Territorios sonoros / Prácticas», «Barras / Unidades»—: «ese tipo de botones, así como una
 * píldora, no deben estar presentes en el diseño». Es la misma regla que ya regía para los estados,
 * extendida a los controles.
 *
 * <b>POR QUE LA PILDORA ESTABA MAL AQUI, Y NO SOLO POR GUSTO.</b> Cada opción se dibujaba como una
 * cápsula redondeada independiente, con su borde y su fondo, y la elegida rellena de morado. Un
 * borde alrededor de cada una dice «objeto separado», y no lo son: son posiciones de un mismo
 * control, y solo una puede estar puesta. Además, tres cápsulas sueltas junto a otras tres cápsulas
 * en la misma pantalla se leen como seis opciones de una sola lista. La forma tiene que decir lo
 * que la cosa es.
 *
 * <b>COMO SE MARCA LO ELEGIDO.</b> Con un subrayado y el color de la marca, nunca con un relleno:
 * un relleno morado convierte la opción otra vez en una cápsula, esta vez oscura, y además hunde el
 * contraste del texto. Es el mismo criterio con el que se rehízo la tira de lecturas de la vista de
 * gráfico, que marca lo elegido con una barra superior.
 *
 * <b>EL TAMAÑO SIGUE LA ESCALA DEL PROYECTO.</b> Estas posiciones se pintaban a 9,5 px en
 * versalitas, por debajo del suelo de 12 px que fija la escala tipográfica; se subieron a
 * `text-dato` con su altura de control mínima, al reutilizar el
 * control para filtrar la bandeja de Solicitudes.
 *
 * <b>SEMANTICA.</b> `role="group"` con `aria-pressed` en cada botón, que es lo que ya usaba el
 * marcado anterior: se conserva para no cambiar lo que anuncia el lector de pantalla al mismo
 * tiempo que cambia lo que se ve.
 */
@Component({
  selector: 'app-selector-segmentado',
  standalone: true,
  imports: [CommonModule],
  /*
    EL HOST DECLARA SU `display`, Y NO ES UN DETALLE.

    Sin declararlo, un componente de Angular es `display: inline`, y los márgenes verticales NO se
    aplican a una caja en línea. Como las utilidades `space-y-*` de Tailwind 4 ponen justamente un
    `margin-block-start` en los hermanos, el aire por debajo del selector desaparecía: medido el 15
    de septiembre de 2026 en Páginas y bloques, 9 px donde el contenedor pedía 24, y las casillas de
    cifras quedaban pegadas al subrayado de la pestaña activa.

    `block` y no `inline-block`: el selector siempre ocupa su propia fila o es hijo de un
    contenedor flex —la barra de lista—, donde un hijo en bloque se comporta igual.
  */
  styles: [':host { display: block; }'],
  template: `
    <div class="inline-flex flex-wrap" [class.border-b]="conFilete" [class.border-slate-200]="conFilete" role="group" [attr.aria-label]="etiquetaDelGrupo">
      @for (opcion of opciones; track opcion.id) {
        @let puesta = opcion.id === elegida;
        @let vacia = opcion.conteo === 0 && !puesta;
        <button
          type="button"
          (click)="elegir.emit(opcion.id)"
          [attr.aria-pressed]="puesta"
          [attr.data-opcion]="opcion.id"
          [attr.data-vacia]="vacia ? 'si' : null"
          [attr.data-filtro-organizacion]="atributoDeDatos === 'filtro-organizacion' ? opcion.id : null"
          [class]="'-mb-px inline-flex min-h-control cursor-pointer items-center gap-1.5 border-b-2 px-3 text-cuerpo font-bold transition-all ' + (
            puesta
              ? 'border-morado text-morado'
              : vacia
                ? 'border-transparent text-[color:var(--color-rotulo)] font-semibold hover:text-morado'
                : 'border-transparent text-[color:var(--color-prosa)] hover:text-morado'
          )"
        ><span>{{ opcion.etiqueta }}</span>&ngsp;@if (opcion.conteo !== undefined) {<span class="text-dato font-normal tabular-nums" [class.text-[color:var(--color-rotulo)]]="!puesta">{{ opcion.conteo }}</span>}</button>
      }
    </div>
  `,
})
export class SelectorSegmentadoComponent {
  @Input({ required: true }) opciones: readonly OpcionSegmentada[] = [];
  @Input({ required: true }) elegida = '';

  /** Qué se está eligiendo, para quien navega con lector de pantalla. */
  @Input({ required: true }) etiquetaDelGrupo = '';

  @Output() readonly elegir = new EventEmitter<string>();

  /**
   * Nombre del atributo de datos con que las pruebas de un panel localizan cada opción.
   *
   * Cada botón lleva siempre `data-opcion`; esto añade además el atributo que una prueba ya
   * existente busca —`data-filtro-organizacion` en el panel de organizaciones—, para que adoptar
   * el selector compartido no obligue a reescribir lo que ya vigilaba.
   */
  @Input() atributoDeDatos: 'filtro-organizacion' | null = null;

  /**
   * Una posición en cero se apaga, pero SIGUE AHI y sigue pulsándose.
   *
   * No se oculta porque una capacidad que no se ve no existe: al unificar la bandeja el 15 de
   * septiembre de 2026 se ocultaron las posiciones vacías y la dirección de producto reportó que seis
   * subsecciones —eliminaciones, reclamaciones, vinculaciones, duplicados, alertas y propuestas—
   * habían desaparecido. Un cero dice «no hay nada de esto pendiente», que es información; un
   * hueco no dice nada.
   *
   * Se apaga con el color de rótulo y medio grado de peso, no con opacidad: una posición al 50 %
   * de opacidad hunde el contraste del texto por debajo del mínimo legible.
   */

  /**
   * Si el selector dibuja su propio filete inferior.
   *
   * Suelto, sí: el filete es lo que hace que la pestaña activa «se apoye» en algo. Dentro de una
   * barra que ya dibuja un filete a todo lo ancho —Solicitudes, con su enlace a la derecha— se
   * apaga, o quedan dos líneas: una corta bajo las pestañas y otra larga siete píxeles más abajo.
   * Medido.
   */
  @Input() conFilete = true;
}
