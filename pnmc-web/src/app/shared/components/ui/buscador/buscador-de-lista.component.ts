import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, inject, input, output, viewChild } from '@angular/core';
import { LucideSearch, LucideX } from '@lucide/angular';

/**
 * El buscador de una lista del proyecto.
 *
 * <b>FILTRA MIENTRAS SE ESCRIBE.</b> quedó fijado para
 * todo el proyecto, no para una pantalla: «quiero que los buscadores de todo el proyecto sean
 * automáticos, que vayan filtrando a medida que vas escribiendo y no hasta darle enter, poner la x
 * para limpiar la barra de búsqueda y eliminar elementos o botones redundantes». Los trece
 * buscadores de la consola y del portal hacían tres cosas distintas: unos filtraban al escribir,
 * otros esperaban a Intro, y dos tenían además un botón «Buscar» al lado que hacía lo mismo que
 * Intro.
 *
 * <b>LA ESPERA NO ES UN RETARDO, ES LO QUE EVITA UNA PETICION POR TECLA.</b> Cuando la búsqueda la
 * resuelve el servidor —Agenda, Noticias, Organizaciones—, emitir en cada pulsación mandaría diez
 * peticiones por «festival» y las respuestas podrían llegar desordenadas, dejando en pantalla el
 * resultado de «festiv» encima del de «festival». Se espera a que la escritura se detenga. Cuando
 * el filtrado es en memoria no hay nada que esperar y `espera` se pone en cero.
 *
 * <b>LA «X» SOLO APARECE CUANDO HAY ALGO QUE BORRAR</b>, y al pulsarla devuelve el foco al campo:
 * quien limpia una búsqueda casi siempre va a escribir otra.
 *
 * NO SE USA EL ASPA QUE PINTA EL NAVEGADOR en `input[type=search]`: existe solo en algunos
 * navegadores, no se puede estilar, no se anuncia con nombre y no avisa a la aplicación de forma
 * fiable. Se apaga en el contrato de estilo y se pone una de verdad.
 */
@Component({
  selector: 'app-buscador-de-lista',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideSearch, LucideX],
  styles: [`
    :host { position: relative; display: block; flex: 1 1 14rem; min-width: 11rem; max-width: 18rem; }
    :host([ancho]) { max-width: none; }

    .buscador__lupa {
      position: absolute; left: .75rem; top: 50%; transform: translateY(-50%);
      color: var(--color-rotulo); pointer-events: none;
    }
    .buscador__campo {
      min-height: var(--spacing-control); width: 100%;
      border: 1px solid #dfe3ea; border-radius: .7rem; background: #fff;
      padding: .45rem .8rem .45rem 2.1rem;
      color: var(--color-valor); font-size: var(--text-cuerpo);
      transition: border-color .12s, box-shadow .12s;
    }
    .buscador__campo::placeholder { color: var(--color-rotulo); }
    .buscador__campo:focus { outline: none; border-color: var(--color-morado); box-shadow: 0 0 0 3px rgba(41, 18, 66, .12); }
    /* El aspa nativa, fuera: la de este componente la sustituye en todos los navegadores. */
    .buscador__campo::-webkit-search-cancel-button { appearance: none; }

    .buscador__limpiar {
      position: absolute; right: .35rem; top: 50%; transform: translateY(-50%);
      display: inline-flex; align-items: center; justify-content: center;
      width: 1.75rem; height: 1.75rem;
      border: 1px solid transparent; border-radius: 50%;
      background: transparent; color: var(--color-rotulo); cursor: pointer;
    }
    .buscador__limpiar:hover { background: #f1f3f7; color: var(--color-valor); }
    .buscador__limpiar:focus-visible { outline: none; border-color: var(--color-morado); box-shadow: 0 0 0 3px rgba(41, 18, 66, .12); }
    /* El texto no pasa por debajo del aspa. */
    .buscador__campo--con-aspa { padding-right: 2.3rem; }
  `],
  template: `
    @if (!etiquetaVisible()) {
      <label class="sr-only" [attr.for]="identificador()">{{ etiqueta() }}</label>
    }
    <svg class="buscador__lupa" lucideSearch [size]="14" aria-hidden="true"></svg>
    <input
      #campo
      class="buscador__campo"
      [class.buscador__campo--con-aspa]="valor().length > 0"
      type="search"
      [attr.id]="identificador()"
      [attr.name]="identificador()"
      [attr.data-testid]="identificador()"
      [attr.placeholder]="marcador()"
      [value]="valor()"
      (input)="alEscribir($event)"
      (keydown.escape)="alEscapar($event)"
      (keydown.enter)="emitirYa()" />
    @if (valor().length > 0) {
      <button type="button" class="buscador__limpiar"
        [attr.aria-label]="'Limpiar ' + etiqueta().toLowerCase()"
        [title]="'Limpiar ' + etiqueta().toLowerCase()"
        (click)="limpiar()">
        <svg lucideX [size]="14" aria-hidden="true"></svg>
      </button>
    }
  `,
})
export class BuscadorDeListaComponent {
  /** Lo que hay escrito. Lo guarda el panel: este componente no tiene estado propio. */
  readonly valor = input.required<string>();

