import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom, from, map, of } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/** El parámetro que pide previsualizar un registro dentro de su listado público. */
export const PARAMETRO_DE_PREVISUALIZACION = 'pnmcPrevisualizar';

/** Los listados que saben previsualizar uno de los suyos. */
export type ModuloPrevisualizable = 'noticias' | 'agenda' | 'catalogo-editorial' | 'festivales' | 'mercados';

/**
 * Trae un registro sin publicar para enseñarlo dentro de su listado real.
 *
 * <b>QUE PROBLEMA RESUELVE.</b> La consola solo ofrecía «Ver en el portal» cuando el registro ya
 * estaba publicado: la única forma de saber cómo iba a quedar algo era publicarlo. Lo que se
 * descubre así —un titular que se corta a dos líneas, un resumen vacío, una imagen mal encuadrada—
 * se descubre con el registro ya en la calle.
 *
 * <b>SE INYECTA EN EL PROPIO SERVICIO DE LECTURA, no en las páginas.</b> Las tres pantallas
 * públicas —Noticias, Agenda y Catálogo Editorial— tienen su diseño aprobado y no se tocan: la
 * previsualización entra por donde ya entran sus datos, así que la tarjeta que se ve es la tarjeta
 * de verdad y no una reproducción que se desincroniza al primer cambio.
 *
 * <b>EL SERVIDOR ES LA UNICA GUARDA, y basta.</b> La ruta exige sesión de consola; sin ella
 * responde 401 y este servicio devuelve <code>null</code>, con lo que el listado se queda como
 * estaba. Por eso —a diferencia de <code>vista-previa-puente.ts</code>— aquí no hace falta exigir
 * que la página esté dentro de un marco: allí el borrador viajaba por <code>postMessage</code>
 * desde el panel y había que blindar a la página; aquí el borrador no sale del servidor sin una
 * petición autenticada. Y no exigirlo permite abrir la previsualización en una pestaña aparte, que
 * es lo que se quiere hacer la mitad de las veces.
 */
@Injectable({ providedIn: 'root' })
export class PrevisualizacionEnListadoService {
  private readonly api = inject(ApiClientService);

  /**
   * El identificador pedido para ese módulo, o `null`.
   *
   * EL PARAMETRO LLEVA EL MODULO —`?pnmcPrevisualizar=noticias:12`— para que una dirección no pueda
   * pedirle a la Agenda que previsualice una noticia: el listado solo atiende lo suyo.
   */
  idPara(modulo: ModuloPrevisualizable, busqueda?: string): number | null {
    const cadena = busqueda ?? (typeof location === 'undefined' ? '' : location.search);
    const valor = new URLSearchParams(cadena).get(PARAMETRO_DE_PREVISUALIZACION);
    if (!valor) return null;
    const [pedido, id] = valor.split(':');
    if (pedido !== modulo) return null;
    const numero = Number(id);
    return Number.isInteger(numero) && numero > 0 ? numero : null;
  }

  /**
   * El registro, con la forma exacta de la lectura pública, o `null` si no se puede.
   *
   * NUNCA LANZA. Un fallo aquí no puede tumbar el listado: sin sesión, sin registro o sin red, la
   * pantalla se queda con lo que ya tenía, que es exactamente lo que vería un visitante.
   */
  async obtener<T>(modulo: ModuloPrevisualizable, id: number): Promise<T | null> {
    try {
      return await firstValueFrom(this.api.get<T>(`/api/v1/admin/previsualizacion/${modulo}/${id}`, {
        errorFallback: 'No fue posible previsualizar el registro.',
      }));
    } catch {
      return null;
    }
  }

  /**
   * Dónde empieza la tarjeta ORDINARIA de cada listado.
   *
   * <b>NO ES UN DETALLE DE COLOCACION, ES DE QUE TARJETA SE VE.</b> Noticias abre con tres piezas
   * destacadas —una portada grande y dos tarjetas de apoyo— que usan un diseño distinto del de la
   * cuadrícula. Poner ahí el registro previsualizado lo enseñaba con la tarjeta de portada, que es
   * justamente la que no va a tener: la pregunta «¿cómo se verá?» recibía la respuesta de otra
   * tarjeta. Agenda y Catálogo Editorial no destacan nada, así que su primera posición ya es la
   * ordinaria.
   */
  private static readonly PRIMERA_TARJETA_ORDINARIA: Record<ModuloPrevisualizable, number> = {
    noticias: 3,
    agenda: 0,
    'catalogo-editorial': 0,
    festivales: 0,
    // El directorio de mercados no tiene tarjeta destacada: la previsualizada va la primera.
    mercados: 0,
  };

  /**
   * Coloca el registro previsualizado en el listado, sin duplicarlo.
   *
   * LO MAS ARRIBA POSIBLE DENTRO DE LAS TARJETAS ORDINARIAS: previsualizar es una pregunta sobre
   * ESE registro, y obligar a buscarlo entre cincuenta convierte la respuesta en una búsqueda. Si
   * ya estaba en el listado —porque ya era público— se sustituye en vez de aparecer dos veces.
   */
  anteponer<T>(
    lista: T[],
    previsualizado: T | null,
    mismoId: (a: T, b: T) => boolean,
    modulo: ModuloPrevisualizable = 'agenda',
  ): T[] {
    if (!previsualizado) return lista;
    const restantes = lista.filter(item => !mismoId(item, previsualizado));
    const posicion = Math.min(
      PrevisualizacionEnListadoService.PRIMERA_TARJETA_ORDINARIA[modulo],
      restantes.length);
    return [...restantes.slice(0, posicion), previsualizado, ...restantes.slice(posicion)];
  }

  /**
   * <b>VIVE AQUI Y NO EN `CatalogService`.</b> Era un método privado del cajón de sastre de mapas,
   * así que cualquier servicio que quisiera previsualizar tenía que pasar por él. Lo que hace es
   * previsualización, no catálogo.
   *
   * Antepone el registro que se está previsualizando, si lo hay.
   *
   * <b>SE ADAPTA CON EL MISMO ADAPTADOR QUE EL RESTO.</b> El servidor devuelve el mismo DTO que la
   * lectura pública y aquí pasa por la misma función: así la tarjeta previsualizada no puede
   * diferenciarse de la real ni en una fecha mal formateada.
   *
   * SIN PARAMETRO NO SE PIDE NADA. La inmensa mayoría de las visitas no previsualizan, y no pueden
   * pagar una petición de más por una función de la consola.
   */
  conListado<TDto, TDiseno>(
    modulo: ModuloPrevisualizable,
    items: TDiseno[],
    adaptar: (dto: TDto) => TDiseno,
    mismoId: (a: TDiseno, b: TDiseno) => boolean,
  ): Observable<TDiseno[]> {
    const id = this.idPara(modulo);
    if (id === null) return of(items);
    return from(this.obtener<TDto>(modulo, id)).pipe(
      map(dto => this.anteponer(items, dto ? adaptar(dto) : null, mismoId, modulo)),
    );
  }
}
