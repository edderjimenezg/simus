import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
// Solo el tipo: un import de valor cerraria un ciclo, porque TextosWebService
// inyecta este servicio.
import type { MiembroDelEquipoWeb } from './textos-web.service';

/** Lo que el sitio publico lee: solo lo publicado. */
export interface PublicWebContent {
  texts: Record<string, string>;
  count: number;
}

/**
 * Nomina publicada. `members: null` significa «no hay nomina publicada, use la
 * compilada»; `members: []` significa «se publico una nomina vacia a proposito».
 * Son dos casos distintos y por eso el contrato los distingue.
 */
export interface PublicWebTeam {
  published: boolean;
  members: MiembroDelEquipoWeb[] | null;
}

/** Una imagen publicada, tal como la anuncia el manifiesto. Sin bytes. */
export interface PublicWebImage {
  /** Ruta al archivo, ya con `?v=` para que el navegador la pueda cachear un ano. */
  url: string;
  version: number;
  mime: string;
  hash: string;
  width: number;
  height: number;
  bytes: number;
  alt: string;
  use: 'fondo' | 'logotipo';
}

/**
 * El manifiesto de imagenes publicadas.
 *
 * Una clave AUSENTE del diccionario significa «no hay imagen publicada para esa
 * ranura» —retirada o nunca publicada— y el sitio usa su imagen compilada. No
 * hay tercer estado: el servidor filtra por lo publicado.
 */
export interface PublicWebImages {
  images: Record<string, PublicWebImage>;
  count: number;
}

export interface AdminGroupField {
  key: string;
  label: string;
  limit: number;
  draft: string;
  published: string | null;
  /** publicado | retirado | no_publicado. Derivado en el servidor. */
  state: string;
  version: number;
  updatedBy: string;
  updatedAt: string;
}

export interface AdminGroup {
  groupId: string;
  groupLabel: string;
  section: string;
  fields: AdminGroupField[];
}

/**
 * Los topes que el servidor aplica a la nomina, tal como los publica.
 *
 * El panel los LEE de aqui en vez de escribirlos. Cuando los llevaba escritos a
 * mano se quedo en 120 para los tres campos mientras el servidor ya aceptaba 160
 * en el cargo y 180 en el correo: un cargo institucional largo no se podia ni
 * teclear, y nada lo delataba porque las dos validaciones nunca se comparan.
 */
export interface TopesDeLaNomina {
  maxMembers: number;
  maxPhotoChars: number;
  maxSerializedChars: number;
  maxNameLength: number;
  maxRoleLength: number;
  maxEmailLength: number;
}

export interface AdminTeam {
  members: MiembroDelEquipoWeb[];
  publishedMembers: MiembroDelEquipoWeb[] | null;
  state: string;
  version: number;
  updatedBy: string;
  updatedAt: string;
  limits?: TopesDeLaNomina;
}

/** El informe que devuelven la simulacion y la aplicacion del import (2C). */
export interface ImportReport {
  aplicado: boolean;
  planHash: string;
  textos: {
    porAplicar: number;
    aplicados: number;
    sinCambio: number;
    rechazados: { key: string; reason: string }[];
    rechazadosTotal: number;
    enConflicto: { key: string; reason: string }[];
    enConflictoTotal: number;
  };
  equipo: {
    porAplicar: boolean;
    personas: number | null;
    sinCambio: boolean;
    motivoRechazo: string | null;
    personasRechazadas: { index: number; id: string; reason: string }[];
  };
  noImportado: {
    clavesDesconocidas: string[];
    clavesDesconocidasTotal: number;
    publicadoIgualAlBorrador: number;
    publicadoDistinto: string[];
    publicadoDistintoTotal: number;
    revisionesDescartadas: number;
    entradasDeMedios: number;
    nominaPublicadaPresente: boolean;
  };
  cambios: number;
  aviso: string;
}

/**
 * Una entrada del historial de una clave (2E).
 *
 * `value` es el texto que quedo tras la accion, y es nulo cuando la accion no
 * reescribio el texto —retirar quita del sitio, no toca el borrador—. El valor
 * anterior no viaja: es el `value` de la entrada siguiente de la lista.
 */
export interface EntradaDeHistorialDeContenidoWeb {
  action: 'guardado' | 'publicado' | 'retirado' | 'republicado' | 'importado';
  value: string | null;
  user: string;
  at: string;
}

