import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ServicioProteccionSolicitud {
  private token = '';

  constructor(private readonly http: HttpClient) {}

  preparar(): Observable<{ token: string }> {
    return this.http.get<{ token: string }>('/api/sesion/proteccion', { withCredentials: true }).pipe(
      tap(resultado => this.token = resultado.token)
    );
  }

  opciones() {
    return { withCredentials: true, headers: new HttpHeaders({ 'X-SIMUS-CSRF': this.token }) };
  }
}
