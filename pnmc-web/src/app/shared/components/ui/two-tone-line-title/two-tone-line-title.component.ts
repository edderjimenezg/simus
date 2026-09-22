import { 
  Component, 
  Input, 
  ElementRef, 
  ViewChild, 
  AfterViewInit, 
  OnDestroy, 
  signal, 
  computed, 
  effect 
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-two-tone-line-title',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div #container class="relative w-full">
      <h1 [class]="className">
        @for (lineText of lines(); track $index) {
          <span class="block" [class]="getLineClass($index)">
            {{ lineText }}
          </span>
        }
      </h1>

      <div aria-hidden="true" class="invisible pointer-events-none absolute inset-0 -z-10" [class]="className">
        @for (word of words(); track $index) {
          <span data-line-word="true">{{ word }}{{ $index < words().length - 1 ? ' ' : '' }}</span>
        }
      </div>
    </div>
  `,
  styles: []
})
export class TwoToneLineTitleComponent implements AfterViewInit, OnDestroy {
  @Input() text = '';
  @Input() className = '';

  @ViewChild('container') containerRef!: ElementRef<HTMLDivElement>;

  // Convertimos en computed para que sea reactivo
  words = computed(() => {
    return String(this.text || '').trim().split(/\s+/).filter(Boolean);
  });

  lines = signal<string[]>([]);
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    // Efecto reactivo ante cambios en el input 'text'
    effect(() => {
      if (this.text) {
        setTimeout(() => this.buildLines(), 10);
      }
    });
  }

  ngAfterViewInit() {
    if (typeof window !== 'undefined' && typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => {
        this.buildLines();
      });
      this.resizeObserver.observe(this.containerRef.nativeElement);
    }
    setTimeout(() => this.buildLines(), 50);

    // Escuchar cuando las fuentes estén completamente cargadas en el navegador
    if (typeof document !== 'undefined' && (document as any).fonts?.ready) {
      (document as any).fonts.ready.then(() => {
        this.buildLines();
      });
    }
  }

  ngOnDestroy() {
    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
    }
  }

  buildLines() {
    const container = this.containerRef?.nativeElement;
    if (!container) return;

    const wordNodes = Array.from(container.querySelectorAll('[data-line-word="true"]')) as HTMLElement[];
    if (wordNodes.length === 0) {
      this.lines.set([this.text]);
      return;
    }

    const grouped: string[][] = [];
    let currentTop: number | null = null;

    wordNodes.forEach((node) => {
      const nodeTop = node.offsetTop;
      const nodeText = (node.textContent || '').trim();

      if (!nodeText) return;

      if (currentTop === null || Math.abs(nodeTop - currentTop) > 1) {
        grouped.push([nodeText]);
        currentTop = nodeTop;
        return;
      }

      grouped[grouped.length - 1].push(nodeText);
    });

    const normalizedLines = grouped.map((lineWords) => lineWords.join(' ')).filter(Boolean);
    this.lines.set(normalizedLines.length > 0 ? normalizedLines : [this.text]);
  }

  getLineClass(index: number): string {
    const whiteLineCount = Math.ceil(this.lines().length / 2);
    return index < whiteLineCount ? 'text-white' : 'text-[#00DA5E]';
  }
}
