import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService, UsuarioDelSistema } from '../../../core/services/admin.service';
import { AdminUsersPanelComponent } from './admin-users-panel.component';

const GESTORA: UsuarioDelSistema = {
  id: '7', fullName: 'Ana Gestora', email: 'ana@pnmc.local', role: 'gestor_interno',
  roleLabel: 'Gestor', roles: ['gestor_interno'], isActive: true, lastLoginAt: null, telefono: null,
  apartadosActivos: 3, apartadosTotales: 16,
};
const WEBMASTER: UsuarioDelSistema = {
  id: '1', fullName: 'Web Master', email: 'web@pnmc.local', role: 'webmaster',
  roleLabel: 'Webmaster', roles: ['webmaster'], isActive: true, lastLoginAt: null, telefono: null,
};
const EXTERNA: UsuarioDelSistema = {
  id: '9', fullName: 'Cuenta Externa', email: 'ext@pnmc.local', role: 'externo',
  roleLabel: 'Externo', roles: ['externo'], isActive: true, lastLoginAt: null, telefono: null,
};

const CATALOGO = {
  modulos: ['monitor', 'solicitudes', 'agenda', 'catalogo-editorial'],
  siempreActivados: ['monitor', 'solicitudes'],
};

/**
 * Qué módulos de la consola tiene activados cada cuenta.
 *
 * <b>ESTO NO ES UN MENU RECORTADO.</b> Lo que se decide en esta pantalla lo aplica el servidor: sin
 * el módulo, la ruta responde 403 aunque se escriba a mano. Aquí se comprueba la otra mitad: que la
 * pantalla ofrezca exactamente lo que se puede decidir, y que no ofrezca lo que no.
 */
