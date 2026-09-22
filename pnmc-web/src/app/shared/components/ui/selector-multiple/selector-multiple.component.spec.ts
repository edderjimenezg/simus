import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SelectorMultipleComponent } from './selector-multiple.component';

/**
 * El desplegable de selección múltiple, pedido sobre los dos catálogos del
 * formulario del Festival: «una lista desplegable».
 *
 * <b>Qué se instrumenta.</b> No el aspecto, sino las tres cosas que un desplegable hecho a mano
 * rompe con facilidad y que nadie ve hasta que pasa:
 *
 * <ul>
 *   <li><b>Que marcar no cierre el panel.</b> Es la diferencia entre un selector de una opción y
 *       uno de varias. Si se cierra, marcar tres prácticas exige abrirlo tres veces.</li>
 *   <li><b>Que el botón no envíe el formulario.</b> Este control vive dentro del formulario del
 *       Festival: un botón sin `type` dentro de un `form` es de envío, así que desplegar la lista
 *       guardaría el Festival.</li>
 *   <li><b>Que `aria-controls` no apunte al vacío.</b> El panel se pinta con `@if`; con el atributo
 *       fijo, cerrado señala un identificador que no está en el DOM.</li>
 * </ul>
 */
@Component({
  standalone: true,
  imports: [SelectorMultipleComponent],
  template: `
    <form (submit)="envios = envios + 1; $event.preventDefault()">
      <app-selector-multiple etiqueta="Prácticas musicales" identificador="practicas"
                             [opciones]="opciones" [seleccionados]="marcados()"
                             (alternar)="alternar($event)"></app-selector-multiple>
    </form>
  `,
})
class AnfitrionDePrueba {
  readonly opciones = [
    { id: 1, nombre: 'Músicas campesinas' },
    { id: 2, nombre: 'Músicas urbanas' },
    { id: 3, nombre: 'Músicas vocales' },
  ];
  readonly marcados = signal<number[]>([]);
  envios = 0;

  alternar(id: number): void {
    this.marcados.update(lista => (lista.includes(id) ? lista.filter(item => item !== id) : [...lista, id]));
  }
}

describe('SelectorMultipleComponent', () => {
  let fixture: ComponentFixture<AnfitrionDePrueba>;
  let anfitrion: AnfitrionDePrueba;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AnfitrionDePrueba] }).compileComponents();
    fixture = TestBed.createComponent(AnfitrionDePrueba);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  function boton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('#practicas-boton');
  }

  function panel(): HTMLElement | null {
    return fixture.nativeElement.querySelector('#practicas-lista');
  }

  function casillas(): HTMLInputElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('#practicas-lista input[type="checkbox"]'));
  }

  function abrir(): void {
    boton().click();
    fixture.detectChanges();
  }

  it('cerrado no hay panel, y `aria-controls` no señala nada', () => {
    expect(panel()).toBeNull();
    expect(boton().getAttribute('aria-expanded')).toBe('false');
    // El atributo se pone SOLO cuando el panel existe. Fijo, apuntaría a un identificador ausente.
    expect(boton().getAttribute('aria-controls')).toBeNull();
  });

  it('abierto hay una casilla por opción, y `aria-controls` apunta a un elemento que existe', () => {
    abrir();

    expect(casillas().length).toBe(3);
    expect(boton().getAttribute('aria-expanded')).toBe('true');
    const senalado = boton().getAttribute('aria-controls');
    expect(senalado).toBe('practicas-lista');
    expect(fixture.nativeElement.querySelector('#' + senalado)).not.toBeNull();
  });

  it('marcar emite el identificador y NO cierra el panel', () => {
    abrir();

    casillas()[1].click();
    fixture.detectChanges();

    expect(anfitrion.marcados()).toEqual([2]);
    // Si se cerrara, marcar tres prácticas obligaría a abrir el desplegable tres veces.
    expect(panel()).not.toBeNull();
    expect(casillas()[1].checked).toBeTrue();

    casillas()[2].click();
    fixture.detectChanges();
    expect(anfitrion.marcados()).toEqual([2, 3]);
  });

  it('el botón dice qué hay marcado sin abrirlo, y lo cuenta', () => {
    expect(boton().textContent).toContain('Sin seleccionar');

    anfitrion.marcados.set([3, 1]);
    fixture.detectChanges();

    // En el orden del catálogo, no en el de los clics: el mismo conjunto debe leerse siempre igual.
    expect(boton().textContent).toContain('Músicas campesinas, Músicas vocales');
    expect(boton().textContent).toContain('2');
  });

  it('Escape cierra y devuelve el foco al botón', () => {
    abrir();
    casillas()[0].focus();

    casillas()[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(panel()).toBeNull();
    // Sin devolverlo, el foco queda sobre una casilla que ya no está en el DOM y el navegador lo
    // manda al principio del documento.
    expect(document.activeElement).toBe(boton());
  });

  it('un clic fuera cierra el panel', () => {
    abrir();
    expect(panel()).not.toBeNull();

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();

    expect(panel()).toBeNull();
  });

  it('abrir el desplegable NO envía el formulario que lo contiene', () => {
    abrir();

    // Un botón sin `type` dentro de un `form` es de envío. Aquí eso significaría que desplegar el
    // catálogo de prácticas guarda el Festival.
    expect(boton().type).toBe('button');
    expect(anfitrion.envios).toBe(0);
  });
});

