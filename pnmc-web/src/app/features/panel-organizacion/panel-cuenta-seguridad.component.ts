import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ExternalSessionService } from '../../core/services/external-session.service';
import { MisAutorizacionesComponent } from './mis-autorizaciones.component';

/**
 * La ruta «Cuenta y seguridad», en /gestion/cuenta-seguridad.
 *
 * <b>NUEVA EN EL REDISEÑO DEL 1 DE SEPTIEMBRE DE 2026.</b> No existía ninguna pantalla de cuenta
 * para la sesión externa. El correo de acceso ya lo trae la sesión -no hace falta pedirlo-, pero
 * «Cambiar contraseña» no tiene con qué conectar: no hay ningún endpoint de cambio de contraseña
 * autoservicio en toda la API externa (`AdminService`, `PanelOrganizacionApi`). El dueño del
 * proyecto lo confirmó: el botón queda deshabilitado con «Próximamente», el mismo patrón que ya
 * usan Google/Apple/Microsoft en `external-access-page.component.html` para lo mismo -una acción
 * real que todavía no tiene backend-.
 *
 * <b>AQUI VIVEN LAS AUTORIZACIONES DE DATOS, y no en una ruta propia.</b> Lo que una persona
 * autorizó sobre SUS datos personales es de su cuenta, no de la organización que administra: si
 * mañana esa cuenta deja de administrarla, sus autorizaciones siguen siendo suyas. Esta pantalla ya
 * reúne lo que es de la cuenta —su correo, su clave, su sesión— y es donde alguien va a buscarlo.
 */
@Component({
  selector: 'app-panel-cuenta-seguridad',
  standalone: true,
  imports: [CommonModule, MisAutorizacionesComponent],
  templateUrl: './panel-cuenta-seguridad.component.html',
})
export class PanelCuentaSeguridadComponent {
  private readonly sesionExterna = inject(ExternalSessionService);
  private readonly router = inject(Router);

  readonly sesion = this.sesionExterna.actual;

  iniciales(): string {
    return (this.sesion()?.fullName ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(parte => parte[0])
      .join('')
      .toUpperCase() || 'US';
  }

  cerrarSesion(): void {
    this.sesionExterna.salir().subscribe(() => this.router.navigate(['/']));
  }
}
