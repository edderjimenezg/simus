import { buildAgendaEventIcs } from './agenda-ics';
import { EventoDeDiseno } from '../../../core/services/adaptadores-del-diseno';

/**
 * El evento en el calendario de quien lo añade.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que un evento de un solo día llegue al calendario con
 * duración cero. `DTEND` es EXCLUSIVO en el formato ICS: declarar el mismo día como fin produce un
 * evento que varios clientes no dibujan. Por eso el fin es siempre el día siguiente al último.
 */
describe('construirIcsDeEvento', () => {
  function evento(parcial: Partial<EventoDeDiseno> = {}): EventoDeDiseno {
    return {
      id: '1', slug: 'semana-de-la-musica', t: 'Semana de la Música', desc: 'Encuentro abierto.',
      d: '11', m: 'SEP', y: '2026', fechaFin: '2026-09-11', time: '',
      l: 'PAIPA, BOYACÁ', cat: 'Festivales', img: '', organizer: '',
      exactLocation: 'Teatro Municipal', link: '', dateObj: new Date(2026, 8, 11, 12),
      departmentCode: '15', municipalityCode: '15516',
      department: 'BOYACÁ', municipality: 'PAIPA', situacion: 'proximo',
      ...parcial,
    };
  }

  it('un evento de un solo día termina al día siguiente, porque DTEND es exclusivo', () => {
    const ics = buildAgendaEventIcs(evento({}));

    expect(ics).toContain('DTSTART;VALUE=DATE:20260911');
    expect(ics).toContain('DTEND;VALUE=DATE:20260912');
  });

  it('un evento de varios días llega entero', () => {
    const ics = buildAgendaEventIcs(evento({ fechaFin: '2026-09-16' }));

    expect(ics).toContain('DTSTART;VALUE=DATE:20260911');
    expect(ics).toContain('DTEND;VALUE=DATE:20260917');
  });

  it('con hora, el inicio la lleva y no se inventa una duración', () => {
    const ics = buildAgendaEventIcs(evento({ time: '7:30 PM' }));

    expect(ics).toContain('DTSTART:20260911T193000');
    // NO SE SUPONE «UNA HORA»: la Agenda no guarda hora de fin, y suponerla convertiría un
    // concierto de tres horas en una cita corta en el calendario de quien lo añade.
    expect(ics).toContain('DTEND;VALUE=DATE:20260912');
  });

  it('el lugar reúne sede, municipio y departamento, y siempre acaba en Colombia', () => {
    const ics = buildAgendaEventIcs(evento({}));

    expect(ics).toContain('LOCATION:Teatro Municipal\\, PAIPA\\, BOYACÁ\\, Colombia');
  });

  it('un evento virtual sin sede no deja separadores sueltos en el lugar', () => {
    const ics = buildAgendaEventIcs(evento({ exactLocation: '', l: '' }));

    expect(ics).toContain('LOCATION:Colombia');
  });

  it('escapa las comas y los puntos y coma del texto', () => {
    // EL PUNTO Y COMA LLEGABA SIN ESCAPAR: el código escribía '\;', que en JavaScript es solo
    // ';', y ICS lo lee como separador de parámetros, partiendo el campo en dos. Un título con
    // «Bandas; edición 2026» rompía la línea SUMMARY en el calendario de quien lo añadía.
    const ics = buildAgendaEventIcs(evento({ t: 'Bandas; edición 2026, Paipa' }));

    expect(ics).toContain('SUMMARY:Bandas\\; edición 2026\\, Paipa');
  });

  it('la identidad del evento es su dirección, que no cambia', () => {
    const ics = buildAgendaEventIcs(evento({}));

    // ASI EL CALENDARIO ACTUALIZA en vez de duplicar cuando alguien lo vuelve a añadir.
    expect(ics).toContain('UID:semana-de-la-musica@pnmc-web');
  });
});
