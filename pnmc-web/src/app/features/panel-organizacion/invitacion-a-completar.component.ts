import { Component, computed, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LucideCircleAlert } from '@lucide/angular';
import { PerfilOrganizacion } from './panel-organizacion.api';
import { datosPendientes, frasePendiente } from './informacion-pendiente';

/**
 * La invitación a completar los datos de la organización.
 *
 * <b>RESUELVE §15.2 Y §15.3 CON LA MISMA PIEZA, EN DOS TONOS.</b> Las dos secciones del plan
 * responden a la misma pregunta —«¿qué le falta a esta organización?»— y se diferencian solo en
 * cuándo y con cuánta insistencia se dice:
 *
 * <ul>
 *   <li><b>Bienvenida (§15.2)</b>: tras el primer ingreso, una vez. «correo confirmado → primer
 *       login → bienvenida → información faltante → Completar organización», y con
 *       <b>«Completar después»</b>, porque ninguno de esos campos es obligatorio.</li>
 *   <li><b>Indicador (§15.3)</b>: permanente y discreto, mientras siga faltando algo.</li>
 * </ul>
 *
 * <b>SIN PORCENTAJES NI GAMIFICACION.</b> El §15.3 lo prohíbe expresamente salvo que aporten valor
 * real, y aquí no lo aportan: «perfil al 60 %» no le dice a nadie qué escribir. Se nombran los
 * campos —hasta dos— y se cuenta el resto.
 *
 * <b>NO APARECE SI NO FALTA NADA</b>, y tampoco mientras el perfil no se haya leído: un cartel
 * pintado sobre `null` diría que falta todo justo antes de descubrir que no falta nada.
 */
@Component({
  selector: 'app-invitacion-a-completar',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideCircleAlert],
  template: `
    @if (visible()) {
      @if (esBienvenida()) {
        <!--
          LA BIENVENIDA SE ANUNCIA. Aparece sola, sin que nadie la pida, justo después de entrar:
          quien usa lector de pantalla tiene que enterarse de que hay algo nuevo en la página.
        -->
        <section role="status"
          class="rounded-2xl border border-verde-medio/30 bg-verde/10 p-6"
          aria-labelledby="bienvenida-titulo">
          <p class="font-alternate text-xs font-bold uppercase tracking-[0.18em] text-verde-texto">{{ correoConfirmado() ? 'Tu correo quedó confirmado' : 'Tu organización quedó registrada' }}</p>
          <h2 id="bienvenida-titulo" class="mt-1 font-alternate text-lg font-bold uppercase tracking-wide text-morado">
            Te damos la bienvenida{{ nombre() ? ', ' + nombre() : '' }}
          </h2>
          <p class="mt-2 text-sm leading-relaxed text-slate-700">
            @if (correoConfirmado()) {
              Ya puedes registrar procesos y entregárselos al Programa.
            } @else {
              Mientras confirmas tu correo puedes ir completando los datos de tu organización.
            }
            <b>{{ frase() }}</b>
          </p>
          <p class="mt-1 text-sm text-slate-600">
            Ninguno de estos datos es obligatorio: puedes dejarlos para más adelante.
          </p>

          <div class="mt-5 flex flex-col gap-2 sm:flex-row">
            <a routerLink="/gestion/organizacion" (click)="cerrar()"
              class="rounded-lg bg-morado px-4 py-2.5 text-center text-sm font-bold text-white transition hover:bg-morado/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2"
            >Completar organización</a>
            <button type="button" (click)="cerrar()"
              class="rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-700 transition hover:border-morado hover:text-morado focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado focus-visible:ring-offset-2"
            >Completar después</button>
          </div>
        </section>
      } @else {
        <!--
          EL INDICADOR PERMANENTE. Ni «role=status» ni «alert»: no es una novedad, es un estado
          que lleva ahí desde que se entró, y anunciarlo en cada navegación sería ruido.
        -->
        <section class="flex flex-col gap-4 rounded-xl bg-morado/5 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
          aria-labelledby="pendiente-titulo">
          <div class="flex min-w-0 items-start gap-3">
            <svg lucideCircleAlert [size]="20" class="mt-0.5 shrink-0 text-morado-claro" aria-hidden="true"></svg>
            <div class="min-w-0">
              <p id="pendiente-titulo" class="text-cuerpo font-semibold text-valor">Tu organización tiene información pendiente por completar.</p>
              <p class="mt-0.5 text-cuerpo leading-relaxed text-prosa">{{ frase() }}</p>
            </div>
          </div>
          <a routerLink="/gestion/organizacion"
            class="inline-flex min-h-control shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-center text-dato font-bold text-slate-600 transition hover:border-morado hover:text-morado focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-morado-vivo focus-visible:ring-offset-2"
          >Completar organización</a>
        </section>
      }
    }
  `,
  /*
    EL ANFITRION TIENE QUE SER UN BLOQUE, Y NO TENERLO ERA EL DEFECTO.

    Un componente de Angular sin `display` declarado se queda en `display: inline`, que es el valor
    inicial de CSS. `panel-resumen` apila sus piezas con `space-y-6`, que en Tailwind 4 se resuelve
    como un `margin-bottom` sobre cada hijo menos el último… y los márgenes verticales NO SE
    APLICAN a una caja en línea. Resultado medido a 1500, 1280, 1024,
    768 y 390 px: 0 px de separación entre este aviso y el bloque de abajo, en los cinco anchos.
    El aviso no se superponía por estar mal colocado; se superponía porque su hueco no existía.
  */
  styles: [':host { display: block; }'],
})
export class InvitacionACompletarComponent {
  /** El perfil leído, o `null` mientras no se sabe. */
  readonly perfil = input<PerfilOrganizacion | null>(null);

