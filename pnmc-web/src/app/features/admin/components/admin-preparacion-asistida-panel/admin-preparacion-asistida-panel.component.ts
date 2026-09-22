import { Component, Input, OnInit, signal, computed, ViewChild, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BuscadorDeListaComponent } from '../../../../shared/components/ui/buscador/buscador-de-lista.component';
import { IndicadorDeEstadoComponent } from '../../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FormsModule } from '@angular/forms';
import {
  LucideSparkles,
  LucideUpload,
  LucideDownload,
  LucideCheck,
  LucideArrowRight,
  LucideSearch,
  LucideFileSpreadsheet,
  LucideInfo,
  LucideFileText,
  LucideAlertTriangle,
  LucideRotateCcw,
  LucideSend,
  LucideRefreshCw
} from '@lucide/angular';
import { AdminField } from '../../domain/admin-config';
import { crearLibroExcel } from '../../../../shared/utils/crear-libro-excel';
import { firstValueFrom } from 'rxjs';
import { EstadoDeListaComponent } from '../../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ConfirmacionComponent } from '../../../../shared/components/ui/confirmacion/confirmacion.component';
import { IndicadorDePasosComponent } from '../../../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import {
  FilaDeImportacion,
  Importacion,
  ImportacionesService,
  DominioImportable,
  PaginaDeImportaciones,
  SolicitudDePrevisualizacion,
} from './importaciones.service';

/**
 * Cómo se llama cada campo en los archivos que llegan de fuera.
 *
 * <b>LAS CLAVES SON LOS CAMPOS QUE DECLARA EL SERVIDOR</b>, en español, y no los del formulario de
 * la consola. Hasta eran los nombres ingleses de `ADMIN_MODULES` y
 * había que traducirlos a los del contrato al enviar: dos vocabularios para lo mismo, y un dominio
 * nuevo habría traído un tercero.
 *
 * <b>SOLO CAMPOS QUE ALGUN DOMINIO DECLARA.</b> El emparejador busca `SINONIMOS[campo]` entre los
 * campos del dominio abierto, así que una clave que ningún dominio declara no puede emparejar
 * nunca: hacía creer que el asistente entendía columnas que no entiende.
 */
const SINONIMOS: Record<string, string[]> = {
  // ---------- Comunes a cualquier dominio ------------------------------------------------------
  nombre: ['nombre', 'name', 'titulo', 'título', 'entidad', 'razon', 'razón', 'denominacion', 'denominación', 'nombre del festival', 'nombre de la organizacion', 'nombre de la organización', 'festival', 'organizacion', 'organización'],
  descripcion: ['descripcion', 'descripción', 'resumen', 'detalle', 'summary', 'about', 'sobre', 'info', 'resumen general'],
  correoContacto: ['correo', 'email', 'mail', 'correo contacto', 'email contacto', 'mail contacto', 'correo electronico', 'correo electrónico', 'correo general', 'correo de contacto'],
  telefonoContacto: ['telefono', 'teléfono', 'celular', 'telefono contacto', 'contacto', 'tel', 'phone', 'mobile', 'teléfono contacto', 'celular contacto', 'teléfono general', 'telefono de contacto'],
  sitioWeb: ['sitio web', 'pagina web', 'pagina', 'página', 'url', 'web', 'website', 'sitio general'],
  instagram: ['instagram', 'ig', 'insta', 'instagram url', 'perfil instagram'],
  facebook: ['facebook', 'fb', 'facebook url', 'perfil facebook'],
  otroEnlace: ['otro enlace', 'otro link', 'red social', 'enlace', 'link', 'otros links', 'enlaces'],

  // ---------- Festivales ------------------------------------------------------------------------
  nivelCobertura: ['nivel de cobertura', 'cobertura', 'alcance', 'nivel', 'ambito', 'ámbito'],
  departamento: ['departamento', 'depto', 'dpto', 'dept', 'departament', 'provincia', 'estado', 'dep', 'departamento pnmc'],
  municipio: ['municipio', 'muni', 'ciudad', 'pueblo', 'municipality', 'city', 'mun', 'localidad', 'municipio pnmc'],
  periodicidad: ['periodicidad', 'frecuencia', 'cada cuanto'],
  periodicidadDetalle: ['detalle periodicidad', 'detalle de periodicidad', 'otra periodicidad', 'frecuencia detalle'],

  // ---------- Organizaciones --------------------------------------------------------------------
  nombreLegal: ['razon social', 'razón social', 'nombre legal', 'denominacion legal', 'denominación legal'],
  identificacion: ['nit', 'identificacion', 'identificación', 'documento', 'numero de identificacion', 'número de identificación', 'rut', 'cedula', 'cédula'],
  tipoIdentificacion: ['tipo de identificacion', 'tipo de identificación', 'tipo documento', 'tipo de documento'],
  departamentoSede: ['departamento', 'departamento sede', 'departamento de la sede', 'depto', 'dpto', 'dep'],
  municipioSede: ['municipio', 'municipio sede', 'municipio de la sede', 'ciudad', 'muni', 'localidad'],
  direccion: ['direccion', 'dirección', 'domicilio', 'sede', 'address'],
};


const limpiarTexto = (str = '') => {
  return String(str)
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
    .trim();
};

@Component({
  selector: 'app-admin-preparacion-asistida-panel',
  standalone: true,
  imports: [
    EstadoDeListaComponent,
    BuscadorDeListaComponent,
    CommonModule,
    FormsModule,
    LucideSparkles,
    LucideUpload,
    LucideDownload,
    LucideCheck,
    LucideArrowRight,
    LucideSearch,
    LucideFileSpreadsheet,
    LucideInfo,
    LucideFileText,
    LucideAlertTriangle,
    LucideRotateCcw,
    LucideSend,
    LucideRefreshCw, IndicadorDeEstadoComponent, ConfirmacionComponent, IndicadorDePasosComponent],
  templateUrl: './admin-preparacion-asistida-panel.component.html',
  styleUrls: ['./admin-preparacion-asistida-panel.component.css']
})
export class AdminPreparacionAsistidaPanelComponent implements OnInit {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private readonly importaciones = inject(ImportacionesService);

  @Input() divipola: any = {};

  /**
   * EL MÓDULO YA VIENE DECIDIDO POR EL CONTEXTO. Se define mover
   * «Importaciones» de un ítem de navegación aparte a una acción dentro de cada módulo -«Ecosistema
   * → Festivales → Importar Festivales»-: quien llega aquí ya eligió el módulo al pulsar ese botón,
   * así que no hace falta volver a preguntarlo. Sin este input -uso suelto, si alguna vez lo hay- el
   * selector del Paso 1 se sigue mostrando, igual que antes.
   */
  @Input() idModulo?: string;

