import { nombrePropio } from '../../../shared/texto/nombre-propio';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { Component, OnInit, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { LucideRefreshCw, LucideShieldCheck } from '@lucide/angular';
import { AdminAltaOrganizacionComponent } from '../admin-alta-organizacion/admin-alta-organizacion.component';
import {
  AsistenciaAOrganizacionService,
  MensajeAOrganizacion,
  ProcesoPendienteDeCierre,
} from '../../../core/services/asistencia-a-organizacion.service';
import { SelloDeProcedenciaComponent } from '../../../shared/components/ui/sello-de-procedencia/sello-de-procedencia.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { DialogoDeFormularioComponent } from '../../../shared/components/ui/dialogo-de-formulario/dialogo-de-formulario.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { etiquetaDeEstado } from '../domain/admin-config';
import {
  AdminService,
  FichaOrganizacionAdministrativa,
  OrganizacionAdministrativa,
  RespuestaOrganizaciones,
} from '../../../core/services/admin.service';

/**
 * Los cuatro estados del ciclo de vida, en palabras, para poder nombrarlos en un aviso.
 *
 * NO SUSTITUYE A `estadoEtiqueta` DEL CONTRATO, que es lo que la tabla pinta. Esto es para las
 * frases que se escriben DESPUES de una acción, cuando el servidor devuelve el código del estado en
 * el que quedó y hay que decirlo en español sin volver a pedir la ficha.
 */
const ETIQUETAS_DE_ESTADO: Readonly<Record<string, string>> = {
  pendiente_de_confirmacion: 'pendiente de confirmación',
  activa: 'activa',
  inactiva: 'inactiva',
  eliminada: 'eliminada',
};

/**
 * Las organizaciones del ecosistema, con quién responde por cada una y de dónde vino cada una.
 *
 * POR QUE ES UNA PESTAÑA APARTE Y NO UN MODULO MAS. Festivales apunta a `app-admin-records-panel`, que se
 * configura desde `ADMIN_MODULES`: una tabla con su esquema, su formulario y su circuito editorial.
 * Una organización no es un registro de ese tipo. Vive en `Entidades`, la persona que responde por
 * ella vive en otra tabla, y lo que hay que ver de ella —quién la representa y cuántos procesos
 * sostiene— no cabe en las columnas de aquel panel.
 *
 * DESDE EL 11 DE SEPTIEMBRE DE 2026 TAMBIÉN DA DE ALTA. Antes esta pantalla solo leía, y el
 * comentario remitía a una pestaña «Entidades» que ya no existe: el resultado era que una
 * organización solo podía entrar registrándose a sí misma desde fuera, de modo que todo lo que el
 * Programa conoce y nadie ha reclamado se quedaba sin poder escribir. El alta usa exactamente las
 * mismas piezas que el canal externo; lo único que cambia es la procedencia y los permisos.
 *
 * LO QUE ESTA PANTALLA SIGUE SIN HACER: editar una organización ya registrada. Se dice aquí en vez
 * de dejar que alguien lo busque.
 */
@Component({
  selector: 'app-admin-organizaciones-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent,DialogoDirective, FiltroDesplegableComponent, CabeceraDeTablaComponent,  DatoEnLecturaComponent,
    BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent, SelectorSegmentadoComponent,NombrePropioPipe, 
    CommonModule, FormsModule, AdminAltaOrganizacionComponent, SelloDeProcedenciaComponent,
    MenuDeAccionesComponent, IndicadorDeEstadoComponent,
    LucideRefreshCw, LucideShieldCheck, BuscadorDeListaComponent, HistorialDeRegistroComponent,
    DialogoDeFormularioComponent,
  ],
  templateUrl: './admin-organizaciones-panel.component.html',
})
export class AdminOrganizacionesPanelComponent implements OnInit {
  /** El estado con su nombre, nunca con su código. Ver `etiquetaDeEstado`. */
  readonly etiquetaDeEstado = etiquetaDeEstado;

  /**
   * Si hay algún filtro puesto que pueda estar escondiendo organizaciones que sí existen.
   *
   * <b>LA PESTAÑA DE ESTADO NO CUENTA.</b> Es la lista que se eligió mirar, no un filtro sobre
   * ella: la nombra `ambitoDeLaLista` y el botón de limpiar no la toca.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.busqueda().trim().length > 0 || this.departamentoActivo() !== 'todos',
  );

  /** Cómo nombrar la pestaña abierta cuando no es la de todas. */
  readonly ambitoDeLaLista = computed(() => {
    const estado = this.estadoActivo();
    return estado === 'todos' ? null : `en estado «${this.etiquetaDeEstado(estado)}»`;
  });

  /** Quita la búsqueda y el territorio, y deja la pestaña donde está. */
  limpiarFiltros(): void {
    this.busqueda.set('');
    this.departamentoActivo.set('todos');
    this.pagina.set(1);
    this.cargar();
  }

  private readonly adminService = inject(AdminService);
  private readonly asistencia = inject(AsistenciaAOrganizacionService);

  readonly respuesta = signal<RespuestaOrganizaciones | null>(null);
  readonly cargando = signal(false);
  readonly error = signal('');
  readonly estadoActivo = signal<string>('todos');
  readonly departamentoActivo = signal<string>('todos');
  readonly busqueda = signal('');

  /** El formulario de alta, abierto o no. */
  readonly dandoDeAlta = signal(false);

  readonly avisoDeAlta = signal<string | null>(null);

  /** El cajón que pide las organizaciones sin departamento. Igual que en el servidor. */
  readonly SIN_TERRITORIO = 'sin_territorio';

  /**
   * Lo que se le pide al servidor. NO es lo que ordena la tabla: eso lo decide el servidor y
   * vuelve en la respuesta.
   */
  readonly ordenPedido = signal<string>('nombre');
  readonly direccionPedida = signal<'asc' | 'desc'>('asc');

