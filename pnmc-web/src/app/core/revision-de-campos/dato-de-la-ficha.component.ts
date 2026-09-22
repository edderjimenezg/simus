import { Component, Input, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideCheck, LucideCircleAlert, LucidePencilLine, LucideTrash2 } from '@lucide/angular';
import { BotonComponent } from '../../shared/components/ui/boton/boton.component';
import { ObservacionDeCampo } from './revision-de-campos';
import { RevisionDeCamposStore } from './revision-de-campos.store';

/**
 * Un campo de la ficha en modo lectura, con el control para sugerir un ajuste sobre él.
 *
 * <b>QUÉ SUSTITUYE.</b> Cuarenta y siete renglones idénticos de
 * `ficha-festival.component.html`, todos de esta forma:
 *
 * ```html
 * <div><dt class="…">Nombre del festival</dt><dd class="…">{{ texto(c.nombre) }}</dd></div>
 * ```
 *
 * <b>POR QUÉ UN COMPONENTE Y NO EL CONTROL PEGADO EN CADA UNO.</b> Porque son cuarenta y siete, y
 * el pedido del usuario es «en todos los campos poder pedir cambios
 * puntuales». Escrito a mano, el día que cambie el aspecto del control hay cuarenta y siete sitios
 * que tocar, y el que se olvide no falla: se queda con el aspecto viejo.
 *
 * <b>EL SELECTOR ES UN ATRIBUTO SOBRE UN `div`, Y ESO NO ES ESTILO.</b> El contenido de un `<dl>`
 * solo admite `<dt>`/`<dd>` o `<div>` que los agrupe; un elemento propio en medio rompe la relación
 * entre el rótulo y el valor, que es justo lo que un lector de pantalla usa para leer «Nombre del
 * festival: Festival de la Candelaria». Con el selector de atributo el DOM queda EXACTAMENTE igual
 * al que había antes de este componente.
 *
 * <b>EL ALMACÉN SE PIDE OPCIONAL.</b> Sin él —la ficha montada en una prueba, o en cualquier sitio
 * que no sea uno de los dos paneles— pinta el rótulo y el valor y nada más. Que la revisión no esté
 * disponible no puede impedir leer la ficha.
 *
 * <b>LOS TRES MODOS, Y QUÉ PINTA CADA UNO:</b>
 * <ul>
 *   <li>`lectura`  — rótulo y valor. Idéntico a lo que había antes.</li>
 *   <li>`revision` — además, una sugerencia de ajuste y su nota, si la hay.</li>
 *   <li>`atencion` — además, la nota recibida y la casilla para marcarla atendida.</li>
 * </ul>
 */
