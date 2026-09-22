import { inject, Injectable } from '@angular/core';
import { firstValueFrom, forkJoin, Observable, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiClientService } from '../http/api-client.service';
import { PrevisualizacionEnListadoService } from './previsualizacion-en-listado.service';
import { EventoAgenda } from './agenda.service';

/** Lo que devuelve `/publico/agenda`: la página pedida y cuántas hay en total. */
interface PaginaDeAgendaPublica {
  items?: EventoAgenda[];
  totalPaginas?: number;
}
import {
  EventoDeDiseno,
  aEventoDeDiseno,
} from './adaptadores-del-diseno';
import { feature as topojsonFeature } from 'topojson-client';
import type { Feature, FeatureCollection, Geometry } from 'geojson';
import type { GeometryCollection, GeometryObject, Objects, Topology } from 'topojson-specification';

const TOPOLOGY_DEPARTMENTS_OBJECT = 'MGN_ADM_DPTO_POLITICO';
const TOPOLOGY_MUNICIPALITIES_OBJECT = 'MGN_ADM_MPIO_GRAFICO';
const EMPTY_FEATURE_COLLECTION: ColeccionCartografica = { type: 'FeatureCollection', features: [] };
const MUNICIPALITY_CACHE_LIMIT = 2;

type PropiedadesCartograficas = Record<string, unknown>;
type GeometriaTopo = GeometryObject<PropiedadesCartograficas>;
type TopologiaCartografica = Topology<Objects<PropiedadesCartograficas>>;
type ColeccionCartografica = FeatureCollection<Geometry, PropiedadesCartograficas>;


const esObjeto = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

const esTopologia = (value: unknown): value is TopologiaCartografica =>
  esObjeto(value) && value['type'] === 'Topology' && esObjeto(value['objects']) && Array.isArray(value['arcs']);

const esColeccionCartografica = (value: unknown): value is ColeccionCartografica =>
  esObjeto(value) && value['type'] === 'FeatureCollection' && Array.isArray(value['features']);

/** Un municipio con su punto de referencia, tal como lo guarda el geovisor. */
export interface PuntoMunicipalDelCatalogo {
  readonly lat: number;
  readonly lng: number;
  readonly nombre: string;
  readonly codigoDepartamento: string;
}

/** Un municipio con su código DIVIPOLA, que es lo que se guarda. */
export interface MunicipioConCodigo {
  codigo: string;
  nombre: string;
}

/** Un departamento con sus municipios, ambos con su código. */
export interface TerritorioConCodigo {
  codigo: string;
  nombre: string;
  municipios: MunicipioConCodigo[];
}

/** La fila territorial que entrega el catálogo público oficial. */
interface UbicacionDivipolaPublica {
  readonly municipalityCode?: string | null;
  readonly municipalityName?: string | null;
  readonly departmentCode?: string | null;
  readonly departmentName?: string | null;
  readonly latitude?: number | null;
  readonly longitude?: number | null;
}

interface PaqueteCartografico extends ColeccionCartografica {
  municipalities: ColeccionCartografica;
  getMunicipalitiesForDepartment: (departmentCode: string) => ColeccionCartografica | Promise<ColeccionCartografica>;
  municipalityLoaderMode?: 'embedded' | 'remote';
  sourceFormat: 'topojson' | 'geojson' | 'unknown';
}

@Injectable({
  providedIn: 'root'
})
export class CatalogService {
  private readonly apiClient = inject(ApiClientService);
  private readonly previsualizacion = inject(PrevisualizacionEnListadoService);
  private readonly remoteMunicipalityCache = new Map<string, Promise<ColeccionCartografica>>();

  private normalizeDepartmentCode(value: unknown): string {
    const digits = String(value ?? '').replace(/\D+/g, '');
    if (!digits) return '';
    return digits.padStart(2, '0').slice(-2);
  }

  private normalizeMunicipalityCode(value: unknown, departmentCode: unknown = '', municipalityShortCode: unknown = ''): string {
    const valueDigits = String(value ?? '').replace(/\D+/g, '');
    if (valueDigits.length >= 5) return valueDigits.slice(-5);

    const departmentDigits = this.normalizeDepartmentCode(departmentCode);
    const municipalityDigits = String(municipalityShortCode ?? '').replace(/\D+/g, '').padStart(3, '0').slice(-3);

    if (departmentDigits && municipalityDigits) {
      return `${departmentDigits}${municipalityDigits}`;
    }

    return valueDigits || '';
  }

  private ensureFeatureCollection(value: unknown): ColeccionCartografica {
    if (esColeccionCartografica(value)) {
      return value;
    }
    return { type: 'FeatureCollection', features: [] };
  }

