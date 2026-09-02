import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

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
  constructor(private readonly http: HttpClient) {}

  listar(): Observable<FestivalPanel[]> {
    return this.http.get<FestivalPanel[]>('/api/mi-panel/festivales', { withCredentials: true });
  }

  crear(idOrganizacion: string, solicitud: SolicitudFestival): Observable<{ idFestival: string }> {
    return this.http.post<{ idFestival: string }>('/api/mi-panel/festivales', { idOrganizacion, ...solicitud }, { withCredentials: true });
  }

  actualizarBorrador(idFestival: string, solicitud: SolicitudFestival): Observable<void> {
    return this.http.patch<void>(`/api/mi-panel/festivales/${idFestival}/perfil-borrador`, solicitud, { withCredentials: true });
  }
}
