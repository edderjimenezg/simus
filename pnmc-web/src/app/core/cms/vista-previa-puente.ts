import { Provider, signal } from '@angular/core';
import { TextosWebService } from '../services/textos-web.service';

/**
 * EL PUENTE QUE HACE «EN VIVO» LA PREVISUALIZACION DEL PANEL.
 *
 * El problema que resuelve, con su dato: `TextosWebService.getWebText` devuelve
 * `publicado ?? valor de fabrica` (textos-web.service.ts). El sitio publico
 * no conoce los borradores, y no debe conocerlos. Pero el panel carga la pagina
 * real dentro de un marco para previsualizarla, y ahi si hace falta ver lo que
 * la editora acaba de teclear y todavia no ha publicado.
 *
 * DESDE EL 30 DE AGOSTO DE 2026 TAMBIEN LAS IMAGENES. `getWebImage` tiene el
 * mismo comportamiento y por el mismo motivo: `serverImages` solo trae lo
 * publicado (textos-web.service.ts), asi que una portada recien subida no se
 * veia en la previsualizacion hasta despues de publicarla, que es justo al reves
 * de para lo que sirve previsualizar.
 *
 * POR QUE NO SE TOCA `textos-web.service.ts`. Meter una rama de borradores dentro
 * del servicio la mete en el paquete de produccion, donde no tiene nada que
 * hacer: un fallo en su condicion ensena texto sin publicar a un visitante. Aqui
 * la rama vive en una subclase que solo existe si alguien la registra, y quien
 * la registra es `main.ts` bajo `!environment.production`. En el paquete de
 * produccion este archivo no entra: `angular.json` sustituye el entorno y el
 * `import()` que lo trae nunca se pide.
 *
 * LAS CUATRO GUARDAS, y ninguna sobra:
 *  1. `window.self !== window.top` — solo dentro de un marco. Una pestana suelta
 *     del sitio nunca acepta borradores, aunque le lleguen.
 *  2. `?pnmcVista=` en la direccion — la pagina tiene que haberse abierto A
 *     PROPOSITO como previsualizacion.
 *  3. `origin` igual al propio — descarta cualquier mensaje de otro sitio.
 *  4. `source === window.parent` y la ficha correcta — descarta a un tercer
 *     marco de la misma pagina, y descarta al panel de OTRA pestana.
 */

/** Nombre del parametro que enciende la vista previa. */
export const PARAMETRO_DE_VISTA = 'pnmcVista';

export interface MensajeDeBorrador {
  tipo: 'pnmc:borrador';
  ficha: string;
  textos: Record<string, string>;
  /**
   * URLs de imagen por clave de ranura. Opcional a proposito: el panel enviaba
   * solo textos hasta, y un mensaje sin este campo tiene
   * que seguir moviendo los textos.
   */
  imagenes?: Record<string, string>;
}

/**
 * Decide si un mensaje puede mover los textos de esta pagina.
 *
 * Se exporta aparte de la clase, y sin tocar el DOM, para poder ponerla en rojo
 * guarda por guarda desde una prueba.
 */
export function mensajeAceptable(
  evento: { origin: string; source: unknown; data: unknown },
  contexto: { origenPropio: string; ventanaPadre: unknown; ficha: string | null },
): boolean {
  if (!contexto.ficha) { return false; }
  if (evento.origin !== contexto.origenPropio) { return false; }
  if (evento.source !== contexto.ventanaPadre) { return false; }
  const dato = evento.data as Partial<MensajeDeBorrador> | null;
  if (!dato || dato.tipo !== 'pnmc:borrador') { return false; }
  if (dato.ficha !== contexto.ficha) { return false; }
  if (!dato.textos || typeof dato.textos !== 'object') { return false; }
  return true;
}

/** La ficha que trae la direccion, o null si esta pagina no es una vista previa. */
export function fichaDeLaDireccion(busqueda: string): string | null {
  const ficha = new URLSearchParams(busqueda).get(PARAMETRO_DE_VISTA);
  return ficha && ficha.length > 0 ? ficha : null;
}

export class TextosWebConVistaPrevia extends TextosWebService {
  private readonly borrador = signal<Record<string, string>>({});
  private readonly imagenes = signal<Record<string, string>>({});
  private readonly ficha = fichaDeLaDireccion(
    typeof location === 'undefined' ? '' : location.search,
  );

  constructor() {
    super();
    if (typeof window === 'undefined') { return; }
    // Guarda 1: si esto no es un marco, el puente ni se monta.
    if (window.self === window.top) { return; }
    if (!this.ficha) { return; }

    window.addEventListener('message', (evento: MessageEvent) => {
      const aceptable = mensajeAceptable(evento, {
        origenPropio: window.location.origin,
        ventanaPadre: window.parent,
        ficha: this.ficha,
      });
      if (!aceptable) { return; }
      this.aplicar(evento.data as MensajeDeBorrador);
    });
  }

  /**
   * Aplica un mensaje YA ACEPTADO por `mensajeAceptable`.
   *
   * Es publico para que las pruebas puedan empujar un mensaje sin montar un marco de
   * verdad: las guardas 1 y 2 dependen de `window.top` y de `location.search`, que en
   * Karma valen lo que valen y no lo que la prueba quiera. La validacion no se salta —
   * vive en `mensajeAceptable`, que se prueba guarda por guarda aparte—; lo que este
   * metodo aisla es el EFECTO de un mensaje valido.
   *
   * SE FUSIONA, no se sustituye: el panel envia solo las claves del bloque abierto, y
   * cambiar de bloque no puede borrar lo que se escribio en el anterior.
   */
  aplicar(datos: MensajeDeBorrador): void {
    this.borrador.update(previo => ({ ...previo, ...datos.textos }));
    if (datos.imagenes) {
      this.imagenes.update(previo => ({ ...previo, ...datos.imagenes }));
    }
  }

  /**
   * El borrador manda sobre lo publicado, y solo sobre las claves que llegaron.
   *
   * `!== undefined` y no un `||`: un borrador que vacia un campo tiene que
   * ensenarse vacio. Con `||`, borrar un texto en el panel lo devolveria al
   * valor de fabrica en la previsualizacion y el defecto PNMC-040 volveria por
   * la puerta de atras.
   */
  override getWebText(key: string): string {
    const delBorrador = this.borrador()[key];
    if (delBorrador !== undefined) { return delBorrador; }
    return super.getWebText(key);
  }

  /**
   * La imagen sin publicar manda sobre la publicada, y solo en las claves que llegaron.
   *
   * AQUI SI SE DESCARTA LA CADENA VACIA, al reves que en `getWebText`, y no es una
   * incoherencia: un texto vacio es una decision editorial legitima —dejar un rotulo en
   * blanco—, pero un `<img src="">` no deja un hueco, hace que el navegador vuelva a pedir
   * la URL de la pagina. `textos-web.service.ts` protege esa propiedad para el visitante
   * y la previsualizacion no la puede romper por la puerta de atras.
   */
  override getWebImage(key: string): string {
    const sinPublicar = this.imagenes()[key];
    if (sinPublicar) { return sinPublicar; }
    return super.getWebImage(key);
  }
}

/**
 * El proveedor que sustituye el servicio. Solo lo pide `main.ts`, y solo fuera
 * de produccion.
 */
export const PROVEEDOR_DE_VISTA_PREVIA: Provider = {
  provide: TextosWebService,
  useClass: TextosWebConVistaPrevia,
};
