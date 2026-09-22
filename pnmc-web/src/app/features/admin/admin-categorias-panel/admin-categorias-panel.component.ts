import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { etiquetaDeEstado } from '../domain/admin-config';
import { LucideArrowUp, LucideArrowDown } from '@lucide/angular';
import { CommonModule } from '@angular/common';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
import { FormsModule } from '@angular/forms';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ProyectoTransversal, ProyectosTransversalesService } from '../../../core/services/proyectos-transversales.service';
import { DialogoDeFormularioComponent } from '../../../shared/components/ui/dialogo-de-formulario/dialogo-de-formulario.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import {
  CategoriaDeContenido,
  CategoriasDeContenidoService,
  ETIQUETAS_DE_MODULO,
  MODULOS_DE_CATEGORIA,
} from '../../../core/services/categorias-de-contenido.service';

/**
 * Las categorías temáticas de Agenda, Noticias y Catálogo Editorial, en un solo sitio.
 *
 * <b>POR QUE HAY UNA PANTALLA PARA ESTO.</b> Los tres módulos guardaban su categoría como texto
 * libre escrito a mano en cada ficha. Eso tiene dos consecuencias que solo se ven con el tiempo:
 * nadie puede corregir un nombre sin abrir una por una las fichas que lo usan, y el portal acaba
 * ofreciendo «Convocatorias» y «convocatoria» como dos filtros distintos que dividen el mismo
 * contenido en dos. Aquí la categoría es un registro: se renombra una vez y cambia en todas.
 *
 * <b>LO COMUN ES UNA DECISION, NO UN ACCIDENTE.</b> Una categoría del módulo «común» aparece en
 * los tres formularios. Es lo que permite que «Bandas» signifique lo mismo en una noticia, en un
 * evento y en una publicación —y que el portal pueda cruzarlos— sin crear tres categorías gemelas
 * que después nadie mantiene sincronizadas.
 *
 * <b>NO SE BORRA LO QUE ESTA EN USO.</b> El servidor lo impide y esta pantalla lo explica: borrar
 * dejaría fichas publicadas apuntando a nada. Para retirar una categoría hay que reasignar antes
 * el contenido que la usa.
 */

interface FormularioDeCategoria {
  id: number | null;
  codigoModulo: string;
  nombreCategoria: string;
  descripcion: string;
}

interface GrupoDeCategorias {
  codigo: string;
  etiqueta: string;
  categorias: CategoriaDeContenido[];
}

@Component({
  selector: 'app-admin-categorias-panel',
  standalone: true,
  imports: [
    DialogoDirective, BarraDeListaComponent,EstadoDeListaComponent, LucideArrowUp, LucideArrowDown, BotonComponent, SelectorSegmentadoComponent, IndicadorDeEstadoComponent, CommonModule, FormsModule, MenuDeAccionesComponent, DialogoDeFormularioComponent],
  templateUrl: './admin-categorias-panel.component.html',
})
export class AdminCategoriasPanelComponent {
  private readonly api = inject(CategoriasDeContenidoService);
  private readonly proyectosApi = inject(ProyectosTransversalesService);

  /**
   * Qué se está administrando: el vocabulario temático o las iniciativas del Programa.
   *
   * <b>VIVEN EN LA MISMA SECCION PORQUE SON LA MISMA CLASE DE COSA</b> —listas controladas que
   * alimentan los formularios de contenido— y separarlas en dos entradas de navegación obligaría a
   * recordar en cuál está cada una. <b>PERO SON DOS PESTAÑAS Y NO UNA LISTA</b>, porque responden
   * preguntas distintas: la categoría dice de qué trata un contenido, el proyecto a qué iniciativa
   * pertenece.
   */
  readonly vista = signal<'categorias' | 'proyectos'>('categorias');

  /** Las dos pestañas con su recuento, para el selector compartido. */
  readonly pestanas = computed<readonly OpcionSegmentada[]>(() => [
    { id: 'categorias', etiqueta: 'Categorías temáticas', conteo: this.total() },
    { id: 'proyectos', etiqueta: 'Proyectos del Programa', conteo: this.proyectos().length },
  ]);

  readonly proyectos = signal<ProyectoTransversal[]>([]);
  readonly formularioDeProyecto = signal<{ id: number | null; nombre: string; descripcion: string; activo: boolean } | null>(null);

