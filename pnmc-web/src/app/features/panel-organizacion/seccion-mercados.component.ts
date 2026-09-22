import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ETIQUETAS_ESTADO_MERCADO } from '../../core/services/mercados.service';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';
import { FalloDelServidor, MercadoDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

/**
 * Los mercados musicales de la organización, en `/gestion/procesos/mercados`.
 *
 * <b>ES LA HERMANA DE `/gestion/procesos/festivales`, Y NO EXISTÍA.</b> El enrutador registraba
 * `procesos/mercados/nuevo` y `procesos/mercados/:id`, pero no la lista: quien entraba a la
 * dirección del proceso —o pulsaba la miga «Mercados musicales» desde una ficha— no llegaba a
 * ningún sitio. Un proceso del Ecosistema se recorre igual que Festivales, que es la base de
 * funcionamiento, y eso empieza por tener las mismas direcciones.
 *
 * <b>LAS MISMAS TRES CIFRAS QUE FESTIVALES</b> —registros, pendientes y borradores—, porque
 * responden a las mismas tres preguntas: cuántos administro, cuántos esperan al Programa y cuántos
 * no he enviado todavía.
 */
@Component({
  selector: 'app-seccion-mercados',
  standalone: true,
  imports: [RouterLink, DatePipe, NombrePropioPipe],
  templateUrl: './seccion-mercados.component.html',
})
export class SeccionMercadosComponent implements OnInit {
  private readonly api = inject(PanelOrganizacionApi);
  private readonly store = inject(PanelOrganizacionStore);

  readonly organizacionId = this.store.organizacionId;
  readonly mercados = signal<MercadoDeLaOrganizacion[]>([]);
  readonly cargando = signal(false);
  readonly error = signal('');

  readonly pendientes = computed(() => this.mercados().filter(mercado =>
    mercado.estadoRegistro === 'en_revision' || mercado.estadoRegistro === 'ajustes_solicitados').length);
  readonly borradores = computed(() => this.mercados().filter(mercado =>
    mercado.estadoRegistro === 'borrador').length);

  ngOnInit(): void {
    this.cargar();
  }

  cargar(organizacionId = this.organizacionId()): void {
    if (!organizacionId) return;
    this.mercados.set([]);
    this.error.set('');
    this.cargando.set(true);
    this.api.obtenerMercados(organizacionId).subscribe({
      next: mercados => {
        this.cargando.set(false);
        this.mercados.set(mercados);
      },
      error: (fallo: FalloDelServidor) => {
        this.cargando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible consultar los mercados de la organización');
      },
    });
  }

  etiquetaEstado(estado: string | null | undefined): string {
    return ETIQUETAS_ESTADO_MERCADO[(estado ?? '').toLowerCase()] ?? 'Sin estado';
  }

  /** Dónde se realiza: el mismo criterio que la ficha y que la lista de procesos. */
  territorio(mercado: MercadoDeLaOrganizacion): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por definir';
  }
}
