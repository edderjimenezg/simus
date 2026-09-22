import { CommonModule } from '@angular/common';
import { SelectorMultipleComponent } from '../../shared/components/ui/selector-multiple/selector-multiple.component';
import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';

import { AvisoDeCorreoSinConfirmarComponent } from './aviso-de-correo-sin-confirmar.component';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';
import { IndicadorDePasosComponent, PasoDelIndicador } from '../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';
import {
  CatalogosDeMercadoExternos,
  FalloDelServidor,
  FestivalElegibleParaMercado,
  GuardarMercadoSolicitud,
  PanelOrganizacionApi,
  UbicacionDivipola,
} from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { PERIODICIDADES, pideDetalle } from '../../core/vocabularios/periodicidad';

export const PASOS_DEL_MERCADO: readonly PasoDelIndicador[] = [
  { id: 1, titulo: 'Datos generales' },
  { id: 2, titulo: 'Territorio y contacto' },
  { id: 3, titulo: 'Relación con un festival' },
  { id: 4, titulo: 'Revisar y guardar borrador' },
];

/**
 * Las periodicidades, las mismas que usa el Festival.
 *
 * SE COMPARTE EL VOCABULARIO Y NO SE INVENTA UNO PARALELO: un mercado anual y un festival anual
 * son lo mismo, y dos listas con los mismos valores divergen en cuanto alguien añade uno en una.
 */
// EL VOCABULARIO VIVE EN `core/vocabularios/periodicidad`, no aquí. Esta copia ofrecía
// «Permanente» —que el contrato del servidor no declara— y le faltaban «Trimestral», «Bianual» y
// «Trienal», que sí acepta: una pantalla ofreciendo un noveno valor y escondiendo tres legítimos.

interface FormularioDelMercado {
  nombre: string;
  descripcion: string;
  alcanceId: number | null;
  modalidadId: number | null;
  periodicidad: string;
  periodicidadDetalle: string;
  nivelCobertura: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  lugarEspecifico: string;
  correoMercado: string;
  telefonoMercado: string;
  sitioWebMercado: string;
  instagramMercado: string;
  facebookMercado: string;
  otroEnlaceMercado: string;
  observacionesContacto: string;
  seRealizaEnElMarcoDeUnFestival: boolean;
  festivalId: number | null;
  /** Del catálogo del Ecosistema, el mismo que llena un Festival. */
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
}

function vacioANulo(valor: string): string | null {
  const limpio = (valor ?? '').trim();
  return limpio.length === 0 ? null : limpio;
}

/**
 * El asistente de alta de un mercado musical, en /gestion/procesos/mercados/nuevo.
 *
 * <b>ES EL MISMO RECORRIDO QUE EL DE UN FESTIVAL, A PROPOSITO.</b> Una organización que ya registró
 * un festival reconoce esta pantalla: el mismo encabezado, el mismo indicador de pasos, los mismos
 * rótulos y los mismos controles. Lo que cambia es la ficha, no el procedimiento; inventar un
 * segundo estilo de alta obligaría a aprender dos veces lo mismo.
 *
 * <b>LA PUERTA DEL CORREO VA ANTES DEL FORMULARIO.</b> Con el correo sin confirmar el servidor
 * rechaza el registro igual; enseñar los cuatro pasos sería dejar que alguien escriba un mercado
 * entero para encontrarse el muro al guardar.
 *
 * <b>EL TERCER PASO ES LO UNICO QUE ESTE ASISTENTE TIENE Y EL DE FESTIVAL NO.</b> Un mercado puede
 * desarrollarse por su cuenta o dentro de un festival, y cuando ocurre dentro de uno eso es una
 * RELACION entre dos registros del sistema, no un nombre escrito a mano.
 */
