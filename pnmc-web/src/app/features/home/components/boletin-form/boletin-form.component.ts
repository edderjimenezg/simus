import { Component, Input, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BoletinService, PoliticaDeTratamiento } from '../../../../core/services/boletin.service';
import { DialogoDirective } from '../../../../shared/directives/dialogo.directive';

type EstadoDelFormulario = 'reposo' | 'confirmando' | 'enviando' | 'listo';

/**
 * El formulario del boletín de la portada, con su paso de autorización.
 *
 * EL MODELO: un campo de correo y un botón sin manejador. Se escribía el correo, se pulsaba
 * «Registrarme», y no pasaba nada: ni petición, ni aviso, ni error. Quien lo usara se quedaba
 * creyendo que se había suscrito. Lo reportó el usuario.
 *
 * POR QUÉ HAY UN PASO INTERMEDIO Y NO SE ENVÍA DIRECTO. Un correo es dato personal, y usarlo para
 * enviar comunicaciones exige autorización previa, expresa e informada (Ley 1581 de 2012,
 * artículo 9). «Expresa» significa un acto propio: marcar una casilla que nace desmarcada. Un
 * envío directo al pulsar el botón sería consentimiento tácito, que la ley no admite.
 *
 * EL TEXTO DE LA AUTORIZACIÓN LO TRAE EL SERVIDOR, no esta plantilla. Es el mismo que el API
 * guarda como evidencia en `dbo.BoletinSuscripciones.AutorizacionTexto`: si el front tuviera su
 * propia copia, la pantalla podría decir una cosa y la base guardar otra.
 */
@Component({
  selector: 'app-boletin-form',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogoDirective],
  templateUrl: './boletin-form.component.html',
})
export class BoletinFormComponent {
  /** Textos del CMS. Llegan de fuera para que el CMS siga siendo el dueño de lo que se lee. */
  @Input() placeholder = 'Tu correo electrónico';
  @Input() textoDelBoton = 'Registrarme';

  private readonly boletin = inject(BoletinService);

  readonly correo = signal('');
  readonly estado = signal<EstadoDelFormulario>('reposo');
  readonly autoriza = signal(false);

  readonly politica = signal<PoliticaDeTratamiento | null>(null);
  readonly cargandoPolitica = signal(false);

  /** Un solo mensaje de error a la vez, y siempre visible junto a lo que falló. */
  readonly mensajeDeError = signal('');
  readonly mensajeDeExito = signal('');

  /**
   * La misma regla que aplica `NormalizarCorreo` en el API
   * (`pnmc-api/src/PNMC.Api/Endpoints/BoletinEndpoints.cs`): una sola arroba, que no vaya ni al
   * principio ni al final, un punto interior en el dominio, y ningún espacio.
   *
   * ES UNA CORTESÍA, NO UNA DEFENSA. El servidor vuelve a comprobarlo y la base lo impone con
   * `CK_BoletinSuscripciones_Correo_Formato`. Esto solo evita el viaje de ida y vuelta para
   * decirle a alguien que le falta la arroba.
   */
  correoValido(valor: string): boolean {
    const limpio = valor.trim();
    if (limpio.length === 0 || limpio.length > 180) return false;
    if (/\s/.test(limpio)) return false;

    const arroba = limpio.indexOf('@');
    if (arroba <= 0 || arroba !== limpio.lastIndexOf('@') || arroba === limpio.length - 1) return false;

    const dominio = limpio.slice(arroba + 1);
    const punto = dominio.indexOf('.');
    return punto > 0 && punto !== dominio.length - 1;
  }

  /** Paso 1: el botón de la portada. Valida el correo y abre la autorización. */
  abrirAutorizacion(): void {
    this.mensajeDeError.set('');
    this.mensajeDeExito.set('');

    if (!this.correoValido(this.correo())) {
      this.mensajeDeError.set('Escribe un correo electrónico válido, por ejemplo nombre@dominio.com');
      return;
    }

    this.autoriza.set(false);
    this.estado.set('confirmando');
    this.cargarPolitica();
  }

  cargarPolitica(): void {
    // Se pide cada vez que se abre. Es una respuesta minúscula y sin base de datos detrás, y
    // guardarla en caché arriesgaría enseñar una versión vieja del texto justo en la pantalla
    // donde el texto es lo único que importa.
    this.cargandoPolitica.set(true);
    this.mensajeDeError.set('');

    this.boletin.consultarPolitica().subscribe({
      next: (politica) => {
        this.politica.set(politica);
        this.cargandoPolitica.set(false);
      },
      error: () => {
        this.politica.set(null);
        this.cargandoPolitica.set(false);
        this.mensajeDeError.set('No pudimos cargar la política de tratamiento de datos. Intenta de nuevo.');
      },
    });
  }

  cerrarAutorizacion(): void {
    if (this.estado() === 'enviando') return;
    this.estado.set('reposo');
    this.autoriza.set(false);
    this.mensajeDeError.set('');
  }

  /** Cierra sólo al pulsar el fondo; los clics dentro del diálogo burbujean hasta este elemento. */
  cerrarAlPulsarFondo(evento: MouseEvent): void {
    if (evento.target === evento.currentTarget) this.cerrarAutorizacion();
  }

  alternarAutorizacion(marcada: boolean): void {
    this.autoriza.set(marcada);
    if (marcada) this.mensajeDeError.set('');
  }

  /**
   * Se puede confirmar cuando hay texto de política a la vista Y la casilla está marcada.
   *
   * LO PRIMERO NO ES ADORNO: sin el texto cargado, la persona estaría aceptando algo que no ha
   * podido leer.
   */
  puedeConfirmar(): boolean {
    return this.autoriza() && this.politica() !== null && this.estado() === 'confirmando';
  }

  /** Paso 2: confirmar. Aquí sí sale la petición de alta. */
  confirmar(): void {
    if (!this.puedeConfirmar()) return;

    this.estado.set('enviando');
    this.mensajeDeError.set('');

    this.boletin.suscribir(this.correo().trim(), true, 'portada').subscribe({
      next: (respuesta) => {
        this.estado.set('listo');
        // El mensaje del servidor, no uno inventado aquí: el API responde igual para un correo
        // nuevo y para uno ya apuntado, y esta pantalla no debe sugerir que sabe cuál de los dos
        // fue.
        this.mensajeDeExito.set(respuesta?.message?.trim() || 'Listo. Vas a recibir la información del Plan en ese correo.');
        this.correo.set('');
        this.autoriza.set(false);
      },
      error: (error: any) => {
        this.estado.set('confirmando');
        this.mensajeDeError.set(this.mensajeDelError(error));
      },
    });
  }

  /**
   * El motivo que da el servidor cuando lo hay, y uno propio cuando no.
   *
   * El 429 se nombra aparte porque es el único que se arregla esperando, y decir «error» a secas
   * invita a volver a pulsar, que es justo lo que lo mantiene cerrado.
   */
  private mensajeDelError(error: any): string {
    if (error?.status === 429) {
      return 'Demasiados intentos seguidos. Espera un minuto y vuelve a intentarlo.';
    }

    const delServidor = typeof error?.payload?.message === 'string' ? error.payload.message.trim() : '';
    if (error?.status === 400 && delServidor.length > 0) {
      return delServidor;
    }

    return 'No pudimos registrar tu correo. Intenta de nuevo en unos minutos.';
  }

  volverAEmpezar(): void {
    this.estado.set('reposo');
    this.mensajeDeExito.set('');
    this.mensajeDeError.set('');
  }
}
