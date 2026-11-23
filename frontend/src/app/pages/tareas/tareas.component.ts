import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccionTurno, ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentDto, MemberDto, SaveTaskRequest, TaskCategory, TaskDto, TaskItemStatus, TaskPriority } from '../../api/schema';
import { fechaCorta, horaLocal, mensajeDeError } from '../../core/api';
import { fechaDelConsultorio, formatoEnConsultorio, inicioDelDia, instanteEnZona, sumarDias, zonaDelConsultorio } from '../../core/zona-horaria';
import { AuthService } from '../../core/auth.service';
import { DialogDirective } from '../../shared/dialog.directive';
import { PluralPipe } from '../../shared/plural.pipe';

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
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, DialogDirective, PluralPipe],
  templateUrl: './tareas.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  get veTareas(): boolean { return this.auth.tiene('tasks.read'); }
  get puedeEscribirTareas(): boolean { return this.auth.tiene('tasks.write'); }
  get veTurnos(): boolean { return this.auth.tiene('appointments.read'); }
  get puedeEscribirTurnos(): boolean { return this.auth.tiene('appointments.write'); }
  readonly filtro = signal<Filtro>('abiertas');
  readonly tareas = signal<TaskDto[]>([]);
  readonly turnos = signal<AppointmentDto[]>([]);
  readonly miembros = signal<MemberDto[]>([]);
  readonly cargandoTareas = signal(true);
  readonly cargandoTurnos = signal(true);
  readonly mensaje = signal('');
  readonly error = signal('');

  readonly mostrarFormulario = signal(false);
  readonly tareaEnEdicion = signal<TaskDto | null>(null);
  readonly guardando = signal(false);
  readonly errorFormulario = signal('');
  formulario = new FormGroup({
    titulo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)] }),
    descripcion: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    prioridad: new FormControl<TaskPriority>('Medium', { nonNullable: true }),
    categoria: new FormControl<TaskCategory>('General', { nonNullable: true }),
    asignada: new FormControl('', { nonNullable: true }),
    vence: new FormControl('', { nonNullable: true })
  });

  constructor() {
    if (this.veTareas) this.cargarTareas(); else this.cargandoTareas.set(false);
    if (this.veTurnos) this.cargarTurnos(); else this.cargandoTurnos.set(false);
    if (this.auth.tiene('users.read')) this.api.miembros().subscribe({ next: miembros => (this.miembros.set(miembros.filter(m => m.isActive))) });
  }

  // ── Tareas ─────────────────────────────────────────────────────────────────
  cambiarFiltro(filtro: Filtro): void { this.filtro.set(filtro); this.cargarTareas(); }

  cargarTareas(): void {
    this.cargandoTareas.set(true);
    const filtro = this.filtro() === 'completadas' ? { Status: 'Completed' } : this.filtro() === 'mias' ? { Mine: true } : {};
    this.api.tareas(filtro).subscribe({
      next: pagina => {
        this.cargandoTareas.set(false);
        const visibles = this.filtro() === 'completadas' ? pagina.items : pagina.items.filter(t => t.status === 'Pending' || t.status === 'InProgress');
        const peso: Record<TaskPriority, number> = { High: 0, Medium: 1, Low: 2 };
        this.tareas.set(visibles.sort((a, b) =>
          Number(b.isOverdue) - Number(a.isOverdue) || (a.dueAtUtc ?? '9').localeCompare(b.dueAtUtc ?? '9') || peso[a.priority] - peso[b.priority]));
      },
      error: error => { this.cargandoTareas.set(false); this.error.set(mensajeDeError(error, 'No se pudieron cargar las tareas.')); }
    });
  }

  nuevaTarea(): void {
    this.tareaEnEdicion.set(null);
    this.errorFormulario.set('');
    this.formulario.reset({ titulo: '', descripcion: '', prioridad: 'Medium', categoria: 'General', asignada: this.auth.userId ?? '', vence: fechaDelConsultorio() });
    this.mostrarFormulario.set(true);
  }

  editarTarea(tarea: TaskDto): void {
    this.tareaEnEdicion.set(tarea);
    this.errorFormulario.set('');
    this.formulario.reset({
      titulo: tarea.title, descripcion: tarea.description ?? '', prioridad: tarea.priority, categoria: tarea.category,
      asignada: tarea.assignedToUserId ?? '', vence: tarea.dueAtUtc ? fechaDelConsultorio(tarea.dueAtUtc) : ''
    });
    this.mostrarFormulario.set(true);
  }

  guardarTarea(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    const enEdicion = this.tareaEnEdicion();
    const solicitud: SaveTaskRequest = {
      title: datos.titulo.trim(), description: datos.descripcion.trim() || null, priority: datos.prioridad, category: datos.categoria,
      assignedToUserId: datos.asignada || null,
      // Vence al final del día elegido, en la zona horaria del consultorio.
      dueAt: datos.vence ? instanteEnZona(datos.vence, '23:59', zonaDelConsultorio()).toISOString() : null,
      patientId: enEdicion?.patientId, appointmentId: enEdicion?.appointmentId
    };
    this.guardando.set(true);
    this.errorFormulario.set('');
    const operacion = enEdicion ? this.api.actualizarTarea(enEdicion.id, solicitud) : this.api.crearTarea(solicitud);
    operacion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.mostrarFormulario.set(false);
        this.mensaje.set(enEdicion ? 'La tarea se actualizó.' : 'La tarea se creó correctamente.');
        this.cargarTareas();
      },
      error: error => { this.guardando.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo guardar la tarea.')); }
    });
  }

  cambiarEstado(tarea: TaskDto, estado: TaskItemStatus): void {
    this.api.estadoTarea(tarea.id, estado).subscribe({
      next: () => { this.error.set(''); this.mensaje.set(`"${tarea.title}" ahora figura como ${ESTADOS[estado].toLowerCase()}.`); this.cargarTareas(); },
      error: error => (this.error.set(mensajeDeError(error, 'No se pudo actualizar la tarea.')))
    });
  }

  // ── Turnos que necesitan seguimiento ───────────────────────────────────────
  cargarTurnos(): void {
    this.cargandoTurnos.set(true);
    // De hoy a 7 días, contados en días del consultorio.
    const hoy = fechaDelConsultorio();
    const desde = inicioDelDia(hoy), hasta = inicioDelDia(sumarDias(hoy, 7));
    this.api.turnos({ From: desde.toISOString(), To: hasta.toISOString(), Status: 'Pending', pageSize: 50 }).subscribe({
      next: pagina => { this.cargandoTurnos.set(false); this.turnos.set([...pagina.items].sort((a, b) => a.startUtc.localeCompare(b.startUtc))); },
      error: error => { this.cargandoTurnos.set(false); this.error.set(mensajeDeError(error, 'No se pudieron cargar los turnos.')); }
    });
  }

  accionTurno(turno: AppointmentDto, accion: AccionTurno): void {
    this.api.accionTurno(turno.id, accion).subscribe({
      next: actualizado => { this.error.set(''); this.mensaje.set(`El turno de ${turno.patientName} ahora figura como ${ESTADOS_DE_TURNO[actualizado.status].toLowerCase()}.`); this.cargarTurnos(); },
      error: error => (this.error.set(mensajeDeError(error, 'No se pudo actualizar el turno.')))
    });
  }

  fecha(valor: string | null | undefined): string { return valor ? fechaCorta(valor) : 'Sin fecha'; }
  cuando(valor: string): string {
    const texto = formatoEnConsultorio(valor, { weekday: 'short', day: 'numeric', month: 'short' });
    return `${texto} · ${horaLocal(valor)}`;
  }
}
