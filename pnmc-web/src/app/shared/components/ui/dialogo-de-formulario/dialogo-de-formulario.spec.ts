import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DialogoDeFormularioComponent } from './dialogo-de-formulario.component';

@Component({
  standalone: true,
  imports: [DialogoDeFormularioComponent],
  template: `
    <app-dialogo-de-formulario
      titulo="Nueva categoría"
      proposito="Queda disponible en los módulos que elijas."
      verbo="Guardar"
      [guardando]="guardando()"
      [puedeGuardar]="puedeGuardar()"
      [error]="error()"
      identificador="guardar-categoria"
      (guardar)="guardados = guardados + 1"
      (cerrar)="cierres = cierres + 1">
      <label class="campo"><span>Nombre</span><input name="nombre" /></label>
    </app-dialogo-de-formulario>
  `,
})
class Anfitrion {
  readonly guardando = signal(false);
  readonly puedeGuardar = signal(true);
  readonly error = signal<string | null>(null);
  guardados = 0;
  cierres = 0;
}

describe('DialogoDeFormularioComponent', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let anfitrion: Anfitrion;

  const raiz = () => fixture.nativeElement as HTMLElement;
  const guardar = () => raiz().querySelector<HTMLButtonElement>('[data-testid="guardar-categoria"]')!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('es un diálogo modal titulado por su propio título, con los campos que le proyecten', () => {
    const dialogo = raiz().querySelector('[role="dialog"]')!;
    expect(dialogo.getAttribute('aria-modal')).toBe('true');
    expect(raiz().querySelector(`#${dialogo.getAttribute('aria-labelledby')}`)?.textContent).toContain('Nueva categoría');
    expect(raiz().textContent).toContain('Queda disponible en los módulos que elijas.');
    expect(raiz().querySelector('input[name="nombre"]')).not.toBeNull();
  });

  it('enviar el formulario guarda una sola vez y no recarga la página', () => {
    guardar().click();
    fixture.detectChanges();

    expect(anfitrion.guardados).toBe(1);
  });

  it('mientras guarda no vuelve a emitir, y el verbo no cambia', () => {
    anfitrion.guardando.set(true);
    fixture.detectChanges();

    // MUTANTE QUE MATA: cambiar «Guardar» por «Guardando…». El botón del proyecto ya enseña su
    // indicador sin cambiar de ancho; cambiar el texto mueve el control bajo el puntero.
    expect(guardar().textContent).toContain('Guardar');
    expect(guardar().textContent).not.toContain('Guardando');

    guardar().click();
    fixture.detectChanges();
    expect(anfitrion.guardados).toBe(0);
  });

  it('con algo pendiente no deja guardar', () => {
    anfitrion.puedeGuardar.set(false);
    fixture.detectChanges();

    expect(guardar().disabled).toBeTrue();
    guardar().click();
    expect(anfitrion.guardados).toBe(0);
  });

  it('la negativa del servidor se anuncia DENTRO del diálogo', () => {
    anfitrion.error.set('Ya hay una categoría con ese nombre en ese módulo.');
    fixture.detectChanges();

    const aviso = raiz().querySelector('[role="alert"]');
    expect(aviso?.textContent).toContain('Ya hay una categoría con ese nombre');
    // Sigue abierto: detrás está el velo, y ahí el mensaje no se lee.
    expect(raiz().querySelector('[role="dialog"]')).not.toBeNull();
  });

  it('cancelar y Escape piden cerrar, y no guardan nada', () => {
    const cancelar = [...raiz().querySelectorAll<HTMLButtonElement>('button')]
      .find(b => (b.textContent ?? '').includes('Cancelar'))!;
    cancelar.click();
    raiz().querySelector('[role="dialog"]')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(anfitrion.cierres).toBe(2);
    expect(anfitrion.guardados).toBe(0);
  });
});
