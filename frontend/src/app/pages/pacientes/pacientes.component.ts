
import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, Subscription, debounceTime, of, switchMap } from 'rxjs';
import { ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentDto, HealthInsuranceDto, PatientDto, PatientListItemDto, PatientStatus, PatientSummaryDto, SavePatientRequest } from '../../api/schema';
import { esConflictoDeConcurrencia, fechaCorta, horaLocal, iniciales, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { DialogDirective } from '../../shared/dialog.directive';
import { PluralPipe } from '../../shared/plural.pipe';

const ESTADOS: Record<PatientStatus, string> = { New: 'Nuevo', Active: 'Activo', InTreatment: 'En tratamiento', Inactive: 'Inactivo' };
const NUEVA_OBRA_SOCIAL = '__nueva__';

@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, RouterLink, DialogDirective, PluralPipe],
  templateUrl: './pacientes.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './pacientes.component.css'
})
export class PacientesComponent implements OnDestroy {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly busquedas = new Subject<void>();
  private readonly suscripcion: Subscription;

  readonly estados = ESTADOS;
  readonly nuevaObraSocial = NUEVA_OBRA_SOCIAL;
  get puedeEditar(): boolean { return this.auth.tiene('patients.write'); }
  get puedeCrearObraSocial(): boolean { return this.auth.tiene('insurances.manage'); }
  get veHistoriaClinica(): boolean { return this.auth.tiene('clinical.read'); }
  get veOdontograma(): boolean { return this.auth.tiene('odontogram.read'); }
  get vePagos(): boolean { return this.auth.tiene('finance.read'); }
  readonly busqueda = signal('');
  readonly obraSocialSeleccionada = signal('Todas');
  readonly estadoSeleccionado = signal('Todos');
  readonly obrasSociales = signal<HealthInsuranceDto[]>([]);
  readonly pacientes = signal<PatientListItemDto[]>([]);
  readonly totalPacientes = signal(0);
  readonly cargando = signal(true);
  readonly pacienteSeleccionado = signal<PatientListItemDto | null>(null);
  readonly ficha = signal<PatientDto | null>(null);
  readonly resumen = signal<PatientSummaryDto | null>(null);
  readonly historial = signal<AppointmentDto[]>([]);
  readonly mostrarFormulario = signal(false);
  readonly modoEdicion = signal(false);
  readonly guardando = signal(false);
  readonly mensaje = signal('');
  readonly error = signal('');
  /** Mientras se recarga tras un conflicto 409, el listado no borra el aviso. */
  private conservarAviso = false;
  readonly errorFormulario = signal('');

