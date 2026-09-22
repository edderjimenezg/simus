import { Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { AyudaConceptualCatalogoComponent } from '../../../shared/components/ui/ayuda-conceptual-catalogo/ayuda-conceptual-catalogo.component';
import { DatoDeLaFichaComponent } from '../../../core/revision-de-campos/dato-de-la-ficha.component';
import { ObservacionDeCampo } from '../../../core/revision-de-campos/revision-de-campos';
import { RevisionDeCamposStore } from '../../../core/revision-de-campos/revision-de-campos.store';
import { estadoDelFestival } from '../estados-del-festival';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import {
  PASOS_DE_LA_FICHA,
  PasoDeLaFicha,
  SeccionDeLaFicha,
  numeroDelPaso,
  pasoDeLaSeccion,
  seccion,
} from './secciones-de-la-ficha';
import {
  CatalogoDelFestival,
  CatalogosDelFestival,
  FalloDelServidor,
  FestivalDeLaOrganizacion,
  GuardarFestivalSolicitud,
  NIVELES_DE_COBERTURA,
  PanelOrganizacionApi,
  PropuestaCambioFestival,
  UbicacionDivipola,
} from '../panel-organizacion.api';

/** Los doce catálogos vacíos, para que la plantilla nunca lea de `null`. */
const CATALOGOS_VACIOS: CatalogosDelFestival = {
  practicasMusicales: [],
  territoriosSonoros: [],
  tipologias: [],
  expresionesArtisticas: [],
  fuentesFinanciacion: [],
  modalidadesParticipacion: [],
  naturalezasEntidad: [],
  tiposIngreso: [],
  tiposOrganizador: [],
  zonasUrbanoRural: [],
  titulacionesColectivas: [],
  regionesOcad: [],
};

// AQUI ESTABAN `FilaDeLocalizacion`, `FilaDeAliada` y `FilaDeArchivo`: las tres tablas que el
// formulario de la Edición editaba dentro de esta ficha. Ese formulario no lo abría nadie y se
// retiró; las mismas tres tablas se editan en
// `ficha-edicion-festival.component.ts`, que es el que sí se monta.

interface FormularioGeneral {
  nombre: string;
  descripcion: string;
  correoContacto: string;
  telefonoCelular: string;
  instagram: string;
  facebook: string;
  paginaWeb: string;
  otroEnlace: string;
  observacionesContacto: string;
  periodicidad: string;
  periodicidadDetalle: string;
  nivelCobertura: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
}

/**
 * Los campos de la VERSIÓN: `dbo.VersionesFestival`, o `ART_MUS_FESTIVALES_VERSION` en el volcado.
 *
 * SON CADENAS, INCLUSO LOS NUMÉRICOS Y LOS BOOLEANOS. Es lo que devuelve un control de HTML, y
 * mezclar `number | ''` con `string` en el mismo modelo obliga a comprobar el tipo en cada uso.
 * La normalización vive entera en `construirSolicitudDeVersion()`.
 */
/**
 * Las dos listas de casillas de la CABECERA del Festival.
 *
 * ERAN DOS TIPOS —`ListaDeCasillas`, con las cinco listas de la Edición, y este— porque la ficha
 * editaba los dos lados y una sola pareja de métodos que escribiera en «la lista que toque» habría
 * vuelto a mezclarlos. Al retirarse el formulario de la Edición queda
 * un solo lado, y con él un solo tipo.
 */
export type ListaDelFestival = 'practicasMusicalesIds' | 'territoriosSonorosIds';

function generalVacio(): FormularioGeneral {
  return {
    nombre: '', descripcion: '', correoContacto: '', telefonoCelular: '',
    instagram: '', facebook: '', paginaWeb: '', otroEnlace: '', observacionesContacto: '',
    periodicidad: '', periodicidadDetalle: '', nivelCobertura: 'municipal', codigoDepartamento: '', codigoMunicipio: '',
    practicasMusicalesIds: [], territoriosSonorosIds: [],
  };
}

/**
 * La ficha del Festival: la misma vista para leerla, para editarla y para crear un Festival.
 *
 * <b>QUÉ PROBLEMA CIERRA.</b> Había dos formularios sobre el mismo Festival: uno de nueve campos
 * para darlo de alta y otro, más largo, para completarlo después. El modelo de SIMUS guarda
 * cuarenta campos escribibles entre la cabecera y la edición, más cinco catálogos y tres tablas. El
 * usuario pidió unificarlos: «esa ficha completa la vamos a unificar cuando
 * le demos crear festival… la idea es que ese formulario lo seccionemos, por secciones con toda la
 * información».
 *
 * <b>LOS DOS BLOQUES, Y POR QUÉ.</b> El 29 de agosto de 2026 Se define lo que faltaba: «en
 * todo el formulario debemos identificar la diferencia entre la información de la edición o
 * ediciones, y distinguir eso; para tener un orden lógico es importante que primero se pregunte
 * toda la información básica en los primeros módulos, y después, si se desea, se creen botones para
 * añadir una edición de festival». Las tres primeras secciones escriben `dbo.Festivales`; las
 * nueve siguientes, `dbo.VersionesFestival`. Cada bloque vive bajo su pestaña y lleva su insignia,
 * porque «Correo de contacto» existe en los dos y son dos columnas de dos tablas distintas.
 *
 * <b>UN FESTIVAL, VARIAS EDICIONES.</b> Es lo que ata `ART_MUS_FESTIVALES_VERSION.ID_FESTIVAL`, y
 * lo que hace que el bloque del Festival se rellene una vez y el de la edición se repita. La ficha
 * de lectura lo dice con el número: «Este Festival tiene N ediciones registradas».
 *
 * <b>LEER Y EDITAR SON LA MISMA PANTALLA.</b> El criterio es este: * «quiero que mantengamos la misma vista para editar la edición del festival y para la ficha; si le
 * doy editar, me habilita los campos del festival… cuando se crea un festival también mantengamos
 * esta estructura, así homogenizamos». Con eso se fue el formulario de doce pantallas con «Atrás» y
 * «Siguiente»: las mismas dos pestañas y las mismas doce secciones sirven para los tres casos.
 *
 * <b>SOLO EL NOMBRE Y EL ALCANCE SON OBLIGATORIOS.</b> «Solo son obligatorios campos generales para
 * que el festival exista». Por eso «Guardar borrador» está disponible mientras se edita, sin
 * ninguna condición: todo lo demás se puede completar después, o no completarse nunca.
 *
 * <b>DOS FILAS Y DOS PETICIONES.</b> Guardar hace las dos llamadas en ese orden, porque sin
 * cabecera no hay a qué colgarle una edición. Si la segunda falla, la primera ya ocurrió, y el
 * mensaje lo dice.
 *
 * <b>LOS RÓTULOS SON LOS DEL VOLCADO.</b> «Fuente de financiación» y no «Fuente principal»,
 * «Página web» y no «Sitio web», «Director» y no «Dirección del Festival». Lo pidió el usuario el
 * 28 de agosto: «a los nombres de los campos no los cambies como vi que hiciste, en por ejemplo
 * "fuente de financiamiento"; déjalo tal cual». La única palabra que se aparta del volcado es
 * «edición» en lugar de «versión», y también la pidió él: «recuerda es edición y no versión». El
 * cambio es de pantalla: la base, el API y las rutas siguen diciendo VERSION.
 *
 * <b>CERRAR.</b> La X cierra siempre. El fondo y Escape cierran solo cuando NO se está editando:
 * con cuarenta campos rellenados, un clic fuera del diálogo perdería el trabajo entero.
 */
@Component({
  selector: 'app-ficha-festival',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, FormsModule, DialogoDirective, DatoDeLaFichaComponent, AyudaConceptualCatalogoComponent, NgTemplateOutlet],
  templateUrl: './ficha-festival.component.html',
  styles: [`
    /* La línea que dice qué se escribe en cada sección iba a 12 px en el gris de
       \`slate\`; es texto corrido y le toca el tamaño y el color del texto corrido
       de la consola. Ver los papeles del texto en \`styles.css\`. */
    .ayuda-de-seccion {
      display: block;
      margin-top: .2rem;
      max-width: 58ch;
      color: var(--color-prosa);
      font-size: var(--text-cuerpo);
      font-weight: 400;
      letter-spacing: normal;
      line-height: 1.5;
      text-transform: none;
    }
  `],
})
export class FichaFestivalComponent implements OnInit, OnDestroy {
  /**
   * El Festival cuya ficha se abre, o `null` para darlo de alta.
   *
   * `null` ES EL MODO ALTA y no un descuido: es lo que distingue «Crear un Festival» de «Ficha
   * completa». En alta no hay cabecera todavía, así que no hay versiones que pedir ni ficha que
   * leer, y el diálogo abre con los campos ya habilitados.
   */
  @Input() festival: FestivalDeLaOrganizacion | null = null;

