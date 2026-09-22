import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * El historial de actuaciones sobre UN registro concreto.
 *
 * <b>SE PIDE POR TABLA Y REGISTRO A LA VEZ, y esa es la parte importante.</b> El identificador a
 * secas cruza tablas: la noticia 5 y el evento 5 comparten número, así que pedir solo por
 * identificador devolvía la historia de los dos mezclada.
 *
 * <b>NO ES UNA SEGUNDA BITÁCORA.</b> Lee la misma ruta de auditoría que ya usa la sección de
 * Auditoría y trazabilidad, con un filtro más. Una consulta propia habría dado dos sitios donde
 * decidir qué cuenta como actuación.
 */

export interface LineaDeHistorial {
  id: string;
  accion: string;
  accionEtiqueta: string;
  tabla: string;
  registroId: string;
  nombreRegistro: string | null;
  autor: { id: string; nombre: string; correo: string } | null;
  fecha: string;
}

/** Lo que devuelve una lectura del historial: sus líneas y si la consulta llegó a hacerse. */
export interface ResultadoDeHistorial {
  lineas: LineaDeHistorial[];
  /** `true` si el servidor no respondió. No es lo mismo que un registro sin actuaciones. */
  fallo: boolean;
}

/** Con qué nombre escribe cada módulo sus líneas en la bitácora. */
export const TABLA_DE_AUDITORIA = {
  agenda: 'EventosAgenda',
  noticias: 'Noticias',
  editorial: 'PublicacionesEditoriales',
  festivales: 'Festivales',
  organizaciones: 'Entidades',
  usuarios: 'Usuarios',
  mercados: 'Mercados',
} as const;

@Injectable({ providedIn: 'root' })
export class HistorialDeRegistroService {
  private readonly api = inject(ApiClientService);

  /**
   * Lee el historial de un registro.
   *
   * <b>DISTINGUE «NO HAY» DE «NO SE PUDO LEER», y esa es la parte importante.</b> Antes devolvía
   * una lista vacía en los dos casos: un fallo de red se veía en pantalla como «no hay actuaciones
   * registradas», que es una afirmación falsa sobre el registro. Un historial que no se puede leer
   * no puede impedir trabajar —por eso no lanza—, pero tampoco puede hacerse pasar por un
   * historial vacío.
   */
  async leer(tabla: string, registroId: string | number): Promise<ResultadoDeHistorial> {
    const params = new URLSearchParams({
      tabla,
      registroId: String(registroId),
      tamano: '50',
    });
    try {
      const pagina = await firstValueFrom(
        this.api.get<{ items: LineaDeHistorial[] }>(`/api/v1/admin/auditoria?${params.toString()}`, {}));
      return { lineas: pagina?.items ?? [], fallo: false };
    } catch {
      return { lineas: [], fallo: true };
    }
  }
}