  formularioPaciente = new FormGroup({
    nombre: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    apellido: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    dni: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9.]{7,10}$/)] }),
    telefono: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9+()\-\s]{8,40}$/)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
    obraSocial: new FormControl('', { nonNullable: true }),
    obraSocialNueva: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] })
  });

  constructor() {
    this.api.obrasSociales().subscribe({ next: obras => (this.obrasSociales.set(obras.filter(obra => obra.isActive))) });
    this.suscripcion = this.busquedas.pipe(
      debounceTime(250),
      switchMap(() => {
        this.cargando.set(true);
        return this.api.pacientes({
          Search: this.busqueda().trim(),
          Status: this.estadoSeleccionado() === 'Todos' ? null : this.estadoSeleccionado(),
          HealthInsuranceId: ['Todas', 'Particular'].includes(this.obraSocialSeleccionada()) ? null : this.obraSocialSeleccionada(),
          Private: this.obraSocialSeleccionada() === 'Particular' ? true : null
        });
      })
    ).subscribe({
      next: pagina => {
        this.cargando.set(false);
        if (!this.conservarAviso) this.error.set('');
        this.conservarAviso = false;
        this.pacientes.set(pagina.items);
        this.totalPacientes.set(pagina.totalCount);
        const actual = this.pacientes().find(paciente => paciente.id === this.pacienteSeleccionado()?.id);
        if (actual) this.pacienteSeleccionado.set(actual);
        else if (pagina.items.length) this.seleccionarPaciente(pagina.items[0]);
        else { this.pacienteSeleccionado.set(null); this.ficha.set(null); this.resumen.set(null); this.historial.set([]); }
      },
      error: error => { this.cargando.set(false); this.error.set(mensajeDeError(error, 'No se pudo cargar el listado de pacientes.')); }
    });
    this.busqueda.set(this.route.snapshot.queryParamMap.get('buscar') ?? '');
    this.buscar();
  }

  ngOnDestroy(): void { this.suscripcion.unsubscribe(); }

  buscar(): void { this.busquedas.next(); }

  seleccionarPaciente(paciente: PatientListItemDto): void {
    this.pacienteSeleccionado.set(paciente);
    this.ficha.set(null);
    this.resumen.set(null);
    this.historial.set([]);
    this.api.resumenPaciente(paciente.id).subscribe({
      next: resumen => { if (this.pacienteSeleccionado()?.id === paciente.id) { this.resumen.set(resumen); this.ficha.set(resumen.patient); } }
    });
    if (this.auth.tiene('appointments.read')) {
      this.api.turnos({ PatientId: paciente.id, pageSize: 20 }).subscribe({
        next: pagina => {
          if (this.pacienteSeleccionado()?.id === paciente.id) {
            this.historial.set([...pagina.items].sort((a, b) => b.startUtc.localeCompare(a.startUtc)).slice(0, 8));
          }
        }
      });
    }
  }

  abrirNuevoPaciente(): void {
    this.modoEdicion.set(false);
    this.errorFormulario.set('');
    this.formularioPaciente.reset({ nombre: '', apellido: '', dni: '', telefono: '', email: '', obraSocial: '', obraSocialNueva: '' });
    this.mostrarFormulario.set(true);
  }

  editarPaciente(): void {
    const ficha = this.ficha();
    if (!ficha) return;
    this.modoEdicion.set(true);
    this.errorFormulario.set('');
    this.formularioPaciente.reset({
      nombre: ficha.firstName, apellido: ficha.lastName, dni: ficha.documentNumber,
      telefono: ficha.phone ?? '', email: ficha.email ?? '', obraSocial: ficha.healthInsuranceId ?? '', obraSocialNueva: ''
    });
    this.mostrarFormulario.set(true);
  }

  cerrarFormulario(): void { this.mostrarFormulario.set(false); }

  guardarPaciente(): void {
    const datos = this.formularioPaciente.getRawValue();
    const creaObraSocial = datos.obraSocial === NUEVA_OBRA_SOCIAL;
    if (this.formularioPaciente.invalid || (creaObraSocial && datos.obraSocialNueva.trim().length < 2)) {
      this.formularioPaciente.markAllAsTouched();
      if (creaObraSocial && datos.obraSocialNueva.trim().length < 2) this.errorFormulario.set('Escribí el nombre de la obra social nueva.');
      return;
    }

    this.guardando.set(true);
    this.errorFormulario.set('');
    const obraSocial$: Observable<HealthInsuranceDto | null> = creaObraSocial ? this.api.crearObraSocial(datos.obraSocialNueva.trim()) : of(null);
    obraSocial$.pipe(
      switchMap(nueva => {
        if (nueva) this.obrasSociales.set([...this.obrasSociales(), nueva].sort((a, b) => a.name.localeCompare(b.name)));
        const base = this.modoEdicion() ? this.ficha() : null;
        const solicitud: SavePatientRequest = {
          firstName: datos.nombre.trim(), lastName: datos.apellido.trim(), documentNumber: datos.dni.replace(/\./g, '').trim(),
          phone: datos.telefono.trim(), email: datos.email.trim() || null,
          healthInsuranceId: nueva ? nueva.id : datos.obraSocial || null,
          // El resto de la ficha no se edita en esta pantalla: se conserva tal como está en el servidor.
          sex: base?.sex ?? 'NotSpecified', birthDate: base?.birthDate, occupation: base?.occupation, addressLine: base?.addressLine,
          city: base?.city, province: base?.province, postalCode: base?.postalCode, emergencyContactName: base?.emergencyContactName,
          emergencyContactPhone: base?.emergencyContactPhone, emergencyContactRelationship: base?.emergencyContactRelationship,
          insurancePlan: base?.insurancePlan, affiliateNumber: base?.affiliateNumber, administrativeNotes: base?.administrativeNotes
        };
        return base
          ? this.api.actualizarPaciente(base.id, solicitud)
          : this.api.crearPaciente({
              patient: solicitud,
              medicalHistory: { diabetes: false, hypertension: false, heartDisease: false, anticoagulated: false, pregnant: false, asthma: false, hepatitis: false, smoker: false }
            });
      })
    ).subscribe({
      next: paciente => {
        this.guardando.set(false);
        this.mostrarFormulario.set(false);
        this.mensaje.set(this.modoEdicion() ? 'Los datos del paciente se actualizaron correctamente.' : 'El paciente se registró correctamente.');
        this.seleccionarPaciente({
          id: paciente.id, firstName: paciente.firstName, lastName: paciente.lastName, documentNumber: paciente.documentNumber,
          phone: paciente.phone, healthInsurance: paciente.healthInsuranceName, status: paciente.status
        });
        this.buscar();
      },
      error: error => {
        this.guardando.set(false);
        if (!esConflictoDeConcurrencia(error)) { this.errorFormulario.set(mensajeDeError(error, 'No se pudo guardar el paciente.')); return; }
        this.mostrarFormulario.set(false);
        this.recargarTrasConflicto(error);
      }
    });
  }

  cambiarEstado(): void {
    const ficha = this.ficha();
    if (!ficha) return;
    const nuevo: PatientStatus = ficha.status === 'Inactive' ? 'Active' : 'Inactive';
    this.api.cambiarEstadoPaciente(ficha.id, nuevo).subscribe({
      next: paciente => { this.ficha.set(paciente); this.mensaje.set(`El paciente ahora figura como ${ESTADOS[paciente.status].toLowerCase()}.`); this.buscar(); },
      error: error => {
        if (esConflictoDeConcurrencia(error)) { this.recargarTrasConflicto(error); return; }
        this.mensaje.set('');
        this.error.set(mensajeDeError(error, 'No se pudo cambiar el estado del paciente.'));
      }
    });
  }

  /** Otra persona modificó al paciente: se muestra el aviso del backend y se recargan la ficha y el listado. */
  private recargarTrasConflicto(error: unknown): void {
    this.mensaje.set('');
    this.error.set(mensajeDeError(error, 'Otra persona modificó este paciente. Se recargaron los datos.'));
    this.conservarAviso = true;
    const seleccionado = this.pacienteSeleccionado();
    if (seleccionado) this.seleccionarPaciente(seleccionado);
    this.buscar();
  }

  iniciales(paciente: { firstName: string; lastName: string }): string { return iniciales(`${paciente.firstName} ${paciente.lastName}`); }
  fecha(valor: string | null | undefined, vacio: string): string { return valor ? fechaCorta(valor) : vacio; }
  fechaYHora(valor: string | null | undefined, vacio: string): string { return valor ? `${fechaCorta(valor)} · ${horaLocal(valor)}` : vacio; }
  hora(valor: string): string { return horaLocal(valor); }
  estadoDeTurno(turno: AppointmentDto): string { return ESTADOS_DE_TURNO[turno.status]; }
  pesos(importe: number): string { return pesos(importe); }
}
