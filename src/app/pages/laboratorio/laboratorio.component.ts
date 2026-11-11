import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Paciente } from '../../models/paciente.model';
import { EstadoTrabajo, TrabajoLaboratorio } from '../../models/trabajo-laboratorio.model';
import { LaboratorioService } from '../../services/laboratorio.service';
import { PacientesService } from '../../services/paciente.service';
 
@Component({
  selector: 'app-laboratorio',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './laboratorio.component.html',
  styleUrl: './laboratorio.component.css'
})
export class LaboratorioComponent {
  trabajos: TrabajoLaboratorio[] = [];
  pacientes: Paciente[] = [];
  busqueda = '';
  filtroEstado = 'Todos';
  filtroVista: 'Todos' | 'Activos' | 'Historial' = 'Todos';
  mostrarFormulario = false;
  mensaje = '';
 
  readonly estados: EstadoTrabajo[] = ['Solicitado', 'En proceso', 'Listo para retirar', 'Recibido', 'Entregado al paciente'];
 
  formulario = new FormGroup({
    pacienteId: new FormControl<number | null>(null, Validators.required),
    tipoTrabajo: new FormControl('', [Validators.required, Validators.minLength(3)]),
    piezaDental: new FormControl(''),
    laboratorio: new FormControl('', Validators.required),
    fechaEntrega: new FormControl('', Validators.required),
    costo: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    observaciones: new FormControl('')
  });
 
  constructor(private laboratorioService: LaboratorioService, private pacientesService: PacientesService) {
    this.trabajos = this.laboratorioService.obtenerTodos();
    this.pacientesService.obtenerTodos().subscribe((pacientes: Paciente[]) => {
      this.pacientes = pacientes.filter(p => p.estado === 'Activo');
    });
  }
 
  get trabajosFiltrados(): TrabajoLaboratorio[] {
    const texto = this.busqueda.trim().toLowerCase();
    return this.trabajos.filter(trabajo => {
      const coincideTexto = `${trabajo.pacienteNombre} ${trabajo.tipoTrabajo} ${trabajo.laboratorio}`.toLowerCase().includes(texto);
      const coincideEstado = this.filtroEstado === 'Todos' || trabajo.estado === this.filtroEstado;
      const esHistorial = trabajo.estado === 'Entregado al paciente';
      const coincideVista = this.filtroVista === 'Todos' || (this.filtroVista === 'Historial' ? esHistorial : !esHistorial);
      return coincideTexto && coincideEstado && coincideVista;
    });
  }
 
  get trabajosAtrasados(): number {
    const hoy = this.fechaHoy();
    return this.trabajos.filter(t => t.fechaEntrega < hoy && !['Recibido', 'Entregado al paciente'].includes(t.estado)).length;
  }
 
  get entregasEstaSemana(): number {
    const hoy = new Date();
    const limite = new Date();
    limite.setDate(hoy.getDate() + 7);
    return this.trabajos.filter(t => {
      const fecha = new Date(`${t.fechaEntrega}T00:00:00`);
      return fecha >= new Date(`${this.fechaHoy()}T00:00:00`) && fecha <= limite && !['Recibido', 'Entregado al paciente'].includes(t.estado);
    }).length;
  }
 
  get listosParaRetirar(): number {
    return this.trabajos.filter(t => t.estado === 'Listo para retirar').length;
  }
 
  get activos(): number {
    return this.trabajos.filter(t => t.estado !== 'Entregado al paciente').length;
  }
 
  private fechaHoy(): string {
    const fecha = new Date();
    const zona = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
    return zona.toISOString().slice(0, 10);
  }
 
  abrirFormulario(): void {
    this.mensaje = '';
    this.formulario.reset({ pacienteId: null, tipoTrabajo: '', piezaDental: '', laboratorio: '', fechaEntrega: '', costo: null, observaciones: '' });
    this.mostrarFormulario = true;
  }
 
  cerrarFormulario(): void { this.mostrarFormulario = false; }
 
  guardarTrabajo(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }
    const datos = this.formulario.getRawValue();
    const paciente = this.pacientes.find(p => p.id === datos.pacienteId);
    if (!paciente) return;
 
    this.laboratorioService.agregar({
      pacienteId: paciente.id,
      pacienteNombre: `${paciente.nombre} ${paciente.apellido}`,
      tipoTrabajo: datos.tipoTrabajo!.trim(),
      piezaDental: datos.piezaDental?.trim() || '—',
      laboratorio: datos.laboratorio!.trim(),
      fechaSolicitud: this.fechaHoy(),
      fechaEntrega: datos.fechaEntrega!,
      fechaRecepcion: '',
      fechaEntregaPaciente: '',
      estado: 'Solicitado',
      costo: Number(datos.costo),
      observaciones: datos.observaciones?.trim() || ''
    });
    this.actualizarLista();
    this.mostrarFormulario = false;
    this.mensaje = 'El trabajo se registró correctamente.';
  }
 
  cambiarEstado(trabajo: TrabajoLaboratorio, estado: string): void {
    this.laboratorioService.actualizarEstado(trabajo.id, estado as EstadoTrabajo);
    this.actualizarLista();
  }
 
  private actualizarLista(): void { this.trabajos = this.laboratorioService.obtenerTodos(); }
 
  claseEstado(estado: EstadoTrabajo): string {
    return estado.toLowerCase().replaceAll(' ', '-');
  }
 
  fechaLegible(fecha: string): string {
    if (!fecha) return '—';
    return new Date(`${fecha}T00:00:00`).toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }
 
  formatoPesos(valor: number): string {
    return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(valor);
  }
}
 