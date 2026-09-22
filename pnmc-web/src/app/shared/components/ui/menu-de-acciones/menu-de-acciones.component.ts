import { CommonModule } from '@angular/common';
import { LucideEllipsis } from '@lucide/angular';
import { clasesDeBoton } from '../boton/boton.component';
import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
  signal,
} from '@angular/core';

/**
 * Una acción disponible sobre un registro.
 *
 * <b>LA LISTA SE DERIVA DEL ESTADO, NO SE PINTA ENTERA.</b> Quien construye el menú decide qué
 * acciones existen hoy para esta fila: una acción que no se puede hacer no se ofrece desactivada,
 * simplemente no está. `deshabilitada` se reserva para lo que se podría hacer pero ahora mismo no
 * —un envío en curso, una fila bloqueada—, y entonces `motivo` explica por qué.
 */
export interface AccionDeRegistro {
  /** Lo que se emite al elegirla. */
  id: string;
  /** Lo que lee la persona. Un verbo: «Publicar», no «Publicación». */
  etiqueta: string;
  /**
   * El peso de la acción.
   *
   * `principal` es la que se espera que se use casi siempre y sale también como botón suelto;
   * `normal` vive en el desplegable; `peligro` es la que no se puede deshacer y va separada al
   * final, en rojo.
   */
  tono?: 'principal' | 'normal' | 'peligro';
  /** Si la acción es un enlace —«Ver en el portal»— en vez de una orden al componente. */
  enlace?: string;
  /** Para un enlace que sale del panel. */
  nuevaPestana?: boolean;
  deshabilitada?: boolean;
  /** Por qué está deshabilitada, para el `title`. */
  motivo?: string;
}

/**
 * El menú de acciones de una fila o de una ficha.
 *
 * <b>POR QUÉ EXISTE.</b> Las listas de gestión —agenda, noticias, catálogo editorial, ediciones—
 * pintaban entre tres y siete botoncitos por fila, todos del mismo tamaño y del mismo color, en una
 * celda que se desbordaba en cuanto la pantalla se estrechaba. El criterio es este: «botones con lista desplegable de acciones o algo así, que mejore la
 * experiencia». Con un solo control por fila, la tabla vuelve a leerse y la acción que de verdad
 * importa —publicar, editar— queda destacada en vez de perdida entre seis iguales.
 *
 * <b>UN SOLO CONTROL POR FILA.</b> Hasta la acción marcada como
 * `principal` salía fuera como un segundo botón. Con nueve tablas usando esta pieza, eso dejaba
 * cada columna de acciones distinta en cada fila —unas con dos controles y otras con uno— y con
 * anchos que cambiaban según el verbo: «Revisar Festival» no mide lo que «Publicar». Ahora lo
 * único que se ve es el disparador, siempre igual y siempre en el mismo sitio; la acción esperada
 * es la primera opción del menú y se lee destacada.
 *
 * Lo que se pierde es un clic en la acción frecuente. Lo que se gana: una columna que se recorre
 * con la vista, nueve píxeles de ancho devueltos a los datos, y una decisión —publicar, revisar—
 * que deja de tomarse desde una lista sin ver el registro.
 */
@Component({
  selector: 'app-menu-de-acciones',
  standalone: true,
  imports: [CommonModule, LucideEllipsis],
  templateUrl: './menu-de-acciones.component.html',
})
export class MenuDeAccionesComponent {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  @Input({ required: true }) acciones: AccionDeRegistro[] = [];

  /**
   * Qué registro describe el menú, para quien no ve la pantalla.
   *
   * «Acciones» repetido treinta veces en una tabla no orienta a nadie; «Acciones de Festival del
   * Agua» sí. Va al `aria-label` del disparador.
   */
  @Input() descripcion = '';

  /** El rótulo del disparador. Se cambia cuando «Acciones» no es la palabra justa. */
  @Input() etiqueta = 'Acciones';

  /** Alinea el desplegable a la derecha del disparador, que es lo natural al final de una fila. */
  @Input() alineacion: 'izquierda' | 'derecha' = 'derecha';

  @Output() elegida = new EventEmitter<string>();

  readonly abierto = signal(false);

  /** La acción principal y el disparador se visten como los botones del proyecto, del tamaño de fila. */
  readonly clasesDeBoton = clasesDeBoton;

  /** La que se pinta fuera del menú, si la hay. */
  get principal(): AccionDeRegistro | undefined {
    return this.acciones.find(accion => accion.tono === 'principal');
  }

  /** Las que viven dentro del desplegable, en el orden en que llegaron. */
  get secundarias(): AccionDeRegistro[] {
    return this.acciones.filter(accion => accion.tono !== 'principal' && accion.tono !== 'peligro');
  }

  /** Las irreversibles, separadas al final. */
  get peligrosas(): AccionDeRegistro[] {
    return this.acciones.filter(accion => accion.tono === 'peligro');
  }

  /** Si no queda nada para el desplegable, el disparador no se pinta. */
  get hayAcciones(): boolean {
    return this.acciones.length > 0;
  }

  alternar(): void {
    this.abierto.update(valor => !valor);
  }

  cerrar(devolverFoco = false): void {
    if (!this.abierto()) return;
    this.abierto.set(false);
    if (devolverFoco) {
      this.host.nativeElement.querySelector<HTMLButtonElement>('[data-disparador-acciones]')?.focus();
    }
  }

  elegir(accion: AccionDeRegistro): void {
    if (accion.deshabilitada) return;
    // EL FOCO VUELVE AL DISPARADOR ANTES DE EMITIR. La opción pulsada desaparece con el desplegable;
    // si el foco se queda en ella, la ventana que la acción abre —el historial, la ficha— toma como
    // «quien la abrió» un elemento que ya no existe y, al cerrarse, el foco cae al cuerpo de la
    // página. Medido al cerrar el historial de un mercado con Escape.
    this.cerrar(true);
    this.elegida.emit(accion.id);
  }

  /**
   * Recorre las opciones con las flechas.
   *
   * SE BUSCAN EN EL DOM Y NO EN EL ARREGLO porque el arreglo incluye la acción principal, que vive
   * fuera del menú, y las deshabilitadas, que no deben recibir el foco.
   */
  mover(evento: Event, direccion: 1 | -1): void {
    evento.preventDefault();
    const opciones = Array.from(
      this.host.nativeElement.querySelectorAll<HTMLElement>('[data-opcion-accion]:not([disabled])'),
    );
    if (opciones.length === 0) return;
    const actual = opciones.indexOf(document.activeElement as HTMLElement);
    const siguiente = (actual + direccion + opciones.length) % opciones.length;
    opciones[siguiente].focus();
  }

  /** Un clic fuera cierra el menú: es lo que espera cualquiera que lo abrió por error. */
  @HostListener('document:click', ['$event'])
  alHacerClicFuera(evento: MouseEvent): void {
    if (!this.abierto()) return;
    if (!this.host.nativeElement.contains(evento.target as Node)) this.cerrar();
  }

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
  alPulsarEscape(evento: Event): void {
    if (!this.abierto()) return;
    evento.stopPropagation();
    this.cerrar(true);
  }
}
