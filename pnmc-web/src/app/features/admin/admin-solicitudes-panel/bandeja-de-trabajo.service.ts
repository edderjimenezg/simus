import { Injectable, computed, inject, signal } from '@angular/core';
import { AdminService, AlertaDeCalidad, EventoEnRevision } from '../../../core/services/admin.service';
import { MercadosService, PropuestaDeMercadoEnRevision } from '../../../core/services/mercados.service';
import { SessionService } from '../../../core/services/session.service';

/** Las posiciones del filtro de la bandeja: «todos» y un tipo de asunto por cada cola que la nutre. */
export type FiltroDeLaBandeja =
  'todos' | 'revision' | 'edicion' | 'evento' | 'propuesta' | 'solicitud' | 'retiro' | 'reclamacion' | 'duplicado' | 'alerta';

/**
 * LA COLA DE LO QUE ESPERA UNA DECISIÓN DEL PROGRAMA, fuera del armazón.
 *
 * <b>POR QUÉ ES UN SERVICIO Y NO UNA SEÑAL DEL PANEL.</b> La bandeja la miran tres sitios que no
 * son el mismo componente: el panel de Solicitudes, que la lista y decide; la barra izquierda, que
 * enseña cuántos asuntos esperan; y el Resumen operativo, que los desglosa y lleva a la bandeja ya
 * filtrada. Hasta las ocho listas y su unión vivían en el armazón, que
 * con eso cargaba treinta y cinco miembros y cuatrocientas líneas de plantilla que no eran suyas.
 * Aquí viven una sola vez; quien las necesita las inyecta.
 *
 * <b>QUIÉN CARGA.</b> El armazón, porque es quien sabe cuándo hay sesión y cuándo sondear; el panel
 * no pide nada al montarse. Las decisiones que toma el panel llaman a `recargar()` para que el
 * asunto decidido salga de la cola, con los duplicados y alertas incluidos.
 */
@Injectable({ providedIn: 'root' })
export class BandejaDeTrabajoService {
  private readonly adminService = inject(AdminService);
  private readonly sessionService = inject(SessionService);
  private readonly mercadosService = inject(MercadosService);

  /** Festivales en revisión, ya con la forma que la cola espera (módulo, título, responsable). */
  readonly revisiones = signal<any[]>([]);
  /**
   * Las propuestas de cambio sobre un Festival YA PUBLICADO.
   *
   * NO SON LO MISMO QUE `revisiones()`. Aquella trae Festivales en `en_revision` -un registro
   * nuevo, o uno que vuelve de `ajustes_solicitados`-; esta trae propuestas sobre un Festival que
   * ya está visible en el sitio público y no cambia hasta que la propuesta se decide. Hasta el 2
   * de septiembre de 2026 esta lista solo vivía dentro de `AdminFestivalReviewPanelComponent`, una
   * bandeja aparte que la unificada no conocía: el usuario lo vio en vivo -«me dice que hay una
   * solicitud... pero abajo sí aparece una que dice propuesta cambio de festival»-, dos bandejas
   * que deberían concordar y no se hablaban entre sí.
   */
  readonly propuestas = signal<any[]>([]);

  /**
   * Las propuestas de cambio sobre un MERCADO ya publicado.
   *
   * <b>ES UNA COLA APARTE Y NO UNA MAS EN `propuestas()`.</b> Las de Festival vienen de tablas
   * propias con la forma de aquel circuito, y las de Mercado de las tablas genéricas por módulo que
   * nacieron. Mezclarlas obligaría a que cada línea de la bandeja
   * adivinara de cuál de las dos formas viene; separadas, cada una se traduce una vez al entrar y
   * abajo las dos son filas iguales con el mismo tipo de asunto.
   */
  readonly propuestasDeMercado = signal<PropuestaDeMercadoEnRevision[]>([]);
  readonly ediciones = signal<any[]>([]);
  readonly eventos = signal<EventoEnRevision[]>([]);
  /** Vinculaciones y retiros, tal como llegan de `cargarSolicitudesDeVinculacion`. */
  readonly solicitudes = signal<any[]>([]);
  readonly reclamaciones = signal<any[]>([]);
  readonly duplicados = signal<any[]>([]);
  /**
   * Los hallazgos de calidad sobre un registro: territorio incompleto, dato que se contradice.
   *
   * Los cargaba el panel de «Calidad y coincidencias», que era una segunda pantalla para lo mismo. Al
   * unificar la bandeja pasaron aquí, junto a los duplicados, que el
   * armazón ya leía para la cifra del Resumen operativo.
   */
  readonly alertas = signal<AlertaDeCalidad[]>([]);