@Component({
  selector: 'app-asistente-de-mercado',
  standalone: true,
  imports: [SelectorMultipleComponent, CommonModule, FormsModule, NombrePropioPipe, IndicadorDePasosComponent, AvisoDeCorreoSinConfirmarComponent],
  templateUrl: './asistente-de-mercado.component.html',
})
export class AsistenteDeMercadoComponent implements OnInit {
  constructor() {
    // LA ORGANIZACION ELEGIDA SE LLENA DESPUES DE LA SESION, y puede cambiarla el selector de la
    // cabecera en cualquier momento. Leerla una sola vez en `ngOnInit` deja la pantalla contestando
    // sobre una organización que ya no es la que está delante.
    effect(() => {
      this.store.organizacionId();
      untracked(() => this.cargarLosFestivales());
    });
  }

  private readonly api = inject(PanelOrganizacionApi);
  private readonly store = inject(PanelOrganizacionStore);
  private readonly router = inject(Router);
  protected readonly confirmacion = inject(ConfirmacionDeCorreoService);

  readonly faltaConfirmarCorreo = this.confirmacion.falta.bind(this.confirmacion);

  readonly pasos = PASOS_DEL_MERCADO;
  readonly periodicidades = PERIODICIDADES;
  readonly pideDetalle = pideDetalle;

  readonly pasoActivo = signal(1);
  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly error = signal('');
  readonly errores = signal<Record<string, string>>({});

  readonly catalogos = signal<CatalogosDeMercadoExternos>({ alcances: [], modalidades: [], practicasMusicales: [], territoriosSonoros: [] });
  readonly ubicaciones = signal<UbicacionDivipola[]>([]);
  readonly festivales = signal<FestivalElegibleParaMercado[]>([]);

  /**
   * En qué punto está la consulta de los festivales de la organización.
   *
   * <b>SIN ESTO, TRES SITUACIONES DISTINTAS SE VEIAN IGUAL.</b> «Todavía no has registrado ningún
   * festival» se enseñaba también cuando la consulta había fallado y cuando aún no había terminado,
   * porque el error se convertía en una lista vacía. se detectó el 15 de
   * septiembre de 2026 sobre una organización que SI tenía festivales registrados: la pantalla le
   * afirmaba lo contrario. Una lista vacía por un fallo no es una respuesta, y decirlo como si lo
   * fuera manda a registrar de nuevo algo que ya existe.
   */
  readonly consultaDeFestivales = signal<'sin_preguntar' | 'preguntando' | 'respondida' | 'fallida'>('sin_preguntar');

  readonly nombreOrganizacion = computed(() => this.store.organizacionElegida()?.name ?? '');

  readonly formulario: FormularioDelMercado = {
    nombre: '', descripcion: '', alcanceId: null, modalidadId: null,
    periodicidad: '', periodicidadDetalle: '', nivelCobertura: 'municipal',
    codigoDepartamento: '', codigoMunicipio: '', lugarEspecifico: '',
    correoMercado: '', telefonoMercado: '', sitioWebMercado: '',
    instagramMercado: '', facebookMercado: '', otroEnlaceMercado: '', observacionesContacto: '',
    seRealizaEnElMarcoDeUnFestival: false, festivalId: null,
    practicasMusicalesIds: [], territoriosSonorosIds: [],
  };

  ngOnInit(): void {
    const organizacionId = this.store.organizacionId();
    if (!organizacionId) {
      this.cargando.set(false);
      return;
    }

    forkJoin({
      catalogos: this.api.obtenerCatalogosDeMercado().pipe(catchError(() => of({ alcances: [], modalidades: [], practicasMusicales: [], territoriosSonoros: [] }))),
      ubicaciones: this.api.obtenerUbicaciones().pipe(catchError(() => of([] as UbicacionDivipola[]))),
    }).subscribe(({ catalogos, ubicaciones }) => {
      this.catalogos.set(catalogos ?? { alcances: [], modalidades: [], practicasMusicales: [], territoriosSonoros: [] });
      this.ubicaciones.set(ubicaciones ?? []);
      this.cargando.set(false);
    });

    this.cargarLosFestivales();
  }