@Component({
  // NO ES UN ELEMENTO PROPIO, Y ES LO UNICO QUE PUEDE SER. `@angular-eslint/component-selector`
  // exige `type: "element"` en todo el proyecto y esta es la unica excepcion; el motivo no es de
  // estilo: el contenido de un `<dl>` solo admite `<dt>`/`<dd>` o `<div>` que los agrupe, y un
  // elemento propio en medio rompe la relacion entre el rotulo y el valor —que es justo lo que un
  // lector de pantalla usa para leer «Nombre del festival: Festival de la Candelaria»—. Se apaga
  // aqui y en esta linea, no en la configuracion: la regla sigue valiendo para todo lo demas.
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'div[app-dato-de-la-ficha]',
  standalone: true,
  imports: [BotonComponent, FormsModule, LucideCheck, LucideCircleAlert, LucidePencilLine, LucideTrash2],
  host: { '[class]': 'clases()' },
  styles: [`
    /* ── LOS MISMOS TAMAÑOS QUE EL RESUMEN DEL TRAMITE ─────────────────────
       Comprobado: el resumen del trámite se pintaba con
       7 combinaciones de tamaño/peso/familia/color y la ficha completa del mismo
       trámite con 23, para decir exactamente lo mismo. La dirección de producto
       zanjó cuál manda: «me gustan los tamaños usados en la vista de resumen de
       la solicitud».

       Aquí eso son tres reglas: el rótulo a 12 px en caja normal —no en
       versalitas, que es lo que hacía que un rótulo de dato pesara como un
       título—, el valor a 16 px y el texto corrido a 14. Y los grises salen de
       los papeles declarados en \`styles.css\`, no de la escala \`slate\`, que es
       de donde venía la disparidad. */
    .dato__rotulo {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: .15rem .55rem;
      color: var(--color-rotulo);
      font-size: var(--text-dato);
      line-height: 1.3;
    }

    .dato__valor {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: .5rem;
      margin: .2rem 0 0;
      color: var(--color-valor);
      font-size: var(--text-lectura);
      font-weight: 600;
      line-height: 1.35;
    }

    .dato__antes { margin: .35rem 0 0; color: var(--color-rotulo); font-size: var(--text-cuerpo); line-height: 1.5; }
    .dato__antes span { font-weight: 700; }

    /* LA NOTA, SIN FILETE DE COLOR AL COSTADO. Con cuarenta y siete campos, una
       línea de color por cada nota convertía la ficha en un peine: «un montón de
       secciones, una línea izquierda de color innecesaria». Lo que dice si el
       ajuste está sugerido o atendido es el icono y su rótulo, que ya están. */
    /* LA NOTA, PEGADA A SU CAMPO Y CON SUS ACCIONES AL LADO. Ocupaba 44 rem de
       ancho, así que el lápiz y la papelera acababan a cuatro dedos del final
       del texto, alineados con el borde de la columna y no con la nota. */
    .dato__nota { margin: .45rem 0 0; max-width: 34rem; }
    /* EL AVISO NO PUEDE SER MAS PEQUEÑO QUE LO QUE ANUNCIA. «Ajuste sugerido»
       iba a 12 px encima de una nota de 16: el rótulo pesaba menos que su propio
       texto y el bloque se leía al revés. Sube al escalón intermedio, que es el
       de los avisos y las marcas de estado. */
    .dato__nota-titulo {
      display: flex;
      align-items: center;
      gap: .4rem;
      margin: 0;
      color: #8a5a12;
      font-size: var(--text-cuerpo);
      font-weight: 700;
    }
    .dato__nota-titulo.es-atendida { color: var(--color-verde-texto); }
    .dato__nota-texto { margin: .15rem 0 0; color: var(--color-prosa); font-size: var(--text-lectura); line-height: 1.5; }

    /* UN SOLO CONTROL DE ICONO, Y CON AREA DE TOQUE. Eran cuatro firmas
       distintas —20, 20, 28 y 28 px de lado, con radios de 4, 6 y 999— y dos de
       ellas por debajo del mínimo de 24x24 que fija WCAG 2.2 (criterio 2.5.8).
       Aquí se declaran una vez, a la medida mínima de control del proyecto. */
    .dato__icono {
      display: inline-flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: var(--spacing-control-minimo);
      height: var(--spacing-control-minimo);
      border: 0;
      border-radius: .4rem;
      background: none;
      color: #8a5a12;
      cursor: pointer;
      transition: background .12s, color .12s;
    }
    .dato__icono:hover { background: rgba(41, 18, 66, .07); }
    .dato__icono:focus-visible { outline: 2px solid var(--color-morado-claro); outline-offset: 1px; }
    .dato__icono.es-atendida { color: var(--color-verde-texto); }
    .dato__icono.es-morado, .dato__icono.es-anotar { color: var(--color-morado-claro); }
    .dato__icono.es-anotar { margin-top: -.1rem; opacity: .55; }
    .dato__icono.es-anotar:hover, .dato__icono.es-anotar:focus-visible { opacity: 1; }
    .dato__icono.es-retirar { color: #9c4630; }

    /* ── El editor del comentario, en el flujo ───────────────────────────── */
    /* Sin filete al costado tampoco aquí: el rótulo en morado y el borde de la
       propia caja ya dicen dónde se está escribiendo, y solo hay un editor
       abierto a la vez. */
    .dato__editor { margin: .55rem 0 .2rem; max-width: 34rem; }
    .dato__editor-rotulo {
      display: block;
      margin-bottom: .35rem;
      color: var(--color-morado);
      font-size: var(--text-cuerpo);
      font-weight: 700;
    }
    .dato__editor-texto {
      width: 100%;
      border: 1px solid #dde1e8;
      border-radius: .6rem;
      background: #fff;
      padding: .5rem .65rem;
      color: var(--color-valor);
      font-size: var(--text-lectura);
      line-height: 1.5;
      resize: vertical;
    }
    .dato__editor-texto:focus { border-color: var(--color-morado); outline: none; }
    .dato__editor-acciones { display: flex; flex-wrap: wrap; gap: .4rem; margin-top: .5rem; }
  `],
  template: `
    <dt class="dato__rotulo">
      <span>{{ etiqueta }}</span>
      @if (mostrarIndicador(); as observacion) {
        <button type="button" (click)="seleccionar(observacion)"
                [attr.aria-label]="'Revisar ajuste sugerido para ' + etiqueta"
                [title]="observacion.estado === 'atendida' ? 'Ajuste resuelto' : 'Este campo tiene un ajuste sugerido'"
                class="dato__icono" [class.es-atendida]="observacion.estado === 'atendida'">
          @if (observacion.estado === 'atendida') {
            <svg lucideCheck [size]="16" aria-hidden="true"></svg>
          } @else {
            <svg lucideCircleAlert [size]="16" aria-hidden="true"></svg>
          }
        </button>
      }
      <!--
        NI PILDORA NI 9,6 PX. Esto era una cápsula rellena con el texto a 0,6 rem, dos cosas que el
        proyecto tiene descartadas: las píldoras de estado —se marcan con icono y texto— y el suelo
        de 12 px de la escala tipográfica. Corregido al revisar el
        apartado de observaciones, y vale para los cuarenta y siete campos de una vez.
      -->
      @if (cambioDelEnvio(); as cambio) {
        <span [attr.data-testid]="'cambio-envio-' + campo"
              [title]="cambio.respondeAjusteSugerido ? 'Este campo cambió para atender una sugerencia' : 'Este campo también cambió desde el envío anterior'"
              [class]="cambio.respondeAjusteSugerido ? 'text-morado' : 'text-sky-800'"
              class="inline-flex items-center gap-1.5 text-cuerpo font-bold normal-case tracking-normal">
          @if (cambio.respondeAjusteSugerido) {
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5 shrink-0" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="m4 10 4 4 8-8" /></svg>
          } @else {
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8" class="h-3.5 w-3.5 shrink-0" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="M4 10h12M12 6l4 4-4 4" /></svg>
          }
          {{ cambio.respondeAjusteSugerido ? 'Ajuste atendido' : 'Cambio adicional' }}
        </span>
      }
    </dt>
    <!--
      EL CONTROL DE ANOTAR VA AL FINAL DEL VALOR, NO DELANTE.

      Delante solo aparecía en los campos SIN nota, así que el valor empezaba 1,2 rem más a la
      derecha en unos campos que en otros y la columna quedaba dentada. Al final, y en el mismo
      sitio donde están el lápiz y la papelera cuando la nota ya existe, los valores arrancan todos
      en la misma vertical y las acciones de un campo están siempre en el mismo borde.
    -->
    <dd class="dato__valor">
      <span>{{ valor }}</span>
      @if (sePuedeAnotar() && !nota()) {
        <button type="button" [attr.data-testid]="'pedir-cambio-' + campo" (click)="abrir()"
                [attr.aria-label]="'Sugerir ajuste en ' + etiqueta" title="Sugerir ajuste"
                class="dato__icono es-anotar">
          <svg lucidePencilLine [size]="16" aria-hidden="true"></svg>
        </button>
      }
    </dd>

    @if (cambioDelEnvio(); as cambio) {
      <p class="dato__antes">
        <span>Antes:</span> {{ cambio.valorAnterior || 'Sin información' }}
        <span class="sr-only">. Ahora: {{ cambio.valorRecibido || 'Sin información' }}</span>
      </p>
    }

    @if (nota(); as escrita) {
      @if (!mostrarIndicador()) {
      <div [attr.data-testid]="'nota-' + campo"
           class="dato__nota">
        <div class="flex items-start gap-3">
          <div class="min-w-0 flex-1">
            <!--
              UN ICONO DIBUJADO, NO UN GLIFO DE TEXTO. «✎» y «✓» se pintan con la tipografía del
              sistema: no tienen grosor de trazo que ajustar, cambian de forma según la plataforma
              y no casan con los demás iconos de la consola, que son de Lucide.
            -->
            <p class="dato__nota-titulo" [class.es-atendida]="escrita.estado === 'atendida'">
              @if (escrita.estado === 'atendida') {
                <svg lucideCheck [size]="16" aria-hidden="true"></svg>
              } @else {
                <svg lucidePencilLine [size]="16" aria-hidden="true"></svg>
              }
              {{ escrita.estado === 'atendida' ? 'Ajuste atendido' : 'Ajuste sugerido' }}
            </p>
            <p class="dato__nota-texto">{{ escrita.nota }}</p>
          </div>

          @if (sePuedeAnotar()) {
            <div class="flex shrink-0 items-center gap-0.5" aria-label="Acciones de la sugerencia">
              <button type="button" [attr.data-testid]="'editar-nota-' + campo" (click)="abrir()"
                      [attr.aria-label]="'Modificar sugerencia de ' + etiqueta" title="Modificar sugerencia"
                      class="dato__icono es-morado">
                <svg lucidePencilLine [size]="16" aria-hidden="true"></svg>
              </button>
            <button type="button" [attr.data-testid]="'quitar-nota-' + campo" (click)="quitar()"
                    [attr.aria-label]="'Quitar sugerencia de ' + etiqueta" title="Quitar sugerencia"
                    class="dato__icono es-retirar">
              <svg lucideTrash2 [size]="16" aria-hidden="true"></svg>
            </button>
            </div>
          }
        </div>

        @if (esAtencion()) {
          <label class="mt-2 flex items-start gap-2 text-slate-700">
            <input type="checkbox" [attr.data-testid]="'atender-' + campo"
                   [checked]="escrita.estado === 'atendida'"
                   (change)="atender(escrita.id, $any($event.target))"
                   class="mt-0.5 h-4 w-4 rounded border-slate-300 text-verde-texto focus:ring-verde-texto" />
            <!-- Con varias casillas en la misma pantalla, «Ya lo corregi» a secas no dice cual. -->
            <span>Ya lo corregí<span class="sr-only">: {{ etiqueta }}</span></span>
          </label>
        }
      </div>
      }
    }

    <!--
      DEJAR UN COMENTARIO OCURRE EN EL SITIO, NO EN UN GLOBO OSCURO.

      Esto era un panel flotante morado oscuro con acentos verde esmeralda, texto blanco a 12 px y
      un botón verde relleno: cuatro decisiones de color que no existen en ninguna otra parte de la
      consola, un quinto lenguaje de botón, y un z-[100] con min(23rem, 100vw - 3rem) para
      esquivar los bordes. quedó definido: «la manera
      en como se dejan los comentarios […] no está tan bien depurada».

      Ahora el editor se abre DEBAJO del campo, en el flujo y sobre la misma superficie. Sin capas,
      sin cálculos de posición y con los controles del proyecto. Y sin repetir el valor registrado:
      está justo encima, a dos renglones.
    -->
    @if (abierta()) {
      <div class="dato__editor">
        <label [attr.for]="'nota-campo-' + campo" class="dato__editor-rotulo">Qué hay que ajustar en «{{ etiqueta }}»</label>
        <textarea [attr.id]="'nota-campo-' + campo" [attr.data-testid]="'caja-nota-' + campo"
                  rows="2" [(ngModel)]="borrador" class="dato__editor-texto"></textarea>
        <div class="dato__editor-acciones">
          <app-boton [identificador]="'guardar-nota-' + campo" importancia="principal" tamano="menudo" (accion)="confirmar()">Guardar sugerencia</app-boton>
          <app-boton [identificador]="'cancelar-nota-' + campo" importancia="terciaria" tamano="menudo" (accion)="cerrar()">Cancelar</app-boton>
        </div>
      </div>
    }
  `,
})
export class DatoDeLaFichaComponent {
  /** El identificador del campo. Empieza por `festival.` o por `edicion.`: de ahí sale el ámbito. */
  @Input({ required: true }) campo!: string;

