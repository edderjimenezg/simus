import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, Subject, fromEvent, merge, timer } from 'rxjs';
import { catchError, distinctUntilChanged, exhaustMap, filter, map } from 'rxjs/operators';
import { ApiClientService } from '../http/api-client.service';

/** Intervalo corto, limitado a pestañas visibles, para recibir avisos sin recargar la página. */
export const INTERVALO_NOTIFICACIONES_MS = 4_000;
export type AmbitoDeNotificaciones = 'externo' | 'institucional';

export interface NotificacionEnVivo {
  id: string;
  recipientUserId: string | null;
  recipientEmail: string;
  eventType: string;
  channel: string;
  title: string;
  body: string;
  status: string;
  moduleId: string;
  recordId: string;
  createdAt: string;
  sentAt: string | null;
  readAt: string | null;
  metadataJson?: string | null;
}

export interface PaginaDeNotificacionesEnVivo {
  items: NotificacionEnVivo[];
  limit: number;
  offset: number;
  total: number;
}

export interface DestinoDeNotificacion {
  ruta: readonly (string | number)[];
  parametros: Record<string, string> | null;
  accion: string;
}

function metadatosDe(notificacion: NotificacionEnVivo): Record<string, unknown> {
  if (!notificacion.metadataJson) return {};
  try {
    return JSON.parse(notificacion.metadataJson) as Record<string, unknown>;
  } catch {
    return {};
  }
}

/**
 * Traduce eventos persistidos a destinos canónicos de Gestión.
 * Los eventos informativos devuelven `null`: leer un aviso no debe fingir que existe una tarea.
 */
export function destinoExternoDe(notificacion: NotificacionEnVivo): DestinoDeNotificacion | null {
  const evento = notificacion.eventType;
  if (['FestivalCambiosPedidosPorCampo', 'FestivalAjustesSolicitados'].includes(evento)
      && notificacion.recordId) {
    return {
      ruta: ['/gestion/procesos/festivales', notificacion.recordId],
      parametros: { seccion: 'informacion', ajustes: '1' },
      accion: 'Resolver ajustes',
    };
  }

  if (evento === 'FestivalPropuestaAjustesSolicitados') {
    const festivalId = String(metadatosDe(notificacion)['FestivalOrigenId'] ?? '').trim();
    if (!festivalId) return null;
    return {
      ruta: ['/gestion/procesos/festivales', festivalId],
      parametros: { seccion: 'informacion', editar: '1' },
      accion: 'Revisar propuesta',
    };
  }

  if (evento === 'ReclamacionRequiereAclaracion' && notificacion.recordId) {
    return {
      ruta: ['/gestion/solicitudes'],
      parametros: { solicitud: notificacion.recordId, accion: 'aclarar' },
      accion: 'Responder aclaración',
    };
  }

  if (evento === 'ReclamacionAprobadaPendienteConciliacion' && notificacion.recordId) {
    return {
      ruta: ['/gestion/solicitudes'],
      parametros: { solicitud: notificacion.recordId, accion: 'conciliar' },
      accion: 'Conciliar datos',
    };
  }

  return null;
}

/**
 * Un solo mecanismo de sondeo para las dos cabeceras autenticadas. Cada consumidor declara el
 * ámbito de su buzón para que dos sesiones simultáneas no mezclen avisos.
 *
 * La consulta es inmediata, se repite únicamente mientras la pestaña está visible y vuelve a
 * ejecutarse al recuperar foco o conexión. `exhaustMap` impide acumular peticiones si una lectura
 * tarda más que el intervalo. Un fallo transitorio no emite una lista vacía y, por tanto, no borra
 * avisos que la persona ya estaba leyendo.
 */
@Injectable({ providedIn: 'root' })
export class NotificacionesEnVivoService {
  private readonly api = inject(ApiClientService);
  private readonly documento = inject(DOCUMENT);
  private readonly actualizacionSolicitada = new Subject<void>();

  observar(ambito: AmbitoDeNotificaciones, limite = 50): Observable<PaginaDeNotificacionesEnVivo> {
    const ventana = this.documento.defaultView;
    const activadores: Observable<unknown>[] = [
      timer(0, INTERVALO_NOTIFICACIONES_MS),
      this.actualizacionSolicitada,
      fromEvent(this.documento, 'visibilitychange'),
    ];
    if (ventana) {
      activadores.push(fromEvent(ventana, 'focus'), fromEvent(ventana, 'online'));
    }

    return merge(...activadores).pipe(
      filter(() => !this.documento.hidden),
      exhaustMap(() => this.api.get<PaginaDeNotificacionesEnVivo>('/api/v1/notificaciones', {
        params: { ambito, limite },
        errorFallback: 'No fue posible actualizar las notificaciones',
      }).pipe(catchError(() => EMPTY))),
      map(pagina => ({ ...pagina, items: pagina?.items ?? [] })),
      distinctUntilChanged((anterior, actual) => JSON.stringify(anterior) === JSON.stringify(actual)),
    );
  }

  actualizarAhora(): void {
    this.actualizacionSolicitada.next();
  }
}
