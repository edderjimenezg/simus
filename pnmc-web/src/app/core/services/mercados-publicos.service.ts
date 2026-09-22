import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { CatalogoDeMercado } from './mercados.service';

/**
 * La ficha pública de un mercado musical, tal como la sirve el API sin sesión.
 *
 * NO LLEVA CORREO, TELÉFONO NI OBSERVACIONES DE CONTACTO, por la misma regla que la ficha pública
 * de un Festival: «todo dato personal o correo se quita de la versión pública, solo se deja redes
 * sociales». Es `MercadoPublicoDto` del servidor; la ficha con contacto es la del panel de la
 * organización y la de la consola, que exigen sesión.
 */
export interface MercadoPublico {
  id: number;
  nombre: string;
  descripcion: string | null;
  alcance: string | null;
  modalidad: string | null;
  periodicidad: string | null;
  periodicidadDetalle: string | null;
  sitioWebMercado: string | null;
  instagramMercado: string | null;
  facebookMercado: string | null;
  otroEnlaceMercado: string | null;
  nivelCobertura: string;
  codigoDepartamento: string | null;
  nombreDepartamento: string | null;
  codigoMunicipio: string | null;
  nombreMunicipio: string | null;
  lugarEspecifico: string | null;
  seRealizaEnElMarcoDeUnFestival: boolean;
  festivalId: number | null;
  festivalNombre: string | null;
  organizacionNombre: string | null;
  numeroDeEdiciones: number;
  fechaPublicacion: string | null;
  practicasMusicales: CatalogoDeMercado[];
  territoriosSonoros: CatalogoDeMercado[];
}

/** Una realización publicada de un mercado. Es `EdicionDeMercadoDto` con visibilidad «publicado». */
export interface EdicionDeMercadoPublica {
  id: number;
  mercadoId: number;
  anio: number;
  numeroEdicion: number | null;
  nombre: string | null;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  nombreDepartamento: string | null;
  nombreMunicipio: string | null;
  lugarEspecifico: string | null;
  estado: string;
}

export interface PaginaDeMercadosPublicos {
  items: MercadoPublico[];
  total: number;
  limit: number;
  offset: number;
}

/**
 * Lo público de Mercados Musicales: el directorio, la ficha y sus ediciones publicadas.
 *
 * Es la pareja de `FestivalesPublicosService`. Hasta el directorio y
 * el mapa leían `/publico/mercados` cada uno por su cuenta y con el tipo de la consola.
 */
@Injectable({ providedIn: 'root' })
export class MercadosPublicosService {
  private readonly api = inject(ApiClientService);

  consultarMercados(): Observable<PaginaDeMercadosPublicos> {
    return this.api.get<PaginaDeMercadosPublicos>('/api/v1/publico/mercados', {
      params: { limit: 500, offset: 0 },
      errorFallback: 'No fue posible consultar los mercados musicales.',
    });
  }

  consultarMercado(mercadoId: string | number): Observable<MercadoPublico> {
    return this.api.get<MercadoPublico>(`/api/v1/publico/mercados/${encodeURIComponent(String(mercadoId))}`, {
      errorFallback: 'No fue posible consultar esta ficha pública del mercado.',
    });
  }

  consultarEdiciones(mercadoId: string | number): Observable<EdicionDeMercadoPublica[]> {
    return this.api.get<EdicionDeMercadoPublica[]>(`/api/v1/publico/mercados/${encodeURIComponent(String(mercadoId))}/ediciones`, {
      errorFallback: 'No fue posible consultar las ediciones publicadas del mercado.',
    });
  }
}
