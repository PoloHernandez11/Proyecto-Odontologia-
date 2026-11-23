import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { OdontiaApi } from '../../api/odontia-api.service';
import { BudgetDto, BudgetStatus, PatientListItemDto, PaymentDto, PaymentMethod } from '../../api/schema';
import { esConflictoDeConcurrencia, fechaCorta, mensajeDeError, pesos } from '../../core/api';
import { fechaDelConsultorio } from '../../core/zona-horaria';
import { AuthService } from '../../core/auth.service';
import { DialogDirective } from '../../shared/dialog.directive';
import { PluralPipe } from '../../shared/plural.pipe';

export const METODOS_DE_PAGO: Record<PaymentMethod, string> = {
  Cash: 'Efectivo', BankTransfer: 'Transferencia', DebitCard: 'Tarjeta de débito', CreditCard: 'Tarjeta de crédito',
  DigitalWallet: 'Billetera virtual', HealthInsurance: 'Obra social', Check: 'Cheque', Other: 'Otro'
};
const ESTADOS: Record<BudgetStatus, string> = {
  Draft: 'Borrador', Issued: 'Emitido', Approved: 'Aprobado', Rejected: 'Rechazado', Expired: 'Vencido', Cancelled: 'Cancelado'
};

/**
 * Cobros a pacientes. En el backend, lo que se le cobra a un paciente es un presupuesto aprobado;
 * los pagos se imputan a ese presupuesto y el saldo lo calcula siempre el servidor.
 */
