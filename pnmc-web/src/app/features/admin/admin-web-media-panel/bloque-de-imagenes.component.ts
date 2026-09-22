import { CommonModule } from '@angular/common';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { LucideHistory, LucideImage, LucideUpload } from '@lucide/angular';
import {
  AdminImage,
  AdminImageHistoryEntry,
  TopesDeLasImagenes,
  ContenidoWebApiService,
} from '../../../core/services/contenido-web-api.service';
import {
  DEFAULT_IMAGE_URLS,
  PRESENTACION_DE_LA_RANURA,
  PRESENTACION_NEUTRA,
  ranurasQueRotanCon,
  WEB_IMAGE_KEYS,
} from '../../../core/cms/registro-de-imagenes-web';
import { construirMiniatura, etiquetaDeEstado, pesoLegible, versionLigera } from './miniatura';
import { RecorteHecho, RecortadorComponent } from './recortador.component';

/** Lo que el bloque le cuenta al panel para que la previsualización lo pinte. */
export interface ImagenEnVivo {
  clave: string;
  /** URL utilizable dentro del marco. `null` significa «vuelve a lo publicado». */
  url: string | null;
  /**
   * Las claves que en el sitio rotan con esta. La portada del Home elige una de cuatro al
   * azar en cada visita (home.component.ts), así que sin esto la previsualización enseña
   * la que quiera y la editora no puede mirar la que está cambiando.
   */
  rotanConElla: string[];
  /**
   * Si el marco tiene que volver a cargar la página.
   *
   * Verdadero al publicar y al retirar: eso cambia el MANIFIESTO que sirve el servidor, y el
   * marco solo lo relee recargando. Falso al subir: el borrador viaja por mensaje y se ve al
   * instante, y recargar ahí tiraría la página entera para pintar lo mismo.
   */
  recargarElSitio: boolean;
}

/**
 * Las imágenes de un bloque del panel de textos, editadas donde está el texto que las acompaña.
 *
 * <b>POR QUÉ AQUÍ Y NO SOLO EN EL PANEL DE IMÁGENES.</b> El panel aparte sigue existiendo y es
 * el inventario de las dieciséis ranuras. Pero cambiar la portada del Home y cambiar su titular
 * son la misma decisión editorial, y tenerlas en dos pantallas obliga a recordar cómo era la
 * otra. Aquí van juntas y la previsualización de la derecha enseña las dos a la vez.
 *
 * <b>LO QUE NO HACE.</b> No publica por su cuenta: subir deja la imagen en la mesa del editor
 * y el sitio sigue mostrando lo de antes. Es el mismo reparto que en los textos.
 *
 * <b>REEMPLAZAR BORRA.</b> Desde <c>V20260829_03</c> el historial guarda quién, cuándo, qué
 * acción y las señas —tipo, peso, medidas, huella—, nunca los bytes. No hay «restaurar», y el
 * botón lo dice antes de abrir el selector de archivo.
 */
@Component({
  selector: 'app-bloque-de-imagenes',
  standalone: true,
  imports: [IndicadorDeEstadoComponent, CommonModule, LucideUpload, LucideHistory, LucideImage, RecortadorComponent],
  templateUrl: './bloque-de-imagenes.component.html',
})
export class BloqueDeImagenesComponent {
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(ContenidoWebApiService);

  /** Las claves de ranura que se editan en este bloque, en el orden del registro. */
  readonly claves = input.required<string[]>();
  readonly enabled = input(true);
  readonly puedePublicar = input(false);

  /** Sube cada vez que una imagen sin publicar cambia; lo escucha el panel. */
  readonly enVivo = output<ImagenEnVivo>();

  readonly cargando = signal(false);
  readonly aviso = signal<{ tipo: 'ok' | 'error' | 'info'; texto: string } | null>(null);
  readonly claveOcupada = signal<string | null>(null);
  readonly topes = signal<TopesDeLasImagenes | null>(null);

  /** Las ranuras traídas del servidor, por clave. */
  private readonly porClave = signal<Record<string, AdminImage>>({});

  /** El archivo que se está ajustando, si hay alguno. Solo uno a la vez. */
  readonly ajustando = signal<{ clave: string; archivo: File } | null>(null);

