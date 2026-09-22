import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  CategoriaDeContenido,
  CategoriasDeContenidoService,
} from '../../../../core/services/categorias-de-contenido.service';

/**
 * El desplegable de categoría temática que usan Agenda, Noticias y Catálogo Editorial.
 *
 * <b>ES UNA PIEZA Y NO TRES PORQUE LA REGLA ES UNA.</b> Pedir las categorías de un módulo trae
 * también las comunes; esa regla vive en el servidor, y repetir aquí la llamada en cada panel solo
 * multiplicaba las ocasiones de escribirla mal en uno de ellos.
 *
 * <b>LO ROTULA QUIEN LO EMBEBE.</b> Recibe su `id` por entrada y las tres pantallas que lo usan
 * —Agenda, Noticias y Catálogo Editorial— pintan su propio `<label for>` con la tipografía de su
 * módulo. Por eso lleva `data-rotulo-externo`: el trinquete no puede ver un rótulo que está en otro
 * fichero, y la marca declara que existe. Si alguna pantalla deja de ponerlo, la marca miente, así
 * que el `<label>` es parte del contrato de usar esta pieza.
 *
 * <b>SI NO HAY NINGUNA, LO DICE Y EXPLICA DONDE SE CREAN.</b> Un desplegable vacío sin explicación
 * parece un fallo de carga; decir que se administran en «Categorías temáticas» convierte el hueco
 * en una instrucción.
 */
@Component({
  selector: 'app-selector-de-categoria',
  standalone: true,
  imports: [CommonModule],
  template: `
    <select [id]="idCampo" data-rotulo-externo
            [value]="categoriaId ?? ''"
            (change)="elegir($event)"
            [attr.aria-describedby]="categorias().length === 0 ? idCampo + '-ayuda' : null"
            class="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-morado">
      <option value="">Sin categoría</option>
      @for (categoria of categorias(); track categoria.id) {
        <option [value]="categoria.id">
          {{ categoria.nombreCategoria }}{{ categoria.codigoModulo === 'comun' ? ' · común' : '' }}
        </option>
      }
    </select>

    @if (cargando()) {
      <small class="mt-1 block text-dato text-slate-400">Consultando categorías…</small>
    } @else if (error()) {
      <small class="mt-1 block text-dato text-amber-700" role="alert">{{ error() }}</small>
    } @else if (categorias().length === 0) {
      <small [id]="idCampo + '-ayuda'" class="mt-1 block text-dato text-slate-400">
        Todavía no hay categorías para este módulo. Se crean en «Categorías temáticas».
      </small>
    }
  `,
})
export class SelectorDeCategoriaComponent {
  private readonly api = inject(CategoriasDeContenidoService);

  /** agenda · noticias · editorial. Las comunes llegan siempre, las ponga quien las ponga. */
  @Input({ required: true }) set modulo(valor: string) {
    this.moduloActual = valor;
    void this.cargar();
  }

  @Input() categoriaId: number | null = null;
  @Input() idCampo = 'categoria';
  @Output() categoriaIdChange = new EventEmitter<number | null>();

  private moduloActual = '';

  readonly categorias = signal<CategoriaDeContenido[]>([]);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);

  private async cargar(): Promise<void> {
    if (!this.moduloActual) { return; }
    this.cargando.set(true);
    this.error.set(null);

    const resultado = await this.api.listar(this.moduloActual);
    if (resultado.ok && resultado.data) {
      this.categorias.set(resultado.data.items);
    } else {
      this.categorias.set([]);
      this.error.set(resultado.error ?? 'No fue posible consultar las categorías.');
    }

    this.cargando.set(false);
  }

  elegir(evento: Event): void {
    const valor = (evento.target as HTMLSelectElement).value;
    // VACIO ES `null` Y NO `0`: «sin categoría» es la ausencia de una, y un cero viajaría al
    // servidor como un identificador que ninguna categoría tiene.
    this.categoriaIdChange.emit(valor ? Number(valor) : null);
  }
}
