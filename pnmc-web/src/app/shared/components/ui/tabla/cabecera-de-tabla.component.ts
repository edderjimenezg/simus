import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ColumnaDeTabla, OrdenDeTabla } from './orden-de-tabla';

/**
 * La fila de cabeceras de una tabla administrativa.
 *
 * <b>UNA SOLA PIEZA PARA LAS DIEZ TABLAS.</b> Antes, las dos tablas que ordenaban tenían cada una
 * su copia del mismo marcado —`th[scope][aria-sort]` con un botón dentro, la flecha y el título— y
 * las otras ocho no ordenaban en absoluto. Aquí el panel declara sus columnas y esta pieza decide
 * cuál lleva botón, con qué flecha y qué anuncia.
 *
 * <b>UNA COLUMNA QUE NO ORDENA NO SE DIBUJA COMO CONTROL.</b> La de acciones va sin botón, sin
 * flecha y sin `title`: una cabecera que ofrece ordenar y al pulsarla no hace nada es la misma
 * clase de defecto que un contrato declarado que el servidor no aplica.
 */
@Component({
  selector: 'app-cabecera-de-tabla',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  /*
    EL ANFITRION NO OCUPA SITIO EN LA TABLA. Con `display: contents`, los `<th>` que hay dentro se
    comportan como hijos directos del `<tr>` que envuelve a esta pieza; sin él, el elemento propio
    quedaría entre la fila y sus celdas y el navegador no las trataría como celdas de tabla.
    Es el mismo recurso que usa `app-dato-en-lectura` dentro de una lista de definición.
  */
  styles: [':host { display: contents; }'],
  template: `
    @for (columna of columnas(); track columna.id) {
      <th scope="col" [class]="columna.clases || ''" [attr.aria-sort]="orden().ariaDe(columna.id)">
        @if (columna.ordenable === false) {
          {{ columna.etiqueta }}
        } @else {
          <button type="button" class="tabla-administrativa__ordenar"
            [attr.data-ordenar]="columna.id"
            [title]="orden().tituloDe(columna)"
            (click)="ordenarPor.emit(columna.id)">
            {{ columna.etiqueta }}
            <span aria-hidden="true">{{ orden().flechaDe(columna.id) }}</span>
          </button>
        }
      </th>
    }
  `,
})
export class CabeceraDeTablaComponent {
  readonly columnas = input.required<readonly ColumnaDeTabla[]>();
  readonly orden = input.required<OrdenDeTabla>();

  /** Qué columna se pulsó. El panel decide si reordena en memoria o se lo pide al servidor. */
  readonly ordenarPor = output<string>();
}
