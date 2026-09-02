import { Component, inject, signal } from '@angular/core';
import { ApiHealth, ApiStatusService } from './core/api-status.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly apiStatus = inject(ApiStatusService);

  protected readonly health = signal<ApiHealth | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(false);
    this.apiStatus.getHealth().subscribe({
      next: health => {
        this.health.set(health);
        this.loading.set(false);
      },
      error: () => {
        this.health.set(null);
        this.loading.set(false);
        this.error.set(true);
      }
    });
  }
}
