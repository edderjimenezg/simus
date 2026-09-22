import { Component, ElementRef, HostListener, computed, inject, input, output, signal } from '@angular/core';

/** Una fila de catalogo: identificador y nombre, que es lo unico que el control pinta. */
export interface OpcionSeleccionable {
  id: number;
  nombre: string;
}

/**
 * Un desplegable de seleccion multiple, hecho con casillas.
 *
 * <b>POR QUE NO ES UN `select multiple`.</b> Ese control del navegador exige mantener pulsada una
 * tecla para marcar mas de una opcion, y un clic normal BORRA todo lo marcado sin avisar. Nada en
 * pantalla lo explica. está definido sobre el formulario del
 * Festival: «no esta funcionando bien el selector».
 *
 * <b>POR QUE TAMPOCO SON TREINTA CASILLAS SUELTAS.</b> Fue el arreglo anterior, y resolvio la
 * perdida de datos a costa de la altura: dieciseis practicas mas catorce territorios ocupaban mas
 * pantalla que el resto del formulario junto. Se define «una lista desplegable» sobre los dos
 * bloques el mismo dia. Este control conserva las casillas —una opcion se marca y se desmarca
 * sola— y las guarda dentro de un panel que se abre.
 *
 * <b>LO QUE EL BOTON DICE SIN ABRIRLO.</b> Los nombres de lo marcado y su recuento. Un desplegable
 * que solo dijera «3 seleccionadas» obligaria a abrirlo para saber cuales.
 *
 * <b>`aria-controls` SOLO CUANDO EL PANEL EXISTE.</b> El panel se pinta con `@if`, asi que fuera
 * de ese caso el atributo apuntaria a un identificador que no esta en el DOM. Es el mismo defecto
 * que se corrigio el 27 de agosto en las pestanas del panel de la organizacion.
 */
@Component({
  selector: 'app-selector-multiple',
  standalone: true,
  template: `
    <div class="relative">
      <span [id]="identificador() + '-etiqueta'" class="block text-sm font-bold text-slate-900">
        {{ etiqueta() }}
        @if (ayuda()) { <span class="font-normal text-slate-600">{{ ayuda() }}</span> }
      </span>

      <!--
        EL TIPO DEL BOTON NO ES DECORATIVO. Este control vive dentro del formulario del Festival, y
        un boton sin tipo dentro de un formulario es de envio: abrir el desplegable, en vez de
        desplegar, guardaria el Festival.
      -->
      <button type="button"
              [id]="identificador() + '-boton'"
              [attr.aria-labelledby]="identificador() + '-etiqueta ' + identificador() + '-boton'"
              [attr.aria-expanded]="abierto()"
              [attr.aria-controls]="abierto() ? identificador() + '-lista' : null"
              aria-haspopup="true"
              (click)="alternarPanel()"
              class="mt-1 flex w-full items-center justify-between gap-3 rounded-lg border border-slate-300 bg-white p-3 text-left text-sm transition hover:border-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado">
        <span class="min-w-0 flex-1 truncate" [class.text-slate-500]="seleccionados().length === 0">{{ resumen() }}</span>
        <span class="flex shrink-0 items-center gap-2">
          @if (seleccionados().length > 0) {
            <span class="rounded-full bg-verde-texto px-2 py-0.5 text-xs font-bold text-white">{{ seleccionados().length }}</span>
          }
          <span aria-hidden="true" class="text-slate-500">{{ abierto() ? '&#9650;' : '&#9660;' }}</span>
        </span>
      </button>

      @if (abierto()) {
        <div [id]="identificador() + '-lista'" role="group" [attr.aria-labelledby]="identificador() + '-etiqueta'"
             class="absolute left-0 right-0 z-30 mt-1 max-h-64 overflow-y-auto rounded-xl border border-slate-200 bg-white p-2 shadow-lg">
          @for (opcion of opciones(); track opcion.id) {
            <label class="flex cursor-pointer items-start gap-2 rounded-lg p-2 text-sm transition hover:bg-slate-50 has-[:checked]:bg-verde-medio/10">
              <input type="checkbox" [checked]="estaMarcada(opcion.id)" (change)="alternar.emit(opcion.id)"
                     class="mt-0.5 h-4 w-4 shrink-0 accent-verde-texto">
              <span>{{ opcion.nombre }}</span>
            </label>
          } @empty {
            <p class="p-2 text-sm text-slate-500">Todavía no hay opciones en este catálogo.</p>
          }
        </div>
      }
    </div>
  `,
})
export class SelectorMultipleComponent {
  private readonly elemento = inject(ElementRef<HTMLElement>);