/** Una mitad —borrador o publicado— de una ranura de imagen, vista desde el panel. */
export interface AdminImageHalf {
  mime: string;
  bytes: number;
  width: number;
  height: number;
  hash: string;
}

/** Una ranura de imagen tal como la pinta el panel. NO trae bytes. */
export interface AdminImage {
  key: string;
  label: string;
  use: 'fondo' | 'logotipo';
  /**
   * Falso para la marca institucional ajena. El panel no ofrece subir, y la base
   * lo impide ademas por CHECK: no depende de que esta pantalla se acuerde.
   */
  editable: boolean;
  alt: string;
  suggestedWidth: number | null;
  suggestedHeight: number | null;
  draft: AdminImageHalf | null;
  published: AdminImageHalf | null;
  /** publicado | retirado | no_publicado. Derivado en el servidor. */
  state: string;
  version: number;
  updatedBy: string;
  updatedAt: string;
}

/**
 * Los topes que el servidor aplica, tal como los publica.
 *
 * El panel los LEE de aqui y no los lleva escritos. Es la correccion que ya hubo
 * que hacer en la nomina: cuando el panel se quedo en un limite y el servidor
 * aceptaba otro, la editora veia «cabe» y recibia un 400.
 */
export interface TopesDeLasImagenes {
  maxBytes: number;
  maxThumbnailBytes: number;
  maxAltLength: number;
  maxDimension: number;
  allowedTypes: string[];
}

export interface AdminImageGroup {
  groupId: string;
  groupLabel: string;
  section: string;
  images: AdminImage[];
  limits: TopesDeLasImagenes;
}

export interface ResumenDeGrupoDelSitio {
  id: string;
  label: string;
  section: string;
  total: number;
  published: number;
  pending: number;
  retired: number;
  updatedAt: string;
}

export interface ResumenDeGestionDelSitio {
  generatedAt: string;
  pendingTotal: number;
  texts: {
    total: number;
    published: number;
    pending: number;
    retired: number;
    groups: ResumenDeGrupoDelSitio[];
  };
  images: {
    total: number;
    published: number;
    pending: number;
    retired: number;
    groups: ResumenDeGrupoDelSitio[];
  };
  team: {
    members: number;
    published: boolean;
    pending: boolean;
    updatedAt: string | null;
  };
}

/**
 * Una entrada del historial de una imagen.
 *
 * SIN EL ARCHIVO, y no hay forma de recuperarlo: desde V20260829_03 el historial guarda
 * quien, cuando, que accion y las senas —tipo, peso, medidas, huella—, pero no los bytes.
 * Reemplazar una imagen borra la anterior.
 */
export interface AdminImageHistoryEntry {
  id: number;
  action: string;
  mime: string | null;
  bytes: number | null;
  width: number | null;
  height: number | null;
  hash: string | null;
  user: string;
  at: string;
}

/** Lo que devuelve cualquier escritura sobre una ranura. */
export interface AdminImageState {
  key: string;
  state: string;
  version: number;
  changed: boolean;
  hash: string | null;
  mime: string | null;
  width: number | null;
  height: number | null;
  bytes: number | null;
}

/** Resultado uniforme: el llamador nunca ve una excepcion de HTTP. */
export interface ApiOutcome<T> {
  ok: boolean;
  data?: T;
  /** Mensaje ya redactado para mostrar al editor. */
  error?: string;
  /** true cuando otra persona modifico el mismo contenido (409). */
  conflict?: boolean;
}

/**
 * Las rutas del CMS que sirve la API.
 *
 * Este servicio es deliberadamente delgado: traduce HTTP a resultados y nada
 * mas. Quien decide que hacer con un conflicto o con un fallo de red es el
 * panel, que es quien tiene delante a la persona a la que hay que explicarselo.
 */
@Injectable({ providedIn: 'root' })
export class ContenidoWebApiService {
  private readonly apiClient = inject(ApiClientService);

  private static readonly PUBLIC_TEXTS = '/api/v1/contenido-web';
  private static readonly PUBLIC_TEAM = '/api/v1/equipo-web';
  private static readonly PUBLIC_IMAGES = '/api/v1/imagenes-web';
  private static readonly ADMIN_CONTENT = '/api/v1/admin/contenido-web';
  private static readonly ADMIN_TEAM = '/api/v1/admin/equipo-web';
  private static readonly ADMIN_MEDIA = '/api/v1/admin/imagenes-web';

