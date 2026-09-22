import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';

import { AdminAltaFestivalComponent, PASOS_DEL_FESTIVAL } from './admin-alta-festival.component';
import { AltaAdministrativaService, CoincidenciaDeAlta, FestivalNuevo, ResultadoDeAlta } from '../../../core/services/alta-administrativa.service';
import { AdminService } from '../../../core/services/admin.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { PASOS_DE_UN_ALTA } from '../../../core/vocabularios/pasos-de-un-alta';

/**
 * Registrar un Festival desde la consola.
 *
 * <b>POR QUE ESTAS PRUEBAS EXISTEN AHORA.</b> Este asistente no tenía ninguna hasta el 17 de
 * septiembre de 2026, y en el corte v171 se le cambió el armazón —pasó a `app-asistente-de-alta`— y
 * <b>se le reordenaron los cinco pasos</b> para que siguieran el orden declarado en §5 bis del
 * lenguaje visual. Reordenar los pasos de un alta sin una sola prueba es exactamente donde se cuela
 * un campo que se queda fuera de su paso y nadie lo nota hasta que alguien intenta registrar.
 *
 * <b>LO QUE FIJAN:</b> que los pasos son los del vocabulario común; qué campo vive en cuál; que la
 * procedencia y la organización responsable son dos cosas distintas; que lo que falta se nombra; y
 * que el formulario queda limpio después de registrar.
 */
