import { Routes } from '@angular/router';
import { invitadoGuard, permisoGuard, sesionGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';
import { LoginComponent } from './pages/login/login.component';

// El login y el marco se cargan con la aplicación; cada pantalla, recién cuando se abre (chunks diferidos).
export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [invitadoGuard] },
  // Pantallas públicas de cuenta. Las tres últimas se abren desde los enlaces que la API envía por correo.
  { path: 'registro', loadComponent: () => import('./pages/cuenta/cuenta.components').then(m => m.RegistroComponent), canActivate: [invitadoGuard] },
  { path: 'confirmar-correo', loadComponent: () => import('./pages/cuenta/cuenta.components').then(m => m.ConfirmarCorreoComponent) },
  { path: 'restablecer-contrasena', loadComponent: () => import('./pages/cuenta/cuenta.components').then(m => m.RestablecerContrasenaComponent) },
  { path: 'aceptar-invitacion', loadComponent: () => import('./pages/cuenta/cuenta.components').then(m => m.AceptarInvitacionComponent) },
  {
    // Todas las pantallas internas comparten el mismo marco (menú lateral) y exigen sesión iniciada.
    path: '',
    component: ShellComponent,
    canActivate: [sesionGuard],
    children: [
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'agenda', loadComponent: () => import('./pages/agenda/agenda.component').then(m => m.AgendaComponent), canActivate: [permisoGuard('agenda.read')] },
      { path: 'turnos-tareas', loadComponent: () => import('./pages/tareas/tareas.component').then(m => m.TareasComponent), canActivate: [permisoGuard('tasks.read', 'appointments.read')] },
      { path: 'pacientes', loadComponent: () => import('./pages/pacientes/pacientes.component').then(m => m.PacientesComponent), canActivate: [permisoGuard('patients.read')] },
      { path: 'historia-clinica', loadComponent: () => import('./pages/elegir-paciente/elegir-paciente.component').then(m => m.ElegirPacienteComponent), canActivate: [permisoGuard('clinical.read')], data: { titulo: 'Historia clínica', destino: '/historia-clinica' } },
      { path: 'odontograma', loadComponent: () => import('./pages/elegir-paciente/elegir-paciente.component').then(m => m.ElegirPacienteComponent), canActivate: [permisoGuard('odontogram.read')], data: { titulo: 'Odontograma', destino: '/odontograma' } },
      { path: 'historia-clinica/:id', loadComponent: () => import('./pages/historia-clinica/historia-clinica.component').then(m => m.HistoriaClinicaComponent), canActivate: [permisoGuard('clinical.read')] },
      { path: 'odontograma/:id', loadComponent: () => import('./pages/odontograma/odontograma.component').then(m => m.OdontogramaComponent), canActivate: [permisoGuard('odontogram.read')] },
      // Direcciones de la versión anterior
      { path: 'pacientes/:id/historia', redirectTo: 'historia-clinica/:id' },
      { path: 'pacientes/:id/odontograma', redirectTo: 'odontograma/:id' },
      { path: 'laboratorio', loadComponent: () => import('./pages/laboratorio/laboratorio.component').then(m => m.LaboratorioComponent), canActivate: [permisoGuard('lab.read')] },
      { path: 'pagos', loadComponent: () => import('./pages/pagos/pagos.component').then(m => m.PagosComponent), canActivate: [permisoGuard('finance.read')] },
      { path: 'finanzas', loadComponent: () => import('./pages/finanzas/finanzas.component').then(m => m.FinanzasComponent), canActivate: [permisoGuard('finance.read')] },
      { path: 'configuracion', loadComponent: () => import('./pages/configuracion/configuracion.component').then(m => m.ConfiguracionComponent) },
      { path: 'sin-acceso', loadComponent: () => import('./pages/sin-acceso/sin-acceso.component').then(m => m.SinAccesoComponent) },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '' }
];
