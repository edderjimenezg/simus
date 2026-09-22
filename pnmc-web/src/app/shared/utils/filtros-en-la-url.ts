import { WritableSignal, effect, inject, untracked } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/**
 * Los filtros de un listado, guardados en la URL.
 *
 * <b>POR QUE EXISTE.</b> El §10 del plan pide que Noticias, Agenda y Catálogo Editorial compartan
 * «filtros/URL» y avisa de lo que hay que evitar: «soluciones visualmente iguales pero técnicamente
 * distintas». Aquí eran peor que distintas: <b>ninguna de las tres existía</b>. Los tres frentes
 * públicos guardaban sus filtros solo en señales del componente, con tres vocabularios para lo
 * mismo —`newsCategoryFilter` con el vacío escrito como `'all'`, `selectedCategory` con `'Todos'`,
 * `activeTab` con `'all'`—, y eso tenía tres consecuencias que se notan usando el sitio:
 *
 * <ul>
 *   <li><b>Una búsqueda no se puede compartir.</b> Filtrar Noticias por «Convocatorias» y pasarle
 *       el enlace a alguien le abre la lista entera.</li>
 *   <li><b>Recargar lo pierde todo.</b> Y también volver de una ficha al listado.</li>
 *   <li><b>«Atrás» no deshace el filtro</b>, porque el filtro nunca fue un estado de la
 *       navegación.</li>
 * </ul>
 *
 * El listado de Festivales sí lo resolvía, con `hidratarDesdeUrl` y `sincronizarUrl` escritos a
 * mano dentro del componente. Esta pieza es exactamente esas dos funciones sacadas a un sitio
 * común, para que la cuarta pantalla no vuelva a escribirlas.
 *
 * <b>LO QUE NO HACE, Y ES DELIBERADO.</b> No toca el marcado ni decide cómo se ven los filtros:
 * los diseños de esos tres frentes están aprobados y no se tocan. Esto vive por debajo.
 */

/** Cómo se traduce un filtro entre la URL y la señal que lo guarda. */
export interface FiltroEnLaUrl<T> {
  /** El nombre del parámetro en la URL. Corto y en español: `q`, `categoria`, `pagina`. */
  parametro: string;
  /** La señal del componente que lo guarda. */
  senal: WritableSignal<T>;
  /**
   * El valor que significa «sin filtrar».
   *
   * <b>SE DECLARA Y NO SE ADIVINA.</b> Los tres frentes escriben ese vacío de forma distinta
   * —`''`, `'all'`, `'Todos'`, `1`— y unificarlo por dentro obligaría a tocar su lógica de
   * filtrado, que es código de diseño aprobado. Se declara aquí y la URL queda limpia igual:
   * un filtro en su valor vacío no se escribe.
   */
  vacio: T;
  /** De texto de la URL al valor de la señal. Por omisión, el texto tal cual. */
  desdeTexto?: (texto: string) => T;
  /** Del valor de la señal a texto. Por omisión, `String(valor)`. */
  aTexto?: (valor: T) => string;
}

/**
 * Un filtro declarado, de cualquiera de los dos tipos que un listado necesita.
 *
 * Se declara como unión y no con un genérico suelto para que las funciones de abajo puedan recibir
 * una lista mezclada —texto y número— sin que cada llamada tenga que afirmar un tipo.
 */
export type FiltroDeclarado = FiltroEnLaUrl<string> | FiltroEnLaUrl<number>;

// LAS TRES FUNCIONES DE ABAJO AFIRMAN EL TIPO UNA VEZ, Y AQUI ESTA EL PORQUE.
// `FiltroDeclarado` es una union de dos formas completas —`FiltroEnLaUrl<string>` y
// `FiltroEnLaUrl<number>`—, no un objeto con campos de tipo union. TypeScript no puede estrechar
// `senal` mirando `vacio`, porque son propiedades distintas: comprobar una no dice nada de la otra.
// Se estrecha a mano, con `typeof vacio` como discriminante, y la afirmacion queda encerrada en
// estas pocas lineas en vez de repartirse por cada llamada.

/** Pone en la señal el texto de la URL, resolviendo el tipo en un solo sitio. */
function aplicar(filtro: FiltroDeclarado, texto: string): void {
  if (typeof filtro.vacio === 'number') {
    const numerico = filtro as FiltroEnLaUrl<number>;
    numerico.senal.set((numerico.desdeTexto ?? Number)(texto));
  } else {
    const textual = filtro as FiltroEnLaUrl<string>;
    textual.senal.set((textual.desdeTexto ?? ((valor: string) => valor))(texto));
  }
}

/** Devuelve un filtro a su valor vacío, resolviendo el tipo igual que `aplicar`. */
function vaciar(filtro: FiltroDeclarado): void {
  if (typeof filtro.vacio === 'number') (filtro as FiltroEnLaUrl<number>).senal.set(filtro.vacio);
  else (filtro as FiltroEnLaUrl<string>).senal.set(filtro.vacio);
}

/** Cómo se escribe en la URL el valor que tiene ahora la señal. */
function comoTexto(filtro: FiltroDeclarado): string {
  return typeof filtro.vacio === 'number'
    ? ((filtro as FiltroEnLaUrl<number>).aTexto ?? String)((filtro as FiltroEnLaUrl<number>).senal())
    : ((filtro as FiltroEnLaUrl<string>).aTexto ?? String)((filtro as FiltroEnLaUrl<string>).senal());
}

