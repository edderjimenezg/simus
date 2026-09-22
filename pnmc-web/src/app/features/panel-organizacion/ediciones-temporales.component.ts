import { CommonModule } from '@angular/common';
import { ConfirmacionComponent } from '../../shared/components/ui/confirmacion/confirmacion.component';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { AdminService, AjustesDeEdicion, EdicionDeFestival } from '../../core/services/admin.service';
import { FichaEdicionFestivalComponent } from './ficha-edicion-festival.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../shared/components/ui/indicador-de-estado/tono-del-estado';

/**
 * Las ediciones de un Festival, con su ciclo completo.
 *
 * <b>QUE CAMBIO EL 12 DE SEPTIEMBRE DE 2026.</b> está definido así: «tengo una edición en
 * estado borrador y no existe una ruta clara para publicarla». Era cierto por partida doble:
 * «Publicar» era un enlace subrayado del mismo tamaño que los demás, perdido en la última columna,
 * y el formulario de la edición solo ofrecía «Guardar borrador», de modo que al terminar de
 * diligenciarla no había ninguna forma de decir «ya está».
 *
 * <b>Y SOBRABA UNA ACCION.</b> «Consultar observaciones» aparecía en todas las filas, también en
 * ediciones que nunca pasaron por revisión y que por tanto no podían tener ninguna: pulsarlo abría
 * un recuadro que decía «No hay observaciones por campo». Ahora la fila trae cuántas tiene, y la
 * acción existe solo si hay alguna.
 */
@Component({
  selector: 'app-ediciones-temporales',
  standalone: true,
  imports: [CommonModule, FichaEdicionFestivalComponent, MenuDeAccionesComponent, IndicadorDeEstadoComponent, ConfirmacionComponent],
  templateUrl: './ediciones-temporales.component.html',
})
export class EdicionesTemporalesComponent implements OnChanges {
  @Input({ required: true }) festivalId!: string;
  @Input({ required: true }) festivalPublicado = false;

  private readonly api = inject(AdminService);

  readonly ediciones = signal<EdicionDeFestival[]>([]);
  readonly edicionAbierta = signal<string | null>(null);
  /**
   * Si la ficha abierta se está consultando y no editando.
   *
   * SON DOS MODOS DE LA MISMA FICHA y no dos pantallas: ver `soloLectura` en
   * `FichaEdicionFestivalComponent`. Aquí solo se recuerda con cuál se abrió.
   */
  readonly consultando = signal(false);
  readonly ajustes = signal<AjustesDeEdicion | null>(null);
  readonly edicionConAjustes = signal<string | null>(null);
  readonly cargando = signal(false);
  readonly error = signal('');
  readonly mensaje = signal('');

  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  ngOnChanges(): void { if (this.festivalId) this.cargar(); }

  cargar(): void {
    this.cargando.set(true);
    this.error.set('');
    this.api.cargarEdicionesDeFestival(Number(this.festivalId)).subscribe({
      next: x => { this.ediciones.set(x); this.cargando.set(false); },
      error: e => { this.error.set(e?.message ?? 'No fue posible consultar las ediciones.'); this.cargando.set(false); },
    });
  }

  fechasDe(edicion: EdicionDeFestival): string {
    if (edicion.fechaInicio && edicion.fechaFin) return `${edicion.fechaInicio} a ${edicion.fechaFin}`;
    return edicion.fechaInicio || edicion.fechaFin || (edicion.anio ? String(edicion.anio) : 'Fechas por definir');
  }

  nombreDe(edicion: EdicionDeFestival): string {
    return edicion.nombre || `Edición ${edicion.numeroEdicion || edicion.anio || 'sin identificar'}`;
  }

  /**
   * Las acciones que esta edición admite hoy.
   *
   * <b>SALEN DEL ESTADO Y NO SE PINTAN TODAS.</b> Un borrador se edita y se publica; una publicada
   * se despublica o se archiva; una archivada solo se consulta. Ofrecer las seis en las tres
   * situaciones obliga a descubrir por ensayo y error cuáles funcionan hoy.
   *
   * <b>«PUBLICAR» ES LA ACCION PRINCIPAL de un borrador</b>, así que sale fuera del menú: era
   * exactamente la que no se encontraba.
   */
  accionesDe(edicion: EdicionDeFestival): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [];
    const borrador = edicion.estadoVisibilidad === 'borrador';
    const publicada = edicion.estadoVisibilidad === 'publicada';

