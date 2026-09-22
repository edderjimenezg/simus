import { CommonModule } from '@angular/common';
import { Component, Input, signal } from '@angular/core';
import {
  LucideArchive,
  LucideCircleCheckBig,
  LucideCircleX,
  LucideClock3,
  LucideMailQuestion,
  LucidePause,
  LucidePencilLine,
  LucideTrash2,
  LucideTriangleAlert,
} from '@lucide/angular';

/**
 * Los tonos que un estado puede tener, que son menos que los estados.
 *
 * <b>EL TONO NO ES EL ESTADO.</b> «Publicado» y «Activa» son estados distintos de módulos distintos
 * y comparten tono: algo terminado y en pie. Separarlos permite que cada módulo conserve su
 * vocabulario sin inventar un color propio cada vez.
 */
export type TonoDeEstado =
  /** Terminado y en pie: publicado, activa. */
  | 'logrado'
  /** En curso y esperando a otro: en revisión, pendiente de confirmación. */
  | 'en_curso'
  /** Requiere una acción de quien mira: ajustes solicitados. */
  | 'requiere_accion'
  /** Todavía no empieza: borrador. */
  | 'preliminar'
  /** Cerrado sin efecto: archivado, inactiva. */
  | 'cerrado'
  /** Terminado en contra: rechazado, eliminada. */
  | 'adverso';

/**
 * El indicador de estado de un registro: un icono, y el texto como alternativa.
 *
 * <b>POR QUE EXISTE.</b> está definido, y para todo el proyecto:
 * «No me gustan este tipo de píldoras en general en todo el diseño, reemplazarlas por iconografía y
 * un texto alternativo al ponerse encima o algo así». Las píldoras —recuadro redondeado de color con
 * el nombre del estado dentro— se habían vuelto el recurso por omisión en festivales, ediciones,
 * organizaciones, agenda, noticias y catálogo editorial, y en una tabla de treinta filas compiten
 * entre sí y con el contenido.
 *
 * <b>EL TEXTO NO SE PIERDE, CAMBIA DE CANAL.</b> Vive en el DOM como etiqueta visualmente oculta
 * —así lo lee cualquier tecnología asistiva sin depender de que interprete un <c>title</c>— y
 * aparece en pantalla al pasar por encima. Un icono mudo sería una regresión, no una mejora:
 * empeoraría la pantalla justo para quien más la necesita.
 *
 * <b>NO AÑADE PARADAS DE TABULACION, y es deliberado.</b> Hacer el indicador enfocable metería
 * treinta paradas nuevas en una tabla de treinta filas, y quien navega con teclado tendría que
 * atravesarlas para llegar a las acciones. El significado le llega igual por la etiqueta oculta.
 */
@Component({
  selector: 'app-indicador-de-estado',
  standalone: true,
  imports: [
    CommonModule,
    // CADA ICONO ES SU PROPIO COMPONENTE en @lucide/angular 1.x —el selector es un atributo sobre un
    // <svg>—, así que no hay forma de elegirlo por variable: se declaran los siete que usa el
    // indicador y la plantilla escoge con @switch. Es más largo de leer y es lo que compila.
    LucideCircleCheckBig,
    LucideClock3,
    LucideTriangleAlert,
    LucideArchive,
    LucideCircleX,
    LucidePencilLine,
    LucidePause,
    LucideTrash2,
    LucideMailQuestion,
  ],
  templateUrl: './indicador-de-estado.component.html',
})
export class IndicadorDeEstadoComponent {
  /** Lo que se lee: «Publicado», «Ajustes solicitados». Nunca el código en crudo. */
  @Input({ required: true }) etiqueta = '';

  @Input() tono: TonoDeEstado = 'preliminar';

  /**
   * Un matiz del icono cuando el tono no basta.
   *
   * `cerrado` sirve para «archivada» y para «inactiva», que se leen distinto: una guardó el registro
   * y la otra lo suspendió. Con esto el icono lo distingue sin inventar un tono más.
   */
  @Input() matiz: 'archivo' | 'pausa' | 'eliminado' | 'correo' | null = null;

  /** Si además del icono se escribe la etiqueta al lado. Para cabeceras, donde hay sitio. */
  @Input() conTexto = false;

  readonly encima = signal(false);

  /**
   * Qué icono le toca, como nombre y no como componente.
   *
   * LA PLANTILLA ESCOGE CON @switch. En @lucide/angular 1.x cada icono es un componente con selector
   * de atributo sobre un <svg>, así que no se puede enlazar por variable. El nombre se decide aquí
   * —un solo sitio— y allí solo se dibuja.
   */
  get icono(): 'logrado' | 'en_curso' | 'accion' | 'archivo' | 'adverso' | 'pausa' | 'eliminado' | 'correo' | 'preliminar' {
    if (this.matiz === 'archivo') return 'archivo';
    if (this.matiz === 'pausa') return 'pausa';
    if (this.matiz === 'eliminado') return 'eliminado';
    if (this.matiz === 'correo') return 'correo';
    switch (this.tono) {
      case 'logrado': return 'logrado';
      case 'en_curso': return 'en_curso';
      case 'requiere_accion': return 'accion';
      case 'cerrado': return 'archivo';
      case 'adverso': return 'adverso';
      default: return 'preliminar';
    }
  }

  /**
   * El color del icono.
   *
   * SOLO EL TRAZO, sin fondo ni borde: es justo lo que se pidió retirar. El color sigue diciendo de
   * un vistazo si algo va bien o pide atención, sin dibujar un rectángulo que compita con el texto.
   */
  get clase(): string {
    switch (this.tono) {
      case 'logrado': return 'text-emerald-600';
      case 'en_curso': return 'text-sky-600';
      case 'requiere_accion': return 'text-amber-600';
      case 'cerrado': return 'text-slate-500';
      case 'adverso': return 'text-rose-600';
      default: return 'text-slate-400';
    }
  }
}
