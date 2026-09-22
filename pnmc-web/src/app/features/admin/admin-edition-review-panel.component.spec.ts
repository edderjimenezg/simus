import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminService, AjustesDeEdicion } from '../../core/services/admin.service';
import { AdminEditionReviewPanelComponent } from './admin-edition-review-panel.component';

const VACIA: AjustesDeEdicion = { id: 0, edicionId: '9', estado: 'borrador', observacionGeneral: null, fechaEnvio: null, observaciones: [] };
class AdminServiceFalso {
  envio: { id: string; payload: any; enviar: boolean } | null = null;
  cargarAjustesInstitucionalesDeEdicion() { return of(VACIA); }
  guardarAjustesInstitucionalesDeEdicion(id: string, payload: any, enviar: boolean) { this.envio = { id, payload, enviar }; return of({ ...VACIA, ...payload, estado: enviar ? 'enviada' : 'borrador' }); }
}
describe('AdminEditionReviewPanelComponent', () => {
  let component: AdminEditionReviewPanelComponent; let api: AdminServiceFalso;
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [AdminEditionReviewPanelComponent], providers: [{ provide: AdminService, useClass: AdminServiceFalso }] }).compileComponents(); const fixture: ComponentFixture<AdminEditionReviewPanelComponent> = TestBed.createComponent(AdminEditionReviewPanelComponent); component = fixture.componentInstance; component.edicionId = '9'; api = TestBed.inject(AdminService) as unknown as AdminServiceFalso; fixture.detectChanges(); });
  it('envía una nota de campo al expediente de la edición y avisa al cerrar la cola', () => { let enviada = false; component.enviada.subscribe(() => enviada = true); component.general = 'Ajuste general'; component.agregar(); component.notas[0].campoId = 'director'; component.notas[0].campoEtiqueta = 'Director o directora'; component.notas[0].nota = 'Indica la dirección.'; component.guardar(true); expect(api.envio?.id).toBe('9'); expect(api.envio?.enviar).toBeTrue(); expect(api.envio?.payload.observaciones[0].campoId).toBe('director'); expect(enviada).toBeTrue(); });
  it('guardar borrador no emite el cierre de la fila institucional', () => { let enviada = false; component.enviada.subscribe(() => enviada = true); component.guardar(false); expect(api.envio?.enviar).toBeFalse(); expect(enviada).toBeFalse(); });
});
