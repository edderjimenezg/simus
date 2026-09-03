import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ServicioAcceso } from './core/servicio-acceso';
import { FestivalPanel, ServicioFestivales } from './core/servicio-festivales';
import { AdministradorOrganizacion, ContextoPanel, OrganizacionPanel, ServicioPanelOrganizacion } from './core/servicio-panel-organizacion';
import { TerritorioRegistro } from './core/servicio-registro-externo';
import { ServicioProteccionSolicitud } from './core/servicio-proteccion-solicitud';

type SeccionPanel = 'resumen' | 'organizacion' | 'administradores' | 'procesos' | 'cuenta';
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
  private readonly servicioFestivales = inject(ServicioFestivales);
  private readonly proteccion = inject(ServicioProteccionSolicitud);
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
  protected readonly festivales = signal<FestivalPanel[]>([]);
  protected readonly cargandoFestivales = signal(false);
  protected readonly editandoFestival = signal<FestivalPanel | null>(null);
  protected readonly guardandoFestival = signal(false);
  protected readonly perfil = { nombre: '', numeroIdentificacion: '', codigoDepartamento: '', codigoMunicipio: '' };
  protected readonly formularioFestival = { nombre: '', descripcion: '', codigoDepartamento: '', codigoMunicipio: '' };

  constructor() {
    if (this.router.url.endsWith('/procesos')) this.seccion.set('procesos');
    this.cargarContexto();
  }

  protected seleccionarOrganizacion(id: string): void {
    const siguiente = this.contexto()?.organizaciones.find(organizacion => organizacion.id === id) ?? null;
    if (!siguiente) return;
    this.organizacionActiva.set(siguiente);
    this.prepararPerfil(siguiente);
    this.administradores.set([]);
    if (this.seccion() === 'administradores') this.cargarAdministradores();
    if (this.seccion() === 'procesos') this.cargarFestivales();
  }

  protected cambiarSeccion(seccion: SeccionPanel): void {
    this.seccion.set(seccion);
    this.errorGeneral.set(null);
    this.mensajeExito.set(null);
    if (seccion === 'organizacion') this.cargarTerritorio();
    if (seccion === 'administradores') this.cargarAdministradores();
    if (seccion === 'procesos') this.cargarFestivales();
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

  protected cambiarDepartamentoFestival(): void {
    this.formularioFestival.codigoMunicipio = '';
    this.municipios.set([]);
    this.limpiarError('codigoDepartamentoFestival');
    if (!this.formularioFestival.codigoDepartamento) return;
    this.servicioPanel.obtenerMunicipios(this.formularioFestival.codigoDepartamento).subscribe({
      next: municipios => this.municipios.set(municipios),
      error: () => this.errorGeneral.set('No fue posible cargar los municipios. Inténtalo nuevamente.')
    });
  }

  protected abrirNuevoFestival(): void {
    const organizacion = this.organizacionActiva();
    if (!organizacion) return;
    this.editandoFestival.set(null);
    this.formularioFestival.nombre = '';
    this.formularioFestival.descripcion = '';
    this.formularioFestival.codigoDepartamento = organizacion.codigoDepartamento;
    this.formularioFestival.codigoMunicipio = organizacion.codigoMunicipio;
    this.erroresCampos.set({});
    this.mensajeExito.set(null);
    this.cargarTerritorioFestival();
  }

  protected continuarFestival(festival: FestivalPanel): void {
    this.editandoFestival.set(festival);
    this.formularioFestival.nombre = festival.nombre;
    this.formularioFestival.descripcion = festival.descripcion ?? '';
    this.formularioFestival.codigoDepartamento = festival.codigoDepartamento;
    this.formularioFestival.codigoMunicipio = festival.codigoMunicipio;
    this.erroresCampos.set({});
    this.mensajeExito.set(null);
    this.cargarTerritorioFestival();
  }

  protected cerrarFormularioFestival(): void {
    this.editandoFestival.set(null);
    this.formularioFestival.nombre = '';
    this.formularioFestival.descripcion = '';
    this.erroresCampos.set({});
  }

  protected guardarFestival(): void {
    const organizacion = this.organizacionActiva();
    if (!organizacion) return;
    const errores: Record<string, string> = {};
    if (!this.formularioFestival.nombre.trim()) errores['nombreFestival'] = 'Ingresa el nombre del Festival.';
    if (!this.formularioFestival.codigoDepartamento) errores['codigoDepartamentoFestival'] = 'Selecciona un departamento.';
    if (!this.formularioFestival.codigoMunicipio) errores['codigoMunicipioFestival'] = 'Selecciona un municipio.';
    if (Object.keys(errores).length) { this.erroresCampos.set(errores); this.enfocarPrimerError(errores, 'festival-'); return; }
    const solicitud = {
      nombre: this.formularioFestival.nombre,
      descripcion: this.formularioFestival.descripcion || undefined,
      codigoDepartamento: this.formularioFestival.codigoDepartamento,
      codigoMunicipio: this.formularioFestival.codigoMunicipio
    };
    this.guardandoFestival.set(true);
    this.errorGeneral.set(null);
    const festival = this.editandoFestival();
    const operacion: Observable<unknown> = festival
      ? this.servicioFestivales.actualizarBorrador(festival.idFestival, solicitud)
      : this.servicioFestivales.crear(organizacion.id, solicitud);
    operacion.subscribe({
      next: () => {
        this.guardandoFestival.set(false);
        this.mensajeExito.set(festival ? 'El borrador del Festival se actualizó.' : 'El Festival se guardó como borrador.');
        this.cerrarFormularioFestival();
        this.cargarFestivales();
      },
      error: error => { this.guardandoFestival.set(false); this.procesarErrorFestival(error); }
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
    this.proteccion.preparar().subscribe({
      next: () => this.cargarContextoProtegido(idAConservar),
      error: error => {
        this.cargando.set(false);
        if (error instanceof HttpErrorResponse && error.status === 401) void this.router.navigateByUrl('/');
        else this.errorGeneral.set('No fue posible preparar una sesión segura. Inténtalo nuevamente.');
      }
    });
  }

  private cargarContextoProtegido(idAConservar?: string): void {
    this.cargando.set(true);
    this.servicioPanel.obtenerContexto().subscribe({
      next: contexto => {
        this.contexto.set(contexto);
        const activa = contexto.organizaciones.find(organizacion => organizacion.id === idAConservar) ?? contexto.organizaciones[0] ?? null;
        this.organizacionActiva.set(activa);
        if (activa) this.prepararPerfil(activa);
        this.cargarFestivales();
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

  private cargarTerritorioFestival(): void {
    if (!this.departamentos().length) {
      this.servicioPanel.obtenerDepartamentos().subscribe({
        next: departamentos => this.departamentos.set(departamentos),
        error: () => this.errorGeneral.set('No fue posible cargar los departamentos. Inténtalo nuevamente.')
      });
    }
    const departamento = this.formularioFestival.codigoDepartamento;
    if (departamento) {
      this.servicioPanel.obtenerMunicipios(departamento).subscribe({
        next: municipios => this.municipios.set(municipios),
        error: () => this.errorGeneral.set('No fue posible cargar los municipios. Inténtalo nuevamente.')
      });
    }
  }

  private cargarFestivales(): void {
    this.cargandoFestivales.set(true);
    this.servicioFestivales.listar().subscribe({
      next: festivales => { this.festivales.set(festivales); this.cargandoFestivales.set(false); },
      error: error => {
        this.cargandoFestivales.set(false);
        if (error instanceof HttpErrorResponse && error.status === 401) void this.router.navigateByUrl('/');
        else this.errorGeneral.set('No fue posible cargar los Festivales. Inténtalo nuevamente.');
      }
    });
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

  private procesarErrorFestival(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) { void this.router.navigateByUrl('/'); return; }
    if (error instanceof HttpErrorResponse && (error.status === 422 || error.status === 409)) {
      const campos = (error.error as ErrorApi | null)?.campos ?? {};
      const traducciones: Record<string, string> = { nombre: 'nombreFestival', codigoDepartamento: 'codigoDepartamentoFestival', codigoMunicipio: 'codigoMunicipioFestival', descripcion: 'descripcionFestival' };
      const errores = Object.fromEntries(Object.entries(campos).map(([campo, mensajes]) => [traducciones[campo] ?? campo, mensajes[0]]));
      this.erroresCampos.set(errores);
      if (Object.keys(errores).length) { this.enfocarPrimerError(errores, 'festival-'); return; }
      this.errorGeneral.set((error.error as ErrorApi | null)?.mensaje ?? 'No fue posible guardar el Festival.');
      return;
    }
    this.errorGeneral.set('No fue posible guardar el Festival. Inténtalo nuevamente.');
  }

  private enfocarPrimerError(errores: Record<string, string>, prefijo = 'perfil-'): void {
    const campo = Object.keys(errores)[0];
    queueMicrotask(() => document.getElementById(`${prefijo}${campo}`)?.focus());
  }
}
