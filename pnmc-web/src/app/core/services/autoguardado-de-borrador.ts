import { signal } from '@angular/core';

/**
 * El bucle de autoguardado que comparten los formularios largos de la consola.
 *
 * <b>ES UNA CLASE Y NO TRES COPIAS</b> porque la parte delicada —esperar a que alguien deje de
 * escribir, no pisarse con el guardado anterior, citar la versión que se vio— es idéntica en los
 * tres módulos y es justo la que se escribe mal cuando se copia.
 *
 * <b>ESPERA A QUE SE DEJE DE ESCRIBIR.</b> Guardar en cada pulsación mandaría una petición por
 * letra; guardar cada N segundos a ciegas manda peticiones cuando nadie ha tocado nada. Se espera
 * un momento de quietud, que es cuando hay algo nuevo que guardar.
 *
 * <b>NO SE SOLAPAN DOS GUARDADOS.</b> Si uno está en vuelo cuando vence el siguiente, se marca que
 * hay trabajo pendiente y se reintenta al terminar: dos peticiones simultáneas llegarían con la
 * misma versión y la segunda sería rechazada por conflicto contra la primera, que es una carrera
 * consigo mismo. No es teórico: el asistente de Festival, que se había escrito su propio bucle,
 * comprobaba la señal del guardado MANUAL antes de mandar y no la del automático, así que dos
 * automáticos sí podían solaparse. El segundo recibía un 409 con el mensaje «El borrador cambió en
 * otra sesión», que es falso —había chocado consigo mismo— y dejaba el formulario en error.
 *
 * <b>NO SABE POR DONDE VIAJA LO QUE GUARDA.</b> La consola institucional guarda por
 * `/institucional/borradores/{dominio}` y el asistente de Festival por una ruta del canal externo,
 * atada a su organización. Atar esta clase a una de las dos obligaba a la otra a escribirse su
 * propio bucle, que es exactamente lo que pasó. El transporte entra por el constructor.
 */

/**
 * Por dónde viaja el borrador. Lo implementa quien sabe hablar con su servidor.
 *
 * Las tres operaciones son las que el bucle necesita y ninguna más: `guardar` devuelve `null`
 * cuando falla —el bucle lo dice en pantalla en vez de callarlo— y la versión que devuelve es la
 * que se citará en el siguiente guardado.
 */
export interface TransporteDeBorrador<T> {
  leer(): Promise<{ datos: T; version: number; fechaActualizacion: string } | null>;
  guardar(datos: T, version: number | null): Promise<{ version: number; fechaActualizacion: string } | null>;
  descartar(): Promise<void>;
}

export type EstadoDelAutoguardado = 'inactivo' | 'guardando' | 'guardado' | 'fallido';

/** Lo que se espera sin teclear antes de mandar el borrador, en milisegundos. */
const QUIETUD = 900;

export class AutoguardadoDeBorrador<T> {
  readonly estado = signal<EstadoDelAutoguardado>('inactivo');
  readonly ultimoGuardado = signal<string | null>(null);

  private temporizador: ReturnType<typeof setTimeout> | null = null;
  private version: number | null = null;
  private enVuelo = false;
  private pendiente = false;
  private ultimoDato: T | null = null;
  private activo = false;

  constructor(private readonly transporte: TransporteDeBorrador<T>) {}

  /** El borrador guardado, si lo hay, para ofrecer recuperarlo. */
  async recuperar(): Promise<{ datos: T; fecha: string; version: number } | null> {
    const fila = await this.transporte.leer();
    if (!fila) { return null; }

    return { datos: fila.datos, fecha: fila.fechaActualizacion, version: fila.version };
  }

  /**
   * Empieza a guardar. Hasta que se llama, escribir no manda nada.
   *
   * <b>ENCENDER CON UNA VERSION SIGNIFICA QUE YA HAY UN BORRADOR GUARDADO</b> y que el formulario
   * lo refleja: se acaba de recuperar. El estado arranca en «guardado» y no en «inactivo», porque
   * lo que hay en pantalla está respaldado y decir lo contrario invitaría a copiar el texto a otro
   * sitio sin motivo. Sin versión —un formulario en blanco— no hay nada guardado todavía.
   */
  encender(version: number | null = null): void {
    this.activo = true;
    this.version = version;
    if (version !== null) this.estado.set('guardado');
  }

  /** Anota que hay algo nuevo que guardar. Se manda cuando haya un momento de quietud. */
  anotar(datos: T): void {
    if (!this.activo) { return; }
    this.ultimoDato = datos;
    if (this.temporizador) { clearTimeout(this.temporizador); }
    this.temporizador = setTimeout(() => { void this.mandar(); }, QUIETUD);
  }

  /**
   * Cierra el borrador y deja de guardar.
   *
   * SE LLAMA AL GUARDAR DE VERDAD: a partir de ahí el contenido ya está en su tabla y el borrador
   * solo serviría para ofrecer recuperar algo que ya existe.
   */
  async cerrar(): Promise<void> {
    this.apagar();
    await this.transporte.descartar();
  }

  /** Deja de guardar sin tocar lo guardado: el formulario se cerró, pero el borrador sigue ahí. */
  apagar(): void {
    this.activo = false;
    if (this.temporizador) { clearTimeout(this.temporizador); this.temporizador = null; }
    this.estado.set('inactivo');
    this.ultimoGuardado.set(null);
    this.version = null;
    this.ultimoDato = null;
  }

  private async mandar(): Promise<void> {
    if (!this.activo || this.ultimoDato === null) { return; }

    if (this.enVuelo) { this.pendiente = true; return; }

    this.enVuelo = true;
    this.estado.set('guardando');

    const respuesta = await this.transporte.guardar(this.ultimoDato, this.version);

    if (respuesta) {
      this.version = respuesta.version;
      this.estado.set('guardado');
      this.ultimoGuardado.set(respuesta.fechaActualizacion);
    } else {
      // SE DICE Y NO SE CALLA. Quien está escribiendo necesita saber que lo que ve en pantalla ya
      // no está respaldado, que es justo el momento en que conviene copiar el texto a otro sitio.
      this.estado.set('fallido');
    }

    this.enVuelo = false;

    if (this.pendiente) {
      this.pendiente = false;
      await this.mandar();
    }
  }
}
