import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DisponibilidadRegistro, ServicioEstadoApi } from './core/estado-api.service';
import { ServicioAcceso } from './core/servicio-acceso';
import { ServicioRegistroExterno, SolicitudRegistroExterno, TerritorioRegistro } from './core/servicio-registro-externo';

type VistaAcceso = 'ingresar' | 'registro';

interface ErrorApi {
  mensaje?: string;
  campos?: Record<string, string[]>;
}

const PASOS_REGISTRO = [
  { numero: 1, etiqueta: 'Datos personales' },
  { numero: 2, etiqueta: 'Organización y territorio' },
  { numero: 3, etiqueta: 'Consentimientos' }
];

const PASO_POR_CAMPO: Record<string, number> = {
  primerNombre: 1, segundoNombre: 1, primerApellido: 1, segundoApellido: 1,
  codigoTipoIdentificacion: 1, numeroIdentificacion: 1, correo: 1, telefono: 1,
  contrasena: 1, confirmarContrasena: 1,
  nombreOrganizacion: 2, numeroIdentificacionOrganizacion: 2, codigoDepartamento: 2, codigoMunicipio: 2,
  consentimientos: 3
};

@Component({
  selector: 'app-acceso-page',
  imports: [FormsModule],
  templateUrl: './acceso-page.component.html',
  styleUrl: './acceso-page.component.scss'
})
export class AccesoPageComponent {
  private readonly servicioEstadoApi = inject(ServicioEstadoApi);
  private readonly servicioAcceso = inject(ServicioAcceso);
  private readonly servicioRegistroExterno = inject(ServicioRegistroExterno);
  private readonly router = inject(Router);

  protected readonly vista = signal<VistaAcceso>('ingresar');
  protected readonly disponibilidadRegistro = signal<DisponibilidadRegistro | null>(null);
  protected readonly cargandoDisponibilidad = signal(true);
  protected readonly enviandoIngreso = signal(false);
  protected readonly enviandoRegistro = signal(false);
  protected readonly errorGeneral = signal<string | null>(null);
  protected readonly erroresCampos = signal<Record<string, string>>({});
  protected readonly ingreso = { correo: '', contrasena: '' };
  protected readonly departamentos = signal<TerritorioRegistro[]>([]);
  protected readonly municipios = signal<TerritorioRegistro[]>([]);
  protected readonly aceptaciones = signal<Record<string, boolean>>({});
  protected readonly pasosRegistro = PASOS_REGISTRO;
  protected readonly pasoRegistro = signal(1);
  protected readonly registro = {
    primerNombre: '', segundoNombre: '', primerApellido: '', segundoApellido: '',
    codigoTipoIdentificacion: '', numeroIdentificacion: '', correo: '', telefono: '',
    contrasena: '', confirmarContrasena: '', nombreOrganizacion: '',
    numeroIdentificacionOrganizacion: '', codigoDepartamento: '', codigoMunicipio: ''
  };

  constructor() {
    this.consultarDisponibilidadRegistro();
  }

  protected cambiarVista(vista: VistaAcceso): void {
    this.vista.set(vista);
    this.errorGeneral.set(null);
    this.erroresCampos.set({});
    this.pasoRegistro.set(1);
    if (vista === 'registro' && this.disponibilidadRegistro()?.territorioDisponible) this.cargarDepartamentos();
  }

  protected irAPaso(numero: number): void {
    this.pasoRegistro.set(numero);
  }

  protected pasoSiguiente(): void {
    this.pasoRegistro.update(paso => Math.min(3, paso + 1));
  }

  protected pasoAnterior(): void {
    this.pasoRegistro.update(paso => Math.max(1, paso - 1));
  }

  protected consultarDisponibilidadRegistro(): void {
    this.cargandoDisponibilidad.set(true);
    this.servicioEstadoApi.obtenerDisponibilidadRegistro().subscribe({
      next: disponibilidad => {
        this.disponibilidadRegistro.set(disponibilidad);
        if (this.vista() === 'registro' && disponibilidad.territorioDisponible) this.cargarDepartamentos();
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
        void this.router.navigateByUrl('/mi-panel');
      },
      error: error => {
        this.enviandoIngreso.set(false);
        this.procesarErrorIngreso(error);
      }
    });
  }

  protected cambiarDepartamento(): void {
    this.registro.codigoMunicipio = '';
    this.municipios.set([]);
    this.limpiarError('codigoDepartamento');
    if (!this.registro.codigoDepartamento) return;
    this.servicioRegistroExterno.obtenerMunicipios(this.registro.codigoDepartamento).subscribe({
      next: municipios => this.municipios.set(municipios),
      error: () => this.errorGeneral.set('No fue posible cargar los municipios. Inténtalo nuevamente.')
    });
  }

