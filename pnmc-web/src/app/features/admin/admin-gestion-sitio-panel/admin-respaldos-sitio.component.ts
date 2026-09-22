import { CommonModule } from '@angular/common';
import { Component, Input, inject, signal } from '@angular/core';
import { LucideAlertCircle, LucideDownload, LucideUpload } from '@lucide/angular';
import { ContenidoWebApiService, ImportReport } from '../../../core/services/contenido-web-api.service';
import { TextosWebService } from '../../../core/services/textos-web.service';
import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';

@Component({
  selector: 'app-admin-respaldos-sitio',
  standalone: true,
  imports: [CommonModule, LucideAlertCircle, LucideDownload, LucideUpload, ConfirmacionComponent],
  templateUrl: './admin-respaldos-sitio.component.html',
})
export class AdminRespaldosSitioComponent {
  private readonly api = inject(ContenidoWebApiService);
  private readonly textos = inject(TextosWebService);

  @Input() session: { fullName?: string } | null = null;

  readonly procesando = signal(false);
  readonly aviso = signal<{ tipo: 'ok' | 'error' | 'info'; texto: string } | null>(null);
  readonly pendienteLocal = signal(this.textos.pendingLocalContent());

  async descargarServidor(): Promise<void> {
    this.procesando.set(true);
    const resultado = await this.api.getServerSnapshot();
    this.procesando.set(false);

    if (!resultado.ok || !resultado.data) {
      this.mostrar('error', resultado.error ?? 'No fue posible generar el respaldo del servidor.');
      return;
    }

    this.descargarJson(
      resultado.data,
      `pnmc-textos-equipo-${new Date().toISOString().slice(0, 10)}.json`,
    );
    this.mostrar('ok', 'Se descargó el respaldo vigente de textos y equipo.');
  }

  descargarRescateLocal(): void {
    try {
      const respaldo = this.textos.exportContent(this.session?.fullName || 'Webmaster');
      this.descargarJson(
        respaldo,
        `pnmc-rescate-navegador-${respaldo.exportedAt.slice(0, 10)}.json`,
      );
      this.mostrar('ok', 'Se descargó el contenido histórico guardado en este navegador.');
    } catch {
      this.mostrar('error', 'No fue posible descargar el rescate de este navegador.');
    }
  }

  async importarAlServidor(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = '';
    if (!archivo) { return; }

    this.procesando.set(true);
    try {
      let respaldo: unknown;
      try {
        respaldo = JSON.parse(await archivo.text());
      } catch {
        this.mostrar('error', 'El archivo no es un JSON válido.');
        return;
      }

      const simulacion = await this.api.previewImport(respaldo, false);
      if (!simulacion.ok || !simulacion.data) {
        this.mostrar('error', simulacion.error ?? 'No fue posible simular la importación.');
        return;
      }

      if (!await this.preguntarPorElPlan(simulacion.data)) {
        this.mostrar('info', 'Importación cancelada. No se escribió nada.');
        return;
      }

      const vueltaAtras = await this.api.getServerSnapshot();
      if (!vueltaAtras.ok || !vueltaAtras.data) {
        this.mostrar('error', 'No se importó nada porque no fue posible descargar la copia previa del servidor.');
        return;
      }
      this.descargarJson(
        vueltaAtras.data,
        `pnmc-antes-de-importar-${new Date().toISOString().slice(0, 10)}.json`,
      );

      const aplicado = await this.api.applyImport(respaldo, simulacion.data.planHash, false);
      if (!aplicado.ok || !aplicado.data) {
        this.mostrar(
          'error',
          aplicado.conflict
            ? 'El contenido cambió después de la simulación. No se escribió nada; vuelva a intentarlo.'
            : aplicado.error ?? 'No fue posible aplicar la importación.',
        );
        return;
      }

      this.mostrar(
        'ok',
        `Se importaron ${aplicado.data.cambios} elementos como borrador. El sitio público no cambió.`,
      );
    } finally {
      this.procesando.set(false);
    }
  }

  /**
   * Lo que la simulación dice que pasaría, esperando confirmación.
   *
   * <b>ERA UN `confirm()` CON SEIS LINEAS DENTRO.</b> El diálogo del navegador recibe una sola
   * cadena, así que el resultado de la simulación —textos que entran, textos que no cambian, los que
   * se omiten porque ya se editaron en el servidor, los que se rechazan— viajaba como un párrafo con
   * saltos de línea y viñetas de texto. Aquí es una lista, que es lo que siempre fue.
   */
  readonly planPendiente = signal<ImportReport | null>(null);

  /** Lo que resuelve la espera del flujo de importación cuando se decide. */
  private decidirElPlan: ((sigue: boolean) => void) | null = null;

  private preguntarPorElPlan(plan: ImportReport): Promise<boolean> {
    this.planPendiente.set(plan);
    return new Promise<boolean>(resolver => { this.decidirElPlan = resolver; });
  }

  /** Las líneas de la simulación, cada una con su cifra. */
  lineasDelPlan(plan: ImportReport): string[] {
    const lineas = [
      `${plan.textos.porAplicar} textos entrarían como borrador`,
      `${plan.textos.sinCambio} textos no cambiarían`,
      plan.equipo.porAplicar ? `la nómina con ${plan.equipo.personas} personas entraría como borrador` : 'la nómina no cambiaría',
    ];
    if (plan.textos.enConflictoTotal > 0) {
      lineas.push(`${plan.textos.enConflictoTotal} textos se omitirían porque ya fueron editados en el servidor`);
    }
    if (plan.textos.rechazadosTotal > 0) {
      lineas.push(`${plan.textos.rechazadosTotal} textos serían rechazados por validación`);
    }
    return lineas;
  }

  confirmarLaImportacion(): void { this.resolverElPlan(true); }
  cancelarLaImportacion(): void { this.resolverElPlan(false); }

  private resolverElPlan(sigue: boolean): void {
    this.planPendiente.set(null);
    this.decidirElPlan?.(sigue);
    this.decidirElPlan = null;
  }

  private descargarJson(contenido: unknown, nombre: string): void {
    const blob = new Blob([JSON.stringify(contenido, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombre;
    document.body.appendChild(enlace);
    enlace.click();
    document.body.removeChild(enlace);
    URL.revokeObjectURL(url);
  }

  private mostrar(tipo: 'ok' | 'error' | 'info', texto: string): void {
    this.aviso.set({ tipo, texto });
    setTimeout(() => this.aviso.set(null), 7000);
  }
}
