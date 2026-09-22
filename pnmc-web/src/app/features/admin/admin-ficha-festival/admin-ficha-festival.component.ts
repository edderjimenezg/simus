import { CommonModule } from '@angular/common';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FormsModule } from '@angular/forms';
import { Component, EventEmitter, Input, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Output } from '@angular/core';
import { ApiClientService } from '../../../core/http/api-client.service';
import { ProcedenciaDeRegistro } from '../../../core/services/procedencia';
import { ElementoDeClasificacion } from '../../../core/services/clasificacion-de-contenido.service';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { DialogoDeFormularioComponent } from '../../../shared/components/ui/dialogo-de-formulario/dialogo-de-formulario.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { etiquetaDeEstado } from '../domain/admin-config';
import { etiquetaDePeriodicidad } from '../../../core/vocabularios/periodicidad';

/**
 * La ficha de un Festival en la consola, con sus Ediciones dentro.
 *
 * <b>EL FESTIVAL ES LA ENTIDAD PRINCIPAL Y LAS EDICIONES CUELGAN DE ÉL.</b> No son un módulo
 * equivalente: son el historial de realizaciones de ese Festival. Presentarlas al mismo nivel
 * sugiere que son cosas comparables y lleva a construir dos altas paralelas en vez de una anidada
 * en la otra.
 *
 * <b>LO QUE FALTABA.</b> La consola listaba Festivales y llevaba a la bandeja de revisión, pero no
 * había forma de abrir uno y ver qué es: sus datos permanentes, quién responde por él, de dónde
 * vino y qué ediciones tiene. Eso vivía solo en el panel de la organización.
 *
 * <b>NO DUPLICA EL CIRCUITO DE REVISIÓN.</b> Publicar o supervisar una Edición sigue donde estaba.
 * Esta es la vista que faltaba, no una segunda administración.
 */

export interface EdicionDelFestival {
  id: string;
  anio: number | null;
  numeroEdicion: number | null;
  nombre: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  estadoVisibilidad: string;
  procedencia: ProcedenciaDeRegistro | null;
}

export interface FichaFestivalAdministrativa {
  id: string;
  nombre: string;
  descripcion: string | null;
  estado: string;
  nivelCobertura: string;
  departamento: string | null;
  municipio: string | null;
  periodicidad: string | null;
  correoContacto: string | null;
  telefono: string | null;
  sitioWeb: string | null;
  organizacionResponsableId: number | null;
  organizacionResponsableNombre: string | null;
  procedencia: ProcedenciaDeRegistro | null;
  practicasMusicales: ElementoDeClasificacion[];
  territoriosSonoros: ElementoDeClasificacion[];
  ediciones: EdicionDelFestival[];
  fechaCreacion: string;
  fechaActualizacion: string | null;
}

/** Cómo se lee cada estado de visibilidad de una Edición. */
const ETIQUETAS_DE_EDICION: Record<string, string> = {
  borrador: 'Borrador',
  en_revision: 'En revisión',
  publicada: 'Publicada',
  retirada: 'Retirada',
};

