import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-tag',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="text-[0.7rem] inline-block font-bold px-5 py-2 rounded-md uppercase font-alternate tracking-[0.12em]" [ngClass]="className">
      {{ text }}
    </span>
  `,
  styles: []
})
export class TagComponent {
  @Input() text = '';
  @Input() className = '';
}
