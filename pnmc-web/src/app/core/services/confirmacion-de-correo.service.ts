import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/** Lo que la sesión externa dice de la cuenta que entró. */
export interface SesionExterna {
  userId: string;
  fullName: string;
  email: string;
  accountStatus: string;
  correoConfirmado: boolean;
}

/** A dónde se mandó el enlace y cuánto vive. */
export interface EnvioDeConfirmacion {
  enviadoA: string;
  venceEn: number;
}

/**
 * Saber si el correo de la cuenta está confirmado, pedir otro enlace y corregir la dirección.
 *
 * <b>SERVICIO PROPIO Y NO UN METODO MAS EN <code>PanelOrganizacionApi</code>.</b> Aquella es una
 * clase abstracta que ocho dobles de prueba implementan; añadirle un método rompe los ocho por algo
 * que no les incumbe. La lección ya costó una vez, con los eventos de la organización.
 *
 * <b>EL ESTADO VIVE AQUI Y NO EN CADA PANTALLA.</b> Desde la
 * confirmación no bloquea solo el envío a revisión: bloquea registrar procesos, anunciar eventos y
 * reclamar Festivales. Si cada pantalla lo preguntara por su cuenta habría cuatro copias de la misma
 * consulta y, peor, cuatro momentos distintos en que una de ellas se quedaría enseñando el aviso
 * después de confirmar.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmacionDeCorreoService {
  private readonly api = inject(ApiClientService);

  /** `null` mientras no se sabe: no es lo mismo que «está sin confirmar». */
  readonly estado = signal<SesionExterna | null>(null);

  /**
   * Si hoy falta confirmar. Mientras no se sepa, responde que no.
   *
   * NO AVISAR ES MEJOR QUE AVISAR DE MAS: un cartel pintado por un fallo de red diría que falta
   * confirmar un correo que quizá ya está confirmado, y mandaría a pedir enlaces sin motivo. El
   * bloqueo de verdad lo pone el servidor; esto solo lo anticipa.
   */
  falta(): boolean {
    const sesion = this.estado();
    return sesion !== null && !sesion.correoConfirmado;
  }

  correo(): string {
    return this.estado()?.email ?? '';
  }

  /** Carga el estado una vez por pantalla que lo necesite; los fallos no pintan avisos falsos. */
  async asegurar(): Promise<void> {
    try {
      this.estado.set(await this.sesion());
    } catch {
      this.estado.set(null);
    }
  }

  sesion(): Promise<SesionExterna> {
    return firstValueFrom(this.api.get<SesionExterna>('/api/v1/externo/auth/me', {
      errorFallback: 'No fue posible consultar la sesión.',
    }));
  }

  /**
   * Pide otro enlace de confirmación.
   *
   * DEVUELVE A DONDE SE MANDO, y no un simple «hecho»: quien se equivocó al escribir su correo lo
   * ve ahí mismo, que es justamente el fallo que la confirmación viene a cerrar.
   */
  reenviar(): Promise<EnvioDeConfirmacion> {
    return firstValueFrom(this.api.post<EnvioDeConfirmacion>(
      '/api/v1/externo/cuenta/reenviar-confirmacion', {}, {
        errorFallback: 'No fue posible enviar otro enlace.',
      }));
  }

  /**
   * Corrige la dirección de la cuenta y manda el enlace a la nueva.
   *
   * ES LA OTRA MITAD DEL AVISO. Reenviar el enlace a una dirección mal escrita lo manda otra vez a
   * ninguna parte; sin esta ruta, una letra de más al registrarse deja la cuenta muerta, porque el
   * correo es único y es el de la organización.
   */
  async corregir(correo: string): Promise<EnvioDeConfirmacion> {
    const resultado = await firstValueFrom(this.api.put<EnvioDeConfirmacion>(
      '/api/v1/externo/cuenta/correo', { correo }, {
        errorFallback: 'No fue posible cambiar el correo.',
      }));
    const sesion = this.estado();
    if (sesion) this.estado.set({ ...sesion, email: resultado.enviadoA });
    return resultado;
  }
}
