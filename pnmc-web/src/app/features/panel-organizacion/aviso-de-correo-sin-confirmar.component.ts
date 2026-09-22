import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideMailQuestion } from '@lucide/angular';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';

/**
 * El aviso de que falta confirmar el correo, con las dos salidas que de verdad lo resuelven.
 *
 * <b>POR QUE ES UN COMPONENTE Y NO UN BLOQUE EN CADA PANTALLA.</b> Desde el 12 de septiembre de
 * 2026 la confirmación bloquea cuatro cosas —registrar un proceso, anunciar un evento, reclamar un
 * Festival y entregarlo al Programa— y por tanto aparece en cuatro sitios. Escrito cuatro veces, la
 * frase se contradice: el resumen del panel llegó a decir «puedes seguir preparando tu registro»
 * cuando el servidor ya no lo permitía.
 *
 * <b>DICE LAS CUATRO COSAS QUE PIDE EL PLAN</b> —qué falta, por qué, qué hacer y qué se habilita
 * después—, que es lo contrario de «no tiene permisos»: quien lee esto SÍ administra su
 * organización, y mandarlo a buscar un rol que no le falta lo deja dando vueltas.
 *
 * <b>Y OFRECE CORREGIR LA DIRECCION.</b> Reenviar el enlace a un correo mal escrito lo manda otra
 * vez a ninguna parte. Como el correo es único y es el de la organización, una errata sin esta
 * salida deja la cuenta muerta para siempre.
 */
@Component({
  selector: 'app-aviso-de-correo-sin-confirmar',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideMailQuestion],
  templateUrl: './aviso-de-correo-sin-confirmar.component.html',
  /*
    ANFITRION EN BLOQUE. Un componente sin `display` declarado se queda en `display: inline`, y los
    márgenes verticales no se aplican a una caja en línea: dentro de una pila con `space-y-*` la
    separación que la pila cree estar poniendo vale 0 px. Medido en el
    Resumen de la organización, donde el aviso de información pendiente quedaba pegado al bloque
    siguiente en los cinco anchos probados.
  */
  styles: [':host { display: block; }'],
})
export class AvisoDeCorreoSinConfirmarComponent implements OnInit {
  private readonly confirmacion = inject(ConfirmacionDeCorreoService);

  /**
   * Qué queda habilitado al confirmar, en infinitivo: «registrar Festivales».
   *
   * SE NOMBRA LO QUE LA PERSONA ESTABA INTENTANDO. «Podrás usar el sistema» no le dice nada a quien
   * acaba de pulsar «Registrar un Festival».
   */
  @Input() queSeHabilita = 'entregarle registros al Programa';

  /** `true` cuando el aviso sustituye a la pantalla en vez de encabezarla. */
  @Input() bloquea = false;

  readonly correo = this.confirmacion.correo.bind(this.confirmacion);
  readonly falta = this.confirmacion.falta.bind(this.confirmacion);

  readonly trabajando = signal(false);
  readonly aviso = signal('');
  readonly error = signal('');
  readonly corrigiendo = signal(false);
  readonly correoNuevo = signal('');

  async ngOnInit(): Promise<void> {
    if (this.confirmacion.estado() === null) await this.confirmacion.asegurar();
  }

  abrirCorreccion(): void {
    this.corrigiendo.set(true);
    this.correoNuevo.set(this.correo());
    this.aviso.set('');
    this.error.set('');
  }

  cancelarCorreccion(): void {
    this.corrigiendo.set(false);
    this.error.set('');
  }

  async reenviar(): Promise<void> {
    await this.intentar(async () => {
      const enviado = await this.confirmacion.reenviar();
      this.aviso.set(`Te enviamos otro enlace a ${enviado.enviadoA}. Vence en ${enviado.venceEn} días.`);
    });
  }

  async corregir(): Promise<void> {
    const correo = this.correoNuevo().trim();
    if (!correo) {
      this.error.set('Escribe la dirección correcta.');
      return;
    }
    await this.intentar(async () => {
      const enviado = await this.confirmacion.corregir(correo);
      this.corrigiendo.set(false);
      this.aviso.set(`Cambiamos tu correo a ${enviado.enviadoA} y enviamos el enlace allí. Vence en ${enviado.venceEn} días.`);
    });
  }

  /** El mismo envoltorio para las dos acciones: una sola forma de fallar y de dejar de girar. */
  private async intentar(accion: () => Promise<void>): Promise<void> {
    this.trabajando.set(true);
    this.aviso.set('');
    this.error.set('');
    try {
      await accion();
    } catch (fallo: unknown) {
      // EL MENSAJE DEL SERVIDOR MANDA cuando lo hay: «ya hay una cuenta con ese correo» es
      // accionable, y sustituirlo por un «no fue posible» genérico quita justo lo que se necesita.
      this.error.set(fallo instanceof Error && fallo.message ? fallo.message : 'No fue posible completar la acción.');
    } finally {
      this.trabajando.set(false);
    }
  }
}
