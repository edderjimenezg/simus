import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ConsultaGuiadaComponent } from './consulta-guiada.component';
import { ConsultaGuiadaService, ContextoDeConsulta } from './consulta-guiada.service';

/**
 * La Consulta Guiada como componente, en cualquier sitio.
 *
 * LO QUE ESTAS PRUEBAS IMPIDEN: que una respuesta calculada sobre toda la operación se presente sin
 * decirlo dentro de una pantalla que habla de otra cosa, y que el componente sepa a quién le está
 * preguntando —el ámbito lo decide el servidor por la ruta base, no una condición de rol escrita
 * aquí—.
 */
describe('ConsultaGuiadaComponent', () => {
  let fixture: ComponentFixture<ConsultaGuiadaComponent>;
  let componente: ConsultaGuiadaComponent;
  let basesPedidas: string[];
  let contextoEnviado: ContextoDeConsulta | undefined;

  const RESPUESTA = {
    respuesta: 'La presencia registrada comprende 3 departamentos.',
    consultaElegida: 'cobertura_de_festivales',
    resueltoPor: 'regla' as const,
    generadoEn: '2026-09-12T12:01:00Z',
    consulta: {
      titulo: 'Cobertura territorial registrada',
      fuente: 'dbo.Festivales frente a dbo.Divipola',
      alcance: 'Solo Festivales publicados',
      columnas: ['Indicador', 'Total'],
      filas: [['Departamentos', '3']],
    },
    contexto: 'Toda la operación · esta consulta no se acota a el Festival «Petronio»',
  };

  async function montar(base: string, contexto?: ContextoDeConsulta, consultar?: () => unknown): Promise<void> {
    basesPedidas = [];
    contextoEnviado = undefined;
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ConsultaGuiadaComponent],
      providers: [{
        provide: ConsultaGuiadaService,
        useValue: {
          estado: (rutaBase: string) => {
            basesPedidas.push(rutaBase);
            return of({
              disponible: true,
              modeloLocalDisponible: false,
              modo: 'deterministico' as const,
              mensaje: 'Consultas verificadas.',
              consultas: [],
              sugerencias: ['¿Qué cobertura territorial tienen los Festivales?'],
            });
          },
          consultar: (rutaBase: string, _pregunta: string, ctx?: ContextoDeConsulta) => {
            basesPedidas.push(rutaBase);
            contextoEnviado = ctx;
            return consultar ? consultar() : of(RESPUESTA);
          },
        },
      }],
    }).compileComponents();

    fixture = TestBed.createComponent(ConsultaGuiadaComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('base', base);
    if (contexto) fixture.componentRef.setInput('contexto', contexto);
    fixture.detectChanges();
  }

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('pregunta a la ruta base que recibe, sin saber a qué espacio pertenece', async () => {
    await montar('/api/v1/externo/organizaciones/117/consulta-guiada', { organizacionId: 117 });

    componente.usarSugerencia('¿Qué necesita mi atención?');
    fixture.detectChanges();

    // EL AMBITO LO DECIDE EL SERVIDOR POR LA RUTA. El componente no tiene una sola condición de rol
    // escrita: por eso el mismo sirve a la consola del Programa y al espacio de una organización.
    expect(basesPedidas.every(base => base === '/api/v1/externo/organizaciones/117/consulta-guiada')).toBeTrue();
    expect(contextoEnviado).toEqual({ organizacionId: 117 });
  });

  it('enseña el alcance que declaró el servidor, junto a la fuente y la tabla', async () => {
    await montar('/api/v1/admin/analisis', { festivalId: 9 });

    componente.usarSugerencia('¿Qué cobertura territorial tienen los Festivales?');
    fixture.detectChanges();

    // UNA CIFRA DE TODA LA OPERACION DENTRO DE LA FICHA DE UN FESTIVAL PARECE DE ESE FESTIVAL. El
    // servidor lo declara y la pantalla lo enseña; esconderlo es peor que no responder.
    const alcance = raiz().querySelector('[data-alcance-de-la-respuesta]');
    expect(alcance?.textContent).toContain('no se acota');
    expect(raiz().textContent).toContain('Cobertura territorial registrada');
    expect(raiz().textContent).toContain('dbo.Festivales frente a dbo.Divipola');
    expect(raiz().textContent).toContain('Consulta verificada');
  });

  it('presenta el error como un turno y conserva la conversación', async () => {
    await montar('/api/v1/admin/analisis', undefined,
      () => throwError(() => new Error('Consulta temporalmente no disponible.')));

    componente.borrador.set('¿Cómo está la plataforma?');
    componente.preguntar();

    expect(componente.turnos().at(-1)?.esError).toBeTrue();
    expect(componente.turnos().at(-1)?.texto).toContain('temporalmente no disponible');
    expect(componente.borrador()).toBe('');
    // LA CONVERSACION NO SE PIERDE: el saludo, la pregunta y el error siguen ahí.
    expect(componente.turnos().length).toBe(3);
  });

  /**
   * Quien pregunta con el teclado no se queda sin sitio al llegar la respuesta.
   */
  it('devuelve el foco al campo cuando la respuesta llega', async () => {
    await montar('/api/v1/admin/analisis');

    const campo: HTMLTextAreaElement = fixture.nativeElement.querySelector('[data-pregunta]');
    campo.focus();
    componente.borrador.set('¿qué cobertura territorial tenemos?');
    componente.preguntar();
    fixture.detectChanges();
    // SE ESPERA UNA TAREA, COMO EN EL COMPONENTE. Encadenar `detectChanges` en vez de esperar haría
    // pasar la prueba con una implementación que en el navegador no devuelve el foco: es lo que
    // ocurrió con `afterNextRender`, verde aquí y roto en la pantalla.
    await new Promise(resolver => setTimeout(resolver));
    fixture.detectChanges();

    // MEDIDO EN EL NAVEGADOR ANTES DE ARREGLARLO: el foco acababa en el cuerpo del documento, y para
    // volver a preguntar había que tabular desde el principio de la página. La respuesta se anuncia
    // sola —el hilo es una región viva—; lo que faltaba era el sitio desde el que seguir.
    expect(document.activeElement).toBe(campo);
    expect(campo.disabled).toBeFalse();
  });

  /**
   * La tabla de una respuesta se anuncia por su nombre y declara sus encabezados.
   */
  it('la tabla lleva su título dentro y sus encabezados declarados', async () => {
    await montar('/api/v1/admin/analisis');
    componente.borrador.set('¿qué cobertura territorial tenemos?');
    componente.preguntar();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const tabla: HTMLTableElement = fixture.nativeElement.querySelector('table');
    // QUIEN NAVEGA CON LECTOR DE PANTALLA SALTA DE TABLA EN TABLA y las oye por su nombre. Sin
    // `caption` todas se anuncian igual, y el rótulo que sí está en pantalla queda fuera del
    // recorrido porque vive en un `h3` de fuera de la tabla.
    expect(tabla.querySelector('caption')?.textContent).toContain('Cobertura territorial registrada');
    expect(tabla.querySelectorAll('th[scope="col"]').length).toBe(2);
  });
});
