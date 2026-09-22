import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FestivalDeLaOrganizacion } from './panel-organizacion.api';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../shared/components/ui/indicador-de-estado/tono-del-estado';
import {
  EstadoFestival,
  EstadoPropuesta,
  claveDeEstado,
  estadoDeLaPropuesta,
  estadoDelFestival,
  etiquetaDeLaPropuesta,
  etiquetaDelFestival,
} from './estados-del-festival';

/**
 * La lista de Festivales de una organización, con las acciones que admite cada estado.
 *
 * QUÉ RESCATA. La línea 213 de
 * `pnmc-web/src/app/features/external-access/external-access-page.component.html`:
 * 4.024 caracteres en un solo renglón, con un `@for` cuyo cuerpo son once `@if` anidados.
 * Ahí vivían los seis estados de la máquina (los cinco del Festival más el ciclo propio de
 * la propuesta de cambios) y no había forma de leerlos ni de probarlos por separado.
 *
 * QUÉ NO HACE. No llama a ningún servicio. Recibe la lista por `@Input` y emite la acción
 * por `@Output`, para que cada estado se pueda probar sin red y sin base.
 *
 * DÓNDE VIVEN AHORA LOS ESTADOS. En `estados-del-festival.ts`. Los comparte con la pestaña de
 * Solicitudes, que necesita la misma tabla para saber qué está esperando decisión del equipo del
 * PNMC; dos copias divergirían en cuanto una de las dos pantallas cambiara.
 */
