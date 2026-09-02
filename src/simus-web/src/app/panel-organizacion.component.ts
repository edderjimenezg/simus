import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ServicioAcceso } from './core/servicio-acceso';
import { AdministradorOrganizacion, ContextoPanel, OrganizacionPanel, ServicioPanelOrganizacion } from './core/servicio-panel-organizacion';
import { TerritorioRegistro } from './core/servicio-registro-externo';

type SeccionPanel = 'resumen' | 'organizacion' | 'administradores' | 'cuenta';
interface ErrorApi { mensaje?: string; campos?: Record<string, string[]>; }

@Component({
  selector: 'app-panel-organizacion',
  imports: [FormsModule, RouterLink],
  templateUrl: './panel-organizacion.component.html',
  styleUrl: './panel-organizacion.component.scss'
})
export class PanelOrganizacionComponent {
  private readonly servicioPanel = inject(ServicioPanelOrganizacion);
  private readonly servicioAcceso = inject(ServicioAcceso);
  private readonly router = inject(Router);

  protected readonly contexto = signal<ContextoPanel | null>(null);
  protected readonly organizacionActiva = signal<OrganizacionPanel | null>(null);
  protected readonly seccion = signal<SeccionPanel>('resumen');
  protected readonly cargando = signal(true);
  protected readonly cargandoAdministradores = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGeneral = signal<string | null>(null);
  protected readonly mensajeExito = signal<string | null>(null);
  protected readonly erroresCampos = signal<Record<string, string>>({});
  protected readonly administradores = signal<AdministradorOrganizacion[]>([]);
  protected readonly departamentos = signal<TerritorioRegistro[]>([]);
  protected readonly municipios = signal<TerritorioRegistro[]>([]);
  protected readonly perfil = { nombre: '', numeroIdentificacion: '', codigoDepartamento: '', codigoMunicipio: '' };

  constructor() { this.cargarContexto(); }

  protected seleccionarOrganizacion(id: string): void {
    const siguiente = this.contexto()?.organizaciones.find(organizacion => organizacion.id === id) ?? null;
    if (!siguiente) return;
    this.organizacionActiva.set(siguiente);
    this.prepararPerfil(siguiente);
    this.administradores.set([]);
    if (this.seccion() === 'administradores') this.cargarAdministradores();
  }

  protected cambiarSeccion(seccion: SeccionPanel): void {
    this.seccion.set(seccion);
    this.errorGeneral.set(null);
    this.mensajeExito.set(null);
    if (seccion === 'organizacion') this.cargarTerritorio();
    if (seccion === 'administradores') this.cargarAdministradores();
  }

  protected cambiarDepartamento(): void {
    this.perfil.codigoMunicipio = '';
    this.municipios.set([]);
    this.limpiarError('codigoDepartamento');
    if (!this.perfil.codigoDepartamento) return;
    this.servicioPanel.obtenerMunicipios(this.perfil.codigoDepartamento).subscribe({
      next: municipios => this.municipios.set(municipios),
      error: () => this.errorGeneral.set('No fue posible cargar los municipios. Inténtalo nuevamente.')
    });
  }

  protected guardarPerfil(): void {
    const organizacion = this.organizacionActiva();
    if (!organizacion) return;
    const errores: Record<string, string> = {};
    if (!this.perfil.nombre.trim()) errores['nombre'] = 'Ingresa el nombre de la organización.';
    if (!this.perfil.codigoDepartamento) errores['codigoDepartamento'] = 'Selecciona un departamento.';
    if (!this.perfil.codigoMunicipio) errores['codigoMunicipio'] = 'Selecciona un municipio.';
    if (Object.keys(errores).length) { this.erroresCampos.set(errores); this.enfocarPrimerError(errores); return; }
    this.guardando.set(true);
    this.errorGeneral.set(null);
    this.mensajeExito.set(null);
    this.servicioPanel.actualizarOrganizacion(organizacion.id, {
      nombre: this.perfil.nombre,
      numeroIdentificacion: this.perfil.numeroIdentificacion || undefined,
      codigoDepartamento: this.perfil.codigoDepartamento,
      codigoMunicipio: this.perfil.codigoMunicipio
    }).subscribe({
      next: () => { this.guardando.set(false); this.mensajeExito.set('Los datos de la organización se actualizaron.'); this.cargarContexto(organizacion.id); },
      error: error => { this.guardando.set(false); this.procesarError(error); }
    });
  }

