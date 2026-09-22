import { Injectable, inject, signal } from '@angular/core';
import {
  DEFAULT_TEXTS,
  TEAM_DEFAULTS,
  WEB_TEXT_KEYS_LIST,
} from '../cms/registro-de-textos-web';
import { DEFAULT_IMAGE_ALTS, DEFAULT_IMAGE_URLS } from '../cms/registro-de-imagenes-web';
import { ContenidoWebApiService } from './contenido-web-api.service';
import type { PublicWebImage } from './contenido-web-api.service';

// Reexportado para que los consumidores del CMS tengan una sola puerta de entrada.
export {
  ABOUT_PNMC_TEXT_GROUPS,
  DEFAULT_TEXTS,
  WEB_TEXT_GROUPS,
  WEB_TEXT_KEYS_LIST,
  WEB_TEXT_KEY_INDEX,
  WEB_TEXT_SECTIONS,
} from '../cms/registro-de-textos-web';
export type {
  DefinicionDeCampoDeTexto,
  DefinicionDeGrupoDeTexto,
  DescriptorDeClaveDeTexto,
  DefinicionDeSeccionDeTexto,
} from '../cms/registro-de-textos-web';

export interface MiembroDelEquipoWeb {
  id: string;
  group: 'coordination' | 'leadership';
  role: string;
  name: string;
  email: string;
  photo: string;
}

export interface RevisionDeTextoWeb {
  content: string;
  status: string;
  updatedAt: string;
  updatedBy: string;
}

export interface RegistroDeTextoWeb {
  content: string;
  /** Ultimo valor publicado; un borrador posterior no lo mueve. */
  publishedContent?: string;
  status: string;
  updatedAt: string;
  updatedBy: string;
  history?: RevisionDeTextoWeb[];
}

export interface RegistroDeImagenWeb {
  content: string;
  publishedContent?: string;
  status: string;
  updatedAt: string;
  updatedBy: string;
}

export interface RegistroDeEquipoWeb {
  content: MiembroDelEquipoWeb[];
  publishedContent?: MiembroDelEquipoWeb[];
  status: string;
  updatedAt: string;
  updatedBy: string;
}

/**
 * Respaldo completo del CMS. Mientras el contenido viva en localStorage este
 * archivo es la unica copia que sobrevive a un borrado de datos del navegador,
 * a un cambio de equipo o a un cambio de perfil de usuario.
 */
export interface RespaldoDeContenidoWeb {
  schemaVersion: number;
  app: 'pnmc-cms';
  exportedAt: string;
  exportedBy: string;
  stores: Record<string, unknown>;
}

/** Version del formato de respaldo; subir solo ante cambios incompatibles. */
export const WEB_CONTENT_BACKUP_VERSION = 1;

/**
 * Fuente de contenido editable del sitio publico.
 *
 * Desde la Fase 3 el contenido lo sirve la API y `localStorage` dejo de
 * alimentar el sitio: se lee una sola vez, y solo para poder rescatar con
 * `exportContent` lo que los editores alcanzaron a escribir antes del corte.
 *
 * Los datos viven en senales, de modo que la carga asincrona del arranque
 * refresca las plantillas sin recargar la pagina. Esa fue la razon de convertir
 * el almacen a senales en la Fase 1: cambiar la fuente de datos sin tocar
 * ninguna de las llamadas a `getWebText`, que siguen siendo sincronas.
 */
@Injectable({
  providedIn: 'root'
})
export class TextosWebService {
  private readonly textStorageKey = 'pnmc_web_texts';
  private readonly mediaStorageKey = 'pnmc_web_media';
  private readonly teamStorageKey = 'pnmc_web_team_members';
  private readonly texts = signal<Record<string, RegistroDeTextoWeb>>(
    this.readStore<Record<string, RegistroDeTextoWeb>>(this.textStorageKey, {}),
  );
  private readonly media = signal<Record<string, RegistroDeImagenWeb>>(
    this.readStore<Record<string, RegistroDeImagenWeb>>(this.mediaStorageKey, {}),
  );
  private readonly team = signal<RegistroDeEquipoWeb | null>(
    this.readStore<RegistroDeEquipoWeb | null>(this.teamStorageKey, null),
  );

  private readonly api = inject(ContenidoWebApiService);

