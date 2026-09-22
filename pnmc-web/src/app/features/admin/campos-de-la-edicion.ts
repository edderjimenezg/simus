/**
 * LOS CAMPOS DE UNA EDICION, PARA QUE NADIE TENGA QUE ESCRIBIR SU IDENTIFICADOR.
 *
 * <b>QUE SUSTITUYE.</b> El panel de revisión de Ediciones pedía tres cosas por cada observación:
 * «Identificador del campo», «Etiqueta visible» y la instrucción. Es decir, le pedía a quien revisa
 * que recordara de memoria cómo se llama internamente un campo del formulario de la organización y
 * que volviera a escribir su rótulo. Dos de las tres son derivables de la tercera, y el proyecto
 * tiene fijado que <b>lo derivable no se pregunta</b> y que <b>un vocabulario controlado se captura
 * con una lista</b>, nunca con texto libre.
 *
 * <b>DE DONDE SALEN LOS ROTULOS.</b> De los rótulos reales del formulario de Edición que llena la
 * organización (`ficha-edicion-festival.component.html`), literalmente y en su orden. Si allí se
 * renombra un campo, aquí hay que renombrarlo: es lo que hace que la organización reconozca el
 * campo del que se le habla.
 *
 * <b>EL `campoId` NO ANCLA NADA TODAVIA.</b> La ficha de la organización muestra la observación por
 * su etiqueta, no la engancha al control. Se conserva igualmente porque es lo que permitirá
 * anclarla el día que se haga, y porque es lo que ya guarda el servidor.
 */
export interface CampoDeLaEdicion {
  readonly seccionId: string;
  readonly seccionEtiqueta: string;
  readonly campoId: string;
  readonly campoEtiqueta: string;
}

export const CAMPOS_DE_LA_EDICION: readonly CampoDeLaEdicion[] = [
  ...campos('identificacion', 'Identificación y operación', [
    ['anio', 'Año'],
    ['numeroEdicion', 'Número de Edición'],
    ['nombre', 'Nombre'],
    ['director', 'Director o directora'],
    ['fechaInicio', 'Fecha de inicio'],
    ['fechaFin', 'Fecha de cierre'],
    ['descripcion', 'Descripción'],
    ['estado', 'Estado operativo'],
  ]),
  ...campos('caracterizacion', 'Caracterización', [
    ['tipologiaFestivalId', 'Tipología'],
    ['otraTipologia', 'Otra tipología'],
    ['fuenteFinanciacionPrimariaId', 'Fuente de financiación principal'],
    ['otraFuenteFinanciacionPrimaria', 'Otra fuente principal'],
    ['fuenteFinanciacionSecundariaId', 'Fuente de financiación secundaria'],
    ['otraFuenteFinanciacionSecundaria', 'Otra fuente secundaria'],
    ['usaEstampillaProcultura', 'Usa estampilla Procultura'],
  ]),
  ...campos('practicas', 'Prácticas, expresiones y participación', [
    ['practicasMusicalesIds', 'Prácticas musicales'],
    ['territoriosSonorosIds', 'Territorios sonoros'],
    ['expresionesArtisticasIds', 'Expresiones artísticas'],
    ['modalidadesParticipacionIds', 'Modalidades de participación'],
    ['tiposIngresoIds', 'Tipos de ingreso'],
    ['practicasMusicalesQueCongrega', 'Prácticas musicales que congrega'],
    ['otraModalidadParticipacion', 'Otra modalidad de participación'],
    ['otraExpresionArtistica', 'Otra expresión artística'],
  ]),
  ...campos('localizaciones', 'Localizaciones', [
    ['localizaciones', 'Localizaciones'],
  ]),
  ...campos('aliadas', 'Entidades aliadas', [
    ['entidadesAliadas', 'Entidades aliadas'],
  ]),
  ...campos('materiales', 'Materiales', [
    ['materiales', 'Materiales'],
  ]),
];

function campos(
  seccionId: string,
  seccionEtiqueta: string,
  pares: readonly (readonly [string, string])[],
): CampoDeLaEdicion[] {
  return pares.map(([campoId, campoEtiqueta]) => ({ seccionId, seccionEtiqueta, campoId, campoEtiqueta }));
}

/** Las secciones en su orden, cada una con sus campos, para pintar un desplegable agrupado. */
export function seccionesDeLaEdicion(): { id: string; etiqueta: string; campos: CampoDeLaEdicion[] }[] {
  const porSeccion = new Map<string, { id: string; etiqueta: string; campos: CampoDeLaEdicion[] }>();
  for (const campo of CAMPOS_DE_LA_EDICION) {
    const grupo = porSeccion.get(campo.seccionId)
      ?? { id: campo.seccionId, etiqueta: campo.seccionEtiqueta, campos: [] };
    grupo.campos.push(campo);
    porSeccion.set(campo.seccionId, grupo);
  }
  return [...porSeccion.values()];
}

export function campoDeLaEdicion(campoId: string): CampoDeLaEdicion | undefined {
  return CAMPOS_DE_LA_EDICION.find(campo => campo.campoId === campoId);
}
