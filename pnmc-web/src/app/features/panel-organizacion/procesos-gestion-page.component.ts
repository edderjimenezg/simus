import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { FalloDelServidor, FestivalDeLaOrganizacion, MercadoDeLaOrganizacion, PanelOrganizacionApi } from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';

interface ProcesoProyectado {
  nombre: string;
  descripcion: string;
}

/**
 * Entrada para incorporar procesos al Ecosistema desde una organización.
 *
 * Antes del primer registro es una invitación de incorporación. Después pasa a
 * ser un inventario breve de los procesos propios, sin repetirlos como tarjetas
 * ni presentar módulos todavía inexistentes como disponibles.
 */
@Component({
  selector: 'app-procesos-gestion-page',
  standalone: true,
  imports: [RouterLink, NgTemplateOutlet, DatePipe],
  template: `
    <section class="space-y-7 pb-8" aria-labelledby="procesos-gestion-titulo">
      @if (cargando()) {
        <p role="status" class="py-8 text-sm text-slate-500">Consultando los procesos de la organización…</p>
      } @else if (error()) {
        <p role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{{ error() }}</p>
      } @else if (!tieneProcesos()) {
        <header class="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
          <p id="procesos-gestion-titulo" class="max-w-2xl text-sm leading-relaxed text-slate-600">Aún no hay procesos registrados. Elige el tipo que deseas incorporar al ecosistema.</p>
          <div class="relative shrink-0" data-selector-procesos>
            <button type="button" (click)="alternarOpciones()" [attr.aria-expanded]="opcionesAbiertas()" aria-haspopup="menu" class="inline-flex rounded-lg bg-morado px-4 py-2.5 text-sm font-bold text-white transition hover:bg-violet-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2">Registrar proceso</button>
            @if (opcionesAbiertas()) { <ng-container [ngTemplateOutlet]="selectorDeProcesos"></ng-container> }
          </div>
        </header>

        <section class="flex max-w-4xl flex-col gap-4 border-y border-slate-200 py-4 sm:flex-row sm:items-center sm:justify-between" aria-label="Tipo de proceso Festival">
          <div class="min-w-0"><h3 class="text-base font-bold text-slate-900">Festival</h3><p class="mt-1 text-sm text-slate-600">Registro y gestión de la información permanente del Festival, sus Ediciones y su publicación en el ecosistema.</p></div>
          <a routerLink="/gestion/procesos/festivales/nuevo" class="shrink-0 rounded-lg border border-morado px-4 py-2.5 text-center text-sm font-bold text-morado no-underline transition hover:bg-violet-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2">Registrar Festival</a>
        </section>

        <section class="flex max-w-4xl flex-col gap-4 border-b border-slate-200 py-4 sm:flex-row sm:items-center sm:justify-between" aria-label="Tipo de proceso Mercado musical">
          <div class="min-w-0"><h3 class="text-base font-bold text-slate-900">Mercado musical</h3><p class="mt-1 text-sm text-slate-600">Nodos de intercambio, visibilización y profesionalización. Puede ser independiente o realizarse en el marco de uno de tus Festivales.</p></div>
          <a routerLink="/gestion/procesos/mercados/nuevo" data-registrar-mercado class="shrink-0 rounded-lg border border-morado px-4 py-2.5 text-center text-sm font-bold text-morado no-underline transition hover:bg-violet-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2">Registrar Mercado</a>
        </section>
      } @else {
        <header class="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
          <p id="procesos-gestion-titulo" class="max-w-2xl text-sm leading-relaxed text-slate-500">{{ cuantosProcesos() }} {{ cuantosProcesos() === 1 ? 'proceso registrado' : 'procesos registrados' }}</p>
          <div class="relative self-start sm:self-auto" data-selector-procesos>
            <button type="button" (click)="alternarOpciones()" [attr.aria-expanded]="opcionesAbiertas()" aria-haspopup="menu" class="inline-flex rounded-lg bg-morado px-4 py-2.5 text-sm font-bold text-white transition hover:bg-violet-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2">Registrar proceso</button>
            @if (opcionesAbiertas()) { <ng-container [ngTemplateOutlet]="selectorDeProcesos"></ng-container> }
          </div>
        </header>

        <section aria-label="Procesos administrados" class="divide-y divide-slate-200 border-y border-slate-200">
            <!--
              LOS DOS TIPOS EN LA MISMA LISTA Y CON LA MISMA FILA. Separarlos en dos bloques haría
              parecer que son dos inventarios distintos; son procesos del mismo Ecosistema, y lo que
              los distingue es el rótulo de la izquierda.
            -->
            @for (mercado of mercados(); track mercado.id) {
              <a [routerLink]="['/gestion/procesos/mercados', mercado.id]" class="group grid gap-3 px-3 py-4 no-underline transition hover:bg-white focus-visible:relative focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado md:grid-cols-[minmax(12rem,.8fr)_minmax(16rem,1.35fr)_10rem_9rem] md:items-center md:gap-5 sm:px-4">
                <div class="min-w-0"><div class="flex flex-wrap items-center gap-x-2 gap-y-1"><span class="text-xs font-bold uppercase tracking-widest text-verde-texto">Mercado musical</span><span class="text-xs text-slate-400">{{ mercado.numeroDeEdiciones }} {{ mercado.numeroDeEdiciones === 1 ? 'edición' : 'ediciones' }}</span></div><p class="mt-1 truncate text-base font-bold text-slate-900">{{ mercado.nombre }}</p>@if (mercado.seRealizaEnElMarcoDeUnFestival && mercado.festivalNombre) { <p class="mt-1 truncate text-xs text-slate-500">En el marco de {{ mercado.festivalNombre }}</p> }</div>
                <p class="min-w-0 line-clamp-2 text-sm leading-relaxed text-slate-600">{{ mercado.descripcion || 'Sin descripción registrada.' }}</p>
                <p class="min-w-0 truncate text-sm text-slate-600">{{ territorioDelMercado(mercado) }}</p>
                <div class="min-w-0"><p class="text-sm font-bold text-slate-800">{{ etiquetaEstado(mercado.estadoRegistro) }}</p>@if (mercado.fechaActualizacion) { <p class="mt-1 text-xs text-slate-500">Actualizado {{ mercado.fechaActualizacion | date: 'd MMM y' }}</p> }</div>
              </a>
            }
            @for (festival of festivales(); track festival.id) {
              <a [routerLink]="['/gestion/procesos/festivales', festival.id]" class="group grid gap-3 px-3 py-4 no-underline transition hover:bg-white focus-visible:relative focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado md:grid-cols-[minmax(12rem,.8fr)_minmax(16rem,1.35fr)_10rem_9rem] md:items-center md:gap-5 sm:px-4">
                <div class="min-w-0"><div class="flex flex-wrap items-center gap-x-2 gap-y-1"><span class="text-xs font-bold uppercase tracking-widest text-verde-texto">Festival</span><span class="text-xs text-slate-400">{{ festival.periodicidad || 'Periodicidad sin registrar' }} · {{ festival.cuantasEdiciones || 0 }} {{ (festival.cuantasEdiciones || 0) === 1 ? 'edición' : 'ediciones' }}</span></div><p class="mt-1 truncate text-base font-bold text-slate-900">{{ festival.nombre }}</p>@if ((festival.practicasMusicales?.length || 0) + (festival.territoriosSonoros?.length || 0) > 0) { <p class="mt-1 truncate text-xs text-slate-500">{{ resumenCategorias(festival) }}</p> }</div>
                <p class="min-w-0 line-clamp-2 text-sm leading-relaxed text-slate-600">{{ festival.descripcion || 'Sin descripción registrada.' }}</p>
                <p class="min-w-0 truncate text-sm text-slate-600" [title]="territorioDe(festival)">{{ territorioDe(festival) }}</p>
                <div class="min-w-0"><p class="text-sm font-bold text-slate-800">{{ etiquetaEstado(festival.estado) }}</p>@if (festival.cambiosPedidos) { <p class="mt-1 text-xs font-bold text-amber-800">{{ festival.cambiosPedidos }} {{ festival.cambiosPedidos === 1 ? 'ajuste por atender' : 'ajustes por atender' }}</p> } @else if (festival.fechaActualizacion) { <p class="mt-1 text-xs text-slate-500">Actualizado {{ festival.fechaActualizacion | date: 'd MMM y' }}</p> }</div>
              </a>
            }
        </section>
      }
    </section>
    <ng-template #selectorDeProcesos>
      <div role="menu" class="absolute right-0 z-30 mt-3 w-[min(23rem,calc(100vw-2rem))] border border-slate-200 bg-white p-2 shadow-xl">
        <a role="menuitem" routerLink="/gestion/procesos/festivales/nuevo" (click)="opcionesAbiertas.set(false)" class="block border-l-2 border-verde-texto px-3 py-3 no-underline transition hover:bg-emerald-50"><span class="block text-sm font-bold text-morado">Festival</span><span class="mt-1 block text-xs leading-relaxed text-slate-500">Información permanente, Ediciones y presencia pública en el ecosistema.</span></a>
        <a role="menuitem" routerLink="/gestion/procesos/mercados/nuevo" (click)="opcionesAbiertas.set(false)" data-menu-registrar-mercado class="block border-l-2 border-verde-texto px-3 py-3 no-underline transition hover:bg-emerald-50"><span class="block text-sm font-bold text-morado">Mercado musical</span><span class="mt-1 block text-xs leading-relaxed text-slate-500">Independiente o dentro de uno de tus Festivales, con su territorio y su publicación.</span></a>
        @for (proceso of procesosProyectados; track proceso.nombre) {
          <button type="button" role="menuitem" disabled class="block w-full cursor-not-allowed px-3 py-2.5 text-left opacity-55">
            <span class="block text-sm font-semibold text-slate-700">{{ proceso.nombre }}</span>
            <span class="mt-0.5 block text-xs leading-relaxed text-slate-500">{{ proceso.descripcion }}</span>
          </button>
        }
      </div>
    </ng-template>
  `,
})
export class ProcesosGestionPageComponent implements OnInit {
  private readonly api = inject(PanelOrganizacionApi);
  private readonly store = inject(PanelOrganizacionStore);

