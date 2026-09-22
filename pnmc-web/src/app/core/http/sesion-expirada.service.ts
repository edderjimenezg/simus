import { Injectable, signal } from '@angular/core';

/**
 * Estado de «la sesión caducó mientras trabajabas».
 *
 * ## Por qué existe
 *
 * No había ningún interceptor HTTP (`app.config.ts` registraba
 * `provideHttpClient()` a secas). Cuando la cookie de sesión caducaba, el
 * sondeo de fondo recibía el 401 y lo único que ocurría era que
 * `admin-shell-page.component.ts-522` escribía «Error de conexión» en un
 * texto de 9,3 px de la barra lateral.
 *
 * `isAuthenticated()` seguía devolviendo `true`, la consola seguía pintada, y
 * la persona seguía pulsando botones que fallaban de siete maneras distintas
 * sin que ninguna dijera nunca *tu sesión caducó*.
 *
 * ## Por qué es un servicio sin dependencias
 *
 * El interceptor no puede inyectar `SessionService` sin arriesgar un ciclo
 * (`SessionService` → `AdminService` → `ApiClient` → interceptor). Este
 * servicio no depende de nada, así que corta el ciclo: el interceptor solo
 * levanta la bandera y quien sepa qué hacer con ella reacciona.
 */
@Injectable({ providedIn: 'root' })
export class SesionExpiradaService {
  private readonly _expirada = signal<boolean>(false);
  private readonly _rutaDeRetorno = signal<string | null>(null);

  /** La sesión caducó y aún no se ha atendido. */
  readonly expirada = this._expirada.asReadonly();

  /**
   * Dónde estaba la persona cuando caducó, para poder devolverla ahí.
   * Antes no se guardaba en ningún sitio: tras volver a entrar siempre se
   * aterrizaba en el panel inicial.
   */
  readonly rutaDeRetorno = this._rutaDeRetorno.asReadonly();

  marcarExpirada(ruta: string | null): void {
    // Solo la primera vez: el sondeo de fondo dispara once peticiones cada diez
    // segundos y todas fallarían con 401. Sin esta guarda, la ruta de retorno
    // se sobrescribiría en bucle y el aviso parpadearía.
    if (this._expirada()) return;
    this._rutaDeRetorno.set(ruta);
    this._expirada.set(true);
  }

  limpiar(): void {
    this._expirada.set(false);
    this._rutaDeRetorno.set(null);
  }
}
