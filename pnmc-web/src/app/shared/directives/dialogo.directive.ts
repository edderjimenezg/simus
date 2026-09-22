import { Directive, ElementRef, OnDestroy, afterNextRender, inject, input, output } from '@angular/core';

/**
 * Lo que un diálogo modal tiene que hacer, resuelto una vez.
 *
 * <b>POR QUE EXISTE, CON LO QUE SE MIDIO.</b> La auditoría abrió los
 * diálogos de la consola en el navegador y comprobó que <b>ninguno lleva el foco dentro</b>: a los
 * 300, 900, 2.000 y 3.500 ms el elemento activo seguía siendo el botón que lo había abierto. Y
 * <b>ninguno lo retiene</b>: a las nueve tabulaciones el foco estaba en el contenido de fondo, que el
 * velo oscurece. Quien navega con teclado o con lector de pantalla abría un diálogo y se quedaba
 * fuera de él, recorriendo una página que no puede ver.
 *
 * <b>NO HABIA DONDE ARREGLARLO.</b> Diecisiete plantillas declaran `role="dialog"` y no existía
 * ninguna directiva ni servicio compartido —ni `cdkTrapFocus`, ni `A11yModule`—: cada una resolvía un
 * subconjunto distinto del problema. Siete cerraban con Esc, siete bloqueaban el fondo, una devolvía
 * el foco. Esto es ese subconjunto, completo y en un solo sitio.
 *
 * <b>ES IDEMPOTENTE A PROPOSITO.</b> Varios diálogos ya bloquean el fondo o escuchan Esc por su
 * cuenta, y la migración va a ser gradual. El bloqueo del fondo lleva cuenta de cuántos diálogos lo
 * piden, así que cerrar uno no desbloquea la página mientras otro siga abierto, y aplicar la
 * directiva sobre un diálogo que ya hacía algo de esto no rompe nada.
 *
 * <b>LO QUE NO HACE:</b> no dibuja el velo ni decide cuándo se abre. Eso lo sigue haciendo cada
 * pantalla con su `@if`, que es lo que hace que el ciclo de vida de la directiva coincida con el del
 * diálogo sin tener que coordinarlos.
 *
 * @example
 * <div role="dialog" aria-modal="true" appDialogo (cerrar)="cerrarFicha()">
 */
@Directive({
  selector: '[appDialogo]',
  standalone: true,
  host: {
    '(keydown)': 'alPulsarTecla($event)',
    tabindex: '-1',
  },
})
export class DialogoDirective implements OnDestroy {
  /** Esc cierra. Se puede desactivar cuando cerrar sin confirmar perdería trabajo. */
  readonly dialogoCierraConEscape = input(true);

  /**
   * Si el fondo deja de desplazarse mientras está abierto.
   *
   * <b>UN MENU NO ES UN DIALOGO, y esto es lo que los separa.</b> Un diálogo modal ocupa la
   * pantalla y dejar el fondo desplazándose detrás es desorientador; un menú colgado de la
   * cabecera mide ocho centímetros y bloquear la página entera por él es desproporcionado.
   *
   * <b>Y ADEMAS ROMPE LA CABECERA.</b> El bloqueo se hace con `overflow: hidden` en el `body`, y
   * eso convierte al `body` en contenedor de desplazamiento: la cabecera `position: sticky` deja
   * de tener a quién pegarse y cae a su sitio en el documento. Comprobado:
   * con la página desplazada 1 500 px, abrir el menú de la cuenta mandaba la cabecera a
   * `top: -1500` y había que subir del todo para recuperarla. Lo reportó la dirección de producto.
   *
   * Se deja en `true` por omisión porque los diecisiete diálogos que ya usan la directiva sí lo
   * quieren; los menús lo apagan.
   */
  readonly dialogoBloqueaElFondo = input(true);

  /** Lo emite Esc. Quien lo recibe decide si cierra o pregunta antes. */
  readonly cerrar = output<void>();

  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly abridor = document.activeElement as HTMLElement | null;

  /**
   * Cuántos diálogos piden el bloqueo ahora mismo.
   *
   * ES ESTATICO PORQUE EL FONDO ES UNO SOLO. Con un diálogo dentro de otro —confirmar un borrado
   * desde una ficha abierta—, cerrar el de dentro devolvía el desplazamiento a la página estando el
   * de fuera todavía abierto.
   */
  private static bloqueos = 0;
  private static desbordeOriginal = '';

