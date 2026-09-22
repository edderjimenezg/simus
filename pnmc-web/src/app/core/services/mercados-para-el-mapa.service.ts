import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiClientService } from '../http/api-client.service';
import { MercadoPublico } from './mercados-publicos.service';

/**
 * Un mercado tal como lo consume el geovisor.
 *
 * <b>ES UN REGISTRO PLANO Y NO UN `{ id, fields }`,</b> a diferencia de los festivales. No es una
 * incoherencia nuestra: `map-domain.ts` declara desde hace meses los alias de las columnas del
 * cuestionario de caracterización de mercados y los aplana a estas claves, y toda la maquinaria que
 * ya existe —el recuento por departamento, los totales, la ficha del mapa— lee esa forma. Emitir
 * aquí la otra obligaría a reescribir esa maquinaria por algo que no lo necesita.
 */
export interface RegistroDeMercadoParaElMapa {
  name: string;
  department: string;
  municipality: string;
  departmentCode: string;
  municipalityCode: string;
  description: string;
  responsibleEntity: string;
  periodicity: string;
  versions: number;
  linkedFestival: string;
  festivalName: string;
  websiteUrl: string;
  coverageLevel: string;
  id: string;
}

const texto = (valor: unknown): string => (typeof valor === 'string' ? valor.trim() : '');

/**
 * Los mercados musicales publicados, para el geovisor y el directorio público.
 *
 * <b>LA CAPA EXISTIA Y SE ALIMENTABA DE UNA LISTA VACIA.</b> El geovisor declara «Mercados
 * Musicales» con sus colores, su escala por departamento y treinta y cinco campos de ficha desde
 * hace meses; lo que no tenía era de dónde leerlos, porque sus tablas se habían retirado. La
 * decisión escrita entonces fue que «cada proceso volverá con su modelo y su revisión propios», y
 * Mercados ya volvió: este servicio es ese regreso.
 *
 * <b>`linkedFestival` VIAJA COMO «Sí» O «No»</b> y no como booleano, porque así lo lee
 * `buildMarketTotals` para contar cuántos mercados de un departamento ocurren dentro de un
 * festival. Es el vocabulario del cuestionario, no una conversión nuestra.
 */
@Injectable({ providedIn: 'root' })
export class MercadosParaElMapaService {
  private readonly api = inject(ApiClientService);

  consultar(): Observable<{ records: RegistroDeMercadoParaElMapa[] }> {
    return this.api.get<{ items?: MercadoPublico[] }>('/api/v1/publico/mercados', {
      params: { limit: 500, offset: 0 },
      errorFallback: 'No fue posible cargar los mercados musicales del mapa',
    }).pipe(
      map(respuesta => ({
        records: (Array.isArray(respuesta?.items) ? respuesta.items : []).map(mercado => ({
          id: String(mercado?.id ?? ''),
          name: texto(mercado?.nombre),
          department: texto(mercado?.nombreDepartamento),
          municipality: texto(mercado?.nombreMunicipio),
          departmentCode: texto(mercado?.codigoDepartamento),
          municipalityCode: texto(mercado?.codigoMunicipio),
          coverageLevel: texto(mercado?.nivelCobertura),
          description: texto(mercado?.descripcion),
          responsibleEntity: texto(mercado?.organizacionNombre),
          periodicity: texto(mercado?.periodicidad),
          versions: mercado?.numeroDeEdiciones ?? 0,
          linkedFestival: mercado?.seRealizaEnElMarcoDeUnFestival ? 'Sí' : 'No',
          festivalName: texto(mercado?.festivalNombre),
          websiteUrl: texto(mercado?.sitioWebMercado),
        })),
      })),
    );
  }
}
