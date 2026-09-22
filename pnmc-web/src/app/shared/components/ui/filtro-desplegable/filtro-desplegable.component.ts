import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideChevronDown, LucideSearch } from '@lucide/angular';

/** Una opción del filtro: su valor, lo que se lee, y cuántos registros hay detrás. */
export interface OpcionDeFiltro {
  readonly id: string;
  readonly etiqueta: string;
  /** Cuántos hay. Opcional: un filtro de «orden» no cuenta nada. */
  readonly conteo?: number;
}

/**
 * Un filtro de lista, con su panel propio.
 *
 * <b>POR QUE NO UN `<select>`.</b> Los once filtros de la consola eran desplegables nativos, y el
 * dirección de producto reportó que «al hacer clic sobre algunos filtros,
 * los desplegables o paneles resultantes son desproporcionadamente grandes frente al contenido que
 * contienen». Comprobado: el control en sí ya era proporcional —entre 118 y 217 px según su texto— pero
 * el panel que abre un `<select>` lo dibuja el sistema operativo, y con los 32 departamentos de
 * DIVIPOLA ocupa media pantalla. De un `<select>` no se puede gobernar nada de eso: ni el ancho, ni
 * el alto, ni el interlineado, ni dónde aparece, ni si trae buscador.
 *
 * <b>EL TAMAÑO LO PIDE EL CONTENIDO.</b> El panel mide lo que mide su opción más larga, con un
 * mínimo para que no quede un sello y un máximo para que no se coma la pantalla; el alto crece
 * hasta unas diez opciones y a partir de ahí aparece el scroll, no antes. Y se ancla al control
 * que lo abrió, alineado por su borde izquierdo.
 *
 * <b>EL BUSCADOR APARECE CUANDO HACE FALTA</b>, no siempre: con cuatro estados sobra, con treinta y
 * dos departamentos es la diferencia entre elegir y rebuscar. El umbral está en
 * `MINIMO_PARA_BUSCAR`.
 *
 * <b>SEMANTICA Y TECLADO.</b> `combobox` + `listbox`, como pide el patrón: flechas para moverse,
 * Enter para elegir, Escape para cerrar volviendo el foco al control, Inicio y Fin para los
 * extremos. Un filtro que solo funcione con ratón deja fuera a quien navega con teclado, y este
 * sustituye a un nativo que sí funcionaba así.
 */