  readonly filtro = signal<FiltroDeLaBandeja>('todos');

  /**
   * Un festival cuyo trámite alguien pidió abrir desde fuera de la bandeja: un aviso de la
   * campana, o «Solicitudes» desde la tabla de Festivales. El panel lo atiende en cuanto el
   * trámite existe en la cola —esté ya cargada o llegue después— y lo vacía.
   */
  readonly tramiteDeFestivalPedido = signal<string | null>(null);

  // LOS ESTADOS SON LOS DE `RecordGovernanceEndpoints.LinkStatuses`, EN ESPAÑOL. Esta lista
  // decía 'approved', 'rejected', 'cancelled', 'archived', 'resuelto' -ninguno de los seis
  // valores que de verdad escribe el servidor-, así que la comparación nunca era cierta y una
  // solicitud ya aprobada o rechazada se quedaba en la bandeja para siempre, indistinguible de
  // una pendiente. Con datos reales de prueba se vio: tres «Solicitudes de eliminación» ya
  // decididas seguían listadas como si necesitaran una decisión.
  readonly solicitudesActivas = computed(() => this.solicitudes().filter(item => !['aprobada', 'rechazada', 'cancelada'].includes(String(item.status ?? '').toLowerCase())));
  /**
   * Los módulos que producen una solicitud de retiro de un registro publicado.
   *
   * <b>SON DOS DESDE EL 15 DE SEPTIEMBRE DE 2026.</b> Mercados Musicales también deja pedir el
   * retiro de lo publicado, y comparar contra un solo literal habría creado solicitudes que nadie
   * ve: se guardan, no salen en la bandeja, y quien las pidió espera una decisión que no llega.
   */
  private static readonly MODULOS_DE_RETIRO = ['festivales_retiro', 'mercados_retiro'];

  private esRetiro(moduleId: string | null | undefined): boolean {
    return BandejaDeTrabajoService.MODULOS_DE_RETIRO.includes(String(moduleId ?? ''));
  }

  readonly retiros = computed(() => this.solicitudesActivas().filter(item => this.esRetiro(item.moduleId)));
  readonly reclamacionesActivas = computed(() => this.reclamaciones().filter(item => ['enviada', 'en_revision', 'requiere_aclaracion', 'aclaracion_enviada'].includes(String(item.estado ?? '').toLowerCase())));

