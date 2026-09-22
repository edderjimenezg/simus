import { LucideCircleCheck } from '@lucide/angular';
import { CommonModule } from '@angular/common';
import { Component, ElementRef, Input, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ConsultaGuiadaService,
  ContextoDeConsulta,
  EstadoDeConsultaGuiada,
  TablaDeConsulta,
} from './consulta-guiada.service';

/** Un turno de la conversación: lo que se preguntó o lo que se respondió. */
export interface TurnoDeConsulta {
  papel: 'usuario' | 'asistente';
  texto: string;
  consulta: TablaDeConsulta | null;
  generadoEn: string | null;
  resueltoPor: string | null;
  contexto: string | null;
  esError: boolean;
}

/**
 * La Consulta Guiada, en cualquier sitio donde haga falta.
 *
 * <b>POR QUE ES UN COMPONENTE Y NO UNA PANTALLA.</b> Hasta el asistente
 * estaba escrito dentro de la plantilla de «Análisis y consultas» y solo existía ahí. El plan de
 * consolidación lo declaró capacidad transversal: la misma pregunta —«¿qué me falta?»— se hace desde
 * Organizaciones, desde una ficha de Festival o desde el espacio de una organización, y copiar
 * cuarenta líneas de plantilla en cada sitio garantiza que dentro de tres cortes haya cuatro
 * versiones distintas.
 *
 * <b>NO SABE A QUIEN LE PREGUNTA.</b> Recibe la ruta base y el contexto; el ámbito, los permisos y
 * lo que se puede responder los decide el servidor. Así el mismo componente sirve a la consola del
 * Programa y al espacio de una organización sin una sola condición de rol escrita en la interfaz.
 */
@Component({
  selector: 'app-consulta-guiada',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideCircleCheck],
  templateUrl: './consulta-guiada.component.html',
  // EL ANFITRION TIENE QUE SABER ESTIRARSE. Sin esto, el elemento `app-consulta-guiada` es una caja
  // en flujo normal y la conversación de dentro —que se apoya en `flex-1` con `min-h-0`— no recibe
  // altura: el pie con la advertencia de alcance se sale por debajo del cajón y queda cortado.
  host: { class: 'flex min-h-0 flex-1 flex-col' },
})
export class ConsultaGuiadaComponent implements OnInit {
  private readonly servicio = inject(ConsultaGuiadaService);

  /**
   * La ruta base, sin `/estado` ni `/consultar`.
   *
   * ES LO UNICO QUE DISTINGUE LOS DOS ESPACIOS. `/api/v1/admin/analisis` pregunta por la operación
   * del Programa; `/api/v1/externo/organizaciones/{id}/consulta-guiada`, por lo de esa organización.
   */
  @Input({ required: true }) base = '';

  /** Desde dónde se pregunta, para que la respuesta hable de lo que se está mirando. */
  @Input() contexto: ContextoDeConsulta | undefined;

  /**
   * Si pinta su propio título.
   *
   * DENTRO DEL CAJON SOBRA: el cajón ya se anuncia como «Consulta guiada» y repetirlo dos veces
   * seguidas hace que un lector de pantalla lo lea dos veces y que la pantalla parezca mal armada.
   */
  @Input() conTitulo = true;

  /** El saludo, que cambia según a quién se le pregunta. */
  @Input() saludo =
    'Puedo consultar cifras agregadas y enseñarte de dónde sale cada una.';

  /** La advertencia del pie. No es decorativa: acota la autoridad del asistente. */
  @Input() limite =
    'Solo lectura. El asistente no identifica personas ni puede modificar, aprobar o publicar registros.';

  readonly estado = signal<EstadoDeConsultaGuiada | null>(null);
  readonly turnos = signal<TurnoDeConsulta[]>([]);
  readonly borrador = signal('');

  private readonly campoDePregunta = viewChild<ElementRef<HTMLTextAreaElement>>('campoDePregunta');
  readonly cargandoEstado = signal(true);
  readonly consultando = signal(false);
  readonly errorEstado = signal('');

  readonly puedePreguntar = computed(
    () => !this.consultando() && this.estado()?.disponible !== false);

  ngOnInit(): void {
    this.turnos.set([{
      papel: 'asistente',
      texto: this.saludo,
      consulta: null,
      generadoEn: null,
      resueltoPor: null,
      contexto: null,
      esError: false,
    }]);
    this.cargarEstado();
  }

  cargarEstado(): void {
    this.cargandoEstado.set(true);
    this.errorEstado.set('');
    this.servicio.estado(this.base).subscribe({
      next: estado => {
        this.estado.set(estado);
        this.cargandoEstado.set(false);
      },
      error: error => {
        this.errorEstado.set(this.mensajeDeError(error, 'No fue posible consultar el estado del asistente.'));
        this.cargandoEstado.set(false);
      },
    });
  }