  /** La categoría que se está fusionando y con cuál. */
  readonly fusionando = signal<CategoriaDeContenido | null>(null);
  readonly destinoDeFusion = signal<number | null>(null);

  readonly enabled = input(true);

  readonly categorias = signal<CategoriaDeContenido[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly formulario = signal<FormularioDeCategoria | null>(null);
  readonly confirmandoBorrado = signal<number | null>(null);

  readonly modulos = MODULOS_DE_CATEGORIA;
  readonly etiquetasDeModulo = ETIQUETAS_DE_MODULO;

  /**
   * Las categorías agrupadas por módulo, con «común» primero.
   *
   * COMUN VA ARRIBA A PROPOSITO: es el grupo que afecta a los tres formularios, así que quien
   * añade una categoría ve antes lo compartido que lo propio de un módulo, y tiene la ocasión de
   * reutilizar en vez de duplicar.
   */
  readonly grupos = computed<GrupoDeCategorias[]>(() => {
    const todas = this.categorias();
    const orden = ['comun', 'agenda', 'noticias', 'editorial'];
    return orden.map(codigo => ({
      codigo,
      etiqueta: ETIQUETAS_DE_MODULO[codigo] ?? etiquetaDeEstado(codigo),
      categorias: todas
        .filter(c => c.codigoModulo === codigo)
        .sort((a, b) => a.ordenVisualizacion - b.ordenVisualizacion || a.nombreCategoria.localeCompare(b.nombreCategoria, 'es')),
    }));
  });

  readonly total = computed(() => this.categorias().length);

  /**
   * Las categorías que pueden absorber a la que se está fusionando.
   *
   * SE OFRECEN TAMBIEN LAS DE OTROS MODULOS, y ese es el caso interesante: fusionar «Encuentros»
   * de Agenda dentro de «Encuentros» común es cómo se converge a un vocabulario compartido sin
   * perder lo ya publicado.
   */
  readonly destinosPosibles = computed(() => {
    const origen = this.fusionando();
    if (!origen) { return []; }
    return this.categorias()
      .filter(c => c.id !== origen.id)
      .sort((a, b) => a.codigoModulo.localeCompare(b.codigoModulo, 'es') || a.nombreCategoria.localeCompare(b.nombreCategoria, 'es'));
  });

  constructor() {
    effect(() => {
      const activo = this.enabled();
      if (!activo) { return; }
      untracked(() => { void this.cargar(); });
    });
  }

  verCategorias(): void { this.vista.set('categorias'); }

  async verProyectos(): Promise<void> {
    this.vista.set('proyectos');
    await this.cargarProyectos();
  }

  private async cargarProyectos(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    const resultado = await this.proyectosApi.listar();
    this.proyectos.set(resultado.ok && resultado.data ? resultado.data.items : []);
    if (!resultado.ok) { this.error.set(resultado.error ?? 'No fue posible consultar los proyectos.'); }
    this.cargando.set(false);
  }

  nuevoProyecto(): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formularioDeProyecto.set({ id: null, nombre: '', descripcion: '', activo: true });
  }

