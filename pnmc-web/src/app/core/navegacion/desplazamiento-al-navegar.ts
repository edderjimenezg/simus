import { ViewportScroller } from '@angular/common';
import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, Router, Scroll } from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * Cuándo una navegación sube la página arriba, y cuándo no.
 *
 * <b>POR QUE EXISTE.</b> El enrutador estaba configurado con
 * <c>scrollPositionRestoration: 'top'</c>, que sube arriba en TODA navegación. Eso es correcto al
 * cambiar de pantalla y es un salto molesto cuando lo único que cambia es el estado de la que ya
 * estás mirando: en el Catálogo Editorial, abrir una ficha desde el mosaico devolvía el listado al
 * principio, así que al cerrarla habías perdido dónde estabas. El criterio es este: * «al abrir las fichas en la vista mosaico se hace un autoscroll que vuelve a la parte superior».
 *
 * <b>LA REGLA: SE SUBE CUANDO CAMBIA LA PANTALLA, NO CUANDO CAMBIA SU ESTADO.</b> Y «pantalla» aquí
 * tiene una definición exacta y comprobable: la cadena de rutas que TIENEN COMPONENTE. La ficha del
 * catálogo es una ruta hija SIN componente —existe para dar dirección a una obra sin desmontar el
 * listado—, así que abrirla no cambia ninguna pantalla y no mueve el desplazamiento. Navegar de
 * `/editorial` a `/noticias` sí, y sube.
 *
 * <b>ATRAS Y ADELANTE SIGUEN RESTAURANDO SU POSICION</b>, que es lo que hacía la configuración que
 * se retira: cuando el evento trae posición guardada se usa esa, y cuando trae un ancla se va al
 * ancla. Solo cambia el tercer caso —una navegación nueva—, que antes era siempre «arriba» y ahora
 * es «arriba solo si de verdad cambiaste de pantalla».
 *
 * <b>POR QUE A MANO Y NO CON `withInMemoryScrolling`.</b> Ese ayudante no admite excepciones: o sube
 * siempre o no sube nunca. La alternativa era dejar de navegar al abrir una ficha —cambiando la
 * dirección con `Location.go` por detrás—, y eso desincroniza el estado del enrutador con la barra
 * de direcciones: a partir de ahí, cualquier enlace relativo de la página apunta a donde no es.
 */
export function configurarDesplazamientoAlNavegar(): void {
  const enrutador = inject(Router);
  const ventana = inject(ViewportScroller);

  let pantallaAnterior: string | null = null;

  enrutador.events
    .pipe(filter((evento): evento is Scroll => evento instanceof Scroll))
    .subscribe(evento => {
      const pantalla = identidadDePantalla(enrutador.routerState.snapshot.root);
      const cambioDePantalla = pantalla !== pantallaAnterior;
      pantallaAnterior = pantalla;

      if (evento.position) {
        // Atrás o adelante: se vuelve exactamente a donde se estaba.
        ventana.scrollToPosition(evento.position);
      } else if (evento.anchor) {
        ventana.scrollToAnchor(evento.anchor);
      } else if (cambioDePantalla) {
        ventana.scrollToPosition([0, 0]);
      }
    });
}

/**
 * La identidad de la pantalla: los tramos de ruta que montan un componente, con sus parámetros.
 *
 * <b>LOS TRAMOS SIN COMPONENTE NO CUENTAN, y eso es lo que hace que un detalle no sea una
 * pantalla.</b> `/editorial` y `/editorial/PNMC-ED-019` dan la misma identidad —`editorial`—
 * porque el segundo tramo existe solo para dar dirección a una obra y no monta nada.
 *
 * <b>LOS PARAMETROS DE UN TRAMO QUE SI MONTA COMPONENTE CUENTAN, y eso costó una regresión.</b> La
 * primera versión miraba solo la ruta, y en el Espacio de Gestión Administrativa la sección viaja
 * como PARAMETRO de una ruta que sí monta pantalla —`administracion/:seccion`—: con la regla a
 * medias, pasar de «Catálogo Editorial» a «Usuarios y roles» dejaba la página donde estuviera,
 * a mitad de la lista anterior. Comprobado: 1 200 px de desplazamiento que se quedaban puestos.
 *
 * Se mira la configuración de la ruta y no la dirección, que es lo que hace que la regla no dependa
 * de recordar una lista de excepciones.
 */
export function identidadDePantalla(raiz: ActivatedRouteSnapshot): string {
  const tramos: string[] = [];
  let nodo: ActivatedRouteSnapshot | null = raiz;
  while (nodo) {
    const config = nodo.routeConfig;
    if (config && (config.component || config.loadComponent)) {
      const parametros = Object.entries(nodo.params)
        .map(([clave, valor]) => `${clave}=${String(valor)}`)
        .sort()
        .join(',');
      // UNA RUTA CON EMPAREJADOR NO TIENE `path` —la consola administrativa usa uno—, y con el
      // `?? ''` de antes su identidad quedaba vacia, es decir, la MISMA que la del Inicio, cuya
      // ruta es la cadena vacia. Dos pantallas con la misma identidad no cuentan como cambio de
      // pantalla y el desplazamiento no vuelve arriba. Cuando no hay `path`, sirven los tramos de
      // direccion que la ruta consumio, que para eso los guarda el enrutador.
      const nombre = config.path ?? nodo.url.map(tramo => tramo.path).join('/');
      tramos.push(parametros ? `${nombre}(${parametros})` : nombre);
    }
    nodo = nodo.firstChild;
  }
  return tramos.join('/');
}