@Component({
  selector: 'app-seccion-festivales',
  standalone: true,
  imports: [DatePipe, IndicadorDeEstadoComponent],
  templateUrl: './seccion-festivales.component.html',
})
export class SeccionFestivalesComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly element: ElementRef<HTMLElement> = inject(ElementRef);
  /** Lo que devuelve GET /api/v1/externo/organizaciones/{id}/festivales. */
  @Input() festivales: FestivalDeLaOrganizacion[] = [];

  /**
   * Hay una petición en vuelo. Deshabilita los botones, como hacía `loading()` en la
   * página de acceso externo: sin esto, dos pulsaciones seguidas mandan dos veces el
   * mismo Festival a revisión.
   */
  @Input() ocupado = false;

  /** Abrir el formulario con el Festival cargado. Solo Borrador y AjustesSolicitados. */
  @Output() editar = new EventEmitter<FestivalDeLaOrganizacion>();

  /**
   * Abrir la ficha completa de la edición del Festival: los veintiséis campos del modelo, sus
   * cinco catálogos, los municipios, las entidades aliadas y el material.
   *
   * SE EMITE EN CUALQUIER ESTADO. Leer no cambia nada, y quien decide si además se puede editar es
   * el servidor, en `esEditable`: repetir aquí esa regla daría dos verdades sobre lo mismo.
   */
  @Output() abrirFicha = new EventEmitter<FestivalDeLaOrganizacion>();

  /** Remitir el Festival al equipo del PNMC. */
  @Output() enviarARevision = new EventEmitter<FestivalDeLaOrganizacion>();

  /** Abrir (o retomar) una propuesta de cambios sobre un Festival ya publicado. */
  @Output() proponerCambios = new EventEmitter<FestivalDeLaOrganizacion>();

  /** Remitir la propuesta de cambios a revisión institucional. */
  @Output() enviarPropuesta = new EventEmitter<FestivalDeLaOrganizacion>();

  /**
   * Ver el historial de ediciones del Festival, o registrar la primera/otra. Solo existe sobre un
   * Festival Publicado -mientras está en Borrador o AjustesSolicitados no tiene ninguna versión
   * vigente todavía que ediciones puedan acompañar-.
   */
  @Output() verEdiciones = new EventEmitter<FestivalDeLaOrganizacion>();

  /** Retirar un registro que aún no es público. El servidor conserva la auditoría. */
  @Output() retirar = new EventEmitter<FestivalDeLaOrganizacion>();
  @Output() solicitarRetiro = new EventEmitter<FestivalDeLaOrganizacion>();

  @HostListener('document:click', ['$event']) cerrarAlPulsarFuera(evento: MouseEvent): void {
    const destino = evento.target as Node | null;
    if (!destino || this.element.nativeElement.contains(destino)) return;
    this.cerrarMenus();
  }

  @HostListener('document:keydown.escape') cerrarConEscape(): void { this.cerrarMenus(); }

  private cerrarMenus(): void {
    this.element.nativeElement.querySelectorAll('details[open]').forEach((menu: Element) => { (menu as HTMLDetailsElement).open = false; });
  }


  estadoDe(festival: FestivalDeLaOrganizacion): EstadoFestival {
    return estadoDelFestival(festival.estado);
  }

  etiquetaEstado(festival: FestivalDeLaOrganizacion): string {
    return etiquetaDelFestival(festival.estado);
  }

  /** Hay una propuesta de cambios abierta o cerrada, sea cual sea su estado. */
  hayPropuesta(festival: FestivalDeLaOrganizacion): boolean {
    return claveDeEstado(festival.estadoPropuesta) !== '';
  }

  estadoPropuestaDe(festival: FestivalDeLaOrganizacion): EstadoPropuesta | null {
    return estadoDeLaPropuesta(festival.estadoPropuesta);
  }

  etiquetaEstadoPropuesta(festival: FestivalDeLaOrganizacion): string {
    return etiquetaDeLaPropuesta(festival.estadoPropuesta);
  }

  /**
   * Editar el Festival directamente. El API solo lo admite en Borrador y en
   * AjustesSolicitados (`FestivalesExternosEndpoints.cs-483`, `EsEditable`); en
   * cualquier otro estado el PUT contesta 409, así que el botón no se pinta.
   */
  puedeEditar(festival: FestivalDeLaOrganizacion): boolean {
    const estado = this.estadoDe(festival);
    return estado === 'Borrador' || estado === 'AjustesSolicitados';
  }

  /**
   * Enviar el Festival al equipo del PNMC. Borrador y AjustesSolicitados, igual que el API.
   *
   * <b>HASTA EL 30 DE AGOSTO DE 2026 SOLO SE OFRECÍA EN BORRADOR</b>, y era un defecto anotado en
   * este mismo sitio: el API admite los dos estados (`FestivalesExternosEndpoints.cs`, `EsEditable`)
   * y el propio panel escribía «Ya puedes enviarlo nuevamente a revisión» al guardar unos ajustes.
   * Un Festival devuelto se corregía y se quedaba ahí: no había ningún botón para devolverlo. Lo
   * pidió el usuario sobre la tarjeta de «dsadasdasd».
   */
  puedeEnviarARevision(festival: FestivalDeLaOrganizacion): boolean {
    const estado = this.estadoDe(festival);
    return estado === 'Borrador' || estado === 'AjustesSolicitados';
  }

  /**
   * Quedan campos con un cambio pedido que nadie ha marcado como corregido.
   *
   * <b>Es la condición de «Volver a enviar a revisión»</b>, y no la esconde: la apaga. Quien
   * devolvió el Festival pidió correcciones campo por campo; reenviarlo con todas sin marcar le
   * devuelve al funcionario la misma ficha que él mandó corregir.
   *
   * <b>EL NÚMERO LO CUENTA EL SERVIDOR</b> —`cambiosPedidos`, en la misma respuesta que la lista— y
   * cuenta solo lo que queda sin atender. La misma regla la aplica el API en
   * `POST /externo/festivales/{id}/enviar-a-revision`, que responde 409 mientras quede alguno: este
   * botón la anticipa, no la sustituye.
   */
  faltanCambiosPorAtender(festival: FestivalDeLaOrganizacion): boolean {
    return (festival.cambiosPedidos ?? 0) > 0;
  }

  /** «Volver a enviar» nombra un reenvío, no un primer envío. */
  rotuloEnviarARevision(festival: FestivalDeLaOrganizacion): string {
    return this.estadoDe(festival) === 'AjustesSolicitados'
      ? 'Volver a enviar a revisión'
      : 'Enviar a revisión';
  }

  /**
   * El nombre accesible del reenvío lleva el MOTIVO cuando el botón está apagado.
   *
   * <b>Y no un `aria-describedby`, que era lo que había.</b> Un `<button disabled>` sale del
   * recorrido de tabulación: quien navega con teclado nunca lo enfoca, así que la descripción
   * enlazada no se lee jamás. Era una explicación que existía exactamente donde no se puede oír.
   * El motivo va en el nombre, que sí se anuncia al recorrer la tarjeta.
   */
  nombreDelReenvio(festival: FestivalDeLaOrganizacion): string {
    const base = this.nombreAccesible(this.rotuloEnviarARevision(festival), festival);
    if (!this.faltanCambiosPorAtender(festival)) return base;
    const faltan = festival.cambiosPedidos ?? 0;
    return `${base}. ${faltan === 1
      ? 'Queda 1 ajuste sugerido por resolver'
      : `Quedan ${faltan} ajustes sugeridos por resolver`}.`;
  }

  /**
   * Abrir una propuesta nueva. Solo sobre un Festival Publicado
   * (`proponerCambiosFestival` corta con `festival.estado !== 'Publicado'`) y solo cuando
   * no hay una propuesta viva: si la anterior se publicó o se rechazó, se empieza otra.
   */
  puedeProponerCambios(festival: FestivalDeLaOrganizacion): boolean {
    if (this.estadoDe(festival) !== 'Publicado') return false;
    const propuesta = this.estadoPropuestaDe(festival);
    return propuesta === null || propuesta === 'Rechazada' || propuesta === 'Publicada';
  }

  /**
   * Retomar la propuesta que ya está abierta. Pulsa el mismo camino que «Proponer
   * cambios» —el API devuelve la propuesta viva en vez de crear otra— y por eso emite el
   * mismo evento; lo que cambia es el rótulo, porque no es lo mismo empezar que corregir.
   */
  puedeEditarPropuesta(festival: FestivalDeLaOrganizacion): boolean {
    if (this.estadoDe(festival) !== 'Publicado') return false;
    const propuesta = this.estadoPropuestaDe(festival);
    return propuesta === 'Borrador' || propuesta === 'AjustesSolicitados';
  }

  /**
   * Enviar la propuesta a revisión. Mismas dos situaciones que retomarla, que es lo que
   * comprueba `enviarPropuestaARevision` antes de llamar al API.
   */
  puedeEnviarPropuesta(festival: FestivalDeLaOrganizacion): boolean {
    return this.puedeEditarPropuesta(festival);
  }

  /**
   * «Continuar registro», «Atender ajustes»: cada rótulo nombra la acción concreta, no un
   * genérico «Editar» -Se define que la persona no tenga que
   * adivinar qué va a pasar al pulsar-.
   */
  rotuloEditarFestival(festival: FestivalDeLaOrganizacion): string {
    return this.estadoDe(festival) === 'AjustesSolicitados' ? 'Atender ajustes' : 'Continuar registro';
  }

  rotuloEditarPropuesta(festival: FestivalDeLaOrganizacion): string {
    return this.estadoPropuestaDe(festival) === 'AjustesSolicitados'
      ? 'Atender ajustes de la propuesta'
      : 'Continuar edición';
  }

  /**
   * Cuántas ediciones tiene, o si todavía no tiene ninguna. Solo aplica a un Festival Publicado:
   * mientras está en Borrador o AjustesSolicitados no hay ninguna versión vigente que enseñar.
   */
  puedeVerEdiciones(festival: FestivalDeLaOrganizacion): boolean {
    return this.estadoDe(festival) === 'Publicado';
  }

  rotuloVerEdiciones(festival: FestivalDeLaOrganizacion): string {
    const cuantas = festival.cuantasEdiciones ?? 0;
    return cuantas > 0 ? `Ver ediciones (${cuantas})` : 'Registrar nueva edición';
  }

  /** Un Festival no público se puede retirar; uno publicado requiere la ruta institucional. */
  puedeRetirar(festival: FestivalDeLaOrganizacion): boolean {
    const estado = this.estadoDe(festival);
    return estado === 'Borrador' || estado === 'AjustesSolicitados' || estado === 'EnRevision';
  }

  rotuloRetiro(festival: FestivalDeLaOrganizacion): string {
    return this.estadoDe(festival) === 'EnRevision' ? 'Retirar de revisión' : 'Eliminar borrador';
  }

  puedeSolicitarRetiro(festival: FestivalDeLaOrganizacion): boolean { return this.estadoDe(festival) === 'Publicado'; }

  /** «Medellín (Antioquia)», o el que falte, o el aviso de que no hay ninguno todavía. */
  territorioDe(festival: FestivalDeLaOrganizacion): string {
    if (festival.nombreMunicipio && festival.nombreDepartamento) return `${festival.nombreMunicipio} (${festival.nombreDepartamento})`;
    return festival.nombreMunicipio || festival.nombreDepartamento || 'Territorio por definir';
  }

  /**
   * Con varias tarjetas en la lista, un lector de pantalla anuncia «Editar» seis veces sin
   * decir qué se edita. El nombre visible se deja corto y el accesible lleva el Festival.
   */
  nombreAccesible(accion: string, festival: FestivalDeLaOrganizacion): string {
    return `${accion}: ${festival.nombre}`;
  }


  // ─────────────────────────── La ficha desplegable ───────────────────────────






}
