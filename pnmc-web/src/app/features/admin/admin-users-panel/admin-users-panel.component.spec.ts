import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService, UsuarioDelSistema } from '../../../core/services/admin.service';
import { AdminUsersPanelComponent } from './admin-users-panel.component';

const USUARIOS: UsuarioDelSistema[] = [
  { id: '1', fullName: 'Ana Gestora', email: 'ana@example.org', role: 'gestor_interno', roleLabel: 'Gestor', roles: ['gestor_interno'], isActive: true, lastLoginAt: null, telefono: null },
  { id: '2', fullName: 'Web Master', email: 'web@example.org', role: 'webmaster', roleLabel: 'Webmaster', roles: ['webmaster'], isActive: false, lastLoginAt: null, telefono: null },
];

describe('AdminUsersPanelComponent', () => {
  let fixture: ComponentFixture<AdminUsersPanelComponent>;
  let component: AdminUsersPanelComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminUsersPanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AdminService, useValue: {
          cargarCuentasAdministrativas: () => of(USUARIOS),
          desactivarCuenta: () => of(undefined),
          eliminarCuentaDefinitivamente: () => of(undefined),
          guardarCuentaAdministrativa: () => of({ user: USUARIOS[0] }),
          // ABRIR LA FICHA DE UNA CUENTA ADMINISTRATIVA PREGUNTA QUE MODULOS TIENE, porque los
          // permisos de la consola se conceden por cuenta. Sin estos dos dobles, abrirla revienta.
          cargarModulosDisponibles: () => of({ modulos: ['monitor', 'solicitudes', 'agenda'], siempreActivados: ['monitor', 'solicitudes'] }),
          cargarModulosDeCuenta: () => of({ idUsuario: 1, modulos: ['monitor', 'solicitudes'], siempreActivados: ['monitor', 'solicitudes'], todosPorSerWebmaster: false }),
          guardarModulosDeCuenta: () => of({ idUsuario: 1, modulos: ['monitor', 'solicitudes', 'agenda'], siempreActivados: ['monitor', 'solicitudes'], todosPorSerWebmaster: false }),
        } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminUsersPanelComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    component.users.set(USUARIOS);
  });

  it('filtra por texto, rol y estado sin alterar la lista recibida', () => {
    component.busqueda.set('web@');
    component.filtroRol.set('webmaster');
    component.filtroEstado.set('inactivos');

    expect(component.usuariosFiltrados().map(usuario => usuario.id)).toEqual(['2']);
    expect(component.users().length).toBe(2);
  });

  it('abrir la ficha ya no arrastra consigo la auditoría de la cuenta', () => {
    // ANTES LA PEDIA SIEMPRE: diez líneas de bitácora en cada apertura de ficha, para un dato que
    // casi nadie mira. Es la misma decisión que ya habían tomado las otras cuatro pantallas.
    component.verFicha(USUARIOS[0]);

    expect(component.usuarioSeleccionado()?.id).toBe('1');
    http.expectNone(p => p.url.includes('/admin/auditoria'));
  });

  it('el historial de la cuenta lo abre la pieza compartida, pidiendo la tabla Usuarios', () => {
    // ERA LA CUARTA LISTA ESCRITA A MANO, y con los dos defectos de siempre: enseñaba el verbo
    // crudo de la bitácora —«actualizar»— en vez de lo que hizo la persona, y fechaba con
    // `date: 'short'` sin locale, que en este proyecto significa inglés.
    component.verFicha(USUARIOS[0]);
    component.historialAbierto.set(USUARIOS[0]);
    fixture.detectChanges();

    const peticion = http.expectOne(p => p.url.includes('/admin/auditoria'));
    expect(peticion.request.urlWithParams).toContain('tabla=Usuarios');
    expect(peticion.request.urlWithParams).toContain('registroId=1');
    peticion.flush({ items: [] });
    fixture.detectChanges();

    // DICE «HISTORIAL» A SECAS Y NO «HISTORIAL Y PROCEDENCIA»: esta pantalla no tiene a mano la
    // procedencia de la cuenta, y el panel no afirma nada que no le hayan dicho.
    expect(fixture.nativeElement.textContent).toContain('Historial');
    expect(fixture.nativeElement.textContent).not.toContain('anterior al seguimiento de procedencia');
  });

  describe('desactivar, reactivar y eliminar, según el estado', () => {
    it('una cuenta activa ofrece desactivar; una inactiva, reactivar y eliminar', () => {
      // UNA ACCION SOLO SI APLICA AL ESTADO. Reactivar no existía como acción —había que abrir el
      // editor y marcar una casilla— y eliminar no existía en absoluto (15 de septiembre de 2026).
      expect(component.accionesDe(USUARIOS[0]).map(a => a.id)).toEqual(['ficha', 'editar', 'desactivar']);
      expect(component.accionesDe(USUARIOS[1]).map(a => a.id)).toEqual(['ficha', 'editar', 'reactivar', 'eliminar']);
    });

    it('eliminar pide confirmación, llama a la eliminación definitiva y saca la cuenta de la lista', () => {
      const servicio = TestBed.inject(AdminService);
      const eliminar = spyOn(servicio, 'eliminarCuentaDefinitivamente').and.returnValue(of(undefined));
      component.verFicha(USUARIOS[1]);

      // PIDE ANTES DE HACER: la primera llamada solo abre la confirmación del proyecto.
      component.eliminar(USUARIOS[1]);
      expect(eliminar).not.toHaveBeenCalled();
      expect(component.tituloDeLaConfirmacion()).toBe('Eliminar la cuenta');
      expect(component.detalleDeLaConfirmacion()).toContain('no se puede deshacer');

      component.confirmarLaDecision();

      expect(eliminar).toHaveBeenCalledWith('2');
      expect(component.confirmacion()).toBeNull();
      expect(component.users().map(u => u.id)).toEqual(['1']);
      expect(component.usuarioSeleccionado()).toBeNull();
      expect(component.message()).toContain('eliminada');
    });

    it('sin confirmar no se elimina nada, y una cuenta activa nunca se elimina', () => {
      const servicio = TestBed.inject(AdminService);
      const eliminar = spyOn(servicio, 'eliminarCuentaDefinitivamente').and.returnValue(of(undefined));

      component.eliminar(USUARIOS[1]);
      component.cerrarLaConfirmacion();
      // Una cuenta activa no llega ni a preguntar: primero se desactiva.
      component.eliminar(USUARIOS[0]);

      expect(eliminar).not.toHaveBeenCalled();
      expect(component.confirmacion()).toBeNull();
      expect(component.users().length).toBe(2);
    });

    it('si el servidor se niega, se dice con su motivo y la cuenta sigue en la lista', () => {
      // «tiene 2 actuaciones en la bitácora» es lo que hay que leer, no un genérico.
      const servicio = TestBed.inject(AdminService);
      spyOn(servicio, 'eliminarCuentaDefinitivamente').and.returnValue(
        throwError(() => ({ message: 'No fue posible eliminar la cuenta', payload: { message: '«Web Master» tiene 2 actuaciones en la bitácora y no se puede eliminar sin perder la trazabilidad. Queda desactivada.' } })));
      component.eliminar(USUARIOS[1]);
      component.confirmarLaDecision();

      expect(component.message()).toContain('2 actuaciones');
      // EL DIALOGO SIGUE ABIERTO CON LA NEGATIVA DENTRO: el motivo se lee donde se pulsó.
      expect(component.errorDeLaConfirmacion()).toContain('2 actuaciones');
      expect(component.confirmacion()).not.toBeNull();
      expect(component.users().length).toBe(2);
    });

    it('reactivar guarda la cuenta con isActive en cierto, sin abrir el editor', () => {
      const servicio = TestBed.inject(AdminService);
      const guardar = spyOn(servicio, 'guardarCuentaAdministrativa').and.returnValue(of({ user: { ...USUARIOS[1], isActive: true } }));

      component.reactivar(USUARIOS[1]);

      expect(guardar).toHaveBeenCalledWith(jasmine.objectContaining({ id: '2', isActive: true, roles: ['webmaster'] }));
      expect(component.users().find(u => u.id === '2')?.isActive).toBeTrue();
      expect(component.editorAbierto()).toBeFalse();
    });
  });

  afterEach(() => http.verify());
});
