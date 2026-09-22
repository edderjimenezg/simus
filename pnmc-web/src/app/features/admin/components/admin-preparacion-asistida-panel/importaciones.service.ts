import { inject, Injectable } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { ApiClientService } from '../../../../core/http/api-client.service';

/**
 * Lo que el servidor dice que se puede importar, y en qué estado nace lo importado.
 *
 * <b>LA LISTA NO SE LLEVA ESCRITA EN EL NAVEGADOR.</b> Cuando la llevaba, esta pantalla anunciaba
 * ocho módulos y solo uno tenía circuito: quien elegía cualquiera de los otros siete llegaba a un
 * destino que el servidor no sabía recibir. Ahora se pregunta.
 */
export interface DominioImportable {
  dominio: string;
  etiqueta: string;
  /** El estado en el que nace un registro importado. Nunca uno público: es la regla 2. */
  estadoAlImportar: string;
  /** Por qué ese estado, escrito para quien lo lea dentro de un año. */
  porQueEseEstado: string;
  /** Los campos que este dominio sabe leer de un archivo. La pantalla se arma con ellos. */
  campos: CampoDeImportacion[];
}

/** Un campo que un dominio sabe leer de un archivo. */
export interface CampoDeImportacion {
  nombre: string;
  etiqueta: string;
  obligatorio: boolean;
}

/**
 * Una fila del archivo: campo declarado por el dominio → valor leído de la hoja.
 *
 * <b>UN DICCIONARIO Y NO TRECE PROPIEDADES.</b> Hasta esta interfaz
 * declaraba una propiedad por cada campo de un Festival, y por eso importar cualquier otra cosa
 * exigía otro contrato, otro endpoint y otra pantalla.
 */
export interface FilaDeArchivo {
  numeroFila: number;
  valores: Record<string, string | null>;
}

export interface SolicitudDePrevisualizacion {
  nombreArchivo: string;
  formato: 'csv' | 'xlsx';
  huellaArchivo: string;
  versionContrato: 1;
  filas: FilaDeArchivo[];
}

export interface HallazgoDeImportacion {
  severidad: 'informacion' | 'advertencia' | 'error';
  codigo: string;
  campo: string | null;
  mensaje: string;
}

export interface FilaDeImportacion {
  id: number;
  numeroFila: number;
  resultado: 'crear' | 'rechazar' | 'coincidencia_existente';
  puedeImportarse: boolean;
  registroCoincidenteId: number | null;
  registroCreadoId: number | null;
  /**
   * El dato normalizado, proyectado a campo → valor por el dominio.
   *
   * NO ES LA FORMA GUARDADA: cada dominio guarda lo que necesita —códigos territoriales resueltos,
   * identificadores de catálogo— y proyecta lo que se enseña. Así la pantalla pinta una tabla con
   * los campos declarados sin saber nada de ningún dominio.
   */
  datos: Record<string, string | null>;
  hallazgos: HallazgoDeImportacion[];
}

export interface Importacion {
  id: number;
  dominio: string;
  nombreArchivo: string;
  formato: string;
  huellaArchivo: string;
  huellaPlan: string;
  versionContrato: number;
  estado: 'previsualizado' | 'aplicando' | 'aplicado' | 'depurando' | 'expirado' | 'conflicto';
  fechaPrevisualizacion: string;
  fechaAplicacion: string | null;
  fechaExpiracion: string | null;
  fechaDepuracion: string | null;
  estadoRetencion: 'detalle_temporal' | 'datos_duplicados_minimizados' | 'pendiente_minimizacion' | 'detalle_depurado' | 'en_proceso';
  esPropio: boolean;
  puedeConfirmar: boolean;
  totalFilas: number;
  filasImportables: number;
  filasRechazadas: number;
  filasAplicadas: number;
  filasExcluidas: number;
  filas: FilaDeImportacion[];
  aviso: string;
}

export interface ResumenImportacion {
  id: number;
  nombreArchivo: string;
  formato: string;
  estado: Importacion['estado'];
  fechaPrevisualizacion: string;
  fechaAplicacion: string | null;
  fechaExpiracion: string | null;
  fechaDepuracion: string | null;
  estadoRetencion: Importacion['estadoRetencion'];
  esPropio: boolean;
  puedeConfirmar: boolean;
  totalFilas: number;
  filasImportables: number;
  filasRechazadas: number;
  filasAplicadas: number;
  filasExcluidas: number;
}

export interface PaginaDeImportaciones {
  items: ResumenImportacion[];
  pagina: number;
  tamano: number;
  total: number;
  totalPaginas: number;
  diasRetencionPrevisualizacion: number;
  politicaRetencion: string;
}