  /**
   * El orden que DIBUJA la cabecera, en la pieza compartida de tablas.
   *
   * SE FIJA DESDE LA RESPUESTA Y NO DESDE LO PEDIDO, a propósito: si se pide una columna que el
   * servidor no admite, este se cae al orden por nombre, y pintar la flecha con lo pedido dejaría
   * una flechita sobre una cabecera que no ordenó nada. La tabla estaría afirmando algo falso.
   */
  readonly orden = new OrdenDeTabla('nombre', 'asc');

  readonly organizaciones = computed(() => this.respuesta()?.items ?? []);
  readonly estados = computed(() => this.respuesta()?.estados ?? []);

  /** «Todas» y los estados con su recuento, para el selector segmentado compartido. */
  readonly pestanasDeEstado = computed<readonly OpcionSegmentada[]>(() => [
    { id: 'todos', etiqueta: 'Todas', conteo: this.totalDeEstados() },
    ...this.estados().map(e => ({ id: e.id, etiqueta: e.etiqueta, conteo: e.total })),
  ]);
  readonly territorios = computed(() => this.respuesta()?.territorios ?? []);
  readonly total = computed<number>(() => Number(this.respuesta()?.total ?? 0));

  /**
   * Las columnas de la tabla, en el orden en que se pintan.
   *
   * LA LISTA VIVE AQUI Y NO EN LA PLANTILLA para que la cabecera, la flecha y el `aria-sort` no
   * puedan decir tres cosas distintas: los tres salen de la misma fila.
   */
  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'nombre', etiqueta: 'Organización' },
    { id: 'responsable', etiqueta: 'Responsable' },
    { id: 'procesos', etiqueta: 'Procesos' },
    { id: 'territorio', etiqueta: 'Territorio' },
    { id: 'creacion', etiqueta: 'Creación' },
    { id: 'estado', etiqueta: 'Estado' },
    // «ACCIONES» NO SE ORDENA, Y HASTA HOY LO PARECIA: la cabecera se dibujaba como botón, con su
    // flecha y el título «Ordenar por Acciones», y al pulsarla no pasaba nada. Una cabecera que
    // ofrece ordenar y no ordena es la misma clase de defecto que un contrato que el servidor no
    // aplica: la pantalla promete algo que detrás no existe.
    { id: 'acciones', etiqueta: 'Acciones', clases: 'text-right', ordenable: false },
  ];

  /**
   * La columna por la que el SERVIDOR ordenó, que no siempre es la que se pidió.
   *
   * SE LEE DE LA RESPUESTA Y NO DE `ordenPedido` A PROPOSITO. Si se pidiera una columna que el
   * servidor no admite, este se cae al orden por nombre; pintando la flecha con lo pedido, quedaría
   * una flechita sobre una cabecera que no ordenó nada, y la tabla estaría afirmando algo falso.
   */
  readonly ordenAplicado = computed<string>(() => String(this.respuesta()?.orden ?? 'nombre'));
  readonly direccionAplicada = computed<string>(() => String(this.respuesta()?.direccion ?? 'asc'));

  /**
   * Las opciones del desplegable de territorio.
   *
   * SI EL DEPARTAMENTO ELEGIDO SE QUEDA SIN FILAS —porque el filtro de estado o la búsqueda las
   * quitaron— el servidor deja de listarlo, y el desplegable se quedaría en blanco mientras la
   * tabla sigue filtrada por él. Se añade de vuelta con cero para que el control diga la verdad
   * sobre lo que está puesto.
   */
  readonly opcionesDeTerritorio = computed(() => {
    const opciones = this.territorios();
    const elegido = this.departamentoActivo();
    if (elegido === 'todos' || opciones.some(opcion => String(opcion?.codigo) === elegido)) {
      return opciones;
    }
    return [
      ...opciones,
      {
        codigo: elegido,
        etiqueta: elegido === this.SIN_TERRITORIO ? 'Sin territorio' : 'Departamento ' + elegido,
        total: 0,
      },
    ];
  });

  /** Cuántas hay en todos los territorios juntos, con la búsqueda y el estado ya aplicados. */
  readonly totalDeTerritorios = computed<number>(
    () => this.territorios().reduce((suma, opcion) => suma + (Number(opcion?.total) || 0), 0),
  );

  /**
   * La cifra de la pestaña «Todas».
   *
   * SALE DE SUMAR LAS FACETAS, NO DE `total`. `total` es el recuento del resultado ya filtrado:
   * con «Registrada» pulsada valía 1, así que la pestaña «Todas» decía 1 al lado de una lista de
   * una fila, y pulsarla mostraba tres. La suma de las facetas de estado es lo que saldría al
   * quitar ese filtro, que es justo lo que esa pestaña promete.
   */
  readonly totalDeEstados = computed<number>(
    () => this.estados().reduce((suma, estado) => suma + (Number(estado?.total) || 0), 0),
  );

  /**
   * Cuántas filas se piden por página.
   *
   * ANTES SE PEDIAN CIEN, QUE ES EL TOPE DEL SERVIDOR, y no había manera de ver la fila ciento uno:
   * la tabla mostraba cien ordenadas y la más antigua se quedaba fuera. Veinticinco caben en una
   * pantalla sin obligar a desplazarse hasta el pie para saber si hay más.
   */
  readonly TAMANO_DE_PAGINA = 25;

  /** La página que se PIDIO. La que se está viendo la dice el servidor. */
  readonly pagina = signal(1);

  readonly paginaAplicada = computed<number>(() => Number(this.respuesta()?.pagina ?? 1));

  readonly tamanoAplicado = computed<number>(
    () => Number(this.respuesta()?.tamanoPagina) || this.TAMANO_DE_PAGINA,
  );

  readonly totalDePaginas = computed<number>(
    () => Math.max(1, Math.ceil(this.total() / Math.max(1, this.tamanoAplicado()))),
  );

  /**
   * El número de la primera fila de la página, contando desde uno.
   *
   * SE CALCULA CON LA PAGINA QUE DEVOLVIO EL SERVIDOR, no con la pedida: si se piden más páginas de
   * las que hay, el rango diría «126–150 de 7».
   */
  readonly desde = computed<number>(
    () => (this.total() === 0 ? 0 : (this.paginaAplicada() - 1) * this.tamanoAplicado() + 1),
  );

  readonly hasta = computed<number>(
    () => (this.total() === 0 ? 0 : this.desde() + this.organizaciones().length - 1),
  );

  readonly hayPaginaAnterior = computed<boolean>(() => this.paginaAplicada() > 1);

  readonly hayPaginaSiguiente = computed<boolean>(() => this.paginaAplicada() < this.totalDePaginas());

  /**
   * Cuántas organizaciones no tienen a nadie declarado en `EntidadesResponsable`.
   *
   * NO ES UNA CIFRA DECORATIVA. La tabla nació y solo la escribe el alta
   * externa: todo lo sembrado antes cae en el respaldo —la cuenta que administra la entidad—, que
   * es una deducción, no una persona identificada. Este número dice cuánto del registro está en
   * esa situación sin que haya que contar filas a ojo.
   */
  readonly sinResponsableDeclarado = computed<number>(
    () => this.organizaciones().filter(item => item?.responsable?.origen !== 'declarado').length,
  );
  LucideRefreshCw = LucideRefreshCw;
  LucideShieldCheck = LucideShieldCheck;

  abrirAlta(): void {
    this.avisoDeAlta.set(null);
    this.dandoDeAlta.set(true);
  }

  cerrarAlta(): void { this.dandoDeAlta.set(false); }

  /** Cierra el formulario y recarga: la organización nueva tiene que salir en la tabla. */
  registrada(organizacion: { id: string; nombre: string }): void {
    this.dandoDeAlta.set(false);
    this.avisoDeAlta.set(`«${organizacion.nombre}» quedó registrada. Su procedencia es el Programa.`);
    this.cargar();
  }

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set('');
    this.adminService
      .cargarOrganizaciones({
        estado: this.estadoActivo(),
        departamento: this.departamentoActivo(),
        q: this.busqueda(),
        orden: this.ordenPedido(),
        direccion: this.direccionPedida(),
        pagina: this.pagina(),
        tamano: this.TAMANO_DE_PAGINA,
      })
      .subscribe({
        next: datos => {
          this.respuesta.set(datos);
          this.orden.fijar(String(datos?.orden ?? 'nombre'), String(datos?.direccion ?? 'asc') === 'desc' ? 'desc' : 'asc');
          this.cargando.set(false);
        },
        error: (fallo: { status?: number | null }) => {
          this.respuesta.set(null);
          this.cargando.set(false);
          // UN 404 AQUI NO ES UN FALLO, ES UN DESFASE: la API local sirve el binario con el que
          // arrancó, y hasta que no se recarga esta ruta no existe. El mensaje genérico escondía la
          // única pista que resolvía el problema.
          this.error.set(
            fallo?.status === 404
              ? 'La API que responde no tiene todavía la ruta de organizaciones. Reiníciala para que cargue la versión actual.'
              : 'No fue posible leer las organizaciones.',
          );
        },
      });
  }

  filtrarPor(estado: string): void {
    if (this.estadoActivo() === estado) return;
    this.estadoActivo.set(estado);
    // TODO FILTRO VUELVE A LA PRIMERA PAGINA. Estando en la cuatro y filtrando a tres resultados,
    // el servidor devuelve una página vacía y la tabla dice «no hay organizaciones para este
    // filtro», que es mentira: las hay, pero en otra página.
    this.pagina.set(1);
    this.cargar();
  }

  filtrarPorTerritorio(departamento: string): void {
    const pedido = departamento || 'todos';
    if (this.departamentoActivo() === pedido) return;
    this.departamentoActivo.set(pedido);
    this.pagina.set(1);
    this.cargar();
  }

  /**
   * La búsqueda vuelve al principio; el botón de actualizar no, que no cambia el filtro.
   *
   * LLEGA CON EL TEXTO YA ESPERADO por el buscador compartido, que avisa cuando la escritura se
   * detiene. Antes había que pulsar Intro.
   */
  buscar(texto: string): void {
    this.busqueda.set(texto);
    this.pagina.set(1);
    this.cargar();
  }

  irAPagina(numero: number): void {
    const destino = Math.min(Math.max(1, Math.trunc(numero) || 1), this.totalDePaginas());
    if (destino === this.paginaAplicada()) return;
    this.pagina.set(destino);
    this.cargar();
  }

  /**
   * Ordena por una columna, o le da la vuelta si ya estaba puesta.
   *
   * NO HAY TERCER ESTADO «SIN ORDEN», y eso es deliberado. El servidor devuelve una página de un
   * resultado ordenado: siempre hay un orden. Un botón que fingiera apagarlo dejaría el de por
   * omisión —el nombre— con la tabla diciendo que no ordena por nada.
   */
  /** Los territorios, en la forma que pide el filtro compartido y sin mayúsculas sostenidas. */
  readonly filtroDeTerritorio = computed<readonly OpcionDeFiltro[]>(() => [
    { id: 'todos', etiqueta: 'Todo el país', conteo: this.totalDeTerritorios() },
    ...this.opcionesDeTerritorio().map(o => ({ id: String(o.codigo), etiqueta: nombrePropio(o.etiqueta), conteo: o.total })),
  ]);

  ordenarPor(columna: string): void {
    // SE ALTERNA SOBRE LO PEDIDO, NO SOBRE LO DIBUJADO: la cabecera enseña el orden que el
    // servidor aplicó, y el segundo clic invierte el que se pidió. Ver `requestSort` en Festivales.
    if (this.ordenPedido() === columna) {
      this.direccionPedida.set(this.direccionPedida() === 'asc' ? 'desc' : 'asc');
    } else {
      this.ordenPedido.set(columna);
      this.direccionPedida.set('asc');
    }
    // Cambiar el orden reordena TODO el resultado, así que la página cuatro de antes ya no
    // contiene las mismas filas: quedarse en ella sería quedarse en un sitio que ya no existe.
    this.pagina.set(1);
    this.cargar();
  }

  /** El único proceso activo es Festivales; no se anuncian directorios aún no construidos. */
  /**
   * El desglose de sus procesos, que es lo que un número suelto no dice.
   *
   * «3» PUEDE SER TRES BORRADORES QUE NADIE HA VISTO O TRES FESTIVALES PUBLICADOS de los que
   * depende media región. Quien decide si archivar una organización necesita distinguirlos.
   */
  procesosDe(organizacion: OrganizacionAdministrativa): { etiqueta: string; total: number }[] {
    const procesos = organizacion?.procesos ?? ({} as OrganizacionAdministrativa['procesos']);
    const total = Number(procesos.total) || 0;
    const publicados = Number(procesos.publicados) || 0;
    const enCurso = Number(procesos.enCurso) || 0;
    return [
      { etiqueta: 'Publicados', total: publicados },
      { etiqueta: 'En curso', total: enCurso },
      { etiqueta: 'Borradores', total: Math.max(0, total - publicados - enCurso) },
    ].filter(item => item.total > 0);
  }

  /** Cuántos de sus procesos quedarían sin nadie que responda por ellos si se cierra. */
  dependientesDe(organizacion: OrganizacionAdministrativa): number {
    return Number(organizacion?.procesos?.dependientes) || 0;
  }

  /** El tono y el matiz del indicador de estado, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  // ─────────────────────── Las acciones de cada fila ───────────────────────
  //
  // POR QUE ESTAN AQUI Y NO SOLO DENTRO DE LA FICHA. El criterio es este: // «aún no entiendo cómo editar el estado de las organizaciones […] aparece en la última columna
  // pero no sé cómo cambiarles los estados». Tenía razón: para cambiarlo había que adivinar que la
  // fila se abre al pulsarla, encontrar el botón «Estado y vigencia» dentro de la ficha y pulsarlo.
  // Una acción que hay que descubrir por ensayo y error no existe. Ahora la columna del estado tiene
  // al lado el menú que lo cambia.

  /**
   * Las acciones que esta organización admite hoy.
   *
   * SALEN DE SU ESTADO. Ofrecer «Activar» a una organización ya activa es ofrecer un acto sin
   * efecto, y quien lo pulse no sabrá si funcionó. La institucional no admite ninguna de las tres
   * que la cierran: responde por todo registro que nadie ha reclamado.
   */
  accionesDe(organizacion: OrganizacionAdministrativa): AccionDeRegistro[] {
    // «HISTORIAL» ESTA EN LAS CUATRO PANTALLAS Y DICE LO MISMO EN TODAS. Es una lectura: no cambia
    // nada, no depende del estado y la institucional también la admite —también a ella la han
    // tocado—. Por eso va con «Abrir ficha», antes de la comprobación que corta el resto.
    const acciones: AccionDeRegistro[] = [
      { id: 'ficha', etiqueta: 'Abrir ficha' },
      { id: 'historial', etiqueta: 'Historial' },
    ];
    if (organizacion.esInstitucional) return acciones;

    // MIENTRAS NO HAYA PROVEEDOR DE CORREO, ESTA ES LA UNICA SALIDA de una organización que llamó
    // por teléfono: el enlace de confirmación no sale del sistema y ella no puede abrirlo. No es un
    // atajo —exige decir cómo se comprobó y queda en la bitácora— y por eso va en el menú, arriba,
    // donde la ve quien está mirando por qué esa organización sigue bloqueada.
    if (!organizacion.correoConfirmado && organizacion.correosDeCuenta.length > 0) {
      acciones.push({ id: 'confirmar-correo', etiqueta: 'Confirmar el correo' });
    }

    for (const opcion of this.accionesDeCicloDeVida) {
      if (opcion.estado === organizacion.estado) continue;
      // «ACTIVAR» SIGNIFICA DOS COSAS DISTINTAS, y solo una de ellas es un atajo. Sobre una
      // organización cerrada significa «devuélvele el acceso», y eso es legítimo aunque su correo
      // siga sin comprobar: el servidor la deja en el estado que le corresponde —«pendiente de
      // confirmación»— en vez de mentir con «activa». Sobre una que ya está pendiente significaría
      // «sáltate la confirmación», y eso sí se rechaza; ahí lo que resuelve su caso es confirmar.
      if (opcion.estado === 'activa'
        && !organizacion.correoConfirmado
        && organizacion.estado === 'pendiente_de_confirmacion') continue;
      acciones.push({
        id: opcion.estado,
        etiqueta: opcion.accion,
        // ELIMINAR ES LO UNICO QUE NO SE DESHACE, y va separada al final del menú por eso.
        tono: opcion.estado === 'eliminada' ? 'peligro' : 'normal',
      });
    }
    return acciones;
  }

  /**
   * Ejecuta la acción elegida desde la tabla.
   *
   * LAS TRES DEL CICLO DE VIDA ABREN LA FICHA CON EL DIALOGO YA PUESTO en vez de actuar a ciegas:
   * desactivar y eliminar exigen motivo, y eliminar además puede tener que resolver qué pasa con
   * los procesos de la organización. Hacerlo desde un menú sin enseñar nada de eso convertiría un
   * acto con consecuencias en un clic descuidado.
   */
  ejecutarAccion(organizacion: OrganizacionAdministrativa, accion: string): void {
    // EL HISTORIAL NO ABRE LA FICHA. Se consulta para responder «quién tocó esto y cuándo», que es
    // una pregunta sobre la fila, no sobre el expediente; abrir la ficha detrás dejaría al usuario
    // en otra pantalla al cerrar el panel.
    if (accion === 'historial') {
      this.historialAbierto.set(organizacion);
      return;
    }
    this.abrirFicha(organizacion);
    if (accion === 'ficha') return;
    if (accion === 'confirmar-correo') {
      this.confirmacionPedidaAlAbrir = true;
      return;
    }
    this.estadoPedidoAlAbrir = accion;
  }

  /** El estado que se pidió desde la tabla, para abrir el diálogo en cuanto llegue la ficha. */
  private estadoPedidoAlAbrir: string | null = null;

  /** Igual, para la confirmación del correo: se pide desde la tabla y se resuelve en la ficha. */
  private confirmacionPedidaAlAbrir = false;

  totalDeProcesos(organizacion: OrganizacionAdministrativa): number {
    return Number(organizacion?.procesos?.total) || 0;
  }

  /**
   * De dónde salió el nombre del responsable, en una palabra.
   *
   * SE PINTA SIEMPRE, TAMBIEN CUANDO ES EL BUENO. Una marca que solo aparece en el caso malo
   * obliga a distinguir «esto es un dato firmado» de «esto no se cargó», y las dos cosas se ven
   * igual: un hueco.
   */
  origenDe(organizacion: OrganizacionAdministrativa): { texto: string; clase: string } {
    const origen = String(organizacion?.responsable?.origen ?? '');
    // SOLO EL COLOR DEL TEXTO, sin fondo ni borde: las píldoras se retiraron de todo el diseño el
    // 12 de septiembre de 2026. El color sigue distinguiendo un dato firmado de uno deducido.
    if (origen === 'declarado') {
      return { texto: 'Declarado en el alta', clase: 'text-[#0b6b39]' };
    }
    if (origen === 'cuenta') {
      return { texto: 'Deducido de la cuenta', clase: 'text-amber-700' };
    }
    return { texto: 'Sin responsable', clase: 'text-slate-400' };
  }

  /**
   * El color de cada estado de organización.
   *
   * LOS CINCO CODIGOS RETIRADOS SIGUEN TENIENDO COLOR. La base movió las filas, pero un volcado
   * viejo o un enlace guardado pueden traer el código antiguo, y una píldora sin clase se ve como
   * un defecto de la pantalla y no como un dato desactualizado.
   */
  claseDeEstado(estado: string): string {
    switch (estado) {
      case 'activa': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'pendiente_de_confirmacion': return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'inactiva': return 'bg-slate-200 text-slate-700 border-slate-300';
      case 'eliminada': return 'bg-rose-50 text-rose-700 border-rose-200';
      default: return 'bg-slate-100 text-slate-500 border-slate-200';
    }
  }

  // ───────────────────────────── La ficha de una organización ─────────────────────────────
  //
  // EL USUARIO LA PIDIÓ EL 2 DE SEPTIEMBRE DE 2026: «abrir ficha; consultar personas asociadas;
  // consultar procesos administrados; revisar solicitudes; revisar vinculaciones; consultar
  // historial». La tabla ya dice cuántos procesos administra cada fila; esto trae CUÁLES, con
  // nombre, más lo que la involucra desde otras tablas.

  readonly fichaAbiertaId = signal<string | null>(null);
  readonly ficha = signal<FichaOrganizacionAdministrativa | null>(null);
  readonly cargandoFicha = signal(false);
  readonly errorFicha = signal('');

  /**
   * La organización que otra pantalla pidió abrir.
   *
   * <b>LA NAVEGACION DEL ECOSISTEMA LLEGA HASTA EL REGISTRO, no hasta su lista.</b> Desde la ficha
   * de un Festival, «Ver organización» abría la sección de Organizaciones y ahí se acababa: quien
   * venía de un Festival tenía que buscar a mano, entre todas, la organización cuyo nombre acababa
   * de leer. Una relación que existe en la base y no se puede recorrer en pantalla es una relación
   * que nadie puede administrar.
   */
  readonly organizacionPedida = input<string | null>(null);

  /**
   * Pide abrir la ficha de uno de los procesos que administra esta organización.
   *
   * LO RESUELVE LA CONSOLA Y NO ESTE PANEL: la ficha de Festival vive en la sección del ecosistema,
   * y abrirla desde aquí significaría montar dos veces la misma pantalla en dos sitios distintos.
   */
  readonly verFestival = output<string>();

  /** La organización cuyo historial se está consultando, o `null`. */
  readonly historialAbierto = signal<OrganizacionAdministrativa | null>(null);

  /** Con qué nombre escribe este módulo sus líneas en la bitácora. */
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.organizaciones;

  abrirFicha(organizacion: OrganizacionAdministrativa): void {
    const id = String(organizacion.id);
    if (this.fichaAbiertaId() === id) {
      this.cerrarFicha();
      return;
    }
    this.fichaAbiertaId.set(id);
    this.ficha.set(null);
    this.avisoDeAsistencia.set(null);
    this.errorDeAsistencia.set(null);
    void this.cargarMensajes(id);
    this.errorFicha.set('');
    this.cargandoFicha.set(true);
    this.adminService.cargarFichaDeOrganizacion(id).subscribe({
      next: ficha => {
        this.cargandoFicha.set(false);
        this.ficha.set(ficha);
        // SI SE PIDIO UNA ACCION DESDE LA TABLA, el diálogo se abre ya con ella elegida: quien pulsó
        // «Desactivar» en el menú no tiene que volver a buscarla en el desplegable.
        const pedido = this.estadoPedidoAlAbrir;
        this.estadoPedidoAlAbrir = null;
        if (pedido) {
          this.administrarEstado();
          this.campoDelEstado('estado', pedido);
        }
        if (this.confirmacionPedidaAlAbrir) {
          this.confirmacionPedidaAlAbrir = false;
          this.confirmarElCorreo();
        }
      },
      error: error => {
        this.cargandoFicha.set(false);
        this.estadoPedidoAlAbrir = null;
        this.confirmacionPedidaAlAbrir = false;
        this.errorFicha.set(error?.message || 'No fue posible abrir la ficha de la organización.');
      },
    });
  }

  /**
   * Abre la ficha que pidió otra pantalla, en cuanto la lista la tenga.
   *
   * SE ESPERA A QUE LA LISTA ESTE CARGADA porque la ficha se abre con la fila, no con el
   * identificador suelto: la cabecera del panel necesita el nombre y el estado para pintarse, y
   * pedirlos otra vez sería una consulta de más para un dato que ya está en la página.
   */
  private readonly abrirLaPedida = effect(() => {
    const pedida = this.organizacionPedida();
    if (!pedida || this.fichaAbiertaId() === pedida) return;
    const fila = this.organizaciones().find(item => String(item.id) === pedida);
    if (fila) untracked(() => this.abrirFicha(fila));
  });

  cerrarFicha(): void {
    this.fichaAbiertaId.set(null);
    this.ficha.set(null);
    this.errorFicha.set('');
    this.mensajes.set([]);
    this.formularioDeMensaje.set(null);
    this.formularioDeEstado.set(null);
    this.formularioDeConfirmacion.set(null);
  }

  // ─────────────────────────── Asistencia ───────────────────────────
  //
  // ASISTIR NO ES EDITAR. Un funcionario no corrige los datos de una organización que no es suya:
  // le escribe para que ella los corrija, y administra el estado de su ficha cuando ya no opera.
  // Es la misma distinción que sostiene la procedencia de los registros.

  readonly mensajes = signal<MensajeAOrganizacion[]>([]);
  readonly formularioDeMensaje = signal<{ asunto: string; mensaje: string } | null>(null);
  readonly formularioDeEstado = signal<{ estado: string; motivo: string; estadoActual: string } | null>(null);
  readonly trabajando = signal(false);
  readonly avisoDeAsistencia = signal<string | null>(null);
  readonly errorDeAsistencia = signal<string | null>(null);

  /**
   * El ciclo de vida de una organización: cuatro estados y una acción para cada uno.
   *
   * <b>ES UN SOLO EJE.</b> Hasta había dos —un «estado del registro» y
   * una marca de «vigencia» aparte— y el diálogo llegó a tener un recuadro explicando la diferencia
   * entre los dos campos, que es la señal de que el modelo estaba mal. Ahora cada estado dice a la
   * vez cómo está la organización y qué puede hacer, y la base impide que discrepen.
   *
   * <b>Y CADA UNO SE PIDE POR SU ACCION, no eligiéndolo de una lista.</b> «Desactivar» y «Eliminar»
   * son actos con consecuencias distintas; un desplegable los presentaba como dos opciones
   * intercambiables.
   */
  readonly accionesDeCicloDeVida = [
    {
      estado: 'activa',
      accion: 'Activar',
      ayuda: 'Vuelve a operar con normalidad: recupera el acceso y puede registrar y publicar.',
    },
    {
      estado: 'inactiva',
      accion: 'Desactivar',
      ayuda: 'Deja de poder entrar y conserva su ficha, sus procesos y sus cuentas. Se puede deshacer.',
    },
    {
      estado: 'eliminada',
      // DICE QUE SE ELIMINA, como en Festivales —«Eliminar del ecosistema»— y en Usuarios
      // —«Eliminar la cuenta»—. Un «Eliminar» a secas en un menú que también ofrece desactivar y
      // confirmar el correo no distingue si se va la organización, su acceso o su ficha.
      accion: 'Eliminar del ecosistema',
      ayuda: 'Sale del ecosistema. Antes hay que decidir qué pasa con los procesos que administra.',
    },
  ];

  /**
   * Las acciones que la organización admite desde el estado en el que está.
   *
   * PENDIENTE DE CONFIRMACION NO ES UNA ACCION DE LA CONSOLA, y por eso no aparece: a ese estado se
   * llega naciendo, y se sale confirmando el correo. Activar a mano una organización que no ha
   * confirmado saltaría la única comprobación que evita que el Programa reciba un registro atado a
   * una dirección que nadie controla.
   */
  readonly accionesDisponibles = computed(() => {
    const organizacion = this.ficha()?.organizacion;
    const actual = organizacion?.estado ?? '';
    return this.accionesDeCicloDeVida.filter(opcion => {
      if (opcion.estado === actual) return false;
      // LA MISMA REGLA QUE EL MENU DE LA TABLA, escrita una vez y leída desde los dos sitios sería
      // mejor; mientras el diálogo viva sobre la ficha y el menú sobre la fila, al menos coinciden.
      if (opcion.estado === 'activa'
        && organizacion?.correoConfirmado === false
        && actual === 'pendiente_de_confirmacion') return false;
      return true;
    });
  });

  /** La ayuda de la acción elegida, para que el diálogo no obligue a saberse las tres. */
  readonly ayudaDelEstado = computed<string>(() => {
    const f = this.formularioDeEstado();
    return this.accionesDeCicloDeVida.find(e => e.estado === f?.estado)?.ayuda ?? '';
  });

  /** Cómo se llama el acto que se está pidiendo, para el botón de confirmar. */
  readonly accionElegida = computed<string>(() => {
    const f = this.formularioDeEstado();
    return this.accionesDeCicloDeVida.find(e => e.estado === f?.estado)?.accion ?? 'Guardar';
  });

  /**
   * Si el cambio pedido cierra la organización.
   *
   * ARCHIVAR O DESACTIVAR LA SACA DE LA OPERACION, y dentro de seis meses nadie recordará por qué.
   * Por eso solo entonces se exige el motivo: pedirlo para reactivar añadiría fricción a deshacer
   * un error.
   */
  readonly cierraLaOrganizacion = computed(() => {
    const estado = this.formularioDeEstado()?.estado;
    return estado === 'inactiva' || estado === 'eliminada';
  });

  private async cargarMensajes(id: string): Promise<void> {
    const resultado = await this.asistencia.mensajes(id);
    this.mensajes.set(resultado.ok && resultado.data ? resultado.data.items : []);
  }

  escribirle(): void {
    this.avisoDeAsistencia.set(null);
    this.errorDeAsistencia.set(null);
    this.formularioDeMensaje.set({ asunto: '', mensaje: '' });
  }

  cerrarMensaje(): void { this.formularioDeMensaje.set(null); }

  campoDelMensaje(clave: 'asunto' | 'mensaje', valor: string): void {
    const actual = this.formularioDeMensaje();
    if (!actual) { return; }
    this.formularioDeMensaje.set({ ...actual, [clave]: valor });
  }

  async enviarMensaje(): Promise<void> {
    const f = this.formularioDeMensaje();
    const id = this.fichaAbiertaId();
    if (!f || !id) { return; }

    this.trabajando.set(true);
    this.errorDeAsistencia.set(null);
    try {
      const resultado = await this.asistencia.escribir(id, { asunto: f.asunto.trim(), mensaje: f.mensaje.trim() });
      if (resultado.ok && resultado.data) {
        const cuantos = resultado.data.destinatarios;
        this.avisoDeAsistencia.set(`Mensaje enviado a ${cuantos} ${cuantos === 1 ? 'persona' : 'personas'} de la organización.`);
        this.formularioDeMensaje.set(null);
        await this.cargarMensajes(id);
      } else {
        this.errorDeAsistencia.set(resultado.error ?? 'No fue posible enviar el mensaje.');
      }
    } finally {
      this.trabajando.set(false);
    }
  }

  // ─────────────────── Confirmar el correo por otra vía ───────────────────
  //
  // POR QUE EXISTE. Mientras SIMUS no tenga proveedor de correo, el enlace de confirmación se queda
  // en la cola de salida y la organización no puede abrirlo nunca. La salida no es apagar la regla
  // —eso la volvería un adorno— sino que una persona del Programa compruebe la dirección por otra
  // vía y lo declare, con su nombre, la fecha y el motivo. Cuando llegue el proveedor esta vía se
  // queda como recurso excepcional: el caso «la organización no recibe nuestro correo» no
  // desaparece con el proveedor.

  readonly formularioDeConfirmacion = signal<{ motivo: string; correo: string } | null>(null);

  /** Las direcciones entre las que elegir. Con una sola no se pregunta. */
  readonly correosParaConfirmar = computed<string[]>(
    () => this.ficha()?.organizacion.correosDeCuenta ?? []);

  confirmarElCorreo(): void {
    const f = this.ficha();
    if (!f) { return; }
    this.avisoDeAsistencia.set(null);
    this.errorDeAsistencia.set(null);
    const correos = f.organizacion.correosDeCuenta ?? [];
    this.formularioDeConfirmacion.set({ motivo: '', correo: correos.length === 1 ? correos[0] : '' });
  }

  cerrarConfirmacion(): void { this.formularioDeConfirmacion.set(null); }

  campoDeConfirmacion<K extends 'motivo' | 'correo'>(clave: K, valor: string): void {
    const actual = this.formularioDeConfirmacion();
    if (!actual) { return; }
    this.formularioDeConfirmacion.set({ ...actual, [clave]: valor });
  }

  /**
   * Si el motivo escrito basta.
   *
   * DIEZ CARACTERES NO SON UN CAPRICHO: «ok» o «sí» no dicen cómo se comprobó nada, y sin el cómo
   * la bitácora solo diría que alguien lo dio por bueno, que es exactamente un bypass con otro
   * nombre. El servidor exige lo mismo; aquí se dice antes de pulsar.
   */
  readonly motivoDeConfirmacionSuficiente = computed(
    () => (this.formularioDeConfirmacion()?.motivo ?? '').trim().length >= 10);

  async guardarConfirmacion(): Promise<void> {
    const f = this.formularioDeConfirmacion();
    const id = this.fichaAbiertaId();
    if (!f || !id) { return; }

    this.trabajando.set(true);
    this.errorDeAsistencia.set(null);
    try {
      const resultado = await this.asistencia.confirmarCorreo(id, {
        motivo: f.motivo.trim(),
        correo: f.correo.trim() || undefined,
      });
      if (resultado.ok && resultado.data) {
        const activadas = Number(resultado.data.organizacionesActivadas) || 0;
        // SE DICE CUANTAS ORGANIZACIONES SE ACTIVARON, no solo «hecho»: el correo es de una persona
        // que puede responder por varias, y si se activaron dos hay que saberlo.
        this.avisoDeAsistencia.set(
          `Correo ${resultado.data.correoConfirmado} dado por comprobado.` +
          (activadas === 0
            ? ' La organización conserva el estado que tenía.'
            : activadas === 1
              ? ' La organización queda activa.'
              : ` Quedan activas ${activadas} organizaciones de esa cuenta.`));
        this.formularioDeConfirmacion.set(null);
        this.abrirFichaPorId(id);
        this.cargar();
      } else {
        this.errorDeAsistencia.set(resultado.error ?? 'No fue posible confirmar el correo.');
      }
    } finally {
      this.trabajando.set(false);
    }
  }

  administrarEstado(): void {
    const f = this.ficha();
    if (!f) { return; }
    this.avisoDeAsistencia.set(null);
    this.errorDeAsistencia.set(null);
    this.procesosQueBloquean.set([]);
    // SE ABRE SIN ELEGIR NADA. Preseleccionar el estado actual convertía el diálogo en «cambia esto
    // si quieres»; lo que hay que pedir es un acto, y el acto empieza eligiéndolo.
    this.formularioDeEstado.set({
      estado: this.accionesDisponibles()[0]?.estado ?? '',
      motivo: '',
      estadoActual: f.organizacion.estadoEtiqueta,
    });
  }

  cerrarEstado(): void {
    this.formularioDeEstado.set(null);
    this.procesosQueBloquean.set([]);
  }

  campoDelEstado<K extends 'estado' | 'motivo'>(clave: K, valor: string): void {
    const actual = this.formularioDeEstado();
    if (!actual) { return; }
    // CAMBIAR LO QUE SE PIDE INVALIDA LA LISTA ANTERIOR: los procesos que bloqueaban un cierre no
    // dicen nada sobre una verificación, y dejarlos en pantalla haría creer que siguen bloqueando.
    this.procesosQueBloquean.set([]);
    this.formularioDeEstado.set({ ...actual, [clave]: valor });
  }

  /**
   * Los procesos que el servidor devolvió al rechazar el cierre.
   *
   * SE GUARDAN PARA PODER OFRECER QUE HACER CON ELLOS. El 409 no es un error de quien administra:
   * es el sistema diciendo que falta una decisión, y la pantalla tiene que ofrecerla en el mismo
   * sitio en vez de mandar a liberar los procesos uno por uno desde otra pantalla.
   */
  readonly procesosQueBloquean = signal<ProcesoPendienteDeCierre[]>([]);

  /**
   * Guarda el cambio de estado, y con él lo que se haya decidido sobre sus procesos.
   *
   * <b>SON DOS SALIDAS Y NO UNA.</b> Cerrar una organización que administra procesos publicados se
   * rechaza nombrándolos; la segunda petición dice qué hacer con ellos. «Liberar» devuelve su
   * custodia al Programa dejándolos publicados, para que otra organización pueda reclamarlos;
   * «archivar» los saca del sitio público conservando su historial, que es lo que corresponde
   * cuando el proceso terminó con la organización. Lo pidió la dirección de producto el 15 de
   * septiembre de 2026: «debe dar la opción de también eliminar los procesos registrados o
   * liberarlos».
   */
  async guardarEstado(queHacerConLosProcesos?: 'liberar' | 'archivar'): Promise<void> {
    const f = this.formularioDeEstado();
    const id = this.fichaAbiertaId();
    if (!f || !id) { return; }

    this.trabajando.set(true);
    this.errorDeAsistencia.set(null);
    try {
      const resultado = await this.asistencia.cambiarEstado(id, {
        estado: f.estado,
        motivo: f.motivo.trim() || undefined,
        queHacerConLosProcesos,
      });
      if (resultado.ok) {
        const afectados = Number(resultado.data?.procesosAfectados) || 0;
        const queSeHizo = String(resultado.data?.queSeHizoConLosProcesos ?? '');
        this.avisoDeAsistencia.set(this.resumenDelCambio(afectados, queSeHizo, String(resultado.data?.estado ?? '')));
        this.formularioDeEstado.set(null);
        this.procesosQueBloquean.set([]);
        // SE RECARGA LA FICHA Y LA TABLA: el estado sale en las dos, y dejar una con el valor
        // anterior haría dudar de si se guardó.
        this.abrirFichaPorId(id);
        this.cargar();
      } else {
        this.procesosQueBloquean.set(resultado.procesosPendientes ?? []);
        this.errorDeAsistencia.set(resultado.error ?? 'No fue posible cambiar el estado.');
      }
    } finally {
      this.trabajando.set(false);
    }
  }

  /**
   * Lo que acaba de pasar, en una frase: el cambio de estado nunca es lo único que ocurre.
   *
   * SE NOMBRA EL ESTADO EN EL QUE QUEDO, no el que se pidió. Reactivar una organización cuyo correo
   * sigue sin comprobar la devuelve al acceso pero la deja «pendiente de confirmación», y un aviso
   * que dijera solo «actualizado» dejaría a quien lo hizo creyendo que quedó activa.
   */
  private resumenDelCambio(afectados: number, queSeHizo = '', estado = ''): string {
    const nombre = ETIQUETAS_DE_ESTADO[estado];
    const partes = [nombre
      ? `La organización quedó ${nombre}.`
      : 'El estado de la organización quedó actualizado.'];
    // SE DICE QUE SE HIZO, NO SOLO CUANTOS. «Se liberaron 3» y «se archivaron 3» llevan a sitios
    // distintos: los primeros siguen publicados esperando a quien los reclame; los segundos no.
    if (afectados > 0 && queSeHizo === 'liberar') {
      partes.push(afectados === 1
        ? 'Su proceso volvió a la custodia del Programa y sigue publicado.'
        : `Sus ${afectados} procesos volvieron a la custodia del Programa y siguen publicados.`);
    }
    if (afectados > 0 && queSeHizo === 'archivar') {
      partes.push(afectados === 1
        ? 'Su proceso salió del sitio público y conserva su historial.'
        : `Sus ${afectados} procesos salieron del sitio público y conservan su historial.`);
    }
    return partes.join(' ');
  }

  /** Recarga la ficha abierta sin cerrarla. */
  private abrirFichaPorId(id: string): void {
    this.adminService.cargarFichaDeOrganizacion(id).subscribe({
      next: ficha => this.ficha.set(ficha),
      error: () => { /* La ficha anterior sigue en pantalla; el aviso ya dijo que se guardó. */ },
    });
  }

  /** Los festivales administrados, con nombre. */
  gruposDeProcesos(
    ficha: FichaOrganizacionAdministrativa,
  ): { etiqueta: string; items: { id: string; nombre: string; estado: string; dejaHuerfano: boolean }[] }[] {
    return [
      { etiqueta: 'Festivales', items: ficha?.festivales ?? [] },
    ].filter(grupo => grupo.items.length > 0);
  }

  /** El estado de un proceso en palabras, con el vocabulario que ya usa la ficha de Festival. */
  estadoDelProceso(estado: string): string {
    switch (estado) {
      case 'publicado': return 'Publicado';
      case 'en_revision': return 'En revisión';
      case 'ajustes_solicitados': return 'Ajustes solicitados';
      case 'aprobado': return 'Aprobado';
      case 'rechazado': return 'Rechazado';
      case 'archivado': return 'Archivado';
      case 'borrador': return 'Borrador';
      default: return estado || '—';
    }
  }
}
