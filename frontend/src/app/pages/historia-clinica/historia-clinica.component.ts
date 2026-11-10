
import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OdontiaApi } from '../../api/odontia-api.service';
import { ClinicalEntryDto, ClinicalEntryType, ClinicalRecordDto, PatientDto } from '../../api/schema';
import { fechaCorta, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

const TIPOS: Record<ClinicalEntryType, string> = {
  Consultation: 'Consulta', Evolution: 'Evolución', Procedure: 'Procedimiento', Observation: 'Observación', Prescription: 'Receta', Study: 'Estudio'
};
const ANTECEDENTES: [keyof ClinicalRecordDto['medicalHistory'], string][] = [
  ['diabetes', 'Diabetes'], ['hypertension', 'Hipertensión'], ['heartDisease', 'Cardiopatía'], ['anticoagulated', 'Anticoagulado'],
  ['pregnant', 'Embarazo'], ['asthma', 'Asma'], ['hepatitis', 'Hepatitis'], ['smoker', 'Fumador']
];

@Component({
  selector: 'app-historia-clinica', standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './historia-clinica.component.html', styleUrl: './historia-clinica.component.css'
})
export class HistoriaClinicaComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly pacienteId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  readonly tipos = TIPOS;
  readonly tiposDisponibles = Object.entries(TIPOS) as [ClinicalEntryType, string][];
  // Registrar información clínica exige el permiso y además ser profesional del consultorio.
  readonly puedeEscribir = this.auth.tiene('clinical.write') && this.auth.esProfesional();
  readonly veOdontograma = this.auth.tiene('odontogram.read');

  paciente: PatientDto | null = null;
  historia: ClinicalRecordDto | null = null;
  registros: ClinicalEntryDto[] = [];
  cargando = true;
  noEncontrado = false;
  mostrarFormulario = false;
  guardando = false;
  mensaje = '';
  error = '';

  formulario = new FormGroup({
    tipo: new FormControl<ClinicalEntryType>('Consultation', { nonNullable: true }),
    motivo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)] }),
    diagnostico: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    tratamiento: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    piezas: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\s*(\d{2}(\s*,\s*\d{2})*)?\s*$/)] }),
    observaciones: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] })
  });

  constructor() {
    this.api.paciente(this.pacienteId).subscribe({
      next: paciente => { this.paciente = paciente; this.cargando = false; },
      error: () => { this.noEncontrado = true; this.cargando = false; }
    });
    this.api.historiaClinica(this.pacienteId).subscribe({ next: historia => (this.historia = historia) });
    this.cargarRegistros();
  }

  private cargarRegistros(): void {
    this.api.evoluciones(this.pacienteId).subscribe({
      next: pagina => (this.registros = [...pagina.items].sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc))),
      error: error => (this.error = mensajeDeError(error, 'No se pudo cargar la historia clínica.'))
    });
  }

  get antecedentes(): string[] {
    const historia = this.historia?.medicalHistory;
    return historia ? ANTECEDENTES.filter(([clave]) => historia[clave] === true).map(([, titulo]) => titulo) : [];
  }

  fecha(valor: string): string { return fechaCorta(valor); }

  guardarRegistro(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    const descripcion = [
      datos.diagnostico.trim() && `Diagnóstico: ${datos.diagnostico.trim()}`,
      datos.tratamiento.trim() && `Tratamiento: ${datos.tratamiento.trim()}`
    ].filter(Boolean).join('\n');
    const piezas = datos.piezas.split(',').map(pieza => Number(pieza.trim())).filter(pieza => pieza > 0);
    this.guardando = true;
    this.error = '';
    this.api.crearEvolucion(this.pacienteId, {
      type: datos.tipo, title: datos.motivo.trim(), description: descripcion || null,
      indications: datos.observaciones.trim() || null, teeth: piezas.length ? piezas : null
    }).subscribe({
      next: () => {
        this.guardando = false;
        this.formulario.reset();
        this.mostrarFormulario = false;
        this.mensaje = 'El registro se guardó en la historia clínica.';
        this.cargarRegistros();
      },
      error: error => { this.guardando = false; this.error = mensajeDeError(error, 'No se pudo guardar el registro.'); }
    });
  }
}
