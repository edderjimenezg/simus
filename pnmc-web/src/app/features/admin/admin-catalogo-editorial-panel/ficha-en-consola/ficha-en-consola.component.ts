import { etiquetaDeEstado } from '../../domain/admin-config';
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AnotacionEditorial, CatalogoEditorialService, ETIQUETAS_CATALOGACION, ETIQUETAS_PUBLICACION, PublicacionEditorial } from '../../../../core/services/catalogo-editorial.service';
import { aRecursoEditorialDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import { FichaEditorialComponent } from '../../../editorial/components/ficha-editorial/ficha-editorial.component';
import { IndicadorDeEstadoComponent } from '../../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../../domain/formatos-de-fecha';

/**
 * La ficha del Catálogo Editorial **en la consola**: la misma que ve el portal, y encima el trabajo.
 *
 * <b>POR QUE DOS VERSIONES Y NO UNA.</b> El criterio es este: «una versión visible de
 * este catálogo, y una versión un poco más avanzada con los comentarios, revisiones, anotaciones,
 * de cara al panel de gestión administrativa». Son dos lecturas distintas del mismo registro:
 * quien consulta quiere la obra; quien cataloga quiere además saber en qué estado está, quién la
 * validó, qué se dijo de ella y qué falta.
 *
 * <b>EL CUERPO CATALOGRAFICO ES EL MISMO COMPONENTE, Y ESO ES LO IMPORTANTE.</b> Se reutiliza
 * `FichaEditorialComponent` tal cual, así que quien cataloga ve EXACTAMENTE lo que va a ver el
 * portal —ni una tarjeta de más ni una de menos— y no tiene que imaginárselo. Una segunda versión
 * escrita a mano divergiría de la pública en el primer cambio, que es el defecto que este mismo
 * corte acaba de cerrar en el portal.
 *
 * <b>LO QUE SE AÑADE ENCIMA</b> es justo lo que el criterio de biblioteca dejó fuera de lo público:
 * los dos estados, la confianza de la ficha, las banderas de revisión, las notas de catalogación,
 * la procedencia, las fuentes con su verificador, los derechos con quién los comprobó, y el hilo de
 * decisiones y anotaciones.
 */
@Component({
  selector: 'app-ficha-en-consola',
  standalone: true,
  imports: [CommonModule, FormsModule, FichaEditorialComponent, IndicadorDeEstadoComponent],
  templateUrl: './ficha-en-consola.component.html',
})
export class FichaEnConsolaComponent {
  private readonly api = inject(CatalogoEditorialService);

  @Input({ required: true }) set publicacion(valor: PublicacionEditorial) {
    this.ficha.set(valor);
    void this.cargarHistorial();
  }

  @Output() cerrar = new EventEmitter<void>();
  /** Para que el panel recargue su lista cuando algo cambia desde aquí. */
  @Output() cambio = new EventEmitter<void>();

  readonly ficha = signal<PublicacionEditorial | null>(null);
  readonly historial = signal<AnotacionEditorial[]>([]);
  readonly cargandoHistorial = signal(false);
  readonly anotacion = signal('');
  readonly guardandoAnotacion = signal(false);
  readonly error = signal('');
  readonly aviso = signal('');

  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;
  readonly formatoDeFecha = FECHA_Y_HORA_ADMINISTRATIVA;
  readonly etiquetasCatalogacion = ETIQUETAS_CATALOGACION;
  readonly etiquetasPublicacion = ETIQUETAS_PUBLICACION;

  /*
    EL CODIGO NUNCA SALE A PANTALLA: el respaldo era `|| codigo`, que escribia «en_revision» tal
    cual en la ficha. El respaldo compartido lo pasa a lenguaje humano.
  */
  rotuloDeCatalogacion(codigo: string | null | undefined): string {
    return this.etiquetasCatalogacion[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }

  rotuloDePublicacion(codigo: string | null | undefined): string {
    return this.etiquetasPublicacion[(codigo ?? '').trim()] ?? etiquetaDeEstado(codigo);
  }

  /**
   * La ficha en la forma que consume la pantalla del portal.
   *
   * SE TRADUCE AQUI Y NO SE DUPLICA LA VISTA. El adaptador espera la forma PUBLICA, que es la que
   * recibe el portal; la consola tiene la interna, que la contiene entera. Pasar por el adaptador
   * garantiza que lo que se ve aquí es lo que se verá allí.
   */
  /**
   * Los términos normalizados, con su norma, para quien cataloga.
   *
   * Vive aquí y no en la ficha pública a propósito: el eje DCMI va en inglés porque es un
   * identificador de intercambio —«MovingImage», «InteractiveResource»— y citar «RDA 336» junto al
   * valor solo tiene sentido para quien tiene que sostener esa clasificación ante otro catálogo.
   */
  readonly normalizacionInterna = computed(() => {
    const f = this.ficha();
    if (!f) { return []; }
    const eje = (clave: string, norma: string) => ({
      rotulo: norma,
      valor: (f.tipologia ?? []).filter(x => x.eje === clave).map(x => x.etiqueta).join(' · '),
    });
    return [
      { rotulo: 'Fecha normalizada (EDTF)', valor: f.fechaEdtf ?? '' },
      { rotulo: 'Idioma (ISO 639)', valor: f.idioma ?? '' },
      eje('recurso', 'Tipo de recurso (DCMI Type Vocabulary)'),
      eje('contenido', 'Contenido (RDA 336)'),
      eje('medio', 'Medio (RDA 337)'),
      eje('soporte', 'Soporte (RDA 338)'),
    ].filter(fila => fila.valor);
  });

  readonly comoLoVeElPortal = computed(() => {
    const f = this.ficha();
    if (!f) { return null; }
    return aRecursoEditorialDeDiseno({
      codigo: f.codigo, titulo: f.titulo, subtitulo: f.subtitulo,
      designacionVolumen: f.designacionVolumen, serieOColeccion: f.serieOColeccion,
      resumen: f.resumen, fechaEdtf: f.fechaEdtf, anioInicio: f.anioInicio, anioFin: f.anioFin,
      notaFecha: f.notaFecha, idioma: f.idioma, tipoPublicacion: f.tipoPublicacion,
      formato: f.formato, tipologia: f.tipologia,
      tamanoFormato: f.tamanoFormato, paginas: f.paginas, duracion: f.duracion,
      categoria: f.categoria, categoriaSecundaria: f.categoriaSecundaria,
      practicaMusical: f.practicaMusical, subcategoria: f.subcategoria,
      palabrasClave: f.palabrasClave, ambito: f.ambito, ambitoTexto: f.ambitoTexto,
      seccionPrincipal: f.seccionPrincipal, rutaSeccion: f.rutaSeccion,
      miniaturaRuta: f.miniaturaRuta, textoPortada: f.textoPortada,
      creditos: (f.creditos ?? []).map(c => ({
        agenteId: c.agenteId, nombre: c.agenteNombre, tipo: c.agenteTipo,
        rolCodigo: c.rolCodigo, rol: c.rolEtiqueta, principal: c.principal,
      })),
      identificadores: (f.identificadores ?? []).map(i => ({
        esquema: i.esquema, codigo: i.codigoRecibido, cualificador: i.cualificador,
      })),
      accesos: (f.accesos ?? []).map(a => ({
        tipo: a.tipo, url: a.url, ubicacionFisica: a.ubicacionFisica,
        etiqueta: a.etiqueta, nota: a.nota,
      })),
      licencia: f.derechos?.licenciaONota ?? null,
      practicasMusicales: f.practicasMusicales ?? [],
      territoriosSonoros: f.territoriosSonoros ?? [],
      fechaActualizacion: f.fechaActualizacion,
    });
  });

  /**
   * Lo que le falta a esta ficha para poder verse en el portal.
   *
   * <b>SE DICE ANTES DE PULSAR, Y NO DESPUES.</b> La puerta pública exige cuatro cosas a la vez y
   * el servidor las comprueba al publicar; enterarse ahí es enterarse tarde. Aquí se enumera lo que
   * falta, que es lo que convierte «no se puede publicar» en una lista de tareas.
   */
  readonly loQueFalta = computed(() => {
    const f = this.ficha();
    if (!f) { return []; }
    const faltas: string[] = [];
    if (f.estadoCatalogacion !== 'validada') { faltas.push('La ficha todavía no está validada.'); }
    if ((f.fuentes ?? []).length === 0) { faltas.push('No cita ninguna fuente.'); }
    if (!f.derechos?.permitePublicarFicha) { faltas.push('Los derechos no permiten publicar la ficha.'); }
    return faltas;
  });

  readonly sePuedeVerEnElPortal = computed(() =>
    this.loQueFalta().length === 0 && this.ficha()?.estadoPublicacion === 'publicado');

  /** Lo que el portal NO enseña de esta ficha, y que es de lo que va esta pantalla. */
  administrativos(): { rotulo: string; valor: string }[] {
    const f = this.ficha();
    if (!f) { return []; }
    return [
      { rotulo: 'Confianza de la ficha', valor: f.confianza ?? '' },
      { rotulo: 'Revisar clasificación', valor: f.revisarClasificacion ? 'Sí' : '' },
      { rotulo: 'Revisar créditos', valor: f.revisarCreditos ? 'Sí' : '' },
      { rotulo: 'Notas de catalogación', valor: f.notasCatalogacion ?? '' },
      { rotulo: 'Lámina del catálogo original', valor: f.diapositivaOrigen ?? '' },
      { rotulo: 'Versión del registro', valor: String(f.version ?? '') },
    ].filter(d => d.valor.trim().length > 0);
  }

  derechos(): { rotulo: string; valor: string }[] {
    const d = this.ficha()?.derechos;
    if (!d) { return []; }
    return [
      { rotulo: 'Estado de los derechos', valor: d.estado ?? '' },
      { rotulo: 'Permite publicar la ficha', valor: d.permitePublicarFicha ? 'Sí' : 'No' },
      { rotulo: 'Permite publicar el archivo', valor: d.permitePublicarArchivo ? 'Sí' : 'No' },
      { rotulo: 'Licencia o nota', valor: d.licenciaONota ?? '' },
      { rotulo: 'Verificado por', valor: d.verificadoPor ?? '' },
    ].filter(x => x.valor.trim().length > 0);
  }

  /** Cómo se lee cada entrada del hilo. El verbo va en letra, no en clave. */
  tituloDeLaEntrada(entrada: AnotacionEditorial): string {
    if (entrada.accion === 'Anotacion') { return 'Anotación'; }
    if (entrada.accion === 'CatalogacionCambiada') {
      return `Catalogación · ${this.etiquetasCatalogacion[entrada.estadoNuevo] ?? entrada.estadoNuevo}`;
    }
    if (entrada.accion === 'PublicacionCambiada') {
      return `Publicación · ${this.etiquetasPublicacion[entrada.estadoNuevo] ?? entrada.estadoNuevo}`;
    }
    return entrada.accion;
  }

  esAnotacion(entrada: AnotacionEditorial): boolean { return entrada.accion === 'Anotacion'; }

  async cargarHistorial(): Promise<void> {
    const f = this.ficha();
    if (!f) { return; }
    this.cargandoHistorial.set(true);
    const resultado = await this.api.historial(f.id);
    this.cargandoHistorial.set(false);
    if (resultado.ok) { this.historial.set(resultado.data ?? []); }
    else { this.error.set(resultado.error ?? 'No fue posible consultar el historial.'); }
  }

  async guardarAnotacion(): Promise<void> {
    const f = this.ficha();
    const texto = this.anotacion().trim();
    if (!f || texto.length === 0) { return; }

    this.error.set(''); this.aviso.set('');
    this.guardandoAnotacion.set(true);
    const resultado = await this.api.anotar(f.id, texto);
    this.guardandoAnotacion.set(false);

    if (!resultado.ok) { this.error.set(resultado.error ?? 'No fue posible guardar la anotación.'); return; }
    this.anotacion.set('');
    this.aviso.set('La anotación quedó en el historial de la ficha.');
    await this.cargarHistorial();
  }
}
