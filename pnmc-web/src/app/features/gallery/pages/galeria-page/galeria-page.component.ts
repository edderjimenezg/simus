import { Component } from '@angular/core';

/**
 * Estructura pública de Galería, sin álbumes heredados ni archivos simulados.
 * Solo incorporará exploración y descargas cuando cuente con una biblioteca
 * multimedia, permisos de uso y flujo de publicación verificables.
 */
@Component({
  selector: 'app-galeria-page',
  standalone: true,
  templateUrl: './galeria-page.component.html',
})
export class GaleriaPageComponent {}
