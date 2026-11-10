import { CommonModule } from '@angular/common';
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentStatus, ClinicalDashboardDto, DashboardTaskDto, PaymentDto, ReceptionDashboardDto } from '../../api/schema';
import { fechaCorta, horaLocal, iniciales, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';

type Vista = 'recepcion' | 'clinica';

const CLASE_DE_ESTADO: Record<AppointmentStatus, string> = {
  Pending: 'pendiente', Confirmed: 'confirmado', Arrived: 'en-sala', InWaitingRoom: 'en-sala', InProgress: 'en-sala',
  Attended: 'atendido', Cancelled: 'cancelado', NoShow: 'cancelado'
};

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly estados = ESTADOS_DE_TURNO;
  readonly clases = CLASE_DE_ESTADO;
  readonly nombreUsuario = this.auth.nombre;
  readonly tieneRecepcion = this.auth.tiene('dashboard.reception');
  readonly tieneClinica = this.auth.tiene('dashboard.clinical');
  readonly puedeAgendar = this.auth.tiene('appointments.write');
  readonly puedeTareas = this.auth.tiene('tasks.write');
  readonly veOdontograma = this.auth.tiene('odontogram.read');
  readonly veHistoria = this.auth.tiene('clinical.read');
  readonly veFinanzas = this.auth.tiene('finance.read');

  // El perfil no se elige: sale de los permisos. Quien tiene ambos tableros puede alternar entre ellos.
  vista: Vista = this.tieneClinica && (this.auth.esProfesional() || !this.tieneRecepcion) ? 'clinica' : 'recepcion';
  recepcion: ReceptionDashboardDto | null = null;
  clinica: ClinicalDashboardDto | null = null;
  pagosDelMes: PaymentDto[] = [];
  cargando = true;
  error = '';
  mensaje = '';

  constructor() { this.cargar(); }

  get sinTablero(): boolean { return !this.tieneRecepcion && !this.tieneClinica; }
  get saludo(): string { const hora = new Date().getHours(); return hora < 12 ? 'Buenos días' : hora < 20 ? 'Buenas tardes' : 'Buenas noches'; }
  get fechaHoyTexto(): string { return new Intl.DateTimeFormat('es-AR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()); }

  cambiarVista(vista: Vista): void { this.vista = vista; this.cargar(); }

  cargar(): void {
    if (this.sinTablero) { this.cargando = false; return; }
    this.cargando = true;
    if (this.vista === 'recepcion') {
      this.api.dashboardRecepcion().subscribe({
        next: datos => { this.recepcion = datos; this.cargando = false; this.error = ''; },
        error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar el resumen del día.'); }
      });
    } else {
      this.api.dashboardClinico().subscribe({
        next: datos => { this.clinica = datos; this.cargando = false; this.error = ''; },
        error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar el resumen del día.'); }
      });
      if (this.veFinanzas) {
        const hoy = new Date();
        this.api.pagos({ From: new Date(hoy.getFullYear(), hoy.getMonth(), 1).toISOString(), To: new Date(hoy.getFullYear(), hoy.getMonth() + 1, 1).toISOString() })
          .subscribe({ next: pagina => (this.pagosDelMes = pagina.items.filter(pago => pago.status === 'Registered')) });
      }
    }
  }

  get ingresosDelMes(): number { return this.pagosDelMes.reduce((suma, pago) => suma + pago.amount, 0); }

  buscarPaciente(evento: Event): void {
    const texto = (evento.target as HTMLInputElement).value.trim();
    if (texto) this.router.navigate(['/pacientes'], { queryParams: { buscar: texto } });
  }

  completarTarea(tarea: DashboardTaskDto): void {
    this.api.estadoTarea(tarea.id, 'Completed').subscribe({
      next: () => { this.mensaje = `Tarea completada: ${tarea.title}`; this.cargar(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo completar la tarea.'))
    });
  }

  iniciarConsulta(turnoId: string): void {
    this.api.accionTurno(turnoId, 'start').subscribe({
      next: () => { this.mensaje = 'Consulta iniciada.'; this.cargar(); },
      error: error => (this.error = mensajeDeError(error, 'No se pudo iniciar la consulta.'))
    });
  }

  hora(valor: string | null | undefined): string { return valor ? horaLocal(valor) : '—'; }
  fecha(valor: string | null | undefined): string { return valor ? fechaCorta(valor) : 'Sin fecha'; }
  iniciales(nombre: string): string { return iniciales(nombre); }
  pesos(importe: number): string { return pesos(importe); }
  vence(tarea: DashboardTaskDto): string { return tarea.isOverdue ? 'Vencida' : tarea.dueAtUtc ? fechaCorta(tarea.dueAtUtc).slice(0, 5) : 'Sin fecha'; }
}
