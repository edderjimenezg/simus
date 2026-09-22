import { Noticia } from './noticias.service';
import { EventoAgenda } from './agenda.service';
import { AccesoPublico, CreditoPublico, IdentificadorPublico, PublicacionEditorialPublica, TipologiaEditorial } from '../contratos/catalogo-editorial';

/**
 * De nuestro modelo a la forma que esperan los diseños aprobados.
 *
 * <b>POR QUE EXISTE ESTE FICHERO.</b> Los diseños de Catálogo Editorial, Agenda y Noticias están
 * aprobados tal como están en el diseño aprobado del portal, y la instrucción es reproducirlos EXACTAMENTE. Aquellas
 * plantillas nombran los campos con la forma del desarrollo donde nacieron —`item.section`,
 * `item.d`, `item.desc`—, que venía de tablas planas y de endpoints heredados.
 *
 * <b>ASI QUE LA TRADUCCION VIVE AQUI Y NO EN LAS PLANTILLAS.</b> Las plantillas quedan byte a byte
 * como el diseño aprobado; lo único que cambia es de dónde salen sus datos. Cuando haya que
 * retocar el diseño, se retoca el diseño; cuando cambie el modelo, se retoca este fichero. Mezclar
 * las dos cosas en la plantilla es lo que convierte un rediseño en una arqueología.
 *
 * <b>NINGUN CAMPO SE INVENTA.</b> Lo que el modelo no tiene se queda vacío, y la plantilla ya sabe
 * ocultar lo vacío: sus tarjetas filtran por valor antes de dibujarse. Rellenar un hueco con texto
 * de relleno es exactamente lo que hacía la versión falsamente conectada.
 */

/** La forma que consume la plantilla del catálogo editorial. */
export interface RecursoEditorialDeDiseno {
  id: string;
  title: string;
  year: string;
  section: string;
  sectionPath: string;
  publicationType: string;
  practice: string;
  category: string;
  subcategory: string;
  regionalScope: string;
  author: string;
  corporateAuthor: string;
  /** Los créditos que no son la autoría principal, en una línea. Lo que enseña el diseño. */
  additionalCredits: string;
  displayAuthor: string;
  isbn: string;
  ismn: string;
  formatSize: string;
  pages: string;
  duration: string;
  // `location` y `url` YA NO EXISTEN. Eran una proyección aplanada de `access`: la misma
  // información en dos formas, unida con «; ». Con dos representaciones de lo mismo la pantalla
  // acabó enseñando la ubicación dos veces con rótulos distintos, y el botón de las tres fichas con
  // varios enlaces abría «url1; url2», que no es ninguna dirección. La lista manda.
  summary: string;
  // `additionalFields` YA NO EXISTE. Era el cajón de sastre que traían los Excel de referencia, y
  // el contrato público ni siquiera lo envía: llegaba siempre vacío y solo engordaba la cadena de
  // búsqueda. La columna `CamposAdicionales` tiene CERO filas en las 171 publicaciones.
  coverText: string;
  thumbnail: string;
  keywords: string[];

  // ── LO QUE UNA FICHA BIBLIOGRAFICA NECESITA Y UNA CADENA NO PUEDE LLEVAR ──
  //
  // El diseño aprobado aplana todo a texto, y para lo que enseña —cuatro tarjetas de pares
  // rótulo/valor— basta. Pero hay tres cosas que NO son un par: los créditos, que son una lista de
  // personas con su papel; los identificadores, que son varios y se distinguen por su cualificador
  // —PNMC-ED-103 trae CINCO ISBN: PDF, EPUB, HTML, iBook y MOBI, y sin el cualificador serían cinco
  // líneas iguales—; y los accesos, que son varias vías con su tipo. Aplanarlos los destruye.
  subtitle: string;
  volume: string;
  series: string;
  language: string;
  edtfDate: string;
  dateNote: string;
  secondaryCategory: string;
  scopeText: string;
  mediaFormat: string;
  licence: string;
  /** Las cuatro facetas RDA/DCMI, con su norma. */
  typology: TipologiaEditorial[];
  /** Una entrada por agente y papel. El papel viaja en letra, no en código. */
  credits: CreditoPublico[];
  /** Uno por identificador real de la obra, con su cualificador. */
  identifiers: IdentificadorPublico[];
  /** Las vías de acceso, cada una con su tipo. */
  access: AccesoPublico[];
}

