import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { environment } from '../../../environments/environment';

/**
 * El texto de la autorización y su versión, tal como los sirve el API.
 *
 * NO SE ESCRIBE AQUÍ NI EN LA PLANTILLA. La persona tiene que aceptar exactamente el mismo texto
 * que el servidor va a guardar como evidencia; si el front tuviera su propia copia, bastaría una
 * edición en cualquiera de los dos lados para que la pantalla dijera una cosa y la base guardara
 * otra, y entonces la evidencia no probaría nada.
 *
 * Ver `pnmc-api/openapi.yaml`, ruta `/api/v1/publico/boletin/politica`.
 */
export interface PoliticaDeTratamiento {
  version: string;
  texto: string;
}

export interface RespuestaDeAlta {
  message: string;
}

/**
 * El origen de la suscripción. Hoy solo existe la portada; la columna `Origen` de
 * `dbo.BoletinSuscripciones` acepta más valores para el día que haya un segundo formulario.
 */
/** Desde qué pantalla se dio el alta. La lista blanca vive en el servidor. */
export type OrigenDeSuscripcion = 'portada' | 'noticias';

/** Una fila de la lista, tal como la sirve la consola. */
export interface SuscripcionDelBoletin {
  id: number;
  correo: string;
  origen: string;
  estado: 'activa' | 'baja';
  fechaAlta: string;
  fechaBaja: string | null;
  autorizacionTexto: string;
}

export interface ListadoDeSuscripciones {
  items: SuscripcionDelBoletin[];
  total: number;
  activas: number;
  limit: number;
  offset: number;
}

@Injectable({ providedIn: 'root' })
export class BoletinService {
  private readonly apiClient = inject(ApiClientService);
  private readonly http = inject(HttpClient);

  consultarPolitica(): Observable<PoliticaDeTratamiento> {
    return this.apiClient.get<PoliticaDeTratamiento>('/api/v1/publico/boletin/politica', {
      errorFallback: 'No fue posible cargar la política de tratamiento de datos.',
    });
  }

  /**
   * Da de alta un correo en el boletín.
   *
   * RESPONDE IGUAL SI EL CORREO YA ESTABA. Es una decisión del API, no un descuido: distinguir
   * los dos casos convertiría esta ruta anónima en un comprobador abierto de quién está en la
   * lista. Por eso la pantalla tampoco puede decir «ya estabas suscrito»: no lo sabe.
   */
  suscribir(correo: string, autorizaTratamiento: boolean, origen: OrigenDeSuscripcion = 'portada'): Observable<RespuestaDeAlta> {
    return this.apiClient.post<RespuestaDeAlta>('/api/v1/publico/boletin/suscripciones', {
      correo,
      autorizaTratamiento,
      origen,
    }, {
      errorFallback: 'No fue posible registrar el correo en el boletín.',
    });
  }

  // -------------------------------------------------------------------------------------
  // La consola, en Comunicaciones. Todo esto exige sesion institucional.
  // -------------------------------------------------------------------------------------

  consultarSuscripciones(opciones: { q?: string; estado?: string; orden?: string; direccion?: string; limit?: number; offset?: number } = {}): Observable<ListadoDeSuscripciones> {
    const params: Record<string, string | number> = {
      limit: opciones.limit ?? 25,
      offset: opciones.offset ?? 0,
    };
    if (opciones.q) params['q'] = opciones.q;
    if (opciones.estado) params['estado'] = opciones.estado;
    // EL ORDEN LO RESUELVE EL SERVIDOR: la lista pagina de veinticinco en veinticinco, así que
    // ordenar la página cargada reordena veinticinco correos y calla sobre el resto.
    if (opciones.orden) params['orden'] = opciones.orden;
    if (opciones.direccion) params['direccion'] = opciones.direccion;

    return this.apiClient.get<ListadoDeSuscripciones>('/api/v1/admin/comunicaciones/boletin/', {
      params,
      errorFallback: 'No fue posible consultar las suscripciones al boletín.',
    });
  }

  darDeBaja(id: number): Observable<{ message: string }> {
    return this.apiClient.post<{ message: string }>(`/api/v1/admin/comunicaciones/boletin/${id}/baja`, {}, {
      errorFallback: 'No fue posible dar de baja esa suscripción.',
    });
  }

  /**
   * El CSV, como blob.
   *
   * NO PASA POR `ApiClientService` porque ese cliente tipa toda respuesta como JSON, y un CSV
   * llegaría convertido en texto ya destrozado. Se usa `HttpClient` directamente con
   * `responseType: 'blob'` y `withCredentials`, que es lo que lleva la cookie de sesión.
   */
  descargarCsv(opciones: { q?: string; estado?: string } = {}): Observable<Blob> {
    const params: Record<string, string> = {};
    if (opciones.q) params['q'] = opciones.q;
    if (opciones.estado) params['estado'] = opciones.estado;

    const base = environment.apiBaseUrl.replace(/\/$/, '');
    return this.http.get(`${base}/api/v1/admin/comunicaciones/boletin/export.csv`, {
      params,
      responseType: 'blob',
      withCredentials: true,
    });
  }
}
