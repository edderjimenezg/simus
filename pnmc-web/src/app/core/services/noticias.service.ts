import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { PrevisualizacionEnListadoService } from './previsualizacion-en-listado.service';
import { ElementoDeClasificacion } from './clasificacion-de-contenido.service';
import { ProcedenciaDeRegistro } from './procedencia';

/**
 * Noticias contra el API.
 *
 * <b>Dos superficies, dos rutas.</b> La consola lee `/institucional/noticias` y ve todo; el portal
 * lee `/publico/noticias` y solo ve lo publicado con fecha llegada. El filtro NO está aquí: está
 * en el servidor, en un único sitio, porque una condición de visibilidad que viva en el navegador
 * la salta cualquiera con la consola abierta.
 */

export interface Noticia {
  id: number;
  slug: string;
  titulo: string;
  resumen: string;
  cuerpo: string | null;
  fechaPublicacion: string | null;
  imagenRuta: string | null;
  imagenAlternativa: string | null;
  autoriaNombre: string | null;
  /** El archivo del banco con rol «imagen_principal», si lo hay. */
  imagenArchivoId: number | null;
  imagenUrl: string | null;
  imagenAlt: string | null;
  /** La categoría temática administrable. Es lo que se guarda. */
  categoriaId: number | null;
  /** Su nombre, resuelto por el servidor. El diseño aprobado organiza el portal alrededor de él. */
  categoria: string | null;
  /** El estado guardado. Es con el que se decide qué acciones caben. */
  estado: string;
  /**
   * Cómo está HOY, contando su fecha de aparición.
   *
   * COINCIDE CON `estado` SALVO EN UN CASO: publicada con fecha futura, que es «programada». Es lo
   * que hay que ENSEÑAR. Lo calcula el servidor, que es el que conoce la fecha buena.
   */
  estadoEfectivo: string;
  version: number;
  etiquetas: string[];
  /** Clasificación opcional contra los catálogos del sistema. Puede venir vacía. */
  practicasMusicales: ElementoDeClasificacion[];
  territoriosSonoros: ElementoDeClasificacion[];
  /** De dónde vino el registro. Nulo en lo anterior a la tabla de procedencia. */
  procedencia: ProcedenciaDeRegistro | null;
  /** Las iniciativas del Programa a las que pertenece. No es su categoría. */
  proyectosTransversales: ElementoDeClasificacion[];
  fechaActualizacion: string;
}

export interface PaginaNoticias {
  items: Noticia[];
  pagina: number;
  tamano: number;
  total: number;
  totalPaginas: number;
}

/** Las etiquetas que ve una persona. El código vive en el servidor; aquí solo se presenta. */
export const ETIQUETAS_ESTADO_NOTICIA: Record<string, string> = {
  borrador: 'Borrador',
  en_revision: 'En revisión',
  publicado: 'Publicado',
  /**
   * PUBLICADA, PERO CON FECHA FUTURA.
   *
   * No es un estado que se guarde: lo calcula el servidor con su propia fecha y viaja en
   * `estadoEfectivo`. Una noticia así NO está en el portal todavía, y llamarla «Publicado» hacía
   * creer lo contrario.
   */
  programada: 'Programada',
  archivado: 'Archivado',
};