/**
 * El año tal como lo enseña la ficha.
 *
 * UN SOLO AÑO CUANDO INICIO Y FIN COINCIDEN. El modelo guarda los dos por separado porque el
 * acervo trae obras de varios años; repetirlo —«2016 – 2016»— delata que nadie miró el caso común.
 */
function anioLegible(publicacion: PublicacionEditorialPublica): string {
  const { anioInicio, anioFin } = publicacion;
  if (anioInicio && anioFin && anioInicio !== anioFin) { return `${anioInicio} – ${anioFin}`; }
  return String(anioInicio ?? anioFin ?? '');
}

/**
 * Junta varios valores en una línea, sin repetirlos.
 *
 * <b>SEPARA CON PUNTO Y COMA Y NO CON COMA, y eso está medido.</b> Nueve de los nombres del acervo
 * llevan una coma dentro —«Franco, Luis Fernando», y las entidades con su forma completa—, así que
 * unir con coma produce una lista en la que no se sabe dónde acaba un nombre y empieza el
 * siguiente. Ninguno lleva punto y coma. Es además la separación que usa ISBD entre menciones de
 * responsabilidad distintas.
 */
const unir = (valores: (string | null | undefined)[]): string =>
  [...new Set(valores.map(v => (v ?? '').trim()).filter(Boolean))].join('; ');

/**
 * <b>PARTE DEL CONTRATO PUBLICO, NO DEL DE LA CONSOLA.</b> Esta función alimenta la pantalla del
 * portal, así que su entrada tiene que ser exactamente lo que el portal recibe. Mientras aceptó el
 * tipo de la consola, el servicio que la llamaba declaraba esa forma sobre una respuesta que ya no
 * la tenía: una mentira que el compilador no podía ver.
 */
/**
 * Una mención de responsabilidad legible: hasta tres nombres, y cuántos quedan fuera.
 *
 * NO ES LA LISTA DE CREDITOS, que vive entera en la ficha y agrupada por papel. Esto es con qué se
 * cita la obra de un vistazo, en una ranura de una línea.
 */
function acotar(nombres: string[], cuantosCaben = 3): string {
  const unicos = [...new Set(nombres.map(n => (n ?? '').trim()).filter(Boolean))];
  if (unicos.length <= cuantosCaben) { return unicos.join('; '); }
  const restantes = unicos.length - cuantosCaben;
  return `${unicos.slice(0, cuantosCaben).join('; ')} y ${restantes} más`;
}

