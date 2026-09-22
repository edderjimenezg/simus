import { FECHA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { PrevisualizacionDePiezaComponent } from '../../../shared/components/ui/previsualizacion-de-pieza/previsualizacion-de-pieza.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { etiquetaDeEstado } from '../domain/admin-config';
import { CommonModule, formatDate } from '@angular/common';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { SelectorDeCategoriaComponent } from '../../../shared/components/ui/selector-de-categoria/selector-de-categoria.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { SelectorDeClasificacionComponent } from '../../../shared/components/ui/selector-de-clasificacion/selector-de-clasificacion.component';
import { SelectorDeProyectosComponent } from '../../../shared/components/ui/selector-de-proyectos/selector-de-proyectos.component';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { IndicadorDePasosComponent, PasoDelIndicador } from '../../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { AutoguardadoDeBorrador } from '../../../core/services/autoguardado-de-borrador';
import { BorradoresDeConsolaService } from '../../../core/services/borradores-de-consola.service';
import { CategoriasDeContenidoService } from '../../../core/services/categorias-de-contenido.service';
import { BancoDeArchivosService } from '../../../core/services/banco-de-archivos.service';
import { FormsModule } from '@angular/forms';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ETIQUETAS_ESTADO_NOTICIA, Noticia, NoticiasService } from '../../../core/services/noticias.service';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';

/**
 * Noticias en la consola.
 *
 * <b>PUBLICAR PUEDE FALLAR, Y ESTA BIEN QUE FALLE AQUI.</b> El servidor exige título, resumen,
 * cuerpo y fecha, y cuando falta algo devuelve TODO lo que falta en un solo mensaje. Esta pantalla
 * lo enseña tal cual en vez de traducirlo: una interfaz que escondiera esos motivos obligaría a
 * adivinar por qué un botón no hace nada.
 *
 * <b>LA DIRECCION SE ENSEÑA PERO NO SE EDITA AL GUARDAR.</b> Se calcula del título al crear y
 * después queda fija, porque un enlace compartido no puede dejar de funcionar porque alguien
 * corrigiera una tilde. Decirlo en pantalla evita que alguien lo intente y crea que falló.
 */

/** Lo que el formulario mantiene mientras se escribe. Plano a propósito: no es la noticia. */
interface FormularioDeNoticia {
  id: number | null;
  version: number;
  slug: string;
  titulo: string;
  resumen: string;
  cuerpo: string;
  fechaPublicacion: string;
  imagenRuta: string;
  imagenAlternativa: string;
  autoriaNombre: string;
  categoriaId: number | null;
  etiquetas: string;
  /** Clasificación opcional contra los catálogos del sistema. */
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
  /** Iniciativas del Programa con las que se enlaza. No es la categoría: es otra pregunta. */
  proyectosTransversalesIds: number[];
  /** La imagen sale del banco de archivos, no de una ruta escrita a mano. */
  imagenArchivoId: number | null;
  imagenUrl: string;
  imagenAlt: string;
}

/**
 * Los cuatro pasos del formulario, en el orden en que se recorren.
 *
 * <b>POR QUE POR PASOS.</b> De una sola tirada el formulario se lee como un muro en el que es
 * fácil saltarse algo o abandonar a la mitad. Agrupados por pregunta —qué se cuenta, con qué se
 * ilustra, cómo se clasifica y una revisión— cada pantalla cabe de una ojeada. Es la misma forma
 * que ya tienen el asistente externo de Festival y la Agenda.
 */
export const PASOS_DE_LA_NOTICIA: readonly PasoDelIndicador[] = [
  { id: 1, titulo: 'Contenido' },
  { id: 2, titulo: 'Imagen' },
  { id: 3, titulo: 'Clasificación' },
  { id: 4, titulo: 'Revisión' },
];