  /**
   * Si esta es la primera vez que la cuenta entra.
   *
   * Lo dice el servidor en la respuesta del ingreso, leyendo `LastLoginAt` antes de sobrescribirlo.
   * `/externo/me` lo devuelve siempre en falso: una bienvenida que reaparece en cada recarga deja
   * de ser una bienvenida.
   */
  readonly primerIngreso = input(false);

  /** Cómo se llama quien entró, para saludarle. Opcional: sin nombre, el saludo se acorta. */
  readonly nombre = input('');

  /**
   * Si el correo de la cuenta ya está comprobado.
   *
   * <b>SE DESCUBRIO EN NAVEGADOR, Y ERA UNA AFIRMACION FALSA.</b> La bienvenida abría con «Tu
   * correo quedó confirmado» y decía «ya puedes registrar procesos» a una organización recién
   * registrada cuyo correo NO estaba confirmado —el caso normal mientras no haya proveedor de
   * correo—. Justo encima, el aviso de la misma pantalla decía lo contrario. El §15.2 secuencia la
   * bienvenida DESPUES de confirmar, y donde no se cumple esa secuencia lo que no puede pasar es
   * que la pantalla se contradiga a sí misma.
   *
   * No decide SI se da la bienvenida —eso lo decide el primer ingreso—, sino QUE dice: lo que ya
   * es cierto y no lo que debería serlo.
   */
  readonly correoConfirmado = input(false);

  /** Si se pulsó «Completar después». Solo afecta a la bienvenida de esta visita. */
  private readonly aplazada = signal(false);

  readonly pendientes = computed(() => datosPendientes(this.perfil()));
  readonly frase = computed(() => frasePendiente(this.perfil()));

  /**
   * La bienvenida solo en el primer ingreso y mientras no se aplace; el indicador, siempre que
   * falte algo.
   */
  readonly esBienvenida = computed(() => this.primerIngreso() && !this.aplazada());

  readonly visible = computed(() => this.pendientes().length > 0);

  cerrar(): void {
    this.aplazada.set(true);
  }
}