  /**
   * Pide los festivales de la organización elegida.
   *
   * <b>SE PIDEN AL ABRIR Y NO AL LLEGAR AL PASO 3:</b> el paso tiene que poder responder en cuanto
   * se abre, no después de una espera. <b>Y SE VUELVEN A PEDIR SI CAMBIA LA ORGANIZACION</b>, porque
   * los candidatos de una no son los de otra: leer el identificador una sola vez dejaba la lista de
   * la primera organización pegada al formulario de la segunda.
   */
  cargarLosFestivales(): void {
    const organizacionId = this.store.organizacionId();
    if (!organizacionId) {
      this.consultaDeFestivales.set('sin_preguntar');
      return;
    }

    this.consultaDeFestivales.set('preguntando');
    this.api.obtenerFestivalesElegibles(organizacionId).subscribe({
      next: festivales => {
        this.festivales.set(festivales ?? []);
        this.consultaDeFestivales.set('respondida');
      },
      // EL FALLO SE DICE, NO SE CONVIERTE EN UNA LISTA VACIA. Es la diferencia entre «no tienes
      // ninguno» —que es una afirmación sobre tus datos— y «no pudimos preguntarlo».
      error: () => {
        this.festivales.set([]);
        this.consultaDeFestivales.set('fallida');
      },
    });
  }

