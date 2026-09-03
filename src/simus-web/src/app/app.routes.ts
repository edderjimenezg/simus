import { Routes } from '@angular/router';
import { AccesoPageComponent } from './acceso-page.component';
import { PanelOrganizacionComponent } from './panel-organizacion.component';
import { AyudaComponent } from './portal/ayuda.component';
import { HomeComponent } from './portal/home.component';
import { InstitucionalComponent } from './portal/institucional.component';
import { NAVEGACION_PORTAL } from './portal/portal-nav';
import { PortalLayoutComponent } from './portal/portal-layout.component';
import { SeccionPendienteComponent } from './portal/seccion-pendiente.component';

const rutasPendientesPortal = NAVEGACION_PORTAL
  .filter(elemento => !elemento.disponible)
  .map(elemento => ({
    path: elemento.ruta.slice(1),
    component: SeccionPendienteComponent,
    data: { titulo: elemento.etiqueta, motivo: elemento.motivo }
  }));

export const routes: Routes = [
  {
    path: '',
    component: PortalLayoutComponent,
    children: [
      { path: '', pathMatch: 'full', component: HomeComponent },
      { path: 'institucional', component: InstitucionalComponent },
      { path: 'ayuda', component: AyudaComponent },
      ...rutasPendientesPortal
    ]
  },
  { path: 'ingresar', component: AccesoPageComponent, data: { vista: 'ingresar' } },
  { path: 'registro', component: AccesoPageComponent, data: { vista: 'registro' } },
  { path: 'mi-panel', component: PanelOrganizacionComponent },
  { path: 'mi-panel/procesos', component: PanelOrganizacionComponent },
  { path: '**', redirectTo: '' }
];
