import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService, UsuarioDelSistema } from '../../../core/services/admin.service';
import { AdminUsersPanelComponent } from './admin-users-panel.component';

const CON_DOCUMENTO: UsuarioDelSistema = {
  id: '7', fullName: 'Camila Gestora', email: 'camila@pnmc.local', role: 'gestor_interno',
  roleLabel: 'Gestor', roles: ['gestor_interno'], isActive: true, lastLoginAt: null,
  telefono: '3001112233', identificacion: '1020304050', tipoDocumento: 'cc',
  tipoDocumentoEtiqueta: 'Cédula de ciudadanía',
};
const SIN_DOCUMENTO: UsuarioDelSistema = {
  id: '8', fullName: 'Ana Sin Documento', email: 'ana@pnmc.local', role: 'gestor_interno',
  roleLabel: 'Gestor', roles: ['gestor_interno'], isActive: true, lastLoginAt: null, telefono: null,
};

const TIPOS = [
  { codigo: 'cc', etiqueta: 'Cédula de ciudadanía' },
  { codigo: 'pa', etiqueta: 'Pasaporte' },
];

/**
 * El perfil de una cuenta administrativa: quién es, y no solo con qué correo entra.
 *
 * <b>EL HUECO QUE ESTO CIERRA.</b> Una cuenta administrativa era un nombre y un correo: las columnas
 * de identificación existían en la base y nadie las leía. Lo señaló la dirección de producto el 15 de
 * septiembre de 2026 al fijar los permisos por cuenta: «eso hoy no está planteado».
 */