  departamentos(): { codigo: string; nombre: string }[] {
    const unicos = new Map(this.ubicaciones().map(fila => [fila.departmentCode, fila.departmentName]));
    return Array.from(unicos, ([codigo, nombre]) => ({ codigo, nombre }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  }

  municipios(): UbicacionDivipola[] {
    if (!this.formulario.codigoDepartamento) return [];
    return this.ubicaciones().filter(fila => fila.departmentCode === this.formulario.codigoDepartamento);
  }

  irAlPaso(id: number): void {
    if (!this.pasos.some(paso => paso.id === id)) return;
    this.pasoActivo.set(id);
  }

  siguiente(): void { this.irAlPaso(Math.min(this.pasoActivo() + 1, this.pasos.length)); }
  anterior(): void { this.irAlPaso(Math.max(this.pasoActivo() - 1, 1)); }

  /**
   * Cambiar el nivel de cobertura suelta lo que deja de tener sentido.
   *
   * Sin esto, elegir un municipio y pasar después a cobertura nacional deja el código del municipio
   * en el formulario aunque su campo haya desaparecido, y el servidor rechaza el guardado señalando
   * un campo que ya no está en pantalla.
   */
  cambiarCobertura(nivel: string): void {
    this.formulario.nivelCobertura = nivel;
    if (nivel === 'nacional') { this.formulario.codigoDepartamento = ''; this.formulario.codigoMunicipio = ''; }
    if (nivel === 'departamental') this.formulario.codigoMunicipio = '';
  }

  cambiarDepartamento(codigo: string): void {
    this.formulario.codigoDepartamento = codigo;
    this.formulario.codigoMunicipio = '';
  }

  /** Responder que no borra el festival elegido: una relación que la ficha no enseña no debe existir. */
  cambiarSiEstaEnUnFestival(dentro: boolean): void {
    this.formulario.seRealizaEnElMarcoDeUnFestival = dentro;
    if (!dentro) this.formulario.festivalId = null;
  }

  etiquetaDeEstado(estado: string): string {
    const etiquetas: Record<string, string> = {
      borrador: 'en borrador', en_revision: 'en revisión',
      ajustes_solicitados: 'con ajustes solicitados', publicado: 'publicado', archivado: 'archivado',
    };
    return etiquetas[(estado ?? '').toLowerCase()] ?? estado;
  }

  /**
   * Lo mínimo para que el borrador tenga sentido.
   *
   * <b>ES MENOS DE LO QUE PIDE LA REVISION, Y ESA ES LA IDEA.</b> Un borrador se guarda con lo que
   * haya; lo que se entrega al Programa tiene que poder revisarse, y eso lo exige el servidor al
   * enviar. Pedirlo todo aquí convertiría el primer guardado en un muro.
   */
  validar(): boolean {
    const encontrados: Record<string, string> = {};
    if (!this.formulario.nombre.trim()) encontrados['nombre'] = 'El nombre del mercado es obligatorio.';
    if (this.formulario.nivelCobertura !== 'nacional' && !this.formulario.codigoDepartamento) {
      encontrados['codigoDepartamento'] = 'Elige el departamento que corresponde al nivel de cobertura.';
    }
    if (this.formulario.nivelCobertura === 'municipal' && !this.formulario.codigoMunicipio) {
      encontrados['codigoMunicipio'] = 'Elige el municipio.';
    }
    if (this.formulario.seRealizaEnElMarcoDeUnFestival && !this.formulario.festivalId) {
      encontrados['festivalId'] = 'Elige el festival en cuyo marco se realiza, o responde que no.';
    }
    this.errores.set(encontrados);
    return Object.keys(encontrados).length === 0;
  }

  guardar(): void {
    const organizacionId = this.store.organizacionId();
    if (!organizacionId) return;
    if (!this.validar()) {
      // SE VUELVE AL PASO QUE TIENE EL FALLO: un error anunciado en el paso 4 sobre un campo del
      // paso 2 obliga a buscarlo a mano.
      const conFallo = this.errores();
      if (conFallo['nombre']) this.irAlPaso(1);
      else if (conFallo['codigoDepartamento'] || conFallo['codigoMunicipio']) this.irAlPaso(2);
      else if (conFallo['festivalId']) this.irAlPaso(3);
      return;
    }

    this.guardando.set(true);
    this.error.set('');
    this.api.crearMercado(organizacionId, this.aSolicitud(+organizacionId)).subscribe({
      next: mercado => {
        this.guardando.set(false);
        void this.router.navigate(['/gestion/procesos/mercados', mercado.id]);
      },
      error: (fallo: FalloDelServidor) => {
        this.guardando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible registrar el mercado.');
      },
    });
  }

  /** Marca o desmarca un valor de catálogo. La lista la mantiene el formulario, no el control. */
  alternarDeCatalogo(clave: 'practicasMusicalesIds' | 'territoriosSonorosIds', id: number): void {
    const lista = this.formulario[clave];
    const donde = lista.indexOf(id);
    if (donde >= 0) { lista.splice(donde, 1); } else { lista.push(id); }
  }

  private aSolicitud(organizacionId: number): GuardarMercadoSolicitud {
    return {
      nombre: this.formulario.nombre.trim(),
      descripcion: vacioANulo(this.formulario.descripcion),
      alcanceId: this.formulario.alcanceId,
      modalidadId: this.formulario.modalidadId,
      periodicidad: vacioANulo(this.formulario.periodicidad),
      periodicidadDetalle: vacioANulo(this.formulario.periodicidadDetalle),
      correoMercado: vacioANulo(this.formulario.correoMercado),
      telefonoMercado: vacioANulo(this.formulario.telefonoMercado),
      sitioWebMercado: vacioANulo(this.formulario.sitioWebMercado),
      instagramMercado: vacioANulo(this.formulario.instagramMercado),
      facebookMercado: vacioANulo(this.formulario.facebookMercado),
      otroEnlaceMercado: vacioANulo(this.formulario.otroEnlaceMercado),
      observacionesContacto: vacioANulo(this.formulario.observacionesContacto),
      nivelCobertura: this.formulario.nivelCobertura,
      codigoDepartamento: vacioANulo(this.formulario.codigoDepartamento),
      codigoMunicipio: vacioANulo(this.formulario.codigoMunicipio),
      lugarEspecifico: vacioANulo(this.formulario.lugarEspecifico),
      seRealizaEnElMarcoDeUnFestival: this.formulario.seRealizaEnElMarcoDeUnFestival,
      festivalId: this.formulario.seRealizaEnElMarcoDeUnFestival ? this.formulario.festivalId : null,
      organizacionId,
      practicasMusicalesIds: [...this.formulario.practicasMusicalesIds],
      territoriosSonorosIds: [...this.formulario.territoriosSonorosIds],
    };
  }
}
