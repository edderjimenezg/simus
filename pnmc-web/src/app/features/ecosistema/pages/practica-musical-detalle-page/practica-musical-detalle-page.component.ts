import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { PracticaMusicalConceptual, PracticasMusicalesService } from '../../../../core/services/practicas-musicales.service';
@Component({selector:'app-practica-musical-detalle-page',standalone:true,imports:[RouterLink,CompactHeroComponent],templateUrl:'./practica-musical-detalle-page.component.html'})
export class PracticaMusicalDetallePageComponent implements OnInit { private readonly ruta=inject(ActivatedRoute); private readonly servicio=inject(PracticasMusicalesService); private readonly router=inject(Router); readonly ficha=signal<PracticaMusicalConceptual|null>(null); ngOnInit():void {this.servicio.consultar(this.ruta.snapshot.paramMap.get('slug')||'').subscribe({next:f=>this.ficha.set(f)});} volver():void {this.router.navigateByUrl('/ecosistema/practicas-musicales');}}
