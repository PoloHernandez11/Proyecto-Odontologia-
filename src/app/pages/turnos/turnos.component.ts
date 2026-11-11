import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PacientesService } from '../../services/paciente.service';

interface Paciente {
  id: string | number;
  nombre: string;
  apellido: string;
  dni: string;
  telefono?: string;
  email?: string;
}

interface Tarea {
  id: number;
  titulo: string;
  prioridad: 'alta' | 'media' | 'baja';
  completada: boolean;
}

@Component({
  selector: 'app-turnos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './turnos.component.html',
  styleUrl: './turnos.component.css'
})
export class TurnosComponent implements OnInit {
  formTurno!: FormGroup;
  formPaciente!: FormGroup;
  pacientes: Paciente[] = [];

  tareas: Tarea[] = [
    { id: 1, titulo: 'Confirmar turnos de la tarde', prioridad: 'alta', completada: false },
    { id: 2, titulo: 'Preparar fichas clínicas del día', prioridad: 'media', completada: true },
    { id: 3, titulo: 'Revisar material de laboratorio pendiente', prioridad: 'baja', completada: false }
  ];

  constructor(
    private fb: FormBuilder,
    private pacientesService: PacientesService
  ) {}

  ngOnInit(): void {
    this.inicializarFormularios();
    this.cargarPacientes();
  }

  private inicializarFormularios(): void {
    this.formTurno = this.fb.group({
      pacienteId: ['', Validators.required],
      fecha: ['', Validators.required],
      hora: ['', Validators.required],
      motivo: ['', Validators.required]
    });

    this.formPaciente = this.fb.group({
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      dni: ['', Validators.required],
      telefono: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]]
    });
  }

  cargarPacientes(): void {
    this.pacientesService.obtenerTodos().subscribe({
      next: (data: any) => {
        this.pacientes = data;
      },
      error: (err) => {
        console.error('Error al obtener la lista de pacientes:', err);
      }
    });
  }

  agendarTurno(): void {
    if (this.formTurno.invalid) return;

    const nuevoTurno = this.formTurno.value;
    console.log('Turno agendado:', nuevoTurno);
    this.formTurno.reset();
  }

  registrarPaciente(): void {
    if (this.formPaciente.invalid) return;

    const datosPaciente = this.formPaciente.value;
    this.pacientesService.crearPaciente(datosPaciente).subscribe({
      next: () => {
        this.formPaciente.reset();
        this.cargarPacientes();
      },
      error: (err) => {
        console.error('Error al registrar paciente:', err);
      }
    });
  }

  toggleTarea(tarea: Tarea): void {
    tarea.completada = !tarea.completada;
  }
}