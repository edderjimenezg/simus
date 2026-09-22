import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../../core/http/api-client.service';

export interface IndicadorAnalisisAdministrativo {
  id: string;
  rotulo: string;
  total: number;
  alcance: string;
}

export interface TableroAnalisisAdministrativo {
  generadoEn: string;
  indicadores: IndicadorAnalisisAdministrativo[];
  estadosFestival: string[][];
  /** Mercados musicales por estado: un módulo del Ecosistema se ve en el tablero como Festivales. */
  estadosMercado: string[][];
  departamentosPrincipales: string[][];
}

/**
 * El tablero del panorama administrativo.
 *
 * <b>SOLO EL TABLERO.</b> El estado del asistente y las preguntas se fueron a
 * `ConsultaGuiadaService`, cuando la Consulta Guiada pasó a existir
 * también en el espacio de las organizaciones: mantener aquí una copia de esas dos llamadas habría
 * dejado dos formas de leer la misma respuesta.
 */
@Injectable({ providedIn: 'root' })
export class AnalisisAdministrativoService {
  private readonly api = inject(ApiClientService);

  tablero(): Observable<TableroAnalisisAdministrativo> {
    return this.api.get<TableroAnalisisAdministrativo>('/api/v1/admin/analisis/tablero', {
      errorFallback: 'No fue posible construir el tablero administrativo',
    });
  }
}
