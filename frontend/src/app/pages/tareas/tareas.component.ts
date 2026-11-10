import { CommonModule } from '@angular/common';
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccionTurno, ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentDto, MemberDto, SaveTaskRequest, TaskCategory, TaskDto, TaskItemStatus, TaskPriority } from '../../api/schema';
import { fechaCorta, fechaIso, horaLocal, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

const PRIORIDADES: Record<TaskPriority, string> = { High: 'Alta', Medium: 'Media', Low: 'Baja' };
const CATEGORIAS: Record<TaskCategory, string> = {
  General: 'General', Appointments: 'Turnos', Administrative: 'Administración', Clinical: 'Clínica', Laboratory: 'Laboratorio', Supplies: 'Insumos'
};
const ESTADOS: Record<TaskItemStatus, string> = { Pending: 'Pendiente', InProgress: 'En curso', Completed: 'Completada', Cancelled: 'Cancelada' };
type Filtro = 'abiertas' | 'mias' | 'completadas';

/** Turnos y tareas: los turnos que necesitan seguimiento (por confirmar) y las tareas internas del consultorio. */
@Component({
  selector: 'app-tareas',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './tareas.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './tareas.component.css'
})
export class TareasComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);

  readonly prioridades = PRIORIDADES;
  readonly categorias = CATEGORIAS;
  readonly estados = ESTADOS;
  readonly estadosDeTurno = ESTADOS_DE_TURNO;
  readonly listaDePrioridades = Object.entries(PRIORIDADES) as [TaskPriority, string][];
  readonly listaDeCategorias = Object.entries(CATEGORIAS) as [TaskCategory, string][];
  readonly veTareas = this.auth.tiene('tasks.read');
  readonly puedeEscribirTareas = this.auth.tiene('tasks.write');
  readonly veTurnos = this.auth.tiene('appointments.read');
  readonly puedeEscribirTurnos = this.auth.tiene('appointments.write');

  filtro: Filtro = 'abiertas';
  tareas: TaskDto[] = [];
  turnos: AppointmentDto[] = [];
  miembros: MemberDto[] = [];
  cargandoTareas = true;
  cargandoTurnos = true;
  mensaje = '';
  error = '';

  mostrarFormulario = false;
  tareaEnEdicion: TaskDto | null = null;
  guardando = false;
  errorFormulario = '';
  formulario = new FormGroup({
    titulo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)] }),
    descripcion: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    prioridad: new FormControl<TaskPriority>('Medium', { nonNullable: true }),
    categoria: new FormControl<TaskCategory>('General', { nonNullable: true }),
    asignada: new FormControl('', { nonNullable: true }),
    vence: new FormControl('', { nonNullable: true })
  });

  constructor() {
    if (this.veTareas) this.cargarTareas(); else this.cargandoTareas = false;
    if (this.veTurnos) this.cargarTurnos(); else this.cargandoTurnos = false;
    if (this.auth.tiene('users.read')) this.api.miembros().subscribe({ next: miembros => (this.miembros = miembros.filter(m => m.isActive)) });
  }

  // ── Tareas ─────────────────────────────────────────────────────────────────
  cambiarFiltro(filtro: Filtro): void { this.filtro = filtro; this.cargarTareas(); }

  cargarTareas(): void {
    this.cargandoTareas = true;
    const filtro = this.filtro === 'completadas' ? { Status: 'Completed' } : this.filtro === 'mias' ? { Mine: true } : {};
    this.api.tareas(filtro).subscribe({
      next: pagina => {
        this.cargandoTareas = false;
        const visibles = this.filtro === 'completadas' ? pagina.items : pagina.items.filter(t => t.status === 'Pending' || t.status === 'InProgress');
        const peso: Record<TaskPriority, number> = { High: 0, Medium: 1, Low: 2 };
        this.tareas = visibles.sort((a, b) =>
          Number(b.isOverdue) - Number(a.isOverdue) || (a.dueAtUtc ?? '9').localeCompare(b.dueAtUtc ?? '9') || peso[a.priority] - peso[b.priority]);
      },
      error: error => { this.cargandoTareas = false; this.error = mensajeDeError(error, 'No se pudieron cargar las tareas.'); }
    });
  }

  nuevaTarea(): void {
    this.tareaEnEdicion = null;
    this.errorFormulario = '';
    this.formulario.reset({ titulo: '', descripcion: '', prioridad: 'Medium', categoria: 'General', asignada: this.auth.userId ?? '', vence: fechaIso(new Date()) });
    this.mostrarFormulario = true;
  }

  editarTarea(tarea: TaskDto): void {
    this.tareaEnEdicion = tarea;
    this.errorFormulario = '';
    this.formulario.reset({
      titulo: tarea.title, descripcion: tarea.description ?? '', prioridad: tarea.priority, categoria: tarea.category,
      asignada: tarea.assignedToUserId ?? '', vence: tarea.dueAtUtc ? fechaIso(new Date(tarea.dueAtUtc)) : ''
    });
    this.mostrarFormulario = true;
  }

  guardarTarea(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    const solicitud: SaveTaskRequest = {
      title: datos.titulo.trim(), description: datos.descripcion.trim() || null, priority: datos.prioridad, category: datos.categoria,
      assignedToUserId: datos.asignada || null,
      // Vence al final del día elegido, en la hora local del navegador.
      dueAt: datos.vence ? new Date(`${datos.vence}T23:59:00`).toISOString() : null,
      patientId: this.tareaEnEdicion?.patientId, appointmentId: this.tareaEnEdicion?.appointmentId
    };
    this.guardando = true;
    this.errorFormulario = '';
    const operacion = this.tareaEnEdicion ? this.api.actualizarTarea(this.tareaEnEdicion.id, solicitud) : this.api.crearTarea(solicitud);
    operacion.subscribe({
      next: () => {
        this.guardando = false;
        this.mostrarFormulario = false;
        this.mensaje = this.tareaEnEdicion ? 'La tarea se actualizó.' : 'La tarea se creó correctamente.';
        this.cargarTareas();
      },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo guardar la tarea.'); }
    });
  }

  cambiarEstado(tarea: TaskDto, estado: TaskItemStatus): void {
    this.api.estadoTarea(tarea.id, estado).subscribe({
      next: () => { this.error = ''; this.mensaje = `"${tarea.title}" ahora figura como ${ESTADOS[estado].toLowerCase()}.`; this.cargarTareas(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo actualizar la tarea.'))
    });
  }

  // ── Turnos que necesitan seguimiento ───────────────────────────────────────
  cargarTurnos(): void {
    this.cargandoTurnos = true;
    const desde = new Date(); desde.setHours(0, 0, 0, 0);
    const hasta = new Date(desde); hasta.setDate(hasta.getDate() + 7);
    this.api.turnos({ From: desde.toISOString(), To: hasta.toISOString(), Status: 'Pending', pageSize: 50 }).subscribe({
      next: pagina => { this.cargandoTurnos = false; this.turnos = [...pagina.items].sort((a, b) => a.startUtc.localeCompare(b.startUtc)); },
      error: error => { this.cargandoTurnos = false; this.error = mensajeDeError(error, 'No se pudieron cargar los turnos.'); }
    });
  }

  accionTurno(turno: AppointmentDto, accion: AccionTurno): void {
    this.api.accionTurno(turno.id, accion).subscribe({
      next: actualizado => { this.error = ''; this.mensaje = `El turno de ${turno.patientName} ahora figura como ${ESTADOS_DE_TURNO[actualizado.status].toLowerCase()}.`; this.cargarTurnos(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo actualizar el turno.'))
    });
  }

  fecha(valor: string | null | undefined): string { return valor ? fechaCorta(valor) : 'Sin fecha'; }
  cuando(valor: string): string {
    const texto = new Intl.DateTimeFormat('es-AR', { weekday: 'short', day: 'numeric', month: 'short' }).format(new Date(valor));
    return `${texto} · ${horaLocal(valor)}`;
  }
}
