import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SesionExpiradaService } from './sesion-expirada.service';

/**
 * Convierte un 401 en una sesión cerrada de verdad.
 *
 * ## La distinción que hace todo el trabajo
 *
 * No todo 401 es una sesión caducada:
 *
 * - `POST /auth/login` responde 401 con **credenciales incorrectas**. Tratarlo
 *   como caducidad expulsaría a quien todavía no ha entrado, y le diría que su
 *   sesión expiró cuando lo que pasó es que se equivocó de contraseña.
 * - `GET /auth/me` responde 401 cuando **no hay sesión**, que es su forma
 *   normal de decir «este visitante no ha iniciado sesión». Ocurre en cada
 *   arranque de la aplicación.
 * - `POST /auth/logout` puede responder 401 si la sesión ya se fue.
 *
 * Confundir cualquiera de los tres con una caducidad sería un defecto peor que
 * el que este interceptor arregla. Por eso se excluyen explícitamente.
 *
 * ## Qué sí cuenta
 *
 * Un 401 en cualquier otra ruta que exija sesión —`/api/v1/admin/`,
 * `/api/v1/institucional/` o `/api/v1/externo/`— significa una cosa sola: había
 * sesión, se usó, y el servidor ya no la reconoce. Eso es lo que hay que
 * decirle a la persona.
 *
 * LA LISTA ESTABA INCOMPLETA, y no por el renombrado del 14 de septiembre de
 * 2026: decía `admin|external` y dejaba fuera los DOS ámbitos en español, que
 * son 125 de las 245 operaciones del API. Un 401 en cualquiera de ellas no se
 * reconocía como caducidad.
 *
 * Las rutas públicas (`/api/v1/contenido-web`, noticias, agenda…) quedan fuera a
 * propósito: son anónimas y un 401 ahí sería otro problema distinto.
 */
export const sesionExpiradaInterceptor: HttpInterceptorFn = (req, next) => {
  const sesionExpirada = inject(SesionExpiradaService);

  const esRutaDeAutenticacion = /\/auth\/(login|logout|me)\b/.test(req.url);
  const esRutaConSesion = /\/api\/v1\/(admin|institucional|externo)\//.test(req.url);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        esRutaConSesion &&
        !esRutaDeAutenticacion
      ) {
        const ruta =
          typeof location !== 'undefined' ? location.pathname + location.search : null;
        sesionExpirada.marcarExpirada(ruta);
      }
      return throwError(() => error);
    }),
  );
};
