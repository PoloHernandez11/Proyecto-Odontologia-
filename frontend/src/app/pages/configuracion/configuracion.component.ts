
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { OdontiaApi } from '../../api/odontia-api.service';
import { ConsultorioDto, DayOfWeek, NotificationSettingsDto, ProfileDto, WorkingHoursDto } from '../../api/schema';
import { iniciales, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

type Seccion = 'datos' | 'profesional' | 'horario' | 'seguridad' | 'notificaciones' | 'consultorio';
interface Franja { inicio: string; fin: string; }
interface DiaDeAtencion { dia: DayOfWeek; nombre: string; franjas: Franja[]; }
const DIAS: { dia: DayOfWeek; nombre: string }[] = [
  { dia: 'Monday', nombre: 'Lunes' }, { dia: 'Tuesday', nombre: 'Martes' }, { dia: 'Wednesday', nombre: 'Miércoles' },
  { dia: 'Thursday', nombre: 'Jueves' }, { dia: 'Friday', nombre: 'Viernes' }, { dia: 'Saturday', nombre: 'Sábado' }, { dia: 'Sunday', nombre: 'Domingo' }
];
const DIAS_HABILES: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];
const texto = (maximo: number, ...extra: ((control: FormControl<string>) => unknown)[]) =>
  new FormControl('', { nonNullable: true, validators: [Validators.maxLength(maximo), ...(extra as [])] });

const NOTIFICACIONES: { clave: keyof NotificationSettingsDto; titulo: string; detalle: string }[] = [
  { clave: 'emailEnabled', titulo: 'Avisos por correo electrónico', detalle: 'Recibir las notificaciones también en tu casilla de correo.' },
  { clave: 'inAppEnabled', titulo: 'Avisos dentro de Odontia', detalle: 'Mostrar las notificaciones en la aplicación.' },
  { clave: 'notifyTaskAssigned', titulo: 'Tareas asignadas', detalle: 'Cuando alguien te asigna una tarea.' },
  { clave: 'notifyTaskDue', titulo: 'Tareas por vencer', detalle: 'Cuando una tarea tuya está por vencer o ya venció.' },
  { clave: 'notifyLabUpdates', titulo: 'Novedades de laboratorio', detalle: 'Cuando un trabajo cambia de estado o se atrasa.' },
  { clave: 'notifyAppointmentChanges', titulo: 'Cambios en turnos', detalle: 'Cuando se agenda, reprograma o cancela un turno tuyo.' },
  { clave: 'dailyAgendaSummary', titulo: 'Resumen diario de agenda', detalle: 'Un resumen con los turnos del día.' }
];

