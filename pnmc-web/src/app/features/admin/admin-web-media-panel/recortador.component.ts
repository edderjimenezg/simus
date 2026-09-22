import { CommonModule } from '@angular/common';
import { Component, computed, ElementRef, input, output, signal, viewChild } from '@angular/core';
import {
  CALIDAD,
  type ClaseDeMarco,
  type Medida,
  type Recorte,
  acercar,
  acotar,
  alcanzaParaElMarco,
  encajeInicial,
  franjaCentral,
  recortar,
  salidaDe,
} from './recorte';

/** Lo que el recortador devuelve cuando la persona confirma. */
export interface RecorteHecho {
  archivo: Blob;
  miniatura: Blob | null;
  ancho: number;
  alto: number;
}

/**
 * Ajustar una imagen dentro del marco de su ranura.
 *
 * <b>QUÉ SE VE, y por qué así.</b> El marco se pinta a su relación real y la imagen se mueve
 * DEBAJO de él, no al revés. La persona no está recortando un rectángulo sobre una foto: está
 * colocando una foto dentro de un hueco que ya existe y que no puede cambiar. Es la misma
 * operación mental que enmarcar, y la pantalla la refleja.
 *
 * <b>SE APLICA EL MISMO FILTRO QUE EL SITIO, Y SOBRE SU MISMO FONDO.</b> La portada del Home sale
 * con <c>grayscale(1)</c> al 30 % sobre <c>#291242</c>; el banner, con
 * <c>brightness(0.5) saturate(0.7)</c>. Si aquí se viera a todo color sobre gris, la persona
 * elegiría bien una imagen que en el sitio se ve de otra manera. Comprobado: los tratamientos del
 * sitio son distintos entre sí y ninguno es neutro.
 *
 * <b>LO QUE NO SE PROMETE.</b> En las ranuras de clase <c>variable</c> —las portadas de página,
 * cuya altura es <c>40vh</c>— la relación cambia con la ventana del visitante, entre 4,00 y 4,44.
 * El recortador lo dice en pantalla en vez de fingir un marco exacto: se recorta a la más ancha y
 * el sitio ajusta el alto.
 */