/**
 * Engancha un conjunto de filtros a la URL: los lee al entrar y los escribe en cada cambio.
 *
 * <b>SE LLAMA EN EL CONSTRUCTOR</b>, porque monta un `effect` y necesita el contexto de inyección.
 *
 * <b>LEE UNA VEZ Y ESCRIBE SIEMPRE.</b> Leer en cada cambio de la URL crearía un bucle: la
 * escritura dispararía la lectura, que dispararía la escritura. La lectura al entrar es lo que
 * sostiene los tres casos que importan —recargar, «Atrás» hasta salir, y abrir un enlace que
 * alguien compartió—; a partir de ahí la fuente de verdad son las señales.
 */
export function enlazarFiltrosConLaUrl(filtros: readonly FiltroDeclarado[]): void {
  const route = inject(ActivatedRoute);
  const router = inject(Router);

  hidratarDesdeLaUrl(filtros, route);

  effect(() => {
    // Se leen TODAS las señales dentro del efecto para que cualquiera de ellas lo despierte.
    const parametros: Record<string, string | null> = {};
    for (const filtro of filtros) {
      const esVacio = Object.is(filtro.senal(), filtro.vacio);
      parametros[filtro.parametro] = esVacio ? null : comoTexto(filtro);
    }

    // `untracked` para que navegar no se cuente como una lectura y el efecto no se autoalimente.
    untracked(() => {
      void router.navigate([], {
        relativeTo: route,
        queryParams: parametros,
        queryParamsHandling: 'merge',
        // SIN APILAR UNA ENTRADA DE HISTORIAL POR CADA TECLA. `replaceUrl` deja «Atrás» para volver
        // a la pantalla anterior, no para deshacer letra a letra lo escrito en el buscador.
        replaceUrl: true,
      });
    });
  });
}

/**
 * Pone en las señales lo que ya traía la URL.
 *
 * Se exporta aparte porque hay pantallas que necesitan hidratar sin sincronizar de vuelta —una
 * ficha que lee un parámetro y no lo reescribe— y porque así se puede probar sola.
 */
export function hidratarDesdeLaUrl(filtros: readonly FiltroDeclarado[], route: ActivatedRoute): void {
  const parametros = route.snapshot.queryParamMap;
  for (const filtro of filtros) {
    const texto = parametros.get(filtro.parametro);
    // UN PARAMETRO AUSENTE NO ES UN PARAMETRO VACIO. Si la URL no lo trae, la señal se queda como
    // la dejó el componente: puede tener un valor inicial que no es el vacío —`'newest'` en el
    // orden de Noticias— y machacarlo con `vacio` cambiaría lo que se ve al entrar.
    if (texto === null) continue;
    aplicar(filtro, texto);
  }
}

/**
 * El filtro de página, que es igual en las cuatro pantallas y siempre se escribía a mano.
 *
 * La página 1 no se escribe en la URL: es el valor vacío, y una URL que dice `?pagina=1` no aporta
 * nada y estorba al compartirla. Un valor que no sea un entero positivo cae en 1 en vez de dejar
 * la lista en blanco: `?pagina=abc` llega de un enlace mal copiado, no de un ataque, y lo correcto
 * es enseñar la primera página.
 */
export function filtroDePagina(senal: WritableSignal<number>, parametro = 'pagina'): FiltroEnLaUrl<number> {
  return {
    parametro,
    senal,
    vacio: 1,
    desdeTexto: (texto: string) => {
      const numero = Number(texto);
      return Number.isInteger(numero) && numero > 0 ? numero : 1;
    },
  };
}

/**
 * Un filtro de texto cuyo vacío es la cadena vacía. El caso más común.
 */
export function filtroDeTexto(parametro: string, senal: WritableSignal<string>, vacio = ''): FiltroEnLaUrl<string> {
  return { parametro, senal, vacio };
}

/**
 * Un filtro de dos posiciones —la forma de leer el listado— que solo escribe la que no es la de
 * partida.
 *
 * `vista=cuadricula` en la URL sí dice algo; `vista=listado` es el valor por omisión y solo alarga
 * el enlace.
 */
export function filtroDeVista<T extends string>(
  parametro: string,
  senal: WritableSignal<T>,
  porOmision: T,
): FiltroEnLaUrl<string> {
  return { parametro, senal: senal as unknown as WritableSignal<string>, vacio: porOmision };
}

/**
 * Si algún filtro está puesto, para rotular «Limpiar filtros (n)» con una sola cuenta.
 *
 * <b>LAS TRES PANTALLAS LO CONTABAN DISTINTO O NO LO CONTABAN.</b> Noticias tenía
 * `resetAllFilters()` pero no decía cuántos había; Agenda no tenía forma de limpiarlo todo; el
 * Catálogo solo limpiaba la búsqueda. Con el vacío ya declarado por filtro, la cuenta sale sola.
 */
export function contarFiltrosPuestos(filtros: readonly FiltroDeclarado[]): number {
  return filtros.filter(filtro => !Object.is(filtro.senal(), filtro.vacio)).length;
}

/**
 * Devuelve cada filtro a su valor vacío.
 *
 * La página también, y eso importa: quitar los filtros mientras se está en la página 4 deja la
 * lista en blanco si la consulta sin filtrar no llega a cuatro páginas.
 */
export function limpiarFiltros(filtros: readonly FiltroDeclarado[]): void {
  for (const filtro of filtros) vaciar(filtro);
}
