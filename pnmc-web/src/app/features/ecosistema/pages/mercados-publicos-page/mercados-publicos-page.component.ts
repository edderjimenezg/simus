import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideSearch, LucideSlidersHorizontal, LucideX } from '@lucide/angular';

import { AccesoMapaEcosistemaComponent } from '../../../../shared/components/ui/acceso-mapa-ecosistema/acceso-mapa-ecosistema.component';
import {
  BarraExploracionEcosistemaComponent,
  VistaEcosistema,
} from '../../../../shared/components/ui/barra-exploracion-ecosistema/barra-exploracion-ecosistema.component';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { IconoDeEnlaceComponent, TipoDeEnlace } from '../../../../shared/components/ui/icono-de-enlace/icono-de-enlace.component';
import { enlaceExterno } from '../../../../shared/utils/enlace-externo';
import { MetricaEcosistema } from '../../../../shared/components/ui/franja-metricas-ecosistema/franja-metricas-ecosistema.component';
import { NavigationService } from '../../../../core/services/navigation.service';
import { NombrePropioPipe } from '../../../../shared/texto/nombre-propio.pipe';
import { ApiClientService } from '../../../../core/http/api-client.service';
import { MercadoPublico } from '../../../../core/services/mercados-publicos.service';
import { PrevisualizacionEnListadoService } from '../../../../core/services/previsualizacion-en-listado.service';

/** Cuántos caben en una página del directorio. El mismo número que usa el de Festivales. */
const POR_PAGINA = 12;

/**
 * El directorio público de Mercados Musicales, en /ecosistema/mercados-musicales.
 *
 * <b>EL MISMO DIRECTORIO QUE EL DE FESTIVALES, Y ESO ES TODO EL PUNTO.</b> Nació el 15 de
 * septiembre de 2026 con una línea de conteo, un buscador suelto y una tabla de tres columnas,
 * mientras el de Festivales tenía columna de filtros, franja de cifras, conmutador de vista,
 * paginación y una fila que enseña de qué va cada registro. Quedó definido así:
 * «vistas públicas de mercados como en festivales hicimos».
 *
 * <b>Y SE ARMA CON LAS PIEZAS COMPARTIDAS QUE YA EXISTIAN</b> —`app-barra-exploracion-ecosistema`,
 * `app-franja-metricas-ecosistema`, `app-acceso-mapa-ecosistema`, `app-icono-de-enlace`—, que se
 * hicieron para esto. Componerlas a mano en cada directorio es la forma segura de que el siguiente
 * proceso del Ecosistema se vea distinto.
 *
 * <b>SOLO LO PUBLICADO LLEGA AQUI.</b> Un borrador o algo en revisión es trabajo interno; el
 * servidor no lo entrega por esta ruta, y por eso esta pantalla no filtra por estado.
 *
 * <b>ES UN DIRECTORIO, NO UNA FICHA DE CATALOGACION.</b> Se enseña lo que una biblioteca publica de
 * su fondo: qué es, dónde ocurre, cada cuánto, quién responde y si se realiza dentro de un
 * festival. El trabajo de catalogar se queda en la consola.
 */
@Component({
  selector: 'app-mercados-publicos-page',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, NombrePropioPipe,
    CompactHeroComponent, BarraExploracionEcosistemaComponent, AccesoMapaEcosistemaComponent,
    IconoDeEnlaceComponent, LucideSearch, LucideSlidersHorizontal, LucideX,
  ],
  templateUrl: './mercados-publicos-page.component.html',
})
export class MercadosPublicosPageComponent implements OnInit {
  private readonly api = inject(ApiClientService);
  private readonly navegacion = inject(NavigationService);
  private readonly router = inject(Router);
  private readonly previsualizacion = inject(PrevisualizacionEnListadoService);
  private readonly ruta = inject(ActivatedRoute);

  readonly mercados = signal<MercadoPublico[]>([]);
  readonly cargando = signal(true);
  readonly error = signal('');

  // ─────────────────────────── Lo que acota la lista ───────────────────────────
  readonly busqueda = signal('');
  readonly departamento = signal('');
  readonly municipio = signal('');
  readonly practica = signal('');
  readonly territorioSonoro = signal('');
  readonly orden = signal<'nombre' | 'territorio' | 'ediciones'>('nombre');
  readonly vista = signal<VistaEcosistema>('listado');
  readonly pagina = signal(1);

  /** Los departamentos que de verdad tienen mercados publicados. Ofrecer los 33 sería mentir. */
  readonly departamentos = computed(() => [...new Set(
    this.mercados().map(m => m.nombreDepartamento).filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'es')));

  /**
   * Los municipios del departamento elegido, y no los de todo el país.
   *
   * SIN DEPARTAMENTO NO SE OFRECEN: una lista de todos los municipios con mercado no dice nada
   * sobre dónde buscar, y obliga a recorrerla entera para encontrar el de al lado.
   */
  readonly municipios = computed(() => {
    const departamento = this.departamento();
    if (!departamento) return [];
    return [...new Set(this.mercados()
      .filter(m => m.nombreDepartamento === departamento)
      .map(m => m.nombreMunicipio)
      .filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'es'));
  });

