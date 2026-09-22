import { EventoDeDiseno } from '../../../core/services/adaptadores-del-diseno';

/**
 * Un evento de la Agenda, en el formato que entienden los calendarios.
 *
 * <b>POR QUE ESTE FICHERO SE REESCRIBIO ENTERO.</b> Existía desde el desarrollo anterior y recibía
 * un `any` con la forma de aquel: `{ y, m, d, t, l, desc, organizer, link, time }`, con el mes como
 * abreviatura en español —«SEP»— y la hora como texto «7:00 PM» que había que analizar con una
 * expresión regular. Nada lo importaba: era código muerto esperando a un modelo que nunca llegó.
 * Ahora existe el modelo, así que se reescribió contra él en vez de conservar la forma heredada o
 * perder la capacidad, que es útil de verdad en una agenda.
 *
 * <b>LO QUE CAMBIA CON EL MODELO REAL.</b> Las fechas ya vienen como `date` ISO, así que no hay
 * nada que analizar; la duración sale de `fechaFin` en lugar de suponerse de una hora; y un evento
 * sin hora se exporta como evento de día completo, que es lo que es.
 */

const escaparTexto = (valor: string | null | undefined): string => String(valor ?? '')
  .replace(/\\/g, '\\\\')
  .replace(/\n/g, '\\n')
  .replace(/,/g, '\\,')
  // DOS BARRAS Y NO UNA: en JavaScript '\;' es solo ';', así que el punto y coma llegaba sin
  // escapar al fichero y ICS lo lee como separador de parámetros, partiendo el campo en dos.
  .replace(/;/g, '\\;');

const soloDigitos = (valor: string): string => valor.replace(/-/g, '');

const marcaDeAhora = (): string => new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');

/**
 * El día siguiente al último, que es lo que exige `DTEND` para un evento de día completo.
 *
 * ICS TRATA `DTEND` COMO EXCLUSIVO: un evento de un solo día que declarara el mismo día como fin
 * aparecería en el calendario con duración cero y varios clientes no lo dibujan.
 */
const diaSiguiente = (fechaIso: string): string => {
  const fecha = new Date(`${fechaIso}T00:00:00Z`);
  fecha.setUTCDate(fecha.getUTCDate() + 1);
  return soloDigitos(fecha.toISOString().slice(0, 10));
};

/** Dónde ocurre, tal como lo leería una persona en su calendario. */
const lugarDe = (evento: EventoDeDiseno): string => {
  const partes = [evento.exactLocation, evento.l, 'Colombia'];
  return [...new Set(partes.filter(p => p && String(p).trim().length > 0))].join(', ');
};

/** Los meses del adaptador, para volver de «SEP» a «09». */
const MESES_ICS = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];

/**
 * De «7:00 PM» a «190000».
 *
 * El adaptador entrega la hora en doce horas porque así la enseña el diseño; el formato ICS la
 * quiere en veinticuatro y sin separadores.
 */
const horaVeinticuatro = (hora: string): string => {
  const partes = /^(\d{1,2}):(\d{2})\s*(AM|PM)$/i.exec(hora.trim());
  if (!partes) { return '000000'; }
  let horas = Number(partes[1]) % 12;
  if (partes[3].toUpperCase() === 'PM') { horas += 12; }
  return `${String(horas).padStart(2, '0')}${partes[2]}00`;
};

/**
 * El evento del diseño aprobado, en formato de calendario.
 *
 * <b>CONSERVA EL NOMBRE QUE USA LA PLANTILLA</b> —`buildAgendaEventIcs`— porque el diseño se porta
 * tal cual y su botón llama así. Lo que cambió es lo de dentro: recibe la forma del adaptador, con
 * fechas ISO, en vez del `any` heredado con el mes abreviado en español y la hora como «7:00 PM».
 */
export function buildAgendaEventIcs(evento: EventoDeDiseno): string {
  const inicio = `${evento.y}-${String(MESES_ICS.indexOf(evento.m) + 1).padStart(2, '0')}-${String(evento.d).padStart(2, '0')}`;
  const ultimo = evento.fechaFin || inicio;
  const resumen = escaparTexto(evento.t);
  const descripcion = escaparTexto([
    evento.desc,
    evento.organizer ? `Organiza: ${evento.organizer}` : '',
    evento.link ? `Más información: ${evento.link}` : '',
  ].filter(Boolean).join('\n'));

  const lineasDeTiempo = evento.time
    // CON HORA, SOLO EL INICIO LLEVA HORA. No se inventa una duración: la Agenda no guarda hora de
    // fin, y suponer «una hora» convertiría un concierto de tres en una cita corta en el calendario
    // de quien lo añade.
    ? [
        `DTSTART:${soloDigitos(inicio)}T${horaVeinticuatro(evento.time)}`,
        `DTEND;VALUE=DATE:${diaSiguiente(ultimo)}`,
      ]
    : [
        `DTSTART;VALUE=DATE:${soloDigitos(inicio)}`,
        `DTEND;VALUE=DATE:${diaSiguiente(ultimo)}`,
      ];

  return [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//PNMC//Agenda//ES',
    'BEGIN:VEVENT',
    `UID:${evento.slug || evento.id}@pnmc-web`,
    `DTSTAMP:${marcaDeAhora()}`,
    ...lineasDeTiempo,
    `SUMMARY:${resumen}`,
    `DESCRIPTION:${descripcion}`,
    `LOCATION:${escaparTexto(lugarDe(evento))}`,
    'END:VEVENT',
    'END:VCALENDAR',
  ].join('\r\n');
}
