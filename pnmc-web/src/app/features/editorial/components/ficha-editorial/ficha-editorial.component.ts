import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, Input, inject, signal } from '@angular/core';
import { LucideExternalLink, LucideLibrary, LucideQuote } from '@lucide/angular';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RecursoEditorialDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import { TagComponent } from '../../../../shared/components/ui/tag/tag.component';

/** Un par rótulo/valor de las tarjetas de metadatos. */
export interface DetalleDeFicha { rotulo: string; valor: string }

/** Cómo se lee en pantalla cada uno de los tres tipos de acceso. */
/**
 * El idioma, en palabras y no en código.
 *
 * <b>SE ENSEÑABA «es».</b> Un catálogo guarda ISO 639 porque es lo que permite intercambiar la
 * ficha, pero a quien consulta se le dice «Español». Medido sobre el acervo: 164 fichas declaran
 * `es`, 5 `es ; (lengua nativa)` y 2 `es ; en`, así que el valor puede traer VARIOS códigos
 * separados por punto y coma y algún texto que no es un código; lo que no se reconoce se deja tal
 * cual en vez de inventarle un nombre.
 */
const NOMBRE_DEL_IDIOMA: Record<string, string> = {
  es: 'Español', en: 'Inglés', fr: 'Francés', pt: 'Portugués', it: 'Italiano', de: 'Alemán',
};

export function idiomaEnPalabras(codigos: string): string {
  return (codigos ?? '')
    .split(';')
    .map(parte => parte.trim())
    .filter(Boolean)
    .map(parte => NOMBRE_DEL_IDIOMA[parte.toLowerCase()] ?? parte)
    .join(' · ');
}

/**
 * Qué va a pasar al pulsar, dicho antes de pulsar.
 *
 * <b>NINGUNA FICHA DECIA A DONDE LLEVABA.</b> Los 44 enlaces del acervo traen todos la misma
 * etiqueta genérica —«Recurso en línea»—, y la ficha los pintaba con ese texto y además repetía un
 * botón «Abrir recurso» al lado: dos controles para la misma acción y ninguno de los dos decía si
 * abría un PDF, un vídeo o una página. Comprobado: de esos 44, <b>13 son vídeos de YouTube, 4 son PDF
 * y 27 son páginas</b>. El texto se deriva del destino real.
 */
export function accionDelEnlace(url: string): string {
  const limpia = (url ?? '').toLowerCase().split('?')[0];
  if (limpia.endsWith('.pdf')) { return 'Descargar el PDF'; }
  if (/\.(docx?|odt|rtf)$/.test(limpia)) { return 'Descargar el documento'; }
  if (/(youtube\.com|youtu\.be|vimeo\.com)/.test(limpia)) { return 'Ver el vídeo'; }
  if (/\.(mp3|wav|ogg)$/.test(limpia)) { return 'Escuchar el audio'; }
  return 'Ver el recurso en línea';
}

/** El sitio al que lleva, para que nadie salga del catálogo sin saber a dónde va. */
export function sitioDelEnlace(url: string): string {
  try { return new URL(url).hostname.replace(/^www\./, ''); } catch { return ''; }
}

/**
 * El plural de los papeles que SON nombres de agente.
 *
 * <b>NO SE PLURALIZA SOLO, Y HAY UN MOTIVO MEDIDO.</b> De los dieciséis papeles del acervo, siete
 * no nombran a quien hace algo sino a lo que se hizo —«Asesoría», «Conceptualización»,
 * «Coordinación», «Metodología», «Revisión musical»…—, y esos NO se pluralizan: «Asesorías: Ana;
 * Luis» dice otra cosa. Una regla automática de morfología los estropearía a los siete.
 *
 * <b>LO QUE NO ESTA AQUI SE DEJA COMO LO ESCRIBIO LA FUENTE</b>, que es el comportamiento seguro:
 * el vocabulario de papeles es texto libre del origen y va a crecer. Un papel nuevo saldrá en
 * singular, que se lee algo forzado con tres nombres pero nunca dice algo falso.
 */
const PLURAL_DEL_PAPEL: Record<string, string> = {
  'Autor': 'Autores',
  'Autora': 'Autoras',
  'Autor (corporativo)': 'Autores (corporativos)',
  'Compositor': 'Compositores',
  'Arreglista': 'Arreglistas',
  'Arreglista/Adaptador': 'Arreglistas/Adaptadores',
  'Productor': 'Productores',
  'Curador': 'Curadores',
  'Recopilador/Compilador': 'Recopiladores/Compiladores',
  'Editorial': 'Editoriales',
  'Intérprete': 'Intérpretes',
  'Director': 'Directores',
  'Investigador': 'Investigadores',
  'Editor': 'Editores',
};

