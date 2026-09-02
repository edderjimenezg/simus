import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DisponibilidadRegistro, ServicioEstadoApi } from './core/estado-api.service';
import { ServicioAcceso } from './core/servicio-acceso';

type VistaAcceso = 'ingresar' | 'registro' | 'sesion';

interface ErrorApi {
  campos?: Record<string, string[]>;
}

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly servicioEstadoApi = inject(ServicioEstadoApi);
  private readonly servicioAcceso = inject(ServicioAcceso);

  protected readonly vista = signal<VistaAcceso>('ingresar');
  protected readonly disponibilidadRegistro = signal<DisponibilidadRegistro | null>(null);
  protected readonly cargandoDisponibilidad = signal(true);
  protected readonly enviandoIngreso = signal(false);
  protected readonly errorGeneral = signal<string | null>(null);
  protected readonly erroresCampos = signal<Record<string, string>>({});
  protected readonly ingreso = { correo: '', contrasena: '' };

  constructor() {
    this.consultarDisponibilidadRegistro();
  }

  protected cambiarVista(vista: Exclude<VistaAcceso, 'sesion'>): void {
    this.vista.set(vista);
    this.errorGeneral.set(null);
    this.erroresCampos.set({});
  }

  protected consultarDisponibilidadRegistro(): void {
    this.cargandoDisponibilidad.set(true);
    this.servicioEstadoApi.obtenerDisponibilidadRegistro().subscribe({
      next: disponibilidad => {
        this.disponibilidadRegistro.set(disponibilidad);
        this.cargandoDisponibilidad.set(false);
      },
      error: () => {
        this.disponibilidadRegistro.set(null);
        this.cargandoDisponibilidad.set(false);
      }
    });
  }

  protected limpiarError(campo: string): void {
    const errores = { ...this.erroresCampos() };
    delete errores[campo];
    this.erroresCampos.set(errores);
    this.errorGeneral.set(null);
  }

  protected ingresar(): void {
    const errores: Record<string, string> = {};
    const correo = this.ingreso.correo.trim();
    if (!correo) errores['correo'] = 'Ingresa tu correo electrónico.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo)) errores['correo'] = 'Ingresa un correo electrónico válido.';
    if (!this.ingreso.contrasena) errores['contrasena'] = 'Ingresa tu contraseña.';

    if (Object.keys(errores).length) {
      this.erroresCampos.set(errores);
      this.enfocarPrimerError(errores);
      return;
    }

    this.enviandoIngreso.set(true);
    this.errorGeneral.set(null);
    this.erroresCampos.set({});
    this.servicioAcceso.ingresar(correo, this.ingreso.contrasena).subscribe({
      next: () => {
        this.enviandoIngreso.set(false);
        this.vista.set('sesion');
      },
      error: error => {
        this.enviandoIngreso.set(false);
        this.procesarErrorIngreso(error);
      }
    });
  }

  protected cerrarSesion(): void {
    this.servicioAcceso.cerrarSesion().subscribe({
      next: () => {
        this.ingreso.contrasena = '';
        this.vista.set('ingresar');
      },
      error: () => this.errorGeneral.set('No fue posible cerrar la sesión. Inténtalo nuevamente.')
    });
  }

  private procesarErrorIngreso(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.errorGeneral.set('No fue posible conectar con el sistema. Verifica tu conexión e inténtalo nuevamente.');
      return;
    }
    if (error.status === 422) {
      const campos = (error.error as ErrorApi | null)?.campos ?? {};
      const errores = Object.fromEntries(Object.entries(campos).map(([campo, mensajes]) => [campo, mensajes[0]]));
      this.erroresCampos.set(errores);
      this.errorGeneral.set(null);
      this.enfocarPrimerError(errores);
      return;
    }
    if (error.status === 401) this.errorGeneral.set('Revisa el correo electrónico y la contraseña ingresados.');
    else if (error.status === 429) this.errorGeneral.set('Has alcanzado el límite de intentos. Espera unos minutos antes de volver a intentarlo.');
    else if (error.status === 503) this.errorGeneral.set('El servicio de acceso no está disponible temporalmente. Inténtalo más tarde.');
    else this.errorGeneral.set('No fue posible iniciar sesión. Inténtalo nuevamente.');
  }

  private enfocarPrimerError(errores: Record<string, string>): void {
    const campo = Object.keys(errores)[0];
    queueMicrotask(() => document.getElementById(campo)?.focus());
  }
}