  /** La sección de la ficha donde vive. Es lo que agrupa las notas en el resumen de cada paso. */
  @Input({ required: true }) seccion!: string;

  /** El rótulo que se ve, y el que viaja con la nota como evidencia de sobre qué se pidió. */
  @Input({ required: true }) etiqueta!: string;

  /** Ya viene formateado: la ficha lo pasa por `texto()`, `lista()`, `siNo()` o `etiquetaCobertura()`. */
  @Input() valor = '';

  /** `2` para los campos que ocupan la fila entera, como hacía `class="sm:col-span-2"`. */
  @Input() ancho: 1 | 2 = 1;

  private readonly almacen = inject(RevisionDeCamposStore, { optional: true });

  borrador = '';

  // YA NO HACE FALTA `relative`: el editor vive en el flujo y no se ancla a nada.
  readonly clases = computed(() => this.ancho === 2 ? 'sm:col-span-2' : '');

  readonly nota = computed(() => this.almacen?.nota(this.campo));
  readonly cambioDelEnvio = computed(() => this.almacen?.esRevision() ? this.almacen.cambioDelEnvio(this.campo) : undefined);

  readonly mostrarIndicador = computed(() => {
    const nota = this.nota();
    return this.almacen?.presentacionAtencion() === 'indicador' ? nota : null;
  });

