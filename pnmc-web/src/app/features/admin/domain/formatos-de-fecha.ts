/**
 * Los dos únicos formatos de fecha del Espacio de Gestión Administrativa.
 *
 * <b>ERAN DIEZ.</b> Medidos sobre las plantillas y los componentes de
 * `features/admin`: `d MMM y`, `d MMM y, HH:mm`, `d MMM, HH:mm`, `dd/MM/yy HH:mm`, `dd/MM/yyyy`,
 * `dd/MM/yyyy HH:mm`, `HH:mm`, `short`, y dos llamadas distintas a `toLocaleString('es-CO')`. Tres
 * de ellos convivían en pantallas contiguas del mismo panel —«13 sept 2026, 16:29» en Festivales,
 * «13/09/26, 5:16 p. m.» en Auditoría— y en la bandeja de Solicitudes la columna «Enviado» no
 * pasaba por ningún formato y enseñaba el dato crudo del servidor, `2026-09-12T21:43:17`.
 *
 * <b>POR QUE IMPORTA.</b> Quien revisa compara fechas entre pantallas —cuándo se envió, cuándo se
 * decidió, cuándo se publicó— y con cuatro formatos esa comparación deja de ser inmediata. El dato
 * crudo, además, obliga a traducir mentalmente una marca ISO con «T» y segundos.
 *
 * <b>SOLO DOS, Y LA DIFERENCIA ES SI LA HORA IMPORTA.</b> Una fecha de vencimiento o de publicación
 * no necesita hora; una actuación registrada en una bitácora sí, porque el orden dentro del día es
 * justo lo que se consulta. No hay un tercer caso.
 *
 * Se usan con el `DatePipe` de Angular, que ya está localizado en `es-CO` desde `app.config.ts`.
 */

/** «13 sept 2026». Para fechas donde la hora no aporta nada. */
export const FECHA_ADMINISTRATIVA = 'd MMM y';

/** «13 sept 2026, 16:29». Para todo lo que se ordena dentro del día. */
export const FECHA_Y_HORA_ADMINISTRATIVA = 'd MMM y, HH:mm';
