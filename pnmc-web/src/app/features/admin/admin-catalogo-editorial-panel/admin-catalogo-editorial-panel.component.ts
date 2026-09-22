import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { LucideBookOpen } from '@lucide/angular';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, DireccionDeOrden, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { etiquetaDeEstado } from '../domain/admin-config';
import { CommonModule } from '@angular/common';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { FormsModule } from '@angular/forms';
import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import {
  CatalogoEditorialService,
  ETIQUETAS_CATALOGACION,
  ETIQUETAS_PUBLICACION,
  PublicacionEditorial,
} from '../../../core/services/catalogo-editorial.service';
import { SelectorDeCategoriaComponent } from '../../../shared/components/ui/selector-de-categoria/selector-de-categoria.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { FichaEnConsolaComponent } from './ficha-en-consola/ficha-en-consola.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { SelectorDeClasificacionComponent } from '../../../shared/components/ui/selector-de-clasificacion/selector-de-clasificacion.component';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { IndicadorDePasosComponent, PasoDelIndicador } from '../../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { AutoguardadoDeBorrador } from '../../../core/services/autoguardado-de-borrador';
import { BorradoresDeConsolaService } from '../../../core/services/borradores-de-consola.service';
import { CargadorDeImagenComponent } from '../../../shared/components/cargador-de-imagen/cargador-de-imagen.component';
import {
  ImportarDesdeDocumentoComponent,
  PropuestaDeDocumento,
  ValorPropuesto,
} from '../../../shared/components/importar-desde-documento/importar-desde-documento.component';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs/operators';
import { BotonComponent, ImportanciaDeBoton } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import {
  AccesoEnFormulario,
  CreditoEnFormulario,
  IdentificadorEnFormulario,
  VocabulariosEditoriales,
} from '../../../core/contratos/catalogo-editorial';

/**
 * Catálogo Editorial en la consola.
 *
 * <b>Las dos decisiones se presentan por separado porque lo son.</b> Cada ficha lleva su estado
 * de catalogación —si está bien hecha— y su estado de publicación —si se ve—, y la pantalla no
 * los funde en una sola píldora. Fundirlos en la interfaz es el primer paso para fundirlos en
 * el modelo, que es lo que hacía la tabla plana retirada.
 *
 * <b>Publicar puede fallar, y está bien que falle aquí.</b> El servidor exige a la vez ficha
 * validada, fuente y derechos; cuando falta alguna devuelve el motivo y esta pantalla lo enseña
 * tal cual en vez de traducirlo. Una interfaz que escondiera esos motivos obligaría a adivinar
 * por qué un botón no hace nada.
 */
/** Lo que el formulario mantiene mientras se escribe. Plano a propósito: no es la ficha. */
/**
 * La ficha completa, tal como se captura.
 *
 * <b>ESTABA A UN TERCIO DE LO QUE EL REGISTRO ES.</b> El formulario pedía doce campos —y casi todos
 * en texto libre— para un modelo que guarda más de treinta, además de cuatro listas que son lo que
 * distingue una ficha catalográfica de un formulario cualquiera: los créditos con su papel, los
 * identificadores con su cualificador, las vías de consulta y las cuatro facetas de tipología. Lo
 * dijo criterio: «toda la experiencia de usuario para crear una nueva publicación no se
 * acerca para nada al detalle que tienen realmente este tipo de registros».
 *
 * <b>Y LO QUE TIENE LISTA SE ELIGE, NO SE ESCRIBE.</b> Idioma, tipo de publicación, ámbito, formato,
 * sección, práctica musical y subcategoría son vocabularios: se capturaban a mano y el acervo lo
 * paga —56 «tipos de publicación» distintos para 171 fichas, con «Libro», «Libro impreso» y «Libro
 * Cuaderno de ejercicios» conviviendo—. Ahora salen de `vocabularios()`, que los lee del propio
 * acervo.
 */

function fichaEnBlanco(): FormularioDeFicha {
  return {
    id: null, version: 0,
    codigo: '', titulo: '', subtitulo: '', designacionVolumen: '', serieOColeccion: '',
    identificadores: [],
    resumen: '', textoPortada: '', idioma: 'es',
    anioInicio: null, anioFin: null, fechaEdtf: '', notaFecha: '',
    tamanoFormato: '', paginas: '', duracion: '', miniaturaRuta: '',
    seccionPrincipal: '', rutaSeccion: '', categoriaId: null, categoriaSecundaria: '',
    subcategoria: '', practicaMusical: '', ambito: '', ambitoTexto: '',
    tipoPublicacion: '', formato: '', tipologias: [], palabrasClave: '',
    practicasMusicalesIds: [], territoriosSonorosIds: [],
    creditos: [], accesos: [],
  };
}

interface FormularioDeFicha {
  id: number | null;
  version: number;

  // ── Identificación ──
  codigo: string;
  titulo: string;
  subtitulo: string;
  designacionVolumen: string;
  serieOColeccion: string;
  identificadores: IdentificadorEnFormulario[];

  // ── Descripción ──
  resumen: string;
  textoPortada: string;
  idioma: string;
  anioInicio: number | null;
  anioFin: number | null;
  fechaEdtf: string;
  notaFecha: string;
  tamanoFormato: string;
  paginas: string;
  duracion: string;
  miniaturaRuta: string;

  // ── Clasificación ──
  seccionPrincipal: string;
  rutaSeccion: string;
  categoriaId: number | null;
  categoriaSecundaria: string;
  subcategoria: string;
  practicaMusical: string;
  ambito: string;
  ambitoTexto: string;
  tipoPublicacion: string;
  formato: string;
  /** Un código por faceta: contenido, medio, soporte y tipo de recurso. */
  tipologias: string[];
  palabrasClave: string;
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];

  // ── Créditos y consulta ──
  creditos: CreditoEnFormulario[];
  accesos: AccesoEnFormulario[];
}

/**
 * Bajo qué letra se ordena un título.
 *
 * <b>LO QUE HACIA MAL LA VERSION ANTERIOR.</b> Tomaba el primer carácter tal cual, así que
 * «¡Que viva San Juan, que viva San Pedro!» se archivaba bajo «¡», «“Ramón el camaleón”» bajo la
 * comilla y «Álbum…» en una letra distinta de «Acento». Tres publicaciones del acervo quedaban fuera
 * del abecedario, en botones que no son letras y que nadie va a pulsar buscando nada.
 *
 * <b>LA REGLA.</b> Se salta lo que no sea letra ni dígito, se le quitan las tildes y se pasa a
 * mayúscula. Así «¡Ay ’ombe…» va a la A, «¡Que viva…» a la Q y «“Ramón…» a la R, que es donde las
 * buscaría cualquiera.
 *
 * <b>LA EÑE NO ES UNA ENE CON ADORNO.</b> Quitar diacríticos a lo bruto la convertiría en N, y en
 * español es una letra propia con su sitio en el alfabeto. Se aparta antes de normalizar y se
 * devuelve después.
 *
 * <b>LOS NUMEROS VAN JUNTOS.</b> El acervo empieza títulos con 1, 2 y 8 —«8 Arreglos para Banda»—:
 * son cinco publicaciones repartidas en tres botones de un solo dígito. Un único «0-9», que es lo
 * que hace cualquier catálogo, las reúne donde se las busca.
 */
/** El primer tramo de una ruta, que es su sección. La misma regla que aplica el servidor. */
export function SeccionDeLaRuta(ruta: string | null | undefined): string {
  const limpia = (ruta ?? '').trim();
  const corte = limpia.indexOf('>');
  return corte < 0 ? limpia : limpia.slice(0, corte).trim();
}

export function inicialDeTitulo(titulo: string): string {
  for (const caracter of (titulo ?? '').trim()) {
    if (/\d/.test(caracter)) { return '0-9'; }

    // La eñe se aparta ANTES de normalizar: `NFD` la partiría en `n` + tilde y se perdería.
    if (caracter === 'ñ' || caracter === 'Ñ') { return 'Ñ'; }

    const sinTilde = caracter.normalize('NFD').replace(/\p{Diacritic}/gu, '').toUpperCase();
    if (/^[A-Z]$/.test(sinTilde)) { return sinTilde; }
    // Cualquier otra cosa —signos de apertura, comillas, espacios— se salta y se mira la siguiente.
  }
  return '';
}

/**
 * Los cuatro pasos de la ficha, en el orden en que se recorren.
 *
 * <b>POR QUE POR PASOS.</b> Una ficha bibliográfica pide muchos campos de naturaleza distinta
 * —identificación, descripción, clasificación— y verlos todos a la vez hace que parezcan
 * igualmente obligatorios. Separados, cada pantalla dice qué se está decidiendo.
 */
export const PASOS_DE_LA_FICHA: readonly PasoDelIndicador[] = [
  { id: 1, titulo: 'Identificación' },
  // PRIMERO QUE ES, DESPUES COMO SE DESCRIBE. El orden anterior pedía tamaño y páginas antes de
  // saber si la obra tiene objeto físico, y clasificaba al final. «Hay que organizar un poquito con
  // más lógica todo ese recorrido», dijo la dirección de producto: lo que el objeto ES gobierna qué
  // campos tienen sentido, así que va delante.
  { id: 2, titulo: 'Qué es' },
  { id: 3, titulo: 'Descripción' },
  // LOS CREDITOS Y LAS VIAS DE CONSULTA SON UN PASO PROPIO, y no un apartado del anterior: son
  // listas que se construyen fila a fila, y mezclarlas con campos sueltos convierte la pantalla en
  // un muro. Es además donde vive lo que hace citable y alcanzable a la obra.
  { id: 4, titulo: 'Clasificación' },
  { id: 5, titulo: 'Créditos y consulta' },
  { id: 6, titulo: 'Revisión' },
];