  readonly practicas = computed(() => [...new Set(
    this.mercados().flatMap(m => m.practicasMusicales.map(p => p.nombre)))].sort((a, b) => a.localeCompare(b, 'es')));

  readonly territoriosSonoros = computed(() => [...new Set(
    this.mercados().flatMap(m => m.territoriosSonoros.map(t => t.nombre)))].sort((a, b) => a.localeCompare(b, 'es')));

  readonly filtrosActivos = computed(() =>
    [this.departamento(), this.municipio(), this.practica(), this.territorioSonoro()].filter(Boolean).length);

  readonly hayFiltros = computed(() => this.filtrosActivos() > 0 || this.busqueda().trim().length > 0);

  /**
   * El filtro y el orden se resuelven en memoria, y aquí sí es correcto.
   *
   * <b>NO CONTRADICE LA REGLA DE ORDENAR SOBRE EL TOTAL:</b> esa existe porque una lista paginada
   * por el servidor y barajada en el cliente solo baraja la página cargada. Aquí el directorio trae
   * de una vez todo lo publicado —igual que el geovisor—, así que ordenar y filtrar en memoria
   * actúa sobre TODOS los registros, que es lo que la regla pide. La paginación es solo de pintado.
   */
  readonly filtrados = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    const departamento = this.departamento();
    const municipio = this.municipio();
    const practica = this.practica();
    const territorio = this.territorioSonoro();

    const lista = this.mercados().filter(m => {
      if (departamento && m.nombreDepartamento !== departamento) return false;
      if (municipio && m.nombreMunicipio !== municipio) return false;
      if (practica && !m.practicasMusicales.some(p => p.nombre === practica)) return false;
      if (territorio && !m.territoriosSonoros.some(t => t.nombre === territorio)) return false;
      if (!texto) return true;
      return [m.nombre, m.descripcion, m.nombreDepartamento, m.nombreMunicipio, m.organizacionNombre, m.festivalNombre]
        .some(valor => (valor ?? '').toLowerCase().includes(texto));
    });

