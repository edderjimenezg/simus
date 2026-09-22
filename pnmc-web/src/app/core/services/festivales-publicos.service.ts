import { Injectable, inject } from '@angular/core';
import { from, Observable, of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { ApiClientService } from '../http/api-client.service';
import { PrevisualizacionEnListadoService } from './previsualizacion-en-listado.service';

export interface TerritorioPrincipalPublico {
  departamento: string | null;
  municipio: string | null;
  nivelCobertura: string;
}

export interface CatalogoFestivalPublico {
  id: number;
  nombre: string;
}

/**
 * La ficha publica de un Festival, tal como la sirve el API sin sesion.
 *
 * NO LLEVA CORREO, TELEFONO NI DIRECTOR, y la ausencia es deliberada. Hasta el 30 de agosto de
 * 2026 los tres viajaban por `GET /api/v1/publico/festivales`; ese dia la direccion de producto fijo
 * la regla —«todo dato personal o correo se quita de la version publica, solo se deja redes
 * sociales del festival»— y los tres salieron del DTO del servidor.
 *
 * SI ALGUIEN LOS NECESITA EN PANTALLA, NO SE AÑADEN AQUI: la ficha con contacto es la del panel de
 * la organizacion, que exige sesion. Volver a pedirlos por esta puerta pone en rojo
 * `FichaPublicaSinDatosPersonalesTests` del API.
 */
export interface FestivalPublico {
  id: string;
  nombre: string;
  descripcion: string | null;
  organizacionResponsable: string | null;
  territorioPrincipal: TerritorioPrincipalPublico;
  periodicidad: string | null;
  practicasMusicales: CatalogoFestivalPublico[];
  territoriosSonoros: CatalogoFestivalPublico[];
  /** Presencia publica del Festival. Es lo unico de contacto que sale sin sesion. */
  instagram: string | null;
  facebook: string | null;
  sitioWeb: string | null;
  otroEnlace: string | null;
}

/**
 * Una realización publicada de un Festival.
 * No es una versión del perfil: no replica contactos, organización, catálogos ni enlaces generales.
 * La dirección artística pertenece al expediente privado de la Edición y no cruza esta frontera pública.
 */
export interface EdicionFestivalPublica {
  id: number;
  anio: number | null;
  numeroEdicion: number | null;
  nombre: string | null;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  estado: string;
  estadoEtiqueta: string;
}

export interface RespuestaPaginada<T> {
  items: T[];
  limit: number;
  offset: number;
  total: number;
}

export interface DistribucionAnaliticaFestival { nombre: string; total: number; }
export interface ResumenAnaliticoFestivales {
  totalFestivales: number;
  porDepartamento: DistribucionAnaliticaFestival[];
  porMunicipio: DistribucionAnaliticaFestival[];
  porPracticaMusical: DistribucionAnaliticaFestival[];
  porTerritorioSonoro: DistribucionAnaliticaFestival[];
  porPeriodicidad: DistribucionAnaliticaFestival[];
}

@Injectable({ providedIn: 'root' })
export class FestivalesPublicosService {
  private readonly apiClient = inject(ApiClientService);
  private readonly previsualizacion = inject(PrevisualizacionEnListadoService);

  /**
   * Cuántos Festivales hay publicados, y nada más.
   *
   * <b>EXISTE PARA QUE EL INICIO NO DEPENDA DEL MAPA.</b> El bloque del Ecosistema Musical de la
   * portada leía su cifra de `fetchMapCountsBundle()`, que descarga la cartografía de Colombia
   * —513 KB medidos contra el API local— más los registros de
   * Festivales y la agenda entera, para calcular los recuentos por departamento que el MAPA
   * necesita. La portada no necesita nada de eso: necesita un número.
   *
   * Y no es solo peso. La dirección de producto fijó ese mismo día la regla: «lo que aparece en el
   * home no es un preview del mapa ecosistémico, es un preview de los registros de ecosistema
   * directamente; si algún día no está el mapa, ese apartado debe seguir dando los datos».
   *
   * <b>SE PIDE LA PAGINA MAS PEQUEÑA Y SE LEE `total`.</b> El listado ya devuelve el total de la
   * consulta, así que no hace falta un endpoint nuevo ni traerse los registros para contarlos.
   */
  contarPublicados(): Observable<number> {
    return this.apiClient.get<RespuestaPaginada<FestivalPublico>>('/api/v1/publico/festivales', {
      params: { limit: 1, offset: 0 },
      errorFallback: 'No fue posible consultar cuántos Festivales hay publicados.',
    }).pipe(map(pagina => Number(pagina?.total ?? 0) || 0));
  }

  consultarFestivales(): Observable<RespuestaPaginada<FestivalPublico>> {
    return this.apiClient.get<RespuestaPaginada<FestivalPublico>>('/api/v1/publico/festivales', {
      params: { limit: 500, offset: 0 },
      errorFallback: 'No fue posible consultar los Festivales públicos.',
    }).pipe(
      // LA PREVISUALIZACION ENTRA POR DONDE ENTRAN LOS DATOS, no tocando la pantalla. Ver
      // `PrevisualizacionEnListadoService`; sin el parámetro en la dirección no se pide nada.
      switchMap(pagina => {
        const id = this.previsualizacion.idPara('festivales');
        if (id === null) return of(pagina);
        return from(this.previsualizacion.obtener<FestivalPublico>('festivales', id)).pipe(
          map(festival => ({
            ...pagina,
            items: this.previsualizacion.anteponer(
              pagina.items ?? [], festival, (a, b) => a.id === b.id, 'festivales'),
          })),
        );
      }),
    );
  }

  consultarFestival(festivalId: string): Observable<FestivalPublico> {
    return this.apiClient.get<FestivalPublico>(`/api/v1/publico/festivales/${encodeURIComponent(festivalId)}`, {
      errorFallback: 'No fue posible consultar esta ficha pública del Festival.',
    });
  }

  /**
   * Las ediciones publicadas de un Festival, de la mas reciente a la mas antigua.
   *
   * DEVUELVE LISTA VACIA CON FRECUENCIA, y no es un fallo: una edicion solo sale por aqui cuando
   * el Festival esta publicado Y la edicion no viene de una propuesta rechazada. Medido contra
   * `PNMC_LOCAL`: de los 58 Festivales con ediciones en la base, ninguno
   * estaba publicado —todos en borrador, en revision o con ajustes pedidos—, asi que la ficha
   * publica de los 33 publicados no ensena ninguna. La pantalla tiene que decirlo, no callarlo.
   */
  consultarEdiciones(festivalId: string): Observable<EdicionFestivalPublica[]> {
    return this.apiClient.get<EdicionFestivalPublica[]>(
      `/api/v1/publico/festivales/${encodeURIComponent(festivalId)}/ediciones`,
      { errorFallback: 'No fue posible consultar las ediciones publicadas de este Festival.' },
    );
  }

  consultarResumenAnalitico(): Observable<ResumenAnaliticoFestivales> {
    return this.apiClient.get<ResumenAnaliticoFestivales>('/api/v1/publico/analitica/festivales/resumen', {
      errorFallback: 'No fue posible consultar el resumen analítico de Festivales.',
    });
  }
}