export function aRecursoEditorialDeDiseno(publicacion: PublicacionEditorialPublica): RecursoEditorialDeDiseno {
  const creditos = publicacion.creditos ?? [];

  // LA AUTORIA SALE DE LOS CREDITOS, que es donde vive de verdad desde una revisión anterior. El diseño
  // separa persona de entidad —«Autor» y «Autor corporativo»— y el tipo de agente lo dice el API.
  //
  // SE COMPARA SIN MAYUSCULAS, Y ESO ARREGLA UN DEFECTO QUE LLEVABA AQUI DESDE EL PRINCIPIO. El
  // API devuelve `persona` y `entidad` en minúscula —así los fija `CK_AgentesEditoriales_Tipo`— y
  // aquí se comparaba con `'Entidad'`. La comparación NUNCA se cumplía: `entidades` salía siempre
  // vacía, así que la fila «Autor corporativo» de la Ficha Bibliográfica no se ha visto jamás en
  // ninguna publicación del catálogo, y toda entidad acreditada se contaba como persona.
  //
  // LA PRUEBA NO LO ATRAPO PORQUE ESCRIBIA EL MISMO VALOR EQUIVOCADO en su ficha de ejemplo: medía
  // que el código hacía lo que el código hacía, no lo que el API devuelve. Ahora usa los valores
  // reales.
  const esEntidad = (tipo: string) => (tipo ?? '').trim().toLowerCase() === 'entidad';
  const personas = creditos.filter(c => c.principal && !esEntidad(c.tipo));
  const entidades = creditos.filter(c => c.principal && esEntidad(c.tipo));
  const secundarios = creditos.filter(c => !c.principal);

  const identificador = (esquema: string) =>
    unir(publicacion.identificadores?.filter(i => i.esquema === esquema).map(i => i.codigo) ?? []);

  const autor = unir(personas.map(c => c.nombre));
  const corporativo = unir(entidades.map(c => c.nombre));

  /*
   * LA AUTORIA VISIBLE NO PUEDE DEPENDER DE QUE ALGUIEN MARCARA «PRINCIPAL».
   *
   * Medido sobre el acervo real: de las cien primeras publicaciones, CUARENTA Y CUATRO tienen
   * créditos y NINGUNO marcado como principal —«8 Arreglos para Banda» acredita al Ministerio de
   * Cultura y a la Dirección de Artes, las dos como autoría corporativa no principal—. Con la
   * autoría sacada solo de los principales, esas cuarenta y cuatro salían en el mosaico SIN AUTOR,
   * con el hueco donde debería estar el nombre. No es que no lo tengan: es que la fuente no eligió
   * uno.
   *
   * SE CAE HACIA LO QUE HAY, EN EL ORDEN EN QUE LO LEE UNA FICHA: la persona principal, la entidad
   * principal, la entidad que haya —que es lo que un catálogo pone cuando la obra es colectiva— y,
   * solo al final, las personas acreditadas.
   *
   * Y SE ACOTA, QUE ES LA MITAD QUE FALTABA. La primera versión de este respaldo caía hacia TODOS
   * los créditos, y en `PNMC-ED-010` —veintiocho compositores y nueve arreglistas— la «autoría
   * visible» pasó a ser un muro de treinta y nueve nombres en una ranura pensada para una línea.
   * Una mención de responsabilidad no es la lista de créditos: es con qué se cita la obra. Tres
   * nombres y «y N más», que es como lo resuelve cualquier catálogo.
   */
  const entidadesCualesquiera = entidades.length > 0 ? entidades : creditos.filter(c => esEntidad(c.tipo));
  const personasCualesquiera = personas.length > 0 ? personas : creditos.filter(c => !esEntidad(c.tipo));
  const visible = acotar(
    (entidadesCualesquiera.length > 0 ? entidadesCualesquiera : personasCualesquiera).map(c => c.nombre),
  );

  return {
    id: publicacion.codigo ?? '',
    title: publicacion.titulo ?? '',
    year: anioLegible(publicacion),
    section: publicacion.seccionPrincipal ?? '',
    sectionPath: publicacion.rutaSeccion ?? '',
    publicationType: publicacion.tipoPublicacion ?? '',
    practice: publicacion.practicaMusical ?? '',
    category: publicacion.categoria ?? '',
    subcategory: publicacion.subcategoria ?? '',
    regionalScope: publicacion.ambito ?? '',
    author: autor,
    corporateAuthor: corporativo,
    // Los créditos que no son la autoría principal, con su rol, que es como los lee una persona.
    additionalCredits: unir(secundarios.map(c => `${c.nombre} (${c.rol})`)),
    displayAuthor: autor || corporativo || visible,
    isbn: identificador('ISBN'),
    ismn: identificador('ISMN'),
    formatSize: publicacion.tamanoFormato ?? '',
    pages: publicacion.paginas ?? '',
    duration: publicacion.duracion ?? '',
    summary: publicacion.resumen ?? '',
    // `camposAdicionales` NO SE PUBLICA: es un depósito interno sin forma declarada, y el criterio
    // de biblioteca deja fuera lo que no es dato de la obra. El diseño ya sabe ocultar lo vacío.
    coverText: publicacion.textoPortada ?? '',
    thumbnail: publicacion.miniaturaRuta ?? '',
    keywords: publicacion.palabrasClave ?? [],

    subtitle: publicacion.subtitulo ?? '',
    volume: publicacion.designacionVolumen ?? '',
    series: publicacion.serieOColeccion ?? '',
    language: publicacion.idioma ?? '',
    edtfDate: publicacion.fechaEdtf ?? '',
    dateNote: publicacion.notaFecha ?? '',
    secondaryCategory: publicacion.categoriaSecundaria ?? '',
    // EL AMBITO ESCRITO A MANO MANDA sobre el del vocabulario cuando existe: quien catalogó lo
    // escribió porque el término cerrado no le servía.
    scopeText: publicacion.ambitoTexto || publicacion.ambito || '',
    mediaFormat: publicacion.formato ?? '',
    licence: publicacion.licencia ?? '',
    typology: publicacion.tipologia ?? [],
    credits: creditos,
    identifiers: publicacion.identificadores ?? [],
    access: publicacion.accesos ?? [],
  };
}

/** La forma que consume la plantilla de noticias. */
export interface NoticiaDeDiseno {
  id: string;
  slug: string;
  title: string;
  desc: string;
  content: string;
  category: string;
  date: string;
  img: string;
  alt: string;
}

