import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClientService } from '../../../core/http/api-client.service';
import { ElementoDeClasificacion } from '../../../core/services/clasificacion-de-contenido.service';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { ETIQUETAS_ESTADO_MERCADO, ETIQUETAS_ESTADO_EDICION, ETIQUETAS_VISIBILIDAD_EDICION } from '../../../core/services/mercados.service';
import { ProcedenciaDeRegistro } from '../../../core/services/procedencia';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { etiquetaDePeriodicidad } from '../../../core/vocabularios/periodicidad';

/** Una realización del mercado, con lo justo para reconocerla en la ficha. */
export interface EdicionDelMercado {
  id: string;
  anio: number;
  numeroEdicion: number | null;
  nombre: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  /** El ciclo del acontecimiento: en preparación, programada, realizada, cancelada. */
  estado: string;
  /** Si el portal la enseña: borrador o publicado. */
  estadoVisibilidad: string;
  procedencia: ProcedenciaDeRegistro | null;
}

export interface FichaMercadoAdministrativa {
  id: string;
  nombre: string;
  descripcion: string | null;
  estado: string;
  nivelCobertura: string;
  departamento: string | null;
  municipio: string | null;
  lugarEspecifico: string | null;
  periodicidad: string | null;
  alcance: string | null;
  modalidad: string | null;
  correoContacto: string | null;
  telefono: string | null;
  sitioWeb: string | null;
  organizacionResponsableId: number | null;
  organizacionResponsableNombre: string | null;
  seRealizaEnElMarcoDeUnFestival: boolean;
  festivalId: number | null;
  festivalNombre: string | null;
  procedencia: ProcedenciaDeRegistro | null;
  practicasMusicales: ElementoDeClasificacion[];
  territoriosSonoros: ElementoDeClasificacion[];
  ediciones: EdicionDelMercado[];
  fechaCreacion: string;
  fechaActualizacion: string | null;
}

/**
 * La ficha de un mercado musical en la consola, con sus ediciones dentro.
 *
 * <b>ES LA PAREJA DE `AdminFichaFestivalComponent`, Y ANTES NO EXISTIA.</b> Un mercado solo se
 * abría en el cajón de edición de su panel: no había forma de verlo entero —qué es, quién responde
 * por él, de dónde vino, qué le ha pasado y qué realizaciones tiene— ni de publicarlo, retirarlo o
 * archivarlo desde su ficha, que es lo que sí puede hacerse con un festival. La dirección de producto
 * lo señaló: «la navegación, edición, publicación, etc. de un mercado
 * debe ser igual en cuanto sea posible a la de un festival, todo está muy diferente».
 *
 * <b>NO DUPLICA EL CIRCUITO DE REVISION.</b> Decidir sobre un mercado que la organización envió
 * sigue en la bandeja. Esta es la lectura que faltaba y las tres decisiones de publicación que solo
 * el Programa toma sobre lo suyo.
 */
@Component({
  selector: 'app-admin-ficha-mercado',
  standalone: true,
  imports: [
    CommonModule, FormsModule, NombrePropioPipe,
    PanelLateralComponent, BotonComponent, DatoEnLecturaComponent, EstadoDeListaComponent,
    IndicadorDeEstadoComponent, SelloDeProcedenciaComponent, HistorialDeRegistroComponent,
    DialogoDePrevisualizacionComponent,
  ],
  templateUrl: './admin-ficha-mercado.component.html',
})
export class AdminFichaMercadoComponent {
  private readonly api = inject(ApiClientService);

  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.mercados;

  @Input({ required: true }) set mercadoId(id: string) { void this.cargar(id); }

  /**
   * Si quien mira puede publicar.
   *
   * <b>SOLO EL WEBMASTER</b>, la misma regla que en Festivales: publicar es la decisión que saca un
   * registro al portal. El servidor lo comprueba igual; esto solo evita ofrecer un botón que va a
   * devolver un 403.
   */
  @Input() puedePublicar = false;

  @Output() cerrar = new EventEmitter<void>();
  /** Pide abrir la ficha de la organización responsable, que vive en otra sección. */
  @Output() verOrganizacion = new EventEmitter<number>();
  /** Avisa de que el estado cambió, para que la lista de detrás no siga diciendo el anterior. */
  @Output() cambio = new EventEmitter<void>();