  @ViewChild('finConsultasRef') finConsultasRef!: ElementRef;
  @ViewChild('archivoFuenteRef') archivoFuenteRef!: ElementRef;

  // Icons
  LucideSparkles = LucideSparkles;
  LucideUpload = LucideUpload;
  LucideDownload = LucideDownload;
  LucideCheck = LucideCheck;
  LucideArrowRight = LucideArrowRight;
  LucideSearch = LucideSearch;
  LucideFileSpreadsheet = LucideFileSpreadsheet;
  LucideInfo = LucideInfo;
  LucideFileText = LucideFileText;
  LucideAlertTriangle = LucideAlertTriangle;
  LucideRotateCcw = LucideRotateCcw;
  LucideSend = LucideSend;

  paso = signal<number>(1);
  idModuloSeleccionado = signal<string>('');

  archivoFuente = signal<File | null>(null);
  encabezadosFuente = signal<string[]>([]);
  filasFuente = signal<any[][]>([]);

  correspondencias = signal<Record<string, number>>({});
  sugerenciasDeCorrespondencia = signal<Record<string, any>>({});

  procesando = signal<boolean>(false);

  // Dos defectos de este panel, arreglados aqui:
  //
  // 1) «Reiniciar» borraba de un solo clic el archivo cargado, el modulo de
  //    destino, el mapeo manual de columnas y las filas ya procesadas, sin
  //    preguntar y sin deshacer. Ahora `reiniciar` confirma nombrando QUE se
  //    pierde (archivo, filas leidas, filas procesadas), no un «¿estas seguro?»
  //    generico. El resto de acciones del panel (avanzar de paso, volver,
  //    procesar, descargar) no destruyen nada y NO se confirman a proposito:
  //    una confirmacion de mas ensena a pulsar «Aceptar» sin leer.
  //
  // 2) Varios botones lanzaban su accion sin bloquearse mientras estaba en
  //    vuelo, asi que un doble clic la ejecutaba dos veces. Estas senales se
  //    ponen a true antes de la accion y a false en TODAS las salidas (exito,
  //    error y retornos tempranos), y se enlazan al [disabled] del boton.
  //    «Aplicar reglas de preparación» no estrena senal: reutiliza la de `procesando`,
  //    que ya existia; solo faltaba enlazarla y liberarla en el retorno temprano.
  /**
   * El aviso del panel, donde antes había un `alert()` del navegador.
   *
   * <b>SEIS `alert()` VIVIAN AQUI</b> —plantilla que no baja, archivo vacío, archivo ilegible, falta
   * el módulo, falta el archivo, Excel que no se genera—. Un `alert()` para la página entera, se
   * planta encima de lo que la persona estaba mirando y, sobre todo, dice el problema LEJOS del
   * control que lo causó: «Por favor selecciona un módulo de destino» aparecía en el centro de la
   * pantalla y el selector quedaba detrás. Aquí el aviso vive en el panel, se anuncia con
   * `role="alert"` y se va solo en cuanto la acción siguiente tiene éxito.
   */
  readonly avisoDelPanel = signal<string>('');

  /** Lo que espera confirmación: reiniciar la preparación o crear los borradores. */
  readonly confirmacion = signal<'reiniciar' | 'importar' | null>(null);

  reiniciando = signal<boolean>(false);
  descargandoExcel = signal<boolean>(false);
  descargandoPlantilla = signal<boolean>(false);
  previsualizandoImportacion = signal<boolean>(false);
  confirmandoImportacion = signal<boolean>(false);
  previsualizacionImportacion = signal<Importacion | null>(null);
  idsFilasExcluidas = signal<readonly number[]>([]);
  mensajeImportacion = signal<string>('');
  errorImportacion = signal<string>('');
  mostrarHistorial = signal<boolean>(false);
  cargandoHistorial = signal<boolean>(false);
  cargandoDetalleHistorial = signal<boolean>(false);
  historialImportaciones = signal<PaginaDeImportaciones | null>(null);
  detalleHistorial = signal<Importacion | null>(null);
  estadoHistorial = signal<string>('');
  archivoHistorial = signal<string>('');
  errorHistorial = signal<string>('');

  filasProcesadas = signal<any[]>([]);
  observaciones = signal<any[]>([]);
  estadisticas = signal<{ total: number; advertencias: number; normalizaciones: number; filasPreparadas: number }>({
    total: 0,
    advertencias: 0,
    normalizaciones: 0,
    filasPreparadas: 0,
  });

  mensajesDeConsulta = signal<{ emisor: 'sistema' | 'usuario'; texto: string }[]>([
    {
      emisor: 'sistema',
      texto: 'Esta preparación funciona con reglas locales. Carga un archivo Excel o CSV y selecciona el módulo cuyo formato necesitas.\n\nDespués podrás consultar conteos, advertencias o coincidencias dentro del archivo cargado.'
    }
  ]);
  consulta = signal<string>('');

  /**
   * Los dominios que el SERVIDOR dice que sabe recibir.
   *
   * <b>LA LISTA YA NO SE LLEVA ESCRITA AQUI.</b> Estaba fija en «solo Festivales», y antes de eso
   * anunciaba ocho módulos de los que siete no tenían circuito: quien elegía cualquiera de ellos
   * llegaba a un destino que el servidor no sabía recibir. Desde la
   * Importación Asistida es una capacidad con su propio contrato —ver `ReglasDeImportacion` en el
   * API— y esta pantalla pregunta en vez de suponer. El día que se despliegue un dominio nuevo,
   * aparece aquí sin tocar el navegador.
   */
  readonly dominiosImportables = signal<DominioImportable[]>([]);

  /**
   * La correspondencia entre el identificador del dominio en el API y el del módulo de la consola.
   *
   * EXISTE PORQUE LOS DOS VOCABULARIOS YA EXISTIAN: `ADMIN_MODULES` nombra sus módulos en inglés
   * desde el diseño de referencia y el API los nombra en español, como todo lo nuestro. Se traduce
   * en un solo sitio y a la vista, en vez de repartir la coincidencia por la pantalla.
   */
  /**
   * El único puente entre los dos vocabularios que ya existían.
   *
   * `ADMIN_MODULES` nombra sus módulos en inglés desde el diseño de referencia y el API nombra sus
   * dominios en español, como todo lo nuestro. La correspondencia solo hace falta al entrar desde
   * la cabecera de un módulo —«Importar Festivales»—; de ahí en adelante manda el dominio.
   */
  private static readonly DOMINIO_DE_MODULO: Record<string, string> = { festivals: 'festivales' };

