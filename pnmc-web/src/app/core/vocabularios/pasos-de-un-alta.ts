import { PasoDelIndicador } from '../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';

/**
 * Los pasos en que se pregunta por un proceso del Ecosistema.
 *
 * <b>SON EL ORDEN DE `lenguaje-visual-de-la-consola.md` §5 bis, hecho pasos.</b> El criterio dice que
 * se pregunta en el orden en que alguien sabe la respuesta —qué es, cada cuánto, dónde, cómo se
 * contacta, con qué se relaciona— y que ese orden es el mismo en las tres pantallas del proceso. El
 * asistente del Festival no lo cumplía: pedía el correo y las redes en el paso 2, antes del
 * territorio, así que quien registraba saltaba de «cada cuánto ocurre» a «cuál es su Instagram» y
 * volvía después a decir en qué municipio.
 *
 * <b>EL RESPONSABLE VA PRIMERO Y NO ES UNA EXCEPCION AL ORDEN:</b> no forma parte de la entrevista
 * sobre el proceso, es la pregunta previa —quién responde por él—, y además acota lo que se puede
 * elegir después.
 *
 * <b>Y LA REVISION NO PREGUNTA NADA:</b> enseña lo que se va a crear y con qué procedencia, que es
 * lo último que alguien quiere confirmar antes de que el registro exista.
 */
export const PASOS_DE_UN_ALTA: readonly PasoDelIndicador[] = [
  { id: 1, titulo: 'Responsable' },
  { id: 2, titulo: 'Qué es' },
  { id: 3, titulo: 'Cuándo y dónde' },
  { id: 4, titulo: 'Contacto y vínculos' },
  { id: 5, titulo: 'Revisión' },
];