@Component({
  selector: 'app-admin-ficha-festival',
  standalone: true,
  imports: [DialogoDirective, BotonComponent, DatoEnLecturaComponent, EstadoDeListaComponent, PanelLateralComponent, NombrePropioPipe, CommonModule, FormsModule, SelloDeProcedenciaComponent, IndicadorDeEstadoComponent,
    DialogoDePrevisualizacionComponent, HistorialDeRegistroComponent, DialogoDeFormularioComponent],
  templateUrl: './admin-ficha-festival.component.html',
})
export class AdminFichaFestivalComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(ApiClientService);

  /**
   * Quién ha tocado este Festival, con la misma pieza que Noticias, Agenda y Catálogo.
   *
   * <b>ANTES NO SE PODIA SABER.</b> `TABLA_DE_AUDITORIA` ya declaraba `festivales: 'Festivales'`
   * y la bitácora venía registrando cada actuación, pero ninguna pantalla la abría: el dato
   * existía y era inalcanzable. Es justo la pregunta que sostiene la procedencia —quién lo
   * incorporó, quién lo cambió y cuándo— sobre la entidad principal del Ecosistema.
   */
  readonly historialAbierto = signal(false);

  /**
   * El estado con su nombre —«En revisión»— y no su código —«en_revision»—, que es lo que se leía
   * antes. Usa el ayudante compartido, que además humaniza cualquier código que no esté en la tabla.
   */
  readonly etiquetaDeEstado = etiquetaDeEstado;

  nombresDe(elementos: readonly { nombre: string }[]): string {
    return elementos.map(e => e.nombre).join(', ');
  }
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.festivales;

  @Output() cerrar = new EventEmitter<void>();

  /**
   * Si quien mira puede publicar.
   *
   * <b>SOLO EL WEBMASTER.</b> Lo decidió la dirección: un Festival que el propio Programa incorporó
   * no tiene a quién esperar, y obligarlo a recorrer una bandeja donde el revisor y el registrador
   * son la misma persona convierte el circuito en un trámite vacío. El servidor lo comprueba
   * igual; esto solo evita ofrecer un botón que va a devolver un 403.
   */
  @Input() puedePublicar = false;
  /** Pide abrir la ficha de la organización responsable, que vive en otra sección. */
  @Output() verOrganizacion = new EventEmitter<number>();

  /** Si se está mirando cómo quedará este Festival en el listado público. */
  readonly previsualizando = signal(false);

  readonly ficha = signal<FichaFestivalAdministrativa | null>(null);
  /** La periodicidad se lee por su nombre, no por su código: «otra_regular» no es una palabra. */
  readonly etiquetaDePeriodicidad = etiquetaDePeriodicidad;

  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);
  readonly etiquetasDeEdicion = ETIQUETAS_DE_EDICION;

  /* El codigo nunca sale a pantalla: el respaldo era `|| codigo`. */
  rotuloDeEdicion(codigo: string | null | undefined): string {
    return this.etiquetasDeEdicion[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }
  readonly publicando = signal(false);
  readonly avisoDePublicacion = signal<string | null>(null);

  /** El formulario de liberación, abierto o no. El motivo es obligatorio y por eso vive aquí. */
  readonly liberando = signal<{ motivo: string } | null>(null);

  @Input({ required: true }) set festivalId(id: string) { void this.cargar(id); }

  pedirLiberacion(): void {
    this.avisoDePublicacion.set(null);
    this.error.set(null);
    this.liberando.set({ motivo: '' });
  }

  cancelarLiberacion(): void { this.liberando.set(null); }

  motivoDeLiberacion(valor: string): void { this.liberando.set({ motivo: valor }); }

  /**
   * Devuelve el Festival a custodia del Programa para que otra organización pueda reclamarlo.
   *
   * <b>ES LA ALTERNATIVA A TOCAR CREDENCIALES.</b> Cuando nadie de la organización puede entrar, su
   * Festival se queda congelado: nadie lo edita y ninguna otra organización puede tomarlo, porque
   * el circuito de reclamación solo ofrece los que ya están en custodia. Esto no recupera el acceso
   * de nadie: desbloquea el proceso.
   */
  async liberar(): Promise<void> {
    const ficha = this.ficha();
    const formulario = this.liberando();
    if (!ficha || !formulario || !formulario.motivo.trim()) { return; }

    this.publicando.set(true);
    this.error.set(null);

    try {
      const respuesta = await firstValueFrom(this.api.post<{ reclamable: boolean }>(
        `/api/v1/admin/festivales/${ficha.id}/liberar-administracion`,
        { motivo: formulario.motivo.trim() },
        {}));
      this.liberando.set(null);
      this.avisoDePublicacion.set(respuesta?.reclamable
        ? 'El Festival volvió al Programa y ya puede ser reclamado por otra organización.'
        : 'El Festival volvió al Programa. Podrá ser reclamado cuando esté publicado.');
      await this.cargar(ficha.id);
    } catch (fallo: unknown) {
      // EL MOTIVO DEL SERVIDOR ENTERO. Aquí hay tres razones distintas por las que puede negarse
      // —ya está en custodia, falta la entidad institucional del Programa, no hay permiso— y un
      // «no fue posible» genérico deja a quien administra sin saber cuál de las tres arreglar.
      this.error.set(this.motivoDelServidor(fallo) ?? 'No fue posible devolver el Festival al Programa.');
    } finally {
      this.publicando.set(false);
    }
  }

  /**
   * El mensaje que explica por qué el servidor dijo que no.
   *
   * SE MIRAN LAS DOS FORMAS en que el API responde un fallo: los errores por campo —una lista de
   * textos bajo el nombre del campo— y el `detail` de un problema HTTP, que es donde viaja el
   * aviso de que falta la entidad institucional.
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

  /**
   * Publica, archiva o devuelve a borrador el Festival.
   *
   * TRES ESTADOS Y NO LOS SIETE: los demás —en revisión, ajustes solicitados, aprobado,
   * rechazado— pertenecen al circuito de revisión del canal externo, y escribirlos desde aquí
   * dejaría la bandeja diciendo cosas que nadie decidió en ella.
   */
  async cambiarPublicacion(estado: 'publicado' | 'archivado' | 'borrador'): Promise<void> {
    const ficha = this.ficha();
    if (!ficha) { return; }

    this.publicando.set(true);
    this.error.set(null);
    this.avisoDePublicacion.set(null);

    try {
      await firstValueFrom(this.api.post<{ estado: string }>(
        `/api/v1/admin/festivales/${ficha.id}/publicacion`, { estado }, {}));
      this.avisoDePublicacion.set(
        estado === 'publicado' ? 'El Festival quedó publicado en el ecosistema.'
          : estado === 'archivado' ? 'El Festival quedó archivado.'
            : 'El Festival volvió a borrador y ya no se ve en el portal.');
      await this.cargar(ficha.id);
    } catch (fallo: unknown) {
      this.error.set(this.motivoDelServidor(fallo) ?? 'No fue posible cambiar el estado del Festival.');
    } finally {
      this.publicando.set(false);
    }
  }

  private async cargar(id: string): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    try {
      this.ficha.set(await firstValueFrom(
        this.api.get<FichaFestivalAdministrativa>(`/api/v1/admin/festivales/${id}`, {})));
    } catch {
      this.ficha.set(null);
      this.error.set('No fue posible abrir la ficha del Festival.');
    } finally {
      this.cargando.set(false);
    }
  }
}