  // --- Lectura publica ---------------------------------------------------------

  async getPublicTexts(timeoutMs: number): Promise<PublicWebContent | null> {
    return this.attempt(() => firstValueFrom(
      this.apiClient.get<PublicWebContent>(ContenidoWebApiService.PUBLIC_TEXTS, { timeoutMs }),
    ));
  }

  async getPublicTeam(timeoutMs: number): Promise<PublicWebTeam | null> {
    return this.attempt(() => firstValueFrom(
      this.apiClient.get<PublicWebTeam>(ContenidoWebApiService.PUBLIC_TEAM, { timeoutMs }),
    ));
  }

  /**
   * El manifiesto de imagenes publicadas. NO trae bytes: son dieciseis entradas
   * de metadatos con la URL de cada archivo, unos 2 KB.
   *
   * Una clave que no venga en el diccionario es una clave sin imagen publicada
   * —retirada o nunca publicada— y el sitio usa su imagen compilada.
   */
  async getPublicImages(timeoutMs: number): Promise<PublicWebImages | null> {
    return this.attempt(() => firstValueFrom(
      this.apiClient.get<PublicWebImages>(ContenidoWebApiService.PUBLIC_IMAGES, { timeoutMs }),
    ));
  }

  // --- Consola de administracion -----------------------------------------------