  readonly festivales = signal<readonly FestivalDeLaOrganizacion[]>([]);
  readonly mercados = signal<readonly MercadoDeLaOrganizacion[]>([]);
  readonly cargando = signal(true);
  readonly error = signal('');
  readonly opcionesAbiertas = signal(false);
  readonly cuantosProcesos = computed(() => this.festivales().length + this.mercados().length);
  readonly tieneProcesos = computed(() => this.cuantosProcesos() > 0);
  readonly festivalesEnBorrador = computed(() => this.festivales().filter(festival => this.estadoNormalizado(festival.estado) === 'borrador').length);
  readonly festivalesConAjustes = computed(() => this.festivales().filter(festival => this.estadoNormalizado(festival.estado) === 'ajustessolicitados').length);
  readonly festivalesEnRevision = computed(() => this.festivales().filter(festival => this.estadoNormalizado(festival.estado) === 'enrevision').length);
  readonly festivalesPublicados = computed(() => this.festivales().filter(festival => this.estadoNormalizado(festival.estado) === 'publicado').length);
  readonly procesosProyectados: readonly ProcesoProyectado[] = [
    { nombre: 'Escuelas de música', descripcion: 'Procesos formativos y capacidades pedagógicas territoriales.' },
    { nombre: 'Redes de documentación', descripcion: 'Memoria, archivos, investigación y redes de conocimiento.' },
    { nombre: 'Lutería', descripcion: 'Saberes, construcción y reparación de instrumentos.' },
    { nombre: 'Escenarios', descripcion: 'Infraestructura y lugares para creación y circulación.' },
  ];

