import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import {
  CatalogosDelFestival,
  GuardarFestivalSolicitud,
  NIVELES_DE_COBERTURA,
  PanelOrganizacionApi,
  UbicacionDivipola,
} from './panel-organizacion.api';
import { PanelOrganizacionStore } from './panel-organizacion.store';
import { BotonComponent } from '../../shared/components/ui/boton/boton.component';
import { DialogoDirective } from '../../shared/directives/dialogo.directive';
import { IndicadorDePasosComponent } from '../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { ApiClientService } from '../../core/http/api-client.service';
import { AutoguardadoDeBorrador } from '../../core/services/autoguardado-de-borrador';
import { ConfirmacionDeCorreoService } from '../../core/services/confirmacion-de-correo.service';
import { AvisoDeCorreoSinConfirmarComponent } from './aviso-de-correo-sin-confirmar.component';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';
import { PERIODICIDADES, etiquetaDePeriodicidad, pideDetalle } from '../../core/vocabularios/periodicidad';

const CATALOGOS_VACIOS: CatalogosDelFestival = {
  practicasMusicales: [], territoriosSonoros: [], tipologias: [], expresionesArtisticas: [],
  fuentesFinanciacion: [], modalidadesParticipacion: [], naturalezasEntidad: [], tiposIngreso: [],
  tiposOrganizador: [], zonasUrbanoRural: [], titulacionesColectivas: [], regionesOcad: [],
};

/** Los cuatro pasos, en el orden en que se recorren. */
export interface PasoDelAsistente {
  id: number;
  titulo: string;
}

/**
 * Una coincidencia con un Festival que ya existe.
 *
 * <b>`tipoCoincidencia` DECIDE LO QUE LA PANTALLA OFRECE, y por eso viaja.</b> Hasta el 13 de
 * septiembre de 2026 todas las coincidencias se pintaban igual, con su formulario de «Solicitar
 * administración» debajo. Eso solo tiene sentido para un registro HISTÓRICO sin organización
 * administradora; para uno que ya administra alguien, pedir su administración por aquí no es lo que
 * corresponde —ese es otro trámite— y el aviso útil es simplemente que no se registre dos veces.
 */
interface CoincidenciaBasica {
  festivalId: string;
  nombreFestival: string;
  descripcion: string | null;
  evidencias: string[];
  /** `HistoricoReclamable` | `YaRegistradoPorTuOrganizacion` | `YaRegistradoPorOtraOrganizacion`. */
  tipoCoincidencia: string;
  /** Quién lo administra, cuando no es un registro histórico sin dueño. */
  organizadorHistorico: string | null;
}

interface FichaFestivalPublica {
  nombre: string;
  descripcion: string | null;
  organizacionResponsable: string | null;
  territorioPrincipal: { departamento: string | null; municipio: string | null; nivelCobertura: string };
  periodicidad: string | null;
  practicasMusicales: { nombre: string }[];
  territoriosSonoros: { nombre: string }[];
  instagram: string | null;
  facebook: string | null;
  sitioWeb: string | null;
  otroEnlace: string | null;
}

interface EdicionPublica { id: number; nombre: string; fechaInicio: string | null; fechaFin: string | null; descripcion: string | null; }
interface CoincidenciaHistorica extends CoincidenciaBasica {
  ficha: FichaFestivalPublica | null;
  ediciones: EdicionPublica[];
  /**
   * No tiene ficha pública, y eso NO es un fallo.
   *
   * <b>SE DISTINGUE DEL ERROR DE RED A PROPOSITO.</b> Desde que la búsqueda también avisa de los
   * Festivales que la propia organización ya registró, muchas coincidencias son BORRADORES: no
   * tienen ficha pública porque todavía no se han publicado. Contar eso como «no fue posible
   * cargar» —que es lo que decía— convierte una situación normal en un fallo aparente, y encima en
   * la pantalla donde alguien está decidiendo si su registro está duplicado.
   */
  sinFichaPublica: boolean;
}

export const PASOS_DEL_ASISTENTE: readonly PasoDelAsistente[] = [
  { id: 1, titulo: 'Datos generales' },
  { id: 2, titulo: 'Territorio y contacto' },
  { id: 3, titulo: 'Prácticas y territorios sonoros' },
  { id: 4, titulo: 'Revisar y guardar borrador' },
];

