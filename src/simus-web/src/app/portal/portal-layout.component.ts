import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NAVEGACION_PORTAL } from './portal-nav';

@Component({
  selector: 'app-portal-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './portal-layout.component.html',
  styleUrl: './portal-layout.component.scss'
})
export class PortalLayoutComponent {
  protected readonly navegacion = NAVEGACION_PORTAL;
  protected readonly menuAbierto = signal(false);

  protected alternarMenu(): void {
    this.menuAbierto.update(abierto => !abierto);
  }

  protected cerrarMenu(): void {
    this.menuAbierto.set(false);
  }
}
