import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ConceptoCobro, MetodoPago, Pago } from '../models/pago.model';

// Servicio central: calcula saldos y registra cobros parciales sin duplicar deudas.
@Injectable({ providedIn: 'root' })
export class PagoService {
  private readonly clave = 'odontia-pagos-pacientes';
  private readonly claveConceptos = 'odontia-conceptos-cobro';
  private navegador: boolean;
  private conceptos: ConceptoCobro[] = [];
  private pagos: Pago[] = [];

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.navegador = isPlatformBrowser(platformId);
    this.cargar();
  }

  private hoy(): string {
    const fecha = new Date();
    return new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  }

  private cargar(): void {
    if (this.navegador) {
      try {
        const conceptos = localStorage.getItem(this.claveConceptos);
        const pagos = localStorage.getItem(this.clave);
        if (conceptos && pagos) {
          this.conceptos = JSON.parse(conceptos) as ConceptoCobro[];
          this.pagos = JSON.parse(pagos) as Pago[];
          return;
        }
      } catch {
        localStorage.removeItem(this.claveConceptos);
        localStorage.removeItem(this.clave);
      }
    }

    // Datos ficticios basados en los ejemplos de la pantalla de Figma.
    this.conceptos = [
      { id: 1, pacienteId: 1, pacienteNombre: 'Martín Rodríguez', descripcion: 'Endodoncia pieza 36', fecha: '2026-10-12', importeTotal: 185000, estado: 'Pago parcial' },
      { id: 2, pacienteId: 3, pacienteNombre: 'Jorge Castro', descripcion: 'Corona de zirconio pieza 21', fecha: '2026-10-19', importeTotal: 160000, estado: 'Pago parcial' },
      { id: 3, pacienteId: 4, pacienteNombre: 'Lucía Fernández', descripcion: 'Limpieza y flúor', fecha: '2026-10-19', importeTotal: 36000, estado: 'Pagado' },
      { id: 4, pacienteId: 2, pacienteNombre: 'Sofía Herrera', descripcion: 'Control semestral', fecha: '2026-10-19', importeTotal: 30000, estado: 'Pagado' },
      { id: 5, pacienteId: 7, pacienteNombre: 'Paula Giménez', descripcion: 'Obturación piezas 14 y 15', fecha: '2026-10-16', importeTotal: 95000, estado: 'Pendiente' },
      { id: 6, pacienteId: 6, pacienteNombre: 'Diego Romero', descripcion: 'Consulta y radiografía', fecha: '2026-10-15', importeTotal: 25000, estado: 'Pagado' },
      { id: 7, pacienteId: 8, pacienteNombre: 'Ricardo Sosa', descripcion: 'Evaluación implante 46', fecha: '2026-10-14', importeTotal: 45000, estado: 'Pagado' }
    ];
    this.pagos = [
      this.crearDemo(1, 1, 60000, 'Transferencia', 'REC-00412', 'Anticipo'),
      this.crearDemo(2, 1, 40000, 'Tarjeta de crédito', 'REC-00458', 'Segundo pago'),
      this.crearDemo(3, 2, 80000, 'Transferencia', 'REC-00457', ''),
      this.crearDemo(4, 3, 36000, 'Tarjeta de débito', 'REC-00456', ''),
      this.crearDemo(5, 4, 30000, 'Efectivo', 'REC-00455', ''),
      this.crearDemo(6, 6, 25000, 'Efectivo', 'REC-00431', '')
    ];
    this.sincronizarEstados();
    this.guardar();
  }

  private crearDemo(id: number, conceptoId: number, importe: number, metodo: MetodoPago, comprobante: string, observaciones: string): Pago {
    const concepto = this.conceptos.find(c => c.id === conceptoId)!;
    return { id, conceptoId, pacienteId: concepto.pacienteId, pacienteNombre: concepto.pacienteNombre, conceptoDescripcion: concepto.descripcion, fecha: concepto.fecha, importe, metodo, comprobante, observaciones, anulado: false };
  }

  private guardar(): void {
    if (!this.navegador) return;
    localStorage.setItem(this.claveConceptos, JSON.stringify(this.conceptos));
    localStorage.setItem(this.clave, JSON.stringify(this.pagos));
  }

  obtenerConceptos(): ConceptoCobro[] { return this.conceptos.map(c => ({ ...c })); }
  obtenerPagos(): Pago[] { return this.pagos.map(p => ({ ...p })); }
  obtenerConcepto(id: number): ConceptoCobro | undefined { return this.conceptos.find(c => c.id === id); }
  totalPagado(conceptoId: number): number { return this.pagos.filter(p => p.conceptoId === conceptoId && !p.anulado).reduce((suma, p) => suma + p.importe, 0); }
  saldo(conceptoId: number): number { const c = this.obtenerConcepto(conceptoId); return c ? Math.max(0, c.importeTotal - this.totalPagado(conceptoId)) : 0; }

  agregarConcepto(pacienteId: number, pacienteNombre: string, descripcion: string, importeTotal: number, fecha = this.hoy()): { ok: boolean; mensaje: string } {
    if (!Number.isInteger(pacienteId) || !pacienteNombre.trim()) return { ok: false, mensaje: 'Seleccioná un paciente.' };
    if (!descripcion.trim()) return { ok: false, mensaje: 'Ingresá la descripción del concepto.' };
    if (!Number.isFinite(importeTotal) || importeTotal <= 0) return { ok: false, mensaje: 'El importe debe ser mayor a cero.' };
    const id = Math.max(0, ...this.conceptos.map(c => c.id)) + 1;
    this.conceptos.push({ id, pacienteId, pacienteNombre: pacienteNombre.trim(), descripcion: descripcion.trim(), fecha, importeTotal, estado: 'Pendiente' });
    this.guardar();
    return { ok: true, mensaje: `Se agregó el concepto para ${pacienteNombre}.` };
  }

  registrar(conceptoId: number, importe: number, metodo: MetodoPago, comprobante: string, observaciones: string, cuotas = 1): { ok: boolean; mensaje: string } {
    const concepto = this.conceptos.find(c => c.id === conceptoId);
    if (!concepto) return { ok: false, mensaje: 'No se encontró el concepto seleccionado.' };
    if (!Number.isFinite(importe) || importe <= 0) return { ok: false, mensaje: 'El importe debe ser mayor a cero.' };
    const saldo = this.saldo(conceptoId);
    if (importe > saldo) return { ok: false, mensaje: `El importe supera el saldo pendiente (${saldo}).` };
    if (!Number.isInteger(cuotas) || cuotas < 1 || cuotas > 24) return { ok: false, mensaje: 'La cantidad de cuotas debe estar entre 1 y 24.' };
    if (metodo !== 'Tarjeta de crédito') cuotas = 1;
    const id = Math.max(0, ...this.pagos.map(p => p.id)) + 1;
    const codigo = comprobante.trim() || `REC-${String(40000 + id).padStart(5, '0')}`;
    this.pagos.unshift({ id, conceptoId, pacienteId: concepto.pacienteId, pacienteNombre: concepto.pacienteNombre, conceptoDescripcion: concepto.descripcion, fecha: this.hoy(), importe, metodo, comprobante: codigo, observaciones: observaciones.trim(), anulado: false, cuotas, importeCuota: Math.round((importe / cuotas) * 100) / 100 });
    this.sincronizarEstados();
    this.guardar();
    return { ok: true, mensaje: `Cobro registrado correctamente. Comprobante ${codigo}.` };
  }

  private sincronizarEstados(): void {
    this.conceptos.forEach(c => {
      const pagado = this.totalPagado(c.id);
      c.estado = pagado <= 0 ? 'Pendiente' : pagado >= c.importeTotal ? 'Pagado' : 'Pago parcial';
    });
  }

  formatoFecha(fecha: string): string { return new Date(`${fecha}T00:00:00`).toLocaleDateString('es-AR'); }
}