  async getGroup(groupId: string): Promise<ApiOutcome<AdminGroup>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<AdminGroup>(`${ContenidoWebApiService.ADMIN_CONTENT}/groups/${encodeURIComponent(groupId)}`),
    ));
  }

  async getSiteManagementSummary(): Promise<ApiOutcome<ResumenDeGestionDelSitio>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<ResumenDeGestionDelSitio>(`${ContenidoWebApiService.ADMIN_CONTENT}/summary`),
    ));
  }

  async saveGroup(
    groupId: string,
    fields: { key: string; content: string }[],
    publish: boolean,
  ): Promise<ApiOutcome<{ groupId: string; changed: number; published: boolean }>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<{ groupId: string; changed: number; published: boolean }>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/groups/${encodeURIComponent(groupId)}`,
        { fields, publish },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  async getTeam(): Promise<ApiOutcome<AdminTeam>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<AdminTeam>(ContenidoWebApiService.ADMIN_TEAM),
    ));
  }

  async saveTeam(
    members: MiembroDelEquipoWeb[],
    publish: boolean,
    version: number,
  ): Promise<ApiOutcome<{ changed: boolean; published: boolean; count: number; version: number }>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<{ changed: boolean; published: boolean; count: number; version: number }>(
        ContenidoWebApiService.ADMIN_TEAM,
        { members, publish, version },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  async retire(key: string): Promise<ApiOutcome<{ key: string; state: string; version: number }>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<{ key: string; state: string; version: number }>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/${encodeURIComponent(key)}/retire`, { reason: null },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  async republish(key: string): Promise<ApiOutcome<{ key: string; state: string; version: number }>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<{ key: string; state: string; version: number }>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/${encodeURIComponent(key)}/republish`, { reason: null },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  // --- Imagenes administrables (29 ago 2026) -----------------------------------

  async getImageGroup(groupId: string): Promise<ApiOutcome<AdminImageGroup>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<AdminImageGroup>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/groups/${encodeURIComponent(groupId)}`),
    ));
  }

  /**
   * Sube una imagen a una ranura.
   *
   * `version` es OBLIGATORIA y no es un adorno: es la que el servidor compara
   * para saber si alguien mas guardo esa ranura mientras el editor la miraba. Sin
   * ella no hay comprobacion de concurrencia y dos editores se pisan megabytes en
   * silencio.
   *
   * `publish` exige ademas el rol de webmaster, y el servidor lo comprueba: que
   * este boton no se pinte no es la guarda, es la cortesia.
   */
  async uploadImage(
    key: string,
    archivo: File,
    version: number,
    opciones: { publish?: boolean; miniatura?: Blob; alt?: string } = {},
  ): Promise<ApiOutcome<AdminImageState>> {
    const cuerpo = new FormData();
    cuerpo.append('file', archivo, archivo.name);
    cuerpo.append('version', String(version));
    cuerpo.append('publish', opciones.publish ? 'true' : 'false');
    if (opciones.miniatura) {
      cuerpo.append('thumbnail', opciones.miniatura, 'miniatura.webp');
    }
    if (opciones.alt !== undefined) {
      cuerpo.append('alt', opciones.alt);
    }

    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.postForm<AdminImageState>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/${encodeURIComponent(key)}`, cuerpo,
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  /** «Desplegar el borrador»: saca al sitio lo que ya estaba guardado. */
  async publishImage(key: string, version: number): Promise<ApiOutcome<AdminImageState>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<AdminImageState>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/${encodeURIComponent(key)}/publish`, { version },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  /** Quita la imagen del sitio. El sitio vuelve a la compilada y el borrador se conserva. */
  async retireImage(key: string): Promise<ApiOutcome<AdminImageState>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<AdminImageState>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/${encodeURIComponent(key)}/retire`, { reason: null },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  async republishImage(key: string): Promise<ApiOutcome<AdminImageState>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<AdminImageState>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/${encodeURIComponent(key)}/republish`, { reason: null },
        { headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  async getImageHistory(key: string, limit = 20): Promise<ApiOutcome<{ key: string; entries: AdminImageHistoryEntry[] }>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<{ key: string; entries: AdminImageHistoryEntry[] }>(
        `${ContenidoWebApiService.ADMIN_MEDIA}/${encodeURIComponent(key)}/history`,
        { params: { limit } }),
    ));
  }

  /**
   * La URL de la previsualizacion de una mitad. Se construye aqui y no en la
   * plantilla para que la ruta viva en un solo sitio.
   *
   * `v` no lo lee el servidor: rompe la cache del navegador cuando la version
   * sube. Sin el, publicar una imagen nueva dejaria la vieja en la cuadricula.
   */
  imagePreviewUrl(key: string, estado: 'borrador' | 'publicado', version: number, miniatura = true): string {
    return `/api/v1/admin/imagenes-web/${encodeURIComponent(key)}/preview/${estado}`
      + `?miniatura=${miniatura}&v=${version}`;
  }

  /**
   * Historial de una clave, del cambio mas reciente al mas antiguo (2E).
   * El servidor acota cuantas entradas devuelve.
   */
  async getHistory(key: string, limit = 20): Promise<ApiOutcome<{ key: string; entries: EntradaDeHistorialDeContenidoWeb[] }>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<{ key: string; entries: EntradaDeHistorialDeContenidoWeb[] }>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/${encodeURIComponent(key)}/history`,
        { params: { limit } },
      ),
    ));
  }

  /**
   * Historial de todas las claves de un grupo, en una sola peticion. El panel
   * abre grupos enteros; pedirlo clave por clave serian 27 peticiones para
   * pintar una pantalla.
   */
  async getGroupHistory(
    groupId: string,
    perKey = 10,
  ): Promise<ApiOutcome<{ groupId: string; byKey: Record<string, EntradaDeHistorialDeContenidoWeb[]> }>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<{ groupId: string; byKey: Record<string, EntradaDeHistorialDeContenidoWeb[]> }>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/groups/${encodeURIComponent(groupId)}/history`,
        { params: { perKey } },
      ),
    ));
  }

  // --- Importar el respaldo del navegador (2C) ---------------------------------

  /** Instantanea de la base en el formato del respaldo. Es la vuelta atras. */
  async getServerSnapshot(): Promise<ApiOutcome<unknown>> {
    return this.call(() => firstValueFrom(
      this.apiClient.get<unknown>(`${ContenidoWebApiService.ADMIN_CONTENT}/backup`, { timeoutMs: 60000 }),
    ));
  }

  /** Ensayo en seco: devuelve el plan y su huella sin escribir una sola fila. */
  async previewImport(backup: unknown, overwriteEdited: boolean): Promise<ApiOutcome<ImportReport>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<ImportReport>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/import/preview?overwriteEdited=${overwriteEdited}`,
        backup,
        { timeoutMs: 60000, headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  /** Aplica el plan citando la huella que devolvio la simulacion. */
  async applyImport(backup: unknown, planHash: string, overwriteEdited: boolean): Promise<ApiOutcome<ImportReport>> {
    return this.call(() => this.withCsrf(token => firstValueFrom(
      this.apiClient.post<ImportReport>(
        `${ContenidoWebApiService.ADMIN_CONTENT}/import`,
        { backup, planHash, overwriteEdited },
        { timeoutMs: 60000, headers: ContenidoWebApiService.csrfHeader(token) },
      ),
    )));
  }

  // --- Antiforgery -------------------------------------------------------------

  /**
   * Token antiforgery vigente. Se pide una vez y se reutiliza; si el servidor lo
   * rechaza —la sesion se renovo, el token caduco— se vuelve a pedir y se
   * reintenta una sola vez.
   */
  private csrfToken: string | null = null;

  private async ensureCsrfToken(force = false): Promise<string | null> {
    if (this.csrfToken && !force) {
      return this.csrfToken;
    }

    try {
      const response = await firstValueFrom(
        this.apiClient.get<{ token: string }>(`${ContenidoWebApiService.ADMIN_CONTENT}/csrf`),
      );
      this.csrfToken = response?.token ?? null;
    } catch {
      this.csrfToken = null;
    }

    return this.csrfToken;
  }

  /**
   * Envuelve una escritura con el token. Un unico reintento tras refrescarlo:
   * mas seria un bucle contra un servidor que rechaza por otra razon.
   */
  private async withCsrf<T>(operation: (token: string) => Promise<T>): Promise<T> {
    const token = await this.ensureCsrfToken();
    try {
      return await operation(token ?? '');
    } catch (error: unknown) {
      if (ContenidoWebApiService.statusOf(error) !== 400) {
        throw error;
      }
      const fresh = await this.ensureCsrfToken(true);
      return operation(fresh ?? '');
    }
  }

  private static csrfHeader(token: string): Record<string, string> {
    return { 'X-CSRF-TOKEN': token };
  }

  // --- Plomeria ----------------------------------------------------------------

  /**
   * Lectura publica: un fallo devuelve null y el sitio sigue con su texto
   * compilado. Deliberadamente no propaga el error: que la API no responda no
   * puede dejar la pagina en blanco.
   */
  private async attempt<T>(operation: () => Promise<T>): Promise<T | null> {
    try {
      return await operation();
    } catch {
      return null;
    }
  }

  /**
   * Llamada de administracion: el editor sí necesita saber qué pasó, así que el
   * error se traduce a un mensaje en vez de tragarse.
   */
  private async call<T>(operation: () => Promise<T>): Promise<ApiOutcome<T>> {
    try {
      return { ok: true, data: await operation() };
    } catch (error: unknown) {
      const status = ContenidoWebApiService.statusOf(error);

      if (status === 409) {
        return {
          ok: false,
          conflict: true,
          error: 'Otra persona modificó este contenido mientras usted lo editaba. Vuelva a cargarlo antes de guardar.',
        };
      }
      if (status === 401) {
        return { ok: false, error: 'Su sesión expiró. Vuelva a iniciar sesión para guardar.' };
      }
      if (status === 403) {
        return { ok: false, error: 'Su rol no permite esta acción.' };
      }
      if (status === 400) {
        return { ok: false, error: ContenidoWebApiService.validationMessage(error) };
      }
      if (status === 0) {
        return {
          ok: false,
          error: 'No fue posible contactar al servidor. Verifique su conexión; el contenido no se guardó.',
        };
      }

      if (status >= 500) {
        // Se distingue del rechazo por contenido a proposito. El fallo tipico
        // aqui es de despliegue —la cookie antiforgery exige HTTPS y el servidor
        // esta sirviendo por HTTP—, y decir «rechazo el guardado» mandaria al
        // editor a revisar su texto, que es donde no esta el problema.
        return {
          ok: false,
          error: `El servidor falló al procesar la petición (error ${status}). `
            + 'No es un problema de su texto: avise al equipo técnico. El contenido no se guardó.',
        };
      }

      return { ok: false, error: 'El servidor rechazó el guardado. El contenido no se guardó.' };
    }
  }

  private static statusOf(error: unknown): number {
    const candidate = error as { status?: number; originalError?: { status?: number } } | null;
    return candidate?.status ?? candidate?.originalError?.status ?? -1;
  }

  /** Saca del ProblemDetails el primer motivo concreto, que es el util. */
  private static validationMessage(error: unknown): string {
    const problem = (error as { error?: { errors?: Record<string, string[]> } } | null)?.error;
    const first = problem?.errors ? Object.entries(problem.errors)[0] : undefined;
    if (first) {
      return `${first[0]}: ${first[1]?.[0] ?? 'valor no admitido'}`;
    }
    return 'El servidor rechazó el contenido enviado.';
  }
}