  usarSugerencia(sugerencia: string): void {
    this.borrador.set(sugerencia);
    this.preguntar();
  }

  preguntar(): void {
    const pregunta = this.borrador().trim();
    if (!pregunta || !this.puedePreguntar()) return;

    this.agregar({ papel: 'usuario', texto: pregunta });
    this.borrador.set('');
    this.consultando.set(true);

    this.servicio.consultar(this.base, pregunta, this.contexto).subscribe({
      next: respuesta => {
        this.agregar({
          papel: 'asistente',
          texto: respuesta.respuesta,
          consulta: respuesta.consulta,
          generadoEn: respuesta.generadoEn,
          resueltoPor: respuesta.resueltoPor,
          contexto: respuesta.contexto,
        });
        this.consultando.set(false);
        this.devolverElFoco();
      },
      error: error => {
        this.agregar({
          papel: 'asistente',
          texto: this.mensajeDeError(error, 'No fue posible responder la pregunta.'),
          esError: true,
        });
        this.consultando.set(false);
        this.devolverElFoco();
      },
    });
  }

  /**
   * Devuelve el foco al campo cuando llega la respuesta.
   *
   * <b>SIN ESTO EL FOCO SE PIERDE EN EL CUERPO DEL DOCUMENTO.</b> Medido en el navegador el 18 de
   * septiembre de 2026: se escribe la pregunta, se pulsa Intro, llega la respuesta y quien navega con
   * teclado se queda sin sitio —para preguntar otra vez hay que tabular desde el principio de la
   * página—. La respuesta ya se anuncia sola, porque el hilo de la conversación es una región viva;
   * lo que faltaba era el sitio desde el que seguir.
   *
   * <b>NO SE ROBA EL FOCO.</b> Si mientras se respondía la persona se fue a otro control, se queda
   * donde está: solo se recupera cuando el foco está en el cuerpo o dentro del propio asistente,
   * que es justo el caso en el que se había perdido.
   */
  private devolverElFoco(): void {
    // SE ESPERA UNA TAREA, Y NO ES UN DETALLE DE TIEMPOS. El campo está deshabilitado mientras se
    // consulta, y un `focus()` sobre un control deshabilitado no hace nada y no avisa: el primer
    // intento se escribió sin espera y el foco seguía perdiéndose igual.
    //
    // <b>Y NO SE USA `afterNextRender`, QUE ERA EL CANDIDATO OBVIO.</b> Se probó y se midió en el
    // navegador: no se dispara. Registrado desde la respuesta del servidor, cuando el pintado de esa
    // respuesta ya ocurrió, se queda esperando un pintado siguiente que no llega. En las pruebas sí
    // funcionaba —porque el `detectChanges` que viene después provoca uno—, que es la forma más
    // cara de equivocarse: verde en la suite y roto en la pantalla.
    setTimeout(() => {
      const campo = this.campoDePregunta()?.nativeElement;
      if (!campo || campo.disabled) return;
      const dondeEsta = document.activeElement;
      const sePerdio = !dondeEsta || dondeEsta === document.body;
      const sigueAqui = dondeEsta instanceof Node && campo.closest('section, form, div')?.contains(dondeEsta);
      if (sePerdio || sigueAqui) campo.focus();
    });
  }

  alPulsarTecla(evento: KeyboardEvent): void {
    if (evento.key !== 'Enter' || evento.shiftKey) return;
    evento.preventDefault();
    this.preguntar();
  }

  fecha(valor: string | null): string {
    if (!valor) return '';
    const fecha = new Date(valor);
    return Number.isNaN(fecha.getTime())
      ? ''
      : fecha.toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' });
  }

  /**
   * Quién eligió la consulta, en palabras.
   *
   * SE DICE SIEMPRE. Quien lee una cifra tiene derecho a saber si la eligió una regla revisable o
   * un modelo, y el día que haya modelo esa distinción es lo único que separa una cifra auditable
   * de una que hay que volver a comprobar.
   */
  etiquetaDelDecisor(valor: string | null): string {
    return valor === 'regla' ? 'Consulta verificada'
      : valor === 'modelo_local' ? 'Clasificada por modelo local'
        : valor === 'sin_coincidencia' ? 'Fuera del catálogo' : '';
  }

  private agregar(turno: Partial<TurnoDeConsulta> & { papel: 'usuario' | 'asistente'; texto: string }): void {
    this.turnos.update(turnos => [...turnos, {
      consulta: null,
      generadoEn: null,
      resueltoPor: null,
      contexto: null,
      esError: false,
      ...turno,
    }]);
  }

  private mensajeDeError(error: unknown, respaldo: string): string {
    const mensaje = (error as { message?: unknown })?.message;
    return typeof mensaje === 'string' && mensaje.trim().length > 0 ? mensaje : respaldo;
  }
}
