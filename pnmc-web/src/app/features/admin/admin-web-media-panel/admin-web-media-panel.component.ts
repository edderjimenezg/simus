import { CommonModule } from '@angular/common';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import {
  LucideAlertCircle,
  LucideCheckCircle2,
  LucideExternalLink,
  LucideUpload,
} from '@lucide/angular';
import {
  AdminImage,
  AdminImageGroup,
  AdminImageHistoryEntry,
  ContenidoWebApiService,
} from '../../../core/services/contenido-web-api.service';
import { WEB_IMAGE_GROUPS } from '../../../core/cms/registro-de-imagenes-web';
import { construirMiniatura, etiquetaDeEstado, pesoLegible } from './miniatura';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';

/**
 * Administración de las imágenes del sitio público.
 *
 * <b>POR QUÉ ES UN PANEL APARTE Y NO UNA PESTAÑA MÁS DEL DE TEXTOS.</b> El de
 * textos se organiza por grupos de campos y su unidad de trabajo es «guardar el
 * grupo entero»; aquí la unidad es una ranura sola, y las acciones son distintas
 * —subir un archivo, verlo, desplegarlo, retirarlo—. Meterlas en el mismo
 * formulario obligaba a que «Guardar borrador» significara dos cosas según qué
 * hubiera debajo. La administración de textos se queda exactamente como estaba.
 *
 * <b>LA FORMA DE LA PANTALLA</b> es una cuadrícula de tarjetas y no una tabla,
 * porque lo que la persona necesita ver de un vistazo es la imagen. Cada tarjeta
 * enseña LO QUE HAY EN EL SITIO y, cuando difieren, LO QUE ESTÁ SIN PUBLICAR, una
 * al lado de la otra: la comparación es la decisión, y ponerlas en pantallas
 * distintas obliga a recordar.
 */