/**
 * Escape no puede cerrar la ventana que contiene al desplegable.
 *
 * <b>POR QUE EXISTE.</b> Desde que toda ventana lleva `appDialogo` —15 de septiembre de
 * 2026— la ventana también cierra con Escape. Sin detener el evento, una sola pulsación cerraba el
 * desplegable Y la ventana: quien abría las prácticas de un mercado y pulsaba Escape perdía el
 * formulario a medio llenar. Medido en el navegador.
 */
@Component({
  standalone: true,
  imports: [SelectorMultipleComponent],
  template: `
    <!-- tabindex como el de una ventana real: la directiva de diálogo lo pone en su anfitrión. -->
    <div tabindex="-1" (keydown.escape)="laVentanaSeCerraria = laVentanaSeCerraria + 1">
      <app-selector-multiple etiqueta="Prácticas musicales" identificador="practicas-en-ventana"
                             [opciones]="opciones" [seleccionados]="[]"></app-selector-multiple>
    </div>
  `,
})
class VentanaDePrueba {
  readonly opciones = [{ id: 1, nombre: 'Músicas campesinas' }];
  laVentanaSeCerraria = 0;
}

describe('SelectorMultipleComponent · Escape dentro de una ventana', () => {
  let fixture: ComponentFixture<VentanaDePrueba>;
  let anfitrion: VentanaDePrueba;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [VentanaDePrueba] }).compileComponents();
    fixture = TestBed.createComponent(VentanaDePrueba);
    anfitrion = fixture.componentInstance;
    fixture.detectChanges();
  });

  function escapar(): void {
    fixture.nativeElement.querySelector('#practicas-en-ventana-boton')
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
  }

  it('con el panel abierto, Escape lo cierra y NO llega a la ventana', () => {
    fixture.nativeElement.querySelector('#practicas-en-ventana-boton').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#practicas-en-ventana-lista')).not.toBeNull();

    escapar();

    expect(fixture.nativeElement.querySelector('#practicas-en-ventana-lista')).toBeNull();
    expect(anfitrion.laVentanaSeCerraria).toBe(0);
  });

  it('con el panel cerrado, Escape sigue su camino y cierra la ventana', () => {
    // Lo contrario también importa: si se detuviera siempre, una ventana con un desplegable
    // cerrado dejaría de cerrarse con Escape sin ninguna razón visible.
    escapar();

    expect(anfitrion.laVentanaSeCerraria).toBe(1);
  });
});
