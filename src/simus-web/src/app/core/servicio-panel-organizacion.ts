import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TerritorioRegistro } from './servicio-registro-externo';
import { ServicioProteccionSolicitud } from './servicio-proteccion-solicitud';

export interface PersonaPanel {
  primerNombre: string;
  primerApellido: string;
  correo: string;
  telefono: string | null;
}

export interface OrganizacionPanel {
  id: string;
  nombre: string;
  numeroIdentificacion: string | null;
  estado: string;
  codigoDepartamento: string;
  departamento: string;
  codigoMunicipio: string;
  municipio: string;
  fechaActualizacion: string;
}

export interface ContextoPanel {
  persona: PersonaPanel;
  organizaciones: OrganizacionPanel[];
}

export interface AdministradorOrganizacion {
  idPersona: string;
  nombre: string;
  correo: string;
  telefono: string | null;
  fechaAsignacion: string;
}

export interface ActualizacionOrganizacion {
  nombre: string;
  numeroIdentificacion?: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
}

@Injectable({ providedIn: 'root' })
export class ServicioPanelOrganizacion {
  constructor(private readonly http: HttpClient, private readonly proteccion: ServicioProteccionSolicitud) {}

  obtenerContexto(): Observable<ContextoPanel> {
    return this.http.get<ContextoPanel>('/api/mi-panel/contexto', { withCredentials: true });
  }

  obtenerAdministradores(idOrganizacion: string): Observable<AdministradorOrganizacion[]> {
    return this.http.get<AdministradorOrganizacion[]>(`/api/mi-panel/organizaciones/${idOrganizacion}/administradores`, { withCredentials: true });
  }

  actualizarOrganizacion(idOrganizacion: string, solicitud: ActualizacionOrganizacion): Observable<void> {
    return this.http.patch<void>(`/api/mi-panel/organizaciones/${idOrganizacion}`, solicitud, this.proteccion.opciones());
  }

  obtenerDepartamentos(): Observable<TerritorioRegistro[]> {
    return this.http.get<TerritorioRegistro[]>('/api/registro/departamentos', { withCredentials: true });
  }

  obtenerMunicipios(codigoDepartamento: string): Observable<TerritorioRegistro[]> {
    return this.http.get<TerritorioRegistro[]>(`/api/registro/departamentos/${encodeURIComponent(codigoDepartamento)}/municipios`, { withCredentials: true });
  }
}