@Component({
  selector: 'app-admin-catalogo-editorial-panel',
  standalone: true,
  imports: [
    DialogoDirective, BarraDeListaComponent,BuscadorDeListaComponent, FiltroDesplegableComponent, LucideBookOpen, CabeceraDeTablaComponent, BotonComponent, EstadoDeListaComponent, SelectorSegmentadoComponent,
    CommonModule, FormsModule,
    SelectorDeCategoriaComponent, SelectorDeClasificacionComponent, IndicadorDePasosComponent,
    SelloDeProcedenciaComponent, HistorialDeRegistroComponent, MenuDeAccionesComponent, IndicadorDeEstadoComponent, DialogoDePrevisualizacionComponent,
    FichaEnConsolaComponent,
    CargadorDeImagenComponent,
    ImportarDesdeDocumentoComponent,
  ],
  templateUrl: './admin-catalogo-editorial-panel.component.html',
})
export class AdminCatalogoEditorialPanelComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(CatalogoEditorialService);
  private readonly enrutador = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly destruccion = inject(DestroyRef);
  private readonly borradores = inject(BorradoresDeConsolaService);

  /** El borrador que se guarda solo mientras se escribe. Solo para fichas nuevas. */
  readonly autoguardado = new AutoguardadoDeBorrador<FormularioDeFicha>(this.borradores.transporte('catalogo-editorial'));

  readonly pasos = PASOS_DE_LA_FICHA;
  readonly paso = signal(1);

  readonly borradorRecuperable = signal<{ datos: FormularioDeFicha; fecha: string } | null>(null);

  readonly enabled = input(true);
  readonly puedePublicar = input(false);

  /** La tabla con la que el Catálogo Editorial escribe su bitácora. */
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.editorial;

  readonly historialAbierto = signal<PublicacionEditorial | null>(null);

  /**
   * La ficha que se está consultando, o `null`.
   *
   * <b>HASTA HOY NO SE PODIA CONSULTAR UNA FICHA, SOLO EDITARLA.</b> Para ver qué tenía un registro
   * había que abrir el formulario de cuatro pasos, que es una pantalla para escribir y no para
   * leer: mirar un dato obligaba a ponerse en disposición de cambiarlo. Y lo que de verdad no
   * estaba en ninguna parte era el trabajo: en qué estado está, quién la validó, qué se anotó y
   * qué le falta para verse.
   */
  readonly fichaEnConsola = signal<PublicacionEditorial | null>(null);

  readonly total = signal(0);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly filtroEstado = signal<string>('todos');
  readonly busqueda = signal<string>('');
  readonly trabajando = signal<number | null>(null);

  /** El formulario. `null` cuando está cerrado; con `id` cuando edita una ficha existente. */
  readonly ficha = signal<FormularioDeFicha | null>(null);

  /**
   * Las listas con las que se rellenan los desplegables.
   *
   * SE PIDEN AL ENTRAR AL PANEL y no al abrir el formulario: quien va a catalogar abre la ficha
   * enseguida, y un desplegable que aparece vacío y se llena medio segundo después se lee como que
   * no hay opciones.
   */
  readonly vocabularios = signal<VocabulariosEditoriales | null>(null);

  /**
   * Si se enseñan los catálogos transversales del sistema.
   *
   * <b>ESTAN DETRAS DE UNA ACCION Y NO DELANTE.</b> El paso de clasificación llegó a tener treinta y
   * un controles, y medido sobre el acervo: la categoría del sistema la usan CERO publicaciones, los
   * vínculos con el catálogo de prácticas musicales CERO y los de territorios sonoros CERO, mientras
   * la taxonomía editorial —ubicación, subcategoría, práctica— la usan las 171. Poner delante lo que
   * no usa nadie entierra lo que usa todo el mundo. Se conservan enteros: sirven para cruzar una
   * publicación con las fichas de prácticas y territorios, y esa capacidad no se recorta.
   */
  readonly cruzarConElSistema = signal(false);
  readonly guardando = signal(false);

  readonly ESTADOS_CATALOGACION = Object.keys(ETIQUETAS_CATALOGACION);
  readonly ESTADOS_PUBLICACION = Object.keys(ETIQUETAS_PUBLICACION);

  readonly hayFichas = computed(() => this.acervo().length > 0);

  constructor() {
    // `untracked` por el mismo motivo que en Gestión del sitio: `cargar()` lee y escribe
    // `cargando` antes de su primer await, y sin él el efecto se suscribiría a una señal que
    // él mismo cambia y se reentraría sin final.
    effect(() => {
      if (!this.enabled()) { return; }
      untracked(() => {
        this.leerLaDireccion();
        void this.cargar();
        // LAS LISTAS SE PIDEN A LA VEZ QUE EL LISTADO, no al abrir el formulario: si fallan, el
        // panel sigue sirviendo y los desplegables se quedan con lo que ya tuviera la ficha.
        void this.api.vocabularios().then(listas => this.vocabularios.set(listas)).catch(() => undefined);
      });
    });
  }

  abrirHistorial(publicacion: PublicacionEditorial): void { this.historialAbierto.set(publicacion); }

  consultar(publicacion: PublicacionEditorial): void { this.fichaEnConsola.set(publicacion); }

  cerrarFichaEnConsola(): void { this.fichaEnConsola.set(null); }

  /**
   * Las acciones que esta publicación admite hoy.
   *
   * CATALOGAR Y PUBLICAR SON DOS DECISIONES DISTINTAS y el menú las conserva separadas: «Validar»
   * dice que la ficha está bien hecha, «Publicar» dice que se ve. Quien no tiene el permiso de
   * publicación no ve esas dos opciones en absoluto, en vez de verlas y descubrir el 403 al pulsar.
   */
  accionesDe(ficha: PublicacionEditorial): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [];
    const publicada = ficha.estadoPublicacion === 'publicado';
    const ocupada = this.trabajando() === ficha.id;

    if (this.puedePublicar() && !publicada) {
      acciones.push({ id: 'publicar', etiqueta: 'Publicar', tono: 'principal', deshabilitada: ocupada });
    }
    // CONSULTAR VA PRIMERO Y SIEMPRE. Es lo único que se puede hacer con cualquier ficha, esté en
    // el estado que esté, y hasta hoy no se podía: para ver un registro había que abrir el
    // formulario de edición, que es una pantalla para escribir.
    acciones.push({ id: 'consultar', etiqueta: 'Abrir ficha' });
    acciones.push({ id: 'editar', etiqueta: 'Editar', deshabilitada: ocupada });
    if (ficha.estadoCatalogacion !== 'validada') {
      acciones.push({ id: 'validar', etiqueta: 'Validar', deshabilitada: ocupada });
    }
    if (this.puedePublicar() && publicada) {
      // «DESPUBLICAR» Y NO «RETIRAR»: es la misma decisión que en Agenda y Noticias y se llama
      // igual en los tres. El estado que guarda sigue siendo `retirado`, vocabulario del catálogo.
      acciones.push({ id: 'despublicar', etiqueta: 'Despublicar', deshabilitada: ocupada });
    }
    if (publicada) {
      acciones.push({ id: 'ver', etiqueta: 'Ver en el portal', enlace: this.rutaPublica(), nuevaPestana: true });
    } else {
      // PREVISUALIZAR SOLO CUANDO TODAVIA NO SE VE. Y aquí importa más que en los otros dos: la
      // tarjeta del Catálogo enseña portada, tipo y resumen recortado, y una ficha sin portada no
      // se nota en el formulario, se nota en el mosaico.
      acciones.push({ id: 'previsualizar', etiqueta: 'Previsualizar', deshabilitada: ocupada });
    }
    acciones.push({ id: 'historial', etiqueta: 'Historial' });
    return acciones;
  }

  async ejecutarAccion(ficha: PublicacionEditorial, accion: string): Promise<void> {
    switch (accion) {
      case 'consultar': this.consultar(ficha); break;
      case 'editar': this.editar(ficha); break;
      case 'validar': await this.catalogar(ficha, 'validada'); break;
      case 'publicar': await this.publicar(ficha, 'publicado'); break;
      case 'despublicar': await this.publicar(ficha, 'retirado'); break;
      case 'previsualizar': this.previsualizando.set(ficha); break;
      case 'historial': this.abrirHistorial(ficha); break;
    }
  }

  cerrarHistorial(): void { this.historialAbierto.set(null); }

  /** El registro que se está mirando como se verá en su listado, o `null`. */
  readonly previsualizando = signal<PublicacionEditorial | null>(null);

  cerrarPrevisualizacion(): void { this.previsualizando.set(null); }

  /** Cómo se llama lo que se está previsualizando. Se escribe fuera del marco. */
  nombreParaPrevisualizar(registro: PublicacionEditorial): string { return registro.titulo; }

  /**
   * En qué estado está hoy, en palabras.
   *
   * SE LEE EL ESTADO EFECTIVO DONDE LO HAY: una noticia guardada como publicada con fecha futura
   * está «Programada», y decirle «Publicado» al lado de una previsualización que avisa de que
   * todavía no es pública sería contradecirse en la misma caja.
   */
  estadoParaPrevisualizar(registro: PublicacionEditorial): string { return ETIQUETAS_PUBLICACION[registro.estadoPublicacion] ?? registro.estadoPublicacion; }

  /**
   * La dirección pública del catálogo.
   *
   * LLEVA AL CATALOGO Y NO A UNA FICHA PROPIA: el diseño aprobado del portal abre la publicación
   * dentro del propio mosaico, sin ruta de detalle. Enlazar a una que no existe sería peor.
   */
  rutaPublica(): string { return '/editorial'; }

  // ═══════════════════════════ El índice del acervo ═══════════════════════════
  //
  // POR QUE ESTA PANTALLA SE REHIZO. La consola listaba las fichas como una BANDEJA DE TRABAJO:
  // buscar por título o código y filtrar por estado —borrador, en revisión, publicado—. Para un
  // módulo donde lo que importa es qué falta aprobar, eso basta. Aquí no: son 171 publicaciones
  // INDEPENDIENTES, y quien las gestiona no pregunta «¿qué está en borrador?» sino «¿qué hay de
  // Banda?», «¿qué publicó el Programa Nacional de Estímulos?», «¿cuántas cartillas de 2011 hay?».
  // El criterio es este: hay que poder navegar por categorías, autores, títulos y
  // registros, «de manera completa, clara, gestionable».
  //
  // EL ESTADO NO DESAPARECE: deja de ser el eje. Sigue siendo una faceta más, porque publicar y
  // retirar se siguen decidiendo desde aquí.

  /** Todo el acervo en memoria. Son 171 fichas: cabe, y hace instantánea cada faceta. */
  readonly acervo = signal<PublicacionEditorial[]>([]);

  readonly texto = signal('');
  readonly vista = signal<'lista' | 'mosaico'>('lista');

  /**
   * Las columnas de la tabla del acervo.
   *
   * SON CINCO Y ERAN SEIS: «Catalogación» se retiró de la vista principal el 15 de septiembre de
   * 2026 porque 168 de 171 filas decían «Validada». El dato sigue en el sistema, en la ficha y en
   * el filtro de la izquierda, y en la tabla aparece bajo el estado de publicación solo cuando NO
   * está validada, que es cuando informa de algo.
   */
  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'obra', etiqueta: 'Obra' },
    { id: 'autoria', etiqueta: 'Autoría y ubicación' },
    { id: 'publicacion', etiqueta: 'Publicación' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];


  /** Las dos maneras de mirar el acervo, para el selector segmentado de la barra. */
  readonly MANERAS_DE_VER = [
    { id: 'lista', etiqueta: 'Lista' },
    { id: 'mosaico', etiqueta: 'Mosaico' },
  ];
  /**
   * El orden del acervo, en la pieza compartida de tablas.
   *
   * <b>UN SOLO ESTADO PARA EL DESPLEGABLE Y PARA LAS CABECERAS.</b> La tabla no ordenaba al pulsar
   * sus columnas —lo reportó la dirección de producto— y el desplegable «Ordenar» era el único
   * control. Ponerle cabeceras ordenables al lado habría dejado dos controles para lo mismo, listos
   * para contradecirse; escriben los dos en esta señal, así que el desplegable siempre enseña por
   * qué está ordenada la tabla, se haya elegido donde se haya elegido.
   *
   * El desplegable conserva dos criterios QUE NO SON COLUMNAS —por código y por año— porque el
   * código y el año viven dentro de la columna «Obra» y no tienen cabecera propia.
   */
  readonly orden = new OrdenDeTabla('obra', 'asc');

  /**
   * El orden tal y como viaja en la dirección: `columna:direccion`, y nada si es el de partida.
   *
   * Se escribe también la dirección, que antes no viajaba: con las cabeceras ordenables, «por
   * título» y «por título al revés» son dos resultados distintos, y un enlace copiado tiene que
   * llevar al que se estaba mirando.
   */
  readonly ordenEnLaDireccion = computed(() => {
    const columna = this.orden.columna();
    const direccion = this.orden.direccion();
    if (columna === 'obra' && direccion === 'asc') { return ''; }
    return `${columna}:${direccion}`;
  });

  /**
   * Los criterios del desplegable: los que son columna y los dos que no lo son.
   *
   * <b>UN CRITERIO PUEDE TRAER SU SENTIDO, y una cabecera no.</b> «Por año, de la más reciente» es
   * un criterio completo: dice por qué ordena y hacia dónde. Una cabecera, en cambio, alterna, y
   * por eso siempre empieza ascendente en las diez tablas. No es una excepción a esa regla: son
   * dos controles con significados distintos.
   */
  /**
   * Los criterios en la forma que pide el filtro compartido.
   *
   * Se deriva de `CRITERIOS_DE_ORDEN` para que el rótulo se escriba UNA vez: si se duplicara, el
   * desplegable y la lógica de dirección acabarían diciendo cosas distintas.
   */
  readonly filtroDeOrden = computed<readonly OpcionDeFiltro[]>(
    () => this.CRITERIOS_DE_ORDEN.map(c => ({ id: c.id, etiqueta: c.etiqueta })),
  );

  readonly CRITERIOS_DE_ORDEN: readonly { id: string; etiqueta: string; direccion: DireccionDeOrden }[] = [
    { id: 'obra', etiqueta: 'Por título', direccion: 'asc' },
    { id: 'autoria', etiqueta: 'Por autoría', direccion: 'asc' },
    { id: 'publicacion', etiqueta: 'Por estado de publicación', direccion: 'asc' },
    { id: 'codigo', etiqueta: 'Por código', direccion: 'asc' },
    { id: 'anio', etiqueta: 'Por año, de la más reciente', direccion: 'desc' },
  ];

  /** Las facetas activas. Vacío es «todas»: no hay estado inicial que esconda fichas. */
  readonly facetaUbicacion = signal('');
  /** La ruta completa dentro de la sección: el nivel fino de la misma jerarquía. */
  readonly facetaRuta = signal('');
  readonly facetaPractica = signal('');
  readonly facetaFormato = signal('');
  readonly facetaAgente = signal('');
  readonly facetaLustro = signal('');
  readonly facetaEstado = signal('');
  /** La inicial del título, para el índice alfabético. */
  readonly facetaLetra = signal('');

  /** Qué sección tiene desplegadas sus rutas. Solo una: es un árbol, no una lista de listas. */
  readonly seccionAbierta = signal('');

  /**
   * Cada recorrido tiene su propia dirección.
   *
   * <b>SIN ESTO, UN RECORRIDO NO SE PUEDE COMPARTIR NI DESHACER.</b> Los filtros vivían solo en
   * memoria: no había forma de mandarle a alguien «las de Banda entre 2015 y 2019», ni de volver con
   * el botón de atrás después de bajar tres niveles del árbol, ni de recargar sin perder el sitio. En
   * una pantalla que existe para navegar 171 registros, eso convierte cada exploración en algo que
   * solo ocurre una vez.
   *
   * <b>VAN COMO PARAMETROS DE CONSULTA Y NO COMO TRAMOS DE RUTA.</b> La consola ya usa la ruta para
   * decir qué sección administrativa se está mirando —`administracion/:seccion`—; meter ahí las
   * facetas mezclaría dos cosas que cambian por motivos distintos. Además, `replaceUrl` evita llenar
   * el historial con un paso por cada clic: se navega hacia atrás entre BUSQUEDAS, no entre teclas.
   */
  private readonly EJES_EN_LA_DIRECCION = {
    q: this.texto,
    ubicacion: this.facetaUbicacion,
    ruta: this.facetaRuta,
    practica: this.facetaPractica,
    formato: this.facetaFormato,
    agente: this.facetaAgente,
    anios: this.facetaLustro,
    estado: this.facetaEstado,
    letra: this.facetaLetra,
  } as const;

  /**
   * El lustro al que pertenece un año.
   *
   * POR LUSTROS Y NO POR AÑO SUELTO. El acervo abarca de 1990 a 2024: veinticuatro años distintos
   * son veinticuatro filas de faceta, y casi todas con una o dos fichas. Agrupados de cinco en
   * cinco quedan SIETE tramos con reparto real —el mayor, 2015-2019, tiene 52—, que es una faceta
   * que sirve para acotar en vez de una lista para recorrer.
   */
  private lustroDe(anio: number | null | undefined): string {
    if (!anio) { return ''; }
    const inicio = Math.floor(anio / 5) * 5;
    return `${inicio}–${inicio + 4}`;
  }

  /** El estado efectivo de la ficha, en una sola frase legible. */
  private estadoDe(ficha: PublicacionEditorial): string {
    return `${ETIQUETAS_CATALOGACION[ficha.estadoCatalogacion] ?? ficha.estadoCatalogacion} · ${ETIQUETAS_PUBLICACION[ficha.estadoPublicacion] ?? ficha.estadoPublicacion}`;
  }

  /**
   * Lo que queda tras buscar y filtrar.
   *
   * LA BUSQUEDA MIRA TAMBIEN LA AUTORIA Y LAS PALABRAS CLAVE, no solo título y código como hacía el
   * servidor. Buscar «Bassi» tiene que encontrar su obra aunque su nombre no esté en el título, que
   * es como se busca de verdad en un catálogo.
   */
  readonly fichasVisibles = computed(() => {
    const termino = this.texto().trim().toLowerCase();
    const ubicacion = this.facetaUbicacion();
    const practica = this.facetaPractica();
    const formato = this.facetaFormato();
    const agente = this.facetaAgente();

    const resultado = this.acervo().filter(f => {
      if (ubicacion && f.seccionPrincipal !== ubicacion) { return false; }
      if (this.facetaRuta() && !this.enLaRuta(f.rutaSeccion, this.facetaRuta())) { return false; }
      if (practica && f.practicaMusical !== practica) { return false; }
      if (formato && f.formato !== formato) { return false; }
      if (agente && !(f.creditos ?? []).some(c => c.agenteNombre === agente)) { return false; }
      if (this.facetaLustro() && this.lustroDe(f.anioInicio) !== this.facetaLustro()) { return false; }
      if (this.facetaEstado() && this.estadoDe(f) !== this.facetaEstado()) { return false; }
      if (this.facetaLetra() && inicialDeTitulo(f.titulo) !== this.facetaLetra()) { return false; }
      if (!termino) { return true; }
      const buscable = [
        f.codigo, f.titulo, f.subtitulo, f.serieOColeccion, f.tipoPublicacion,
        ...(f.creditos ?? []).map(c => c.agenteNombre),
        ...(f.palabrasClave ?? []),
      ].filter(Boolean).join(' ').toLowerCase();
      return buscable.includes(termino);
    });

    return this.orden.ordenarFilas(resultado, f => {
      switch (this.orden.columna()) {
        case 'codigo': return f.codigo;
        case 'anio': return f.anioInicio ?? null;
        case 'autoria': return this.autoriaDe(f);
        case 'publicacion': return this.etiquetaPublicacion(f.estadoPublicacion);
        default: return f.titulo;
      }
    });
  });

  /**
   * Cada faceta con su recuento, calculado SOBRE LO QUE YA ESTA FILTRADO menos ella misma.
   *
   * ASI EL NUMERO DICE LA VERDAD. Si se contara sobre el acervo entero, con «Banda» ya elegido el
   * panel seguiría prometiendo «Repertorio: 23» y al pulsarlo aparecerían cuatro. Y si se contara
   * sobre el resultado final, la faceta elegida se quedaría a uno y las demás a cero: no se podría
   * cambiar de idea sin borrar el filtro primero.
   */
  private contar(
    campo: (f: PublicacionEditorial) => string | null | undefined,
    excluir: 'ubicacion' | 'practica' | 'formato' | 'lustro' | 'estado' | 'letra' | 'ninguna',
  ) {
    const termino = this.texto().trim().toLowerCase();
    const base = this.acervo().filter(f => {
      if (excluir !== 'ubicacion' && this.facetaUbicacion() && f.seccionPrincipal !== this.facetaUbicacion()) { return false; }
      if (excluir !== 'practica' && this.facetaPractica() && f.practicaMusical !== this.facetaPractica()) { return false; }
      if (excluir !== 'formato' && this.facetaFormato() && f.formato !== this.facetaFormato()) { return false; }
      if (excluir !== 'lustro' && this.facetaLustro() && this.lustroDe(f.anioInicio) !== this.facetaLustro()) { return false; }
      if (excluir !== 'estado' && this.facetaEstado() && this.estadoDe(f) !== this.facetaEstado()) { return false; }
      // LA RUTA ES HIJA DE LA UBICACION, así que al contar ubicaciones también se ignora.
      //
      // Si no, la cuenta se cierra sobre sí misma: con «Repertorio > Música Popular y Tradicional»
      // puesto, ninguna otra sección puede tener fichas —por construcción—, la lista de ubicaciones
      // se queda en una sola y el árbol entero DESAPARECE de la pantalla. Es lo que el dueño del
      // proyecto vio como «se compacta automáticamente todas las subcategorías»: no se plegaba, se
      // iba. Medido con esa misma ruta.
      if (excluir !== 'ubicacion' && this.facetaRuta() && !this.enLaRuta(f.rutaSeccion, this.facetaRuta())) { return false; }
      if (excluir !== 'letra' && this.facetaLetra() && inicialDeTitulo(f.titulo) !== this.facetaLetra()) { return false; }
      if (this.facetaAgente() && !(f.creditos ?? []).some(c => c.agenteNombre === this.facetaAgente())) { return false; }
      if (!termino) { return true; }
      return [f.codigo, f.titulo, ...(f.creditos ?? []).map(c => c.agenteNombre)]
        .filter(Boolean).join(' ').toLowerCase().includes(termino);
    });

    const cuenta = new Map<string, number>();
    for (const ficha of base) {
      const valor = (campo(ficha) ?? '').trim();
      if (valor) { cuenta.set(valor, (cuenta.get(valor) ?? 0) + 1); }
    }
    return [...cuenta.entries()]
      .map(([valor, cuantas]) => ({ valor, cuantas }))
      .sort((a, b) => b.cuantas - a.cuantas || a.valor.localeCompare(b.valor, 'es'));
  }

  readonly ubicaciones = computed(() => this.contar(f => f.seccionPrincipal, 'ubicacion'));

  /**
   * ¿El ACERVO tiene más de una sección? —no el resultado filtrado.
   *
   * Un filtro no puede hacer desaparecer el control con el que se quita. Con el árbol atado al
   * recuento filtrado bastaba acotar lo suficiente para quedarse sin él en pantalla.
   */
  readonly hayVariasUbicaciones = computed(
    () => new Set(this.acervo().map(f => (f.seccionPrincipal ?? '').trim()).filter(Boolean)).size > 1,
  );
  readonly practicas = computed(() => this.contar(f => f.practicaMusical, 'practica'));
  readonly formatos = computed(() => this.contar(f => f.formato, 'formato'));

  /** Los lustros, en orden cronológico y no por cantidad: una línea de tiempo se lee en orden. */
  readonly lustros = computed(() =>
    [...this.contar(f => this.lustroDe(f.anioInicio), 'lustro')].sort((a, b) => a.valor.localeCompare(b.valor)));

  /**
   * Los estados, y solo si hay más de uno.
   *
   * HOY LAS 171 ESTAN «VALIDADA · PUBLICADO», así que ofrecer la faceta sería ofrecer un botón que
   * no quita ninguna ficha. Aparecerá sola en cuanto entre la primera en borrador, que es cuando
   * empieza a servir.
   */
  readonly estados = computed(() => {
    const cuenta = this.contar(f => this.estadoDe(f), 'estado');
    return cuenta.length > 1 ? cuenta : [];
  });

  /**
   * El índice alfabético por inicial del título.
   *
   * ES LA NAVEGACION MAS VIEJA DE UN CATALOGO Y SIGUE SIENDO LA MEJOR para encontrar algo cuyo
   * nombre se recuerda a medias. Las 171 reparten sus títulos en 26 iniciales distintas, así que el
   * índice acota de verdad. Se enseñan también las letras sin fichas, apagadas: un abecedario con
   * huecos se lee peor que uno completo.
   */
  readonly abecedario = computed(() => {
    // SE CUENTA SOBRE LO YA FILTRADO, como todas las demás facetas. Contarlo sobre el acervo entero
    // dejaba las 26 letras pulsables aunque los filtros activos las hubieran vaciado: se pulsaba una
    // y el listado se quedaba en cero. Una letra apagada dice «aquí no hay nada»; una letra activa
    // que no lleva a ningún sitio es una promesa incumplida.
    const cuenta = new Map(this.contar(f => inicialDeTitulo(f.titulo), 'letra')
      .map(({ valor, cuantas }) => [valor, cuantas]));
    // Los números al final, como en cualquier índice: se busca por letra y, si no, por cifra.
    const letras = [...'ABCDEFGHIJKLMNÑOPQRSTUVWXYZ'.split(''), '0-9'];
    return letras.map(letra => ({ letra, cuantas: cuenta.get(letra) ?? 0 }));
  });

  /**
   * Las rutas de una sección, que es el nivel fino de la misma jerarquía.
   *
   * SE DESPLIEGAN AL ELEGIR LA SECCION y no antes: las 22 rutas juntas son una lista plana que no
   * enseña que «Repertorio > Banda» está DENTRO de «Repertorio». El árbol es la estructura real.
   */
  rutasDe(seccion: string): { valor: string; hoja: string; cuantas: number }[] {
    const cuenta = new Map<string, number>();
    for (const ficha of this.acervo()) {
      if (ficha.seccionPrincipal !== seccion || !ficha.rutaSeccion) { continue; }
      cuenta.set(ficha.rutaSeccion, (cuenta.get(ficha.rutaSeccion) ?? 0) + 1);
    }
    return [...cuenta.entries()]
      .map(([valor, cuantas]) => ({
        valor,
        // La sección ya encabeza el grupo: repetirla en cada hoja solo alarga la lista.
        hoja: valor.split('>').slice(1).map(x => x.trim()).join(' › ') || seccion,
        cuantas,
      }))
      .sort((a, b) => b.cuantas - a.cuantas || a.hoja.localeCompare(b.hoja, 'es'));
  }

  /**
   * El fichero de autoridades: quién tiene obra en el acervo y cuánta.
   *
   * ES LA DIMENSION QUE FALTABA. Se puede llegar a una publicación por su título o por su sitio,
   * pero no por quién la hizo, que es como se busca la mitad de las veces. Comprobado: <b>86 agentes
   * tienen más de una obra</b>, así que la lista no es una curiosidad: es un índice.
   *
   * SE ENSEÑAN LOS QUE REPITEN. Con 410 agentes, listarlos todos es un directorio telefónico; los
   * que aparecen una sola vez se alcanzan escribiendo su nombre en la búsqueda.
   */
  readonly indiceDeAutoria = computed(() => {
    const cuenta = new Map<string, number>();
    for (const ficha of this.acervo()) {
      for (const nombre of new Set((ficha.creditos ?? []).map(c => c.agenteNombre))) {
        cuenta.set(nombre, (cuenta.get(nombre) ?? 0) + 1);
      }
    }
    return [...cuenta.entries()]
      .filter(([, cuantas]) => cuantas > 1)
      .map(([nombre, cuantas]) => ({ nombre, cuantas }))
      .sort((a, b) => b.cuantas - a.cuantas || a.nombre.localeCompare(b.nombre, 'es'));
  });

  readonly hayFiltros = computed(() => this.rutaActiva().length > 0 || Boolean(this.texto()));

  /**
   * La ruta por la que se ha llegado hasta aquí, en piezas que se pueden quitar una a una.
   *
   * <b>ES LO QUE CONVIERTE UN FILTRO EN UNA RUTA.</b> Con las facetas repartidas por una columna,
   * después de tres clics nadie sabe qué tiene puesto sin repasarla entera, y la única salida era
   * borrarlo todo y empezar de cero. Aquí se ve el camino completo y se retrocede un paso, que es
   * como se explora: «Formación» → «Pedagogía Instrumental» → y si no era eso, se quita la última.
   *
   * EL ORDEN ES EL DE LA JERARQUIA, no el de los clics: sección antes que ruta, y la búsqueda al
   * final porque acota sobre todo lo demás.
   */
  readonly rutaActiva = computed(() => {
    const piezas: { eje: string; valor: string; quitar: () => void }[] = [];
    if (this.facetaUbicacion()) {
      piezas.push({ eje: 'Ubicación', valor: this.facetaUbicacion(), quitar: () => { this.facetaUbicacion.set(''); this.facetaRuta.set(''); } });
    }
    if (this.facetaRuta()) {
      const hoja = this.facetaRuta().split('>').slice(1).map(x => x.trim()).join(' › ');
      piezas.push({ eje: 'Dentro de', valor: hoja || this.facetaRuta(), quitar: () => this.facetaRuta.set('') });
    }
    if (this.facetaPractica()) { piezas.push({ eje: 'Práctica', valor: this.facetaPractica(), quitar: () => this.facetaPractica.set('') }); }
    if (this.facetaFormato()) { piezas.push({ eje: 'Formato', valor: this.facetaFormato(), quitar: () => this.facetaFormato.set('') }); }
    if (this.facetaLustro()) { piezas.push({ eje: 'Años', valor: this.facetaLustro(), quitar: () => this.facetaLustro.set('') }); }
    if (this.facetaEstado()) { piezas.push({ eje: 'Estado', valor: this.facetaEstado(), quitar: () => this.facetaEstado.set('') }); }
    if (this.facetaAgente()) { piezas.push({ eje: 'Autoría', valor: this.facetaAgente(), quitar: () => this.facetaAgente.set('') }); }
    if (this.facetaLetra()) { piezas.push({ eje: 'Empieza por', valor: this.facetaLetra(), quitar: () => this.facetaLetra.set('') }); }
    if (this.texto().trim()) { piezas.push({ eje: 'Busca', valor: this.texto().trim(), quitar: () => this.texto.set('') }); }
    return piezas;
  });

  /** Alternar: pulsar la faceta que ya está puesta la quita. Es como se explora. */
  /**
   * Elegir una sección la filtra Y despliega sus rutas.
   *
   * SON EL MISMO GESTO A PROPOSITO. Separar «filtrar por Formación» de «ver qué hay dentro de
   * Formación» obliga a dos clics para lo que es una sola intención: mirar ahí dentro.
   */
  /**
   * Filtrar por una sección entera.
   *
   * DESPLIEGA TAMBIEN, porque elegir una sección y querer ver qué hay dentro es la misma
   * intención; lo que ya no pasa es lo contrario: desplegar no filtra. Ver `alternarSeccionAbierta`.
   */
  alternarUbicacion(valor: string): void {
    const ya = this.facetaUbicacion() === valor;
    this.facetaUbicacion.set(ya ? '' : valor);
    // SE ABRE, PERO NO SE CIERRA AL QUITAR EL FILTRO: dejar de filtrar por «Repertorio» no es
    // dejar de querer ver qué hay dentro de Repertorio. Plegar es cosa de la flecha.
    this.seccionAbierta.set(valor);
    // Cambiar de sección deja sin sentido la ruta fina que hubiera puesta.
    this.facetaRuta.set('');
  }

  /**
   * Abrir o cerrar una sección SIN filtrar por ella.
   *
   * Los dos gestos vivían en el mismo botón, así que asomarse a las rutas de «Formación» acotaba
   * el listado a Formación aunque solo se quisiera mirar. Lo reportó la dirección de producto el 15
   * de septiembre de 2026: «al cliquear en la categoría principal para abrir (en la flechita de la
   * izquierda) igualmente se filtra».
   */
  alternarSeccionAbierta(valor: string): void {
    this.seccionAbierta.set(this.seccionAbierta() === valor ? '' : valor);
  }

  /**
   * Elegir un criterio en el desplegable.
   *
   * NO ALTERNA: elegir «Por autoría» estando ya en «Por autoría» no tiene sentido como inversión
   * —el desplegable no se «vuelve a pulsar»—, así que fija ascendente y deja la inversión para la
   * cabecera, que es donde el segundo clic significa «al revés».
   */
  ordenarPorCriterio(criterio: string): void {
    const elegido = this.CRITERIOS_DE_ORDEN.find(c => c.id === criterio);
    this.orden.fijar(criterio, elegido?.direccion ?? 'asc');
  }

  alternarRuta(valor: string): void { this.facetaRuta.set(this.facetaRuta() === valor ? '' : valor); }
  alternarLustro(valor: string): void { this.facetaLustro.set(this.facetaLustro() === valor ? '' : valor); }
  alternarEstado(valor: string): void { this.facetaEstado.set(this.facetaEstado() === valor ? '' : valor); }
  alternarLetra(valor: string): void { this.facetaLetra.set(this.facetaLetra() === valor ? '' : valor); }
  alternarPractica(valor: string): void { this.facetaPractica.set(this.facetaPractica() === valor ? '' : valor); }
  alternarFormato(valor: string): void { this.facetaFormato.set(this.facetaFormato() === valor ? '' : valor); }
  alternarAgente(valor: string): void { this.facetaAgente.set(this.facetaAgente() === valor ? '' : valor); }

  limpiarFiltros(): void {
    this.texto.set('');
    this.facetaUbicacion.set('');
    this.facetaRuta.set('');
    this.facetaPractica.set('');
    this.facetaFormato.set('');
    this.facetaAgente.set('');
    this.facetaLustro.set('');
    this.facetaEstado.set('');
    this.facetaLetra.set('');
    this.seccionAbierta.set('');
  }

  /**
   * Desde una ficha, al resto de las que comparten algo con ella.
   *
   * <b>CIERRA EL CIRCUITO QUE FALTABA.</b> El índice llevaba del acervo a una publicación, pero
   * desde la publicación no se volvía: para ver «qué más hay en esta ruta» o «qué más hizo esta
   * autoría» había que cerrar la ficha, buscar la faceta en la columna y acordarse del nombre exacto.
   * Y es justo el momento en que la pregunta aparece, porque se está mirando ese dato.
   *
   * SE LIMPIA LO DEMAS a propósito: el resultado es «todo lo de esta ruta», no «lo de esta ruta que
   * además cumplía los filtros que traía puestos antes», que casi nunca es lo que se quiere.
   */
  explorarDesdeLaFicha(eje: 'ruta' | 'agente', valor: string): void {
    this.limpiarFiltros();
    if (eje === 'ruta') {
      this.facetaUbicacion.set(SeccionDeLaRuta(valor));
      this.seccionAbierta.set(SeccionDeLaRuta(valor));
      this.facetaRuta.set(valor);
    } else {
      this.facetaAgente.set(valor);
    }
  }

  /**
   * ¿Esta ficha está en esa ruta, o en alguna que cuelgue de ella?
   *
   * <b>EL FILTRO COMPARABA LA RUTA ENTERA, LETRA A LETRA.</b> Con rutas de tres niveles —
   * «Repertorio > Música popular y tradicional > Homenajes»— eso significa que elegir el nivel
   * intermedio no encontraba NADA: ninguna ficha tiene exactamente esa ruta, la tienen más larga.
   * Lo reportó la dirección de producto con ese mismo ejemplo, y lo
   * destapó el arreglo anterior: hasta entonces los tramos intermedios no se podían pulsar porque
   * los tres llamaban con la ruta completa, así que el fallo estaba tapado por otro.
   *
   * Un nivel contiene lo que cuelga de él; por eso se compara por prefijo Y se exige el separador,
   * para que «Banda» no se lleve también «Bandas sinfónicas».
   */
  private enLaRuta(ruta: string | null | undefined, filtro: string): boolean {
    const suya = (ruta ?? '').trim();
    return suya === filtro || suya.startsWith(`${filtro} > `);
  }

  /** Los tramos de una ruta, para pintarla como jerarquía y no como una línea de texto. */
  tramosDeLaRuta(ruta: string | null | undefined): string[] {
    return (ruta ?? '').split('>').map(x => x.trim()).filter(Boolean);
  }

  /**
   * Explorar desde UN TRAMO de la ruta de una fila, al nivel de ese tramo.
   *
   * <b>ESTE ERA UN DEFECTO DE VERDAD, y está definido exacto</b>: «al
   * seleccionar una categoría general como Repertorio, el sistema termina activando "Dentro de:
   * Orquesta"». La ruta se pintaba partida en tramos para que la jerarquía se viera —bien— pero
   * los tres botones llamaban al mismo sitio con la ruta ENTERA, así que pulsar «Repertorio»
   * filtraba por «Repertorio > Orquesta». Comprobado: 3 publicaciones en
   * vez de las 23 de Repertorio.
   *
   * Ahora cada tramo filtra a SU nivel: el primero deja solo la sección —que es lo que significa
   * elegir una categoría general— y los siguientes arman la ruta hasta ahí.
   */
  explorarDesdeElTramo(ruta: string | null | undefined, indice: number): void {
    const tramos = this.tramosDeLaRuta(ruta);
    if (tramos.length === 0) { return; }
    this.limpiarFiltros();
    this.facetaUbicacion.set(tramos[0]);
    this.seccionAbierta.set(tramos[0]);
    // El primer tramo ES la sección: no hay ruta fina que poner, y ponerla sería bajar un nivel
    // que nadie pidió.
    this.facetaRuta.set(indice === 0 ? '' : tramos.slice(0, indice + 1).join(' > '));
  }

  /** El primer nombre acreditado, que es con el que se explora. Vacío si no hay ninguno. */
  autoriaPrincipalDe(ficha: PublicacionEditorial): string {
    const creditos = ficha.creditos ?? [];
    const principales = creditos.filter(c => c.principal);
    return (principales.length > 0 ? principales : creditos)[0]?.agenteNombre ?? '';
  }

  /** La autoría con la que se cita cada ficha en el listado. */
  autoriaDe(ficha: PublicacionEditorial): string {
    const creditos = ficha.creditos ?? [];
    const principales = creditos.filter(c => c.principal);
    const nombres = [...new Set((principales.length > 0 ? principales : creditos).map(c => c.agenteNombre))];
    if (nombres.length === 0) { return 'Sin autoría registrada'; }
    return nombres.length <= 2 ? nombres.join('; ') : `${nombres.slice(0, 2).join('; ')} y ${nombres.length - 2} más`;
  }

  /**
   * Recupera el recorrido escrito en la dirección.
   *
   * SE LEE UNA VEZ AL ENTRAR, y también en cada navegación: así el botón de atrás deshace el último
   * filtro en vez de sacar a la persona del Catálogo Editorial.
   */
  /**
   * Deja escrito en la dirección cualquier cambio del recorrido.
   *
   * <b>UN EFECTO Y NO UNA LLAMADA EN CADA ALTERNADOR.</b> Hay nueve ejes y cada uno se toca desde
   * varios sitios —el panel, la ruta activa, el abecedario, la fila—; acordarse de llamar a escribir
   * en cada uno es exactamente la clase de cosa que se olvida en el décimo. El efecto lee todas las
   * señales, así que cualquier cambio, venga de donde venga, acaba en la dirección.
   */
  private readonly guardarEnLaDireccion = effect(() => {
    // Se leen para que el efecto dependa de todas.
    for (const origen of Object.values(this.EJES_EN_LA_DIRECCION)) { origen(); }
    this.vista();
    this.orden.columna();
    this.orden.direccion();
    untracked(() => this.escribirLaDireccion());
  });

  private leerLaDireccion(): void {
    const aplicar = () => {
      const parametros = this.ruta.snapshot.queryParamMap;
      for (const [clave, destino] of Object.entries(this.EJES_EN_LA_DIRECCION)) {
        destino.set(parametros.get(clave) ?? '');
      }
      // LA SECCION DESPLEGADA SOLO SE ABRE AQUI, NUNCA SE CIERRA.
      //
      // Decía `set(this.facetaUbicacion())`, y como el efecto que escribe la dirección dispara una
      // navegación, cualquier cambio de cualquier filtro volvía a pasar por aquí: quitar una
      // faceta cualquiera dejaba `ubicacion` vacío un instante y el árbol entero se plegaba solo.
      // El criterio es este: «una vez des-selecciono cualquier filtro se compacta
      // automáticamente todas las subcategorías, no tiene sentido ese comportamiento».
      //
      // Abrir sí: llegar con `?ubicacion=Repertorio` tiene que enseñar sus rutas. Cerrar es un
      // gesto de quien mira, y solo lo hace la flecha.
      if (this.facetaUbicacion()) { this.seccionAbierta.set(this.facetaUbicacion()); }
      const vista = parametros.get('vista');
      if (vista === 'mosaico' || vista === 'lista') { this.vista.set(vista); }
      const orden = parametros.get('orden');
      if (orden) {
        const [columna, direccion] = orden.split(':');
        this.orden.fijar(columna, direccion === 'desc' ? 'desc' : 'asc');
      }

      // SE SINCRONIZA LA FIRMA: sin esto, volver atrás dispararía el efecto, vería un cambio y
      // apilaría una entrada nueva, con lo que atrás dejaría de avanzar hacia atrás.
      const puestos: Record<string, string> = {};
      for (const [clave, origen] of Object.entries(this.EJES_EN_LA_DIRECCION)) {
        const valor = origen().trim();
        if (valor) { puestos[clave] = valor; }
      }
      if (this.vista() !== 'lista') { puestos['vista'] = this.vista(); }
      if (this.ordenEnLaDireccion()) { puestos['orden'] = this.ordenEnLaDireccion(); }
      this.ultimosParametros = JSON.stringify(puestos);
      this.direccionLeida = true;
    };

    aplicar();
    this.enrutador.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed(this.destruccion))
      .subscribe(aplicar);
  }

  /** Lo último que se escribió en la dirección, para saber qué cambió respecto de ahora. */
  private ultimosParametros = '';

  /**
   * Si ya se leyó la dirección de entrada.
   *
   * <b>SIN ESTO, LA PANTALLA SE BORRABA A SI MISMA AL ABRIRLA.</b> El efecto que escribe la
   * dirección se crea como campo de la clase, y los campos se inicializan ANTES del cuerpo del
   * constructor, que es donde se lee la dirección. Resultado: al abrir un enlace con filtros, el
   * efecto corría primero, veía todas las facetas vacías —porque aún no se habían leído— y escribía
   * una dirección sin parámetros, machacando la que traía el enlace. Recargar perdía el recorrido y
   * compartirlo no servía de nada.
   *
   * Comprobado: con el enlace de «Formación > Pedagogía Instrumental», recargar devolvía 171 fichas en
   * vez de 25.
   */
  private direccionLeida = false;

  /**
   * Escribe el recorrido en la dirección.
   *
   * <b>SOLO LO QUE ESTA PUESTO.</b> Un parámetro vacío por cada eje dejaría una dirección ilegible y
   * larguísima para decir «no hay ningún filtro».
   *
   * <b>Y AQUI ESTA LA DECISION QUE COSTO UNA PASADA.</b> La primera versión reemplazaba SIEMPRE la
   * entrada del historial, para que teclear en la búsqueda no dejara un paso por letra. Efecto
   * medido: el botón de atrás, después de bajar dos niveles del árbol, sacaba de la pantalla entera
   * —a «/administracion/resumen»— en vez de deshacer el último filtro. Es lo contrario de lo que
   * hace falta en una pantalla que existe para explorar.
   *
   * La regla es la que distingue los dos gestos: <b>elegir una faceta es un paso deliberado y APILA;
   * teclear en la búsqueda es continuo y REEMPLAZA</b>. Así, atrás deshace clics y no pulsaciones.
   */
  private escribirLaDireccion(): void {
    // No se escribe hasta haber leído: el orden importa y está explicado en `direccionLeida`.
    if (!this.direccionLeida) { return; }

    const parametros: Record<string, string> = {};
    for (const [clave, origen] of Object.entries(this.EJES_EN_LA_DIRECCION)) {
      const valor = origen().trim();
      if (valor) { parametros[clave] = valor; }
    }
    if (this.vista() !== 'lista') { parametros['vista'] = this.vista(); }
    if (this.ordenEnLaDireccion()) { parametros['orden'] = this.ordenEnLaDireccion(); }

    const firma = JSON.stringify(parametros);
    if (firma === this.ultimosParametros) { return; }

    // ¿Cambió algo que no sea el texto de búsqueda? Entonces es un paso y merece su entrada.
    const anterior: Record<string, string> = this.ultimosParametros ? JSON.parse(this.ultimosParametros) : {};
    const sinTexto = (p: Record<string, string>) => {
      const copia = { ...p };
      delete copia['q'];
      return JSON.stringify(copia);
    };
    const esPaso = this.ultimosParametros !== '' && sinTexto(anterior) !== sinTexto(parametros);
    this.ultimosParametros = firma;

    void this.enrutador.navigate([], {
      relativeTo: this.ruta,
      queryParams: parametros,
      replaceUrl: !esPaso,
    });
  }

  // ═════════════════════ Alta a partir de un documento ═════════════════════

  /** Si está abierto el lector de documentos. */
  readonly leyendoDocumento = signal(false);

  abrirLectura(): void {
    this.leyendoDocumento.set(true);
    this.error.set(null);
  }

  cerrarLectura(): void { this.leyendoDocumento.set(false); }

  /**
   * Abre el formulario con lo que el documento propuso.
   *
   * <b>SE TOMA LA PRIMERA PROPUESTA DE CADA CAMPO Y NADA MAS.</b> El servidor devuelve varias —el
   * título de los metadatos y el de la portada, por ejemplo— ordenadas por confianza. Volcarlas todas
   * sería imposible: un campo solo admite un valor. Se pone la primera y las demás quedan en el aviso,
   * porque lo que importa es que quien cataloga vea el formulario lleno y lo corrija, no que elija
   * entre variantes antes de ver nada.
   *
   * <b>Y NO SE GUARDA.</b> Queda como formulario abierto: la ficha se crea cuando una persona pulsa
   * guardar, que es la regla del proyecto sobre lo obtenido por importación asistida.
   */
  usarLaPropuesta(propuesta: PropuestaDeDocumento): void {
    const primero = (clave: string): string => {
      const lista = propuesta[clave] as ValorPropuesto[] | undefined;
      return Array.isArray(lista) && lista.length > 0 ? lista[0].valor : '';
    };

    const identificadores = (propuesta['identificadores'] as ValorPropuesto[] | undefined) ?? [];
    const agentes = (propuesta['agentes'] as ValorPropuesto[] | undefined) ?? [];
    const anio = primero('anios');

    this.ficha.set({
      ...fichaEnBlanco(),
      titulo: primero('titulos'),
      subtitulo: primero('subtitulos'),
      anioInicio: anio ? Number(anio) : null,
      paginas: (propuesta['paginas'] as string | null) ?? '',
      miniaturaRuta: (propuesta['portadaUrl'] as string | null) ?? '',
      // UN PDF ES DIGITAL, y eso no es una conjetura: es de donde salió el documento.
      formato: 'Digital',
      practicaMusical: primero('practicas'),
      tipoPublicacion: primero('tiposDePublicacion'),
      identificadores: identificadores.map(x => ({ esquema: x.valor.includes('979-0') ? 'ISMN' : 'ISBN', codigo: x.valor, cualificador: '' })),
      // LOS AGENTES RECONOCIDOS ENTRAN COMO CREDITOS, con el papel más común del acervo. El papel es
      // lo que el documento NO dice, así que se propone el más frecuente y se corrige de un clic.
      creditos: agentes.map((x, i) => ({
        nombre: x.valor,
        tipo: 'entidad' as const,
        rolCodigo: 'aut',
        rolEtiqueta: 'Autor',
        principal: i === 0,
      })),
    });

    const ruta = primero('rutas');
    if (ruta) { this.elegirRuta(ruta); }

    this.leyendoDocumento.set(false);
    this.paso.set(1);

    // EL AVISO DE RECONOCIMIENTO OPTICO TIENE QUE SOBREVIVIR AL ASISTENTE. Se comprobó en navegador:
    // el asistente lo decía muy bien y acto seguido se cerraba para abrir el formulario, así que la
    // advertencia desaparecía EXACTAMENTE cuando la persona empieza a editar, que es cuando sirve. El
    // aviso del formulario sí permanece, y es el sitio correcto para decirlo.
    const optico = propuesta.leidoConReconocimientoOptico === true;
    this.aviso.set(optico
      ? 'Formulario abierto con lo que se leyó del documento. La portada no tenía texto y se leyó con reconocimiento óptico: revisa con especial atención los nombres propios y los acentos. Nada se ha creado todavía.'
      : 'Formulario abierto con lo que se leyó del documento. Revisa cada campo antes de guardar: nada se ha creado todavía.');
  }

  // ─────────────────────────── Alta y edición ───────────────────────────

  nueva(): void {
    // EL CODIGO NO ES UN CAMPO DEL FORMULARIO. Lo asigna el servidor por orden de registro, así que
    // no lo decide nadie aquí; enseñarlo en solo lectura tampoco resolvía nada —«no tiene sentido
    // que diga código y que diga se asigna al guardar»— y ocupaba la primera ranura de la pantalla.
    // La ficha ya creada lo lleva en su cabecera, que es donde sirve.
    this.ficha.set(fichaEnBlanco());
    this.error.set(null);
    this.paso.set(1);
    // SE PREGUNTA ANTES DE ENCENDER EL AUTOGUARDADO: encender primero pisaría el borrador de la
    // vez anterior con el formulario vacío que acaba de abrirse.
    void this.buscarBorrador();
  }

  private async buscarBorrador(): Promise<void> {
    const encontrado = await this.autoguardado.recuperar();
    if (encontrado && this.ficha()?.id === null) {
      this.borradorRecuperable.set(encontrado);
    } else {
      this.autoguardado.encender();
    }
  }

  recuperarBorrador(): void {
    const encontrado = this.borradorRecuperable();
    if (!encontrado) { return; }
    this.ficha.set({ ...encontrado.datos, id: null });
    this.borradorRecuperable.set(null);
    this.autoguardado.encender();
  }

  descartarBorrador(): void {
    this.borradorRecuperable.set(null);
    void this.autoguardado.cerrar();
    this.autoguardado.encender();
  }

  // ─────────────────────────── Las listas de la ficha ───────────────────────────
  //
  // SE AÑADEN Y SE QUITAN FILAS, que es como se cataloga: una obra tiene los créditos que tiene y
  // los identificadores que tiene, no un número fijo. Cada cambio reemplaza el arreglo entero para
  // que la señal lo note; mutar en sitio dejaría la pantalla sin repintar.

  private cambiar(parcial: Partial<FormularioDeFicha>): void {
    const actual = this.ficha();
    if (actual) { this.ficha.set({ ...actual, ...parcial }); }
  }

  /**
   * ¿Este nombre parece dos personas?
   *
   * <b>PASO DE VERDAD Y NUEVE VECES.</b> El acervo traía nueve agentes cuyo nombre llevaba una coma,
   * y no eran «apellido, nombre»: eran DOS PERSONAS en un mismo registro —«Eblis Javier Álvarez
   * Vargas, Carlos Andrés Rico Carvajal»—. Con eso, buscar por el segundo no encontraba su obra y el
   * fichero de autoridades contaba un autor donde había dos. Los nueve se separaron; esto evita que
   * vuelva a entrar el décimo.
   *
   * AVISA, NO IMPIDE. Un nombre legítimo puede llevar coma —«Juana de Arco, la menor»— y decidirlo
   * es de quien cataloga, no del formulario.
   */
  pareceDosPersonas(nombre: string): boolean {
    return nombre.includes(',');
  }

  agregarCredito(): void {
    const f = this.ficha();
    if (!f) { return; }
    this.cambiar({ creditos: [...f.creditos, { nombre: '', tipo: 'persona', rolCodigo: 'aut', rolEtiqueta: 'Autor', principal: f.creditos.length === 0 }] });
  }

  quitarCredito(indice: number): void {
    const f = this.ficha();
    if (f) { this.cambiar({ creditos: f.creditos.filter((_, i) => i !== indice) }); }
  }

  campoDelCredito(indice: number, parcial: Partial<CreditoEnFormulario>): void {
    const f = this.ficha();
    if (!f) { return; }
    this.cambiar({ creditos: f.creditos.map((c, i) => i === indice ? { ...c, ...parcial } : c) });
  }

  /** Al elegir el papel, la etiqueta viene con él: es la que usa el acervo para ese código. */
  elegirRol(indice: number, codigo: string): void {
    const rol = this.vocabularios()?.rolesDeCredito.find(r => r.codigo === codigo);
    this.campoDelCredito(indice, { rolCodigo: codigo, rolEtiqueta: rol?.etiqueta ?? codigo });
  }

  agregarIdentificador(): void {
    const f = this.ficha();
    if (f) { this.cambiar({ identificadores: [...f.identificadores, { esquema: 'ISBN', codigo: '', cualificador: '' }] }); }
  }

  quitarIdentificador(indice: number): void {
    const f = this.ficha();
    if (f) { this.cambiar({ identificadores: f.identificadores.filter((_, i) => i !== indice) }); }
  }

  campoDelIdentificador(indice: number, parcial: Partial<IdentificadorEnFormulario>): void {
    const f = this.ficha();
    if (!f) { return; }
    this.cambiar({ identificadores: f.identificadores.map((x, i) => i === indice ? { ...x, ...parcial } : x) });
  }

  agregarAcceso(tipo: 'enlace' | 'ubicacion'): void {
    const f = this.ficha();
    if (!f) { return; }
    this.cambiar({ accesos: [...f.accesos, { tipo, url: '', ubicacionFisica: '', etiqueta: '', nota: '', archivoId: null }] });
  }

  quitarAcceso(indice: number): void {
    const f = this.ficha();
    if (f) { this.cambiar({ accesos: f.accesos.filter((_, i) => i !== indice) }); }
  }

  campoDelAcceso(indice: number, parcial: Partial<AccesoEnFormulario>): void {
    const f = this.ficha();
    if (!f) { return; }
    this.cambiar({ accesos: f.accesos.map((a, i) => i === indice ? { ...a, ...parcial } : a) });
  }

  /**
   * La tipología se elige por faceta: un valor de contenido, uno de medio, uno de soporte.
   *
   * SON CUATRO LISTAS INDEPENDIENTES, no una sola con 22 opciones. Mezclarlas dejaría elegir dos
   * soportes y ningún medio, que no es una clasificación válida.
   */
  tipologiaDelEje(eje: string): string {
    const codigos = this.ficha()?.tipologias ?? [];
    const opciones = this.vocabularios()?.tipologias.filter(t => t.eje === eje) ?? [];
    return opciones.find(o => codigos.includes(o.codigo))?.codigo ?? '';
  }

  elegirTipologia(eje: string, codigo: string): void {
    const f = this.ficha();
    if (!f) { return; }
    const delEje = new Set((this.vocabularios()?.tipologias ?? []).filter(t => t.eje === eje).map(t => t.codigo));
    const resto = f.tipologias.filter(c => !delEje.has(c));
    this.cambiar({ tipologias: codigo ? [...resto, codigo] : resto });
  }

  opcionesDeTipologia(eje: string) {
    return (this.vocabularios()?.tipologias ?? []).filter(t => t.eje === eje);
  }

  /** Las cuatro facetas, con la norma de la que sale cada una. */
  readonly ejesDeTipologia = [
    { clave: 'contenido', rotulo: 'Contenido', norma: 'RDA 336 · de qué está hecho' },
    { clave: 'medio', rotulo: 'Medio', norma: 'RDA 337 · cómo se percibe' },
    { clave: 'soporte', rotulo: 'Soporte', norma: 'RDA 338 · sobre qué viene' },
    { clave: 'recurso', rotulo: 'Tipo de recurso', norma: 'DCMI Type · para intercambiar la ficha' },
  ];

  /**
   * Lo escrito, para leerlo antes de guardar.
   *
   * ENSEÑA LO QUE TIENE VALOR Y CUENTA LO QUE ES LISTA. Repetir los treinta campos aquí sería otro
   * formulario; lo que hace falta antes de guardar es ver de un vistazo si falta algo importante.
   */
  resumenDeLaFicha(): { rotulo: string; valor: string }[] {
    const f = this.ficha();
    if (!f) { return []; }
    const lista = (cuantos: number, singular: string, plural: string) =>
      cuantos === 0 ? 'Ninguno' : `${cuantos} ${cuantos === 1 ? singular : plural}`;
    // EL CODIGO NO SE RESUME: no es una decisión de quien rellena, lo asigna el servidor al guardar.
    // Enseñarlo aquí volvería a plantear la pregunta que se acaba de quitar del formulario.
    const idiomas = this.idiomasDeLaFicha()
      .map(c => this.vocabularios()?.idiomas.find(i => i.codigo === c)?.nombre ?? c);
    return [
      { rotulo: 'Título', valor: [f.titulo, f.subtitulo, f.designacionVolumen].filter(Boolean).join(' · ') || '—' },
      { rotulo: 'Tipo y formato', valor: [f.tipoPublicacion, f.formato].filter(Boolean).join(' · ') || '—' },
      { rotulo: 'Años', valor: f.anioInicio ? `${f.anioInicio}${f.anioFin && f.anioFin !== f.anioInicio ? ' — ' + f.anioFin : ''}` : '—' },
      { rotulo: 'Idioma', valor: idiomas.join(' · ') || '—' },
      // La ubicación entera, que es de donde sale la sección: verla completa antes de guardar es lo
      // que permite darse cuenta de que la obra está colgando del sitio equivocado.
      { rotulo: 'Ubicación', valor: f.rutaSeccion || '—' },
      { rotulo: 'Créditos', valor: lista(f.creditos.filter(c => c.nombre.trim()).length, 'crédito', 'créditos') },
      { rotulo: 'Identificadores', valor: lista(f.identificadores.filter(i => i.codigo.trim()).length, 'identificador', 'identificadores') },
      { rotulo: 'Vías de consulta', valor: lista(f.accesos.filter(a => a.url.trim() || a.ubicacionFisica.trim()).length, 'vía', 'vías') },
      { rotulo: 'Tipología', valor: lista(f.tipologias.filter(Boolean).length, 'faceta', 'facetas') },
      { rotulo: 'Portada', valor: f.miniaturaRuta ? 'Cargada' : 'Sin portada' },
    ];
  }

  // ─────────────────────────── El idioma, que son varios ───────────────────────────
  //
  // EL CAMPO GUARDA UNA COMBINACION —`es`, `es ; en`, `es ; (lengua nativa)`— y el desplegable
  // enseñaba esas combinaciones como si fueran tres idiomas: «aparecen tres españoles diferentes»,
  // dijo la dirección de producto, y tenía razón. Ahora se marcan los idiomas uno a uno y la
  // combinación se arma al guardar, con el mismo separador que ya usa el acervo.

  /**
   * Los idiomas de la obra: el principal y, si hace falta, los demás.
   *
   * <b>SIETE CASILLAS NO ERAN LA RESPUESTA.</b> La primera versión pintaba una casilla por idioma
   * del vocabulario, y quedó descartado: para 171 fichas de las que 164 son solo en
   * español, obligar a puntear una lista es pedirle a todo el mundo que resuelva el caso raro. Un
   * idioma se ELIGE; un segundo idioma se AÑADE, que es una acción y no una pregunta.
   */
  idiomasDeLaFicha(): string[] {
    return (this.ficha()?.idioma ?? '').split(';').map(x => x.trim()).filter(Boolean);
  }

  cambiarIdioma(indice: number, codigo: string): void {
    const actuales = this.idiomasDeLaFicha();
    const siguientes = codigo
      ? actuales.map((x, i) => (i === indice ? codigo : x))
      : actuales.filter((_, i) => i !== indice);
    this.cambiar({ idioma: [...new Set(siguientes)].join(' ; ') });
  }

  agregarIdioma(): void {
    const actuales = this.idiomasDeLaFicha();
    const libre = (this.vocabularios()?.idiomas ?? []).find(i => !actuales.includes(i.codigo));
    if (libre) { this.cambiar({ idioma: [...actuales, libre.codigo].join(' ; ') }); }
  }

  /** Los que todavía se pueden añadir. Sin esto, «Agregar» ofrecería repetir el que ya está. */
  quedanIdiomasPorAgregar(): boolean {
    return (this.vocabularios()?.idiomas ?? []).length > this.idiomasDeLaFicha().length;
  }

  /**
   * Dónde vive la publicación dentro del acervo.
   *
   * <b>SE ELIGE EL SITIO Y LA SECCION SALE SOLA.</b> La ruta se escribía a mano y la sección se
   * elegía aparte, con lo que podían discrepar. La dirección de producto pidió que se calculara «como se
   * hace en el resto», y los datos dicen cómo: hay <b>22 rutas</b> para 9 secciones, y el primer
   * tramo de la ruta ES la sección en las 170 fichas que tienen ambas. Así que la ruta manda.
   */
  elegirRuta(ruta: string): void {
    const elegida = (this.vocabularios()?.rutas ?? []).find(r => r.ruta === ruta);
    this.cambiar({ rutaSeccion: ruta, seccionPrincipal: elegida?.seccion ?? '' });
  }

  /** Las rutas agrupadas por su sección, que es como se recorren al elegir. */
  rutasPorSeccion(): { seccion: string; rutas: { ruta: string; hoja: string; usos: number }[] }[] {
    const grupos = new Map<string, { ruta: string; hoja: string; usos: number }[]>();
    for (const r of this.vocabularios()?.rutas ?? []) {
      const tramos = r.ruta.split('>').map(x => x.trim());
      const lista = grupos.get(r.seccion) ?? [];
      // La hoja es lo que distingue una ruta de otra dentro de la misma sección; la sección ya
      // encabeza el grupo y repetirla en cada opción solo alarga la lista.
      lista.push({ ruta: r.ruta, hoja: tramos.slice(1).join(' › ') || r.seccion, usos: r.usos });
      grupos.set(r.seccion, lista);
    }
    return [...grupos.entries()].map(([seccion, rutas]) => ({ seccion, rutas }));
  }

  /**
   * ¿Tiene sentido pedir tamaño y número de páginas?
   *
   * SOLO SI HAY OBJETO FISICO. El criterio es este: «la descripción física solamente
   * aplica cuando el formato es físico, o mixto, no cuando es digital». Comprobado contra el acervo:
   * de las 17 digitales, las cuatro que traen tamaño son tres plegables IMPRESOS y la versión
   * digital de un libro de 500 páginas —es decir, casos mal clasificados o mixtos—.
   *
   * SE ENSEÑA IGUAL SI YA TIENE VALOR, para no esconder un dato que alguien escribió y dejar la
   * ficha sin forma de corregirlo.
   */
  aplicaDescripcionFisica(): boolean {
    const f = this.ficha();
    if (!f) { return false; }
    // MIENTRAS NO SE DECLARE EL FORMATO NO SE PREGUNTA. La primera versión la enseñaba también con
    // el formato sin elegir, «por si acaso», y eso es justo lo que se estaba quitando: pedir algo
    // antes de saber si aplica. Se decide en el paso anterior, que existe para eso.
    return f.formato === 'Físico' || f.formato === 'Mixto'
      || Boolean(f.tamanoFormato) || Boolean(f.paginas);
  }

  /**
   * ¿Y la duración?
   *
   * LA DURACION NO ES DESCRIPCION FISICA, aunque lo parezca. Comprobado: 33 publicaciones físicas la
   * declaran, pero también DOS videoclips digitales. Lo que la hace pertinente no es el soporte sino
   * que el contenido transcurra en el tiempo, así que la gobierna la faceta de contenido.
   */
  aplicaDuracion(): boolean {
    const f = this.ficha();
    if (!f) { return false; }
    const contenido = this.tipologiaDelEje('contenido');
    const enElTiempo = ['musica-interpretada', 'imagen-movimiento', 'palabra-hablada'];
    return enElTiempo.includes(contenido) || f.tipologias.some(c => enElTiempo.includes(c))
      || Boolean(f.duracion);
  }

  /**
   * ¿Se puede guardar ya?
   *
   * <b>LA CONDICION VIVE AQUI Y NO EN LA PLANTILLA POR LO QUE PASO.</b> Estaba escrita dentro del
   * `[disabled]` del botón y exigía el código; al quitar el código del formulario nadie la revisó, y
   * el botón dejó de habilitarse nunca: no se podía crear ninguna ficha. Una condición en la
   * plantilla no la mira ninguna prueba. Aquí sí.
   *
   * EL TITULO ES LO UNICO IMPRESCINDIBLE. El código lo pone el servidor y el resto de la ficha se
   * completa después: un catálogo se construye en varias sesiones, y exigirlo todo de una vez
   * empuja a inventar datos para poder guardar.
   */
  sePuedeGuardar(): boolean {
    return !this.guardando() && Boolean(this.ficha()?.titulo.trim());
  }

  irAlPaso(id: number): void { this.paso.set(id); }

  siguientePaso(): void { this.paso.set(Math.min(this.pasos.length, this.paso() + 1)); }

  pasoAnterior(): void { this.paso.set(Math.max(1, this.paso() - 1)); }

  editar(publicacion: PublicacionEditorial): void {
    this.ficha.set({
      id: publicacion.id,
      version: publicacion.version,
      codigo: publicacion.codigo,
      titulo: publicacion.titulo,
      subtitulo: publicacion.subtitulo ?? '',
      designacionVolumen: publicacion.designacionVolumen ?? '',
      serieOColeccion: publicacion.serieOColeccion ?? '',
      identificadores: (publicacion.identificadores ?? []).map(i => ({
        esquema: i.esquema, codigo: i.codigoRecibido, cualificador: i.cualificador ?? '',
      })),
      resumen: publicacion.resumen ?? '',
      textoPortada: publicacion.textoPortada ?? '',
      idioma: publicacion.idioma ?? '',
      anioInicio: publicacion.anioInicio,
      anioFin: publicacion.anioFin,
      fechaEdtf: publicacion.fechaEdtf ?? '',
      notaFecha: publicacion.notaFecha ?? '',
      tamanoFormato: publicacion.tamanoFormato ?? '',
      paginas: publicacion.paginas ?? '',
      duracion: publicacion.duracion ?? '',
      miniaturaRuta: publicacion.miniaturaRuta ?? '',
      seccionPrincipal: publicacion.seccionPrincipal ?? '',
      rutaSeccion: publicacion.rutaSeccion ?? '',
      categoriaId: publicacion.categoriaId,
      categoriaSecundaria: publicacion.categoriaSecundaria ?? '',
      subcategoria: publicacion.subcategoria ?? '',
      practicaMusical: publicacion.practicaMusical ?? '',
      ambito: publicacion.ambito ?? '',
      ambitoTexto: publicacion.ambitoTexto ?? '',
      tipoPublicacion: publicacion.tipoPublicacion ?? '',
      formato: publicacion.formato ?? '',
      tipologias: (publicacion.tipologia ?? []).map(x => x.codigo),
      // Se editan como una línea separada por comas: es como las dicta quien cataloga.
      // El servidor las recibe ya separadas y una por una.
      palabrasClave: publicacion.palabrasClave.join(', '),
      practicasMusicalesIds: publicacion.practicasMusicales.map(p => p.id),
      territoriosSonorosIds: publicacion.territoriosSonoros.map(t => t.id),
      creditos: (publicacion.creditos ?? []).map(c => ({
        nombre: c.agenteNombre, tipo: c.agenteTipo,
        rolCodigo: c.rolCodigo, rolEtiqueta: c.rolEtiqueta, principal: c.principal,
      })),
      accesos: (publicacion.accesos ?? []).map(a => ({
        tipo: a.tipo as 'enlace' | 'ubicacion' | 'archivo',
        url: a.url ?? '', ubicacionFisica: a.ubicacionFisica ?? '',
        etiqueta: a.etiqueta ?? '', nota: a.nota ?? '', archivoId: a.archivoId ?? null,
      })),
    });
    this.error.set(null);
    this.paso.set(1);
  }

  /**
   * Cierra la ficha sin guardar.
   *
   * EL BORRADOR NO SE TOCA. Cerrar sin querer es justo el accidente del que protege; borrarlo aquí
   * lo dejaría inútil. Se retira al guardar de verdad, o cuando alguien lo descarta a mano.
   */
  cerrarFicha(): void {
    this.autoguardado.apagar();
    this.borradorRecuperable.set(null);
    this.ficha.set(null);
  }

  campo<K extends keyof FormularioDeFicha>(clave: K, valor: FormularioDeFicha[K]): void {
    const actual = this.ficha();
    if (!actual) { return; }
    const siguiente = { ...actual, [clave]: valor };
    this.ficha.set(siguiente);
    if (siguiente.id === null) { this.autoguardado.anotar(siguiente); }
  }

  async guardarFicha(): Promise<void> {
    const ficha = this.ficha();
    if (!ficha) { return; }

    this.guardando.set(true);
    this.error.set(null);

    const texto = (valor: string) => valor.trim() || null;
    const cuerpo = {
      codigo: ficha.codigo.trim(),
      titulo: ficha.titulo.trim(),
      subtitulo: texto(ficha.subtitulo),
      designacionVolumen: texto(ficha.designacionVolumen),
      serieOColeccion: texto(ficha.serieOColeccion),
      resumen: texto(ficha.resumen),
      textoPortada: texto(ficha.textoPortada),
      idioma: texto(ficha.idioma),
      anioInicio: ficha.anioInicio,
      anioFin: ficha.anioFin,
      fechaEdtf: texto(ficha.fechaEdtf),
      notaFecha: texto(ficha.notaFecha),
      tamanoFormato: texto(ficha.tamanoFormato),
      paginas: texto(ficha.paginas),
      duracion: texto(ficha.duracion),
      miniaturaRuta: texto(ficha.miniaturaRuta),
      seccionPrincipal: texto(ficha.seccionPrincipal),
      rutaSeccion: texto(ficha.rutaSeccion),
      categoriaId: ficha.categoriaId,
      categoriaSecundaria: texto(ficha.categoriaSecundaria),
      subcategoria: texto(ficha.subcategoria),
      practicaMusical: texto(ficha.practicaMusical),
      ambito: texto(ficha.ambito),
      ambitoTexto: texto(ficha.ambitoTexto),
      tipoPublicacion: texto(ficha.tipoPublicacion),
      formato: texto(ficha.formato),
      palabrasClave: ficha.palabrasClave.split(',').map(x => x.trim()).filter(Boolean),
      practicasMusicalesIds: ficha.practicasMusicalesIds,
      territoriosSonorosIds: ficha.territoriosSonorosIds,
      // LAS CUATRO LISTAS VIAJAN SIEMPRE, incluso vacías. El servidor distingue ausente de vacío:
      // omitirlas dejaría lo anterior, y entonces retirar el último crédito de una ficha sería
      // imposible desde la consola.
      tipologias: ficha.tipologias.filter(Boolean),
      identificadores: ficha.identificadores
        .filter(i => i.codigo.trim())
        .map((i, orden) => ({
          esquema: i.esquema, codigo: i.codigo.trim(),
          cualificador: texto(i.cualificador), orden,
        })),
      creditos: ficha.creditos
        .filter(c => c.nombre.trim() && c.rolCodigo)
        .map((c, orden) => ({
          nombre: c.nombre.trim(), tipo: c.tipo,
          rolCodigo: c.rolCodigo, rolEtiqueta: c.rolEtiqueta.trim() || c.rolCodigo,
          principal: c.principal, orden,
        })),
      accesos: ficha.accesos
        .filter(a => a.url.trim() || a.ubicacionFisica.trim() || a.archivoId)
        .map((a, orden) => ({
          tipo: a.tipo, archivoId: a.archivoId,
          url: texto(a.url), ubicacionFisica: texto(a.ubicacionFisica),
          etiqueta: texto(a.etiqueta), nota: texto(a.nota), orden,
        })),
    };

    const resultado = ficha.id === null
      ? await this.api.crear(cuerpo as never)
      // CITA LA VERSION QUE SE ABRIO. Si otra persona guardó entre medias, el servidor rechaza
      // en vez de pisar su trabajo, y el mensaje pide recargar.
      : await this.api.guardar(ficha.id, { ...cuerpo, version: ficha.version } as never);

    this.guardando.set(false);

    if (!resultado.ok || !resultado.data) {
      this.error.set(resultado.error ?? 'No fue posible guardar la publicación.');
      return;
    }

    this.aviso.set(`«${resultado.data.titulo}» quedó guardada.`);
    // EL BORRADOR AUTOMATICO SE RETIRA AQUI: la ficha ya está en su tabla, y conservarlo
    // ofrecería recuperar algo que ya existe.
    if (ficha.id === null) { await this.autoguardado.cerrar(); } else { this.autoguardado.apagar(); }
    this.ficha.set(null);
    await this.cargar();
  }

  /*
    EL CODIGO NUNCA SALE A PANTALLA. El respaldo era `?? codigo`, así que un estado que el servidor
    añadiera y este diccionario no conociera se leía «en_revision» en la tabla. El respaldo
    compartido lo escribe en lenguaje humano.
  */
  etiquetaCatalogacion(codigo: string): string { return ETIQUETAS_CATALOGACION[codigo] ?? etiquetaDeEstado(codigo); }
  etiquetaPublicacion(codigo: string): string { return ETIQUETAS_PUBLICACION[codigo] ?? etiquetaDeEstado(codigo); }

  /**
   * Trae el acervo entero.
   *
   * <b>YA NO SE PIDE FILTRADO AL SERVIDOR.</b> Antes cada búsqueda y cada cambio de estado era una
   * petición, y el servidor solo sabía filtrar por título, código y estado: no había forma de
   * preguntar «¿cuántas de Banda?» sin traerlas todas de todos modos. Con las 171 en memoria, cada
   * faceta y cada recuento son inmediatos y no cuesta ni una petición más.
   */
  async cargar(): Promise<void> {
    if (this.cargando()) { return; }
    this.cargando.set(true);
    this.error.set(null);
    const resultado = await this.api.listarTodasInternas();
    this.cargando.set(false);

    if (!resultado.ok || !resultado.data) {
      this.error.set(resultado.error ?? 'No fue posible consultar el Catálogo Editorial.');
      return;
    }
    this.acervo.set(resultado.data);
    this.total.set(resultado.data.length);
  }

  async filtrarPor(estado: string): Promise<void> {
    this.filtroEstado.set(estado);
    await this.cargar();
  }

  async catalogar(ficha: PublicacionEditorial, estado: string): Promise<void> {
    await this.aplicar(ficha, () => this.api.cambiarCatalogacion(ficha.id, estado));
  }

  // ═══════════════════════ Actuar sobre varias a la vez ═══════════════════════
  //
  // POR QUE HACE FALTA. Con 171 publicaciones, retirar una sección entera del portal o validar las
  // doce fichas que acaba de revisar alguien significaba abrir el menú de cada fila, una por una.
  // El índice ya sabe acotar el conjunto —«las de Banda de 2015-2019»—; actuar sobre ese conjunto es
  // el paso que faltaba.
  //
  // SOLO SOBRE LO QUE SE VE. La selección se limita a las fichas visibles: una acción que alcanzara
  // a registros fuera de la pantalla es exactamente el accidente del que hay que proteger.

  readonly seleccion = signal<ReadonlySet<number>>(new Set());

  readonly seleccionadas = computed(() => this.fichasVisibles().filter(f => this.seleccion().has(f.id)));

  readonly todasSeleccionadas = computed(() => {
    const visibles = this.fichasVisibles();
    return visibles.length > 0 && visibles.every(f => this.seleccion().has(f.id));
  });

  /**
   * Lo que se puede hacer con lo que está elegido, y solo eso.
   *
   * <b>LA BARRA OFRECIA LOS CUATRO ESTADOS SIEMPRE.</b> Con una publicación ya publicada elegida
   * seguía diciendo «Publicar», que no hace nada, en vez de «Despublicar», que es la decisión que
   * cabe. Lo reportó la dirección de producto, y es la regla del proyecto:
   * una acción solo se muestra si de verdad está disponible para el estado actual del registro.
   *
   * <b>CON UNA SELECCION MEZCLADA SE OFRECE LO QUE CAMBIA ALGO</b>, y el rótulo dice sobre cuántas:
   * elegir treinta de las que veinte ya están publicadas y ver «Publicar 10» es más honesto que
   * «Publicar» a secas, que haría esperar treinta.
   */
  readonly accionesEnLote = computed(() => {
    const elegidas = this.seleccionadas();
    const cuantas = (estado: string) => elegidas.filter(f => f.estadoPublicacion !== estado).length;
    const rotulo = (verbo: string, total: number) =>
      total === elegidas.length ? verbo : `${verbo} ${total}`;

    const acciones: { id: string; estado: string; etiqueta: string; importancia: ImportanciaDeBoton }[] = [];
    const porPublicar = cuantas('publicado');
    if (porPublicar > 0) {
      acciones.push({ id: 'publicar', estado: 'publicado', etiqueta: rotulo('Publicar', porPublicar), importancia: 'confirmar' });
    }
    const porRevisar = cuantas('en_revision');
    if (porRevisar > 0) {
      acciones.push({ id: 'revisar', estado: 'en_revision', etiqueta: rotulo('Enviar a revisión', porRevisar), importancia: 'secundaria' });
    }
    const porVolver = cuantas('borrador');
    if (porVolver > 0) {
      acciones.push({ id: 'borrador', estado: 'borrador', etiqueta: rotulo('Volver a borrador', porVolver), importancia: 'secundaria' });
    }
    return acciones;
  });

  /**
   * Cuántas de las elegidas se pueden despublicar: las que están publicadas.
   *
   * VA APARTE de las demás porque es la única con consecuencia pública, y en la barra se dibuja
   * separada por un filete y en rojo.
   */
  readonly cuantasSePuedenDespublicar = computed(
    () => this.seleccionadas().filter(f => f.estadoPublicacion === 'publicado').length,
  );

  /** «Despublicar», y con el número solo cuando no son todas las elegidas. */
  readonly rotuloDeDespublicar = computed(() => {
    const cuantas = this.cuantasSePuedenDespublicar();
    return cuantas === this.seleccionadas().length ? 'Despublicar' : `Despublicar ${cuantas}`;
  });

  /**
   * Hay ALGUNAS elegidas, pero no todas: la casilla de la cabecera se pinta a medias.
   *
   * Sin este estado, con tres de 171 elegidas la casilla de arriba aparece vacía y pulsarla parece
   * que va a elegir tres más, cuando lo que hace es elegirlas todas.
   */
  readonly algunasSeleccionadas = computed(() => {
    const elegidas = this.seleccion().size;
    return elegidas > 0 && !this.todasSeleccionadas();
  });

  estaSeleccionada(id: number): boolean { return this.seleccion().has(id); }

  quitarLaSeleccion(): void { this.seleccion.set(new Set()); }

  alternarSeleccion(id: number): void {
    const copia = new Set(this.seleccion());
    if (copia.has(id)) { copia.delete(id); } else { copia.add(id); }
    this.seleccion.set(copia);
  }

  /** Marca o desmarca TODO lo visible, que es el conjunto sobre el que se puede actuar. */
  alternarTodas(): void {
    this.seleccion.set(this.todasSeleccionadas() ? new Set() : new Set(this.fichasVisibles().map(f => f.id)));
  }

  /**
   * Aplica un cambio de publicación a las seleccionadas.
   *
   * <b>UNA A UNA Y EN ORDEN, no en paralelo.</b> Cada llamada puede fallar por su cuenta —una ficha
   * sin fuente no se publica— y el servidor devuelve el motivo de ESA ficha. Lanzarlas todas a la vez
   * mezclaría los motivos y dejaría sin saber cuál falló por qué.
   *
   * <b>LO QUE FALLA SE DICE CON NOMBRE Y APELLIDO.</b> Un «no se pudieron publicar 3» obliga a
   * revisar las doce a mano para encontrarlas.
   */
  async publicarSeleccionadas(estado: string): Promise<void> {
    const elegidas = this.seleccionadas();
    if (elegidas.length === 0) { return; }

    this.aviso.set(null);
    this.error.set(null);
    const fallidas: string[] = [];
    let logradas = 0;

    for (const ficha of elegidas) {
      this.trabajando.set(ficha.id);
      const resultado = await this.api.cambiarPublicacion(ficha.id, estado);
      if (resultado.ok && resultado.data) {
        const actualizada = resultado.data;
        this.acervo.update(lista => lista.map(x => (x.id === actualizada.id ? actualizada : x)));
        logradas++;
      } else {
        fallidas.push(`${ficha.codigo} (${resultado.error ?? 'motivo no indicado'})`);
      }
    }

    this.trabajando.set(null);
    this.seleccion.set(new Set());

    if (logradas > 0) {
      this.aviso.set(`${logradas} ${logradas === 1 ? 'publicación quedó' : 'publicaciones quedaron'} como ${this.etiquetaPublicacion(estado).toLowerCase()}.`);
    }
    if (fallidas.length > 0) {
      this.error.set(`No se pudo con ${fallidas.length === 1 ? 'una' : fallidas.length}: ${fallidas.join('; ')}.`);
    }
  }

  async publicar(ficha: PublicacionEditorial, estado: string): Promise<void> {
    await this.aplicar(ficha, () => this.api.cambiarPublicacion(ficha.id, estado));
  }

  private async aplicar(
    ficha: PublicacionEditorial,
    operacion: () => Promise<{ ok: boolean; data?: PublicacionEditorial | null; error?: string }>,
  ): Promise<void> {
    this.trabajando.set(ficha.id);
    this.aviso.set(null);
    this.error.set(null);
    const resultado = await operacion();
    this.trabajando.set(null);

    if (!resultado.ok || !resultado.data) {
      // El motivo del servidor se enseña tal cual: es el que dice QUÉ falta para publicar.
      this.error.set(resultado.error ?? 'No fue posible aplicar el cambio.');
      return;
    }
    const actualizada = resultado.data;
    this.acervo.update(lista => lista.map(x => (x.id === actualizada.id ? actualizada : x)));
    this.aviso.set(`«${actualizada.titulo}» quedó como ${this.etiquetaPublicacion(actualizada.estadoPublicacion).toLowerCase()}.`);
  }
}