  protected limpiarError(campo: string): void {
    const errores = { ...this.erroresCampos() };
    delete errores[campo];
    this.erroresCampos.set(errores);
  }

  protected cerrarSesion(): void {
    this.servicioAcceso.cerrarSesion().subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => this.errorGeneral.set('No fue posible cerrar la sesión. Inténtalo nuevamente.')
    });
  }

  protected fecha(fecha: string): string {
    return new Intl.DateTimeFormat('es-CO', { dateStyle: 'long' }).format(new Date(fecha));
  }

  private cargarContexto(idAConservar?: string): void {
    this.cargando.set(true);
    this.servicioPanel.obtenerContexto().subscribe({
      next: contexto => {
        this.contexto.set(contexto);
        const activa = contexto.organizaciones.find(organizacion => organizacion.id === idAConservar) ?? contexto.organizaciones[0] ?? null;
        this.organizacionActiva.set(activa);
        if (activa) this.prepararPerfil(activa);
        this.cargando.set(false);
      },
      error: error => {
        this.cargando.set(false);
        if (error instanceof HttpErrorResponse && error.status === 401) void this.router.navigateByUrl('/');
        else this.errorGeneral.set('No fue posible cargar tu panel. Inténtalo nuevamente.');
      }
    });
  }

  private prepararPerfil(organizacion: OrganizacionPanel): void {
    this.perfil.nombre = organizacion.nombre;
    this.perfil.numeroIdentificacion = organizacion.numeroIdentificacion ?? '';
    this.perfil.codigoDepartamento = organizacion.codigoDepartamento;
    this.perfil.codigoMunicipio = organizacion.codigoMunicipio;
  }

  private cargarTerritorio(): void {
    if (!this.departamentos().length) {
      this.servicioPanel.obtenerDepartamentos().subscribe({
        next: departamentos => this.departamentos.set(departamentos),
        error: () => this.errorGeneral.set('No fue posible cargar los departamentos. Inténtalo nuevamente.')
      });
    }
    const departamento = this.perfil.codigoDepartamento;
    if (departamento && !this.municipios().length) {
      this.servicioPanel.obtenerMunicipios(departamento).subscribe({
        next: municipios => this.municipios.set(municipios),
        error: () => this.errorGeneral.set('No fue posible cargar los municipios. Inténtalo nuevamente.')
      });
    }
  }

  private cargarAdministradores(): void {
    const organizacion = this.organizacionActiva();
    if (!organizacion) return;
    this.cargandoAdministradores.set(true);
    this.servicioPanel.obtenerAdministradores(organizacion.id).subscribe({
      next: administradores => { this.administradores.set(administradores); this.cargandoAdministradores.set(false); },
      error: error => { this.cargandoAdministradores.set(false); this.procesarError(error); }
    });
  }

  private procesarError(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) { void this.router.navigateByUrl('/'); return; }
    if (error instanceof HttpErrorResponse && (error.status === 422 || error.status === 409)) {
      const campos = (error.error as ErrorApi | null)?.campos ?? {};
      const errores = Object.fromEntries(Object.entries(campos).map(([campo, mensajes]) => [campo, mensajes[0]]));
      this.erroresCampos.set(errores);
      if (Object.keys(errores).length) { this.enfocarPrimerError(errores); return; }
      this.errorGeneral.set((error.error as ErrorApi | null)?.mensaje ?? 'No fue posible guardar los cambios.');
      return;
    }
    if (error instanceof HttpErrorResponse && error.status === 403) this.errorGeneral.set('No tienes permiso para administrar esta organización.');
    else this.errorGeneral.set('No fue posible completar la acción. Inténtalo nuevamente.');
  }

  private enfocarPrimerError(errores: Record<string, string>): void {
    const campo = Object.keys(errores)[0];
    queueMicrotask(() => document.getElementById(`perfil-${campo}`)?.focus());
  }
}
