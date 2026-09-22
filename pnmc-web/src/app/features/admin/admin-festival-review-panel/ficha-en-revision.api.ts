import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApiClientService } from '../../../core/http/api-client.service';
import {
  GuardarRevisionSolicitud,
  ComparacionEnviosFestival,
  RevisionDeCampos,
} from '../../../core/revision-de-campos/revision-de-campos';
import {
  CatalogosDelFestival,
  FestivalDeLaOrganizacion,
  NotificacionDelPanel,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  PropuestaCambioFestival,
  ResponsableOrganizacion,
  TipoDocumento,
  UbicacionDivipola,
} from '../../panel-organizacion/panel-organizacion.api';

/**
 * La misma ficha del Festival, servida por la puerta institucional.
 *
 * <b>POR QUÉ IMPLEMENTA `PanelOrganizacionApi`.</b> Porque el funcionario tiene que ver
 * EXACTAMENTE la ficha que llenó la organización —«la idea es que la ficha tenga el mismo diseño
 * que la que diligenció el usuario de la organización», del usuario— y esa
 * ficha es `FichaFestivalComponent`, que pide sus datos por este contrato. Reescribir el componente
 * para la consola daría dos pantallas que se parecen hoy y divergen a la tercera semana.
 *
 * <b>POR QUÉ NO SIRVEN LAS RUTAS EXTERNAS.</b> El funcionario tiene cookie `pnmc.admin` y la
 * organización `pnmc.external`: son dos autenticaciones distintas, y `/externo/versiones/{id}` le
 * contesta 401 aunque el dato sea el mismo. Las cuatro lecturas de aquí van a `/institucional/*`,
 * que del lado del servidor llaman a los mismos métodos de proyección —así las dos respuestas
 * tienen la misma forma por construcción y no por costumbre—.
 *
 * <b>LAS ESCRITURAS FALLAN A PROPÓSITO.</b> Un funcionario no corrige la ficha de una organización:
 * pide que la corrijan. Devolver un error explícito y no un `503` amable es lo que hace que el fallo
 * salga en la primera pasada si alguien conecta un botón de guardar por descuido.
 */
@Injectable()
export class FichaEnRevisionApi extends PanelOrganizacionApi {
  private readonly api = inject(ApiClientService);

  private testigo(fallo: string): Observable<{ requestToken: string }> {
    return this.api.get<{ requestToken: string }>('/api/v1/institucional/festivales/csrf', {
      errorFallback: fallo,
    });
  }

  // ── Lo que la ficha necesita para pintarse ────────────────────────────────────────────────

  obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> {
    return this.api.get<CatalogosDelFestival>('/api/v1/institucional/catalogos/festival', {
      errorFallback: 'No fue posible cargar los catálogos del Festival',
    });
  }

  obtenerUbicaciones(): Observable<UbicacionDivipola[]> {
    return this.api.get<UbicacionDivipola[]>('/api/v1/publico/divipola', {
      errorFallback: 'No fue posible consultar los departamentos y municipios',
    });
  }

  obtenerFestivalEnRevision(festivalId: string): Observable<FestivalDeLaOrganizacion> {
    return this.api.get<FestivalDeLaOrganizacion>(
      `/api/v1/institucional/festivales/${festivalId}`,
      { errorFallback: 'No fue posible cargar la información completa del Festival' },
    );
  }

  // AQUI LEIA LOS PERFILES VERSIONADOS —`/institucional/festivales/{id}/perfiles-versionados` y
  // `/institucional/perfiles-versionados/{id}`—, para llenar la pestaña «Ediciones» de la ficha.
  // Esa pestaña no la abría nadie: los cuatro montajes del componente forzaban la del Festival.
  // Las DOS RUTAS SIGUEN EXISTIENDO y funcionan; lo que se retira es este adaptador, que era su
  // único cliente. Si algún día la consola necesita leer el historial versionado de un Festival,
  // las rutas están, y la revisión de qué pantalla lo enseña es un asunto aparte.

  // ── La revisión campo por campo ───────────────────────────────────────────────────────────

  obtenerRevision(festivalId: string): Observable<RevisionDeCampos> {
    return this.api.get<RevisionDeCampos>(
      `/api/v1/institucional/festivales/${festivalId}/revision`,
      { errorFallback: 'No fue posible abrir la revisión del Festival' },
    );
  }

