import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DialogoDirective } from '../../shared/directives/dialogo.directive';
import { CommonModule } from '@angular/common';
import { DialogoDeFormularioComponent } from '../../shared/components/ui/dialogo-de-formulario/dialogo-de-formulario.component';
import { IndicadorDeEstadoComponent } from '../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AnunciarEventoSolicitud,
  EventoDeLaOrganizacion,
  ProcesoQueEnmarca,
} from './panel-organizacion.api';
import { EventosDeOrganizacionService } from './eventos-de-organizacion.service';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { CatalogService, TerritorioConCodigo } from '../../core/services/catalog.service';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';

/**
 * Los eventos que la organización anuncia sobre sus propios procesos.
 *
 * <b>NINGÚN EVENTO SIN PROCESO, Y ESA ES LA PANTALLA ENTERA.</b> Una organización no publica
 * eventos sueltos en la agenda del Programa: publica eventos <b>de sus procesos</b> —su festival,
 * su mercado—. Si no tiene ninguno publicado, esta pantalla <b>no ofrece el formulario</b> y
 * explica por qué, en vez de dejar escribir un evento entero para rechazarlo al guardar por algo
 * que se sabía desde el principio.
 *
 * <b>EL EVENTO QUEDA EN REVISIÓN.</b> Quien lo escribe no decide si se publica, y la pantalla lo
 * dice antes de enviar: prometer una publicación inmediata y luego no cumplirla es peor que
 * advertirlo.
 *
 * <b>HOY EL ÚNICO PROCESO ES EL FESTIVAL.</b> Los demás del ecosistema todavía no tienen su
 * modelo; el desplegable dice el tipo de cada proceso para que el día que existan la pantalla no
 * cambie.
 */

interface FormularioDeEvento {
  procesoId: string;
  titulo: string;
  descripcion: string;
  descripcionLarga: string;
  fechaInicio: string;
  fechaFin: string;
  conHoraInicio: boolean;
  horaInicio: string;
  conHoraFin: boolean;
  horaFin: string;
  modalidad: string;
  lugar: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  url: string;
}

const VACIO: FormularioDeEvento = {
  procesoId: '', titulo: '', descripcion: '', descripcionLarga: '',
  fechaInicio: '', fechaFin: '', conHoraInicio: false, horaInicio: '',
  conHoraFin: false, horaFin: '', modalidad: 'presencial', lugar: '',
  codigoDepartamento: '', codigoMunicipio: '', url: '',
};

@Component({
  selector: 'app-panel-eventos',
  standalone: true,
  imports: [DialogoDirective, NombrePropioPipe, CommonModule, FormsModule, RouterLink, IndicadorDeEstadoComponent, DialogoDeFormularioComponent],
  templateUrl: './panel-eventos.component.html',
})
export class PanelEventosComponent implements OnInit {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(EventosDeOrganizacionService);
  private readonly store = inject(PanelOrganizacionStore);
  private readonly catalogo = inject(CatalogService);

  readonly procesos = signal<ProcesoQueEnmarca[]>([]);
  readonly eventos = signal<EventoDeLaOrganizacion[]>([]);
  readonly territorios = signal<TerritorioConCodigo[]>([]);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly formulario = signal<FormularioDeEvento | null>(null);

  /** Sin procesos publicados no hay nada que enmarcar, y la pantalla lo dice. */
  readonly puedeAnunciar = computed(() => this.procesos().length > 0);

  readonly municipiosDelDepartamento = computed(() => {
    const codigo = this.formulario()?.codigoDepartamento ?? '';
    if (!codigo) { return []; }
    return this.territorios().find(t => t.codigo === codigo)?.municipios ?? [];
  });

  /** Qué le falta al formulario. Se dice mientras se escribe, no al pulsar el botón. */
  readonly loQueFalta = computed(() => {
    const f = this.formulario();
    if (!f) { return []; }
    const faltas: string[] = [];
    if (!f.procesoId) { faltas.push('el proceso en el que se enmarca'); }
    if (!f.titulo.trim()) { faltas.push('el título'); }
    if (!f.descripcion.trim()) { faltas.push('la descripción'); }
    if (!f.fechaInicio) { faltas.push('la fecha de inicio'); }
    if ((f.modalidad === 'presencial' || f.modalidad === 'mixta') && !f.lugar.trim()) { faltas.push('el lugar'); }
    if ((f.modalidad === 'virtual' || f.modalidad === 'mixta') && !f.url.trim()) { faltas.push('el enlace'); }
    return faltas;
  });

  /** Las fechas y horas que no tienen sentido, dichas al lado de los campos. */
  readonly problemasDeFecha = computed(() => {
    const f = this.formulario();
    if (!f) { return []; }
    const problemas: string[] = [];
    if (f.fechaInicio && f.fechaFin && f.fechaFin < f.fechaInicio) {
      problemas.push('El evento no puede terminar antes de empezar.');
    }
    const mismoDia = !f.fechaFin || f.fechaFin === f.fechaInicio;
    if (mismoDia && f.conHoraInicio && f.conHoraFin && f.horaInicio && f.horaFin && f.horaFin < f.horaInicio) {
      problemas.push('La hora de fin no puede ser anterior a la de inicio.');
    }
    return problemas;
  });

  async ngOnInit(): Promise<void> {
    this.catalogo.fetchDivipolaConCodigos().subscribe({
      next: territorios => this.territorios.set(territorios),
      error: () => this.territorios.set([]),
    });
    await this.cargar();
  }