@Component({
  selector: 'app-admin-web-media-panel',
  standalone: true,
  imports: [
    EstadoDeListaComponent,
    CommonModule,
    SelectorSegmentadoComponent,
    BotonComponent,
    LucideUpload,
    LucideCheckCircle2,
    LucideAlertCircle,
    LucideExternalLink,
      IndicadorDeEstadoComponent],
  templateUrl: './admin-web-media-panel.component.html',
})
export class AdminWebMediaPanelComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(ContenidoWebApiService);

  /** Falso para quien no tiene rol de edición: la pantalla se pinta en solo lectura. */
  readonly enabled = input(true);

  /**
   * Si la sesión puede PUBLICAR. Es más estrecho que editar, igual que en los
   * textos. Que el botón no se pinte no es la guarda —el servidor responde 403
   * igual—, es la cortesía de no ofrecer algo que va a fallar.
   */
  readonly puedePublicar = input(false);

  /** Los cinco grupos del catálogo, en el orden del registro. */
  readonly grupos = WEB_IMAGE_GROUPS.map(g => ({ id: g.id, label: g.label, section: g.section }));
  /** Los grupos como posiciones del selector segmentado: eran píldoras. */
  readonly gruposComoOpciones: readonly OpcionSegmentada[] = this.grupos.map(g => ({ id: g.id, etiqueta: g.label }));

  readonly grupoElegido = signal(this.grupos[0]?.id ?? '');
  readonly datosDelGrupo = signal<AdminImageGroup | null>(null);
  readonly cargando = signal(false);
  readonly aviso = signal<{ tipo: 'ok' | 'error' | 'info'; texto: string } | null>(null);
  readonly claveOcupada = signal<string | null>(null);

  /** Historial abierto, si hay alguno. Se pide bajo demanda: son otra petición. */
  readonly historialDe = signal<string | null>(null);
  readonly historial = signal<AdminImageHistoryEntry[]>([]);

  readonly imagenes = computed(() => this.datosDelGrupo()?.images ?? []);
  readonly topes = computed(() => this.datosDelGrupo()?.limits ?? null);

  readonly tiposAceptados = computed(() => (this.topes()?.allowedTypes ?? []).join(','));

  constructor() {
    effect(() => {
      const grupo = this.grupoElegido();
      if (!grupo || !this.enabled()) { return; }
      void this.cargarGrupo(grupo);
    });
  }

  // --- Utilidades de plantilla -------------------------------------------------

  peso = pesoLegible;
  estado = etiquetaDeEstado;

  claseDeEstado(estado: string): string {
    switch (estado) {
      case 'publicado': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'retirado': return 'bg-amber-50 text-amber-800 border-amber-200';
      default: return 'bg-slate-100 text-slate-500 border-slate-200';
    }
  }

  /**
   * ¿Hay un borrador distinto de lo publicado?
   *
   * Se compara la HUELLA y no los bytes ni la fecha: es lo único que responde
   * «¿es el mismo archivo?» sin traérselo. Con una fecha, republicar el mismo
   * archivo marcaría un cambio que no existe.
   */
  hayBorradorSinPublicar(imagen: AdminImage): boolean {
    return imagen.draft !== null && imagen.draft.hash !== imagen.published?.hash;
  }

  urlDeVista(imagen: AdminImage, estado: 'borrador' | 'publicado', miniatura = true): string {
    return this.api.imagePreviewUrl(imagen.key, estado, imagen.version, miniatura);
  }

  // --- Carga -------------------------------------------------------------------

  async cargarGrupo(groupId: string): Promise<void> {
    this.cargando.set(true);
    this.historialDe.set(null);
    const resultado = await this.api.getImageGroup(groupId);
    this.cargando.set(false);

    if (!resultado.ok || !resultado.data) {
      this.datosDelGrupo.set(null);
      this.mostrar('error', resultado.error ?? 'No fue posible cargar el grupo de imágenes.');
      return;
    }
    this.datosDelGrupo.set(resultado.data);
  }

  elegirGrupo(groupId: string): void {
    this.grupoElegido.set(groupId);
  }

  // --- Acciones ----------------------------------------------------------------

  /**
   * Sube el archivo que la persona acaba de elegir.
   *
   * NO PUBLICA. Subir deja la imagen en la mesa del editor y el sitio sigue
   * mostrando lo de antes; salir al sitio es un segundo acto con su propio rol.
   * Es el mismo reparto que en los textos y por el mismo motivo: publicar sin
   * mirar es lo que produce los errores que después hay que retirar a la carrera.
   */
  async subir(imagen: AdminImage, evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    // El input se vacía siempre, incluso al fallar: sin esto, elegir DOS VECES el
    // mismo archivo no dispara `change` la segunda y el editor cree que la
    // pantalla se colgó.
    entrada.value = '';
    if (!archivo) { return; }

    const topes = this.topes();
    if (topes && archivo.size > topes.maxBytes) {
      // Se avisa ANTES de subir. El servidor lo rechazaría igual, pero después de
      // que la persona haya esperado a que suban dos megas por una conexión lenta.
      this.mostrar('error',
        `«${archivo.name}» pesa ${pesoLegible(archivo.size)} y el máximo es ${pesoLegible(topes.maxBytes)}.`);
      return;
    }

    this.claveOcupada.set(imagen.key);
    const miniatura = await construirMiniatura(archivo);
    const resultado = await this.api.uploadImage(imagen.key, archivo, imagen.version, {
      miniatura: miniatura ?? undefined,
    });
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible subir la imagen.');
      return;
    }

    if (resultado.data && !resultado.data.changed) {
      this.mostrar('info', 'Ese archivo ya estaba guardado en esta ranura: no se cambió nada.');
      return;
    }

    this.mostrar('ok', `«${imagen.label}» quedó guardada. Todavía no está en el sitio.`);
    await this.cargarGrupo(this.grupoElegido());
  }

  /** «Desplegar el borrador»: es lo que saca la imagen al sitio. */
  async desplegar(imagen: AdminImage): Promise<void> {
    this.claveOcupada.set(imagen.key);
    const resultado = imagen.state === 'retirado'
      // Una ranura retirada no vuelve con «publicar»: eso es lo que hace que
      // retirar signifique algo. La acción explícita es republicar.
      ? await this.api.republishImage(imagen.key)
      : await this.api.publishImage(imagen.key, imagen.version);
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible publicar la imagen.');
      return;
    }
    this.mostrar('ok', `«${imagen.label}» ya está en el sitio.`);
    await this.cargarGrupo(this.grupoElegido());
  }

  /**
   * Retira la imagen del sitio.
   *
   * NO DEJA UN HUECO: el sitio vuelve a la imagen compilada, la que tenía antes
   * de que esto existiera. Y el borrador se conserva, así que volver a ponerla es
   * un clic. Se dice en el aviso porque «retirar» suena a borrar.
   */
  async retirar(imagen: AdminImage): Promise<void> {
    this.claveOcupada.set(imagen.key);
    const resultado = await this.api.retireImage(imagen.key);
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible retirar la imagen.');
      return;
    }
    this.mostrar('ok', `«${imagen.label}» salió del sitio. El sitio vuelve a mostrar la imagen original y su archivo queda guardado aquí.`);
    await this.cargarGrupo(this.grupoElegido());
  }

  async verHistorial(imagen: AdminImage): Promise<void> {
    if (this.historialDe() === imagen.key) {
      this.historialDe.set(null);
      return;
    }
    const resultado = await this.api.getImageHistory(imagen.key);
    if (!resultado.ok || !resultado.data) {
      this.mostrar('error', resultado.error ?? 'No fue posible leer el historial.');
      return;
    }
    this.historial.set(resultado.data.entries);
    this.historialDe.set(imagen.key);
  }

  private mostrar(tipo: 'ok' | 'error' | 'info', texto: string): void {
    this.aviso.set({ tipo, texto });
    setTimeout(() => this.aviso.set(null), 6000);
  }
}
