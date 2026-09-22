import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LucideLandmark, LucideUsersRound } from '@lucide/angular';
import { ProcedenciaDeRegistro } from '../../../../core/services/procedencia';

/**
 * El sello discreto que dice de dónde vino un registro.
 *
 * <b>QUÉ RESPONDE, Y POR QUÉ NO BASTA UNA SOLA LÍNEA.</b> Sobre cualquier ficha hay que poder
 * contestar tres preguntas distintas: qué entidad la incorporó, qué cuenta ejecutó la acción y
 * quién responde por ella. Las dos primeras las contesta este sello; la tercera es un dato del
 * propio registro —su organización responsable— y se enseña donde le corresponde. Mezclarlas
 * acabaría atribuyéndole al Programa festivales que no organiza.
 *
 * <b>ES DISCRETO A PROPÓSITO.</b> La procedencia importa cuando alguien pregunta por ella, no
 * mientras se trabaja: va como una etiqueta pequeña con el detalle en el <i>title</i>, no como un
 * bloque que compita con el contenido.
 *
 * <b>SI NO CONSTA, LO DICE.</b> Los registros anteriores a la tabla de procedencia pueden no
 * tenerla, y decirlo es más honesto que dejar el hueco en blanco o inventar una.
 */
@Component({
  selector: 'app-sello-de-procedencia',
  standalone: true,
  imports: [CommonModule, LucideLandmark, LucideUsersRound],
  template: `
    @if (procedencia; as p) {
      <!--
        SIN RECUADRO DESDE EL 12 DE SEPTIEMBRE DE 2026. Era una píldora más, y el usuario las retiró
        de todo el diseño. El icono dice de un vistazo si vino del Programa o de fuera, el texto
        sigue al lado y el detalle completo sigue en el atributo title.
      -->
      <span [title]="detalle(p)"
            [class]="p.esInstitucional ? 'text-morado' : 'text-slate-500'"
            class="inline-flex items-center gap-1 text-dato font-bold uppercase tracking-wider">
        @if (p.esInstitucional) {
          <svg lucideLandmark size="11" aria-hidden="true"></svg>
        } @else {
          <svg lucideUsersRound size="11" aria-hidden="true"></svg>
        }
        {{ etiquetaCorta(p) }}
      </span>
      @if (detallado) {
        <span class="mt-1 block text-dato leading-relaxed text-slate-500">
          {{ p.contextoEtiqueta }}@if (p.organizacionProcedenciaNombre) { · {{ p.organizacionProcedenciaNombre }} }
          @if (p.usuarioCreadorNombre) {
            <br />Creado por {{ p.usuarioCreadorNombre }} el {{ p.fechaRegistro | date: 'd MMM y':'':'es-CO' }}
          }
        </span>
      }
    } @else if (detallado) {
      <span class="text-dato text-slate-400">La procedencia de este registro no consta.</span>
    }
  `,
})
export class SelloDeProcedenciaComponent {
  @Input() procedencia: ProcedenciaDeRegistro | null = null;

  /** Con `detallado`, además del sello se escribe quién y cuándo. Para fichas, no para tablas. */
  @Input() detallado = false;

  /**
   * Lo que cabe en el sello.
   *
   * NO ES EL TEXTO DEL SERVIDOR TAL CUAL: «Registro institucional» no cabe en una celda de tabla.
   * El texto completo sigue disponible en el `title` y en el modo detallado, así que no se pierde.
   */
  etiquetaCorta(p: ProcedenciaDeRegistro): string {
    switch (p.contextoOrigen) {
      case 'administrativo': return 'Institucional';
      case 'externo': return 'Organización';
      case 'importacion': return 'Importado';
      case 'siembra': return 'Sistema';
      default: return p.contextoEtiqueta;
    }
  }

  detalle(p: ProcedenciaDeRegistro): string {
    const partes = [p.contextoEtiqueta];
    if (p.organizacionProcedenciaNombre) { partes.push(`Procedencia: ${p.organizacionProcedenciaNombre}`); }
    if (p.usuarioCreadorNombre) { partes.push(`Creado por: ${p.usuarioCreadorNombre}`); }
    return partes.join(' · ');
  }
}