  constructor() {
    afterNextRender(() => {
      // EL BLOQUEO SE DECIDE AQUI Y NO EN EL CONSTRUCTOR. Una entrada de señal todavía no tiene
      // valor cuando el constructor corre: leerla ahí devuelve siempre el de partida, así que
      // `[dialogoBloqueaElFondo]="false"` no llegaba a aplicarse y el menú seguía bloqueando el
      // fondo. Comprobado: `body.style.overflow` seguía en «hidden».
      if (this.dialogoBloqueaElFondo()) { this.bloquearElFondo(); }
      this.entrar();
    });
  }

  private bloquearElFondo(): void {
    if (DialogoDirective.bloqueos === 0) {
      DialogoDirective.desbordeOriginal = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
    }
    DialogoDirective.bloqueos++;
    this.bloqueoPedido = true;
  }

  /** Si ESTE en concreto pidió el bloqueo, para no soltar uno que nunca tomó. */
  private bloqueoPedido = false;

  ngOnDestroy(): void {
    if (this.bloqueoPedido) {
      DialogoDirective.bloqueos = Math.max(0, DialogoDirective.bloqueos - 1);
      if (DialogoDirective.bloqueos === 0) {
        document.body.style.overflow = DialogoDirective.desbordeOriginal;
      }
    }

    // EL FOCO VUELVE A DONDE ESTABA. Sin esto, cerrar un diálogo deja el foco en el `body` y la
    // siguiente tabulación empieza por el principio de la página, muy lejos de donde se estaba.
    // Solo se devuelve si el abridor sigue en el documento: puede haber desaparecido con la fila que
    // el propio diálogo acaba de borrar.
    if (this.abridor?.isConnected) { this.abridor.focus({ preventScroll: true }); }
  }

  // Público porque lo invoca el enlace de host declarado arriba.
  alPulsarTecla(evento: KeyboardEvent): void {
    if (evento.key === 'Escape' && this.dialogoCierraConEscape()) {
      evento.stopPropagation();
      this.cerrar.emit();
      return;
    }
    if (evento.key === 'Tab') { this.retenerElFoco(evento); }
  }

  /**
   * El foco entra al abrirse.
   *
   * VA AL PRIMER CONTROL, y si no hay ninguno, al propio diálogo —que por eso lleva `tabindex="-1"`—.
   * No se enfoca el botón de cerrar aunque sea el primero del marcado: empezar por «cerrar» le dice a
   * quien usa un lector de pantalla que lo primero que puede hacer es irse.
   */
  private entrar(): void {
    const dentro = this.enfocables();
    const primero = dentro.find(el => !/cerrar|close/i.test(el.getAttribute('aria-label') ?? '')) ?? dentro[0];
    (primero ?? this.host.nativeElement).focus({ preventScroll: true });
  }

  private retenerElFoco(evento: KeyboardEvent): void {
    const dentro = this.enfocables();
    if (dentro.length === 0) { evento.preventDefault(); return; }

    const primero = dentro[0];
    const ultimo = dentro[dentro.length - 1];
    const activo = document.activeElement;

    // Se envuelve por los dos extremos. Y si el foco estaba FUERA —porque algo lo sacó—, se recupera.
    if (!evento.shiftKey && activo === ultimo) { evento.preventDefault(); primero.focus(); }
    else if (evento.shiftKey && activo === primero) { evento.preventDefault(); ultimo.focus(); }
    else if (!this.host.nativeElement.contains(activo)) { evento.preventDefault(); primero.focus(); }
  }

  /**
   * Lo que se puede enfocar ahora mismo dentro del diálogo.
   *
   * SE CALCULA EN CADA TABULACION Y NO UNA VEZ: el contenido cambia —se abre un desplegable, aparece
   * un error, se añade una fila—, y una lista tomada al abrir dejaría fuera lo que llegó después.
   */
  private enfocables(): HTMLElement[] {
    const selector =
      'a[href],button:not([disabled]),input:not([disabled]):not([type=hidden]),' +
      'select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';
    return [...this.host.nativeElement.querySelectorAll<HTMLElement>(selector)]
      .filter(el => el.offsetParent !== null || getComputedStyle(el).position === 'fixed');
  }
}
