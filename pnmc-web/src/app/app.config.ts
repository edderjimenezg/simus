import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsCO from '@angular/common/locales/es-CO';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { configurarDesplazamientoAlNavegar } from './core/navegacion/desplazamiento-al-navegar';
import { sesionExpiradaInterceptor } from './core/http/sesion-expirada.interceptor';
import { TextosWebService } from './core/services/textos-web.service';

/**
 * Tiempo máximo que el arranque espera al contenido del CMS.
 *
 * Se espera —y no se carga en segundo plano— para que la página no se pinte con
 * los textos compilados y los cambie medio segundo después: ese parpadeo es
 * peor que medio segundo de espera. Pero se espera <b>poco</b>: si la API no
 * está, el sitio abre igual con su texto de fábrica.
 */
const CMS_ARRANQUE_MS = 2500;

/**
 * Los datos de localizacion de es-CO.
 *
 * <b>Que estaba roto.</b> La cola de revision institucional pinta cuando se envio cada Festival
 * con `| date:'medium':'':'es-CO'`, y en todo `src` no habia ni un `registerLocaleData` ni un
 * `LOCALE_ID`. Angular no trae mas datos que los de `en-US`, asi que el pipe lanzaba
 * <code>NG02100: InvalidPipeArgument: 'NG0701: Missing locale data for the locale "es-CO"'</code>
 * y el parrafo entero se quedaba en blanco: la tarjeta decia el nombre del Festival y la
 * organizacion, y no decia desde cuando esperaba. Medido abriendo /admin
 * con `gestor@pnmc.local` y leyendo la consola del navegador.
 *
 * <b>Por que se registra el idioma y NO se cambia `LOCALE_ID`.</b> Registrar solo anade los datos
 * al catalogo: unicamente cambia lo que ya pedia `es-CO` expresamente, que son las dos lineas de
 * ese panel. Fijar `LOCALE_ID` cambiaria ademas TODAS las fechas y numeros del sitio publico, y
 * `cms:snapshot:check` compara el texto renderizado de las paginas publicas —la Agenda pinta
 * fechas—, asi que una correccion de dos lineas pondria esa puerta en rojo por otro motivo.
 */
registerLocaleData(localeEsCO, 'es-CO');

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    // EL DESPLAZAMIENTO AL NAVEGAR SE DECIDE A MANO, y no con `withInMemoryScrolling`, porque ese
    // ayudante no admite excepciones: o sube arriba en toda navegación o no sube nunca. La regla que
    // hace falta es más fina —se sube al cambiar de PANTALLA, no al cambiar el estado de la que ya
    // estás mirando— y vive en `configurarDesplazamientoAlNavegar`, con su porqué.
    provideRouter(routes, withInMemoryScrolling({ anchorScrolling: 'enabled' })),
    // El interceptor de sesión caducada. Antes esto era `provideHttpClient()` a
    // secas: no había NINGÚN interceptor, así que un 401 no cerraba la sesión.
    // La consola seguía pintada con `isAuthenticated()` en `true` y cada acción
    // fallaba con un mensaje distinto sin decir nunca que la sesión expiró.
    provideHttpClient(withInterceptors([sesionExpiradaInterceptor])),
    provideAppInitializer(() => configurarDesplazamientoAlNavegar()),
    provideAppInitializer(() => {
      const webTexts = inject(TextosWebService);

      // La promesa se resuelve siempre: `loadPublishedContent` no lanza, y la
      // carrera con el reloj cubre el caso de una API que acepta la conexión y
      // no contesta. Un arranque que se quedara colgado dejaría el sitio sin
      // pintar, que es el único desenlace inaceptable.
      return Promise.race([
        webTexts.loadPublishedContent(CMS_ARRANQUE_MS),
        new Promise<boolean>(resolve => setTimeout(() => resolve(false), CMS_ARRANQUE_MS)),
      ]);
    }),
  ]
};
