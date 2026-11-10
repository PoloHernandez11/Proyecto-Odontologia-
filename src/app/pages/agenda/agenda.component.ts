import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Turno } from '../../models/turno.model';
import { TurnoService } from '../../services/turno.service';
import { PerfilService } from '../../services/perfil.service';

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './agenda.component.html',
  styleUrl: './agenda.component.css'
})
export class AgendaComponent {
  tituloPagina = 'Agenda';
  perfil = 'secretaria';
  nombreUsuario = 'Carla';
  vista: 'dia' | 'semana' | 'mes' = 'dia';
  fechaActual = new Date();
  busqueda = '';
  profesionalSeleccionado = 'Todos';
  turnoSeleccionado: Turno | null = null;
  mostrarFormulario = false;
  mensaje = '';
  turnos: Turno[] = [];

  formularioTurno = new FormGroup({
    paciente: new FormControl('', [Validators.required, Validators.minLength(3)]),
    telefono: new FormControl('', [Validators.required, Validators.pattern(/^[0-9+() .-]{6,20}$/)]),
    fecha: new FormControl(this.fechaLocal(new Date()), [Validators.required]),
    hora: new FormControl('09:00', [Validators.required]),
    profesional: new FormControl('Dra. Laura Méndez', [Validators.required]),
    consultorio: new FormControl('Consultorio 1', [Validators.required]),
    tratamiento: new FormControl('', [Validators.required])
  });

  constructor(private turnoService: TurnoService, private route: ActivatedRoute, private perfilService: PerfilService) {
    this.turnos = this.turnoService.obtenerTodos();
    this.tituloPagina = this.route.snapshot.routeConfig?.path === 'turnos-tareas' ? 'Turnos y tareas' : 'Agenda';
    this.route.queryParamMap.subscribe(parametros => {
      const guardado = this.perfilService.obtener();
      const perfilUrl = parametros.get('perfil');
      this.perfil = perfilUrl === 'secretaria' || perfilUrl === 'odontologo' ? perfilUrl : guardado.perfil;
      this.nombreUsuario = guardado.nombre || (this.perfil === 'odontologo' ? 'Dra. Laura Méndez' : 'Carla');
      if (parametros.get('nuevo') === '1') this.abrirFormulario();
    });
  }

  get turnosFiltrados(): Turno[] {
    const texto = this.busqueda.toLowerCase().trim();
    return this.turnos.filter(turno => {
      const fechaTurno = new Date(`${turno.fecha}T12:00:00`);
      const inicioSemana = new Date(this.fechaActual);
      inicioSemana.setDate(inicioSemana.getDate() - ((inicioSemana.getDay() + 6) % 7));
      inicioSemana.setHours(0, 0, 0, 0);
      const finSemana = new Date(inicioSemana); finSemana.setDate(finSemana.getDate() + 7);
      const coincideFecha = this.vista === 'dia' ? turno.fecha === this.fechaEnFormato : this.vista === 'semana' ? fechaTurno >= inicioSemana && fechaTurno < finSemana : fechaTurno.getFullYear() === this.fechaActual.getFullYear() && fechaTurno.getMonth() === this.fechaActual.getMonth();
      const coincideProfesional = this.profesionalSeleccionado === 'Todos' || turno.profesional === this.profesionalSeleccionado;
      const coincideTexto = turno.paciente.toLowerCase().includes(texto) || turno.telefono.includes(texto);
      return coincideFecha && coincideProfesional && coincideTexto && turno.estado !== 'Cancelado';
    });
  }

  private fechaLocal(fecha: Date): string {
    return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
  }

  get fechaEnFormato(): string {
    return this.fechaLocal(this.fechaActual);
  }

  get fechaTitulo(): string {
    return new Intl.DateTimeFormat('es-AR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(this.fechaActual);
  }

  cambiarDia(cantidad: number): void {
    const nuevaFecha = new Date(this.fechaActual);
    nuevaFecha.setDate(nuevaFecha.getDate() + cantidad);
    this.fechaActual = nuevaFecha;
    this.turnoSeleccionado = null;
  }

  irHoy(): void { this.fechaActual = new Date(); this.turnoSeleccionado = null; }
  seleccionarVista(vista: 'dia' | 'semana' | 'mes'): void { this.vista = vista; this.turnoSeleccionado = null; }
  seleccionarTurno(turno: Turno): void { this.turnoSeleccionado = turno; this.mensaje = ''; }
  abrirFormulario(): void { this.formularioTurno.reset({ paciente: '', telefono: '', fecha: this.fechaEnFormato, hora: '09:00', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: '' }); this.mostrarFormulario = true; this.mensaje = ''; }
  cerrarFormulario(): void { this.mostrarFormulario = false; }

  guardarTurno(): void {
    if (this.formularioTurno.invalid) { this.formularioTurno.markAllAsTouched(); return; }
    const datos = this.formularioTurno.getRawValue();
    const nuevoTurno: Turno = {
      id: Math.max(0, ...this.turnos.map(turno => turno.id)) + 1,
      fecha: datos.fecha!, hora: datos.hora!, duracion: 45,
      paciente: datos.paciente!, telefono: datos.telefono!,
      profesional: datos.profesional!, consultorio: datos.consultorio!,
      tratamiento: datos.tratamiento!, estado: 'Pendiente'
    };
    const turnoGuardado = this.turnoService.agregar(nuevoTurno);
    this.turnos = this.turnoService.obtenerTodos();
    this.turnoSeleccionado = turnoGuardado;
    this.mostrarFormulario = false;
    this.mensaje = 'El turno se agregó correctamente.';
  }

  cambiarEstado(estado: Turno['estado']): void {
    if (!this.turnoSeleccionado) return;
    this.turnoSeleccionado.estado = estado;
    this.turnoService.actualizar(this.turnoSeleccionado);
    this.turnos = this.turnoService.obtenerTodos();
    this.mensaje = `El turno ahora figura como: ${estado}.`;
  }
}