  /** Qué se busca aquí, en palabras. Es el nombre accesible del campo y el de la «x». */
  readonly etiqueta = input.required<string>();

  /** El texto tenue de dentro del campo. */
  readonly marcador = input('Buscar…');

  /** El `id`, el `name` y el `data-testid` del campo, que en la consola son el mismo. */
  readonly identificador = input.required<string>();

  /**
   * La pantalla ya pinta un rótulo visible asociado a este campo con su propio `<label for>`.
   *
   * <b>ENTONCES ESTE COMPONENTE NO PONE EL SUYO.</b> Dos etiquetas para un mismo campo hacen que un
   * lector de pantalla lea el nombre dos veces, o que se queden con la que no toca. Donde el
   * buscador va suelto en una barra de lista —lo corriente— el rótulo no se ve y lo pone esta
   * pieza; donde va dentro de un formulario con su rótulo escrito, se declara aquí.
   */
  readonly etiquetaVisible = input(false);

  /**
   * Cuánto se espera tras la última tecla antes de avisar, en milisegundos.
   *
   * CERO PARA LO QUE SE FILTRA EN MEMORIA y 300 para lo que va al servidor. No hay un tercer valor
   * a elegir: con menos, una escritura normal manda varias peticiones; con más, se nota el retraso.
   */
  readonly espera = input(300);

  /** El texto, cuando la escritura se ha detenido. */
  readonly cambiar = output<string>();

  private readonly campo = viewChild.required<ElementRef<HTMLInputElement>>('campo');
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // UNA ESPERA PENDIENTE NO PUEDE SOBREVIVIR A LA PANTALLA que la lanzó: emitiría sobre un panel
    // que ya no existe.
    inject(DestroyRef).onDestroy(() => this.cancelar());
  }

  alEscribir(evento: Event): void {
    const texto = (evento.target as HTMLInputElement).value;
    this.cancelar();
    if (this.espera() <= 0) { this.cambiar.emit(texto); return; }
    this.temporizador = setTimeout(() => this.cambiar.emit(texto), this.espera());
  }

  /** Intro no es la forma de buscar, pero tampoco puede no hacer nada: adelanta la espera. */
  emitirYa(): void {
    this.cancelar();
    this.cambiar.emit(this.campo().nativeElement.value);
  }

  /**
   * Escape limpia el campo, Y SOLO SE DETIENE SI HABIA ALGO QUE LIMPIAR.
   *
   * Con el campo ya vacío la tecla sigue su camino y cierra la ventana que contenga al buscador,
   * que es lo que se espera. Si se detuviera siempre, un buscador vacío dejaría una ventana que no
   * cierra con Escape sin ninguna razón visible.
   *
   * SE MIRA LO QUE HAY ESCRITO, NO `valor()`: entre la tecla y el valor que devuelve quien nos usa
   * hay una espera, así que recién escrito `valor()` todavía está vacío y Escape no limpiaría nada.
   */
  alEscapar(evento: Event): void {
    const escrito = (evento.target as HTMLInputElement | null)?.value ?? this.valor();
    if (escrito.length === 0) return;
    evento.stopPropagation();
    this.limpiar();
  }

  limpiar(): void {
    this.cancelar();
    this.campo().nativeElement.value = '';
    this.cambiar.emit('');
    this.campo().nativeElement.focus();
  }

  private cancelar(): void {
    if (this.temporizador !== null) { clearTimeout(this.temporizador); this.temporizador = null; }
  }
}