  /** La organización que responde por el Festival. En alta, es a nombre de quién se registra. */
  @Input({ required: true }) organizacionId!: string;

  /** Solo para el subtítulo del alta. */
  @Input() nombreOrganizacion = '';

  /** Abrir directamente en el formulario, si la versión lo admite. Lo pide el botón «Editar». */
  @Input() abrirEnEdicion = false;

  /** Campo al que debe llegar una sugerencia de ajuste abierta desde el resumen del trámite. */
  @Input() campoInicial?: string;

  /** Limita la ficha incrustada a la sección que contiene el campo que se está corrigiendo. */
  @Input() seccionVisible?: string;

  /**
   * Limita el formulario incrustado al control exacto de una observación. La edición conserva el
   * mismo modelo, validación y guardado de la ficha completa; únicamente cambia su presentación.
   */
  @Input() campoVisible?: string;

  /** Evita desplazamientos y ornamentos propios de la ficha cuando el control vive en un inspector. */
  @Input() modoCampoContextual = false;

  /** La ruta de ajustes presenta la observación una sola vez, por fuera del formulario. */
  @Input() ocultarSugerenciasContextuales = false;

  /** Permite nombrar el guardado según la tarea que contiene la ficha. */
  @Input() etiquetaGuardar = 'Guardar borrador';

  /** En un flujo contenido, cancelar devuelve el control a la pantalla que montó la ficha. */
  @Input() cerrarAlCancelar = false;

  /** La página contenedora puede reservar las acciones al inspector contextual que esté abierto. */
  @Input() ocultarAcciones = false;

  /**
   * Entradas de composición para reutilizar esta ficha en
   * `/gestion/procesos/festivales/{id}`. En modo incrustado, la página contextual controla la
   * navegación y esta ficha conserva únicamente el formulario canónico del perfil.
   */

  // AQUI ESTABAN `pestanaForzada` Y `mostrarTablist`, y los cuatro montajes pasaban siempre los
  // mismos dos valores: «festival» y `false`. Eran dos entradas para apagar una segunda pestaña
  // que ya no existe. Retiradas junto con ella.

  /**
   * Sin fondo oscuro ni trampa de foco: la ficha vive dentro de una página, no flota sobre ella.
   *
   * `appDialogo` no se puede activar o desactivar por el valor de una entrada -se activa por su
   * sola presencia en la plantilla-, así que las dos formas de montar esta ficha comparten el MISMO
   * contenido (`#contenido` en la plantilla) y solo cambia el envoltorio. Sin este interruptor, una
   * ficha incrustada en una pestaña seguiría atrapando el tabulador dentro de sí misma y nadie
   * podría llegar con teclado a las otras cuatro pestañas de la ficha interna.
   */
  @Input() modoIncrustado = false;

  /**
   * Contexto de lectura para una misma ficha servida desde otra frontera, por ejemplo la
   * revisión institucional. Evita que el funcionario vea un encabezado de edición externa y
   * deja explícito qué parte del expediente está revisando.
   */
  /**
   * No pintar el encabezado propio de la ficha: lo pone quien la monta.
   *
   * Para la ficha incrustada en un panel que ya identifica el registro. Ver el comentario en la
   * plantilla, sobre el bloque del `<header>`.
   */
  @Input() ocultarEncabezado = false;

  @Input() tituloPersonalizado?: string;
  @Input() descripcionContexto?: string;

  /** Estado visual del autoguardado cuando la ficha la abre la revisión institucional. */
  @Input() estadoAutoguardado: 'pendiente' | 'guardando' | 'guardado' = 'guardado';
  @Input() pulsoAutoguardado = false;
  @Input() horaAutoguardado = '';

  /** La consola puede sustituir temporalmente la lectura por el resumen de una decisión. */
  @Input() mostrarResumenDeRevision = false;

  /**
   * La ficha se está mirando desde la consola institucional.
   *
   * <b>QUÉ CAMBIA, Y QUÉ NO.</b> No cambia la pantalla: las dos pestañas, los nueve pasos y los
   * cuarenta y siete campos son los mismos, que es lo que pidió el usuario
   * —«la idea es que la ficha tenga el mismo diseño que la que diligenció el usuario de la
   * organización»—. Lo que cambia es quién manda en el pie: se van «Editar» y «Guardar borrador»,
   * y entra lo que proyecte el panel institucional.
   *
   * <b>SALE DEL ALMACÉN Y NO DE UNA ENTRADA, Y ESO LO ARREGLÓ UNA PRUEBA.</b> Hasta el 29 de agosto
   * de 2026 esto era un `@Input() modoRevision`, es decir un SEGUNDO interruptor para lo mismo que
   * ya dice `RevisionDeCamposStore.modo`. Con los dos, quien montara la ficha podía poner uno y
   * olvidar el otro, y el resultado era una pantalla con «Pedir cambio» en cada campo Y un botón de
   * «Editar el Festival» al lado — que llama a un PUT que la consola no puede hacer. Un interruptor,
   * una verdad.
   */
  readonly enRevision = computed(() => this.revision?.esRevision() === true);

  /** Cerrar el diálogo. La X, el fondo, Escape y «Cerrar». */
  @Output() cerrar = new EventEmitter<void>();

  /** Se guardó. El panel recarga su lista, porque el nombre puede haber cambiado. */
  @Output() guardado = new EventEmitter<void>();

  /** El Festival quedó creado. Sale ANTES que `guardado` para que el panel no pierda el aviso. */
  @Output() creado = new EventEmitter<{ id: string; nombre: string }>();

  /**
   * Editando una propuesta de cambios en vez de la cabecera directa.
   *
   * PUBLICADO NO ES «NO SE PUEDE TOCAR», ES «SE TOCA POR OTRO CAMINO». Hasta el 1 de septiembre de
   * 2026 esto era un `@Output() proponerCambios` que el padre atendía navegando fuera del panel,
   * a `/registro?modo=festival` -una pantalla entera distinta, con otro diseño-. El usuario lo
   * señaló: «el botón Editar Festival no debe abrir una ruta paralela, antigua o independiente».
   * El circuito de propuestas (`dbo.PropuestasCambioFestival`) ya existía del lado del servidor;
   * lo que faltaba era la puerta DESDE ESTA FICHA, reutilizando exactamente el mismo `editando()`
   * que ya habilita los campos para Borrador/AjustesSolicitados -mismos campos, mismo pie, mismo
   * «Guardar borrador»-. Lo único que cambia es a qué endpoint va `guardar()` -ver más abajo-.
   */
  readonly editandoPropuesta = signal(false);