  /**
   * Los destinos que se pueden elegir: exactamente los que el servidor declara.
   *
   * <b>YA NO SE FILTRAN DE `ADMIN_MODULES`.</b> Aquella lista describe los módulos de la consola y
   * no tiene por qué coincidir con lo que la importación sabe recibir: cuando coincidían era por
   * casualidad, y cuando dejaron de hacerlo esta pantalla anunció ocho destinos de los que siete no
   * existían. El servidor es el único que sabe la respuesta, así que es el único que la da.
   */
  readonly modulosAdministrativos = this.dominiosImportables;

  /**
   * Cómo se resume una fila previsualizada en una línea, sin saber de qué dominio es.
   *
   * SE TOMAN LOS PRIMEROS CAMPOS DECLARADOS CON VALOR. La pantalla enseñaba el nombre, el nivel de
   * cobertura y el municipio —tres campos de Festival— y con un dominio distinto habría quedado en
   * blanco. El primero declarado es siempre el nombre; los dos siguientes con dato sitúan el
   * registro sin que la pantalla tenga que conocer ninguno.
   */
  resumenDeFila(fila: FilaDeImportacion): string {
    return this.camposActivos()
      .slice(1)
      .map(campo => fila.datos[campo.name])
      .filter((valor): valor is string => Boolean(valor?.trim()))
      .slice(0, 2)
      .join(' · ');
  }

  /** El nombre de la fila: el primer campo que el dominio declara, que siempre es su identidad. */
  nombreDeFila(fila: FilaDeImportacion): string {
    const primero = this.camposActivos()[0]?.name;
    return (primero ? fila.datos[primero] : null) ?? 'Sin nombre';
  }

  /** El dominio del API que corresponde a un módulo de la consola, o el propio valor si ya lo es. */
  private dominioDe(modulo: string): string {
    return AdminPreparacionAsistidaPanelComponent.DOMINIO_DE_MODULO[modulo] ?? modulo;
  }

  /** Lo que el servidor promete sobre el dominio elegido: campos, estado al importar y por qué. */
  readonly promesaDelDominio = computed(() =>
    this.dominiosImportables().find(item => item.dominio === this.idModuloSeleccionado()) ?? null);

  /** El dominio abierto. Es el mismo objeto que la promesa: no hay dos fuentes para lo mismo. */
  moduloActivo = computed(() => this.promesaDelDominio());

  /**
   * Los campos del dominio elegido, tal como los declara el servidor.
   *
   * <b>YA NO SALEN DE `ADMIN_MODULES`.</b> Aquella lista describe el FORMULARIO del módulo en la
   * consola —en inglés, con tipos, anchos y opciones— y se filtraba a mano con un conjunto escrito
   * aquí para quedarse con los trece que la importación de Festivales entendía. Eran dos
   * declaraciones del mismo contrato en dos idiomas, y añadir un dominio habría añadido una
   * tercera. Ahora el contrato lo declara quien lo va a recibir.
   */
  camposActivos = computed<AdminField[]>(() => (this.promesaDelDominio()?.campos ?? []).map(campo => ({
    name: campo.nombre,
    label: campo.etiqueta,
    type: 'text',
    required: campo.obligatorio,
  })));

  cantidadElegidaParaImportar = computed(() => {
    const vista = this.previsualizacionImportacion();
    if (!vista?.puedeConfirmar) return 0;
    const excluidas = new Set(this.idsFilasExcluidas());
    return vista.filas.filter(fila => fila.puedeImportarse && !excluidas.has(fila.id)).length;
  });

  ngOnInit() {
    if (this.idModulo) this.idModuloSeleccionado.set(this.dominioDe(this.idModulo));
    this.desplazarAlFinal();
    // SE PREGUNTA QUE SE PUEDE IMPORTAR. Si el servidor no contesta, la pantalla no inventa una
    // lista: se queda sin destinos y lo dice, que es preferible a ofrecer uno que no existe.
    this.importaciones.dominiosImportables().subscribe({
      next: respuesta => this.dominiosImportables.set(respuesta.dominios ?? []),
      error: () => this.dominiosImportables.set([]),
    });
  }

  async alternarHistorial(): Promise<void> {
    const visible = !this.mostrarHistorial();
    this.mostrarHistorial.set(visible);
    if (visible && !this.historialImportaciones()) await this.cargarHistorial(1);
  }

  /** Buscar en el historial: guarda el nombre y consulta la primera página. */
  buscarEnElHistorial(texto: string): void {
    this.archivoHistorial.set(texto);
    void this.cargarHistorial(1);
  }

  async cargarHistorial(pagina = 1): Promise<void> {
    if (this.cargandoHistorial()) return;
    this.cargandoHistorial.set(true);
    this.errorHistorial.set('');
    try {
      const resultado = await firstValueFrom(this.importaciones.listar(this.idModuloSeleccionado(), {
        pagina,
        tamano: 10,
        estado: this.estadoHistorial(),
        archivo: this.archivoHistorial(),
      }));
      this.historialImportaciones.set(resultado);
      if (resultado.items.every(item => item.id !== this.detalleHistorial()?.id)) this.detalleHistorial.set(null);
    } catch (error: unknown) {
      this.errorHistorial.set(this.mensajeDeError(error, 'No fue posible consultar el historial.'));
    } finally {
      this.cargandoHistorial.set(false);
    }
  }

  async consultarLoteHistorial(id: number): Promise<void> {
    if (this.cargandoDetalleHistorial()) return;
    this.cargandoDetalleHistorial.set(true);
    this.errorHistorial.set('');
    try {
      this.detalleHistorial.set(await firstValueFrom(
        this.importaciones.consultar(this.idModuloSeleccionado(), id)));
    } catch (error: unknown) {
      this.errorHistorial.set(this.mensajeDeError(error, 'No fue posible abrir el lote.'));
    } finally {
      this.cargandoDetalleHistorial.set(false);
    }
  }

  cerrarDetalleHistorial(): void {
    this.detalleHistorial.set(null);
  }

  retomarLoteHistorial(): void {
    const lote = this.detalleHistorial();
    if (!lote?.puedeConfirmar) return;
    this.previsualizacionImportacion.set(lote);
    this.idsFilasExcluidas.set([]);
    // LAS FILAS VUELVEN TAL COMO EL DOMINIO LAS PROYECTO. Antes se rearmaban campo a campo con los
    // nombres ingleses del formulario de Festival, así que retomar un lote de cualquier otro
    // dominio habría devuelto trece columnas vacías.
    this.filasProcesadas.set(lote.filas.map(fila => ({ ...fila.datos })));
    this.paso.set(3);
    this.mostrarHistorial.set(false);
    this.detalleHistorial.set(null);
  }

