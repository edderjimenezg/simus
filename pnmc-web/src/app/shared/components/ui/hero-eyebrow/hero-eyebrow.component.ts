import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-hero-eyebrow',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './hero-eyebrow.component.html',
  /*
    ANFITRION EN BLOQUE. Un componente sin `display` declarado se queda en `display: inline`, y los
    márgenes verticales no se aplican a una caja en línea: dentro de una pila con `space-y-*` la
    separación que la pila cree estar poniendo vale 0 px. Medido en el
    Resumen de la organización, donde el aviso de información pendiente quedaba pegado al bloque
    siguiente en los cinco anchos probados.
  */
  styles: [':host { display: block; }'],
})
export class HeroEyebrowComponent {
  @Input() text = '';
}
