import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { LucideImage, LucideTrash2 } from '@lucide/angular';

import { BancoDeArchivosService } from '../../../core/services/banco-de-archivos.service';

/**
 * Subir una imagen y quedarse con su dirección.
 *
 * <b>POR QUE EXISTE.</b> El formulario del Catálogo Editorial pedía la portada como una RUTA
 * escrita a mano —«/editorial/thumbs/PNMC-ED-001.png»—, lo que obligaba a subir el fichero por otro
 * sitio y acertar a transcribir dónde quedó. El criterio es este: «las portadas deberían
 * poderse subir y no poner en un link». Un formulario que pide la URL de una imagen deja además el
 * contenido del catálogo dependiendo de un servidor ajeno que puede caerse.
 *
 * <b>NO TRAE SU PROPIA SUBIDA.</b> Usa `BancoDeArchivosService`, que es por donde suben las imágenes
 * las Noticias y la Agenda: una sola puerta, un solo sitio donde viven los ficheros y una sola
 * comprobación de tipo y tamaño en el servidor.
 *
 * <b>EL TEXTO ALTERNATIVO ES OBLIGATORIO Y SE PIDE ANTES.</b> Sin él, la imagen no existe para quien
 * navega con lector de pantalla, y escribirlo después casi nunca ocurre.
 */
@Component({
  selector: 'app-cargador-de-imagen',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideImage, LucideTrash2],
  templateUrl: './cargador-de-imagen.component.html',
})
export class CargadorDeImagenComponent {
  private readonly banco = inject(BancoDeArchivosService);

  /** Qué imagen es, para el rótulo y para el texto alternativo por omisión. */
  @Input() etiqueta = 'Imagen';

  /** La dirección que ya tiene el registro, si la tiene. */
  @Input() rutaActual = '';

  @Output() rutaCambia = new EventEmitter<string>();

  readonly subiendo = signal(false);
  readonly error = signal<string | null>(null);
  readonly textoAlternativo = signal('');

  async elegir(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    if (!archivo) { return; }

    const alt = this.textoAlternativo().trim();
    if (!alt) {
      this.error.set('Escribe primero el texto alternativo: describe la imagen para quien no puede verla.');
      entrada.value = '';
      return;
    }

    this.subiendo.set(true);
    this.error.set(null);

    const resultado = await this.banco.subirImagen(archivo, alt);
    if (resultado.ok && resultado.archivo) {
      this.rutaCambia.emit(resultado.archivo.url);
    } else {
      this.error.set(resultado.error ?? 'No fue posible subir la imagen.');
    }

    // SE LIMPIA LA ENTRADA para que elegir el MISMO fichero otra vez vuelva a disparar el evento.
    entrada.value = '';
    this.subiendo.set(false);
  }

  quitar(): void {
    this.rutaCambia.emit('');
    this.error.set(null);
  }
}
