import { TonoDeEstado } from './indicador-de-estado.component';

/** El matiz del icono, cuando dos estados comparten tono y no significan lo mismo. */
export type MatizDeEstado = 'archivo' | 'pausa' | 'eliminado' | 'correo' | null;

/**
 * De qué tono es un estado, en un solo sitio para todo el proyecto.
 *
 * <b>POR QUE UNA FUNCION Y NO UN COLOR EN CADA PANTALLA.</b> Antes cada módulo escribía sus propias
 * clases de color para sus propias píldoras, y el mismo concepto acababa en verde en una pantalla y
 * en gris en otra. Aquí se decide una vez: «publicado» y «activa» son lo mismo —algo terminado y en
 * pie— aunque se llamen distinto porque el sujeto es distinto.
 *
 * <b>ADMITE LOS VOCABULARIOS DE LOS DOS LADOS.</b> El contrato externo nombra los estados de
 * Festival en PascalCase —`Publicado`, `AjustesSolicitados`— y la base en minúsculas con guion bajo.
 * Normaliza los dos en vez de obligar a cada llamada a decidir cuál trae.
 *
 * <b>LO QUE NO RECONOCE CAE EN `preliminar`</b>, que es el tono más neutro: un estado desconocido no
 * debería pintarse como un logro ni como un error.
 *
 * <b>CUBRE LOS SEIS VOCABULARIOS QUE CONVIVEN EN LA CONSOLA</b>, y no se unifican: cada módulo
 * conserva las palabras que le corresponden porque el sujeto es distinto. Lo único común es el tono.
 *
 * <ul>
 *   <li>Circuito editorial —Agenda, Noticias, Catálogo, Festival—: borrador · en revisión · ajustes
 *       solicitados · aprobado · publicado · rechazado · archivado.</li>
 *   <li>Ciclo de vida de organización: pendiente de confirmación · activa · inactiva · eliminada.</li>
 *   <li>Visibilidad de una Edición: borrador · publicada · archivada.</li>
 *   <li>Realización de una Edición: en preparación · programada · realizada · cancelada.</li>
 *   <li>Catalogación de una publicación: pendiente de revisión · en revisión · validada · observada.</li>
 *   <li>Importación de Festivales: previsualizado · aplicando · aplicado · depurando · expirado ·
 *       con conflicto.</li>
 * </ul>
 *
 * <b>AÑADIR UN ESTADO ES AÑADIR UN `case` AQUI</b>, no un color en una pantalla. Es justo lo que
 * evita que el mismo concepto acabe en verde en un módulo y en gris en otro.
 */
export function tonoDelEstado(codigo: string | null | undefined): TonoDeEstado {
  switch (normalizar(codigo)) {
    // ---- Terminado y en pie -----------------------------------------------------------------
    case 'publicado':
    case 'publicada':
    case 'activa':
    case 'activo':
    case 'aprobado':
    case 'aprobada':
    case 'validada':
    case 'realizada':
    case 'vigente':
    case 'aplicado':  // Importación de Festivales: el lote ya entró.
    case 'aplicada':
      return 'logrado';

    // ---- En curso, esperando a otro ----------------------------------------------------------
    case 'en_revision':
    case 'enviada':
    case 'enviado':
    case 'pendiente':
    case 'pendiente_de_confirmacion':
    case 'pendiente_revision':
    case 'programada':
    case 'requiere_aclaracion':
    // Importación de Festivales: entre la previsualización y la confirmación, y mientras el lote se
    // aplica o se depura. El comentario va aquí y no entre los `case` porque un comentario suelto
    // entre dos etiquetas vacías hace que `no-fallthrough` deje de reconocerlas como un solo bloque.
    case 'previsualizado':
    case 'aplicando':
    case 'depurando':
      return 'en_curso';

    // ---- Pide una acción de quien mira -------------------------------------------------------
    case 'ajustes_solicitados':
    case 'observada':
    case 'conflicto':
      return 'requiere_accion';

    // ---- Cerrado sin efecto ------------------------------------------------------------------
    case 'archivado':
    case 'archivada':
    case 'inactiva':
    case 'inactivo':
    case 'retirado':
    case 'retirada':
    case 'cerrada':
    case 'cerrado':
    case 'cancelada':
    case 'cancelado':
    case 'expirado':
    case 'expirada':
      return 'cerrado';

    // ---- Terminado en contra -----------------------------------------------------------------
    case 'rechazado':
    case 'rechazada':
    case 'eliminada':
    case 'eliminado':
    case 'fallida':
    case 'fallido':
      return 'adverso';

    // ---- Todavía no empieza ------------------------------------------------------------------
    default:
      return 'preliminar';
  }
}

/** El matiz que distingue estados del mismo tono que no significan lo mismo. */
export function matizDelEstado(codigo: string | null | undefined): MatizDeEstado {
  switch (normalizar(codigo)) {
    case 'archivado':
    case 'archivada':
      return 'archivo';
    // INACTIVA NO ES ARCHIVADA: una suspende y se deshace, la otra guarda el registro. Comparten
    // tono porque las dos dejan el registro fuera, y el icono es lo que las separa.
    case 'inactiva':
    case 'inactivo':
      return 'pausa';
    case 'eliminada':
      return 'eliminado';
    case 'pendiente_de_confirmacion':
      return 'correo';
    default:
      return null;
  }
}

/**
 * Normaliza el código.
 *
 * `AjustesSolicitados` -> `ajustes_solicitados`. El contrato externo de Festival usa PascalCase y la
 * base usa minúsculas: es la trampa que ya está documentada en `EstadosFestival`, y aquí solo hay
 * que leer los dos, no unificarlos.
 */
function normalizar(codigo: string | null | undefined): string {
  return (codigo ?? '')
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .toLowerCase();
}
