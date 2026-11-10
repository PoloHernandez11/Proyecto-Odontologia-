import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, of, switchMap } from 'rxjs';
import { OdontiaApi } from '../../api/odontia-api.service';
import { LabBoardDto, LabWorkDto, LabWorkStatus, LaboratoryDto, PatientListItemDto, ProfessionalDto } from '../../api/schema';
import { fechaCorta, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';

const ESTADOS: Record<LabWorkStatus, string> = {
  Pending: 'Solicitado', Sent: 'Enviado', InProduction: 'En proceso', ReadyForPickup: 'Listo para retirar',
  Received: 'Recibido', Placed: 'Colocado al paciente', Cancelled: 'Cancelado'
};
const CLASES: Record<LabWorkStatus, string> = {
  Pending: 'solicitado', Sent: 'en-proceso', InProduction: 'en-proceso', ReadyForPickup: 'listo-para-retirar', Received: 'recibido', Placed: 'entregado-al-paciente', Cancelled: 'cancelled'
};
const CERRADOS: LabWorkStatus[] = ['Placed', 'Cancelled'];
const NUEVO_LABORATORIO = '__nuevo__';

@Component({
  selector: 'app-laboratorio',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './laboratorio.component.html',
  styleUrl: './laboratorio.component.css'
})
export class LaboratorioComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);

  readonly nombres = ESTADOS;
  readonly clases = CLASES;
  readonly estados = Object.keys(ESTADOS) as LabWorkStatus[];
  readonly nuevoLaboratorio = NUEVO_LABORATORIO;
  readonly puedeCrear = this.auth.tiene('lab.write');
  readonly puedeSeguir = this.auth.tiene('lab.track') || this.auth.tiene('lab.write');
  readonly puedeCrearLaboratorio = this.auth.tiene('laboratories.manage');

  trabajos: LabWorkDto[] = [];
  tablero: LabBoardDto | null = null;
  laboratorios: LaboratoryDto[] = [];
  pacientes: PatientListItemDto[] = [];
  profesionales: ProfessionalDto[] = [];
  busqueda = '';
  filtroEstado = 'Todos';
  filtroVista: 'Todos' | 'Activos' | 'Historial' = 'Activos';
  cargando = true;
  mostrarFormulario = false;
  guardando = false;
  mensaje = '';
  error = '';
  errorFormulario = '';

  formulario = new FormGroup({
    pacienteId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    tipoTrabajo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(120)] }),
    piezaDental: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\d{0,2}$/)] }),
    laboratorioId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    laboratorioNuevo: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(150)] }),
    profesionalId: new FormControl('', { nonNullable: true }),
    fechaEntrega: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    costo: new FormControl<number | null>(null, [Validators.min(0)]),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });

  constructor() {
    this.cargar();
    this.api.laboratorios().subscribe({ next: laboratorios => (this.laboratorios = laboratorios.filter(l => l.isActive)) });
  }

  cargar(): void {
    this.cargando = true;
    this.api.trabajosDeLaboratorio().subscribe({
      next: pagina => { this.cargando = false; this.error = ''; this.trabajos = pagina.items; },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudieron cargar los trabajos de laboratorio.'); }
    });
    this.api.tableroDeLaboratorio().subscribe({ next: tablero => (this.tablero = tablero) });
  }

  get trabajosFiltrados(): LabWorkDto[] {
    const texto = this.busqueda.trim().toLowerCase();
    return this.trabajos.filter(trabajo => {
      const coincideTexto = `${trabajo.patientName} ${trabajo.workType} ${trabajo.laboratoryName}`.toLowerCase().includes(texto);
      const coincideEstado = this.filtroEstado === 'Todos' || trabajo.status === this.filtroEstado;
      const cerrado = CERRADOS.includes(trabajo.status);
      const coincideVista = this.filtroVista === 'Todos' || (this.filtroVista === 'Historial' ? cerrado : !cerrado);
      return coincideTexto && coincideEstado && coincideVista;
    });
  }
  get activos(): number { return this.trabajos.filter(trabajo => !CERRADOS.includes(trabajo.status)).length; }
  get listosParaRetirar(): number { return this.tablero?.countsByStatus['ReadyForPickup'] ?? this.trabajos.filter(t => t.status === 'ReadyForPickup').length; }

  verVista(vista: 'Todos' | 'Activos' | 'Historial'): void { this.filtroVista = vista; this.filtroEstado = 'Todos'; }

  abrirFormulario(): void {
    this.mensaje = '';
    this.errorFormulario = '';
    this.formulario.reset({
      pacienteId: '', tipoTrabajo: '', piezaDental: '', laboratorioId: '', laboratorioNuevo: '',
      profesionalId: this.auth.esProfesional() ? this.auth.userId ?? '' : '', fechaEntrega: '', costo: null, observaciones: ''
    });
    this.mostrarFormulario = true;
    if (!this.pacientes.length) this.api.pacientes({ pageSize: 100 }).subscribe({ next: pagina => (this.pacientes = pagina.items.filter(p => p.status !== 'Inactive')) });
    if (!this.profesionales.length) this.api.profesionales().subscribe({ next: profesionales => (this.profesionales = profesionales) });
  }
  cerrarFormulario(): void { this.mostrarFormulario = false; }

  guardarTrabajo(): void {
    const datos = this.formulario.getRawValue();
    const creaLaboratorio = datos.laboratorioId === NUEVO_LABORATORIO;
    if (this.formulario.invalid || (creaLaboratorio && datos.laboratorioNuevo.trim().length < 2)) {
      this.formulario.markAllAsTouched();
      if (creaLaboratorio && datos.laboratorioNuevo.trim().length < 2) this.errorFormulario = 'Escribí el nombre del laboratorio nuevo.';
      return;
    }
    this.guardando = true;
    this.errorFormulario = '';
    const laboratorio$: Observable<LaboratoryDto | null> = creaLaboratorio ? this.api.crearLaboratorio(datos.laboratorioNuevo.trim()) : of(null);
    laboratorio$.pipe(
      switchMap(nuevo => {
        if (nuevo) this.laboratorios = [...this.laboratorios, nuevo];
        return this.api.crearTrabajoDeLaboratorio({
          patientId: datos.pacienteId, laboratoryId: nuevo ? nuevo.id : datos.laboratorioId, professionalUserId: datos.profesionalId || null,
          workType: datos.tipoTrabajo.trim(), toothCode: datos.piezaDental ? Number(datos.piezaDental) : null,
          estimatedDeliveryDate: datos.fechaEntrega, cost: datos.costo, notes: datos.observaciones.trim() || null
        });
      })
    ).subscribe({
      next: trabajo => {
        this.guardando = false;
        this.mostrarFormulario = false;
        this.mensaje = `Se registró el trabajo "${trabajo.workType}" para ${trabajo.patientName}.`;
        this.cargar();
      },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo registrar el trabajo.'); }
    });
  }

  cambiarEstado(trabajo: LabWorkDto, estado: LabWorkStatus): void {
    if (estado === trabajo.status) return;
    this.api.estadoTrabajoDeLaboratorio(trabajo.id, estado).subscribe({
      next: actualizado => { this.error = ''; this.mensaje = `El trabajo de ${actualizado.patientName} ahora figura como: ${ESTADOS[actualizado.status]}.`; this.cargar(); },
      // Si el backend rechaza el cambio (transición inválida o falta de permiso), se vuelve a mostrar el estado real.
      error: error => { this.error = mensajeDeError(error, 'No se pudo actualizar el estado del trabajo.'); this.cargar(); }
    });
  }

  fechaLegible(fecha: string | null | undefined): string { return fecha ? fechaCorta(fecha) : 'Sin fecha'; }
  formatoPesos(importe: number | null | undefined): string { return importe === null || importe === undefined ? '—' : pesos(importe); }
}
