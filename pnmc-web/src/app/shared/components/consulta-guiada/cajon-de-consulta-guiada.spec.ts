import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CajonDeConsultaGuiadaComponent } from './cajon-de-consulta-guiada.component';
import { ConsultaGuiadaService } from './consulta-guiada.service';

/**
 * El cajón que pone la Consulta Guiada a un clic desde cualquier pantalla.
 *
 * LO QUE ESTAS PRUEBAS IMPIDEN: que se pida el catálogo al servidor en cada carga de la consola por
 * un panel que casi nadie abre, y que quien navega con teclado abra un cajón al que no puede llegar
 * o del que no puede salir.
 */
describe('CajonDeConsultaGuiadaComponent', () => {
  let fixture: ComponentFixture<CajonDeConsultaGuiadaComponent>;
  let vecesQueSePidioElCatalogo: number;

  beforeEach(async () => {
    vecesQueSePidioElCatalogo = 0;
    await TestBed.configureTestingModule({
      imports: [CajonDeConsultaGuiadaComponent],
      providers: [{
        provide: ConsultaGuiadaService,
        useValue: {
          estado: () => {
            vecesQueSePidioElCatalogo++;
            return of({
              disponible: true,
              modeloLocalDisponible: false,
              modo: 'deterministico' as const,
              mensaje: 'Consultas verificadas.',
              consultas: [],
              sugerencias: [],
            });
          },
          consultar: () => of({
            respuesta: '', consultaElegida: '', resueltoPor: 'regla' as const,
            generadoEn: '', consulta: null, contexto: 'Toda la operación',
          }),
        },
      }],
    }).compileComponents();

    fixture = TestBed.createComponent(CajonDeConsultaGuiadaComponent);
    fixture.componentRef.setInput('base', '/api/v1/admin/analisis');
    fixture.detectChanges();
  });

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const lanzador = (): HTMLButtonElement => raiz().querySelector('[data-abrir-consulta-guiada]')!;

  it('no consulta nada hasta que alguien lo abre', () => {
    // MONTARLO CON LA PANTALLA haría una petición en cada carga de la consola para un panel que en
    // la mayoría de las visitas nadie abre.
    expect(vecesQueSePidioElCatalogo).toBe(0);
    expect(raiz().querySelector('[data-cajon-consulta-guiada]')).toBeNull();
  });

  it('al abrirlo monta el asistente, lleva el foco dentro y lo devuelve al cerrar', async () => {
    document.body.appendChild(fixture.nativeElement);
    lanzador().focus();
    lanzador().click();
    fixture.detectChanges();
    await Promise.resolve();

    const cajon = raiz().querySelector<HTMLElement>('[data-cajon-consulta-guiada]');
    expect(cajon).not.toBeNull();
    expect(vecesQueSePidioElCatalogo).toBe(1);
    expect(cajon!.getAttribute('role')).toBe('dialog');

    // SIN ESTO EL TABULADOR SIGUE EN LA PANTALLA DE DETRAS y el cajón queda inalcanzable.
    //
    // ESTA COMPROBACION ESTUVO EN VERDE MIENTRAS LA FUNCION ESTABA ROTA, y conviene dejarlo escrito.
    // El componente llevaba el foco con un queueMicrotask, que en el banco de pruebas alcanzaba a
    // ejecutarse después del detectChanges y hacía pasar la prueba. En el navegador real el microtask
    // corre ANTES de que Angular renderice el bloque @if, así que el panel no existía todavía y el
    // foco se quedaba en el botón de origen: medido, a los 3.500 ms el
    // elemento activo seguía siendo ese botón. Ahora lo lleva appDialogo con afterNextRender, y lo
    // que se comprueba es que el foco esté DENTRO, no que esté en un nodo concreto.
    await fixture.whenStable();
    expect(cajon!.contains(document.activeElement)).toBeTrue();

    // Y EL ESCAPE SE PULSA, NO SE LLAMA. Invocar el método saltaba precisamente la parte que podía
    // fallar —que la tecla llegue al diálogo—, que es la que la directiva resuelve.
    cajon!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(raiz().querySelector('[data-cajon-consulta-guiada]')).toBeNull();
    // Y VUELVE DE DONDE SALIO: devolverlo al principio del documento obliga a recorrer la consola
    // entera para retomar el trabajo.
    expect(document.activeElement).toBe(lanzador());
    fixture.nativeElement.remove();
  });
});