describe('el alta de un Festival desde la consola', () => {
  let fixture: ComponentFixture<AdminAltaFestivalComponent>;
  let componente: AdminAltaFestivalComponent;
  let enviado: FestivalNuevo | null;
  let respuesta: ResultadoDeAlta;
  let coincidencias: CoincidenciaDeAlta[];

  const raiz = () => fixture.nativeElement as HTMLElement;
  const texto = () => raiz().textContent ?? '';

  beforeEach(async () => {
    enviado = null;
    respuesta = { ok: true, id: '77', nombre: 'Festival de la Prueba' };
    coincidencias = [];

    const alta: Partial<AltaAdministrativaService> = {
      crearFestival: async (datos: FestivalNuevo) => { enviado = datos; return respuesta; },
      coincidenciasDeFestival: async () => coincidencias,
    };
    // EL CATALOGO Y LAS ORGANIZACIONES SE PIDEN EN EL CONSTRUCTOR: sin dobles, el componente ni
    // siquiera se crea.
    const catalogo: Partial<CatalogService> = { fetchDivipolaConCodigos: () => of([]) };
    const admin: Partial<AdminService> = {
      cargarOrganizaciones: (() => of({ items: [{ id: '3104', nombre: 'Fundación de Prueba' }], total: 1 })) as AdminService['cargarOrganizaciones'],
    };

    await TestBed.configureTestingModule({
      imports: [AdminAltaFestivalComponent],
      providers: [
        { provide: AltaAdministrativaService, useValue: alta },
        { provide: CatalogService, useValue: catalogo },
        { provide: AdminService, useValue: admin },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAltaFestivalComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('usa los cinco pasos del vocabulario común, no una lista propia', () => {
    // MUTANTE QUE MATA: volver a declarar los pasos aquí. Dos listas para lo mismo es como
    // empezaron a divergir las tres de periodicidad.
    expect(PASOS_DEL_FESTIVAL).toBe(PASOS_DE_UN_ALTA);
    expect(PASOS_DEL_FESTIVAL.map(p => p.titulo))
      .toEqual(['Responsable', 'Qué es', 'Cuándo y dónde', 'Contacto y vínculos', 'Revisión']);
  });

  it('cada campo está en su paso, y en el orden declarado', () => {
    expect(componente.paso()).toBe(1);
    expect(texto()).toContain('Organización responsable');
    expect(texto()).not.toContain('Nombre del Festival');

    componente.paso.set(2);
    fixture.detectChanges();
    expect(texto()).toContain('Nombre del Festival');
    expect(texto()).toContain('Descripción');
    // MUTANTE QUE MATA: devolver el contacto al paso 2, que es donde estaba antes del corte v171.
    expect(texto()).not.toContain('Correo de contacto');

    componente.paso.set(3);
    fixture.detectChanges();
    expect(texto()).toContain('Periodicidad');
    expect(texto()).toContain('Nivel de cobertura');

    componente.paso.set(4);
    fixture.detectChanges();
    expect(texto()).toContain('Correo de contacto');
    expect(texto()).toContain('Instagram');
  });

  it('la revisión dice quién lo incorpora y quién responde por él, que no son lo mismo', () => {
    componente.campo('nombre', 'Festival de la Prueba');
    componente.campo('organizacionResponsableId', 3104);
    componente.paso.set(5);
    fixture.detectChanges();

    expect(texto()).toContain('Festival de la Prueba');
    expect(texto()).toContain('Plan Nacional de Música para la Convivencia');
    expect(texto()).toContain('Fundación de Prueba');
    expect(texto()).toContain('Se creará como borrador');
  });

  it('sin organización responsable se puede registrar igual: un Festival histórico no tiene quién responda todavía', () => {
    componente.campo('nombre', 'Festival histórico');
    componente.campo('nivelCobertura', 'nacional');
    componente.paso.set(5);
    fixture.detectChanges();

    expect(componente.loQueFalta()).toEqual([]);
    expect(texto()).toContain('Todavía no se sabe');
  });

  it('lo que falta se nombra campo a campo', () => {
    expect(componente.loQueFalta()).toEqual(['el nombre', 'el departamento', 'el municipio']);

    componente.campo('nombre', 'Festival de la Prueba');
    componente.cambiarDepartamento('41');
    expect(componente.loQueFalta()).toEqual(['el municipio']);

    componente.campo('codigoMunicipio', '41001');
    expect(componente.loQueFalta()).toEqual([]);
  });

  it('una periodicidad que no se explica sola exige su explicación', () => {
    componente.campo('nombre', 'Festival de la Prueba');
    componente.campo('nivelCobertura', 'nacional');
    componente.campo('periodicidad', 'otra_regular');

    expect(componente.loQueFalta()).toEqual(['la explicación de la periodicidad']);

    componente.campo('periodicidadDetalle', 'Cada vez que hay presupuesto.');
    expect(componente.loQueFalta()).toEqual([]);
  });

  it('cambiar a cobertura nacional borra el territorio que ese nivel no admite', () => {
    componente.cambiarDepartamento('41');
    componente.campo('codigoMunicipio', '41001');

    componente.cambiarNivel('nacional');

    // EL SERVIDOR LO LIMPIARIA IGUAL, pero dejarlo en pantalla hace creer que se guardó.
    expect(componente.formulario().codigoDepartamento).toBe('');
    expect(componente.formulario().codigoMunicipio).toBe('');
  });

  it('registrar manda lo escrito, avisa a quien lo abrió y deja el formulario limpio', async () => {
    componente.campo('nombre', '  Festival de la Prueba  ');
    componente.campo('nivelCobertura', 'nacional');
    componente.campo('organizacionResponsableId', 3104);
    componente.paso.set(5);

    await componente.registrar();

    // EL TEXTO VIAJA SIN ESPACIOS DE LOS LADOS: es lo que va a quedar escrito en el registro.
    expect(enviado!.festival.nombre).toBe('Festival de la Prueba');
    expect(enviado!.organizacionResponsableId).toBe(3104);
    // Y LO VACIO VIAJA COMO NULO, no como cadena vacía: son dos cosas distintas en la base.
    expect(enviado!.festival.descripcion).toBeNull();

    expect(componente.formulario().nombre).toBe('');
    expect(componente.paso()).toBe(1);
  });

  it('si el servidor devuelve errores por campo, se quedan donde se escribieron', async () => {
    respuesta = { ok: false, errores: { nombre: ['Ya hay un Festival con ese nombre.'] }, error: 'Revisa los campos marcados.' };
    componente.campo('nombre', 'Repetido');
    componente.campo('nivelCobertura', 'nacional');

    await componente.registrar();

    expect(componente.erroresDe('nombre')).toEqual(['Ya hay un Festival con ese nombre.']);
    expect(componente.error()).toBe('Revisa los campos marcados.');
    // NO SE LIMPIA EL FORMULARIO: lo escrito sigue ahí para corregirlo.
    expect(componente.formulario().nombre).toBe('Repetido');
  });

  it('avisa de los que ya se llaman parecido, sin bloquear, y no pregunta por menos de tres letras', fakeAsync(() => {
    coincidencias = [{ id: '9', nombre: 'Festival de la Prueba', territorio: 'Huila', estado: 'publicado', organizacionResponsable: 'Otra Fundación' }];

    componente.campo('nombre', 'Fe');
    tick(600);
    expect(componente.coincidencias()).toEqual([]);

    componente.campo('nombre', 'Festival de la Prueba');
    tick(600);

    expect(componente.coincidencias().length).toBe(1);
    // AVISA, NO BLOQUEA: registrar sigue siendo posible.
    expect(componente.loQueFalta()).not.toContain('el nombre');
  }));
});
