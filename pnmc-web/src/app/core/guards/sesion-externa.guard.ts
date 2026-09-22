import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs/operators';
import { ExternalSessionService } from '../services/external-session.service';

/**
 * Deniega el paso a las pantallas que solo existen con sesión externa abierta.
 *
 * <b>Qué reemplaza.</b> `auth.guard.ts` estaba escrito, no se importaba en ninguna ruta —las
 * treinta entradas de `app.routes.ts` iban sin `canActivate`— y además devolvía `true` en TODAS
 * sus ramas, incluida aquella en la que el servidor contesta que no hay sesión. Es decir:
 * aparentaba proteger y no protegía. Un fichero así es peor que ninguno, porque quien lo lee
 * deja de buscar dónde está el control de acceso.
 *
 * <b>Por qué este guard sí puede denegar y aquel no podía.</b> La consola institucional vive en
 * una sola ruta, `/admin`, y esa ruta ES su propia pantalla de acceso: sin sesión pinta el
 * formulario. Un guard que la denegara tendría que redirigir a `/admin`, es decir, a sí misma.
 * El panel externo es otra cosa: su puerta —`/ecosistema/ingresar`— es una pantalla DISTINTA, así
 * que aquí denegar sí tiene a dónde mandar a quien llega sin sesión.
 *
 * <b>Qué evita.</b> Sin esto, `/gestion` respondía a quien no ha entrado pintando el
 * formulario de acceso dentro del panel: la barra de direcciones decía «gestión» mientras la
 * pantalla pedía credenciales. Es el mismo defecto que se corrigió al entrar —una dirección que
 * no corresponde a lo que se ve— y se arrastraba por el otro extremo del recorrido.
 *
 * <b>Por qué consulta al servidor y no solo a la señal.</b> Al abrir un enlace guardado o al
 * recargar, la señal local nace vacía aunque la cookie siga siendo válida. Comprobar solo la señal
 * echaría fuera a quien sí tiene sesión, que es un fallo peor que el que se corrige.
 */
export const sesionExternaGuard: CanActivateFn = () => {
  const sesionExterna = inject(ExternalSessionService);
  const router = inject(Router);

  if (sesionExterna.dentro()) {
    return true;
  }

  return sesionExterna.refrescar().pipe(
    map(sesion => sesion !== null || router.createUrlTree(['/ingresar'])),
  );
};
