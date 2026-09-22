import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LucideChevronDown, LucideTriangleAlert } from '@lucide/angular';
import { AdminService } from '../../core/services/admin.service';
import { AutorizacionDeDatos } from '../../core/services/politicas-de-datos';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';

/**
 * Qué autorizó esta cuenta, cuándo, con qué texto delante, y qué puede retirar.
 *
 * <b>NO ES UNA PANTALLA DE CORTESIA: ES UN DERECHO CON PLAZO.</b> El artículo 8 de la Ley 1581 de
 * 2012 da al titular el derecho a conocer sus datos, a solicitar <i>prueba de la autorización</i>
 * (num. 2), a ser informado del uso que se les ha dado (num. 3) y a revocarla (num. 5). Hasta este
 * corte no había ningún sitio donde una persona pudiera ver nada de eso: el sistema recogía diez
 * datos personales en el alta y la única constancia era una fila que decía a qué documento
 * apuntaba, sin el texto.
 *
 * <b>SE ENSEÑA TAMBIEN LO RETIRADO.</b> Una lista que solo muestra lo vigente no permite comprobar
 * que algo se retiró de verdad ni cuándo, que es justo lo que quiere ver quien acaba de retirarlo.
 *
 * <b>NO TODO SE PUEDE RETIRAR, Y ESO SE EXPLICA EN VEZ DE ESCONDERSE.</b> El art. 9 del decreto
 * 1377 de 2013 admite que la revocatoria no proceda cuando el titular tiene un deber legal o
 * contractual de permanecer en la base de datos. Quien es responsable declarado de una organización
 * con procesos inscritos está en ese caso. Lo que no se hace es poner el botón y que conteste que
 * no: cuando no procede, el botón no está y en su lugar va el motivo.
 */
@Component({
  selector: 'app-mis-autorizaciones',
  standalone: true,
  imports: [CommonModule, LucideChevronDown, LucideTriangleAlert, IndicadorDeEstadoComponent],
  templateUrl: './mis-autorizaciones.component.html',
  /*
    ANFITRION EN BLOQUE. Un componente sin `display` declarado se queda en `display: inline`, y los
    márgenes verticales no se aplican a una caja en línea: dentro de una pila con `space-y-*` la
    separación que la pila cree estar poniendo vale 0 px. Medido en el
    Resumen de la organización, donde el aviso de información pendiente quedaba pegado al bloque
    siguiente en los cinco anchos probados.
  */
  styles: [':host { display: block; }'],
})
export class MisAutorizacionesComponent implements OnInit {
  private readonly admin = inject(AdminService);

  readonly autorizaciones = signal<readonly AutorizacionDeDatos[]>([]);
  readonly cargando = signal(true);
  readonly error = signal('');

  /** Cuál tiene el texto desplegado. Solo una: son textos largos. */
  readonly textoAbierto = signal<number>(0);

  /** Cuál se está retirando ahora mismo, para no permitir dos a la vez. */
  readonly retirando = signal<string>('');

  /**
   * Las vigentes primero y las retiradas después.
   *
   * <b>NO SE MEZCLAN POR FECHA.</b> Ordenadas solo por cuándo se otorgaron, una autorización
   * retirada el mes pasado podría aparecer por encima de una vigente de hace un año, y la pregunta
   * que esta pantalla contesta primero es «¿qué está autorizado AHORA?».
   */
  readonly ordenadas = computed(() =>
    [...this.autorizaciones()].sort((una, otra) => {
      if (una.vigente !== otra.vigente) return una.vigente ? -1 : 1;
      return otra.fechaOtorgada.localeCompare(una.fechaOtorgada);
    }),
  );

  ngOnInit(): void {
    this.cargar();
  }

  alternarTexto(id: number): void {
    this.textoAbierto.update(puesto => (puesto === id ? 0 : id));
  }

  /**
   * Retira una autorización.
   *
   * El servidor devuelve la lista ya actualizada en vez de un acuse: así la pantalla no tiene que
   * adivinar cómo quedó la fila —con qué fecha, con qué motivo— ni pedirla otra vez.
   */
  retirar(autorizacion: AutorizacionDeDatos): void {
    if (!autorizacion.sePuedeRevocar || this.retirando()) return;

    this.retirando.set(autorizacion.finalidad);
    this.error.set('');
    this.admin.revocarAutorizacion(autorizacion.finalidad).subscribe({
      next: lista => {
        this.autorizaciones.set(lista ?? []);
        this.retirando.set('');
      },
      error: fallo => {
        this.error.set(fallo?.error?.message ?? 'No fue posible retirar la autorización.');
        this.retirando.set('');
      },
    });
  }

  private cargar(): void {
    this.cargando.set(true);
    this.admin.cargarMisAutorizaciones().subscribe({
      next: lista => {
        this.autorizaciones.set(lista ?? []);
        this.cargando.set(false);
      },
      error: fallo => {
        this.error.set(fallo?.error?.message ?? 'No fue posible cargar tus autorizaciones.');
        this.cargando.set(false);
      },
    });
  }
}