// El vocabulario vive en `core/vocabularios/periodicidad`, no aquí.

interface FormularioDelAsistente {
  nombre: string;
  descripcion: string;
  periodicidad: string;
  periodicidadDetalle: string;
  nivelCobertura: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  correoContacto: string;
  telefonoCelular: string;
  instagram: string;
  facebook: string;
  paginaWeb: string;
  otroEnlace: string;
  observacionesContacto: string;
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
}

const CAMPOS_DE_TEXTO_DEL_BORRADOR: readonly (keyof Omit<FormularioDelAsistente, 'practicasMusicalesIds' | 'territoriosSonorosIds'>)[] = [
  'nombre', 'descripcion', 'periodicidad', 'periodicidadDetalle', 'nivelCobertura',
  'codigoDepartamento', 'codigoMunicipio', 'correoContacto', 'telefonoCelular',
  'instagram', 'facebook', 'paginaWeb', 'otroEnlace', 'observacionesContacto',
];

function vacioANulo(valor: string): string | null {
  const limpio = (valor ?? '').trim();
  return limpio.length === 0 ? null : limpio;
}

/**
 * El asistente de alta de Festival, en /gestion/procesos/festivales/nuevo.
 *
 * <b>REVIERTE, SOLO PARA CREAR, LA DECISIÓN DEL 29 DE AGOSTO DE 2026.</b> Ese día se retiró un
 * asistente por pasos de `FichaFestivalComponent` a favor de una vista única para leer, editar y
 * crear -«mantengamos este como base para editar y ver ficha; cuando se crea un festival también
 * mantengamos esta estructura», `secciones-de-la-ficha.ts-35`-. La dirección de producto, consultado
 * de nuevo, confirmó que quiere el asistente de vuelta, pero SOLO para
 * dar de alta un Festival nuevo: `FichaFestivalComponent` sigue exactamente igual para leer y
 * editar -esa mitad de la decisión de agosto no se tocó-.
 *
 * <b>NO ES EL MISMO FORMULARIO QUE LA FICHA, Y NO LO NECESITA.</b> Aquí no se pide ninguna edición:
 * la propuesta original fue explícita -«las ediciones no aparecerían durante la creación
 * inicial... se completa después desde su consola»-. Los tres primeros pasos escriben exactamente
 * los mismos catorce campos de `GuardarFestivalSolicitud` que ya usa
 * `FichaFestivalComponent.construirSolicitudDelFestival()`, con las mismas reglas de
 * `validar()` -mismo `nombre` obligatorio, mismo territorio condicional según `nivelCobertura`-,
 * así que al guardar solo se llama `crearFestival()`, nunca `crearVersion()`.
 *
 * <b>`?paso=` Y NO UN segmento de ruta.</b> Mismo criterio que `?proceso=` en
 * `SeccionEcosistemaComponent` y `?modo=` en el login: es un único formulario que se llena
 * progresivamente, no cuatro destinos distintos que alguien pudiera enlazar por separado.
 */
@Component({
  selector: 'app-asistente-de-festival',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, FormsModule, IndicadorDePasosComponent, AvisoDeCorreoSinConfirmarComponent,
    BotonComponent, DialogoDirective],
  templateUrl: './asistente-de-festival.component.html',
})
export class AsistenteDeFestivalComponent implements OnInit, OnDestroy {
  /**
   * La confirmación del correo, que decide si esta pantalla llega a ofrecer el formulario.
   *
   * <b>NO SE ABRE EL FORMULARIO SI FALTA.</b> El servidor lo rechaza igual, pero descubrirlo al
   * final —después de cuatro pasos y catorce campos— es exactamente el fallo que el plan de
   * consolidación mandó cerrar: la puerta se explica al principio, con
   * la salida para resolverla ahí mismo.
   *
   * <b>YA ESTA RESUELTA AL LLEGAR AQUI.</b> La carga la hace el panel al entrar, una vez, porque la
   * confirmación es de la cuenta y no de la pantalla. Si se preguntara aquí, esta pantalla tendría
   * que elegir entre esperar una respuesta de red antes de pintar nada o enseñar los cuatro pasos y
   * retirarlos medio segundo después, con lo que ya se estuviera escribiendo dentro.
   */
  protected readonly confirmacion = inject(ConfirmacionDeCorreoService);
  readonly faltaConfirmarCorreo = this.confirmacion.falta.bind(this.confirmacion);