  private normalizeDepartmentFeatureCollection(collection: unknown): ColeccionCartografica {
    const normalized = this.ensureFeatureCollection(collection);

    return {
      ...normalized,
      features: normalized.features.map((item: Feature<Geometry, PropiedadesCartograficas>) => {
        const properties = item.properties || {};
        const departmentCode = this.normalizeDepartmentCode(properties['departmentCode'] || properties['dpto_ccdgo']);
        const departmentName = String(properties['departmentName'] || properties['dpto_cnmbr'] || '');

        return {
          ...item,
          properties: {
            ...properties,
            departmentCode,
            departmentName,
          },
        };
      }),
    };
  }

  private normalizeMunicipalityFeatureCollection(collection: unknown): ColeccionCartografica {
    const normalized = this.ensureFeatureCollection(collection);

    return {
      ...normalized,
      features: normalized.features.map((item: Feature<Geometry, PropiedadesCartograficas>) => {
        const properties = item.properties || {};
        const departmentCode = this.normalizeDepartmentCode(properties['departmentCode'] || properties['dpto_ccdgo']);
        const municipalityCode = this.normalizeMunicipalityCode(
          properties['municipalityCode'] || properties['mpio_cdpmp'],
          departmentCode,
          properties['municipalityShortCode'] || properties['mpio_ccdgo']
        );
        const municipalityShortCode = String(
          properties['municipalityShortCode'] || properties['mpio_ccdgo'] || ''
        ).replace(/\D+/g, '').padStart(3, '0').slice(-3);
        const municipalityName = String(properties['municipalityName'] || properties['mpio_cnmbr'] || '');
        const departmentName = String(properties['departmentName'] || properties['dpto_cnmbr'] || '');

        return {
          ...item,
          properties: {
            ...properties,
            departmentCode,
            departmentName,
            municipalityCode,
            municipalityShortCode,
            municipalityName,
          },
        };
      }),
    };
  }

  private toGeoBundleFromTopology(topologyPayload: TopologiaCartografica) {
    const topologyObjects = topologyPayload.objects;
    const departmentsObject = topologyObjects[TOPOLOGY_DEPARTMENTS_OBJECT];
    const municipalitiesObject = topologyObjects[TOPOLOGY_MUNICIPALITIES_OBJECT];

    // Cast properties appropriately or use topojsonFeature
    const departmentsGeo = departmentsObject
      ? topojsonFeature(topologyPayload, departmentsObject)
      : { type: 'FeatureCollection', features: [] };

    const departments = this.normalizeDepartmentFeatureCollection(departmentsGeo);

    // La fuente cartografica contiene 1.122 municipios. Convertirlos todos a
    // GeoJSON durante la carga inicial consumia alrededor de medio segundo en
    // local y cientos de MB, aunque la vista nacional no dibuja ninguno. Se
    // conserva el TopoJSON compacto y solo se decodifica el departamento que el
    // usuario abre. El cache LRU acotado evita tanto repetir el trabajo al volver
    // atras como retener gradualmente los 1.122 municipios en memoria.
    const municipalityCache = new Map<string, ColeccionCartografica>();
    const municipalityGeometriesByDepartment = new Map<string, GeometriaTopo[]>();
    const geometriesMunicipales = municipalitiesObject?.type === 'GeometryCollection'
      ? municipalitiesObject.geometries
      : [];
    for (const geometry of geometriesMunicipales) {
      const properties = esObjeto(geometry.properties) ? geometry.properties : {};
      const departmentCode = this.normalizeDepartmentCode(
        properties['departmentCode'] || properties['dpto_ccdgo'],
      );
      if (!departmentCode) continue;

      const grouped = municipalityGeometriesByDepartment.get(departmentCode);
      if (grouped) grouped.push(geometry);
      else municipalityGeometriesByDepartment.set(departmentCode, [geometry]);
    }

    const getMunicipalitiesForDepartment = (departmentCodeValue: string) => {
      const departmentCode = this.normalizeDepartmentCode(departmentCodeValue);
      if (!departmentCode || municipalitiesObject?.type !== 'GeometryCollection') {
        return EMPTY_FEATURE_COLLECTION;
      }

      const cached = municipalityCache.get(departmentCode);
      if (cached) {
        // Refresca el orden de insercion para que Map funcione como un LRU.
        municipalityCache.delete(departmentCode);
        municipalityCache.set(departmentCode, cached);
        return cached;
      }

      const geometries = municipalityGeometriesByDepartment.get(departmentCode) || [];
      const departmentMunicipalitiesObject: GeometryCollection<PropiedadesCartograficas> = {
        ...municipalitiesObject,
        geometries,
      };
      const municipalityGeo = topojsonFeature(topologyPayload, departmentMunicipalitiesObject);
      const normalized = this.normalizeMunicipalityFeatureCollection(municipalityGeo);

      municipalityCache.set(departmentCode, normalized);
      if (municipalityCache.size > MUNICIPALITY_CACHE_LIMIT) {
        const oldestKey = municipalityCache.keys().next().value;
        if (oldestKey) municipalityCache.delete(oldestKey);
      }

      return normalized;
    };

    return {
      departments,
      municipalities: EMPTY_FEATURE_COLLECTION,
      getMunicipalitiesForDepartment,
    };
  }