  /** El rotulo visible, que es tambien el nombre accesible del boton y del panel. */
  readonly etiqueta = input.required<string>();

  /** Prefijo de los identificadores del DOM. Dos controles en la misma pagina no pueden repetirlo. */
  readonly identificador = input.required<string>();

  readonly opciones = input<OpcionSeleccionable[]>([]);
  readonly seleccionados = input<number[]>([]);
  readonly ayuda = input('');
  readonly textoVacio = input('Sin seleccionar');

  /** Se emite el identificador que se marco o desmarco; la lista la mantiene quien usa el control. */
  readonly alternar = output<number>();

  readonly abierto = signal(false);

  /**
   * Los nombres de lo marcado, en el orden del catalogo y no en el de los clics.
   *
   * En el del catalogo porque el mismo conjunto de opciones debe leerse igual siempre: con el orden
   * de los clics, marcar A y luego B da un resumen distinto que marcar B y luego A.
   */
  readonly resumen = computed(() => {
    const marcados = this.opciones().filter(opcion => this.seleccionados().includes(opcion.id));
    return marcados.length === 0 ? this.textoVacio() : marcados.map(opcion => opcion.nombre).join(', ');
  });

  estaMarcada(id: number): boolean {
    return this.seleccionados().includes(id);
  }

  alternarPanel(): void {
    this.abierto.update(estaba => !estaba);
  }

  /**
   * Escape cierra y DEVUELVE EL FOCO AL BOTON.
   *
   * Sin devolverlo, el foco se queda sobre una casilla que acaba de salir del DOM y el navegador lo
   * manda al principio del documento: quien navega con teclado tiene que recorrer la pagina entera
   * para volver al campo que estaba llenando.
   */
  /**
   * ESCAPE SE DETIENE AQUI CUANDO ESTE CONTROL LO ATIENDE.
   *
   * <b>POR QUE EXISTE.</b> Desde que toda ventana lleva `appDialogo` —15 de septiembre de
   * 2026— la ventana también cierra con Escape. Sin detener el evento, una sola pulsación cerraba
   * el desplegable Y la ventana entera: quien abría las prácticas de un mercado y pulsaba Escape
   * perdía el formulario a medio llenar. Lo de dentro se cierra primero, y la ventana solo se
   * cierra cuando nada dentro reclamó la tecla.
   */
  @HostListener('keydown.escape', ['$event'])
  cerrar(evento?: Event): void {
    if (!this.abierto()) return;
    evento?.stopPropagation();
    this.abierto.set(false);
    this.elemento.nativeElement.querySelector('button')?.focus();
  }

  /**
   * Un clic fuera cierra el panel, y no se devuelve el foco.
   *
   * Aqui no se devuelve a proposito: el clic ya lo puso donde el usuario queria, y robarlo de vuelta
   * al boton haria que el campo siguiente perdiera el cursor nada mas pincharlo.
   */
  @HostListener('document:click', ['$event'])
  alClicarFuera(evento: MouseEvent): void {
    if (!this.abierto()) return;
    if (!this.elemento.nativeElement.contains(evento.target as Node)) this.abierto.set(false);
  }
}
