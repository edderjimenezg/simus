import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface TerritorioRegistro { codigo: string; nombre: string; }

export interface SolicitudRegistroExterno {
  primerNombre: string;
  segundoNombre?: string;
  primerApellido: string;
  segundoApellido?: string;
  codigoTipoIdentificacion: string;
  numeroIdentificacion: string;
  correo: string;
  telefono?: string;
  contrasena: string;
  nombreOrganizacion: string;
  numeroIdentificacionOrganizacion?: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  codigosDocumentosAceptados: string[];
}

export interface ResultadoRegistroExterno { idPersona: string; idOrganizacion: string; }

@Injectable({ providedIn: 'root' })
export class ServicioRegistroExterno {
  constructor(private readonly http: HttpClient) {}

  obtenerDepartamentos(): Observable<TerritorioRegistro[]> {
    return this.http.get<TerritorioRegistro[]>('/api/registro/departamentos');
  }

  obtenerMunicipios(codigoDepartamento: string): Observable<TerritorioRegistro[]> {
    return this.http.get<TerritorioRegistro[]>(`/api/registro/departamentos/${encodeURIComponent(codigoDepartamento)}/municipios`);
  }

  registrar(solicitud: SolicitudRegistroExterno): Observable<ResultadoRegistroExterno> {
    return this.http.post<ResultadoRegistroExterno>('/api/registro', solicitud, { withCredentials: true });
  }
}