function enPlural(papel: string): string {
  return PLURAL_DEL_PAPEL[papel] ?? papel;
}

export interface ViaDeConsulta {
  clase: 'enlace' | 'presencial';
  /** Lo que va a pasar al pulsar, en palabras. */
  accion: string;
  /** El sitio al que lleva, o el lugar donde se consulta. */
  detalle: string;
  url: string;
  nota: string;
}

/**
 * La ficha catalográfica de una publicación: resumen, tarjetas de metadatos y columna lateral.
 *
 * <b>POR QUE EXISTE ESTE COMPONENTE.</b> El diseño aprobado escribe la ficha DOS VECES casi
 * idéntica —una en la fila desplegada de la tabla y otra dentro del diálogo del mosaico—, unas
 * ciento treinta líneas cada una. Dos copias divergen: cuando alguien añade un campo a la ficha lo
 * añade en una y la otra se queda corta sin que nadie lo note, y eso ya había pasado —el paréntesis
 * suelto de React vivía solo en la copia del diálogo—. El desarrollo de septiembre llegó a la misma
 * conclusión y lo resolvió igual: un componente con una variante, y las diferencias reales juntas
 * en un sitio.
 *
 * <b>LO QUE DE VERDAD CAMBIA ENTRE LAS DOS</b> son nueve clases de tamaño, el ancho de la columna
 * lateral y dos rótulos de botón. Está todo en <see cref="ESTILOS"/>. El resto es el mismo diseño.
 */
@Component({
  selector: 'app-ficha-editorial',
  standalone: true,
  // Los iconos entran como directivas sueltas, que es como los registra el resto del portal.
  imports: [CommonModule, TagComponent, LucideExternalLink, LucideLibrary, LucideQuote],
  templateUrl: './ficha-editorial.component.html',
})
export class FichaEditorialComponent {
  @Input({ required: true }) item!: RecursoEditorialDeDiseno;

  /** `fila` es la tabla desplegada; `dialogo`, el modal del mosaico. */
  @Input() variante: 'fila' | 'dialogo' = 'dialogo';

  /**
   * Una palabra clave o una sección con la que explorar el resto del acervo.
   *
   * <b>ES UNA SALIDA Y NO UNA NAVEGACION DIRECTA, a propósito.</b> Esta misma ficha se pinta en el
   * portal y dentro de la consola de gestión. En el portal, pulsar «#banda» tiene que filtrar el
   * catálogo; en la consola, donde la ficha se enseña para revisarla, no debe mover a nadie de
   * sitio. Quien la monta decide: el portal escucha, la consola no.
   */
  @Output() explorar = new EventEmitter<{ campo: 'palabraClave' | 'seccion'; valor: string }>();

  /** Si nadie escucha, las etiquetas no se pintan como si fueran pulsables. */
  get sePuedeExplorar(): boolean { return this.explorar.observed; }


  /** Si la portada no cargó. Sin esto, un hueco gris se lee como «no hay portada». */
  readonly portadaFallida = signal(false);

  get estilo() {
    return this.variante === 'fila'
      ? {
          parrafo: 'text-[0.85rem]', columna: 'xl:grid-cols-[minmax(0,1fr)_18rem]',
          rellenoLateral: 'p-5', rotulo: 'text-[0.5rem]', referencia: 'text-[0.72rem]',
          autoria: 'text-[0.78rem]', dato: 'text-[0.72rem]',
          boton: 'px-6 py-3 text-[0.65rem]',
        }
      : {
          parrafo: 'text-[0.9rem]', columna: 'xl:grid-cols-[minmax(0,1fr)_20rem]',
          rellenoLateral: 'p-6', rotulo: 'text-[0.55rem]', referencia: 'text-[0.75rem]',
          autoria: 'text-[0.8rem]', dato: 'text-[0.75rem]',
          boton: 'px-6 py-4 text-xs',
        };
  }

  /**
   * El identificador del vídeo de YouTube, si la dirección lo es.
   *
   * <b>SE MIRA LA DIRECCION Y NO LA SECCION.</b> La sección la escribe quien cataloga y puede
   * equivocarse; una dirección o es de YouTube o no lo es. Un recurso mal clasificado cae en la
   * rama de la portada, que siempre funciona, en vez de pintar un reproductor vacío. Trece accesos
   * del acervo apuntan a YouTube.
   */
  private readonly saneador = inject(DomSanitizer);

