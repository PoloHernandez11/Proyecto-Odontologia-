import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Paciente } from '../../models/paciente.model';
import { RegistroClinico } from '../../models/historia-clinica.model';
import { PacientesService } from '../../services/paciente.service';
import { HistoriaClinicaService } from '../../services/historia-clinica.service';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-historia-clinica', standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './historia-clinica.component.html', styleUrl: './historia-clinica.component.css'
})
export class HistoriaClinicaComponent {
  paciente: Paciente | undefined;
  registros: RegistroClinico[] = [];
  mostrarFormulario = false;
  mensaje = '';
  perfil = 'odontologo';
  formulario = new FormGroup({
    motivo: new FormControl('', [Validators.required, Validators.minLength(3)]),
    diagnostico: new FormControl('', [Validators.required, Validators.minLength(3)]),
    tratamiento: new FormControl('', Validators.required),
    observaciones: new FormControl('')
  });

  constructor(private route: ActivatedRoute, 
    private pacientes: PacientesService, 
    private historias: HistoriaClinicaService,
    private authService: AuthService,
    private router: Router) 
  {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    //this.paciente = this.pacientes.obtenerTodos().find(item => item.id === id);
    this.perfil = this.route.snapshot.queryParamMap.get('perfil') || 'odontologo';
    if (this.paciente) this.registros = this.historias.obtenerRegistros(this.paciente.id);
  }
  ngOnInit(): void{
    if(!this.authService.puedeVerHistorialClinico()){
      alert('Acceso Restringido: El historial clinico es exclusivo del odontologo.');
      this.router.navigate(['/pacientes']);
    }

  }

  guardarRegistro(): void {
    if (!this.paciente || this.formulario.invalid) { this.formulario.markAllAsTouched(); return; }
    const datos = this.formulario.getRawValue();
    const registro: RegistroClinico = {
      id: Date.now(), pacienteId: this.paciente.id,
      fecha: new Date().toISOString().slice(0, 10), profesional: 'Dra. Laura Méndez',
      motivo: datos.motivo!.trim(), diagnostico: datos.diagnostico!.trim(),
      tratamiento: datos.tratamiento!, observaciones: datos.observaciones || ''
    };
    this.historias.agregarRegistro(registro);
    this.registros = this.historias.obtenerRegistros(this.paciente.id);
    this.formulario.reset(); this.mostrarFormulario = false;
    this.mensaje = 'La consulta se guardó correctamente.';
  }
}
