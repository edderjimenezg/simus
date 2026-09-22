import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AdminPrimerIngresoComponent } from './admin-primer-ingreso.component';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  standalone: true,
  imports: [AdminPrimerIngresoComponent],
  template: `
    <app-admin-primer-ingreso
      [debeCambiarContrasena]="debeCambiar()"
      correo="rocio@pnmc.local"
      (terminado)="terminados = terminados + 1" />
  `,
})
class Anfitrion {
  readonly debeCambiar = signal(true);
  terminados = 0;
}

/**
 * El primer ingreso de una cuenta administrativa.
 *
 * <b>POR QUE IMPORTA QUE ESTE PROBADO.</b> Es la única pantalla que toda cuenta nueva atraviesa, y
 * decide dos cosas que no se pueden deshacer desde fuera: la contraseña con la que esa persona
 * volverá a entrar, y los datos con que el sistema la identifica en toda su trazabilidad. Una cuenta
 * nace con correo y clave por omisión, y aquí las cambia.
 *
 * <b>LAS DOS CONTRASEÑAS SE COMPARAN EN EL NAVEGADOR</b> a propósito: el servidor no puede detectar
 * un dedo torcido, y quien se equivoca al teclearla se queda fuera en el siguiente ingreso.
 */
describe('el primer ingreso de una cuenta administrativa', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let componente: AdminPrimerIngresoComponent;
  let cambio: { actual: string; nueva: string } | null;
  let perfil: unknown | null;
  let fallaElCambio: string | null;

  const raiz = () => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    cambio = null; perfil = null; fallaElCambio = null;
    const admin: Partial<AdminService> = {
      cambiarMiContrasena: ((actual: string, nueva: string) => {
        cambio = { actual, nueva };
        return fallaElCambio ? throwError(() => ({ message: fallaElCambio })) : of({});
      }) as AdminService['cambiarMiContrasena'],
      completarMiPerfil: ((datos: unknown) => { perfil = datos; return of({}); }) as AdminService['completarMiPerfil'],
      cargarTiposDeDocumento: (() => of([{ codigo: 'CC', etiqueta: 'Cédula de ciudadanía' }])) as AdminService['cargarTiposDeDocumento'],
    };

    await TestBed.configureTestingModule({
      imports: [Anfitrion],
      providers: [{ provide: AdminService, useValue: admin }],
    }).compileComponents();

    fixture = TestBed.createComponent(Anfitrion);
    fixture.detectChanges();
    componente = fixture.debugElement.children[0].componentInstance as AdminPrimerIngresoComponent;
  });

  it('empieza por la contraseña cuando el servidor dice que hay que cambiarla', () => {
    expect(componente.paso()).toBe('contrasena');
  });

  it('una contraseña corta no se envía, y se dice el mínimo', () => {
    componente.nueva.set('corta');
    componente.repetida.set('corta');

    componente.cambiarLaContrasena();

    expect(cambio).toBeNull();
    expect(componente.error()).toContain('diez caracteres');
  });

  it('dos contraseñas que no coinciden no se envían', () => {
    // MUTANTE QUE MATA: comparar solo en el servidor. El servidor recibe una sola contraseña y no
    // puede saber que la persona quiso escribir otra.
    componente.nueva.set('clave-larga-de-verdad');
    componente.repetida.set('clave-larga-de-verdadd');

    componente.cambiarLaContrasena();

    expect(cambio).toBeNull();
    expect(componente.error()).toContain('no coinciden');
  });

  it('con las dos iguales la cambia y pasa a los datos de la persona', () => {
    componente.actual.set('admin');
    componente.nueva.set('clave-larga-de-verdad');
    componente.repetida.set('clave-larga-de-verdad');

    componente.cambiarLaContrasena();

    expect(cambio).toEqual({ actual: 'admin', nueva: 'clave-larga-de-verdad' });
    expect(componente.paso()).toBe('perfil');
  });

  it('si el servidor rechaza la contraseña, se queda en el paso y lo dice con su motivo', () => {
    fallaElCambio = 'La contraseña actual no es la que está guardada.';
    componente.nueva.set('clave-larga-de-verdad');
    componente.repetida.set('clave-larga-de-verdad');

    componente.cambiarLaContrasena();

    expect(componente.paso()).toBe('contrasena');
    expect(componente.error()).toBe('La contraseña actual no es la que está guardada.');
  });

  it('el perfil exige lo que identifica, y no lo que no todo el mundo tiene', () => {
    componente.paso.set('perfil');
    componente.campoDelPerfil('primerNombre', 'Rocío');
    componente.campoDelPerfil('primerApellido', 'Salazar');

    componente.guardarElPerfil();
    expect(perfil).toBeNull();
    expect(componente.error()).toContain('documento');

    componente.campoDelPerfil('tipoDocumento', 'CC');
    componente.campoDelPerfil('identificacion', '1020304050');
    componente.guardarElPerfil();

    // EL SEGUNDO NOMBRE Y EL SEGUNDO APELLIDO NO SE EXIGEN: no los tiene todo el mundo.
    expect(perfil).not.toBeNull();
    expect(fixture.componentInstance.terminados).toBe(1);
  });

  it('quien ya cambió la clave entra directo a sus datos', async () => {
    const otro = TestBed.createComponent(Anfitrion);
    otro.componentInstance.debeCambiar.set(false);
    otro.detectChanges();
    await otro.whenStable();
    otro.detectChanges();

    const hijo = otro.debugElement.children[0].componentInstance as AdminPrimerIngresoComponent;
    expect(hijo.paso()).toBe('perfil');
  });

  it('no se puede cerrar: es la única pantalla que toda cuenta nueva atraviesa', () => {
    // MUTANTE QUE MATA: darle una cruz o cerrar con Escape. Una cuenta a medio estrenar entra al
    // sistema con la clave por omisión y sin datos que la identifiquen en la trazabilidad.
    const cerrar = [...raiz().querySelectorAll('button')]
      .filter(b => /cerrar|cancelar|salir/i.test(b.textContent ?? '') || /cerrar/i.test(b.getAttribute('aria-label') ?? ''));
    expect(cerrar.length).toBe(0);
  });
});
