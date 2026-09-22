import { ElementoDeClasificacion } from '../services/clasificacion-de-contenido.service';
import { ProcedenciaDeRegistro } from '../services/procedencia';

/**
 * Los contratos del Catálogo Editorial: lo que viaja entre el API y la pantalla.
 *
 * <b>VIVEN EN SU PROPIO FICHERO, Y NO DENTRO DEL SERVICIO, POR UNA RAZON CONCRETA.</b> Estaban
 * declarados en `catalogo-editorial.service.ts`, así que el adaptador del diseño tenía que importar
 * del servicio para conocer las formas. En cuanto el servicio pasó a necesitar el adaptador —al
 * recuperar la lectura del portal, que estaba en un servicio de mapas en inglés— los dos ficheros
 * quedaron importándose el uno al otro. Un ciclo entre módulos con valores en tiempo de ejecución
 * no falla al compilar: falla al arrancar, y en el sitio equivocado.
 *
 * Aquí conviven <b>dos contratos deliberadamente distintos</b>: el de la consola, que lo lleva todo,
 * y el público, que lleva solo lo que se publica. No se unifican: con un solo contrato, publicar un
 * campo nuevo no cuesta nada y no lo decide nadie.
 */
/**
 * El vocabulario de tipo de agente, tal como lo emite el API.
 *
 * Lo fijan a la vez `CatalogoEditorialContrato.TiposAgente` y
 * `CK_AgentesEditoriales_Tipo CHECK (Tipo IN (N'persona', N'entidad'))`. Va en minúscula.
 */
export type TipoDeAgenteEditorial = 'persona' | 'entidad';

export interface DerechosEditoriales {
  estado: string;
  permitePublicarFicha: boolean;
  permitePublicarArchivo: boolean;
  licenciaONota: string | null;
  fuenteId: number | null;
  fechaVerificacion: string | null;
  verificadoPor: string | null;
}

export interface FuenteEditorial {
  id: number;
  nombre: string;
  referencia: string | null;
  url: string | null;
  fechaFuente: string | null;
  fechaConsulta: string;
  verificadaPor: string;
}

export interface CreditoEditorial {
  id: number;
  agenteId: number;
  agenteNombre: string;
  /** Persona o Entidad. El diseño aprobado separa «Autor» de «Autor corporativo». */
  /** El mismo vocabulario en minúscula que en el contrato público. Ver `TipoDeAgenteEditorial`. */
  agenteTipo: TipoDeAgenteEditorial;
  rolCodigo: string;
  rolEtiqueta: string;
  principal: boolean;
  orden: number;
}

export interface IdentificadorEditorial {
  id: number;
  esquema: string;
  codigoRecibido: string;
  cualificador: string | null;
  valido: boolean | null;
  observacionValidacion: string | null;
}

export interface AccesoEditorial {
  id: number;
  tipo: string;
  archivoId: number | null;
  url: string | null;
  ubicacionFisica: string | null;
  etiqueta: string | null;
  nota: string | null;
  orden: number;
  derechos: DerechosEditoriales;
}

/**
 * Un término de tipología: de qué está hecha la publicación.
 *
 * CUATRO EJES INDEPENDIENTES —`recurso` (Dublin Core / DCMI), `contenido` (RDA 336), `medio`
 * (RDA 337) y `soporte` (RDA 338)— y cada uno admite VARIOS valores: un libro con su CD declara
 * dos soportes. Viaja con su `norma` para que la ficha pueda citarla sin codificarla en la
 * plantilla.
 */
export interface TipologiaEditorial {
  eje: 'recurso' | 'contenido' | 'medio' | 'soporte';
  codigo: string;
  etiqueta: string;
  norma: string;
}

