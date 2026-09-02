import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ResultadoIngreso {
  idPersona: string;
}

@Injectable({ providedIn: 'root' })
export class ServicioAcceso {
  constructor(private readonly http: HttpClient) {}

  ingresar(correo: string, contrasena: string): Observable<ResultadoIngreso> {
    return this.http.post<ResultadoIngreso>('/api/acceso/ingresar', { correo, contrasena }, { withCredentials: true });
  }

  cerrarSesion(): Observable<void> {
    return this.http.post<void>('/api/sesion/cerrar', {}, { withCredentials: true });
  }
}
