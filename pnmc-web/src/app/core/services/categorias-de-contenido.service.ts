import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * Las categorías temáticas de Agenda, Noticias y Catálogo Editorial.
 *
 * <b>POR QUE EXISTEN.</b> Los tres módulos guardaban su categoría como texto libre: nadie podía
 * corregir un nombre mal escrito sin editar cada ficha, y dos personas escribían «Convocatorias» y
 * «convocatoria» sin que nada lo impidiera.
 *
 * <b>PEDIR UN MODULO TRAE TAMBIEN LAS COMUNES</b>, y eso lo decide el servidor. Es lo que hace que
 * declarar «Bandas» como común la deje disponible en los tres formularios sin duplicarla.
 */

export interface CategoriaDeContenido {
  id: number;
  codigoModulo: string;
  nombreCategoria: string;
  slug: string;
  descripcion: string | null;
  ordenVisualizacion: number;
  /** Cuántos contenidos la usan. Es lo que permite decidir si compartirla o retirarla. */
  contenidosQueLaUsan: number;
}

/** Los módulos que pueden tener categorías propias, más el compartido. */
export const MODULOS_DE_CATEGORIA = ['agenda', 'noticias', 'editorial', 'comun'] as const;

export const ETIQUETAS_DE_MODULO: Record<string, string> = {
  agenda: 'Agenda y eventos',
  noticias: 'Noticias y prensa',
  editorial: 'Catálogo Editorial',
  comun: 'Común a los tres',
};

export interface ResultadoDeCategoria<T> {
  ok: boolean;
  data?: T | null;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class CategoriasDeContenidoService {
  private readonly api = inject(ApiClientService);

  private static readonly CONSOLA = '/api/v1/institucional/categorias-contenido';
  private static readonly PUBLICO = '/api/v1/publico/categorias-contenido';

  /** El motivo del servidor entero: dice si el nombre está repetido o el módulo no existe. */
  private motivoDelServidor(error: unknown): string {
    const cuerpo = (error as { payload?: unknown })?.payload;
    if (cuerpo && typeof cuerpo === 'object') {
      for (const valor of Object.values(cuerpo as Record<string, unknown>)) {
        if (Array.isArray(valor)) {
          const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
          if (typeof mensaje === 'string') { return mensaje.trim(); }
        }
      }
    }
    return '';
  }

  private async envolver<T>(operacion: () => Promise<T>, respaldo: string): Promise<ResultadoDeCategoria<T>> {
    try {
      return { ok: true, data: await operacion() };
    } catch (error: unknown) {
      return { ok: false, error: this.motivoDelServidor(error) || respaldo };
    }
  }

  /** Las de un módulo —con las comunes— o todas si no se pide ninguno. */
  listar(modulo?: string) {
    const params = modulo ? `?modulo=${encodeURIComponent(modulo)}` : '';
    return this.envolver(
      () => firstValueFrom(this.api.get<{ items: CategoriaDeContenido[] }>(`${CategoriasDeContenidoService.CONSOLA}${params}`, {})),
      'No fue posible consultar las categorías.');
  }

  /** La misma lectura, sin sesión. La usan las pantallas del portal. */
  listarPublicas(modulo?: string) {
    const params = modulo ? `?modulo=${encodeURIComponent(modulo)}` : '';
    return this.envolver(
      () => firstValueFrom(this.api.get<{ items: CategoriaDeContenido[] }>(`${CategoriasDeContenidoService.PUBLICO}${params}`, {})),
      'No fue posible consultar las categorías.');
  }

  crear(categoria: { codigoModulo: string; nombreCategoria: string; descripcion?: string | null }) {
    return this.envolver(
      () => firstValueFrom(this.api.post<CategoriaDeContenido>(CategoriasDeContenidoService.CONSOLA, categoria, {})),
      'No fue posible crear la categoría.');
  }

  guardar(id: number, categoria: { codigoModulo: string; nombreCategoria: string; descripcion?: string | null; ordenVisualizacion?: number }) {
    return this.envolver(
      () => firstValueFrom(this.api.put<CategoriaDeContenido>(`${CategoriasDeContenidoService.CONSOLA}/${id}`, categoria, {})),
      'No fue posible guardar la categoría.');
  }

  /**
   * Pasa el contenido de una categoría a otra y retira la que queda vacía.
   *
   * ES LO QUE HACE REVERSIBLE LA DECISION entre categorías propias y comunes. Sin fusionar, elegir
   * mal era definitivo: había que reasignar a mano ficha por ficha.
   */
  fusionar(id: number, destinoId: number) {
    return this.envolver(
      () => firstValueFrom(this.api.post<CategoriaDeContenido>(`${CategoriasDeContenidoService.CONSOLA}/${id}/fusionar`, { destinoId }, {})),
      'No fue posible fusionar la categoría.');
  }

  eliminar(id: number) {
    return this.envolver(
      () => firstValueFrom(this.api.delete<void>(`${CategoriasDeContenidoService.CONSOLA}/${id}`, {})),
      'No fue posible eliminar la categoría.');
  }
}