  readonly ficha = signal<FichaMercadoAdministrativa | null>(null);
  /** La periodicidad se lee por su nombre, no por su código: «otra_regular» no es una palabra. */
  readonly etiquetaDePeriodicidad = etiquetaDePeriodicidad;

  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly publicando = signal(false);
  readonly avisoDePublicacion = signal<string | null>(null);
  readonly historialAbierto = signal(false);
  readonly previsualizando = signal(false);

  etiquetaDeEstado(codigo: string | null | undefined): string {
    return ETIQUETAS_ESTADO_MERCADO[(codigo ?? '').trim().toLowerCase()] ?? (codigo ?? '');
  }

  /** Si el portal la enseña. En femenino: el sujeto es la edición. */
  rotuloDeVisibilidad(codigo: string | null | undefined): string {
    return ETIQUETAS_VISIBILIDAD_EDICION[(codigo ?? '').trim().toLowerCase()] ?? (codigo ?? '');
  }

  /** Qué pasó con ella: el otro eje, que no se mezcla con el anterior. */
  rotuloDeRealizacion(codigo: string | null | undefined): string {
    return ETIQUETAS_ESTADO_EDICION[(codigo ?? '').trim().toLowerCase()] ?? (codigo ?? '');
  }

  nombresDe(elementos: readonly { nombre: string }[]): string {
    return elementos.map(e => e.nombre).join(', ');
  }

  nombreDeLaEdicion(edicion: EdicionDelMercado): string {
    return edicion.nombre || `Edición ${edicion.numeroEdicion ?? edicion.anio}`;
  }

  /**
   * Publica, archiva o devuelve a borrador el mercado.
   *
   * TRES ESTADOS Y NO LOS CINCO: «en revisión» y «ajustes solicitados» pertenecen al circuito de
   * revisión del canal externo, y escribirlos desde aquí dejaría la bandeja diciendo cosas que
   * nadie decidió en ella.
   */
  async cambiarPublicacion(estado: 'publicado' | 'archivado' | 'borrador'): Promise<void> {
    const ficha = this.ficha();
    if (!ficha) { return; }

    this.publicando.set(true);
    this.error.set(null);
    this.avisoDePublicacion.set(null);

    try {
      await firstValueFrom(this.api.post<{ estado: string }>(
        `/api/v1/admin/mercados/${ficha.id}/publicacion`, { estado }, {}));
      this.avisoDePublicacion.set(
        estado === 'publicado' ? 'El mercado quedó publicado en el ecosistema.'
          : estado === 'archivado' ? 'El mercado quedó archivado.'
            : 'El mercado volvió a borrador y ya no se ve en el portal.');
      await this.cargar(ficha.id);
      this.cambio.emit();
    } catch (fallo: unknown) {
      this.error.set(this.motivoDelServidor(fallo) ?? 'No fue posible cambiar el estado del mercado.');
    } finally {
      this.publicando.set(false);
    }
  }

  /**
   * El mensaje que explica por qué el servidor dijo que no.
   *
   * SE MIRAN LAS DOS FORMAS en que el API responde un fallo: los errores por campo y el `detail`
   * de un problema HTTP. Un «no fue posible» genérico deja sin saber qué arreglar.
   */
  private motivoDelServidor(fallo: unknown): string | null {
    const cuerpo = (fallo as { payload?: unknown })?.payload;
    if (!cuerpo || typeof cuerpo !== 'object') { return null; }

    const detalle = (cuerpo as { detail?: unknown }).detail;
    if (typeof detalle === 'string' && detalle.trim()) { return detalle.trim(); }

    for (const valor of Object.values(cuerpo as Record<string, unknown>)) {
      if (Array.isArray(valor)) {
        const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
        if (typeof mensaje === 'string') { return mensaje.trim(); }
      }
    }
    return null;
  }

  private async cargar(id: string): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    try {
      this.ficha.set(await firstValueFrom(
        this.api.get<FichaMercadoAdministrativa>(`/api/v1/admin/mercados/${id}`, {})));
    } catch {
      this.ficha.set(null);
      this.error.set('No fue posible abrir la ficha del mercado.');
    } finally {
      this.cargando.set(false);
    }
  }
}
