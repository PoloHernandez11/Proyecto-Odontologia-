import { Injectable } from '@angular/core';
import { Tarea  } from '../models/tarea.model';

@Injectable({
  providedIn: 'root'
})
export class TareasService {
  private storageKey = 'clinica_tareas';

  obtenerTodas(): Tarea[] {
    const data = localStorage.getItem(this.storageKey);
    if (data) {
      return JSON.parse(data);
    }
    // Datos iniciales de ejemplo si está vacío
    return [
      { id: 1, titulo: 'Esterilizar instrumental de ortodoncia', prioridad: 'Alta', completada: false, fechaLimite: '2026-10-25' },
      { id: 2, titulo: 'Llamar a paciente Juan Pérez para confirmar turno', prioridad: 'Media', completada: true, fechaLimite: '2026-10-24' }
    ];
  }

  guardar(tareas: Tarea[]): void {
    localStorage.setItem(this.storageKey, JSON.stringify(tareas));
  }

  agregar(nueva: Omit<Tarea, 'id'>): void {
    const tareas = this.obtenerTodas();
    const id = tareas.length > 0 ? Math.max(...tareas.map(t => t.id)) + 1 : 1;
    tareas.push({ ...nueva, id });
    this.guardar(tareas);
  }

  cambiarEstado(id: number): void {
    const tareas = this.obtenerTodas();
    const tarea = tareas.find(t => t.id === id);
    if (tarea) {
      tarea.completada = !tarea.completada;
      this.guardar(tareas);
    }
  }

  eliminar(id: number): void {
    let tareas = this.obtenerTodas();
    tareas = tareas.filter(t => t.id !== id);
    this.guardar(tareas);
  }
}