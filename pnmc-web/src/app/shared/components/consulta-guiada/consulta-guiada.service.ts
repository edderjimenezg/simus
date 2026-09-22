import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../../core/http/api-client.service';

/** Una pregunta que el catálogo sabe resolver. */
export interface ConsultaOfrecida {
  id: string;
  rotulo: string;
  descripcion: string;
}

/** Qué puede responder la Consulta Guiada para quien pregunta. */
export interface EstadoDeConsultaGuiada {
  disponible: boolean;
  modeloLocalDisponible: boolean;
  modo: 'deterministico' | 'modelo_local';
  mensaje: string;
  consultas: ConsultaOfrecida[];
  sugerencias: string[];
}

/** La tabla que prueba de dónde salió una respuesta. */
export interface TablaDeConsulta {
  titulo: string;
  fuente: string;
  alcance: string;
  columnas: string[];
  filas: string[][];
}

export interface RespuestaDeConsultaGuiada {
  respuesta: string;
  consultaElegida: string;
  resueltoPor: 'regla' | 'modelo_local' | 'sin_coincidencia';
  generadoEn: string;
  consulta: TablaDeConsulta | null;
  /** Sobre qué se calculó: «Toda la operación», «el Festival …». Nunca se esconde. */
  contexto: string;
}

/** Desde dónde se pregunta. */
export interface ContextoDeConsulta {
  seccion?: string;
  organizacionId?: number;
  festivalId?: number;
}

/**
 * Habla con la Consulta Guiada, sea la del Programa o la de una organización.
 *
 * <b>UNA SOLA CLASE PARA LOS DOS ESPACIOS.</b> Las dos rutas terminan en <code>/estado</code> y
 * <code>/consultar</code> y devuelven exactamente el mismo contrato, porque detrás son el mismo
 * núcleo con distinto ámbito. Un servicio por espacio habría duplicado el manejo de errores y, con
 * el tiempo, las dos formas de leer la respuesta.
 */
@Injectable({ providedIn: 'root' })
export class ConsultaGuiadaService {
  private readonly api = inject(ApiClientService);

  estado(base: string): Observable<EstadoDeConsultaGuiada> {
    return this.api.get<EstadoDeConsultaGuiada>(`${base}/estado`, {
      errorFallback: 'No fue posible consultar qué puede responder el asistente.',
    });
  }

  consultar(base: string, pregunta: string, contexto?: ContextoDeConsulta): Observable<RespuestaDeConsultaGuiada> {
    return this.api.post<RespuestaDeConsultaGuiada>(`${base}/consultar`, { pregunta, contexto }, {
      errorFallback: 'El asistente no pudo responder.',
    });
  }
}