  ngOnInit(): void {
    const organizacionId = this.store.organizacionId();
    if (!organizacionId) {
      this.cargando.set(false);
      return;
    }
    // LOS DOS TIPOS SE PIDEN A LA VEZ. En serie, la lista aparecería primero a medias y volvería a
    // saltar al llegar la segunda respuesta; y si un tipo fallara, el otro se quedaría sin pintar.
    forkJoin({
      festivales: this.api.obtenerFestivales(organizacionId),
      mercados: this.api.obtenerMercados(organizacionId).pipe(catchError(() => of([] as MercadoDeLaOrganizacion[]))),
    }).subscribe({
      next: ({ festivales, mercados }) => {
        this.festivales.set(festivales);
        this.mercados.set(mercados);
        this.cargando.set(false);
      },
      error: (fallo: FalloDelServidor) => {
        this.error.set(fallo?.message ?? 'No fue posible consultar los procesos de la organización');
        this.cargando.set(false);
      },
    });
  }

  alternarOpciones(): void {
    this.opcionesAbiertas.update(abiertas => !abiertas);
  }

  @HostListener('document:keydown.escape')
  cerrarOpcionesConEscape(): void { this.opcionesAbiertas.set(false); }

  @HostListener('document:pointerdown', ['$event'])
  cerrarOpcionesAlSalir(evento: PointerEvent): void {
    const objetivo = evento.target as Element | null;
    if (!objetivo?.closest('[data-selector-procesos]')) this.opcionesAbiertas.set(false);
  }

  etiquetaEstado(estado: string | null | undefined): string {
    // EL NORMALIZADOR QUITA GUIONES Y ESPACIOS, así que `ajustes_solicitados` del mercado y
    // `AjustesSolicitados` del festival caen los dos en la misma clave. Una segunda tabla para el
    // mercado divergiría en el primer estado nuevo.
    const etiquetas: Record<string, string> = { borrador: 'Borrador', enrevision: 'En revisión', ajustessolicitados: 'Ajustes solicitados', publicado: 'Publicado', archivado: 'Archivado' };
    return etiquetas[this.estadoNormalizado(estado)] ?? 'Sin estado';
  }

  territorioDelMercado(mercado: MercadoDeLaOrganizacion): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por definir';
  }

  territorioDe(festival: FestivalDeLaOrganizacion): string {
    return [festival.nombreMunicipio, festival.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por definir';
  }

  resumenCategorias(festival: FestivalDeLaOrganizacion): string {
    const practicas = festival.practicasMusicales?.slice(0, 2).map(practica => practica.nombre).filter(Boolean) ?? [];
    const territorios = festival.territoriosSonoros?.slice(0, 2).map(territorio => territorio.nombre).filter(Boolean) ?? [];
    return [...practicas, ...territorios].join(' · ');
  }

  private estadoNormalizado(estado: string | null | undefined): string {
    return (estado ?? '').replace(/[_\s-]/g, '').toLowerCase();
  }
}
