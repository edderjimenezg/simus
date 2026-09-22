import { CommonModule } from '@angular/common';
import { Component, ElementRef, Input, ViewChild, signal } from '@angular/core';
import { LucideMessageCircleQuestion, LucideX } from '@lucide/angular';
import { ConsultaGuiadaComponent } from './consulta-guiada.component';
import { DialogoDirective } from '../../directives/dialogo.directive';
import { ContextoDeConsulta } from './consulta-guiada.service';

/**
 * La Consulta Guiada a un clic, desde cualquier pantalla del espacio de trabajo.
 *
 * <b>ES LO QUE LA VUELVE TRANSVERSAL DE VERDAD.</b> Tenerla en una sección la deja donde nadie la
 * busca: quien está revisando organizaciones y se pregunta «¿cuántas hay sin correo confirmado?»
 * no abandona lo que está haciendo para ir a «Análisis y consultas», reformular la pregunta y
 * volver. Con el cajón, pregunta donde está, y el contexto de esa pantalla viaja con la pregunta.
 *
 * <b>NO SE MONTA HASTA QUE SE ABRE.</b> El componente de dentro pide el catálogo al servidor en su
 * arranque; montarlo con la pantalla haría una petición en cada carga de la consola para un panel
 * que casi nadie abre en esa visita.
 *
 * <b>ES UN CAJON Y NO UN MODAL DE PANTALLA COMPLETA</b> a propósito: la respuesta casi siempre se
 * contrasta con lo que hay detrás -«dice 3 pendientes, ¿cuáles son estos?»-, y taparlo obligaría a
 * memorizar la cifra para cerrarla y volver a mirar.
 */
@Component({
  selector: 'app-cajon-de-consulta-guiada',
  standalone: true,
  imports: [CommonModule, ConsultaGuiadaComponent, DialogoDirective, LucideMessageCircleQuestion, LucideX],
  templateUrl: './cajon-de-consulta-guiada.component.html',
})
export class CajonDeConsultaGuiadaComponent {
  /** La ruta base, que decide el ámbito. Ver {@link ConsultaGuiadaComponent}. */
  @Input({ required: true }) base = '';

  /** Desde dónde se pregunta. La pantalla que lo monta lo mantiene al día. */
  @Input() contexto: ContextoDeConsulta | undefined;

  @Input() saludo = 'Puedo consultar cifras agregadas y enseñarte de dónde sale cada una.';
  @Input() limite = 'Solo lectura. El asistente no identifica personas ni puede modificar, aprobar o publicar registros.';

  /**
   * Dónde vive el botón que abre el cajón.
   *
   * «flotante» es lo de siempre: fijo en la esquina inferior derecha. «cabecera» lo deja como un
   * botón normal en el flujo, para que quien lo monta lo coloque donde no se superponga a nada. Ver
   * el comentario de la plantilla: flotando tapaba la columna de acciones de la tabla del Catálogo.
   */
  @Input() anclaje: 'flotante' | 'cabecera' = 'flotante';

  readonly abierto = signal(false);

  /** Las clases del lanzador según el anclaje. Mismo aspecto; lo que cambia es si flota. */
  clasesDelLanzador(): string {
    const comunes =
      'flex items-center gap-2 rounded-full border border-morado/20 bg-morado text-white ' +
      'font-bold uppercase tracking-widest transition hover:bg-morado-claro ' +
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2';
    return this.anclaje === 'cabecera'
      ? `${comunes} min-h-control px-3 py-2 text-dato`
      // Redondo y de 3,5rem: cabe en la esquina sin comerse una columna de la tabla.
      : `${comunes} fixed bottom-6 right-6 z-[2000] h-14 w-14 justify-center p-0 shadow-lg`;
  }

  @ViewChild('panel') panel?: ElementRef<HTMLElement>;

  private disparador: HTMLElement | null = null;

  abrir(evento: Event): void {
    this.disparador = evento.target as HTMLElement;
    this.abierto.set(true);
    // EL FOCO LO LLEVA AHORA LA DIRECTIVA, Y POR UN MOTIVO CONCRETO. Aquí había un
    // queueMicrotask(() => this.panel?.nativeElement.focus()) que parecía resolverlo y no lo
    // resolvía: el microtask corre ANTES de que Angular renderice el bloque @if, así que el panel
    // todavía era undefined y el foco se quedaba en el botón de origen. Medido en el navegador el
    // 13 de septiembre de 2026: a los 3.500 ms el elemento activo seguía siendo ese botón. La
    // directiva usa afterNextRender, que es el momento en que el panel ya existe.
  }

  cerrar(): void {
    if (!this.abierto()) return;
    this.abierto.set(false);
    this.disparador = null;
    // LA VUELTA DEL FOCO Y EL ESCAPE LOS LLEVA LA DIRECTIVA. Se retiraron de aquí el
    // HostListener de 'document:keydown.escape' y el this.disparador?.focus(): escuchar Escape en el
    // documento entero desde un componente compartido hacía que un cajón abierto reaccionara a
    // teclas dirigidas a otro overlay, y tener dos sitios que devuelven el foco es tener dos sitios
    // donde uno de los dos se queda atrás.
  }
}
