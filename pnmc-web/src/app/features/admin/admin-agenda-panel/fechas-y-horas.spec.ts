import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminAgendaPanelComponent } from './admin-agenda-panel.component';

/**
 * Las fechas de un evento tienen que tener sentido, y se dice mientras se escriben.
 *
 * <b>POR QUE SE COMPRUEBA AQUI ADEMAS DE EN EL SERVIDOR.</b> El servidor las valida y la base las
 * respalda con sus `CHECK`; esta capa no las sustituye. Lo que añade es el momento: enterarse al
 * pulsar «guardar» obliga a volver arriba a buscar cuál de las dos fechas estaba mal, y el error
 * llega lejos del campo que lo causa.
 */
describe('las fechas y horas de un evento de agenda', () => {
  function crear() {
    TestBed.configureTestingModule({
      imports: [AdminAgendaPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const componente = TestBed.createComponent(AdminAgendaPanelComponent).componentInstance;
    componente.nuevo();
    return componente;
  }

  it('un evento nuevo no tiene ningún problema de fecha', () => {
    expect(crear().problemasDeFecha()).toEqual([]);
  });

  it('avisa cuando el evento termina antes de empezar', () => {
    const componente = crear();

    componente.campo('fechaInicio', '2026-11-10');
    componente.campo('fechaFin', '2026-11-03');

    expect(componente.problemasDeFecha()).toContain('El evento no puede terminar antes de empezar.');
  });

  it('un evento de varios días bien puesto no avisa', () => {
    const componente = crear();

    componente.campo('fechaInicio', '2026-11-03');
    componente.campo('fechaFin', '2026-11-10');

    expect(componente.problemasDeFecha()).toEqual([]);
  });

  it('las horas no existen mientras nadie las pida', () => {
    const componente = crear();

    // NI SIQUIERA SI QUEDO UN VALOR ESCRITO: la casilla es la que decide.
    expect(componente.formulario()?.conHoraInicio).toBe(false);
    expect(componente.formulario()?.conHoraFin).toBe(false);
  });

  it('avisa si se marca la casilla y no se escribe la hora', () => {
    const componente = crear();

    componente.campo('conHoraInicio', true);

    // EL AVISO ACOMPAÑA AL ROTULO: la casilla ya no pregunta «¿tiene hora?», ofrece «agregar
    // hora», y el mensaje tiene que hablar el mismo idioma que el control que lo provocó.
    expect(componente.problemasDeFecha().join(' ')).toContain('escríbela o quita la casilla');
  });

  it('en un evento de un día avisa si la hora de fin es anterior', () => {
    const componente = crear();

    componente.campo('fechaInicio', '2026-11-03');
    componente.campo('conHoraInicio', true);
    componente.campo('horaInicio', '19:00');
    componente.campo('conHoraFin', true);
    componente.campo('horaFin', '17:00');

    expect(componente.problemasDeFecha()).toContain('La hora de fin no puede ser anterior a la de inicio.');
  });

  it('en un evento de varios días esa misma hora no es un problema', () => {
    const componente = crear();

    // ACABAR A LAS 9 DE LA MAÑANA DEL ULTIMO DIA es lo normal en un festival que empieza a las 19
    // del primero. Comparar las horas ahí sería inventarse un error.
    componente.campo('fechaInicio', '2026-11-03');
    componente.campo('fechaFin', '2026-11-07');
    componente.campo('conHoraInicio', true);
    componente.campo('horaInicio', '19:00');
    componente.campo('conHoraFin', true);
    componente.campo('horaFin', '09:00');

    expect(componente.problemasDeFecha()).toEqual([]);
  });

  it('avisa si falta la fecha de inicio', () => {
    const componente = crear();

    componente.campo('fechaInicio', '');

    expect(componente.problemasDeFecha()).toContain('Falta la fecha de inicio.');
  });

  it('quitar la hora de inicio se lleva también la de fin', () => {
    const componente = crear();
    componente.cambiarHoraDeInicio(true);
    componente.campo('horaInicio', '18:00');
    componente.campo('conHoraFin', true);
    componente.campo('horaFin', '21:00');

    componente.cambiarHoraDeInicio(false);

    // UN EVENTO QUE ACABA A UNA HORA Y NO EMPIEZA A NINGUNA es un dato que nadie puede leer, y el
    // formulario no debería poder construirlo. Se retira cuando deja de tener sentido, no al
    // guardar.
    expect(componente.formulario()?.conHoraFin).toBe(false);
    expect(componente.formulario()?.horaFin).toBe('');
  });

  it('avisa si llega una hora de fin sin hora de inicio', () => {
    const componente = crear();
    // LA PANTALLA YA NO DEJA CONSTRUIRLO —el control de la hora de fin no existe hasta que hay
    // hora de inicio—, pero un borrador recuperado de otra sesión sí puede traer la combinación.
    componente.campo('conHoraFin', true);
    componente.campo('horaFin', '21:00');

    expect(componente.problemasDeFecha()).toContain('No puede haber hora de fin sin hora de inicio.');
  });
});