    // CONSULTAR ES LO UNICO QUE SE PUEDE SIEMPRE, y por eso es lo primero de la lista cuando la
    // edición no se puede editar. Hasta una edición ARCHIVADA no tenía
    // NI UNA acción —no se edita, no se publica, no se archiva otra vez y no se elimina— y su fila
    // quedaba con la columna «Acciones» en blanco: el registro existía y no había forma de volver a
    // verlo. Medido contra PNMC_LOCAL sobre las dos ediciones archivadas del festival 3873.
    //
    // NO SE OFRECE CUANDO SE PUEDE EDITAR: el formulario de edición ES la ficha, y tener «Editar» y
    // «Ver la ficha» uno debajo del otro abriendo lo mismo es ofrecer dos caminos para un sitio.
    if (!borrador) {
      acciones.push({ id: 'ver', etiqueta: 'Ver la ficha', tono: 'principal' });
    }

    if (borrador) {
      acciones.push({
        id: 'publicar',
        etiqueta: 'Publicar',
        tono: 'principal',
        // SE OFRECE DESACTIVADA Y NO SE ESCONDE, porque aquí sí se puede hacer: lo que falta es un
        // requisito del Festival, y decirlo es más útil que hacer desaparecer el botón.
        deshabilitada: !this.festivalPublicado,
        motivo: 'Publica primero el Festival para poder publicar una de sus ediciones.',
      });
      acciones.push({ id: 'editar', etiqueta: 'Editar' });
    }

    if (publicada) {
      acciones.push({ id: 'despublicar', etiqueta: 'Despublicar' });
    }

    // LA ACCION EXISTE SOLO SI HAY OBSERVACIONES. Ofrecerla sin ninguna lleva a una vista vacía,
    // que hace perder un clic y deja la duda de si algo falló.
    if (edicion.cuantasObservaciones > 0) {
      acciones.push({
        id: 'observaciones',
        etiqueta: edicion.cuantasObservaciones === 1
          ? 'Ver 1 observación'
          : `Ver ${edicion.cuantasObservaciones} observaciones`,
      });
    }

    if (edicion.estadoVisibilidad !== 'archivada') {
      acciones.push({ id: 'archivar', etiqueta: 'Archivar', tono: 'peligro' });
    }

    // ELIMINAR SOLO LO QUE DE VERDAD SE PUEDE ELIMINAR.
    //
    // «Se elimina un borrador registrado por error; lo que se publicó en algún momento sí debe
    // quedar archivado», de la dirección de producto. Quien decide es el
    // servidor, y por eso esto mira `sePuedeEliminar` y no `estadoVisibilidad`: una edición
    // publicada y después despublicada vuelve a «borrador», así que el estado de ahora diría que sí
    // y el público ya la vio. La respuesta está en el historial, que el cliente no tiene.
    //
    // VA LA ULTIMA Y EN TONO DE PELIGRO, debajo de «Archivar», que es la salida que conserva.
    if (edicion.sePuedeEliminar) {
      acciones.push({ id: 'eliminar', etiqueta: 'Eliminar', tono: 'peligro' });
    }

