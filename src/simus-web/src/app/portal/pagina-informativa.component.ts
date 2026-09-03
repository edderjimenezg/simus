import { Component, input } from '@angular/core';

@Component({
  selector: 'app-pagina-informativa',
  imports: [],
  templateUrl: './pagina-informativa.component.html',
  styleUrl: './pagina-informativa.component.scss'
})
export class PaginaInformativaComponent {
  readonly rotulo = input<string>('');
  readonly titulo = input.required<string>();
  readonly introduccion = input<string>('');
}
