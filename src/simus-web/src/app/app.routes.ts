import { Routes } from '@angular/router';
import { AccesoPageComponent } from './acceso-page.component';
import { PanelOrganizacionComponent } from './panel-organizacion.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'ingresar' },
  { path: 'ingresar', component: AccesoPageComponent, data: { vista: 'ingresar' } },
  { path: 'registro', component: AccesoPageComponent, data: { vista: 'registro' } },
  { path: 'mi-panel', component: PanelOrganizacionComponent },
  { path: 'mi-panel/procesos', component: PanelOrganizacionComponent },
  { path: '**', redirectTo: 'ingresar' }
];