@Component({
  selector: 'app-recortador',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-3">

      <!-- El marco, a su relación real -->
      <div class="space-y-1.5">
        <div class="flex items-baseline justify-between gap-3">
          <p class="text-dato font-black uppercase tracking-wider text-slate-500">
            Ajuste la imagen dentro del marco
          </p>
          <p data-testid="medidas-de-salida" class="font-mono text-dato text-slate-400">
            {{ salida().w }}×{{ salida().h }} px
          </p>
        </div>

        <div
          #escenario
          data-testid="escenario"
          class="relative w-full cursor-grab overflow-hidden rounded-xl select-none active:cursor-grabbing"
          [style.background]="fondo()"
          [style.aspect-ratio]="marco().w + ' / ' + marco().h"
          (pointerdown)="empezarArrastre($event)"
          (pointermove)="arrastrar($event)"
          (pointerup)="soltar($event)"
          (pointercancel)="soltar($event)"
          (wheel)="rueda($event)"
        >
          @if (urlDeLaImagen(); as url) {
            <img
              [src]="url"
              alt=""
              draggable="false"
              class="pointer-events-none absolute origin-top-left max-w-none"
              [style.width.%]="100 / recorte().w"
              [style.height.%]="100 / recorte().h"
              [style.left.%]="-recorte().x * 100 / recorte().w"
              [style.top.%]="-recorte().y * 100 / recorte().h"
              [style.filter]="filtro()"
              [style.opacity]="opacidad()"
            />
          }

          <!-- La franja que el estado plegado ve siempre, en las ranuras de dos marcos -->
          @if (clase() === 'dos-marcos') {
            <div
              data-testid="franja-plegada"
              class="pointer-events-none absolute inset-y-0 border-x-2 border-dashed border-verde/70"
              [style.left.%]="(100 - anchoDeLaFranja()) / 2"
              [style.width.%]="anchoDeLaFranja()"
            ></div>
          }

          <!-- Donde el sitio pinta texto encima -->
          @if (zonaDeTexto(); as zona) {
            <div
              data-testid="zona-de-texto"
              class="pointer-events-none absolute rounded bg-white/10 outline outline-1 outline-dashed outline-white/40"
              [style.left.%]="zona.x"
              [style.top.%]="zona.y"
              [style.width.%]="zona.w"
              [style.height.%]="zona.h"
            ></div>
          }
        </div>

        <!--
          LA LEYENDA DEL RECUADRO. Sin ella, el rectangulo punteado es un adorno inexplicable:
          hay que decir que ahi el sitio pinta el titular encima, o la persona centra la cara
          justo debajo de el.
        -->
        @if (zonaDeTexto()) {
          <p data-testid="leyenda-zona-de-texto" class="text-dato leading-relaxed text-slate-400">
            El recuadro marca dónde el sitio pinta el titular encima. Deje fuera de él lo que
            quiera que se vea.
          </p>
        }
        @if (clase() === 'dos-marcos') {
          <p data-testid="leyenda-franja" class="text-dato leading-relaxed text-slate-400">
            Las líneas verticales marcan la franja que se ve cuando el bloque está plegado.
          </p>
        }
      </div>

      <!-- Acercamiento -->
      <div class="flex items-center gap-3">
        <span class="text-dato font-bold uppercase tracking-wider text-slate-400">Acercar</span>
        <input
          type="range"
          aria-label="Acercamiento de la imagen"
          data-testid="acercamiento"
          class="h-1 flex-1 cursor-pointer accent-morado"
          min="1"
          max="4"
          step="0.02"
          [value]="nivel()"
          (input)="cambiarNivel($any($event.target).valueAsNumber)"
        />
        <button
          type="button"
          data-testid="reencuadrar"
          (click)="reencuadrar()"
          class="cursor-pointer rounded-lg border border-slate-200 px-2 py-1 text-dato font-bold text-slate-600 hover:bg-slate-50"
        >Reencuadrar</button>
      </div>

      <!-- Lo que la persona necesita saber antes de guardar -->
      @if (aviso(); as texto) {
        <p data-testid="aviso-del-recorte" class="rounded-lg bg-amber-50 px-3 py-2 text-dato leading-relaxed text-amber-800">
          {{ texto }}
        </p>
      }

      <div class="flex flex-wrap gap-2">
        <button
          type="button"
          data-testid="confirmar-recorte"
          [disabled]="!puedeGuardar() || trabajando()"
          (click)="confirmar()"
          class="cursor-pointer rounded-lg bg-morado px-3 py-1.5 text-dato font-bold text-white transition-colors hover:bg-[#3d1c66] disabled:opacity-50"
        >{{ trabajando() ? 'Procesando…' : 'Usar esta imagen' }}</button>
        <button
          type="button"
          data-testid="cancelar-recorte"
          (click)="cancelado.emit()"
          class="cursor-pointer rounded-lg border border-slate-200 px-3 py-1.5 text-dato font-bold text-slate-600 hover:bg-slate-50"
        >Cancelar</button>
      </div>
    </div>
  `,
})
export class RecortadorComponent {
  /** El archivo que la persona acaba de elegir. */
  readonly archivo = input.required<File>();

  /** El marco de la ranura, medido con el navegador sobre el sitio. */
  readonly marco = input.required<Medida>();

  readonly clase = input<ClaseDeMarco>('fija');

  /** El filtro CSS con el que el sitio pinta esta ranura. Se aplica aquí igual. */
  readonly filtro = input<string>('none');
  readonly opacidad = input<number>(1);

  /**
   * El color que el sitio tiene DEBAJO de la imagen.
   *
   * No es decoración del recortador. La portada del Home va al 30 % de opacidad sobre
   * <c>#291242</c>: sobre un gris oscuro cualquiera, la misma foto se ve gris y en el sitio se
   * ve morada. Con opacidad menor que 1, el fondo es la mitad del resultado.
   */
  readonly fondo = input<string>('#0f172a');

  /** La segunda relación, en las ranuras que se pintan en dos marcos a la vez. */
  readonly relacionPlegada = input<number | null>(null);

  /**
   * Dónde cae el texto que el sitio pinta encima, en porcentaje del marco. Comprobado: el banner lo
   * lleva en x 61 %–97 %, así que el sujeto tiene que quedar a la izquierda.
   */
  readonly zonaDeTexto = input<{ x: number; y: number; w: number; h: number } | null>(null);

  readonly listo = output<RecorteHecho>();
  readonly cancelado = output<void>();

  private readonly escenario = viewChild<ElementRef<HTMLElement>>('escenario');

  readonly urlDeLaImagen = signal<string | null>(null);
  readonly natural = signal<Medida>({ w: 0, h: 0 });
  readonly recorte = signal<Recorte>({ x: 0, y: 0, w: 1, h: 1 });
  readonly trabajando = signal(false);

  private imagen: HTMLImageElement | null = null;
  private arrastrandoDesde: { x: number; y: number } | null = null;

  /** El recorte más alejado posible: el encaje. Nunca se puede pasar de aquí. */
  readonly minimo = computed(() => encajeInicial(this.natural(), this.marco()));

  readonly salida = computed(() => salidaDe(this.marco(), this.natural(), this.recorte()));

  readonly nivel = computed(() => {
    const m = this.minimo().w;
    return m === 0 ? 1 : m / this.recorte().w;
  });

  readonly anchoDeLaFranja = computed(() => {
    const plegada = this.relacionPlegada();
    if (!plegada) { return 100; }
    return franjaCentral(this.marco().w / this.marco().h, plegada) * 100;
  });

  readonly puedeGuardar = computed(() =>
    this.natural().w > 0 && alcanzaParaElMarco(this.natural(), this.marco()));

  readonly aviso = computed(() => {
    const natural = this.natural();
    if (natural.w === 0) { return null; }

    if (!alcanzaParaElMarco(natural, this.marco())) {
      // BLOQUEA, no avisa. Fue la decisión de producto, y el caso que la motivó llevaba meses en
      // el árbol sin que nada lo dijera.
      return `Esta imagen mide ${natural.w}×${natural.h} y el hueco necesita al menos `
        + `${this.marco().w}×${this.marco().h}. Se vería ampliada y borrosa. Use una más grande.`;
    }

    if (this.clase() === 'variable') {
      return 'Este hueco cambia de alto según la ventana de quien visita el sitio. Se recorta a la '
        + 'proporción más ancha; en pantallas altas se verá un poco más de imagen arriba y abajo.';
    }

    if (this.clase() === 'dos-marcos') {
      return 'Esta imagen se ve entera cuando el bloque está abierto y solo en la franja marcada '
        + 'cuando está plegado. Deje dentro de la franja lo que quiera que se vea siempre.';
    }

    return null;
  });

  constructor() {
    // El archivo llega por entrada y no cambia mientras el recortador está abierto: se lee una vez.
    queueMicrotask(() => void this.cargar());
  }

  private async cargar(): Promise<void> {
    const url = URL.createObjectURL(this.archivo());
    const img = new Image();
    await new Promise<void>(resolver => {
      img.onload = () => resolver();
      img.onerror = () => resolver();
      img.src = url;
    });

    this.imagen = img;
    this.urlDeLaImagen.set(url);
    this.natural.set({ w: img.naturalWidth, h: img.naturalHeight });
    this.recorte.set(this.minimo());
  }

  reencuadrar(): void {
    this.recorte.set(this.minimo());
  }

  cambiarNivel(nivel: number): void {
    const minimo = this.minimo();
    const actual = this.recorte();
    const centro = { x: actual.x + actual.w / 2, y: actual.y + actual.h / 2 };
    const objetivo = Math.max(1, nivel);
    // Se calcula desde el encaje y no acumulando, para que arrastrar la barra a un sitio dé
    // siempre el mismo resultado sin importar por dónde pasó.
    const w = minimo.w / objetivo;
    this.recorte.set(acotar({
      x: centro.x - w / 2,
      y: centro.y - (minimo.h / objetivo) / 2,
      w,
      h: minimo.h / objetivo,
    }));
  }

  rueda(evento: WheelEvent): void {
    evento.preventDefault();
    const caja = this.escenario()?.nativeElement.getBoundingClientRect();
    if (!caja) { return; }
    const actual = this.recorte();
    const centro = {
      x: actual.x + ((evento.clientX - caja.left) / caja.width) * actual.w,
      y: actual.y + ((evento.clientY - caja.top) / caja.height) * actual.h,
    };
    this.recorte.set(acercar(actual, evento.deltaY < 0 ? 1.12 : 1 / 1.12, centro, this.minimo()));
  }

  empezarArrastre(evento: PointerEvent): void {
    (evento.target as HTMLElement).setPointerCapture?.(evento.pointerId);
    this.arrastrandoDesde = { x: evento.clientX, y: evento.clientY };
  }

  arrastrar(evento: PointerEvent): void {
    if (!this.arrastrandoDesde) { return; }
    const caja = this.escenario()?.nativeElement.getBoundingClientRect();
    if (!caja) { return; }

    const actual = this.recorte();
    // El desplazamiento va en la MISMA dirección que el dedo: se arrastra la imagen, no el marco.
    const dx = ((evento.clientX - this.arrastrandoDesde.x) / caja.width) * actual.w;
    const dy = ((evento.clientY - this.arrastrandoDesde.y) / caja.height) * actual.h;
    this.arrastrandoDesde = { x: evento.clientX, y: evento.clientY };

    this.recorte.set(acotar({ ...actual, x: actual.x - dx, y: actual.y - dy }));
  }

  soltar(evento: PointerEvent): void {
    (evento.target as HTMLElement).releasePointerCapture?.(evento.pointerId);
    this.arrastrandoDesde = null;
  }

  async confirmar(): Promise<void> {
    if (!this.imagen || !this.puedeGuardar()) { return; }
    this.trabajando.set(true);

    const salida = this.salida();
    // Se guarda en gris lo que el sitio va a pintar en gris: pesa un 56 % menos y el visitante ve
    // lo mismo, porque el CSS lo iba a desaturar igual.
    const grises = this.filtro().includes('grayscale(1)');

    const archivo = await recortar(this.imagen, this.recorte(), salida, { calidad: CALIDAD, grises });
    const miniatura = await recortar(
      this.imagen, this.recorte(),
      { w: 320, h: Math.max(1, Math.round((320 * salida.h) / salida.w)) },
      { calidad: 0.8, grises });

    this.trabajando.set(false);
    if (archivo) {
      this.listo.emit({ archivo, miniatura, ancho: salida.w, alto: salida.h });
    }
  }
}
