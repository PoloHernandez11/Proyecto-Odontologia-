import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PacienteService } from '../../services/paciente.service';
import { PerfilService } from '../../services/perfil.service';

interface Turno {
  hora: string;
  iniciales: string;
  paciente: string;
  detalle: string;
  profesional: string;
  estado: string;
}

interface Tarea {
  descripcion: string;
  fecha: string;
  completada: boolean;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  perfil = 'secretaria';
  nombreUsuario = 'Carla';
  busqueda = '';
  mostrarNotificaciones = false;
  mostrarPerfil = false;
  pacienteId = 1;

  turnos: Turno[] = [
    { hora: '08:30', iniciales: 'SH', paciente: 'Sofía Herrera', detalle: 'Control semestral', profesional: 'Dra. Méndez', estado: 'Atendido' },
    { hora: '09:15', iniciales: 'MR', paciente: 'Martín Rodríguez', detalle: 'Endodoncia pieza 36 · 2ª sesión', profesional: 'Dra. Méndez', estado: 'En sala' },
    { hora: '10:00', iniciales: 'LF', paciente: 'Lucía Fernández', detalle: 'Limpieza y flúor', profesional: 'Dr. Paz', estado: 'En sala' },
    { hora: '10:30', iniciales: 'JC', paciente: 'Jorge Castro', detalle: 'Prueba de corona 21', profesional: 'Dra. Méndez', estado: 'Confirmado' },
    { hora: '11:15', iniciales: 'VL', paciente: 'Valentina López', detalle: 'Consulta ortodoncia', profesional: 'Dr. Paz', estado: 'Pendiente' },
    { hora: '12:00', iniciales: 'DR', paciente: 'Diego Romero', detalle: 'Extracción pieza 48', profesional: 'Dra. Méndez', estado: 'Confirmado' },
    { hora: '14:30', iniciales: 'AT', paciente: 'Ana Torres', detalle: 'Blanqueamiento', profesional: 'Dr. Paz', estado: 'Cancelado' }
  ];

  turnosOdontologo = [
    { hora: '08:30', paciente: 'Sofía Herrera', detalle: 'Control semestral', estado: 'Atendido' },
    { hora: '09:15', paciente: 'Martín Rodríguez', detalle: 'Endodoncia pieza 36 · 2ª sesión', estado: 'En sala' },
    { hora: '10:30', paciente: 'Jorge Castro', detalle: 'Prueba de corona 21', estado: 'Confirmado' },
    { hora: '12:00', paciente: 'Diego Romero', detalle: 'Extracción pieza 48', estado: 'Confirmado' },
    { hora: '15:00', paciente: 'Paula Giménez', detalle: 'Obturación 14 y 15', estado: 'Pendiente' }
  ];

  alertasClinicas = [
    { paciente: 'Diego Romero · Anticoagulado', detalle: 'Consultar al cardiólogo antes de la extracción', tipo: 'alerta' },
    { paciente: 'Paula Giménez · Diabetes tipo 2', detalle: 'Controlar glucemia previa', tipo: 'aviso' },
    { paciente: 'Ricardo Sosa · Rx pendiente', detalle: 'Solicitar tomografía para implante', tipo: 'documento' }
  ];

  pacientesEnEspera = [
    { iniciales: 'MR', nombre: 'Martín Rodríguez', profesional: 'Dra. Méndez · Consultorio 1', llegada: 'Llegó 09:08' },
    { iniciales: 'LF', nombre: 'Lucía Fernández', profesional: 'Dr. Paz · Consultorio 2', llegada: 'Llegó 09:52' }
  ];

  tareas: Tarea[] = [
    { descripcion: 'Confirmar turnos de mañana (5)', fecha: 'Hoy', completada: false },
    { descripcion: 'Llamar a Jorge Castro por presupuesto', fecha: 'Hoy', completada: false },
    { descripcion: 'Retirar trabajo de Laboratorio Dental Sur', fecha: 'Mié 21', completada: false }
  ];

  get fechaHoyTexto(): string { return new Intl.DateTimeFormat('es-AR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()); }

  constructor(private route: ActivatedRoute, private pacientes: PacienteService, private perfilService: PerfilService) {
    this.pacienteId = this.pacientes.obtenerTodos()[0]?.id ?? 1;
    this.route.queryParamMap.subscribe(parametros => {
      const guardado = this.perfilService.obtener();
      this.perfil = parametros.get('perfil') || guardado.perfil;
      this.nombreUsuario = guardado.nombre || (this.perfil === 'odontologo' ? 'Dra. Méndez' : 'Carla');
    });
  }

  get turnosFiltrados(): Turno[] {
    const texto = this.busqueda.toLowerCase().trim();
    return this.turnos.filter(turno =>
      turno.paciente.toLowerCase().includes(texto) ||
      turno.hora.includes(texto)
    );
  }

  buscarPaciente(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    this.busqueda = campo.value;
  }

  alternarNotificaciones(): void { this.mostrarNotificaciones = !this.mostrarNotificaciones; }

  completarTarea(tarea: Tarea): void {
    tarea.completada = !tarea.completada;
  }
}
