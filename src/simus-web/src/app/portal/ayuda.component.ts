import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PaginaInformativaComponent } from './pagina-informativa.component';

@Component({
  selector: 'app-ayuda',
  imports: [PaginaInformativaComponent, RouterLink],
  templateUrl: './ayuda.component.html'
})
export class AyudaComponent {}
