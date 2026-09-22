import { SelectorSegmentadoComponent } from '../ui/selector-segmentado/selector-segmentado.component';
import { BarraDeListaComponent } from '../ui/barra-de-lista/barra-de-lista.component';
import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { ArchivoEnElBanco, BancoDeArchivosService, PaginaDelBanco } from '../../../core/services/banco-de-archivos.service';
import { MenuDeAccionesComponent, AccionDeRegistro } from '../ui/menu-de-acciones/menu-de-acciones.component';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../../../features/admin/domain/formatos-de-fecha';
import { LucideRefreshCw } from '@lucide/angular';
import { ConfirmacionComponent } from '../ui/confirmacion/confirmacion.component';
import { BotonComponent } from '../ui/boton/boton.component';
import { EstadoDeListaComponent } from '../ui/estado-de-lista/estado-de-lista.component';

/**
 * La gestión del banco de archivos.
 *
 * <b>POR QUE EXISTE.</b> El banco admitía archivos y los servía, y no había ninguna forma de mirar
 * dentro: para saber algo de un archivo había que conocer ya su identificador. Un almacén en el que
 * no se puede mirar acumula lo que nadie recuerda —medido: de cinco
 * archivos, TRES eran huérfanos de formularios abandonados, y uno de ellos ocupaba cuota de una
 * organización que no tenía cómo retirarlo—.
 *
 * <b>UNA SOLA PANTALLA PARA LOS DOS CANALES.</b> La consola ve el banco entero con su procedencia;
 * una organización ve lo suyo y su cuota. Lo que cambia lo decide el servidor según por dónde se
 * pregunta, así que aquí solo cambia el canal. Dos pantallas gemelas divergirían en el primer
 * arreglo, que es lo que este mismo corte acaba de cerrar en la ficha del catálogo.
 */
@Component({
  selector: 'app-gestion-del-banco',
  standalone: true,
  imports: [
    BarraDeListaComponent,SelectorSegmentadoComponent, BotonComponent, EstadoDeListaComponent, LucideRefreshCw, CommonModule, MenuDeAccionesComponent, ConfirmacionComponent],
  templateUrl: './gestion-del-banco.component.html',
})
export class GestionDelBancoComponent implements OnInit {
  private readonly banco = inject(BancoDeArchivosService);

  @Input() canal: 'institucional' | 'externo' = 'institucional';

  readonly pagina = signal<PaginaDelBanco | null>(null);
  readonly cargando = signal(false);
  readonly error = signal('');
  readonly aviso = signal('');
  readonly soloHuerfanos = signal(false);

  /** Las tres procedencias, como posiciones de un solo control. */
  readonly PROCEDENCIAS = [
    { id: '', etiqueta: 'Todos' },
    { id: 'institucional', etiqueta: 'Del Programa' },
    { id: 'organizaciones', etiqueta: 'De organizaciones' },
  ];
  readonly procedencia = signal('');
  readonly numero = signal(1);
  readonly retirando = signal<number | null>(null);

  readonly formatoDeFecha = FECHA_Y_HORA_ADMINISTRATIVA;

  readonly archivos = computed(() => this.pagina()?.items ?? []);
  readonly huerfanos = computed(() => this.archivos().filter(a => a.huerfano).length);

  ngOnInit(): void { void this.cargar(); }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set('');
    const resultado = await this.banco.listar(this.canal, {
      procedencia: this.procedencia() || undefined,
      soloHuerfanos: this.soloHuerfanos(),
      pagina: this.numero(),
    });
    this.cargando.set(false);
    if (resultado.ok) { this.pagina.set(resultado.data ?? null); }
    else { this.error.set(resultado.error ?? 'No fue posible consultar el banco de archivos.'); }
  }

  async filtrarPor(procedencia: string): Promise<void> {
    this.procedencia.set(procedencia); this.numero.set(1); await this.cargar();
  }

  async alternarHuerfanos(): Promise<void> {
    this.soloHuerfanos.update(v => !v); this.numero.set(1); await this.cargar();
  }

  async irAPagina(n: number): Promise<void> { this.numero.set(n); await this.cargar(); }

  /**
   * Lo que se puede hacer con un archivo.
   *
   * <b>RETIRAR SOLO SE OFRECE SI DE VERDAD SE PUEDE.</b> Un archivo en uso no se puede quitar
   * —seis tablas pueden apuntarlo— y ofrecerlo para que el servidor lo rechace es hacer perder un
   * clic. Se ofrece desactivado con el motivo delante, que es más útil que esconderlo: dice por
   * qué no, y qué habría que hacer antes.
   */
  accionesDe(archivo: ArchivoEnElBanco): AccionDeRegistro[] {
    const usos = archivo.usos.map(u => u.descripcion).join(' · ');
    return [
      { id: 'abrir', etiqueta: 'Ver el archivo', enlace: archivo.url, nuevaPestana: true, tono: 'principal' },
      {
        id: 'retirar',
        etiqueta: 'Retirar del banco',
        tono: 'peligro',
        deshabilitada: !archivo.huerfano || this.retirando() === archivo.id,
        motivo: archivo.huerfano ? undefined : `Se está usando: ${usos}. Quítalo primero de donde está.`,
      },
    ];
  }

  ejecutarAccion(archivo: ArchivoEnElBanco, accion: string): void {
    if (accion !== 'retirar') { return; }
    this.archivoPorRetirar.set(archivo);
  }

  /** El archivo que espera confirmación, preguntada en el diálogo del proyecto y no en el navegador. */
  readonly archivoPorRetirar = signal<ArchivoEnElBanco | null>(null);

  comoSeLlama(archivo: ArchivoEnElBanco): string { return archivo.nombre || `el archivo ${archivo.id}`; }

  cerrarLaConfirmacion(): void { this.archivoPorRetirar.set(null); }

  async confirmarElRetiro(): Promise<void> {
    const archivo = this.archivoPorRetirar();
    if (!archivo) { return; }
    this.archivoPorRetirar.set(null);
    const comoSeLlama = this.comoSeLlama(archivo);

    this.error.set(''); this.aviso.set('');
    this.retirando.set(archivo.id);
    const resultado = await this.banco.retirar(this.canal, archivo.id);
    this.retirando.set(null);

    if (!resultado.ok) {
      this.error.set([resultado.error, resultado.usos?.join(' · ')].filter(Boolean).join(' '));
      return;
    }
    this.aviso.set(`Se retiró ${comoSeLlama}.`);
    await this.cargar();
  }

  /** Los que no se pudieron pintar. Se recuerda por identificador para no reintentar en bucle. */
  private readonly sinVistaPrevia = signal<ReadonlySet<number>>(new Set());

  seVe(id: number): boolean { return !this.sinVistaPrevia().has(id); }

  noSeVe(id: number): void {
    this.sinVistaPrevia.update(previos => new Set([...previos, id]));
  }

  /** El peso en la unidad que se lee, no en bytes. */
  peso(bytes: number | null): string {
    if (bytes === null || bytes === undefined) { return '—'; }
    if (bytes < 1024) { return `${bytes} B`; }
    if (bytes < 1024 * 1024) { return `${Math.round(bytes / 1024)} KB`; }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