  private organizacionId(): string | null {
    // EL IDENTIFICADOR DE LA ORGANIZACION ACTIVA lo mantiene el armazón del panel: quien tiene
    // varias elige una, y todas las pantallas leen la misma.
    return this.store.organizacionId() || null;
  }

  async cargar(): Promise<void> {
    const organizacion = this.organizacionId();
    if (!organizacion) { this.cargando.set(false); return; }

    this.cargando.set(true);
    this.error.set(null);
    try {
      const [procesos, eventos] = await Promise.all([
        firstValueFrom(this.api.procesosQueEnmarcan(organizacion)),
        firstValueFrom(this.api.eventosDeLaOrganizacion(organizacion)),
      ]);
      this.procesos.set(procesos ?? []);
      this.eventos.set(eventos ?? []);
    } catch {
      this.error.set('No fue posible consultar tus eventos.');
    } finally {
      this.cargando.set(false);
    }
  }

  nuevo(): void {
    this.aviso.set(null);
    this.error.set(null);
    // SI SOLO HAY UN PROCESO, YA VIENE ELEGIDO: obligar a elegir en una lista de uno es pedir una
    // decisión que no existe.
    const unico = this.procesos().length === 1 ? this.procesos()[0].id : '';
    this.formulario.set({ ...VACIO, procesoId: unico, fechaInicio: new Date().toISOString().slice(0, 10) });
  }

  cerrar(): void { this.formulario.set(null); }

  campo<K extends keyof FormularioDeEvento>(clave: K, valor: FormularioDeEvento[K]): void {
    const actual = this.formulario();
    if (!actual) { return; }
    this.formulario.set({ ...actual, [clave]: valor });
  }

  cambiarDepartamento(codigo: string): void {
    const actual = this.formulario();
    if (!actual) { return; }
    this.formulario.set({ ...actual, codigoDepartamento: codigo, codigoMunicipio: '' });
  }

  /**
   * Marca o desmarca la hora de inicio, y arrastra la de fin.
   *
   * SIN HORA DE INICIO NO HAY HORA DE FIN: un evento que acaba a una hora y no empieza a ninguna es
   * un dato que nadie puede leer. Es la misma regla que la consola.
   */
  cambiarHoraDeInicio(activada: boolean): void {
    const actual = this.formulario();
    if (!actual) { return; }
    this.formulario.set(activada
      ? { ...actual, conHoraInicio: true }
      : { ...actual, conHoraInicio: false, horaInicio: '', conHoraFin: false, horaFin: '' });
  }

  async anunciar(): Promise<void> {
    const f = this.formulario();
    const organizacion = this.organizacionId();
    if (!f || !organizacion) { return; }

    if (this.problemasDeFecha().length > 0) {
      this.error.set(this.problemasDeFecha().join(' '));
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    const solicitud: AnunciarEventoSolicitud = {
      titulo: f.titulo.trim(),
      descripcion: f.descripcion.trim(),
      descripcionLarga: f.descripcionLarga.trim() || null,
      fechaInicio: f.fechaInicio,
      fechaFin: f.fechaFin || null,
      // SI LA CASILLA ESTA SIN MARCAR, NO HAY HORA, aunque haya quedado un valor de antes.
      horaInicio: f.conHoraInicio ? (f.horaInicio || null) : null,
      horaFin: f.conHoraFin ? (f.horaFin || null) : null,
      modalidad: f.modalidad,
      lugar: f.lugar.trim() || null,
      codigoDepartamento: f.codigoDepartamento || null,
      codigoMunicipio: f.codigoMunicipio || null,
      url: f.url.trim() || null,
      festivalId: Number(f.procesoId),
    };

    try {
      await firstValueFrom(this.api.anunciarEvento(organizacion, solicitud));
      this.aviso.set('Tu evento quedó enviado. Aparecerá en la agenda cuando el Programa lo revise.');
      this.formulario.set(null);
      await this.cargar();
    } catch (fallo: unknown) {
      this.error.set(this.motivoDe(fallo));
    } finally {
      this.guardando.set(false);
    }
  }

  /**
   * El motivo que devolvió el servidor, y no uno genérico.
   *
   * <b>«REVISA LOS CAMPOS» SOBRE UNOS CAMPOS QUE ESTABAN BIEN.</b> Esto se tragaba la respuesta
   * entera: el servidor decía exactamente qué pasaba —un proceso que no es tuyo, una hora de fin
   * sin hora de inicio, un municipio sin departamento— y la pantalla lo cambiaba por una frase que
   * manda a buscar un error donde no lo hay. se detectó el 15 de septiembre
   * de 2026 sobre un evento que fallaba por una columna que este canal no escribía.
   *
   * El respaldo genérico se queda para lo que de verdad no trae motivo —una red caída—, y dice que
   * no se pudo enviar, no que haya que revisar nada.
   */
  private motivoDe(fallo: unknown): string {
    const cuerpo = (fallo as { payload?: { errors?: Record<string, string[]>; message?: string; detail?: string } })?.payload;
    const errores = cuerpo?.errors;
    if (errores) {
      const mensajes = Object.values(errores).flat().filter(Boolean);
      if (mensajes.length) return mensajes.join(' ');
    }
    return cuerpo?.message ?? 'No fue posible enviar el evento. Inténtalo de nuevo en un momento.';
  }
}
