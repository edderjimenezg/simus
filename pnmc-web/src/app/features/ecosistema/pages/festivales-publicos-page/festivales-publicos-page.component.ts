import { CommonModule } from '@angular/common';
import { Component, OnInit, WritableSignal, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  enlazarFiltrosConLaUrl,
  filtroDePagina,
  filtroDeTexto,
  filtroDeVista,
} from '../../../../shared/utils/filtros-en-la-url';
import {
  LucideArrowLeft,
  LucideArrowRight,
  LucideBuilding2,
  LucideMusic2,
  LucideSearch,
  LucideSlidersHorizontal,
  LucideX,
} from '@lucide/angular';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { NavigationService } from '../../../../core/services/navigation.service';
import {
  FestivalPublico,
  FestivalesPublicosService,
  ResumenAnaliticoFestivales,
} from '../../../../core/services/festivales-publicos.service';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { MetricaEcosistema } from '../../../../shared/components/ui/franja-metricas-ecosistema/franja-metricas-ecosistema.component';
import { BarraExploracionEcosistemaComponent } from '../../../../shared/components/ui/barra-exploracion-ecosistema/barra-exploracion-ecosistema.component';
import { AccesoMapaEcosistemaComponent } from '../../../../shared/components/ui/acceso-mapa-ecosistema/acceso-mapa-ecosistema.component';
import { NombrePropioPipe } from '../../../../shared/texto/nombre-propio.pipe';
import { enlaceExterno } from '../../../../shared/utils/enlace-externo';
import { IconoDeEnlaceComponent } from '../../../../shared/components/ui/icono-de-enlace/icono-de-enlace.component';

/** Las dos formas de leer el directorio. El listado es la de arranque. */
export type VistaFestivales = 'listado' | 'cuadricula';

/** Criterios de orden. Todos salen de campos que el DTO publico ya trae. */
export type OrdenFestivales = 'nombre-asc' | 'nombre-desc' | 'territorio' | 'organizacion';

/**
 * Los órdenes que esta pantalla admite, en un solo sitio.
 *
 * ESTABA ESCRITO DOS VECES —una al leer la URL y otra al elegir del desplegable— y las dos listas
 * tenían que decir lo mismo sin que nada lo garantizara. Añadir un orden nuevo en una y no en la
 * otra da una pantalla donde se puede elegir un criterio que al recargar se pierde.
 */
export const ORDENES_ADMITIDOS: readonly OrdenFestivales[] = ['nombre-asc', 'nombre-desc', 'territorio', 'organizacion'];

/** Cuantas fichas trae cada pagina del directorio. */
export const TAMANO_PAGINA_FESTIVALES = 12;

/** Un enlace publicado por la organizacion, ya normalizado para el `href`. */
export interface EnlaceFestival {
  clave: 'sitioWeb' | 'instagram' | 'facebook' | 'otroEnlace';
  etiqueta: string;
  href: string;
}

/**
 * Clave de comparacion: sin tildes y en minusculas.
 *
 * Hace falta por dos motivos reales de estos datos, comprobados contra
 * `/api/v1/publico/festivales`: los municipios vienen con tilde («ABRIAQUÍ») y
 * quien busca escribe sin ella; y `periodicidad` guarda «anual» y «Anual» como
 * dos valores distintos, que en un desplegable son dos opciones para lo mismo.
 */
function comparable(valor: string): string {
  return valor.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('es');
}

/**
 * Valores distintos, sin vacios, ordenados en español.
 *
 * Colapsa por `clave` pero devuelve la PRIMERA grafia vista, para no inventar
 * una forma que no esta en la base.
 */
function unicos(valores: readonly (string | null | undefined)[]): string[] {
  const vistos = new Map<string, string>();
  for (const bruto of valores) {
    const valor = (bruto ?? '').trim();
    if (!valor) continue;
    const k = comparable(valor);
    if (!vistos.has(k)) vistos.set(k, valor);
  }
  return [...vistos.values()].sort((a, b) => a.localeCompare(b, 'es'));
}

/** Igualdad de filtro: la del desplegable contra la del registro. */
function igual(a: string | null | undefined, b: string): boolean {
  return comparable((a ?? '').trim()) === comparable(b.trim());
}

