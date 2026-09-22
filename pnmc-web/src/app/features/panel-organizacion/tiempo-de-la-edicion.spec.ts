import { tiempoDeLaEdicion } from './tiempo-de-la-edicion';

/**
 * La tabla de casos del mes y la duración.
 *
 * <b>ES LA MISMA QUE LA DEL API</b>, en `TiempoDeLaEdicionTests`. La regla está escrita dos veces
 * —el cliente la necesita en vivo mientras se teclean las fechas, el servidor la sirve a quien lee
 * por el API— y lo único que impide que se separen es que las dos estén fijadas por los mismos
 * casos. Si aquí se añade uno, allí también.
 */
describe('tiempoDeLaEdicion', () => {
  const casos: [string, string | null, string | null, string | null, number | null][] = [
    ['un día dura 1, no 0',            '2026-03-01', '2026-03-01', 'Marzo',            1],
    ['cuatro días seguidos',           '2026-09-01', '2026-09-04', 'Septiembre',       4],
    ['cruza de mes y los nombra los dos', '2026-01-30', '2026-02-02', 'Enero, Febrero', 4],
    ['cruza tres meses',               '2026-01-15', '2026-03-02', 'Enero, Febrero, Marzo', 47],
    ['cruza de año',                   '2026-12-30', '2027-01-02', 'Diciembre, Enero', 4],
    ['solo fecha de inicio',           '2026-07-10', null,         'Julio',            null],
    ['solo fecha de fin',              null,         '2026-11-05', 'Noviembre',        null],
    ['sin fechas',                     null,         null,         null,               null],
    ['fin anterior al inicio no inventa nada', '2026-05-10', '2026-05-01', null,       null],
    ['una fecha mal escrita se ignora', 'ayer',      '2026-05-01', 'Mayo',             null],
  ];

  for (const [titulo, inicio, fin, mes, dias] of casos) {
    it(titulo, () => {
      const resultado = tiempoDeLaEdicion(inicio, fin);
      expect(resultado.mes).withContext('mes').toBe(mes);
      expect(resultado.duracionDias).withContext('días').toBe(dias);
    });
  }

  it('el 1 de marzo es marzo, y no el 28 de febrero', () => {
    // MUTANTE QUE MATA: leer la fecha con `new Date('2026-03-01')`. Esa forma se interpreta como
    // medianoche UTC y en Colombia —UTC-5— cae en el día anterior: el mes saldría «Febrero» y un
    // festival de un solo día duraría 0. Es el mismo desfase que ya se corrigió en la ficha del
    // Festival derivando el año por corte de cadena.
    expect(tiempoDeLaEdicion('2026-03-01', '2026-03-01')).toEqual({ mes: 'Marzo', duracionDias: 1 });
  });
});
