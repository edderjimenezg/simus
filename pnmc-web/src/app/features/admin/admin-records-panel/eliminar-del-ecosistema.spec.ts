import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminRecordsPanelComponent } from './admin-records-panel.component';
import { AdminService } from '../../../core/services/admin.service';
import { SessionService } from '../../../core/services/session.service';
import { ADMIN_MODULES } from '../domain/admin-config';

/**
 * ELIMINAR UN FESTIVAL PUBLICADO DEL ECOSISTEMA, DIRECTO DESDE LA CONSOLA.
 *
 * <b>El defecto que esto cierra.</b> El criterio es este: hasta
 * entonces un Festival publicado solo se archivaba si la propia organización pedía su retiro y un
 * funcionario aprobaba esa solicitud -pensando en un Festival duplicado o que incumple las bases,
 * donde no tiene sentido esperar a que quien lo publicó pida borrarlo primero-.
 *
 * <b>DESDE EL 12 DE SEPTIEMBRE DE 2026 ES UN DIALOGO, NO `window.prompt`.</b> Estas pruebas
 * espiaban `window.prompt` y `window.confirm`, que es exactamente el problema: la única acción
 * irreversible de la pantalla se pedía con dos cuadros del navegador —sin estilo, fuera del
 * recorrido de teclado de la consola y bloqueados sin aviso en algunos contextos—. Ahora se prueba
 * lo que de verdad ve quien administra: un formulario con motivo obligatorio.
 */
