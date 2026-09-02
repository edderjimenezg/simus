import { Routes } from '@angular/router';
import { AccesoPageComponent } from './acceso-page.component';
import { PanelOrganizacionComponent } from './panel-organizacion.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: AccesoPageComponent },
  { path: 'mi-panel', component: PanelOrganizacionComponent },
  { path: 'mi-panel/procesos', component: PanelOrganizacionComponent },
  { path: '**', redirectTo: '' }
];
