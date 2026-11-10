import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { iniciales } from '../core/api';
import { AuthService } from '../core/auth.service';

interface OpcionMenu { titulo: string; ruta: string; icono: string; permisos: string[]; }
interface GrupoMenu { titulo: string; opciones: OpcionMenu[]; }

const DIENTE = 'M7 3.5c2.1-.8 3.5.6 5 .6s2.9-1.4 5-.6c2.8 1.1 3.1 4.1 2 6.9-.7 1.7-1.4 3.1-1.9 5.2-.4 1.7-.9 3.5-2.2 3.5-1.1 0-1.3-2.1-1.9-3.8-.3-1-.5-1.5-1-1.5s-.7.5-1 1.5c-.6 1.7-.8 3.8-1.9 3.8-1.3 0-1.8-1.8-2.2-3.5-.5-2.1-1.2-3.5-1.9-5.2C3.9 7.6 4.2 4.6 7 3.5Z';

// Menú único de la aplicación. Cada opción se muestra sólo si el usuario tiene alguno de sus permisos.
const MENU: GrupoMenu[] = [
  { titulo: 'PRINCIPAL', opciones: [
    { titulo: 'Inicio', ruta: '/dashboard', icono: 'M3 11l9-8 9 8M5 10v10h14V10', permisos: [] },
    { titulo: 'Agenda', ruta: '/agenda', icono: 'M4 6h16v14H4zM4 10h16M8 3v4M16 3v4', permisos: ['agenda.read'] },
    { titulo: 'Turnos y tareas', ruta: '/turnos-tareas', icono: 'M4 4h16v16H4zM8 12l3 3 5-6', permisos: ['tasks.read', 'appointments.read'] },
    { titulo: 'Pacientes', ruta: '/pacientes', icono: 'M16 20v-1a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v1M9.5 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7M21 20v-1a4 4 0 0 0-3-3.8M15.5 4.2a3.5 3.5 0 0 1 0 6.6', permisos: ['patients.read'] }
  ] },
  { titulo: 'CLÍNICA', opciones: [
    { titulo: 'Historia clínica', ruta: '/historia-clinica', icono: 'M6 3h9l4 4v14H6zM14 3v5h5M9 13h7M9 17h7', permisos: ['clinical.read'] },
    { titulo: 'Odontograma', ruta: '/odontograma', icono: DIENTE, permisos: ['odontogram.read'] },
    { titulo: 'Laboratorio dental', ruta: '/laboratorio', icono: 'M9 3h6M10 3v6l-5 9a2 2 0 0 0 1.8 3h10.4a2 2 0 0 0 1.8-3l-5-9V3M7.5 15h9', permisos: ['lab.read'] }
  ] },
  { titulo: 'GESTIÓN', opciones: [
    { titulo: 'Pagos de pacientes', ruta: '/pagos', icono: 'M3 6h18v12H3zM3 10h18M7 15h3', permisos: ['finance.read'] },
    { titulo: 'Finanzas', ruta: '/finanzas', icono: 'M4 20V10M10 20V4M16 20v-7M21 20H3', permisos: ['finance.read'] }
  ] },
  { titulo: 'CUENTA', opciones: [
    { titulo: 'Perfil y configuración', ruta: '/configuracion', icono: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8M4 21a8 8 0 0 1 16 0', permisos: [] }
  ] }
];

/** Marco común de las pantallas internas: menú lateral único + contenido de la ruta activa. */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css'
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly grupos = computed(() => MENU
    .map(grupo => ({ ...grupo, opciones: grupo.opciones.filter(opcion => this.auth.tieneAlguno(opcion.permisos)) }))
    .filter(grupo => grupo.opciones.length > 0));
  readonly nombre = this.auth.nombre;
  readonly iniciales = computed(() => iniciales(this.auth.nombre()));
  readonly detalle = computed(() => [this.auth.roles().join(' · '), this.auth.consultorio()].filter(Boolean).join(' — '));
  saliendo = false;

  salir(): void {
    if (this.saliendo) return;
    this.saliendo = true;
    this.auth.cerrarSesion().subscribe(() => { this.saliendo = false; this.router.navigate(['/login']); });
  }
}
