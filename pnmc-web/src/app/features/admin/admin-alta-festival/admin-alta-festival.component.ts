import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { Component, EventEmitter, Output, computed, inject, signal } from '@angular/core';
import { CatalogService, TerritorioConCodigo } from '../../../core/services/catalog.service';
import { AdminService, OrganizacionAdministrativa } from '../../../core/services/admin.service';
import {
  AltaAdministrativaService,
  CoincidenciaDeAlta,
} from '../../../core/services/alta-administrativa.service';
import { PasoDelIndicador } from '../../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { AsistenteDeAltaComponent } from '../../../shared/components/ui/asistente-de-alta/asistente-de-alta.component';
import { PASOS_DE_UN_ALTA } from '../../../core/vocabularios/pasos-de-un-alta';
import { SelectorDeClasificacionComponent } from '../../../shared/components/ui/selector-de-clasificacion/selector-de-clasificacion.component';
import { AdminAltaOrganizacionComponent } from '../admin-alta-organizacion/admin-alta-organizacion.component';
import { NombrePropioPipe } from '../../../shared/texto/nombre-propio.pipe';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { PERIODICIDADES, pideDetalle } from '../../../core/vocabularios/periodicidad';

/**
 * Registrar un Festival desde el Panel de Gestión Administrativa.
 *
 * <b>TRES COSAS DISTINTAS, Y AQUÍ SE VEN LAS TRES.</b> La procedencia es el Programa —lo incorpora
 * la consola—; el usuario es la cuenta que tiene la sesión; y la organización responsable es la
 * que gestiona el Festival, que puede ser otra o no existir todavía. Que el PNMC lo registre
 * <b>no</b> lo convierte en su organización responsable.
 *
 * <b>SI LA ORGANIZACIÓN NO EXISTE, SE CREA SIN SALIR DE AQUÍ.</b> Obligar a abandonar el registro
 * a medias para ir a otra pantalla es como se pierden los datos que ya se habían escrito.
 *
 * <b>NO ES UNA VARIANTE DEL MODELO.</b> El servidor usa la misma alta que el asistente externo:
 * mismas reglas de territorio, periodicidad y catálogos.
 */

interface FormularioDeFestival {
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
  organizacionResponsableId: number | null;
  practicasMusicalesIds: number[];
  territoriosSonorosIds: number[];
}

/**
 * Los pasos, que ya no son propios.
 *
 * SE CONSERVA EL NOMBRE porque lo importan otras pantallas, pero apunta al vocabulario común: un
 * proceso del Ecosistema se pregunta igual sea cual sea, y tener dos listas de pasos es como
 * empezaron a divergir las de periodicidad.
 */
export const PASOS_DEL_FESTIVAL: readonly PasoDelIndicador[] = PASOS_DE_UN_ALTA;

// EL VOCABULARIO VIVE EN `core/vocabularios/periodicidad`, no aquí. Estaba copiado en tres
// pantallas y las tres habían dejado de decir lo mismo; ver el comentario de ese fichero.

const VACIO: FormularioDeFestival = {
  nombre: '', descripcion: '', periodicidad: '', periodicidadDetalle: '',
  nivelCobertura: 'municipal', codigoDepartamento: '', codigoMunicipio: '',
  correoContacto: '', telefonoCelular: '', instagram: '', facebook: '',
  paginaWeb: '', otroEnlace: '', observacionesContacto: '',
  organizacionResponsableId: null, practicasMusicalesIds: [], territoriosSonorosIds: [],
};

@Component({
  selector: 'app-admin-alta-festival',
  standalone: true,
  imports: [ BotonComponent, NombrePropioPipe,
    CommonModule, FormsModule, AsistenteDeAltaComponent,
    SelectorDeClasificacionComponent, AdminAltaOrganizacionComponent,
  ],
  templateUrl: './admin-alta-festival.component.html',
})
export class AdminAltaFestivalComponent {
  private readonly api = inject(AltaAdministrativaService);
  private readonly catalogo = inject(CatalogService);
  private readonly admin = inject(AdminService);

  @Output() registrado = new EventEmitter<{ id: string; nombre: string }>();
  @Output() cancelado = new EventEmitter<void>();

  readonly pasos = PASOS_DEL_FESTIVAL;
  readonly periodicidades = PERIODICIDADES;
  readonly paso = signal(1);
  readonly formulario = signal<FormularioDeFestival>({ ...VACIO });
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly errores = signal<Record<string, string[]>>({});
  readonly coincidencias = signal<CoincidenciaDeAlta[]>([]);
  readonly territorios = signal<TerritorioConCodigo[]>([]);
  readonly organizaciones = signal<OrganizacionAdministrativa[]>([]);
  readonly creandoOrganizacion = signal(false);

  readonly municipiosDelDepartamento = computed(() => {
    const codigo = this.formulario().codigoDepartamento;
    if (!codigo) { return []; }
    return this.territorios().find(t => t.codigo === codigo)?.municipios ?? [];
  });

  readonly organizacionElegida = computed(() => {
    const id = this.formulario().organizacionResponsableId;
    return id === null ? null : this.organizaciones().find(o => Number(o.id) === id) ?? null;
  });

  readonly pideDetalleDePeriodicidad = computed(() =>
    pideDetalle(this.formulario().periodicidad));