  etiquetaEstadoImportacion(estado: Importacion['estado']): string {
    return ({
      previsualizado: 'Pendiente de confirmación',
      aplicando: 'Aplicando',
      aplicado: 'Aplicada',
      depurando: 'Depurando',
      expirado: 'Expirada',
      conflicto: 'Con conflicto',
    } as const)[estado];
  }

  etiquetaRetencion(estado: Importacion['estadoRetencion']): string {
    return ({
      detalle_temporal: 'Detalle temporal',
      datos_duplicados_minimizados: 'Datos duplicados minimizados',
      pendiente_minimizacion: 'Pendiente de minimización',
      detalle_depurado: 'Detalle retirado',
      en_proceso: 'En proceso',
    } as const)[estado];
  }

  desplazarAlFinal() {
    setTimeout(() => {
      this.finConsultasRef?.nativeElement?.scrollIntoView({ behavior: 'smooth' });
    }, 100);
  }

  agregarMensajeDelSistema(texto: string) {
    this.mensajesDeConsulta.update(anteriores => [...anteriores, { emisor: 'sistema', texto }]);
    this.desplazarAlFinal();
  }

  enviarConsulta() {
    const mensaje = this.consulta().trim();
    if (!mensaje) return;

    this.mensajesDeConsulta.update(anteriores => [...anteriores, { emisor: 'usuario', texto: mensaje }]);
    this.consulta.set('');
    this.desplazarAlFinal();

    let respuesta: string;
    const mensajeNormalizado = limpiarTexto(mensaje);

    if (this.filasFuente().length === 0) {
      respuesta = 'Aún no hay un archivo cargado. Sube un archivo en el Paso 1 y selecciona el formato de destino para consultar sus registros localmente.';
    } else if (mensajeNormalizado.includes('registros') || mensajeNormalizado.includes('filas') || mensajeNormalizado.includes('cantidad') || mensajeNormalizado.includes('cuantos') || mensajeNormalizado.includes('cuantas')) {
      if (this.paso() < 3) {
        respuesta = `Actualmente tienes cargados **${this.filasFuente().length} registros** en espera de ser mapeados y procesados. Avanza al Paso 3 para ver la limpieza de datos detallada.`;
      } else {
        respuesta = `Las reglas locales procesaron **${this.estadisticas().total} registros** para el formato **${this.moduloActivo()?.etiqueta}**.\n- Mapeados: ${this.estadisticas().filasPreparadas}\n- Normalizaciones aplicadas: ${this.estadisticas().normalizaciones}\n- Advertencias: ${this.estadisticas().advertencias}`;
      }
    } else if (mensajeNormalizado.includes('error') || mensajeNormalizado.includes('advertencia') || mensajeNormalizado.includes('alerta') || mensajeNormalizado.includes('fallo') || mensajeNormalizado.includes('problema')) {
      if (this.paso() < 3) {
        respuesta = 'La comprobación de advertencias se ejecuta en el Paso 3, una vez definido el mapeo de columnas.';
      } else if (this.estadisticas().advertencias === 0) {
        respuesta = 'No se encontraron advertencias en el archivo preparado. Los campos obligatorios están diligenciados y el mapeo está completo.';
      } else {
        const muestras = this.observaciones().filter(observacion => observacion.type === 'warning').slice(0, 3);
        respuesta = `Se encontraron **${this.estadisticas().advertencias} advertencias**. Estas son algunas muestras:\n\n` +
          muestras.map(observacion => `• ${observacion.message}`).join('\n') +
          (this.estadisticas().advertencias > 3 ? `\n\n...y otras ${this.estadisticas().advertencias - 3} advertencias adicionales que puedes consultar en la sección inferior de observaciones.` : '');
      }
    } else if (mensajeNormalizado.includes('correccion') || mensajeNormalizado.includes('limpi') || mensajeNormalizado.includes('cambio') || mensajeNormalizado.includes('normaliz')) {
      if (this.paso() < 3) {
        respuesta = 'Las correcciones de formato de correos, teléfonos y normalización DIVIPOLA se aplicarán en el Paso 3 tras definir el mapeo.';
      } else if (this.estadisticas().normalizaciones === 0) {
        respuesta = 'No se requirió realizar ninguna corrección automática. Los datos de teléfonos, correos y departamentos venían con el formato correcto.';
      } else {
        const muestras = this.observaciones().filter(observacion => observacion.type === 'correction').slice(0, 3);
        respuesta = `Las reglas aplicaron **${this.estadisticas().normalizaciones} normalizaciones**. Algunos ejemplos:\n\n` +
          muestras.map(observacion => `• ${observacion.message}`).join('\n') +
          (this.estadisticas().normalizaciones > 3 ? `\n\n...y otras ${this.estadisticas().normalizaciones - 3} modificaciones automáticas registradas.` : '');
      }
    } else if (mensajeNormalizado.length >= 3) {
      const termino = mensajeNormalizado;
      const coincidencias: any[] = [];

      if (this.paso() === 3 && this.filasProcesadas().length > 0) {
        this.filasProcesadas().forEach((row, idx) => {
          const matchedFields: string[] = [];
          Object.entries(row).forEach(([key, val]) => {
            if (limpiarTexto(String(val)).includes(termino)) {
              const fieldLabel = this.camposActivos().find(f => f.name === key)?.label || key;
              matchedFields.push(`**${fieldLabel}**: "${val}"`);
            }
          });

          if (matchedFields.length > 0) {
            const nameVal = row.name || row.title || `Fila ${idx + 2}`;
            coincidencias.push({ rowNum: idx + 2, name: nameVal, details: matchedFields.join(', ') });
          }
        });
      } else {
        this.filasFuente().forEach((row, idx) => {
          const matchedVals: string[] = [];
          row.forEach((val, colIdx) => {
            if (limpiarTexto(String(val)).includes(termino)) {
              matchedVals.push(`**Columna ${this.encabezadosFuente()[colIdx]}**: "${val}"`);
            }
          });
          if (matchedVals.length > 0) {
            coincidencias.push({ rowNum: idx + 2, name: `Fila ${idx + 2}`, details: matchedVals.join(', ') });
          }
        });
      }

      if (coincidencias.length === 0) {
        respuesta = `No se encontró ninguna fila que contenga el término "${mensaje}" en los datos cargados.`;
      } else {
        const cantidad = coincidencias.length;
        const muestras = coincidencias.slice(0, 4);
        respuesta = `Se encontraron **${cantidad} coincidencias** para "${mensaje}":\n\n` +
          muestras.map(muestra => `• [Fila ${muestra.rowNum}] **${muestra.name}** (${muestra.details})`).join('\n') +
          (cantidad > 4 ? `\n\n...y otras ${cantidad - 4} filas coincidentes.` : '');
      }
    } else {
      respuesta = 'Puedes consultar conteos y advertencias, o buscar un término específico dentro del archivo cargado.';
    }

    this.agregarMensajeDelSistema(respuesta);
  }

