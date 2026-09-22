import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ApiClientService } from '../../../../core/http/api-client.service';

/**
 * Lo mínimo que la ficha de un festival necesita de un mercado.
 *
 * NO SE REUTILIZA `Mercado` ENTERO a propósito: aquí solo se enseña el nombre, dónde ocurre y el
 * enlace. Traer el DTO completo ataría esta ficha a cambios del módulo de Mercados que no le
 * afectan.
 */
interface MercadoEnElMarco {
  id: number;
  nombre: string;
  nombreDepartamento: string | null;
  nombreMunicipio: string | null;
  nivelCobertura: string;
}
import { LucideArrowLeft, LucideCalendarDays, LucideGlobe, LucideLink, LucideMusic2, LucideUsersRound } from '@lucide/angular';
import { CatalogoFestivalPublico, EdicionFestivalPublica, FestivalPublico, FestivalesPublicosService } from '../../../../core/services/festivales-publicos.service';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { NombrePropioPipe } from '../../../../shared/texto/nombre-propio.pipe';
import { enlaceExterno } from '../../../../shared/utils/enlace-externo';
import { etiquetaDeCobertura } from '../../../../core/vocabularios/cobertura';
import { etiquetaDePeriodicidad } from '../../../../core/vocabularios/periodicidad';

export interface EnlacePublicado { clave: string; etiqueta: string; href: string; }
export interface DatoDeLaFicha {
  etiqueta: string;
  valor: string;
  /**
   * Con qué parámetro del listado se explora este dato, si se puede explorar.
   *
   * <b>EL CRITERIO ES COMPROBABLE Y NO UNA OPINION.</b> El §8 del plan pide volver navegables las
   * taxonomías «cuando tenga sentido», y avisa en la línea siguiente: «no convertir
   * indiscriminadamente todas las etiquetas en enlaces». Aquí «tiene sentido» significa una cosa
   * concreta: <b>que el listado de Festivales lleve ese filtro EN LA URL</b>, porque es lo que hace
   * que el enlace aterrice de verdad en la lista acotada. Eso deja fuera, a propósito:
   *
   * <ul>
   *   <li><b>Organización responsable</b>: no hay listado público de organizaciones al que llevar.</li>
   *   <li><b>Nivel de cobertura</b> y <b>Periodicidad</b>: el listado los filtra, pero no los
   *       escribe en la URL, así que un enlace llegaría a la lista sin filtrar y parecería roto.</li>
   * </ul>
   */
  filtro?: 'departamento' | 'municipio';
}


