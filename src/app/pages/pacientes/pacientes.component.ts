/*import { Component, OnInit } from '@angular/core';
import { PacientesService } from '../../services/paciente.service';
import { Paciente } from '../../models/paciente.model';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
 
@Component({
  selector: 'app-pacientes',
  templateUrl: './pacientes.component.html',
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink],
  styleUrl: './pacientes.component.css'
})
export class PacientesComponent implements OnInit {
  pacientes: Paciente[] = [];
  cargando = false;
  error = '';
 
  constructor(private pacientesService: PacientesService) {}
 
  ngOnInit(): void {
    this.cargarPacientes();
  }
 
  cargarPacientes(): void {
    this.cargando = true;
    this.pacientesService.obtenerTodos().subscribe({
      next: (data) => {
        this.pacientes = data;
        this.cargando = false;
      },
      error: (err) => {
        this.error = 'No se pudieron cargar los pacientes del servidor.';
        this.cargando = false;
        console.error(err);
      }
    });
  }
 
  guardarPaciente(nuevoPaciente: Omit<Paciente, 'id'>): void {
    this.pacientesService.crearPaciente(nuevoPaciente).subscribe({
      next: () => {
        this.cargarPacientes(); // Recargamos la lista desde la API
      },
      error: (err) => {
        console.error('Error al guardar paciente:', err);
      }
    });
  }
}
  */
 
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PacientesService } from '../../services/paciente.service';
import { PerfilService } from '../../services/perfil.service';
import { Paciente } from '../../models/paciente.model';
 
interface TurnoPaciente {
  fecha: string;
  tratamiento: string;
  hora: string;
  profesional: string;
  estado: string;
}
 
@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './pacientes.component.html',
  styleUrl: './pacientes.component.css'
})
export class PacientesComponent implements OnInit {
 
  // Datos generales
  pacientes: Paciente[] = [];
  pacienteSeleccionado: Paciente | null = null;
 
  perfil = 'secretaria';
  nombreUsuario = 'Carla';
 
  cargando = false;
  error = '';
  mensaje = '';
 
  // Filtros
  busqueda = '';
  obraSocialSeleccionada = 'Todas';
  estadoSeleccionado = 'Todos';
 
  obrasSociales: string[] = [
    'OSDE 21',
    'OSDE 31',
    'OSDE 310',
    'Galeno 22',
    'Swiss Medical',
    'Particular'
  ];
 
  // Historial de turnos
  historialPaciente: TurnoPaciente[] = [];
 
  // Formulario
  mostrarFormulario = false;
  modoEdicion = false;
 
  private fb = inject(FormBuilder);
 
  formularioPaciente = this.fb.nonNullable.group({
    nombre: ['', [
      Validators.required,
      Validators.minLength(2)
    ]],
    apellido: ['', [
      Validators.required,
      Validators.minLength(2)
    ]],
    dni: ['', [
      Validators.required,
      Validators.pattern(/^[0-9]{7,8}$/)
    ]],
    telefono: ['', [
      Validators.required,
      Validators.pattern(/^[0-9+\s()-]{8,20}$/)
    ]],
    email: ['', [
      Validators.email
    ]],
    obraSocial: ['Particular', Validators.required]
  });
 
  constructor(
    private pacientesService: PacientesService,
    private perfilService: PerfilService,
    private route: ActivatedRoute
  ) {}
 
  ngOnInit(): void {
    const guardado = this.perfilService.obtener();
 
    this.route.queryParamMap.subscribe(parametros => {
      this.perfil = parametros.get('perfil') || guardado.perfil;
 
      this.nombreUsuario =
        guardado.nombre ||
        (this.perfil === 'odontologo' ? 'Dra. Méndez' : 'Carla');
    });
 
    this.cargarPacientes();
  }
 
  // Obtener pacientes desde la API REST
  cargarPacientes(): void {
    this.cargando = true;
    this.error = '';
 
    this.pacientesService.obtenerTodos().subscribe({
      next: (data) => {
        this.pacientes = data;
        this.cargando = false;
 
        if (this.pacienteSeleccionado) {
          this.pacienteSeleccionado =
            data.find(p => p.id === this.pacienteSeleccionado?.id) ?? null;
        }
      },
      error: (err) => {
        console.error('Error al cargar pacientes:', err);
        this.error = 'No se pudieron cargar los pacientes del servidor.';
        this.cargando = false;
      }
    });
  }
 
