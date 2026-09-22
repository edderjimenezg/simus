import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/**
 * Qué papel cumple el botón en la pantalla.
 *
 * <b>NO SON COLORES, SON JERARQUIAS.</b> Quien escribe una pantalla elige qué importancia tiene la
 * acción, no qué morado lleva. Si mañana el morado de marca cambia, cambia aquí y en ningún otro
 * sitio; y mientras tanto nadie puede inventarse una quinta importancia sin pasar por este fichero.
 */
export type ImportanciaDeBoton =
  /** La acción que la pantalla existe para que ocurra. Una por pantalla, o ninguna. */
  | 'principal'
  /** Acciones de apoyo: cancelar, filtrar, exportar. Son la mayoría. */
  | 'secundaria'
  /** Acciones menores dentro de una fila o una tarjeta. Sin borde. */
  | 'terciaria'
  /** Confirma algo que ya se decidió: aprobar, publicar. */
  | 'confirmar'
  /** Destruye o retira. Se distingue A PROPOSITO del resto. */
  | 'destructiva';

/**
 * El tamaño del control.
 *
 * <b>LOS TRES CUMPLEN EL MINIMO DE WCAG 2.2 AA</b> (24x24 px, criterio 2.5.8). El más pequeño lo
 * cumple justo; «cómodo» son los 44 px que la propia norma señala como solución robusta en táctil.
 */
export type TamanoDeBoton = 'menudo' | 'normal' | 'comodo';

/**
 * El botón del proyecto.
 *
 * <b>POR QUE EXISTE, CON EL NUMERO QUE LO OBLIGO.</b> La auditoría
 * contó <b>503 botones con 266 firmas de clases distintas</b>, y <b>179 de esas firmas se usaban una
 * sola vez</b>. Las tres más repetidas cubrían 49 de 503. Es decir: no había un botón, había
 * doscientos sesenta y seis. Sin esto no se puede fijar en un solo sitio ni un tamaño mínimo de
 * letra ni un área de toque, porque cada botón los declaraba por su cuenta.
 *
 * <b>ESTE COMPONENTE NO REDISEÑA NADA.</b> Las variantes reproducen las firmas que ya dominaban el
 * código —`bg-morado` con versal y `tracking-wider` para la principal, `border-slate-200` para la
 * secundaria, `border-red-600` para la destructiva—. Cambiar el aspecto es una decisión de diseño
 * que se toma mirando la pantalla, no de madrugada dentro de un componente nuevo.
 *
 * <b>LO QUE SI GARANTIZA, Y ANTES NO PODIA GARANTIZARSE:</b>
 *
 * <list type="number">
 *   <item><b>Área de toque.</b> Alto mínimo tomado de los tokens del tema. Los botones de faceta del
 *   Catálogo medían 13 px de alto; aquí eso no se puede componer.</item>
 *   <item><b>`type="button"`.</b> Sin declararlo, un botón dentro de un formulario lo envía. El
 *   trinquete `botones_de_accion_sin_tipo` está en 0 y esto lo mantiene así sin vigilancia.</item>
 *   <item><b>Un solo envío.</b> Mientras `ocupado` esté en cierto, el botón no vuelve a emitir. El
 *   doble clic que crea dos registros deja de ser posible por construcción.</item>
 *   <item><b>Nombre accesible.</b> Un botón de solo icono sin `etiqueta` es un botón que un lector de
 *   pantalla anuncia como «botón», y ya.</item>
 * </list>
 */
/**
 * Las clases de un botón del proyecto, como función pura.
 *
 * Existe aparte del componente porque hay botones que no pueden ser `app-boton`: el disparador
 * del menú de acciones lleva `aria-haspopup`, `aria-expanded` y teclas de flecha que el componente
 * no reenvía. Con esto se visten igual sin copiar la lista de clases, que es como dos botones
 * hermanos acaban midiendo 28 y 30 píxeles.
 */
export function clasesDeBoton(importancia: ImportanciaDeBoton, tamano: TamanoDeBoton, opciones: { aloAncho?: boolean; pulsado?: boolean | null } = {}): string {
  const base =
    'inline-flex items-center justify-center gap-2 rounded-xl font-bold transition ' +
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado-vivo focus-visible:ring-offset-2 ' +
    'disabled:cursor-not-allowed disabled:opacity-50';

  const porTamano: Record<TamanoDeBoton, string> = {
    // El mínimo estricto de la norma. Para acciones dentro de una fila densa.
    menudo: 'min-h-control-minimo px-3 py-1 text-dato',
    // El de siempre: reproduce el `px-4 py-2.5 text-xs` que ya dominaba el código.
    normal: 'min-h-control px-4 py-2.5 text-dato',
    // Táctil holgado: formularios, diálogos, móvil.
    comodo: 'min-h-control-comodo px-5 py-3 text-cuerpo',
  };

  // TODAS LLEVAN BORDE, AUNQUE SEA TRANSPARENTE. Sin esto, una principal y una
  // secundaria puestas en la misma fila median 37 y 39 px de alto: los dos
  // píxeles del borde que una tenía y la otra no. Comprobado el 14 de septiembre
  // de 2026 en el pie de la revisión de un Festival.
  const porImportancia: Record<ImportanciaDeBoton, string> = {
    // SIN VERSALITAS NI PESO NEGRO. Reproducían la firma que dominaba el código
    // antes de existir este componente, pero puesta en una fila junto a una
    // secundaria y una destructiva —«Enviar sugerencias de ajuste», «Publicar
    // Festival», «Rechazar registro»— la principal era la única en mayúsculas
    // y la única a 900: tres botones hermanos con tres tratamientos de texto.
    // Lo que la distingue es el relleno, que ya basta.
    principal: 'border border-morado bg-morado text-white hover:border-morado-claro hover:bg-morado-claro',
    secundaria: 'border border-slate-200 text-slate-600 hover:border-morado hover:text-morado',
    terciaria: 'border border-transparent text-slate-600 hover:text-morado',
    confirmar: 'border border-emerald-600 bg-emerald-600 text-white hover:bg-emerald-700',
    destructiva: 'border border-red-600 text-red-700 hover:bg-red-50',
  };

  return [
    base,
    porTamano[tamano],
    porImportancia[importancia],
    opciones.aloAncho ? 'w-full' : '',
    opciones.pulsado ? 'bg-morado/8 border-morado text-morado' : '',
  ].filter(Boolean).join(' ');
}

