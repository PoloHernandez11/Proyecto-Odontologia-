import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OdontiaApi } from '../../api/odontia-api.service';
import { AccountMovementDto, AccountStatementDto, ClinicalEntryDto, ClinicalEntryType, ClinicalRecordDto, MovementType, PatientDto, PaymentMethod } from '../../api/schema';
import { codigoDeError, esConflictoDeConcurrencia, fechaCorta, horaLocal, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { fechaDelConsultorio, formatoEnConsultorio, instanteEnZona, zonaDelConsultorio } from '../../core/zona-horaria';
import { DialogDirective } from '../../shared/dialog.directive';
import { METODOS_DE_PAGO } from '../pagos/pagos.component';

const TIPOS: Record<ClinicalEntryType, string> = {
  Consultation: 'Consulta', Evolution: 'Evolución', Procedure: 'Procedimiento', Observation: 'Observación', Prescription: 'Receta', Study: 'Estudio'
};
const ANTECEDENTES: [keyof ClinicalRecordDto['medicalHistory'], string][] = [
  ['diabetes', 'Diabetes'], ['hypertension', 'Hipertensión'], ['heartDisease', 'Cardiopatía'], ['anticoagulated', 'Anticoagulado'],
  ['pregnant', 'Embarazo'], ['asthma', 'Asma'], ['hepatitis', 'Hepatitis'], ['smoker', 'Fumador']
];
export const TIPOS_DE_MOVIMIENTO: Record<MovementType, string> = {
  BudgetCharge: 'Presupuesto', BudgetChargeReversal: 'Anulación de presupuesto', Payment: 'Pago', PaymentReversal: 'Anulación de pago',
  Adjustment: 'Ajuste', ManualCharge: 'Cargo manual', ManualChargeReversal: 'Anulación de cargo'
};

/** Registros de un mismo día del consultorio (yyyy-MM-dd en su zona horaria). */
export interface DiaDeRegistros { fecha: string; titulo: string; registros: ClinicalEntryDto[]; }

/** Fecha yyyy-MM-dd que no sea posterior a hoy en el consultorio (vacía es válida: la obligatoriedad la decide cada control). */
function noFutura(control: AbstractControl<string>): ValidationErrors | null {
  return control.value && control.value > fechaDelConsultorio() ? { futura: true } : null;
}
/** Importe mayor a cero con hasta dos decimales. */
function importeValido(control: AbstractControl<number | null>): ValidationErrors | null {
  const valor = control.value;
  if (valor === null || valor === undefined || (valor as unknown) === '') return { required: true };
  const numero = Number(valor);
  return Number.isFinite(numero) && numero > 0 && Math.abs(Math.round(numero * 100) - numero * 100) < 1e-6 ? null : { importe: true };
}

/**
 * Instante a enviar para un día elegido: hoy, el momento actual; un día anterior, el mediodía de ese día en la zona del consultorio
 * (así el registro queda en ese día sin importar la zona del navegador).
 */
export function instanteDelDia(fecha: string): string {
  return fecha === fechaDelConsultorio() ? new Date().toISOString() : instanteEnZona(fecha, '12:00', zonaDelConsultorio()).toISOString();
}

/** "Martes 24 de noviembre de 2026" para una fecha yyyy-MM-dd del consultorio. */
export function tituloDelDia(fecha: string): string {
  const instante = instanteEnZona(fecha, '12:00', zonaDelConsultorio());
  const dia = formatoEnConsultorio(instante, { weekday: 'long' });
  const resto = formatoEnConsultorio(instante, { day: 'numeric', month: 'long', year: 'numeric' });
  return `${dia.charAt(0).toUpperCase()}${dia.slice(1)} ${resto}`;
}

@Component({
  selector: 'app-historia-clinica', standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DialogDirective],
  templateUrl: './historia-clinica.component.html', changeDetection: ChangeDetectionStrategy.OnPush,
 styleUrl: './historia-clinica.component.css'
})
export class HistoriaClinicaComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly pacienteId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  readonly tipos = TIPOS;
  readonly tiposDisponibles = Object.entries(TIPOS) as [ClinicalEntryType, string][];
  readonly tiposDeMovimiento = TIPOS_DE_MOVIMIENTO;
  readonly listaDeMetodos = Object.entries(METODOS_DE_PAGO) as [PaymentMethod, string][];
  // Registrar información clínica exige el permiso y además ser profesional del consultorio.
  get puedeEscribir(): boolean { return this.auth.tiene('clinical.write') && this.auth.esProfesional(); }
  get veOdontograma(): boolean { return this.auth.tiene('odontogram.read'); }
  get veCuenta(): boolean { return this.auth.tiene('finance.read'); }
  get puedeCobrar(): boolean { return this.auth.tiene('payments.write'); }
  get puedeAnularCargo(): boolean { return this.auth.tiene('payments.void'); }
  /** Hoy en el consultorio: tope de los selectores de fecha. */
  get hoy(): string { return fechaDelConsultorio(); }
  readonly paciente = signal<PatientDto | null>(null);
  readonly historia = signal<ClinicalRecordDto | null>(null);
  readonly registros = signal<ClinicalEntryDto[]>([]);
  readonly cargando = signal(true);
  readonly noEncontrado = signal(false);
  readonly mostrarFormulario = signal(false);
  readonly guardando = signal(false);
  readonly mensaje = signal('');
  readonly error = signal('');

  /** Registros agrupados por día del consultorio: el día más reciente primero y, dentro de cada día, el registro más reciente primero. */
  readonly registrosPorDia = computed<DiaDeRegistros[]>(() => {
    const dias = new Map<string, ClinicalEntryDto[]>();
    for (const registro of this.registros()) {
      const fecha = fechaDelConsultorio(registro.occurredAtUtc);
      const lista = dias.get(fecha);
      if (lista) lista.push(registro); else dias.set(fecha, [registro]);
    }
    return [...dias.entries()]
      .sort(([a], [b]) => b.localeCompare(a))
      .map(([fecha, registros]) => ({ fecha, titulo: tituloDelDia(fecha), registros }));
  });

  formulario = new FormGroup({
    fecha: new FormControl(fechaDelConsultorio(), { nonNullable: true, validators: [Validators.required, noFutura] }),
    tipo: new FormControl<ClinicalEntryType>('Consultation', { nonNullable: true }),
    motivo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)] }),
    diagnostico: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    tratamiento: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    piezas: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\s*(\d{2}(\s*,\s*\d{2})*)?\s*$/)] }),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] })
  });

  // ── Pagos y saldo (cuenta corriente) ───────────────────────────────────────
  readonly cuenta = signal<AccountStatementDto | null>(null);
  readonly cargandoCuenta = signal(false);
  readonly mensajeCuenta = signal('');
  readonly errorCuenta = signal('');
  readonly mostrarCargo = signal(false);
  readonly mostrarPago = signal(false);
  readonly cargoPorAnular = signal<AccountMovementDto | null>(null);
  readonly guardandoCuenta = signal(false);
  readonly errorFormulario = signal('');
  /** Movimientos del más reciente al más antiguo. */
  readonly movimientos = computed(() => [...(this.cuenta()?.movements ?? [])].sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc)));

  formularioCargo = new FormGroup({
    descripcion: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(250)] }),
    importe: new FormControl<number | null>(null, [importeValido]),
    fecha: new FormControl('', { nonNullable: true, validators: [noFutura] })
  });
  formularioPago = new FormGroup({
    importe: new FormControl<number | null>(null, [importeValido]),
    metodo: new FormControl<PaymentMethod>('BankTransfer', { nonNullable: true }),
    fecha: new FormControl('', { nonNullable: true, validators: [noFutura] })
  });
  motivoAnulacion = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(500)] });

  constructor() {
    this.api.paciente(this.pacienteId).subscribe({
      next: paciente => { this.paciente.set(paciente); this.cargando.set(false); },
      error: () => { this.noEncontrado.set(true); this.cargando.set(false); }
    });
    this.api.historiaClinica(this.pacienteId).subscribe({ next: historia => (this.historia.set(historia)) });
    this.cargarRegistros();
    if (this.veCuenta) this.cargarCuenta();
  }

  private cargarRegistros(): void {
    this.api.evoluciones(this.pacienteId).subscribe({
      next: pagina => (this.registros.set([...pagina.items].sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc)))),
      error: error => (this.error.set(mensajeDeError(error, 'No se pudo cargar la historia clínica.')))
    });
  }

  get antecedentes(): string[] {
    const historia = this.historia()?.medicalHistory;
    return historia ? ANTECEDENTES.filter(([clave]) => historia[clave] === true).map(([, titulo]) => titulo) : [];
  }

  fecha(valor: string): string { return fechaCorta(valor); }
  hora(valor: string): string { return horaLocal(valor); }
  pesos(importe: number): string { return pesos(importe); }

  abrirFormulario(): void {
    if (!this.mostrarFormulario()) this.formulario.controls.fecha.setValue(fechaDelConsultorio());
    this.mostrarFormulario.set(!this.mostrarFormulario());
  }

  guardarRegistro(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    const descripcion = [
      datos.diagnostico.trim() && `Diagnóstico: ${datos.diagnostico.trim()}`,
      datos.tratamiento.trim() && `Tratamiento: ${datos.tratamiento.trim()}`
    ].filter(Boolean).join('\n');
    const piezas = datos.piezas.split(',').map(pieza => Number(pieza.trim())).filter(pieza => pieza > 0);
    this.guardando.set(true);
    this.error.set('');
    this.api.crearEvolucion(this.pacienteId, {
      type: datos.tipo, occurredAt: instanteDelDia(datos.fecha), title: datos.motivo.trim(), description: descripcion || null,
      indications: datos.observaciones.trim() || null, teeth: piezas.length ? piezas : null
    }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.formulario.reset();
        this.mostrarFormulario.set(false);
        this.mensaje.set('El registro se guardó en la historia clínica.');
        this.cargarRegistros();
      },
      error: error => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(error, 'No se pudo guardar el registro.'));
        // Conflicto de concurrencia: la historia cambió en otra sesión; se recarga para ver la versión vigente.
        if (esConflictoDeConcurrencia(error)) {
          this.api.historiaClinica(this.pacienteId).subscribe({ next: historia => (this.historia.set(historia)) });
          this.cargarRegistros();
        }
      }
    });
  }

  // ── Cuenta corriente ───────────────────────────────────────────────────────
  /** "Saldo pendiente: $X" o "A favor: $X" (saldo negativo: el paciente pagó de más). */
  textoDeSaldo(saldo: number): string { return saldo < 0 ? `A favor: ${pesos(-saldo)}` : `Saldo pendiente: ${pesos(saldo)}`; }
  /** Solo los cargos manuales vigentes se anulan desde acá; el resto se corrige en su propio circuito (presupuestos, recibos). */
  esAnulable(movimiento: AccountMovementDto): boolean { return movimiento.type === 'ManualCharge' && !movimiento.reversedByMovementId; }

  private cargarCuenta(alCargar?: (cuenta: AccountStatementDto) => void): void {
    this.cargandoCuenta.set(true);
    this.api.cuentaCorriente(this.pacienteId).subscribe({
      next: cuenta => { this.cargandoCuenta.set(false); this.cuenta.set(cuenta); alCargar?.(cuenta); },
      error: error => { this.cargandoCuenta.set(false); this.errorCuenta.set(mensajeDeError(error, 'No se pudo cargar la cuenta del paciente.')); }
    });
  }

  abrirCargo(): void {
    this.errorFormulario.set('');
    this.mensajeCuenta.set('');
    this.formularioCargo.reset({ descripcion: '', importe: null, fecha: '' });
    this.mostrarCargo.set(true);
  }

  registrarCargo(): void {
    if (this.formularioCargo.invalid) { this.formularioCargo.markAllAsTouched(); return; }
    const datos = this.formularioCargo.getRawValue();
    this.guardandoCuenta.set(true);
    this.errorFormulario.set('');
    this.api.registrarCargo(this.pacienteId, {
      description: datos.descripcion.trim(), amount: Number(datos.importe), occurredAt: datos.fecha ? instanteDelDia(datos.fecha) : null
    }).subscribe({
      next: cuenta => {
        this.guardandoCuenta.set(false);
        this.mostrarCargo.set(false);
        this.errorCuenta.set('');
        this.cuenta.set(cuenta);
        this.mensajeCuenta.set('Se registró el cargo.');
      },
      error: error => { this.guardandoCuenta.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo registrar el cargo.')); }
    });
  }

  abrirPago(): void {
    this.errorFormulario.set('');
    this.mensajeCuenta.set('');
    const saldo = this.cuenta()?.balance ?? 0;
    this.formularioPago.reset({ importe: saldo > 0 ? saldo : null, metodo: 'BankTransfer', fecha: '' });
    this.mostrarPago.set(true);
  }

  registrarPago(): void {
    if (this.formularioPago.invalid) { this.formularioPago.markAllAsTouched(); return; }
    const datos = this.formularioPago.getRawValue();
    this.guardandoCuenta.set(true);
    this.errorFormulario.set('');
    this.api.registrarPago({
      patientId: this.pacienteId, budgetId: null, amount: Number(datos.importe), method: datos.metodo,
      paidAt: datos.fecha ? instanteDelDia(datos.fecha) : null
    }).subscribe({
      next: () => {
        this.guardandoCuenta.set(false);
        this.mostrarPago.set(false);
        this.errorCuenta.set('');
        // El pago ya quedó registrado: el saldo se toma del estado de cuenta recalculado por el backend.
        this.cargarCuenta(cuenta => this.mensajeCuenta.set(`Se registró el pago. ${this.textoDeSaldo(cuenta.balance)}`));
      },
      error: error => { this.guardandoCuenta.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo registrar el pago.')); }
    });
  }

  pedirAnulacion(movimiento: AccountMovementDto): void {
    this.errorFormulario.set('');
    this.mensajeCuenta.set('');
    this.motivoAnulacion.reset('');
    this.cargoPorAnular.set(movimiento);
  }

  anularCargo(): void {
    const cargo = this.cargoPorAnular();
    if (!cargo) return;
    if (this.motivoAnulacion.invalid || !this.motivoAnulacion.value.trim()) { this.motivoAnulacion.markAsTouched(); this.errorFormulario.set('Indicá el motivo de la anulación.'); return; }
    this.guardandoCuenta.set(true);
    this.errorFormulario.set('');
    this.api.anularCargo(this.pacienteId, cargo.id, { reason: this.motivoAnulacion.value.trim() }).subscribe({
      next: cuenta => {
        this.guardandoCuenta.set(false);
        this.cargoPorAnular.set(null);
        this.errorCuenta.set('');
        this.cuenta.set(cuenta);
        this.mensajeCuenta.set('Se anuló el cargo.');
      },
      error: error => {
        this.guardandoCuenta.set(false);
        // 409 (otra sesión lo anuló al mismo tiempo) o cargo ya anulado: el aviso queda a la vista y se recarga la cuenta.
        if (error instanceof HttpErrorResponse && (error.status === 409 || codigoDeError(error) === 'charge_already_voided')) {
          this.cargoPorAnular.set(null);
          this.errorCuenta.set(mensajeDeError(error, 'No se pudo anular el cargo.'));
          this.cargarCuenta();
          return;
        }
        this.errorFormulario.set(mensajeDeError(error, 'No se pudo anular el cargo.'));
      }
    });
  }
}
