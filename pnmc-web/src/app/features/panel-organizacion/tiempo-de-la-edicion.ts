/**
 * El mes o los meses en que ocurre una Edición, y cuántos días dura.
 *
 * <b>LOS PIDE LA HISTORIA DE USUARIO COMO OBLIGATORIOS Y AUTOCALCULADOS</b> —«estos campos son
 * automáticos y se diligencian teniendo en cuenta la fecha inicio y fecha fin del festival»— y no
 * existían por ninguna parte: ni columna, ni cálculo, ni casilla en pantalla.
 *
 * <b>POR QUÉ ESTO VIVE EN EL CLIENTE SI EL SERVIDOR YA LO CALCULA.</b> Porque tienen que verse
 * MIENTRAS se teclean las fechas, no después de guardar; ese es el comportamiento que la historia
 * describe. El servidor los calcula igual para quien lea la edición por el API.
 *
 * <b>Y POR ESO ESTO ES UNA FUNCIÓN PURA CON SU PROPIA PRUEBA.</b> Dos implementaciones de la misma
 * regla se separan en cuanto una de las dos se retoca; la defensa es que las dos estén fijadas por
 * la misma tabla de casos —la de `TiempoDeLaEdicionTests` en el API y la de esta función aquí—.
 *
 * NINGUNO DE LOS DOS SE GUARDA. Un derivado almacenado es un derivado que se desincroniza: basta
 * con corregir una fecha por otro camino para que el mes diga lo que ya no es.
 */

const MESES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
];

export interface TiempoDeLaEdicion {
  /** «Enero», o «Enero, Febrero» si la Edición cruza de mes. */
  readonly mes: string | null;
  /** Días entre inicio y fin, contando los dos extremos: un evento de un día dura 1. */
  readonly duracionDias: number | null;
}

/**
 * Lee una fecha `AAAA-MM-DD` sin pasar por `Date`.
 *
 * <b>`new Date('2026-03-01')` NO ES EL 1 DE MARZO EN TODAS PARTES.</b> Esa forma se interpreta como
 * medianoche UTC, y en Colombia —UTC-5— cae el 28 de febrero. El mes calculado sería el anterior,
 * y en un festival de un solo día la duración se iría a cero. Se parte la cadena y ya.
 */
function leer(fecha: string | null | undefined): { anio: number; mes: number; dia: number } | null {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec((fecha ?? '').trim());
  if (!partes) return null;
  const anio = Number(partes[1]); const mes = Number(partes[2]); const dia = Number(partes[3]);
  if (mes < 1 || mes > 12 || dia < 1 || dia > 31) return null;
  return { anio, mes, dia };
}

/** Días absolutos, para restar dos fechas sin husos de por medio. */
function comoDia(f: { anio: number; mes: number; dia: number }): number {
  return Math.floor(Date.UTC(f.anio, f.mes - 1, f.dia) / 86_400_000);
}

export function tiempoDeLaEdicion(fechaInicio: string | null | undefined, fechaFin: string | null | undefined): TiempoDeLaEdicion {
  const inicio = leer(fechaInicio);
  const fin = leer(fechaFin);
  if (!inicio && !fin) return { mes: null, duracionDias: null };

  // CON UNA SOLA FECHA SE CONTESTA LO QUE SE SABE: el mes de la que haya, y ninguna duración.
  // Callar las dos cosas porque falta una obliga a terminar el formulario para ver algo que ya se
  // puede decir.
  if (!inicio || !fin) {
    const unica = (inicio ?? fin)!;
    return { mes: MESES[unica.mes - 1], duracionDias: null };
  }
  // UNA FECHA DE FIN ANTERIOR A LA DE INICIO NO SE INVENTA UN RESULTADO: eso lo señala la
  // validación, y un «-3 días» en pantalla compite con ese aviso.
  if (comoDia(fin) < comoDia(inicio)) return { mes: null, duracionDias: null };

  const meses: string[] = [];
  let anio = inicio.anio; let mes = inicio.mes;
  // SE LIMITA A DOCE: una edición que cruce más de un año es un dato equivocado, y una lista de
  // cuarenta meses en una casilla no ayuda a verlo.
  while ((anio < fin.anio || (anio === fin.anio && mes <= fin.mes)) && meses.length < 12) {
    meses.push(MESES[mes - 1]);
    mes += 1;
    if (mes > 12) { mes = 1; anio += 1; }
  }
  return { mes: meses.join(', '), duracionDias: comoDia(fin) - comoDia(inicio) + 1 };
}
