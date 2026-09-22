import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { computed, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { PanelCuentaSeguridadComponent } from './panel-cuenta-seguridad.component';
import { ExternalSessionService, SesionExterna } from '../../core/services/external-session.service';

/**
 * La ruta «Cuenta y seguridad», en /ecosistema/mi-panel/cuenta-seguridad.
 *
 * NUEVA EN EL REDISEÑO DEL 1 DE SEPTIEMBRE DE 2026. Solo tres cosas: el correo de acceso de solo
 * lectura, «Cambiar contraseña» deshabilitado -no hay endpoint de cambio de contraseña
 * autoservicio en toda la API externa- y «Cerrar sesión», que sí funciona.
 */
class SesionFalsa {
  readonly interna = signal<SesionExterna | null>(null);
  readonly actual = this.interna.asReadonly();
  readonly organizaciones = computed(() => this.interna()?.organizations ?? []);
  vecesQueSalio = 0;

  establecer(sesion: SesionExterna | null): void {
    this.interna.set(sesion);
  }

  salir(): Observable<unknown> {
    this.vecesQueSalio += 1;
    this.interna.set(null);
    return of(null);
  }
}

const SESION: SesionExterna = {
  userId: '9',
  fullName: 'Ana Restrepo',
  email: 'ana@fmv.org',
  accountStatus: 'activa',
  organizations: [{ id: '117', name: 'Fundación Musical del Valle', role: 'administrador' }],
};

describe('PanelCuentaSeguridadComponent', () => {
  let fixture: ComponentFixture<PanelCuentaSeguridadComponent>;
  let sesion: SesionFalsa;
  let navegar: jasmine.Spy;

  beforeEach(() => {
    sesion = new SesionFalsa();
    sesion.establecer(SESION);
    TestBed.configureTestingModule({
      imports: [PanelCuentaSeguridadComponent],
      providers: [
        provideRouter([]),
        { provide: ExternalSessionService, useValue: sesion },
      ],
    });
    navegar = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    fixture = TestBed.createComponent(PanelCuentaSeguridadComponent);
    fixture.detectChanges();
  });

  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('muestra el correo de acceso de la sesión', () => {
    expect(raiz().textContent).toContain('ana@fmv.org');
  });

  it('prepara la foto de perfil sin intentar guardar una imagen todavía', () => {
    const boton = Array.from(raiz().querySelectorAll('button'))
      .find(candidato => (candidato.textContent ?? '').trim() === 'Editar foto de perfil');

    expect(raiz().textContent).toContain('Foto de perfil');
    expect(boton).withContext('no se pintó la acción futura de foto').toBeTruthy();
    expect(boton!.disabled).toBeTrue();
    expect(boton!.title).toBe('Próximamente');
  });

  it('«Cambiar contraseña» está deshabilitado: no hay endpoint que lo respalde', () => {
    const boton = Array.from(raiz().querySelectorAll('button'))
      .find(candidato => (candidato.textContent ?? '').trim() === 'Cambiar contraseña');

    expect(boton).withContext('no se pintó «Cambiar contraseña»').toBeTruthy();
    expect(boton!.disabled).toBeTrue();
    expect(boton!.title).toBe('Próximamente');
  });

  it('«Cerrar sesión» cierra la sesión y vuelve al inicio', () => {
    const boton = Array.from(raiz().querySelectorAll('button'))
      .find(candidato => (candidato.textContent ?? '').trim() === 'Cerrar sesión');
    expect(boton).withContext('no se pintó «Cerrar sesión»').toBeTruthy();

    boton!.click();

    expect(sesion.vecesQueSalio).toBe(1);
    expect(navegar).toHaveBeenCalledWith(['/']);
  });
});
