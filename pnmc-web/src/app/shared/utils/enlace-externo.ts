/**
 * Convierte lo que una organización escribió en un enlace que se puede pinchar, o en nada.
 *
 * <b>ES UNA COMPROBACION DE SEGURIDAD, NO UN ARREGLO DE FORMATO.</b> Un campo de texto libre que
 * acaba en un `href` es por donde entra un `javascript:` en la página pública. Por eso solo pasan
 * `http` y `https`: <b>cualquier otro esquema explícito se descarta</b>, y lo que no trae esquema se
 * asume dominio y se sirve por `https`.
 *
 * <b>Y POR ESO VIVE AQUI Y NO EN CADA DIRECTORIO.</b> Estaba escrita dentro del componente del
 * directorio de Festivales; copiarla al de Mercados habría dejado dos versiones de la misma regla,
 * que es la peor forma de tener una comprobación de seguridad: el día que una se endurezca, la otra
 * se queda como estaba y nadie lo nota.
 *
 * Devuelve la cadena vacía cuando no hay enlace utilizable, para que quien la use pueda decidir con
 * un `if` en vez de con un `try`.
 */
export function enlaceExterno(valor: string | null | undefined): string {
  const bruto = (valor ?? '').trim();
  if (!bruto) return '';
  if (/^https?:\/\//i.test(bruto)) return bruto;
  if (/^[a-z][a-z0-9+.-]*:/i.test(bruto)) return '';
  return `https://${bruto}`;
}