  /**
   * LA COLA ENTERA, ANTES DE FILTRAR.
   *
   * Se separó de `colaVisible()` porque el filtro por tipo
   * necesita decir CUANTOS hay de cada tipo, y un filtro que se cuenta a sí mismo sobre la lista
   * ya filtrada siempre respondería «todos» para la posición puesta y «cero» para las demás.
   */
  readonly colaCompleta = computed(() => {
    const reviews = this.revisiones().map(item => ({
      // EL ID LLEVA EL MÓDULO. `bandeja.revisiones()` junta las filas de TODOS los módulos -Festivales,
      // Escuelas, Mercados...-, cada uno con su propio autoincremental: el registro 101 de
      // Escuelas y el 101 de Lutería son filas distintas que sin el módulo en el id compartían
      // `review-101`, y seleccionar una abría el panel de la otra. Se vio en vivo el 1 de
      // septiembre de 2026 al decidir sobre un asunto y ver aparecer «seleccionado» uno que
      // nadie había tocado.
      id: `review-${item.moduleId}-${item.id}`, rawId: item.id, festivalId: item.moduleId === 'festivals' ? item.id : null, kind: 'revision',
      type: Number(item.numeroEnvio ?? 1) > 1
        ? `Reenvío de Festival · ciclo ${item.numeroEnvio}`
        : 'Registro inicial de Festival',
      module: item.moduleId || 'Festival', record: item.title || item.nombre || `Registro #${item.id}`, organization: item.owner || item.organizacionPrincipalNombre || 'Organización sin identificar', status: item.status || 'en_revision', createdAt: item.updatedAt || item.fechaEnvioRevision, raw: item,
    }));
    const proposals = this.propuestas().map(item => ({
      id: `proposal-${item.id}`, rawId: item.id, festivalId: item.festivalOrigenId, kind: 'propuesta', type: 'Propuesta de cambio', module: 'festivals', record: item.nombreFestival || `Festival #${item.id}`, organization: item.organizacionNombre || 'Organización sin identificar', status: 'en_revision', createdAt: item.fechaEnvioRevision, raw: item,
    }));
    // LA MISMA FILA QUE UNA PROPUESTA DE FESTIVAL, con el módulo que la distingue. Quien mira la
    // bandeja no tiene por qué saber que por debajo son dos tablas distintas.
    const propuestasDeMercado = this.propuestasDeMercado().map(item => ({
      id: `proposal-mercado-${item.id}`, rawId: item.id, festivalId: null, kind: 'propuesta',
      type: 'Propuesta de cambio', module: 'mercados',
      record: item.nombreDelRegistro, organization: item.organizacionNombre || 'Organización sin identificar',
      status: 'en_revision', createdAt: item.fechaEnvio, raw: item,
    }));
    const ediciones = this.ediciones().map(item => ({ id: `edition-${item.id}`, rawId: item.id, festivalId: item.festivalId, kind: 'edicion', type: 'Edición en revisión', module: 'ediciones_festival', record: `${item.anio} · ${item.nombre}`, organization: item.organizacionPrincipalNombre, status: 'en_revision', createdAt: item.fechaEnvioRevision, raw: item }));
    const requests = this.solicitudesActivas().map(item => ({
      id: `request-${item.id}`, rawId: item.id, festivalId: item.recordId, kind: this.esRetiro(item.moduleId) ? 'retiro' : 'solicitud', type: this.esRetiro(item.moduleId) ? 'Solicitud de eliminación' : 'Solicitud institucional', module: item.moduleId || 'Ecosistema', record: item.recordName || `Registro #${item.recordId ?? '—'}`, organization: item.entidadNombre || `Organización #${item.entidadId ?? '—'}`, status: item.status || 'pendiente', createdAt: item.createdAt, raw: item,
    }));
    const claims = this.reclamacionesActivas().map(item => ({
      id: `claim-${item.id}`, rawId: item.id, festivalId: item.registroCanonicoId, kind: 'reclamacion', type: 'Reclamación de administración', module: 'Festival', record: item.registroNombre || `Registro #${item.registroCanonicoId ?? '—'}`, organization: item.organizacionSolicitanteNombre || `Organización #${item.organizacionSolicitanteId ?? '—'}`, status: item.estado || 'enviada', createdAt: item.fechaEnvio ?? item.fechaCreacion, raw: item,
    }));
    // LOS DOS QUE NADIE PIDIO. Un duplicado y una alerta no son trámites: no tienen solicitante
    // ni fecha de envío, los detecta el sistema al importar y al registrar. Entran igual en la
    // bandeja porque el criterio de la bandeja es «lo que espera una decisión», y estos la
    // esperan; lo que cambia es el verbo, y eso lo resuelve `accionRapida`.
    const duplicados = this.duplicados()
      .filter(item => (item.status ?? 'pendiente') === 'pendiente')
      .map(item => ({
        id: `duplicate-${item.id}`, rawId: item.id, festivalId: null, kind: 'duplicado',
        type: 'Posible duplicado', module: item.moduleId || 'Ecosistema',
        record: `Registros ${item.sourceRecordId} y ${item.candidateRecordId}`,
        // NO SE INVENTA UNA ORGANIZACION: el contrato de duplicados no la trae. Escribir
        // «Organización sin identificar» diría que se buscó y no había; esto dice de dónde sale.
        organization: 'Detectado por el sistema',
        status: item.status || 'pendiente', createdAt: item.createdAt, raw: item,
      }));
    const alertas = this.alertas()
      .filter(item => ['abierta', 'en_revision'].includes(item.status ?? 'abierta'))
      .map(item => ({
        id: `alert-${item.id}`, rawId: item.id, festivalId: null, kind: 'alerta',
        type: 'Alerta de calidad', module: item.moduleId || 'Ecosistema',
        record: `${item.moduleId || 'Registro'} #${item.recordId}`,
        organization: 'Detectado por el sistema',
        status: item.status || 'abierta', createdAt: item.createdAt, raw: item,
      }));
    // LOS EVENTOS QUE UNA ORGANIZACION ENVIO A LA AGENDA. Hasta se
    // quedaban esperando sin aparecer en ninguna parte de la bandeja: el circuito los ponía «en
    // revisión» y la única forma de verlos era abrir Agenda y filtrar por estado. Lo reportó el
    // dirección de producto: «al enviar evento a revisión debería aparecer en la bandeja».
    const eventos = this.eventos().map(item => ({
      id: `evento-${item.id}`, rawId: item.id, festivalId: item.festivalId, kind: 'evento',
      type: 'Evento en revisión', module: 'agenda',
      record: item.titulo,
      organization: item.organizacionNombre || 'Organización sin identificar',
      status: 'en_revision', createdAt: item.fechaEnvio, raw: item,
    }));
    return [...reviews, ...ediciones, ...eventos, ...proposals, ...propuestasDeMercado,
            ...requests, ...claims, ...duplicados, ...alertas];
  });

