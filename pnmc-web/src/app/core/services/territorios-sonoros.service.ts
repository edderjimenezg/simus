import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

export interface TerritorioSonoroConceptual {
  id: number; nombre: string; slug: string; orden: number;
  definicionBreve: string | null; definicionAmpliada: string | null; descripcionConceptual: string | null;
  caracteristicas: string | null; relacionTerritorial: string | null; contextos: string | null;
  ejemplos: string | null; fuentes: string | null; recursoVisualUrl: string | null; textoAlternativoRecurso: string | null;
}

@Injectable({ providedIn: 'root' })
export class TerritoriosSonorosService {
  private readonly api = inject(ApiClientService);
  private readonly catalogo$ = this.api.get<TerritorioSonoroConceptual[]>('/api/v1/publico/territorios-sonoros', { errorFallback: 'No fue posible consultar los Territorios sonoros.' }).pipe(shareReplay({ bufferSize: 1, refCount: true }));
  listar(): Observable<TerritorioSonoroConceptual[]> { return this.catalogo$; }
  consultar(slug: string): Observable<TerritorioSonoroConceptual> { return this.api.get(`/api/v1/publico/territorios-sonoros/${encodeURIComponent(slug)}`, { errorFallback: 'No fue posible consultar este Territorio sonoro.' }); }
}