const MESES = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];

/**
 * La fecha como la escribe el diseño: «11 SEP 2026».
 *
 * SE PARTE LA CADENA ISO EN VEZ DE USAR `new Date`. Una fecha sin hora la interpreta el navegador
 * como UTC y la pinta en la zona local, de modo que en Colombia —cinco horas atrás— toda fecha
 * retrocedía un día. Es el fallo clásico y no se nota hasta que alguien mira el calendario.
 */
function fechaDeDiseno(iso: string | null): string {
  if (!iso) { return ''; }
  const [anio, mes, dia] = iso.slice(0, 10).split('-');
  const indice = Number(mes) - 1;
  if (!anio || Number.isNaN(indice) || !MESES[indice]) { return ''; }
  return `${Number(dia)} ${MESES[indice]} ${anio}`;
}

export function aNoticiaDeDiseno(noticia: Noticia): NoticiaDeDiseno {
  return {
    id: String(noticia.id),
    slug: noticia.slug,
    title: noticia.titulo,
    desc: noticia.resumen,
    content: noticia.cuerpo ?? '',
    category: noticia.categoria ?? '',
    date: fechaDeDiseno(noticia.fechaPublicacion),
    img: noticia.imagenRuta ?? '',
    alt: noticia.imagenAlternativa ?? '',
  };
}

/** La forma que consume la plantilla de la agenda, con su fecha despiezada. */
export interface EventoDeDiseno {
  id: string;
  slug: string;
  t: string;
  d: string;
  m: string;
  y: string;
  l: string;
  cat: string;
  desc: string;
  img: string;
  organizer: string;
  exactLocation: string;
  time: string;
  link: string;
  dateObj: Date;
  /** ISO. No estaba en la forma heredada: sin ella un festival de una semana dura un día. */
  fechaFin: string;
  departmentCode: string;
  municipalityCode: string;
  /**
   * Los NOMBRES, además de los códigos.
   *
   * El aside del diseño filtra por nombre de departamento y municipio, porque su selector se
   * construye sobre el catálogo territorial por nombre. Sin estos dos, ese filtro comparaba con
   * `undefined` y caía a despiezar la cadena de lugar, que acierta por accidente y falla en cuanto
   * un municipio lleva coma.
   */
  department: string;
  municipality: string;
  situacion: string;
}

/**
 * La hora como la escribe el diseño: «7:00 PM».
 *
 * El modelo la guarda en veinticuatro horas porque la columna es `time`; el diseño la enseña en
 * doce con meridiano, y esa es la decisión aprobada.
 */
function horaDeDiseno(hora: string | null): string {
  if (!hora) { return ''; }
  const [h, m] = hora.split(':');
  const horas = Number(h);
  if (Number.isNaN(horas)) { return ''; }
  const meridiano = horas >= 12 ? 'PM' : 'AM';
  const doce = horas % 12 === 0 ? 12 : horas % 12;
  return `${doce}:${m ?? '00'} ${meridiano}`;
}

export function aEventoDeDiseno(evento: EventoAgenda): EventoDeDiseno {
  const [anio, mes, dia] = evento.fechaInicio.slice(0, 10).split('-');
  const indice = Number(mes) - 1;

  return {
    id: String(evento.id),
    slug: evento.slug,
    t: evento.titulo,
    d: String(Number(dia)),
    m: MESES[indice] ?? '',
    y: anio,
    // El lugar en una línea, sin repetir el nombre cuando municipio y departamento coinciden.
    l: [...new Set([evento.nombreMunicipio, evento.nombreDepartamento].filter(Boolean))].join(', '),
    cat: evento.categoria ?? '',
    desc: evento.descripcion,
    img: evento.imagenRuta ?? '',
    organizer: evento.organizador ?? '',
    exactLocation: evento.lugar ?? '',
    time: horaDeDiseno(evento.horaInicio),
    link: evento.url ?? '',
    // Mediodía local para que el desfase de zona no mueva el día en ningún sentido.
    dateObj: new Date(Number(anio), indice, Number(dia), 12),
    fechaFin: evento.fechaFin ?? evento.fechaInicio,
    departmentCode: evento.codigoDepartamento ?? '',
    municipalityCode: evento.codigoMunicipio ?? '',
    department: evento.nombreDepartamento ?? '',
    municipality: evento.nombreMunicipio ?? '',
    situacion: evento.situacion,
  };
}
