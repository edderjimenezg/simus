import { Injectable, signal, computed, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import {
  AdminService,
  RespuestaLoginAdministrativo,
  RespuestaSesionAdministrativa,
  UsuarioAdministrativo,
} from './admin.service';

@Injectable({
  providedIn: 'root'
})
export class SessionService {
  private readonly adminService = inject(AdminService);
  private _currentUser = signal<UsuarioAdministrativo | null>(null);

  currentUser = computed(() => this._currentUser());
  isAuthenticated = computed(() => this._currentUser() !== null);

  checkSession(): Observable<RespuestaSesionAdministrativa | null> {
    return this.adminService.cargarSesionAdministrativa().pipe(
      tap((res) => {
        if (res && res.user) {
          this._currentUser.set(res.user);
        } else {
          this._currentUser.set(null);
        }
      }),
      catchError(() => {
        this._currentUser.set(null);
        return of(null);
      })
    );
  }

  login(credentials: { email: string; password?: string }): Observable<RespuestaLoginAdministrativo> {
    return this.adminService.iniciarSesionAdministrativa(credentials).pipe(
      tap((res) => {
        if (res && res.user) {
          this._currentUser.set(res.user);
        }
      })
    );
  }

  logout(): Observable<unknown> {
    return this.adminService.cerrarSesionAdministrativa().pipe(
      tap(() => {
        this._currentUser.set(null);
      }),
      catchError((err) => {
        this._currentUser.set(null);
        throw err;
      })
    );
  }

  setSession(user: UsuarioAdministrativo | null): void {
    this._currentUser.set(user);
  }
}
