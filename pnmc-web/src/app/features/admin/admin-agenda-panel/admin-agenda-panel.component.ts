import { FECHA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { nombrePropio } from '../../../shared/texto/nombre-propio';
import { PrevisualizacionDePiezaComponent } from '../../../shared/components/ui/previsualizacion-de-pieza/previsualizacion-de-pieza.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { CategoriasDeContenidoService } from '../../../core/services/categorias-de-contenido.service';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { ConmutadorDeOjoComponent } from '../../../shared/components/ui/conmutador-de-ojo/conmutador-de-ojo.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { etiquetaDeEstado } from '../domain/admin-config';
import { CommonModule, formatDate } from '@angular/common';
import { DialogoDePrevisualizacionComponent } from '../../../shared/components/previsualizacion-en-listado/dialogo-de-previsualizacion.component';
import { FormsModule } from '@angular/forms';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { CatalogService, TerritorioConCodigo } from '../../../core/services/catalog.service';
import { AutoguardadoDeBorrador } from '../../../core/services/autoguardado-de-borrador';
import { BorradoresDeConsolaService } from '../../../core/services/borradores-de-consola.service';
import { IndicadorDePasosComponent, PasoDelIndicador } from '../../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { SelectorDeClasificacionComponent } from '../../../shared/components/ui/selector-de-clasificacion/selector-de-clasificacion.component';
import { SelectorDeProyectosComponent } from '../../../shared/components/ui/selector-de-proyectos/selector-de-proyectos.component';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { SelectorDeCategoriaComponent } from '../../../shared/components/ui/selector-de-categoria/selector-de-categoria.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { BancoDeArchivosService } from '../../../core/services/banco-de-archivos.service';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import {
  ImportarDesdeDocumentoComponent,
  PropuestaDeDocumento,
  ValorPropuesto,
} from '../../../shared/components/importar-desde-documento/importar-desde-documento.component';
import {
  AgendaService,
  ETIQUETAS_ESTADO_EVENTO,
  ETIQUETAS_MODALIDAD,
  ETIQUETAS_SITUACION,
  EventoAgenda,
} from '../../../core/services/agenda.service';

/**
 * La Agenda en la consola.
 *
 * <b>LA MODALIDAD DECIDE QUE CAMPOS HACEN FALTA, y el formulario lo dice antes de intentarlo.</b>
 * Un presencial necesita lugar; un virtual, enlace; un mixto, los dos. El servidor lo exige y la
 * base también, pero enterarse al pulsar «publicar» es peor que verlo mientras se escribe.
 *
 * <b>PUBLICAR PUEDE FALLAR, Y ESTA BIEN QUE FALLE AQUI.</b> El servidor devuelve TODO lo que falta
 * en un solo mensaje y esta pantalla lo enseña tal cual.
 */

interface FormularioDeEvento {
  id: number | null;
  version: number;
  slug: string;
  titulo: string;
  descripcion: string;
  descripcionLarga: string;
  fechaInicio: string;
  fechaFin: string;
  /**
   * Las horas solo existen si alguien las pide.
   *
   * MUCHOS EVENTOS NO TIENEN HORA —una exposición que dura una semana, una convocatoria— y un
   * campo de hora siempre visible invita a rellenarlo «por si acaso» con una hora inventada. La
   * casilla convierte esa decisión en explícita.
   *
   * LA CASILLA DICE «AGREGAR HORA DE INICIO» Y NO «¿TIENE HORA DE INICIO?»: una pregunta invita a
   * contestarla, una acción describe lo que va a pasar. Y `conHoraFin` NO SE OFRECE hasta que
   * `conHoraInicio` está puesta, porque una hora de fin sin hora de inicio no significa nada.
   */
  conHoraInicio: boolean;
  horaInicio: string;
  conHoraFin: boolean;
  horaFin: string;
  modalidad: string;
  lugar: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  url: string;
  imagenRuta: string;
  imagenAlternativa: string;
  categoriaId: number | null;
  organizador: string;
  ordenVisualizacion: string;
  etiquetas: string;
  imagenArchivoId: number | null;
  imagenUrl: string;
  imagenAlt: string;
  /** Clasificación opcional contra los catálogos del sistema. */
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
  /** Iniciativas del Programa con las que se enlaza. No es la categoría: es otra pregunta. */
  proyectosTransversalesIds: number[];
}

/**
 * Los cinco pasos del formulario, en el orden en que se recorren.
 *
 * <b>POR QUE POR PASOS.</b> El formulario de un evento pide veinte campos, y de una sola tirada se
 * lee como un muro en el que es fácil saltarse algo o abandonar a la mitad. Agrupados por pregunta
 * —qué es, cuándo, dónde, con qué, y una revisión— cada pantalla cabe de una ojeada. Es la misma
 * forma que ya tiene el asistente externo de alta de Festival.
 *
 * <b>SE PUEDE SALTAR A CUALQUIER PASO.</b> No hay bloqueo entre pasos porque casi todo es opcional
 * hasta publicar, y bloquear obligaría a inventar un dato para poder llegar al que de verdad se
 * quería corregir.
 */
export const PASOS_DEL_EVENTO: readonly PasoDelIndicador[] = [
  { id: 1, titulo: 'Identidad' },
  { id: 2, titulo: 'Cuándo' },
  { id: 3, titulo: 'Dónde' },
  { id: 4, titulo: 'Contenido' },
  { id: 5, titulo: 'Revisión' },
];