@Injectable({ providedIn: 'root' })
export class ImportacionesService {
  /**
   * La ruta del dominio que se está importando.
   *
   * <b>AQUI HABIA UNA CONSTANTE FIJA</b> con «festivales» escrito dentro de la ruta. El Bloque 5b
   * convirtió la importación en genérica —el núcleo, el contrato, la pantalla y el registro de
   * dominios—, renombró esta clase de `ImportacionesFestivalesService` a `ImportacionesService`…
   * y dejó la constante como estaba. El resultado era que la consola ofrecía varios destinos y
   * <b>todas las llamadas iban a Festivales</b>: elegir «Organizaciones» habría previsualizado
   * organizaciones contra las reglas de Festival.
   *
   * <b>LO ESTABA SEÑALANDO LA AUDITORIA DE RUTAS</b> desde el primer día, como una llamada que
   * ninguna ruta sirve —el servidor la publica bajo `{dominio}`—. Se perdía entre cuatro falsos
   * positivos fijos que esa misma herramienta arrastraba; corregirlos, en este mismo corte, es lo
   * que dejó el hallazgo a la vista.
   */
  private static ruta(dominio: string): string {
    return `/api/v1/admin/importaciones/${dominio}`;
  }

  private readonly api = inject(ApiClientService);


  /**
   * Qué dominios se pueden importar hoy, según el servidor.
   *
   * SE PIDE UNA VEZ AL ABRIR: es una lista corta que solo cambia cuando se despliega un dominio
   * nuevo, y pedirla en cada paso del asistente añadiría una llamada por nada.
   */
  dominiosImportables(): Observable<{ dominios: DominioImportable[] }> {
    return this.api.get<{ dominios: DominioImportable[] }>('/api/v1/admin/importaciones', {
      errorFallback: 'No fue posible consultar qué se puede importar.',
    });
  }

  listar(dominio: string, filtros: { pagina?: number; tamano?: number; estado?: string; archivo?: string } = {}): Observable<PaginaDeImportaciones> {
    const params: Record<string, string | number> = {
      pagina: filtros.pagina ?? 1,
      tamano: filtros.tamano ?? 10,
    };
    if (filtros.estado) params['estado'] = filtros.estado;
    if (filtros.archivo?.trim()) params['archivo'] = filtros.archivo.trim();
    return this.api.get<PaginaDeImportaciones>(`${ImportacionesService.ruta(dominio)}/`, {
      params,
      errorFallback: 'No fue posible consultar el historial de importaciones',
    });
  }

  consultar(dominio: string, id: number): Observable<Importacion> {
    return this.api.get<Importacion>(`${ImportacionesService.ruta(dominio)}/${id}`, {
      errorFallback: 'No fue posible consultar el detalle de la importación',
    });
  }

  previsualizar(dominio: string, solicitud: SolicitudDePrevisualizacion): Observable<Importacion> {
    return this.conCsrf(dominio, token => this.api.post<Importacion>(
      `${ImportacionesService.ruta(dominio)}/previsualizar`,
      solicitud,
      {
        headers: { 'X-CSRF-TOKEN': token },
        timeoutMs: 60000,
        // EL MENSAJE TAMPOCO PODIA SEGUIR DICIENDO «de Festivales»: se lee tras importar cualquier
        // cosa, y decir el destino equivocado en un error es peor que no decir ninguno.
        errorFallback: 'No fue posible previsualizar la importación.',
      },
    ));
  }

  confirmar(
    dominio: string,
    id: number,
    huellaPlan: string,
    claveIdempotencia: string,
    idsFilasExcluidas: readonly number[],
  ): Observable<Importacion> {
    return this.conCsrf(dominio, token => this.api.post<Importacion>(
      `${ImportacionesService.ruta(dominio)}/${id}/confirmar`,
      { huellaPlan, claveIdempotencia, idsFilasExcluidas },
      {
        headers: { 'X-CSRF-TOKEN': token },
        timeoutMs: 60000,
        errorFallback: 'No fue posible confirmar la importación.',
      },
    ));
  }

  /** El testigo se pide al MISMO dominio al que se va a escribir: es por grupo de rutas. */
  private conCsrf<T>(dominio: string, operacion: (token: string) => Observable<T>): Observable<T> {
    return this.api.get<{ token: string }>(`${ImportacionesService.ruta(dominio)}/csrf`, {
      errorFallback: 'No fue posible preparar la operación segura',
    }).pipe(switchMap(respuesta => operacion(respuesta.token)));
  }
}
