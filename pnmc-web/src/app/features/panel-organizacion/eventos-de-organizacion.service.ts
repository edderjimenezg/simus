import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApiClientService } from '../../core/http/api-client.service';
import { AnunciarEventoSolicitud, EventoDeLaOrganizacion, ProcesoQueEnmarca } from './panel-organizacion.api';

/**
 * Los eventos que una organización anuncia sobre sus propios procesos.
 *
 * <b>POR QUÉ NO ESTÁ EN `PanelOrganizacionApi`.</b> Esa clase abstracta es el contrato de la ficha
 * de Festival, y la implementan también los dobles de prueba y el adaptador de la consola que
 * revisa fichas —que no anuncia eventos y nunca lo hará—. Meter aquí tres métodos más obligaría a
 * ocho dobles a declarar métodos que no usan, y al adaptador de revisión a negarlos uno por uno.
 * Los eventos son otra preocupación y viven en su propio servicio.
 *
 * <b>EL TESTIGO ANTIFALSIFICACIÓN ES EL DEL CANAL EXTERNO</b>, el mismo que usa el alta de
 * Festival: una sola forma de pedirlo, y no dos que puedan divergir.
 */
@Injectable({ providedIn: 'root' })
export class EventosDeOrganizacionService {
  private readonly api = inject(ApiClientService);

  private testigo(fallo: string): Observable<{ requestToken: string }> {
    return this.api.get<{ requestToken: string }>('/api/v1/externo/organizaciones/csrf', { errorFallback: fallo });
  }

  /**
   * Los procesos de la organización que pueden enmarcar un evento.
   *
   * SI VIENE VACIA, LA PANTALLA NO OFRECE EL FORMULARIO: una organización sin procesos publicados
   * no tiene qué anunciar en la agenda del Programa.
   */
  procesosQueEnmarcan(organizacionId: string): Observable<ProcesoQueEnmarca[]> {
    return this.api.get<ProcesoQueEnmarca[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/procesos-que-enmarcan`,
      { errorFallback: 'No fue posible consultar tus procesos' });
  }

  eventosDeLaOrganizacion(organizacionId: string): Observable<EventoDeLaOrganizacion[]> {
    return this.api.get<EventoDeLaOrganizacion[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/eventos`,
      { errorFallback: 'No fue posible consultar tus eventos' });
  }

  anunciarEvento(organizacionId: string, solicitud: AnunciarEventoSolicitud): Observable<{ id: string; estado: string }> {
    return this.testigo('No fue posible preparar el anuncio del evento').pipe(
      switchMap(token => this.api.post<{ id: string; estado: string }>(
        `/api/v1/externo/organizaciones/${organizacionId}/eventos`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible anunciar el evento',
        },
      )),
    );
  }
}