@Component({
  selector: 'app-admin-agenda-panel',
  standalone: true,
  imports: [
    DialogoDirective, BarraDeListaComponent,ConmutadorDeOjoComponent, PrevisualizacionDePiezaComponent, DatoEnLecturaComponent, PanelLateralComponent, SelectorSegmentadoComponent, FiltroDesplegableComponent, CabeceraDeTablaComponent, BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent, BuscadorDeListaComponent, NombrePropioPipe, 
    CommonModule, FormsModule,
    SelectorDeCategoriaComponent, SelectorDeClasificacionComponent, SelectorDeProyectosComponent, IndicadorDePasosComponent,
    SelloDeProcedenciaComponent, HistorialDeRegistroComponent, MenuDeAccionesComponent, IndicadorDeEstadoComponent, DialogoDePrevisualizacionComponent,
    ImportarDesdeDocumentoComponent,
  ],
  templateUrl: './admin-agenda-panel.component.html',
})
export class AdminAgendaPanelComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly api = inject(AgendaService);
  private readonly categorias = inject(CategoriasDeContenidoService);
  private readonly catalogo = inject(CatalogService);
  private readonly banco = inject(BancoDeArchivosService);
  private readonly borradores = inject(BorradoresDeConsolaService);

  /**
   * El borrador que se guarda solo mientras se escribe.
   *
   * SOLO PARA EVENTOS NUEVOS. Editar uno que ya existe tiene su propia red —está guardado, con su
   * versión— y ofrecer «recuperar» sobre una ficha real confundiría dos cosas distintas.
   */
  readonly autoguardado = new AutoguardadoDeBorrador<FormularioDeEvento>(this.borradores.transporte('agenda'));

  readonly pasos = PASOS_DEL_EVENTO;
  readonly paso = signal(1);

  /** Un borrador encontrado al abrir, a la espera de que alguien decida si lo recupera. */
  readonly borradorRecuperable = signal<{ datos: FormularioDeEvento; fecha: string } | null>(null);

  readonly subiendoImagen = signal(false);
  readonly errorDeImagen = signal<string | null>(null);

  readonly enabled = input(true);

  /**
   * El catálogo territorial, para elegir en vez de teclear.
   *
   * ANTES SE ESCRIBIA EL CODIGO A MANO —«15», «15516»— y un dígito de más producía un evento con
   * territorio inválido que la foránea rechazaba con un error que no decía cuál era el problema.
   */
  readonly territorios = signal<TerritorioConCodigo[]>([]);

  readonly municipiosDelDepartamento = computed(() => {
    const codigo = this.formulario()?.codigoDepartamento ?? '';
    if (!codigo) { return []; }
    return this.territorios().find(t => t.codigo === codigo)?.municipios ?? [];
  });

  readonly eventos = signal<EventoAgenda[]>([]);
  readonly total = signal(0);

  /**
   * En qué página está, cuántas hay y cuántos caben.
   *
   * <b>LA AGENDA NO PAGINABA, Y ESE ERA EL DEFECTO.</b> El servidor devuelve 12 eventos por página
   * y ordena «sin publicar primero»; con 471 eventos, de los cuales 436 están publicados, la
   * pantalla enseñaba la primera página —35 borradores y archivados— y los publicados vivían de la
   * página cuatro en adelante, sin ningún control para llegar. se detectó
   * como «en agenda no aparecen los eventos publicados»: aparecían, pero no había cómo verlos.
   * Comprobado: 471 en total, 40 páginas, 12 filas en pantalla.
   */
  /**
   * Si la lista trae también lo archivado.
   *
   * ARRANCA EN NO: un archivo cerrado no es trabajo pendiente, y con 471 eventos ocupa sitio sin
   * decir nada. Se pide al servidor, no se filtra aquí: filtrar la página que se tiene delante
   * daría listas cortas y engañosas.
   */
  readonly mostrarArchivados = signal(false);

  /**
   * De quién son los eventos que se listan: del Programa, de organizaciones, o todos.
   *
   * SON LAS DOS PROCEDENCIAS QUE SE PIDIERON —«la institucional o administrativa y la de externos
   * que también pueden agregar eventos»— y es la misma separación que ya usa el Banco de archivos.
   * La aplica el servidor: con cuarenta páginas, separar la que se tiene delante diría «no hay
   * ninguno externo» cuando lo que pasa es que están en otra.
   */
  readonly filtroProcedencia = signal<'todas' | 'institucional' | 'externo'>('todas');

  /** Los tres, con los nombres que pidió la dirección de producto: todos, institucionales, externos. */
  readonly PROCEDENCIAS: readonly { id: string; etiqueta: string }[] = [
    { id: 'todas', etiqueta: 'Todos' },
    { id: 'institucional', etiqueta: 'Institucionales' },
    { id: 'externo', etiqueta: 'Externos' },
  ];

  cambiarProcedencia(valor: string): void {
    if (valor === 'todas' || valor === 'institucional' || valor === 'externo') {
      this.filtroProcedencia.set(valor);
    }
  }

  readonly pagina = signal(1);
  readonly totalDePaginas = signal(1);
  readonly porPagina = signal(12);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly filtroEstado = signal<string>('todos');
  readonly filtroCategoria = signal<string>('todas');
  readonly busqueda = signal('');

  /**
   * Las categorías como posiciones de un selector, «todas» delante.
   *
   * <b>SIN RECUENTO POR CATEGORIA, y es a propósito.</b> El número que trae el vocabulario es
   * cuántos contenidos usan esa categoría EN TOTAL, y esta lista se acota además por procedencia,
   * por estado, por archivados y por búsqueda: con «Externos» puesto, la lista tiene una fila y la
   * pestaña decía «Encuentros 155». Un recuento que no describe lo que hay detrás del clic es peor
   * que ninguno. «Todas» sí lo lleva, porque ese sí es el total de lo que se está listando.
   *
   * Tenerlos de verdad exigiría que el servidor agregara por categoría para cada combinación de
   * filtros; el día que lo haga, vuelven.
   */
  readonly pestanasDeCategoria = computed<readonly OpcionSegmentada[]>(() => [
    { id: 'todas', etiqueta: 'Todas', conteo: this.total() },
    ...this.categoriasEnUso().map(c => ({ id: c.nombre, etiqueta: c.nombre })),
  ]);

  /**
   * Si hay algún filtro puesto que pueda estar escondiendo eventos que sí existen.
   *
   * El ojo de archivados cuenta: oculta eventos reales. La pestaña de categoría no, porque es la
   * lista que se eligió mirar y la nombra `ambitoDeLaLista`.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.busqueda().trim().length > 0
      || this.filtroEstado() !== 'todos'
      || this.filtroProcedencia() !== 'todas'
      || this.mostrarArchivados(),
  );

  readonly ambitoDeLaLista = computed(() => {
    const categoria = this.filtroCategoria();
    return categoria === 'todas' ? null : `en «${categoria}»`;
  });

  /**
   * Quita la búsqueda, el estado, la procedencia y los archivados; deja la pestaña donde está.
   *
   * <b>EL EFECTO NO DEPENDE DE LA BUSQUEDA</b>, así que limpiar solo el texto no volvía a pedir la
   * lista. Cuando sí cambia alguno de los que el efecto vigila, recarga él y aquí no se pide.
   */
  limpiarFiltros(): void {
    const recargaraElEfecto = this.filtroEstado() !== 'todos'
      || this.filtroProcedencia() !== 'todas'
      || this.mostrarArchivados();
    this.busqueda.set('');
    this.filtroEstado.set('todos');
    this.filtroProcedencia.set('todas');
    this.mostrarArchivados.set(false);
    if (!recargaraElEfecto) {
      this.pagina.set(1);
      void this.cargar();
    }
  }

  /** Los estados del módulo. Fijos: los declara el vocabulario, no la respuesta. */
  readonly FILTRO_DE_ESTADO: readonly OpcionDeFiltro[] = [
    { id: 'todos', etiqueta: 'Todos los estados' },
    { id: 'borrador', etiqueta: 'Borradores' },
    { id: 'en_revision', etiqueta: 'En revisión' },
    { id: 'publicado', etiqueta: 'Publicados' },
    { id: 'archivado', etiqueta: 'Archivados' },
  ];

  /** Las columnas de la tabla. «Acciones» no ordena por nada y por eso lo declara. */
  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'actividad', etiqueta: 'Actividad' },
    { id: 'cuando', etiqueta: 'Cuándo' },
    { id: 'donde', etiqueta: 'Dónde' },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  /**
   * El orden de la tabla, en la pieza compartida.
   *
   * <b>ARRANCA SIN COLUMNA, Y ESO ES LO CORRECTO AQUI.</b> El orden de partida de la consola es
   * «sin publicar primero y dentro de eso lo más próximo», que no es el de ninguna columna: son
   * dos datos combinados. Marcar «Cuándo» con su flecha diría que la tabla está ordenada por
   * fecha, y no lo está —los publicados van después aunque caigan antes—. Sin columna, las cuatro
   * cabeceras enseñan «↕», que es la invitación a ordenar por una.
   */
  readonly orden = new OrdenDeTabla('', 'asc', 'Sin publicar primero');

  /**
   * Pulsar una cabecera: se lo pide al servidor y se vuelve a la primera página.
   *
   * <b>ORDENAR EN MEMORIA ERA EL DEFECTO, no una simplificación.</b> La Agenda pinta doce filas de
   * cuarenta páginas; ordenar el array cargado reordenaba esas doce y dejaba el resto donde
   * estaba. Se detectó: «debe ser de todos, no
   * solo de los de la página visible, por eso en la página 1 no me aparecía nunca ningún
   * publicado». Es el mismo criterio que ya seguían Festivales y Organizaciones.
   *
   * VUELVE A LA PRIMERA PAGINA porque la página treinta de un orden no contiene lo mismo que la
   * página treinta del otro: quedarse en ella deja a quien ordena en un sitio arbitrario.
   */
  ordenarPor(columna: string): void {
    this.orden.alternar(columna);
    this.pagina.set(1);
    void this.cargar();
  }

  /**
   * Las categorías del vocabulario, no las de la página que se está viendo.
   *
   * <b>SE LEEN DEL CATALOGO Y NO DE LOS EVENTOS CARGADOS.</b> Cuando el filtro era de cliente,
   * contar sobre la página tenía sentido; con el filtro en el servidor y cuarenta páginas, contar
   * sobre doce eventos ofrecería dos categorías de veinte y escondería las demás. Solo se listan
   * las que ALGUIEN usa —`contenidosQueLaUsan`—, para no llevar a listas vacías.
   */
  readonly categoriasEnUso = signal<{ nombre: string; total: number }[]>([]);

  private async cargarCategorias(): Promise<void> {
    // LAS PROPIAS Y LAS COMUNES: el vocabulario reparte las categorías por módulo y deja un grupo
    // compartido para lo que cruza los tres. Un evento puede llevar cualquiera de los dos.
    const [propias, comunes] = await Promise.all([
      this.categorias.listar('agenda'),
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
   * <b>AQUI NO SE FILTRA NI SE ORDENA NADA.</b> La búsqueda, el estado, la categoría, la
   * procedencia, los archivados y el orden los resuelve el servidor sobre los 471 eventos. Todo lo
   * que se hiciera aquí operaría sobre doce filas y mentiría sobre las otras treinta y nueve
   * páginas, que es exactamente el defecto que tenía el orden hasta.
   */
  readonly eventosVisibles = computed(() => this.eventos());

  readonly formulario = signal<FormularioDeEvento | null>(null);
  readonly guardando = signal(false);

  readonly etiquetasDeEstado = ETIQUETAS_ESTADO_EVENTO;

  /** La tabla con la que la Agenda escribe su bitácora. Sin ella el historial cruzaría módulos. */
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.agenda;

  /** El evento cuyo historial se está mirando, si alguno. */
  readonly historialAbierto = signal<EventoAgenda | null>(null);
  readonly etiquetasDeSituacion = ETIQUETAS_SITUACION;
  readonly etiquetasDeModalidad = ETIQUETAS_MODALIDAD;

  /*
    EL CODIGO NUNCA SALE A PANTALLA. El respaldo de estas dos lecturas era `|| codigo`, asi que un
    estado o una modalidad que el servidor anadiera y el diccionario no conociera se leia
    «en_revision» en la tabla. El respaldo compartido lo escribe en lenguaje humano.
  */
  rotuloDeEstado(codigo: string | null | undefined): string {
    return this.etiquetasDeEstado[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }

  rotuloDeModalidad(codigo: string | null | undefined): string {
    return this.etiquetasDeModalidad[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }


  /**
   * Qué tiene mal el formulario ahora mismo, si algo.
   *
   * <b>LAS FECHAS SE COMPRUEBAN MIENTRAS SE ESCRIBEN, no al guardar.</b> El servidor las valida
   * igual —y la base también, con sus CHECK—, pero enterarse al pulsar el botón obliga a volver
   * arriba a buscar cuál de las dos estaba mal. Aquí se dice al lado del campo.
   *
   * LA HORA SOLO SE COMPARA SI EL EVENTO EMPIEZA Y ACABA EL MISMO DIA: en uno de varios días,
   * acabar a las nueve de la mañana del último es lo normal.
   */
  readonly problemasDeFecha = computed(() => {
    const f = this.formulario();
    if (!f) { return []; }

    const problemas: string[] = [];
    const inicio = f.fechaInicio.trim();
    const fin = f.fechaFin.trim();

    if (!inicio) { problemas.push('Falta la fecha de inicio.'); }
    if (inicio && fin && fin < inicio) {
      problemas.push('El evento no puede terminar antes de empezar.');
    }

    const mismoDia = !fin || fin === inicio;
    if (mismoDia && f.conHoraInicio && f.conHoraFin && f.horaInicio && f.horaFin && f.horaFin < f.horaInicio) {
      problemas.push('La hora de fin no puede ser anterior a la de inicio.');
    }

    if (f.conHoraInicio && !f.horaInicio.trim()) { problemas.push('Agregaste hora de inicio: escríbela o quita la casilla.'); }
    if (f.conHoraFin && !f.horaFin.trim()) { problemas.push('Agregaste hora de fin: escríbela o quita la casilla.'); }

    // NO PUEDE HABER FIN SIN INICIO. La pantalla ya no deja construirlo —el control de la hora de
    // fin no existe hasta que hay hora de inicio—, pero el borrador recuperado de una sesión
    // anterior sí podría traer esa combinación, y entonces hay que decirlo en vez de guardarla.
    if (f.conHoraFin && !f.conHoraInicio) { problemas.push('No puede haber hora de fin sin hora de inicio.'); }

    return problemas;
  });

  /**
   * Qué le falta al formulario para poder publicarse.
   *
   * ES LA MISMA REGLA QUE APLICA EL SERVIDOR, adelantada a la pantalla. No la sustituye —el
   * servidor sigue siendo quien decide, y la base lo respalda con una CHECK—; solo evita que quien
   * organiza se entere al pulsar el botón de algo que se sabía mientras escribía.
   */
  readonly loQueFalta = computed(() => {
    const f = this.formulario();
    if (!f) { return []; }
    const faltas: string[] = [];
    if (!f.titulo.trim()) { faltas.push('el título'); }
    if (!f.descripcion.trim()) { faltas.push('la descripción'); }
    if ((f.modalidad === 'presencial' || f.modalidad === 'mixta') && !f.lugar.trim()) { faltas.push('el lugar'); }
    if ((f.modalidad === 'virtual' || f.modalidad === 'mixta') && !f.url.trim()) { faltas.push('el enlace'); }
    return faltas;
  });

  constructor() {
    this.catalogo.fetchDivipolaConCodigos().subscribe({
      next: territorios => this.territorios.set(territorios),
      error: () => this.territorios.set([]),
    });

    effect(() => {
      const activo = this.enabled();
      // Se leen para que el efecto dependa de las tres: cambiar cualquiera vuelve a pedir la lista.
      this.filtroEstado();
      this.filtroCategoria();
      this.mostrarArchivados();
      this.filtroProcedencia();
      if (!activo) { return; }
      untracked(() => { this.pagina.set(1); void this.cargar(); void this.cargarCategorias(); });
    });
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);

    const resultado = await this.api.listarInternos({
      q: this.busqueda().trim() || undefined,
      estado: this.filtroEstado() === 'todos' ? undefined : this.filtroEstado(),
      // AL SERVIDOR Y NO EN MEMORIA: con cuarenta páginas, filtrar la que se tiene delante
      // respondería «no hay ninguno de esa categoría» cuando lo que pasa es que están en otra.
      categoria: this.filtroCategoria() === 'todas' ? undefined : this.filtroCategoria(),
      procedencia: this.filtroProcedencia() === 'todas' ? undefined : this.filtroProcedencia(),
      incluirArchivados: this.mostrarArchivados(),
      // SIN COLUMNA ELEGIDA NO SE MANDA ORDEN: el servidor responde con el orden de trabajo —sin
      // publicar primero—, que es con el que la consola tiene que abrir.
      orden: this.orden.columna() || undefined,
      direccion: this.orden.columna() ? this.orden.direccion() : undefined,
      pagina: this.pagina(),
    });

    if (resultado.ok && resultado.data) {
      this.eventos.set(resultado.data.items);
      this.total.set(resultado.data.total);
      this.totalDePaginas.set(Math.max(1, resultado.data.totalPaginas || 1));
      this.porPagina.set(resultado.data.tamano || resultado.data.items.length);
    } else {
      this.eventos.set([]);
      this.total.set(0);
      this.totalDePaginas.set(1);
      this.error.set(resultado.error ?? 'No fue posible consultar la agenda.');
    }

    this.cargando.set(false);
  }

  /**
   * Buscar devuelve a la primera página: la cuarta ya no contiene lo mismo.
   *
   * LLEGA CON EL TEXTO YA ESPERADO. El buscador compartido avisa cuando la escritura se detiene, no
   * en cada tecla, así que aquí no hay nada que amortiguar: cada llamada es una búsqueda.
   */
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
    // ARRIBA DEL TODO AL CAMBIAR DE PAGINA: la fila que se estaba mirando ya no existe, y quedarse
    // a media tabla deja la sensación de que no ha pasado nada.
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /**
   * El evento que se está LEYENDO, que no es el que se está editando.
   *
   * Son dos cosas distintas y por eso son dos señales: `formulario()` abre una pantalla para
   * escribir, con su borrador y su validación; esto abre un cajón para mirar, del que se sale sin
   * guardar nada.
   */
  readonly fichaEnLectura = signal<EventoAgenda | null>(null);

  /** «18:00:00» → «18:00». El servidor manda la hora con segundos y nadie programa al segundo. */
  soloLaHora(hora: string | null | undefined): string {
    const valor = (hora ?? '').trim();
    return valor.length >= 5 ? valor.slice(0, 5) : valor;
  }

  /**
   * Cuándo y dónde, en la línea que va bajo el título de un evento.
   *
   * ES LA FIRMA DE UN EVENTO: lo que en una noticia es la autoría y la fecha, aquí es la fecha y
   * el sitio, porque es lo que decide si alguien puede ir.
   */
  cuandoYDonde(evento: EventoAgenda): string {
    const piezas: string[] = [];
    if (evento.fechaInicio) {
      const inicio = formatDate(evento.fechaInicio, FECHA_ADMINISTRATIVA, 'es-CO');
      const fin = evento.fechaFin && evento.fechaFin !== evento.fechaInicio
        ? formatDate(evento.fechaFin, FECHA_ADMINISTRATIVA, 'es-CO')
        : null;
      piezas.push(fin ? `Del ${inicio} al ${fin}` : inicio);
    }
    if (evento.horaInicio) {
      const hora = this.soloLaHora(evento.horaInicio);
      piezas.push(evento.horaFin ? `${hora} a ${this.soloLaHora(evento.horaFin)}` : hora);
    }
    const sitio = evento.lugar || nombrePropio(evento.nombreMunicipio || evento.nombreDepartamento || '');
    if (sitio) { piezas.push(sitio); }
    return piezas.join(' · ');
  }

  /** Lo que acompaña al evento sin ser su texto. Lo que no hay, no se pinta. */
  datosDelEvento(evento: EventoAgenda): { rotulo: string; valor: string }[] {
    const datos: { rotulo: string; valor: string }[] = [];
    datos.push({ rotulo: 'Modalidad', valor: this.rotuloDeModalidad(evento.modalidad) });
    const territorio = nombrePropio(evento.nombreMunicipio || evento.nombreDepartamento || '');
    if (territorio) { datos.push({ rotulo: 'Territorio', valor: territorio }); }
    if (evento.organizador) { datos.push({ rotulo: 'Organiza', valor: evento.organizador }); }
    if (evento.url) { datos.push({ rotulo: 'Más información', valor: evento.url }); }
    return datos;
  }

  consultar(evento: EventoAgenda): void { this.fichaEnLectura.set(evento); }
  cerrarLaFicha(): void { this.fichaEnLectura.set(null); }

  /** Desde la lectura se puede pasar a editar sin volver a buscar la fila. */
  editarDesdeLaFicha(evento: EventoAgenda): void {
    this.fichaEnLectura.set(null);
    this.editar(evento);
  }

  abrirHistorial(evento: EventoAgenda): void { this.historialAbierto.set(evento); }

  /**
   * Las acciones que este evento admite hoy, en un solo sitio.
   *
   * SE DERIVAN DEL ESTADO Y NO SE PINTAN TODAS. Una acción que no se puede hacer no se ofrece
   * desactivada: no está. Es el mismo ciclo de Noticias y Catálogo editorial —editar · enviar a
   * revisión · publicar · despublicar · archivar · ver · historial— con las mismas palabras y en el
   * mismo orden, porque tres módulos del mismo ciclo no pueden pedir tres gestos distintos.
   */
  accionesDe(evento: EventoAgenda): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [];
    // VER VA PRIMERO Y SIEMPRE. Es lo único que se puede hacer con cualquier evento, esté en el
    // estado que esté, y hasta hoy no se podía: para leer uno había que abrir el FORMULARIO de
    // edición, que es una pantalla para escribir y que además bloquea la lista detrás. Lo pidió el
    // dirección de producto: «deberían permitir visualizar dentro del
    // panel de gestión».
    acciones.push({ id: 'consultar', etiqueta: 'Abrir ficha' });
    acciones.push(
      evento.estado === 'publicado'
        ? { id: 'editar', etiqueta: 'Editar' }
        : { id: 'publicar', etiqueta: 'Publicar', tono: 'principal' });
    if (evento.estado !== 'publicado') { acciones.push({ id: 'editar', etiqueta: 'Editar' }); }
    if (evento.estado === 'borrador') { acciones.push({ id: 'revisar', etiqueta: 'Enviar a revisión' }); }
    // PREVISUALIZAR SOLO CUANDO TODAVIA NO SE VE. Si ya está en la agenda pública, lo que sirve es
    // abrirla; ofrecer las dos obliga a decidir entre dos acciones que hacen casi lo mismo.
    if (evento.estado !== 'publicado') { acciones.push({ id: 'previsualizar', etiqueta: 'Previsualizar' }); }
    if (evento.estado === 'publicado') {
      // DESPUBLICAR NO ES ARCHIVAR. Retirar del portal algo que hay que seguir editando devuelve a
      // borrador; archivar lo cierra. Sin los dos, quien quiere corregir una ficha publicada acaba
      // archivándola, que es otra cosa.
      acciones.push({ id: 'despublicar', etiqueta: 'Despublicar' });
      acciones.push({ id: 'ver', etiqueta: 'Ver en el portal', enlace: this.rutaPublica(), nuevaPestana: true });
    }
    acciones.push({ id: 'historial', etiqueta: 'Historial' });
    if (evento.estado === 'publicado') { acciones.push({ id: 'archivar', etiqueta: 'Archivar', tono: 'peligro' }); }
    return acciones;
  }

  ejecutarAccion(evento: EventoAgenda, accion: string): void {
    switch (accion) {
      case 'consultar': this.consultar(evento); break;
      case 'editar': this.editar(evento); break;
      case 'revisar': this.cambiarEstado(evento, 'en_revision'); break;
      case 'publicar': this.cambiarEstado(evento, 'publicado'); break;
      case 'despublicar': this.cambiarEstado(evento, 'borrador'); break;
      case 'archivar': this.cambiarEstado(evento, 'archivado'); break;
      case 'previsualizar': this.previsualizando.set(evento); break;
      case 'historial': this.abrirHistorial(evento); break;
    }
  }

  cerrarHistorial(): void { this.historialAbierto.set(null); }

  /** El registro que se está mirando como se verá en su listado, o `null`. */
  readonly previsualizando = signal<EventoAgenda | null>(null);

  cerrarPrevisualizacion(): void { this.previsualizando.set(null); }

  /** Cómo se llama lo que se está previsualizando. Se escribe fuera del marco. */
  nombreParaPrevisualizar(registro: EventoAgenda): string { return registro.titulo; }

  /**
   * En qué estado está hoy, en palabras.
   *
   * SE LEE EL ESTADO EFECTIVO DONDE LO HAY: una noticia guardada como publicada con fecha futura
   * está «Programada», y decirle «Publicado» al lado de una previsualización que avisa de que
   * todavía no es pública sería contradecirse en la misma caja.
   */
  estadoParaPrevisualizar(registro: EventoAgenda): string { return this.etiquetasDeEstado[registro.estado] ?? registro.estado; }

  /**
   * La dirección pública del evento.
   *
   * LLEVA A LA AGENDA Y NO A UNA FICHA PROPIA porque el diseño aprobado del portal no tiene página
   * de detalle para un evento: lo abre dentro de la misma lista. Enlazar a una ruta que no existe
   * sería peor que llevar a la lista donde el evento aparece.
   */
  rutaPublica(): string { return '/agenda'; }

  // ═════════════════════ Alta a partir de un afiche ═════════════════════
  //
  // ES LA MISMA HERRAMIENTA QUE LA DEL CATALOGO EDITORIAL. Lo que cambia es qué se reconoce dentro
  // del documento: allí, identificadores y autoría; aquí, cuándo y dónde. El componente que sube el
  // fichero y enseña lo leído es el mismo, y el servidor decide qué buscar según la ruta.

  readonly leyendoDocumento = signal(false);

  abrirLectura(): void {
    this.leyendoDocumento.set(true);
    this.error.set(null);
  }

  cerrarLectura(): void { this.leyendoDocumento.set(false); }

  /**
   * Abre el formulario del evento con lo que el afiche propuso.
   *
   * <b>EL LUGAR LLEGA RESUELTO CONTRA DIVIPOLA</b>, no como texto: el servidor ya devolvió el código
   * de municipio y el de departamento, que es lo que el evento necesita para salir en el mapa.
   *
   * <b>Y NACE EN BORRADOR.</b> No se crea ningún evento: se abre el formulario para que una persona
   * lo revise, lo complete y lo apruebe. Es la regla del proyecto sobre importación asistida.
   */
  usarLaPropuesta(propuesta: PropuestaDeDocumento): void {
    const titulos = (propuesta['titulos'] as ValorPropuesto[] | undefined) ?? [];
    const fechas = (propuesta['fechas'] as { inicio: string; fin: string | null }[] | undefined) ?? [];
    const lugares = (propuesta['lugares'] as { municipio: string; codigoMunicipio: string; codigoDepartamento: string }[] | undefined) ?? [];
    const organizaciones = (propuesta['organizaciones'] as ValorPropuesto[] | undefined) ?? [];

    this.nuevo();
    const actual = this.formulario();
    if (!actual) { return; }

    this.formulario.set({
      ...actual,
      titulo: titulos.length > 0 ? titulos[0].valor : '',
      // LA PRIMERA FECHA ES LA MAS TEMPRANA, que es la del evento en un afiche: las posteriores
      // suelen ser cierres de inscripción o fechas de otras funciones.
      fechaInicio: fechas.length > 0 ? fechas[0].inicio : actual.fechaInicio,
      fechaFin: fechas.length > 0 ? (fechas[0].fin ?? '') : '',
      lugar: lugares.length > 0 ? lugares[0].municipio : '',
      codigoMunicipio: lugares.length > 0 ? lugares[0].codigoMunicipio : '',
      codigoDepartamento: lugares.length > 0 ? lugares[0].codigoDepartamento : '',
      organizador: organizaciones.length > 0 ? organizaciones[0].valor : '',
      imagenUrl: (propuesta['aficheUrl'] as string | null) ?? '',
      imagenArchivoId: (propuesta['aficheArchivoId'] as number | null) ?? null,
    });

    this.leyendoDocumento.set(false);
    this.paso.set(1);
    this.aviso.set('Formulario abierto con lo que se leyó del afiche. Revisa cada campo antes de guardar: no se ha creado ningún evento.');
  }

  nuevo(): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({
      id: null, version: 0, slug: '', titulo: '', descripcion: '', descripcionLarga: '',
      fechaInicio: new Date().toISOString().slice(0, 10), fechaFin: '',
      conHoraInicio: false, horaInicio: '', conHoraFin: false, horaFin: '',
      modalidad: 'presencial', lugar: '', codigoDepartamento: '', codigoMunicipio: '',
      url: '', imagenRuta: '', imagenAlternativa: '', categoriaId: null, organizador: '',
      ordenVisualizacion: '', etiquetas: '',
      imagenArchivoId: null, imagenUrl: '', imagenAlt: '',
      practicasMusicalesIds: [], territoriosSonorosIds: [], proyectosTransversalesIds: [],
    });
    this.paso.set(1);

    // SE PREGUNTA ANTES DE ENCENDER EL AUTOGUARDADO: si hay un borrador de la vez anterior, hay
    // que ofrecerlo intacto. Encender primero lo pisaría con el formulario vacío que acaba de
    // abrirse, que es exactamente lo que este mecanismo existe para evitar.
    void this.buscarBorrador();
  }

  /**
   * Busca un borrador sin terminar y lo deja ofrecido.
   */
  private async buscarBorrador(): Promise<void> {
    const encontrado = await this.autoguardado.recuperar();
    if (encontrado && this.formulario()?.id === null) {
      this.borradorRecuperable.set(encontrado);
    } else {
      this.autoguardado.encender();
    }
  }

  /** Devuelve el formulario a como estaba cuando se perdió. */
  recuperarBorrador(): void {
    const encontrado = this.borradorRecuperable();
    if (!encontrado) { return; }
    this.formulario.set({ ...encontrado.datos, id: null });
    this.borradorRecuperable.set(null);
    this.autoguardado.encender();
  }

  /** Descarta el borrador ofrecido y sigue con el formulario vacío. */
  descartarBorrador(): void {
    this.borradorRecuperable.set(null);
    void this.autoguardado.cerrar();
    this.autoguardado.encender();
  }

  irAlPaso(id: number): void { this.paso.set(id); }

  siguientePaso(): void { this.paso.set(Math.min(this.pasos.length, this.paso() + 1)); }

  pasoAnterior(): void { this.paso.set(Math.max(1, this.paso() - 1)); }

  editar(evento: EventoAgenda): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({
      id: evento.id,
      version: evento.version,
      slug: evento.slug,
      titulo: evento.titulo,
      descripcion: evento.descripcion,
      descripcionLarga: evento.descripcionLarga ?? '',
      fechaInicio: evento.fechaInicio,
      fechaFin: evento.fechaFin ?? '',
      // Al editar, la casilla nace marcada si el evento YA tenía hora: es lo que dice el dato.
      conHoraInicio: evento.horaInicio !== null,
      horaInicio: evento.horaInicio ?? '',
      conHoraFin: evento.horaFin !== null,
      horaFin: evento.horaFin ?? '',
      modalidad: evento.modalidad,
      lugar: evento.lugar ?? '',
      codigoDepartamento: evento.codigoDepartamento ?? '',
      codigoMunicipio: evento.codigoMunicipio ?? '',
      url: evento.url ?? '',
      imagenRuta: evento.imagenRuta ?? '',
      imagenAlternativa: evento.imagenAlternativa ?? '',
      categoriaId: evento.categoriaId,
      organizador: evento.organizador ?? '',
      ordenVisualizacion: evento.ordenVisualizacion === null ? '' : String(evento.ordenVisualizacion),
      imagenArchivoId: evento.imagenArchivoId,
      imagenUrl: evento.imagenUrl ?? '',
      imagenAlt: evento.imagenAlt ?? '',
      etiquetas: evento.etiquetas.join(', '),
      practicasMusicalesIds: evento.practicasMusicales.map(p => p.id),
      territoriosSonorosIds: evento.territoriosSonoros.map(t => t.id),
      proyectosTransversalesIds: evento.proyectosTransversales.map(p => p.id),
    });
    this.paso.set(1);
  }

  /**
   * Cierra el formulario sin guardar.
   *
   * EL BORRADOR NO SE TOCA. Cerrar sin querer es justo el accidente del que este mecanismo
   * protege; borrarlo aquí lo convertiría en inútil. Se retira al guardar de verdad, o cuando
   * alguien lo descarta a mano.
   */
  /**
   * Marca o desmarca la hora de inicio, y arrastra la de fin.
   *
   * <b>SIN HORA DE INICIO NO HAY HORA DE FIN.</b> Desmarcar la primera dejando puesta la segunda
   * produciría un evento que acaba a una hora y no empieza a ninguna, que es un dato que nadie
   * puede leer y que el formulario no debería poder construir. Se retira aquí, en el momento en
   * que deja de tener sentido, y no al guardar.
   */
  cambiarHoraDeInicio(activada: boolean): void {
    const actual = this.formulario();
    if (!actual) { return; }
    const siguiente = activada
      ? { ...actual, conHoraInicio: true }
      : { ...actual, conHoraInicio: false, horaInicio: '', conHoraFin: false, horaFin: '' };
    this.formulario.set(siguiente);
    if (siguiente.id === null) { this.autoguardado.anotar(siguiente); }
  }

  cerrarFormulario(): void {
    this.autoguardado.apagar();
    this.borradorRecuperable.set(null);
    this.formulario.set(null);
  }

  campo<K extends keyof FormularioDeEvento>(clave: K, valor: FormularioDeEvento[K]): void {
    const actual = this.formulario();
    if (!actual) { return; }
    const siguiente = { ...actual, [clave]: valor };
    this.formulario.set(siguiente);
    // SOLO SE AUTOGUARDAN LOS NUEVOS: una ficha que ya existe tiene su propia red.
    if (siguiente.id === null) { this.autoguardado.anotar(siguiente); }
  }

  /**
   * Cambia el departamento y limpia el municipio.
   *
   * DEJARLO PUESTO GUARDARIA UN PAR QUE NO EXISTE —un municipio de otro departamento— y la
   * foránea a `Divipola` lo rechazaría con un error que no explica cuál de los dos campos falla.
   */
  cambiarDepartamento(codigo: string): void {
    const actual = this.formulario();
    if (!actual) { return; }
    this.formulario.set({ ...actual, codigoDepartamento: codigo, codigoMunicipio: '' });
  }

  /**
   * Sube la imagen elegida al banco y la deja vinculada al formulario.
   *
   * <b>EL TEXTO ALTERNATIVO SE PIDE ANTES DE SUBIR</b>, no después: el servidor lo exige y, sobre
   * todo, es cuando quien sube tiene la imagen delante y sabe qué describir. Pedirlo al final es
   * como se llega a «imagen» de texto alternativo.
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

    // SE LIMPIA LA ENTRADA para que elegir el MISMO fichero otra vez vuelva a disparar el evento:
    // el navegador no lo emite si el valor no cambia, y el segundo intento no haría nada.
    entrada.value = '';
    this.subiendoImagen.set(false);
  }

  /** Quita la imagen del formulario. Guardar después es lo que la retira de verdad. */
  quitarImagen(): void {
    this.campo('imagenArchivoId', null);
    this.campo('imagenUrl', '');
    this.errorDeImagen.set(null);
  }

  async guardar(): Promise<void> {
    const f = this.formulario();
    if (!f) { return; }

    // NO SE LLAMA AL SERVIDOR CON UNA FECHA IMPOSIBLE. Él la rechazaría igual, pero el viaje solo
    // sirve para que el error llegue más tarde y más lejos del campo que lo causa.
    const problemas = this.problemasDeFecha();
    if (problemas.length > 0) {
      this.error.set(problemas.join(' '));
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    const cuerpo = {
      titulo: f.titulo.trim(),
      descripcion: f.descripcion.trim(),
      fechaInicio: f.fechaInicio,
      // UNA CADENA VACIA NO ES UNA FECHA NI UNA HORA. Enviarla tal cual haría que el servidor la
      // rechazara por formato en vez de aceptar «no se ha puesto», que es lo que significa.
      fechaFin: f.fechaFin.trim() || null,
      // SI LA CASILLA ESTA SIN MARCAR, NO HAY HORA, aunque haya quedado un valor escrito de antes:
      // desmarcar es la forma de decir «este evento no tiene hora», y respetarla es lo que evita
      // que una hora tecleada y luego descartada acabe publicada.
      horaInicio: f.conHoraInicio ? (f.horaInicio.trim() || null) : null,
      horaFin: f.conHoraFin ? (f.horaFin.trim() || null) : null,
      descripcionLarga: f.descripcionLarga.trim() || null,
      organizador: f.organizador.trim() || null,
      // `String(...)` Y NO `.trim()` A SECAS: un `<input type="number">` con `ngModel` entrega un
      // NUMERO, no una cadena, y llamar a `.trim()` sobre él lanzaba. El formulario se quedaba en
      // «Guardando…» para siempre, sin petición y sin aviso, que es peor que un error.
      ordenVisualizacion: String(f.ordenVisualizacion ?? '').trim() ? Number(f.ordenVisualizacion) : null,
      imagenArchivoId: f.imagenArchivoId,
      // RETIRAR ES UNA DECISION EXPLICITA. Un identificador nulo es indistinguible de «no mandé
      // ese campo», y sin la marca el servidor no sabe si debe quitar la imagen o dejarla.
      retirarImagen: f.id !== null && f.imagenArchivoId === null,
      modalidad: f.modalidad,
      lugar: f.lugar.trim() || null,
      codigoDepartamento: f.codigoDepartamento.trim() || null,
      codigoMunicipio: f.codigoMunicipio.trim() || null,
      url: f.url.trim() || null,
      imagenRuta: f.imagenRuta.trim() || null,
      imagenAlternativa: f.imagenAlternativa.trim() || null,
      categoriaId: f.categoriaId,
      etiquetas: f.etiquetas.split(',').map(e => e.trim()).filter(Boolean),
      practicasMusicalesIds: f.practicasMusicalesIds,
      territoriosSonorosIds: f.territoriosSonorosIds,
      proyectosTransversalesIds: f.proyectosTransversalesIds,
    };

    // EL `finally` NO ES CEREMONIA. Sin él, cualquier fallo antes de la respuesta deja el botón en
    // «Guardando…» de forma indefinida: quien administra no sabe si esperar o volver a pulsar, y
    // no hay nada en pantalla que explique qué pasó. Lo trajo un defecto real de esta pantalla.
    try {
      const resultado = f.id === null
        ? await this.api.crear(cuerpo)
        : await this.api.guardar(f.id, { ...cuerpo, version: f.version });

      if (resultado.ok) {
        this.aviso.set(f.id === null ? 'Evento creado como borrador.' : 'Evento guardado.');
        // EL BORRADOR AUTOMATICO SE RETIRA AQUI: el evento ya está en su tabla, y conservarlo
        // ofrecería recuperar algo que ya existe.
        if (f.id === null) { await this.autoguardado.cerrar(); } else { this.autoguardado.apagar(); }
        this.formulario.set(null);
        await this.cargar();
      } else {
        this.error.set(resultado.error ?? 'No fue posible guardar el evento.');
      }
    } catch {
      this.error.set('No fue posible guardar el evento. Revisa los campos e inténtalo de nuevo.');
    } finally {
      this.guardando.set(false);
    }
  }

  async cambiarEstado(evento: EventoAgenda, estado: string): Promise<void> {
    this.error.set(null);
    this.aviso.set(null);

    const resultado = await this.api.cambiarEstado(evento.id, estado);

    if (resultado.ok) {
      this.aviso.set(`«${evento.titulo}» quedó en ${this.etiquetasDeEstado[estado] ?? estado}.`);
      await this.cargar();
    } else {
      this.error.set(resultado.error ?? 'No fue posible cambiar el estado.');
    }
  }
}
