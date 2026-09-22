import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../../../../core/http/api-client.service';

/**
 * La página que recibe el enlace de confirmación de correo.
 *
 * <b>ES ANONIMA A PROPOSITO.</b> El enlace se abre desde el buzón, y eso puede pasar en otro
 * navegador, en el móvil o sin haber entrado nunca. Pedir sesión para confirmar convertiría el
 * enlace en un callejón sin salida.
 *
 * <b>EL TESTIGO NO SE QUEDA EN LA BARRA.</b> Se lee de la dirección al cargar y se envía en el
 * cuerpo de la petición: un testigo en la URL acaba en el historial del navegador y en la cabecera
 * <i>Referer</i> de la siguiente petición.
 *
 * <b>DICE POR QUE FALLA.</b> «El enlace venció» y «ese enlace no existe» piden dos cosas distintas
 * a quien lo abre —pedir otro, o revisar de dónde salió—, y el servidor ya las distingue.
 */
@Component({
  selector: 'app-confirmar-correo-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './confirmar-correo-page.component.html',
})
export class ConfirmarCorreoPageComponent {
  private readonly api = inject(ApiClientService);
  private readonly ruta = inject(ActivatedRoute);

  readonly trabajando = signal(true);
  readonly confirmado = signal(false);
  readonly error = signal('');

  constructor() {
    const testigo = this.ruta.snapshot.queryParamMap.get('testigo') ?? '';
    void this.confirmar(testigo);
  }

  private async confirmar(testigo: string): Promise<void> {
    if (!testigo) {
      this.error.set('Este enlace está incompleto. Ábrelo tal como llegó a tu correo.');
      this.trabajando.set(false);
      return;
    }
    try {
      await firstValueFrom(this.api.post('/api/v1/externo/auth/confirmar-correo', { testigo }, {}));
      this.confirmado.set(true);
    } catch (error: unknown) {
      this.error.set(this.motivoDelServidor(error) ?? 'No fue posible confirmar el correo.');
    } finally {
      this.trabajando.set(false);
    }
  }

  /** El motivo exacto que devolvió el servidor, que es el que sabe cuál de los cuatro casos es. */
  private motivoDelServidor(error: unknown): string | null {
    const cuerpo = (error as { payload?: unknown })?.payload;
    if (!cuerpo || typeof cuerpo !== 'object') return null;
    for (const valor of Object.values(cuerpo as Record<string, unknown>)) {
      if (Array.isArray(valor)) {
        const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
        if (typeof mensaje === 'string') return mensaje.trim();
      }
    }
    return null;
  }
}