@Component({
  selector: 'app-filtro-desplegable',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, LucideChevronDown, LucideSearch],
  styles: [`
    :host { display: inline-block; position: relative; }

    .filtro__control {
      display: inline-flex; align-items: center; gap: .45rem;
      min-height: var(--spacing-control); max-width: 18rem;
      border: 1px solid #dfe3ea; border-radius: .7rem; background: #fff;
      padding: .45rem .7rem;
      color: var(--color-valor); font-size: var(--text-cuerpo); font-family: inherit;
      cursor: pointer; text-align: left;
    }
    .filtro__control:hover { border-color: #c3cad8; }
    .filtro__control:focus-visible { outline: none; border-color: var(--color-morado); box-shadow: 0 0 0 3px rgba(41, 18, 66, .12); }
    .filtro__control[aria-expanded="true"] { border-color: var(--color-morado); }
    .filtro__valor { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .filtro__flecha { flex: 0 0 auto; color: var(--color-rotulo); transition: transform .12s; }
    .filtro__control[aria-expanded="true"] .filtro__flecha { transform: rotate(180deg); }

    /* EL PANEL MIDE LO QUE PIDE SU CONTENIDO, entre un mínimo y un máximo: con max-content, una
       lista de opciones cortas da un panel corto, que es justo lo que no hacía el nativo.
       SIN ACENTOS GRAVES EN ESTE BLOQUE: va dentro de un literal de plantilla y uno solo lo cierra,
       con lo que el resto del CSS pasa a leerse como expresión y el compilador de Angular falla
       con «Failed to resolve styles at position 1». Costó un build entender de dónde salía. */
    .filtro__panel {
      position: absolute; z-index: 60; top: calc(100% + .35rem); left: 0;
      width: max-content; min-width: 100%; max-width: 22rem;
      border: 1px solid var(--color-filete); border-radius: .7rem; background: #fff;
      box-shadow: 0 10px 30px rgba(20, 26, 45, .12);
      padding: .3rem;
    }
    /* El scroll aparece cuando de verdad hace falta: diez opciones caben sin él. */
    .filtro__lista { max-height: 19rem; overflow-y: auto; margin: 0; padding: 0; list-style: none; }
    .filtro__buscador { position: relative; padding: .25rem .25rem .35rem; }
    .filtro__buscador svg { position: absolute; left: .85rem; top: 50%; transform: translateY(-50%); color: var(--color-rotulo); pointer-events: none; }
    .filtro__buscador input {
      width: 100%; min-height: var(--spacing-control);
      border: 1px solid #dfe3ea; border-radius: .5rem; background: #fff;
      padding: .35rem .6rem .35rem 2rem;
      color: var(--color-valor); font-size: var(--text-cuerpo); font-family: inherit;
    }
    .filtro__buscador input:focus-visible { outline: none; border-color: var(--color-morado); }

    .filtro__opcion {
      display: flex; align-items: center; justify-content: space-between; gap: 1.5rem;
      width: 100%; min-height: var(--spacing-control);
      border: 0; border-radius: .45rem; background: none;
      padding: .35rem .6rem;
      color: var(--color-valor); font-size: var(--text-cuerpo); font-family: inherit; text-align: left;
      cursor: pointer;
    }
    .filtro__opcion:hover, .filtro__opcion.esta-marcada { background: #f3f4f8; }
    .filtro__opcion[aria-selected="true"] { color: var(--color-morado); font-weight: 700; }
    .filtro__conteo { flex: 0 0 auto; color: var(--color-rotulo); font-size: var(--text-dato); font-variant-numeric: tabular-nums; }
    .filtro__vacio { padding: .6rem; color: var(--color-rotulo); font-size: var(--text-cuerpo); }
  `],
  template: `
    <button type="button" class="filtro__control"
      [attr.aria-expanded]="abierto()" aria-haspopup="listbox"
      [attr.aria-label]="etiqueta()"
      [attr.data-filtro]="identificador() || null"
      (click)="alternar()">
      <span class="filtro__valor">{{ textoDelElegido() }}</span>
      <svg lucideChevronDown [size]="14" class="filtro__flecha" aria-hidden="true"></svg>
    </button>

    @if (abierto()) {
      <div class="filtro__panel">
        @if (opciones().length >= MINIMO_PARA_BUSCAR) {
          <div class="filtro__buscador">
            <svg lucideSearch [size]="13" aria-hidden="true"></svg>
            <input #buscador type="search" [attr.aria-label]="'Buscar en ' + etiqueta()"
              [ngModel]="busqueda()" (ngModelChange)="busqueda.set($event)" name="buscar-en-filtro" />
          </div>
        }
        <ul class="filtro__lista" role="listbox" [attr.aria-label]="etiqueta()">
          @for (opcion of visibles(); track opcion.id; let i = $index) {
            <li>
              <button type="button" class="filtro__opcion" role="option"
                [class.esta-marcada]="i === marcada()"
                [attr.aria-selected]="opcion.id === elegida()"
                (click)="elegir(opcion.id)" (mouseenter)="marcada.set(i)">
                <span>{{ opcion.etiqueta }}</span>
                @if (opcion.conteo !== undefined) { <span class="filtro__conteo">{{ opcion.conteo }}</span> }
              </button>
            </li>
          } @empty {
            <li class="filtro__vacio">Ninguna coincide.</li>
          }
        </ul>
      </div>
    }
  `,
  host: {
    '(document:click)': 'alPulsarFuera($event)',
    // EL TECLADO SE ESCUCHA EN EL ANFITRION, no en el panel. El panel es un `div` sin foco propio:
    // colgarle un `keydown` obliga a que el evento llegue burbujeando desde dentro, y una regla de
    // accesibilidad lo señala con razón —un manejador de teclado sobre algo que no puede recibir
    // foco es, casi siempre, un control inalcanzable—. Aquí el foco está siempre en un descendiente
    // real (el disparador, el buscador o una opción), así que el sitio honesto es el anfitrión.
    '(keydown)': 'alTeclear($event)',
  },
})
export class FiltroDesplegableComponent {
  /** A partir de cuántas opciones aparece el buscador. Con menos, rebuscar es más lento que mirar. */
  readonly MINIMO_PARA_BUSCAR = 9;

