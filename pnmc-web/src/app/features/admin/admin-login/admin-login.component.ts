import { Component, inject, signal, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { 
  LucideMail, 
  LucideLock, 
  LucideEye, 
  LucideEyeOff, 
  LucideShieldCheck, 
  LucideAlertCircle, 
  LucideRefreshCw, 
  LucideCheckCircle2, 
  LucideUserCheck,
} from '@lucide/angular';
import { SessionService } from '../../../core/services/session.service';
import { UsuarioAdministrativo } from '../../../core/services/admin.service';
import { ADMIN_ROLES } from '../domain/admin-config';
import { DEV_ADMIN_ACCOUNTS } from './dev-accounts';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LucideMail,
    LucideLock,
    LucideEye,
    LucideEyeOff,
    LucideShieldCheck,
    LucideAlertCircle,
    LucideRefreshCw,
    LucideCheckCircle2,
    LucideUserCheck
  ],
  templateUrl: './admin-login.component.html',
  styleUrls: ['./admin-login.component.css']
})
export class AdminLoginComponent {
  private sessionService = inject(SessionService);

  @Output() loginSuccess = new EventEmitter<UsuarioAdministrativo>();

  // Internal Login State
  selectedRole = signal<string>('webmaster');
  email = signal<string>(DEV_ADMIN_ACCOUNTS['webmaster']?.email ?? '');
  password = signal<string>(DEV_ADMIN_ACCOUNTS['webmaster']?.password ?? '');
  loginState = signal<{ status: string; message: string }>({ status: 'idle', message: '' });
  formMode = signal<string>('login'); // 'login' | 'recover'
  recoverEmail = signal<string>('');
  recoverSuccess = signal<boolean>(false);
  showPassword = signal<boolean>(false);
  inputsGlowing = signal<boolean>(false);
  showDevPanel = signal<boolean>(false);

  // Icons reference for templates
  LucideMail = LucideMail;
  LucideLock = LucideLock;
  LucideEye = LucideEye;
  LucideEyeOff = LucideEyeOff;
  LucideShieldCheck = LucideShieldCheck;
  LucideAlertCircle = LucideAlertCircle;
  LucideRefreshCw = LucideRefreshCw;
  LucideCheckCircle2 = LucideCheckCircle2;
  LucideUserCheck = LucideUserCheck;

  // Vacio en produccion: angular.json sustituye el modulo de cuentas de prueba,
  // asi que estos correos y contrasenas no existen en el bundle publicado.
  ROLE_CREDENTIALS = DEV_ADMIN_ACCOUNTS;

  /** Solo hay panel de cuentas de prueba si el entorno lo trae sembrado. */
  readonly hasDevAccounts = Object.keys(DEV_ADMIN_ACCOUNTS).length > 0;

  adminRoles = Object.values(ADMIN_ROLES);

  selectRole(roleId: string) {
    if (this.formMode() !== 'login') {
      this.formMode.set('login');
    }
    this.selectedRole.set(roleId);
    const creds = this.ROLE_CREDENTIALS[roleId];
    if (creds) {
      this.email.set(creds.email);
      this.password.set(creds.password || '');
    }
    this.inputsGlowing.set(true);
    setTimeout(() => this.inputsGlowing.set(false), 800);
  }

  async handleSubmit(event: Event) {
    event.preventDefault();
    this.loginState.set({ status: 'saving', message: 'Validando credenciales...' });
    
    // Simulate or call API via SessionService
    this.sessionService.login({ email: this.email(), password: this.password() }).subscribe({
      next: (res) => {
        this.loginState.set({ status: 'idle', message: '' });
        this.loginSuccess.emit(res.user);
      },
      error: (err) => {
        this.loginState.set({ status: 'error', message: err.message || 'Error de inicio de sesión' });
      }
    });
  }

  handleRecoverSubmit(event: Event) {
    event.preventDefault();
    if (!this.recoverEmail()) return;
    this.loginState.set({ status: 'saving', message: 'Enviando solicitud...' });
    
    setTimeout(() => {
      this.recoverSuccess.set(true);
      this.loginState.set({ status: 'idle', message: '' });
    }, 1000);
  }
}
