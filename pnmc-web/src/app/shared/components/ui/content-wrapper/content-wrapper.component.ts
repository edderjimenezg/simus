import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-content-wrapper',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section [id]="id" class="py-12 md:py-16 scroll-mt-24" [ngClass]="className">
      <div [ngClass]="fullBleed ? innerClassName : 'max-w-7xl mx-auto px-6 lg:px-8 ' + innerClassName">
        <ng-content></ng-content>
      </div>
    </section>
  `,
  styles: []
})
export class ContentWrapperComponent {
  @Input() className = '';
  @Input() id = '';
  @Input() fullBleed = false;
  @Input() innerClassName = '';
}
