import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface EstadoApi {
  api: 'disponible' | 'no_disponible';
  baseDatos: 'disponible' | 'no_disponible' | 'no_configurada';
}

export interface DisponibilidadRegistro {
  registroDisponible: boolean;
  territorioDisponible: boolean;
  documentos: DocumentoConsentimiento[];
  impedimentos: string[];
}

export interface DocumentoConsentimiento {
  id: string;
  codigo: string;
  titulo: string;
  version: string;
  urlPublica: string;
}

@Injectable({ providedIn: 'root' })
export class ServicioEstadoApi {
  constructor(private readonly http: HttpClient) {}

  obtenerSalud(): Observable<EstadoApi> {
    return this.http.get<EstadoApi>('/api/salud');
  }

  obtenerDisponibilidadRegistro(): Observable<DisponibilidadRegistro> {
    return this.http.get<DisponibilidadRegistro>('/api/registro/preparacion');
  }
}
