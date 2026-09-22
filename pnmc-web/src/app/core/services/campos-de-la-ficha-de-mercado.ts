import { Mercado } from './mercados.service';

/**
 * Los campos de la ficha de un mercado, agrupados como se leen en pantalla.
 *
 * <b>PARA QUE EXISTE ESTA LISTA.</b> La devolución del Programa se hace campo por campo: quien
 * revisa señala cuáles hay que corregir y escribe una nota en cada uno. Para eso hace falta saber
 * qué campos tiene un mercado, cómo se llaman en pantalla y qué dicen ahora mismo —el valor se
 * copia en la nota, porque cuando la organización lo corrija ese valor dejará de existir, justo
 * porque se pidió cambiarlo—.
 *
 * <b>UNA SOLA LISTA, NO UNA POR PANTALLA.</b> La consola la usa para ofrecer dónde pedir cambios y
 * la ficha de la organización para pintar cada nota al lado de su campo. Dos listas se separan en
 * cuanto se añada un campo, y la nota de un campo que una de las dos no conoce se queda sin sitio
 * donde pintarse.
 *
 * <b>LOS ROTULOS SON LOS DEL FORMULARIO</b>, no traducciones libres: quien recibe la nota tiene que
 * poder encontrar el campo, y lo encuentra por el rótulo que ve al editar.
 */
export interface CampoDeLaFichaDeMercado {
  /** Identificador estable. Viaja en la nota y no cambia aunque cambie el rótulo. */
  readonly id: string;
  /** Lo que se lee sobre el campo en el formulario. */
  readonly etiqueta: string;
  /** Qué dice el campo ahora mismo, para copiarlo en la nota como evidencia. */
  readonly valor: (mercado: Mercado) => string | null;
}

export interface SeccionDeLaFichaDeMercado {
  readonly id: string;
  readonly titulo: string;
  readonly campos: readonly CampoDeLaFichaDeMercado[];
}

/** Lo que se enseña cuando un campo está vacío. No es un valor: es la ausencia de uno. */
const SIN_DATO = null;

function texto(valor: string | null | undefined): string | null {
  return valor && valor.trim().length > 0 ? valor.trim() : SIN_DATO;
}

export const SECCIONES_DE_LA_FICHA_DE_MERCADO: readonly SeccionDeLaFichaDeMercado[] = [
  {
    id: 'identificacion',
    titulo: 'Identificación',
    campos: [
      { id: 'nombre', etiqueta: 'Nombre del mercado', valor: m => texto(m.nombre) },
      { id: 'descripcion', etiqueta: 'Descripción', valor: m => texto(m.descripcion) },
      { id: 'alcance', etiqueta: 'Alcance', valor: m => texto(m.alcance) },
      { id: 'modalidad', etiqueta: 'Modalidad', valor: m => texto(m.modalidad) },
      { id: 'periodicidad', etiqueta: 'Periodicidad', valor: m => texto(m.periodicidad) },
    ],
  },
  {
    id: 'territorio',
    titulo: 'Dónde se realiza',
    campos: [
      { id: 'nivelCobertura', etiqueta: 'Nivel de cobertura', valor: m => texto(m.nivelCobertura) },
      {
        id: 'territorio',
        etiqueta: 'Departamento y municipio',
        // EL MUNICIPIO SOLO NO UBICA: hay municipios con el mismo nombre en departamentos distintos.
        valor: m => texto([m.nombreMunicipio, m.nombreDepartamento].filter(Boolean).join(', ')),
      },
      { id: 'lugarEspecifico', etiqueta: 'Lugar específico', valor: m => texto(m.lugarEspecifico) },
    ],
  },
  {
    id: 'contacto',
    titulo: 'Contacto',
    campos: [
      { id: 'correoMercado', etiqueta: 'Correo de contacto', valor: m => texto(m.correoMercado) },
      { id: 'telefonoMercado', etiqueta: 'Teléfono', valor: m => texto(m.telefonoMercado) },
      { id: 'sitioWebMercado', etiqueta: 'Página web', valor: m => texto(m.sitioWebMercado) },
    ],
  },
  {
    id: 'relaciones',
    titulo: 'Relaciones con el Ecosistema',
    campos: [
      {
        id: 'festival',
        etiqueta: 'En el marco de',
        valor: m => (m.seRealizaEnElMarcoDeUnFestival ? texto(m.festivalNombre) : 'Proceso independiente'),
      },
      {
        id: 'practicasMusicales',
        etiqueta: 'Prácticas musicales',
        valor: m => texto((m.practicasMusicales ?? []).map(x => x.nombre).join(', ')),
      },
      {
        id: 'territoriosSonoros',
        etiqueta: 'Territorios sonoros',
        valor: m => texto((m.territoriosSonoros ?? []).map(x => x.nombre).join(', ')),
      },
    ],
  },
];

/** Todos los campos, en orden, para buscar uno por su identificador sin recorrer las secciones. */
export const CAMPOS_DE_LA_FICHA_DE_MERCADO: readonly CampoDeLaFichaDeMercado[] =
  SECCIONES_DE_LA_FICHA_DE_MERCADO.flatMap(seccion => seccion.campos);
