import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApiClientService } from '../../core/http/api-client.service';
import { PropuestaDeCambioDeMercado } from '../../core/services/mercados.service';
import {
  CatalogosDelFestival,
  CatalogosDeMercadoExternos,
  ContactoPublicoDeMercadoSolicitud,
  ContactoPublicoFestivalSolicitud,
  EdicionDeMercadoDeLaOrganizacion,
  EntradaHistorialFestival,
  EntradaHistorialMercado,
  FestivalDeLaOrganizacion,
  FestivalElegibleParaMercado,
  GuardarEdicionDeMercadoSolicitud,
  GuardarFestivalSolicitud,
  GuardarMercadoSolicitud,
  MercadoDeLaOrganizacion,
  NotificacionDelPanel,
  ObservacionDeCampoDeMercado,
  PaginaDeNotificaciones,
  PanelOrganizacionApi,
  PerfilOrganizacion,
  PerfilOrganizacionSolicitud,
  PropuestaCambioFestival,
  ResponsableOrganizacion,
  ResponsableOrganizacionSolicitud,
  TipoDocumento,
  UbicacionDivipola,
} from './panel-organizacion.api';

/**
 * La implementación de verdad de {@link PanelOrganizacionApi}.
 *
 * <b>Por qué existe este fichero.</b> El panel se construyó contra una clase abstracta para poder
 * probarse sin red, y esa clase se quedó sin implementación: la ruta no cargaba el panel, así que
 * las 2.567 líneas escritas no llegaban a la pantalla. Nada fallaba, sencillamente no se veía nada
 * distinto. Ese es el defecto que este fichero cierra, junto con el `provide` de la ruta.
 *
 * <b>Qué lleva testigo CSRF y qué no.</b> Las cuatro escrituras lo exigen porque cambian datos;
 * las lecturas no. El testigo se pide a `/external/organizations/csrf`, que es el mismo que ya
 * usan las rutas de Festival: un segundo emisor obligaría a mantener dos cadenas de antiforgery
 * para el mismo canal.
 *
 * <b>Las notificaciones no llevan testigo y no es un descuido.</b> `POST /notifications/{id}/read`
 * no valida antiforgery en el servidor (`NotificationEndpoints.cs-82`, no recibe `IAntiforgery`)
 * y se cierra con `PoliticaCualquierSesion` porque su destinatario puede ser tanto la organización
 * externa como el funcionario. Mandar una cabecera que nadie comprueba daría la impresión de una
 * protección que no está.
 */
@Injectable({ providedIn: 'root' })
export class PanelOrganizacionService extends PanelOrganizacionApi {
  private readonly api = inject(ApiClientService);

  private testigo(fallo: string): Observable<{ requestToken: string }> {
    return this.api.get<{ requestToken: string }>('/api/v1/externo/organizaciones/csrf', {
      errorFallback: fallo,
    });
  }

  obtenerPerfil(organizacionId: string): Observable<PerfilOrganizacion> {
    return this.api.get<PerfilOrganizacion>(
      `/api/v1/externo/organizaciones/${organizacionId}/perfil`,
      { errorFallback: 'No fue posible consultar los datos de la organización' },
    );
  }