  /** URLs de objeto de los recortes ya subidos, para que el marco los pinte sin ir al servidor. */
  private readonly urlesLocales = new Map<string, string>();

  readonly historialDe = signal<string | null>(null);
  readonly historial = signal<AdminImageHistoryEntry[]>([]);

  peso = pesoLegible;
  estado = etiquetaDeEstado;

  /** Las ranuras de este bloque, en el orden que declara `claves`. */
  readonly ranuras = computed<AdminImage[]>(() => {
    const datos = this.porClave();
    return this.claves().map(clave => datos[clave]).filter((x): x is AdminImage => !!x);
  });

  readonly tiposAceptados = computed(() => (this.topes()?.allowedTypes ?? []).join(','));

  constructor() {
    // Los grupos se piden UNA VEZ por bloque abierto. Las cuatro portadas del Home viven en el
    // mismo grupo `home_media`, así que son cuatro tarjetas y una sola petición.
    effect(() => {
      const claves = this.claves();
      if (!this.enabled() || claves.length === 0) { return; }
      void this.cargar(claves);
    });
  }

  // --- Lectura -----------------------------------------------------------------

  private gruposDe(claves: string[]): string[] {
    const grupos = new Set<string>();
    for (const clave of claves) {
      const ranura = WEB_IMAGE_KEYS.find(x => x.key === clave);
      if (ranura) { grupos.add(ranura.groupId); }
    }
    return [...grupos];
  }

  private async cargar(claves: string[]): Promise<void> {
    this.cargando.set(true);
    const acumulado: Record<string, AdminImage> = {};
    let fallo: string | null = null;

    for (const grupo of this.gruposDe(claves)) {
      const resultado = await this.api.getImageGroup(grupo);
      if (!resultado.ok || !resultado.data) {
        fallo = resultado.error ?? 'No fue posible cargar las imágenes de este bloque.';
        continue;
      }
      this.topes.set(resultado.data.limits);
      for (const imagen of resultado.data.images) {
        if (claves.includes(imagen.key)) { acumulado[imagen.key] = imagen; }
      }
    }

    this.cargando.set(false);
    this.porClave.set(acumulado);

    // SE ANUNCIA LO QUE YA HABIA. Un borrador subido en otra sesión existe antes de que esta
    // pantalla se abra, y sin esto la previsualización pintaba lo publicado mientras la tarjeta
    // de al lado enseñaba otra imagen bajo el rótulo «sin publicar». Se anuncian también las
    // que NO tienen borrador, con `url: null`, para que el panel borre lo que quedara puesto.
    for (const clave of claves) { this.anunciar(clave, false, false); }

    if (fallo) { this.mostrar('error', fallo); }
  }

  /** Vuelve a pedir el grupo. Publica porque el panel y las pruebas la necesitan. */
  async recargar(): Promise<void> {
    await this.cargar(this.claves());
  }

  // --- Lo que se ve en cada tarjeta ---------------------------------------------

  /**
   * La imagen que el visitante ve HOY en esa ranura.
   *
   * Cuando no hay nada publicado no es un hueco: es la URL compilada del registro, que es
   * exactamente lo que el sitio pinta. Hasta el panel enseñaba en su
   * lugar un recuadro gris con la frase «el sitio muestra su imagen original», que obliga a
   * abrir el sitio en otra pestaña para saber cuál es.
   */
  urlEnElSitio(imagen: AdminImage): string {
    if (imagen.state === 'publicado' && imagen.published) {
      return this.api.imagePreviewUrl(imagen.key, 'publicado', imagen.version, true);
    }
    return versionLigera(DEFAULT_IMAGE_URLS[imagen.key] ?? '');
  }

  /** La imagen sin publicar tal como la pinta LA TARJETA: miniatura, que mide 273 px de ancho. */
  urlSinPublicar(imagen: AdminImage): string | null {
    if (!this.hayBorradorSinPublicar(imagen)) { return null; }
    return this.urlesLocales.get(imagen.key)
      ?? this.api.imagePreviewUrl(imagen.key, 'borrador', imagen.version, true);
  }

