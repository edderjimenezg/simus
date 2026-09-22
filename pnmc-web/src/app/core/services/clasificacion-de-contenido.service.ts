import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * Los dos catálogos con los que se clasifica un contenido: prácticas musicales y territorios
 * sonoros.
 *
 * <b>SE LEEN DE LAS RUTAS PUBLICAS QUE YA EXISTEN</b> —las mismas que alimentan las fichas
 * conceptuales del portal— en vez de abrir dos rutas nuevas para la consola. Son catálogos de
 * lectura, idénticos para todo el mundo, y duplicarlos daría dos sitios donde el orden podría
 * discrepar.
 *
 * <b>SE PIDEN UNA VEZ POR SESION.</b> Cambian con muy poca frecuencia y los abren tres
 * formularios; volver a pedirlos cada vez que se abre un diálogo es gasto sin contrapartida.
 */

export interface ElementoDeClasificacion {
  id: number;
  nombre: string;
  slug: string;
}

@Injectable({ providedIn: 'root' })
export class ClasificacionDeContenidoService {
  private readonly api = inject(ApiClientService);

  private practicas: Promise<ElementoDeClasificacion[]> | null = null;
  private territorios: Promise<ElementoDeClasificacion[]> | null = null;

  practicasMusicales(): Promise<ElementoDeClasificacion[]> {
    this.practicas ??= this.leer('/api/v1/publico/practicas-musicales');
    return this.practicas;
  }

  territoriosSonoros(): Promise<ElementoDeClasificacion[]> {
    this.territorios ??= this.leer('/api/v1/publico/territorios-sonoros');
    return this.territorios;
  }

  private async leer(ruta: string): Promise<ElementoDeClasificacion[]> {
    try {
      const filas = await firstValueFrom(this.api.get<ElementoDeClasificacion[]>(ruta, {}));
      return filas.map(f => ({ id: f.id, nombre: f.nombre, slug: f.slug }));
    } catch {
      // SE DEVUELVE VACIO Y NO SE LANZA: la clasificación es opcional, y un catálogo que no
      // responde no puede impedir guardar un evento. El formulario lo dice y sigue.
      // EL FALLO NO SE RECUERDA: se limpia la promesa para que el siguiente intento vuelva a pedir.
      this.olvidar(ruta);
      return [];
    }
  }

  private olvidar(ruta: string): void {
    if (ruta.includes('practicas-musicales')) { this.practicas = null; } else { this.territorios = null; }
  }
}
