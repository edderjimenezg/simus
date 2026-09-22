import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';

/**
 * Lo que un funcionario puede hacer por una organización que no es suya.
 *
 * <b>ASISTIR NO ES EDITAR.</b> No corrige sus datos ni toca sus credenciales: le <b>escribe</b>
 * para que ella los corrija, y <b>administra el estado</b> de su ficha cuando ya no opera. Es la
 * misma distinción que sostiene la procedencia: quien incorporó un registro no es quien responde
 * por él.
 */

export interface MensajeAOrganizacion {
  id: number;
  asunto: string;
  mensaje: string;
  remitenteNombre: string | null;
  destinatarioCorreo: string | null;
  fechaEnvio: string;
  /** `null` mientras nadie lo haya abierto. «Le escribimos y no lo ha leído» es información. */
  leidoEn: string | null;
}

/** Un proceso que el cierre de la organización dejaría sin nadie que responda por él. */
export interface ProcesoPendienteDeCierre {
  id: string;
  nombre: string;
  estado: string;
}

export interface ResultadoDeAsistencia<T> {
  ok: boolean;
  data?: T | null;
  error?: string;
  /**
   * Los procesos que impidieron el cierre.
   *
   * NO ES UN ERROR CUALQUIERA: el servidor rechaza cerrar una organización que todavía administra
   * procesos publicados o pendientes de decisión, y los nombra para que la consola pueda ofrecer
   * qué hacer con ellos en vez de dejar a quien administra adivinando cuáles son.
   */
  procesosPendientes?: ProcesoPendienteDeCierre[];
}

@Injectable({ providedIn: 'root' })
export class AsistenciaAOrganizacionService {
  private readonly api = inject(ApiClientService);

  private ruta(organizacionId: number | string): string {
    return `/api/v1/admin/organizaciones/${organizacionId}`;
  }

  mensajes(organizacionId: number | string) {
    return this.envolver(
      () => firstValueFrom(this.api.get<{ items: MensajeAOrganizacion[] }>(`${this.ruta(organizacionId)}/mensajes`, {})),
      'No fue posible consultar los mensajes.');
  }

  escribir(organizacionId: number | string, mensaje: { asunto: string; mensaje: string }) {
    return this.envolver(
      () => firstValueFrom(this.api.post<{ destinatarios: number }>(`${this.ruta(organizacionId)}/mensajes`, mensaje, {})),
      'No fue posible enviar el mensaje.');
  }

  /**
   * Cambia el estado de la organización o la marca inactiva.
   *
   * EL MOTIVO PROTEGE EL CIERRE y también la petición de ajustes: en el primer caso queda en la
   * bitácora, en el segundo se le envía a la organización como mensaje.
   *
   * `queHacerConLosProcesos` ES LA RESPUESTA A UN 409, NO UN ATAJO, y tiene dos valores porque son
   * dos actos distintos: `liberar` devuelve su custodia al Programa dejándolos publicados, para que
   * otra organización pueda reclamarlos; `archivar` los saca del sitio público conservando su ficha,
   * su historial y su bitácora. Ninguno borra nada.
   */
  cambiarEstado(
    organizacionId: number | string,
    cambio: { estado?: string; activa?: boolean; motivo?: string; queHacerConLosProcesos?: 'liberar' | 'archivar' },
  ) {
    return this.envolver(
      () => firstValueFrom(this.api.post<{ estado: string; activa: boolean; queSeHizoConLosProcesos: string | null; procesosAfectados: number; avisados: number }>(
        `${this.ruta(organizacionId)}/estado`, cambio, {})),
      'No fue posible cambiar el estado de la organización.');
  }

  /**
   * Da por comprobado el correo de una cuenta responsable, con el motivo de cómo se comprobó.
   *
   * <b>NO ES UN ATAJO, ES UN ACTO REGISTRADO.</b> Mientras no haya proveedor de correo el enlace de
   * confirmación no sale del sistema, y una organización que llama por teléfono para registrar su
   * Festival se quedaría bloqueada por un mensaje que nadie puede recibir. La salida no es apagar
   * la regla: es que una persona del Programa compruebe la dirección por otra vía y lo declare aquí,
   * con su nombre, la fecha y el motivo en la bitácora.
   *
   * <b>`correo` SOLO HACE FALTA CUANDO HAY VARIAS CUENTAS.</b> Con una sola no hay ambigüedad; con
   * varias, dar por buena «la primera» confirmaría una dirección que quien llamó no verificó.
   */
  confirmarCorreo(organizacionId: number | string, datos: { motivo: string; correo?: string }) {
    return this.envolver(
      () => firstValueFrom(this.api.post<{ correoConfirmado: string; organizacionesActivadas: number; estado: string }>(
        `${this.ruta(organizacionId)}/confirmar-correo`, datos, {})),
      'No fue posible confirmar el correo de la organización.');
  }

  private async envolver<T>(operacion: () => Promise<T>, respaldo: string): Promise<ResultadoDeAsistencia<T>> {
    try {
      return { ok: true, data: await operacion() };
    } catch (error: unknown) {
      // EL MOTIVO DEL SERVIDOR ENTERO: dice si nadie puede leer el mensaje, si falta el motivo del
      // cierre o si la entidad institucional no se puede tocar. Un «no fue posible» genérico
      // dejaría a quien administra adivinando cuál de las tres cosas pasó.
      const cuerpo = (error as { payload?: unknown })?.payload;
      if (cuerpo && typeof cuerpo === 'object') {
        // LOS PROCESOS QUE BLOQUEAN EL CIERRE viajan aparte del texto, porque la pantalla los
        // enumera y ofrece liberarlos: convertirlos en una frase obligaría a leerlos de vuelta.
        const registro = cuerpo as Record<string, unknown>;
        if (Array.isArray(registro['procesos'])) {
          const procesos = (registro['procesos'] as ProcesoPendienteDeCierre[]).filter(p => p && p.id);
          const aviso = typeof registro['mensaje'] === 'string' ? (registro['mensaje'] as string) : respaldo;
          return { ok: false, error: aviso, procesosPendientes: procesos };
        }

        for (const valor of Object.values(registro)) {
          if (Array.isArray(valor)) {
            const mensaje = valor.find(m => typeof m === 'string' && m.trim().length > 0);
            if (typeof mensaje === 'string') { return { ok: false, error: mensaje.trim() }; }
          }
        }
      }
      return { ok: false, error: respaldo };
    }
  }
}
