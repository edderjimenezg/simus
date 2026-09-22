import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService, CatalogosDeFestival, EdicionDeFestivalDetalle, EdicionDeFestivalSolicitud } from '../../core/services/admin.service';
import { AyudaConceptualCatalogoComponent } from '../../shared/components/ui/ayuda-conceptual-catalogo/ayuda-conceptual-catalogo.component';
import { tiempoDeLaEdicion } from './tiempo-de-la-edicion';
import { BancoDeArchivosService } from '../../core/services/banco-de-archivos.service';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';
import { EstadoDeListaComponent } from '../../shared/components/ui/estado-de-lista/estado-de-lista.component';

interface Divipola { departmentCode?: string; municipalityCode?: string; departmentName?: string; municipalityName?: string }

/** Ficha completa de una realización, separada del perfil público versionado del Festival. */
@Component({
  selector: 'app-ficha-edicion-festival', standalone: true, imports: [
    EstadoDeListaComponent,NombrePropioPipe, CommonModule, FormsModule, AyudaConceptualCatalogoComponent],
  templateUrl: './ficha-edicion-festival.component.html',
  /*
   * CONSULTAR SE TIENE QUE LEER COMO UNA FICHA, NO COMO UN FORMULARIO APAGADO.
   *
   * El `fieldset[disabled]` ya impide escribir, pero por omisión el navegador deja los cincuenta
   * marcos, los fondos y el gris de «deshabilitado», y eso se lee como «esto se rellena y algo va
   * mal», que es justo lo contrario de lo que pasa. Quitando marco y fondo, y devolviendo el color
   * del texto, los mismos campos se leen como lo que son: el dato registrado junto a su rótulo.
   *
   * VA EN EL COMPONENTE Y NO EN LA HOJA GLOBAL porque `data-consulta` solo existe aquí; una regla
   * global sobre `fieldset[disabled]` alcanzaría a cualquier formulario que se deshabilite mientras
   * envía, y entonces el formulario perdería sus marcos cada vez que alguien pulsa «Guardar».
   */
  styles: [`
    fieldset[data-consulta] input,
    fieldset[data-consulta] select,
    fieldset[data-consulta] textarea {
      border-color: transparent;
      background-color: transparent;
      padding-left: 0;
      padding-right: 0;
      color: #0f172a;
      opacity: 1;
      -webkit-text-fill-color: #0f172a;
    }
    fieldset[data-consulta] select { appearance: none; }
    /* «Sin registrar» tiene que verse: en un campo deshabilitado el marcador se apaga dos veces. */
    fieldset[data-consulta] input::placeholder { color: #64748b; opacity: 1; }
    fieldset[data-consulta] [data-solo-edicion] { display: none; }
  `],
})
export class FichaEdicionFestivalComponent implements OnChanges, AfterViewChecked {
  @Input({ required: true }) festivalId!: string;
  @Input() edicionId: string | null = null;
  /**
   * Si el Festival ya está publicado.
   *
   * DECIDE SI «PUBLICAR EDICION» EXISTE. Una edición no puede publicarse antes que su Festival, y
   * el servidor lo rechaza; ofrecer el botón igualmente sería prometer un acto que va a fallar.
   */
  @Input() festivalPublicado = false;

  /**
   * Abre la ficha para consultarla, sin poder cambiar nada.
   *
   * <b>POR QUE EXISTE.</b> Medido contra PNMC_LOCAL: una edicion
   * archivada no tenia NI UNA sola accion —ni publicar, ni editar, ni archivar, ni eliminar, y
   * ninguna observacion— asi que su fila quedaba con la columna «Acciones» vacia y no habia forma
   * de volver a ver lo que se habia registrado en ella. El registro seguia ahi y era inalcanzable.
   *
   * CONSULTAR SIEMPRE SE PUEDE, y es lo unico que se puede siempre: editar depende del estado,
   * publicar depende del Festival, eliminar depende de si llego a publicarse. Leer no depende de
   * nada, asi que es la accion que nunca falta.
   *
   * ES LA MISMA FICHA, NO UNA COPIA EN MODO LECTURA. Duplicar las cincuenta etiquetas de este
   * formulario en una vista paralela garantiza que las dos se separen en el primer cambio.
   */
  @Input() soloLectura = false;

  @Output() guardado = new EventEmitter<void>();

  /**
   * Al publicar, que no es lo mismo que guardar.
   *
   * SON DOS SUCESOS DISTINTOS porque el aviso que sigue es distinto: «quedó guardada como borrador»
   * después de pulsar «Publicar edición» deja sin saber si se publicó.
   */
  @Output() publicado = new EventEmitter<void>();