  /**
   * Lo publicado que sirve la API. Desde la Fase 3 es la <b>unica</b> fuente del
   * sitio publico: `localStorage` dejo de alimentarlo.
   *
   * `null` significa «todavia no se cargo». Que una clave falte aqui significa
   * que nadie la publico, y entonces el sitio sirve su texto compilado: la
   * pagina nunca queda en blanco porque la API no responda.
   */
  private readonly serverTexts = signal<Record<string, string> | null>(null);

  /**
   * Nomina publicada. Tres estados, no dos: `undefined` es «sin cargar», `null`
   * es «cargado y no hay nomina publicada, use la compilada», y un arreglo vacio
   * es «se publico una nomina vacia a proposito». El contrato del servidor
   * distingue los dos ultimos y aqui no se pueden colapsar.
   */
  private readonly serverTeam = signal<MiembroDelEquipoWeb[] | null | undefined>(undefined);

  /**
   * Manifiesto de imagenes publicadas, por clave. `null` es «todavia no se
   * cargo». Que una clave falte significa que nadie publico esa ranura —o que
   * alguien la retiro— y entonces el sitio usa la imagen compilada del registro.
   *
   * DOS ESTADOS Y NO TRES, a diferencia de la nomina: aqui no existe «se publico
   * una imagen vacia». Una ranura tiene archivo o no lo tiene.
   */
  private readonly serverImages = signal<Record<string, PublicWebImage> | null>(null);

  /** Si la ultima carga desde la API tuvo exito. Lo usa el panel para avisar. */
  readonly serverReachable = signal<boolean | null>(null);

  /**
   * Carga lo publicado desde la API. La llama el arranque de la aplicacion.
   *
   * No lanza nunca: si la API no responde, el sitio se queda con sus textos
   * compilados, que es exactamente lo que veia antes de que existiera la API.
   */
  async loadPublishedContent(timeoutMs = 5000): Promise<boolean> {
    const [texts, team, images] = await Promise.all([
      this.api.getPublicTexts(timeoutMs),
      this.api.getPublicTeam(timeoutMs),
      this.api.getPublicImages(timeoutMs),
    ]);

    if (texts) {
      this.serverTexts.set(texts.texts ?? {});
    }
    if (team) {
      this.serverTeam.set(team.published ? (team.members ?? []) : null);
    }
    if (images) {
      this.serverImages.set(images.images ?? {});
    }

    const ok = texts !== null;
    this.serverReachable.set(ok);
    return ok;
  }

  // --- Acceso a almacenamiento -------------------------------------------------

  /** Lee y parsea un almacen; ante fallo o SSR devuelve el valor de reserva. */
  private readStore<T>(storageKey: string, fallback: T): T {
    try {
      if (typeof window !== 'undefined') {
        const saved = localStorage.getItem(storageKey);
        if (saved) {
          return JSON.parse(saved) as T;
        }
      }
    } catch {
      // Un almacen corrupto o inaccesible se trata como vacio.
    }
    return fallback;
  }

  // --- Textos ------------------------------------------------------------------

  /**
   * El texto que ve el visitante.
   *
   * Desde la Fase 3 la fuente es la API y solo la API. `localStorage` ya no
   * entra aqui: mantener las dos en paralelo produciria dos verdades que se
   * contradicen, y la del navegador nunca fue visible para nadie mas que para
   * el editor que la escribio.
   */
  getWebText(key: string): string {
    // `??` y no `||`: con `||` una clave publicada en blanco caia al valor
    // compilado y borrar un texto no lo borraba (PNMC-040).
    //
    // Los dos casos SI se distinguen, y por eso el cambio es seguro: el endpoint
    // publico filtra `Publicado != null` (ContenidoWebEndpoints.cs), de modo que
    // una clave sin publicar no viaja en el diccionario y aqui llega `undefined`.
    // Una cadena vacia solo puede venir de una publicacion deliberada, y se
    // respeta igual que se respeta una nomina publicada vacia.
    const published = this.serverTexts()?.[key];
    return published ?? DEFAULT_TEXTS[key] ?? '';
  }

  // --- Imagenes ----------------------------------------------------------------

