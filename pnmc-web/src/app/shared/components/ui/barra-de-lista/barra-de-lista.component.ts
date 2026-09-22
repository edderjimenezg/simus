import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * La barra de una lista de la consola: el sitio donde van el buscador, los filtros, el ojo y las
 * acciones, y el orden en que van.
 *
 * <b>ERA UNA CONVENCION Y AHORA ES UNA PIEZA.</b> Hasta la barra se
 * componía a mano en doce paneles: las mismas clases en el mismo orden porque alguien lo copió bien.
 * Y se copió mal tres veces —Noticias, Usuarios y roles, Mercados—, cada una reclamada por el dueño
 * del proyecto: «nuevamente no se usó el patrón que ya hemos construido». Una convención se rompe en
 * el siguiente panel; una pieza no deja elegir.
 *
 * <b>COLOCA POR TIPO, NO POR ORDEN DE ESCRITURA.</b> El buscador va primero, después lo que acota la
 * lista, después el ojo que oculta un estado, y las acciones al extremo derecho. Se proyecta cada
 * pieza a su hueco por su selector: quien escribe el panel puede ponerlas en cualquier orden y salen
 * en el correcto. Lo que no se pasa no se pinta —una lista sin ojo no tiene un hueco vacío—.
 *
 * <b>EL EXTREMO ES LO UNICO QUE HAY QUE MARCAR</b>, con el atributo `extremo`, porque ahí van
 * botones y no hay un selector de elemento que los distinga de un botón cualquiera. Todo lo demás
 * se coloca solo.
 *
 * <b>LAS PESTAÑAS NO VAN DENTRO.</b> El patrón las pone DEBAJO de la barra, fuera de ella —lo fijó
 * la dirección de producto sobre Agenda—, así que esta pieza no tiene hueco para un
 * `app-selector-segmentado`: si alguien lo mete, cae al hueco libre y se nota.
 *
 * <b>UNA PIEZA CONDICIONAL SE PROYECTA CON `ngProjectAs`.</b> Angular proyecta el contenido de un
 * `@if` al hueco por omisión; para que un ojo condicional caiga en su sitio hay que envolverlo:
 * `<ng-container ngProjectAs="app-conmutador-de-ojo">`. Está escrito aquí para no descubrirlo
 * doce veces.
 */
@Component({
  selector: 'app-barra-de-lista',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Sin estilos propios: la barra ya tiene los suyos en `styles.css` (`.barra-de-lista` y
  // `.barra-de-lista__extremo`), y son los mismos para todas. Esta pieza fija el ORDEN, no el
  // aspecto.
  styles: [':host { display: contents; }'],
  template: `
    <div class="barra-de-lista" data-barra-de-lista>
      <ng-content select="app-buscador-de-lista" />
      <ng-content select="app-filtro-desplegable" />
      <ng-content select="app-conmutador-de-ojo" />
      <ng-content />
      <div class="barra-de-lista__extremo">
        <ng-content select="[extremo]" />
      </div>
    </div>
  `,
})
export class BarraDeListaComponent {}