  guardarPerfil(organizacionId: string, solicitud: PerfilOrganizacionSolicitud): Observable<PerfilOrganizacion> {
    return this.testigo('No fue posible preparar el guardado de la organización').pipe(
      switchMap(token => this.api.put<PerfilOrganizacion>(
        `/api/v1/externo/organizaciones/${organizacionId}/perfil`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar los datos de la organización',
        },
      )),
    );
  }

  obtenerResponsable(organizacionId: string): Observable<ResponsableOrganizacion> {
    return this.api.get<ResponsableOrganizacion>(
      `/api/v1/externo/organizaciones/${organizacionId}/responsable`,
      { errorFallback: 'No fue posible consultar la persona responsable' },
    );
  }

  guardarResponsable(organizacionId: string, solicitud: ResponsableOrganizacionSolicitud): Observable<ResponsableOrganizacion> {
    return this.testigo('No fue posible preparar el guardado de la persona responsable').pipe(
      switchMap(token => this.api.put<ResponsableOrganizacion>(
        `/api/v1/externo/organizaciones/${organizacionId}/responsable`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar la persona responsable',
        },
      )),
    );
  }

  obtenerTiposDocumento(): Observable<TipoDocumento[]> {
    // SIN ESTA LISTA NO SE PUEDE GUARDAR A LA PERSONA RESPONSABLE: el tipo de documento es NOT NULL
    // en `dbo.EntidadesResponsable`, así que un desplegable vacío deja el formulario sin salida.
    return this.api.get<TipoDocumento[]>(
      '/api/v1/externo/organizaciones/catalogos/tipos-documento',
      { errorFallback: 'No fue posible consultar los tipos de documento' },
    );
  }

  obtenerFestivales(organizacionId: string): Observable<FestivalDeLaOrganizacion[]> {
    return this.api.get<FestivalDeLaOrganizacion[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/festivales`,
      { errorFallback: 'No fue posible consultar los Festivales de la organización' },
    );
  }

  override obtenerHistorialFestival(organizacionId: string, festivalId: string): Observable<EntradaHistorialFestival[]> {
    return this.api.get<EntradaHistorialFestival[]>(`/api/v1/externo/organizaciones/${organizacionId}/festivales/${festivalId}/historial`, { errorFallback: 'No fue posible consultar el historial del Festival' });
  }

  enviarFestivalARevision(festivalId: string): Observable<FestivalDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el envío a revisión').pipe(
      switchMap(token => this.api.post<FestivalDeLaOrganizacion>(
        `/api/v1/externo/festivales/${festivalId}/enviar-a-revision`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible enviar el Festival a revisión',
        },
      )),
    );
  }

  override retirarFestival(festivalId: string): Observable<void> {
    return this.testigo('No fue posible preparar el retiro del Festival').pipe(
      switchMap(token => this.api.delete<void>(`/api/v1/externo/festivales/${festivalId}`, {
        headers: { 'X-CSRF-TOKEN': token.requestToken },
        errorFallback: 'No fue posible retirar el Festival',
      })),
    );
  }

  override solicitarRetiroFestival(festivalId: string, justificacion: string): Observable<void> {
    return this.testigo('No fue posible preparar la solicitud de eliminación').pipe(
      switchMap(token => this.api.post<void>(`/api/v1/externo/festivales/${festivalId}/solicitudes-retiro`, { justificacion }, {
        headers: { 'X-CSRF-TOKEN': token.requestToken },
        errorFallback: 'No fue posible enviar la solicitud de eliminación',
      })),
    );
  }

  iniciarPropuesta(festivalId: string): Observable<PropuestaCambioFestival> {
    return this.testigo('No fue posible preparar la propuesta de cambios').pipe(
      switchMap(token => this.api.post<PropuestaCambioFestival>(
        `/api/v1/externo/festivales/${festivalId}/propuestas-cambio`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible iniciar la propuesta de cambios',
        },
      )),
    );
  }

  obtenerPropuestaActiva(festivalId: string): Observable<PropuestaCambioFestival> {
    return this.api.get<PropuestaCambioFestival>(
      `/api/v1/externo/festivales/${festivalId}/propuesta-cambio`,
      { errorFallback: 'No fue posible consultar la propuesta de cambios' },
    );
  }

  guardarPropuesta(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<PropuestaCambioFestival> {
    return this.testigo('No fue posible preparar el guardado de la propuesta').pipe(
      switchMap(token => this.api.put<PropuestaCambioFestival>(
        `/api/v1/externo/festivales/${festivalId}/propuesta-cambio`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar la propuesta de cambios',
        },
      )),
    );
  }

  enviarPropuestaARevision(festivalId: string): Observable<unknown> {
    return this.testigo('No fue posible preparar el envío de la propuesta').pipe(
      switchMap(token => this.api.post<unknown>(
        `/api/v1/externo/festivales/${festivalId}/propuesta-cambio/enviar-a-revision`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible enviar la propuesta a revisión',
        },
      )),
    );
  }

  obtenerNotificaciones(): Observable<PaginaDeNotificaciones> {
    return this.api.get<PaginaDeNotificaciones>('/api/v1/notificaciones', {
      params: { ambito: 'externo', limite: 50 },
      errorFallback: 'No fue posible consultar tus notificaciones',
    });
  }

  marcarNotificacionLeida(notificacionId: string): Observable<NotificacionDelPanel> {
    return this.api.post<NotificacionDelPanel>(
      `/api/v1/notificaciones/${notificacionId}/lectura?ambito=externo`,
      {},
      { errorFallback: 'No fue posible marcar la notificación como leída' },
    );
  }

  override ocultarNotificacionesLeidas(): Observable<{ ocultadas: number }> {
    return this.api.post<{ ocultadas: number }>(
      '/api/v1/notificaciones/leidas/ocultar?ambito=externo',
      {},
      { errorFallback: 'No fue posible limpiar los avisos leídos' },
    );
  }

  // ── La ficha completa del Festival: las versiones ──────────────────────────────────────────

  obtenerCatalogosDelFestival(): Observable<CatalogosDelFestival> {
    return this.api.get<CatalogosDelFestival>('/api/v1/externo/catalogos/festival', {
      errorFallback: 'No fue posible cargar los catálogos del Festival',
    });
  }

  /**
   * DIVIPOLA POR ESTE CONTRATO Y NO POR `AdminService`.
   *
   * El catálogo público se consume tipado: una propiedad mal escrita no puede vaciar el desplegable
   * de municipios en silencio. Además queda dentro de la clase
   * abstracta que la prueba sustituye, que es lo que permite montar el formulario sin red.
   */
  obtenerUbicaciones(): Observable<UbicacionDivipola[]> {
    return this.api.get<UbicacionDivipola[]>('/api/v1/publico/divipola', {
      errorFallback: 'No fue posible consultar los departamentos y municipios',
    });
  }

  crearFestival(organizacionId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el alta del Festival').pipe(
      switchMap(token => this.api.post<FestivalDeLaOrganizacion>(
        `/api/v1/externo/organizaciones/${organizacionId}/festivales`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible crear el Festival',
        },
      )),
    );
  }

  guardarFestival(festivalId: string, solicitud: GuardarFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el guardado del Festival').pipe(
      switchMap(token => this.api.put<FestivalDeLaOrganizacion>(
        `/api/v1/externo/festivales/${festivalId}`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar el Festival',
        },
      )),
    );
  }

  override actualizarContactoPublico(festivalId: string, solicitud: ContactoPublicoFestivalSolicitud): Observable<FestivalDeLaOrganizacion> {
    return this.testigo('No fue posible preparar la actualización del contacto público').pipe(
      switchMap(token => this.api.put<FestivalDeLaOrganizacion>(
        `/api/v1/externo/festivales/${festivalId}/contacto-publico`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible actualizar el contacto público',
        },
      )),
    );
  }

  // ── Mercados musicales ──────────────────────────────────────────────────────────────────────

  override obtenerMercados(organizacionId: string): Observable<MercadoDeLaOrganizacion[]> {
    return this.api.get<MercadoDeLaOrganizacion[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/mercados`,
      { errorFallback: 'No fue posible consultar los mercados de la organización' },
    );
  }

  override obtenerMercado(mercadoId: string): Observable<MercadoDeLaOrganizacion> {
    return this.api.get<MercadoDeLaOrganizacion>(`/api/v1/externo/mercados/${mercadoId}`, {
      errorFallback: 'No fue posible abrir el mercado',
    });
  }

  override obtenerCatalogosDeMercado(): Observable<CatalogosDeMercadoExternos> {
    return this.api.get<CatalogosDeMercadoExternos>('/api/v1/externo/catalogos/mercado', {
      errorFallback: 'No fue posible cargar los catálogos del mercado',
    });
  }

  override obtenerFestivalesElegibles(organizacionId: string): Observable<FestivalElegibleParaMercado[]> {
    return this.api.get<FestivalElegibleParaMercado[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/festivales-elegibles`,
      { errorFallback: 'No fue posible consultar los festivales de la organización' },
    );
  }

  override crearMercado(organizacionId: string, solicitud: GuardarMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el registro del mercado').pipe(
      switchMap(token => this.api.post<MercadoDeLaOrganizacion>(
        `/api/v1/externo/organizaciones/${organizacionId}/mercados`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible registrar el mercado',
        },
      )),
    );
  }

  override guardarMercado(mercadoId: string, solicitud: GuardarMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el guardado del mercado').pipe(
      switchMap(token => this.api.put<MercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}`,
        solicitud,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar el mercado',
        },
      )),
    );
  }

  override obtenerEdicionesDeMercado(mercadoId: string): Observable<EdicionDeMercadoDeLaOrganizacion[]> {
    return this.api.get<EdicionDeMercadoDeLaOrganizacion[]>(`/api/v1/externo/mercados/${mercadoId}/ediciones`, {
      errorFallback: 'No fue posible consultar las ediciones del mercado',
    });
  }

  override crearEdicionDeMercado(mercadoId: string, solicitud: GuardarEdicionDeMercadoSolicitud): Observable<EdicionDeMercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el registro de la edición').pipe(
      switchMap(token => this.api.post<EdicionDeMercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}/ediciones`,
        solicitud,
        { headers: { 'X-CSRF-TOKEN': token.requestToken }, errorFallback: 'No fue posible registrar la edición' },
      )),
    );
  }

  override guardarEdicionDeMercado(mercadoId: string, edicionId: number, solicitud: GuardarEdicionDeMercadoSolicitud): Observable<EdicionDeMercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el guardado de la edición').pipe(
      switchMap(token => this.api.put<EdicionDeMercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}/ediciones/${edicionId}`,
        solicitud,
        { headers: { 'X-CSRF-TOKEN': token.requestToken }, errorFallback: 'No fue posible guardar la edición' },
      )),
    );
  }

  override enviarMercadoARevision(mercadoId: string): Observable<MercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el envío a revisión').pipe(
      switchMap(token => this.api.post<MercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}/enviar-a-revision`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible enviar el mercado a revisión',
        },
      )),
    );
  }

  override retirarMercado(mercadoId: string): Observable<void> {
    return this.testigo('No fue posible preparar el retiro del mercado').pipe(
      switchMap(token => this.api.delete<void>(`/api/v1/externo/mercados/${mercadoId}`, {
        headers: { 'X-CSRF-TOKEN': token.requestToken },
        errorFallback: 'No fue posible retirar el mercado',
      })),
    );
  }

  override solicitarRetiroDeMercado(mercadoId: string, justificacion: string): Observable<void> {
    return this.testigo('No fue posible preparar la solicitud de retiro').pipe(
      switchMap(token => this.api.post<void>(`/api/v1/externo/mercados/${mercadoId}/solicitudes-retiro`, { justificacion }, {
        headers: { 'X-CSRF-TOKEN': token.requestToken },
        errorFallback: 'No fue posible enviar la solicitud de retiro',
      })),
    );
  }

  override actualizarContactoDeMercado(
    mercadoId: string, solicitud: ContactoPublicoDeMercadoSolicitud): Observable<MercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar la actualización del contacto').pipe(
      switchMap(token => this.api.put<MercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}/contacto-publico`, solicitud, {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible actualizar el contacto del mercado',
        })),
    );
  }

  override cambiarVisibilidadDeEdicionDeMercado(
    mercadoId: string, edicionId: number, accion: 'publicar' | 'despublicar' | 'archivar'): Observable<EdicionDeMercadoDeLaOrganizacion> {
    return this.testigo('No fue posible preparar el cambio de la edición').pipe(
      switchMap(token => this.api.post<EdicionDeMercadoDeLaOrganizacion>(
        `/api/v1/externo/mercados/${mercadoId}/ediciones/${edicionId}/${accion}`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible cambiar la edición',
        })),
    );
  }

  override eliminarEdicionDeMercado(mercadoId: string, edicionId: number): Observable<void> {
    return this.testigo('No fue posible preparar la eliminación de la edición').pipe(
      switchMap(token => this.api.delete<void>(
        `/api/v1/externo/mercados/${mercadoId}/ediciones/${edicionId}`,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible eliminar la edición',
        })),
    );
  }

  override propuestaDeMercado(mercadoId: string): Observable<PropuestaDeCambioDeMercado> {
    return this.api.get<PropuestaDeCambioDeMercado>(
      `/api/v1/externo/mercados/${mercadoId}/propuesta`,
      { errorFallback: 'No fue posible consultar tu propuesta de cambio' },
    );
  }

  override guardarPropuestaDeMercado(
    mercadoId: string, motivo: string | null, campos: { campoId: string; valorPropuesto: string | null }[],
  ): Observable<PropuestaDeCambioDeMercado> {
    return this.testigo('No fue posible preparar la propuesta').pipe(
      switchMap(token => this.api.put<PropuestaDeCambioDeMercado>(
        `/api/v1/externo/mercados/${mercadoId}/propuesta`,
        { motivo, campos },
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible guardar la propuesta de cambio',
        })),
    );
  }

  override enviarPropuestaDeMercado(mercadoId: string): Observable<PropuestaDeCambioDeMercado> {
    return this.testigo('No fue posible preparar el envío').pipe(
      switchMap(token => this.api.post<PropuestaDeCambioDeMercado>(
        `/api/v1/externo/mercados/${mercadoId}/propuesta/enviar`,
        {},
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible enviar la propuesta de cambio',
        })),
    );
  }

  override abandonarPropuestaDeMercado(mercadoId: string): Observable<void> {
    return this.testigo('No fue posible preparar la operación').pipe(
      switchMap(token => this.api.delete<void>(
        `/api/v1/externo/mercados/${mercadoId}/propuesta`,
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible abandonar la propuesta de cambio',
        })),
    );
  }

  override cambiosPedidosDeMercado(mercadoId: string): Observable<ObservacionDeCampoDeMercado[]> {
    return this.api.get<ObservacionDeCampoDeMercado[]>(
      `/api/v1/externo/mercados/${mercadoId}/cambios-pedidos`,
      { errorFallback: 'No fue posible consultar los cambios pedidos' },
    );
  }

  override atenderCambioPedidoDeMercado(observacionId: number, atendida: boolean): Observable<ObservacionDeCampoDeMercado> {
    return this.testigo('No fue posible preparar el cambio').pipe(
      switchMap(token => this.api.post<ObservacionDeCampoDeMercado>(
        `/api/v1/externo/mercados/cambios-pedidos/${observacionId}/atender`,
        { atendida },
        {
          headers: { 'X-CSRF-TOKEN': token.requestToken },
          errorFallback: 'No fue posible marcar el cambio',
        })),
    );
  }

  override obtenerHistorialMercado(organizacionId: string, mercadoId: string): Observable<EntradaHistorialMercado[]> {
    return this.api.get<EntradaHistorialMercado[]>(
      `/api/v1/externo/organizaciones/${organizacionId}/mercados/${mercadoId}/historial`,
      { errorFallback: 'No fue posible consultar el historial del mercado' },
    );
  }
}