describe('el perfil de una cuenta administrativa', () => {
  let fixture: ComponentFixture<AdminUsersPanelComponent>;
  let componente: AdminUsersPanelComponent;
  let guardado: Record<string, unknown> | null;

  beforeEach(() => {
    guardado = null;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AdminUsersPanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AdminService, useValue: {
          cargarCuentasAdministrativas: () => of([CON_DOCUMENTO, SIN_DOCUMENTO]),
          desactivarCuenta: () => of(undefined),
          eliminarCuentaDefinitivamente: () => of(undefined),
          cargarTiposDeDocumento: () => of(TIPOS),
          cargarModulosDisponibles: () => of({ modulos: ['monitor'], siempreActivados: ['monitor'] }),
          cargarModulosDeCuenta: () => of({ idUsuario: 7, modulos: ['monitor'], siempreActivados: ['monitor'], todosPorSerWebmaster: false }),
          guardarCuentaAdministrativa: (payload: Record<string, unknown>) => {
            guardado = payload;
            return of({ user: CON_DOCUMENTO });
          },
        } },
      ],
    });
    fixture = TestBed.createComponent(AdminUsersPanelComponent);
    componente = fixture.componentInstance;
    componente.users.set([CON_DOCUMENTO, SIN_DOCUMENTO]);
    fixture.detectChanges();
  });

  it('el catálogo de tipos de documento sale del servidor', () => {
    // NO SE ESCRIBE EN EL FRONTEND: el proyecto ya tuvo dos copias de este catálogo y una había
    // divergido, aceptando dos códigos que la tabla no tenía.
    componente.startCreate();
    expect(componente.tiposDeDocumento()).toEqual(TIPOS);
  });

  it('el alta no pide quién es la persona: eso lo dice ella en su primer ingreso', () => {
    // UNA CUENTA ADMINISTRATIVA SE ENTREGA, NO SE RELLENA POR OTRO. quedó fijado
    //: «a ellos se les registra simplemente un correo y una contraseña
    // por defecto… una vez cambien la contraseña se les pide completar esos datos básicos». Pedir
    // aquí la cédula obliga a quien crea la cuenta a ir a buscarla, y deja un dato de identidad
    // escrito por un tercero.
    componente.startCreate();
    fixture.detectChanges();
    const html: HTMLElement = fixture.nativeElement;

    expect(html.querySelector('[data-identificacion]')).toBeNull();
    expect(html.querySelector('[data-tipo-documento]')).toBeNull();
    expect(html.querySelector('[data-como-se-entrega]')).not.toBeNull();
  });

  it('editar sí los enseña, porque ahí se corrigen', () => {
    componente.startEdit(CON_DOCUMENTO);
    fixture.detectChanges();
    const html: HTMLElement = fixture.nativeElement;

    expect(html.querySelector('[data-identificacion]')).not.toBeNull();
    expect(html.querySelector('[data-como-se-entrega]')).toBeNull();
  });

  it('editar una cuenta trae lo que ya tiene, para poder corregirlo', () => {
    // ABRIRLA EN BLANCO HARIA QUE GUARDAR UN CAMBIO DE ROL BORRARA SU IDENTIFICACION sin que nadie
    // lo pidiera.
    componente.startEdit(CON_DOCUMENTO);

    expect(componente.valoresDelFormulario().identificacion).toBe('1020304050');
    expect(componente.valoresDelFormulario().tipoDocumento).toBe('cc');
    expect(componente.valoresDelFormulario().telefono).toBe('3001112233');
  });

  it('un número sin su tipo no se guarda, y lo dice al lado del campo', () => {
    componente.startCreate();
    componente.valoresDelFormulario.update(f => ({ ...f, fullName: 'Nueva', email: 'nueva@pnmc.local', password: 'ClaveLarga123' }));
    componente.campoDelPerfil('identificacion', '1020304050');

    componente.enviarElFormulario();

    expect(componente.errorDelPerfil()).toContain('tipo de documento');
    expect(guardado).withContext('no debe enviarse').toBeNull();
  });

  it('un tipo sin número tampoco', () => {
    componente.startCreate();
    componente.valoresDelFormulario.update(f => ({ ...f, fullName: 'Nueva', email: 'nueva@pnmc.local', password: 'ClaveLarga123' }));
    componente.campoDelPerfil('tipoDocumento', 'cc');

    componente.enviarElFormulario();

    expect(componente.errorDelPerfil()).toContain('número');
    expect(guardado).toBeNull();
  });

  it('los dos juntos sí se guardan', () => {
    componente.startCreate();
    componente.valoresDelFormulario.update(f => ({ ...f, fullName: 'Nueva', email: 'nueva@pnmc.local', password: 'ClaveLarga123' }));
    componente.campoDelPerfil('tipoDocumento', 'cc');
    componente.campoDelPerfil('identificacion', '1020304050');

    componente.enviarElFormulario();

    expect(guardado!['identificacion']).toBe('1020304050');
    expect(guardado!['tipoDocumento']).toBe('cc');
  });

  it('una cuenta sin documento se sigue pudiendo guardar', () => {
    // NO SE EXIGE: ninguna de las cuentas que ya existen lo tiene, y exigirlo convertiría cambiar
    // un rol en un formulario que no se puede guardar sin ir a buscar una cédula.
    componente.startCreate();
    componente.valoresDelFormulario.update(f => ({ ...f, fullName: 'Nueva', email: 'nueva@pnmc.local', password: 'ClaveLarga123' }));

    componente.enviarElFormulario();

    expect(componente.errorDelPerfil()).toBeNull();
    expect(guardado).not.toBeNull();
  });

  it('la ficha enseña el tipo de documento en palabras y no su código', () => {
    componente.verFicha(CON_DOCUMENTO);
    fixture.detectChanges();

    const texto = fixture.nativeElement.querySelector('[data-documento-de-la-cuenta]')?.textContent ?? '';
    expect(texto).toContain('Cédula de ciudadanía');
    expect(texto).toContain('1020304050');
  });

  it('una cuenta sin documento lo dice en vez de dejar el hueco', () => {
    // UN HUECO HARIA DUDAR de si el dato no está o si la ficha no lo carga.
    componente.verFicha(SIN_DOCUMENTO);
    fixture.detectChanges();

    const texto = fixture.nativeElement.querySelector('[data-documento-de-la-cuenta]')?.textContent ?? '';
    expect(texto).toContain('Sin documento registrado');
  });
});
