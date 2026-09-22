import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FichaFestivalComponent } from './ficha-festival/ficha-festival.component';
import { EdicionesTemporalesComponent } from './ediciones-temporales.component';
import { ContactoPublicoFestivalSolicitud, EntradaHistorialFestival, FalloDelServidor, FestivalDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';
import { estadoDelFestival, etiquetaDelFestival } from './estados-del-festival';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { CambiosPedidosApi } from '../../core/revision-de-campos/cambios-pedidos.api';
import { RevisionDeCamposStore } from '../../core/revision-de-campos/revision-de-campos.store';
import { AdminService, EdicionDeFestival } from '../../core/services/admin.service';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { ObservacionDeCampo } from '../../core/revision-de-campos/revision-de-campos';
import { etiquetaDePeriodicidad } from '../../core/vocabularios/periodicidad';

/** Una sección de la ficha interna. */
export interface SeccionDeFicha {
  id: string;
  etiqueta: string;
}

export const SECCIONES_DE_FICHA: readonly SeccionDeFicha[] = [
  { id: 'resumen', etiqueta: 'Vista general' },
  { id: 'informacion', etiqueta: 'Información' },
  { id: 'ediciones', etiqueta: 'Ediciones' },
  { id: 'historial', etiqueta: 'Historial' },
];

/**
 * La ficha interna de un Festival, en /gestion/procesos/festivales/{id}.
 *
 * Separa la lectura operativa, la información permanente, las Ediciones, los ajustes y la
 * trazabilidad. `FichaFestivalComponent` es el único editor del perfil; las Ediciones se gestionan
 * mediante `EdicionesTemporalesComponent`, sin reutilizar el historial de versiones como si fuera
 * una realización. `RevisionDeCamposStore` conserva aquí las observaciones de este Festival y
 * permite llevar cada ajuste directamente a su campo.
 */
@Component({
  selector: 'app-ficha-de-festival-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, FichaFestivalComponent, EdicionesTemporalesComponent, IndicadorDeEstadoComponent],
  providers: [RevisionDeCamposStore],
  templateUrl: './ficha-de-festival-page.component.html',
})
export class FichaDeFestivalPageComponent implements OnInit {
  private readonly api = inject(PanelOrganizacionApi);
  private readonly cambiosPedidos = inject(CambiosPedidosApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly store = inject(PanelOrganizacionStore);
  private idFestivalSolicitado = '';
  readonly revision = inject(RevisionDeCamposStore);

  readonly secciones = SECCIONES_DE_FICHA;
  readonly organizacionId = this.store.organizacionId;

  readonly seccionActiva = signal('resumen');
  readonly inspectorAjustesAbierto = signal(false);
  readonly abrirEnEdicion = signal(false);
  readonly campoInicial = signal<string | undefined>(undefined);
  readonly ajusteSeleccionadoId = signal<number | null>(null);
  /** La periodicidad se lee por su nombre, no por su código: «otra_regular» no es una palabra. */
  readonly etiquetaDePeriodicidad = etiquetaDePeriodicidad;

  readonly editandoAjuste = signal(false);
  readonly guardandoResolucion = signal(false);
  readonly mensajeAjuste = signal('');
  readonly errorAjuste = signal('');
  readonly mensajeDeLlegada = signal('');
  readonly editandoContactoPublico = signal(false);
  readonly guardandoContactoPublico = signal(false);
  readonly errorContactoPublico = signal('');
  readonly mensajeContactoPublico = signal('');
  readonly enviandoFestival = signal(false);
  readonly contactoPublico = signal<ContactoPublicoFestivalSolicitud>({
    correoContacto: null, telefonoCelular: null, instagram: null, facebook: null, paginaWeb: null, otroEnlace: null,
  });

  readonly cargando = signal(true);
  readonly error = signal('');
  readonly festival = signal<FestivalDeLaOrganizacion | null>(null);
  readonly historial = signal<EntradaHistorialFestival[]>([]);
  readonly cargandoHistorial = signal(false);
  readonly errorHistorial = signal('');

  readonly estadoEtiqueta = computed(() => {
    const f = this.festival();
    return f ? etiquetaDelFestival(f.estado) : '';
  });

  readonly festivalPublicado = computed(() => estadoDelFestival(this.festival()?.estado) === 'Publicado');

  /** El tono y el matiz del indicador de estado, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  // ─────────────────────── La edición más reciente ───────────────────────
  //
  // EL RESUMEN LA NOMBRA, y no basta con el número de ediciones. «4 ediciones» no dice si la última
  // es de 2019 o de este año, que es justo lo que quien administra necesita saber de un vistazo.
  // Es una petición más y se paga a propósito: el contrato de la lista de Festivales no la trae, y
  // añadirla ahí obligaría a contarla para los cuarenta Festivales de una organización grande.

  private readonly ediciones = inject(AdminService);

  readonly edicionMasReciente = signal<EdicionDeFestival | null>(null);

  /** Cómo se lee la edición más reciente en el resumen. */
  edicionMasRecienteEnPalabras(): string {
    const edicion = this.edicionMasReciente();
    if (!edicion) return 'Todavía sin ediciones';
    const nombre = edicion.nombre || `Edición ${edicion.numeroEdicion || edicion.anio || 'sin identificar'}`;
    return `${nombre} · ${edicion.estadoVisibilidadEtiqueta}`;
  }

  private cargarEdicionMasReciente(festivalId: string): void {
    this.ediciones.cargarEdicionesDeFestival(Number(festivalId)).subscribe({
      // LA LISTA VIENE DE LA MAS RECIENTE A LA MAS ANTIGUA desde el servidor, así que la primera es
      // la que se busca. Ordenarla otra vez aquí duplicaría un criterio que ya está decidido.
      next: lista => this.edicionMasReciente.set(lista[0] ?? null),
      error: () => this.edicionMasReciente.set(null),
    });
  }
  readonly festivalEnRevision = computed(() => estadoDelFestival(this.festival()?.estado) === 'EnRevision');
  readonly festivalConAjustes = computed(() => estadoDelFestival(this.festival()?.estado) === 'AjustesSolicitados');

  /**
   * Si la información del Festival admite cambios directos hoy.
   *
   * <b>ES LA MISMA REGLA QUE APLICA `FichaFestivalComponent`</b> —«Borrador» y «Ajustes
   * solicitados», y nada más— y hasta la cabecera de esta página no la
   * consultaba: el botón «Editar» se pintaba SIEMPRE. Sobre el festival 3873, publicado, medido
   * contra PNMC_LOCAL: pulsarlo abría la ficha en un modo de edición que ella misma cierra, así que
   * el botón cumplía su promesa a medias y dejaba la duda de si algo había fallado.
   */
  readonly informacionEditable = computed(() =>
    ['Borrador', 'AjustesSolicitados'].includes(estadoDelFestival(this.festival()?.estado) ?? ''));
  readonly festivalRechazado = computed(() => estadoDelFestival(this.festival()?.estado) === 'Rechazado');
  readonly ajusteSeleccionado = computed(() => {
    const notas = this.revision.notas();
    const id = this.ajusteSeleccionadoId();
    return id === null ? null : notas.find(nota => nota.id === id) ?? null;
  });
  readonly puedeEnviarFestival = computed(() => {
    const estado = estadoDelFestival(this.festival()?.estado);
    return (estado === 'Borrador' || estado === 'AjustesSolicitados') && this.revision.cuantasPendientes() === 0;
  });
  readonly ajustesCompletos = computed(() =>
    this.festivalConAjustes()
      && this.revision.revision()?.estado === 'enviada'
      && this.revision.hayNotas()
      && this.revision.cuantasPendientes() === 0);

  nombreOrganizacion(): string {
    return this.store.organizacionElegida()?.name ?? '';
  }

  ngOnInit(): void {
    this.revision.resolucionAutomatica.set(true);
    this.revision.presentacionAtencion.set('indicador');
    this.revision.alSeleccionarNota = nota => this.seleccionarAjuste(nota);
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(parametros =>
      this.aplicarParametrosDeNavegacion(parametros));
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(parametros =>
      this.cargarFestivalSolicitado(parametros.get('id') ?? ''));
  }

  private cargarFestivalSolicitado(id: string): void {
    const organizacionId = this.organizacionId();
    this.idFestivalSolicitado = id;
    this.festival.set(null);
    this.historial.set([]);
    this.error.set('');
    this.errorHistorial.set('');
    this.cargando.set(true);
    if (!organizacionId || !id) {
      this.cargando.set(false);
      this.error.set('Falta la organización o el Festival.');
      return;
    }

    this.api.obtenerFestivales(organizacionId).subscribe({
      next: festivales => {
        if (this.idFestivalSolicitado !== id) return;
        this.cargando.set(false);
        const encontrado = festivales.find(item => item.id === id) ?? null;
        this.festival.set(encontrado);
        if (!encontrado) {
          this.error.set('No se encontró este Festival entre los de la organización.');
          return;
        }
        this.cargarEdicionMasReciente(id);
        this.cargarCambiosPedidos(encontrado);
        if (this.seccionActiva() === 'historial') this.cargarHistorial();
      },
      error: (fallo: FalloDelServidor) => {
        if (this.idFestivalSolicitado !== id) return;
        this.cargando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible consultar los Festivales de la organización');
      },
    });
  }

  irASeccion(id: string): void {
    if (!this.secciones.some(seccion => seccion.id === id)) return;
    this.seccionActiva.set(id);
    this.abrirEnEdicion.set(false);
    this.editandoAjuste.set(false);
    if (id !== 'informacion') this.inspectorAjustesAbierto.set(false);
    this.campoInicial.set(undefined);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { seccion: id, ajustes: id === 'informacion' && this.inspectorAjustesAbierto() ? '1' : null, ajuste: null, editar: null, campo: null, mensaje: null },
      queryParamsHandling: 'merge',
    });
    if (id === 'historial') this.cargarHistorial();
  }

  abrirPerfil(campo?: string): void {
    this.abrirEnEdicion.set(true);
    this.campoInicial.set(campo);
    this.seccionActiva.set('informacion');
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { seccion: 'informacion', editar: '1', campo: campo ?? null, mensaje: null },
      queryParamsHandling: 'merge',
    });
  }

  abrirFlujoDeAjustes(): void {
    this.seccionActiva.set('informacion');
    this.inspectorAjustesAbierto.set(false);
    this.ajusteSeleccionadoId.set(null);
    this.revision.notaSeleccionadaId.set(null);
    // Una orientación general no tiene un campo ni un indicador asociado. Solo en ese caso se
    // abre el panel directamente; las observaciones por campo se abren exclusivamente desde su
    // símbolo junto al dato correspondiente.
    if (!this.revision.hayNotas() && this.revision.revision()?.observacionGeneral) {
      this.inspectorAjustesAbierto.set(true);
    }
    this.actualizarDireccionDelInspector();
  }

  cerrarInspectorDeAjustes(): void {
    if (this.editandoAjuste()) return;
    this.inspectorAjustesAbierto.set(false);
    this.ajusteSeleccionadoId.set(null);
    this.revision.notaSeleccionadaId.set(null);
    this.mensajeAjuste.set('');
    this.errorAjuste.set('');
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ajustes: null, ajuste: null, editar: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  seleccionarAjuste(nota: ObservacionDeCampo): void {
    if (this.editandoAjuste() && this.ajusteSeleccionado()?.id !== nota.id) return;
    this.seccionActiva.set('informacion');
    this.inspectorAjustesAbierto.set(true);
    this.ajusteSeleccionadoId.set(nota.id);
    this.revision.notaSeleccionadaId.set(nota.id);
    this.editandoAjuste.set(false);
    this.mensajeAjuste.set('');
    this.errorAjuste.set('');
  }

  editarElAjuste(nota: ObservacionDeCampo): void {
    this.ajusteSeleccionadoId.set(nota.id);
    this.campoInicial.set(nota.campoId);
    this.editandoAjuste.set(true);
    this.mensajeAjuste.set('');
    this.errorAjuste.set('');
  }

  cancelarEdicionDelAjuste(): void {
    this.editandoAjuste.set(false);
  }

  editarOrientacionGeneral(): void {
    this.ajusteSeleccionadoId.set(null);
    this.campoInicial.set(undefined);
    this.editandoAjuste.set(true);
    this.mensajeAjuste.set('');
    this.errorAjuste.set('');
  }

  seccionEditableDelAjuste(nota: ObservacionDeCampo | null): string | undefined {
    if (!nota) return undefined;
    return ['generales', 'contacto-festival', 'musica-festival'].includes(nota.seccionId)
      ? nota.seccionId
      : undefined;
  }

  etiquetaSeccionDelAjuste(nota: ObservacionDeCampo): string {
    const etiquetas: Record<string, string> = {
      generales: 'Información general',
      'contacto-festival': 'Contacto del Festival',
      'musica-festival': 'Prácticas y territorios sonoros',
    };
    return etiquetas[nota.seccionId] ?? 'Información del Festival';
  }

  cargarHistorial(): void {
    const festival = this.festival();
    if (!festival || this.cargandoHistorial()) return;
    this.cargandoHistorial.set(true); this.errorHistorial.set('');
    this.api.obtenerHistorialFestival(this.organizacionId(), festival.id).subscribe({
      next: entradas => { this.historial.set(entradas); this.cargandoHistorial.set(false); },
      error: (fallo: FalloDelServidor) => { this.errorHistorial.set(fallo?.message ?? 'No fue posible consultar el historial.'); this.cargandoHistorial.set(false); },
    });
  }

  etiquetaAccionHistorial(accion: string): string {
    const etiquetas: Record<string, string> = {
      FestivalEnviadoARevision: 'Enviado a revisión', FestivalAjustesSolicitados: 'Ajustes solicitados',
      FestivalPublicado: 'Publicado', FestivalRechazado: 'No aprobado', FestivalArchivado: 'Archivado',
      EdicionEnviadaARevision: 'Edición enviada a revisión', EdicionCambiosPedidosPorCampo: 'Ajustes solicitados para la edición',
      EdicionPublicada: 'Edición publicada', EdicionRechazada: 'Edición no aprobada',
    };
    return etiquetas[accion] ?? 'Actualización registrada';
  }

  territorioDelFestival(): string {
    const festival = this.festival();
    if (!festival) return 'Territorio por definir';
    return [festival.nombreMunicipio, festival.nombreDepartamento].filter(Boolean).join(', ')
      || 'Territorio por definir';
  }

  descripcionDeLaSeccion(): string {
    const descripciones: Record<string, string> = {
      resumen: 'Estado actual, siguiente acción y datos esenciales del registro.',
      informacion: 'Datos permanentes que identifican al Festival y su presencia pública.',
      ediciones: 'Realizaciones concretas del Festival, con sus propios datos y estados.',
      historial: 'Movimientos y decisiones que permiten seguir la trazabilidad del registro.',
    };
    return descripciones[this.seccionActiva()] ?? '';
  }

  /**
   * Trae los cambios pedidos, si los hay. Mismo criterio que tenía `SeccionEcosistemaComponent`
   * antes de esta fase: no se piden para cualquier ficha, solo cuando el estado o el contador lo
   * justifican -una organización con cuarenta Festivales no puede pagar cuarenta peticiones para
   * pintar una lista-.
   */
  private cargarCambiosPedidos(festival: FestivalDeLaOrganizacion): void {
    this.revision.alAtender = (observacionId, atendida) => this.cambiosPedidos.atender(observacionId, atendida);
    const devuelto = estadoDelFestival(festival.estado) === 'AjustesSolicitados';
    if (!festival.cambiosPedidos && !devuelto) {
      this.revision.modo.set('lectura');
      this.revision.sembrar(null, festival.id, festival.nombre);
      return;
    }

    this.revision.modo.set(devuelto ? 'atencion' : 'lectura');
    this.revision.sembrar(null, festival.id, festival.nombre);
    this.cambiosPedidos.obtener(festival.id).subscribe({
      next: revision => {
        if (!revision) {
          this.inspectorAjustesAbierto.set(false);
          this.editandoAjuste.set(false);
          return;
        }
        this.revision.sembrar(revision, festival.id, festival.nombre);
        const pedida = this.ajusteSeleccionadoId();
        const seleccionada = pedida === null
          ? null
          : revision.observaciones.find(nota => nota.id === pedida) ?? null;
        this.ajusteSeleccionadoId.set(seleccionada?.id ?? null);
        this.revision.notaSeleccionadaId.set(seleccionada?.id ?? null);
        if (!seleccionada && revision.observaciones.length > 0) {
          this.inspectorAjustesAbierto.set(false);
          this.editandoAjuste.set(false);
        }
      },
      error: (fallo: FalloDelServidor) =>
        this.revision.error.set(fallo?.message ?? 'No fue posible consultar las sugerencias de ajuste'),
    });
  }

  private recargarFestival(): void {
    this.api.obtenerFestivales(this.organizacionId()).subscribe(festivales => {
      const actual = festivales.find(item => item.id === this.festival()?.id) ?? null;
      this.festival.set(actual);
      if (actual) this.cargarCambiosPedidos(actual);
    });
  }

  alGuardarLaFicha(): void {
    this.abrirEnEdicion.set(false);
    this.campoInicial.set(undefined);
    this.recargarFestival();
  }

  alGuardarElAjuste(): void {
    const nota = this.ajusteSeleccionado();
    if (!nota || this.guardandoResolucion()) return;
    if (nota.estado === 'atendida') {
      this.editandoAjuste.set(false);
      this.recargarFestival();
      return;
    }

    this.guardandoResolucion.set(true);
    this.errorAjuste.set('');
    this.cambiosPedidos.atender(nota.id, true).subscribe({
      next: actualizada => {
        this.guardandoResolucion.set(false);
        this.revision.marcarAtendida(actualizada.id, actualizada.estado === 'atendida', actualizada.fechaAtencion);
        this.editandoAjuste.set(false);
        this.inspectorAjustesAbierto.set(false);
        this.ajusteSeleccionadoId.set(null);
        this.revision.notaSeleccionadaId.set(null);
        this.campoInicial.set(undefined);
        this.mensajeAjuste.set('El ajuste quedó guardado.');
        window.setTimeout(() => this.mensajeAjuste.set(''), 3500);
        this.recargarFestival();
      },
      error: (fallo: FalloDelServidor) => {
        this.guardandoResolucion.set(false);
        this.errorAjuste.set(fallo?.message ?? 'El cambio se guardó, pero no fue posible cerrar el ajuste. Inténtalo nuevamente.');
      },
    });
  }

  alGuardarOrientacionGeneral(): void {
    this.editandoAjuste.set(false);
    this.mensajeAjuste.set('Los cambios quedaron guardados. Revisa la información y envía nuevamente el Festival cuando esté listo.');
    this.recargarFestival();
  }

  /**
   * Mantiene URL y ficha sincronizadas incluso cuando Angular reutiliza el mismo componente.
   * Esto ocurre al pulsar un aviso mientras ya se está consultando ese Festival.
   */
  private aplicarParametrosDeNavegacion(parametros: ParamMap): void {
    const seccionPedida = parametros.get('seccion');
    if (this.secciones.some(seccion => seccion.id === seccionPedida)) {
      this.seccionActiva.set(seccionPedida!);
    }

    const ajustePedido = Number(parametros.get('ajuste'));
    const ajusteId = Number.isInteger(ajustePedido) && ajustePedido > 0 ? ajustePedido : null;
    const enFlujoDeAjustes = parametros.get('ajustes') === '1' || ajusteId !== null;
    const editar = parametros.get('editar') === '1';

    this.abrirEnEdicion.set(editar);
    this.campoInicial.set(parametros.get('campo') ?? undefined);
    this.ajusteSeleccionadoId.set(ajusteId);
    this.revision.notaSeleccionadaId.set(ajusteId);
    this.inspectorAjustesAbierto.set(ajusteId !== null);
    this.editandoAjuste.set(ajusteId !== null && editar);
    if (enFlujoDeAjustes) this.seccionActiva.set('informacion');
    if (this.seccionActiva() === 'historial' && this.festival()) this.cargarHistorial();

    const mensaje = parametros.get('mensaje');
    if (mensaje === 'creado') {
      this.mensajeDeLlegada.set('El Festival quedó creado como borrador. Complétalo y envíalo a revisión cuando esté listo.');
    } else if (mensaje === 'reenviado') {
      this.mensajeDeLlegada.set('El Festival se envió nuevamente a revisión institucional.');
    }
  }

  /**
   * La URL identifica el espacio de trabajo, no cada interacción interna. Cambiarla al seleccionar
   * una observación activaba la restauración de desplazamiento del Router y llevaba la página al
   * inicio. La selección y la edición permanecen ahora como estado local del inspector.
   */
  private actualizarDireccionDelInspector(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { seccion: 'informacion', ajustes: '1', ajuste: null, editar: null, campo: null, mensaje: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  abrirContactoPublico(): void {
    const festival = this.festival();
    if (!festival || !this.festivalPublicado()) return;
    this.errorContactoPublico.set('');
    this.mensajeContactoPublico.set('');
    this.contactoPublico.set({
      correoContacto: festival.correoContacto ?? null,
      telefonoCelular: festival.telefonoCelular ?? null,
      instagram: festival.instagram ?? null,
      facebook: festival.facebook ?? null,
      paginaWeb: festival.paginaWeb ?? null,
      otroEnlace: festival.otroEnlace ?? null,
    });
    this.editandoContactoPublico.set(true);
  }

  cancelarContactoPublico(): void {
    this.editandoContactoPublico.set(false);
    this.errorContactoPublico.set('');
  }

  guardarContactoPublico(): void {
    const festival = this.festival();
    if (!festival || this.guardandoContactoPublico()) return;
    this.guardandoContactoPublico.set(true);
    this.errorContactoPublico.set('');
    this.api.actualizarContactoPublico(festival.id, this.contactoPublico()).subscribe({
      next: actualizado => {
        this.guardandoContactoPublico.set(false);
        this.festival.set(actualizado);
        this.editandoContactoPublico.set(false);
        this.mensajeContactoPublico.set('Los datos públicos de contacto quedaron actualizados.');
      },
      error: (fallo: FalloDelServidor) => {
        this.guardandoContactoPublico.set(false);
        this.errorContactoPublico.set(fallo?.message ?? 'No fue posible actualizar el contacto público');
      },
    });
  }

  enviarFestivalARevision(): void {
    const festival = this.festival();
    if (!festival || !this.puedeEnviarFestival() || this.enviandoFestival()) return;
    this.enviandoFestival.set(true); this.error.set('');
    this.api.enviarFestivalARevision(festival.id).subscribe({
      next: actualizado => {
        this.enviandoFestival.set(false);
        this.festival.set(actualizado);
        this.inspectorAjustesAbierto.set(false);
        this.editandoAjuste.set(false);
        this.seccionActiva.set('resumen');
        this.router.navigate(['/gestion/procesos/festivales', actualizado.id], { queryParams: { mensaje: 'reenviado' } });
      },
      error: (fallo: FalloDelServidor) => { this.enviandoFestival.set(false); this.error.set(fallo?.message ?? 'No fue posible enviar el Festival a revisión.'); },
    });
  }
}