  /**
   * La imagen que ve el visitante en una ranura del sitio.
   *
   * NUNCA DEVUELVE CADENA VACIA, y esa es la propiedad que hay que proteger. Un
   * `<img src="">` no deja un hueco: hace que el navegador vuelva a pedir la
   * URL de la pagina, y el resultado es una peticion extra por cada imagen del
   * sitio. Por eso los tres caminos caen en la URL compilada:
   *
   *  - manifiesto sin cargar (`null`), que es el estado durante el arranque;
   *  - clave ausente del manifiesto, que es «nunca publicada» o «retirada»;
   *  - clave desconocida, que solo puede ser un error de escritura.
   *
   * RETIRAR RESTAURA LA IMAGEN DE FABRICA. Es lo que se quiere de un boton
   * «retirar» sobre una portada, y se diferencia a proposito de los textos,
   * donde retirar deja la clave sin valor publicado y el sitio cae al texto
   * compilado por el mismo mecanismo.
   */
  getWebImage(key: string): string {
    const publicada = this.serverImages()?.[key];
    return publicada?.url ?? DEFAULT_IMAGE_URLS[key] ?? '';
  }

  /**
   * El texto alternativo de una ranura. Sale del CMS si la imagen esta
   * publicada, y del registro si no: una imagen sin `alt` es un fallo de
   * accesibilidad, y el estado de publicacion no debe decidirlo.
   */
  getWebImageAlt(key: string): string {
    const publicada = this.serverImages()?.[key];
    return publicada?.alt || DEFAULT_IMAGE_ALTS[key] || '';
  }

  // --- Equipo ------------------------------------------------------------------

  /**
   * La nomina que ve el visitante. Misma regla que los textos: la fuente es la
   * API. Sin nomina publicada se sirve la compilada, para que la pagina «Sobre
   * el PNMC» nunca aparezca sin equipo.
   */
  getWebTeamMembers(): MiembroDelEquipoWeb[] {
    const fromServer = this.serverTeam();

    // `undefined` es «sin cargar» y `null` es «no hay nomina publicada»: los dos
    // caen en la compilada. Un arreglo vacio, en cambio, se respeta: alguien
    // publico una nomina sin nadie y eso es una decision, no una ausencia.
    if (fromServer === undefined || fromServer === null) {
      return this.compiledTeam();
    }

    return fromServer.map(member => ({ ...member }));
  }

  /** La nomina de fabrica, derivada del registro. */
  private compiledTeam(): MiembroDelEquipoWeb[] {
    return TEAM_DEFAULTS
      .map((member, legacyIndex) => ({ member, legacyIndex }))
      .filter(({ member }) => member[1].trim().length > 0)
      .map(({ member, legacyIndex }, index) => ({
        id: `team-${legacyIndex + 1}`,
        group: index < 2 ? 'coordination' : 'leadership',
        role: member[0],
        name: member[1],
        email: member[2],
        photo: '',
      } as MiembroDelEquipoWeb));
  }

  // --- Rescate de lo que quedo en el navegador ---------------------------------

  /**
   * Serializa lo que este navegador tenga en `localStorage` al formato de
   * respaldo, para poder subirlo a la base con la importacion del panel.
   *
   * Desde la Fase 3 esta es la <b>unica</b> razon por la que se sigue leyendo
   * `localStorage`: es contenido historico que hay que rescatar una vez. El
   * panel ya no escribe ahi, asi que este archivo no crece.
   *
   * Se emite el contenido crudo, borradores incluidos, porque el rescate importa
   * precisamente lo que quedo a medio escribir.
   */
  exportContent(author = 'Webmaster'): RespaldoDeContenidoWeb {
    return {
      schemaVersion: WEB_CONTENT_BACKUP_VERSION,
      app: 'pnmc-cms',
      exportedAt: new Date().toISOString(),
      exportedBy: author,
      stores: {
        [this.textStorageKey]: this.texts(),
        [this.mediaStorageKey]: this.media(),
        // Sin registro propio va `null`, y la importacion lo reporta como
        // «el respaldo no trae bloque de equipo». Rellenarlo con la nomina de
        // fabrica seria fabricar un dato que este navegador nunca tuvo.
        [this.teamStorageKey]: this.team(),
      },
    };
  }

  /** Cuanto contenido historico queda en este navegador, para avisar al editor. */
  pendingLocalContent(): { textKeys: number; teamMembers: number } {
    return {
      textKeys: Object.keys(this.texts()).length,
      teamMembers: this.team()?.content?.length ?? 0,
    };
  }

  getWebTextsKeysList() {
    return WEB_TEXT_KEYS_LIST;
  }
}