/** El mismo sobre que usa el resto de la consola: éxito con datos, o fallo con un motivo legible. */
export interface ResultadoNoticia<T> {
  ok: boolean;
  data?: T | null;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class NoticiasService {
  private readonly api = inject(ApiClientService);
  private readonly previsualizacion = inject(PrevisualizacionEnListadoService);

  private static readonly CONSOLA = '/api/v1/institucional/noticias';
  private static readonly PUBLICO = '/api/v1/publico/noticias';

  /**
   * Convierte el error del API en un motivo que se pueda enseñar.
   *
   * EL MENSAJE DEL SERVIDOR SE CONSERVA ENTERO. Cuando publicar se rechaza, el API explica QUÉ
   * falta —y lo dice TODO de una vez, no lo primero que encontró—. Un «no fue posible» genérico
   * dejaría a quien redacta adivinando cuál de las condiciones incumple.
   *
   * SE LEE `payload` Y NO `error`. `ApiClientService` deja el cuerpo del error en `ApiError.payload`;
   * leer un `error` que nunca existió hacía que siempre ganara el respaldo genérico, que es
   * exactamente lo que este método existe para evitar.
   */
  private motivoDelServidor(error: unknown): string {
    const fallo = error as { payload?: unknown; message?: string };
    const cuerpo = fallo?.payload;
    if (cuerpo && typeof cuerpo === 'object') {
      for (const valor of Object.values(cuerpo as Record<string, unknown>)) {
        if (Array.isArray(valor)) {
          const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
          if (typeof mensaje === 'string') { return mensaje.trim(); }
        }
      }
      const suelto = (cuerpo as { message?: unknown }).message;
      if (typeof suelto === 'string' && suelto.trim()) { return suelto.trim(); }
    }
    // SIN CUERPO NO SE DEVUELVE NADA, a propósito. `ApiError.message` trae el respaldo genérico
    // del cliente —«Error al consultar backend»—, y quien llama tiene uno mejor: sabe qué estaba
    // intentando. Devolver el genérico aquí lo dejaría ganar siempre sobre el específico.
    return '';
  }

  private async envolver<T>(operacion: () => Promise<T>, respaldo: string): Promise<ResultadoNoticia<T>> {
    try {
      return { ok: true, data: await operacion() };
    } catch (error: unknown) {
      return { ok: false, error: this.motivoDelServidor(error) || respaldo };
    }
  }

  listarPublicas(filtros: { q?: string; etiqueta?: string; categoria?: string; pagina?: number; tamano?: number } = {}) {
    const params = new URLSearchParams();
    if (filtros.q) params.set('q', filtros.q);
    if (filtros.etiqueta) params.set('etiqueta', filtros.etiqueta);
    if (filtros.categoria) params.set('categoria', filtros.categoria);
    if (filtros.tamano) params.set('tamano', String(filtros.tamano));
    params.set('pagina', String(filtros.pagina ?? 1));
    return this.envolver(
      async () => {
        const pagina = await firstValueFrom(
          this.api.get<PaginaNoticias>(`${NoticiasService.PUBLICO}?${params.toString()}`, {}));
        // LA PREVISUALIZACION ENTRA POR DONDE ENTRAN LOS DATOS, no tocando la pantalla: el diseño
        // de Noticias está aprobado tal cual y la tarjeta que se ve tiene que ser la de verdad.
        // Ver `PrevisualizacionEnListadoService`; sin el parámetro no se pide nada.
        const id = this.previsualizacion.idPara('noticias');
        if (id === null) return pagina;
        const previsualizada = await this.previsualizacion.obtener<Noticia>('noticias', id);
        return {
          ...pagina,
          items: this.previsualizacion.anteponer(pagina.items ?? [], previsualizada, (a, b) => a.id === b.id, 'noticias'),
        };
      },
      'No fue posible consultar las noticias.');
  }

  obtenerPublica(slug: string) {
    return this.envolver(
      () => firstValueFrom(this.api.get<Noticia>(`${NoticiasService.PUBLICO}/${encodeURIComponent(slug)}`, {})),
      'No fue posible abrir la noticia.');
  }

  listarInternas(filtros: { q?: string; estado?: string; categoria?: string; orden?: string; direccion?: string; pagina?: number } = {}) {
    const params = new URLSearchParams();
    if (filtros.q) params.set('q', filtros.q);
    if (filtros.estado) params.set('estado', filtros.estado);
    // LA CATEGORIA Y EL ORDEN LOS RESUELVE EL SERVIDOR. La lista pagina: filtrar u ordenar la
    // página cargada respondería sobre doce noticias y callaría sobre el resto.
    if (filtros.categoria) params.set('categoria', filtros.categoria);
    if (filtros.orden) params.set('orden', filtros.orden);
    if (filtros.direccion) params.set('direccion', filtros.direccion);
    params.set('pagina', String(filtros.pagina ?? 1));
    return this.envolver(
      () => firstValueFrom(this.api.get<PaginaNoticias>(`${NoticiasService.CONSOLA}?${params.toString()}`, {})),
      'No fue posible consultar las noticias.');
  }

  crear(noticia: Partial<Noticia> & { etiquetas?: string[] }) {
    return this.envolver(
      () => firstValueFrom(this.api.post<Noticia>(NoticiasService.CONSOLA, noticia, {})),
      'No fue posible crear la noticia.');
  }

  guardar(id: number, noticia: Partial<Noticia> & { etiquetas?: string[]; version: number }) {
    return this.envolver(
      () => firstValueFrom(this.api.put<Noticia>(`${NoticiasService.CONSOLA}/${id}`, noticia, {})),
      'No fue posible guardar la noticia.');
  }

  cambiarEstado(id: number, estado: string) {
    return this.envolver(
      () => firstValueFrom(this.api.post<Noticia>(`${NoticiasService.CONSOLA}/${id}/estado`, { estado }, {})),
      'No fue posible cambiar el estado de la noticia.');
  }
}