describe('los módulos de una cuenta', () => {
  let fixture: ComponentFixture<AdminUsersPanelComponent>;
  let componente: AdminUsersPanelComponent;
  let guardado: { id: string; modulos: readonly string[] } | null;
  let fallarAlGuardar: boolean;

  function montar(modulosDeLaCuenta: string[], webmaster = false, catalogo = CATALOGO): void {
    guardado = null;
    fallarAlGuardar = false;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AdminUsersPanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AdminService, useValue: {
          cargarCuentasAdministrativas: () => of([GESTORA, WEBMASTER, EXTERNA]),
          desactivarCuenta: () => of(undefined),
          eliminarCuentaDefinitivamente: () => of(undefined),
          cargarModulosDisponibles: () => of(catalogo),
          cargarModulosDeCuenta: () => of({
            idUsuario: 7, modulos: modulosDeLaCuenta,
            siempreActivados: CATALOGO.siempreActivados, todosPorSerWebmaster: webmaster,
          }),
          guardarModulosDeCuenta: (id: string, modulos: readonly string[]) => {
            if (fallarAlGuardar) { return throwError(() => new Error('no se pudo')); }
            guardado = { id, modulos };
            // EL DOBLE CONTESTA LO QUE CONTESTA EL SERVIDOR: la lista canónica, sin repetidos.
            // Concatenar los fijos a ciegas los duplicaba en cuanto el cliente empezó a mandarlos
            // con el resto, y entonces la cifra que la pantalla lee del servidor salía inflada.
            return of({
              idUsuario: 7,
              modulos: [...new Set([...modulos, ...CATALOGO.siempreActivados])],
              siempreActivados: CATALOGO.siempreActivados, todosPorSerWebmaster: webmaster,
            });
          },
        } },
      ],
    });
    fixture = TestBed.createComponent(AdminUsersPanelComponent);
    componente = fixture.componentInstance;
    componente.users.set([GESTORA, WEBMASTER, EXTERNA]);
    fixture.detectChanges();
  }

  function casillas() {
    return componente.casillasDeModulo();
  }

  it('ofrece los módulos del servidor y marca los que la cuenta tiene', () => {
    // EL CATALOGO SALE DEL SERVIDOR y no de la barra izquierda: los permisos los aplica él, así que
    // ofrecer un módulo que no conoce sería ofrecer una llave sin cerradura.
    montar(['monitor', 'solicitudes', 'agenda']);
    componente.verFicha(GESTORA);
    fixture.detectChanges();

    expect(casillas().map(c => c.codigo)).toEqual(CATALOGO.modulos);
    expect(casillas().find(c => c.codigo === 'agenda')!.activo).toBeTrue();
    expect(casillas().find(c => c.codigo === 'catalogo-editorial')!.activo).toBeFalse();
  });

  it('un módulo que el servidor conoce pero la barra no ofrece no tiene casilla', () => {
    // «galeria» sigue en el catálogo del servidor para el día que exista, pero se retiró de la
    // navegación: una casilla para él sería una llave sin puerta.
    montar(['monitor', 'solicitudes'], false, { ...CATALOGO, modulos: [...CATALOGO.modulos, 'galeria'] });
    componente.verFicha(GESTORA);
    fixture.detectChanges();

    expect(casillas().map(c => c.codigo)).toEqual(CATALOGO.modulos);
  });

  it('las casillas se agrupan por familia de la barra, con lo que abre cada apartado', () => {
    // ERA UNA LISTA PLANA de dieciséis casillas al pie de una tarjeta, sin decir qué abre cada una.
    // La estructura es la de la navegación, reutilizada: si un apartado cambia de familia allí,
    // cambia aquí sin tocar nada.
    montar(['monitor', 'solicitudes', 'agenda']);
    componente.verFicha(GESTORA);
    fixture.detectChanges();

    const grupos = componente.gruposDeModulos();
    expect(grupos.map(g => g.id)).toEqual(['bandeja', 'publicaciones']);
    const agenda = grupos.find(g => g.id === 'publicaciones')!.modulos.find(m => m.codigo === 'agenda')!;
    expect(agenda.etiqueta).toBe('Agenda y eventos');
    expect(agenda.descripcion.length).toBeGreaterThan(10);
    expect(componente.resumenDeModulos()).toEqual({ activos: 3, total: 4 });
    expect(fixture.nativeElement.querySelector('[data-gestion-de-modulos]').textContent).toContain('Bandeja de trabajo');
  });

  it('los dos de siempre se ven puestos y no se pueden tocar', () => {
    // NO DESAPARECEN: si desaparecieran parecería que el módulo no existe. Se ven puestos y fijos.
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);
    fixture.detectChanges();

    for (const codigo of CATALOGO.siempreActivados) {
      const casilla = casillas().find(c => c.codigo === codigo)!;
      expect(casilla.activo).withContext(codigo).toBeTrue();
      expect(casilla.fijo).withContext(codigo).toBeTrue();
    }
    expect(casillas().find(c => c.codigo === 'agenda')!.fijo).toBeFalse();
  });

  it('intentar quitar uno de los de siempre no manda nada', () => {
    montar(['monitor', 'solicitudes', 'agenda']);
    componente.verFicha(GESTORA);

    componente.alternarModulo('monitor');

    expect(guardado).toBeNull();
  });

  it('activar un módulo guarda el conjunto entero, no la diferencia', () => {
    // La pantalla es una lista de casillas y lo que la persona decide es el conjunto. Con un
    // «añade» y un «quita» dos pestañas abiertas podrían dejar un estado que nadie eligió.
    montar(['monitor', 'solicitudes', 'agenda']);
    componente.verFicha(GESTORA);

    componente.alternarModulo('catalogo-editorial');

    expect(guardado!.id).toBe('7');
    expect([...guardado!.modulos].sort()).toEqual(['agenda', 'catalogo-editorial', 'monitor', 'solicitudes']);
  });

  it('desactivar un módulo lo saca del conjunto', () => {
    montar(['monitor', 'solicitudes', 'agenda']);
    componente.verFicha(GESTORA);

    componente.alternarModulo('agenda');

    expect(guardado!.modulos).not.toContain('agenda');
  });

  it('«Todo el grupo» concede el grupo entero en una sola petición', () => {
    // CONCEDER ES UNA DECISION DE CONJUNTO: quien entrega una cuenta decide «esta lleva
    // Publicaciones», no casilla por casilla. Catorce gestos eran catorce peticiones, y a media
    // faena la cuenta quedaba con un conjunto que nadie había decidido.
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);

    componente.alternarGrupo('publicaciones', true);

    expect(guardado!.modulos).toContain('catalogo-editorial');
    expect(guardado!.modulos).toContain('agenda');
  });

  it('cuando el grupo ya está entero, el mismo control lo quita', () => {
    montar(['monitor', 'solicitudes', 'agenda', 'catalogo-editorial']);
    componente.verFicha(GESTORA);

    expect(componente.grupoCompleto('publicaciones')).toBeTrue();
    componente.alternarGrupo('publicaciones', false);

    expect(guardado!.modulos).not.toContain('agenda');
    expect(guardado!.modulos).not.toContain('catalogo-editorial');
  });

  it('«Marcar todos» concede todo lo que se puede conceder', () => {
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);

    componente.concederTodo();

    expect([...guardado!.modulos].sort()).toEqual(['agenda', 'catalogo-editorial', 'monitor', 'solicitudes']);
  });

  it('«Dejar solo lo mínimo» conserva los dos que no se pueden quitar', () => {
    // MUTANTE QUE MATA: mandar el conjunto vacío. El verbo dice «lo mínimo» y no «nada» porque
    // Resumen operativo y Solicitudes no se pueden quitar: prometer que se queda sin nada sería
    // mentir, y mandar vacío dejaría el envío discrepando de lo que la pantalla enseña.
    montar(['monitor', 'solicitudes', 'agenda', 'catalogo-editorial']);
    componente.verFicha(GESTORA);

    componente.dejarSoloLoMinimo();

    expect([...guardado!.modulos].sort()).toEqual(['monitor', 'solicitudes']);
  });

  it('al guardar se dice que se guardó, y la fila de la lista se pone al día', () => {
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);
    expect(componente.apartadosGuardados()).toBeFalse();

    componente.alternarModulo('agenda');
    fixture.detectChanges();

    expect(componente.apartadosGuardados()).toBeTrue();
    // LA LISTA DE ATRAS DECIA LO DE ANTES: dos sitios de la misma pantalla contándose distinto es
    // lo que el proyecto llama «la pantalla se contradice a sí misma».
    expect(componente.users().find(u => u.id === '7')!.apartadosActivos).toBe(3);
  });

  it('la lista dice qué puede abrir cada cuenta, y distingue los tres casos', () => {
    montar(['monitor', 'solicitudes']);

    expect(componente.apartadosDe(GESTORA)).toBe('3 de 16');
    // UN WEBMASTER LOS TIENE POR LO QUE ES, no por concesión: «16 de 16» haría creer que alguien se
    // los concedió uno a uno y que se le pueden quitar.
    expect(componente.apartadosDe(WEBMASTER)).toBe('Todos');
    // Y UNA CUENTA EXTERNA NO ENTRA A LA CONSOLA: no tiene apartados que contar.
    expect(componente.apartadosDe(EXTERNA)).toBe('No entra a la consola');
  });

  it('la ficha dice el rol con su nombre, no con su código', () => {
    // LA LISTA DECIA «Gestor» Y LA FICHA «gestor_interno», dos centímetros más a la derecha: la
    // misma cuenta dicha de dos maneras en la misma pantalla.
    montar(['monitor', 'solicitudes']);

    expect(componente.etiquetasDeRol(GESTORA)).toBe('Gestor');
    expect(componente.etiquetasDeRol(EXTERNA)).toBe('Externo');
  });

  it('a un webmaster se le ven todos y ninguno se puede tocar', () => {
    // SI SE PUDIERAN QUITAR, la última cuenta capaz de administrar usuarios podría dejar al
    // Programa sin forma de conceder permisos a nadie, y eso no se arregla desde la interfaz.
    montar(CATALOGO.modulos, true);
    componente.verFicha(WEBMASTER);
    fixture.detectChanges();

    expect(casillas().every(c => c.fijo)).toBeTrue();
    componente.alternarModulo('agenda');
    expect(guardado).toBeNull();
  });

  it('una cuenta externa no ofrece módulos: no entra a la consola', () => {
    montar(['monitor', 'solicitudes']);
    componente.verFicha(EXTERNA);
    fixture.detectChanges();

    expect(componente.modulosDeLaCuenta()).toBeNull();
    expect(casillas()).toEqual([]);
  });

  it('si guardar falla lo dice y no finge que se guardó', () => {
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);
    fallarAlGuardar = true;

    componente.alternarModulo('agenda');
    fixture.detectChanges();

    expect(componente.errorDeModulos()).toContain('siguen como estaban');
    expect(componente.modulosDeLaCuenta()!.modulos).not.toContain('agenda');
  });

  it('el rótulo de cada módulo es el que se lee en la barra izquierda', () => {
    // ESCRIBIRLO AQUI OTRA VEZ SERIA UNA SEGUNDA LISTA DE NOMBRES esperando a divergir de la barra.
    expect(componente ? true : true).toBeTrue();
    montar(['monitor', 'solicitudes']);
    componente.verFicha(GESTORA);
    fixture.detectChanges();

    expect(casillas().find(c => c.codigo === 'agenda')!.etiqueta).toBe('Agenda y eventos');
    expect(casillas().find(c => c.codigo === 'monitor')!.etiqueta).toBe('Resumen operativo');
  });
});