  readonly esAtencion = computed(() =>
    this.almacen?.esAtencion() === true && !this.almacen.resolucionAutomatica());

  /** El funcionario puede escribir: la solicitud todavía no salió hacia la organización. */
  readonly sePuedeAnotar = computed(() => this.almacen?.esRevision() === true && !this.almacen.yaEnviada());

  readonly abierta = computed(() => this.almacen?.campoEnEdicion() === this.campo);

  abrir(): void {
    if (!this.almacen) return;
    this.borrador = this.almacen.nota(this.campo)?.nota ?? '';
    this.almacen.campoEnEdicion.set(this.campo);
  }

  cerrar(): void {
    this.almacen?.campoEnEdicion.set(null);
  }

  confirmar(): void {
    this.almacen?.escribir({
      campoId: this.campo,
      seccionId: this.seccion,
      campoEtiqueta: this.etiqueta,
      // EL VALOR VIAJA COPIADO. Cuando la organización corrija el campo, lo de hoy deja de existir
      // —justo porque se pidió cambiarlo—, y el expediente tiene que poder decir sobre qué se pidió.
      valorObservado: this.valor || null,
    }, this.borrador);
  }

  quitar(): void {
    this.almacen?.quitar(this.campo);
  }

  atender(observacionId: number, casilla: HTMLInputElement): void {
    this.almacen?.atender(observacionId, casilla);
  }

  seleccionar(nota: ObservacionDeCampo): void {
    this.almacen?.seleccionarNota(nota);
  }
}
