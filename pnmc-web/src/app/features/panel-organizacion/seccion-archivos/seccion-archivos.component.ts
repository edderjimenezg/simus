import { Component } from '@angular/core';
import { GestionDelBancoComponent } from '../../../shared/components/banco-de-archivos/gestion-del-banco.component';

/**
 * «Mis archivos», en el espacio de la organización.
 *
 * <b>POR QUE HACIA FALTA.</b> Cuando una organización llenaba su cuota, el sistema le decía «retira
 * material que ya no uses» y no había ninguna forma de retirarlo: la única salida era escribir al
 * Programa. Una promesa que el sistema no puede cumplir es peor que no hacerla. Y tampoco podía
 * ver lo que llevaba subido, así que no sabía qué ocupaba su espacio.
 *
 * <b>ES LA MISMA PANTALLA QUE LA DE LA CONSOLA</b>, con otro canal: el servidor decide qué devuelve
 * según por dónde se pregunta. Dos pantallas gemelas divergirían en el primer arreglo.
 */
@Component({
  selector: 'app-seccion-archivos',
  standalone: true,
  imports: [GestionDelBancoComponent],
  template: '<app-gestion-del-banco canal="externo"></app-gestion-del-banco>',
})
export class SeccionArchivosComponent {}