/** Ficha pública: perfil estable del Festival y sus realizaciones publicadas. */
@Component({
  selector: 'app-festival-publico-detalle-page',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, RouterLink, CompactHeroComponent, LucideArrowLeft, LucideCalendarDays, LucideGlobe, LucideLink, LucideMusic2, LucideUsersRound],
  templateUrl: './festival-publico-detalle-page.component.html',
})
export class FestivalPublicoDetallePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly festivalesPublicos = inject(FestivalesPublicosService);
  private readonly title = inject(Title);
  private readonly api = inject(ApiClientService);

  readonly festival = signal<FestivalPublico | null>(null);
  readonly cargando = signal(true);
  readonly ediciones = signal<EdicionFestivalPublica[]>([]);
  readonly edicionesCargadas = signal(false);
  readonly edicionesError = signal('');
  readonly edicionAbierta = signal<number | null>(null);
  readonly edicionMasReciente = computed(() => this.ediciones()[0] ?? null);
  readonly sinDato = 'Sin información publicada';

  /**
   * Los mercados musicales que se realizan en el marco de este festival.
   *
   * <b>ES LA RELACION VISTA DEL OTRO LADO, y es donde de verdad se ve para qué sirve:</b> quien abre
   * esta ficha descubre que dentro del festival ocurre un mercado sin haber tenido que saber que
   * ese mercado existe. Sin esto, la relación solo se puede recorrer en un sentido, que es la mitad
   * de una relación.
   *
   * <b>SI LA CONSULTA FALLA NO SE DICE QUE NO HAY NINGUNO.</b> Una lista vacía por un fallo no es
   * una respuesta: la ficha simplemente no enseña la sección, en vez de afirmar algo que no sabe.
   */
  readonly mercadosDelFestival = signal<MercadoEnElMarco[]>([]);

  ngOnInit(): void {
    const festivalId = this.route.snapshot.paramMap.get('festivalId') || '';
    this.festivalesPublicos.consultarFestival(festivalId).subscribe({
      next: festival => { this.festival.set(festival); this.title.setTitle(`${festival.nombre} | Ecosistema musical`); this.cargando.set(false); },
      error: () => this.cargando.set(false),
    });
    this.api.get<MercadoEnElMarco[]>(`/api/v1/publico/festivales/${festivalId}/mercados`, {
      errorFallback: 'No fue posible consultar los mercados de este festival',
    }).subscribe({
      next: mercados => this.mercadosDelFestival.set(mercados ?? []),
      error: () => this.mercadosDelFestival.set([]),
    });
    this.festivalesPublicos.consultarEdiciones(festivalId).subscribe({
      next: ediciones => { this.ediciones.set(ediciones); this.edicionesCargadas.set(true); this.edicionAbierta.set(ediciones[0]?.id ?? null); },
      error: error => { this.edicionesError.set(error?.message || 'No fue posible consultar las ediciones publicadas.'); this.edicionesCargadas.set(true); },
    });
  }

  /** Dónde ocurre el mercado, en una línea y según su nivel de cobertura. */
  territorioDelMercado(mercado: MercadoEnElMarco): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por confirmar';
  }

  volverADirectorio(): void { this.router.navigateByUrl('/ecosistema/festivales'); }
  alternarEdicion(id: number): void { this.edicionAbierta.update(abierta => abierta === id ? null : id); }
  territorio(festival: FestivalPublico): string { return [festival.territorioPrincipal.municipio, festival.territorioPrincipal.departamento].filter(Boolean).join(', ') || 'Territorio por confirmar'; }
  valor(bruto: string | null | undefined): string { return (bruto ?? '').trim() || this.sinDato; }
  nombres(valores: CatalogoFestivalPublico[]): string { return valores.length ? valores.map(valor => valor.nombre).join(', ') : this.sinDato; }
  datosGenerales(festival: FestivalPublico): DatoDeLaFicha[] {
    return [
      { etiqueta: 'Organización responsable', valor: this.valor(festival.organizacionResponsable) },
      { etiqueta: 'Departamento', valor: this.valor(festival.territorioPrincipal.departamento), filtro: 'departamento' },
      { etiqueta: 'Municipio', valor: this.valor(festival.territorioPrincipal.municipio), filtro: 'municipio' },
      { etiqueta: 'Nivel de cobertura', valor: this.valor(etiquetaDeCobertura(festival.territorioPrincipal.nivelCobertura)) },
      { etiqueta: 'Periodicidad', valor: this.valor(etiquetaDePeriodicidad(festival.periodicidad)) },
    ];
  }
  /**
   * Los parámetros con los que este dato abre el listado acotado.
   *
   * Vive aquí y no en la plantilla porque Angular no admite claves calculadas en un objeto
   * literal: `{ (dato.filtro): dato.valor }` no compila.
   */
  exploracionDe(dato: DatoDeLaFicha): Record<string, string> {
    return dato.filtro ? { [dato.filtro]: dato.valor } : {};
  }

  enlacesDelFestival(festival: FestivalPublico): EnlacePublicado[] {
    return ([['sitioWeb', 'Sitio web', festival.sitioWeb], ['instagram', 'Instagram', festival.instagram], ['facebook', 'Facebook', festival.facebook], ['otroEnlace', 'Otro enlace', festival.otroEnlace]] as const)
      .map(([clave, etiqueta, valor]) => ({ clave, etiqueta, href: enlaceExterno(valor) }))
      .filter(enlace => Boolean(enlace.href));
  }
  rangoDeFechas(edicion: EdicionFestivalPublica): string {
    const inicio = (edicion.fechaInicio ?? '').trim(); const fin = (edicion.fechaFin ?? '').trim();
    if (!inicio && !fin) return 'Fechas por confirmar';
    return inicio && fin ? `${this.fecha(inicio)} — ${this.fecha(fin)}` : this.fecha(inicio || fin);
  }
  fecha(bruto: string | null | undefined): string {
    const partes = /^(\d{4})-(\d{2})-(\d{2})/.exec((bruto ?? '').trim());
    if (!partes) return this.valor(bruto);
    const meses = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
    return `${Number(partes[3])} de ${meses[Number(partes[2]) - 1] ?? partes[2]} de ${partes[1]}`;
  }
}
