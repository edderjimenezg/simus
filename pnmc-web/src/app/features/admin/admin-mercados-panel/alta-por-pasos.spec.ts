import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AdminMercadosPanelComponent } from './admin-mercados-panel.component';

/**
 * Registrar un mercado se pregunta por pasos, como registrar un Festival.
 *
 * <b>EL DEFECTO QUE VIENEN A IMPEDIR.</b> Hasta el alta de un mercado
 * era un solo desplazamiento con trece campos y la de un Festival un asistente de cinco pasos: dos
 * formas de dar de alta dos procesos que el proyecto trata igual. Y el orden de los pasos es el que
 * `lenguaje-visual-de-la-consola.md` §5 bis declara —qué es, cada cuánto, dónde, cómo se contacta,
 * con qué se relaciona—, no el que fuera cómodo al escribir la pantalla.
 *
 * <b>EDITAR NO HEREDA EL ASISTENTE:</b> quien corrige un teléfono ya sabe cuál es el campo.
 */
describe('el alta de un mercado, por pasos', () => {
  let fixture: ComponentFixture<AdminMercadosPanelComponent>;
  let componente: AdminMercadosPanelComponent;
  let http: HttpTestingController;

  const PAGINA_VACIA = { items: [], total: 0, limit: 20, offset: 0 };

  const raiz = () => fixture.nativeElement as HTMLElement;
  const texto = () => raiz().textContent ?? '';

  function arrancar(): void {
    fixture.detectChanges();
    http.match(r => r.url.includes('/institucional/mercados') && !r.url.includes('festivales-elegibles'))
      .forEach(p => p.flush(PAGINA_VACIA));
    http.match(() => true).forEach(p => p.flush({ items: [] }));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminMercadosPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(AdminMercadosPanelComponent);
    componente = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    arrancar();
  });

  afterEach(() => http.match(() => true).forEach(p => p.flush({ items: [] })));

  it('el alta abre en el primer paso y pregunta primero por quién responde', () => {
    componente.nuevo();
    fixture.detectChanges();

    expect(componente.pasoDelAlta()).toBe(1);
    expect(texto()).toContain('Registrar mercado musical');
    expect(texto()).toContain('Organización responsable');
    // MUTANTE QUE MATA: enseñar los trece campos a la vez, que es lo que hacía. El nombre se
    // pregunta en el paso siguiente, no junto a la organización.
    expect(texto()).not.toContain('Nombre del mercado');
  });

  it('los cinco pasos siguen el orden declarado: qué es, cuándo y dónde, contacto y vínculos', () => {
    componente.nuevo();
    fixture.detectChanges();

    componente.pasoDelAlta.set(2);
    fixture.detectChanges();
    expect(texto()).toContain('Nombre del mercado');
    expect(texto()).toContain('Alcance');
    expect(texto()).not.toContain('Correo del mercado');

    componente.pasoDelAlta.set(3);
    fixture.detectChanges();
    expect(texto()).toContain('Periodicidad');
    expect(texto()).toContain('Nivel de cobertura');

    componente.pasoDelAlta.set(4);
    fixture.detectChanges();
    expect(texto()).toContain('Correo del mercado');
    expect(texto()).toContain('Prácticas musicales');
    expect(texto()).toContain('Este mercado se realiza en el marco de un festival');
  });

  it('la revisión dice qué se va a crear, con qué procedencia y en qué estado', () => {
    componente.nuevo();
    componente.campo('nombre', 'Mercado de la Sabana');
    componente.pasoDelAlta.set(5);
    fixture.detectChanges();

    expect(texto()).toContain('Mercado de la Sabana');
    // LA PROCEDENCIA ES DEL PROGRAMA Y LA RESPONSABLE ES OTRA COSA: son las tres dimensiones que
    // el proyecto nunca confunde.
    expect(texto()).toContain('Plan Nacional de Música para la Convivencia');
    expect(texto()).toContain('Se creará como borrador');
  });

  it('lo que falta se nombra campo a campo y apaga el botón de crear', () => {
    componente.nuevo();
    componente.pasoDelAlta.set(5);
    fixture.detectChanges();

    expect(componente.loQueFaltaParaRegistrar()).toEqual(['el nombre', 'la organización responsable', 'el departamento', 'el municipio']);
    expect(texto()).toContain('Falta el nombre, la organización responsable');

    const crear = raiz().querySelector<HTMLButtonElement>('[data-testid="registrar-mercado"]')!;
    expect(crear.disabled).toBeTrue();
  });

  it('un mercado dentro de un festival no se crea sin decir cuál', () => {
    componente.nuevo();
    componente.campo('nombre', 'Mercado con festival');
    componente.campo('organizacionId', 7);
    componente.campo('nivelCobertura', 'nacional');
    componente.cambiarSiEstaEnUnFestival(true);
    fixture.detectChanges();

    expect(componente.loQueFaltaParaRegistrar()).toEqual(['el festival en cuyo marco ocurre']);
  });

  it('editar no abre el asistente: los campos se ven todos, para ir al que se corrige', () => {
    componente.editar({
      id: 4, nombre: 'Mercado publicado', descripcion: '', estadoRegistro: 'publicado',
      nivelCobertura: 'nacional', organizacionId: 7,
      practicasMusicales: [], territoriosSonoros: [],
    } as never);
    fixture.detectChanges();

    expect(raiz().querySelector('app-asistente-de-alta')).toBeNull();
    expect(texto()).toContain('Editar mercado musical');
    // Los mismos campos, todos a la vez: es lo que distingue corregir de registrar.
    expect(texto()).toContain('Nombre del mercado');
    expect(texto()).toContain('Correo del mercado');
    expect(texto()).toContain('Nivel de cobertura');
  });
});
