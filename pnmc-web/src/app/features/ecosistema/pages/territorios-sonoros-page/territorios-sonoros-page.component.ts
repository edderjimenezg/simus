import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { TerritorioSonoroConceptual, TerritoriosSonorosService } from '../../../../core/services/territorios-sonoros.service';
import { NombrePropioPipe } from '../../../../shared/texto/nombre-propio.pipe';

@Component({ selector: 'app-territorios-sonoros-page', standalone: true, imports: [NombrePropioPipe, RouterLink, CompactHeroComponent], templateUrl: './territorios-sonoros-page.component.html' })
export class TerritoriosSonorosPageComponent implements OnInit {
  private readonly servicio = inject(TerritoriosSonorosService); private readonly router = inject(Router);
  readonly territorios = signal<TerritorioSonoroConceptual[]>([]); readonly error = signal('');
  ngOnInit(): void { this.servicio.listar().subscribe({ next: datos => this.territorios.set(datos), error: e => this.error.set(e.message) }); }
  volver(): void { this.router.navigateByUrl('/ecosistema'); }
}
