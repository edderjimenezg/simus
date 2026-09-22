import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AsistenteDeMercadoComponent } from './asistente-de-mercado.component';
import { PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

/**
 * El alta de un mercado desde su organización.
 *
 * <b>LO QUE FIJAN.</b> Que la pregunta del festival produzca una RELACION y no un nombre suelto:
 * que responder que no borre el festival elegido, que decir que sí sin elegir no se guarde, y que
 * cuando la organización no tiene ningún festival la pantalla lo diga en vez de dejar un selector
 * vacío. Es la regla que la estructura heredada no tenía —guardaba el festival como clave ajena y
 * como texto, sin nada que obligara a que coincidieran— y la que el criterio pide
 * explícitamente.
 */
describe('el asistente de alta de un mercado', () => {
  let fixture: ComponentFixture<AsistenteDeMercadoComponent>;
  let componente: AsistenteDeMercadoComponent;

  const FESTIVALES = [
    { id: 7, nombre: 'Festival de Prueba', estadoRegistro: 'borrador' },
    { id: 9, nombre: 'Festival Publicado', estadoRegistro: 'publicado' },
  ];

  function montar(festivales: unknown = FESTIVALES): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AsistenteDeMercadoComponent],
      providers: [
        provideRouter([]),
        { provide: PanelOrganizacionStore, useValue: {
          organizacionId: () => '42',
          organizacionElegida: () => ({ id: '42', name: 'Fundación de Prueba' }),
        } },
        { provide: PanelOrganizacionApi, useValue: {
          obtenerCatalogosDeMercado: () => of({
            alcances: [{ id: 1, nombre: 'Local', slug: 'local' }],
            modalidades: [{ id: 1, nombre: 'Presencial', slug: 'presencial' }],
          }),
          obtenerUbicaciones: () => of([
            { departmentCode: '05', departmentName: 'ANTIOQUIA', municipalityCode: '05001', municipalityName: 'MEDELLIN' },
          ]),
          obtenerFestivalesElegibles: () => (festivales === 'falla' ? throwError(() => new Error('sin red')) : of(festivales)),
          crearMercado: () => of({ id: 1 }),
        } },
      ],
    });
    fixture = TestBed.createComponent(AsistenteDeMercadoComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('los festivales que ofrece son los de la organización, en cualquier estado', () => {
    // EL ESTADO NO EXCLUYE: la condición es existir y pertenecer. Un festival en borrador se puede
    // elegir igual que uno publicado.
    montar();

    expect(componente.festivales().map(f => f.id)).toEqual([7, 9]);
  });

  it('responder que no borra el festival que se hubiera elegido', () => {
    montar();
    componente.cambiarSiEstaEnUnFestival(true);
    componente.formulario.festivalId = 7;

    componente.cambiarSiEstaEnUnFestival(false);

    expect(componente.formulario.festivalId).toBeNull();
  });

  it('decir que sí y no elegir no pasa la validación', () => {
    montar();
    componente.formulario.nombre = 'Mercado con festival sin elegir';
    componente.formulario.nivelCobertura = 'nacional';
    componente.cambiarSiEstaEnUnFestival(true);

    expect(componente.validar()).toBeFalse();
    expect(componente.errores()['festivalId']).toBeTruthy();
  });

  it('sin festivales propios, el paso lo dice en vez de dejar un selector vacío', () => {
    // NI SE CREA AL VUELO NI SE ESCRIBE A MANO: registrar un festival tiene su propio recorrido.
    montar([]);
    componente.cambiarSiEstaEnUnFestival(true);
    componente.irAlPaso(3);
    fixture.detectChanges();

    const html: HTMLElement = fixture.nativeElement;
    expect(html.querySelector('[data-sin-festivales-propios]')).not.toBeNull();
    expect(html.querySelector('[data-festival-elegido]')).toBeNull();
  });

  it('si la consulta falla NO dice que no tengas festivales', () => {
    // EL DEFECTO QUE TRAJO ESTA PRUEBA. se detectó el 15 de septiembre de
    // 2026: una organización con festivales registrados leía «Todavía no tienes ningún festival
    // registrado en SIMUS». El error de la consulta se convertía en una lista vacía, y una lista
    // vacía se enseñaba como una afirmación sobre sus datos. Ahora son dos cosas distintas.
    montar('falla');
    componente.cambiarSiEstaEnUnFestival(true);
    componente.irAlPaso(3);
    fixture.detectChanges();

    const html: HTMLElement = fixture.nativeElement;
    expect(componente.consultaDeFestivales()).toBe('fallida');
    expect(html.querySelector('[data-fallo-al-consultar-festivales]')).not.toBeNull();
    expect(html.querySelector('[data-sin-festivales-propios]')).toBeNull();
  });

  it('pasar a cobertura nacional suelta el territorio que deja de tener sentido', () => {
    montar();
    componente.cambiarDepartamento('05');
    componente.formulario.codigoMunicipio = '05001';

    componente.cambiarCobertura('nacional');

    expect(componente.formulario.codigoDepartamento).toBe('');
    expect(componente.formulario.codigoMunicipio).toBe('');
  });
});