export interface PublicacionEditorial {
  id: number;
  codigo: string;
  titulo: string;
  subtitulo: string | null;
  designacionVolumen: string | null;
  serieOColeccion: string | null;
  resumen: string | null;
  fechaEdtf: string | null;
  anioInicio: number | null;
  anioFin: number | null;
  idioma: string | null;
  /** Lo que la fuente escribió cuando no pudo fijar la fecha. */
  notaFecha: string | null;
  /** Descriptores de presentación. Texto libre: sus vocabularios siguen TBC (). */
  tipoPublicacion: string | null;
  /** La categoría temática administrable. Es lo que se guarda. */
  categoriaId: number | null;
  /** Su nombre, resuelto por el servidor. Solo lectura. */
  categoria: string | null;
  ambito: string | null;
  /** El ámbito escrito a mano cuando no entra en el vocabulario. */
  ambitoTexto: string | null;
  /** Físico, Digital o Mixto. Dato propio: NO se deduce del soporte, se comprobó. */
  formato: string | null;
  /** La segunda práctica, cuando la obra toca dos. */
  categoriaSecundaria: string | null;
  palabrasClave: string[];
  miniaturaRuta: string | null;
  /** Los nueve que el diseño aprobado necesita para llenar sus tarjetas de detalle. */
  seccionPrincipal: string | null;
  rutaSeccion: string | null;
  practicaMusical: string | null;
  subcategoria: string | null;
  tamanoFormato: string | null;
  paginas: string | null;
  duracion: string | null;
  camposAdicionales: string | null;
  textoPortada: string | null;
  /** Las cuatro facetas RDA/DCMI de la publicación. */
  tipologia: TipologiaEditorial[];
  /** Cuánta confianza da la ficha de origen: Alta, Media o Baja. Ordena la cola de catalogación. */
  confianza: string | null;
  revisarClasificacion: boolean;
  revisarCreditos: boolean;
  /** La nota de quien catalogó. */
  notasCatalogacion: string | null;
  /** De qué lámina del catálogo original salió la ficha. Es su procedencia. */
  diapositivaOrigen: string | null;
  estadoCatalogacion: string;
  estadoPublicacion: string;
  version: number;
  derechos: DerechosEditoriales;
  fuentes: FuenteEditorial[];
  creditos: CreditoEditorial[];
  identificadores: IdentificadorEditorial[];
  accesos: AccesoEditorial[];
  programas: unknown[];
  /** Clasificación opcional contra los catálogos del sistema. Puede venir vacía. */
  practicasMusicales: ElementoDeClasificacion[];
  territoriosSonoros: ElementoDeClasificacion[];
  /** De dónde vino el registro. Nulo en lo anterior a la tabla de procedencia. */
  procedencia: ProcedenciaDeRegistro | null;
  fechaActualizacion: string;
}

/**
 * La ficha tal como la publica el catálogo, que NO es la de la consola.
 *
 * <b>EL CRITERIO ES EL DE UNA BIBLIOTECA</b>, y lo fijó la dirección de producto: se publica lo que
 * sirve para identificar, encontrar y conseguir una obra, y nada del trabajo de catalogarla. Fuera
 * quedan los estados, la versión, la confianza de la ficha, las banderas de revisión, las notas de
 * catalogación, la procedencia y todo nombre de funcionario. El porqué de cada exclusión está en
 * `CatalogoEditorialContratos.cs`, sobre `PublicacionEditorialPublicaDto`.
 *
 * <b>ES UN TIPO APARTE A PROPOSITO.</b> Compartir el de la consola es lo que hacía que el portal
 * recibiera campos internos sin que nadie lo decidiera.
 */
export interface CreditoPublico {
  agenteId: number;
  nombre: string;
  /**
   * Persona o entidad: un catálogo distingue autor de autor corporativo.
   *
   * <b>VA COMO UNION Y NO COMO `string` PARA QUE EL COMPILADOR LO VIGILE.</b> Declarado como
   * `string`, el adaptador pudo comparar durante todo el módulo contra `'Entidad'` capitalizado
   * —un valor que el API no emite nunca— y nadie se enteró: la autoría corporativa no se separó
   * jamás. El vocabulario es el del contrato (`CatalogoEditorialContrato.TiposAgente`) y el del
   * `CHECK` de la tabla, los dos en minúscula. Con la unión escrita, una comparación imposible es
   * un error de compilación y no un hueco silencioso en la pantalla.
   */
  tipo: TipoDeAgenteEditorial;
  /** El código MARC Relators, que es lo que hace citable el papel. */
  rolCodigo: string;
  rol: string;
  principal: boolean;
}

export interface IdentificadorPublico { esquema: string; codigo: string; cualificador: string | null }

export interface AccesoPublico {
  tipo: string;
  url: string | null;
  ubicacionFisica: string | null;
  etiqueta: string | null;
  nota: string | null;
}

export interface PublicacionEditorialPublica {
  /** La signatura: es como se cita y como se enlaza. No hay identificador numérico. */
  codigo: string;
  titulo: string;
  subtitulo: string | null;
  designacionVolumen: string | null;
  serieOColeccion: string | null;
  resumen: string | null;
  fechaEdtf: string | null;
  anioInicio: number | null;
  anioFin: number | null;
  notaFecha: string | null;
  idioma: string | null;
  tipoPublicacion: string | null;
  formato: string | null;
  tipologia: TipologiaEditorial[];
  tamanoFormato: string | null;
  paginas: string | null;
  duracion: string | null;
  categoria: string | null;
  categoriaSecundaria: string | null;
  practicaMusical: string | null;
  subcategoria: string | null;
  palabrasClave: string[];
  ambito: string | null;
  ambitoTexto: string | null;
  seccionPrincipal: string | null;
  rutaSeccion: string | null;
  miniaturaRuta: string | null;
  textoPortada: string | null;
  creditos: CreditoPublico[];
  identificadores: IdentificadorPublico[];
  accesos: AccesoPublico[];
  /** La licencia o la nota de derechos. Sin quién la verificó ni cuándo. */
  licencia: string | null;
  practicasMusicales: ElementoDeClasificacion[];
  territoriosSonoros: ElementoDeClasificacion[];
  fechaActualizacion: string;
}

export interface PaginaPublicacionesPublicas {
  items: PublicacionEditorialPublica[];
  pagina: number;
  tamano: number;
  total: number;
  totalPaginas: number;
}

