import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { LucideFileText, LucideScanText, LucideSparkles, LucideTriangleAlert } from '@lucide/angular';

import { firstValueFrom } from 'rxjs';

import { ApiClientService } from '../../../core/http/api-client.service';

/** Un valor leído del documento, con el porqué de la propuesta. */
export interface ValorPropuesto { valor: string; origen: string; nota?: string | null }

/** Lo que un documento propone. La forma la fija cada módulo; esto es lo común. */
export interface PropuestaDeDocumento {
  requiereOcr: boolean;
  /**
   * Lo propuesto salió de MIRAR la página, no de su capa de texto.
   *
   * Se dice en pantalla. Un reconocimiento óptico acierta mucho pero no siempre, y quien cataloga
   * tiene derecho a saber con qué fiabilidad le llega cada propuesta.
   */
  leidoConReconocimientoOptico?: boolean;
  loQueSePudoLeer: string[];
  [clave: string]: unknown;
}

/**
 * Leer un documento y proponer con qué rellenar un registro.
 *
 * <b>ES EL MISMO COMPONENTE PARA EL CATALOGO Y PARA LA AGENDA.</b> Lo que cambia entre un libro y un
 * afiche es qué se reconoce dentro, y eso lo decide el servidor; lo que hace esta pantalla —recibir
 * el fichero, enseñarlo leyendo, contar qué se pudo sacar y avisar de lo que no— es idéntico. Tener
 * dos copias sería tener dos sitios donde arreglar el mismo mensaje.
 *
 * <b>NO GUARDA NADA.</b> Emite la propuesta y quien la monta decide qué hacer con ella. El registro
 * lo crea después una persona, revisando: es la regla del proyecto sobre importación asistida, y aquí
 * se cumple porque este componente no sabe crear nada.
 *
 * <b>EL ORIGEN DE CADA VALOR SE ENSEÑA SIEMPRE.</b> Un título que escribió quien generó el documento
 * y otro deducido del tamaño de la letra no merecen la misma confianza, y quien cataloga tiene
 * derecho a saber cuál es cuál antes de aceptarlo.
 */
@Component({
  selector: 'app-importar-desde-documento',
  standalone: true,
  imports: [CommonModule, LucideFileText, LucideScanText, LucideSparkles, LucideTriangleAlert],
  templateUrl: './importar-desde-documento.component.html',
})
export class ImportarDesdeDocumentoComponent {
  private readonly api = inject(ApiClientService);

  /** A qué ruta se manda el documento. La fija el módulo que monta el componente. */
  @Input({ required: true }) ruta = '';

  /** Cómo se llama lo que se va a crear, para hablar en su idioma. */
  @Input() queSeCrea = 'registro';

  @Output() propuesta = new EventEmitter<PropuestaDeDocumento>();

  readonly leyendo = signal(false);
  readonly error = signal<string | null>(null);
  readonly resultado = signal<PropuestaDeDocumento | null>(null);
  readonly nombreDelArchivo = signal('');

  async elegir(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    if (!archivo) { return; }

    this.leyendo.set(true);
    this.error.set(null);
    this.resultado.set(null);
    this.nombreDelArchivo.set(archivo.name);

    const cuerpo = new FormData();
    cuerpo.append('file', archivo, archivo.name);

    try {
      // Se reutiliza `postForm`, que es por donde sube el banco de archivos: una sola puerta
      // para lo que viaja como formulario.
      const leido = await firstValueFrom(this.api.postForm<PropuestaDeDocumento>(this.ruta, cuerpo));
      this.resultado.set(leido);
      this.propuesta.emit(leido);
    } catch (fallo: unknown) {
      const detalle = fallo as { payload?: Record<string, string[]>; message?: string };
      const primero = Object.values(detalle?.payload ?? {}).flat()[0];
      this.error.set(primero ?? detalle?.message ?? 'No fue posible leer el documento.');
    }

    // SE LIMPIA LA ENTRADA para que elegir el MISMO fichero otra vez vuelva a disparar el evento.
    entrada.value = '';
    this.leyendo.set(false);
  }

  /** Cuántos valores trae una lista de la propuesta, sin que la plantilla sepa de tipos. */
  cuantos(clave: string): number {
    const valor = this.resultado()?.[clave];
    return Array.isArray(valor) ? valor.length : 0;
  }

  descartar(): void {
    this.resultado.set(null);
    this.error.set(null);
    this.nombreDelArchivo.set('');
  }
}