  readonly loQueFalta = computed(() => {
    const f = this.formulario();
    const faltas: string[] = [];
    if (!f.nombre.trim()) { faltas.push('el nombre'); }
    if (f.nivelCobertura !== 'nacional' && !f.codigoDepartamento) { faltas.push('el departamento'); }
    if (f.nivelCobertura === 'municipal' && !f.codigoMunicipio) { faltas.push('el municipio'); }
    if (this.pideDetalleDePeriodicidad() && !f.periodicidadDetalle.trim()) { faltas.push('la explicación de la periodicidad'); }
    return faltas;
  });

  private temporizador: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.catalogo.fetchDivipolaConCodigos().subscribe({
      next: territorios => this.territorios.set(territorios),
      error: () => this.territorios.set([]),
    });
    void this.cargarOrganizaciones();
  }

  private async cargarOrganizaciones(): Promise<void> {
    // SE PIDE UNA PAGINA GRANDE PORQUE ES UN DESPLEGABLE, no una tabla: quien registra necesita
    // encontrar su organización, y paginarla aquí obligaría a buscar a ciegas.
    try {
      const respuesta = await firstValueFrom(this.admin.cargarOrganizaciones({ tamano: 200 }));
      this.organizaciones.set(respuesta?.items ?? []);
    } catch {
      // SIN LISTA NO SE PUEDE ELEGIR, PERO SI REGISTRAR: un Festival puede quedarse sin
      // organización responsable, y esperar a que cargue la lista no debe impedir escribirlo.
      this.organizaciones.set([]);
    }
  }

  campo<K extends keyof FormularioDeFestival>(clave: K, valor: FormularioDeFestival[K]): void {
    const siguiente = { ...this.formulario(), [clave]: valor };
    this.formulario.set(siguiente);
    if (clave === 'nombre') { this.buscarCoincidencias(); }
  }

  cambiarDepartamento(codigo: string): void {
    this.formulario.set({ ...this.formulario(), codigoDepartamento: codigo, codigoMunicipio: '' });
  }

  /**
   * Cambia el nivel de cobertura y limpia lo que ese nivel no admite.
   *
   * EL SERVIDOR LO LIMPIARIA IGUAL, pero dejarlo en pantalla hace creer que se guardó: alguien
   * elige municipal, pone su municipio, cambia a nacional y sigue viéndolo escrito.
   */
  cambiarNivel(nivel: string): void {
    const actual = this.formulario();
    this.formulario.set({
      ...actual,
      nivelCobertura: nivel,
      codigoDepartamento: nivel === 'nacional' ? '' : actual.codigoDepartamento,
      codigoMunicipio: nivel === 'municipal' ? actual.codigoMunicipio : '',
    });
  }

  erroresDe(campo: string): string[] { return this.errores()[campo] ?? []; }

  abrirAltaDeOrganizacion(): void { this.creandoOrganizacion.set(true); }

  cerrarAltaDeOrganizacion(): void { this.creandoOrganizacion.set(false); }

  /** La organización recién creada queda elegida: es para lo que se abrió el formulario. */
  async organizacionCreada(organizacion: { id: string; nombre: string }): Promise<void> {
    this.creandoOrganizacion.set(false);
    await this.cargarOrganizaciones();
    this.campo('organizacionResponsableId', Number(organizacion.id));
  }

  private buscarCoincidencias(): void {
    if (this.temporizador) { clearTimeout(this.temporizador); }
    this.temporizador = setTimeout(async () => {
      const nombre = this.formulario().nombre.trim();
      this.coincidencias.set(nombre.length < 3 ? [] : await this.api.coincidenciasDeFestival(nombre));
    }, 600);
  }

  async registrar(): Promise<void> {
    const f = this.formulario();
    this.guardando.set(true);
    this.error.set(null);
    this.errores.set({});

    try {
      const resultado = await this.api.crearFestival({
        festival: {
          nombre: f.nombre.trim(),
          descripcion: f.descripcion.trim() || null,
          periodicidad: f.periodicidad || null,
          periodicidadDetalle: f.periodicidadDetalle.trim() || null,
          correoContacto: f.correoContacto.trim() || null,
          nivelCobertura: f.nivelCobertura,
          codigoDepartamento: f.codigoDepartamento || null,
          codigoMunicipio: f.codigoMunicipio || null,
          practicasMusicalesIds: f.practicasMusicalesIds,
          territoriosSonorosIds: f.territoriosSonorosIds,
          instagram: f.instagram.trim() || null,
          facebook: f.facebook.trim() || null,
          paginaWeb: f.paginaWeb.trim() || null,
          otroEnlace: f.otroEnlace.trim() || null,
          telefonoCelular: f.telefonoCelular.trim() || null,
          observacionesContacto: f.observacionesContacto.trim() || null,
        },
        organizacionResponsableId: f.organizacionResponsableId,
      });

      if (resultado.ok && resultado.id) {
        this.registrado.emit({ id: resultado.id, nombre: resultado.nombre ?? f.nombre.trim() });
        this.formulario.set({ ...VACIO });
        this.paso.set(1);
      } else {
        this.errores.set(resultado.errores ?? {});
        this.error.set(resultado.error ?? 'Revisa los campos marcados.');
      }
    } finally {
      this.guardando.set(false);
    }
  }
}
