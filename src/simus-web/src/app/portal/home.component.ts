import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NAVEGACION_PORTAL } from './portal-nav';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  protected readonly secciones = NAVEGACION_PORTAL.filter(elemento => elemento.ruta !== '/');
}
