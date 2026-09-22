import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { BotonComponent } from '../boton/boton.component';
import { EstadoDeListaComponent } from '../estado-de-lista/estado-de-lista.component';

/**
 * Lo que una lista enseña cuando no tiene filas, sabiendo POR QUE no las tiene.
 *
 * <b>UNA LISTA VACIA NO ES UN SOLO HECHO, SON DOS.</b> «Todavía no hay mercados registrados» y «tus
 * filtros no encontraron ninguno» son situaciones distintas, con salidas distintas: en la primera
 * hay que crear el primer registro; en la segunda, quitar el filtro que está escondiendo los que ya
 * existen. La revisión encontró que <b>ninguna lista de la consola las
 * distinguía</b>: las once escribían un solo texto que titubeaba entre las dos —«Registra el primero
 * con «Nuevo mercado», o prueba con otro estado si estás filtrando»— y por tanto no decía ninguna
 * de las dos cosas. El directorio público de Mercados sí las separa desde el corte anterior; esto
 * lleva el mismo criterio a la consola.
 *
 * <b>EL CASO FILTRADO SIEMPRE LLEVA SALIDA.</b> Decirle a alguien que sus filtros no encontraron
 * nada sin darle el botón que los quita es dejarlo buscando cuál de los cuatro controles tiene que
 * devolver a su sitio. El botón va aquí dentro, y por eso ninguna pantalla puede olvidarlo.
 *
 * <b>QUIEN LA USA SOLO DECIDE DOS COSAS:</b> cómo se llama lo que no hay —en plural y en minúscula,
 * «mercados musicales»— y de dónde salen los registros cuando todavía no hay ninguno. El resto
 * —los dos títulos, el orden de las frases, el botón de limpiar— se escribe una sola vez.
 */
@Component({
  selector: 'app-lista-sin-filas',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BotonComponent, EstadoDeListaComponent],
  template: `
    @if (hayFiltros()) {
      <app-estado-de-lista [titulo]="tituloFiltrado()" [detalle]="detalleFiltrado()">
        <app-boton importancia="secundaria" tamano="menudo" identificador="limpiar-filtros"
                   (accion)="limpiar.emit()">Limpiar filtros</app-boton>
      </app-estado-de-lista>
    } @else {
      <app-estado-de-lista [titulo]="tituloSinRegistros()" [detalle]="comoLlegan()">
        <ng-content />
      </app-estado-de-lista>
    }
  `,
})
export class ListaSinFilasComponent {
  /**
   * Lo que no hay, en plural y en minúscula: «mercados musicales», «organizaciones», «cuentas».
   *
   * <b>LAS FRASES NO LLEVAN ADJETIVO A PROPOSITO.</b> «Todavía no hay noticias registradas» obliga
   * a concordar en género, y el primer intento decía «noticias registrados». Sin adjetivo, la misma
   * plantilla sirve para las cuatro combinaciones y no hay nada que un módulo nuevo pueda equivocar.
   */
  readonly entidad = input.required<string>();

  /** Cierto cuando hay algún filtro o búsqueda puesto, y por tanto la lista puede no estar vacía de verdad. */
  readonly hayFiltros = input.required<boolean>();

  /**
   * De dónde salen los registros cuando todavía no hay ninguno.
   *
   * No es decoración: sin esta frase, un vacío legítimo —un módulo recién abierto— se lee como un
   * fallo de la pantalla. Dice quién crea el registro y desde dónde.
   */
  readonly comoLlegan = input.required<string>();

  /**
   * La pestaña en la que se está mirando, cuando no es la de todo: «de esta categoría»,
   * «pendientes de validación», «externas».
   *
   * <b>UNA PESTAÑA NO ES UN FILTRO.</b> Un filtro esconde parte de una lista y se quita; una
   * pestaña es la lista que se eligió mirar. Por eso no la limpia el botón: lo que cambia es la
   * frase, que deja de afirmar que no hay ningún registro —cuando puede haber decenas en la
   * pestaña de al lado— y dice que no hay ninguno AQUI.
   */
  readonly ambito = input<string | null>(null);

  /** El título del caso sin registros, cuando ninguna de las dos frases armadas es la correcta. */
  readonly tituloPropio = input<string | null>(null);

  /** Qué añadir al caso filtrado: por ejemplo cuántos registros hay en total sin filtrar. */
  readonly detallePropio = input<string | null>(null);

  readonly limpiar = output<void>();

  readonly tituloSinRegistros = computed(() => {
    const propio = this.tituloPropio();
    if (propio) return propio;
    const ambito = this.ambito();
    return ambito ? `No hay ${this.entidad()} ${ambito}` : `Todavía no hay ${this.entidad()}`;
  });
  readonly tituloFiltrado = computed(() => `No hay ${this.entidad()} que coincidan con estos filtros`);
  readonly detalleFiltrado = computed(
    () => this.detallePropio() ?? 'Los filtros puestos están escondiendo el resto. Quítalos para volver a la lista completa.',
  );
}
