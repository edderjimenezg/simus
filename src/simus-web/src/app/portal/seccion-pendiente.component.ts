import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

@Component({
  selector: 'app-seccion-pendiente',
  imports: [RouterLink],
  templateUrl: './seccion-pendiente.component.html',
  styleUrl: './seccion-pendiente.component.scss'
})
export class SeccionPendienteComponent {
  private readonly ruta = inject(ActivatedRoute);

  protected readonly titulo = toSignal(this.ruta.data.pipe(map(datos => (datos['titulo'] as string | undefined) ?? 'Sección en construcción')), { initialValue: 'Sección en construcción' });
  protected readonly motivo = toSignal(this.ruta.data.pipe(map(datos => datos['motivo'] as string | undefined)), { initialValue: undefined });
}
