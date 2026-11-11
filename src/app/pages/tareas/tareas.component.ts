import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TareasService } from '../../services/tareas.service';
import { Tarea } from '../../models/tarea.model';

@Component({
  selector: 'app-tareas',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './tareas.component.html',
  styleUrl: './tareas.component.css'
})
export class TareasComponent {
  tareas: Tarea[] = [];
  mostrarFormulario = false;

  // Formulario rápido (template-driven, usa ngModel)
  nuevaTarea: { titulo: string; prioridad: Tarea['prioridad']; fechaLimite: string } = {
    titulo: '',
    prioridad: 'Media',
    fechaLimite: ''
  };

  formularioTarea = new FormGroup({
    titulo: new FormControl('', [Validators.required, Validators.minLength(3)]),
    descripcion: new FormControl(''),
    prioridad: new FormControl<'Baja' | 'Media' | 'Alta'>('Media', Validators.required),
    fechaLimite: new FormControl('', Validators.required)
  });

  constructor(private tareasService: TareasService) {
    this.cargarTareas();
  }

  cargarTareas(): void {
    this.tareas = this.tareasService.obtenerTodas();
  }

  abrirFormulario(): void {
    this.mostrarFormulario = true;
    this.formularioTarea.reset({ prioridad: 'Media' });
  }

  guardarTarea(): void {
    if (this.formularioTarea.invalid) {
      this.formularioTarea.markAllAsTouched();
      return;
    }

    const val = this.formularioTarea.getRawValue();
    this.tareasService.agregar({
      titulo: val.titulo!,
      descripcion: val.descripcion || '',
      prioridad: val.prioridad!,
      completada: false,
      fechaLimite: val.fechaLimite!
    });

    this.mostrarFormulario = false;
    this.cargarTareas();
  }

  agregarTarea(): void {
    const titulo = this.nuevaTarea.titulo.trim();
    if (!titulo || !this.nuevaTarea.fechaLimite) return;

    this.tareasService.agregar({
      titulo,
      prioridad: this.nuevaTarea.prioridad,
      fechaLimite: this.nuevaTarea.fechaLimite,
      completada: false
    });

    this.nuevaTarea = { titulo: '', prioridad: 'Media', fechaLimite: '' };
    this.cargarTareas();
  }

  cambiarEstado(tarea: Tarea): void {
    this.tareasService.cambiarEstado(tarea.id);
    this.cargarTareas();
  }

  toggleCompletada(id: number): void {
    this.tareasService.cambiarEstado(id);
    this.cargarTareas();
  }

  eliminarTarea(id: number): void {
    if (confirm('¿Estás seguro de eliminar esta tarea?')) {
      this.tareasService.eliminar(id);
      this.cargarTareas();
    }
  }
}