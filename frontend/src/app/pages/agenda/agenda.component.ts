import { CommonModule } from '@angular/common';
import { Component, OnDestroy, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, of, switchMap } from 'rxjs';
import { AccionTurno, ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AgendaAppointmentDto, AppointmentStatus, GabineteDto, PatientListItemDto, ProfessionalDto, ScheduleBlockDto } from '../../api/schema';
import { fechaCorta, fechaIso, horaLocal, iniciales, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

type Vista = 'dia' | 'semana' | 'mes';
interface Accion { titulo: string; accion: AccionTurno; }

const ALTO_HORA = 64; // píxeles por hora en la vista de día

const ACCIONES: Partial<Record<AppointmentStatus, Accion[]>> = {
  Pending: [{ titulo: '✓ Confirmar', accion: 'confirm' }, { titulo: 'Registrar llegada', accion: 'check-in' }, { titulo: 'No asistió', accion: 'no-show' }],
  Confirmed: [{ titulo: 'Registrar llegada', accion: 'check-in' }, { titulo: 'Volver a pendiente', accion: 'mark-pending' }, { titulo: 'No asistió', accion: 'no-show' }],
  Arrived: [{ titulo: 'Pasar a sala de espera', accion: 'waiting-room' }, { titulo: 'Iniciar atención', accion: 'start' }],
  InWaitingRoom: [{ titulo: 'Iniciar atención', accion: 'start' }],
  InProgress: [{ titulo: '✓ Marcar atendido', accion: 'attend' }]
};

const CLASE_DE_ESTADO: Record<AppointmentStatus, string> = {
  Pending: 'pendiente', Confirmed: 'confirmado', Arrived: 'en-sala', InWaitingRoom: 'en-sala', InProgress: 'en-sala',
  Attended: 'atendido', Cancelled: 'cancelado', NoShow: 'cancelado'
};

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './agenda.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './agenda.component.css'
})
export class AgendaComponent implements OnDestroy {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly busquedaDePacientes = new Subject<string>();
  private readonly suscripcion: Subscription;

  readonly puedeEscribir = this.auth.tiene('appointments.write');
  readonly puedeSobreturno = this.auth.tiene('appointments.overbook');
  readonly estados = ESTADOS_DE_TURNO;
  readonly clases = CLASE_DE_ESTADO;

  vista: Vista = 'dia';
  fechaActual = new Date();
  busqueda = '';
  profesionalSeleccionado = 'Todos';
  profesionales: ProfessionalDto[] = [];
  gabinetes: GabineteDto[] = [];
  turnos: AgendaAppointmentDto[] = [];
  bloqueos: ScheduleBlockDto[] = [];
  turnoSeleccionado: AgendaAppointmentDto | null = null;
  cargando = true;
  mensaje = '';
  error = '';

  // Cancelación con motivo
  cancelando = false;
  motivoCancelacion = '';

