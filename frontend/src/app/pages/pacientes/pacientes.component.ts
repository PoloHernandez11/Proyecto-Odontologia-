
import { Component, OnDestroy, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, Subscription, debounceTime, of, switchMap } from 'rxjs';
import { ESTADOS_DE_TURNO, OdontiaApi } from '../../api/odontia-api.service';
import { AppointmentDto, HealthInsuranceDto, PatientDto, PatientListItemDto, PatientStatus, PatientSummaryDto, SavePatientRequest } from '../../api/schema';
import { fechaCorta, horaLocal, iniciales, mensajeDeError, pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';

const ESTADOS: Record<PatientStatus, string> = { New: 'Nuevo', Active: 'Activo', InTreatment: 'En tratamiento', Inactive: 'Inactivo' };
const NUEVA_OBRA_SOCIAL = '__nueva__';

@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './pacientes.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  readonly puedeEditar = this.auth.tiene('patients.write');
  readonly puedeCrearObraSocial = this.auth.tiene('insurances.manage');
  readonly veHistoriaClinica = this.auth.tiene('clinical.read');
  readonly veOdontograma = this.auth.tiene('odontogram.read');
  readonly vePagos = this.auth.tiene('finance.read');

  busqueda = '';
  obraSocialSeleccionada = 'Todas';
  estadoSeleccionado = 'Todos';
  obrasSociales: HealthInsuranceDto[] = [];
  pacientes: PatientListItemDto[] = [];
  totalPacientes = 0;
  cargando = true;
  pacienteSeleccionado: PatientListItemDto | null = null;
  ficha: PatientDto | null = null;
  resumen: PatientSummaryDto | null = null;
  historial: AppointmentDto[] = [];
  mostrarFormulario = false;
  modoEdicion = false;
  guardando = false;
  mensaje = '';
  error = '';
  errorFormulario = '';

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
    this.api.obrasSociales().subscribe({ next: obras => (this.obrasSociales = obras.filter(obra => obra.isActive)) });
    this.suscripcion = this.busquedas.pipe(
      debounceTime(250),
      switchMap(() => {
        this.cargando = true;
        return this.api.pacientes({
          Search: this.busqueda.trim(),
          Status: this.estadoSeleccionado === 'Todos' ? null : this.estadoSeleccionado,
          HealthInsuranceId: ['Todas', 'Particular'].includes(this.obraSocialSeleccionada) ? null : this.obraSocialSeleccionada,
          Private: this.obraSocialSeleccionada === 'Particular' ? true : null
        });
      })
    ).subscribe({
      next: pagina => {
        this.cargando = false;
        this.error = '';
        this.pacientes = pagina.items;
        this.totalPacientes = pagina.totalCount;
        const actual = this.pacientes.find(paciente => paciente.id === this.pacienteSeleccionado?.id);
        if (actual) this.pacienteSeleccionado = actual;
        else if (this.pacientes.length) this.seleccionarPaciente(this.pacientes[0]);
        else { this.pacienteSeleccionado = null; this.ficha = null; this.resumen = null; this.historial = []; }
      },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar el listado de pacientes.'); }
    });
    this.busqueda = this.route.snapshot.queryParamMap.get('buscar') ?? '';
    this.buscar();
  }

  ngOnDestroy(): void { this.suscripcion.unsubscribe(); }

  buscar(): void { this.busquedas.next(); }

  seleccionarPaciente(paciente: PatientListItemDto): void {
    this.pacienteSeleccionado = paciente;
    this.ficha = null;
    this.resumen = null;
    this.historial = [];
    this.api.resumenPaciente(paciente.id).subscribe({
      next: resumen => { if (this.pacienteSeleccionado?.id === paciente.id) { this.resumen = resumen; this.ficha = resumen.patient; } }
    });
    if (this.auth.tiene('appointments.read')) {
      this.api.turnos({ PatientId: paciente.id, pageSize: 20 }).subscribe({
        next: pagina => {
          if (this.pacienteSeleccionado?.id === paciente.id) {
            this.historial = [...pagina.items].sort((a, b) => b.startUtc.localeCompare(a.startUtc)).slice(0, 8);
          }
        }
      });
    }
  }

  abrirNuevoPaciente(): void {
    this.modoEdicion = false;
    this.errorFormulario = '';
    this.formularioPaciente.reset({ nombre: '', apellido: '', dni: '', telefono: '', email: '', obraSocial: '', obraSocialNueva: '' });
    this.mostrarFormulario = true;
  }

  editarPaciente(): void {
    if (!this.ficha) return;
    this.modoEdicion = true;
    this.errorFormulario = '';
    this.formularioPaciente.reset({
      nombre: this.ficha.firstName, apellido: this.ficha.lastName, dni: this.ficha.documentNumber,
      telefono: this.ficha.phone ?? '', email: this.ficha.email ?? '', obraSocial: this.ficha.healthInsuranceId ?? '', obraSocialNueva: ''
    });
    this.mostrarFormulario = true;
  }

  cerrarFormulario(): void { this.mostrarFormulario = false; }

  guardarPaciente(): void {
    const datos = this.formularioPaciente.getRawValue();
    const creaObraSocial = datos.obraSocial === NUEVA_OBRA_SOCIAL;
    if (this.formularioPaciente.invalid || (creaObraSocial && datos.obraSocialNueva.trim().length < 2)) {
      this.formularioPaciente.markAllAsTouched();
      if (creaObraSocial && datos.obraSocialNueva.trim().length < 2) this.errorFormulario = 'Escribí el nombre de la obra social nueva.';
      return;
    }

    this.guardando = true;
    this.errorFormulario = '';
    const obraSocial$: Observable<HealthInsuranceDto | null> = creaObraSocial ? this.api.crearObraSocial(datos.obraSocialNueva.trim()) : of(null);
    obraSocial$.pipe(
      switchMap(nueva => {
        if (nueva) this.obrasSociales = [...this.obrasSociales, nueva].sort((a, b) => a.name.localeCompare(b.name));
        const base = this.modoEdicion && this.ficha ? this.ficha : null;
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
        this.guardando = false;
        this.mostrarFormulario = false;
        this.mensaje = this.modoEdicion ? 'Los datos del paciente se actualizaron correctamente.' : 'El paciente se registró correctamente.';
        this.seleccionarPaciente({
          id: paciente.id, firstName: paciente.firstName, lastName: paciente.lastName, documentNumber: paciente.documentNumber,
          phone: paciente.phone, healthInsurance: paciente.healthInsuranceName, status: paciente.status
        });
        this.buscar();
      },
      error: error => { this.guardando = false; this.errorFormulario = mensajeDeError(error, 'No se pudo guardar el paciente.'); }
    });
  }

  cambiarEstado(): void {
    if (!this.ficha) return;
    const nuevo: PatientStatus = this.ficha.status === 'Inactive' ? 'Active' : 'Inactive';
    this.api.cambiarEstadoPaciente(this.ficha.id, nuevo).subscribe({
      next: paciente => { this.ficha = paciente; this.mensaje = `El paciente ahora figura como ${ESTADOS[paciente.status].toLowerCase()}.`; this.buscar(); },
      error: error => (this.mensaje = mensajeDeError(error, 'No se pudo cambiar el estado del paciente.'))
    });
  }

  iniciales(paciente: { firstName: string; lastName: string }): string { return iniciales(`${paciente.firstName} ${paciente.lastName}`); }
  fecha(valor: string | null | undefined, vacio: string): string { return valor ? fechaCorta(valor) : vacio; }
  fechaYHora(valor: string | null | undefined, vacio: string): string { return valor ? `${fechaCorta(valor)} · ${horaLocal(valor)}` : vacio; }
  hora(valor: string): string { return horaLocal(valor); }
  estadoDeTurno(turno: AppointmentDto): string { return ESTADOS_DE_TURNO[turno.status]; }
  pesos(importe: number): string { return pesos(importe); }
}