  obtenerComparacionEnvios(festivalId: string): Observable<ComparacionEnviosFestival | null> {
    return this.api.get<ComparacionEnviosFestival | null>(
      `/api/v1/institucional/festivales/${festivalId}/comparacion-envios`,
      { errorFallback: 'No fue posible comparar este envío con el anterior' },
    );
  }

  guardarBorrador(festivalId: string, solicitud: GuardarRevisionSolicitud): Observable<RevisionDeCampos> {
    return this.testigo('No fue posible preparar el guardado de la revisión').pipe(
      switchMap(token => this.api.put<RevisionDeCampos>(
        `/api/v1/institucional/festivales/${festivalId}/revision`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar el borrador de la revisión',
        },
      )),
    );
  }

  enviarSolicitud(festivalId: string, solicitud: GuardarRevisionSolicitud): Observable<RevisionDeCampos> {
    return this.testigo('No fue posible preparar el envío de las sugerencias de ajuste').pipe(
      switchMap(token => this.api.post<RevisionDeCampos>(
        `/api/v1/institucional/festivales/${festivalId}/revision/enviar`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible enviar las sugerencias de ajuste',
        },
      )),
    );
  }

  /** Publicar o rechazar el registro inicial, desde la misma ficha que se revisó. */
  decidirFestival(
    festivalId: string,
    accion: 'Publicar' | 'Rechazar',
    motivoRechazo?: string,
  ): Observable<{ id: string; estado: string }> {
    return this.testigo('No fue posible preparar la decisión institucional').pipe(
      switchMap(token => this.api.post<{ id: string; estado: string }>(
        `/api/v1/institucional/festivales/${festivalId}/decisiones`,
        { accion, motivoRechazo: motivoRechazo || null },
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible registrar la decisión institucional',
        },
      )),
    );
  }

  // ── Lo que la consola NO hace, y por qué ──────────────────────────────────────────────────
  //
  // NINGUNA DE ESTAS LA LLAMA LA FICHA EN MODO REVISION: `editando()` se queda en `false`, así que
  // no hay formulario, no hay «Guardar borrador» y no hay alta. Están aquí porque el contrato las
  // declara, y fallan en vez de devolver algo plausible: un `of(null)` amable convertiría un botón
  // conectado por descuido en un guardado silencioso que no guarda nada.

  private negar<T>(que: string): Observable<T> {
    return throwError(() => new Error(
      `La consola institucional no ${que}. Desde aquí se piden cambios; los aplica la organización.`));
  }

  obtenerPerfil(): Observable<PerfilOrganizacion> { return this.negar('lee el perfil de la organización'); }
  guardarPerfil(): Observable<PerfilOrganizacion> { return this.negar('edita el perfil de la organización'); }
  obtenerResponsable(): Observable<ResponsableOrganizacion> { return this.negar('lee la persona responsable'); }
  guardarResponsable(): Observable<ResponsableOrganizacion> { return this.negar('edita la persona responsable'); }
  obtenerTiposDocumento(): Observable<TipoDocumento[]> { return this.negar('lee los tipos de documento'); }
  obtenerFestivales(): Observable<FestivalDeLaOrganizacion[]> { return this.negar('lista los Festivales de la organización'); }
  enviarFestivalARevision(): Observable<FestivalDeLaOrganizacion> { return this.negar('envía Festivales a revisión'); }
  iniciarPropuesta(): Observable<PropuestaCambioFestival> { return this.negar('inicia propuestas de cambio'); }
  obtenerPropuestaActiva(): Observable<PropuestaCambioFestival> { return this.negar('lee propuestas de cambio'); }
  guardarPropuesta(): Observable<PropuestaCambioFestival> { return this.negar('edita propuestas de cambio'); }
  enviarPropuestaARevision(): Observable<unknown> { return this.negar('envía propuestas a revisión'); }
  obtenerNotificaciones(): Observable<PaginaDeNotificaciones> { return this.negar('lee las notificaciones de la organización'); }
  marcarNotificacionLeida(): Observable<NotificacionDelPanel> { return this.negar('marca notificaciones'); }
  crearFestival(): Observable<FestivalDeLaOrganizacion> { return this.negar('crea Festivales'); }
  guardarFestival(): Observable<FestivalDeLaOrganizacion> { return this.negar('edita Festivales'); }
  override actualizarContactoPublico(): Observable<FestivalDeLaOrganizacion> { return this.negar('actualiza contactos públicos'); }
}
