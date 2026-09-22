import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApiClientService } from '../http/api-client.service';
import { ObservacionDeCampo, RevisionDeCampos } from './revision-de-campos';

/**
 * Marcar un cambio como atendido, desde el panel de la organización.
 *
 * <b>VIVE EN `core` Y NO EN EL PANEL.</b> Es la mitad que atiende de una conversación cuya otra
 * mitad —quien pide— vive en la consola institucional, y las dos hablan del mismo dato. Puesto en
 * cualquiera de los dos paneles, el otro tendría que importar de un `features/` ajeno.
 */
@Injectable({ providedIn: 'root' })
export class CambiosPedidosApi {
  private readonly api = inject(ApiClientService);

  obtener(festivalId: string): Observable<RevisionDeCampos> {
    return this.api.get<RevisionDeCampos>(
      `/api/v1/externo/festivales/${festivalId}/cambios-pedidos`,
      { errorFallback: 'No fue posible consultar los cambios pedidos' },
    );
  }

  atender(observacionId: number, atendida: boolean): Observable<ObservacionDeCampo> {
    return this.api.get<{ requestToken: string }>('/api/v1/externo/organizaciones/csrf', {
      errorFallback: 'No fue posible preparar la marca',
    }).pipe(
      switchMap(token => this.api.post<ObservacionDeCampo>(
        `/api/v1/externo/cambios-pedidos/${observacionId}/atender`,
        { atendida },
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible marcar el cambio',
        },
      )),
    );
  }
}
