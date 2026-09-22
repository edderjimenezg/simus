import { separarClasificacion } from './map-domain';

/**
 * La aritmética de las lecturas analíticas del geovisor.
 *
 * <b>POR QUE VIVE FUERA DEL COMPONENTE.</b> `mapa-ecosistemico-page.component.ts` pasaba de cinco
 * mil líneas y buena parte no era Angular: eran cuentas. Contar cuántas fichas declaran cada
 * territorio sonoro, cruzar grupos con departamentos o repartir la agenda por meses no necesita
 * señales, ni plantilla, ni ciclo de vida; sólo datos de entrada y datos de salida.
 *
 * <b>LO QUE ESO GANA NO ES ESTETICA.</b> Aquí se puede probar cada cuenta con un array literal y
 * una aserción, sin `TestBed`, sin montar Leaflet y sin la convención de «no llamar a
 * `detectChanges()`» que arrastran las pruebas de esa pantalla. Y al no depender de Angular, estas
 * funciones sobreviven a cualquier reorganización de la vista.
 *
 * <b>NADA DE ESTO LEE SEÑALES.</b> Es la regla que mantiene el fichero honesto: si una función
 * necesitara `this.algo()`, es que no era aritmética y su sitio es el componente.
 */

/** Una ficha de la que se puede leer su clasificación declarada. */
export interface FichaClasificada {
  readonly linkedSonorousTerritories?: string;
  readonly practices?: string;
  readonly record?: { readonly linkedSonorousTerritories?: string; readonly practices?: string };
}

export type DimensionDeLectura = 'territorios-sonoros' | 'practicas';

/** El nombre del grupo de las fichas que no declaran nada. */
export const SIN_CLASIFICAR = 'Sin clasificar';

/**
 * Qué declara una ficha en una dimensión.
 *
 * <b>LA CLASIFICACION VIVE EN DOS SITIOS Y HAY QUE MIRAR LOS DOS.</b> El directorio envuelve cada
 * ficha —`{ record: {...}, name, type }`— y los conteos por departamento que alimentan el mapa no:
 * ahí los campos están en el primer nivel. Leer sólo el envuelto dejaba todos los símbolos del mapa
 * en «Sin clasificar» mientras la columna izquierda enseñaba catorce grupos.
 *
 * <b>«SIN CLASIFICAR» ES UNA CATEGORIA, NO UN HUECO.</b> Es lo que convierte «desaparece» en «no
 * consta», y la diferencia importa: un registro sin territorio declarado es un dato que falta, no
 * un registro que no existe.
 */
export function clasificacionDeclarada(
  ficha: FichaClasificada | null | undefined,
  dimension: DimensionDeLectura,
): string[] {
  const fuente = ficha?.record ?? ficha;
  const crudo = dimension === 'territorios-sonoros'
    ? (fuente?.linkedSonorousTerritories ?? '')
    : (fuente?.practices ?? '');
  const valores = separarClasificacion(crudo);
  return valores.length > 0 ? valores : [SIN_CLASIFICAR];
}

/** Un grupo de una dimensión, con cuántas fichas lo declaran. */
export interface GrupoContado {
  readonly nombre: string;
  readonly total: number;
}

/**
 * Cuántas fichas declaran cada valor de una dimensión, de mayor a menor.
 *
 * <b>«SIN CLASIFICAR» VA AL FINAL AUNQUE SEA EL MAYOR</b>: es la ausencia de dato, no un grupo más,
 * y encabezar la lista con él daría a entender que es la lectura principal del ecosistema.
 *
 * <b>UNA FICHA CUENTA EN TODOS LOS QUE DECLARA</b>, así que la suma de los grupos puede pasar del
 * número de fichas. Quien use esto tiene que decirlo en pantalla: ver `componerEcosistema`.
 */