  /**
   * Cuántos posibles duplicados ESPERAN, que no es lo mismo que cuántos hay registrados.
   *
   * La cifra del Resumen leía la lista entera, o sea todos, incluidos los ya decididos. Medido el
   * 15 de septiembre de 2026: la cifra decía 2 y la bandeja, una línea más abajo del mismo clic,
   * decía 1. Dos números para la misma pregunta obligan a comprobar cuál miente. Se deriva de la
   * cola, que es la que ya sabe qué sigue esperando.
   */
  readonly duplicadosEsperando = computed(() => this.colaCompleta().filter(f => f.kind === 'duplicado').length);

  cambiarFiltro(id: string): void {
    this.filtro.set(id as FiltroDeLaBandeja);
  }

  pedirTramiteDeFestival(festivalId: string | number): void {
    this.tramiteDeFestivalPedido.set(String(festivalId));
  }

  /** Las ocho colas de una vez. Sin sesión no pide nada: el servidor respondería 401 a todo. */
  recargar(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.cargarRevisiones();
    this.cargarPropuestas();
    this.cargarEdiciones();
    this.cargarEventos();
    this.cargarSolicitudesYHallazgos();
  }

  /**
   * Festival tiene una cola institucional propia. La consulta genérica de registros era útil
   * como explorador, pero no es la fuente de verdad de una revisión: perdía el nombre de la
   * organización y podía dejar vacía Solicitudes aunque el Festival estuviera en revisión.
   */
  cargarRevisiones(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.adminService.cargarBandejaDeFestivales().subscribe({
      next: festivales => this.revisiones.set((festivales ?? []).map(item => ({
        ...item,
        moduleId: 'festivals',
        title: item.nombre,
        owner: item.organizacionPrincipalNombre,
        status: 'en_revision',
        updatedAt: item.fechaEnvioRevision,
      }))),
      error: () => this.revisiones.set([]),
    });
  }

  cargarPropuestas(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.adminService.cargarBandejaDePropuestas().subscribe({
      next: propuestas => this.propuestas.set(propuestas ?? []),
      error: () => this.propuestas.set([]),
    });
    // LAS DE MERCADO VIENEN DE OTRA RUTA, y por eso se piden aquí y no en otro método: son el mismo
    // tipo de asunto para quien mira la bandeja, y separar la carga haría que una de las dos se
    // olvidara en el sitio donde se refresca.
    this.mercadosService.propuestasEnRevision().subscribe({
      next: filas => this.propuestasDeMercado.set(filas ?? []),
      error: () => this.propuestasDeMercado.set([]),
    });
  }

  cargarEdiciones(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.adminService.cargarBandejaDeEdiciones().subscribe({
      next: filas => this.ediciones.set(filas ?? []),
      error: () => this.ediciones.set([]),
    });
  }

  cargarEventos(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.adminService.cargarEventosEnRevision().subscribe({
      next: filas => this.eventos.set(filas ?? []),
      error: () => this.eventos.set([]),
    });
  }

  /** Vinculaciones, retiros, reclamaciones, duplicados y alertas: lo que no es un registro entero. */
  cargarSolicitudesYHallazgos(): void {
    if (!this.sessionService.isAuthenticated()) return;
    this.adminService.cargarSolicitudesDeVinculacion({ limit: 50 }).subscribe({
      next: payload => this.solicitudes.set(Array.isArray(payload) ? payload : payload?.items ?? []),
      error: () => this.solicitudes.set([]),
    });
    this.adminService.cargarReclamacionesDeAdministracion().subscribe({
      next: payload => this.reclamaciones.set(Array.isArray(payload) ? payload : []),
      error: () => this.reclamaciones.set([]),
    });
    this.adminService.cargarPosiblesDuplicados({ limit: 50 }).subscribe({
      next: payload => this.duplicados.set(Array.isArray(payload) ? payload : payload?.items ?? []),
      error: () => this.duplicados.set([]),
    });
    this.adminService.cargarAlertasDeCalidad({ limit: 50 }).subscribe({
      next: payload => this.alertas.set(Array.isArray(payload) ? payload : payload?.items ?? []),
      error: () => this.alertas.set([]),
    });
  }

  /** Al cerrar la sesión no queda nada de la anterior: ni colas, ni filtro, ni trámite pedido. */
  vaciar(): void {
    for (const cola of [this.revisiones, this.propuestas, this.ediciones, this.solicitudes, this.reclamaciones, this.duplicados]) {
      cola.set([]);
    }
    this.propuestasDeMercado.set([]);
    this.eventos.set([]);
    this.alertas.set([]);
    this.filtro.set('todos');
    this.tramiteDeFestivalPedido.set(null);
  }
}
