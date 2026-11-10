import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CategoriaFinanciera, MovimientoFinanciero, TipoMovimiento } from '../models/finanza.model';
import { PagoService } from './pago.service';
import { LaboratorioService } from './laboratorio.service';

@Injectable({ providedIn: 'root' })
export class FinanzasService {
  private readonly clave = 'odontia-movimientos-financieros';
  private readonly navegador: boolean;
  private movimientos: MovimientoFinanciero[] = [];

  constructor(
    @Inject(PLATFORM_ID) platformId: object,
    private pagoService: PagoService,
    private laboratorioService: LaboratorioService
  ) {
    this.navegador = isPlatformBrowser(platformId);
    this.cargar();
  }

  private cargar(): void {
    if (this.navegador) {
      try {
        const guardados = localStorage.getItem(this.clave);
        if (guardados) {
          this.movimientos = JSON.parse(guardados) as MovimientoFinanciero[];
          return;
        }
      } catch {
        localStorage.removeItem(this.clave);
      }
    }
    // Gastos ficticios para mostrar el libro financiero. Dos egresos están vinculados
    // a trabajos reales del LaboratorioService y reducen su saldo pendiente.
    this.movimientos = [
      { id: 1, tipo: 'Egreso', fecha: '2026-10-19', concepto: 'Pago parcial · Dental Sur', categoria: 'Laboratorio dental', importe: 40000, metodo: 'Transferencia', origen: 'Laboratorio dental', anulado: false, trabajoLaboratorioId: 1 },
      { id: 2, tipo: 'Egreso', fecha: '2026-10-18', concepto: 'Pago parcial · Lab. Belgrano', categoria: 'Laboratorio dental', importe: 50000, metodo: 'Transferencia', origen: 'Laboratorio dental', anulado: false, trabajoLaboratorioId: 2 },
      { id: 3, tipo: 'Egreso', fecha: '2026-10-17', concepto: 'Compra de guantes y barbijos', categoria: 'Insumos', importe: 38500, metodo: 'Tarjeta de débito', origen: 'Carga manual', anulado: false },
      { id: 4, tipo: 'Egreso', fecha: '2026-10-15', concepto: 'Servicio de limpieza', categoria: 'Otros', importe: 22000, metodo: 'Efectivo', origen: 'Carga manual', anulado: false },
      { id: 5, tipo: 'Egreso', fecha: '2026-10-10', concepto: 'Alquiler del consultorio', categoria: 'Alquiler', importe: 280000, metodo: 'Transferencia', origen: 'Carga manual', anulado: false },
      { id: 6, tipo: 'Egreso', fecha: '2026-10-05', concepto: 'Honorarios equipo auxiliar', categoria: 'Sueldos', importe: 190000, metodo: 'Transferencia', origen: 'Carga manual', anulado: false }
    ];
    this.guardar();
  }

  private guardar(): void {
    if (this.navegador) localStorage.setItem(this.clave, JSON.stringify(this.movimientos));
  }

  obtenerMovimientosManuales(): MovimientoFinanciero[] { return this.movimientos.map(m => ({ ...m })); }

  registrar(tipo: TipoMovimiento, fecha: string, concepto: string, categoria: CategoriaFinanciera, importe: number, metodo: string): { ok: boolean; mensaje: string } {
    if (!fecha || !concepto.trim()) return { ok: false, mensaje: 'Completá la fecha y el concepto.' };
    if (!Number.isFinite(importe) || importe <= 0) return { ok: false, mensaje: 'El importe debe ser mayor a cero.' };
    const id = Math.max(0, ...this.movimientos.map(m => m.id)) + 1;
    this.movimientos.unshift({ id, tipo, fecha, concepto: concepto.trim(), categoria, importe, metodo, origen: 'Carga manual', anulado: false });
    this.guardar();
    return { ok: true, mensaje: `${tipo} registrado correctamente.` };
  }