  /**
   * La dirección del reproductor, declarada segura.
   *
   * SE COMPONE AQUI Y NO EN LA PLANTILLA: lo único que entra es el identificador de once
   * caracteres que `videoDeYoutube()` ya validó contra su expresión, así que no hay forma de que
   * una dirección escrita por quien cataloga llegue al `src` del marco.
   *
   * `youtube-nocookie.com` y no `youtube.com`: el portal es institucional y no tiene por qué
   * plantarle una cookie de seguimiento a quien solo viene a ver una ficha del catálogo.
   */
  direccionDelVideo(id: string): SafeResourceUrl {
    return this.saneador.bypassSecurityTrustResourceUrl(`https://www.youtube-nocookie.com/embed/${id}`);
  }

  videoDeYoutube(): string | null {
    // EL RESPALDO AL CAMPO APLANADO SOBRABA Y ADEMAS ESTORBABA: unía todas las URL de la obra con
    // «; », así que en una ficha con varios enlaces el patrón podía casar contra una cadena que no
    // era una dirección. La lista de accesos es la fuente.
    const url = this.item?.access?.find(a => a.tipo === 'enlace' && /youtu/i.test(a.url ?? ''))?.url ?? '';
    const coincidencia = /(?:youtu\.be\/|youtube\.com\/(?:watch\?v=|embed\/|shorts\/))([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])/.exec(url);
    return coincidencia ? coincidencia[1] : null;
  }

  /**
   * Los créditos agrupados por papel: una entrada por papel, con todos sus nombres.
   *
   * <b>ANTES ERA UNA LINEA POR PERSONA, Y ESO SE LEE MAL EN CUANTO HAY DOS.</b> Medido sobre el
   * acervo: 133 de las 171 publicaciones repiten algún papel, así que no es un caso raro sino la
   * norma; la peor, `PNMC-ED-010`, acredita a VEINTIOCHO compositores, que eran veintiocho filas
   * con el mismo rótulo encima. El criterio es este: «en lugar de varios elementos que
   * digan arreglista y un nombre, arreglista y otro nombre, poner arreglistas y los nombres
   * separados».
   *
   * <b>SE SEPARAN CON PUNTO Y COMA, Y NO CON COMA.</b> Nueve nombres del acervo llevan una coma
   * dentro y ninguno lleva punto y coma, así que la coma no distingue dónde acaba un nombre. Es
   * además la separación de ISBD entre menciones de responsabilidad.
   *
   * <b>SE CONSERVA EL ORDEN EN QUE LLEGAN.</b> El primero de cada papel es el que la fuente puso
   * primero, y en una ficha bibliográfica ese orden no es alfabético: es la mención de
   * responsabilidad tal como aparece en la obra.
   */
  creditosPorPapel(): { papel: string; nombres: string; cuantos: number }[] {
    const grupos = new Map<string, string[]>();
    for (const credito of this.item.credits ?? []) {
      const nombres = grupos.get(credito.rol) ?? [];
      // SIN REPETIR: un mismo agente puede venir dos veces con el mismo papel desde la fuente.
      if (!nombres.includes(credito.nombre)) { nombres.push(credito.nombre); }
      grupos.set(credito.rol, nombres);
    }
    return [...grupos.entries()].map(([papel, nombres]) => ({
      papel: nombres.length > 1 ? enPlural(papel) : papel,
      nombres: nombres.join('; '),
      cuantos: nombres.length,
    }));
  }

  /** El título completo tal como lo cita una biblioteca: título, subtítulo y volumen. */
  tituloCompleto(): string {
    return [this.item.title, this.item.subtitle, this.item.volume].filter(Boolean).join(' · ');
  }

  parrafos(): string[] {
    const texto = this.item.summary || this.item.coverText || 'Sin resumen disponible para este registro.';
    return texto.split('\n').filter(Boolean);
  }

  clasificacion(): DetalleDeFicha[] {
    return this.conValor([
      { rotulo: 'Sección principal', valor: this.item.section },
      { rotulo: 'Ruta de sección', valor: this.item.sectionPath },
      { rotulo: 'Práctica musical', valor: this.item.practice },
      { rotulo: 'Categoría', valor: this.item.category },
      { rotulo: 'Categoría secundaria', valor: this.item.secondaryCategory },
      { rotulo: 'Subcategoría', valor: this.item.subcategory },
      { rotulo: 'Ámbito regional', valor: this.item.scopeText || this.item.regionalScope },
    ]);
  }

  bibliografica(): DetalleDeFicha[] {
    return this.conValor([
      { rotulo: 'Año o rango', valor: this.item.year },
      // LA NOTA SOBRE LA FECHA VA JUNTO A LA FECHA, que es donde significa algo. Estaba en la
      // tarjeta de normalización, a tres bloques de distancia del año que matiza.
      { rotulo: 'Sobre la fecha', valor: this.item.dateNote },
      { rotulo: 'Serie o colección', valor: this.item.series },
      { rotulo: 'Tipo de publicación', valor: this.item.publicationType },
      { rotulo: 'Autor', valor: this.item.author },
      { rotulo: 'Autor corporativo', valor: this.item.corporateAuthor },
      { rotulo: 'Tamaño o formato', valor: this.item.formatSize },
      { rotulo: 'Páginas', valor: this.item.pages },
      { rotulo: 'Duración', valor: this.item.duration },
    ]);
  }