  @Output() cancelar = new EventEmitter<void>();
  private readonly api = inject(AdminService);
  private readonly banco = inject(BancoDeArchivosService);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  /** Qué fila está subiendo ahora mismo, para que su botón lo diga y las demás no se bloqueen. */
  readonly subiendoMaterial = signal<number | null>(null);

  /** El motivo del fallo, por fila: pesa demasiado, no es una imagen, o la cuota se agotó. */
  readonly errorDeMaterial = signal<Record<number, string>>({});

  /**
   * Sube el archivo de una fila de material al banco.
   *
   * <b>EL TEXTO ALTERNATIVO SALE DE LA DESCRIPCIÓN, y si no hay se pide antes de subir.</b> El
   * servidor lo exige —y lo exige ANTES de mirar los bytes, a propósito— porque una imagen sin
   * alternativa textual no la lee quien usa un lector de pantalla. Aquí se aprovecha el campo que
   * ya existe en vez de añadir otro: la descripción del material ES lo que describe la imagen.
   *
   * SE GUARDA LA DIRECCIÓN Y NO EL IDENTIFICADOR porque el contrato de material viaja con `url`.
   * El banco sirve por `/publico/archivos/{id}`, así que la dirección es estable y anónima.
   */
  async subirMaterial(indice: number, entrada: HTMLInputElement): Promise<void> {
    const archivo = entrada.files?.[0];
    entrada.value = '';
    if (!archivo) return;

    const fila = (this.form.materiales ?? [])[indice];
    const alternativa = (fila?.descripcionArchivo ?? '').trim();
    if (!alternativa) {
      this.errorDeMaterial.set({
        ...this.errorDeMaterial(),
        [indice]: 'Escribe primero la descripción: es lo que leerá quien no pueda ver la imagen.',
      });
      return;
    }

    const sinEste = { ...this.errorDeMaterial() };
    delete sinEste[indice];
    this.errorDeMaterial.set(sinEste);
    this.subiendoMaterial.set(indice);

    const resultado = await this.banco.subirImagen(archivo, alternativa, { canal: 'externo' });
    this.subiendoMaterial.set(null);

    if (!resultado.ok || !resultado.archivo) {
      this.errorDeMaterial.set({
        ...this.errorDeMaterial(),
        [indice]: resultado.error ?? 'No fue posible subir el archivo.',
      });
      return;
    }
    if (fila) fila.url = resultado.archivo.url;
  }
  readonly cargando = signal(false); readonly guardando = signal(false); readonly publicando = signal(false); readonly error = signal('');
  readonly catalogos = signal<CatalogosDeFestival | null>(null); readonly divipola = signal<Divipola[]>([]);
  readonly form = this.vacio();

  /**
   * Las fechas del formulario, como señal, para que lo derivado se recalcule al teclearlas.
   *
   * `form` ES UN OBJETO PLANO y no una señal —lo llena `Object.assign` y lo enlaza `ngModel`—, así
   * que un `computed()` sobre él no se enteraría de nada. Esta señal la empuja el propio campo al
   * cambiar, que es el único momento en que el dato derivado puede cambiar.
   */
  private readonly fechas = signal<{ inicio: string | null; fin: string | null }>({ inicio: null, fin: null });

  /**
   * El mes o los meses en que ocurre la Edición, y cuántos días dura.
   *
   * LOS PIDE LA HISTORIA DE USUARIO COMO OBLIGATORIOS Y AUTOCALCULADOS, y no existían: ni columna,
   * ni cálculo, ni casilla. No se escriben ni se guardan; el servidor los deriva igual para quien
   * lea la Edición por el API, con la misma tabla de casos.
   */
  readonly tiempo = computed(() => tiempoDeLaEdicion(this.fechas().inicio, this.fechas().fin));

  /** Lo llama el campo de fecha al cambiar: es lo que hace que mes y duración se vean en vivo. */
  alCambiarLasFechas(): void {
    this.fechas.set({ inicio: this.form.fechaInicio ?? null, fin: this.form.fechaFin ?? null });
  }

  /**
   * La Región OCAD de un departamento.
   *
   * <b>NO SE ELIGE: SE DERIVA.</b> La historia de usuario la marca «campo automático, lo definen
   * los campos seleccionados en Departamento y Municipio», y sale de la correspondencia oficial
   * Departamento–Región del Sistema General de Regalías, que viaja con los catálogos. Esa tabla
   * estaba sembrada con sus 32 departamentos desde y no la consultaba
   * nadie: la región no aparecía en ninguna pantalla.
   */
  regionOcadDe(codigoDepartamento: string | null | undefined): string {
    if (!codigoDepartamento) return 'Se asigna con el departamento';
    const catalogos = this.catalogos();
    const par = (catalogos?.regionOcadPorDepartamento ?? []).find(x => x.codigoDepartamento === codigoDepartamento);
    if (!par) return 'Sin región asignada';
    return (catalogos?.regionesOcad ?? []).find(x => x.id === par.regionOcadId)?.nombre ?? 'Sin región asignada';
  }