  async descargarPlantillaVacia() {
    const mod = this.moduloActivo();
    if (!mod) return;
    if (this.descargandoPlantilla()) return;
    this.descargandoPlantilla.set(true);
    try {
      const fields = this.camposActivos();
      const workbook = await crearLibroExcel();
      workbook.creator = 'Entorno Virtual PNMC';
      workbook.created = new Date();

      const templateSheet = workbook.addWorksheet('Plantilla', {
        views: [{ state: 'frozen', ySplit: 1 }],
      });

      templateSheet.addRow(fields.map(f => f.label));
      templateSheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
      templateSheet.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF291242' } };
      templateSheet.getRow(1).alignment = { vertical: 'middle', wrapText: true };
      templateSheet.getRow(1).height = 30;

      templateSheet.columns = fields.map(f => ({ width: Math.max(18, String(f.label || '').length + 4) }));

      const buffer = await workbook.xlsx.writeBuffer();
      const blob = new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `plantilla_${mod.dominio}.xlsx`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error(err);
      this.avisar('No fue posible descargar la plantilla vacía. Inténtalo de nuevo.');
    } finally {
      this.descargandoPlantilla.set(false);
    }
  }

  cargarArchivoFuente(e: any) {
    const file = e.target.files?.[0];
    if (!file) return;

    this.limpiarPrevisualizacionImportacion();

    this.archivoFuente.set(file);
    const isCsv = file.name.endsWith('.csv');
    const reader = new FileReader();

    reader.onload = async (evt: any) => {
      try {
        let rawLines: any[][] = [];
        if (isCsv) {
          const text = evt.target.result || '';
          rawLines = this.interpretarCSV(text);
        } else {
          const buffer = evt.target.result as ArrayBuffer;
          const workbook = await crearLibroExcel();
          await workbook.xlsx.load(buffer);
          const worksheet = workbook.worksheets[0];
          worksheet.eachRow({ includeEmpty: true }, (row) => {
            const values = Array.isArray(row.values)
              ? row.values.slice(1).map(val => val === null || val === undefined ? '' : String(val))
              : [];
            rawLines.push(values);
          });
        }

        if (rawLines.length === 0) {
          this.avisar('El archivo que cargaste no tiene ninguna fila.');
          return;
        }

        const headers = rawLines[0].map(h => String(h || '').trim());
        const rows = rawLines.slice(1);

        this.encabezadosFuente.set(headers);
        this.filasFuente.set(rows);

        // Se proponen correspondencias por similitud entre encabezados y campos.
        this.proponerCorrespondencias(headers);

        this.agregarMensajeDelSistema(`Archivo externo "${file.name}" cargado con **${rows.length} registros** y **${headers.length} columnas**.`);
      } catch (err: any) {
        console.error(err);
        this.avisar(`No fue posible leer el archivo: ${err.message}`);
      }
    };

    if (isCsv) {
      reader.readAsText(file, 'UTF-8');
    } else {
      reader.readAsArrayBuffer(file);
    }
  }

  interpretarCSV(text: string): any[][] {
    const lines: any[][] = [];
    let row: string[] = [""];
    let insideQuote = false;
    for (let i = 0; i < text.length; i++) {
      const char = text[i];
      const nextChar = text[i + 1];
      if (char === '"') {
        if (insideQuote && nextChar === '"') {
          row[row.length - 1] += '"';
          i++;
        } else {
          insideQuote = !insideQuote;
        }
      } else if (char === ',' && !insideQuote) {
        row.push("");
      } else if ((char === '\r' || char === '\n') && !insideQuote) {
        if (char === '\r' && nextChar === '\n') {
          i++;
        }
        lines.push(row);
        row = [""];
      } else {
        row[row.length - 1] += char;
      }
    }
    if (row.length > 1 || row[0] !== "") {
      lines.push(row);
    }
    return lines;
  }

  proponerCorrespondencias(headers: string[]) {
    const mod = this.moduloActivo();
    if (!mod) return;

    const newMappings: Record<string, number> = {};
    const suggestions: Record<string, any> = {};
    const fields = this.camposActivos();

    fields.forEach(field => {
      const fName = field.name;
      const fLabel = field.label;
      const cleanFName = limpiarTexto(fName);
      const cleanFLabel = limpiarTexto(fLabel);

      let bestIndex = -1;
      let highestScore = 0;
      let matchType = 'Sin asignar';

      headers.forEach((header, index) => {
        const cleanHeader = limpiarTexto(header);
        let score = 0;

        if (cleanHeader === cleanFName || cleanHeader === cleanFLabel) {
          score = 100;
        }
        else if (SINONIMOS[fName]?.some(syn => limpiarTexto(syn) === cleanHeader)) {
          score = 90;
        }
        else if (cleanHeader.includes(cleanFName) || cleanFName.includes(cleanHeader) ||
                 cleanHeader.includes(cleanFLabel) || cleanFLabel.includes(cleanHeader)) {
          score = 60;
        }
        else if (SINONIMOS[fName]?.some(syn => cleanHeader.includes(limpiarTexto(syn)) || limpiarTexto(syn).includes(cleanHeader))) {
          score = 50;
        }

        if (score > highestScore) {
          highestScore = score;
          bestIndex = index;
        }
      });

      if (highestScore >= 90) {
        matchType = 'Coincidencia exacta';
      } else if (highestScore >= 50) {
        matchType = 'Sugerencia';
      }

      newMappings[fName] = bestIndex;
      suggestions[fName] = { score: highestScore, label: bestIndex !== -1 ? headers[bestIndex] : '', type: matchType };
    });

    this.correspondencias.set(newMappings);
    this.sugerenciasDeCorrespondencia.set(suggestions);
  }

  /** Los tres pasos, con el rótulo que se lee en el indicador compartido. */
  readonly pasosDeLaPreparacion = [
    { id: 1, titulo: 'Archivo y módulo' },
    { id: 2, titulo: 'Correspondencia de columnas' },
    { id: 3, titulo: 'Revisión y confirmación' },
  ];

  /**
   * Volver a un paso ya recorrido.
   *
   * SOLO HACIA ATRAS. Saltar hacia delante desde el indicador se saltaría las comprobaciones que
   * cada paso hace al avanzar —que haya módulo, que haya archivo, que el mapeo esté hecho— y
   * dejaría la pantalla pidiendo confirmación de algo que todavía no se ha preparado.
   */
  volverAlPaso(id: number): void {
    if (id < this.paso()) { this.paso.set(id); }
  }