    return acciones;
  }

  ejecutarAccion(edicion: EdicionDeFestival, accion: string): void {
    switch (accion) {
      case 'ver': this.consultar(edicion); break;
      case 'editar': this.editar(edicion); break;
      case 'publicar': this.publicar(edicion); break;
      case 'despublicar': this.despublicar(edicion); break;
      case 'archivar': this.archivar(edicion); break;
      case 'observaciones': this.verAjustes(edicion); break;
      case 'eliminar': this.eliminar(edicion); break;
    }
  }

  /**
   * Elimina el borrador de una edición registrada por error.
   *
   * <b>PREGUNTA ANTES, Y DICE QUE NO SE DESHACE.</b> Es la única acción de esta lista que no tiene
   * vuelta: archivar conserva el registro, despublicar lo devuelve a borrador, y esto lo quita con
   * sus municipios, sus aliadas y sus materiales.
   *
   * NOMBRA LA EDICION EN LA PREGUNTA. Con tres ediciones en pantalla, un «¿seguro?» a secas no dice
   * cuál se va a borrar, que es justo lo que hay que confirmar.
   */
  eliminar(edicion: EdicionDeFestival): void {
    this.edicionPorEliminar.set(edicion);
  }

  /** La edición que espera confirmación. Se pregunta en el diálogo del proyecto, no en el navegador. */
  readonly edicionPorEliminar = signal<EdicionDeFestival | null>(null);

  /** Cómo se llama en la pregunta: con tres ediciones en pantalla, «esta edición» no dice cuál. */
  comoSeLlama(edicion: EdicionDeFestival): string {
    return edicion.nombre?.trim() || (edicion.anio ? `la edición ${edicion.anio}` : 'esta edición');
  }

  cerrarLaConfirmacion(): void { this.edicionPorEliminar.set(null); }

  confirmarLaEliminacion(): void {
    const edicion = this.edicionPorEliminar();
    if (!edicion) return;
    this.edicionPorEliminar.set(null);
    const comoSeLlama = this.comoSeLlama(edicion);
    this.error.set('');
    this.api.eliminarEdicionDeFestival(edicion.id).subscribe({
      next: () => { this.mensaje.set(`Se eliminó ${comoSeLlama}.`); this.cargar(); },
      error: e => this.error.set(e?.message ?? 'No fue posible eliminar la edición.'),
    });
  }

  nueva(): void { this.consultando.set(false); this.edicionAbierta.set('nueva'); this.ajustes.set(null); }

  editar(edicion: EdicionDeFestival): void { this.consultando.set(false); this.edicionAbierta.set(edicion.id); this.ajustes.set(null); }

  /** Abre la misma ficha sin poder cambiar nada. La salida de un registro que ya cerró su ciclo. */
  consultar(edicion: EdicionDeFestival): void { this.consultando.set(true); this.edicionAbierta.set(edicion.id); this.ajustes.set(null); }

  /** Al guardar como borrador desde el formulario. */
  alGuardar(): void {
    this.mensaje.set('La edición quedó guardada como borrador.');
    this.edicionAbierta.set(null);
    this.cargar();
  }

  /**
   * Al publicar desde el formulario.
   *
   * SE DISTINGUE DE «GUARDAR» PORQUE SON DOS ACTOS DISTINTOS y el aviso tiene que decir cuál pasó:
   * un «quedó guardada» después de pulsar «Publicar edición» deja sin saber si se publicó.
   */
  alPublicarDesdeElFormulario(): void {
    this.mensaje.set('La edición quedó publicada.');
    this.edicionAbierta.set(null);
    this.cargar();
  }

  publicar(edicion: EdicionDeFestival): void {
    this.ejecutar(
      this.api.publicarEdicion(edicion.id),
      `«${this.nombreDe(edicion)}» quedó publicada.`,
      'No fue posible publicar la edición.');
  }

  despublicar(edicion: EdicionDeFestival): void {
    this.ejecutar(
      this.api.despublicarEdicion(edicion.id),
      `«${this.nombreDe(edicion)}» volvió a borrador y ya no se ve en el portal.`,
      'No fue posible despublicar la edición.');
  }

  archivar(edicion: EdicionDeFestival): void {
    this.ejecutar(
      this.api.archivarEdicion(edicion.id),
      `«${this.nombreDe(edicion)}» quedó archivada.`,
      'No fue posible archivar la edición.');
  }

  /**
   * Las tres transiciones hacen lo mismo alrededor: avisar, recargar y contar el motivo si falla.
   *
   * EL MOTIVO DEL SERVIDOR ENTERO cuando lo hay: «publica primero el Festival» y «esta edición ya
   * está archivada» piden cosas distintas, y un «no fue posible» genérico obliga a adivinar.
   */
  private ejecutar(peticion: { subscribe: (o: { next: () => void; error: (e: unknown) => void }) => void }, exito: string, respaldo: string): void {
    this.error.set('');
    this.mensaje.set('');
    peticion.subscribe({
      next: () => { this.mensaje.set(exito); this.cargar(); },
      error: (e: unknown) => this.error.set((e as { message?: string })?.message || respaldo),
    });
  }

  verAjustes(edicion: EdicionDeFestival): void {
    this.error.set('');
    this.edicionConAjustes.set(edicion.id);
    this.ajustes.set(null);
    this.api.cargarAjustesDeEdicion(edicion.id).subscribe({
      next: x => this.ajustes.set(x),
      error: e => this.error.set(e?.message ?? 'No fue posible consultar las observaciones.'),
    });
  }

  atender(id: number, atendida: boolean): void {
    this.api.atenderAjusteDeEdicion(id, atendida).subscribe({
      next: () => {
        const edicionId = this.edicionConAjustes();
        const edicion = this.ediciones().find(x => x.id === edicionId);
        if (edicion) this.verAjustes(edicion);
      },
      error: e => this.error.set(e?.message ?? 'No fue posible actualizar la observación.'),
    });
  }
}
