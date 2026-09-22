import { ComponentFixture, TestBed } from '@angular/core/testing';
import { appConfig } from '../../../app.config';
import { of, throwError } from 'rxjs';
import { PanelAnalisisAdministrativoComponent } from './panel-analisis-administrativo.component';
import { AnalisisAdministrativoService } from './analisis-administrativo.service';
import { ConsultaGuiadaService } from '../../../shared/components/consulta-guiada/consulta-guiada.service';

/**
 * Esta pantalla ya solo responde por el tablero.
 *
 * EL ASISTENTE SE PROBABA AQUI HASTA EL 12 DE SEPTIEMBRE DE 2026, cuando dejó de ser una parte de
 * esta pantalla para ser una capacidad transversal. Sus pruebas se fueron con él a
 * `shared/components/consulta-guiada`; dejarlas duplicadas habría comprobado dos veces lo mismo y,
 * a la primera divergencia, habría dado por bueno el comportamiento de la copia muerta.
 */
describe('PanelAnalisisAdministrativoComponent', () => {
  // LAS CIFRAS SE ESCRIBEN EN es-CO, y el locale lo registra `app.config.ts` al cargarse. Nombrar
  // `appConfig` obliga a cargar ese módulo; sin esto la prueba solo pasa cuando corre junto a
  // otra que lo cargue, que es una dependencia del orden de ejecución y no de lo que se prueba.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  let fixture: ComponentFixture<PanelAnalisisAdministrativoComponent>;
  let component: PanelAnalisisAdministrativoComponent;

  const tablero = {
    generadoEn: '2026-09-10T12:00:00Z',
    indicadores: [{ id: 'festivales', rotulo: 'Festivales registrados', total: 12, alcance: 'Todos los estados' }],
    estadosFestival: [['Publicado', '7']],
    estadosMercado: [['Borrador', '2']],
    departamentosPrincipales: [['Nariño', '3']],
  };

  /** El asistente incrustado pide su catálogo; aquí no es el sujeto de la prueba. */
  const consultaGuiadaFalsa = {
    estado: () => of({
      disponible: true,
      modeloLocalDisponible: false,
      modo: 'deterministico' as const,
      mensaje: 'Consultas verificadas.',
      consultas: [],
      sugerencias: [],
    }),
    consultar: () => of({
      respuesta: '', consultaElegida: '', resueltoPor: 'regla' as const,
      generadoEn: '', consulta: null, contexto: 'Toda la operación',
    }),
  };

  async function montar(tableroFalso: Partial<AnalisisAdministrativoService>): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [PanelAnalisisAdministrativoComponent],
      providers: [
        { provide: AnalisisAdministrativoService, useValue: tableroFalso },
        { provide: ConsultaGuiadaService, useValue: consultaGuiadaFalsa },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(PanelAnalisisAdministrativoComponent);
    component = fixture.componentInstance;
  }

  it('carga el tablero con sus indicadores', async () => {
    await montar({ tablero: () => of(tablero) } as Partial<AnalisisAdministrativoService>);
    fixture.detectChanges();

    expect(component.tablero()?.indicadores[0].total).toBe(12);
    expect(fixture.nativeElement.textContent).toContain('Festivales registrados');
    // MERCADOS SE VE EN EL TABLERO COMO FESTIVALES: un módulo del Ecosistema se conecta en las
    // visualizaciones de datos de principio a fin (15 de septiembre de 2026).
    expect(fixture.nativeElement.querySelector('[data-reparto-de-mercados]').textContent).toContain('Borrador');
    // Y los indicadores van en la franja compartida, no en tarjetas.
    expect(fixture.nativeElement.querySelector('[data-franja-de-cifras] [data-cifra="festivales"]').textContent).toContain('12');
    expect(fixture.nativeElement.textContent).toContain('Nariño');
  });

  it('si el tablero falla lo dice y ofrece reintentar, sin tumbar la pantalla', async () => {
    await montar({
      tablero: () => throwError(() => new Error('Tablero temporalmente no disponible.')),
    } as Partial<AnalisisAdministrativoService>);
    fixture.detectChanges();

    // LA CONSULTA GUIADA SIGUE EN PIE AUNQUE EL TABLERO SE CAIGA: son dos lecturas distintas, y
    // perder las dos por una es convertir un fallo en un apagón.
    expect(component.errorTablero()).toContain('temporalmente no disponible');
    expect(fixture.nativeElement.textContent).toContain('Reintentar');
    expect(fixture.nativeElement.querySelector('app-consulta-guiada')).not.toBeNull();
  });
});
