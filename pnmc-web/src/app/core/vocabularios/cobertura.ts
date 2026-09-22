/**
 * Hasta dónde llega un proceso del Ecosistema.
 *
 * <b>ESTABA DENTRO DE LA CONSOLA, Y LAS PAGINAS PUBLICAS NO PODIAN USARLO.</b> Vivía en
 * `features/admin/domain/admin-config.ts`, así que las fichas públicas de Festival y de Mercado
 * —que no deben importar del espacio administrativo— enseñaban el código crudo: «departamental»,
 * en minúscula, en la ficha que lee cualquier ciudadano. Es el mismo caso que el vocabulario de
 * periodicidad, y por eso vive ahora en el mismo sitio.
 *
 * <b>`sin_definir` NO ES UN NIVEL DE UN PROCESO.</b> Es como nace una organización registrada desde
 * fuera, cuyo territorio lo declara cada proceso que monte. Se nombra igual porque el dato existe,
 * pero ningún formulario de proceso lo ofrece.
 */
const ETIQUETAS: Readonly<Record<string, string>> = {
  sin_definir: 'Sin definir',
  municipal: 'Municipal',
  departamental: 'Departamental',
  nacional: 'Nacional',
};

/**
 * Cómo se lee un nivel de cobertura guardado.
 *
 * Si el código no se reconoce se devuelve tal cual y no una cadena vacía: un dato raro es más fácil
 * de diagnosticar que un dato ausente, que es el mismo criterio del resto de los vocabularios.
 */
export function etiquetaDeCobertura(codigo: string | null | undefined): string {
  const clave = (codigo ?? '').trim().toLowerCase();
  if (!clave) return '';
  return ETIQUETAS[clave] ?? clave;
}

/** El vocabulario entero, para quien tenga que ofrecerlo como lista. */
export const COBERTURAS = ETIQUETAS;
