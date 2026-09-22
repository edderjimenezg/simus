import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

/**
 * La vista previa del panel se decide ANTES de arrancar, no despues.
 *
 * El bloque de `agentation` de abajo vive dentro del `.then()`, es decir cuando
 * la aplicacion ya arranco: ahi ya no se puede cambiar un proveedor. La vista
 * previa sustituye `TextosWebService`, asi que tiene que entrar en la
 * configuracion del arranque.
 *
 * Las dos condiciones son necesarias. `!environment.production` mantiene el
 * puente fuera del paquete desplegado —`angular.json` sustituye el fichero de
 * entorno, el `if` queda en `false` y este `import()` nunca se pide—, y el
 * parametro en la direccion evita que una pestana normal de desarrollo cargue un
 * servicio distinto del que corre en produccion.
 */
async function configuracionDeArranque() {
  const esVistaPrevia = typeof location !== 'undefined'
    && new URLSearchParams(location.search).has('pnmcVista');

  if (environment.production || !esVistaPrevia) { return appConfig; }

  const { PROVEEDOR_DE_VISTA_PREVIA } = await import('./app/core/cms/vista-previa-puente');
  return { ...appConfig, providers: [...appConfig.providers, PROVEEDOR_DE_VISTA_PREVIA] };
}

configuracionDeArranque()
  .then((config) => bootstrapApplication(AppComponent, config))
  .then(() => {
    if (!environment.production) {
      void import('./agentation').then(({ mountAgentation }) => mountAgentation());
    }
  })
  .catch((err) => console.error(err));
