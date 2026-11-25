import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, NgZone, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, debounceTime, of, switchMap } from 'rxjs';
import { AccionTurno, ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AgendaAppointmentDto, AgendaLabWorkDto, AppointmentDto, AppointmentStatus, LabWorkStatus, GabineteDto, PatientListItemDto, ProfessionalDto, ScheduleBlockDto } from '../../api/schema';
import { esConflictoDeConcurrencia, fechaCorta, fechaIso, iniciales, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { diaDeCalendario, fechaEnZona, horaEnZona, instanteEnZona, minutosEnZona, partesEnZona, zonaDelNavegador, zonaValida } from '../../core/zona-horaria';
import { DialogDirective } from '../../shared/dialog.directive';
import { PluralPipe } from '../../shared/plural.pipe';
import { horarioPredeterminado } from './horario-predeterminado';

type Vista = 'dia' | 'semana' | 'mes';
interface Accion { titulo: string; accion: AccionTurno; }
interface TurnoEnGrilla { turno: AgendaAppointmentDto; top: number; alto: number; trabajos: AgendaLabWorkDto[]; resumen: string; }
interface BloqueoEnGrilla { bloqueo: ScheduleBlockDto; top: number; alto: number; }
interface ColumnaDelDia { profesional: ProfessionalDto; turnos: TurnoEnGrilla[]; bloqueos: BloqueoEnGrilla[]; }

const ALTO_HORA = 64; // píxeles por hora en la vista de día
const ANCHO_TARJETA = 300; // ancho de la tarjeta de detalle
const PRESION_LARGA_MS = 500; // en pantallas táctiles, mantener presionado un turno abre su detalle

const ESTADOS_DE_TRABAJO: Record<LabWorkStatus, string> = {
  Pending: 'Solicitado', Sent: 'Enviado', InProduction: 'En proceso', ReadyForPickup: 'Listo para retirar',
  Received: 'Recibido', Placed: 'Colocado al paciente', Cancelled: 'Cancelado'
};

const ACCIONES: Partial<Record<AppointmentStatus, Accion[]>> = {
  Pending: [{ titulo: '✓ Confirmar', accion: 'confirm' }, { titulo: 'Registrar llegada', accion: 'check-in' }, { titulo: 'No asistió', accion: 'no-show' }],
  Confirmed: [{ titulo: 'Registrar llegada', accion: 'check-in' }, { titulo: 'Volver a pendiente', accion: 'mark-pending' }, { titulo: 'No asistió', accion: 'no-show' }],
  Arrived: [{ titulo: 'Pasar a sala de espera', accion: 'waiting-room' }, { titulo: 'Iniciar atención', accion: 'start' }],
  InWaitingRoom: [{ titulo: 'Iniciar atención', accion: 'start' }],
  InProgress: [{ titulo: '✓ Marcar atendido', accion: 'attend' }]
};

const CLASE_DE_ESTADO: Record<AppointmentStatus, string> = {
  Pending: 'pendiente', Confirmed: 'confirmado', Arrived: 'en-sala', InWaitingRoom: 'en-sala', InProgress: 'en-sala',
  Attended: 'atendido', Cancelled: 'cancelado', NoShow: 'ausente'
};

/** Sólo los turnos pendientes o confirmados admiten editar el motivo (el backend rechaza el resto con appointment_not_editable). */
const ESTADOS_EDITABLES: AppointmentStatus[] = ['Pending', 'Confirmed'];

function piezasDe(trabajo: AgendaLabWorkDto): string {
  const piezas = trabajo.toothCodes ?? [];
  return piezas.length ? `${piezas.length === 1 ? 'Pieza' : 'Piezas'} ${piezas.join(', ')}` : 'Sin pieza indicada';
}

/**
 * Agenda del consultorio. OnPush: todo el estado de la plantilla son signals y los datos derivados
 * (turnos por profesional, posiciones en la grilla, totales) se calculan una vez en computed().
 * Las fechas y horas se interpretan en la zona horaria del consultorio activo (memberships[].timeZoneId de la
 * sesión); si la sesión no la trae, se usa la que informa la agenda (AgendaDto.timeZoneId).
 */
@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, DialogDirective, PluralPipe],
  templateUrl: './agenda.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './agenda.component.css'
})
export class AgendaComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly busquedaDePacientes = new Subject<string>();

  get puedeEscribir(): boolean { return this.auth.tiene('appointments.write'); }
  get puedeSobreturno(): boolean { return this.auth.tiene('appointments.overbook'); }
  readonly estados = ESTADOS_DE_TURNO;
  readonly clases = CLASE_DE_ESTADO;
  readonly estadosDeTrabajo = ESTADOS_DE_TRABAJO;

  /** Zona que informa la respuesta de la agenda: sólo se usa si la sesión no trae la del consultorio. */
  private readonly zonaDeLaAgenda = signal<string | null>(null);
  /** Zona del consultorio: la de la membresía activa; si no, la de la agenda; si no, la del navegador. */
  readonly zona = computed(() => zonaValida(this.auth.membresia()?.timeZoneId) ?? this.zonaDeLaAgenda() ?? zonaDelNavegador());
  /** Mientras el usuario no navegue, la fecha visible sigue a "hoy" en la zona del consultorio. */
  private siguiendoHoy = true;
  private readonly ahora = signal(new Date());

  readonly vista = signal<Vista>('dia');
  /** Día de calendario visible (campos locales = fecha en la zona del consultorio). */
  readonly fechaActual = signal(diaDeCalendario(fechaEnZona(new Date(), this.zona())));
  readonly busqueda = signal('');
  readonly profesionalSeleccionado = signal('Todos');
  readonly profesionales = signal<ProfessionalDto[]>([]);
  readonly gabinetes = signal<GabineteDto[]>([]);
  readonly turnos = signal<AgendaAppointmentDto[]>([]);
  readonly bloqueos = signal<ScheduleBlockDto[]>([]);
  readonly turnoSeleccionado = signal<AgendaAppointmentDto | null>(null);
  readonly cargando = signal(true);
  readonly mensaje = signal('');
  readonly error = signal('');

  // Eliminación con motivo opcional (el backend lo registra como turno cancelado y deja de mostrarse en la agenda)
  readonly cancelando = signal(false);
  readonly motivoCancelacion = signal('');

  // Edición del motivo de consulta
  readonly editandoMotivo = signal(false);
  readonly guardandoMotivo = signal(false);
  /** Turno leído al empezar a editar: aporta la versión y los datos que el PUT mantiene sin cambios. */
  private turnoEnEdicion: AppointmentDto | null = null;
  readonly motivoEditado = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] });

  // Alta de turno
  readonly mostrarFormulario = signal(false);
  readonly guardando = signal(false);
  readonly errorFormulario = signal('');
  readonly textoPaciente = signal('');
  readonly pacientesEncontrados = signal<PatientListItemDto[]>([]);
  readonly pacienteElegido = signal<PatientListItemDto | null>(null);
  readonly formularioTurno = new FormGroup({
    fecha: new FormControl(fechaEnZona(new Date(), this.zona()), { nonNullable: true, validators: [Validators.required] }),
    hora: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    duracion: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(5), Validators.max(480)] }),
    profesional: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gabinete: new FormControl('', { nonNullable: true }),
    motivo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] }),
    sobreturno: new FormControl(false, { nonNullable: true })
  });

  // Tarjeta de detalle (mouse, teclado o presión larga en pantallas táctiles)
  readonly turnoEnDetalle = signal<AgendaAppointmentDto | null>(null);
  readonly posicionDetalle = signal({ top: 0, left: 0 });
  private temporizadorPresion: ReturnType<typeof setTimeout> | null = null;
  private inicioPresion = { x: 0, y: 0 };
  private ignorarSiguienteClic = false;

  constructor() {
    this.busquedaDePacientes.pipe(
      debounceTime(250),
      switchMap(texto => (texto.trim().length < 2 ? of(null) : this.api.pacientes({ Search: texto.trim(), pageSize: 8 }))),
      takeUntilDestroyed()
    ).subscribe({ next: pagina => this.pacientesEncontrados.set(pagina?.items ?? []) });

    this.api.profesionales().pipe(takeUntilDestroyed()).subscribe({
      next: profesionales => {
        this.profesionales.set(profesionales);
        // Quien sólo puede ver su propia agenda arranca filtrado en sí mismo.
        if (!this.auth.tiene('agenda.all_professionals') && profesionales.some(p => p.userId === this.auth.userId)) {
          this.profesionalSeleccionado.set(this.auth.userId!);
        }
        this.cargar();
        this.abrirDesdeLaUrl();
      },
      error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudo cargar la agenda.')); }
    });
    this.api.gabinetes().pipe(takeUntilDestroyed()).subscribe({ next: gabinetes => this.gabinetes.set(gabinetes.filter(g => g.isActive)) });

    // Scroll, resize y reloj fuera de la zona de Angular: no disparan detección de cambios global.
    // Sólo cuando cambia un signal se refresca esta vista.
    const zone = inject(NgZone);
    zone.runOutsideAngular(() => {
      const ocultar = () => { if (this.turnoEnDetalle()) this.ocultarDetalle(); };
      window.addEventListener('scroll', ocultar, { passive: true });
      window.addEventListener('resize', ocultar, { passive: true });
      const reloj = setInterval(() => this.ahora.set(new Date()), 60_000);
      this.destroyRef.onDestroy(() => {
        window.removeEventListener('scroll', ocultar);
        window.removeEventListener('resize', ocultar);
        clearInterval(reloj);
        this.cancelarPresion();
        document.removeEventListener('pointerdown', this.alTocarFuera, true);
      });
    });
  }

  // ── Rango visible ──────────────────────────────────────────────────────────
  private readonly rango = computed(() => {
    const vista = this.vista();
    const desde = new Date(this.fechaActual());
    if (vista === 'semana') desde.setDate(desde.getDate() - ((desde.getDay() + 6) % 7));
    if (vista === 'mes') desde.setDate(1);
    const hasta = new Date(desde);
    if (vista === 'dia') hasta.setDate(hasta.getDate() + 1);
    else if (vista === 'semana') hasta.setDate(hasta.getDate() + 7);
    else hasta.setMonth(hasta.getMonth() + 1);
    return { desde, hasta };
  });

  readonly fechaTitulo = computed(() => {
    const { desde, hasta } = this.rango();
    const vista = this.vista();
    if (vista === 'dia' || vista === 'mes') {
      const opciones: Intl.DateTimeFormatOptions = vista === 'dia'
        ? { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }
        : { month: 'long', year: 'numeric' };
      const texto = new Intl.DateTimeFormat('es-AR', opciones).format(desde);
      return texto.charAt(0).toUpperCase() + texto.slice(1);
    }
    const fin = new Date(hasta); fin.setDate(fin.getDate() - 1);
    return `Semana del ${fechaCorta(fechaIso(desde))} al ${fechaCorta(fechaIso(fin))}`;
  });

  /** @param limpiarError false para conservar el aviso de un conflicto mientras se recargan los datos. */
  cargar(limpiarError = true): void {
    const { desde, hasta } = this.rango();
    this.cargando.set(true);
    const ultimoDia = new Date(hasta); ultimoDia.setDate(ultimoDia.getDate() - 1);
    const profesional = this.profesionalSeleccionado();
    this.api.agenda(fechaIso(desde), fechaIso(ultimoDia), profesional === 'Todos' ? null : profesional)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agenda => {
          if (this.adoptarZona(agenda.timeZoneId)) return; // la fecha de "hoy" cambió con la zona: se recargó
          this.cargando.set(false);
          if (limpiarError) this.error.set('');
          // Los turnos eliminados (cancelados) no se muestran en ninguna vista; los ausentes sí.
          const turnos = agenda.appointments.filter(turno => turno.status !== 'Cancelled').sort((a, b) => a.startUtc.localeCompare(b.startUtc));
          this.turnos.set(turnos);
          this.bloqueos.set(agenda.blocks);
          this.turnoEnDetalle.set(null);
          const seleccionado = this.turnoSeleccionado();
          if (seleccionado) this.turnoSeleccionado.set(turnos.find(turno => turno.id === seleccionado.id) ?? null);
        },
        error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudo cargar la agenda.')); }
      });
  }

  /** Toma la zona que informa la agenda (respaldo). Devuelve true si eso cambió el día visible y se pidió de nuevo la agenda. */
  private adoptarZona(timeZoneId: string | null | undefined): boolean {
    const zonaInformada = zonaValida(timeZoneId);
    if (!zonaInformada || zonaInformada === this.zonaDeLaAgenda()) return false;
    const anterior = this.zona();
    this.zonaDeLaAgenda.set(zonaInformada);
    const zona = this.zona();
    if (zona === anterior || !this.siguiendoHoy) return false;
    const hoy = fechaEnZona(new Date(), zona);
    if (hoy === fechaIso(this.fechaActual())) return false;
    this.fechaActual.set(diaDeCalendario(hoy));
    this.cargar();
    return true;
  }

  mover(sentido: number): void {
    const nueva = new Date(this.fechaActual());
    const vista = this.vista();
    if (vista === 'dia') nueva.setDate(nueva.getDate() + sentido);
    else if (vista === 'semana') nueva.setDate(nueva.getDate() + 7 * sentido);
    else nueva.setMonth(nueva.getMonth() + sentido, 1);
    this.siguiendoHoy = false;
    this.fechaActual.set(nueva);
    this.turnoSeleccionado.set(null);
    this.cargar();
  }
  irHoy(): void {
    this.siguiendoHoy = true;
    this.fechaActual.set(diaDeCalendario(fechaEnZona(new Date(), this.zona())));
    this.turnoSeleccionado.set(null);
    this.cargar();
  }
  seleccionarVista(vista: Vista): void { this.vista.set(vista); this.turnoSeleccionado.set(null); this.cargar(); }
  verDia(turno: AgendaAppointmentDto): void {
    this.siguiendoHoy = false;
    this.fechaActual.set(diaDeCalendario(fechaEnZona(turno.startUtc, this.zona())));
    this.vista.set('dia');
    this.cargar();
  }
  cambiarProfesional(valor: string): void { this.profesionalSeleccionado.set(valor); this.cargar(); }

  // ── Datos derivados para la plantilla ──────────────────────────────────────
  readonly turnosFiltrados = computed(() => {
    const texto = this.busqueda().toLowerCase().trim();
    const turnos = this.turnos();
    if (!texto) return turnos;
    return turnos.filter(turno => turno.patientName.toLowerCase().includes(texto) || (turno.patientPhone ?? '').includes(texto));
  });

  readonly columnas = computed(() => {
    const elegido = this.profesionalSeleccionado();
    const profesionales = this.profesionales();
    return elegido === 'Todos' ? profesionales : profesionales.filter(p => p.userId === elegido);
  });

  /** Horas visibles en la vista de día: de 8 a 20, ampliadas si hay turnos fuera de ese rango. */
  readonly horas = computed(() => {
    const zona = this.zona();
    let desde = 8, hasta = 20;
    for (const turno of this.turnosFiltrados()) {
      const inicio = partesEnZona(turno.startUtc, zona), fin = partesEnZona(turno.endUtc, zona);
      desde = Math.min(desde, inicio.hora);
      hasta = Math.max(hasta, fin.hora + (fin.minuto > 0 ? 1 : 0));
    }
    return Array.from({ length: hasta - desde }, (_, indice) => desde + indice);
  });
  readonly altoDelDia = computed(() => this.horas().length * ALTO_HORA);
  readonly columnasCss = computed(() => `60px repeat(${this.columnas().length || 1}, minmax(0, 1fr))`);

  /** Posición vertical (px) de un tramo dentro del día visible, en la zona del consultorio. */
  private posicion(inicioUtc: string, finUtc: string): { top: number; alto: number } {
    const zona = this.zona();
    const dia = fechaIso(this.fechaActual());
    const base = this.horas()[0] * 60;
    const minutos = (instante: string, siEsAntes: number, siEsDespues: number) => {
      const fecha = fechaEnZona(instante, zona);
      return fecha < dia ? siEsAntes : fecha > dia ? siEsDespues : minutosEnZona(instante, zona);
    };
    const top = Math.max(0, (minutos(inicioUtc, 0, 24 * 60) - base) / 60 * ALTO_HORA);
    const bottom = Math.min(this.altoDelDia(), (minutos(finUtc, 0, 24 * 60) - base) / 60 * ALTO_HORA);
    return { top, alto: Math.max(26, bottom - top - 2) };
  }

  /** Turnos y bloqueos agrupados por profesional, con su posición ya calculada (una vez por cambio de datos). */
  readonly columnasDelDia = computed<ColumnaDelDia[]>(() => {
    if (this.vista() !== 'dia') return [];
    const turnos = this.turnosFiltrados();
    const bloqueos = this.bloqueos().filter(bloqueo => bloqueo.kind !== 'ExtraAvailability');
    return this.columnas().map(profesional => ({
      profesional,
      turnos: turnos.filter(turno => turno.professionalUserId === profesional.userId).map(turno => {
        const trabajos = turno.labWorks ?? [];
        return { turno, ...this.posicion(turno.startUtc, turno.endUtc), trabajos, resumen: this.resumenDeTrabajos(trabajos) };
      }),
      bloqueos: bloqueos
        .filter(bloqueo => !bloqueo.professionalUserId || bloqueo.professionalUserId === profesional.userId)
        .map(bloqueo => ({ bloqueo, ...this.posicion(bloqueo.startUtc, bloqueo.endUtc) }))
    }));
  });

  readonly lineaDeAhora = computed(() => {
    const ahora = this.ahora(), zona = this.zona();
    if (this.vista() !== 'dia' || fechaEnZona(ahora, zona) !== fechaIso(this.fechaActual())) return null;
    const horas = minutosEnZona(ahora, zona) / 60 - this.horas()[0];
    return horas < 0 || horas > this.horas().length ? null : horas * ALTO_HORA;
  });

  readonly resumen = computed(() => {
    const conteo: Partial<Record<AppointmentStatus, number>> = {};
    for (const turno of this.turnos()) conteo[turno.status] = (conteo[turno.status] ?? 0) + 1;
    const de = (estado: AppointmentStatus) => conteo[estado] ?? 0;
    // turnos() ya no incluye los eliminados (cancelados): no cuentan como agendados.
    return { agendados: this.turnos().length, confirmados: de('Confirmed'), pendientes: de('Pending'), atendidos: de('Attended') };
  });

  readonly accionesDisponibles = computed<Accion[]>(() => {
    const turno = this.turnoSeleccionado();
    return turno && this.puedeEscribir ? ACCIONES[turno.status] ?? [] : [];
  });
  readonly puedeCancelar = computed(() => {
    const turno = this.turnoSeleccionado();
    return this.puedeEscribir && !!turno && ['Pending', 'Confirmed', 'Arrived', 'InWaitingRoom'].includes(turno.status);
  });
  readonly puedeEditarMotivo = computed(() => {
    const turno = this.turnoSeleccionado();
    return this.puedeEscribir && !!turno && ESTADOS_EDITABLES.includes(turno.status);
  });

  hora(instante: string): string { return horaEnZona(instante, this.zona()); }
  fecha(instante: string): string { return fechaCorta(fechaEnZona(instante, this.zona())); }
  iniciales(nombre: string): string { return iniciales(nombre); }
  piezas(trabajo: AgendaLabWorkDto): string { return piezasDe(trabajo); }
  /** Texto para el aria-label y el title de la marca "T". */
  resumenDeTrabajos(trabajos: AgendaLabWorkDto[]): string {
    return `Trabajos de laboratorio: ${trabajos.map(t => `${piezasDe(t)} · ${t.workType} · Mecánico dental: ${t.laboratoryName}`).join('; ')}`;
  }

  // ── Tarjeta de detalle ─────────────────────────────────────────────────────
  private ubicarDetalle(turno: AgendaAppointmentDto, elemento: HTMLElement): void {
    const caja = elemento.getBoundingClientRect();
    // Se ubica a la derecha del turno; si no entra, a la izquierda. Verticalmente, dentro de la ventana.
    const entraALaDerecha = caja.right + 8 + ANCHO_TARJETA <= window.innerWidth;
    const left = entraALaDerecha ? caja.right + 8 : Math.max(8, caja.left - 8 - ANCHO_TARJETA);
    const top = Math.min(Math.max(8, caja.top), Math.max(8, window.innerHeight - 280));
    this.posicionDetalle.set({ top, left });
    this.turnoEnDetalle.set(turno);
  }
  /** Mouse: al entrar. Los eventos táctiles se ignoran (en el táctil se usa la presión larga). */
  alEntrar(turno: AgendaAppointmentDto, evento: PointerEvent): void {
    if (evento.pointerType === 'mouse') this.ubicarDetalle(turno, evento.currentTarget as HTMLElement);
  }
  alSalir(evento: PointerEvent): void { if (evento.pointerType === 'mouse') this.ocultarDetalle(); }
  /** Teclado: sólo con foco visible (un toque también enfoca el botón, y no debe abrir la tarjeta). */
  alEnfocar(turno: AgendaAppointmentDto, evento: FocusEvent): void {
    const elemento = evento.currentTarget as HTMLElement;
    if (elemento.matches(':focus-visible')) this.ubicarDetalle(turno, elemento);
  }
  ocultarDetalle(): void { this.turnoEnDetalle.set(null); }

  alPresionar(turno: AgendaAppointmentDto, evento: PointerEvent): void {
    if (evento.pointerType === 'mouse') return;
    this.cancelarPresion();
    const elemento = evento.currentTarget as HTMLElement;
    this.inicioPresion = { x: evento.clientX, y: evento.clientY };
    this.temporizadorPresion = setTimeout(() => {
      this.temporizadorPresion = null;
      this.ignorarSiguienteClic = true;
      this.ubicarDetalle(turno, elemento);
      // El próximo toque fuera de la tarjeta la cierra.
      setTimeout(() => document.addEventListener('pointerdown', this.alTocarFuera, true));
    }, PRESION_LARGA_MS);
  }
  alMoverPresion(evento: PointerEvent): void {
    if (this.temporizadorPresion && Math.hypot(evento.clientX - this.inicioPresion.x, evento.clientY - this.inicioPresion.y) > 10) this.cancelarPresion();
  }
  cancelarPresion(): void {
    if (this.temporizadorPresion) clearTimeout(this.temporizadorPresion);
    this.temporizadorPresion = null;
  }
  private readonly alTocarFuera = () => {
    document.removeEventListener('pointerdown', this.alTocarFuera, true);
    this.ocultarDetalle();
  };

  // ── Acciones sobre un turno ────────────────────────────────────────────────
  seleccionarTurno(turno: AgendaAppointmentDto): void {
    // Una presión larga abre el detalle y no debe además seleccionar el turno.
    if (this.ignorarSiguienteClic) { this.ignorarSiguienteClic = false; return; }
    this.turnoSeleccionado.set(turno);
    this.mensaje.set('');
    this.cancelando.set(false);
    this.motivoCancelacion.set('');
    this.cerrarEdicionMotivo();
  }

  private alFallarAccion(error: unknown, porDefecto: string): void {
    this.mensaje.set('');
    this.error.set(mensajeDeError(error, porDefecto));
    if (esConflictoDeConcurrencia(error)) { this.cancelando.set(false); this.cerrarEdicionMotivo(); this.cargar(false); }
  }

  ejecutar(accion: Accion): void {
    const seleccionado = this.turnoSeleccionado();
    if (!seleccionado) return;
    this.api.accionTurno(seleccionado.id, accion.accion).subscribe({
      next: turno => { this.error.set(''); this.mensaje.set(`El turno de ${turno.patientName} ahora figura como: ${ESTADOS_DE_TURNO[turno.status]}.`); this.cargar(); },
      error: error => this.alFallarAccion(error, 'No se pudo actualizar el turno.')
    });
  }

  confirmarCancelacion(): void {
    const seleccionado = this.turnoSeleccionado();
    if (!seleccionado) return;
    this.api.cancelarTurno(seleccionado.id, this.motivoCancelacion().trim() || null).subscribe({
      next: turno => { this.error.set(''); this.cancelando.set(false); this.mensaje.set(`Se eliminó el turno de ${turno.patientName}.`); this.cargar(); },
      error: error => this.alFallarAccion(error, 'No se pudo eliminar el turno.')
    });
  }

  // ── Edición del motivo de consulta ─────────────────────────────────────────
  /** Lee el turno completo (versión y notas administrativas, que la agenda no trae) y abre el campo de edición. */
  iniciarEdicionMotivo(): void {
    const seleccionado = this.turnoSeleccionado();
    if (!seleccionado || !this.puedeEditarMotivo()) return;
    this.guardandoMotivo.set(true);
    this.mensaje.set('');
    this.api.turno(seleccionado.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: turno => {
        this.guardandoMotivo.set(false);
        if (!ESTADOS_EDITABLES.includes(turno.status)) {
          // Cambió de estado desde que se cargó la agenda: se muestra el estado vigente.
          this.error.set('El turno cambió de estado y ya no se puede editar su motivo.');
          this.cargar(false);
          return;
        }
        this.error.set('');
        this.turnoEnEdicion = turno;
        this.motivoEditado.reset(turno.reason ?? '');
        this.editandoMotivo.set(true);
      },
      error: error => { this.guardandoMotivo.set(false); this.alFallarAccion(error, 'No se pudo abrir el turno para editarlo.'); }
    });
  }

  cerrarEdicionMotivo(): void {
    this.editandoMotivo.set(false);
    this.turnoEnEdicion = null;
  }

  /** PUT del turno: cambia sólo el motivo; paciente y notas administrativas se envían tal como estaban. */
  guardarMotivo(): void {
    const turno = this.turnoEnEdicion;
    if (!turno || this.guardandoMotivo()) return;
    const motivo = this.motivoEditado.value.trim();
    if (!motivo) this.motivoEditado.setValue('');
    if (this.motivoEditado.invalid) { this.motivoEditado.markAsTouched(); return; }
    this.guardandoMotivo.set(true);
    this.api.actualizarTurno(turno.id, {
      patientId: turno.patientId, reason: motivo, administrativeNotes: turno.administrativeNotes ?? null, version: turno.version
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.guardandoMotivo.set(false);
        this.cerrarEdicionMotivo();
        this.error.set('');
        this.mensaje.set('Se actualizó el motivo del turno.');
        this.cargar();
      },
      error: error => { this.guardandoMotivo.set(false); this.alFallarAccion(error, 'No se pudo actualizar el motivo del turno.'); }
    });
  }

  // ── Alta de turno ──────────────────────────────────────────────────────────
  private abrirDesdeLaUrl(): void {
    const parametros = this.route.snapshot.queryParamMap;
    if (parametros.get('nuevo') !== '1' || !this.puedeEscribir) return;
    this.abrirFormulario();
    const pacienteId = parametros.get('paciente');
    if (pacienteId) {
      this.api.paciente(pacienteId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: paciente => this.elegirPaciente({
          id: paciente.id, firstName: paciente.firstName, lastName: paciente.lastName, documentNumber: paciente.documentNumber,
          phone: paciente.phone, status: paciente.status
        })
      });
    }
  }

  abrirFormulario(): void {
    const profesionales = this.profesionales();
    const elegido = this.profesionalSeleccionado();
    const propio = profesionales.find(p => p.userId === this.auth.userId);
    const profesional = elegido !== 'Todos' ? profesionales.find(p => p.userId === elegido) : propio ?? profesionales[0];
    const { fecha, hora } = horarioPredeterminado(new Date(), fechaIso(this.fechaActual()), this.zona());
    this.formularioTurno.reset({
      fecha, hora, duracion: profesional?.defaultAppointmentMinutes ?? 30,
      profesional: profesional?.userId ?? '', gabinete: '', motivo: '', sobreturno: false
    });
    this.textoPaciente.set('');
    this.pacientesEncontrados.set([]);
    this.pacienteElegido.set(null);
    this.errorFormulario.set('');
    this.mensaje.set('');
    this.mostrarFormulario.set(true);
  }
  cerrarFormulario(): void { this.mostrarFormulario.set(false); }

  buscarPaciente(texto: string): void {
    this.textoPaciente.set(texto);
    this.pacienteElegido.set(null);
    this.busquedaDePacientes.next(texto);
  }
  elegirPaciente(paciente: PatientListItemDto): void {
    this.pacienteElegido.set(paciente);
    this.textoPaciente.set(`${paciente.firstName} ${paciente.lastName}`);
    this.pacientesEncontrados.set([]);
  }
  alCambiarProfesional(): void {
    const profesional = this.profesionales().find(p => p.userId === this.formularioTurno.controls.profesional.value);
    if (profesional) this.formularioTurno.controls.duracion.setValue(profesional.defaultAppointmentMinutes);
  }

  guardarTurno(): void {
    const paciente = this.pacienteElegido();
    if (!paciente) { this.errorFormulario.set('Buscá y elegí un paciente de la lista. Si es nuevo, registralo primero en Pacientes.'); return; }
    if (this.formularioTurno.invalid) { this.formularioTurno.markAllAsTouched(); this.errorFormulario.set('Completá la fecha, la hora, el profesional y el motivo.'); return; }
    const datos = this.formularioTurno.getRawValue();
    // La fecha y la hora se interpretan en la zona horaria del consultorio y se envían en UTC.
    const inicio = instanteEnZona(datos.fecha, datos.hora, this.zona());
    this.guardando.set(true);
    this.errorFormulario.set('');
    this.api.crearTurno({
      patientId: paciente.id, professionalUserId: datos.profesional, gabineteId: datos.gabinete || null,
      start: inicio.toISOString(), durationMinutes: datos.duracion, reason: datos.motivo.trim(),
      isOverbooking: datos.sobreturno, confirmed: false
    }).subscribe({
      next: turno => {
        this.guardando.set(false);
        this.mostrarFormulario.set(false);
        this.mensaje.set(`Se agendó el turno de ${turno.patientName} para el ${this.fecha(turno.startUtc)} a las ${this.hora(turno.startUtc)}.`);
        this.siguiendoHoy = false;
        this.fechaActual.set(diaDeCalendario(fechaEnZona(turno.startUtc, this.zona())));
        this.cargar();
      },
      error: error => { this.guardando.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo agendar el turno.')); }
    });
  }
}
