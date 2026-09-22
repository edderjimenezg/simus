import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { LucideArrowLeft, LucideCalendarDays, LucideGlobe, LucideLink, LucideStore, LucideUsersRound } from '@lucide/angular';
import { EdicionDeMercadoPublica, MercadoPublico, MercadosPublicosService } from '../../../../core/services/mercados-publicos.service';
import { CatalogoDeMercado, ETIQUETAS_ESTADO_EDICION } from '../../../../core/services/mercados.service';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';
import { NombrePropioPipe } from '../../../../shared/texto/nombre-propio.pipe';
import { enlaceExterno } from '../../../../shared/utils/enlace-externo';
import { etiquetaDeCobertura } from '../../../../core/vocabularios/cobertura';
import { etiquetaDePeriodicidad } from '../../../../core/vocabularios/periodicidad';

export interface EnlaceDelMercado { clave: string; etiqueta: string; href: string; }
export interface DatoDeLaFichaDelMercado {
  etiqueta: string;
  valor: string;
  /**
   * Con qué filtro del directorio se explora este dato.
   *
   * <b>ES LO QUE CONVIERTE LA FICHA EN UNA PUERTA.</b> Leer «Amazonas» y no poder preguntar «¿qué
   * más hay en Amazonas?» deja a quien mira en un callejón: tiene que volver al directorio y
   * escribirlo a mano. Es el mismo gesto que ya ofrece la ficha de un Festival.
   */
  filtro?: 'departamento' | 'municipio';
}


/**
 * Ficha pública de un mercado musical: el perfil estable y sus ediciones publicadas.
 *
 * <b>ES LA PAREJA DE LA FICHA DEL FESTIVAL</b>, con las mismas partes en el mismo orden —datos
 * básicos, presencia pública, prácticas y territorios, propósito, ediciones— porque un módulo del
 * Ecosistema se conecta de principio a fin como Festivales. Hasta los
 * mercados tenían directorio y capa en el mapa, pero no una página propia.
 *
 * <b>LA RELACION SE RECORRE EN LOS DOS SENTIDOS.</b> Si el mercado se realiza en el marco de un
 * festival, aquí se enlaza la ficha del festival; y la ficha del festival enlaza esta.
 */
@Component({
  selector: 'app-mercado-publico-detalle-page',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, RouterLink, CompactHeroComponent, LucideArrowLeft, LucideCalendarDays, LucideGlobe, LucideLink, LucideStore, LucideUsersRound],
  templateUrl: './mercado-publico-detalle-page.component.html',
})
export class MercadoPublicoDetallePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly mercadosPublicos = inject(MercadosPublicosService);
  private readonly title = inject(Title);

  readonly mercado = signal<MercadoPublico | null>(null);
  readonly cargando = signal(true);
  readonly ediciones = signal<EdicionDeMercadoPublica[]>([]);
  readonly edicionesCargadas = signal(false);
  readonly edicionesError = signal('');
  readonly edicionAbierta = signal<number | null>(null);
  readonly sinDato = 'Sin información publicada';

  ngOnInit(): void {
    const mercadoId = this.route.snapshot.paramMap.get('mercadoId') || '';
    this.mercadosPublicos.consultarMercado(mercadoId).subscribe({
      next: mercado => { this.mercado.set(mercado); this.title.setTitle(`${mercado.nombre} | Ecosistema musical`); this.cargando.set(false); },
      error: () => this.cargando.set(false),
    });
    this.mercadosPublicos.consultarEdiciones(mercadoId).subscribe({
      next: ediciones => { this.ediciones.set(ediciones); this.edicionesCargadas.set(true); this.edicionAbierta.set(ediciones[0]?.id ?? null); },
      error: error => { this.edicionesError.set(error?.message || 'No fue posible consultar las ediciones publicadas.'); this.edicionesCargadas.set(true); },
    });
  }

  volverADirectorio(): void { this.router.navigateByUrl('/ecosistema/mercados-musicales'); }
  alternarEdicion(id: number): void { this.edicionAbierta.update(abierta => abierta === id ? null : id); }

  territorio(mercado: MercadoPublico): string {
    if (mercado.nivelCobertura === 'nacional') return 'Todo el país';
    return [mercado.nombreMunicipio, mercado.nombreDepartamento].filter(Boolean).join(', ') || 'Territorio por confirmar';
  }
  valor(bruto: string | null | undefined): string { return (bruto ?? '').trim() || this.sinDato; }
  nombres(valores: CatalogoDeMercado[]): string { return valores.length ? valores.map(valor => valor.nombre).join(', ') : this.sinDato; }

  datosGenerales(mercado: MercadoPublico): DatoDeLaFichaDelMercado[] {
    return [
      { etiqueta: 'Organización responsable', valor: this.valor(mercado.organizacionNombre) },
      { etiqueta: 'Departamento', valor: this.valor(mercado.nombreDepartamento), filtro: 'departamento' },
      { etiqueta: 'Municipio', valor: this.valor(mercado.nombreMunicipio), filtro: 'municipio' },
      { etiqueta: 'Nivel de cobertura', valor: this.valor(etiquetaDeCobertura(mercado.nivelCobertura)) },
      { etiqueta: 'Alcance', valor: this.valor(mercado.alcance) },
      { etiqueta: 'Modalidad', valor: this.valor(mercado.modalidad) },
      { etiqueta: 'Periodicidad', valor: this.valor(mercado.periodicidadDetalle || etiquetaDePeriodicidad(mercado.periodicidad)) },
      { etiqueta: 'Lugar', valor: this.valor(mercado.lugarEspecifico) },
    ];
  }

  /**
   * Los parámetros con los que este dato abre el directorio.
   *
   * SE CONSTRUYE AQUI Y NO EN LA PLANTILLA porque una clave calculada —`{ [dato.filtro]: … }`— no
   * es expresión válida de plantilla en Angular.
   */
  exploracionDe(dato: DatoDeLaFichaDelMercado): Record<string, string> {
    return dato.filtro && dato.valor !== this.sinDato ? { [dato.filtro]: dato.valor } : {};
  }

  enlacesDelMercado(mercado: MercadoPublico): EnlaceDelMercado[] {
    return ([['sitioWeb', 'Sitio web', mercado.sitioWebMercado], ['instagram', 'Instagram', mercado.instagramMercado], ['facebook', 'Facebook', mercado.facebookMercado], ['otroEnlace', 'Otro enlace', mercado.otroEnlaceMercado]] as const)
      .map(([clave, etiqueta, valor]) => ({ clave, etiqueta, href: enlaceExterno(valor) }))
      .filter(enlace => Boolean(enlace.href));
  }

  estadoDeLaEdicion(edicion: EdicionDeMercadoPublica): string {
    return ETIQUETAS_ESTADO_EDICION[edicion.estado] ?? edicion.estado;
  }
  nombreDeLaEdicion(edicion: EdicionDeMercadoPublica): string {
    return edicion.nombre || (edicion.numeroEdicion ? `Edición ${edicion.numeroEdicion}` : `Edición ${edicion.anio}`);
  }
  rangoDeFechas(edicion: EdicionDeMercadoPublica): string {
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
  donde(edicion: EdicionDeMercadoPublica): string {
    return [edicion.lugarEspecifico, edicion.nombreMunicipio, edicion.nombreDepartamento].filter(Boolean).join(', ') || 'Lugar por confirmar';
  }
}