@Component({
  selector: 'app-festivales-publicos-page',
  standalone: true,
  imports: [IconoDeEnlaceComponent, NombrePropioPipe, 
    CommonModule,
    FormsModule,
    RouterLink,
    CompactHeroComponent,
    BarraExploracionEcosistemaComponent,
    AccesoMapaEcosistemaComponent,
    LucideArrowLeft,
    LucideArrowRight,
    LucideBuilding2,
    LucideMusic2,
    LucideSearch,
    LucideSlidersHorizontal,
    LucideX,
  ],
  templateUrl: './festivales-publicos-page.component.html',
})
export class FestivalesPublicosPageComponent implements OnInit {
  private readonly webTexts = inject(TextosWebService);
  private readonly festivalesPublicos = inject(FestivalesPublicosService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly navigation = inject(NavigationService);

  /** Texto editable de esta pagina. Ver el grupo `ecosistema_directorios` del registro. */
  texto(clave: string): string {
    return this.webTexts.getWebText(clave);
  }

  // --- Datos -------------------------------------------------------------------

  readonly festivales = signal<FestivalPublico[]>([]);
  readonly cargando = signal(true);
  readonly error = signal('');
  readonly resumen = signal<ResumenAnaliticoFestivales | null>(null);

  /**
   * Cuantos Festivales publicados dice el servidor que hay, contra los que
   * caben en una pagina de la consulta.
   *
   * Se guardan los dos porque la peticion pide `limit=500`: si algun dia el
   * directorio pasa de ahi, la pagina tiene que poder DECIRLO en vez de pintar
   * 500 y callarse. Es el mismo defecto que ya costo caro en el directorio de
   * escuelas —«30 resultados» y seis pintadas—, un tramo mas arriba.
   */
  readonly totalPublicado = signal(0);
  readonly consultaTruncada = computed(
    () => this.totalPublicado() > 0 && this.festivales().length < this.totalPublicado(),
  );

  // --- Filtros -----------------------------------------------------------------

  readonly busqueda = signal('');
  readonly departamento = signal('');
  readonly municipio = signal('');
  readonly practica = signal('');
  readonly territorioSonoro = signal('');
  readonly periodicidad = signal('');
  readonly cobertura = signal('');
  readonly organizacion = signal('');
  readonly soloConEnlaces = signal(false);

  // --- Presentacion ------------------------------------------------------------

  readonly vista = signal<VistaFestivales>('listado');
  readonly orden = signal<OrdenFestivales>('nombre-asc');
  readonly pagina = signal(1);

  // --- Opciones de los desplegables --------------------------------------------
  //
  // Salen de los registros cargados, no de un catalogo aparte. Asi el desplegable
  // no ofrece nunca una combinacion que devuelve cero, y el aviso del panel
  // lateral —«solo lo que tiene Festivales en esta consulta»— es cierto.

  readonly departamentos = computed(() =>
    unicos(this.festivales().map(f => f.territorioPrincipal.departamento)),
  );

  /** Los del departamento elegido; sin departamento, todos los que haya. */
  readonly municipios = computed(() => {
    const departamento = this.departamento();
    return unicos(
      this.festivales()
        .filter(f => !departamento || igual(f.territorioPrincipal.departamento, departamento))
        .map(f => f.territorioPrincipal.municipio),
    );
  });

  readonly practicas = computed(() =>
    unicos(this.festivales().flatMap(f => f.practicasMusicales.map(p => p.nombre))),
  );

  readonly territoriosSonoros = computed(() =>
    unicos(this.festivales().flatMap(f => f.territoriosSonoros.map(t => t.nombre))),
  );

  readonly periodicidades = computed(() => unicos(this.festivales().map(f => f.periodicidad)));

  readonly coberturas = computed(() =>
    unicos(this.festivales().map(f => f.territorioPrincipal.nivelCobertura)),
  );

  readonly organizaciones = computed(() =>
    unicos(this.festivales().map(f => f.organizacionResponsable)),
  );

  // --- Consulta ----------------------------------------------------------------

  readonly resultados = computed(() => {
    const termino = comparable(this.busqueda().trim());
    const departamento = this.departamento();
    const municipio = this.municipio();
    const practica = this.practica();
    const territorio = this.territorioSonoro();
    const periodicidad = this.periodicidad();
    const cobertura = this.cobertura();
    const organizacion = this.organizacion();
    const conEnlaces = this.soloConEnlaces();

    const filtrados = this.festivales().filter(festival => {
      if (departamento && !igual(festival.territorioPrincipal.departamento, departamento)) return false;
      if (municipio && !igual(festival.territorioPrincipal.municipio, municipio)) return false;
      if (practica && !festival.practicasMusicales.some(p => igual(p.nombre, practica))) return false;
      if (territorio && !festival.territoriosSonoros.some(t => igual(t.nombre, territorio))) return false;
      if (periodicidad && !igual(festival.periodicidad, periodicidad)) return false;
      if (cobertura && !igual(festival.territorioPrincipal.nivelCobertura, cobertura)) return false;
      if (organizacion && !igual(festival.organizacionResponsable, organizacion)) return false;
      if (conEnlaces && !this.tieneEnlaces(festival)) return false;
      if (!termino) return true;
      return comparable(this.textoBuscable(festival)).includes(termino);
    });

    return this.ordenar(filtrados);
  });

  /**
   * PAGINACIÓN CONVENCIONAL, EN EL CLIENTE.
   *
   * El servidor ya trae hasta 500 fichas en una sola llamada (`FestivalesPublicosService.
   * consultarFestivales`) y `GET /publico/festivales` no acepta ni busqueda ni filtros —solo
   * `limit`/`offset` sobre la lista completa, ya ordenada por nombre—: filtrar en el servidor por
   * departamento, práctica o territorio pediría un contrato nuevo. Con 500 fichas como techo
   * práctico, paginar EN EL CLIENTE sobre `resultados()` ya filtrado y ordenado es la solución
   * simple que pide el catálogo actual; `consultaTruncada()` sigue avisando el día que el
   * directorio real pase de 500 y esta cuenta deje de ser todo el universo.
   */
  readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.totalResultados() / TAMANO_PAGINA_FESTIVALES)));

  /** Nunca por fuera de rango: un filtro que deja menos resultados no puede dejar la página 4 mirando al vacío. */
  readonly paginaActual = computed(() => Math.min(Math.max(1, this.pagina()), this.totalPaginas()));

  readonly mostrados = computed(() => {
    const inicio = (this.paginaActual() - 1) * TAMANO_PAGINA_FESTIVALES;
    return this.resultados().slice(inicio, inicio + TAMANO_PAGINA_FESTIVALES);
  });

  readonly hayPaginaAnterior = computed(() => this.paginaActual() > 1);
  readonly hayPaginaSiguiente = computed(() => this.paginaActual() < this.totalPaginas());

  /** El rango que anuncia «Mostrando 13–24 de 51»: para saber cuántos hay y cuáles se están viendo. */
  readonly rangoMostrado = computed(() => {
    if (!this.totalResultados()) return { desde: 0, hasta: 0 };
    const desde = (this.paginaActual() - 1) * TAMANO_PAGINA_FESTIVALES + 1;
    return { desde, hasta: desde + this.mostrados().length - 1 };
  });

  // --- Contadores --------------------------------------------------------------
  //
  // Todos se calculan sobre `resultados()`, no sobre el total: los numeros de
  // arriba describen LA CONSULTA que el visitante tiene delante. Si describieran
  // el total, filtrar no los movería y dirían algo que no es.

  readonly totalResultados = computed(() => this.resultados().length);

  readonly totalDepartamentos = computed(
    () => unicos(this.resultados().map(f => f.territorioPrincipal.departamento)).length,
  );

  readonly totalMunicipios = computed(
    () => unicos(this.resultados().map(f => f.territorioPrincipal.municipio)).length,
  );

  readonly totalPracticas = computed(
    () => unicos(this.resultados().flatMap(f => f.practicasMusicales.map(p => p.nombre))).length,
  );

  readonly totalTerritoriosSonoros = computed(
    () => unicos(this.resultados().flatMap(f => f.territoriosSonoros.map(t => t.nombre))).length,
  );

  readonly totalOrganizaciones = computed(
    () => unicos(this.resultados().map(f => f.organizacionResponsable)).length,
  );

  /**
   * Cuantos Festivales del resultado publican al menos un enlace propio.
   *
   * SE LLAMABA `totalConContacto` Y CONTABA CORREOS. El 30 de agosto de 2026 la lectura publica
   * dejo de servir correo, telefono y director, asi que ese contador quedaba midiendo un dato que
   * ya no llega. Cuenta ahora la presencia publica, que es lo unico de contacto que existe aqui.
   */
  readonly totalConEnlaces = computed(
    () => this.resultados().filter(f => this.tieneEnlaces(f)).length,
  );

  /**
   * El universo contra el que se compara la consulta.
   *
   * Prefiere el `total` de la respuesta paginada sobre el del resumen analitico:
   * el primero lo emite la MISMA consulta que llena la lista, el segundo es otro
   * endpoint que puede haber contado en otro momento.
   */
  readonly universo = computed(
    () => this.totalPublicado() || this.resumen()?.totalFestivales || this.festivales().length,
  );

  /** Cuantos filtros hay puestos, sin contar el orden ni la vista. */
  readonly filtrosActivos = computed(() =>
    [
      this.busqueda().trim(),
      this.departamento(),
      this.municipio(),
      this.practica(),
      this.territorioSonoro(),
      this.periodicidad(),
      this.cobertura(),
      this.organizacion(),
      this.soloConEnlaces() ? 'enlaces' : '',
    ].filter(Boolean).length,
  );

  readonly hayFiltros = computed(() => this.filtrosActivos() > 0);

  /** Los mismos cuatro recuentos, ya en la forma que pide la franja de métricas. */
  readonly metricas = computed<MetricaEcosistema[]>(() => [
    { label: 'Resultados', value: this.totalResultados(), detail: `de ${this.universo()} publicados` },
    { label: 'Departamentos', value: this.totalDepartamentos(), detail: 'con Festivales en esta consulta' },
    { label: 'Municipios', value: this.totalMunicipios(), detail: 'con Festivales en esta consulta' },
    { label: 'Prácticas', value: this.totalPracticas(), detail: 'vinculadas al directorio' },
  ]);

  // --- Los filtros, declarados una vez ------------------------------------------

  /**
   * Los filtros de esta pantalla, con el vacío de cada uno.
   *
   * El orden se valida contra su lista blanca al leerlo: `?orden=loQueSea` llega de un enlace mal
   * copiado, y ordenar por un criterio que no existe dejaría la lista en un orden arbitrario sin
   * decirlo.
   */
  readonly filtros = [
    filtroDeTexto('q', this.busqueda),
    filtroDeTexto('departamento', this.departamento),
    filtroDeTexto('municipio', this.municipio),
    filtroDeTexto('practica', this.practica),
    filtroDeTexto('territorio', this.territorioSonoro),
    {
      parametro: 'orden',
      senal: this.orden as unknown as WritableSignal<string>,
      vacio: 'nombre-asc',
      desdeTexto: (texto: string) => (ORDENES_ADMITIDOS.includes(texto as OrdenFestivales) ? texto : 'nombre-asc'),
    },
    filtroDeVista('vista', this.vista, 'listado'),
    filtroDePagina(this.pagina),
  ];

  constructor() {
    enlazarFiltrosConLaUrl(this.filtros);
  }

  // --- Ciclo de vida -----------------------------------------------------------

  ngOnInit(): void {
    this.festivalesPublicos.consultarFestivales().subscribe({
      next: respuesta => {
        this.festivales.set(respuesta.items);
        this.totalPublicado.set(respuesta.total ?? respuesta.items.length);
        this.cargando.set(false);
      },
      error: error => {
        this.error.set(error?.message || 'No fue posible consultar los Festivales públicos.');
        this.cargando.set(false);
      },
    });
    this.festivalesPublicos.consultarResumenAnalitico().subscribe({
      next: resumen => this.resumen.set(resumen),
      error: () => undefined,
    });
  }

  // --- Acciones ----------------------------------------------------------------

  /**
   * Toda entrada de filtro pasa por aqui.
   *
   * Reiniciar la paginacion es obligatorio: sin esto, filtrar desde la pagina 3
   * dejaba la pagina en 3 y la consulta nueva podia no tener ni una tercera pagina, mostrando un
   * vacio que no era tal. Sincroniza tambien la URL: es lo que permite compartirla, recargar y
   * volver atras sin perder busqueda, filtros, orden ni pagina.
   */
  private reiniciarPaginacion(): void {
    this.pagina.set(1);
    // LA URL SE ESCRIBE SOLA desde `enlazarFiltrosConLaUrl`: cualquier cambio de cualquiera de las
    // señales declaradas la despierta. Llamarla a mano era el otro sitio donde se podía olvidar.
  }

  actualizarBusqueda(valor: string): void {
    this.busqueda.set(valor);
    this.reiniciarPaginacion();
  }

  /**
   * Cambiar de departamento invalida el municipio elegido salvo que siga
   * existiendo dentro del nuevo. Sin esto quedaba un par imposible
   * —ANTIOQUIA + BARRANQUILLA— que devuelve cero sin explicar por que.
   */
  actualizarDepartamento(valor: string): void {
    this.departamento.set(valor);
    const municipio = this.municipio();
    if (municipio && !this.municipios().some(item => igual(item, municipio))) {
      this.municipio.set('');
    }
    this.reiniciarPaginacion();
  }

  actualizarMunicipio(valor: string): void {
    this.municipio.set(valor);
    this.reiniciarPaginacion();
  }

  actualizarPractica(valor: string): void {
    this.practica.set(valor);
    this.reiniciarPaginacion();
  }

  actualizarTerritorioSonoro(valor: string): void {
    this.territorioSonoro.set(valor);
    this.reiniciarPaginacion();
  }

  // ─────────────────── Las etiquetas de una tarjeta, que ahora filtran ───────────────────
  //
  // §8 DEL PLAN: «clic en Territorio sonoro → acción contextual → listado filtrado». Las prácticas
  // y los territorios se pintaban unidos en una sola cadena —«Bambuco · Andina · Llanera»— y no
  // hacían nada, aunque el filtro que los usa estaba tres párrafos más arriba, en un desplegable.
  //
  // <b>QUE SI Y QUE NO, Y POR QUE.</b> El mismo §8 avisa: «no convertir indiscriminadamente todas
  // las etiquetas en enlaces». El criterio aquí es uno solo y se puede comprobar: <b>una etiqueta
  // se vuelve accionable si este listado tiene un filtro para ella</b>. Eso deja fuera, a
  // propósito:
  //
  //   · La ORGANIZACION RESPONSABLE. No hay filtro por organización en este listado, y tampoco un
  //     listado público de organizaciones al que llevar. Un enlace ahí prometería una pantalla que
  //     no existe, que es justo lo que las «acciones contextuales solo cuando aplican» evitan.
  //   · La PERIODICIDAD y el NIVEL DE COBERTURA. Sí tienen filtro, pero en la tarjeta no se leen
  //     como etiquetas: van dentro de una frase descriptiva —«Fundación X · anual · municipal»—.
  //     Volver accionable media frase la convierte en un campo de minas de clics.

  /**
   * Pone un filtro desde la etiqueta de una tarjeta, o lo quita si ya era ese.
   *
   * <b>ALTERNA, NO SOLO PONE.</b> Pulsar la etiqueta que ya está filtrando es el gesto natural de
   * «quítamelo»: si solo pusiera, la única forma de deshacerlo sería encontrar el desplegable de
   * arriba y volverlo a «Todos», y quien llegó aquí pulsando una etiqueta no tiene por qué saber
   * que ese desplegable existe.
   */
  alternarFiltroDeEtiqueta(campo: 'departamento' | 'municipio' | 'practica' | 'territorio', valor: string): void {
    const texto = (valor ?? '').trim();
    if (!texto) return;

    switch (campo) {
      case 'departamento':
        // EL DEPARTAMENTO ARRASTRA AL MUNICIPIO, y por eso pasa por su propio método: cambiarlo
        // deja sin sentido un municipio de otro departamento, y `actualizarDepartamento` ya lo
        // resuelve. Duplicar aquí esa regla es donde empiezan a divergir.
        this.actualizarDepartamento(this.departamento() === texto ? '' : texto);
        break;
      case 'municipio':
        this.actualizarMunicipio(this.municipio() === texto ? '' : texto);
        break;
      case 'practica':
        this.actualizarPractica(this.practica() === texto ? '' : texto);
        break;
      case 'territorio':
        this.actualizarTerritorioSonoro(this.territorioSonoro() === texto ? '' : texto);
        break;
    }
  }

  /** Si el listado ya está filtrado por esta etiqueta. Decide el estilo y el `aria-pressed`. */
  filtroActivo(campo: 'departamento' | 'municipio' | 'practica' | 'territorio', valor: string): boolean {
    const texto = (valor ?? '').trim();
    if (!texto) return false;
    switch (campo) {
      case 'departamento': return this.departamento() === texto;
      case 'municipio': return this.municipio() === texto;
      case 'practica': return this.practica() === texto;
      case 'territorio': return this.territorioSonoro() === texto;
    }
  }

  /**
   * Lo que se le lee a quien no ve la pantalla.
   *
   * «Bambuco» a secas no dice qué va a pasar al pulsarlo. Y el texto tiene que cambiar cuando el
   * filtro ya está puesto, porque entonces la acción es la contraria.
   */
  rotuloDeEtiqueta(campo: 'departamento' | 'municipio' | 'practica' | 'territorio', valor: string): string {
    const que = { departamento: 'el departamento', municipio: 'el municipio', practica: 'la práctica musical', territorio: 'el territorio sonoro' }[campo];
    return this.filtroActivo(campo, valor)
      ? `Quitar el filtro por ${que} ${valor}`
      : `Filtrar por ${que} ${valor}`;
  }

  actualizarPeriodicidad(valor: string): void {
    this.periodicidad.set(valor);
    this.reiniciarPaginacion();
  }

  actualizarCobertura(valor: string): void {
    this.cobertura.set(valor);
    this.reiniciarPaginacion();
  }

  actualizarOrganizacion(valor: string): void {
    this.organizacion.set(valor);
    this.reiniciarPaginacion();
  }

  alternarSoloConEnlaces(): void {
    this.soloConEnlaces.update(valor => !valor);
    this.reiniciarPaginacion();
  }

  actualizarOrden(valor: string): void {
    this.orden.set(ORDENES_ADMITIDOS.find(item => item === valor) ?? 'nombre-asc');
    this.reiniciarPaginacion();
  }

  /** No reinicia la página: cambiar de forma de lectura a media exploración no debería devolver a la primera. */
  cambiarVista(valor: VistaFestivales): void {
    this.vista.set(valor);
  }

  limpiarFiltros(): void {
    this.busqueda.set('');
    this.departamento.set('');
    this.municipio.set('');
    this.practica.set('');
    this.territorioSonoro.set('');
    this.periodicidad.set('');
    this.cobertura.set('');
    this.organizacion.set('');
    this.soloConEnlaces.set(false);
    this.reiniciarPaginacion();
  }

  irAPagina(numero: number): void {
    if (numero < 1 || numero > this.totalPaginas()) return;
    this.pagina.set(numero);
    this.irAResultados();
  }

  paginaAnterior(): void {
    this.irAPagina(this.paginaActual() - 1);
  }

  paginaSiguiente(): void {
    this.irAPagina(this.paginaActual() + 1);
  }

  /*
   * AQUI VIVIAN `hidratarDesdeUrl` Y `sincronizarUrl`, escritas a mano.
   *
   * Funcionaban, y eran las UNICAS de los cuatro listados publicos que lo hacian: Noticias, Agenda
   * y Catalogo Editorial guardaban sus filtros solo en señales, asi que una consulta filtrada no se
   * podia compartir, recargar la perdia y «Atras» no la deshacia. El §10 del plan pide que los
   * tres compartan «filtros/URL»; la forma de que lo compartan de verdad no es copiar estas dos
   * funciones tres veces mas, sino sacarlas a `shared/utils/filtros-en-la-url.ts` y que las cuatro
   * pantallas declaren sus filtros en vez de escribir el mecanismo.
   *
   * Lo unico que habia aqui y no estaba en la pieza comun era la lista blanca del orden, que sigue
   * viva: viaja como `desdeTexto` en la declaracion del filtro, arriba.
   */

  volverAlEcosistema(): void {
    this.router.navigateByUrl('/ecosistema');
  }

  /** El paso al mapa, con la capa de Festivales activa. */
  abrirMapa(): void {
    this.navigation.navigateToMapLayer('Festivales', { targetView: 'map' });
  }

  /**
   * Lo que hace el boton «Buscar».
   *
   * El filtrado ya es en vivo, asi que el boton no aplica nada: lleva a los
   * resultados. En movil el panel de filtros va ENCIMA de la lista y ocupa mas
   * de una pantalla, de modo que sin esto escribir en el buscador no mostraba
   * nada — el resultado quedaba fuera de la vista.
   */
  irAResultados(): void {
    if (typeof document === 'undefined') return;
    document.getElementById('festivales-resultados')?.scrollIntoView({ block: 'start' });
  }

  // --- Lectura de un registro --------------------------------------------------

  territorio(festival: FestivalPublico): string {
    return (
      [festival.territorioPrincipal.municipio, festival.territorioPrincipal.departamento]
        .filter(Boolean)
        .join(', ') || 'Territorio por confirmar'
    );
  }

  nombres(valores: { nombre: string }[]): string {
    return valores.map(item => item.nombre).join(', ');
  }

  /** Si el Festival publica algun enlace propio. No hay «contacto» publico mas alla de esto. */
  tieneEnlaces(festival: FestivalPublico): boolean {
    return Boolean(
      festival.sitioWeb || festival.instagram || festival.facebook || festival.otroEnlace,
    );
  }

  /**
   * Los enlaces publicados, ya listos para el `href`.
   *
   * Solo devuelve los que existen: una fila no pinta un boton «Sitio web» que
   * no lleva a ninguna parte. El esquema se comprueba en `enlaceExterno`.
   */
  enlaces(festival: FestivalPublico): EnlaceFestival[] {
    const candidatos: readonly [EnlaceFestival['clave'], string, string | null][] = [
      ['sitioWeb', 'Sitio web', festival.sitioWeb],
      ['instagram', 'Instagram', festival.instagram],
      ['facebook', 'Facebook', festival.facebook],
      ['otroEnlace', 'Otro enlace', festival.otroEnlace],
    ];
    const enlaces: EnlaceFestival[] = [];
    for (const [claveEnlace, etiqueta, valor] of candidatos) {
      const href = this.enlaceExterno(valor);
      if (href) enlaces.push({ clave: claveEnlace, etiqueta, href });
    }
    return enlaces;
  }

  /**
   * Normaliza un enlace publicado por la organizacion.
   *
   * Devuelve cadena vacia —y la plantilla entonces no pinta nada— cuando el
   * valor no es un enlace navegable. La lista blanca de esquemas NO es adorno:
   * el campo lo escribe una organizacion externa desde su ficha, y un
   * `javascript:` alli acabaria en un `href` del sitio publico.
   */
  /**
   * LA REGLA VIVE EN `shared/utils/enlace-externo.ts` desde, cuando el
   * directorio de Mercados la necesitó igual. Duplicar una comprobación de seguridad es la peor
   * forma de tenerla: el día que una se endurezca, la otra se queda como estaba.
   */
  enlaceExterno(valor: string | null | undefined): string {
    return enlaceExterno(valor);
  }

  // --- Internos ----------------------------------------------------------------

  /** Todo lo que el buscador libre mira, en una sola cadena. */
  private textoBuscable(festival: FestivalPublico): string {
    return [
      festival.nombre,
      festival.descripcion,
      festival.organizacionResponsable,
      festival.territorioPrincipal.departamento,
      festival.territorioPrincipal.municipio,
      festival.territorioPrincipal.nivelCobertura,
      festival.periodicidad,
      ...festival.practicasMusicales.map(item => item.nombre),
      ...festival.territoriosSonoros.map(item => item.nombre),
    ]
      .filter(Boolean)
      .join(' ');
  }

  private ordenar(festivales: FestivalPublico[]): FestivalPublico[] {
    const porNombre = (a: FestivalPublico, b: FestivalPublico) =>
      a.nombre.localeCompare(b.nombre, 'es');

    switch (this.orden()) {
      case 'nombre-desc':
        return [...festivales].sort((a, b) => porNombre(b, a));
      case 'territorio':
        return [...festivales].sort(
          (a, b) =>
            (a.territorioPrincipal.departamento ?? '').localeCompare(
              b.territorioPrincipal.departamento ?? '',
              'es',
            ) ||
            (a.territorioPrincipal.municipio ?? '').localeCompare(
              b.territorioPrincipal.municipio ?? '',
              'es',
            ) ||
            porNombre(a, b),
        );
      case 'organizacion':
        return [...festivales].sort(
          (a, b) =>
            (a.organizacionResponsable ?? '').localeCompare(b.organizacionResponsable ?? '', 'es') ||
            porNombre(a, b),
        );
      default:
        return [...festivales].sort(porNombre);
    }
  }
}
