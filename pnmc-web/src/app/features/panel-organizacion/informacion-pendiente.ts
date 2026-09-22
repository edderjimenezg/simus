import { PerfilOrganizacion } from './panel-organizacion.api';

/**
 * Qué información opcional le falta a una organización.
 *
 * <b>EL §15.3 PIDE ALGO CONCRETO Y PROHIBE ALGO CONCRETO.</b> Pide decir «Tu organización tiene
 * información pendiente por completar» o, mejor, nombrarlo: «Completa teléfono y sitio web para
 * mejorar tu información». Y prohíbe expresamente lo contrario: «no introducir porcentajes o
 * gamificación salvo que aporten valor real». Un «perfil al 60 %» no le dice a nadie qué escribir;
 * «te falta el teléfono» sí.
 *
 * <b>VIVE APARTE PORQUE LO USAN DOS PANTALLAS.</b> El indicador permanente del Resumen (§15.3) y la
 * bienvenida del primer ingreso (§15.2) responden a la misma pregunta. Si cada una la contestara
 * por su cuenta, acabarían discrepando: la bienvenida invitaría a completar algo que el indicador
 * ya da por puesto.
 */

/** Un dato que la organización puede aportar y hoy no consta. */
export interface DatoPendiente {
  /** La clave del campo en el perfil, para poder llevar el foco hasta él. */
  campo: keyof PerfilOrganizacion;
  /** Cómo se nombra en una frase: «Completa el teléfono y el sitio web». */
  etiqueta: string;
}

/**
 * Los datos opcionales que se invita a completar, en el orden en que se piden.
 *
 * <b>NINGUNO ES OBLIGATORIO, Y ESA ES LA CONDICION PARA ESTAR AQUI.</b> Lo obligatorio lo exige el
 * formulario de alta y no se puede «completar después»: si faltara, la organización no existiría.
 * Lo que se lista aquí es lo que el §15.2 llama «otros datos opcionales», y por eso la invitación
 * siempre admite un «Completar después».
 *
 * <b>EL ORDEN NO ES ALFABETICO: ES EL DE UTILIDAD.</b> Primero lo que permite que alguien se ponga
 * en contacto —teléfono, dirección—, después lo que explica quién es —descripción—, y al final la
 * presencia en la red. Quien solo vaya a rellenar dos campos debería rellenar los dos primeros.
 */
const DATOS_OPCIONALES: readonly DatoPendiente[] = [
  { campo: 'telefonoContacto', etiqueta: 'el teléfono' },
  { campo: 'direccion', etiqueta: 'la dirección' },
  { campo: 'descripcion', etiqueta: 'la descripción' },
  { campo: 'sitioWeb', etiqueta: 'el sitio web' },
  { campo: 'instagram', etiqueta: 'Instagram' },
  { campo: 'facebook', etiqueta: 'Facebook' },
];

/**
 * Qué le falta a esta organización, en orden de utilidad.
 *
 * Un campo con espacios en blanco cuenta como vacío: guardar un espacio no es aportar un teléfono.
 */
export function datosPendientes(perfil: PerfilOrganizacion | null): DatoPendiente[] {
  if (!perfil) return [];
  return DATOS_OPCIONALES.filter(dato => !(perfil[dato.campo] ?? '').toString().trim());
}

/**
 * La frase que se enseña, ya construida.
 *
 * <b>NOMBRA HASTA DOS Y LUEGO RESUME.</b> «Completa el teléfono, la dirección, la descripción, el
 * sitio web, Instagram y Facebook» es una lista que nadie lee y que además desanima: parece que
 * falta todo. Dos son los que de verdad se van a rellenar de una sentada, y el resto se cuenta.
 *
 * Devuelve cadena vacía si no falta nada, para que la pantalla no tenga que decidirlo otra vez.
 */
export function frasePendiente(perfil: PerfilOrganizacion | null): string {
  const pendientes = datosPendientes(perfil);
  if (pendientes.length === 0) return '';

  const primeros = pendientes.slice(0, 2).map(dato => dato.etiqueta);
  const resto = pendientes.length - primeros.length;

  const lista = primeros.length === 2 ? `${primeros[0]} y ${primeros[1]}` : primeros[0];
  if (resto === 0) return `Completa ${lista} para mejorar tu información.`;
  return `Completa ${lista} y ${resto} ${resto === 1 ? 'dato más' : 'datos más'} para mejorar tu información.`;
}
