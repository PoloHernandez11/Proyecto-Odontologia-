import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CategoriaFinanciera, MovimientoVista, TipoMovimiento } from '../../models/finanza.model';
import { TrabajoLaboratorio } from '../../models/trabajo-laboratorio.model';
import { FinanzasService } from '../../services/finanzas.service';
import { LaboratorioService } from '../../services/laboratorio.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-finanzas', standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './finanzas.component.html', styleUrl: './finanzas.component.css'
})
export class FinanzasComponent {
  periodo = '2026-10';
  movimientos: MovimientoVista[] = [];
  pendientesPacientes: ReturnType<FinanzasService['obtenerPendientesPacientes']> = [];
  pendientesLaboratorios: ReturnType<FinanzasService['obtenerPendientesLaboratorios']> = [];
  trabajos: TrabajoLaboratorio[] = [];
  tipoFiltro: 'Todos' | TipoMovimiento = 'Todos';
  mostrarFormulario = false;
  mostrarPagoLaboratorio = false;
  mensaje = '';
  error = '';
  readonly categorias: CategoriaFinanciera[];
  formulario = new FormGroup({
    tipo: new FormControl<TipoMovimiento>('Egreso', Validators.required),
    fecha: new FormControl(this.hoy(), Validators.required),
    concepto: new FormControl('', [Validators.required, Validators.minLength(3)]),
    categoria: new FormControl<CategoriaFinanciera>('Insumos', Validators.required),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    metodo: new FormControl('Transferencia', Validators.required)
  });
  formularioLaboratorio = new FormGroup({
    trabajoId: new FormControl<number | null>(null, Validators.required),
    fecha: new FormControl(this.hoy(), Validators.required),
    importe: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    metodo: new FormControl('Transferencia', Validators.required)
  });

  constructor(private finanzas: FinanzasService, 
    private laboratorio: LaboratorioService,
    private authService : AuthService,
    private router: Router)
  {
    this.categorias = this.finanzas.categorias();
    this.trabajos = this.laboratorio.obtenerTodos();
    this.actualizar();
  }
  ngOnInit(): void{
    if(!this.authService.puedeVerFinanzas()){
      alert('Acceso Restringido: El modulo de Finanzas es exclusivo del Odontologo')
      this.router.navigate(['/agenda'])
    }
  }

  private hoy(): string { const d = new Date(); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10); }
  private rango(): { desde: string; hasta: string } {
    const [anio, mes] = this.periodo.split('-').map(Number);
    const ultimoDia = new Date(anio, mes, 0).getDate();
    return { desde: `${this.periodo}-01`, hasta: `${this.periodo}-${String(ultimoDia).padStart(2, '0')}` };
  }
  get movimientosFiltrados(): MovimientoVista[] { return this.tipoFiltro === 'Todos' ? this.movimientos : this.movimientos.filter(m => m.tipo === this.tipoFiltro); }
  get ingresos(): number { return this.movimientos.filter(m => m.tipo === 'Ingreso' && !m.anulado).reduce((s, m) => s + m.importe, 0); }
  get egresos(): number { return this.movimientos.filter(m => m.tipo === 'Egreso' && !m.anulado).reduce((s, m) => s + m.importe, 0); }
  get balance(): number { return this.ingresos - this.egresos; }
  get totalPendientePacientes(): number { return this.pendientesPacientes.reduce((s, p) => s + p.saldo, 0); }
  get totalPendienteLaboratorios(): number { return this.pendientesLaboratorios.reduce((s, p) => s + p.saldo, 0); }
  get cobradoPacientes(): number { return this.movimientos.filter(m => m.tipo === 'Ingreso' && m.origen === 'Pagos de pacientes' && !m.anulado).reduce((s, m) => s + m.importe, 0); }
  get pagadoLaboratorios(): number { return this.movimientos.filter(m => m.tipo === 'Egreso' && m.origen === 'Laboratorio dental' && !m.anulado).reduce((s, m) => s + m.importe, 0); }

  actualizar(): void {
    const r = this.rango();
    this.movimientos = this.finanzas.movimientosDelPeriodo(r.desde, r.hasta) as MovimientoVista[];
    this.pendientesPacientes = this.finanzas.obtenerPendientesPacientes();
    this.pendientesLaboratorios = this.finanzas.obtenerPendientesLaboratorios();
    this.trabajos = this.laboratorio.obtenerTodos();
  }
  abrirRegistro(tipo: TipoMovimiento): void {
    this.error = ''; this.mensaje = '';
    this.formulario.reset({ tipo, fecha: this.hoy(), concepto: '', categoria: tipo === 'Ingreso' ? 'Servicios' : 'Insumos', importe: null, metodo: 'Transferencia' });
    this.mostrarFormulario = true;
    this.mostrarPagoLaboratorio = false;
  }
  guardarMovimiento(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const v = this.formulario.getRawValue();
    const r = this.finanzas.registrar(v.tipo!, v.fecha!, v.concepto!, v.categoria!, Number(v.importe), v.metodo!);
    if (!r.ok) { this.error = r.mensaje; return; }
    this.mensaje = r.mensaje; this.mostrarFormulario = false; this.actualizar();
  }
  abrirPagoLaboratorio(trabajoId?: number): void {
    this.error = ''; this.mensaje = '';
    this.formularioLaboratorio.reset({ trabajoId: trabajoId ?? null, fecha: this.hoy(), importe: null, metodo: 'Transferencia' });
    this.mostrarPagoLaboratorio = true; this.mostrarFormulario = false;
  }
  guardarPagoLaboratorio(): void {
    if (this.formularioLaboratorio.invalid) { this.formularioLaboratorio.markAllAsTouched(); return; }
    const v = this.formularioLaboratorio.getRawValue();
    const r = this.finanzas.registrarPagoLaboratorio(v.trabajoId!, Number(v.importe), v.fecha!, v.metodo!);
    if (!r.ok) { this.error = r.mensaje; return; }
    this.mensaje = r.mensaje; this.mostrarPagoLaboratorio = false; this.actualizar();
  }
  saldoTrabajo(id: number): number { return this.finanzas.saldoLaboratorio(id); }
  pesos(n: number): string { return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n); }
  fecha(f: string): string { return new Date(`${f}T00:00:00`).toLocaleDateString('es-AR'); }
}