  readonly opciones = input.required<readonly OpcionDeFiltro[]>();
  readonly elegida = input.required<string>();
  /** Qué se está filtrando, para quien navega con lector de pantalla. */
  readonly etiqueta = input.required<string>();
  /** Nombre con el que una prueba localiza este filtro, si ya lo buscaba por atributo. */
  readonly identificador = input<string | null>(null);

  readonly cambiar = output<string>();

  protected readonly abierto = signal(false);
  protected readonly busqueda = signal('');
  /** Qué opción está bajo el cursor o el teclado, para mover con flechas sin ratón. */
  protected readonly marcada = signal(0);

  private readonly anfitrion: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly buscador = viewChild<ElementRef<HTMLInputElement>>('buscador');

  protected readonly visibles = computed(() => {
    const termino = this.busqueda().trim().toLocaleLowerCase('es-CO');
    if (!termino) { return this.opciones(); }
    return this.opciones().filter(o => o.etiqueta.toLocaleLowerCase('es-CO').includes(termino));
  });

  protected readonly textoDelElegido = computed(() => {
    const elegida = this.elegida();
    return this.opciones().find(o => o.id === elegida)?.etiqueta ?? this.etiqueta();
  });

  constructor() {
    // El foco va al buscador en cuanto se abre: quien abre un filtro de treinta y dos opciones
    // viene a escribir, no a recorrerlas.
    effect(() => {
      if (this.abierto()) { queueMicrotask(() => this.buscador()?.nativeElement.focus()); }
    });
  }

  protected alternar(): void {
    const abriendo = !this.abierto();
    this.abierto.set(abriendo);
    if (abriendo) {
      this.busqueda.set('');
      this.marcada.set(Math.max(0, this.visibles().findIndex(o => o.id === this.elegida())));
    }
  }

  protected elegir(id: string): void {
    this.cambiar.emit(id);
    this.cerrar();
  }

  private cerrar(devolverElFoco = true): void {
    if (!this.abierto()) { return; }
    this.abierto.set(false);
    this.busqueda.set('');
    // EL FOCO VUELVE AL CONTROL. Si se queda en un elemento que acaba de desaparecer, el siguiente
    // tabulador empieza desde el principio del documento.
    if (devolverElFoco) {
      this.anfitrion.nativeElement.querySelector<HTMLButtonElement>('.filtro__control')?.focus();
    }
  }

  /**
   * Una sola puerta para el teclado: cerrado, abre; abierto, navega.
   *
   * Con dos manejadores —uno en el disparador y otro en el panel— la flecha abajo que abre podía
   * llegar también al que navega y saltarse la primera opción.
   */
  protected alTeclear(evento: KeyboardEvent): void {
    if (!this.abierto()) {
      if (evento.key !== 'ArrowDown' && evento.key !== 'Enter' && evento.key !== ' ') { return; }
      evento.preventDefault();
      this.alternar();
      return;
    }
    this.alNavegar(evento);
  }

  private alNavegar(evento: KeyboardEvent): void {
    const total = this.visibles().length;
    switch (evento.key) {
      case 'Escape':
        evento.preventDefault();
        this.cerrar();
        break;
      case 'ArrowDown':
        evento.preventDefault();
        if (total) { this.marcada.set((this.marcada() + 1) % total); }
        break;
      case 'ArrowUp':
        evento.preventDefault();
        if (total) { this.marcada.set((this.marcada() - 1 + total) % total); }
        break;
      case 'Home':
        evento.preventDefault();
        this.marcada.set(0);
        break;
      case 'End':
        evento.preventDefault();
        this.marcada.set(Math.max(0, total - 1));
        break;
      case 'Enter': {
        evento.preventDefault();
        const opcion = this.visibles()[this.marcada()];
        if (opcion) { this.elegir(opcion.id); }
        break;
      }
    }
  }

  protected alPulsarFuera(evento: MouseEvent): void {
    if (!this.abierto()) { return; }
    if (this.anfitrion.nativeElement.contains(evento.target as Node)) { return; }
    // Sin devolver el foco: quien pulsa fuera está yendo a otra parte.
    this.cerrar(false);
  }
}
