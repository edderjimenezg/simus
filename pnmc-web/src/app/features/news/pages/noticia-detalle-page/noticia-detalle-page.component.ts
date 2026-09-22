import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Component, HostListener, inject, signal } from '@angular/core';
import { Noticia, NoticiasService } from '../../../../core/services/noticias.service';
import { thumbnailFor } from '../../../../shared/utils/media';

/**
 * Una noticia del portal, con el lector del diseño aprobado.
 *
 * <b>DE DONDE VIENE ESTE DISEÑO.</b> Del desarrollo el diseño aprobado del portal. Se conservan sus piezas: la barra
 * de progreso de lectura, la letra capitular, la línea de datos —categoría, fecha, minutos— y la
 * columna de noticias relacionadas.
 *
 * <b>LO QUE NO SE PORTO, Y ES DELIBERADO.</b> Aquel lector traía, para cuando no hubiera cuerpo,
 * cuatro párrafos y una cita escritos a mano sobre «vientos, cuerdas y percusiones». Es contenido
 * inventado que se enseñaría como si fuera del Ministerio. Aquí una noticia sin cuerpo enseña su
 * resumen y nada más.
 *
 * <b>EL CUERPO SE PINTA COMO TEXTO, NO COMO MARCADO.</b> El original lo inyectaba con
 * `bypassSecurityTrustHtml`; lo escribe una persona desde la consola, así que interpretarlo como
 * HTML convertiría ese campo en una vía para meter etiquetas en el portal.
 *
 * <b>SE BUSCA POR DIRECCION Y NO POR IDENTIFICADOR.</b> La dirección se calcula del título al
 * crear y no cambia, así que un enlace compartido sigue funcionando.
 */
@Component({
  selector: 'app-noticia-detalle-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './noticia-detalle-page.component.html',
})
export class NoticiaDetallePageComponent {
  private readonly api = inject(NoticiasService);
  // `ActivatedRoute` y no `input()` de ruta: la aplicación no declara `withComponentInputBinding`.
  private readonly ruta = inject(ActivatedRoute);

  readonly miniatura = thumbnailFor;

  readonly noticia = signal<Noticia | null>(null);
  readonly relacionadas = signal<Noticia[]>([]);
  readonly cargando = signal(true);
  readonly noEncontrada = signal(false);
  readonly progresoDeLectura = signal(0);
  readonly enlaceCopiado = signal(false);

  constructor() {
    // `paramMap` y no `snapshot`: navegar de una relacionada a otra reutiliza el componente.
    this.ruta.paramMap.subscribe(parametros => {
      void this.cargar(parametros.get('slug') ?? '');
    });
  }

  /**
   * Cuánto se ha leído.
   *
   * SE MIDE SOBRE EL DOCUMENTO ENTERO y no sobre el artículo: es lo que hace el diseño de
   * referencia y lo que coincide con la sensación de quien desplaza la página.
   */
  @HostListener('window:scroll')
  alDesplazar(): void {
    const alto = document.documentElement.scrollHeight - document.documentElement.clientHeight;
    if (alto <= 0) { this.progresoDeLectura.set(0); return; }
    this.progresoDeLectura.set(Math.min(100, (window.scrollY / alto) * 100));
  }

  private async cargar(slug: string): Promise<void> {
    this.cargando.set(true);
    this.noEncontrada.set(false);
    this.enlaceCopiado.set(false);
    this.progresoDeLectura.set(0);
    window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior });

    const resultado = await this.api.obtenerPublica(slug);

    if (resultado.ok && resultado.data) {
      this.noticia.set(resultado.data);
      await this.cargarRelacionadas(resultado.data);
    } else {
      this.noticia.set(null);
      this.relacionadas.set([]);
      this.noEncontrada.set(true);
    }

    this.cargando.set(false);
  }

  /**
   * Otras noticias para seguir leyendo.
   *
   * SE PIDEN DE LA MISMA CATEGORIA cuando la hay, y las más recientes cuando no. Y la que se está
   * leyendo se excluye: ofrecerle a alguien el artículo que tiene abierto es el detalle que delata
   * que la columna no mira lo que hay en pantalla.
   */
  private async cargarRelacionadas(actual: Noticia): Promise<void> {
    const deLaCategoria = await this.pedirOtras(actual, actual.categoria ?? undefined);

    // SI SU CATEGORIA NO DA PARA MAS, SE OFRECEN LAS RECIENTES. Una categoría con un solo
    // artículo dejaría la columna vacía justo en las noticias más específicas, que son las que
    // más ganas dan de seguir leyendo.
    const otras = deLaCategoria.length > 0 ? deLaCategoria : await this.pedirOtras(actual, undefined);

    this.relacionadas.set(otras);
  }

  private async pedirOtras(actual: Noticia, categoria: string | undefined): Promise<Noticia[]> {
    const resultado = await this.api.listarPublicas({ categoria, tamano: 5 });
    return (resultado.ok && resultado.data ? resultado.data.items : [])
      .filter(n => n.id !== actual.id)
      .slice(0, 4);
  }

  /**
   * El cuerpo partido en párrafos.
   *
   * Se parte por líneas en blanco y se pinta como texto. La letra capitular la pone el CSS sobre
   * el primer párrafo, no una marca en el contenido.
   */
  parrafos(cuerpo: string | null): string[] {
    return (cuerpo ?? '')
      .split(/\n\s*\n/)
      .map(p => p.trim())
      .filter(p => p.length > 0);
  }

  minutosDeLectura(noticia: Noticia): number {
    const palabras = `${noticia.resumen} ${noticia.cuerpo ?? ''}`.trim().split(/\s+/).length;
    return Math.max(1, Math.ceil(palabras / 200));
  }

  /**
   * Copia la dirección de la noticia.
   *
   * SE COPIA EN VEZ DE ABRIR UN MENU DE REDES. Un menú de compartir de terceros carga guiones de
   * otros dominios en un sitio del Ministerio; copiar el enlace resuelve lo mismo sin eso.
   */
  async copiarEnlace(): Promise<void> {
    try {
      await navigator.clipboard.writeText(window.location.href);
      this.enlaceCopiado.set(true);
    } catch {
      // Un navegador sin portapapeles —o sin permiso— no es un fallo que deba interrumpir la
      // lectura: el enlace sigue estando en la barra de direcciones.
      this.enlaceCopiado.set(false);
    }
  }
}
