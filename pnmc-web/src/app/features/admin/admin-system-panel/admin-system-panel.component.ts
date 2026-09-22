import { CommonModule, formatDate } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { LucideRefreshCw } from '@lucide/angular';
import { MonitorDelSistema } from '../../../core/services/admin.service';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { CifraDeTablero, FranjaDeCifrasComponent } from '../../../shared/components/ui/franja-de-cifras/franja-de-cifras.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { nombrePropio } from '../../../shared/texto/nombre-propio';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';

/** Un departamento de DIVIPOLA con sus municipios, tal como los agrupa `cargarDivipolaPorDepartamento`. */
interface DepartamentoConMunicipios {
  nombre: string;
  municipios: string[];
}

/**
 * «Salud del sistema»: si la API y la base responden, qué módulos informa el monitor y qué datos
 * territoriales hay cargados.
 *
 * <b>ES UN TABLERO, Y SE MIDE CONTRA EL RESUMEN OPERATIVO.</b> Hasta
 * recibía el monitor como `any` y lo leía a ciegas, dibujaba su franja de cifras a mano con tres
 * divisores, y cortaba la tabla de DIVIPOLA en diez departamentos mientras el estado vacío prometía
 * «los 33». Ahora lee el contrato tipado, usa la franja compartida, ordena los 33 en memoria y
 * ofrece volver a leer, que es lo que hace el resto de tableros.
 *
 * NO CARGA NADA: el armazón es quien sondea el monitor y los datos territoriales, porque también
 * los usa la barra y el Resumen. Esta pantalla los recibe y pide una nueva lectura con `recargar`.
 */
@Component({
  selector: 'app-admin-system-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent,
    BotonComponent,
    BuscadorDeListaComponent,
    CabeceraDeTablaComponent,
    CommonModule,
    EstadoDeListaComponent,
    FranjaDeCifrasComponent,
    LucideRefreshCw,
    NombrePropioPipe,
  ],
  templateUrl: './admin-system-panel.component.html',
})
export class AdminSystemPanelComponent {
  readonly schemaOnline = input(false);
  readonly monitor = input<MonitorDelSistema | null>(null);
  readonly divipola = input<Record<string, string[]>>({});
  /** Si el armazón está leyendo ahora mismo, para que el botón lo diga. */
  readonly ocupado = input(false);
  readonly recargar = output<void>();

  readonly busqueda = signal('');

  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'departamento', etiqueta: 'Departamento' },
    { id: 'cuantos', etiqueta: 'Municipios' },
    { id: 'municipios', etiqueta: 'Algunos de ellos', ordenable: false },
  ];

  /** Los departamentos se leen de la A a la Z: es el orden con el que se busca uno. */
  readonly orden = new OrdenDeTabla('departamento', 'asc');

  readonly modulosOperativos = computed(() => this.monitor()?.modules ?? []);

  readonly departamentos = computed<DepartamentoConMunicipios[]>(() =>
    Object.entries(this.divipola() ?? {}).map(([nombre, municipios]) => ({
      nombre,
      municipios: Array.isArray(municipios) ? municipios : [],
    })));

  readonly totalDeMunicipios = computed(() =>
    this.departamentos().reduce((suma, departamento) => suma + departamento.municipios.length, 0));

  /** Las tres cifras de arriba: si el esquema responde, y cuánto territorio hay cargado. */
  readonly cifrasDelSistema = computed<CifraDeTablero[]>(() => [
    {
      id: 'esquema',
      cifra: this.schemaOnline() ? 'Disponible' : 'No conectado',
      rotulo: 'Esquema administrativo',
      detalle: this.schemaOnline() ? 'El servidor está respondiendo' : 'El servidor no responde',
      tono: this.schemaOnline() ? 'correcto' : 'aviso',
    },
    { id: 'departamentos', cifra: this.departamentos().length, rotulo: 'Departamentos', detalle: 'DIVIPOLA cargados' },
    { id: 'municipios', cifra: this.totalDeMunicipios(), rotulo: 'Municipios', detalle: 'DIVIPOLA cargados' },
  ]);

  /** Filtrados y ordenados SOBRE EL TOTAL: los 33 caben en memoria. */
  readonly departamentosVisibles = computed(() => {
    const texto = this.busqueda().trim().toLocaleLowerCase('es');
    const columna = this.orden.columna();
    const sentido = this.orden.direccion() === 'desc' ? -1 : 1;
    return this.departamentos()
      .filter(departamento => !texto || departamento.nombre.toLocaleLowerCase('es').includes(texto))
      .sort((a, b) => sentido * (columna === 'cuantos'
        ? a.municipios.length - b.municipios.length
        : a.nombre.localeCompare(b.nombre, 'es')));
  });

  /**
   * Los cinco primeros municipios de un departamento, escritos como una frase.
   *
   * ERAN CINCO CAPSULAS GRISES POR FILA, treinta y tres filas: más de ciento cincuenta píldoras en
   * una pantalla. Ninguna es un objeto ni una acción -son una muestra del contenido de la fila-,
   * así que se escriben como lo que son: texto separado por comas.
   */
  muestraDeMunicipios(municipios: readonly string[]): string {
    return municipios.slice(0, 5).map(nombre => nombrePropio(nombre)).join(', ');
  }

  fecha(valor: string | null | undefined): string {
    if (!valor) return '';
    const fecha = new Date(valor);
    return Number.isNaN(fecha.getTime()) ? '' : formatDate(fecha, FECHA_Y_HORA_ADMINISTRATIVA, 'es-CO');
  }
}
