import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, timeout } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

const API_BASE_URL = environment.apiBaseUrl.replace(/\/$/, '');

const esRegistro = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

const HTTP_STATUS_LABELS: Record<number, string> = {
  400: 'Solicitud inválida',
  401: 'No autenticado',
  403: 'Acceso denegado',
  404: 'Recurso no encontrado',
  409: 'Conflicto de datos',
  422: 'Validación fallida',
  429: 'Demasiadas solicitudes',
  500: 'Error interno del servidor',
  502: 'El servidor no está disponible',
  503: 'Servicio no disponible',
  504: 'Tiempo de espera del servidor',
};

export class ApiError extends Error {
  override name = 'ApiError';
  code: string;
  status: number | null;
  path: string;
  payload: unknown;
  requestId: string;
  technicalMessage: string;

  constructor(
    message: string,
    options: {
      code?: string;
      status?: number | null;
      path?: string;
      payload?: unknown;
      requestId?: string;
      technicalMessage?: string;
    } = {}
  ) {
    super(message);
    this.code = options.code || 'API_ERROR';
    this.status = options.status !== undefined ? options.status : null;
    this.path = options.path || '';
    this.payload = options.payload || null;
    this.requestId = options.requestId || '';
    this.technicalMessage = options.technicalMessage || '';
  }
}

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {
  private readonly http = inject(HttpClient);

  private generateCorrelationId(): string {
    try {
      if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
        return crypto.randomUUID();
      }
    } catch {
      // fallback
    }
    return `req-${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
  }

  private normalizeMessagePart(value: unknown): string {
    return typeof value === 'string' ? value.trim() : '';
  }

  private extractValidationMessage(errors: unknown = {}): string {
    if (!esRegistro(errors)) return '';
    return Object.entries(errors)
      .flatMap(([field, value]) => {
        const messages = Array.isArray(value) ? value : [value];
        return messages
          .map((msg) => this.normalizeMessagePart(msg))
          .filter(Boolean)
          .map((msg) => `${field}: ${msg}`);
      })
      .join(' ');
  }

  /**
   * Los mensajes que el cuerpo de un error trae listos para enseñar.
   *
   * SE LEEN DOS FORMAS, y la segunda es la que faltaba. La primera es ProblemDetails
   * —`errors`, `detail`, `message`, `title`—, que es lo que produce la validación automática de
   * ASP.NET. La segunda es el mapa escueto que devuelven los endpoints escritos a mano:
   * `{ "estado": ["No se puede publicar: no tiene cuerpo; no tiene fecha."] }`.
   *
   * MIENTRAS NO SE LEIA LA SEGUNDA, esos motivos no llegaban a pantalla y quien administra veía
   * «Error al enviar datos al backend» en lugar de la lista de lo que falta. Se corrige AQUI y no
   * en cada servicio porque el defecto era de lectura, no de cada panel: arreglado en un sitio,
   * vale para todos a la vez.
   */
  private extractPayloadMessage(payload: unknown): string {
    if (!esRegistro(payload)) return '';

    const validationMessage = esRegistro(payload['errors'])
      ? this.extractValidationMessage(payload['errors'])
      : '';

    return (
      validationMessage ||
      this.normalizeMessagePart(payload['detail']) ||
      this.normalizeMessagePart(payload['message']) ||
      this.normalizeMessagePart(payload['title']) ||
      this.extractBareFieldMessage(payload)
    );
  }

  /**
   * El primer mensaje de un mapa `campo: [mensajes]` sin envoltorio.
   *
   * SE DEVUELVE SOLO EL MENSAJE, sin el nombre del campo: el texto del servidor ya es una frase
   * completa —«No se puede publicar: no tiene cuerpo»— y anteponerle «estado: » la estropea.
   * `extractValidationMessage` sí lo antepone porque allí los mensajes son de validación de campo
   * y sin el campo no se sabe cuál falla.
   */
  private extractBareFieldMessage(payload: Record<string, unknown>): string {
    for (const valor of Object.values(payload)) {
      if (!Array.isArray(valor)) continue;
      const mensaje = valor.map((m) => this.normalizeMessagePart(m)).find(Boolean);
      if (mensaje) return mensaje;
    }
    return '';
  }

  private extractRequestId(payload: unknown): string {
    if (!esRegistro(payload)) return '';
    const extensions = esRegistro(payload['extensions']) ? payload['extensions'] : undefined;
    return (
      this.normalizeMessagePart(payload['traceId']) ||
      this.normalizeMessagePart(payload['requestId']) ||
      this.normalizeMessagePart(payload['correlationId']) ||
      this.normalizeMessagePart(extensions?.['traceId'])
    );
  }

  private buildErrorMessage(options: {
    code: string;
    fallback: string;
    status?: number | null;
    detail?: string;
    path?: string;
    requestId?: string;
  }): string {
    const statusLabel = options.status ? HTTP_STATUS_LABELS[options.status] : '';
    const parts = [`[${options.code}] ${options.fallback}`];

    if (options.status) {
      parts.push(`Estado HTTP: ${options.status}${statusLabel ? ` ${statusLabel}` : ''}`);
    }
    if (options.detail) {
      parts.push(`Detalle: ${options.detail}`);
    }
    if (options.path) {
      parts.push(`Ruta: ${options.path}`);
    }
    if (options.requestId) {
      parts.push(`ID de solicitud: ${options.requestId}`);
    }

    return parts.join(' | ');
  }

  private buildUserErrorMessage(options: {
    code: string;
    fallback: string;
    detail?: string;
  }): string {
    const cleanFallback = this.normalizeMessagePart(options.fallback).replace(/[.!?]+$/, '');
    const cleanDetail = this.normalizeMessagePart(options.detail).replace(/[.!?]+$/, '');
    const parts = [cleanFallback || 'Ocurrió un error inesperado'];

    if (cleanDetail) {
      parts.push(cleanDetail);
    }

    // El código técnico queda en `ApiError.code` y `technicalMessage` para trazabilidad,
    // pero nunca se muestra como instrucción a quien diligencia un formulario.
    return `${parts.join('. ')}.`;
  }

  private getFullUrl(path: string): string {
    if (/^https?:\/\//i.test(path)) {
      return path;
    }
    const cleanPath = path.startsWith('/') ? path : `/${path}`;
    return `${API_BASE_URL}${cleanPath}`;
  }

  private getRequestHeaders(customHeaders: Record<string, string> = {}): HttpHeaders {
    let headers = new HttpHeaders({
      'Accept': 'application/json',
      'X-Correlation-ID': this.generateCorrelationId()
    });

    Object.entries(customHeaders).forEach(([key, val]) => {
      headers = headers.set(key, val);
    });

    return headers;
  }

  private handleError(error: unknown, fallbackMessage: string, url: string): Observable<never> {
    if (error instanceof HttpErrorResponse) {
      const status = error.status;
      const code = `HTTP_${status}`;
      const payload = error.error;
      const requestId = this.extractRequestId(payload);
      const detail = this.extractPayloadMessage(payload);

      const technicalMessage = this.buildErrorMessage({
        code,
        fallback: fallbackMessage,
        status,
        detail,
        path: url,
        requestId
      });

      return throwError(() => new ApiError(
        this.buildUserErrorMessage({
          code,
          fallback: fallbackMessage,
          detail
        }),
        {
          code,
          status,
          path: url,
          payload,
          requestId,
          technicalMessage
        }
      ));
    }

    // Timeout o problemas de conexión
    if (error instanceof Error && error.name === 'TimeoutError') {
      const detail = 'La solicitud tardó demasiado en responder';
      const technicalMessage = this.buildErrorMessage({
        code: 'TIMEOUT',
        fallback: fallbackMessage,
        detail,
        path: url
      });

      return throwError(() => new ApiError(
        this.buildUserErrorMessage({
          code: 'TIMEOUT',
          fallback: fallbackMessage,
          detail
        }),
        {
          code: 'TIMEOUT',
          path: url,
          technicalMessage
        }
      ));
    }

    // Errores de red normales
    const detail = 'No fue posible conectar con el servidor. Verifica que la API esté activa.';
    const technicalMessage = this.buildErrorMessage({
      code: 'NETWORK',
      fallback: fallbackMessage,
      detail,
      path: url
    });

    return throwError(() => new ApiError(
      this.buildUserErrorMessage({
        code: 'NETWORK',
        fallback: fallbackMessage,
        detail: 'No fue posible conectar con el servidor'
      }),
      {
        code: 'NETWORK',
        path: url,
        technicalMessage
      }
    ));
  }

  get<T>(
    path: string,
    options: {
      params?: HttpParams | Record<string, string | number | boolean>;
      headers?: Record<string, string>;
      timeoutMs?: number;
      errorFallback?: string;
    } = {}
  ): Observable<T> {
    const url = this.getFullUrl(path);
    const headers = this.getRequestHeaders(options.headers);
    const timeoutMs = options.timeoutMs ?? 20000;
    // «BACKEND» NO ES UNA PALABRA QUE LE DIGA NADA A QUIEN USA EL SISTEMA. Estos textos son el
    // último recurso —lo que se lee cuando quien llama no puso un mensaje propio— y acababan en
    // pantalla tal cual: medido, «Mis archivos» de una cuenta sin
    // organización mostraba «Error al consultar backend». Se dicen en el idioma de quien lee.
    const fallback = options.errorFallback ?? 'No fue posible consultar la información.';

    return this.http.get<T>(url, { headers, params: options.params, withCredentials: true }).pipe(
      timeout(timeoutMs),
      catchError((err) => this.handleError(err, fallback, url))
    );
  }

  post<T>(
    path: string,
    body: unknown,
    options: {
      headers?: Record<string, string>;
      timeoutMs?: number;
      errorFallback?: string;
    } = {}
  ): Observable<T> {
    const url = this.getFullUrl(path);
    const headers = this.getRequestHeaders({
      'Content-Type': 'application/json',
      ...options.headers
    });
    const timeoutMs = options.timeoutMs ?? 20000;
    const fallback = options.errorFallback ?? 'No fue posible enviar la información.';

    return this.http.post<T>(url, body, { headers, withCredentials: true }).pipe(
      timeout(timeoutMs),
      catchError((err) => this.handleError(err, fallback, url))
    );
  }

  /**
   * POST de un formulario multipart.
   *
   * NO FIJA Content-Type, y esa es toda la razón por la que existe en vez de
   * reutilizar `post`. El navegador tiene que escribir esa cabecera él mismo,
   * porque incluye el `boundary` que separa las partes del cuerpo; ponerla a
   * mano —o dejar el `application/json` que `post` impone— produce un cuerpo que
   * el servidor no puede despiezar, y el síntoma es un 400 que no dice por qué.
   *
   * Es la primera ruta multipart de este cliente, igual que lo es en el API.
   */
  postForm<T>(
    path: string,
    body: FormData,
    options: {
      headers?: Record<string, string>;
      timeoutMs?: number;
      errorFallback?: string;
    } = {}
  ): Observable<T> {
    const url = this.getFullUrl(path);
    const headers = this.getRequestHeaders(options.headers);
    // Subir una imagen mueve hasta 2 MiB; el tope general de 20 s se queda corto
    // en una conexión lenta y el editor vería un error que no lo es.
    const timeoutMs = options.timeoutMs ?? 60000;
    const fallback = options.errorFallback ?? 'No fue posible subir el archivo.';

    return this.http.post<T>(url, body, { headers, withCredentials: true }).pipe(
      timeout(timeoutMs),
      catchError((err) => this.handleError(err, fallback, url))
    );
  }

  put<T>(
    path: string,
    body: unknown,
    options: {
      headers?: Record<string, string>;
      timeoutMs?: number;
      errorFallback?: string;
    } = {}
  ): Observable<T> {
    const url = this.getFullUrl(path);
    const headers = this.getRequestHeaders({
      'Content-Type': 'application/json',
      ...options.headers
    });
    const timeoutMs = options.timeoutMs ?? 20000;
    const fallback = options.errorFallback ?? 'No fue posible guardar los cambios.';

    return this.http.put<T>(url, body, { headers, withCredentials: true }).pipe(
      timeout(timeoutMs),
      catchError((err) => this.handleError(err, fallback, url))
    );
  }

  delete<T>(
    path: string,
    options: {
      headers?: Record<string, string>;
      timeoutMs?: number;
      errorFallback?: string;
    } = {}
  ): Observable<T> {
    const url = this.getFullUrl(path);
    const headers = this.getRequestHeaders(options.headers);
    const timeoutMs = options.timeoutMs ?? 20000;
    const fallback = options.errorFallback ?? 'No fue posible eliminar el registro.';

    return this.http.delete<T>(url, { headers, withCredentials: true }).pipe(
      timeout(timeoutMs),
      catchError((err) => this.handleError(err, fallback, url))
    );
  }
}
