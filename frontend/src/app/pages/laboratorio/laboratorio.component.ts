import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, FormsModule, NgModel, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentDto, LabBoardDto, LabWorkDto, LabWorkStatus, LaboratoryDto, PatientListItemDto, ProfessionalDto } from '../../api/schema';
import { fechaCorta, horaLocal, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { DialogDirective } from '../../shared/dialog.directive';
import { PluralPipe } from '../../shared/plural.pipe';

const ESTADOS: Record<LabWorkStatus, string> = {
  Pending: 'Solicitado', Sent: 'Enviado', InProduction: 'En proceso', ReadyForPickup: 'Listo para retirar',
  Received: 'Recibido', Placed: 'Colocado al paciente', Cancelled: 'Cancelado'
};
const CLASES: Record<LabWorkStatus, string> = {
  Pending: 'solicitado', Sent: 'en-proceso', InProduction: 'en-proceso', ReadyForPickup: 'listo-para-retirar', Received: 'recibido', Placed: 'entregado-al-paciente', Cancelled: 'cancelled'
};
const CERRADOS: LabWorkStatus[] = ['Placed', 'Cancelled'];
const NUEVO_LABORATORIO = '__nuevo__';
/** Turnos que no sirven como turno de entrega. */
const TURNOS_DESCARTADOS = ['Cancelled', 'NoShow'];

/** Convierte "11, 21" en [11, 21]. Devuelve null si alguna pieza no es un número FDI válido. */
export function piezasFdi(texto: string): number[] | null {
  const piezas: number[] = [];
  for (const parte of texto.split(/[\s,;]+/).filter(Boolean)) {
    if (!/^\d{2}$/.test(parte)) return null;
    const cuadrante = Number(parte[0]), numero = Number(parte[1]);
    const permanente = cuadrante >= 1 && cuadrante <= 4 && numero >= 1 && numero <= 8;
    const temporal = cuadrante >= 5 && cuadrante <= 8 && numero >= 1 && numero <= 5;
    if (!permanente && !temporal) return null;
    if (!piezas.includes(Number(parte))) piezas.push(Number(parte));
  }
  return piezas;
}
export function validarPiezas(control: AbstractControl<string>): ValidationErrors | null {
  return piezasFdi(control.value ?? '') ? null : { piezas: true };
}

@Component({
  selector: 'app-laboratorio',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, DialogDirective, PluralPipe],
  templateUrl: './laboratorio.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './laboratorio.component.css'
})
export class LaboratorioComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);

  readonly nombres = ESTADOS;
  readonly clases = CLASES;
  readonly estados = Object.keys(ESTADOS) as LabWorkStatus[];
  readonly nuevoLaboratorio = NUEVO_LABORATORIO;
  get puedeCrear(): boolean { return this.auth.tiene('lab.write'); }
  get puedeSeguir(): boolean { return this.auth.tiene('lab.track') || this.auth.tiene('lab.write'); }
  get puedeCrearLaboratorio(): boolean { return this.auth.tiene('laboratories.manage'); }
  readonly trabajos = signal<LabWorkDto[]>([]);
  readonly tablero = signal<LabBoardDto | null>(null);
  readonly laboratorios = signal<LaboratoryDto[]>([]);
  readonly pacientes = signal<PatientListItemDto[]>([]);
  readonly profesionales = signal<ProfessionalDto[]>([]);
  readonly busqueda = signal('');
  readonly filtroEstado = signal('Todos');
  readonly filtroVista = signal<'Todos' | 'Activos' | 'Historial'>('Activos');
  readonly cargando = signal(true);
  readonly mostrarFormulario = signal(false);
  readonly guardando = signal(false);
  readonly mensaje = signal('');
  readonly error = signal('');
  readonly errorFormulario = signal('');

  formulario = new FormGroup({
    pacienteId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    tipoTrabajo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(120)] }),
    piezaDental: new FormControl('', { nonNullable: true, validators: [validarPiezas] }),
    turnoId: new FormControl('', { nonNullable: true }),
    laboratorioId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    laboratorioNuevo: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(150)] }),
    profesionalId: new FormControl('', { nonNullable: true }),
    fechaEntrega: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    costo: new FormControl<number | null>(null, [Validators.min(0)]),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });

  /** Turnos del paciente elegido en el formulario, para vincular el turno de entrega. */
  readonly turnosDelPaciente = signal<AppointmentDto[]>([]);
  readonly cargandoTurnos = signal(false);
  private get veTurnos(): boolean { return this.auth.tiene('appointments.read'); }
  constructor() {
    this.cargar();
    this.api.laboratorios().subscribe({ next: laboratorios => (this.laboratorios.set(laboratorios.filter(l => l.isActive))) });
    // Al cambiar de paciente se recargan sus turnos; switchMap descarta la respuesta del paciente anterior.
    this.formulario.controls.pacienteId.valueChanges.pipe(
      switchMap(pacienteId => {
        this.formulario.controls.turnoId.setValue('');
        this.turnosDelPaciente.set([]);
        if (!pacienteId || !this.veTurnos) return of<AppointmentDto[]>([]);
        this.cargandoTurnos.set(true);
        return this.api.turnos({ PatientId: pacienteId, pageSize: 100 }).pipe(map(pagina => pagina.items), catchError(() => of<AppointmentDto[]>([])));
      }),
      takeUntilDestroyed()
    ).subscribe(turnos => { this.cargandoTurnos.set(false); this.turnosDelPaciente.set(this.ordenarTurnos(turnos)); });
  }

  /** Primero los próximos (del más cercano al más lejano) y después los pasados (del más reciente al más antiguo). */
  private ordenarTurnos(turnos: AppointmentDto[]): AppointmentDto[] {
    const ahora = new Date().toISOString();
    const vigentes = turnos.filter(turno => !TURNOS_DESCARTADOS.includes(turno.status));
    const proximos = vigentes.filter(turno => turno.endUtc >= ahora).sort((a, b) => a.startUtc.localeCompare(b.startUtc));
    const pasados = vigentes.filter(turno => turno.endUtc < ahora).sort((a, b) => b.startUtc.localeCompare(a.startUtc));
    return [...proximos, ...pasados];
  }
  etiquetaDeTurno(turno: AppointmentDto): string {
    return `${fechaCorta(turno.startUtc)} · ${horaLocal(turno.startUtc)} · ${turno.reason || 'Turno'}`;
  }

  /** @param limpiarError false para conservar el aviso del error (p. ej. un conflicto) mientras se recargan los datos. */
  cargar(limpiarError = true): void {
    this.cargando.set(true);
    this.api.trabajosDeLaboratorio().subscribe({
      next: pagina => { this.cargando.set(false); if (limpiarError) this.error.set(''); this.trabajos.set(pagina.items); },
      error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudieron cargar los trabajos de laboratorio.')); }
    });
    this.api.tableroDeLaboratorio().subscribe({ next: tablero => (this.tablero.set(tablero)) });
  }

  get trabajosFiltrados(): LabWorkDto[] {
    const texto = this.busqueda().trim().toLowerCase();
    return this.trabajos().filter(trabajo => {
      const coincideTexto = `${trabajo.patientName} ${trabajo.workType} ${trabajo.laboratoryName}`.toLowerCase().includes(texto);
      const coincideEstado = this.filtroEstado() === 'Todos' || trabajo.status === this.filtroEstado();
      const cerrado = CERRADOS.includes(trabajo.status);
      const coincideVista = this.filtroVista() === 'Todos' || (this.filtroVista() === 'Historial' ? cerrado : !cerrado);
      return coincideTexto && coincideEstado && coincideVista;
    });
  }
  get activos(): number { return this.trabajos().filter(trabajo => !CERRADOS.includes(trabajo.status)).length; }
  get listosParaRetirar(): number { return this.tablero()?.countsByStatus['ReadyForPickup'] ?? this.trabajos().filter(t => t.status === 'ReadyForPickup').length; }

  verVista(vista: 'Todos' | 'Activos' | 'Historial'): void { this.filtroVista.set(vista); this.filtroEstado.set('Todos'); }

  abrirFormulario(): void {
    this.mensaje.set('');
    this.errorFormulario.set('');
    this.formulario.reset({
      pacienteId: '', tipoTrabajo: '', piezaDental: '', turnoId: '', laboratorioId: '', laboratorioNuevo: '',
      profesionalId: this.auth.esProfesional() ? this.auth.userId ?? '' : '', fechaEntrega: '', costo: null, observaciones: ''
    });
    this.mostrarFormulario.set(true);
    if (!this.pacientes().length) this.api.pacientes({ pageSize: 100 }).subscribe({ next: pagina => (this.pacientes.set(pagina.items.filter(p => p.status !== 'Inactive'))) });
    if (!this.profesionales().length) this.api.profesionales().subscribe({ next: profesionales => (this.profesionales.set(profesionales)) });
  }
  cerrarFormulario(): void { this.mostrarFormulario.set(false); }

  guardarTrabajo(): void {
    const datos = this.formulario.getRawValue();
    const creaLaboratorio = datos.laboratorioId === NUEVO_LABORATORIO;
    if (this.formulario.invalid || (creaLaboratorio && datos.laboratorioNuevo.trim().length < 2)) {
      this.formulario.markAllAsTouched();
      if (creaLaboratorio && datos.laboratorioNuevo.trim().length < 2) this.errorFormulario.set('Escribí el nombre del mecánico dental o laboratorio nuevo.');
      return;
    }
    this.guardando.set(true);
    this.errorFormulario.set('');
    const piezas = piezasFdi(datos.piezaDental) ?? [];
    const laboratorio$: Observable<LaboratoryDto | null> = creaLaboratorio ? this.api.crearLaboratorio(datos.laboratorioNuevo.trim()) : of(null);
    laboratorio$.pipe(
      switchMap(nuevo => {
        if (nuevo) this.laboratorios.set([...this.laboratorios(), nuevo]);
        return this.api.crearTrabajoDeLaboratorio({
          patientId: datos.pacienteId, laboratoryId: nuevo ? nuevo.id : datos.laboratorioId, professionalUserId: datos.profesionalId || null,
          workType: datos.tipoTrabajo.trim(), toothCode: piezas[0] ?? null, toothCodes: piezas.length ? piezas : null,
          appointmentId: datos.turnoId || null,
          estimatedDeliveryDate: datos.fechaEntrega, cost: datos.costo, notes: datos.observaciones.trim() || null
        });
      })
    ).subscribe({
      next: trabajo => {
        this.guardando.set(false);
        this.mostrarFormulario.set(false);
        this.mensaje.set(`Se registró el trabajo "${trabajo.workType}" para ${trabajo.patientName}.`);
        this.cargar();
      },
      error: error => { this.guardando.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo registrar el trabajo.')); }
    });
  }

  /** @param selector el control del desplegable: si el backend rechaza el cambio, vuelve a mostrar el estado real. */
  cambiarEstado(trabajo: LabWorkDto, estado: LabWorkStatus, selector?: NgModel): void {
    if (estado === trabajo.status) return;
    this.api.estadoTrabajoDeLaboratorio(trabajo.id, estado).subscribe({
      next: actualizado => { this.error.set(''); this.mensaje.set(`El trabajo de ${actualizado.patientName} ahora figura como: ${ESTADOS[actualizado.status]}.`); this.cargar(); },
      // Si el backend rechaza el cambio (conflicto de concurrencia 409, transición inválida o falta de permiso), se vuelve a mostrar el estado real.
      // Las filas se reutilizan (track por id), así que el desplegable se restablece a mano.
      error: error => {
        selector?.control.setValue(trabajo.status);
        this.mensaje.set('');
        this.error.set(mensajeDeError(error, 'No se pudo actualizar el estado del trabajo.'));
        this.cargar(false);
      }
    });
  }

  /** Piezas del trabajo; si el backend todavía no envía `toothCodes`, se usa `toothCode`. */
  piezasDe(trabajo: LabWorkDto): string {
    const piezas = trabajo.toothCodes?.length ? trabajo.toothCodes : trabajo.toothCode ? [trabajo.toothCode] : [];
    if (!piezas.length) return 'Sin pieza indicada';
    return `${piezas.length === 1 ? 'Pieza' : 'Piezas'} ${piezas.join(', ')}`;
  }
  turnoDe(trabajo: LabWorkDto): string {
    return trabajo.appointmentStartUtc ? `${fechaCorta(trabajo.appointmentStartUtc)} · ${horaLocal(trabajo.appointmentStartUtc)}` : '';
  }

  fechaLegible(fecha: string | null | undefined): string { return fecha ? fechaCorta(fecha) : 'Sin fecha'; }
  formatoPesos(importe: number | null | undefined): string { return importe === null || importe === undefined ? '—' : pesos(importe); }
}
