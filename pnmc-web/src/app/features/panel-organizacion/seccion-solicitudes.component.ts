import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { LucideTriangleAlert } from '@lucide/angular';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { tonoDelEstado } from '../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs/operators';
import { ApiClientService } from '../../core/http/api-client.service';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { FestivalDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';

interface ReclamacionAdministracion {
  id: string;
  dominio: string;
  registroCanonicoId: string;
  estado: string;
  justificacion: string;
  motivoDecision?: string | null;
  fechaCreacion: string;
  fechaDecision?: string | null;
  registroNombre?: string | null;
}

interface ConciliacionAdministracion {
  reclamacionId: string;
  valoresHistoricosJson: string;
  valoresBorradorJson: string;
}

/** Bandeja de acciones pendientes y trámites administrativos de la organización. */
@Component({
  selector: 'app-seccion-solicitudes',
  standalone: true,
  imports: [CommonModule, RouterLink, IndicadorDeEstadoComponent, LucideTriangleAlert],
  templateUrl: './seccion-solicitudes.component.html',
})
export class SeccionSolicitudesComponent {
  /** El tono del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;

  private readonly http = inject(ApiClientService);
  private readonly store = inject(PanelOrganizacionStore);
  private readonly api = inject(PanelOrganizacionApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private solicitudDirigida = '';
  private accionDirigida = '';

  readonly organizacionId = this.store.organizacionId;
  readonly reclamaciones = signal<ReclamacionAdministracion[]>([]);
  readonly festivales = signal<FestivalDeLaOrganizacion[]>([]);
  readonly cargando = signal(false);
  readonly cargandoAjustes = signal(false);
  readonly error = signal('');
  readonly ocupado = signal(false);
  readonly aclaracionAbierta = signal<string | null>(null);
  readonly respuestaAclaracion = signal('');
  readonly conciliacion = signal<ConciliacionAdministracion | null>(null);
  readonly selecciones = signal<Record<string, string>>({});

  readonly activas = computed(() => this.reclamaciones().filter(reclamacion => this.esActiva(reclamacion)));
  readonly cerradas = computed(() => this.reclamaciones().filter(reclamacion => !this.esActiva(reclamacion)));
  readonly ajustesPendientes = computed(() => this.festivales().filter(festival => this.estadoNormalizado(festival.estado) === 'ajustessolicitados'));

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(parametros => {
      this.solicitudDirigida = parametros.get('solicitud') ?? '';
      this.accionDirigida = parametros.get('accion') ?? '';
      this.aplicarDestinoDelAviso();
    });
    effect(() => {
      const organizacionId = this.organizacionId();
      if (organizacionId) {
        this.cargar(organizacionId);
        this.cargarAjustes(organizacionId);
      }
    });
  }

  etiquetaEstado(estado: string): string {
    return ({ borrador: 'Borrador', enviada: 'Enviada', en_revision: 'En revisión', requiere_aclaracion: 'Requiere aclaración', aclaracion_enviada: 'Aclaración enviada', aprobada: 'Aprobada', rechazada: 'Rechazada', cancelada: 'Cancelada' } as Record<string, string>)[estado] ?? 'Sin estado';
  }

  claseEstado(estado: string): string {
    if (estado === 'requiere_aclaracion') return 'border-amber-200 bg-amber-50 text-amber-900';
    if (estado === 'aprobada') return 'border-emerald-200 bg-emerald-50 text-emerald-900';
    if (estado === 'rechazada') return 'border-red-200 bg-red-50 text-red-800';
    if (estado === 'cancelada') return 'border-slate-200 bg-slate-100 text-slate-600';
    return 'border-violet-200 bg-violet-50 text-morado';
  }

  descripcionEstado(reclamacion: ReclamacionAdministracion): string {
    if (reclamacion.estado === 'requiere_aclaracion') return 'La institución requiere información adicional para continuar con este trámite.';
    if (reclamacion.estado === 'aprobada') return 'Antes de transferir la administración, debes conciliar los datos que cambiaron.';
    if (reclamacion.estado === 'rechazada') return reclamacion.motivoDecision || 'La institución cerró este trámite sin aprobarlo.';
    if (reclamacion.estado === 'cancelada') return 'Este trámite fue cancelado por la organización.';
    if (reclamacion.estado === 'aclaracion_enviada') return 'Tu aclaración fue enviada y está pendiente de revisión.';
    return 'La institución está revisando este trámite.';
  }

  titulo(reclamacion: ReclamacionAdministracion): string { return reclamacion.registroNombre || `Festival #${reclamacion.registroCanonicoId}`; }
  esActiva(reclamacion: ReclamacionAdministracion): boolean { return !['rechazada', 'cancelada'].includes(reclamacion.estado); }
  puedeCancelar(reclamacion: ReclamacionAdministracion): boolean { return ['borrador', 'enviada', 'en_revision', 'requiere_aclaracion', 'aclaracion_enviada'].includes(reclamacion.estado); }

  resolverAjustes(festival: FestivalDeLaOrganizacion): void {
    this.router.navigate(['/gestion/procesos/festivales', festival.id], {
      queryParams: { seccion: 'informacion', ajustes: '1' },
    });
  }

  abrirAclaracion(reclamacion: ReclamacionAdministracion): void { this.aclaracionAbierta.set(reclamacion.id); this.respuestaAclaracion.set(''); }

  enviarAclaracion(reclamacion: ReclamacionAdministracion): void {
    const respuesta = this.respuestaAclaracion().trim();
    if (!respuesta) { this.error.set('Escribe la aclaración antes de enviarla.'); return; }
    this.ejecutarEscritura(`/api/v1/externo/reclamaciones-administracion/${reclamacion.id}/aclaraciones/responder`, { respuesta }, () => { this.aclaracionAbierta.set(null); this.respuestaAclaracion.set(''); });
  }

  cancelar(reclamacion: ReclamacionAdministracion): void { this.ejecutarEscritura(`/api/v1/externo/reclamaciones-administracion/${reclamacion.id}/cancelar`, {}, () => undefined); }

  abrirConciliacion(reclamacion: ReclamacionAdministracion): void {
    this.error.set('');
    this.http.get<ConciliacionAdministracion>(`/api/v1/externo/reclamaciones-administracion/${reclamacion.id}/conciliacion`, { errorFallback: 'No fue posible cargar la conciliación.' }).subscribe({
      next: fila => {
        this.conciliacion.set(fila);
        const historico = this.leerJson(fila.valoresHistoricosJson);
        const borrador = this.leerJson(fila.valoresBorradorJson);
        const selecciones: Record<string, string> = {};
        for (const clave of Object.keys(historico)) selecciones[clave] = historico[clave] === borrador[this.claveBorrador(clave)] ? 'historico' : 'borrador';
        this.selecciones.set(selecciones);
      },
      error: () => undefined,
    });
  }

  camposConciliacion(): { clave: string; historico: unknown; borrador: unknown }[] {
    const fila = this.conciliacion();
    if (!fila) return [];
    const historico = this.leerJson(fila.valoresHistoricosJson);
    const borrador = this.leerJson(fila.valoresBorradorJson);
    return Object.keys(historico).filter(clave => historico[clave] !== borrador[this.claveBorrador(clave)]).map(clave => ({ clave, historico: historico[clave], borrador: borrador[this.claveBorrador(clave)] }));
  }

  elegir(clave: string, valor: string): void { this.selecciones.update(actual => ({ ...actual, [clave]: valor })); }
  resolverConciliacion(): void { const fila = this.conciliacion(); if (fila) this.ejecutarEscritura(`/api/v1/externo/reclamaciones-administracion/${fila.reclamacionId}/conciliacion/resolver`, { selecciones: this.selecciones() }, () => this.conciliacion.set(null)); }

  private cargar(organizacionId: string): void {
    this.cargando.set(true); this.error.set('');
    this.http.get<ReclamacionAdministracion[]>(`/api/v1/externo/organizaciones/${organizacionId}/reclamaciones-administracion`, { errorFallback: 'No fue posible consultar las solicitudes administrativas.' }).subscribe({
      next: filas => {
        this.reclamaciones.set(filas ?? []);
        this.cargando.set(false);
        this.aplicarDestinoDelAviso();
      },
      error: () => { this.error.set('No fue posible consultar las solicitudes administrativas. Inténtalo de nuevo.'); this.cargando.set(false); },
    });
  }

  private cargarAjustes(organizacionId: string): void {
    this.cargandoAjustes.set(true);
    this.api.obtenerFestivales(organizacionId).subscribe({
      next: festivales => { this.festivales.set(festivales); this.cargandoAjustes.set(false); },
      error: () => this.cargandoAjustes.set(false),
    });
  }

  private ejecutarEscritura(ruta: string, cuerpo: unknown, alTerminar: () => void): void {
    if (this.ocupado()) return;
    this.ocupado.set(true); this.error.set('');
    this.http.get<{ requestToken: string }>('/api/v1/externo/organizaciones/csrf', { errorFallback: 'No fue posible preparar la solicitud.' }).pipe(switchMap(token => this.http.post(ruta, cuerpo, { headers: { 'X-CSRF-TOKEN': token.requestToken }, errorFallback: 'No fue posible actualizar la solicitud.' }))).subscribe({
      next: () => { this.ocupado.set(false); alTerminar(); this.cargar(this.organizacionId()); this.cargarAjustes(this.organizacionId()); },
      error: () => { this.error.set('No fue posible actualizar la solicitud. Inténtalo de nuevo.'); this.ocupado.set(false); },
    });
  }

  private leerJson(valor: string): Record<string, unknown> { try { return JSON.parse(valor) as Record<string, unknown>; } catch { return {}; } }
  private estadoNormalizado(estado: string | null | undefined): string { return (estado ?? '').replace(/[_\s-]/g, '').toLowerCase(); }
  private claveBorrador(clave: string): string { return ({ Name: 'nombre', Description: 'descripcion', ContactEmail: 'correoContacto', ContactPhone: 'telefonoCelular', InstagramUrl: 'instagram', FacebookUrl: 'facebook', WebsiteUrl: 'paginaWeb', OtherUrl: 'otroEnlace', Periodicidad: 'periodicidad' } as Record<string, string>)[clave] ?? clave; }

  private aplicarDestinoDelAviso(): void {
    if (!this.solicitudDirigida || !this.accionDirigida) return;
    const reclamacion = this.reclamaciones().find(item => item.id === this.solicitudDirigida);
    if (!reclamacion) return;
    if (this.accionDirigida === 'aclarar' && reclamacion.estado === 'requiere_aclaracion') {
      this.abrirAclaracion(reclamacion);
      return;
    }
    if (this.accionDirigida === 'conciliar' && reclamacion.estado === 'aprobada' && !this.conciliacion()) {
      this.abrirConciliacion(reclamacion);
    }
  }
}
