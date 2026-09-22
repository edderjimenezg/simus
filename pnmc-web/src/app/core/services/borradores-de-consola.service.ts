import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TransporteDeBorrador } from './autoguardado-de-borrador';
import { ApiClientService } from '../http/api-client.service';

/**
 * El borrador que la consola guarda sola mientras alguien llena un formulario largo.
 *
 * <b>QUE PROBLEMA RESUELVE.</b> Agenda, Noticias y Catálogo Editorial se llenan dentro de un
 * diálogo, de una sentada. Un corte de conexión, una sesión caducada o una pestaña cerrada sin
 * querer se llevaban por delante todo lo escrito y sin rastro.
 *
 * <b>NO CREA CONTENIDO.</b> Crear el evento en estado «borrador» al primer teclazo llenaría la
 * consola de fichas a medias que alguien tendría que ir archivando. El borrador vive aparte y
 * desaparece en cuanto el formulario se guarda de verdad.
 */

export interface BorradorDeConsola {
  id: number;
  dominio: string;
  datosJson: string;
  version: number;
  fechaActualizacion: string;
}

/**
 * Los formularios que guardan borrador. Los mismos que acepta el servidor.
 *
 * DECIA `editorial` Y EL SERVIDOR PASO A DECIR `catalogo-editorial`. El 21 de septiembre de 2026 el
 * módulo de un registro quedó con un solo nombre en todo el sistema —plural y con guion medio—, y
 * este valor viaja en la URL: `/api/v1/institucional/borradores/{dominio}`. Mientras dijera
 * `editorial`, el borrador del Catálogo Editorial no se encontraba y la pantalla perdía lo escrito
 * sin avisar de nada.
 */
export type DominioDeBorrador = 'agenda' | 'noticias' | 'catalogo-editorial';

@Injectable({ providedIn: 'root' })
export class BorradoresDeConsolaService {
  private readonly api = inject(ApiClientService);

  private static readonly RUTA = '/api/v1/institucional/borradores';

  /** El borrador abierto, o `null` si no hay ninguno. Un fallo también devuelve `null`. */
  async leer(dominio: DominioDeBorrador): Promise<BorradorDeConsola | null> {
    try {
      // EL SERVIDOR RESPONDE 204 SIN CUERPO cuando no hay ninguno: `firstValueFrom` entrega
      // entonces `null`, y eso es exactamente lo que significa.
      return await firstValueFrom(this.api.get<BorradorDeConsola>(`${BorradoresDeConsolaService.RUTA}/${dominio}`, {})) ?? null;
    } catch {
      // UN BORRADOR QUE NO SE PUEDE LEER NO PUEDE IMPEDIR ABRIR EL FORMULARIO. Se sigue sin él.
      return null;
    }
  }

  async guardar(dominio: DominioDeBorrador, datos: unknown, version: number | null): Promise<BorradorDeConsola | null> {
    try {
      return await firstValueFrom(this.api.put<BorradorDeConsola>(
        `${BorradoresDeConsolaService.RUTA}/${dominio}`,
        { datosJson: JSON.stringify(datos), version },
        {}));
    } catch {
      return null;
    }
  }

  /**
   * El transporte de un dominio de la consola, listo para `AutoguardadoDeBorrador`.
   *
   * <b>AQUI Y NO EN CADA PANTALLA</b> porque los tres módulos de la consola hablan por la misma
   * ruta y con la misma forma: repetir el adaptador tres veces sería repetir también el
   * `JSON.parse` defensivo, que es la parte que se escribe mal al copiar.
   */
  transporte<T>(dominio: DominioDeBorrador): TransporteDeBorrador<T> {
    return {
      leer: async () => {
        const fila = await this.leer(dominio);
        if (!fila) return null;
        try {
          return { datos: JSON.parse(fila.datosJson) as T, version: fila.version, fechaActualizacion: fila.fechaActualizacion };
        } catch {
          // UN BORRADOR ILEGIBLE NO SE OFRECE. El servidor valida que sea JSON, así que llegar aquí
          // significa que cambió la forma de lo guardado; ofrecerlo devolvería un formulario roto.
          return null;
        }
      },
      guardar: async (datos, version) => {
        const fila = await this.guardar(dominio, datos, version);
        return fila ? { version: fila.version, fechaActualizacion: fila.fechaActualizacion } : null;
      },
      descartar: () => this.descartar(dominio),
    };
  }

  async descartar(dominio: DominioDeBorrador): Promise<void> {
    try {
      await firstValueFrom(this.api.delete<void>(`${BorradoresDeConsolaService.RUTA}/${dominio}`, {}));
    } catch {
      // DESCARTAR ES DE MEJOR ESFUERZO. Si falla, queda un borrador que la próxima vez se ofrece
      // recuperar; molesto, pero nunca peor que perder lo escrito.
    }
  }
}