/**
 * Una entrada del hilo de trabajo de una ficha: una decisión, o una anotación sobre ella.
 *
 * <b>DECISIONES Y ANOTACIONES EN UN SOLO ORDEN.</b> No hay una lista de comentarios aparte: una
 * anotación es una entrada más con `accion = 'Anotacion'` y el estado sin moverse, así que lo que
 * alguien escribió y la decisión que comenta se leen juntos. Es el mismo hilo que usa el circuito
 * de Festivales.
 */
export interface AnotacionEditorial {
  id: number;
  /** `CatalogacionCambiada`, `PublicacionCambiada` o `Anotacion`. */
  accion: string;
  estadoAnterior: string | null;
  estadoNuevo: string;
  comentario: string | null;
  usuarioId: number | null;
  /** Quién, en letra: un historial que dice «usuario 12» obliga a ir a buscar quién es. */
  usuarioNombre: string | null;
  fecha: string;
}

export interface PaginaPublicaciones {
  items: PublicacionEditorial[];
  pagina: number;
  tamano: number;
  total: number;
  totalPaginas: number;
}

/** Las etiquetas que ve una persona. El código vive en el servidor; aquí solo se presenta. */
export const ETIQUETAS_CATALOGACION: Record<string, string> = {
  pendiente_revision: 'Pendiente de revisión',
  en_revision: 'En revisión',
  validada: 'Validada',
  observada: 'Observada',
};

export const ETIQUETAS_PUBLICACION: Record<string, string> = {
  borrador: 'Borrador',
  en_revision: 'En revisión',
  publicado: 'Publicado',
  retirado: 'Retirado',
};

/** El mismo sobre que usa el resto de la consola: éxito con datos, o fallo con un motivo legible. */
export interface ResultadoEditorial<T> {
  ok: boolean;
  data?: T | null;
  error?: string;
}


/**
 * Un término de una lista, con cuántas fichas lo usan.
 *
 * EL RECUENTO ORDENA Y DELATA. Con 56 «tipos de publicación» distintos para 171 fichas, ver que
 * «Libro» tiene 12 usos y «Libro impreso» 14 es lo que permite elegir el término bueno en vez de
 * inventar un tercero.
 */
export interface TerminoDeVocabulario { valor: string; usos: number }

export interface RolEditorial { codigo: string; etiqueta: string; usos: number }

/**
 * Las listas con las que se rellena el formulario de una publicación.
 *
 * SALEN DEL ACERVO, NO DE UNA LISTA ESCRITA A MANO, por la misma razón que DIVIPOLA se lee de su
 * fuente: una lista fija en el código se desincroniza en cuanto alguien cataloga algo nuevo.
 */
/**
 * Un idioma: su código ISO 639 y cómo se lee.
 *
 * `(lengua nativa)` no es un código ISO y viaja igual: es lo que escribió la fuente en cinco fichas
 * bilingües cuya segunda lengua no identifica. Decir que la obra es bilingüe vale más que callarlo
 * por no tener el código, y queda marcado como pendiente de precisar.
 */
/** Un sitio del acervo. */
export interface RutaDelAcervo { ruta: string; seccion: string; usos: number }

export interface IdiomaEditorial {
  codigo: string;
  nombre: string;
  usos: number;
  pendienteDePrecisar: boolean;
}

export interface VocabulariosEditoriales {
  /** El código que le tocaría a la siguiente publicación, por orden de registro. */
  siguienteCodigo: string;
  idiomas: IdiomaEditorial[];
  tiposDePublicacion: TerminoDeVocabulario[];
  ambitos: TerminoDeVocabulario[];
  formatos: TerminoDeVocabulario[];
  /**
   * Los sitios del acervo: la ruta completa con su sección al frente.
   *
   * ES UNA TAXONOMIA PROPIA Y CERRADA —22 rutas para 9 secciones—, no la suma de las categorías: la
   * subcategoría aparece en la ruta en 17 de 170 fichas y la categoría secundaria en ninguna. Lo que
   * sí se deriva es la sección, que es el primer tramo, y lo es en las 170.
   */
  rutas: RutaDelAcervo[];
  practicasMusicales: TerminoDeVocabulario[];
  subcategorias: TerminoDeVocabulario[];
  categoriasSecundarias: TerminoDeVocabulario[];
  esquemasIdentificador: TerminoDeVocabulario[];
  rolesDeCredito: RolEditorial[];
  tipologias: TipologiaEditorial[];
  tiposDeAgente: TipoDeAgenteEditorial[];
}

/** Un crédito tal como se escribe en el formulario: a quién y en qué papel. */
export interface CreditoEnFormulario {
  nombre: string;
  tipo: TipoDeAgenteEditorial;
  rolCodigo: string;
  rolEtiqueta: string;
  principal: boolean;
}

export interface IdentificadorEnFormulario { esquema: string; codigo: string; cualificador: string }

export interface AccesoEnFormulario {
  tipo: 'enlace' | 'ubicacion' | 'archivo';
  url: string;
  ubicacionFisica: string;
  etiqueta: string;
  nota: string;
  archivoId: number | null;
}
