import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PacientesService } from '../../services/paciente.service';

@Component({
  selector: 'app-nuevo-paciente',
  templateUrl: './nuevo-paciente.component.html'
})
export class NuevoPacienteComponent implements OnInit {
  pacienteForm!: FormGroup;
  errorMessage: string = '';

  constructor(
    private fb: FormBuilder,
    private pacienteService: PacientesService
  ) {}

  ngOnInit(): void {    
    this.pacienteForm = this.fb.group({
      nombre: ['', [Validators.required, Validators.minLength(2)]],
      apellido: ['', [Validators.required]],
      // Validamos que sean solo números de 7 a 8 dígitos
      dni: ['', [Validators.required, Validators.pattern('^[0-9]{7,8}$')]],
      telefono: ['', [Validators.required]]
    });
  }

  guardar(): void {
    if (this.pacienteForm.invalid) {
      this.pacienteForm.markAllAsTouched();
      return;
    }

    this.pacienteService.crearPaciente(this.pacienteForm.value).subscribe({
      next: (res) => {
        alert('Paciente guardado con éxito!');
        this.pacienteForm.reset();
        this.errorMessage = '';
      },
      error: (err) => {
        // Capturamos la respuesta del backend si el DNI ya existe
        if (err.status === 400 || err.status === 409) {
          this.errorMessage = err.error.message || 'El DNI ya se encuentra registrado.';
        } else {
          this.errorMessage = 'Ocurrió un error inesperado en el servidor.';
        }
      }
    });
  }
}