  /**
   * Qué es el recurso, dicho para quien lo consulta.
   *
   * <b>ESTA TARJETA SE LLAMABA «NORMALIZACION Y ESTANDARES» Y ENSEÑABA JERGA DE CATALOGACION.</b>
   * Ponía «Tipo de recurso (Dublin Core): MovingImage» —un término del DCMI Type Vocabulary, que no
   * se traduce porque es un identificador de intercambio—, «Contenido (RDA 336)», «Medio (RDA 337)»
   * e «Idioma (ISO 639): es». Nada de eso ayuda a nadie a decidir si le sirve la obra, y el término
   * inglés en mitad de una ficha en español se lee como un error.
   *
   * <b>LA NORMA NO DESAPARECE: CAMBIA DE SITIO.</b> Los cuatro ejes se siguen guardando y el eje
   * DCMI sigue existiendo para poder exportar la ficha; lo ve quien cataloga, en la consola, que es
   * donde sirve. Aquí queda lo que describe el objeto en palabras corrientes: de qué está hecho, en
   * qué medio se percibe y sobre qué soporte viene.
   */
  caracteristicas(): DetalleDeFicha[] {
    const eje = (clave: string) =>
      (this.item.typology ?? []).filter(t => t.eje === clave).map(t => t.etiqueta).join(' · ');
    return this.conValor([
      { rotulo: 'Idioma', valor: idiomaEnPalabras(this.item.language) },
      { rotulo: 'Contenido', valor: eje('contenido') },
      { rotulo: 'Medio', valor: eje('medio') },
      { rotulo: 'Soporte', valor: eje('soporte') },
      { rotulo: 'Formato', valor: this.item.mediaFormat },
      { rotulo: 'Derechos y licencia', valor: this.item.licence },
    ]);
  }

  /**
   * Dónde se consulta la obra: una sola lista, y cada vía con su acción real.
   *
   * <b>LO QUE HABIA ERAN TRES LISTAS QUE SE PISABAN.</b> `enlaces()` pintaba los accesos con URL,
   * `consultasFisicas()` los que no la tienen, y `disponibilidad()` volvía a pintar «Ubicación de
   * la publicación» leyéndola de un campo aplanado que era una copia de los mismos accesos: en la
   * pantalla salían «CONSULTA FÍSICA» y «UBICACIÓN DE LA PUBLICACIÓN» con el mismo texto debajo.
   * Encima, el lateral repetía un botón para abrir el enlace que ya estaba listado arriba.
   *
   * <b>Y EL CAMPO APLANADO ROMPIA TRES FICHAS.</b> El adaptador unía todas las URL de una obra en
   * una sola cadena separada por «; », así que en `PNMC-ED-104` (cuatro enlaces), `PNMC-ED-099`
   * (tres) y `PNMC-ED-100` (dos) el botón abría una dirección que no existe. Ahora la única fuente
   * es `access`, que es una lista de verdad.
   */
  viasDeConsulta(): ViaDeConsulta[] {
    return (this.item.access ?? []).map(acceso => {
      if (acceso.url) {
        return {
          clase: 'enlace' as const,
          accion: accionDelEnlace(acceso.url),
          detalle: sitioDelEnlace(acceso.url),
          url: acceso.url,
          nota: acceso.nota ?? '',
        };
      }
      return {
        clase: 'presencial' as const,
        accion: 'Consulta presencial',
        detalle: acceso.ubicacionFisica || acceso.etiqueta || '',
        url: '',
        nota: acceso.nota ?? '',
      };
    })
      .filter(via => via.url || via.detalle)
      // PRIMERO LO QUE SE PUEDE ABRIR AHORA, después dónde ir a verlo. El orden de la fuente
      // intercalaba los dos —`PNMC-ED-042` abría con un enlace, seguía con la biblioteca y volvía a
      // dos enlaces—, y quien busca la obra tiene que poder decidir de un vistazo si la tiene a un
      // clic o si le toca desplazarse.
      .sort((a, b) => Number(b.clase === 'enlace') - Number(a.clase === 'enlace'));
  }

  hayDondeConsultar(): boolean { return this.viasDeConsulta().length > 0; }

  abrir(url: string | null): void {
    if (url) { window.open(url, '_blank', 'noopener,noreferrer'); }
  }

  private conValor(detalles: DetalleDeFicha[]): DetalleDeFicha[] {
    return detalles.filter(d => !!(d.valor ?? '').trim());
  }
}