@Component({
  selector: 'app-configuracion', standalone: true,
  imports: [ReactiveFormsModule, FormsModule],
  templateUrl: './configuracion.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './configuracion.component.css'
})
export class ConfiguracionComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly notificaciones = NOTIFICACIONES;
  readonly esProfesional = this.auth.esProfesional;
  readonly roles = this.auth.roles;
  readonly nombreConsultorio = this.auth.consultorio;
  readonly puedeEditarConsultorio = this.auth.tiene('consultorio.manage');
  readonly veConsultorio = this.auth.tiene('consultorio.read');
  readonly puedeConfigurarHorario = this.auth.tiene('agenda.manage');

  /** Horario semanal de atención del profesional: la agenda sólo acepta turnos dentro de estas franjas. */
  horario: DiaDeAtencion[] = DIAS.map(d => ({ ...d, franjas: [] }));
  horarioCargado = false;
  seccion: Seccion = 'datos';
  perfil: ProfileDto | null = null;
  consultorio: ConsultorioDto | null = null;
  ajustes: NotificationSettingsDto | null = null;
  cargando = true;
  guardando = false;
  mensaje = '';
  error = '';
  verContrasenas = false;

  datos = new FormGroup({
    nombre: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    apellido: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    tratamiento: texto(20),
    telefono: texto(40, Validators.pattern(/^[0-9+()\-\s]*$/))
  });
  profesional = new FormGroup({
    especialidad: texto(120),
    matricula: texto(40),
    duracion: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(5), Validators.max(480)] })
  });
  contrasena = new FormGroup({
    actual: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    nueva: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(10)] }),
    repetida: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });
  datosConsultorio = new FormGroup({
    nombre: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    telefono: texto(40), email: texto(254, Validators.email), direccion: texto(250), ciudad: texto(100), provincia: texto(100)
  });

  constructor() {
    this.api.perfil().subscribe({
      next: perfil => { this.cargando = false; this.aplicarPerfil(perfil); },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo cargar tu perfil.'); }
    });
    if (this.veConsultorio) this.api.consultorio().subscribe({ next: consultorio => this.aplicarConsultorio(consultorio) });
    const usuario = this.auth.userId;
    if (this.esProfesional() && this.puedeConfigurarHorario && usuario) {
      this.api.horarioDeAtencion(usuario).subscribe({ next: franjas => this.aplicarHorario(franjas) });
    }
  }

  private aplicarHorario(franjas: WorkingHoursDto[]): void {
    this.horario = DIAS.map(d => ({
      ...d,
      franjas: franjas.filter(f => f.dayOfWeek === d.dia).map(f => ({ inicio: f.startTime.slice(0, 5), fin: f.endTime.slice(0, 5) }))
    }));
    this.horarioCargado = true;
  }

  get sinHorario(): boolean { return this.horarioCargado && this.horario.every(d => d.franjas.length === 0); }
  agregarFranja(dia: DiaDeAtencion): void {
    const ultima = dia.franjas[dia.franjas.length - 1];
    dia.franjas.push(ultima ? { inicio: ultima.fin, fin: ultima.fin < '20:00' ? '20:00' : '23:00' } : { inicio: '09:00', fin: '18:00' });
  }
  quitarFranja(dia: DiaDeAtencion, indice: number): void { dia.franjas.splice(indice, 1); }
  /** Copia las franjas del lunes al resto de los días hábiles. */
  copiarLunesASemana(): void {
    const lunes = this.horario.find(d => d.dia === 'Monday')!;
    for (const dia of this.horario) if (dia !== lunes && DIAS_HABILES.includes(dia.dia)) dia.franjas = lunes.franjas.map(f => ({ ...f }));
  }

  guardarHorario(): void {
    const usuario = this.auth.userId;
    if (!usuario) return;
    const invalida = this.horario.find(d => d.franjas.some(f => !f.inicio || !f.fin || f.fin <= f.inicio));
    if (invalida) { this.mensaje = ''; this.error = `Revisá las franjas del ${invalida.nombre.toLowerCase()}: la hora de fin debe ser posterior a la de inicio.`; return; }
    this.guardando = true;
    const slots = this.horario.flatMap(d => d.franjas.map(f => ({ dayOfWeek: d.dia, startTime: `${f.inicio}:00`, endTime: `${f.fin}:00`, gabineteId: null })));
    this.api.guardarHorarioDeAtencion(usuario, slots).subscribe({
      next: franjas => { this.aplicarHorario(franjas); this.exito('Tu horario de atención se guardó. La agenda ya acepta turnos en esas franjas.'); },
      error: error => this.fallo(error, 'No se pudo guardar el horario de atención.')
    });
  }

  private aplicarPerfil(perfil: ProfileDto): void {
    this.perfil = perfil;
    this.ajustes = { ...perfil.notificationSettings };
    this.datos.reset({ nombre: perfil.firstName, apellido: perfil.lastName, tratamiento: perfil.title ?? '', telefono: perfil.phone ?? '' });
    this.profesional.reset({ especialidad: perfil.specialty ?? '', matricula: perfil.licenseNumber ?? '', duracion: perfil.defaultAppointmentMinutes ?? 30 });
  }
  private aplicarConsultorio(consultorio: ConsultorioDto): void {
    this.consultorio = consultorio;
    this.datosConsultorio.reset({
      nombre: consultorio.name, telefono: consultorio.phone ?? '', email: consultorio.email ?? '',
      direccion: consultorio.addressLine ?? '', ciudad: consultorio.city ?? '', provincia: consultorio.province ?? ''
    });
    if (!this.puedeEditarConsultorio) this.datosConsultorio.disable();
  }

  get iniciales(): string { return iniciales(this.perfil?.displayName ?? this.auth.nombre()); }
  /** Requisitos de la contraseña nueva, para mostrarlos mientras se escribe. */
  get requisitos(): { titulo: string; cumple: boolean }[] {
    const valor = this.contrasena.controls.nueva.value;
    return [
      { titulo: 'Al menos 10 caracteres', cumple: valor.length >= 10 },
      { titulo: 'Una mayúscula y una minúscula', cumple: /[a-z]/.test(valor) && /[A-Z]/.test(valor) },
      { titulo: 'Un número', cumple: /\d/.test(valor) },
      { titulo: 'Coincide con la repetición', cumple: valor.length > 0 && valor === this.contrasena.controls.repetida.value }
    ];
  }

  abrir(seccion: Seccion): void { this.seccion = seccion; this.mensaje = ''; this.error = ''; }
  private exito(mensaje: string): void { this.guardando = false; this.error = ''; this.mensaje = mensaje; }
  private fallo(error: unknown, porDefecto: string): void { this.guardando = false; this.mensaje = ''; this.error = mensajeDeError(error, porDefecto); }

  guardarDatos(): void {
    if (this.datos.invalid) { this.datos.markAllAsTouched(); return; }
    const valores = this.datos.getRawValue();
    this.guardando = true;
    this.api.actualizarPerfil({ firstName: valores.nombre.trim(), lastName: valores.apellido.trim(), title: valores.tratamiento.trim() || null, phone: valores.telefono.trim() || null }).subscribe({
      next: perfil => {
        this.aplicarPerfil(perfil);
        this.exito('Tus datos se guardaron correctamente.');
        // Renueva la sesión para que el nombre nuevo se vea en el menú.
        this.auth.renovar().subscribe({ error: () => undefined });
      },
      error: error => this.fallo(error, 'No se pudieron guardar tus datos.')
    });
  }

  guardarProfesional(): void {
    if (this.profesional.invalid) { this.profesional.markAllAsTouched(); return; }
    const valores = this.profesional.getRawValue();
    this.guardando = true;
    this.api.actualizarPerfilProfesional({ specialty: valores.especialidad.trim() || null, licenseNumber: valores.matricula.trim() || null, defaultAppointmentMinutes: Number(valores.duracion) }).subscribe({
      next: perfil => { this.aplicarPerfil(perfil); this.exito('Tus datos profesionales se guardaron correctamente.'); },
      error: error => this.fallo(error, 'No se pudieron guardar tus datos profesionales.')
    });
  }

  cambiarContrasena(): void {
    const valores = this.contrasena.getRawValue();
    if (this.contrasena.invalid || this.requisitos.some(requisito => !requisito.cumple)) {
      this.contrasena.markAllAsTouched();
      this.mensaje = '';
      this.error = 'Revisá que la contraseña nueva cumpla todos los requisitos.';
      return;
    }
    this.guardando = true;
    this.api.cambiarContrasena(valores.actual, valores.nueva).subscribe({
      next: () => { this.contrasena.reset(); this.exito('La contraseña se cambió. Se cerraron tus otras sesiones abiertas.'); },
      error: error => this.fallo(error, 'No se pudo cambiar la contraseña. Revisá la contraseña actual.')
    });
  }

  alternar(clave: keyof NotificationSettingsDto): void {
    if (!this.ajustes) return;
    const anterior = this.ajustes;
    this.ajustes = { ...anterior, [clave]: !anterior[clave] };
    this.api.actualizarNotificaciones(this.ajustes).subscribe({
      next: ajustes => { this.ajustes = ajustes; this.exito('Preferencias de notificación actualizadas.'); },
      error: error => { this.ajustes = anterior; this.fallo(error, 'No se pudo guardar la preferencia.'); }
    });
  }

  guardarConsultorio(): void {
    if (!this.consultorio || this.datosConsultorio.invalid) { this.datosConsultorio.markAllAsTouched(); return; }
    const valores = this.datosConsultorio.getRawValue();
    const actual = this.consultorio;
    this.guardando = true;
    this.api.actualizarConsultorio({
      name: valores.nombre.trim(), phone: valores.telefono.trim() || null, email: valores.email.trim() || null,
      addressLine: valores.direccion.trim() || null, city: valores.ciudad.trim() || null, province: valores.provincia.trim() || null,
      // Campos que esta pantalla no edita: se conservan.
      legalName: actual.legalName, taxId: actual.taxId, website: actual.website, postalCode: actual.postalCode, country: actual.country
    }).subscribe({
      next: consultorio => { this.aplicarConsultorio(consultorio); this.exito('Los datos del consultorio se guardaron correctamente.'); this.auth.renovar().subscribe({ error: () => undefined }); },
      error: error => this.fallo(error, 'No se pudieron guardar los datos del consultorio.')
    });
  }

  cerrarSesion(): void { this.auth.cerrarSesion().subscribe(() => this.router.navigate(['/login'])); }
}
