/**
 * Cada cuánto ocurre un proceso del Ecosistema.
 *
 * <b>ERAN TRES LISTAS, UNA POR PANTALLA, Y YA HABIAN DIVERGIDO.</b> El 17 de septiembre de 2026 el
 * mismo vocabulario vivía copiado dentro de tres componentes —el alta de Festival de la consola, el
 * asistente de Festival y el asistente de Mercado—, y una cuarta pantalla —el cajón de Mercados de
 * la consola— lo capturaba como <b>texto libre</b> con el ejemplo escrito en el marcador. Las copias
 * ya no decían lo mismo: la de Mercados ofrecía «Permanente», que el contrato del servidor no
 * declara, y le faltaban «Trimestral», «Bianual» y «Trienal», que sí acepta.
 *
 * <b>LO QUE SE PUEDE ELEGIR ES LO QUE EL SERVIDOR ADMITE</b>, ni más ni menos. Estos ocho códigos
 * son exactamente los de <c>PeriodicidadesFestival</c> en el contrato: una pantalla que ofrezca un
 * noveno está ofreciendo algo que el circuito no reconoce, y una que ofrezca siete esconde una
 * respuesta legítima.
 *
 * <b>DOS DE ELLOS OBLIGAN A EXPLICARSE.</b> «Otra periodicidad regular» e «Intermitente» no dicen
 * cada cuánto, así que el formulario pide el detalle en cuanto se eligen —y no antes—. La misma
 * regla la aplica el servidor.
 */
export interface OpcionDePeriodicidad {
  readonly codigo: string;
  readonly etiqueta: string;
}

/**
 * Los ocho valores declarados, en el orden en que se ofrecen.
 *
 * DE LO MAS FRECUENTE A LO MENOS: anual es la respuesta de la mayoría de los procesos del
 * Ecosistema, y las dos que obligan a explicarse van al final porque son la salida cuando ninguna
 * de las anteriores sirve.
 */
export const PERIODICIDADES: readonly OpcionDePeriodicidad[] = [
  { codigo: 'anual', etiqueta: 'Anual' },
  { codigo: 'semestral', etiqueta: 'Semestral' },
  { codigo: 'trimestral', etiqueta: 'Trimestral' },
  { codigo: 'bianual', etiqueta: 'Bianual (dos veces al año)' },
  { codigo: 'bienal', etiqueta: 'Bienal (cada dos años)' },
  { codigo: 'trienal', etiqueta: 'Trienal (cada tres años)' },
  { codigo: 'otra_regular', etiqueta: 'Otra periodicidad regular' },
  { codigo: 'intermitente', etiqueta: 'Intermitente o sin periodicidad fija' },
] as const;

/** Las dos que no dicen cada cuánto y por eso piden explicarlo. */
const PIDEN_DETALLE = ['otra_regular', 'intermitente'];

/** Si la periodicidad elegida obliga a explicar cuál es. */
export function pideDetalle(periodicidad: string | null | undefined): boolean {
  return PIDEN_DETALLE.includes((periodicidad ?? '').trim().toLowerCase());
}

/**
 * Cómo se lee una periodicidad guardada.
 *
 * Si el código no está entre los ocho se devuelve tal cual y no una cadena vacía: un dato raro es
 * más fácil de diagnosticar que un dato ausente, que es el mismo criterio de `etiquetaDelFestival`.
 */
export function etiquetaDePeriodicidad(codigo: string | null | undefined): string {
  const clave = (codigo ?? '').trim().toLowerCase();
  if (!clave) return '';
  return PERIODICIDADES.find(opcion => opcion.codigo === clave)?.etiqueta ?? clave;
}
