import { Pipe, PipeTransform } from '@angular/core';
import { nombrePropio } from './nombre-propio';

/**
 * `{{ nombre | nombrePropio }}` — el nombre territorial, escrito como se escribe.
 *
 * Es puro: depende solo de su entrada y Angular puede memorizarlo. Ver `nombre-propio.ts` para el
 * porqué y para los casos que se comprobaron contra la tabla real.
 */
@Pipe({ name: 'nombrePropio', standalone: true, pure: true })
export class NombrePropioPipe implements PipeTransform {
  transform(valor: string | null | undefined): string {
    return nombrePropio(valor);
  }
}