describe('AdminRecordsPanelComponent · eliminar del ecosistema', () => {
  let fixture: ComponentFixture<AdminRecordsPanelComponent>;
  let comp: AdminRecordsPanelComponent;
  let adminService: AdminService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminRecordsPanelComponent],
      providers: [
        { provide: AdminService, useValue: { cargarRegistrosDeLaConsola: () => of({ items: [], total: 0 }), cargarEsquemaDeLaBase: () => of({}), archivarFestival: () => of({}) } },
        { provide: SessionService, useValue: { session: () => null } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRecordsPanelComponent);
    comp = fixture.componentInstance;
    comp.module = ADMIN_MODULES.find((m) => m.id === 'festivals') ?? ADMIN_MODULES[0];
    comp.roleId = 'webmaster';
    comp.divipola = {};
    fixture.detectChanges();
    adminService = TestBed.inject(AdminService);
  });

  it('solo un registro con estado «publicado» puede eliminarse: los demás estados quedan fuera', () => {
    expect(comp.esFestivalPublicado({ status: 'publicado' })).toBe(true);
    expect(comp.esFestivalPublicado({ status: 'Publicado' })).toBe(true);
    expect(comp.esFestivalPublicado({ status: 'en_revision' })).toBe(false);
    expect(comp.esFestivalPublicado({ status: 'borrador' })).toBe(false);
    expect(comp.esFestivalPublicado({ status: 'archivado' })).toBe(false);
    expect(comp.esFestivalPublicado({})).toBe(false);
  });

  it('«Eliminar del ecosistema» solo se ofrece sobre un Festival publicado, y va al final y en rojo', () => {
    const publicado = comp.accionesDe({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    const borrador = comp.accionesDe({ id: '78', title: 'Otro Festival', status: 'borrador' } as never);

    const eliminar = publicado.find(a => a.id === 'eliminar');
    expect(eliminar?.etiqueta).toBe('Eliminar del ecosistema');
    // ES LO UNICO QUE NO SE DESHACE: el menú compartido la separa al final por el tono.
    expect(eliminar?.tono).toBe('peligro');
    expect(publicado[publicado.length - 1].id).toBe('eliminar');

    expect(borrador.some(a => a.id === 'eliminar')).toBeFalse();
  });

  it('sin motivo escrito, el botón no envía y no llama al servidor', () => {
    const espia = spyOn(adminService, 'archivarFestival').and.returnValue(of({}));

    comp.pedirEliminacion({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    comp.eliminarFestivalDelEcosistema('   ');

    expect(espia).not.toHaveBeenCalled();
    // EL DIALOGO SIGUE ABIERTO: cerrarlo sin hacer nada dejaría a quien administra sin saber si
    // la acción se ejecutó.
    expect(comp.eliminacionPedida()).not.toBeNull();
  });

  it('cancelar cierra el diálogo sin llamar al servidor', () => {
    const espia = spyOn(adminService, 'archivarFestival').and.returnValue(of({}));

    comp.pedirEliminacion({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    comp.cancelarEliminacion();

    expect(espia).not.toHaveBeenCalled();
    expect(comp.eliminacionPedida()).toBeNull();
  });

  it('con motivo, llama al endpoint de archivado con el id numérico y el motivo escrito', () => {
    const espia = spyOn(adminService, 'archivarFestival').and.returnValue(of({}));
    spyOn(comp, 'loadRecords');

    comp.pedirEliminacion({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    comp.eliminarFestivalDelEcosistema('  Duplicado de otro Festival ya publicado.  ');

    // MUTANTE QUE MATA: pasar el id como string en vez de convertirlo con `Number(...)`. El
    // endpoint institucional espera un id numérico en la URL, igual que el resto del circuito
    // institucional de Festivales. Y el motivo viaja sin los espacios de los lados.
    expect(espia).toHaveBeenCalledWith(77, 'Duplicado de otro Festival ya publicado.');
    expect(comp.loadRecords).toHaveBeenCalled();
    expect(comp.message()).toContain('eliminado');
    expect(comp.eliminacionPedida()).toBeNull();
  });

  it('si el servidor rechaza la eliminación, el error se enseña DENTRO del diálogo y el motivo escrito no se pierde', () => {
    spyOn(adminService, 'archivarFestival').and.returnValue(throwError(() => ({ message: 'Solo un Festival publicado puede eliminarse del ecosistema desde aquí.' })));
    spyOn(comp, 'loadRecords');

    comp.pedirEliminacion({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    comp.eliminarFestivalDelEcosistema('Duplicado de otro Festival.');

    // MUTANTE QUE MATA: cerrar el diálogo en el `error`, que es lo que hacía hasta el 17 de
    // septiembre de 2026. Ante un fallo pasajero, el motivo recién redactado se perdía y había
    // que escribirlo otra vez. El error va junto al campo, como en el mismo diálogo de Mercados.
    expect(comp.errorDeLaEliminacion()).toBe('Solo un Festival publicado puede eliminarse del ecosistema desde aquí.');
    // El diálogo sigue abierto: quien conserva el texto escrito es `app-confirmacion`, y su propia
    // prueba lo vigila («el error del servidor … no cierra el diálogo ni borra lo escrito»).
    expect(comp.eliminacionPedida()).not.toBeNull();
    expect(comp.loadRecords).not.toHaveBeenCalled();
  });

  it('cancelar tras un fallo limpia el error: al volver a abrir no se hereda el de la vez anterior', () => {
    spyOn(adminService, 'archivarFestival').and.returnValue(throwError(() => ({ message: 'No fue posible.' })));

    comp.pedirEliminacion({ id: '77', title: 'Festival de prueba', status: 'publicado' } as never);
    comp.eliminarFestivalDelEcosistema('Duplicado.');
    expect(comp.errorDeLaEliminacion()).toBe('No fue posible.');

    comp.cancelarEliminacion();

    expect(comp.errorDeLaEliminacion()).toBe('');
  });

  it('el menú de la fila usa los rótulos comunes de las otras cuatro pantallas', () => {
    // §13 DEL PLAN: «una acción equivalente debe mantener nombre, ubicación y comportamiento».
    // Aquí se leían «Ficha administrativa», «Historial y trazabilidad» y «Consultar» como
    // disparador, mientras las otras cuatro decían «Abrir ficha», «Historial» y «Acciones».
    const rotulos = comp.accionesDe({ id: '78', title: 'Otro Festival', status: 'borrador' } as never)
      .map(a => a.etiqueta);

    expect(rotulos).toContain('Abrir ficha');
    expect(rotulos).toContain('Historial');
    expect(rotulos).toContain('Previsualizar');
    expect(rotulos).not.toContain('Historial y trazabilidad');
    expect(rotulos).not.toContain('Ficha administrativa');
  });

  it('previsualizar mientras no se ve; abrirlo en el portal cuando ya se ve, nunca las dos', () => {
    const borrador = comp.accionesDe({ id: '78', status: 'borrador' } as never).map(a => a.id);
    const publicado = comp.accionesDe({ id: '77', status: 'publicado' } as never).map(a => a.id);

    expect(borrador).toContain('previsualizar');
    expect(borrador).not.toContain('ver');
    expect(publicado).toContain('ver');
    expect(publicado).not.toContain('previsualizar');
  });
});