  protected cambiarAceptacion(codigo: string, aceptada: boolean): void {
    this.aceptaciones.update(actuales => ({ ...actuales, [codigo]: aceptada }));
    this.limpiarError('consentimientos');
  }

  protected registrar(): void {
    const disponibilidad = this.disponibilidadRegistro();
    if (!disponibilidad?.registroDisponible) {
      this.errorGeneral.set('El registro aún no puede finalizar porque faltan requisitos institucionales.');
      return;
    }
    const errores: Record<string, string> = {};
    for (const campo of ['primerNombre', 'primerApellido', 'codigoTipoIdentificacion', 'numeroIdentificacion', 'correo', 'contrasena', 'nombreOrganizacion', 'codigoDepartamento', 'codigoMunicipio']) {
      if (!this.registro[campo as keyof typeof this.registro].trim()) errores[campo] = 'Completa este campo para continuar.';
    }
    if (this.registro.correo && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.registro.correo.trim())) errores['correo'] = 'Ingresa un correo electrónico válido.';
    if (this.registro.contrasena && this.registro.contrasena.length < 12) errores['contrasena'] = 'La contraseña debe tener al menos 12 caracteres.';
    if (this.registro.contrasena !== this.registro.confirmarContrasena) errores['confirmarContrasena'] = 'Las contraseñas no coinciden.';
    const codigosAceptados = disponibilidad.documentos.filter(documento => this.aceptaciones()[documento.codigo]).map(documento => documento.codigo);
    if (codigosAceptados.length !== disponibilidad.documentos.length) errores['consentimientos'] = 'Debes aceptar los documentos vigentes para continuar.';
    if (Object.keys(errores).length) { this.erroresCampos.set(errores); this.enfocarPrimerErrorRegistro(errores); return; }

    const solicitud: SolicitudRegistroExterno = {
      primerNombre: this.registro.primerNombre, segundoNombre: this.registro.segundoNombre || undefined,
      primerApellido: this.registro.primerApellido, segundoApellido: this.registro.segundoApellido || undefined,
      codigoTipoIdentificacion: this.registro.codigoTipoIdentificacion, numeroIdentificacion: this.registro.numeroIdentificacion,
      correo: this.registro.correo, telefono: this.registro.telefono || undefined, contrasena: this.registro.contrasena,
      nombreOrganizacion: this.registro.nombreOrganizacion, numeroIdentificacionOrganizacion: this.registro.numeroIdentificacionOrganizacion || undefined,
      codigoDepartamento: this.registro.codigoDepartamento, codigoMunicipio: this.registro.codigoMunicipio,
      codigosDocumentosAceptados: codigosAceptados
    };
    this.enviandoRegistro.set(true);
    this.errorGeneral.set(null);
    this.servicioRegistroExterno.registrar(solicitud).subscribe({
      next: () => { this.enviandoRegistro.set(false); void this.router.navigateByUrl('/mi-panel'); },
      error: error => { this.enviandoRegistro.set(false); this.procesarErrorRegistro(error); }
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

  private cargarDepartamentos(): void {
    if (this.departamentos().length) return;
    this.servicioRegistroExterno.obtenerDepartamentos().subscribe({
      next: departamentos => this.departamentos.set(departamentos),
      error: () => this.errorGeneral.set('No fue posible cargar los departamentos. Inténtalo nuevamente.')
    });
  }

  private procesarErrorRegistro(error: unknown): void {
    if (error instanceof HttpErrorResponse && (error.status === 422 || error.status === 409)) {
      const campos = (error.error as ErrorApi | null)?.campos ?? {};
      const errores = Object.fromEntries(Object.entries(campos).map(([campo, mensajes]) => [campo, mensajes[0]]));
      this.erroresCampos.set(errores);
      this.errorGeneral.set(Object.keys(errores).length ? null : (error.error as ErrorApi | null)?.mensaje ?? 'No fue posible completar el registro.');
      if (Object.keys(errores).length) this.enfocarPrimerErrorRegistro(errores);
      return;
    }
    this.errorGeneral.set('No fue posible completar el registro. Inténtalo nuevamente.');
  }

  private enfocarPrimerError(errores: Record<string, string>): void {
    const campo = Object.keys(errores)[0];
    queueMicrotask(() => document.getElementById(campo)?.focus());
  }

  private enfocarPrimerErrorRegistro(errores: Record<string, string>): void {
    const campo = Object.keys(errores)[0];
    this.pasoRegistro.set(PASO_POR_CAMPO[campo] ?? 1);
    setTimeout(() => document.getElementById(campo)?.focus(), 0);
  }
}