  ngOnChanges(): void {
    this.error.set(''); this.cargarCatalogos();
    if (this.edicionId) this.cargarEdicion(this.edicionId); else this.asignar(this.vacio());
  }

  municipios(departamento: string): Divipola[] { return this.divipola().filter(x => x.departmentCode === departamento); }
  departamentos(): Divipola[] { return this.divipola().filter((x, i, a) => !!x.departmentCode && a.findIndex(y => y.departmentCode === x.departmentCode) === i); }
  alternar(campo: keyof Pick<EdicionDeFestivalSolicitud, 'practicasMusicalesIds' | 'territoriosSonorosIds' | 'expresionesArtisticasIds' | 'modalidadesParticipacionIds' | 'tiposIngresoIds'>, id: number): void {
    const actual = this.form[campo] ?? []; this.form[campo] = actual.includes(id) ? actual.filter(x => x !== id) : [...actual, id];
  }
  agregarLocalizacion(): void { this.form.localizaciones!.push({ codigoDepartamento: '', codigoMunicipio: '', zonaUrbanoRuralId: null, titulacionColectivaId: null }); }
  quitarLocalizacion(i: number): void { this.form.localizaciones!.splice(i, 1); }
  agregarAliada(): void { this.form.entidadesAliadas!.push({ nombre: null, correo: null, naturalezaEntidadId: null, entidadId: null }); }
  quitarAliada(i: number): void { this.form.entidadesAliadas!.splice(i, 1); }
  agregarMaterial(): void { this.form.materiales!.push({ url: null, descripcionArchivo: null }); }
  quitarMaterial(i: number): void { this.form.materiales!.splice(i, 1); }
  /**
   * Guarda la edición sin hacerla pública.
   *
   * ES LA SALIDA DE SIEMPRE y sigue siendo la que menos compromete: deja seguir trabajando.
   */
  guardar(): void {
    this.error.set('');
    this.guardando.set(true);
    this.guardarEntonces(() => { this.guardando.set(false); this.guardado.emit(); });
  }

  /**
   * Guarda y, si el guardado sale bien, publica.
   *
   * <b>SON DOS PETICIONES Y SE HACEN EN ESTE ORDEN A PROPOSITO.</b> Publicar sin guardar antes
   * publicaría la versión anterior de lo que la persona tiene delante, que es la forma más
   * silenciosa de perder un cambio. Si el guardado falla, no se publica y el error es el del
   * guardado, que es el que se puede corregir.
   */
  publicar(): void {
    this.error.set('');
    this.publicando.set(true);
    this.guardarEntonces(edicionId => {
      if (!edicionId) { this.publicando.set(false); this.error.set('No fue posible identificar la edición.'); return; }
      this.api.publicarEdicion(edicionId).subscribe({
        next: () => { this.publicando.set(false); this.publicado.emit(); },
        error: e => {
          this.publicando.set(false);
          // LO GUARDADO NO SE PIERDE, Y SE DICE: el borrador está a salvo y lo único que falló es la
          // publicación. Sin esa frase, el error se lee como «se perdió todo».
          this.error.set((e?.message ?? 'No fue posible publicar la edición.')
            + ' Los cambios quedaron guardados como borrador.');
        },
      });
    });
  }

  /** El guardado que comparten las dos salidas, con el identificador de la edición resultante. */
  private guardarEntonces(despues: (edicionId: string | null) => void): void {
    const solicitud = this.normalizar();
    const respuesta = this.edicionId
      ? this.api.actualizarEdicionDeFestival(this.edicionId, solicitud)
      : this.api.crearEdicionDeFestival(Number(this.festivalId), solicitud);
    respuesta.subscribe({
      next: guardada => despues(guardada?.id ?? this.edicionId),
      error: e => {
        this.guardando.set(false);
        this.publicando.set(false);
        this.error.set(e?.message ?? 'No fue posible guardar la edición.');
      },
    });
  }

