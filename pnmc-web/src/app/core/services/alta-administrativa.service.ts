import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * Dar de alta organizaciones y Festivales desde el Panel de Gestión Administrativa.
 *
 * <b>NO ES UN MODELO PARALELO.</b> El servidor usa para estas altas exactamente las mismas piezas
 * que el canal externo; lo único que cambia es de dónde viene el registro y quién lo ejecuta. Aquí
 * solo se envían los datos.
 *
 * <b>TRES DIMENSIONES QUE NO SE MEZCLAN.</b> La procedencia la pone el servidor —es el Programa—;
 * el usuario sale de la sesión; y la organización responsable la elige quien registra, porque es
 * quien gestiona el proceso y puede no ser ninguna todavía.
 */

export interface CoincidenciaDeAlta {
  id: string;
  nombre: string;
  territorio: string | null;
  estado: string;
  organizacionResponsable: string | null;
}

export interface OrganizacionNueva {
  nombre: string;
  identificacion?: string | null;
  correoContacto?: string | null;
  codigoDepartamentoSede?: string | null;
  codigoMunicipioSede?: string | null;
  responsableNombre: string;
  responsableTipoDocumento?: string | null;
  responsableNumeroDocumento?: string | null;
  responsableCorreo?: string | null;
  responsableTelefono?: string | null;
  responsableAutorizacionDatos: boolean;
}

export interface FestivalNuevo {
  festival: {
    nombre: string;
    descripcion?: string | null;
    periodicidad?: string | null;
    periodicidadDetalle?: string | null;
    correoContacto?: string | null;
    nivelCobertura: string;
    codigoDepartamento?: string | null;
    codigoMunicipio?: string | null;
    practicasMusicalesIds: number[];
    territoriosSonorosIds: number[];
    instagram?: string | null;
    facebook?: string | null;
    paginaWeb?: string | null;
    otroEnlace?: string | null;
    telefonoCelular?: string | null;
    observacionesContacto?: string | null;
  };
  organizacionResponsableId: number | null;
}

export interface ResultadoDeAlta {
  ok: boolean;
  id?: string;
  nombre?: string;
  /** Los errores del servidor, campo a campo. La pantalla los pinta al lado de cada campo. */
  errores?: Record<string, string[]>;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class AltaAdministrativaService {
  private readonly api = inject(ApiClientService);

  private static readonly RUTA = '/api/v1/admin';

  async crearOrganizacion(datos: OrganizacionNueva): Promise<ResultadoDeAlta> {
    return this.enviar(`${AltaAdministrativaService.RUTA}/organizaciones`, datos, 'No fue posible registrar la organización.');
  }

  async crearFestival(datos: FestivalNuevo): Promise<ResultadoDeAlta> {
    return this.enviar(`${AltaAdministrativaService.RUTA}/festivales`, datos, 'No fue posible registrar el Festival.');
  }

  /** Organizaciones que se parecen a la que se va a registrar. Se ofrecen, no bloquean. */
  coincidenciasDeOrganizacion(nombre: string, identificacion?: string): Promise<CoincidenciaDeAlta[]> {
    const params = new URLSearchParams({ nombre });
    if (identificacion) { params.set('identificacion', identificacion); }
    return this.buscar(`${AltaAdministrativaService.RUTA}/organizaciones/coincidencias?${params.toString()}`);
  }

  coincidenciasDeFestival(nombre: string): Promise<CoincidenciaDeAlta[]> {
    return this.buscar(`${AltaAdministrativaService.RUTA}/festivales/coincidencias?nombre=${encodeURIComponent(nombre)}`);
  }

  /**
   * Mercados que ya existen con un nombre parecido.
   *
   * <b>AVISA, NO BLOQUEA.</b> Hay mercados que de verdad se llaman parecido en departamentos
   * distintos, y quien registra es quien sabe si es el mismo. Devuelve con qué comparar —el
   * territorio, el estado y quién responde— en vez de un sí o un no.
   */
  coincidenciasDeMercado(nombre: string): Promise<CoincidenciaDeAlta[]> {
    return this.buscar(`${AltaAdministrativaService.RUTA}/mercados/coincidencias?nombre=${encodeURIComponent(nombre)}`);
  }

  private async buscar(ruta: string): Promise<CoincidenciaDeAlta[]> {
    try {
      return await firstValueFrom(this.api.get<CoincidenciaDeAlta[]>(ruta, {})) ?? [];
    } catch {
      // BUSCAR COINCIDENCIAS ES UNA AYUDA, NO UN REQUISITO. Si falla, se registra igual.
      return [];
    }
  }

  private async enviar(ruta: string, cuerpo: unknown, respaldo: string): Promise<ResultadoDeAlta> {
    try {
      const creado = await firstValueFrom(this.api.post<{ id: string; nombre: string }>(ruta, cuerpo, {}));
      return { ok: true, id: creado?.id, nombre: creado?.nombre };
    } catch (error: unknown) {
      // EL SERVIDOR DEVUELVE LOS ERRORES CAMPO A CAMPO y la pantalla los pinta donde tocan. Un
      // «no fue posible» genérico dejaría a quien registra adivinando qué campo estaba mal.
      const cuerpoDelError = (error as { payload?: unknown })?.payload;
      const errores = this.erroresPorCampo(cuerpoDelError);
      return { ok: false, errores, error: errores ? undefined : respaldo };
    }
  }

  private erroresPorCampo(cuerpo: unknown): Record<string, string[]> | undefined {
    if (!cuerpo || typeof cuerpo !== 'object') { return undefined; }
    // `Results.ValidationProblem` los envuelve en `errors`; `Results.BadRequest` los manda planos.
    const fuente = (cuerpo as { errors?: unknown }).errors ?? cuerpo;
    if (!fuente || typeof fuente !== 'object') { return undefined; }

    const salida: Record<string, string[]> = {};
    for (const [campo, valor] of Object.entries(fuente as Record<string, unknown>)) {
      if (Array.isArray(valor) && valor.every(v => typeof v === 'string')) {
        salida[campo] = valor as string[];
      }
    }
    return Object.keys(salida).length > 0 ? salida : undefined;
  }
}
