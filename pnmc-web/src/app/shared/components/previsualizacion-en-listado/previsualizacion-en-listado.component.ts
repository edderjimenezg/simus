import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, Input, ViewChild, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { LucideExternalLink } from '@lucide/angular';
import { ModuloPrevisualizable, PARAMETRO_DE_PREVISUALIZACION } from '../../../core/services/previsualizacion-en-listado.service';

/** Dónde vive el listado público de cada módulo. */
const LISTADO_PUBLICO: Record<ModuloPrevisualizable, string> = {
  noticias: '/noticias',
  agenda: '/agenda',
  'catalogo-editorial': '/editorial',
  festivales: '/ecosistema/festivales',
  mercados: '/ecosistema/mercados-musicales',
};

/** Ancho lógico del marco: un PC de escritorio, que es donde se mira un listado. */
const ANCHO_LOGICO = 1440;
const ALTO_LOGICO = 900;

/**
 * Ver un registro como se verá en su listado, antes de publicarlo.
 *
 * <b>QUE PROBLEMA RESUELVE.</b> La consola solo ofrecía «Ver en el portal» cuando el registro ya
 * estaba publicado: la única forma de saber cómo iba a quedar algo era publicarlo. Un titular que
 * se corta a dos líneas o una imagen mal encuadrada se descubrían con el registro ya en la calle.
 *
 * <b>SE CARGA LA PAGINA REAL EN UN MARCO</b>, igual que hace la previsualización del CMS desde el
 * 30 de agosto de 2026. No se reproduce la tarjeta: reproducirla sería tener dos diseños que se
 * separan en el primer cambio, y la previsualización dejaría de previsualizar sin que nadie se
 * entere. El registro entra por el mismo servicio que carga el listado —ver
 * `PrevisualizacionEnListadoService`— y se pinta con la misma tarjeta.
 *
 * <b>EL AVISO NO ES DECORATIVO.</b> Dentro del marco el registro se ve exactamente como se verá, y
 * eso incluye no parecer un borrador. Quien mira tiene que saber que todavía no está en la calle, y
 * eso se dice fuera del marco, que es el único sitio donde decirlo no falsea la previsualización.
 */
@Component({
  selector: 'app-previsualizacion-en-listado',
  standalone: true,
  imports: [CommonModule, LucideExternalLink],
  templateUrl: './previsualizacion-en-listado.component.html',
})
export class PrevisualizacionEnListadoComponent {
  private readonly sanitizer = inject(DomSanitizer);

  @Input({ required: true }) modulo: ModuloPrevisualizable = 'noticias';
  @Input({ required: true }) id: string | number = 0;

  /** Cómo se llama lo que se está mirando. Se escribe fuera del marco. */
  @Input() nombre = '';

  /** El estado en que está hoy, para decir si ya es público o todavía no. */
  @Input() estadoEtiqueta = '';

  /** `true` cuando el registro ya se ve en el portal sin previsualizar nada. */
  @Input() yaEsPublico = false;

  readonly ANCHO_LOGICO = ANCHO_LOGICO;
  readonly ALTO_LOGICO = ALTO_LOGICO;
  readonly anchoDisponible = signal(ANCHO_LOGICO);

  @ViewChild('panel') panel?: ElementRef<HTMLElement>;

  /** La dirección del listado público con el registro pedido. */
  readonly direccion = computed<string>(() =>
    `${LISTADO_PUBLICO[this.modulo]}?${PARAMETRO_DE_PREVISUALIZACION}=${this.modulo}:${this.id}`);

  /**
   * La misma dirección, marcada como segura para el `src` del marco.
   *
   * ES UNA RUTA PROPIA Y CONSTRUIDA AQUI —el módulo sale de una lista cerrada y el identificador de
   * la fila—, así que no hay entrada de nadie en ella. Angular igual exige el marcado explícito.
   */
  get direccionSegura(): SafeResourceUrl {
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.direccion());
  }

  /** Cuánto hay que encoger la página de 1440 para que quepa en el cajón. */
  readonly escala = computed(() => Math.min(1, this.anchoDisponible() / ANCHO_LOGICO));

  readonly altoPintado = computed(() => Math.round(ALTO_LOGICO * this.escala()));

  @HostListener('window:resize')
  alRedimensionar(): void {
    this.medir();
  }

  /** Mide el hueco disponible. Se llama al abrir y al cambiar el tamaño de la ventana. */
  medir(): void {
    const ancho = this.panel?.nativeElement.clientWidth;
    if (ancho && ancho > 0) this.anchoDisponible.set(ancho);
  }
}
