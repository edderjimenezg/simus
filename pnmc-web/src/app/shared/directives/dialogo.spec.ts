import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DialogoDirective } from './dialogo.directive';

@Component({
  standalone: true,
  imports: [DialogoDirective],
  template: `
    <button id="abridor" (click)="abierto.set(true)">Abrir</button>
    @if (abierto()) {
      <div role="dialog" aria-modal="true" appDialogo (cerrar)="abierto.set(false)">
        <button id="primero">Primero</button>
        <input id="campo" />
        <button id="ultimo">Último</button>
      </div>
    }
  `,
})
class Anfitrion {
  readonly abierto = signal(false);
}

describe('DialogoDirective', () => {
  let fixture: ComponentFixture<Anfitrion>;
  const abridor = () => fixture.nativeElement.querySelector('#abridor') as HTMLButtonElement;
  const dialogo = () => fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement | null;

  const abrir = async () => {
    abridor().focus();
    abridor().click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Anfitrion] }).compileComponents();
    fixture = TestBed.createComponent(Anfitrion);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
  });

  afterEach(() => fixture.nativeElement.remove());

  it('lleva el foco DENTRO del diálogo al abrirse', async () => {
    // El fallo real que obligó a escribir esto: medido en el navegador,
    // a los 3.500 ms de abrir un diálogo el elemento activo seguía siendo el botón de origen.
    await abrir();
    expect(dialogo()!.contains(document.activeElement)).toBeTrue();
  });

  it('no empieza por el botón de cerrar, cuando hay algo más que hacer', async () => {
    await abrir();
    // Empezar por «cerrar» le dice a quien usa un lector de pantalla que lo primero disponible es irse.
    expect((document.activeElement as HTMLElement).id).toBe('primero');
  });

  it('retiene el foco: tabular desde el último vuelve al primero', async () => {
    await abrir();
    const ultimo = fixture.nativeElement.querySelector('#ultimo') as HTMLElement;
    ultimo.focus();
    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    expect((document.activeElement as HTMLElement).id).toBe('primero');
  });

  it('y con mayúsculas, desde el primero vuelve al último', async () => {
    await abrir();
    (fixture.nativeElement.querySelector('#primero') as HTMLElement).focus();
    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }));
    expect((document.activeElement as HTMLElement).id).toBe('ultimo');
  });

  it('si el foco se escapó, la siguiente tabulación lo recupera', async () => {
    await abrir();
    // Comprobado: sin esto, a las nueve tabulaciones el foco estaba en el contenido de fondo, que el velo
    // oscurece, así que quien navega con teclado recorre una página que no puede ver.
    abridor().focus();
    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    expect(dialogo()!.contains(document.activeElement)).toBeTrue();
  });

  it('Escape emite el cierre, y quien lo recibe decide', async () => {
    await abrir();
    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(dialogo()).toBeNull();
  });

  it('bloquea el fondo mientras está abierto y lo devuelve al cerrar', async () => {
    const antes = document.body.style.overflow;
    await abrir();
    expect(document.body.style.overflow).toBe('hidden');

    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(document.body.style.overflow).toBe(antes);
  });

  it('devuelve el foco al elemento que lo abrió', async () => {
    await abrir();
    dialogo()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(abridor());
  });

  it('un menu puede pedir que NO se bloquee el fondo', () => {
    // UN MENU NO ES UN DIALOGO. El bloqueo se hace con `overflow: hidden` en el `body`, y eso
    // convierte al `body` en contenedor de desplazamiento: la cabecera `position: sticky` deja de
    // tener a quien pegarse y cae a su sitio en el documento. Comprobado:
    // con la pagina desplazada 1 500 px, abrir el menu de la cuenta mandaba la cabecera a
    // `top: -1500`. Lo reporto la dirección de producto.
    //
    // Y SE DECIDE DESPUES DE PINTAR, no en el constructor: una entrada de señal todavia no tiene
    // valor cuando el constructor corre, asi que leerla ahi devolvia siempre `true` y el menu
    // seguia bloqueando aunque pidiera lo contrario.
    @Component({
      standalone: true,
      imports: [DialogoDirective],
      template: `<div role="menu" appDialogo [dialogoBloqueaElFondo]="false"></div>`,
    })
    class MenuDePrueba {}

    const antes = document.body.style.overflow;
    const fixture = TestBed.createComponent(MenuDePrueba);
    fixture.detectChanges();

    expect(document.body.style.overflow).toBe(antes);

    fixture.destroy();
    expect(document.body.style.overflow).toBe(antes);
  });
});