  /** Deja el aviso a la vista del panel, donde estaba el control que lo causó. */
  private avisar(texto: string): void { this.avisoDelPanel.set(texto); }

  cambiarCorrespondencia(fieldName: string, headerIndex: number) {
    this.correspondencias.update(prev => ({
      ...prev,
      [fieldName]: headerIndex
    }));
    this.limpiarPrevisualizacionImportacion();
  }

  obtenerSugerencia(fieldName: string): { score: number; label: string; type: string } {
    return this.sugerenciasDeCorrespondencia()[fieldName] || { score: 0, label: '', type: 'Sin asignar' };
  }

  reiniciar() {
    if (this.reiniciando()) return;
    this.confirmacion.set('reiniciar');
  }

  /**
   * Lo que se pierde al reiniciar, nombrado por su cifra.
   *
   * SE DICE QUE SE PIERDE, NO «¿ESTAS SEGURO?». Quien lleva media hora mapeando columnas necesita
   * leer cuántas filas se van, no una pregunta genérica que no distingue este caso de ningún otro.
   */
  readonly detalleDelReinicio = computed(() => {
    const partes: string[] = [];
    const archivo = this.archivoFuente()?.name;
    if (archivo) { partes.push(`el archivo «${archivo}»`); }
    if (this.filasFuente().length) { partes.push(`${this.filasFuente().length} filas leídas`); }
    if (this.filasProcesadas().length) { partes.push(`${this.filasProcesadas().length} filas ya procesadas`); }
    const lista = partes.length ? `Se descartan ${partes.join(', ')}, el módulo de destino y el mapeo manual de columnas. `
      : 'Se descartan el archivo cargado, el módulo de destino y el mapeo manual de columnas. ';
    return `${lista}Tendrás que volver a subir el archivo y rehacer el mapeo, y no se puede deshacer desde el panel.`;
  });

  private reiniciarDeVerdad(): void {
    this.reiniciando.set(true);
    this.confirmacion.set(null);
    this.avisoDelPanel.set('');
    this.paso.set(1);
    this.archivoFuente.set(null);
    this.encabezadosFuente.set([]);
    this.filasFuente.set([]);
    this.correspondencias.set({});
    this.idModuloSeleccionado.set('');
    this.filasProcesadas.set([]);
    this.observaciones.set([]);
    this.limpiarPrevisualizacionImportacion();
    this.idModuloSeleccionado.set(this.idModulo ? this.dominioDe(this.idModulo) : '');
    this.agregarMensajeDelSistema('Se restablecieron los datos. Selecciona el módulo y carga el archivo para comenzar de nuevo.');
    this.reiniciando.set(false);
  }

  avanzarAMapeo() {
    if (!this.idModuloSeleccionado()) {
      this.avisar('Elige primero a qué módulo va el archivo.');
      return;
    }
    if (this.encabezadosFuente().length === 0) {
      this.avisar('Carga primero el archivo que vas a preparar.');
      return;
    }
    this.paso.set(2);
    // Se recalculan las sugerencias por si cambió el módulo.
    this.proponerCorrespondencias(this.encabezadosFuente());
    this.agregarMensajeDelSistema(`Paso 2: las reglas enlazaron las columnas sugeridas para **${this.moduloActivo()?.etiqueta}**. Revisa y ajusta las correspondencias antes de continuar.`);
  }

