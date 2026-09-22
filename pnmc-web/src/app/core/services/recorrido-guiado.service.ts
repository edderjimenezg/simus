import { Injectable, signal } from '@angular/core';

/**
 * Si hay un recorrido guiado en marcha.
 *
 * <b>POR QUE HACE FALTA UNA SEÑAL COMPARTIDA PARA ESTO.</b> El botón flotante del sitio está fijo
 * en la esquina inferior derecha, y la tarjeta del recorrido se coloca donde quepa: en pantallas
 * estrechas eso es a menudo esa misma esquina. Medido a 390 px, en el paso de la coropleta la
 * tarjeta ocupa de (12, 582) a (378, 857) y el botón cae dentro, encima de «Siguiente».
 *
 * Apartarlo no es solo evitar el solape: un botón que invita a irse a otra parte no pinta nada
 * durante una explicación paso a paso. Si además estorba, la decisión se toma sola.
 *
 * Se resuelve con una señal y no subiendo el `z-index` de la tarjeta porque los dos elementos
 * viven en ramas distintas del árbol —uno en la página, otro junto a la raíz— y quién queda encima
 * de quién dependería de detalles del maquetado que nadie está mirando al escribir una pantalla.
 *
 * <b>VIVE EN `core` Y NO EN EL MAPA</b> porque la pregunta la hacen dos sitios que no se conocen
 * —la raíz de la aplicación y una página— y porque el día que otra pantalla tenga recorrido, la
 * respuesta ya está.
 */
@Injectable({ providedIn: 'root' })
export class RecorridoGuiadoService {
  private readonly enMarcha = signal(false);

  /** Solo lectura para quien únicamente necesita saberlo. */
  readonly hayRecorrido = this.enMarcha.asReadonly();

  abrir(): void {
    this.enMarcha.set(true);
  }

  cerrar(): void {
    this.enMarcha.set(false);
  }
}