export function contarPorDimension(
  fichas: readonly FichaClasificada[],
  dimension: DimensionDeLectura,
): GrupoContado[] {
  const cuenta = new Map<string, number>();
  for (const ficha of fichas) {
    for (const valor of clasificacionDeclarada(ficha, dimension)) {
      cuenta.set(valor, (cuenta.get(valor) ?? 0) + 1);
    }
  }
  return [...cuenta.entries()]
    .map(([nombre, total]) => ({ nombre, total }))
    .sort((uno, otro) => {
      if (uno.nombre === SIN_CLASIFICAR) return 1;
      if (otro.nombre === SIN_CLASIFICAR) return -1;
      return otro.total - uno.total || uno.nombre.localeCompare(otro.nombre, 'es');
    });
}

/**
 * Reparte una paleta entre una lista de nombres YA ORDENADA.
 *
 * <b>POR ORDEN Y NO POR UN RESUMEN DEL TEXTO.</b> Que el color salga de la posición y no de un hash
 * del nombre es lo que hace que el mismo territorio sonoro tenga el mismo color entre dos cargas de
 * la misma pantalla. Un color que baila hace ilegible cualquier comparación.
 */
export function repartirColores(
  nombres: readonly string[],
  paleta: readonly string[],
  colorSinClasificar: string,
): ReadonlyMap<string, string> {
  const mapa = new Map<string, string>();
  let indice = 0;
  for (const nombre of nombres) {
    if (nombre === SIN_CLASIFICAR) {
      mapa.set(nombre, colorSinClasificar);
      continue;
    }
    mapa.set(nombre, paleta[indice % paleta.length]);
    indice += 1;
  }
  return mapa;
}

/** El reparto del ecosistema por una dimensión, listo para dibujar. */
export interface ComposicionDelEcosistema {
  readonly fichas: number;
  readonly suma: number;
  readonly solapan: boolean;
  readonly filas: readonly {
    readonly nombre: string;
    readonly total: number;
    readonly porcentaje: number;
    readonly cuota: number;
    readonly color: string;
  }[];
}

/**
 * De qué está hecho el ecosistema por una dimensión.
 *
 * <b>LA CUOTA SE CALCULA SOBRE LAS FICHAS Y NO SOBRE LA SUMA.</b> «Dos de cada tres festivales son
 * de joropo» es cierto y se entiende; «el 22 % de las declaraciones» no se lo pregunta nadie.
 *
 * <b>`solapan` NO ES DECORACION.</b> Es lo que permite avisar de que las cifras suman más que el
 * total, y sólo cuando de verdad ocurre: un aviso que aparece siempre deja de leerse.
 */
export function componerEcosistema(
  fichas: readonly FichaClasificada[],
  dimension: DimensionDeLectura,
  colores: ReadonlyMap<string, string>,
  colorSinClasificar: string,
): ComposicionDelEcosistema {
  const grupos = contarPorDimension(fichas, dimension);
  const suma = grupos.reduce((total, grupo) => total + grupo.total, 0);
  const maximo = grupos.reduce((mayor, grupo) => Math.max(mayor, grupo.total), 0) || 1;

  return {
    fichas: fichas.length,
    suma,
    solapan: suma > fichas.length,
    filas: grupos.map((grupo) => ({
      nombre: grupo.nombre,
      total: grupo.total,
      porcentaje: Math.max(2, Math.round((grupo.total / maximo) * 100)),
      cuota: fichas.length > 0 ? Math.round((grupo.total / fichas.length) * 100) : 0,
      color: colores.get(grupo.nombre) ?? colorSinClasificar,
    })),
  };
}

/** Una fila del cruce: un grupo y cuántos tiene en cada departamento. */
export interface FilaDelCruce {
  readonly nombre: string;
  readonly total: number;
  readonly departamentos: number;
  readonly celdas: readonly { readonly clave: string; readonly etiqueta: string; readonly total: number }[];
}

/**
 * Cruza los grupos de una dimensión con los departamentos.
 *
 * <b>ES LA UNICA LECTURA QUE ENSEÑA LA TESIS DEL LENTE.</b> El mapa puede pintar cuál grupo
 * predomina en cada departamento; no puede enseñar lo contrario —en cuántos departamentos vive un
 * mismo grupo— porque para eso habría que leerlo al revés.
 *
 * <b>LA CELDA DICE CUANTOS, NO SOLO SI.</b> Una matriz de presencia igualaría un departamento con
 * un proceso y otro con veinte.
 */