  registrarPagoLaboratorio(trabajoId: number, importe: number, fecha: string, metodo: string): { ok: boolean; mensaje: string } {
    const trabajo = this.laboratorioService.obtenerTodos().find(t => t.id === trabajoId);
    if (!trabajo) return { ok: false, mensaje: 'No se encontró el trabajo de laboratorio.' };
    if (!Number.isFinite(importe) || importe <= 0) return { ok: false, mensaje: 'El importe debe ser mayor a cero.' };
    const pagado = this.movimientos.filter(m => !m.anulado && m.trabajoLaboratorioId === trabajoId).reduce((s, m) => s + m.importe, 0);
    const saldo = Math.max(0, trabajo.costo - pagado);
    if (importe > saldo) return { ok: false, mensaje: `El pago supera el saldo del trabajo (${saldo}).` };
    const id = Math.max(0, ...this.movimientos.map(m => m.id)) + 1;
    this.movimientos.unshift({ id, tipo: 'Egreso', fecha, concepto: `Pago a ${trabajo.laboratorio} · ${trabajo.tipoTrabajo} (${trabajo.pacienteNombre})`, categoria: 'Laboratorio dental', importe, metodo, origen: 'Laboratorio dental', anulado: false, trabajoLaboratorioId: trabajoId });
    this.guardar();
    return { ok: true, mensaje: 'Pago a laboratorio registrado y vinculado al trabajo.' };
  }

  obtenerPagosLaboratorio(): MovimientoFinanciero[] { return this.movimientos.filter(m => m.trabajoLaboratorioId !== undefined && !m.anulado); }
  saldoLaboratorio(trabajoId: number): number {
    const trabajo = this.laboratorioService.obtenerTodos().find(t => t.id === trabajoId);
    if (!trabajo) return 0;
    const pagado = this.movimientos.filter(m => !m.anulado && m.trabajoLaboratorioId === trabajoId).reduce((s, m) => s + m.importe, 0);
    return Math.max(0, trabajo.costo - pagado);
  }

  // Los cobros se proyectan desde PagoService: no se copian ni duplican en este servicio.
  movimientosDelPeriodo(desde: string, hasta: string) {
    const ingresos = this.pagoService.obtenerPagos()
      .filter(p => !p.anulado && p.fecha >= desde && p.fecha <= hasta)
      .map(p => ({ id: `P-${p.id}`, tipo: 'Ingreso' as TipoMovimiento, fecha: p.fecha, concepto: `Pago · ${p.pacienteNombre} · ${p.conceptoDescripcion}`, categoria: 'Tratamientos', importe: p.importe, metodo: p.metodo, origen: 'Pagos de pacientes', anulado: false }));
    const manuales = this.movimientos
      .filter(m => m.fecha >= desde && m.fecha <= hasta)
      .map(m => ({ id: `M-${m.id}`, tipo: m.tipo, fecha: m.fecha, concepto: m.concepto, categoria: m.categoria, importe: m.importe, metodo: m.metodo, origen: m.origen, anulado: m.anulado }));
    return [...ingresos, ...manuales].sort((a, b) => b.fecha.localeCompare(a.fecha));
  }

  obtenerPendientesPacientes() {
    return this.pagoService.obtenerConceptos().map(c => ({ id: c.id, nombre: c.pacienteNombre, concepto: c.descripcion, total: c.importeTotal, abonado: this.pagoService.totalPagado(c.id), saldo: this.pagoService.saldo(c.id) })).filter(c => c.saldo > 0);
  }

  obtenerPendientesLaboratorios() {
    return this.laboratorioService.obtenerTodos().map(t => ({ id: t.id, laboratorio: t.laboratorio, paciente: t.pacienteNombre, trabajo: t.tipoTrabajo, costo: t.costo, abonado: t.costo - this.saldoLaboratorio(t.id), saldo: this.saldoLaboratorio(t.id) })).filter(t => t.saldo > 0);
  }

  categorias(): CategoriaFinanciera[] { return ['Servicios', 'Insumos', 'Sueldos', 'Alquiler', 'Laboratorio dental', 'Mantenimiento', 'Otros']; }
}