@Component({
  selector: 'app-admin-noticias-panel',
  standalone: true,
  imports: [
    DialogoDirective, BarraDeListaComponent,PrevisualizacionDePiezaComponent, DatoEnLecturaComponent, PanelLateralComponent, SelectorSegmentadoComponent, FiltroDesplegableComponent, CabeceraDeTablaComponent, BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent, BuscadorDeListaComponent, 
    CommonModule, FormsModule,
    SelectorDeCategoriaComponent, SelectorDeClasificacionComponent, SelectorDeProyectosComponent, IndicadorDePasosComponent,
    SelloDeProcedenciaComponent, HistorialDeRegistroComponent, MenuDeAccionesComponent, IndicadorDeEstadoComponent, DialogoDePrevisualizacionComponent],
  templateUrl: './admin-noticias-panel.component.html',
})
export class AdminNoticiasPanelComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(NoticiasService);
  private readonly categorias = inject(CategoriasDeContenidoService);
  private readonly banco = inject(BancoDeArchivosService);
  private readonly borradores = inject(BorradoresDeConsolaService);

  /** El borrador que se guarda solo mientras se escribe. Solo para noticias nuevas. */
  readonly autoguardado = new AutoguardadoDeBorrador<FormularioDeNoticia>(this.borradores.transporte('noticias'));

  readonly pasos = PASOS_DE_LA_NOTICIA;
  readonly paso = signal(1);

  readonly borradorRecuperable = signal<{ datos: FormularioDeNoticia; fecha: string } | null>(null);

  readonly subiendoImagen = signal(false);
  readonly errorDeImagen = signal<string | null>(null);

  readonly enabled = input(true);

  readonly noticias = signal<Noticia[]>([]);
  readonly total = signal(0);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly filtroEstado = signal<string>('todos');
  readonly filtroCategoria = signal<string>('todas');

  /**
   * En qué página está y cuántas hay.
   *
   * <b>NOTICIAS PAGINABA Y NO TENIA CON QUE PASAR DE PAGINA.</b> El servidor responde por páginas
   * desde siempre y la consola pedía la primera y solo la primera: con más registros que el tamaño
   * de página, el resto era inalcanzable. Es el mismo defecto que tenía la Agenda —«no aparecen los
   * eventos publicados»— y se corrige igual, con el mismo pie de tabla.
   */
  readonly pagina = signal(1);
  readonly totalDePaginas = signal(1);

  /**
   * Las categorías como posiciones de un selector, «Todas» delante.
   *
   * SIN CIFRA EN CADA PESTAÑA. Contarlas sobre las noticias cargadas daría el número de la página y
   * no el del catálogo, que es la clase de cifra filtrada junto a una global que ya se retiró de
   * otras pantallas. La cifra de «Todas» sí es del servidor y por eso se queda.
   */
  readonly pestanasDeCategoria = computed<readonly OpcionSegmentada[]>(() => [
    { id: 'todas', etiqueta: 'Todas', conteo: this.total() },
    ...this.categoriasEnUso().map(c => ({ id: c.nombre, etiqueta: c.nombre })),
  ]);

  /**
   * Si hay algún filtro puesto que pueda estar escondiendo noticias que sí existen.
   *
   * La pestaña de categoría no cuenta: es la lista que se eligió mirar. La nombra
   * `ambitoDeLaLista`, para no afirmar que no hay noticias cuando las hay en otra categoría.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.busqueda().trim().length > 0 || this.filtroEstado() !== 'todos',
  );

  readonly ambitoDeLaLista = computed(() => {
    const categoria = this.filtroCategoria();
    return categoria === 'todas' ? null : `en «${categoria}»`;
  });

  /**
   * Quita la búsqueda y el estado, y deja la pestaña de categoría donde está.
   *
   * <b>EL EFECTO NO DEPENDE DE LA BUSQUEDA</b> —la aplica `buscar()` al escribir—, así que al
   * limpiar solo el texto nadie volvía a pedir la lista y la pantalla se quedaba afirmando que no
   * hay noticias con siete en la base. Si el estado sí cambia, recarga el efecto y no se pide dos
   * veces la misma página.
   */
  limpiarFiltros(): void {
    const recargaraElEfecto = this.filtroEstado() !== 'todos';
    this.busqueda.set('');
    this.filtroEstado.set('todos');
    if (!recargaraElEfecto) {
      this.pagina.set(1);
      void this.cargar();
    }
  }

  readonly FILTRO_DE_ESTADO: readonly OpcionDeFiltro[] = [
    { id: 'todos', etiqueta: 'Todas' },
    { id: 'borrador', etiqueta: 'Borradores' },
    { id: 'en_revision', etiqueta: 'En revisión' },
    { id: 'publicado', etiqueta: 'Publicadas' },
    { id: 'archivado', etiqueta: 'Archivadas' },
  ];

  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'noticia', etiqueta: 'Noticia' },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'fecha', etiqueta: 'Fecha' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  /**
   * El orden de la tabla, en la pieza compartida.
   *
   * <b>ARRANCA SIN COLUMNA.</b> El orden de partida es el de trabajo —sin publicar primero y
   * dentro de eso lo último tocado—, que no es el de ninguna columna. Marcar «Fecha» diría que la
   * tabla está ordenada por fecha, y no lo está.
   */
  readonly orden = new OrdenDeTabla('', 'asc');

  /**
   * Pulsar una cabecera: se lo pide al servidor y se vuelve a la primera página.
   *
   * ORDENAR EN MEMORIA ERA EL DEFECTO. Noticias pagina, así que ordenar las filas cargadas
   * reordenaba una página y dejaba el resto donde estaba. Ver la misma corrección en la Agenda.
   */
  ordenarPor(columna: string): void {
    this.orden.alternar(columna);
    this.pagina.set(1);
    void this.cargar();
  }

  /**
   * Las categorías del vocabulario que ALGUIEN usa, no las de la página cargada.
   *
   * <b>SE LEEN DEL CATALOGO Y NO DE LAS NOTICIAS CARGADAS.</b> Contarlas sobre la página ofrecería
   * dos categorías de veinte y escondería las demás en cuanto haya más de una página. Es el mismo
   * criterio que la Agenda, y por la misma razón: el filtro lo aplica el servidor.
   */
  readonly categoriasEnUso = signal<{ nombre: string; total: number }[]>([]);

  private async cargarCategorias(): Promise<void> {
    // LAS PROPIAS Y LAS COMUNES: el vocabulario reparte las categorías por módulo y deja un grupo
    // compartido para lo que cruza los tres.
    const [propias, comunes] = await Promise.all([
      this.categorias.listar('noticias'),
      this.categorias.listar('comun'),
    ]);
    const filas = propias.ok && propias.data ? propias.data.items : [];
    const todas = [...filas, ...(comunes.ok && comunes.data ? comunes.data.items : [])];
    this.categoriasEnUso.set(
      todas
        .filter(c => c.contenidosQueLaUsan > 0)
        .map(c => ({ nombre: c.nombreCategoria, total: c.contenidosQueLaUsan }))
        .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')),
    );
  }

  /**
   * Lo que se pinta: la página tal y como la devolvió el servidor.
   *
   * AQUI NO SE FILTRA NI SE ORDENA NADA. La búsqueda, el estado, la categoría y el orden los
   * resuelve el servidor sobre todas las noticias; cualquier criterio local operaría sobre la
   * página y mentiría sobre el resto.
   */
  readonly noticiasVisibles = computed(() => this.noticias());
  readonly busqueda = signal('');

  readonly formulario = signal<FormularioDeNoticia | null>(null);
  readonly guardando = signal(false);

  readonly etiquetasDeEstado = ETIQUETAS_ESTADO_NOTICIA;

  /*
    EL CODIGO NUNCA SALE A PANTALLA: el respaldo era `|| noticia.estado`, que escribia «en_revision»
    tal cual en la tabla. El respaldo compartido lo pasa a lenguaje humano.
  */
  rotuloDeEstado(codigo: string | null | undefined): string {
    return this.etiquetasDeEstado[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }


  /** La tabla con la que Noticias escribe su bitácora. */
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.noticias;

  readonly historialAbierto = signal<Noticia | null>(null);

  constructor() {
    // `untracked` PORQUE `cargar()` LEE Y ESCRIBE `cargando` ANTES DE SU PRIMER `await`. Sin esto
    // el efecto se suscribiría a su propia escritura y la consulta se repetiría en bucle.
    effect(() => {
      const activo = this.enabled();
      // Se LEEN para que el efecto dependa de los dos; sus valores los relee `cargar()`.
      this.filtroEstado();
      this.filtroCategoria();
      if (!activo) { return; }
      // CAMBIAR DE FILTRO DEVUELVE A LA PRIMERA PAGINA: la tercera de un filtro no existe en otro.
      untracked(() => { this.pagina.set(1); void this.cargar(); void this.cargarCategorias(); });
    });
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);

    const resultado = await this.api.listarInternas({
      q: this.busqueda().trim() || undefined,
      estado: this.filtroEstado() === 'todos' ? undefined : this.filtroEstado(),
      // AL SERVIDOR Y NO EN MEMORIA: con la lista paginada, filtrar la página que se tiene delante
      // respondería «no hay ninguna de esa categoría» cuando lo que pasa es que están en otra.
      categoria: this.filtroCategoria() === 'todas' ? undefined : this.filtroCategoria(),
      orden: this.orden.columna() || undefined,
      direccion: this.orden.columna() ? this.orden.direccion() : undefined,
      pagina: this.pagina(),
    });

    if (resultado.ok && resultado.data) {
      this.noticias.set(resultado.data.items);
      this.total.set(resultado.data.total);
      this.totalDePaginas.set(Math.max(1, resultado.data.totalPaginas || 1));
    } else {
      this.noticias.set([]);
      this.total.set(0);
      this.totalDePaginas.set(1);
      this.error.set(resultado.error ?? 'No fue posible consultar las noticias.');
    }

    this.cargando.set(false);
  }

  /** Buscar devuelve a la primera página. Llega con el texto ya esperado por el buscador. */
  buscar(texto: string): void {
    this.busqueda.set(texto);
    this.pagina.set(1);
    void this.cargar();
  }

  irALaPagina(numero: number): void {
    const destino = Math.min(Math.max(1, numero), this.totalDePaginas());
    if (destino === this.pagina()) { return; }
    this.pagina.set(destino);
    void this.cargar();
    // ARRIBA DEL TODO: la fila que se estaba mirando ya no existe.
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /**
   * La noticia que se está LEYENDO, que no es la que se está editando.
   *
   * Dos señales porque son dos cosas: `formulario()` abre una pantalla para escribir, con borrador
   * y validación; esto abre un cajón para mirar, del que se sale sin guardar nada.
   */
  readonly fichaEnLectura = signal<Noticia | null>(null);

  consultar(noticia: Noticia): void { this.fichaEnLectura.set(noticia); }
  cerrarLaFicha(): void { this.fichaEnLectura.set(null); }

  editarDesdeLaFicha(noticia: Noticia): void {
    this.fichaEnLectura.set(null);
    this.editar(noticia);
  }

  abrirHistorial(noticia: Noticia): void { this.historialAbierto.set(noticia); }

  /**
   * Las acciones que esta noticia admite hoy.
   *
   * MISMO CICLO, MISMAS PALABRAS Y MISMO ORDEN QUE AGENDA Y CATALOGO EDITORIAL. Las que no aplican
   * al estado no se pintan: la lista es la respuesta, no la pregunta.
   */
  accionesDe(noticia: Noticia): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [];
    // VER VA PRIMERO Y SIEMPRE, como en el Catálogo y en la Agenda: es lo único que se puede hacer
    // con cualquier noticia, y hasta hoy leerla obligaba a abrir el formulario de EDICION.
    acciones.push({ id: 'consultar', etiqueta: 'Abrir ficha' });
    // «PUBLICADO» NO ES LO MISMO QUE «SE VE HOY». Una noticia con fecha futura está guardada como
    // publicada y no aparece en el portal hasta su día: ofrecerle «Ver en el portal» llevaba a un
    // 404, que es exactamente la acción que promete algo que no hay. Lo que decide qué se ofrece es
    // el estado EFECTIVO, que el servidor ya calcula.
    const seVeHoy = noticia.estadoEfectivo === 'publicado';
    acciones.push(
      noticia.estado === 'publicado'
        ? { id: 'editar', etiqueta: 'Editar' }
        : { id: 'publicar', etiqueta: 'Publicar', tono: 'principal' });
    if (noticia.estado !== 'publicado') { acciones.push({ id: 'editar', etiqueta: 'Editar' }); }
    if (noticia.estado === 'borrador') { acciones.push({ id: 'revisar', etiqueta: 'Enviar a revisión' }); }
    // PREVISUALIZAR SOLO CUANDO TODAVIA NO SE VE. Si ya está en la calle, lo que sirve es abrirla;
    // ofrecer las dos cosas obliga a decidir entre dos acciones que hacen casi lo mismo.
    if (!seVeHoy) { acciones.push({ id: 'previsualizar', etiqueta: 'Previsualizar' }); }
    if (noticia.estado === 'publicado') { acciones.push({ id: 'despublicar', etiqueta: 'Despublicar' }); }
    if (seVeHoy) {
      acciones.push({ id: 'ver', etiqueta: 'Ver en el portal', enlace: this.rutaPublica(noticia), nuevaPestana: true });
    }
    acciones.push({ id: 'historial', etiqueta: 'Historial' });
    if (noticia.estado === 'publicado') { acciones.push({ id: 'archivar', etiqueta: 'Archivar', tono: 'peligro' }); }
    return acciones;
  }

  ejecutarAccion(noticia: Noticia, accion: string): void {
    switch (accion) {
      case 'consultar': this.consultar(noticia); break;
      case 'editar': this.editar(noticia); break;
      case 'revisar': this.cambiarEstado(noticia, 'en_revision'); break;
      case 'publicar': this.cambiarEstado(noticia, 'publicado'); break;
      case 'despublicar': this.cambiarEstado(noticia, 'borrador'); break;
      case 'archivar': this.cambiarEstado(noticia, 'archivado'); break;
      case 'previsualizar': this.previsualizando.set(noticia); break;
      case 'historial': this.abrirHistorial(noticia); break;
    }
  }

  cerrarHistorial(): void { this.historialAbierto.set(null); }

  /** La noticia que se está mirando como se verá en su listado, o `null`. */
  readonly previsualizando = signal<Noticia | null>(null);

  cerrarPrevisualizacion(): void { this.previsualizando.set(null); }

  /** Autoría y fecha, en la línea que va bajo el título de una noticia. */
  firmaDeLaNoticia(noticia: Noticia): string {
    const fecha = noticia.fechaPublicacion
      ? formatDate(noticia.fechaPublicacion, FECHA_ADMINISTRATIVA, 'es-CO')
      : 'Sin fecha de aparición';
    return noticia.autoriaNombre ? `${noticia.autoriaNombre} · ${fecha}` : fecha;
  }

  /** Lo que acompaña a la noticia sin ser su texto. Las listas vacías no se pintan. */
  datosDeLaNoticia(noticia: Noticia): { rotulo: string; valor: string }[] {
    const datos: { rotulo: string; valor: string }[] = [];
    if (noticia.etiquetas.length) { datos.push({ rotulo: 'Etiquetas', valor: noticia.etiquetas.join(', ') }); }
    if (noticia.practicasMusicales.length) {
      datos.push({ rotulo: 'Prácticas musicales', valor: noticia.practicasMusicales.map(p => p.nombre).join(', ') });
    }
    if (noticia.territoriosSonoros.length) {
      datos.push({ rotulo: 'Territorios sonoros', valor: noticia.territoriosSonoros.map(t => t.nombre).join(', ') });
    }
    return datos;
  }

  /** Cómo se llama lo que se está previsualizando. Se escribe fuera del marco. */
  nombreParaPrevisualizar(registro: Noticia): string { return registro.titulo; }

  /**
   * En qué estado está hoy, en palabras.
   *
   * SE LEE EL ESTADO EFECTIVO DONDE LO HAY: una noticia guardada como publicada con fecha futura
   * está «Programada», y decirle «Publicado» al lado de una previsualización que avisa de que
   * todavía no es pública sería contradecirse en la misma caja.
   */
  estadoParaPrevisualizar(registro: Noticia): string { return this.etiquetasDeEstado[registro.estadoEfectivo] ?? registro.estadoEfectivo; }

  /** La dirección pública de la noticia. Aquí sí hay ficha propia en el portal. */
  rutaPublica(noticia: Noticia): string { return `/noticias/${noticia.slug}`; }

  nueva(): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({
      id: null, version: 0, slug: '', titulo: '', resumen: '', cuerpo: '',
      fechaPublicacion: '', imagenRuta: '', imagenAlternativa: '', autoriaNombre: '', categoriaId: null, etiquetas: '',
      practicasMusicalesIds: [], territoriosSonorosIds: [], proyectosTransversalesIds: [],
      imagenArchivoId: null, imagenUrl: '', imagenAlt: '',
    });
    this.paso.set(1);
    // SE PREGUNTA ANTES DE ENCENDER EL AUTOGUARDADO: encender primero pisaría el borrador de la
    // vez anterior con el formulario vacío que acaba de abrirse.
    void this.buscarBorrador();
  }

  private async buscarBorrador(): Promise<void> {
    const encontrado = await this.autoguardado.recuperar();
    if (encontrado && this.formulario()?.id === null) {
      this.borradorRecuperable.set(encontrado);
    } else {
      this.autoguardado.encender();
    }
  }

  recuperarBorrador(): void {
    const encontrado = this.borradorRecuperable();
    if (!encontrado) { return; }
    this.formulario.set({ ...encontrado.datos, id: null });
    this.borradorRecuperable.set(null);
    this.autoguardado.encender();
  }

  descartarBorrador(): void {
    this.borradorRecuperable.set(null);
    void this.autoguardado.cerrar();
    this.autoguardado.encender();
  }

  irAlPaso(id: number): void { this.paso.set(id); }

  siguientePaso(): void { this.paso.set(Math.min(this.pasos.length, this.paso() + 1)); }

  pasoAnterior(): void { this.paso.set(Math.max(1, this.paso() - 1)); }

  /**
   * Sube la imagen elegida al banco y la deja vinculada al formulario.
   *
   * EL TEXTO ALTERNATIVO SE PIDE ANTES DE SUBIR, no después: el servidor lo exige y, sobre todo,
   * es cuando quien sube tiene la imagen delante y sabe qué describir. Pedirlo al final es como se
   * llega a «imagen» de texto alternativo.
   */
  async elegirImagen(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    if (!archivo) { return; }

    const ficha = this.formulario();
    if (!ficha) { return; }

    const alt = ficha.imagenAlt.trim();
    if (!alt) {
      this.errorDeImagen.set('Escribe primero el texto alternativo: describe la imagen para quien no puede verla.');
      entrada.value = '';
      return;
    }

    this.subiendoImagen.set(true);
    this.errorDeImagen.set(null);

    const resultado = await this.banco.subirImagen(archivo, alt);

    if (resultado.ok && resultado.archivo) {
      this.campo('imagenArchivoId', resultado.archivo.id);
      this.campo('imagenUrl', resultado.archivo.url);
    } else {
      this.errorDeImagen.set(resultado.error ?? 'No fue posible subir la imagen.');
    }

    // SE LIMPIA LA ENTRADA para que elegir el MISMO fichero otra vez vuelva a disparar el evento.
    entrada.value = '';
    this.subiendoImagen.set(false);
  }

  quitarImagen(): void {
    this.campo('imagenArchivoId', null);
    this.campo('imagenUrl', '');
    this.errorDeImagen.set(null);
  }

  editar(noticia: Noticia): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({
      id: noticia.id,
      version: noticia.version,
      slug: noticia.slug,
      titulo: noticia.titulo,
      resumen: noticia.resumen,
      cuerpo: noticia.cuerpo ?? '',
      fechaPublicacion: noticia.fechaPublicacion ?? '',
      imagenRuta: noticia.imagenRuta ?? '',
      imagenAlternativa: noticia.imagenAlternativa ?? '',
      autoriaNombre: noticia.autoriaNombre ?? '',
      categoriaId: noticia.categoriaId,
      etiquetas: noticia.etiquetas.join(', '),
      practicasMusicalesIds: noticia.practicasMusicales.map(p => p.id),
      territoriosSonorosIds: noticia.territoriosSonoros.map(t => t.id),
      proyectosTransversalesIds: noticia.proyectosTransversales.map(p => p.id),
      imagenArchivoId: noticia.imagenArchivoId,
      imagenUrl: noticia.imagenUrl ?? '',
      imagenAlt: noticia.imagenAlt ?? '',
    });
    this.paso.set(1);
  }

  /**
   * Cierra el formulario sin guardar.
   *
   * EL BORRADOR NO SE TOCA. Cerrar sin querer es justo el accidente del que protege; borrarlo aquí
   * lo dejaría inútil. Se retira al guardar de verdad, o cuando alguien lo descarta a mano.
   */
  cerrarFormulario(): void {
    this.autoguardado.apagar();
    this.borradorRecuperable.set(null);
    this.formulario.set(null);
  }

  campo<K extends keyof FormularioDeNoticia>(clave: K, valor: FormularioDeNoticia[K]): void {
    const actual = this.formulario();
    if (!actual) { return; }
    const siguiente = { ...actual, [clave]: valor };
    this.formulario.set(siguiente);
    if (siguiente.id === null) { this.autoguardado.anotar(siguiente); }
  }

  async guardar(): Promise<void> {
    const ficha = this.formulario();
    if (!ficha) { return; }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    const cuerpo = {
      titulo: ficha.titulo.trim(),
      resumen: ficha.resumen.trim(),
      cuerpo: ficha.cuerpo.trim() || null,
      // UNA CADENA VACIA NO ES UNA FECHA. Enviarla tal cual haría que el servidor la rechazara con
      // un error de formato en vez de aceptar «todavía sin fecha», que es lo que significa.
      fechaPublicacion: ficha.fechaPublicacion.trim() || null,
      imagenRuta: ficha.imagenRuta.trim() || null,
      imagenAlternativa: ficha.imagenAlternativa.trim() || null,
      autoriaNombre: ficha.autoriaNombre.trim() || null,
      categoriaId: ficha.categoriaId,
      etiquetas: ficha.etiquetas.split(',').map(e => e.trim()).filter(Boolean),
      practicasMusicalesIds: ficha.practicasMusicalesIds,
      territoriosSonorosIds: ficha.territoriosSonorosIds,
      proyectosTransversalesIds: ficha.proyectosTransversalesIds,
      imagenArchivoId: ficha.imagenArchivoId,
      // RETIRAR ES UNA DECISION EXPLICITA. Un identificador nulo es indistinguible de «no mandé
      // ese campo», y sin la marca el servidor no sabe si debe quitar la imagen o dejarla.
      retirarImagen: ficha.id !== null && ficha.imagenArchivoId === null,
    };

    const resultado = ficha.id === null
      ? await this.api.crear(cuerpo)
      : await this.api.guardar(ficha.id, { ...cuerpo, version: ficha.version });

    if (resultado.ok) {
      this.aviso.set(ficha.id === null ? 'Noticia creada como borrador.' : 'Noticia guardada.');
      // EL BORRADOR AUTOMATICO SE RETIRA AQUI: la noticia ya está en su tabla.
      if (ficha.id === null) { await this.autoguardado.cerrar(); } else { this.autoguardado.apagar(); }
      this.formulario.set(null);
      await this.cargar();
    } else {
      this.error.set(resultado.error ?? 'No fue posible guardar la noticia.');
    }

    this.guardando.set(false);
  }

  async cambiarEstado(noticia: Noticia, estado: string): Promise<void> {
    this.error.set(null);
    this.aviso.set(null);

    const resultado = await this.api.cambiarEstado(noticia.id, estado);

    if (resultado.ok) {
      this.aviso.set(`«${noticia.titulo}» quedó en ${this.etiquetasDeEstado[estado] ?? estado}.`);
      await this.cargar();
    } else {
      // EL MOTIVO DEL SERVIDOR, ENTERO. Dice todo lo que falta, no lo primero que encontró.
      this.error.set(resultado.error ?? 'No fue posible cambiar el estado.');
    }
  }

  /**
   * Si una noticia publicada todavía no se ve en el portal.
   *
   * <b>PUBLICADA NO ES LO MISMO QUE VISIBLE.</b> Una fechada el lunes está publicada hoy y no
   * aparece hasta el lunes; decirlo en la lista evita el «la publiqué y no sale».
   *
   * <b>LA REGLA LA TRAE EL SERVIDOR y aquí solo se lee.</b> Hasta esta
   * función comparaba fechas por su cuenta, con el reloj del navegador: un equipo con la fecha
   * adelantada daba por aparecida una noticia que el servidor todavía no publicaba. Ahora
   * `estadoEfectivo` lo decide donde se conoce la fecha buena, y de paso es el mismo valor que
   * pinta el indicador de estado, así que el sello y el aviso no pueden decir cosas distintas.
   */
  esperandoSuFecha(noticia: Noticia): boolean {
    return noticia.estadoEfectivo === 'programada';
  }
}