export function cruzarGruposConDepartamentos(
  fichas: readonly (FichaClasificada & { readonly clave: string })[],
  dimension: DimensionDeLectura,
  grupos: readonly GrupoContado[],
  colores: ReadonlyMap<string, string>,
  colorSinClasificar: string,
  nombreDelDepartamento: (clave: string) => string,
): { grupos: FilaDelCruce[]; departamentos: { clave: string; etiqueta: string }[]; maximo: number } {
  const cuenta = new Map<string, Map<string, number>>();
  const porDepartamento = new Map<string, number>();

  for (const ficha of fichas) {
    if (!ficha.clave) continue;
    for (const grupo of clasificacionDeclarada(ficha, dimension)) {
      const fila = cuenta.get(grupo) ?? new Map<string, number>();
      fila.set(ficha.clave, (fila.get(ficha.clave) ?? 0) + 1);
      cuenta.set(grupo, fila);
      porDepartamento.set(ficha.clave, (porDepartamento.get(ficha.clave) ?? 0) + 1);
    }
  }

  // Las columnas van de más a menos, para que la matriz se lea de izquierda a derecha como el
  // ranking; las filas conservan el orden de los grupos, que es el que reparte los colores.
  const departamentos = [...porDepartamento.entries()]
    .sort((uno, otro) => otro[1] - uno[1])
    .map(([clave]) => ({ clave, etiqueta: nombreDelDepartamento(clave) }));

  let maximo = 0;
  const filas = grupos.map((grupo) => {
    const fila = cuenta.get(grupo.nombre) ?? new Map<string, number>();
    const celdas = departamentos.map((departamento) => {
      const total = fila.get(departamento.clave) ?? 0;
      if (total > maximo) maximo = total;
      return { clave: departamento.clave, etiqueta: departamento.etiqueta, total };
    });
    return {
      nombre: grupo.nombre,
      total: grupo.total,
      // EN CUANTOS DEPARTAMENTOS VIVE, que es la cifra que responde la pregunta del lente y no se
      // puede leer contando marcas a ojo en una fila de treinta columnas.
      departamentos: celdas.filter((celda) => celda.total > 0).length,
      celdas,
    };
  });

  return { grupos: filas, departamentos, maximo: maximo || 1 };
}

/** Un mes del reparto temporal de la agenda. */
export interface MesDeLaAgenda {
  readonly indice: number;
  readonly etiqueta: string;
  readonly total: number;
  readonly altura: number;
  readonly esActual: boolean;
}

/**
 * Reparte fechas por mes del año.
 *
 * <b>DOCE MESES SIEMPRE, INCLUIDOS LOS VACIOS.</b> Enseñar sólo los meses con eventos comprime la
 * escala y esconde justo lo que hay que ver: los huecos. Un año con todo en noviembre y un año
 * repartido se verían igual.
 *
 * `'es-CO'` explícito: `LOCALE_ID` está deliberadamente sin fijar en este proyecto.
 */
export function repartirPorMes(fechas: readonly string[], hoy = new Date()): {
  total: number;
  mesActual: number;
  meses: MesDeLaAgenda[];
} {
  const cuentas = new Array<number>(12).fill(0);
  let contados = 0;
  for (const fecha of fechas) {
    const momento = new Date(`${fecha}T00:00:00`);
    if (Number.isNaN(momento.getTime())) continue;
    cuentas[momento.getMonth()] += 1;
    contados += 1;
  }

  const maximo = cuentas.reduce((mayor, total) => Math.max(mayor, total), 0) || 1;
  const mesActual = hoy.getMonth();

  return {
    total: contados,
    mesActual,
    meses: cuentas.map((total, indice) => ({
      indice,
      etiqueta: new Date(2000, indice, 1)
        .toLocaleDateString('es-CO', { month: 'short' })
        .replace('.', ''),
      total,
      altura: Math.round((total / maximo) * 100),
      esActual: indice === mesActual,
    })),
  };
}
