import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { AgendaComponent } from './pages/agenda/agenda.component';
import { TareasComponent } from './pages/tareas/tareas.component'; // 👈 Tu nuevo componente independiente de tareas
import { PacientesComponent } from './pages/pacientes/pacientes.component';
import { HistoriaClinicaComponent } from './pages/historia-clinica/historia-clinica.component';
import { OdontogramaComponent } from './pages/odontograma/odontograma.component';
import { LaboratorioComponent } from './pages/laboratorio/laboratorio.component';
import { PagosComponent } from './pages/pagos/pagos.component';
import { FinanzasComponent } from './pages/finanzas/finanzas.component';
import { ConfiguracionComponent } from './pages/configuracion/configuracion.component';
import { TurnosComponent } from './pages/turnos/turnos.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'turnos', component: TurnosComponent, canActivate: [authGuard] }, // 👈 Nueva ruta
  { path: 'turnos-tareas', redirectTo: 'turnos', pathMatch: 'full'}, // compatibilidad con links viejos
  { path: 'agenda', component: AgendaComponent, canActivate: [authGuard] },
  { path: 'tareas', component: TareasComponent, canActivate: [authGuard] }, // 👈 Ruta independiente para tareas
  { path: 'pacientes', component: PacientesComponent, canActivate: [authGuard] },
  { path: 'pacientes/:id/historia', component: HistoriaClinicaComponent, canActivate: [authGuard] },
  { path: 'pacientes/:id/odontograma', component: OdontogramaComponent, canActivate: [authGuard] },
  { path: 'laboratorio', component: LaboratorioComponent, canActivate: [authGuard] },
  { path: 'pagos', component: PagosComponent, canActivate: [authGuard] },
  { path: 'finanzas', component: FinanzasComponent, canActivate: [authGuard] },
  { path: 'configuracion', component: ConfiguracionComponent, canActivate: [authGuard] },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];