/**
 * Las secciones de la ficha del Festival.
 *
 * <b>SON SECCIONES, NO PASOS.</b> Crear, leer y editar comparten una sola vista: las mismas
 * secciones y en el mismo orden. No hay formulario por pasos con «Atrás» y «Siguiente».
 *
 * <b>QUÉ IMPLICA.</b> Leer y editar son la misma pantalla: las mismas secciones y en el mismo
 * orden. Lo único que cambia al pulsar «Editar el Festival» es que el valor deja de ser texto y
 * pasa a ser un campo, de modo que quien venía de leer no tiene que volver a encontrar dónde está
 * cada dato.
 *
 * <b>ERAN ONCE SECCIONES EN DOS BLOQUES, Y AHORA SON TRES.</b> Las otras ocho describían la
 * Edición y vivían en una pestaña que ningún montaje de `FichaFestivalComponent` llegaba a
 * mostrar: los cuatro forzaban la del Festival. Retiradas junto con
 * esa pestaña. La Edición tiene su propio formulario, `ficha-edicion-festival.component.ts`, con
 * las mismas ocho secciones y contra `/externo/ediciones`, que es el que sí se abre.
 *
 * Con un solo bloque desaparecen también `BloqueDeLaFicha`, `BLOQUES_DE_LA_FICHA` y
 * `pasosDelBloque()`: un reparto de dos lados sobre un lado solo no reparte nada.
 *
 * <b>DÓNDE ESCRIBE ESTO.</b> En `dbo.Festivales`, que es `ART_MUS_FESTIVALES` del volcado de
 * SIMUS: NOMBRE_FESTIVAL, DESCRIPCION_FESTIVAL y su bloque de contacto —CORREO_CONTACTO,
 * INSTAGRAM, FACEBOOK, PAGINA_WEB, OTRO_ENLACE, CELULAR y OBSERVACIONES_CONTACTO—. Esos datos no
 * se vuelven a pedir en la Edición.
 *
 * <b>UN FESTIVAL, VARIAS EDICIONES.</b> El Festival es la identidad estable. Sus contactos y su
 * organización administradora se diligencian una vez; una Edición solo conserva los datos propios,
 * como fechas, programación y dirección artística.
 *
 * <b>SOLO EL NOMBRE Y EL ALCANCE SON OBLIGATORIOS.</b> «Solo son obligatorios campos generales
 * para que el festival exista», del criterio.
 */

export interface SeccionDeLaFicha {
  /** Identificador estable. Se usa en `data-testid` y no cambia aunque cambie el rótulo. */
  readonly id: string;
  /** Lo que se lee en el encabezado de la sección, al leer y al editar. */
  readonly titulo: string;
  /** Una línea que dice qué se escribe aquí. */
  readonly ayuda: string;
}

/**
 * Las tres secciones del Festival, en orden.
 *
 * LOS TÍTULOS SON LOS DE LAS TABLAS Y COLUMNAS DEL VOLCADO, no traducciones libres. El usuario lo
 * pidió: «a los nombres de los campos no los cambies como vi que hiciste,
 * en por ejemplo "fuente de financiamiento"; déjalo tal cual».
 */
export const SECCIONES_DE_LA_FICHA: readonly SeccionDeLaFicha[] = [
  {
    id: 'generales',
    titulo: 'Datos generales',
    ayuda: 'Lo único obligatorio para que el Festival exista. Todo lo demás se puede completar después.',
  },
  {
    id: 'contacto-festival',
    titulo: 'Contacto del Festival',
    ayuda: 'Canales de contacto del Festival.',
  },
  {
    id: 'musica-festival',
    titulo: 'Prácticas y territorios',
    ayuda: 'Prácticas musicales y territorios sonoros relacionados con el Festival.',
  },
];

/**
 * Un paso: uno o dos grupos de campos bajo un mismo número.
 *
 * <b>QUÉ ES UN PASO AQUÍ, Y QUÉ NO.</b> No es una pantalla que haya que recorrer —eso se fue el 29
 * de agosto de 2026 con el formulario de doce pantallas—. Es un número y un encabezado que agrupan
 * lo que se contesta junto, con todos los pasos a la vista. Lo pidió el usuario ese mismo día
 * sobre los paneles ya unificados: «agrupa esto en 3 pasos» sobre los datos básicos.
 *
 * <b>POR QUÉ HACÍA FALTA.</b> Varios encabezados seguidos, todos del mismo tamaño, se leen como
 * una lista sin jerarquía. Con el número, la pantalla dice cuántas cosas se piden y por dónde va
 * cada una.
 *
 * <b>SIGUEN SIENDO UNA CAPA APARTE DE LAS SECCIONES</b> aunque hoy haya tres de cada y coincidan
 * uno a uno: un paso puede agrupar dos secciones —así estaban repartidas las ocho de la Edición— y
 * la plantilla, las pruebas y el aviso de validación hablan de pasos, no de secciones.
 */
export interface PasoDeLaFicha {
  /** Identificador estable. Se usa en `data-testid` y no cambia aunque cambie el rótulo. */
  readonly id: string;
  /** Lo que se lee en el encabezado del paso, junto a su número. */
  readonly titulo: string;
  /** Una línea que dice qué se contesta en este paso. */
  readonly ayuda: string;
  /** Los grupos de campos que caen dentro, en el orden en que se pintan. */
  readonly secciones: readonly string[];
}

/** Los tres pasos del Festival, uno por sección. */
export const PASOS_DE_LA_FICHA: readonly PasoDeLaFicha[] = [
  {
    id: 'generales',
    titulo: 'Datos generales',
    ayuda: 'Lo único obligatorio para que el Festival exista. Todo lo demás se puede completar después.',
    secciones: ['generales'],
  },
  {
    id: 'contacto-festival',
    titulo: 'Contacto del Festival',
    ayuda: 'Canales de contacto del Festival.',
    secciones: ['contacto-festival'],
  },
  {
    id: 'musica-festival',
    titulo: 'Prácticas y territorios',
    ayuda: 'Prácticas musicales y territorios sonoros relacionados con el Festival.',
    secciones: ['musica-festival'],
  },
];

/**
 * El paso al que pertenece un grupo de campos.
 *
 * LO NECESITA EL AVISO DE VALIDACIÓN. Con los pasos plegados por omisión, decir «revisa los campos
 * marcados en Datos generales» sin desplegar ese paso deja el aviso señalando a algo que no se
 * está viendo.
 */
export function pasoDeLaSeccion(seccionId: string): PasoDeLaFicha | undefined {
  return PASOS_DE_LA_FICHA.find(paso => paso.secciones.includes(seccionId));
}

/** El número de un paso, empezando en 1. Es lo que se lee junto a su encabezado. */
export function numeroDelPaso(id: string): number {
  return PASOS_DE_LA_FICHA.findIndex(uno => uno.id === id) + 1;
}

/**
 * Una sección por su identificador.
 *
 * DEVUELVE LA PRIMERA CUANDO EL IDENTIFICADOR NO EXISTE, y no `undefined`, para que la plantilla
 * pueda escribir `seccion('generales').titulo` sin un interrogante en cada uso. Un identificador
 * equivocado se ve en pantalla —el encabezado dice «Datos generales» donde no toca— en vez de
 * dejar el hueco en blanco.
 */
export function seccion(id: string): SeccionDeLaFicha {
  return SECCIONES_DE_LA_FICHA.find(una => una.id === id) ?? SECCIONES_DE_LA_FICHA[0];
}
