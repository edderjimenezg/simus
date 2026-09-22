import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PanelLateralComponent } from '../panel-lateral/panel-lateral.component';
import { HistorialDeRegistroService, LineaDeHistorial } from '../../../../core/services/historial-de-registro.service';
import { ProcedenciaDeRegistro } from '../../../../core/services/procedencia';

/**
 * Quién ha tocado este registro, desde que nació.
 *
 * <b>ABRE LA PROCEDENCIA Y EL HISTORIAL JUNTOS PORQUE SON LA MISMA PREGUNTA.</b> Quien abre esto
 * quiere saber de dónde salió la ficha y qué le ha pasado desde entonces; enseñar el nacimiento en
 * un sitio y las actuaciones en otro obliga a cruzarlos a mano.
 *
 * <b>SE PIDE AL ABRIR, NO AL PINTAR LA TABLA.</b> Una consulta de auditoría por fila convertiría
 * un listado de veinte en veinte viajes a la base para un dato que casi nadie mira.
 */
/**
 * Una fotografía del perfil público de un registro, para el historial.
 *
 * ES GENERICA A PROPOSITO: este diálogo lo usan varias pantallas y no tiene por qué saber qué es un
 * perfil versionado de Festival. Quien lo abre traduce lo suyo a estas cuatro cosas.
 */
export interface VersionDelRegistro {
  readonly id: string | number;
  /** Lo que se lee en negrita: «Versión 3 · Publicado», por ejemplo. */
  readonly etiqueta: string;
  /** La línea de debajo: fechas, o lo que distinga una de otra. */
  readonly detalle?: string | null;
  /** Es la que el portal está sirviendo ahora mismo. */
  readonly vigente?: boolean;
}

@Component({
  selector: 'app-historial-de-registro',
  standalone: true,
  imports: [CommonModule, PanelLateralComponent],
  template: `
    <!--
      EL CAJON DE TODA LA CONSOLA, y no uno compuesto a mano. Este era un cajón propio con su velo y
      su botón, y NO CERRABA CON ESCAPE —medido al verificar Mercados,
      mientras la previsualización, a su lado, sí cerraba—. Dos ventanas hermanas con dos
      comportamientos. Con la pieza compartida hereda el cierre, el foco y el fondo quieto.
    -->
    <app-panel-lateral [titulo]="titulo" [contexto]="procedencia === undefined ? 'Historial' : 'Historial y procedencia'"
      idDelTitulo="historial-de-registro-titulo" (cerrar)="cerrar.emit()">

        <!--
          TRES ESTADOS, NO DOS. Que quien abre el panel no pase la procedencia NO significa que el
          registro no la tenga: significa que esta pantalla no la tiene a mano. Afirmar «no consta
          quién lo incorporó» en ese caso es decir algo falso sobre el registro —y se notaba, porque
          el mismo Festival decía una cosa abierto desde su ficha y otra desde la tabla—. Por eso
          «undefined» (no se preguntó) y «null» (se preguntó y no hay) se tratan distinto.
        -->
        @if (procedencia !== undefined) {
        <section>
          <h4 class="titulo-de-apartado">De dónde vino</h4>
          @if (procedencia; as p) {
            <dl class="mt-3 grid gap-2 text-xs">
              <div class="flex gap-2">
                <dt class="w-28 shrink-0 text-rotulo">Procedencia</dt>
                <dd class="flex-1 text-valor">{{ p.organizacionProcedenciaNombre || p.contextoEtiqueta }}</dd>
              </div>
              <div class="flex gap-2">
                <dt class="w-28 shrink-0 text-rotulo">Contexto</dt>
                <dd class="flex-1 text-valor">{{ p.contextoEtiqueta }}</dd>
              </div>
              <div class="flex gap-2">
                <dt class="w-28 shrink-0 text-rotulo">Creado por</dt>
                <dd class="flex-1 text-valor">{{ p.usuarioCreadorNombre || 'No consta' }}</dd>
              </div>
              <div class="flex gap-2">
                <dt class="w-28 shrink-0 text-rotulo">Fecha</dt>
                <dd class="flex-1 tabular-nums text-valor">{{ p.fechaRegistro | date: 'd MMM y, HH:mm':'':'es-CO' }}</dd>
              </div>
            </dl>
          } @else {
            <!-- DECIRLO ES MÁS HONESTO QUE DEJAR EL HUECO: los registros anteriores a la tabla de
                 procedencia no la tienen, y eso no es un fallo de carga. -->
            <p class="mt-2 text-xs text-rotulo">
              Este registro es anterior al seguimiento de procedencia, así que no consta quién lo
              incorporó. Su historial de actuaciones sí está completo.
            </p>
          }
        </section>
        }

        <!--
          QUE SE HA PUBLICADO, que no es lo mismo que qué ha pasado.
          «Qué ha pasado» son actuaciones —quién pulsó qué y cuándo—; esto son las FOTOGRAFÍAS del
          perfil público: cada una es lo que el portal mostraba en su momento. Pedido por el dueño
          del proyecto: «el historial versionado es algo más como de
          trazabilidad, historial, administrativo… que quede en la sección de festival».

          ES OPCIONAL Y GENÉRICA. Quien no la pase no ve nada; este componente lo usan varias
          pantallas y ninguna otra tiene perfiles versionados.
        -->
        @if (versiones !== undefined) {
        <section class="mt-5">
          <h4 class="titulo-de-apartado">Qué se ha publicado</h4>
          @if (versiones === null) {
            <p role="alert" class="mt-3 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-relaxed text-amber-900">
              No fue posible consultar las versiones publicadas ahora mismo.
            </p>
          } @else if (versiones.length === 0) {
            <p class="mt-3 text-xs text-rotulo">Todavía no se ha publicado ninguna versión de este registro.</p>
          } @else {
            <ol data-testid="historial-versiones" class="mt-3 space-y-3">
              @for (version of versiones; track version.id) {
                <li class="border-l-2 pl-3" [class]="version.vigente ? 'border-verde-medio' : 'border-filete'">
                  <p class="text-xs font-bold text-valor">
                    {{ version.etiqueta }}
                    @if (version.vigente) { <span class="ml-1 font-normal text-verde-texto">· es la que se ve hoy</span> }
                  </p>
                  @if (version.detalle) { <p class="text-dato text-rotulo">{{ version.detalle }}</p> }
                </li>
              }
            </ol>
          }
        </section>
        }

        <section class="mt-5">
          <h4 class="titulo-de-apartado">Qué ha pasado</h4>
          @if (cargando()) {
            <p class="mt-3 text-xs text-rotulo">Consultando el historial…</p>
          } @else if (fallo()) {
            <!-- NO SE PUDO LEER NO ES LO MISMO QUE NO HAY NADA. Decir «sin actuaciones» cuando la
                 consulta falló es afirmar algo falso sobre el registro. -->
            <p role="alert" class="mt-3 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-relaxed text-amber-900">
              No fue posible consultar el historial ahora mismo. Esto no significa que el registro
              no tenga actuaciones: vuelve a abrirlo en un momento.
            </p>
          } @else if (lineas().length === 0) {
            <p class="mt-3 text-xs text-rotulo">No hay actuaciones registradas todavía.</p>
          } @else {
            <ol class="mt-3 space-y-3">
              @for (linea of lineas(); track linea.id) {
                <li class="border-l-2 border-filete pl-3">
                  <p class="text-xs font-bold text-valor">{{ linea.accionEtiqueta || linea.accion }}</p>
                  <p class="text-dato text-rotulo">
                    {{ linea.autor?.nombre || 'Sin autor registrado' }} ·
                    <span class="tabular-nums">{{ linea.fecha | date: 'd MMM y, HH:mm':'':'es-CO' }}</span>
                  </p>
                </li>
              }
            </ol>
          }
        </section>
    </app-panel-lateral>
  `,
})
export class HistorialDeRegistroComponent {
  private readonly api = inject(HistorialDeRegistroService);