  private readonly api = inject(PanelOrganizacionApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(PanelOrganizacionStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly http = inject(ApiClientService);
  private borradorId: string | null = null;

  /**
   * El bucle de autoguardado, el mismo que usan los tres módulos de la consola.
   *
   * <b>ESTE ASISTENTE SE HABIA ESCRITO EL SUYO Y LE FALTABA LA PARTE DELICADA.</b> Antes de mandar
   * comprobaba `guardando()`, que es la señal del guardado MANUAL, no la del automático: dos
   * automáticos sí podían solaparse, llegar con la misma versión y hacer que el servidor rechazara
   * el segundo con «El borrador cambió en otra sesión» —falso: había chocado consigo mismo—,
   * dejando el formulario en error y las últimas pulsaciones sin guardar. La clase compartida marca
   * el trabajo pendiente y lo reintenta al terminar, que es justo lo que faltaba.
   */
  private readonly autoguardado = new AutoguardadoDeBorrador<Record<string, unknown>>({
    // NO SE LEE POR AQUI. El borrador se recupera al abrir el asistente, con la ruta de la
    // organización, y de ahí sale el identificador que este transporte necesita para guardar.
    leer: async () => null,
    guardar: (datos, version) => this.guardarBorradorPrivado(datos, version),
    // NO SE DESCARTA. El borrador privado de la organización es uno solo y se reutiliza: al
    // guardar el Festival de verdad, el servidor lo vacía. Borrarlo desde aquí dejaría a la
    // siguiente alta sin la fila que `ObtenerOCrearBorrador` espera encontrar.
    descartar: async () => undefined,
  });

  private temporizadorDeBusqueda: ReturnType<typeof setTimeout> | null = null;
  private overflowOriginalDelDocumento: string | null = null;

  readonly pasos = PASOS_DEL_ASISTENTE;
  readonly niveles = NIVELES_DE_COBERTURA;
  /** La periodicidad se lee por su nombre, no por su código: «otra_regular» no es una palabra. */
  readonly etiquetaDePeriodicidad = etiquetaDePeriodicidad;

  readonly periodicidades = PERIODICIDADES;
  readonly pideDetalle = pideDetalle;
  readonly organizacionId = this.store.organizacionId;

  readonly cargando = signal(true);
  readonly guardando = signal(false);
  readonly error = signal('');
  /**
   * Lo que la plantilla enseña sobre el guardado, derivado del bucle compartido.
   *
   * Se traduce en vez de exponer el estado crudo porque esta pantalla ya decía «pendiente» donde el
   * bucle dice «inactivo», y cambiar el rótulo visible no era parte de arreglar la carrera.
   */
  readonly estadoGuardado = computed<'pendiente' | 'guardando' | 'guardado' | 'error'>(() => {
    switch (this.autoguardado.estado()) {
      case 'guardando': return 'guardando';
      case 'guardado': return 'guardado';
      case 'fallido': return 'error';
      default: return 'pendiente';
    }
  });
  readonly buscandoCoincidencias = signal(false);
  readonly coincidencias = signal<CoincidenciaHistorica[]>([]);
  readonly justificacionReclamacion = signal('');
  readonly reclamacionEnviada = signal('');
  readonly modalCoincidenciasAbierto = signal(false);
  readonly coincidenciasRevisadas = signal(false);

  /**
   * Esta coincidencia se puede reclamar.
   *
   * SOLO LOS HISTÓRICOS SIN DUEÑO. Un Festival que ya administra otra organización no se reclama
   * desde el asistente de alta: lo que hace falta ahí es no registrarlo dos veces.
   */
  sePuedeReclamar(coincidencia: CoincidenciaBasica): boolean {
    return coincidencia.tipoCoincidencia === 'HistoricoReclamable';
  }

  /** Cuántas de las encontradas son registros históricos que sí se pueden reclamar. */
  readonly cuantasReclamables = computed(
    () => this.coincidencias().filter(una => this.sePuedeReclamar(una)).length);
  readonly pasoActivo = signal(1);

  readonly catalogos = signal<CatalogosDelFestival>(CATALOGOS_VACIOS);
  readonly ubicaciones = signal<UbicacionDivipola[]>([]);
  readonly errores = signal<Record<string, string>>({});

  readonly formulario: FormularioDelAsistente = {
    nombre: '', descripcion: '', periodicidad: '', periodicidadDetalle: '', nivelCobertura: 'municipal',
    codigoDepartamento: '', codigoMunicipio: '', correoContacto: '', telefonoCelular: '',
    instagram: '', facebook: '', paginaWeb: '', otroEnlace: '', observacionesContacto: '',
    practicasMusicalesIds: [], territoriosSonorosIds: [],
  };

  nombreOrganizacion(): string {
    return this.store.organizacionElegida()?.name ?? '';
  }

  ngOnInit(): void {
    const pasoPedido = Number(this.route.snapshot.queryParamMap.get('paso'));
    if (this.pasos.some(paso => paso.id === pasoPedido)) this.pasoActivo.set(pasoPedido);

    forkJoin({
      catalogos: this.api.obtenerCatalogosDelFestival().pipe(catchError(() => of(CATALOGOS_VACIOS))),
      ubicaciones: this.api.obtenerUbicaciones().pipe(catchError(() => of([] as UbicacionDivipola[]))),
    }).subscribe(({ catalogos, ubicaciones }) => {
      this.catalogos.set(catalogos ?? CATALOGOS_VACIOS);
      this.ubicaciones.set(ubicaciones ?? []);
      this.cargando.set(false);
      this.cargarBorradorPrivado();
    });
  }

  ngOnDestroy(): void {
    this.autoguardado.apagar();
    if (this.temporizadorDeBusqueda) clearTimeout(this.temporizadorDeBusqueda);
    this.desbloquearDesplazamiento();
  }

  programarGuardado(): void {
    // EL BUCLE LO LLEVA `autoguardado`: espera la quietud, no solapa dos envíos y reintenta el que
    // quedó pendiente. Aquí solo se le dice qué hay que guardar.
    this.autoguardado.anotar(this.construirSolicitud() as unknown as Record<string, unknown>);

    if (this.temporizadorDeBusqueda) clearTimeout(this.temporizadorDeBusqueda);
    this.temporizadorDeBusqueda = setTimeout(() => this.buscarCoincidencias(), 850);
  }

  private cargarBorradorPrivado(): void {
    const organizacionId = this.organizacionId();
    if (!organizacionId) return;
    this.http.get<{ id: string; datosJson: string; version: number }>(`/api/v1/externo/organizaciones/${organizacionId}/festivales/borrador`, { errorFallback: 'No fue posible recuperar el borrador privado' }).subscribe({
      next: borrador => {
        this.borradorId = borrador.id;
        this.autoguardado.encender(borrador.version);
        try {
          const datos = JSON.parse(borrador.datosJson || '{}') as Record<string, unknown>;
          for (const campo of CAMPOS_DE_TEXTO_DEL_BORRADOR) {
            const valor = datos[campo];
            if (typeof valor === 'string') this.formulario[campo] = valor;
            else if (valor === null) this.formulario[campo] = '';
          }
          this.formulario.nivelCobertura ||= 'municipal';
          this.formulario.practicasMusicalesIds = this.normalizarIds(datos['practicasMusicalesIds']);
          this.formulario.territoriosSonorosIds = this.normalizarIds(datos['territoriosSonorosIds']);
        } catch {
          // UN BORRADOR ILEGIBLE NO SE APLICA, pero el asistente sigue: se empieza en blanco en vez
          // de dejar el formulario a medio rellenar con lo que se pudo interpretar.
          this.autoguardado.apagar();
        }
      },
      error: () => this.autoguardado.apagar(),
    });
  }

  private normalizarIds(valor: unknown): number[] {
    if (!Array.isArray(valor)) return [];
    return valor
      .filter((id): id is number => typeof id === 'number' && Number.isInteger(id) && id > 0)
      .filter((id, indice, todos) => todos.indexOf(id) === indice);
  }

  /**
   * Manda el borrador al servidor. Lo llama el bucle compartido, nunca la plantilla.
   *
   * Devuelve `null` si falla, que es lo que el bucle traduce a «fallido» en pantalla: quien está
   * escribiendo necesita saber que lo que ve ya no está respaldado.
   */
  private async guardarBorradorPrivado(
    datos: Record<string, unknown>, version: number | null,
  ): Promise<{ version: number; fechaActualizacion: string } | null> {
    if (!this.borradorId) return null;

    try {
      const testigo = await firstValueFrom(this.http.get<{ requestToken: string }>(
        '/api/v1/externo/organizaciones/csrf',
        { errorFallback: 'No fue posible preparar el guardado del borrador' }));

      const respuesta = await firstValueFrom(this.http.put<{ version: number }>(
        `/api/v1/externo/borradores-proceso/${this.borradorId}`,
        { datosJson: JSON.stringify(datos), version },
        { headers: { 'X-CSRF-TOKEN': testigo.requestToken }, errorFallback: 'No fue posible guardar el borrador' }));

      return { version: respuesta.version, fechaActualizacion: new Date().toISOString() };
    } catch {
      return null;
    }
  }

  private buscarCoincidencias(abrirAlTerminar = false): void {
    if (!this.formulario.nombre.trim() || !this.formulario.codigoDepartamento || !this.exigeMunicipio() || !this.formulario.codigoMunicipio) {
      this.coincidencias.set([]);
      if (abrirAlTerminar) this.irAlPaso(4);
      return;
    }
    this.buscandoCoincidencias.set(true);
    this.http.get<CoincidenciaBasica[]>(`/api/v1/externo/organizaciones/${this.organizacionId()}/festivales/coincidencias-historicas`, { params: { nombre: this.formulario.nombre, codigoDepartamento: this.formulario.codigoDepartamento, codigoMunicipio: this.formulario.codigoMunicipio }, errorFallback: 'No fue posible buscar coincidencias' }).subscribe({
      next: filas => {
        if (!filas.length) {
          this.coincidencias.set([]);
          this.buscandoCoincidencias.set(false);
          if (abrirAlTerminar) this.irAlPaso(4);
          return;
        }
        forkJoin(filas.map(fila => forkJoin({
          ficha: this.http.get<FichaFestivalPublica>(`/api/v1/publico/festivales/${fila.festivalId}`, { errorFallback: 'No fue posible consultar la ficha pública del Festival.' }),
          ediciones: this.http.get<EdicionPublica[]>(`/api/v1/publico/festivales/${fila.festivalId}/ediciones`, { errorFallback: 'No fue posible consultar las ediciones públicas del Festival.' }),
        }).pipe(catchError((fallo: { status?: number }) => of({
          ficha: null,
          ediciones: [] as EdicionPublica[],
          // 404 ES «no hay ficha pública», no «se cayó la consulta». Un borrador propio da 404.
          sinFichaPublica: fallo?.status === 404,
        })))))
          .subscribe(detalles => {
            this.coincidencias.set(filas.map((fila, indice) => ({
              ...fila,
              ficha: detalles[indice].ficha,
              ediciones: detalles[indice].ediciones,
              sinFichaPublica: 'sinFichaPublica' in detalles[indice] ? detalles[indice].sinFichaPublica : false,
            })));
            this.buscandoCoincidencias.set(false);
            if (abrirAlTerminar && detalles.length) this.abrirModalDeCoincidencias();
          });
      },
      error: () => {
        this.buscandoCoincidencias.set(false);
        this.coincidencias.set([]);
        // Una consulta auxiliar no puede dejar atrapado un registro nuevo.
        if (abrirAlTerminar) this.irAlPaso(4);
      },
    });
  }

  solicitarAdministracion(festivalId: string): void {
    if (!this.borradorId || !this.justificacionReclamacion().trim()) {
      this.error.set('Explica por qué este Festival corresponde a tu organización antes de enviar la solicitud.');
      return;
    }
    this.guardando.set(true);
    this.http.get<{ requestToken: string }>('/api/v1/externo/organizaciones/csrf', { errorFallback: 'No fue posible preparar la solicitud de administración' }).pipe(switchMap(token =>
      this.http.post<{ id: string }>(`/api/v1/externo/organizaciones/${this.organizacionId()}/reclamaciones-administracion`, {
        festivalId, borradorId: this.borradorId, justificacion: this.justificacionReclamacion().trim(),
        senalesJson: JSON.stringify(['Nombre y territorio coincidentes']),
      }, { headers: { 'X-CSRF-TOKEN': token.requestToken }, errorFallback: 'No fue posible enviar la solicitud de administración' })
    )).subscribe({
      next: () => { this.guardando.set(false); this.reclamacionEnviada.set(festivalId); this.cerrarModalDeCoincidencias(); this.coincidenciasRevisadas.set(true); this.irAlPaso(4); this.error.set('Solicitud enviada. Tu información queda en borrador hasta que la institución decida. Si aprueba, podrás conciliar los datos antes de transferir la administración.'); },
      error: fallo => { this.guardando.set(false); this.error.set(fallo?.message ?? 'No fue posible enviar la solicitud.'); },
    });
  }

  // ─────────────────────────── DIVIPOLA y territorio ───────────────────────────
  // Misma lógica que `FichaFestivalComponent` -mismas reglas de `CK_Festivales_NivelCobertura`-,
  // sobre el formulario propio del asistente.

  departamentos(): { codigo: string; nombre: string }[] {
    const unicos = new Map(this.ubicaciones().map(fila => [fila.departmentCode, fila.departmentName]));
    return Array.from(unicos, ([codigo, nombre]) => ({ codigo, nombre }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  }

  municipios(): UbicacionDivipola[] {
    if (!this.formulario.codigoDepartamento) return [];
    return this.ubicaciones().filter(fila => fila.departmentCode === this.formulario.codigoDepartamento);
  }

  exigeDepartamento(): boolean {
    const nivel = this.formulario.nivelCobertura;
    return nivel === 'departamental' || nivel === 'municipal';
  }

  exigeMunicipio(): boolean {
    return this.formulario.nivelCobertura === 'municipal';
  }

  alCambiarNivel(): void {
    if (!this.exigeDepartamento()) {
      this.formulario.codigoDepartamento = '';
      this.formulario.codigoMunicipio = '';
      return;
    }
    if (!this.exigeMunicipio()) this.formulario.codigoMunicipio = '';
    this.programarGuardado();
    this.limpiarError('codigoDepartamento'); this.limpiarError('codigoMunicipio');
  }

  alCambiarDepartamento(): void {
    this.formulario.codigoMunicipio = '';
    this.limpiarError('codigoDepartamento'); this.limpiarError('codigoMunicipio');
    this.programarGuardado();
  }

  // ─────────────────────────── Catálogos (paso 3) ───────────────────────────

  practicaElegida(id: number): boolean {
    return this.formulario.practicasMusicalesIds.includes(id);
  }

  territorioElegido(id: number): boolean {
    return this.formulario.territoriosSonorosIds.includes(id);
  }

  alternarPractica(id: number): void {
    this.alternarEnLaLista(this.formulario.practicasMusicalesIds, id);
    this.programarGuardado();
  }

  alternarTerritorio(id: number): void {
    this.alternarEnLaLista(this.formulario.territoriosSonorosIds, id);
    this.programarGuardado();
  }

  private alternarEnLaLista(lista: number[], id: number): void {
    const indice = lista.indexOf(id);
    if (indice === -1) lista.push(id);
    else lista.splice(indice, 1);
  }

  // ─────────────────────────── Navegación entre pasos ───────────────────────────

  /**
   * «Atrás»/«Siguiente» siempre disponibles, sin bloqueo progresivo.
   *
   * MISMO CRITERIO QUE `validar()` en `FichaFestivalComponent`: ahí la comprobación es agregada, al
   * guardar, no campo a campo mientras se escribe. Aquí tampoco se impide avanzar por un campo sin
   * llenar -el paso 4 valida todo antes de guardar y, si algo falta, vuelve al paso que lo tiene-.
   */
  irAlPaso(id: number, desplazar = true): void {
    if (!this.pasos.some(paso => paso.id === id)) return;
    this.pasoActivo.set(id);
    // Es un único formulario: la URL no cambia y el desplazamiento se controla hacia el bloque
    // que acaba de aparecer. Así «Siguiente», «Atrás» y el indicador de pasos no llevan al inicio
    // del documento ni dejan el nuevo contenido por encima de la ventana.
    if (desplazar) this.desplazarAlPaso(id);
  }

  pasoAnterior(): void {
    if (this.pasoActivo() > 1) this.irAlPaso(this.pasoActivo() - 1);
  }

  pasoSiguiente(): void {
    if (this.pasoActivo() === 3 && !this.coincidenciasRevisadas()) {
      if (this.coincidencias().length) { this.abrirModalDeCoincidencias(); return; }
      this.buscarCoincidencias(true);
      return;
    }
    if (this.pasoActivo() < this.pasos.length) this.irAlPaso(this.pasoActivo() + 1);
  }

  continuarSinReclamar(): void {
    this.cerrarModalDeCoincidencias();
    this.coincidenciasRevisadas.set(true);
    this.irAlPaso(4);
  }

  nombresCatalogo(items: { nombre: string }[]): string {
    return items.map(item => item.nombre).join(', ');
  }

  abrirModalDeCoincidencias(): void {
    if (this.overflowOriginalDelDocumento === null) this.overflowOriginalDelDocumento = this.document.body.style.overflow;
    this.document.body.style.overflow = 'hidden';
    this.document.documentElement.style.overflow = 'hidden';
    this.modalCoincidenciasAbierto.set(true);
  }

  private cerrarModalDeCoincidencias(): void {
    this.modalCoincidenciasAbierto.set(false);
    this.desbloquearDesplazamiento();
  }

  private desbloquearDesplazamiento(): void {
    if (this.overflowOriginalDelDocumento === null) return;
    this.document.body.style.overflow = this.overflowOriginalDelDocumento;
    this.document.documentElement.style.overflow = this.overflowOriginalDelDocumento;
    this.overflowOriginalDelDocumento = null;
  }

  // ─────────────────────────── Guardar ───────────────────────────

  /**
   * Las mismas dos reglas de `FichaFestivalComponent.validar()`: `nombre` siempre obligatorio,
   * territorio obligatorio solo según `nivelCobertura`. No se inventa ninguna regla nueva.
   */
  private validar(): Record<string, string> {
    const errores: Record<string, string> = {};
    if (!(this.formulario.nombre ?? '').trim()) errores['nombre'] = 'Escribe el nombre del festival.';
    if (this.exigeDepartamento() && !this.formulario.codigoDepartamento) errores['codigoDepartamento'] = 'Elige un departamento.';
    if (this.exigeMunicipio() && !this.formulario.codigoMunicipio) errores['codigoMunicipio'] = 'Elige un municipio.';
    if (['otra_regular', 'intermitente'].includes(this.formulario.periodicidad) && !(this.formulario.periodicidadDetalle ?? '').trim())
      errores['periodicidadDetalle'] = 'Explica brevemente la periodicidad seleccionada.';
    if ((this.formulario.correoContacto ?? '').trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test((this.formulario.correoContacto ?? '').trim())) errores['correoContacto'] = 'Ingresa un correo electrónico válido.';
    for (const campo of ['paginaWeb', 'otroEnlace'] as const) {
      const valor = (this.formulario[campo] ?? '').trim();
      if (valor && !/^https?:\/\/[^\s]+$/i.test(valor)) errores[campo] = 'Ingresa una URL válida que comience por http:// o https://.';
    }
    return errores;
  }

  /** En cuál paso vive cada campo que puede fallar, para volver ahí si algo no pasó la validación. */
  private readonly PASO_DEL_CAMPO: Readonly<Record<string, number>> = {
    nombre: 1,
    periodicidad: 1,
    periodicidadDetalle: 1,
    codigoDepartamento: 2,
    codigoMunicipio: 2,
    correoContacto: 2,
    paginaWeb: 2,
    otroEnlace: 2,
  };

  alCambiarCampo(campo: string): void { this.limpiarError(campo); this.programarGuardado(); }

  alCambiarPeriodicidad(): void {
    if (!['otra_regular', 'intermitente'].includes(this.formulario.periodicidad)) this.formulario.periodicidadDetalle = '';
    this.alCambiarCampo('periodicidad');
  }

  private limpiarError(campo: string): void {
    this.errores.update(actual => { if (!actual[campo]) return actual; const siguiente = { ...actual }; delete siguiente[campo]; return siguiente; });
  }

  private construirSolicitud(): GuardarFestivalSolicitud {
    const exigeDepartamento = this.exigeDepartamento();
    const exigeMunicipio = this.exigeMunicipio();
    return {
      nombre: this.formulario.nombre.trim(),
      descripcion: vacioANulo(this.formulario.descripcion),
      correoContacto: vacioANulo(this.formulario.correoContacto),
      telefonoCelular: vacioANulo(this.formulario.telefonoCelular),
      instagram: vacioANulo(this.formulario.instagram),
      facebook: vacioANulo(this.formulario.facebook),
      paginaWeb: vacioANulo(this.formulario.paginaWeb),
      otroEnlace: vacioANulo(this.formulario.otroEnlace),
      observacionesContacto: vacioANulo(this.formulario.observacionesContacto),
      periodicidad: vacioANulo(this.formulario.periodicidad),
      periodicidadDetalle: vacioANulo(this.formulario.periodicidadDetalle),
      nivelCobertura: this.formulario.nivelCobertura,
      codigoDepartamento: exigeDepartamento ? vacioANulo(this.formulario.codigoDepartamento) : null,
      codigoMunicipio: exigeMunicipio ? vacioANulo(this.formulario.codigoMunicipio) : null,
      practicasMusicalesIds: [...this.formulario.practicasMusicalesIds],
      territoriosSonorosIds: [...this.formulario.territoriosSonorosIds],
    };
  }

  /**
   * Guarda el borrador. Sin ediciones: llama solo a `crearFestival()`, nunca a `crearVersion()` -el
   * asistente nunca pidió ninguno de los treinta y tres campos de una edición-.
   */
  guardarBorrador(): void {
    if (this.guardando()) return;
    const errores = this.validar();
    this.errores.set(errores);
    if (Object.keys(errores).length > 0) {
      const primerCampo = Object.keys(errores)[0];
      this.error.set('Revisa los campos marcados.');
      // El foco del campo inválido es el único que debe gobernar este desplazamiento. Si también
      // moviéramos el encabezado, dos tareas consecutivas competirían y producirían el salto que
      // se percibía al intentar guardar.
      this.irAlPaso(this.PASO_DEL_CAMPO[primerCampo] ?? 1, false);
      this.enfocar(`#asistente-${primerCampo === 'correoContacto' ? 'correo' : primerCampo === 'paginaWeb' ? 'pagina-web' : primerCampo === 'otroEnlace' ? 'otro-enlace' : primerCampo}`);
      return;
    }

    if (this.coincidencias().length > 0 && !this.coincidenciasRevisadas()) {
      this.abrirModalDeCoincidencias();
      return;
    }
    this.guardando.set(true);
    this.api.crearFestival(this.organizacionId(), {
      ...this.construirSolicitud(),
      borradorProcesoId: this.borradorId,
    }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.error.set('Festival registrado como borrador. Ya puedes completar sus ediciones cuando lo necesites.');
        this.router.navigate(['/gestion/procesos/festivales']);
      },
      error: fallo => {
        this.guardando.set(false);
        this.error.set(fallo?.message ?? 'No fue posible registrar el Festival.');
      },
    });
  }

  cancelar(): void {
    this.router.navigate(['/gestion/procesos/festivales']);
  }

  etiquetaCobertura(codigo: string | null | undefined): string {
    return this.niveles.find(nivel => nivel.codigo === codigo?.trim().toLowerCase())?.etiqueta ?? 'Sin definir';
  }

  private enfocar(selector: string): void {
    setTimeout(() => {
      const destino = this.host.nativeElement.querySelector<HTMLElement>(selector);
      if (!destino) return;
      destino.focus({ preventScroll: true });
      destino.scrollIntoView({ behavior: this.comportamientoDeDesplazamiento(), block: 'center' });
    });
  }

  private desplazarAlPaso(id: number): void {
    setTimeout(() => {
      const destino = this.host.nativeElement.querySelector<HTMLElement>(`#paso${id}-titulo`);
      destino?.scrollIntoView({ behavior: this.comportamientoDeDesplazamiento(), block: 'start' });
    });
  }

  private comportamientoDeDesplazamiento(): ScrollBehavior {
    return this.document.defaultView?.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      ? 'auto'
      : 'smooth';
  }
}