  prepararDatos() {
    if (this.procesando()) return;
    this.procesando.set(true);
    this.paso.set(3);
    this.limpiarPrevisualizacionImportacion();

    // `try/finally`: sin esto, cualquier excepcion dentro del procesado
    // —recorrer filas, normalizar DIVIPOLA, leer datos del fichero subido—
    // dejaba `procesando` encendida PARA SIEMPRE. Antes de enlazar el
    // `[disabled]` eso no se notaba; ahora el boton quedaria muerto sin mas
    // salida que recargar la pagina. El escenario es alcanzable: `paso.set(3)`
    // ya corrio, el usuario ve el paso vacio, pulsa «Volver a Mapeo» y se
    // encuentra el boton apagado.
  try {
      const mod = this.moduloActivo();
      if (!mod) {
        return;
      }

      const fields = this.camposActivos();
      const results: any[] = [];
      const newObs: any[] = [];
      // Los conteos de avisos y correcciones se derivan de newObs mas abajo.

      const canonicalDepts: Record<string, string> = {};
      const canonicalMunis: Record<string, Set<string>> = {};
      const cleanMuniToOriginal: Record<string, string> = {};

      Object.entries(this.divipola || {}).forEach(([dept, munis]) => {
        const cleanDept = limpiarTexto(dept);
        canonicalDepts[cleanDept] = dept;

        canonicalMunis[cleanDept] = new Set((munis as string[] || []).map(limpiarTexto));
        (munis as string[] || []).forEach(muni => {
          const cleanM = limpiarTexto(muni);
          cleanMuniToOriginal[`${cleanDept}::${cleanM}`] = muni;
        });
      });

      this.filasFuente().forEach((row, rowIndex) => {
        const recordIndex = rowIndex + 2;
        const mappedRow: Record<string, string> = {};
        const rowObs: any[] = [];

        // Aplicar las correspondencias de columnas.
        fields.forEach(field => {
          const sourceIdx = this.correspondencias()[field.name];
          let value = sourceIdx !== undefined && sourceIdx !== -1 ? row[sourceIdx] : '';

          if (value === undefined || value === null) {
            value = '';
          }

          mappedRow[field.name] = String(value).trim();
        });

        // 1. Normalizar correos.
        fields.forEach(field => {
          if (field.type === 'email' && mappedRow[field.name]) {
            const rawVal = mappedRow[field.name];
            const cleanVal = rawVal.toLowerCase().replace(/\s+/g, '');
            if (rawVal !== cleanVal) {
              mappedRow[field.name] = cleanVal;
              rowObs.push({
                type: 'correction',
                message: `Fila ${recordIndex}: Se depuraron espacios/mayúsculas del correo en "${field.label}" ("${rawVal}" ➔ "${cleanVal}").`
              });
            }
            if (!cleanVal.includes('@') || !cleanVal.includes('.')) {
              rowObs.push({
                type: 'warning',
                message: `Fila ${recordIndex}: El correo en "${field.label}" ("${cleanVal}") no tiene un formato válido.`
              });
            }
          }
        });

        // 2. Normalizar teléfonos.
        fields.forEach(field => {
          const isPhone = field.name.toLowerCase().includes('phone') || field.name.toLowerCase().includes('tel');
          if (isPhone && mappedRow[field.name]) {
            const rawVal = mappedRow[field.name];
            const cleanVal = rawVal.replace(/[\s\-.()]/g, '');
            if (rawVal !== cleanVal) {
              mappedRow[field.name] = cleanVal;
              rowObs.push({
                type: 'correction',
                message: `Fila ${recordIndex}: Se limpiaron caracteres especiales del teléfono "${field.label}" ("${rawVal}" ➔ "${cleanVal}").`
              });
            }
          }
        });

        // 3. Validar departamento y municipio contra DIVIPOLA.
        const hasDept = 'department' in mappedRow;
        const hasMuni = 'municipality' in mappedRow;

        if (hasDept && mappedRow['department']) {
          const rawDept = mappedRow['department'];
          const normDept = limpiarTexto(rawDept);
          const matchedDept = canonicalDepts[normDept];

          if (matchedDept) {
            if (rawDept !== matchedDept) {
              mappedRow['department'] = matchedDept;
              rowObs.push({
                type: 'correction',
                message: `Fila ${recordIndex}: Se normalizó el departamento a DIVIPOLA ("${rawDept}" ➔ "${matchedDept}").`
              });
            }

            if (hasMuni && mappedRow['municipality']) {
              const rawMuni = mappedRow['municipality'];
              const normMuni = limpiarTexto(rawMuni);
              const deptMunis = canonicalMunis[normDept];

              if (deptMunis && deptMunis.has(normMuni)) {
                const matchedMuni = cleanMuniToOriginal[`${normDept}::${normMuni}`];
                if (rawMuni !== matchedMuni) {
                  mappedRow['municipality'] = matchedMuni;
                  rowObs.push({
                    type: 'correction',
                    message: `Fila ${recordIndex}: Se normalizó el municipio a DIVIPOLA ("${rawMuni}" ➔ "${matchedMuni}").`
                  });
                }
              } else {
                rowObs.push({
                  type: 'warning',
                  message: `Fila ${recordIndex}: El municipio "${rawMuni}" no se encontró en DIVIPOLA para el departamento "${matchedDept}".`
                });
              }
            }
          } else {
            rowObs.push({
              type: 'warning',
              message: `Fila ${recordIndex}: El departamento "${rawDept}" no coincide con ningún departamento de DIVIPOLA Colombia.`
            });
          }
        }

        // 4. Comprobar campos obligatorios vacíos.
        fields.forEach(field => {
          if (field.required && !mappedRow[field.name]) {
            rowObs.push({
              type: 'warning',
              message: `Fila ${recordIndex}: El campo requerido "${field.label}" está vacío.`
            });
          }
        });

        // 5. Convertir valores de casillas.
        fields.forEach(field => {
          if (field.type === 'checkbox') {
            const rawVal = limpiarTexto(mappedRow[field.name]);
            if (['si', 'sí', 's', 'yes', 'y', '1', 'true', 'activo'].includes(rawVal)) {
              mappedRow[field.name] = 'Sí';
            } else if (rawVal) {
              mappedRow[field.name] = 'No';
            } else {
              mappedRow[field.name] = field.defaultValue === true ? 'Sí' : 'No';
            }
          }
        });

        // Completar valores predeterminados cuando corresponda.
        fields.forEach(field => {
          if (!mappedRow[field.name] && field.defaultValue !== undefined) {
            mappedRow[field.name] = String(field.defaultValue);
          }
        });

        results.push(mappedRow);
        if (rowObs.length > 0) {
          newObs.push(...rowObs);
        }
      });

      this.filasProcesadas.set(results);
      this.observaciones.set(newObs);
      this.estadisticas.set({
        total: this.filasFuente().length,
        advertencias: newObs.filter(o => o.type === 'warning').length,
        normalizaciones: newObs.filter(o => o.type === 'correction').length,
        filasPreparadas: results.length,
      });
      this.agregarMensajeDelSistema(`Preparación completada para **${this.filasFuente().length} registros**. Se aplicaron **${newObs.filter(o => o.type === 'correction').length} normalizaciones** explicables y se encontraron **${newObs.filter(o => o.type === 'warning').length} advertencias**. El archivo está listo para revisión y descarga.`);
    } finally {
      this.procesando.set(false);
    }
  }

  async previsualizarImportacion(): Promise<void> {
    if (this.previsualizandoImportacion() || this.confirmandoImportacion()) return;
    const archivo = this.archivoFuente();
    const filas = this.filasProcesadas();
    if (!archivo || filas.length === 0 || this.idModuloSeleccionado() !== 'festivals') return;

    if (archivo.size > 5 * 1024 * 1024) {
      this.errorImportacion.set('El archivo supera el máximo de 5 MiB permitido para esta operación.');
      return;
    }
    if (filas.length > 500) {
      this.errorImportacion.set('Este primer corte admite hasta 500 filas por lote. Divide el archivo y vuelve a intentarlo.');
      return;
    }

    this.previsualizandoImportacion.set(true);
    this.limpiarPrevisualizacionImportacion();
    try {
      const formato = archivo.name.toLowerCase().endsWith('.csv') ? 'csv' : 'xlsx';
      const solicitud: SolicitudDePrevisualizacion = {
        nombreArchivo: archivo.name,
        formato,
        huellaArchivo: await this.huellaSha256(await archivo.arrayBuffer()),
        versionContrato: 1,
        // LA FILA VIAJA COMO CAMPO -> VALOR, con los campos que el dominio declaró. Antes se armaba
        // una propiedad por cada campo de Festival, y por eso importar otra cosa exigía otro
        // contrato, otro endpoint y otra pantalla.
        filas: filas.map((fila, indice) => ({
          numeroFila: indice + 2,
          valores: Object.fromEntries(
            this.camposActivos().map(campo => [campo.name, fila[campo.name] ?? ''])),
        })),
      };
      const vista = await firstValueFrom(
        this.importaciones.previsualizar(this.idModuloSeleccionado(), solicitud));
      this.previsualizacionImportacion.set(vista);
      if (this.mostrarHistorial()) await this.cargarHistorial(1);
      this.agregarMensajeDelSistema(
        `La API previsualizó **${vista.totalFilas} filas**: **${vista.filasImportables}** pueden crear borradores y **${vista.filasRechazadas}** requieren corrección. Todavía no se escribió ningún registro.`,
      );
    } catch (error: unknown) {
      this.errorImportacion.set(this.mensajeDeError(error, 'No fue posible previsualizar la importación.'));
    } finally {
      this.previsualizandoImportacion.set(false);
    }
  }

  alternarExclusion(idFila: number): void {
    const vista = this.previsualizacionImportacion();
    if (!vista || vista.estado !== 'previsualizado' || this.confirmandoImportacion()) return;
    const fila = vista.filas.find(item => item.id === idFila);
    if (!fila?.puedeImportarse) return;
    this.idsFilasExcluidas.update(actuales =>
      actuales.includes(idFila) ? actuales.filter(id => id !== idFila) : [...actuales, idFila],
    );
    this.errorImportacion.set('');
    this.mensajeImportacion.set('');
  }

