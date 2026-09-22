import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminLoginComponent } from './admin-login.component';
import { SessionService } from '../../../core/services/session.service';
import { AdminService } from '../../../core/services/admin.service';
import { DEV_ADMIN_ACCOUNTS } from './dev-accounts';
import { ADMIN_ROLES } from '../domain/admin-config';

/**
 * Quién entra por <code>/admin</code>.
 *
 * <b>El defecto que estas pruebas fijan.</b> Hasta la pantalla de acceso de
 * la consola interna ofrecía DOS cosas que no le corresponden: un botón «Ingresar al Portal de
 * Colaboradores Externos» y un acceso rápido a <code>externo&#64;pnmc.local</code>.
 *
 * <b>Por qué es un defecto y no una preferencia.</b> Se preguntó a la base:
 * <code>externo&#64;pnmc.local</code> tiene <code>CanalAcceso = 'externo'</code>, igual que
 * <code>participante.dos</code> y <code>participante.tres</code>; los únicos <code>interno</code>
 * son <code>admin&#64;pnmc.local</code> y <code>gestor&#64;pnmc.local</code>. La consola invitaba a
 * entrar por donde ese canal no entra, y el error de acceso que sigue se lee como un fallo del
 * sitio. La puerta de fuera es <code>/ecosistema/ingresar</code>.
 */
describe('AdminLoginComponent · quién entra por /admin', () => {
  let fixture: ComponentFixture<AdminLoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminLoginComponent],
      providers: [
        { provide: SessionService, useValue: { login: () => of(null), session: () => null } },
        { provide: AdminService, useValue: { cargarEsquemaDeLaBase: () => of({}) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminLoginComponent);
    fixture.detectChanges();
  });

  const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('no ofrece puerta al portal externo: esa es /ecosistema/ingresar', () => {
    expect(texto()).not.toMatch(/Portal de Colaboradores Externos/i);
  });

  it('el acceso rápido solo carga cuentas del canal interno', () => {
    const correos = Object.values(DEV_ADMIN_ACCOUNTS).map((c) => c.email);

    expect(correos).toEqual(['admin@pnmc.local', 'gestor@pnmc.local']);
    expect(texto()).not.toContain('externo@pnmc.local');
  });

  it('el rol «externo» sigue existiendo para etiquetar a quien es externo', () => {
    // Se retiro del ACCESO, no del vocabulario: `admin-users-panel` lo usa para nombrar a esas
    // personas. Si esta prueba se pone roja al quitar el rol, el panel de usuarios se queda sin
    // etiqueta y las pinta en crudo.
    expect(ADMIN_ROLES['externo']).toBeTruthy();
    expect(ADMIN_ROLES['externo'].label).toBeTruthy();
  });

  it('la clave de cada rol y su campo `id` dicen lo mismo', () => {
    // LO ENCONTRO UN MUTANTE QUE SOBREVIVIO. Cambie `id: 'externo'` por otra cosa esperando que
    // la prueba de arriba se pusiera roja, y no se puso: `ADMIN_ROLES` se indexa por la CLAVE del
    // objeto, no por ese campo. Pero la plantilla del acceso rapido recorre `Object.values(...)` y
    // busca las credenciales con `ROLE_CREDENTIALS[role.id]`, asi que si la clave y el `id` se
    // separan, ese rol DESAPARECE del acceso rapido sin error y sin que nadie lo note.
    const desalineados = Object.entries(ADMIN_ROLES)
      .filter(([clave, rol]) => clave !== rol.id)
      .map(([clave, rol]) => `${clave} → ${rol.id}`);

    expect(desalineados).withContext('roles cuya clave no coincide con su id').toEqual([]);
  });
});
