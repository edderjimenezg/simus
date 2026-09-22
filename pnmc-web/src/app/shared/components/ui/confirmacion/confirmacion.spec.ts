import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfirmacionComponent } from './confirmacion.component';

@Component({
  standalone: true,
  imports: [ConfirmacionComponent],
  template: `
    <app-confirmacion
      titulo="Eliminar la cuenta"
      detalle="Desaparece con sus roles y sus módulos."
      accion="Eliminar"
      [pideMotivo]="pideMotivo()"
      rotuloDelMotivo="Por qué se elimina"
      ayudaDelMotivo="Queda en el historial, con tu nombre y la fecha."
      [error]="error()"
      identificador="confirmar-eliminar"
      (confirmar)="confirmado = $event"
      (cancelar)="cancelaciones = cancelaciones + 1">
      <p data-extra>Rocío Salazar · rocio&#64;pnmc.local</p>
    </app-confirmacion>
  `,
})
class Anfitrion {
  readonly pideMotivo = signal(false);
  readonly error = signal('');
  confirmado: string | null = null;
  cancelaciones = 0;
}

describe('ConfirmacionComponent', () => {
  let fixture: ComponentFixture<Anfitrion>;
  let anfitrion: Anfitrion;

  const raiz = () => fixture.nativeElement as HTMLElement;
  const texto = () => raiz().textContent ?? '';
  const botonDeConfirmar = () => raiz().querySelector<HTMLButtonElement>('[data-testid="confirmar-eliminar"]')!;
  const area = () => raiz().querySelector<HTMLTextAreaElement>('textarea')!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('dice el verbo, la consecuencia y lo que la pantalla le proyecte, en un diálogo modal de verdad', () => {
    const dialogo = raiz().querySelector('[role="dialog"]')!;
    expect(dialogo.getAttribute('aria-modal')).toBe('true');
    // MUTANTE QUE MATA: titular el diálogo por fuera. Sin `aria-labelledby` apuntando al título, el
    // lector de pantalla anuncia «diálogo» y la persona no sabe qué está a punto de confirmar.
    const titulo = raiz().querySelector(`#${dialogo.getAttribute('aria-labelledby')}`);
    expect(titulo?.textContent).toContain('Eliminar la cuenta');
    expect(texto()).toContain('Desaparece con sus roles y sus módulos.');
    expect(raiz().querySelector('[data-extra]')).not.toBeNull();
    expect(botonDeConfirmar().textContent).toContain('Eliminar');
  });

  it('sin motivo pedido, confirmar emite una cadena vacía y no muestra ningún campo', () => {
    expect(raiz().querySelector('textarea')).toBeNull();

    botonDeConfirmar().click();
    fixture.detectChanges();

    expect(anfitrion.confirmado).toBe('');
  });

  it('cuando pide motivo, no deja confirmar hasta que haya texto y lo emite recortado', () => {
    anfitrion.pideMotivo.set(true);
    fixture.detectChanges();

    expect(texto()).toContain('Por qué se elimina');
    expect(texto()).toContain('Queda en el historial, con tu nombre y la fecha.');
    expect(botonDeConfirmar().disabled).toBeTrue();

    area().value = '  La cuenta se creó por error.  ';
    area().dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(botonDeConfirmar().disabled).toBeFalse();
    botonDeConfirmar().click();
    fixture.detectChanges();

    expect(anfitrion.confirmado).toBe('La cuenta se creó por error.');
  });

  it('el rótulo del motivo apunta al campo y la ayuda queda descrita', () => {
    anfitrion.pideMotivo.set(true);
    fixture.detectChanges();

    const rotulo = raiz().querySelector<HTMLLabelElement>('label')!;
    expect(rotulo.getAttribute('for')).toBe(area().id);
    const ayuda = raiz().querySelector(`#${area().getAttribute('aria-describedby')}`);
    expect(ayuda?.textContent).toContain('Queda en el historial');
  });

  it('el error del servidor se anuncia y no cierra el diálogo ni borra lo escrito', () => {
    anfitrion.pideMotivo.set(true);
    fixture.detectChanges();
    area().value = 'Se creó por error.';
    area().dispatchEvent(new Event('input'));
    fixture.detectChanges();

    anfitrion.error.set('La cuenta tiene 2 actuaciones en la bitácora.');
    fixture.detectChanges();

    const aviso = raiz().querySelector('[role="alert"]');
    expect(aviso?.textContent).toContain('La cuenta tiene 2 actuaciones en la bitácora.');
    expect(raiz().querySelector('[role="dialog"]')).not.toBeNull();
    expect(area().value).toBe('Se creó por error.');
  });

  it('cancelar no confirma nada', () => {
    const cancelar = [...raiz().querySelectorAll<HTMLButtonElement>('button')]
      .find(boton => (boton.textContent ?? '').includes('Cancelar'))!;

    cancelar.click();
    fixture.detectChanges();

    expect(anfitrion.cancelaciones).toBe(1);
    expect(anfitrion.confirmado).toBeNull();
  });

  it('Escape pide cerrar, igual que cancelar', () => {
    raiz().querySelector('[role="dialog"]')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(anfitrion.cancelaciones).toBe(1);
  });
});