@Component({
  selector: 'app-boton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="tipo()"
      [disabled]="deshabilitado() || ocupado()"
      [attr.aria-label]="etiqueta() || null"
      [attr.title]="etiqueta() || null"
      [attr.data-testid]="identificador()"
      [attr.aria-busy]="ocupado() ? 'true' : null"
      [attr.aria-pressed]="pulsado() === null ? null : (pulsado() ? 'true' : 'false')"
      [class]="clases()"
      (click)="pulsar()">
      <!--
        EL INDICADOR DE ESPERA NO CAMBIA EL ANCHO. Un botón que se encoge al enviar mueve lo que
        tiene al lado justo cuando la persona acaba de apuntar ahí. Por eso el contenido se queda y
        el indicador se le añade delante.
      -->
      @if (ocupado()) {
        <span class="boton__espera" aria-hidden="true"></span>
      }
      <ng-content />
    </button>
  `,
  styles: [`
    :host { display: contents; }
    /* La animación de espera respeta a quien pidió menos movimiento. */
    .boton__espera {
      width: 0.85em; height: 0.85em; flex: 0 0 auto;
      border: 2px solid currentColor; border-right-color: transparent;
      border-radius: 50%; animation: boton-girar 0.7s linear infinite;
    }
    @keyframes boton-girar { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) {
      .boton__espera { animation: none; opacity: 0.6; }
    }
  `],
})
export class BotonComponent {
  readonly importancia = input<ImportanciaDeBoton>('secundaria');
  readonly tamano = input<TamanoDeBoton>('normal');
  readonly tipo = input<'button' | 'submit'>('button');
  readonly deshabilitado = input(false);

  /** Hay una operación en curso: se muestra el indicador y NO se vuelve a emitir. */
  readonly ocupado = input(false);

  /**
   * El nombre accesible cuando el botón no lleva texto visible.
   *
   * <b>VIAJA COMO `aria-label` Y COMO `title`.</b> El primero lo resuelve para quien usa lector de
   * pantalla; el segundo, para quien ve el icono y duda: sin él, un botón de solo icono obliga a
   * pulsarlo para saber qué hace. Un comentario no cabe entre los atributos de la plantilla —rompe
   * la compilación—, así que la razón vive aquí.
   */
  readonly etiqueta = input<string | null>(null);

  /**
   * El `data-testid` del botón REAL, no del anfitrión.
   *
   * Hace falta porque el anfitrión es `display: contents`: un `data-testid` puesto desde fuera
   * quedaría en un elemento que no recibe el clic ni tiene `disabled`, y una prueba que lo buscara
   * comprobaría un cascarón. Con esto, migrar un botón escrito a mano a este componente no obliga a
   * reescribir la prueba que ya lo vigilaba.
   */
  readonly identificador = input<string | null>(null);

  /** Ocupa todo el ancho disponible. Útil en móvil y dentro de un diálogo. */
  readonly aloAncho = input(false);

  /**
   * El botón es un conmutador: «Pendientes primero», «Ocultar borradores».
   *
   * `null` —lo corriente— no pone `aria-pressed` y el botón es una acción. `true`/`false` lo ponen
   * y, cuando está pulsado, el botón se rellena para que se vea que está activo sin cambiar de
   * importancia: un conmutador secundario pulsado sigue siendo secundario.
   */
  readonly pulsado = input<boolean | null>(null);

  readonly accion = output<void>();

  pulsar(): void {
    // LA GUARDA ESTA AQUI Y NO SOLO EN EL ATRIBUTO: `disabled` se puede quitar desde fuera, y entre
    // que la petición sale y el estado cambia hay una ventana en la que el segundo clic entra.
    if (this.deshabilitado() || this.ocupado()) { return; }
    this.accion.emit();
  }

  /**
   * Las clases de la variante elegida.
   *
   * COMUNES A TODAS: `inline-flex` centrado para que icono y texto queden alineados sin trucos,
   * `min-h-*` del tema para el área de toque, y un foco visible propio —porque quitarlo sin reponerlo
   * fue justo uno de los hallazgos de la auditoría—.
   */
  readonly clases = computed(() => clasesDeBoton(this.importancia(), this.tamano(), {
    aloAncho: this.aloAncho(), pulsado: this.pulsado(),
  }));
}