  /**
   * La misma imagen sin publicar tal como la pinta EL MARCO: a tamaño completo.
   *
   * No es la de la tarjeta. La miniatura mide 320 px de ancho y el marco la estiraría a 1440
   * como fondo de portada: se vería borrosa y la persona juzgaría la calidad de una imagen que
   * el sitio nunca va a servir así.
   *
   * Cuando el recorte se acaba de hacer en este navegador se usa su URL de objeto, que ya es el
   * archivo entero y no cuesta una ida y vuelta al servidor.
   */
  urlParaElMarco(imagen: AdminImage): string | null {
    if (!this.hayBorradorSinPublicar(imagen)) { return null; }
    return this.urlesLocales.get(imagen.key)
      ?? this.api.imagePreviewUrl(imagen.key, 'borrador', imagen.version, false);
  }

  hayBorradorSinPublicar(imagen: AdminImage): boolean {
    return imagen.draft !== null && imagen.draft.hash !== imagen.published?.hash;
  }

  claseDeEstado(estado: string): string {
    switch (estado) {
      case 'publicado': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'retirado': return 'bg-amber-50 text-amber-800 border-amber-200';
      default: return 'bg-slate-100 text-slate-500 border-slate-200';
    }
  }

  presentacion(clave: string) {
    return PRESENTACION_DE_LA_RANURA[clave] ?? PRESENTACION_NEUTRA;
  }

  /** El marco medido de la ranura. Sale del registro del front, que es donde se midió. */
  marcoDe(clave: string): { w: number; h: number } {
    const ranura = WEB_IMAGE_KEYS.find(x => x.key === clave);
    return { w: ranura?.suggestedWidth ?? 1440, h: ranura?.suggestedHeight ?? 810 };
  }

  /**
   * Las otras claves que rotan con esta en el sitio. Vacío cuando la ranura no rota.
   *
   * SALE DE UNA LISTA DECLARADA, no de comparar tratamientos. Hasta se
   * deducía comparando `PRESENTACION_DE_LA_RANURA`, y eso acertaba con las cuatro portadas por
   * casualidad: comparten tratamiento y además rotan. Las ocho Rutas comparten `TARJETA_DE_RUTA`
   * y no rotan, así que subir la foto de la Ruta 3 la pintaba en las ocho tarjetas.
   *
   * Se cruza con `claves()` porque el bloque solo puede anunciar lo que él muestra: espejar sobre
   * una ranura que está en otro bloque dejaría el marco pintando algo que aquí no se ve.
   */
  rotanCon(clave: string): string[] {
    const grupo = ranurasQueRotanCon(clave);
    return this.claves().filter(otra => grupo.includes(otra));
  }

  // --- Acciones ----------------------------------------------------------------

  /** Abre el ajuste. No sube nada todavía: subir es lo que hace «Usar esta imagen». */
  elegirArchivo(imagen: AdminImage, evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    // El input se vacía siempre: sin esto, elegir DOS VECES el mismo archivo no dispara
    // `change` la segunda y el editor cree que la pantalla se colgó.
    entrada.value = '';
    if (!archivo) { return; }

    const topes = this.topes();
    if (topes && archivo.size > topes.maxBytes) {
      // Se avisa ANTES de decodificar nada. El recortador lo dejaría pasar —lo que sale de él
      // pesa mucho menos que lo que entra— pero un archivo de veinte megas tarda en abrirse y
      // el editor no sabría por qué.
      this.mostrar('error',
        `«${archivo.name}» pesa ${pesoLegible(archivo.size)} y el máximo es ${pesoLegible(topes.maxBytes)}.`);
      return;
    }

    this.ajustando.set({ clave: imagen.key, archivo });
  }

  cancelarAjuste(): void {
    this.ajustando.set(null);
  }

