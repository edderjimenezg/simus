import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { AdminService } from './admin.service';

export interface OrganizacionDeSesion {
  id: string;
  name: string;
  role: string;
}

export interface SesionExterna {
  userId: string;
  fullName: string;
  email: string;
  accountStatus: string;
  organizations: OrganizacionDeSesion[];
  /**
   * Si esta es la primera vez que la cuenta entra.
   *
   * <b>SOLO VIENE EN CIERTO EN LA RESPUESTA DEL INGRESO.</b> `/external/auth/me` lo devuelve
   * siempre en falso, y es correcto: para entonces la cuenta ya entró, y una bienvenida que
   * reaparece en cada recarga deja de ser una bienvenida. Opcional en el tipo porque las sesiones
   * que se recuperan con `cargarUnaVez()` no lo traen.
   */
  esPrimerIngreso?: boolean;
}

/**
 * La sesión externa, para todo el sitio público.
 *
 * <b>Por qué existe y no vive dentro de la página de acceso.</b> Hasta ahora la única pantalla que
 * sabía si había alguien dentro era `/registro`, porque era la única que llamaba a
 * `/external/auth/me`. El resto del portal —la barra de navegación incluida— pintaba «Iniciar
 * sesión» tanto si la organización acababa de entrar como si no había entrado nunca: la sesión
 * existía en el servidor y en la cookie, pero no en la pantalla.
 *
 * <b>Se consulta una sola vez por carga.</b> `cargarUnaVez()` es idempotente a propósito: la barra
 * de navegación se instancia en el armazón y la página de acceso también quiere la sesión; sin la
 * guarda, cada visita a `/registro` haría dos peticiones idénticas.
 *
 * <b>Un 401 no es un error que reportar.</b> Es la respuesta normal para un visitante anónimo, que
 * es la mayoría. Por eso `catchError` deja la sesión en `null` y sigue: si esto propagara el fallo,
 * cada carga del sitio sin sesión pintaría un aviso de error.
 */
@Injectable({ providedIn: 'root' })
export class ExternalSessionService {
  private readonly adminService = inject(AdminService);
  private readonly sesion = signal<SesionExterna | null>(null);
  private consultada = false;

  /** La sesión tal cual, o `null` si no hay ninguna. */
  readonly actual = this.sesion.asReadonly();

  readonly dentro = computed(() => this.sesion() !== null);

  readonly organizaciones = computed<OrganizacionDeSesion[]>(() => this.sesion()?.organizations ?? []);

  /**
   * La organización con la que se identifica la sesión en la barra.
   *
   * Es la primera de la lista, y el API las devuelve ordenadas por nombre justamente para que esta
   * elección no cambie entre dos cargas de la misma página.
   */
  readonly organizacionPrincipal = computed<OrganizacionDeSesion | null>(() => this.organizaciones()[0] ?? null);

  /**
   * Lo que se lee en el botón del panel.
   *
   * Cae al nombre de la persona si la cuenta no administra ninguna organización. Eso no debería
   * ocurrir —desde que el alta es un solo acto toda cuenta externa nace con una— pero una base a
   * medio migrar puede traer cuentas viejas, y un botón vacío es peor que uno con el nombre.
   */
  readonly rotulo = computed(() => this.organizacionPrincipal()?.name ?? this.sesion()?.fullName ?? '');

  /**
   * Lo que cabe en el chip de la barra: la primera palabra del rótulo.
   *
   * <b>Por qué.</b> El chip mide 11rem y recorta con puntos suspensivos. «Antiguo Colectivo de
   * Vientos y Cañas» se leía «Antiguo Colectivo de Vi…», que no es un nombre ni es nada. Lo pidió
   * el usuario sobre `/gestion`.
   *
   * <b>Lo que esto cuesta, medido y no supuesto.</b> En la base local hay ocho organizaciones y
   * <b>cinco empiezan por un genérico</b>: tres por «Asociación», una por «Corporación» y una por
   * «Fundación». Las tres «Asociación …» se leen IGUAL en el chip. El nombre completo sigue
   * entero un clic más allá, en el menú de cuenta, y ahí es donde se distinguen.
   *
   * No cae al nombre de la persona: eso ya lo decide {@link rotulo}, y aquí solo se acorta lo que
   * aquél devuelva.
   */
  readonly rotuloCorto = computed(() => this.rotulo().trim().split(/\s+/).filter(Boolean)[0] ?? '');

  /** Las iniciales para el distintivo circular. Máximo dos, que es lo que cabe. */
  readonly iniciales = computed(() => {
    const partes = this.rotulo().trim().split(/\s+/).filter(Boolean);
    if (partes.length === 0) return '';
    if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
    return (partes[0][0] + partes[1][0]).toUpperCase();
  });

  cargarUnaVez(): void {
    if (this.consultada) return;
    this.refrescar().subscribe();
  }

  refrescar(): Observable<SesionExterna | null> {
    this.consultada = true;
    return this.adminService.cargarSesionExterna().pipe(
      map(respuesta => (respuesta ?? null) as SesionExterna | null),
      tap(sesion => this.sesion.set(sesion)),
      catchError(() => {
        this.sesion.set(null);
        return of(null);
      }),
    );
  }

  /** Para quien acaba de entrar y ya tiene la respuesta del login en la mano. */
  establecer(sesion: SesionExterna | null): void {
    this.consultada = true;
    this.sesion.set(sesion);
  }

  /**
   * Cierra la sesión.
   *
   * LA SESIÓN LOCAL SE BORRA AUNQUE EL SERVIDOR FALLE. Si la petición cae y se dejara puesta, la
   * barra seguiría diciendo que hay alguien dentro y el botón de salir no volvería a servir de
   * nada: quien quiere salir tiene que salir.
   */
  salir(): Observable<unknown> {
    return this.adminService.cerrarSesionExterna().pipe(
      tap(() => this.sesion.set(null)),
      catchError(() => {
        this.sesion.set(null);
        return of(null);
      }),
    );
  }
}
