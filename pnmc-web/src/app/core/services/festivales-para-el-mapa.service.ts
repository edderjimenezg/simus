import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { SEPARADOR_DE_CLASIFICACION } from '../../features/map/domain/map-domain';

/**
 * Los Festivales publicados, en la forma que consume el geovisor.
 *
 * <b>SUSTITUYE A `BackendDataService`, y conviene que quede escrito por qué.</b> Aquel servicio
 * tenía 351 líneas, ocho métodos públicos y siete estaban muertos. El único vivo —el que llama
 * esta pantalla— pedía <code>/api/v1/festivals</code>, <b>una ruta que no existe en esta base</b>:
 * el 404 lo tragaba un <code>catchError</code> y el geovisor llevaba enseñando <b>cero
 * festivales</b> sin que nada lo dijera. Era exactamente «un 404 silenciado detrás de un
 * adaptador», que es lo que aquel mismo fichero decía no querer.
 *
 * <b>LA FORMA `fields` SE CONSERVA</b>, y no por nostalgia: el geovisor la lee en una treintena de
 * sitios y cambiarla sería rehacer esa pantalla, que no es lo que toca hoy. Lo que cambia es de
 * dónde salen los datos —de <code>/publico/festivales</code>, la ruta real— y que la traducción
 * está aquí, a la vista, en vez de repartida por un servicio que nadie usaba.
 *
 * <b>NO HAY CORREO NI TELEFONO, Y ES CORRECTO.</b> El contrato público los retiró el 30 de agosto
 * de 2026 por decisión criterio: «todo dato personal o correo se quita de la versión
 * pública, solo se dejan redes sociales». El mapeador anterior los pedía igual, así que habría
 * seguido pidiendo datos que el API ya no entrega aunque la ruta hubiera existido.
 */

/** La ficha pública tal como la entrega el API. */
interface FestivalPublico {
  readonly id?: string;
  readonly nombre?: string;
  readonly descripcion?: string | null;
  readonly organizacionResponsable?: string | null;
  readonly territorioPrincipal?: {
    readonly departamento?: string | null;
    readonly municipio?: string | null;
    readonly nivelCobertura?: string | null;
  };
  readonly practicasMusicales?: readonly { readonly nombre?: string }[];
  readonly territoriosSonoros?: readonly { readonly nombre?: string }[];
  readonly instagram?: string | null;
  readonly facebook?: string | null;
  readonly otroEnlace?: string | null;
  readonly periodicidad?: string | null;
  readonly periodicidadDetalle?: string | null;
  readonly fechaInicioVigente?: string | null;
  readonly fechaFinVigente?: string | null;
  readonly sitioWeb?: string | null;
}

/** La forma que el geovisor lee. Se conserva mientras esa pantalla la use. */
export interface RegistroDeFestivalParaElMapa {
  id: string;
  fields: Record<string, string | number>;
}

const texto = (valor: unknown): string => (typeof valor === 'string' ? valor.trim() : '');

/**
 * Los nombres de un catálogo, unidos para viajar en un solo campo.
 *
 * SE UNEN CON EL SEPARADOR DE UNIDAD Y NO CON UNA COMA. Siete de los catorce territorios sonoros
 * del Plan llevan coma en su propio nombre: unirlos con coma y volver a separarlos por coma parte
 * «Cantos, Pitos y Tambores» en dos territorios que no existen. El porqué completo está en
 * `SEPARADOR_DE_CLASIFICACION`.
 */
const nombres = (lista: readonly { readonly nombre?: string }[] | undefined): string =>
  (lista ?? []).map((x) => texto(x?.nombre)).filter(Boolean).join(SEPARADOR_DE_CLASIFICACION);

@Injectable({ providedIn: 'root' })
export class FestivalesParaElMapaService {
  private readonly api = inject(ApiClientService);

  /**
   * Trae los Festivales publicados.
   *
   * SE PIDE UNA SOLA PAGINA GRANDE porque el geovisor cuenta por departamento sobre el total: una
   * página parcial daría un mapa con cifras que no son las del país.
   */
  consultar(): Observable<{ records: RegistroDeFestivalParaElMapa[] }> {
    return this.api.get<{ items?: FestivalPublico[] }>('/api/v1/publico/festivales', {
      params: { limit: 500, offset: 0 },
      errorFallback: 'No fue posible cargar los Festivales del mapa',
    }).pipe(
      map((respuesta) => ({
        records: (Array.isArray(respuesta?.items) ? respuesta.items : []).map((festival) => ({
          id: texto(festival?.id),
          fields: {
            name: texto(festival?.nombre),
            // Las cuatro claves del departamento existen porque el geovisor prueba con todas: es
            // su forma de sobrevivir a tres orígenes de datos distintos. Se rellenan igual.
            dpt: texto(festival?.territorioPrincipal?.departamento),
            departamento: texto(festival?.territorioPrincipal?.departamento),
            municipio: texto(festival?.territorioPrincipal?.municipio),
            coverageLevel: texto(festival?.territorioPrincipal?.nivelCobertura),
            desc: texto(festival?.descripcion),
            organizador: texto(festival?.organizacionResponsable),
            sitio_web: texto(festival?.sitioWeb),
            // El geovisor filtra por estas dos, y el API ya las entrega resueltas por nombre.
            'Prácticas musicales': nombres(festival?.practicasMusicales),
            'Territorios sonoros': nombres(festival?.territoriosSonoros),
            // LO QUE EL CONTRATO YA ENTREGABA Y ESTE SERVICIO TIRABA. El DTO público lleva desde
            // siempre la presencia pública completa —Instagram, Facebook, otro enlace— y la
            // periodicidad, y aquí sólo se copiaba el sitio web. La ficha del mapa enseñaba en
            // consecuencia una sección «Contacto y Canales» con una sola línea, no porque el dato
            // faltara sino porque se perdía en este punto.
            instagram: texto(festival?.instagram),
            facebook: texto(festival?.facebook),
            otro_enlace: texto(festival?.otroEnlace),
            periodicidad: texto(festival?.periodicidad),
            periodicidad_detalle: texto(festival?.periodicidadDetalle),
            // CUANDO OCURRE. Se añadieron al contrato para esto: la ficha tenía el apartado y no
            // tenía con qué llenarlo.
            fecha_inicio: texto(festival?.fechaInicioVigente),
            fecha_fin: texto(festival?.fechaFinVigente),
          },
        })),
      })),
    );
  }
}
