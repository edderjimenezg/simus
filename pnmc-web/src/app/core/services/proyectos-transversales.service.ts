import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * Las iniciativas del Programa que atraviesan varios módulos: Celebra la Música y las que vengan.
 *
 * <b>NO SON CATEGORÍAS TEMÁTICAS.</b> Una categoría dice de qué trata un contenido; un proyecto
 * transversal dice a qué iniciativa pertenece. Un evento puede ser de «Encuentros» y además de
 * Celebra la Música: mezclarlas obligaría a inventar categorías como «Encuentros de Celebra».
 *
 * <b>SE ENLAZAN EVENTOS Y NOTICIAS, NO FESTIVALES.</b> Lo decidió la dirección: un Festival es un
 * proceso del ecosistema con su propia organización responsable; lo que pertenece a una iniciativa
 * del Programa es el contenido que se publica sobre él.
 */

export interface ProyectoTransversal {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  ordenVisualizacion: number;
  /** Cuánto contenido lo lleva enlazado. Decide si desactivarlo tiene consecuencias. */
  contenidosEnlazados: number;
}

export interface ResultadoDeProyecto<T> {
  ok: boolean;
  data?: T | null;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class ProyectosTransversalesService {
  private readonly api = inject(ApiClientService);

  private static readonly CONSOLA = '/api/v1/institucional/proyectos-transversales';
  private static readonly PUBLICO = '/api/v1/publico/proyectos-transversales';

  /** Los que siguen abiertos. Es lo que ofrecen los formularios. */
  private activos: Promise<ProyectoTransversal[]> | null = null;

  activosParaFormularios(): Promise<ProyectoTransversal[]> {
    this.activos ??= this.leerActivos();
    return this.activos;
  }

  private async leerActivos(): Promise<ProyectoTransversal[]> {
    try {
      const pagina = await firstValueFrom(
        this.api.get<{ items: ProyectoTransversal[] }>(ProyectosTransversalesService.PUBLICO, {}));
      return pagina?.items ?? [];
    } catch {
      // ENLAZAR CON UNA INICIATIVA ES OPCIONAL: si el catálogo no responde, el formulario lo dice
      // y sigue. El contenido se puede guardar igual.
      this.activos = null;
      return [];
    }
  }

  /** Todos, activos o no: la consola tiene que poder reabrir uno cerrado. */
  async listar(): Promise<ResultadoDeProyecto<{ items: ProyectoTransversal[] }>> {
    return this.envolver(
      () => firstValueFrom(this.api.get<{ items: ProyectoTransversal[] }>(ProyectosTransversalesService.CONSOLA, {})),
      'No fue posible consultar los proyectos transversales.');
  }

  async crear(proyecto: { nombre: string; descripcion?: string | null }) {
    this.activos = null;
    return this.envolver(
      () => firstValueFrom(this.api.post<ProyectoTransversal>(ProyectosTransversalesService.CONSOLA, { ...proyecto, activo: true }, {})),
      'No fue posible crear el proyecto.');
  }

  async guardar(id: number, proyecto: { nombre: string; descripcion?: string | null; activo: boolean }) {
    // SE OLVIDA LA LISTA DE ACTIVOS: desactivar uno tiene que dejar de ofrecerlo en los
    // formularios sin obligar a recargar la consola entera.
    this.activos = null;
    return this.envolver(
      () => firstValueFrom(this.api.put<ProyectoTransversal>(`${ProyectosTransversalesService.CONSOLA}/${id}`, proyecto, {})),
      'No fue posible guardar el proyecto.');
  }

  private async envolver<T>(operacion: () => Promise<T>, respaldo: string): Promise<ResultadoDeProyecto<T>> {
    try {
      return { ok: true, data: await operacion() };
    } catch (error: unknown) {
      const cuerpo = (error as { payload?: unknown })?.payload;
      if (cuerpo && typeof cuerpo === 'object') {
        for (const valor of Object.values(cuerpo as Record<string, unknown>)) {
          if (Array.isArray(valor)) {
            const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
            if (typeof mensaje === 'string') { return { ok: false, error: mensaje.trim() }; }
          }
        }
      }
      return { ok: false, error: respaldo };
    }
  }
}