  private readonly api = inject(PanelOrganizacionApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /**
   * La revisión campo por campo, si el panel que monta esta ficha la provee.
   *
   * <b>OPCIONAL A PROPÓSITO.</b> La ficha existía antes que la revisión y tiene que seguir
   * abriéndose sin ella: el panel de la organización la provee en modo «atención» solo cuando hay
   * cambios pedidos, el institucional en modo «revisión», y una prueba que monte la ficha suelta no
   * la provee en absoluto. Que la revisión no esté no puede impedir leer la ficha.
   */
  private readonly revision = inject(RevisionDeCamposStore, { optional: true });

  readonly nivelesDeCobertura = NIVELES_DE_COBERTURA;

  readonly cargando = signal(true);
  readonly guardando = signal(false);

  /**
   * Los campos están habilitados.
   *
   * <b>NO CAMBIA LA PANTALLA, CAMBIA LOS CONTROLES.</b> «Quiero que mantengamos la misma vista para
   * editar la edición del festival y para la ficha; si le doy editar, me habilita los campos del
   * festival», del usuario. Las dos pestañas, las doce secciones y su orden
   * son los mismos con esta bandera en `true` o en `false`; lo único que cambia es si el valor se
   * lee o se escribe.
   */
  readonly editando = signal(false);



  /**
   * Los pasos desplegados. Puede haber varios, ninguno o todos.
   *
   * <b>NACE VACÍO.</b> «El estado natural son todos cerrados», del criterio el 29 de agosto
   * de 2026. La ficha abre enseñando los nueve encabezados —número, título y una línea de ayuda— y
   * nada más: con todo desplegado medía cuatro pantallas de desplazamiento en la ventana de
   * 1280×639 donde él la revisa.
   *
   * <b>CADA ENCABEZADO ABRE Y CIERRA.</b> «El hamburguesa también debe abrir con clic y retraerse
   * igual con clic», del mismo encargo. Por eso es un conjunto y no un solo identificador: abrir uno
   * no obliga a cerrar otro, y quien quiera comparar dos pasos puede tenerlos a la vez.
   *
   * <b>ES UNA SOLA LISTA PARA LAS DOS PESTAÑAS</b>, y con eso basta para que ir y volver no pierda
   * el sitio: los identificadores de los nueve pasos son distintos, así que lo que estaba abierto en
   * «Ediciones» sigue abierto al volver.
   *
   * <b>NO ES EL FORMULARIO DE DOCE PANTALLAS QUE SE RETIRÓ ESE MISMO DÍA.</b> No hay «Atrás» ni
   * «Siguiente», ningún paso bloquea al siguiente, «Guardar borrador» está siempre, y leer y editar
   * siguen siendo la misma pantalla.
   */
  readonly pasosAbiertos = signal<string[]>([]);

  readonly error = signal('');
  readonly mensaje = signal('');
  /** Los errores por campo, con la misma clave que usa el servidor en `payload.errors`. */
  readonly errores = signal<Record<string, string>>({});

  readonly catalogos = signal<CatalogosDelFestival>(CATALOGOS_VACIOS);
  readonly ubicaciones = signal<UbicacionDivipola[]>([]);

  /** La cabecera del Festival, tal como está en el panel o tal como se acaba de crear. */
  readonly cabecera = signal<FestivalDeLaOrganizacion | null>(null);

  /** Los dos modelos del formulario. Objetos y no señales, como en `seccion-organizacion`. */
  formularioGeneral: FormularioGeneral = generalVacio();

  /** No hay cabecera todavía: el diálogo está dando de alta un Festival. */
  readonly esAlta = computed(() => this.cabecera() === null);

  /** El estado del Festival al que pertenece esta ficha, en el vocabulario del front. */
  readonly estadoDeLaCabecera = computed(() => {
    const cabecera = this.cabecera();
    return cabecera ? estadoDelFestival(cabecera.estado) : null;
  });

  /** Publicado: no se edita, pero se puede proponer un cambio. */
  readonly sePuedeProponerCambios = computed(() => this.estadoDeLaCabecera() === 'Publicado');

  /** En revision: no se toca hasta que vuelva del Ministerio. Es una espera, no una prohibicion. */
  readonly estaEnRevision = computed(() => this.estadoDeLaCabecera() === 'EnRevision');



  // ─────────────────────────── Los dos bloques ───────────────────────────



  /**
   * La cabecera del Festival admite cambios.
   *
   * SON LOS DOS ESTADOS QUE ACEPTA EL PUT DEL FESTIVAL —`EsEditable`,
   * `FestivalesExternosEndpoints.cs-483`—; en cualquier otro contesta 409. En alta no hay
   * cabecera todavía, así que se da por editable: es lo que se está creando.
   */
  readonly cabeceraEditable = computed(() => {
    const cabecera = this.cabecera();
    return cabecera === null
      || ['Borrador', 'AjustesSolicitados'].includes(estadoDelFestival(cabecera.estado));
  });

  /**
   * <b>NO COMPRUEBA `enRevision()`, Y ESO LO DECIDIO UN MUTANTE.</b> El 29 de agosto de 2026 esta
   * linea empezaba por `!this.enRevision() &&`, y quitarlo NO ponia en rojo ninguna prueba: el pie
   * del dialogo ya corta antes con su propio `@if (enRevision())`, asi que la guarda de aqui no la
   * alcanzaba nadie. Dos cerraduras en serie sobre la misma puerta, y la de dentro sin llave que la
   * pruebe. La que queda es la del pie, que es donde se ve.
   */
  readonly puedeEditar = computed(() => this.esAlta() || this.cabeceraEditable());

  /**
   * El documento, para poder congelar la pagina de detras mientras el dialogo esta abierto.
   *
   * SIN ESTO, LA RUEDA DEL RATON DESPLAZA LO QUE HAY DEBAJO. El dialogo es `fixed inset-0` con su
   * propio `overflow-y-auto`, asi que al llegar al final de su contenido —o al pasar el cursor por
   * el fondo oscuro— la rueda seguia moviendo el panel de la organizacion: se cerraba la ficha y la
   * pagina estaba en otro sitio. Lo señalo criterio: «se debe mejorar la
   * navegacion de ese pop-up porque al hacer scroll se desplaza la pagina de atras».
   */
  private readonly documento = inject(DOCUMENT);

  /** Lo que `document.body.style.overflow` decia antes de abrir, para devolverlo tal cual. */
  private desplazamientoPrevio = '';

  /**
   * Congela el desplazamiento de la pagina de detras.
   *
   * SE GUARDA EL VALOR ANTERIOR Y NO SE ASUME `''`: el geovisor tambien congela el `body` cuando se
   * abre, y devolverlo a vacio al cerrar esta ficha lo descongelaria a el.
   */
  private congelarElFondo(): void {
    const cuerpo = this.documento.body;
    if (!cuerpo) return;
    this.desplazamientoPrevio = cuerpo.style.overflow;
    cuerpo.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    const cuerpo = this.documento.body;
    if (cuerpo) cuerpo.style.overflow = this.desplazamientoPrevio;
  }

  ngOnInit(): void {
    this.cabecera.set(this.festival);
    // SIN FONDO NEGRO, NADA QUE CONGELAR: `congelarElFondo()` bloquea el `overflow` de la página de
    // atrás, que en modo incrustado es la propia página que la contiene -congelarla dejaría a la
    // ficha interna entera sin poder desplazarse mientras esta pestaña está abierta-.
    if (!this.modoIncrustado) this.congelarElFondo();

    const catalogos$ = this.api.obtenerCatalogosDelFestival().pipe(catchError(() => of(CATALOGOS_VACIOS)));
    const ubicaciones$ = this.api.obtenerUbicaciones().pipe(catchError(() => of([] as UbicacionDivipola[])));

    // EN ALTA NO SE PIDEN VERSIONES, y no es una optimización: no hay identificador de Festival al
    // que pedírselas. Llamar con la cadena vacía armaría `/festivales//versiones`, que el servidor
    // contesta con un 404 sobre un formulario en blanco.
    if (!this.festival) {
      forkJoin({ catalogos: catalogos$, ubicaciones: ubicaciones$ }).subscribe({
        next: ({ catalogos, ubicaciones }) => {
          this.catalogos.set(catalogos ?? CATALOGOS_VACIOS);
          this.ubicaciones.set(ubicaciones ?? []);
          this.cargando.set(false);
          this.editando.set(true);
        },
        error: (fallo: FalloDelServidor) => {
          this.cargando.set(false);
          this.editando.set(true);
          this.error.set(fallo?.message ?? 'No fue posible cargar los catálogos del Festival');
        },
      });
      return;
    }

    this.sembrarGeneralDesdeLaCabecera(this.festival);
    // ESTE COMPONENTE MUESTRA EL FESTIVAL, Y SOLO EL FESTIVAL. Aquí había una segunda rama que
    // pedía `obtenerVersionesDelFestival()` y abría la primera; solo se entraba en ella cuando
    // `pestanaForzada` no era «festival», y ningún montaje del componente hacía eso. La Edición se
    // registra en su propia sección, contra `/externo/ediciones`.
    forkJoin({ catalogos: catalogos$, ubicaciones: ubicaciones$ }).subscribe({
      next: ({ catalogos, ubicaciones }) => {
        this.catalogos.set(catalogos ?? CATALOGOS_VACIOS);
        this.ubicaciones.set(ubicaciones ?? []);
        this.cargando.set(false);
        // UN FESTIVAL SIN NINGUNA EDICIÓN TAMBIÉN SE EDITA, y hasta no:
        // «Editar» abría la ficha en lectura y ahí se quedaba. Lo que se edita es la cabecera.
        if (this.abrirEnEdicion) { this.abrirEnEdicion = false; this.editarOProponerCambios(); }
      },
      error: (fallo: FalloDelServidor) => {
        this.cargando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible consultar la ficha del Festival');
      },
    });
  }

  // ─────────────────────────── Las tarjetas de las ediciones ───────────────────────────
  //
  // ANTES ERAN UN `<select>` DE UNA LÍNEA, y el usuario lo cambió: «me
  // gustaría que la edición sea más como un tipo tarjeta, con el nombre, algo básico, y un botón
  // abajo, editar… esa sería la forma de navegar». Con tres ediciones había que abrir el desplegable
  // para saber cuáles eran, y su rótulo no decía ni el nombre ni las fechas.







  // ─────────────────────────── Las dos pestañas ───────────────────────────


  /**
   * El cuerpo del paso está desplegado.
   *
   * <b>IGUAL AL LEER QUE AL EDITAR.</b> «El estado natural son todos cerrados», del usuario el 29
   * de agosto de 2026, sin distinguir modo. Los encabezados plegados son el índice de la ficha: se
   * ven los nueve títulos de un vistazo y se abre lo que interese, tanto para leer como para
   * rellenar.
   */
  estaAbierto(id: string): boolean {
    // En la página de Gestión, el perfil se lee y edita como un documento continuo.
    // Los tres bloques propios del Festival no deben convertirse allí en acordeones
    // independientes: la navegación lateral ya determina el contexto de la vista.
    if ((this.modoIncrustado || this.enRevision()) && ['generales', 'contacto-festival', 'musica-festival'].includes(id)) {
      return true;
    }
    return this.pasosAbiertos().includes(id);
  }

  /**
   * Despliega o pliega un paso.
   *
   * <b>EL MISMO MANDO PARA LAS DOS COSAS.</b> «El hamburguesa también debe abrir con clic y
   * retraerse igual con clic», del usuario. Hasta ese momento volver a
   * pulsar el abierto no lo cerraba, y no había forma de devolver la ficha a su estado plegado.
   *
   * ABRIR UNO NO CIERRA LOS DEMÁS: son alternadores independientes, no un acordeón exclusivo. Quien
   * necesite comparar «Contacto del Festival» con «Contacto de la edición» puede tener los dos.
   */
  alternarPaso(id: string): void {
    const paso = PASOS_DE_LA_FICHA.find(uno => uno.id === id);
    if (paso === undefined) return;
    const abiertos = this.pasosAbiertos();
    if (abiertos.includes(id)) {
      this.pasosAbiertos.set(abiertos.filter(uno => uno !== id));
      return;
    }
    this.pasosAbiertos.set([...abiertos, id]);
    this.enfocarEnElPaso(id);
  }

  irAPasoDeRevision(id: string): void {
    const destino = this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="paso-${id}"]`);
    destino?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    destino?.querySelector<HTMLElement>('h3')?.focus({ preventScroll: true });
  }

  /** Despliega un paso sin plegarlo si ya lo estaba. Lo usa el aviso de validación. */
  private desplegarPaso(id: string): void {
    if (this.estaAbierto(id)) return;
    this.pasosAbiertos.set([...this.pasosAbiertos(), id]);
  }


  /** El encabezado de un grupo de campos. La plantilla lo pinta al leer y al editar. */
  seccion(id: string): SeccionDeLaFicha {
    return seccion(id);
  }

  /**
   * El encabezado y la ayuda de un paso.
   *
   * SON NUEVE PASOS Y DOCE GRUPOS DE CAMPOS: tres pasos de la edición juntan dos grupos cada uno.
   * El reparto y el porqué están en `secciones-de-la-ficha.ts`, y lo pidió el usuario el 29 de
   * agosto de 2026: «agrupa esto en 3 pasos» sobre los datos básicos, «agrupa esto en 6 pasos, de
   * la forma más lógica» sobre la edición.
   */
  paso(id: string): PasoDeLaFicha {
    return PASOS_DE_LA_FICHA.find(uno => uno.id === id) ?? PASOS_DE_LA_FICHA[0];
  }

  /** El número del paso dentro de su pestaña, que es como se cuenta en pantalla. */
  numeroDelPaso(id: string): number {
    return numeroDelPaso(id);
  }

  /** Cuántos pasos tiene la ficha. Lo dice el nombre accesible de cada encabezado. */
  cuantosPasos(): number {
    return PASOS_DE_LA_FICHA.length;
  }



  // ─────────────────────────── Editar ───────────────────────────

  /**
   * Lo que dispara `?editar=1` al llegar: `editar()` para Borrador/AjustesSolicitados,
   * `proponerCambios()` para Publicado -son dos endpoints distintos, así que no puede ser el mismo
   * botón el que decida-.
   */
  private editarOProponerCambios(): void {
    if (this.sePuedeProponerCambios()) this.proponerCambios();
    else this.editar();
  }

  /**
   * Habilita los campos de la ficha que se está viendo.
   *
   * <b>NO CAMBIA DE PESTAÑA NI DE EDICIÓN.</b> «Si le doy editar, me habilita los campos del
   * festival, y así se puede tener una mejor experiencia de usuario», del usuario el 29 de agosto
   * de 2026. Quien estaba leyendo la edición 2 sigue en la edición 2, con sus campos abiertos; un
   * salto a los datos básicos obligaría a volver a buscar dónde se estaba.
   *
   * <b>SIN EDICIÓN ABIERTA TAMBIÉN EDITA</b>, y entonces lo que se edita es la cabecera. Es el caso
   * de un Festival recién creado, que no tiene ninguna: antes ese botón ni se pintaba.
   */
  editar(): void {
    if (!this.puedeEditar()) return;
    const cabecera = this.cabecera();
    if (cabecera) this.sembrarGeneralDesdeLaCabecera(cabecera);
    this.errores.set({});
    this.mensaje.set('');
    this.error.set('');
    this.editando.set(true);
    if (this.enfocarCampoInicial()) return;
    // NO DESPLIEGA NADA: «el estado natural son todos cerrados». El foco va al primer encabezado,
    // que es el mando que lo abre, para que quien navega con teclado siga con Enter.
    this.enfocar(`[data-testid="abrir-paso-${PASOS_DE_LA_FICHA[0].id}"]`);
  }


  cancelar(): void {
    // EN ALTA, CANCELAR CIERRA. No hay ficha que volver a leer ni cabecera a la que regresar: el
    // diálogo entero existe para crear algo que todavía no existe.
    if (this.esAlta()) {
      this.cerrar.emit();
      return;
    }
    // EN PROPUESTA, CANCELAR NO LA BORRA: solo deja de mostrar sus campos como editables. La
    // propuesta se queda como está guardada en el servidor -si nunca se guardó nada, sigue siendo
    // la copia de lo publicado con la que nació-; el foco vuelve al botón que la abrió.
    const veniaDeProponerCambios = this.editandoPropuesta();
    this.editando.set(false);
    this.editandoPropuesta.set(false);
    this.errores.set({});
    this.error.set('');
    const cabecera = this.cabecera();
    if (cabecera) this.sembrarGeneralDesdeLaCabecera(cabecera);
    if (this.cerrarAlCancelar) {
      this.cerrar.emit();
      return;
    }
    this.enfocar(veniaDeProponerCambios ? '[data-testid="ficha-proponer-cambios"]' : '[data-testid="ficha-editar"]');
  }

  private sembrarGeneralDesdeLaCabecera(cabecera: FestivalDeLaOrganizacion): void {
    this.formularioGeneral = {
      nombre: cabecera.nombre ?? '',
      descripcion: cabecera.descripcion ?? '',
      correoContacto: cabecera.correoContacto ?? '',
      telefonoCelular: cabecera.telefonoCelular ?? '',
      instagram: cabecera.instagram ?? '',
      facebook: cabecera.facebook ?? '',
      paginaWeb: cabecera.paginaWeb ?? '',
      otroEnlace: cabecera.otroEnlace ?? '',
      observacionesContacto: cabecera.observacionesContacto ?? '',
      periodicidad: cabecera.periodicidad ?? '',
      periodicidadDetalle: cabecera.periodicidadDetalle ?? '',
      nivelCobertura: cabecera.nivelCobertura || 'municipal',
      codigoDepartamento: cabecera.codigoDepartamento ?? '',
      codigoMunicipio: cabecera.codigoMunicipio ?? '',
      // LAS DOS LISTAS SALEN DE LA CABECERA, no de la edición abierta. Leerlas de la edición era lo
      // que borraba las del Festival al guardar una edición que no tenía ninguna.
      practicasMusicalesIds: (cabecera.practicasMusicales ?? []).map(item => item.id),
      territoriosSonorosIds: (cabecera.territoriosSonoros ?? []).map(item => item.id),
    };
  }

  /**
   * Igual que `sembrarGeneralDesdeLaCabecera()`, pero desde la propuesta activa: es lo que se
   * edita cuando el Festival está Publicado. La propuesta nace como copia de lo publicado -lo hace
   * el servidor al crearla-, así que la primera vez que se abre esto coincide con lo que ya se
   * veía; si ya había una a medio editar, esto recupera ese avance en vez de perderlo.
   */
  private sembrarGeneralDesdePropuesta(propuesta: PropuestaCambioFestival): void {
    this.formularioGeneral = {
      nombre: propuesta.nombre ?? '',
      descripcion: propuesta.descripcion ?? '',
      correoContacto: propuesta.correoContacto ?? '',
      telefonoCelular: propuesta.telefonoCelular ?? '',
      instagram: propuesta.instagram ?? '',
      facebook: propuesta.facebook ?? '',
      paginaWeb: propuesta.paginaWeb ?? '',
      otroEnlace: propuesta.otroEnlace ?? '',
      observacionesContacto: propuesta.observacionesContacto ?? '',
      periodicidad: propuesta.periodicidad ?? '',
      periodicidadDetalle: propuesta.periodicidadDetalle ?? '',
      nivelCobertura: propuesta.nivelCobertura || 'municipal',
      codigoDepartamento: propuesta.codigoDepartamento ?? '',
      codigoMunicipio: propuesta.codigoMunicipio ?? '',
      practicasMusicalesIds: (propuesta.practicasMusicales ?? []).map(item => item.id),
      territoriosSonorosIds: (propuesta.territoriosSonoros ?? []).map(item => item.id),
    };
  }

  /**
   * La puerta de entrada a editar un Festival Publicado: inicia -o recupera, si ya había una- su
   * propuesta de cambios activa y habilita los mismos campos que usa Borrador/AjustesSolicitados.
   *
   * `iniciarPropuesta()` es idempotente del lado del servidor: si ya hay una propuesta activa la
   * devuelve tal cual, así que llamar esto dos veces no crea una segunda. Después se pide
   * `obtenerPropuestaActiva()` -y no se siembra desde la respuesta del POST- porque
   * `iniciarPropuesta()` no trae los seis campos de contacto en su respuesta cuando la propuesta
   * ya existía; el GET siempre los trae completos.
   */
  proponerCambios(): void {
    if (!this.sePuedeProponerCambios()) return;
    const cabecera = this.cabecera();
    if (!cabecera) return;
    this.cargando.set(true);
    this.api.iniciarPropuesta(cabecera.id).pipe(
      switchMap(() => this.api.obtenerPropuestaActiva(cabecera.id)),
    ).subscribe({
      next: propuesta => {
        this.cargando.set(false);
        this.sembrarGeneralDesdePropuesta(propuesta);
        this.errores.set({});
        this.mensaje.set('');
        this.error.set('');
        this.editandoPropuesta.set(true);
        this.editando.set(true);
        if (this.enfocarCampoInicial()) return;
        this.enfocar(`[data-testid="abrir-paso-${PASOS_DE_LA_FICHA[0].id}"]`);
      },
      error: (fallo: FalloDelServidor) => {
        this.cargando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible iniciar la propuesta de cambios');
      },
    });
  }


  private aTexto(valor: number | null | undefined): string {
    return valor === null || valor === undefined ? '' : String(valor);
  }

  private aNumero(valor: string): number | null {
    const texto = (valor ?? '').trim();
    if (!texto) return null;
    const numero = Number(texto);
    return Number.isFinite(numero) ? numero : null;
  }

  private aTextoOpcional(valor: string): string | null {
    const texto = (valor ?? '').trim();
    return texto.length > 0 ? texto : null;
  }

  // ─────────────────────────── Las listas de casillas ───────────────────────────
  //
  // DOS PARES DE MÉTODOS Y NO UNO, porque son dos modelos: las cinco listas de la EDICIÓN escriben
  // en `formulario` y las dos del FESTIVAL en `formularioGeneral`. Un solo par que decidiera por el
  // nombre de la lista volvería a mezclar los dos lados, que es el defecto que se cerró el 29 de
  // agosto de 2026.



  estaSeleccionadoEnElFestival(lista: ListaDelFestival, id: number): boolean {
    return this.formularioGeneral[lista].includes(id);
  }

  alternarEnElFestival(lista: ListaDelFestival, id: number): void {
    const actual = this.formularioGeneral[lista];
    this.formularioGeneral[lista] = actual.includes(id)
      ? actual.filter(elegido => elegido !== id)
      : [...actual, id];
  }

  // ─────────────────────────── Las tres tablas ───────────────────────────








  // ─────────────────────────── DIVIPOLA ───────────────────────────

  /** Los departamentos, sin repetir, tal como los da DIVIPOLA. */
  departamentos(): { codigo: string; nombre: string }[] {
    const unicos = new Map(this.ubicaciones().map(fila => [fila.departmentCode, fila.departmentName]));
    return Array.from(unicos, ([codigo, nombre]) => ({ codigo, nombre }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  }


  /**
   * Los municipios de un departamento.
   *
   * ES UN MÉTODO Y NO UN `computed()`: depende del departamento elegido en el formulario, que es un
   * campo de un objeto y no una señal. Un `computed()` se calcularía una vez, con la lista vacía, y
   * se quedaría vacío para siempre.
   */
  municipiosDe(codigoDepartamento: string): UbicacionDivipola[] {
    if (!codigoDepartamento) return [];
    return this.ubicaciones().filter(fila => fila.departmentCode === codigoDepartamento);
  }

  /**
   * El alcance del Festival decide qué territorio se pide.
   *
   * LA REGLA LA IMPONE `CK_Festivales_NivelCobertura` y este formulario la respeta en los dos
   * sentidos: enseña u oculta los desplegables, y normaliza el cuerpo antes de enviarlo.
   *
   *   nacional / sin definir → departamento NULL     y municipio NULL
   *   departamental          → departamento NO NULL  y municipio NULL
   *   municipal              → departamento NO NULL  y municipio NO NULL
   */
  exigeDepartamento(): boolean {
    const nivel = this.formularioGeneral.nivelCobertura;
    return nivel === 'departamental' || nivel === 'municipal';
  }

  exigeMunicipio(): boolean {
    return this.formularioGeneral.nivelCobertura === 'municipal';
  }

  alCambiarNivelDeCobertura(): void {
    if (!this.exigeDepartamento()) {
      this.formularioGeneral.codigoDepartamento = '';
      this.formularioGeneral.codigoMunicipio = '';
      return;
    }
    if (!this.exigeMunicipio()) this.formularioGeneral.codigoMunicipio = '';
  }

  alCambiarDepartamentoGeneral(): void {
    this.formularioGeneral.codigoMunicipio = '';
  }

  municipiosGenerales(): UbicacionDivipola[] {
    return this.municipiosDe(this.formularioGeneral.codigoDepartamento);
  }

  // ─────────────────────────── Guardar ───────────────────────────

  /**
   * Lo que se comprueba antes de gastar una petición.
   *
   * SOLO «DATOS GENERALES» TIENE CAMPOS OBLIGATORIOS. El resto son coherencias que la base impone y
   * que el servidor volvería a comprobar: la fecha de fin no anterior a la de inicio, los municipios
   * completos, las aliadas con nombre y el material con URL. Nada de eso impide que el Festival
   * exista; impide que se mande una fila que la base va a rechazar.
   */
  private validar(): Record<string, string> {
    const errores: Record<string, string> = {};
    if (!this.formularioGeneral.nombre.trim()) {
      errores['nombre'] = 'Escribe el nombre del festival.';
    }
    if (this.exigeDepartamento() && !this.formularioGeneral.codigoDepartamento) {
      errores['codigoDepartamento'] = 'Elige un departamento.';
    }
    if (this.exigeMunicipio() && !this.formularioGeneral.codigoMunicipio) {
      errores['codigoMunicipio'] = 'Elige un municipio.';
    }

    // LAS COHERENCIAS DE LA EDICION SE COMPROBABAN AQUI —fechas, municipios, aliadas, material— y
    // se comprueban ahora donde se escriben, en el formulario de la Edición. Aquí no quedaba nada
    // que mirar: los campos que leían pertenecían a un formulario que ya no existe.
    return errores;
  }

  /**
   * En qué sección vive cada error.
   *
   * SIRVE PARA LLEVAR A QUIEN GUARDA HASTA DONDE FALTA ALGO. Con dos pestañas y una sola a la vista,
   * un «Revisa los campos marcados» sin cambiar de pestaña deja el aviso señalando a una pantalla
   * que no se está viendo. Y con el nombre de la sección, el aviso dice además dónde mirar dentro
   * de ella: la pestaña de la edición mide nueve secciones de largo.
   *
   * <b>GUARDA IDENTIFICADORES Y NO NÚMEROS, y ese cambio es.</b> Antes
   * decía `fechaFin: 1`, y reordenar las secciones dejaba el aviso apuntando a otra —sin que
   * ninguna prueba se pusiera en rojo, porque el salto ocurría igual—.
   */
  private readonly seccionDelError: Record<string, string> = {
    nombre: 'generales', codigoDepartamento: 'generales', codigoMunicipio: 'generales',
    correoContacto: 'contacto-festival', telefonoCelular: 'contacto-festival',
    instagram: 'contacto-festival', facebook: 'contacto-festival',
    paginaWeb: 'contacto-festival', otroEnlace: 'contacto-festival',
    practicasMusicalesIds: 'musica-festival', territoriosSonorosIds: 'musica-festival',
  };

  /**
   * Despliega la sección donde se corrige el primer error y devuelve el aviso que la nombra.
   *
   * DEVUELVE EL TEXTO EN VEZ DE ESCRIBIRLO para que los dos sitios que lo usan —la validación
   * propia y la del servidor— no tengan dos redacciones distintas del mismo aviso.
   *
   * YA NO CAMBIA DE PESTAÑA porque ya no hay dos: mientras las hubo, un «Revisa los campos
   * marcados» podía estar señalando a la que no se estaba viendo. Lo que sigue haciendo falta es
   * DESPLEGAR el paso: con las secciones plegadas por omisión, nombrar una sin abrirla deja el
   * aviso apuntando a algo que no se ve.
   */
  private avisoDelError(claves: string[]): string {
    const generico = 'Revisa los campos marcados.';
    const seccionId = this.seccionDelError[claves[0]];
    if (seccionId === undefined) return generico;
    const descrita = seccion(seccionId);
    const paso = pasoDeLaSeccion(seccionId);
    if (paso !== undefined) this.desplegarPaso(paso.id);
    return `Revisa los campos marcados en «${descrita.titulo}».`;
  }

  private construirSolicitudDelFestival(): GuardarFestivalSolicitud {
    const exigeDepartamento = this.exigeDepartamento();
    const exigeMunicipio = this.exigeMunicipio();
    return {
      nombre: this.formularioGeneral.nombre.trim(),
      descripcion: this.aTextoOpcional(this.formularioGeneral.descripcion),
      correoContacto: this.aTextoOpcional(this.formularioGeneral.correoContacto),
      telefonoCelular: this.aTextoOpcional(this.formularioGeneral.telefonoCelular),
      instagram: this.aTextoOpcional(this.formularioGeneral.instagram),
      facebook: this.aTextoOpcional(this.formularioGeneral.facebook),
      paginaWeb: this.aTextoOpcional(this.formularioGeneral.paginaWeb),
      otroEnlace: this.aTextoOpcional(this.formularioGeneral.otroEnlace),
      observacionesContacto: this.aTextoOpcional(this.formularioGeneral.observacionesContacto),
      periodicidad: this.aTextoOpcional(this.formularioGeneral.periodicidad),
      periodicidadDetalle: this.aTextoOpcional(this.formularioGeneral.periodicidadDetalle),
      nivelCobertura: this.formularioGeneral.nivelCobertura,
      // NULL Y NO CADENA VACÍA: para `CK_Festivales_NivelCobertura`, `''` no es NULL, y el guardado
      // muere con un 500 que no nombra ningún campo.
      codigoDepartamento: exigeDepartamento ? this.aTextoOpcional(this.formularioGeneral.codigoDepartamento) : null,
      codigoMunicipio: exigeMunicipio ? this.aTextoOpcional(this.formularioGeneral.codigoMunicipio) : null,
      // DEL MODELO DEL FESTIVAL Y NO DEL DE LA EDICIÓN. Hasta estas dos
      // líneas leían `this.formulario`: guardar una edición sin prácticas mandaba la lista vacía a
      // la CABECERA y le borraba las suyas, en la misma petición y sin decir nada.
      practicasMusicalesIds: [...this.formularioGeneral.practicasMusicalesIds],
      territoriosSonorosIds: [...this.formularioGeneral.territoriosSonorosIds],
    };
  }



  /**
   * Guarda el Festival: una fila y una petición.
   *
   * <b>ANTES ERAN DOS, Y LA SEGUNDA ERA DE UNA PANTALLA QUE NADIE PODIA ABRIR.</b> Este guardado
   * encadenaba `crearVersion`/`guardarVersion` detrás de la cabecera, alimentado por un formulario
   * de edición que vivía en una pestaña que ningún montaje del componente llegaba a mostrar. Como
   * ese formulario siempre estaba vacío, `hayDatosDeVersion()` era siempre falso y la segunda
   * petición no llegaba a salir nunca: quedaba el código, el mensaje «con su primera edición» que
   * nadie leía y una rama de error que hablaba de un fallo imposible. Retirado el 13 de septiembre
   * de 2026. Las ediciones se registran en su propia sección, contra `/externo/ediciones`.
   *
   * <b>TRES CAMINOS SEGUN QUIEN GUARDA Y EN QUE ESTADO.</b> Un Festival que todavía no existe se
   * crea; uno editable se actualiza; y uno publicado no se toca directamente —lo que se guarda
   * entonces es su propuesta de cambios, que es el circuito que existe justamente para que lo
   * publicado no cambie sin revisión—.
   */
  guardar(): void {
    if (this.guardando()) return;
    const errores = this.validar();
    this.errores.set(errores);
    if (Object.keys(errores).length > 0) {
      this.error.set(this.avisoDelError(Object.keys(errores)));
      // EL FOCO ACABA EN EL AVISO, que es lo que dice qué corregir.
      this.enfocar('[data-testid="ficha-error"]');
      return;
    }

    this.guardando.set(true);
    this.error.set('');
    this.mensaje.set('');

    const solicitudFestival = this.construirSolicitudDelFestival();
    const cabecera = this.cabecera();

    if (!cabecera) {
      this.api.crearFestival(this.organizacionId, solicitudFestival).subscribe({
        next: creado => {
          this.cabecera.set(creado);
          this.creado.emit({ id: creado.id, nombre: creado.nombre });
          this.alGuardarBien('El Festival quedó creado como borrador.');
        },
        error: (fallo: FalloDelServidor) => this.alFallarElGuardado(fallo),
      });
      return;
    }

    // EN PROPUESTA, `this.cabecera` NO CAMBIA: lo publicado sigue publicado hasta que el PNMC
    // aprueba la propuesta -eso lo hace otro circuito, no este guardado-. `map(() => cabecera)`
    // descarta la respuesta del PUT de la propuesta a propósito, para no pisar la cabecera leída
    // con datos que tienen otra forma (`PropuestaCambioFestival`, no `FestivalDeLaOrganizacion`).
    const editandoUnaPropuesta = this.editandoPropuesta();
    const guardarCabecera$ = editandoUnaPropuesta
      ? this.api.guardarPropuesta(cabecera.id, solicitudFestival).pipe(map(() => cabecera))
      : this.cabeceraEditable()
        ? this.api.guardarFestival(cabecera.id, solicitudFestival)
        : of(cabecera);

    guardarCabecera$.subscribe({
      next: actualizada => {
        this.cabecera.set(actualizada ?? cabecera);
        this.alGuardarBien(editandoUnaPropuesta
          ? 'La propuesta de cambios quedó guardada. Envíala a revisión cuando esté lista.'
          : 'Los datos del Festival quedaron guardados.');
      },
      error: (fallo: FalloDelServidor) => this.alFallarElGuardado(fallo),
    });
  }

  private alGuardarBien(mensaje: string): void {
    this.guardando.set(false);
    this.editando.set(false);
    this.editandoPropuesta.set(false);
    this.errores.set({});
    this.mensaje.set(mensaje);
    this.enfocar('[data-testid="ficha-editar"]');
    this.guardado.emit();
  }

  private alFallarElGuardado(fallo: FalloDelServidor, mensajeExtra?: string): void {
    this.guardando.set(false);
    const errores = this.erroresDelServidor(fallo);
    this.errores.set(errores);
    // LA PESTAÑA SE ABRE IGUAL QUE EN LA VALIDACIÓN PROPIA, pero el aviso es el del servidor: el
    // suyo dice qué pasó, y el nuestro solo diría dónde mirar.
    if (Object.keys(errores).length > 0) this.avisoDelError(Object.keys(errores));
    this.error.set(mensajeExtra ?? fallo?.message ?? 'No fue posible guardar el Festival');
    // EL BOTÓN DE GUARDAR SE DESHABILITA MIENTRAS LA PETICIÓN VUELA, y el navegador desenfoca todo
    // elemento que pasa a `disabled`: sin esto, tras un guardado fallido el foco queda en `<body>`,
    // fuera del diálogo, y hay que tabular desde el principio del documento para volver.
    this.enfocar('[data-testid="ficha-error"]');
  }

  /**
   * Traduce `payload.errors` a un mensaje por campo.
   *
   * EL DICCIONARIO LLEGA CON LISTAS. `Results.ValidationProblem` devuelve `{"nombre": ["…"]}`, y
   * pintar el array entero deja el mensaje entre corchetes en pantalla.
   */
  private erroresDelServidor(fallo: FalloDelServidor): Record<string, string> {
    const crudos = fallo?.payload?.errors ?? {};
    const salida: Record<string, string> = {};
    for (const [clave, valor] of Object.entries(crudos)) {
      salida[clave] = Array.isArray(valor) ? valor.join(' ') : String(valor);
    }
    return salida;
  }


  // ─────────────────────────── Pintar ───────────────────────────

  /** Lo que se lee cuando el dato no está: un texto y no un hueco en blanco. */
  texto(valor: string | null | undefined): string {
    const limpio = (valor ?? '').toString().trim();
    return limpio.length > 0 ? limpio : 'Sin registrar';
  }


  lista(elementos: CatalogoDelFestival[] | null | undefined): string {
    const nombres = (elementos ?? []).map(elemento => elemento.nombre).filter(Boolean);
    return nombres.length > 0 ? nombres.join(' · ') : 'Sin registrar';
  }

  /** El rótulo del alcance, no su código: `municipal` es lo que viaja, «Municipal» lo que se lee. */
  etiquetaCobertura(codigo: string | null | undefined): string {
    return NIVELES_DE_COBERTURA.find(nivel => nivel.codigo === (codigo ?? ''))?.etiqueta ?? 'Sin registrar';
  }


  /**
   * El territorio de la CABECERA se lee solo cuando su alcance lo pide.
   *
   * LA REGLA ES LA MISMA QUE VALIDA LA BASE —`CK_Festivales_NivelCobertura`— y no la del formulario:
   * `exigeDepartamento()` mira `formularioGeneral`, que en lectura no tiene nada escrito. Un
   * Festival nacional los tiene en NULL por restricción, y «Sin registrar» ahí se leería como un
   * dato que falta.
   */
  nivelPideDepartamento(nivel: string | null | undefined): boolean {
    return nivel === 'departamental' || nivel === 'municipal';
  }

  nivelPideMunicipio(nivel: string | null | undefined): boolean {
    return nivel === 'municipal';
  }

  /**
   * El nombre del departamento, no su código DIVIPOLA.
   *
   * SI DIVIPOLA NO CARGÓ, SE DEVUELVE EL CÓDIGO. Un «05» en pantalla es el identificador asomando y
   * se ve raro, que es lo que se quiere: mejor que un «Sin registrar» sobre un dato que sí está.
   */
  nombreDelDepartamento(codigo: string | null | undefined): string {
    if (!codigo) return 'Sin registrar';
    return this.departamentos().find(fila => fila.codigo === codigo)?.nombre ?? codigo;
  }

  nombreDelMunicipio(codigoDepartamento: string | null | undefined, codigoMunicipio: string | null | undefined): string {
    if (!codigoMunicipio) return 'Sin registrar';
    return this.municipiosDe(codigoDepartamento ?? '')
      .find(fila => fila.municipalityCode === codigoMunicipio)?.municipalityName ?? codigoMunicipio;
  }

  // ─────────────────────────── La revisión campo por campo ───────────────────────────

  /**
   * Los cambios pedidos que caen dentro de un paso.
   *
   * UN PASO AGRUPA VARIAS SECCIONES —«Organización» son «Organizador» y «Fuente de financiación»— y
   * las notas se guardan por sección, que es la unidad que el usuario nombró: «que sea por
   * secciones». Aquí se unen.
   */
  cambiosDelPaso(pasoId: string): ObservacionDeCampo[] {
    if (this.ocultarSugerenciasContextuales) return [];
    return this.revision?.notasDe(this.paso(pasoId).secciones) ?? [];
  }

  /** Los que quedan sin atender. Es el número del distintivo del encabezado. */
  cambiosPendientesEnElPaso(pasoId: string): number {
    if (this.ocultarSugerenciasContextuales) return 0;
    return this.revision?.cuantasPendientesEn(this.paso(pasoId).secciones) ?? 0;
  }

  /** La organización puede marcar: es su lista y todavía está abierta. */
  puedeAtenderCambios(): boolean {
    return this.revision?.esAtencion() === true && !this.revision.resolucionAutomatica();
  }

  // ───────────────────── El recuento de ajustes, arriba del todo ─────────────────────
  //
  // LO PIDIO EL USUARIO EL 30 DE AGOSTO DE 2026 señalando el cuerpo del diálogo: «aquí me gustaría
  // ver la cantidad de ajustes, y cuando tenga los ajustes totalmente concluidos, podemos decir
  // todos los ajustes hechos, de naranja a verde, sugerir ya guardar cambios».
  //
  // POR QUE ARRIBA Y NO EN EL PIE. Los distintivos por paso dicen dónde mirar, pero solo cuando el
  // paso está a la vista; con nueve pasos plegados y dos ediciones, no había ningún sitio que
  // dijera CUÁNTOS quedan en total. El pie del diálogo tiene ese número para el funcionario
  // —`revision-recuento`— y la organización no tenía ninguno.

  /** Cuántos campos señaló el equipo del PNMC en total. */
  cuantosAjustes(): number {
    return this.revision?.cuantasNotas() ?? 0;
  }

  /** Cuántos quedan sin marcar como corregidos. */
  ajustesPendientes(): number {
    return this.revision?.cuantasPendientes() ?? 0;
  }

  ajustesHechos(): number {
    return this.cuantosAjustes() - this.ajustesPendientes();
  }

  /**
   * Hay recuento que enseñar: es la organización atendiendo, y le pidieron algo.
   *
   * NO SE PINTA EN MODO REVISION. Ahí el número lo lleva el pie —«2 campos señalados · sin
   * guardar»— y significa otra cosa: cuántos lleva escritos quien redacta, no cuántos quedan por
   * corregir. Dos recuentos con el mismo aspecto y distinto sujeto en la misma pantalla se leen
   * como el mismo número.
   */
  atendiendoAjustes(): boolean {
    return this.puedeAtenderCambios() && this.cuantosAjustes() > 0;
  }

  /** De ámbar a verde: no queda ninguno sin marcar. */
  todosLosAjustesHechos(): boolean {
    return this.atendiendoAjustes() && this.ajustesPendientes() === 0;
  }


  /**
   * El fallo del circuito de notas, que hasta no se pintaba en ninguna
   * parte del panel de la organización.
   *
   * <b>Lo escribía `cargarCambiosPedidos()` en `revision.error` y solo lo leía la consola interna</b>
   * (`ficha-en-revision.component.ts`). Del lado de la organización, que el `GET` de los cambios
   * pedidos fallara se veía como una ficha sin una sola nota y un botón de reenvío apagado sin
   * motivo: los dos síntomas de «no te pidieron nada» y los dos falsos.
   */
  errorDeLaRevision(): string {
    return this.revision?.error() ?? '';
  }

  /** Los colores del recuento. El verde es `emerald`, el mismo del aviso de «quedó guardado». */
  claseDelRecuentoDeAjustes(): string {
    return this.todosLosAjustesHechos()
      ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
      : 'border-amber-200 bg-amber-50 text-amber-900';
  }

  atenderCambio(observacionId: number, casilla: HTMLInputElement): void {
    this.revision?.atender(observacionId, casilla);
  }

  // AQUÍ HUBO UN `etiquetaDeEdicion()` que armaba «Edición 2 — Borrador · vigente» para cada opción
  // del desplegable. Se fue con el propio desplegable: la tarjeta pinta el
  // número, el nombre, las fechas y el estado en renglones aparte, así que no hay nada que
  // concatenar. La palabra «edición» —y no «versión»— sigue medida, ahora sobre la tarjeta.

  /** El título del diálogo cambia con lo que se está haciendo. */
  titulo(): string {
    if (this.esAlta()) return 'Crear un Festival';
    return this.editando() ? 'Editar el Festival' : 'Ficha del Festival';
  }

  /**
   * Mueve el foco a un elemento del diálogo después de que Angular haya pintado.
   *
   * SIN ESTO EL FOCO SE CAE AL `<body>`. Entrar al formulario quita el botón «Editar el Festival»,
   * que es donde estaba el foco, y cambiar de pestaña quita el panel entero.
   *
   * ES `setTimeout` Y NO `queueMicrotask`: la microtarea corre antes de que Angular haya pintado el
   * bloque `@if`, así que el elemento al que se quiere ir todavía no existe.
   */
  /**
   * Lleva el foco al primer campo del paso, o a su encabezado si no tiene ninguno.
   *
   * NO SE HACE CON UN SELECTOR DE COMAS. `querySelector('input, …, h3')` devuelve el primero en
   * ORDEN DEL DOCUMENTO, y el encabezado va antes que los campos: ganaría siempre el encabezado y
   * el foco nunca caería en un campo. Por eso son dos consultas, en el orden que interesa.
   *
   * AL LEER NO HAY CAMPOS, así que cae al encabezado, que es lo correcto: no hay nada que escribir.
   */
  private enfocarEnElPaso(id: string): void {
    setTimeout(() => {
      const paso = this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="paso-${id}"]`);
      if (!paso) return;
      const destino = paso.querySelector<HTMLElement>('input, select, textarea')
        ?? paso.querySelector<HTMLElement>(`[data-testid="titular-paso-${id}"]`);
      if (!destino) return;
      destino.focus();
      destino.scrollIntoView?.({ block: 'nearest' });
    });
  }

  private enfocar(selector: string): void {
    setTimeout(() => {
      const destino = this.host.nativeElement.querySelector<HTMLElement>(selector);
      if (!destino) return;
      destino.focus();
      destino.scrollIntoView?.({ block: 'nearest' });
    });
  }

  /**
   * Lleva una observación directamente a su control. El identificador llega del contrato de
   * revisión y se compara como texto; no se interpola en un selector CSS.
   */
  private enfocarCampoInicial(): boolean {
    const campo = this.campoInicial?.trim();
    if (!campo) return false;
    const normalizado = campo.startsWith('festival.') ? campo.slice('festival.'.length) : campo;
    const selectores: Record<string, string> = {
      nombre: '#ficha-nombre-festival',
      descripcion: '#ficha-descripcion-festival',
      periodicidad: '#ficha-periodicidad',
      periodicidadDetalle: '#ficha-periodicidad-detalle',
      nivelCobertura: '#ficha-nivel-cobertura',
      codigoDepartamento: '#ficha-departamento',
      codigoMunicipio: '#ficha-municipio',
      correoContacto: '#ficha-correo-festival',
      telefonoCelular: '#ficha-celular',
      instagram: '#ficha-instagram-festival',
      facebook: '#ficha-facebook-festival',
      paginaWeb: '#ficha-pagina-web',
      otroEnlace: '#ficha-otro-enlace-festival',
      observacionesContacto: '#ficha-observaciones-festival',
      practicasMusicales: '[data-testid="casillas-festival-practicasMusicalesIds"] input',
      territoriosSonoros: '[data-testid="casillas-festival-territoriosSonorosIds"] input',
    };
    setTimeout(() => {
      const selector = selectores[normalizado];
      if (!selector) return;
      const control = this.host.nativeElement.querySelector<HTMLElement>(selector);
      if (!control) return;
      if (this.modoCampoContextual) {
        control.focus({ preventScroll: true });
      } else {
        control.scrollIntoView({ behavior: 'smooth', block: 'center' });
        control.focus();
      }
    });
    return true;
  }

  /** Decide qué control conserva el editor contextual sin duplicar el formulario canónico. */
  mostrarCampoContextual(campo: string): boolean {
    const visible = this.campoVisible?.trim();
    if (!visible) return true;
    const normalizar = (valor: string) => valor.startsWith('festival.') ? valor.slice('festival.'.length) : valor;
    return normalizar(visible) === normalizar(campo);
  }

  // ─────────────────────────── Cerrar ───────────────────────────

  /**
   * La X y «Cerrar». Cierran siempre, también con el formulario abierto: es el único mando que el
   * usuario pidió explícitamente —«recuerda ponerle una x para cerrar esa
   * ventana»— y un cierre que a veces no cierra es peor que perder el borrador.
   */
  cerrarDialogo(): void {
    this.cerrar.emit();
  }

  /**
   * Escape y el fondo. NO cierran mientras se edita.
   *
   * CON CUARENTA CAMPOS RELLENADOS, un clic fuera del diálogo perdería el trabajo entero sin
   * preguntar. En el alta que este diálogo sustituyó, ese mismo camino ya obligó a mirar el
   * `mousedown` y el `click` para que arrastrar una selección no cerrara el formulario; aquí hay
   * mucho más que perder, así que en edición sencillamente no cierran: quedan la X y «Cancelar».
   */
  cerrarSiNoSeEdita(): void {
    if (this.editando()) return;
    this.cerrar.emit();
  }

  private pulsacionNacidaEnElFondo = false;

  alPulsarElFondo(evento: MouseEvent): void {
    this.pulsacionNacidaEnElFondo = evento.target === evento.currentTarget;
  }

  alSoltarSobreElFondo(evento: MouseEvent): void {
    if (!this.pulsacionNacidaEnElFondo) return;
    this.pulsacionNacidaEnElFondo = false;
    if (evento.target !== evento.currentTarget) return;
    this.cerrarSiNoSeEdita();
  }
}