  /**
   * Sube el recorte que salió del recortador.
   *
   * Lo que viaja es SIEMPRE un WebP recortado al marco de la ranura, nunca el archivo original.
   * Es la decisión que el usuario tomó el 29 de agosto: no se guarda el original.
   */
  async usarRecorte(imagen: AdminImage, recorte: RecorteHecho): Promise<void> {
    this.ajustando.set(null);
    this.claveOcupada.set(imagen.key);

    const archivo = new File([recorte.archivo], `${imagen.key}.webp`, { type: 'image/webp' });
    const miniatura = recorte.miniatura ?? await construirMiniatura(archivo);
    const resultado = await this.api.uploadImage(imagen.key, archivo, imagen.version, {
      miniatura: miniatura ?? undefined,
    });
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible subir la imagen.');
      return;
    }

    if (resultado.data && !resultado.data.changed) {
      // No cambió nada en el servidor, pero la persona SÍ acaba de actuar sobre esta ranura:
      // el marco tiene que enseñarle esta y no una de las otras tres que rotan con ella.
      this.mostrar('info', 'Ese archivo ya estaba guardado en esta ranura: no se cambió nada.');
      this.anunciar(imagen.key, false);
      return;
    }

    // La URL de objeto local evita una ida y vuelta al servidor para pintar lo que el navegador
    // acaba de producir, y es lo que hace que la previsualización cambie al instante.
    this.recordarUrlLocal(imagen.key, URL.createObjectURL(recorte.archivo));
    this.mostrar('ok',
      `«${imagen.label}» quedó guardada en ${recorte.ancho}×${recorte.alto} y pesa ${pesoLegible(recorte.archivo.size)}. Todavía no está en el sitio.`);
    await this.recargar();
    this.anunciar(imagen.key, false);
  }

  /** «Desplegar el borrador»: es lo que saca la imagen al sitio. */
  async desplegar(imagen: AdminImage): Promise<void> {
    this.claveOcupada.set(imagen.key);
    const resultado = imagen.state === 'retirado'
      // Una ranura retirada no vuelve con «publicar»: eso es lo que hace que retirar signifique
      // algo. La acción explícita es republicar.
      ? await this.api.republishImage(imagen.key)
      : await this.api.publishImage(imagen.key, imagen.version);
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible publicar la imagen.');
      return;
    }
    this.mostrar('ok', `«${imagen.label}» ya está en el sitio.`);
    await this.recargar();
    this.anunciar(imagen.key, true);
  }

  async retirar(imagen: AdminImage): Promise<void> {
    this.claveOcupada.set(imagen.key);
    const resultado = await this.api.retireImage(imagen.key);
    this.claveOcupada.set(null);

    if (!resultado.ok) {
      this.mostrar('error', resultado.error ?? 'No fue posible retirar la imagen.');
      return;
    }
    this.mostrar('ok',
      `«${imagen.label}» salió del sitio. El sitio vuelve a mostrar la imagen original y su archivo queda guardado aquí.`);
    await this.recargar();
    this.anunciar(imagen.key, true);
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

  /**
   * Le dice al panel qué tiene que pintar el marco para esta ranura.
   *
   * <b>`espejar` solo cuando la persona ACABA DE ACTUAR sobre la ranura.</b> Fijar una portada
   * sobre las cuatro que rotan es una mentira piadosa: sirve para poder mirar la que se está
   * cambiando, y no tiene sentido al cargar la pantalla, cuando nadie está cambiando ninguna.
   * Al cargar se anuncia cada ranura con lo suyo y el marco enseña el sitio tal como quedaría.
   */
  anunciar(clave: string, recargarElSitio: boolean, espejar = true): void {
    const imagen = this.porClave()[clave];
    const url = imagen ? this.urlParaElMarco(imagen) : null;
    this.enVivo.emit({
      clave,
      url,
      rotanConElla: espejar ? this.rotanCon(clave) : [],
      recargarElSitio,
    });
  }

  private recordarUrlLocal(clave: string, url: string): void {
    const previa = this.urlesLocales.get(clave);
    // Se revoca la anterior: sin esto, cambiar diez veces la misma portada deja diez blobs
    // vivos en memoria hasta que se recarga la consola.
    if (previa) { URL.revokeObjectURL(previa); }
    this.urlesLocales.set(clave, url);
  }

  private mostrar(tipo: 'ok' | 'error' | 'info', texto: string): void {
    this.aviso.set({ tipo, texto });
    setTimeout(() => this.aviso.set(null), 6000);
  }
}
