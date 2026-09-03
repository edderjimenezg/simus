import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ServicioProteccionSolicitud } from './servicio-proteccion-solicitud';

export interface FestivalPanel {
  idFestival: string;
  idPerfil: string;
  nombre: string;
  estadoEditorial: string;
  descripcion: string | null;
  codigoDepartamento: string;
  departamento: string;
  codigoMunicipio: string;
  municipio: string;
  fechaActualizacion: string;
  idOrganizacion: string;
  organizacion: string;
}

export interface SolicitudFestival {
  nombre: string;
  descripcion?: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
}

@Injectable({ providedIn: 'root' })
export class ServicioFestivales {
  constructor(private readonly http: HttpClient, private readonly proteccion: ServicioProteccionSolicitud) {}

  listar(): Observable<FestivalPanel[]> {
    return this.http.get<FestivalPanel[]>('/api/mi-panel/festivales', { withCredentials: true });
  }

  crear(idOrganizacion: string, solicitud: SolicitudFestival): Observable<{ idFestival: string }> {
    return this.http.post<{ idFestival: string }>('/api/mi-panel/festivales', { idOrganizacion, ...solicitud }, this.proteccion.opciones());
  }

  actualizarBorrador(idFestival: string, solicitud: SolicitudFestival): Observable<void> {
    return this.http.patch<void>(`/api/mi-panel/festivales/${idFestival}/perfil-borrador`, solicitud, this.proteccion.opciones());
  }
}