  private getRemoteMunicipalitiesForDepartment(departmentCodeValue: string): Promise<ColeccionCartografica> {
    const departmentCode = this.normalizeDepartmentCode(departmentCodeValue);
    if (!departmentCode) return Promise.resolve(EMPTY_FEATURE_COLLECTION);

    const cached = this.remoteMunicipalityCache.get(departmentCode);
    if (cached) {
      this.remoteMunicipalityCache.delete(departmentCode);
      this.remoteMunicipalityCache.set(departmentCode, cached);
      return cached;
    }

    const request = firstValueFrom(
      this.apiClient.get<unknown>(`/api/v1/mapa/cartografia/departamentos/${departmentCode}/municipios`, {
        errorFallback: 'No fue posible cargar los municipios del departamento',
      }).pipe(
        map((payload) => {
          if (esTopologia(payload)) {
            const municipalityObject = payload.objects[TOPOLOGY_MUNICIPALITIES_OBJECT];
            const municipalityGeo = municipalityObject
              ? topojsonFeature(payload, municipalityObject)
              : EMPTY_FEATURE_COLLECTION;
            return this.normalizeMunicipalityFeatureCollection(municipalityGeo);
          }

          return this.normalizeMunicipalityFeatureCollection(payload);
        }),
      ),
    ).catch((error) => {
      if (this.remoteMunicipalityCache.get(departmentCode) === request) {
        this.remoteMunicipalityCache.delete(departmentCode);
      }
      throw error;
    });

    this.remoteMunicipalityCache.set(departmentCode, request);
    if (this.remoteMunicipalityCache.size > MUNICIPALITY_CACHE_LIMIT) {
      const oldestKey = this.remoteMunicipalityCache.keys().next().value;
      if (oldestKey) this.remoteMunicipalityCache.delete(oldestKey);
    }

    return request;
  }

  /**
   * La agenda, en la forma que consume el diseño aprobado.
   *
   * Misma historia que el catálogo: <code>/api/v1/agenda/events</code> nunca llegó a existir en
   * esta base. Se pide <code>cuando=todos</code> porque el diseño reparte los eventos por fecha en
   * el navegador y necesita verlos todos para construir su selector de meses.
   *
   * <b>SE RECORREN TODAS LAS PAGINAS, Y ESO CORRIGE UN DEFECTO DE FONDO.</b> Esta llamada pedía
   * una sola página de 50. La pantalla de Agenda filtra EN EL NAVEGADOR —departamento, municipio,
   * tipo, mes, día— así que con una página cargada, cada uno de esos filtros recorría 50 de los
   * 435 eventos publicados y decía «no hay eventos» sobre una agenda que sí los tenía. Medido
   * contra el API local: `total` 435, `totalPaginas` 9, y el parámetro
   * `tamano` está acotado a 50 en el servidor, así que pedir más no sirve: hay que recorrerlas.
   *
   * El tope de páginas existe para que un error del servidor en `totalPaginas` no convierta esto
   * en una petición infinita.
   */
  fetchAgendaEvents(): Observable<{ items: EventoDeDiseno[] }> {
    const TOPE_DE_PAGINAS = 40;
    const pagina = (numero: number) => this.apiClient.get<PaginaDeAgendaPublica>('/api/v1/publico/agenda', {
      params: { tamano: 50, pagina: numero, cuando: 'todos' },
      errorFallback: 'No fue posible cargar la agenda',
    });

    return pagina(1).pipe(
      switchMap(primera => {
        const paginas = Math.min(Number(primera?.totalPaginas ?? 1) || 1, TOPE_DE_PAGINAS);
        if (paginas <= 1) { return of([primera]); }
        const siguientes = Array.from({ length: paginas - 1 }, (_, indice) => pagina(indice + 2));
        return forkJoin(siguientes).pipe(map(resto => [primera, ...resto]));
      }),
      map(paginas => paginas.flatMap(p => (Array.isArray(p?.items) ? p.items : [])).map(aEventoDeDiseno)),
      switchMap(items => this.previsualizacion.conListado<EventoAgenda, EventoDeDiseno>(
        'agenda', items, aEventoDeDiseno, (a, b) => a.id === b.id)),
      map(items => ({ items })),
    );
  }

