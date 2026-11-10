import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OdontiaApi } from '../../api/odontia-api.service';
import { BudgetDto, LabWorkDto, PaymentDto, PaymentMethod } from '../../api/schema';
import { fechaCorta, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { METODOS_DE_PAGO } from '../pagos/pagos.component';

/**
 * Resumen económico del consultorio a partir de lo que registra el backend: cobros a pacientes,
 * saldos de presupuestos aprobados y costo de los trabajos de laboratorio en curso.
 */
@Component({
  selector: 'app-finanzas', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './finanzas.component.html', styleUrl: './finanzas.component.css'
})
export class FinanzasComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);

  readonly metodos = METODOS_DE_PAGO;
  readonly veLaboratorio = this.auth.tiene('lab.read');
  periodo = this.mesActual();
  pagos: PaymentDto[] = [];
  presupuestos: BudgetDto[] = [];
  trabajos: LabWorkDto[] = [];
  cargando = true;
  error = '';

  constructor() {
    this.cargarPeriodo();
    this.api.presupuestos({ Status: 'Approved' }).subscribe({ next: pagina => (this.presupuestos = pagina.items) });
    if (this.veLaboratorio) this.api.trabajosDeLaboratorio().subscribe({ next: pagina => (this.trabajos = pagina.items) });
  }

  private mesActual(): string { const hoy = new Date(); return `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}`; }

  cargarPeriodo(): void {
    if (!/^\d{4}-\d{2}$/.test(this.periodo)) return;
    const [anio, mes] = this.periodo.split('-').map(Number);
    const desde = new Date(anio, mes - 1, 1), hasta = new Date(anio, mes, 1);
    this.cargando = true;
    this.api.pagos({ From: desde.toISOString(), To: hasta.toISOString(), IncludeVoided: true }).subscribe({
      next: pagina => { this.cargando = false; this.error = ''; this.pagos = [...pagina.items].sort((a, b) => b.paidAtUtc.localeCompare(a.paidAtUtc)); },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudieron cargar los movimientos del período.'); }
    });
  }

  get tituloDelPeriodo(): string {
    const [anio, mes] = this.periodo.split('-').map(Number);
    return new Intl.DateTimeFormat('es-AR', { month: 'long', year: 'numeric' }).format(new Date(anio, mes - 1, 1));
  }
  private get vigentes(): PaymentDto[] { return this.pagos.filter(pago => pago.status === 'Registered'); }
  get ingresos(): number { return this.vigentes.reduce((suma, pago) => suma + pago.amount, 0); }
  get cantidadDeCobros(): number { return this.vigentes.length; }
  get anulados(): number { return this.pagos.length - this.vigentes.length; }
  get pendientePorCobrar(): number { return this.presupuestos.reduce((suma, presupuesto) => suma + presupuesto.outstanding, 0); }
  get laboratorioEnCurso(): LabWorkDto[] { return this.trabajos.filter(trabajo => !['Placed', 'Cancelled'].includes(trabajo.status)); }
  get costoDeLaboratorio(): number { return this.laboratorioEnCurso.reduce((suma, trabajo) => suma + (trabajo.cost ?? 0), 0); }

  /** Ingresos del período agrupados por método de pago, de mayor a menor. */
  get porMetodo(): { metodo: string; total: number; porcentaje: number }[] {
    const totales = new Map<PaymentMethod, number>();
    for (const pago of this.vigentes) totales.set(pago.method, (totales.get(pago.method) ?? 0) + pago.amount);
    const total = this.ingresos || 1;
    return [...totales.entries()].sort((a, b) => b[1] - a[1])
      .map(([metodo, importe]) => ({ metodo: METODOS_DE_PAGO[metodo], total: importe, porcentaje: Math.round(importe / total * 100) }));
  }

  fecha(valor: string): string { return fechaCorta(valor); }
  pesos(importe: number): string { return pesos(importe); }
}
