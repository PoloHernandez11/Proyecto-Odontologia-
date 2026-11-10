import { Routes } from '@angular/router';
import { LoginComponent } from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { AgendaComponent } from './pages/agenda/agenda.component';
import { PacientesComponent } from './pages/pacientes/pacientes.component';
import { HistoriaClinicaComponent } from './pages/historia-clinica/historia-clinica.component';
import { OdontogramaComponent } from './pages/odontograma/odontograma.component';
import { LaboratorioComponent } from './pages/laboratorio/laboratorio.component';
import { PagosComponent } from './pages/pagos/pagos.component';
import { FinanzasComponent } from './pages/finanzas/finanzas.component';
import { ConfiguracionComponent } from './pages/configuracion/configuracion.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'agenda', component: AgendaComponent },
  { path: 'turnos-tareas', component: AgendaComponent },
  { path: 'pacientes', component: PacientesComponent },
  { path: 'pacientes/:id/historia', component: HistoriaClinicaComponent },
  { path: 'pacientes/:id/odontograma', component: OdontogramaComponent },
  { path: 'laboratorio', component: LaboratorioComponent },
  { path: 'pagos', component: PagosComponent },
  { path: 'finanzas', component: FinanzasComponent },
  { path: 'configuracion', component: ConfiguracionComponent },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];
