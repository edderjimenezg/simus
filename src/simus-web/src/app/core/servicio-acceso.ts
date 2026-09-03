import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ServicioProteccionSolicitud } from './servicio-proteccion-solicitud';

export interface ResultadoIngreso {
  idPersona: string;
}

@Injectable({ providedIn: 'root' })
export class ServicioAcceso {
  constructor(private readonly http: HttpClient, private readonly proteccion: ServicioProteccionSolicitud) {}

  ingresar(correo: string, contrasena: string): Observable<ResultadoIngreso> {
    return this.http.post<ResultadoIngreso>('/api/acceso/ingresar', { correo, contrasena }, { withCredentials: true });
  }

  cerrarSesion(): Observable<void> {
    return this.http.post<void>('/api/sesion/cerrar', {}, this.proteccion.opciones());
  }
}