  fetchColombiaGeoJson(): Observable<PaqueteCartografico> {
    return this.apiClient.get<unknown>('/api/v1/mapa/cartografia/departamentos', {
      errorFallback: 'No fue posible cargar la cartografia departamental optimizada'
    }).pipe(
      // Compatibilidad durante despliegues escalonados: si el frontend llega
      // antes que las nuevas rutas del API, conserva el TopoJSON monolitico.
      catchError(() => this.apiClient.get<unknown>('/api/v1/mapa/cartografia/territorios', {
        errorFallback: 'No fue posible cargar datos'
      })),
      map((payload) => {
        if (esTopologia(payload)) {
          const geoBundle = this.toGeoBundleFromTopology(payload);
          const hasEmbeddedMunicipalities = Boolean(
            payload.objects[TOPOLOGY_MUNICIPALITIES_OBJECT],
          );
          return {
            ...geoBundle.departments,
            municipalities: geoBundle.municipalities,
            getMunicipalitiesForDepartment: hasEmbeddedMunicipalities
              ? geoBundle.getMunicipalitiesForDepartment
              : (departmentCode: string) => this.getRemoteMunicipalitiesForDepartment(departmentCode),
            municipalityLoaderMode: hasEmbeddedMunicipalities ? 'embedded' : 'remote',
            sourceFormat: 'topojson',
          };
        }

        if (esColeccionCartografica(payload)) {
          return {
            ...this.normalizeDepartmentFeatureCollection(payload),
            municipalities: { type: 'FeatureCollection', features: [] },
            getMunicipalitiesForDepartment: (departmentCode: string) =>
              this.getRemoteMunicipalitiesForDepartment(departmentCode),
            municipalityLoaderMode: 'remote',
            sourceFormat: 'geojson',
          };
        }

        return {
          type: 'FeatureCollection',
          features: [],
          municipalities: { type: 'FeatureCollection', features: [] },
          getMunicipalitiesForDepartment: () => EMPTY_FEATURE_COLLECTION,
          sourceFormat: 'unknown',
        };
      })
    );
  }


  /**
   * El catálogo territorial con sus códigos, agrupado por departamento.
   *
   * <b>POR QUE NO BASTA `cargarDivipolaPorDepartamento`.</b> Aquel devuelve solo nombres, que sirven para
   * pintar un selector pero no para guardar: la Agenda —y el esquema, con su foránea a
   * `Divipola`— almacenan el CODIGO, porque un nombre escrito a mano hace imposible agrupar por
   * departamento y nadie lo nota hasta que un informe sale corto.
   *
   * <b>UNA SOLA CONSULTA PARA LAS 1.122 FILAS.</b> Es un catálogo estable del DANE y la ruta
   * pública lo entrega entero; pedir los municipios de cada departamento por separado
   * multiplicaría los viajes para pintar un desplegable.
   */
  fetchDivipolaConCodigos(): Observable<TerritorioConCodigo[]> {
    return this.apiClient.get<UbicacionDivipolaPublica[]>('/api/v1/publico/divipola', {
      errorFallback: 'No fue posible cargar los territorios disponibles'
    }).pipe(map((ubicaciones) => this.agruparConCodigos(ubicaciones)));
  }

  private agruparConCodigos(ubicaciones: readonly UbicacionDivipolaPublica[] | null | undefined): TerritorioConCodigo[] {
    const porDepartamento = new Map<string, TerritorioConCodigo>();

    for (const ubicacion of Array.isArray(ubicaciones) ? ubicaciones : []) {
      const codigoDepartamento = String(ubicacion.departmentCode ?? '').trim();
      const nombreDepartamento = String(ubicacion.departmentName ?? '').trim();
      const codigoMunicipio = String(ubicacion.municipalityCode ?? '').trim();
      const nombreMunicipio = String(ubicacion.municipalityName ?? '').trim();
      if (!codigoDepartamento || !nombreDepartamento) continue;

      const departamento = porDepartamento.get(codigoDepartamento)
        ?? { codigo: codigoDepartamento, nombre: nombreDepartamento, municipios: [] };

      if (codigoMunicipio && nombreMunicipio) {
        departamento.municipios.push({ codigo: codigoMunicipio, nombre: nombreMunicipio });
      }
      porDepartamento.set(codigoDepartamento, departamento);
    }

    const departamentos = [...porDepartamento.values()];
    departamentos.forEach(d => d.municipios.sort((a, b) => a.nombre.localeCompare(b.nombre, 'es-CO')));
    return departamentos.sort((a, b) => a.nombre.localeCompare(b.nombre, 'es-CO'));
  }