  estaExcluida(idFila: number): boolean {
    return this.idsFilasExcluidas().includes(idFila);
  }

  confirmarImportacion(): void {
    const vista = this.previsualizacionImportacion();
    if (!vista?.puedeConfirmar || this.cantidadElegidaParaImportar() === 0 || this.confirmandoImportacion()) return;
    this.confirmacion.set('importar');
  }

  /**
   * Qué se va a crear, contado, y qué NO va a pasar.
   *
   * LA FRONTERA SE DICE ANTES DE PULSAR: nada se publica y nada existente se toca. Es la regla del
   * proyecto —lo que entra por Importación Asistida no se publica solo— y quien confirma tiene que
   * verla escrita, no deducirla.
   */
  readonly detalleDeLaImportacion = computed(() => {
    const vista = this.previsualizacionImportacion();
    const cantidad = this.cantidadElegidaParaImportar();
    if (!vista) return '';
    return `Se crean ${cantidad} Festival${cantidad === 1 ? '' : 'es'} en estado borrador a partir de «${vista.nombreArchivo}». ` +
      'No se publica nada y no se modifica ningún registro existente. La creación queda en la auditoría con tu nombre.';
  });

  readonly accionDeLaImportacion = computed(() => {
    const cantidad = this.cantidadElegidaParaImportar();
    return `Crear ${cantidad} borrador${cantidad === 1 ? '' : 'es'}`;
  });

  /** Lo que la confirmación dispara, ya decidido. */
  confirmarLaDecision(): void {
    if (this.confirmacion() === 'reiniciar') { this.reiniciarDeVerdad(); return; }
    if (this.confirmacion() === 'importar') { void this.importarDeVerdad(); }
  }

  cerrarLaConfirmacion(): void { this.confirmacion.set(null); }

  private async importarDeVerdad(): Promise<void> {
    const vista = this.previsualizacionImportacion();
    if (!vista) return;
    this.confirmacion.set(null);
    this.confirmandoImportacion.set(true);
    this.errorImportacion.set('');
    this.mensajeImportacion.set('');
    try {
      const excluidas = [...this.idsFilasExcluidas()].sort((a, b) => a - b);
      const materialConfirmacion = new TextEncoder().encode(JSON.stringify({
        lote: vista.id,
        huellaPlan: vista.huellaPlan,
        excluidas,
      }));
      const claveIdempotencia = `web-${await this.huellaSha256(materialConfirmacion)}`;
      const aplicada = await firstValueFrom(this.importaciones.confirmar(
        this.idModuloSeleccionado(),
        vista.id,
        vista.huellaPlan,
        claveIdempotencia,
        excluidas,
      ));
      this.previsualizacionImportacion.set(aplicada);
      if (this.mostrarHistorial()) await this.cargarHistorial(this.historialImportaciones()?.pagina ?? 1);
      this.mensajeImportacion.set(
        `Importación aplicada: se crearon ${aplicada.filasAplicadas} borradores y se excluyeron ${aplicada.filasExcluidas} filas por decisión humana.`,
      );
      this.agregarMensajeDelSistema(
        `La importación quedó aplicada con **${aplicada.filasAplicadas} Festivales en borrador**. Podrás revisarlos en el módulo al volver a la lista.`,
      );
    } catch (error: unknown) {
      this.errorImportacion.set(this.mensajeDeError(error, 'No fue posible confirmar la importación.'));
    } finally {
      this.confirmandoImportacion.set(false);
    }
  }

  private limpiarPrevisualizacionImportacion(): void {
    this.previsualizacionImportacion.set(null);
    this.idsFilasExcluidas.set([]);
    this.mensajeImportacion.set('');
    this.errorImportacion.set('');
  }

  private async huellaSha256(datos: ArrayBuffer | Uint8Array): Promise<string> {
    if (!globalThis.crypto?.subtle) {
      throw new Error('Este navegador no dispone del mecanismo seguro necesario para calcular la huella del archivo.');
    }
    const origen = datos instanceof Uint8Array ? datos : new Uint8Array(datos);
    const entrada = new Uint8Array(origen.byteLength);
    entrada.set(origen);
    const hash = await globalThis.crypto.subtle.digest('SHA-256', entrada.buffer);
    return Array.from(new Uint8Array(hash)).map(byte => byte.toString(16).padStart(2, '0')).join('');
  }

  private mensajeDeError(error: unknown, respaldo: string): string {
    return error instanceof Error && error.message.trim() ? error.message : respaldo;
  }

  async descargarExcel() {
    const mod = this.moduloActivo();
    if (this.filasProcesadas().length === 0 || !mod) return;
    if (this.descargandoExcel()) return;
    this.descargandoExcel.set(true);

    try {
      const fields = this.camposActivos();
      const workbook = await crearLibroExcel();
      workbook.creator = 'Entorno Virtual PNMC - Preparación asistida';
      workbook.created = new Date();

      const templateSheet = workbook.addWorksheet('Plantilla', {
        views: [{ state: 'frozen', ySplit: 1 }],
      });

      // Add Headers Row
      templateSheet.addRow(fields.map(f => f.label));

      // Add Processed Rows
      this.filasProcesadas().forEach(row => {
        const rowData = fields.map(field => row[field.name] || '');
        templateSheet.addRow(rowData);
      });

      // Style Header
      templateSheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
      templateSheet.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF291242' } };
      templateSheet.getRow(1).alignment = { vertical: 'middle', wrapText: true };
      templateSheet.getRow(1).height = 30;

      // Auto-fit Columns
      templateSheet.columns = fields.map((f) => {
        const labelLen = String(f.label || '').length;
        let maxValLen = 0;
        this.filasProcesadas().forEach(row => {
          const valLen = String(row[f.name] || '').length;
          if (valLen > maxValLen) maxValLen = valLen;
        });
        return { width: Math.min(45, Math.max(16, Math.max(labelLen, maxValLen) + 3)) };
      });

      const buffer = await workbook.xlsx.writeBuffer();
      const blob = new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `pnmc_preparado_${mod.dominio}.xlsx`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);

      this.agregarMensajeDelSistema(`El archivo "pnmc_preparado_${mod.dominio}.xlsx" fue generado y descargado. También puedes previsualizarlo contra la base vigente antes de confirmar la creación de borradores.`);
    } catch (err: any) {
      console.error(err);
      this.avisar(`No fue posible generar el archivo de Excel: ${err.message}`);
    } finally {
      this.descargandoExcel.set(false);
    }
  }
}
