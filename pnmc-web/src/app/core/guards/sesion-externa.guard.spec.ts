import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { sesionExternaGuard } from './sesion-externa.guard';
import { ExternalSessionService } from '../services/external-session.service';
import { routes } from '../../app.routes';

/**
 * El guard del panel externo.
 *
 * <b>LO QUE ESTAS PRUEBAS FIJAN.</b> Hasta abrir
 * <code>/ecosistema/mi-panel</code> sin sesión pintaba el formulario de acceso DENTRO del panel:
 * la barra de direcciones decía «mi-panel» mientras la pantalla pedía credenciales.
 */
describe('sesionExternaGuard', () => {
  let dentro: boolean;
  let respuestaDelServidor: unknown;
  let vecesQueConsulto: number;

  const SESION = {
    userId: '13',
    fullName: 'Camila Prueba Responsable',
    email: 'creacionorgprueba@pnmc.test',
    accountStatus: 'activo',
    organizations: [{ id: '117', name: 'CreacionOrgPrueba', role: 'administrador' }],
  };

  beforeEach(() => {
    dentro = false;
    respuestaDelServidor = null;
    vecesQueConsulto = 0;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ExternalSessionService,
          useValue: {
            dentro: () => dentro,
            refrescar: () => {
              vecesQueConsulto += 1;
              return of(respuestaDelServidor);
            },
          },
        },
      ],
    });
  });

  const correr = () =>
    TestBed.runInInjectionContext(() => sesionExternaGuard(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot,
    ));

  it('con la sesión ya cargada deja pasar sin preguntarle al servidor', () => {
    dentro = true;

    expect(correr()).toBe(true);
    // Preguntar de todas formas añadiría una espera de red a cada navegación interna del panel.
    expect(vecesQueConsulto).withContext('no debería haber consultado').toBe(0);
  });

  it('sin señal pero con cookie válida, deja pasar', (done) => {
    respuestaDelServidor = SESION;

    (correr() as Observable<boolean | UrlTree>).subscribe((resultado) => {
      // AL ABRIR UN ENLACE GUARDADO O AL RECARGAR la señal nace vacía aunque la cookie siga
      // siendo válida. Mirar solo la señal echaría fuera a quien sí tiene sesión, que es un
      // fallo peor que el que este guard corrige.
      expect(resultado).toBe(true);
      expect(vecesQueConsulto).toBe(1);
      done();
    });
  });

  it('sin sesión manda a la puerta de fuera, no al panel vacío', (done) => {
    respuestaDelServidor = null;
    const router = TestBed.inject(Router);

    (correr() as Observable<boolean | UrlTree>).subscribe((resultado) => {
      expect(resultado)
        .withContext('debería denegar con una redirección, no con `false` ni con `true`')
        .toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(resultado as UrlTree)).toBe('/ingresar');
      done();
    });
  });

  it('la ruta del panel lo declara, que es lo único que lo pone en servicio', () => {
    // Un guard que ninguna ruta declara no protege nada, por correcto que sea su interior.
    const panel = routes.find((r) => r.path === 'gestion');

    expect(panel).withContext('no existe la ruta del panel').toBeTruthy();
    expect(panel!.canActivate).withContext('la ruta no declara ningún guard').toBeTruthy();
    expect(panel!.canActivate).toContain(sesionExternaGuard);
  });
});
