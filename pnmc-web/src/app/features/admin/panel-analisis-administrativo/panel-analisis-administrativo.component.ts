import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { CommonModule, formatDate } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { LucideRefreshCw } from '@lucide/angular';
import { CifraDeTablero, FranjaDeCifrasComponent } from '../../../shared/components/ui/franja-de-cifras/franja-de-cifras.component';
import { ConsultaGuiadaComponent } from '../../../shared/components/consulta-guiada/consulta-guiada.component';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import {
  AnalisisAdministrativoService,
  TableroAnalisisAdministrativo,
} from './analisis-administrativo.service';

/**
 * «Análisis y consultas»: el panorama administrativo, con la Consulta Guiada al lado.
 *
 * <b>ESTE COMPONENTE YA SOLO SE OCUPA DEL TABLERO.</b> El asistente era la mitad de este fichero y
 * de su plantilla; desde vive en `shared/components/consulta-guiada`
 * porque es una capacidad transversal —la misma pregunta se hace desde el espacio de una
 * organización— y una capacidad que solo existe dentro de una pantalla no lo es.
 */
@Component({
  selector: 'app-panel-analisis-administrativo',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, ConsultaGuiadaComponent, BotonComponent, EstadoDeListaComponent, FranjaDeCifrasComponent, LucideRefreshCw],
  templateUrl: './panel-analisis-administrativo.component.html',
})
export class PanelAnalisisAdministrativoComponent implements OnInit {
  private readonly analisis = inject(AnalisisAdministrativoService);

  readonly tablero = signal<TableroAnalisisAdministrativo | null>(null);
  readonly cargandoTablero = signal(true);
  readonly errorTablero = signal('');

  /** Los indicadores como cifras de la franja: la cifra, lo que cuenta y su alcance. */
  readonly cifras = computed<CifraDeTablero[]>(() =>
    (this.tablero()?.indicadores ?? []).map(indicador => ({
      id: indicador.id, cifra: indicador.total, rotulo: indicador.rotulo, detalle: indicador.alcance,
    })));

  ngOnInit(): void {
    this.cargarTablero();
  }

  cargarTablero(): void {
    this.cargandoTablero.set(true);
    this.errorTablero.set('');
    this.analisis.tablero().subscribe({
      next: tablero => {
        this.tablero.set(tablero);
        this.cargandoTablero.set(false);
      },
      error: error => {
        this.errorTablero.set(this.mensajeDeError(error, 'No fue posible construir el tablero administrativo.'));
        this.cargandoTablero.set(false);
      },
    });
  }

  fecha(valor: string | null): string {
    if (!valor) return '';
    const fecha = new Date(valor);
    return Number.isNaN(fecha.getTime())
      ? ''
      : formatDate(fecha, FECHA_Y_HORA_ADMINISTRATIVA, 'es-CO');
  }

  private mensajeDeError(error: unknown, respaldo: string): string {
    const mensaje = (error as { message?: unknown })?.message;
    return typeof mensaje === 'string' && mensaje.trim().length > 0 ? mensaje : respaldo;
  }
}
