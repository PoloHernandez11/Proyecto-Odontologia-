import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OdontiaApi } from '../../api/odontia-api.service';
import { BudgetDto, ExpenseCategory, ExpenseDto, FinanceSummaryDto, LabWorkDto, LaboratoryDto, PaymentDto, PaymentMethod } from '../../api/schema';
import { fechaCorta, fechaIso, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { METODOS_DE_PAGO } from '../pagos/pagos.component';

const CATEGORIAS: Record<ExpenseCategory, string> = {
  Supplies: 'Insumos', Laboratory: 'Laboratorio dental', Salaries: 'Sueldos y honorarios', Rent: 'Alquiler', Services: 'Servicios',
  Taxes: 'Impuestos', Equipment: 'Equipamiento', Maintenance: 'Mantenimiento', Other: 'Otros'
};

/** Fila del libro de movimientos: un cobro a un paciente o un egreso del consultorio. */
interface Movimiento {
  id: string; tipo: 'Ingreso' | 'Egreso'; fecha: string; concepto: string; detalle: string; metodo: PaymentMethod;
  importe: number; anulado: boolean; motivo: string | null; egreso: ExpenseDto | null;
}

/**
 * Resumen económico del consultorio. Los totales del período (ingresos, egresos y balance neto) los calcula el backend;
 * el libro combina los cobros a pacientes con los egresos registrados.
 */
@Component({
  selector: 'app-finanzas', standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './finanzas.component.html', styleUrl: './finanzas.component.css',
  changeDetection: ChangeDetectionStrategy.Eager
})
export class FinanzasComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);

  readonly metodos = METODOS_DE_PAGO;
  readonly listaDeMetodos = Object.entries(METODOS_DE_PAGO) as [PaymentMethod, string][];
  readonly categorias = CATEGORIAS;
  readonly listaDeCategorias = Object.entries(CATEGORIAS) as [ExpenseCategory, string][];
  readonly veLaboratorio = this.auth.tiene('lab.read');
  readonly puedeRegistrar = this.auth.tiene('payments.write');
  readonly puedeAnular = this.auth.tiene('payments.void');

  periodo = this.mesActual();
  resumen: FinanceSummaryDto | null = null;
  pagos: PaymentDto[] = [];
  egresos: ExpenseDto[] = [];
  presupuestos: BudgetDto[] = [];
  trabajos: LabWorkDto[] = [];
  laboratorios: LaboratoryDto[] = [];
  filtro: 'Todos' | 'Ingreso' | 'Egreso' = 'Todos';
  cargando = true;
  mensaje = '';
  error = '';

  mostrarFormulario = false;
  egresoPorAnular: ExpenseDto | null = null;
  motivoAnulacion = '';
  guardando = false;
  errorFormulario = '';
  readonly hoy = fechaIso(new Date());
  formulario = new FormGroup({
    concepto: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(250)] }),
    categoria: new FormControl<ExpenseCategory>('Supplies', { nonNullable: true }),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(0.01)]),
    metodo: new FormControl<PaymentMethod>('BankTransfer', { nonNullable: true }),
    fecha: new FormControl(this.hoy, { nonNullable: true, validators: [Validators.required] }),
    proveedor: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(150)] }),
    laboratorio: new FormControl('', { nonNullable: true }),
    comprobante: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] })
  });

  constructor() {
    this.cargarPeriodo();
    this.api.presupuestos({ Status: 'Approved' }).subscribe({ next: pagina => (this.presupuestos = pagina.items) });
    if (this.veLaboratorio) {
      this.api.trabajosDeLaboratorio().subscribe({ next: pagina => (this.trabajos = pagina.items) });
      this.api.laboratorios().subscribe({ next: laboratorios => (this.laboratorios = laboratorios.filter(l => l.isActive)) });
    }
  }

  private mesActual(): string { const hoy = new Date(); return `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}`; }
  private get rango(): { desde: Date; hasta: Date } {
    const [anio, mes] = this.periodo.split('-').map(Number);
    return { desde: new Date(anio, mes - 1, 1), hasta: new Date(anio, mes, 1) };
  }

  cargarPeriodo(): void {
    if (!/^\d{4}-\d{2}$/.test(this.periodo)) return;
    const { desde, hasta } = this.rango;
    const filtro = { From: desde.toISOString(), To: hasta.toISOString(), IncludeVoided: true };
    this.cargando = true;
    this.api.resumenFinanciero(desde, hasta).subscribe({
      next: resumen => { this.resumen = resumen; this.cargando = false; this.error = ''; },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar el resumen del período.'); }
    });
    this.api.pagos(filtro).subscribe({ next: pagina => (this.pagos = pagina.items) });
    this.api.egresos(filtro).subscribe({ next: pagina => (this.egresos = pagina.items) });
  }

  get tituloDelPeriodo(): string {
    return new Intl.DateTimeFormat('es-AR', { month: 'long', year: 'numeric' }).format(this.rango.desde);
  }
  get pendientePorCobrar(): number { return this.presupuestos.reduce((suma, presupuesto) => suma + presupuesto.outstanding, 0); }
  get laboratorioEnCurso(): LabWorkDto[] { return this.trabajos.filter(trabajo => !['Placed', 'Cancelled'].includes(trabajo.status)); }
  get costoDeLaboratorio(): number { return this.laboratorioEnCurso.reduce((suma, trabajo) => suma + (trabajo.cost ?? 0), 0); }

  /** Ingresos del período agrupados por método de pago, de mayor a menor. */
  get porMetodo(): { titulo: string; total: number; porcentaje: number }[] {
    const totales = new Map<PaymentMethod, number>();
    for (const pago of this.pagos) if (pago.status === 'Registered') totales.set(pago.method, (totales.get(pago.method) ?? 0) + pago.amount);
    const total = [...totales.values()].reduce((a, b) => a + b, 0) || 1;
    return [...totales.entries()].sort((a, b) => b[1] - a[1])
      .map(([metodo, importe]) => ({ titulo: METODOS_DE_PAGO[metodo], total: importe, porcentaje: Math.round(importe / total * 100) }));
  }
  /** Egresos del período por categoría, tal como los devuelve el backend. */
  get porCategoria(): { titulo: string; total: number; porcentaje: number }[] {
    const total = this.resumen?.expenses || 1;
    return (this.resumen?.expensesByCategory ?? [])
      .map(fila => ({ titulo: CATEGORIAS[fila.category], total: fila.total, porcentaje: Math.round(fila.total / total * 100) }));
  }

  get movimientos(): Movimiento[] {
    const ingresos: Movimiento[] = this.pagos.map(pago => ({
      id: pago.id, tipo: 'Ingreso', fecha: pago.paidAtUtc, concepto: `Pago · ${pago.patientName}`,
      detalle: `Recibo N.º ${pago.receiptNumber}${pago.budgetNumber ? ' · Presupuesto N.º ' + pago.budgetNumber : ''}`,
      metodo: pago.method, importe: pago.amount, anulado: pago.status === 'Voided', motivo: pago.voidReason ?? null, egreso: null
    }));
    const egresos: Movimiento[] = this.egresos.map(egreso => ({
      id: egreso.id, tipo: 'Egreso', fecha: egreso.paidAtUtc, concepto: egreso.description,
      detalle: [CATEGORIAS[egreso.category], egreso.laboratoryName ?? egreso.supplier, egreso.reference].filter(Boolean).join(' · '),
      metodo: egreso.method, importe: egreso.amount, anulado: egreso.status === 'Voided', motivo: egreso.voidReason ?? null, egreso
    }));
    return [...ingresos, ...egresos]
      .filter(movimiento => this.filtro === 'Todos' || movimiento.tipo === this.filtro)
      .sort((a, b) => b.fecha.localeCompare(a.fecha));
  }

  fecha(valor: string): string { return fechaCorta(valor); }
  pesos(importe: number): string { return pesos(importe); }

  // ── Registrar egreso ───────────────────────────────────────────────────────
  abrirFormulario(): void {
    this.errorFormulario = '';
    this.mensaje = '';
    this.formulario.reset({ concepto: '', categoria: 'Supplies', importe: null, metodo: 'BankTransfer', fecha: this.hoy, proveedor: '', laboratorio: '', comprobante: '', observaciones: '' });
    this.mostrarFormulario = true;
  }

  guardarEgreso(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    if (datos.fecha > this.hoy) { this.errorFormulario = 'La fecha del egreso no puede ser futura.'; return; }
    const deLaboratorio = datos.categoria === 'Laboratory' && datos.laboratorio;
    this.guardando = true;
    this.errorFormulario = '';
    this.api.registrarEgreso({
      description: datos.concepto.trim(), category: datos.categoria, amount: Number(datos.importe), method: datos.metodo,
      // Hoy se registra con la hora actual; un día anterior, al mediodía de ese día (hora local).
      paidAt: datos.fecha === this.hoy ? null : new Date(`${datos.fecha}T12:00:00`).toISOString(),
      supplier: datos.proveedor.trim() || null, laboratoryId: deLaboratorio ? datos.laboratorio : null,
      reference: datos.comprobante.trim() || null, notes: datos.observaciones.trim() || null
    }).subscribe({
      next: egreso => { this.guardando = false; this.mostrarFormulario = false; this.mensaje = `Se registró el egreso "${egreso.description}" por ${pesos(egreso.amount)}.`; this.cargarPeriodo(); },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo registrar el egreso.'); }
    });
  }

  // ── Anular egreso ──────────────────────────────────────────────────────────
  pedirAnulacion(egreso: ExpenseDto): void { this.egresoPorAnular = egreso; this.motivoAnulacion = ''; this.errorFormulario = ''; }
  anular(): void {
    if (!this.egresoPorAnular) return;
    if (this.motivoAnulacion.trim().length < 3) { this.errorFormulario = 'Indicá el motivo de la anulación.'; return; }
    this.guardando = true;
    this.api.anularEgreso(this.egresoPorAnular.id, this.motivoAnulacion.trim()).subscribe({
      next: egreso => { this.guardando = false; this.egresoPorAnular = null; this.mensaje = `Se anuló el egreso "${egreso.description}".`; this.cargarPeriodo(); },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo anular el egreso.'); }
    });
  }
}
