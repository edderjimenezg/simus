import { TestBed, ComponentFixture } from '@angular/core/testing';
import { appConfig } from '../../../../app.config';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HistorialDeRegistroComponent } from './historial-de-registro.component';
import { ProcedenciaDeRegistro } from '../../../../core/services/procedencia';

/**
 * El panel que responde «quién tocó esto y de dónde vino», compartido por las cinco pantallas.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que el panel afirme cosas que nadie le ha dicho. Tenía dos
 * formas de hacerlo y las dos se veían en pantalla:
 *
 * <ul>
 *   <li>Una lectura fallida devolvía lista vacía, y el panel decía «no hay actuaciones
 *       registradas» —una afirmación falsa sobre el registro, indistinguible de la verdadera—.</li>
 *   <li>Si quien abría el panel no le pasaba la procedencia, decía «este registro es anterior al
 *       seguimiento de procedencia». El mismo Festival decía una cosa abierto desde su ficha y otra
 *       desde la tabla, porque la tabla no tiene ese dato a mano.</li>
 * </ul>
 */
describe('el panel de historial no afirma lo que no sabe', () => {
  // LAS FECHAS SE ESCRIBEN EN es-CO, y el locale lo registra `app.config.ts` al cargarse. Nombrar
  // `appConfig` obliga a cargar ese módulo; sin esto la prueba solo pasa cuando corre junto a otra
  // que lo cargue, que es una dependencia del orden de ejecución y no de lo que se prueba.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  let fixture: ComponentFixture<HistorialDeRegistroComponent>;
  let http: HttpTestingController;

  const PROCEDENCIA: ProcedenciaDeRegistro = {
    contextoOrigen: 'administrativo',
    contextoEtiqueta: 'Gestión administrativa',
    esInstitucional: true,
    organizacionProcedenciaId: 1,
    organizacionProcedenciaNombre: 'Plan Nacional de Música para la Convivencia',
    usuarioCreadorId: 9,
    usuarioCreadorNombre: 'Webmaster PNMC',
    fechaRegistro: '2026-09-01T10:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HistorialDeRegistroComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(HistorialDeRegistroComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('titulo', 'Festival de prueba');
    fixture.componentRef.setInput('registro', { tabla: 'Festivales', id: '105' });
  });

  afterEach(() => http.verify());

  function responder(cuerpo: { items: unknown[] } | null, fallar = false): void {
    const peticion = http.expectOne(p => p.url.includes('/admin/auditoria'));
    if (fallar) peticion.error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });
    else peticion.flush(cuerpo as object);
  }

  it('sin procedencia declarada, no dice nada sobre de dónde vino', async () => {
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).not.toContain('anterior al seguimiento de procedencia');
    expect(texto).not.toContain('De dónde vino');
    // Y el rótulo del panel tampoco promete una procedencia que no va a enseñar.
    expect(texto).toContain('Historial');
    expect(texto).not.toContain('Historial y procedencia');
  });

  it('con procedencia nula —consultada y ausente— sí lo dice, porque eso sí es un hecho', async () => {
    fixture.componentRef.setInput('procedencia', null);
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('anterior al seguimiento de procedencia');
  });

  it('con procedencia, la enseña entera', async () => {
    fixture.componentRef.setInput('procedencia', PROCEDENCIA);
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).toContain('Plan Nacional de Música para la Convivencia');
    expect(texto).toContain('Webmaster PNMC');
    expect(texto).toContain('Historial y procedencia');
  });

  it('si la consulta falla, lo dice y lo anuncia: no se hace pasar por un historial vacío', async () => {
    fixture.detectChanges();
    responder(null, true);
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).toContain('No fue posible consultar el historial');
    // MUTANTE QUE MATA: devolver `[]` en el `catch` y dejar que caiga en el caso vacío. Quien
    // administra leería «no hay actuaciones registradas» sobre un registro que sí las tiene.
    expect(texto).not.toContain('No hay actuaciones registradas');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });

  it('cuando hay líneas, enseña la etiqueta de la acción y no el verbo crudo de la bitácora', async () => {
    fixture.detectChanges();
    responder({ items: [{ id: '1', accion: 'actualizar', accionEtiqueta: 'Actualizó', tabla: 'Festivales', registroId: '105', nombreRegistro: null, autor: { id: '9', nombre: 'Webmaster PNMC', correo: 'w@p.co' }, fecha: '2026-09-12T00:55:00Z' }] });
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).toContain('Actualizó');
    expect(texto).not.toContain('actualizar');
  });

  // ───────────────── Qué se ha publicado: las versiones del perfil público ─────────────────
  //
  // TRES ESTADOS, COMO LA PROCEDENCIA Y POR EL MISMO MOTIVO. La pregunta «¿esto se ha publicado
  // alguna vez, y qué mostraba?» es de trazabilidad y la pidió la dirección de producto el 13 de
  // septiembre de 2026 para la gestión administrativa. Confundir «no se preguntó» con «no hay
  // ninguna» diría algo falso sobre el registro, que es lo que el resto de este fichero vigila.

  it('sin versiones declaradas, no dice nada sobre lo publicado', async () => {
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent as string).not.toContain('Qué se ha publicado');
  });

  it('si la consulta de versiones falla, lo dice en vez de hacerse pasar por «ninguna»', async () => {
    // MUTANTE QUE MATA: tratar `null` como lista vacía. La pantalla diría «todavía no se ha
    // publicado ninguna versión» sobre un Festival que puede llevar tres años publicado.
    fixture.componentRef.setInput('versiones', null);
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).toContain('No fue posible consultar las versiones publicadas');
    expect(texto).not.toContain('Todavía no se ha publicado ninguna versión');
  });

  it('con versiones, las enseña y marca cuál es la que se ve hoy', async () => {
    fixture.componentRef.setInput('versiones', [
      { id: 2, etiqueta: 'Versión 2 · Publicado', detalle: '2026-09-01 a 2026-09-05', vigente: true },
      { id: 1, etiqueta: 'Versión 1 · Archivado', detalle: null, vigente: false },
    ]);
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const lista = fixture.nativeElement.querySelector('[data-testid="historial-versiones"]') as HTMLElement;
    expect(lista).not.toBeNull();
    expect(lista.textContent).toContain('Versión 2 · Publicado');
    expect(lista.textContent).toContain('2026-09-01 a 2026-09-05');
    // CUAL ES LA VIGENTE ES LA MITAD QUE IMPORTA: dos versiones publicadas en la lista y solo una
    // es la que el portal sirve.
    expect(lista.textContent).toContain('es la que se ve hoy');
    expect((lista.textContent!.match(/es la que se ve hoy/g) || []).length).toBe(1);
  });

  it('sin ninguna versión publicada lo dice, y no es lo mismo que un fallo', async () => {
    fixture.componentRef.setInput('versiones', []);
    fixture.detectChanges();
    responder({ items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;
    expect(texto).toContain('Todavía no se ha publicado ninguna versión');
    expect(texto).not.toContain('No fue posible consultar');
  });

});