  // Pacientes filtrados
  get pacientesFiltrados(): Paciente[] {
    const texto = this.busqueda.toLowerCase().trim();
 
    return this.pacientes.filter(paciente => {
      const coincideBusqueda =
        `${paciente.nombre} ${paciente.apellido}`.toLowerCase().includes(texto) ||
        paciente.dni.includes(texto) ||
        paciente.telefono.includes(texto);
 
      const coincideObra =
        this.obraSocialSeleccionada === 'Todas' ||
        paciente.obraSocial === this.obraSocialSeleccionada;
 
      const coincideEstado =
        this.estadoSeleccionado === 'Todos' ||
        paciente.estado === this.estadoSeleccionado;
 
      return coincideBusqueda && coincideObra && coincideEstado;
    });
  }
 
  // Iniciales del paciente
  iniciales(paciente: Paciente): string {
    return `${paciente.nombre.charAt(0)}${paciente.apellido.charAt(0)}`.toUpperCase();
  }
 
  // Seleccionar paciente
  seleccionarPaciente(paciente: Paciente): void {
    this.pacienteSeleccionado = paciente;
 
    // Todavía no hay un servicio de historial conectado.
    this.historialPaciente = [];
  }
 
  // Abrir formulario para nuevo paciente
  abrirNuevoPaciente(): void {
    this.modoEdicion = false;
 
    this.formularioPaciente.reset({
      nombre: '',
      apellido: '',
      dni: '',
      telefono: '',
      email: '',
      obraSocial: 'Particular'
    });
 
    this.mostrarFormulario = true;
  }
 
  // Abrir formulario para editar paciente
  editarPaciente(): void {
    if (!this.pacienteSeleccionado) {
      return;
    }
 
    const paciente = this.pacienteSeleccionado;
 
    this.modoEdicion = true;
 
    this.formularioPaciente.patchValue({
      nombre: paciente.nombre,
      apellido: paciente.apellido,
      dni: paciente.dni,
      telefono: paciente.telefono,
      email: paciente.email,
      obraSocial: paciente.obraSocial
    });
 
    this.mostrarFormulario = true;
  }
 
  // Guardar paciente nuevo o actualizado
  guardarPaciente(): void {
    if (this.formularioPaciente.invalid) {
      this.formularioPaciente.markAllAsTouched();
      return;
    }
 
    const datos = this.formularioPaciente.getRawValue();
 
    if (this.modoEdicion && this.pacienteSeleccionado) {
      const id = this.pacienteSeleccionado.id;
 
      const pacienteActualizado: Partial<Paciente> = {
        ...datos,
        ultimaVisita: this.pacienteSeleccionado.ultimaVisita,
        proximoTurno: this.pacienteSeleccionado.proximoTurno,
        estado: this.pacienteSeleccionado.estado
      };
 
      this.pacientesService.actualizar(id, pacienteActualizado).subscribe({
        next: () => {
          this.mensaje = 'Paciente actualizado correctamente.';
          this.cerrarFormulario();
          this.cargarPacientes();
        },
        error: (err) => {
          console.error('Error al actualizar paciente:', err);
          this.error = 'No se pudo actualizar el paciente.';
        }
      });
 
    } else {
      const nuevoPaciente = {
        ...datos,
        ultimaVisita: 'Sin visitas',
        proximoTurno: 'Sin turno',
        estado: 'Activo' as const
      };
 
      this.pacientesService.crearPaciente(nuevoPaciente).subscribe({
        next: (paciente) => {
          this.mensaje = 'Paciente registrado correctamente.';
          this.pacienteSeleccionado = paciente;
          this.cerrarFormulario();
          this.cargarPacientes();
        },
        error: (err) => {
          console.error('Error al registrar paciente:', err);
          this.error = 'No se pudo registrar el paciente.';
        }
      });
    }
  }
 
  // Cerrar formulario
  cerrarFormulario(): void {
    this.mostrarFormulario = false;
    this.formularioPaciente.reset({
      nombre: '',
      apellido: '',
      dni: '',
      telefono: '',
      email: '',
      obraSocial: 'Particular'
    });
  }
}
 