    const orden = this.orden();
    return [...lista].sort((a, b) => {
      if (orden === 'ediciones') return b.numeroDeEdiciones - a.numeroDeEdiciones;
      if (orden === 'territorio') return this.donde(a).localeCompare(this.donde(b), 'es');
      return a.nombre.localeCompare(b.nombre, 'es');
    });
  });

  readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.filtrados().length / POR_PAGINA)));

  readonly visibles = computed(() => {
    const desde = (this.pagina() - 1) * POR_PAGINA;
    return this.filtrados().slice(desde, desde + POR_PAGINA);
  });

  readonly desdeMostrado = computed(() => (this.filtrados().length === 0 ? 0 : (this.pagina() - 1) * POR_PAGINA + 1));
  readonly hastaMostrado = computed(() => Math.min(this.pagina() * POR_PAGINA, this.filtrados().length));

  /** Las cuatro cifras de la franja. Las mismas cuatro preguntas que responde la de Festivales. */
  readonly metricas = computed<MetricaEcosistema[]>(() => {
    const lista = this.filtrados();
    const departamentos = new Set(lista.map(m => m.nombreDepartamento).filter(Boolean)).size;
    const municipios = new Set(lista.map(m => m.nombreMunicipio).filter(Boolean)).size;
    const practicas = new Set(lista.flatMap(m => m.practicasMusicales.map(p => p.nombre))).size;
    return [
      { label: 'Resultados', value: lista.length, detail: `de ${this.mercados().length} publicados` },
      { label: 'Departamentos', value: departamentos, detail: 'con Mercados en esta consulta' },
      { label: 'Municipios', value: municipios, detail: 'con Mercados en esta consulta' },
      { label: 'Prácticas', value: practicas, detail: 'vinculadas al directorio' },
    ];
  });

  ngOnInit(): void {
    // LOS FILTROS VIAJAN EN LA URL, y esa es la mitad que los hace servir de algo: la ficha de un
    // mercado enlaza «Amazonas» o una práctica, y ese enlace tiene que llegar aquí con el filtro
    // puesto. Sin esto, pinchar el territorio en una ficha abriría el directorio entero y quien lo
    // pinchó tendría que volver a elegirlo a mano.
    const parametros = this.ruta.snapshot.queryParamMap;
    this.departamento.set(parametros.get('departamento') ?? '');
    this.municipio.set(parametros.get('municipio') ?? '');
    this.practica.set(parametros.get('practica') ?? '');
    this.territorioSonoro.set(parametros.get('territorio') ?? '');

    this.api.get<{ items?: MercadoPublico[] }>('/api/v1/publico/mercados', {
      params: { limit: 500, offset: 0 },
      errorFallback: 'No fue posible cargar los mercados musicales',
    }).subscribe({
      next: respuesta => {
        const publicados = Array.isArray(respuesta?.items) ? respuesta.items : [];
        // COMO SE VERA ANTES DE PUBLICARLO. Si la consola pidió previsualizar un mercado, se inserta
        // aquí en su sitio, igual que hacen Festivales, Agenda y Noticias con su listado.
        this.previsualizacion.conListado<MercadoPublico, MercadoPublico>('mercados', publicados, dto => dto, (a, b) => a.id === b.id)
          .subscribe(lista => { this.mercados.set(lista); this.cargando.set(false); });
      },
      error: () => {
        this.error.set('No fue posible cargar los mercados musicales. Inténtalo de nuevo en un momento.');
        this.cargando.set(false);
      },
    });
  }

  // ─────────────────────────── Los gestos de la pantalla ───────────────────────────
  //
  // CADA CAMBIO VUELVE A LA PRIMERA PAGINA. Quedarse en la página 4 de una consulta que ahora tiene
  // dos resultados enseña una lista vacía sobre un directorio que sí tiene registros.

  actualizarBusqueda(valor: string): void { this.busqueda.set(valor); this.pagina.set(1); }

  actualizarDepartamento(valor: string): void {
    this.departamento.set(valor);
    // EL MUNICIPIO SE LIMPIA AL CAMBIAR DE DEPARTAMENTO: uno de Huila filtrando sobre Amazonas no
    // devuelve nada, y quien lo ve no sabe que el filtro de antes sigue puesto.
    this.municipio.set('');
    this.pagina.set(1);
  }

  actualizarMunicipio(valor: string): void { this.municipio.set(valor); this.pagina.set(1); }
  actualizarPractica(valor: string): void { this.practica.set(valor); this.pagina.set(1); }
  actualizarTerritorioSonoro(valor: string): void { this.territorioSonoro.set(valor); this.pagina.set(1); }
  actualizarOrden(valor: 'nombre' | 'territorio' | 'ediciones'): void { this.orden.set(valor); this.pagina.set(1); }
  cambiarVista(vista: VistaEcosistema): void { this.vista.set(vista); }

  limpiarFiltros(): void {
    this.busqueda.set('');
    this.departamento.set('');
    this.municipio.set('');
    this.practica.set('');
    this.territorioSonoro.set('');
    this.pagina.set(1);
  }

  irAPagina(numero: number): void {
    this.pagina.set(Math.min(Math.max(1, numero), this.totalPaginas()));
  }

  /** Lleva a los resultados: en móvil están debajo de la columna de filtros y fuera de la pantalla. */
  irAResultados(): void {
    document.getElementById('resultados-de-mercados')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  volver(): void {
    this.router.navigateByUrl('/ecosistema');
  }

  /** El paso al mapa, con la capa de Mercados activa. Es el mismo gesto que ofrece Festivales. */
  abrirMapa(): void {
    this.navegacion.navigateToMapLayer('Mercados Musicales', { targetView: 'map' });
  }

  /** Dónde ocurre, dicho en una línea y según su nivel de cobertura. */
  donde(mercado: MercadoPublico): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por definir';
  }

  /**
   * Los enlaces que el mercado publicó, ya saneados y listos para pinchar.
   *
   * SOLO LOS QUE DE VERDAD LLEVAN A ALGUN SITIO: un icono que no navega es peor que no tener el
   * icono. El saneado lo hace la pieza compartida, que descarta cualquier esquema que no sea
   * `http`/`https`.
   */
  enlaces(mercado: MercadoPublico): { clave: TipoDeEnlace; etiqueta: string; href: string }[] {
    const candidatos: readonly [TipoDeEnlace, string, string | null][] = [
      ['sitioWeb', 'Sitio web', mercado.sitioWebMercado],
      ['instagram', 'Instagram', mercado.instagramMercado],
      ['facebook', 'Facebook', mercado.facebookMercado],
      ['otroEnlace', 'Otro enlace', mercado.otroEnlaceMercado],
    ];
    const enlaces: { clave: TipoDeEnlace; etiqueta: string; href: string }[] = [];
    for (const [clave, etiqueta, valor] of candidatos) {
      const href = enlaceExterno(valor);
      if (href) enlaces.push({ clave, etiqueta, href });
    }
    return enlaces;
  }

  /** La línea bajo el nombre: quién responde, cada cuánto y con qué alcance. */
  resumen(mercado: MercadoPublico): string {
    return [mercado.organizacionNombre, mercado.periodicidad, mercado.alcance].filter(Boolean).join(' · ');
  }

  cuantasEdiciones(mercado: MercadoPublico): string {
    return mercado.numeroDeEdiciones === 1 ? '1 edición registrada' : `${mercado.numeroDeEdiciones} ediciones registradas`;
  }
}