  // Alta de turno
  mostrarFormulario = false;
  guardando = false;
  errorFormulario = '';
  textoPaciente = '';
  pacientesEncontrados: PatientListItemDto[] = [];
  pacienteElegido: PatientListItemDto | null = null;
  formularioTurno = new FormGroup({
    fecha: new FormControl(fechaIso(new Date()), { nonNullable: true, validators: [Validators.required] }),
    hora: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    duracion: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(5), Validators.max(480)] }),
    profesional: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gabinete: new FormControl('', { nonNullable: true }),
    motivo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] }),
    sobreturno: new FormControl(false, { nonNullable: true })
  });

  constructor() {
    this.suscripcion = this.busquedaDePacientes.pipe(
      debounceTime(250),
      switchMap(texto => (texto.trim().length < 2 ? of(null) : this.api.pacientes({ Search: texto.trim(), pageSize: 8 })))
    ).subscribe({ next: pagina => (this.pacientesEncontrados = pagina?.items ?? []) });

    this.api.profesionales().subscribe({
      next: profesionales => {
        this.profesionales = profesionales;
        // Quien sólo puede ver su propia agenda arranca filtrado en sí mismo.
        if (!this.auth.tiene('agenda.all_professionals') && profesionales.some(p => p.userId === this.auth.userId)) {
          this.profesionalSeleccionado = this.auth.userId!;
        }
        this.cargar();
        this.abrirDesdeLaUrl();
      },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar la agenda.'); }
    });
    this.api.gabinetes().subscribe({ next: gabinetes => (this.gabinetes = gabinetes.filter(g => g.isActive)) });
  }

  ngOnDestroy(): void { this.suscripcion.unsubscribe(); }

  // ── Rango visible ──────────────────────────────────────────────────────────
  private get rango(): { desde: Date; hasta: Date } {
    const desde = new Date(this.fechaActual); desde.setHours(0, 0, 0, 0);
    if (this.vista === 'semana') desde.setDate(desde.getDate() - ((desde.getDay() + 6) % 7));
    if (this.vista === 'mes') desde.setDate(1);
    const hasta = new Date(desde);
    if (this.vista === 'dia') hasta.setDate(hasta.getDate() + 1);
    else if (this.vista === 'semana') hasta.setDate(hasta.getDate() + 7);
    else hasta.setMonth(hasta.getMonth() + 1);
    return { desde, hasta };
  }

  get fechaTitulo(): string {
    const { desde, hasta } = this.rango;
    if (this.vista === 'dia') {
      const texto = new Intl.DateTimeFormat('es-AR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(desde);
      return texto.charAt(0).toUpperCase() + texto.slice(1);
    }
    if (this.vista === 'mes') {
      const texto = new Intl.DateTimeFormat('es-AR', { month: 'long', year: 'numeric' }).format(desde);
      return texto.charAt(0).toUpperCase() + texto.slice(1);
    }
    const fin = new Date(hasta); fin.setDate(fin.getDate() - 1);
    return `Semana del ${fechaCorta(fechaIso(desde))} al ${fechaCorta(fechaIso(fin))}`;
  }

  cargar(): void {
    const { desde, hasta } = this.rango;
    this.cargando = true;
    const ultimoDia = new Date(hasta); ultimoDia.setDate(ultimoDia.getDate() - 1);
    this.api.agenda(fechaIso(desde), fechaIso(ultimoDia), this.profesionalSeleccionado === 'Todos' ? null : this.profesionalSeleccionado).subscribe({
      next: agenda => {
        this.cargando = false;
        this.error = '';
        this.turnos = [...agenda.appointments].sort((a, b) => a.startUtc.localeCompare(b.startUtc));
        this.bloqueos = agenda.blocks;
        if (this.turnoSeleccionado) this.turnoSeleccionado = this.turnos.find(turno => turno.id === this.turnoSeleccionado!.id) ?? null;
      },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar la agenda.'); }
    });
  }

  mover(sentido: number): void {
    const nueva = new Date(this.fechaActual);
    if (this.vista === 'dia') nueva.setDate(nueva.getDate() + sentido);
    else if (this.vista === 'semana') nueva.setDate(nueva.getDate() + 7 * sentido);
    else nueva.setMonth(nueva.getMonth() + sentido, 1);
    this.fechaActual = nueva;
    this.turnoSeleccionado = null;
    this.cargar();
  }
  irHoy(): void { this.fechaActual = new Date(); this.turnoSeleccionado = null; this.cargar(); }
  seleccionarVista(vista: Vista): void { this.vista = vista; this.turnoSeleccionado = null; this.cargar(); }
  verDia(turno: AgendaAppointmentDto): void { this.fechaActual = new Date(turno.startUtc); this.vista = 'dia'; this.cargar(); }

  // ── Datos para la plantilla ────────────────────────────────────────────────
  get turnosFiltrados(): AgendaAppointmentDto[] {
    const texto = this.busqueda.toLowerCase().trim();
    if (!texto) return this.turnos;
    return this.turnos.filter(turno => turno.patientName.toLowerCase().includes(texto) || (turno.patientPhone ?? '').includes(texto));
  }
  get turnosActivos(): AgendaAppointmentDto[] { return this.turnosFiltrados.filter(turno => turno.status !== 'Cancelled'); }

  get columnas(): ProfessionalDto[] {
    return this.profesionalSeleccionado === 'Todos' ? this.profesionales : this.profesionales.filter(p => p.userId === this.profesionalSeleccionado);
  }
  turnosDe(profesional: ProfessionalDto): AgendaAppointmentDto[] { return this.turnosActivos.filter(turno => turno.professionalUserId === profesional.userId); }
  bloqueosDe(profesional: ProfessionalDto): ScheduleBlockDto[] {
    return this.bloqueos.filter(bloqueo => bloqueo.kind !== 'ExtraAvailability' && (!bloqueo.professionalUserId || bloqueo.professionalUserId === profesional.userId));
  }

  /** Horas visibles en la vista de día: de 8 a 20, ampliadas si hay turnos fuera de ese rango. */
  get horas(): number[] {
    let desde = 8, hasta = 20;
    for (const turno of this.turnosActivos) {
      const inicio = new Date(turno.startUtc), fin = new Date(turno.endUtc);
      desde = Math.min(desde, inicio.getHours());
      hasta = Math.max(hasta, fin.getHours() + (fin.getMinutes() > 0 ? 1 : 0));
    }
    return Array.from({ length: hasta - desde }, (_, indice) => desde + indice);
  }
  get altoDelDia(): number { return this.horas.length * ALTO_HORA; }
  private posicion(inicioUtc: string, finUtc: string): { top: number; height: number } {
    const inicio = new Date(inicioUtc), fin = new Date(finUtc);
    const dia = new Date(this.fechaActual); dia.setHours(this.horas[0], 0, 0, 0);
    const top = Math.max(0, (inicio.getTime() - dia.getTime()) / 3_600_000 * ALTO_HORA);
    const bottom = Math.min(this.altoDelDia, (fin.getTime() - dia.getTime()) / 3_600_000 * ALTO_HORA);
    return { top, height: Math.max(26, bottom - top - 2) };
  }
  estilo(elemento: { startUtc: string; endUtc: string }): Record<string, string> {
    const { top, height } = this.posicion(elemento.startUtc, elemento.endUtc);
    return { top: `${top}px`, height: `${height}px` };
  }
  get lineaDeAhora(): number | null {
    const ahora = new Date();
    if (this.vista !== 'dia' || fechaIso(ahora) !== fechaIso(this.fechaActual)) return null;
    const horas = ahora.getHours() + ahora.getMinutes() / 60 - this.horas[0];
    return horas < 0 || horas > this.horas.length ? null : horas * ALTO_HORA;
  }

  hora(instante: string): string { return horaLocal(instante); }
  fecha(instante: string): string { return fechaCorta(instante); }
  iniciales(nombre: string): string { return iniciales(nombre); }
  contar(estado: AppointmentStatus): number { return this.turnos.filter(turno => turno.status === estado).length; }
  get accionesDisponibles(): Accion[] { return this.turnoSeleccionado && this.puedeEscribir ? ACCIONES[this.turnoSeleccionado.status] ?? [] : []; }
  get puedeCancelar(): boolean {
    return this.puedeEscribir && !!this.turnoSeleccionado && ['Pending', 'Confirmed', 'Arrived', 'InWaitingRoom'].includes(this.turnoSeleccionado.status);
  }

  // ── Acciones sobre un turno ────────────────────────────────────────────────
  seleccionarTurno(turno: AgendaAppointmentDto): void { this.turnoSeleccionado = turno; this.mensaje = ''; this.cancelando = false; this.motivoCancelacion = ''; }

  ejecutar(accion: Accion): void {
    if (!this.turnoSeleccionado) return;
    this.api.accionTurno(this.turnoSeleccionado.id, accion.accion).subscribe({
      next: turno => { this.error = ''; this.mensaje = `El turno de ${turno.patientName} ahora figura como: ${ESTADOS_DE_TURNO[turno.status]}.`; this.cargar(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo actualizar el turno.'))
    });
  }

  confirmarCancelacion(): void {
    if (!this.turnoSeleccionado) return;
    this.api.cancelarTurno(this.turnoSeleccionado.id, this.motivoCancelacion.trim() || null).subscribe({
      next: turno => { this.error = ''; this.cancelando = false; this.mensaje = `Se canceló el turno de ${turno.patientName}.`; this.cargar(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo cancelar el turno.'))
    });
  }

  // ── Alta de turno ──────────────────────────────────────────────────────────
  private abrirDesdeLaUrl(): void {
    const parametros = this.route.snapshot.queryParamMap;
    if (parametros.get('nuevo') !== '1' || !this.puedeEscribir) return;
    this.abrirFormulario();
    const pacienteId = parametros.get('paciente');
    if (pacienteId) {
      this.api.paciente(pacienteId).subscribe({
        next: paciente => this.elegirPaciente({
          id: paciente.id, firstName: paciente.firstName, lastName: paciente.lastName, documentNumber: paciente.documentNumber,
          phone: paciente.phone, status: paciente.status
        })
      });
    }
  }

  abrirFormulario(): void {
    const propio = this.profesionales.find(p => p.userId === this.auth.userId);
    const profesional = this.profesionalSeleccionado !== 'Todos'
      ? this.profesionales.find(p => p.userId === this.profesionalSeleccionado)
      : propio ?? this.profesionales[0];
    this.formularioTurno.reset({
      fecha: fechaIso(this.fechaActual), hora: '09:00', duracion: profesional?.defaultAppointmentMinutes ?? 30,
      profesional: profesional?.userId ?? '', gabinete: '', motivo: '', sobreturno: false
    });
    this.textoPaciente = '';
    this.pacientesEncontrados = [];
    this.pacienteElegido = null;
    this.errorFormulario = '';
    this.mensaje = '';
    this.mostrarFormulario = true;
  }
  cerrarFormulario(): void { this.mostrarFormulario = false; }

  buscarPaciente(): void { this.pacienteElegido = null; this.busquedaDePacientes.next(this.textoPaciente); }
  elegirPaciente(paciente: PatientListItemDto): void {
    this.pacienteElegido = paciente;
    this.textoPaciente = `${paciente.firstName} ${paciente.lastName}`;
    this.pacientesEncontrados = [];
  }
  alCambiarProfesional(): void {
    const profesional = this.profesionales.find(p => p.userId === this.formularioTurno.controls.profesional.value);
    if (profesional) this.formularioTurno.controls.duracion.setValue(profesional.defaultAppointmentMinutes);
  }

  guardarTurno(): void {
    if (!this.pacienteElegido) { this.errorFormulario = 'Buscá y elegí un paciente de la lista. Si es nuevo, registralo primero en Pacientes.'; return; }
    if (this.formularioTurno.invalid) { this.formularioTurno.markAllAsTouched(); this.errorFormulario = 'Completá la fecha, la hora, el profesional y el motivo.'; return; }
    const datos = this.formularioTurno.getRawValue();
    // La fecha y la hora se interpretan en la zona horaria del navegador y se envían en UTC.
    const inicio = new Date(`${datos.fecha}T${datos.hora}:00`);
    this.guardando = true;
    this.errorFormulario = '';
    this.api.crearTurno({
      patientId: this.pacienteElegido.id, professionalUserId: datos.profesional, gabineteId: datos.gabinete || null,
      start: inicio.toISOString(), durationMinutes: datos.duracion, reason: datos.motivo.trim(),
      isOverbooking: datos.sobreturno, confirmed: false
    }).subscribe({
      next: turno => {
        this.guardando = false;
        this.mostrarFormulario = false;
        this.mensaje = `Se agendó el turno de ${turno.patientName} para el ${fechaCorta(turno.startUtc)} a las ${horaLocal(turno.startUtc)}.`;
        this.fechaActual = new Date(turno.startUtc);
        this.cargar();
      },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo agendar el turno.'); }
    });
  }
}
