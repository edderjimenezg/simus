/**
 * De dónde vino un registro, tal como lo devuelve el API.
 *
 * <b>TRES DIMENSIONES QUE NO SE MEZCLAN.</b> La procedencia dice qué entidad incorporó el registro
 * al sistema; el usuario, qué cuenta ejecutó la acción; y la organización responsable —que vive en
 * el propio registro, no aquí— quién lo gestiona de verdad. Que el PNMC haya registrado un
 * festival no lo convierte en su organización responsable.
 *
 * <b>VIVE EN SU PROPIO FICHERO</b> porque la usan Agenda, Noticias, Catálogo Editorial,
 * Organizaciones y Festivales. Declararla en el servicio de uno de ellos obligaría a los demás a
 * importar de un módulo con el que no tienen nada que ver.
 */
export interface ProcedenciaDeRegistro {
  /** administrativo · externo · importacion · siembra. */
  contextoOrigen: string;
  /** Cómo se lee ese contexto en pantalla. Lo decide el servidor. */
  contextoEtiqueta: string;
  /** `true` cuando el registro entró por la consola institucional. */
  esInstitucional: boolean;
  organizacionProcedenciaId: number | null;
  organizacionProcedenciaNombre: string | null;
  usuarioCreadorId: number | null;
  usuarioCreadorNombre: string | null;
  fechaRegistro: string;
}
