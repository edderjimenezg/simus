import { Component, inject, signal } from '@angular/core';
import { EstadoApi, ServicioEstadoApi } from './core/estado-api.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly servicioEstadoApi = inject(ServicioEstadoApi);

  protected readonly estadoApi = signal<EstadoApi | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(false);
    this.servicioEstadoApi.obtenerSalud().subscribe({
      next: estado => {
        this.estadoApi.set(estado);
        this.loading.set(false);
      },
      error: () => {
        this.estadoApi.set(null);
        this.loading.set(false);
        this.error.set(true);
      }
    });
  }
}
