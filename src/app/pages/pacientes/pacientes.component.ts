import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Paciente } from '../../models/paciente.model';
import { PacienteService } from '../../services/paciente.service';
import { Turno } from '../../models/turno.model';
import { TurnoService } from '../../services/turno.service';

@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './pacientes.component.html',
  styleUrl: './pacientes.component.css'
})
export class PacientesComponent {
  perfil = 'secretaria';
  nombreUsuario = 'Carla';
  busqueda = '';
  obraSocialSeleccionada = 'Todas';
  readonly obrasSociales = ['Particular','OSDE','OSDE 21','OSDE 31','OSDE 310','Swiss Medical','Galeno','Galeno 22','Medifé','Accord Salud','Omint','Sancor Salud','Prevención Salud','Avalian','Federada Salud','Jerárquicos Salud','Medicus','Hospital Italiano','Luis Pasteur','Hominis','IOMA','PAMI','OSECAC','OSPE','OSDEPYM','Unión Personal','OSUTHGRA','OSPRERA','OSMATA','OSCHOCA','OSFATUN','OSPLAD','UPCN'];
  estadoSeleccionado = 'Todos';
  pacientes: Paciente[] = [];
  pacienteSeleccionado: Paciente | null = null;
  mostrarFormulario = false;
  modoEdicion = false;
  mensaje = '';

  formularioPaciente = new FormGroup({
    nombre: new FormControl('', [Validators.required, Validators.minLength(2)]),
    apellido: new FormControl('', [Validators.required, Validators.minLength(2)]),
    dni: new FormControl('', [Validators.required, Validators.pattern(/^[0-9.]{7,10}$/)]),
    telefono: new FormControl('', [Validators.required, Validators.minLength(8)]),
    email: new FormControl('', [Validators.email]),
    obraSocial: new FormControl('Particular', [Validators.required])
  });

  constructor(
    private pacienteService: PacienteService,
    private turnoService: TurnoService,
    private route: ActivatedRoute
  ) {
    this.pacientes = this.pacienteService.obtenerTodos();
    this.route.queryParamMap.subscribe(parametros => {
      this.perfil = parametros.get('perfil') || 'secretaria';
      this.nombreUsuario = this.perfil === 'odontologo' ? 'Dra. Laura Méndez' : 'Carla';
    });
    this.pacienteSeleccionado = this.pacientes[0] || null;
  }

  get pacientesFiltrados(): Paciente[] {
    const texto = this.busqueda.toLowerCase().trim();
    return this.pacientes.filter(paciente => {
      const nombreCompleto = `${paciente.nombre} ${paciente.apellido}`.toLowerCase();
      const coincideTexto = nombreCompleto.includes(texto) || paciente.dni.includes(texto) || paciente.telefono.includes(texto);
      const coincideObraSocial = this.obraSocialSeleccionada === 'Todas' || paciente.obraSocial === this.obraSocialSeleccionada;
      const coincideEstado = this.estadoSeleccionado === 'Todos' || paciente.estado === this.estadoSeleccionado;
      return coincideTexto && coincideObraSocial && coincideEstado;
    });
  }

  get historialPaciente(): Turno[] {
    if (!this.pacienteSeleccionado) return [];
    const nombreCompleto = `${this.pacienteSeleccionado.nombre} ${this.pacienteSeleccionado.apellido}`.toLowerCase();
    return this.turnoService.obtenerTodos().filter(turno => turno.paciente.toLowerCase() === nombreCompleto);
  }

  seleccionarPaciente(paciente: Paciente): void {
    this.pacienteSeleccionado = paciente;
    this.mensaje = '';
  }

  abrirNuevoPaciente(): void {
    this.modoEdicion = false;
    this.formularioPaciente.reset({ nombre: '', apellido: '', dni: '', telefono: '', email: '', obraSocial: 'Particular' });
    this.mostrarFormulario = true;
  }

  editarPaciente(): void {
    if (!this.pacienteSeleccionado) return;
    this.modoEdicion = true;
    this.formularioPaciente.reset({
      nombre: this.pacienteSeleccionado.nombre,
      apellido: this.pacienteSeleccionado.apellido,
      dni: this.pacienteSeleccionado.dni,
      telefono: this.pacienteSeleccionado.telefono,
      email: this.pacienteSeleccionado.email,
      obraSocial: this.pacienteSeleccionado.obraSocial
    });
    this.mostrarFormulario = true;
  }

  cerrarFormulario(): void {
    this.mostrarFormulario = false;
  }

  guardarPaciente(): void {
    if (this.formularioPaciente.invalid) {
      this.formularioPaciente.markAllAsTouched();
      return;
    }

    const datos = this.formularioPaciente.getRawValue();
    const paciente: Paciente = {
      id: this.modoEdicion && this.pacienteSeleccionado
        ? this.pacienteSeleccionado.id
        : Math.max(0, ...this.pacientes.map(item => item.id)) + 1,
      nombre: datos.nombre!.trim(),
      apellido: datos.apellido!.trim(),
      dni: datos.dni!.trim(),
      telefono: datos.telefono!.trim(),
      email: datos.email || '',
      obraSocial: datos.obraSocial!,
      ultimaVisita: this.modoEdicion && this.pacienteSeleccionado ? this.pacienteSeleccionado.ultimaVisita : 'Sin visitas',
      proximoTurno: this.modoEdicion && this.pacienteSeleccionado ? this.pacienteSeleccionado.proximoTurno : 'Sin turno',
      estado: this.modoEdicion && this.pacienteSeleccionado ? this.pacienteSeleccionado.estado : 'Activo'
    };

    if (this.modoEdicion) {
      this.pacienteService.actualizar(paciente);
      this.mensaje = 'Los datos del paciente se actualizaron correctamente.';
    } else {
      this.pacienteService.agregar(paciente);
      this.mensaje = 'El paciente se registró correctamente.';
    }

    this.pacientes = this.pacienteService.obtenerTodos();
    this.pacienteSeleccionado = paciente;
    this.mostrarFormulario = false;
  }

  iniciales(paciente: Paciente): string {
    return `${paciente.nombre.charAt(0)}${paciente.apellido.charAt(0)}`.toUpperCase();
  }
}