@Component({
  selector: 'app-pagos', standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, DialogDirective, PluralPipe],
  templateUrl: './pagos.component.html', changeDetection: ChangeDetectionStrategy.OnPush,
 styleUrl: './pagos.component.css'
})
export class PagosComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly metodos = METODOS_DE_PAGO;
  readonly listaDeMetodos = Object.entries(METODOS_DE_PAGO) as [PaymentMethod, string][];
  readonly estados = ESTADOS;
  get puedeCobrar(): boolean { return this.auth.tiene('payments.write'); }
  get puedeAnular(): boolean { return this.auth.tiene('payments.void'); }
  get puedeCrearConcepto(): boolean { return this.auth.tiene('budgets.write') && this.auth.tiene('budgets.approve'); }
  get puedeAprobar(): boolean { return this.auth.tiene('budgets.approve'); }
  readonly pacienteId = signal(this.route.snapshot.queryParamMap.get('paciente'));
  readonly nombreDelPaciente = signal('');
  readonly presupuestos = signal<BudgetDto[]>([]);
  readonly pagos = signal<PaymentDto[]>([]);
  readonly pacientes = signal<PatientListItemDto[]>([]);
  readonly cargando = signal(true);
  readonly busqueda = signal('');
  readonly filtroEstado = signal('Todos');
  readonly filtroMetodo = signal('Todos');
  readonly mensaje = signal('');
  readonly error = signal('');

  readonly mostrarConcepto = signal(false);
  readonly mostrarCobro = signal(false);
  readonly pagoPorAnular = signal<PaymentDto | null>(null);
  readonly motivoAnulacion = signal('');
  readonly guardando = signal(false);
  readonly errorFormulario = signal('');

  formularioConcepto = new FormGroup({
    pacienteId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    descripcion: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(250)] }),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(1)])
  });
  formularioCobro = new FormGroup({
    presupuestoId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(0.01)]),
    metodo: new FormControl<PaymentMethod>('BankTransfer', { nonNullable: true }),
    comprobante: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] })
  });

  constructor() {
    this.cargar();
    const pacienteId = this.pacienteId();
    if (pacienteId) {
      this.api.paciente(pacienteId).subscribe({ next: paciente => (this.nombreDelPaciente.set(`${paciente.firstName} ${paciente.lastName}`)) });
    }
  }

  /** @param limpiarError false para conservar el aviso de un conflicto mientras se recargan los datos. */
  cargar(limpiarError = true): void {
    this.cargando.set(true);
    this.api.presupuestos({ PatientId: this.pacienteId() }).subscribe({
      next: pagina => { this.cargando.set(false); if (limpiarError) this.error.set(''); this.presupuestos.set([...pagina.items].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))); },
      error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudieron cargar los conceptos de cobro.')); }
    });
    this.api.pagos({ PatientId: this.pacienteId(), IncludeVoided: true }).subscribe({
      next: pagina => (this.pagos.set([...pagina.items].sort((a, b) => b.paidAtUtc.localeCompare(a.paidAtUtc)))),
      error: error => (this.error.set(mensajeDeError(error, 'No se pudieron cargar los pagos.')))
    });
  }

  // ── Totales (los saldos vienen calculados por el backend) ──────────────────
  private get vigentes(): PaymentDto[] { return this.pagos().filter(pago => pago.status === 'Registered'); }
  /** Cobrado en el día de hoy del consultorio (en su zona horaria). */
  get cobradoHoy(): number { const hoy = fechaDelConsultorio(); return this.vigentes.filter(p => fechaDelConsultorio(p.paidAtUtc) === hoy).reduce((s, p) => s + p.amount, 0); }
  get totalCobrado(): number { return this.vigentes.reduce((suma, pago) => suma + pago.amount, 0); }
  get aprobados(): BudgetDto[] { return this.presupuestos().filter(presupuesto => presupuesto.status === 'Approved'); }
  get saldoPendiente(): number { return this.aprobados.reduce((suma, presupuesto) => suma + presupuesto.outstanding, 0); }
  get pacientesConSaldo(): number { return new Set(this.aprobados.filter(p => p.outstanding > 0).map(p => p.patientId)).size; }
  get cobrables(): BudgetDto[] { return this.aprobados.filter(presupuesto => presupuesto.outstanding > 0); }

  get presupuestosFiltrados(): BudgetDto[] {
    const texto = this.busqueda().trim().toLowerCase();
    return this.presupuestos().filter(presupuesto => {
      const coincideTexto = `${presupuesto.patientName} ${this.descripcion(presupuesto)} ${presupuesto.number}`.toLowerCase().includes(texto);
      const estado = this.situacion(presupuesto);
      return coincideTexto && (this.filtroEstado() === 'Todos' || estado.titulo === this.filtroEstado());
    });
  }
  get pagosFiltrados(): PaymentDto[] {
    const texto = this.busqueda().trim().toLowerCase();
    return this.pagos().filter(pago =>
      `${pago.patientName} ${pago.reference ?? ''} ${pago.receiptNumber}`.toLowerCase().includes(texto) &&
      (this.filtroMetodo() === 'Todos' || pago.method === this.filtroMetodo()));
  }

  descripcion(presupuesto: BudgetDto): string {
    const lineas = presupuesto.items.map(item => item.description);
    return lineas.length > 2 ? `${lineas.slice(0, 2).join(' · ')} y ${lineas.length - 2} más` : lineas.join(' · ') || `Presupuesto N.º ${presupuesto.number}`;
  }
  situacion(presupuesto: BudgetDto): { titulo: string; clase: string } {
    if (presupuesto.status !== 'Approved') return { titulo: ESTADOS[presupuesto.status], clase: presupuesto.status === 'Draft' || presupuesto.status === 'Issued' ? 'info' : '' };
    if (presupuesto.outstanding <= 0) return { titulo: 'Pagado', clase: 'ok' };
    return presupuesto.paid > 0 ? { titulo: 'Pago parcial', clase: 'warn' } : { titulo: 'Pendiente', clase: 'bad' };
  }
  get presupuestoElegido(): BudgetDto | undefined { return this.presupuestos().find(p => p.id === this.formularioCobro.controls.presupuestoId.value); }
  fecha(valor: string): string { return fechaCorta(valor); }
  pesos(importe: number): string { return pesos(importe); }

  // ── Nuevo concepto de cobro (presupuesto de una línea, emitido y aprobado) ─
  abrirConcepto(): void {
    this.errorFormulario.set('');
    this.formularioConcepto.reset({ pacienteId: this.pacienteId() ?? '', descripcion: '', importe: null });
    this.mostrarConcepto.set(true);
    if (!this.pacientes().length) this.api.pacientes({ pageSize: 100 }).subscribe({ next: pagina => (this.pacientes.set(pagina.items)) });
  }

  guardarConcepto(): void {
    if (this.formularioConcepto.invalid) { this.formularioConcepto.markAllAsTouched(); return; }
    const datos = this.formularioConcepto.getRawValue();
    this.guardando.set(true);
    this.errorFormulario.set('');
    this.api.crearPresupuesto(datos.pacienteId, {
      discountPercent: 0, importPlanItems: false,
      items: [{ description: datos.descripcion.trim(), quantity: 1, unitPrice: datos.importe, discountPercent: 0 }]
    }).pipe(
      switchMap(presupuesto => this.api.emitirPresupuesto(presupuesto.id)),
      switchMap(presupuesto => this.api.aprobarPresupuesto(presupuesto.id))
    ).subscribe({
      next: presupuesto => { this.guardando.set(false); this.mostrarConcepto.set(false); this.mensaje.set(`Se registró el concepto por ${pesos(presupuesto.total)} para ${presupuesto.patientName}.`); this.cargar(); },
      error: error => { this.guardando.set(false); this.errorFormulario.set(mensajeDeError(error, 'No se pudo registrar el concepto.')); this.cargar(); }
    });
  }

  aprobar(presupuesto: BudgetDto): void {
    const paso = presupuesto.status === 'Draft'
      ? this.api.emitirPresupuesto(presupuesto.id).pipe(switchMap(emitido => this.api.aprobarPresupuesto(emitido.id)))
      : this.api.aprobarPresupuesto(presupuesto.id);
    paso.subscribe({
      next: () => { this.mensaje.set(`El presupuesto N.º ${presupuesto.number} quedó aprobado y ya se puede cobrar.`); this.cargar(); },
      error: error => { this.error.set(mensajeDeError(error, 'No se pudo aprobar el presupuesto.')); if (esConflictoDeConcurrencia(error)) this.cargar(false); }
    });
  }

  // ── Registrar cobro ────────────────────────────────────────────────────────
  abrirCobro(presupuesto?: BudgetDto): void {
    this.errorFormulario.set('');
    this.mensaje.set('');
    this.formularioCobro.reset({ presupuestoId: presupuesto?.id ?? '', importe: presupuesto?.outstanding ?? null, metodo: 'BankTransfer', comprobante: '', observaciones: '' });
    this.mostrarCobro.set(true);
  }
  alElegirPresupuesto(): void { this.formularioCobro.controls.importe.setValue(this.presupuestoElegido?.outstanding ?? null); }

  registrarPago(): void {
    const presupuesto = this.presupuestoElegido;
    if (this.formularioCobro.invalid || !presupuesto) { this.formularioCobro.markAllAsTouched(); return; }
    const datos = this.formularioCobro.getRawValue();
    if (Number(datos.importe) > presupuesto.outstanding) { this.errorFormulario.set(`El importe no puede superar el saldo pendiente (${pesos(presupuesto.outstanding)}).`); return; }
    this.guardando.set(true);
    this.errorFormulario.set('');
    this.api.registrarPago({
      patientId: presupuesto.patientId, budgetId: presupuesto.id, amount: Number(datos.importe), method: datos.metodo,
      reference: datos.comprobante.trim() || null, notes: datos.observaciones.trim() || null
    }).subscribe({
      next: pago => { this.guardando.set(false); this.mostrarCobro.set(false); this.mensaje.set(`Se registró el cobro de ${pesos(pago.amount)} a ${pago.patientName} (recibo N.º ${pago.receiptNumber}).`); this.cargar(); },
      error: error => { this.guardando.set(false); this.alFallarEnFormulario(error, 'No se pudo registrar el cobro.', () => (this.mostrarCobro.set(false))); }
    });
  }

  // ── Anulación ──────────────────────────────────────────────────────────────
  pedirAnulacion(pago: PaymentDto): void { this.pagoPorAnular.set(pago); this.motivoAnulacion.set(''); this.errorFormulario.set(''); }
  anular(): void {
    const pagoPorAnular = this.pagoPorAnular();
    if (!pagoPorAnular) return;
    if (this.motivoAnulacion().trim().length < 3) { this.errorFormulario.set('Indicá el motivo de la anulación.'); return; }
    this.guardando.set(true);
    this.api.anularPago(pagoPorAnular.id, this.motivoAnulacion().trim()).subscribe({
      next: pago => { this.guardando.set(false); this.pagoPorAnular.set(null); this.mensaje.set(`Se anuló el recibo N.º ${pago.receiptNumber}.`); this.cargar(); },
      error: error => { this.guardando.set(false); this.alFallarEnFormulario(error, 'No se pudo anular el pago.', () => (this.pagoPorAnular.set(null))); }
    });
  }

  /** Ante un conflicto de concurrencia el dato cambió: se cierra el diálogo, se muestra el aviso y se recarga. */
  private alFallarEnFormulario(error: unknown, porDefecto: string, cerrar: () => void): void {
    if (!esConflictoDeConcurrencia(error)) { this.errorFormulario.set(mensajeDeError(error, porDefecto)); return; }
    cerrar();
    this.mensaje.set('');
    this.error.set(mensajeDeError(error, porDefecto));
    this.cargar(false);
  }
}
