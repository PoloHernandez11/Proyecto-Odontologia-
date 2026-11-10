import { Routes } from '@angular/router';
import { invitadoGuard, permisoGuard, sesionGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';
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
import { SinAccesoComponent } from './pages/sin-acceso/sin-acceso.component';
import { TareasComponent } from './pages/tareas/tareas.component';
import { ElegirPacienteComponent } from './pages/elegir-paciente/elegir-paciente.component';
import { AceptarInvitacionComponent, ConfirmarCorreoComponent, RegistroComponent, RestablecerContrasenaComponent } from './pages/cuenta/cuenta.components';

export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [invitadoGuard] },
  // Pantallas públicas de cuenta. Las tres últimas se abren desde los enlaces que la API envía por correo.
  { path: 'registro', component: RegistroComponent, canActivate: [invitadoGuard] },
  { path: 'confirmar-correo', component: ConfirmarCorreoComponent },
  { path: 'restablecer-contrasena', component: RestablecerContrasenaComponent },
  { path: 'aceptar-invitacion', component: AceptarInvitacionComponent },
  {
    // Todas las pantallas internas comparten el mismo marco (menú lateral) y exigen sesión iniciada.
    path: '',
    component: ShellComponent,
    canActivate: [sesionGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'agenda', component: AgendaComponent, canActivate: [permisoGuard('agenda.read')] },
      { path: 'turnos-tareas', component: TareasComponent, canActivate: [permisoGuard('tasks.read', 'appointments.read')] },
      { path: 'pacientes', component: PacientesComponent, canActivate: [permisoGuard('patients.read')] },
      { path: 'historia-clinica', component: ElegirPacienteComponent, canActivate: [permisoGuard('clinical.read')], data: { titulo: 'Historia clínica', destino: '/historia-clinica' } },
      { path: 'odontograma', component: ElegirPacienteComponent, canActivate: [permisoGuard('odontogram.read')], data: { titulo: 'Odontograma', destino: '/odontograma' } },
      { path: 'historia-clinica/:id', component: HistoriaClinicaComponent, canActivate: [permisoGuard('clinical.read')] },
      { path: 'odontograma/:id', component: OdontogramaComponent, canActivate: [permisoGuard('odontogram.read')] },
      // Direcciones de la versión anterior
      { path: 'pacientes/:id/historia', redirectTo: 'historia-clinica/:id' },
      { path: 'pacientes/:id/odontograma', redirectTo: 'odontograma/:id' },
      { path: 'laboratorio', component: LaboratorioComponent, canActivate: [permisoGuard('lab.read')] },
      { path: 'pagos', component: PagosComponent, canActivate: [permisoGuard('finance.read')] },
      { path: 'finanzas', component: FinanzasComponent, canActivate: [permisoGuard('finance.read')] },
      { path: 'configuracion', component: ConfiguracionComponent },
      { path: 'sin-acceso', component: SinAccesoComponent },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '' }
];
