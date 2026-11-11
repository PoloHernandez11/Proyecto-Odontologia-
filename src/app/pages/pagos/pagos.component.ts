import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConceptoCobro, MetodoPago, Pago } from '../../models/pago.model';
import { PagoService } from '../../services/pago.service';
import { PacientesService } from '../../services/paciente.service';
import { Paciente } from '../../models/paciente.model';
 
@Component({
  selector: 'app-pagos', standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './pagos.component.html', styleUrl: './pagos.component.css'
})
export class PagosComponent {
  conceptos: ConceptoCobro[] = [];
  pagos: Pago[] = [];
  pacientes: Paciente[] = [];
  mostrarConcepto = false;
  formularioConcepto = new FormGroup({ pacienteId: new FormControl<number | null>(null, Validators.required), descripcion: new FormControl('', [Validators.required, Validators.minLength(3)]), importeTotal: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]) });
  busquedaConceptos = '';
  busquedaPagos = '';
  filtroEstado = 'Todos';
  filtroMetodo = 'Todos';
  mostrarFormulario = false;
  mensaje = '';
  error = '';
  readonly metodos: MetodoPago[] = ['Efectivo', 'Transferencia', 'Tarjeta de débito', 'Tarjeta de crédito'];
  formulario = new FormGroup({
    conceptoId: new FormControl<number | null>(null, Validators.required),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    metodo: new FormControl<MetodoPago>('Transferencia', Validators.required),
    cuotas: new FormControl('1', Validators.required),
    cuotasPersonalizadas: new FormControl<number | null>(null),
    comprobante: new FormControl(''),
    observaciones: new FormControl('')
  });
 
  constructor(private pagoService: PagoService, private pacientesService: PacientesService) {
    this.pacientesService.obtenerTodos().subscribe(pacientes => this.pacientes = pacientes);
    this.actualizarDatos();
  }
 
  get conceptosFiltrados(): ConceptoCobro[] {
    const texto = this.busquedaConceptos.trim().toLowerCase();
    return this.conceptos.filter(c => {
      const paciente = this.pacientes.find(p => p.id === c.pacienteId);
      const dni = paciente?.dni || '';
      return `${c.pacienteNombre} ${c.descripcion} ${dni}`.toLowerCase().includes(texto) && (this.filtroEstado === 'Todos' || c.estado === this.filtroEstado);
    });
  }
  get pagosFiltrados(): Pago[] {
    const texto = this.busquedaPagos.trim().toLowerCase();
    return this.pagos.filter(p => {
      const paciente = this.pacientes.find(item => item.id === p.pacienteId);
      return `${p.pacienteNombre} ${p.conceptoDescripcion} ${p.comprobante} ${paciente?.dni || ''}`.toLowerCase().includes(texto) && (this.filtroMetodo === 'Todos' || p.metodo === this.filtroMetodo);
    });
  }
  get totalCobrado(): number { return this.pagos.filter(p => !p.anulado).reduce((s, p) => s + p.importe, 0); }
  get saldoPendiente(): number { return this.conceptos.reduce((s, c) => s + this.pagoService.saldo(c.id), 0); }
  get pacientesConSaldo(): number { return new Set(this.conceptos.filter(c => this.pagoService.saldo(c.id) > 0).map(c => c.pacienteId)).size; }
  get cobradoHoy(): number { const hoy = new Date(); const fecha = new Date(hoy.getTime() - hoy.getTimezoneOffset() * 60000).toISOString().slice(0, 10); return this.pagos.filter(p => !p.anulado && p.fecha === fecha).reduce((s, p) => s + p.importe, 0); }
  get conceptoSeleccionado(): ConceptoCobro | undefined { const id = this.formulario.controls.conceptoId.value; return id ? this.conceptos.find(c => c.id === id) : undefined; }
  get saldoSeleccionado(): number { return this.conceptoSeleccionado ? this.pagoService.saldo(this.conceptoSeleccionado.id) : 0; }
  get esTarjetaCredito(): boolean { return this.formulario.controls.metodo.value === 'Tarjeta de crédito'; }
  get cuotasCalculadas(): number { const opcion = this.formulario.controls.cuotas.value; return opcion === 'personalizadas' ? Number(this.formulario.controls.cuotasPersonalizadas.value || 0) : Number(opcion || 1); }
  get importeCuotaVista(): number { return Number(this.formulario.controls.importe.value || 0) / Math.max(1, this.cuotasCalculadas); }
  conceptosDePaciente(pacienteId: number): ConceptoCobro[] { return this.conceptos.filter(c => c.pacienteId === pacienteId); }
  saldoPaciente(pacienteId: number): number { return this.conceptosDePaciente(pacienteId).reduce((total, c) => total + this.pagoService.saldo(c.id), 0); }
  abrirConcepto(pacienteId: number): void { this.formularioConcepto.reset({ pacienteId, descripcion: '', importeTotal: null }); this.mostrarConcepto = true; this.error = ''; }
  cerrarConcepto(): void { this.mostrarConcepto = false; }
  guardarConcepto(): void { if (this.formularioConcepto.invalid) { this.formularioConcepto.markAllAsTouched(); return; } const datos = this.formularioConcepto.getRawValue(); const paciente = this.pacientes.find(p => p.id === datos.pacienteId); if (!paciente) return; const resultado = this.pagoService.agregarConcepto(paciente.id, `${paciente.nombre} ${paciente.apellido}`, datos.descripcion || '', Number(datos.importeTotal)); if (!resultado.ok) { this.error = resultado.mensaje; return; } this.mensaje = resultado.mensaje; this.actualizarDatos(); this.mostrarConcepto = false; }
 
  abrirFormulario(conceptoId?: number): void {
    this.error = ''; this.mensaje = '';
    this.formulario.reset({ conceptoId: conceptoId ?? null, importe: null, metodo: 'Transferencia', cuotas: '1', cuotasPersonalizadas: null, comprobante: '', observaciones: '' });
    this.mostrarFormulario = true;
  }
  cerrarFormulario(): void { this.mostrarFormulario = false; }
  registrarPago(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    if (this.esTarjetaCredito && (!Number.isInteger(this.cuotasCalculadas) || this.cuotasCalculadas < 1 || this.cuotasCalculadas > 24)) { this.error = 'Ingresá una cantidad de cuotas entre 1 y 24.'; return; }
    const resultado = this.pagoService.registrar(datos.conceptoId!, Number(datos.importe), datos.metodo!, datos.comprobante || '', datos.observaciones || '', this.esTarjetaCredito ? this.cuotasCalculadas : 1);
    if (!resultado.ok) { this.error = resultado.mensaje; return; }
    this.mensaje = resultado.mensaje; this.actualizarDatos(); this.mostrarFormulario = false;
  }
  private actualizarDatos(): void {
    this.conceptos = this.pagoService.obtenerConceptos();
    this.pagos = this.pagoService.obtenerPagos();
  }
  saldo(concepto: ConceptoCobro): number { return this.pagoService.saldo(concepto.id); }
  pagado(concepto: ConceptoCobro): number { return this.pagoService.totalPagado(concepto.id); }
  porcentaje(concepto: ConceptoCobro): number { return concepto.importeTotal ? Math.min(100, this.pagado(concepto) / concepto.importeTotal * 100) : 0; }
  formatoPesos(valor: number): string { return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(valor); }
  fechaLegible(fecha: string): string { return this.pagoService.formatoFecha(fecha); }
  claseEstado(estado: string): string { return estado.toLowerCase().replaceAll(' ', '-'); }
  nombrePaciente(id: number): string { const p = this.pacientes.find(item => item.id === id); return p ? `${p.nombre} ${p.apellido}` : 'Paciente'; }
}
 