  editarProyecto(proyecto: ProyectoTransversal): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formularioDeProyecto.set({
      id: proyecto.id,
      nombre: proyecto.nombre,
      descripcion: proyecto.descripcion ?? '',
      activo: proyecto.activo,
    });
  }

  cerrarFormularioDeProyecto(): void { this.formularioDeProyecto.set(null); }

  campoDeProyecto<K extends 'nombre' | 'descripcion' | 'activo'>(clave: K, valor: string | boolean): void {
    const actual = this.formularioDeProyecto();
    if (!actual) { return; }
    this.formularioDeProyecto.set({ ...actual, [clave]: valor });
  }

  async guardarProyecto(): Promise<void> {
    const f = this.formularioDeProyecto();
    if (!f) { return; }

    const nombre = f.nombre.trim();
    if (!nombre) {
      this.error.set('Escribe el nombre del proyecto.');
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    try {
      const resultado = f.id === null
        ? await this.proyectosApi.crear({ nombre, descripcion: f.descripcion.trim() || null })
        : await this.proyectosApi.guardar(f.id, { nombre, descripcion: f.descripcion.trim() || null, activo: f.activo });

      if (resultado.ok) {
        this.aviso.set(f.id === null ? `«${nombre}» quedó disponible.` : `«${nombre}» quedó guardado.`);
        this.formularioDeProyecto.set(null);
        await this.cargarProyectos();
      } else {
        this.error.set(resultado.error ?? 'No fue posible guardar el proyecto.');
      }
    } finally {
      this.guardando.set(false);
    }
  }

  /**
   * Lo que se puede hacer hoy con esta categoría.
   *
   * <b>ERAN CINCO BOTONES IGUALES POR FILA</b> —↑, ↓, «Editar», «Fusionar» y «Retirar»— del mismo
   * tamaño y del mismo gris, con el irreversible indistinguible del resto. Es exactamente el caso
   * para el que se construyó `MenuDeAcciones`: una acción principal fuera y el resto dentro, con lo
   * que no tiene vuelta separado y en rojo.
   *
   * <b>REORDENAR NO ENTRA EN EL MENU</b>, y es deliberado: subir una categoría tres posiciones son
   * tres pulsaciones, y meterlas en un desplegable las convierte en seis. Las flechas se quedan
   * fuera, agrupadas aparte, porque son manipulación directa y no una acción sobre el registro.
   *
   * <b>«RETIRAR» SE OFRECE DESACTIVADA CUANDO LA USA ALGUIEN.</b> El servidor ya la rechaza y
   * explica por qué, pero enterarse después de pulsar es enterarse tarde: la fila ya trae la cifra
   * de contenidos que la usan, así que el motivo se puede decir antes.
   */
  accionesDe(categoria: CategoriaDeContenido): AccionDeRegistro[] {
    const enUso = categoria.contenidosQueLaUsan > 0;
    return [
      { id: 'editar', etiqueta: 'Editar', tono: 'principal', deshabilitada: this.guardando() },
      {
        id: 'fusionar',
        etiqueta: 'Fusionar con otra',
        deshabilitada: this.guardando() || this.categorias().length < 2,
        motivo: 'No hay otra categoría con la que fusionarla.',
      },
      {
        id: 'retirar',
        etiqueta: 'Retirar',
        tono: 'peligro',
        deshabilitada: this.guardando() || enUso,
        motivo: enUso
          ? `La usan ${categoria.contenidosQueLaUsan} ${categoria.contenidosQueLaUsan === 1 ? 'contenido' : 'contenidos'}: fusiónala con otra antes de retirarla.`
          : undefined,
      },
    ];
  }

  ejecutarAccion(categoria: CategoriaDeContenido, accion: string): void {
    switch (accion) {
      case 'editar': this.editar(categoria); break;
      case 'fusionar': this.pedirFusion(categoria); break;
      case 'retirar': this.pedirBorrado(categoria); break;
    }
  }

  pedirFusion(categoria: CategoriaDeContenido): void {
    this.aviso.set(null);
    this.error.set(null);
    this.fusionando.set(categoria);
    this.destinoDeFusion.set(null);
  }

  cancelarFusion(): void {
    this.fusionando.set(null);
    this.destinoDeFusion.set(null);
  }

  async fusionar(): Promise<void> {
    const origen = this.fusionando();
    const destino = this.destinoDeFusion();
    if (!origen || destino === null) { return; }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    try {
      const resultado = await this.api.fusionar(origen.id, destino);
      if (resultado.ok) {
        const nombreDestino = this.categorias().find(c => c.id === destino)?.nombreCategoria ?? 'la otra categoría';
        this.aviso.set(`«${origen.nombreCategoria}» se fusionó con «${nombreDestino}». Su contenido pasó entero.`);
        this.fusionando.set(null);
        this.destinoDeFusion.set(null);
        await this.cargar();
      } else {
        this.error.set(resultado.error ?? 'No fue posible fusionar la categoría.');
      }
    } finally {
      this.guardando.set(false);
    }
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);

    const resultado = await this.api.listar();
    if (resultado.ok && resultado.data) {
      this.categorias.set(resultado.data.items);
    } else {
      this.categorias.set([]);
      this.error.set(resultado.error ?? 'No fue posible consultar las categorías.');
    }

    this.cargando.set(false);
  }

  nueva(modulo: string): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({ id: null, codigoModulo: modulo, nombreCategoria: '', descripcion: '' });
  }

  editar(categoria: CategoriaDeContenido): void {
    this.aviso.set(null);
    this.error.set(null);
    this.formulario.set({
      id: categoria.id,
      codigoModulo: categoria.codigoModulo,
      nombreCategoria: categoria.nombreCategoria,
      descripcion: categoria.descripcion ?? '',
    });
  }

  cerrarFormulario(): void { this.formulario.set(null); }

  campo<K extends keyof FormularioDeCategoria>(clave: K, valor: FormularioDeCategoria[K]): void {
    const actual = this.formulario();
    if (!actual) { return; }
    this.formulario.set({ ...actual, [clave]: valor });
  }

  async guardar(): Promise<void> {
    const f = this.formulario();
    if (!f) { return; }

    const nombre = f.nombreCategoria.trim();
    if (!nombre) {
      this.error.set('Escribe el nombre de la categoría.');
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    const cuerpo = {
      codigoModulo: f.codigoModulo,
      nombreCategoria: nombre,
      descripcion: f.descripcion.trim() || null,
    };

    try {
      const resultado = f.id === null
        ? await this.api.crear(cuerpo)
        : await this.api.guardar(f.id, cuerpo);

      if (resultado.ok) {
        this.aviso.set(f.id === null ? `«${nombre}» quedó disponible.` : `«${nombre}» quedó guardada.`);
        this.formulario.set(null);
        await this.cargar();
      } else {
        this.error.set(resultado.error ?? 'No fue posible guardar la categoría.');
      }
    } finally {
      this.guardando.set(false);
    }
  }

  /**
   * Sube o baja una categoría dentro de su grupo.
   *
   * EL ORDEN NO ES COSMETICO: es el que ven los tres formularios y el aside de filtros del portal.
   * Se intercambia con la vecina en vez de renumerar el grupo entero para que el cambio afecte a
   * dos registros y no a todos, y para que un fallo a mitad no deje el grupo sin orden.
   */
  async mover(categoria: CategoriaDeContenido, direccion: -1 | 1): Promise<void> {
    const grupo = this.grupos().find(g => g.codigo === categoria.codigoModulo);
    if (!grupo) { return; }

    const posicion = grupo.categorias.findIndex(c => c.id === categoria.id);
    const vecina = grupo.categorias[posicion + direccion];
    if (!vecina) { return; }

    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    const comunes = (c: CategoriaDeContenido) => ({
      codigoModulo: c.codigoModulo,
      nombreCategoria: c.nombreCategoria,
      descripcion: c.descripcion,
    });

    try {
      const primera = await this.api.guardar(categoria.id, { ...comunes(categoria), ordenVisualizacion: vecina.ordenVisualizacion });
      if (!primera.ok) {
        this.error.set(primera.error ?? 'No fue posible reordenar.');
        return;
      }
      const segunda = await this.api.guardar(vecina.id, { ...comunes(vecina), ordenVisualizacion: categoria.ordenVisualizacion });
      if (!segunda.ok) {
        this.error.set(segunda.error ?? 'No fue posible reordenar.');
      }
      await this.cargar();
    } finally {
      this.guardando.set(false);
    }
  }

  pedirBorrado(categoria: CategoriaDeContenido): void {
    this.aviso.set(null);
    this.error.set(null);
    this.confirmandoBorrado.set(categoria.id);
  }

  cancelarBorrado(): void { this.confirmandoBorrado.set(null); }

  async eliminar(categoria: CategoriaDeContenido): Promise<void> {
    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    try {
      const resultado = await this.api.eliminar(categoria.id);
      if (resultado.ok) {
        this.aviso.set(`«${categoria.nombreCategoria}» se retiró.`);
        await this.cargar();
      } else {
        // EL MOTIVO DEL SERVIDOR ENTERO: dice cuántas fichas la usan, que es justo lo que hace
        // falta saber para decidir si vale la pena reasignarlas.
        this.error.set(resultado.error ?? 'No fue posible eliminar la categoría.');
      }
    } finally {
      this.confirmandoBorrado.set(null);
      this.guardando.set(false);
    }
  }
}