  private cargarCatalogos(): void {
    this.api.cargarCatalogosDeFestival().subscribe({ next: x => this.catalogos.set(x), error: e => this.error.set(e?.message ?? 'No fue posible cargar los catálogos.') });
    this.api.cargarDivipolaPublica().subscribe({ next: x => this.divipola.set(x ?? []) });
  }
  private cargarEdicion(id: string): void {
    this.cargando.set(true); this.api.cargarEdicionDeFestival(id).subscribe({ next: detalle => { this.asignar(this.desdeDetalle(detalle)); this.cargando.set(false); }, error: e => { this.error.set(e?.message ?? 'No fue posible cargar la edición.'); this.cargando.set(false); } });
  }
  private asignar(valor: EdicionDeFestivalSolicitud): void { Object.assign(this.form, valor); this.alCambiarLasFechas(); }
  private vacio(): EdicionDeFestivalSolicitud {
    return { anio: new Date().getFullYear(), numeroEdicion: null, nombre: null, descripcion: null, fechaInicio: null, fechaFin: null, director: null, estado: 'en_preparacion', tipologiaFestivalId: null, otraTipologia: null, fuenteFinanciacionPrimariaId: null, otraFuenteFinanciacionPrimaria: null, fuenteFinanciacionSecundariaId: null, otraFuenteFinanciacionSecundaria: null, usaEstampillaProcultura: null, practicasMusicalesQueCongrega: null, otraModalidadParticipacion: null, otraExpresionArtistica: null, practicasMusicalesIds: [], territoriosSonorosIds: [], expresionesArtisticasIds: [], modalidadesParticipacionIds: [], tiposIngresoIds: [], localizaciones: [], entidadesAliadas: [], materiales: [] };
  }
  private desdeDetalle(x: EdicionDeFestivalDetalle): EdicionDeFestivalSolicitud {
    return { ...this.vacio(), ...x.edicion, ...x, materiales: x.materiales ?? [], localizaciones: x.localizaciones ?? [], entidadesAliadas: x.entidadesAliadas ?? [] };
  }
  private normalizar(): EdicionDeFestivalSolicitud {
    const f = this.form;
    return { ...f, anio: f.anio || null, numeroEdicion: f.numeroEdicion || null, nombre: this.texto(f.nombre), descripcion: this.texto(f.descripcion), director: this.texto(f.director), fechaInicio: this.texto(f.fechaInicio), fechaFin: this.texto(f.fechaFin), otraTipologia: this.texto(f.otraTipologia), otraFuenteFinanciacionPrimaria: this.texto(f.otraFuenteFinanciacionPrimaria), otraFuenteFinanciacionSecundaria: this.texto(f.otraFuenteFinanciacionSecundaria), practicasMusicalesQueCongrega: this.texto(f.practicasMusicalesQueCongrega), otraModalidadParticipacion: this.texto(f.otraModalidadParticipacion), otraExpresionArtistica: this.texto(f.otraExpresionArtistica), localizaciones: (f.localizaciones ?? []).filter(x => x.codigoDepartamento && x.codigoMunicipio), entidadesAliadas: (f.entidadesAliadas ?? []).filter(x => x.nombre || x.correo || x.entidadId), materiales: (f.materiales ?? []).filter(x => x.url || x.descripcionArchivo) };
  }
  private texto(valor: string | null | undefined): string | null { const limpio = valor?.trim(); return limpio || null; }

  /**
   * Deja los campos legibles cuando la ficha se abre solo para consultarla.
   *
   * <b>DOS COSAS QUE EL NAVEGADOR HACE MAL EN UNA FICHA DE LECTURA.</b> Medido en Chrome contra
   * PNMC_LOCAL sobre una edición archivada:
   *
   * 1. Un `input[type=date]` VACIO no se ve vacío: pinta `mm/dd/yyyy`, que en una ficha de consulta
   *    se lee como si el dato registrado fuera ese, y además en un orden que no es el que usa el
   *    país. Pasarlos a texto los deja de verdad en blanco y muestra la fecha tal como la guarda el
   *    sistema, `aaaa-mm-dd`, que es la norma ISO 8601 que ya usa el resto del proyecto.
   * 2. Un campo vacío sin más es ambiguo: no distingue «no se registró» de «no cargó». El
   *    marcador de posición SI es legítimo aquí —no está haciendo de rótulo, que existe y está
   *    encima; está diciendo qué significa el hueco— y solo aparece consultando.
   *
   * <b>POR QUE SE HACE SOBRE EL DOM Y NO CAMPO POR CAMPO.</b> Son más de sesenta controles. Repetir
   * en cada uno un `[attr.type]` y un `[attr.placeholder]` condicionados obliga a acordarse de los
   * dos cada vez que se añada un campo, y basta olvidarlo una vez para que la ficha de consulta
   * vuelva a mentir en ese campo. Esto no toca el modelo ni el valor: solo cómo se presenta algo que
   * en este modo no se puede cambiar.
   */
  ngAfterViewChecked(): void {
    if (!this.soloLectura) return;
    const marco = this.host.nativeElement.querySelector<HTMLElement>('fieldset[data-consulta]');
    if (!marco || marco.dataset['legible'] === 'si') return;

    const campos = marco.querySelectorAll<HTMLInputElement>('input[type=date], input[type=number], input[type=text], input[type=url]');
    for (const campo of Array.from(campos)) {
      if (campo.type === 'date' || campo.type === 'number') campo.type = 'text';
      if (!campo.value) campo.placeholder = 'Sin registrar';
    }
    marco.dataset['legible'] = 'si';
  }
}