  @Input({ required: true }) titulo = '';
  /**
   * De dónde vino el registro, si quien abre el panel lo tiene a mano.
   *
   * <b>`undefined` Y `null` NO SON LO MISMO.</b> `null` es «se consultó y este registro no tiene
   * procedencia» —pasa con los anteriores al seguimiento— y se dice en pantalla. Sin pasar nada,
   * el apartado no se pinta: la pantalla que abre el panel no tenía el dato, y eso no es un
   * hecho sobre el registro.
   */
  @Input() procedencia: ProcedenciaDeRegistro | null | undefined = undefined;

  /**
   * Las fotografías del perfil público que este registro ha tenido.
   *
   * <b>TRES ESTADOS, COMO `procedencia`, Y POR EL MISMO MOTIVO.</b> `undefined` es «esta pantalla no
   * las pide» y no pinta nada; `null` es «se pidieron y la consulta falló», que no es lo mismo que
   * no haber ninguna; y una lista vacía es «no se ha publicado nunca». Decir «sin versiones» cuando
   * la consulta falló es afirmar algo falso sobre el registro.
   */
  @Input() versiones: VersionDelRegistro[] | null | undefined = undefined;
  @Output() cerrar = new EventEmitter<void>();

  readonly lineas = signal<LineaDeHistorial[]>([]);
  readonly cargando = signal(true);
  /** Si la última lectura no llegó a responder. Se dice en pantalla en vez de fingir que no hay. */
  readonly fallo = signal(false);

  /** La tabla y el registro se reciben juntos: por separado, el historial cruzaría módulos. */
  @Input({ required: true }) set registro(valor: { tabla: string; id: string | number }) {
    void this.cargar(valor.tabla, valor.id);
  }

  private async cargar(tabla: string, id: string | number): Promise<void> {
    this.cargando.set(true);
    const resultado = await this.api.leer(tabla, id);
    this.lineas.set(resultado.lineas);
    this.fallo.set(resultado.fallo);
    this.cargando.set(false);
  }
}
