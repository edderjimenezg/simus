import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface EstadoApi {
  api: 'disponible' | 'no_disponible';
  baseDatos: 'disponible' | 'no_disponible' | 'no_configurada';
}

export interface DisponibilidadRegistro {
  registroDisponible: boolean;
  impedimentos: string[];
}

@Injectable({ providedIn: 'root' })
export class ServicioEstadoApi {
  constructor(private readonly http: HttpClient) {}

  obtenerSalud(): Observable<EstadoApi> {
    return this.http.get<EstadoApi>('/api/salud');
  }

  obtenerDisponibilidadRegistro(): Observable<DisponibilidadRegistro> {
    return this.http.get<DisponibilidadRegistro>('/api/registro/disponibilidad');
  }
}