  cargarDivipolaPorDepartamento(): Observable<Record<string, string[]>> {
    return this.apiClient.get<UbicacionDivipolaPublica[]>('/api/v1/publico/divipola', {
      errorFallback: 'No fue posible cargar los territorios disponibles'
    }).pipe(map((ubicaciones) => this.agruparDivipola(ubicaciones)));
  }

  // ---------------------------------------------------------------------------
  // EL PUNTO DE REFERENCIA DE CADA MUNICIPIO (29 de agosto de 2026)
  //
  // POR QUE HACE FALTA. El Modo de Practicas e Influencia dibujaba cada proceso en una
  // posicion CALCULADA: el centroide de su departamento mas una espiral cuyo angulo
  // salia del indice del registro en el array. Para dejar de inventar hace falta un
  // punto real por municipio, y `/api/v1/publico/divipola` lo tiene: es anonima y
  // devuelve los 1.122 municipios con `latitude` y `longitude`. Medido contra el API el
  // 29 de agosto de 2026: 1.122 de 1.122 con coordenada.
  //
  // El contrato único entrega las 1.122 ubicaciones en una respuesta. Es un catálogo estable
  // del DANE; duplicar la paginación de una ruta ya retirada solo volvía frágil al geovisor.
  // ---------------------------------------------------------------------------

  private puntosMunicipalesEnCurso: Promise<Map<string, PuntoMunicipalDelCatalogo>> | null = null;

  /**
   * Los puntos de referencia municipales, indexados por codigo DIVIPOLA de cinco digitos.
   *
   * Se cachea la PROMESA y no el resultado: dos llamadas simultaneas comparten la misma
   * descarga en vez de pedir la tabla dos veces.
   */
  fetchPuntosMunicipales(): Promise<Map<string, PuntoMunicipalDelCatalogo>> {
    if (this.puntosMunicipalesEnCurso) return this.puntosMunicipalesEnCurso;

    this.puntosMunicipalesEnCurso = this.descargarPuntosMunicipales().catch((error) => {
      // Se suelta la cache al fallar: si no, un corte de red deja el modo sin puntos
      // para siempre y volver a entrar no reintenta nunca.
      this.puntosMunicipalesEnCurso = null;
      throw error;
    });

    return this.puntosMunicipalesEnCurso;
  }

  private async descargarPuntosMunicipales(): Promise<Map<string, PuntoMunicipalDelCatalogo>> {
    const puntos = new Map<string, PuntoMunicipalDelCatalogo>();
    const filas = await firstValueFrom(
      this.apiClient.get<UbicacionDivipolaPublica[]>('/api/v1/publico/divipola', {
        errorFallback: 'No fue posible cargar los municipios',
      }),
    );

    for (const fila of Array.isArray(filas) ? filas : []) {
      const codigo = this.normalizeMunicipalityCode(fila?.municipalityCode);
      const lat = fila?.latitude;
      const lng = fila?.longitude;
      // Una fila sin coordenada no se guarda con ceros: se queda fuera, y el registro
      // que la busque contará como «municipio sin punto», que es la verdad.
      if (!codigo || typeof lat !== 'number' || typeof lng !== 'number') continue;
      if (!Number.isFinite(lat) || !Number.isFinite(lng)) continue;

      puntos.set(codigo, {
        lat,
        lng,
        nombre: String(fila?.municipalityName ?? '').trim() || codigo,
        codigoDepartamento: this.normalizeDepartmentCode(fila?.departmentCode),
      });
    }

    return puntos;
  }

  private agruparDivipola(ubicaciones: readonly UbicacionDivipolaPublica[] | null | undefined): Record<string, string[]> {
    const agrupado: Record<string, string[]> = {};
    for (const ubicacion of Array.isArray(ubicaciones) ? ubicaciones : []) {
      const departamento = String(ubicacion.departmentName ?? '').trim();
      const municipio = String(ubicacion.municipalityName ?? '').trim();
      if (!departamento || !municipio) continue;
      (agrupado[departamento] ??= []).push(municipio);
    }
    for (const municipios of Object.values(agrupado)) municipios.sort((a, b) => a.localeCompare(b, 'es-CO'));
    return agrupado;
  }
}
