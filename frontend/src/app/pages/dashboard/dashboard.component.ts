import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentStatus, ClinicalDashboardDto, DashboardTaskDto, PaymentDto, ReceptionDashboardDto } from '../../api/schema';
import { fechaCorta, horaLocal, iniciales, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { fechaDelConsultorio, formatoEnConsultorio, inicioDelDia, partesEnZona, zonaDelConsultorio } from '../../core/zona-horaria';
import { PluralPipe } from '../../shared/plural.pipe';

type Vista = 'recepcion' | 'clinica';

const CLASE_DE_ESTADO: Record<AppointmentStatus, string> = {
  Pending: 'pendiente', Confirmed: 'confirmado', Arrived: 'en-sala', InWaitingRoom: 'en-sala', InProgress: 'en-sala',
  Attended: 'atendido', Cancelled: 'cancelado', NoShow: 'cancelado'
};

/** Primer día (yyyy-MM-01) del mes de la fecha indicada y del mes siguiente. */
function mesDe(fecha: string): { desde: string; hasta: string } {
  const [anio, mes] = fecha.split('-').map(Number);
  const dos = (n: number) => String(n).padStart(2, '0');
  return { desde: `${anio}-${dos(mes)}-01`, hasta: mes === 12 ? `${anio + 1}-01-01` : `${anio}-${dos(mes + 1)}-01` };
}

/** Tablero de inicio. OnPush: el estado de la plantilla son signals. Fechas y horas, en la zona del consultorio. */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, PluralPipe],
  templateUrl: './dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly estados = ESTADOS_DE_TURNO;
  readonly clases = CLASE_DE_ESTADO;
  readonly nombreUsuario = this.auth.nombre;
  get tieneRecepcion(): boolean { return this.auth.tiene('dashboard.reception'); }
  get tieneClinica(): boolean { return this.auth.tiene('dashboard.clinical'); }
  get puedeAgendar(): boolean { return this.auth.tiene('appointments.write'); }
  get puedeTareas(): boolean { return this.auth.tiene('tasks.write'); }
  get veOdontograma(): boolean { return this.auth.tiene('odontogram.read'); }
  get veHistoria(): boolean { return this.auth.tiene('clinical.read'); }
  get veFinanzas(): boolean { return this.auth.tiene('finance.read'); }
  // El perfil no se elige: sale de los permisos. Quien tiene ambos tableros puede alternar entre ellos.
  readonly vista = signal<Vista>(this.tieneClinica && (this.auth.esProfesional() || !this.tieneRecepcion) ? 'clinica' : 'recepcion');
  readonly recepcion = signal<ReceptionDashboardDto | null>(null);
  readonly clinica = signal<ClinicalDashboardDto | null>(null);
  readonly pagosDelMes = signal<PaymentDto[]>([]);
  readonly ingresosDelMes = computed(() => this.pagosDelMes().reduce((suma, pago) => suma + pago.amount, 0));
  readonly cargando = signal(true);
  readonly error = signal('');
  readonly mensaje = signal('');

  /** Saludo y fecha según la hora del consultorio (su zona horaria, no la del navegador). */
  readonly saludo = computed(() => {
    const hora = partesEnZona(new Date(), zonaDelConsultorio()).hora;
    return hora < 12 ? 'Buenos días' : hora < 20 ? 'Buenas tardes' : 'Buenas noches';
  });
  readonly fechaHoyTexto = computed(() => formatoEnConsultorio(new Date(), { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }));

  constructor() { this.cargar(); }

  get sinTablero(): boolean { return !this.tieneRecepcion && !this.tieneClinica; }

  cambiarVista(vista: Vista): void { this.vista.set(vista); this.cargar(); }

  cargar(): void {
    if (this.sinTablero) { this.cargando.set(false); return; }
    this.cargando.set(true);
    if (this.vista() === 'recepcion') {
      this.api.dashboardRecepcion().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: datos => { this.recepcion.set(datos); this.cargando.set(false); this.error.set(''); },
        error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudo cargar el resumen del día.')); }
      });
    } else {
      this.api.dashboardClinico().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: datos => { this.clinica.set(datos); this.cargando.set(false); this.error.set(''); },
        error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudo cargar el resumen del día.')); }
      });
      if (this.veFinanzas) {
        // Mes calendario del consultorio: desde las 00:00 del día 1 hasta las 00:00 del día 1 del mes siguiente.
        const { desde, hasta } = mesDe(fechaDelConsultorio());
        this.api.pagos({ From: inicioDelDia(desde).toISOString(), To: inicioDelDia(hasta).toISOString() })
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({ next: pagina => this.pagosDelMes.set(pagina.items.filter(pago => pago.status === 'Registered')) });
      }
    }
  }

  buscarPaciente(evento: Event): void {
    const texto = (evento.target as HTMLInputElement).value.trim();
    if (texto) this.router.navigate(['/pacientes'], { queryParams: { buscar: texto } });
  }

  completarTarea(tarea: DashboardTaskDto): void {
    this.api.estadoTarea(tarea.id, 'Completed').subscribe({
      next: () => { this.mensaje.set(`Tarea completada: ${tarea.title}`); this.cargar(); },
      error: error => this.error.set(mensajeDeError(error, 'No se pudo completar la tarea.'))
    });
  }

  iniciarConsulta(turnoId: string): void {
    this.api.accionTurno(turnoId, 'start').subscribe({
      next: () => { this.mensaje.set('Consulta iniciada.'); this.cargar(); },
      error: error => this.error.set(mensajeDeError(error, 'No se pudo iniciar la consulta.'))
    });
  }

  hora(valor: string | null | undefined): string { return valor ? horaLocal(valor) : '—'; }
  fecha(valor: string | null | undefined): string { return valor ? fechaCorta(valor) : 'Sin fecha'; }
  iniciales(nombre: string): string { return iniciales(nombre); }
  pesos(importe: number): string { return pesos(importe); }
  vence(tarea: DashboardTaskDto): string { return tarea.isOverdue ? 'Vencida' : tarea.dueAtUtc ? fechaCorta(tarea.dueAtUtc).slice(0, 5) : 'Sin fecha'; }
}
