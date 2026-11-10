
import { Component, Input, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InvitationInfoDto } from '../../api/schema';
import { mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

// Pantallas públicas de cuenta. Tres de ellas se abren desde los enlaces que el backend envía por correo
// (Frontend:ConfirmEmailPath, ResetPasswordPath y AcceptInvitationPath en la configuración de la API).

/** Misma política que el backend: 10 caracteres o más, con mayúscula, minúscula y número. */
const CONTRASENA = [Validators.required, Validators.minLength(10), Validators.maxLength(128), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/)];
const AYUDA_CONTRASENA = 'Al menos 10 caracteres, con una mayúscula, una minúscula y un número.';
const obligatorio = (maximo: number) => new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(maximo)] });

/** Tarjeta común de las pantallas de cuenta: marca, título, contenido y mensajes. */
@Component({
  selector: 'app-marco-cuenta', standalone: true, imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <main class="od-auth">
      <section class="od-auth-card">
        <a class="od-auth-brand" routerLink="/login">Odontia</a>
        <h1>{{ titulo }}</h1>
        <ng-content />
        @if (mensaje) {
          <p class="od-feedback" role="status">{{ mensaje }}</p>
        }
        @if (error) {
          <p class="od-feedback error" role="alert">{{ error }}</p>
        }
        <a class="od-auth-back" routerLink="/login">Ir al inicio de sesión</a>
      </section>
    </main>`
})
export class MarcoCuentaComponent {
  @Input({ required: true }) titulo = '';
  @Input() mensaje = '';
  @Input() error = '';
}

@Component({
  selector: 'app-confirmar-correo', standalone: true, imports: [ReactiveFormsModule, MarcoCuentaComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-marco-cuenta titulo="Confirmar correo" [mensaje]="mensaje" [error]="error">
      @if (estado === 'verificando') {
        <p>Estamos confirmando tu correo electrónico…</p>
      }
      @if (estado === 'fallo') {
        <form class="od-form" (ngSubmit)="reenviar()">
          <p>El enlace no es válido o ya venció. Podés pedir uno nuevo:</p>
          <label>Correo electrónico<input type="email" autocomplete="email" [formControl]="correo"></label>
          <button type="submit" class="od-button" [disabled]="enviando">Enviar un enlace nuevo</button>
        </form>
      }</app-marco-cuenta>`
})
export class ConfirmarCorreoComponent {
  private readonly auth = inject(AuthService);
  estado: 'verificando' | 'listo' | 'fallo' = 'verificando';
  correo = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] });
  enviando = false;
  mensaje = '';
  error = '';

  constructor() {
    const parametros = inject(ActivatedRoute).snapshot.queryParamMap;
    const userId = parametros.get('userId'), token = parametros.get('token');
    if (!userId || !token) { this.estado = 'fallo'; return; }
    this.auth.confirmarCorreo(userId, token).subscribe({
      next: () => { this.estado = 'listo'; this.mensaje = 'Tu correo quedó confirmado. Ya podés iniciar sesión.'; },
      error: () => (this.estado = 'fallo')
    });
  }

  reenviar(): void {
    if (this.correo.invalid) { this.correo.markAsTouched(); this.error = 'Escribí un correo electrónico válido.'; return; }
    this.enviando = true;
    this.error = '';
    this.auth.reenviarConfirmacion(this.correo.value.trim()).subscribe({
      next: () => { this.enviando = false; this.mensaje = 'Si el correo pertenece a una cuenta sin confirmar, te enviamos un enlace nuevo.'; },
      error: error => { this.enviando = false; this.error = mensajeDeError(error, 'No se pudo enviar el enlace.'); }
    });
  }
}

@Component({
  selector: 'app-restablecer-contrasena', standalone: true, imports: [ReactiveFormsModule, MarcoCuentaComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-marco-cuenta titulo="Restablecer contraseña" [mensaje]="mensaje" [error]="error">
      @if (!listo) {
        <form class="od-form" [formGroup]="formulario" (ngSubmit)="guardar()">
          @if (!enlaceValido) {
            <p>Este enlace no es válido. Pedí uno nuevo desde "¿Olvidaste tu contraseña?" en el inicio de sesión.</p>
          }
          @if (enlaceValido) {
            <label>Contraseña nueva<input type="password" autocomplete="new-password" formControlName="nueva"></label>
            <label>Repetir la contraseña<input type="password" autocomplete="new-password" formControlName="repetida"></label>
            <p class="od-note">{{ ayuda }}</p>
            <button type="submit" class="od-button" [disabled]="enviando">{{ enviando ? 'Guardando…' : 'Guardar la contraseña nueva' }}</button>
          }
        </form>
      }</app-marco-cuenta>`
})
export class RestablecerContrasenaComponent {
  private readonly auth = inject(AuthService);
  readonly ayuda = AYUDA_CONTRASENA;
  private readonly parametros = inject(ActivatedRoute).snapshot.queryParamMap;
  readonly enlaceValido = !!this.parametros.get('email') && !!this.parametros.get('token');
  formulario = new FormGroup({
    nueva: new FormControl('', { nonNullable: true, validators: CONTRASENA }),
    repetida: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });
  enviando = false;
  listo = false;
  mensaje = '';
  error = '';

  guardar(): void {
    const { nueva, repetida } = this.formulario.getRawValue();
    if (this.formulario.invalid) { this.error = AYUDA_CONTRASENA; return; }
    if (nueva !== repetida) { this.error = 'Las dos contraseñas no coinciden.'; return; }
    this.enviando = true;
    this.error = '';
    this.auth.restablecerContrasena(this.parametros.get('email')!, this.parametros.get('token')!, nueva).subscribe({
      next: () => { this.enviando = false; this.listo = true; this.mensaje = 'La contraseña se cambió. Ya podés iniciar sesión con la nueva.'; },
      error: error => { this.enviando = false; this.error = mensajeDeError(error, 'No se pudo cambiar la contraseña. El enlace puede haber vencido.'); }
    });
  }
}

@Component({
  selector: 'app-aceptar-invitacion', standalone: true, imports: [ReactiveFormsModule, MarcoCuentaComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-marco-cuenta titulo="Aceptar invitación" [mensaje]="mensaje" [error]="error">
      @if (cargando) {
        <p>Buscando tu invitación…</p>
      }
      @if (invitacion; as i) {
        <form class="od-form" [hidden]="listo" [formGroup]="formulario" (ngSubmit)="aceptar()">
          <p>Te invitaron a <strong>{{ i.consultorioName }}</strong> como <strong>{{ i.roles.join(', ') }}</strong> con el correo {{ i.email }}.</p>
          @if (!i.userExists) {
            <div class="od-form-row">
              <label>Nombre<input type="text" autocomplete="given-name" formControlName="nombre"></label>
              <label>Apellido<input type="text" autocomplete="family-name" formControlName="apellido"></label>
            </div>
          }
          <label>{{ i.userExists ? 'Tu contraseña actual de Odontia' : 'Elegí una contraseña' }}<input type="password" [attr.autocomplete]="i.userExists ? 'current-password' : 'new-password'" formControlName="contrasena"></label>
          @if (!i.userExists) {
            <p class="od-note">{{ ayuda }}</p>
          }
          <button type="submit" class="od-button" [disabled]="enviando">{{ enviando ? 'Aceptando…' : 'Aceptar la invitación' }}</button>
        </form>
      }</app-marco-cuenta>`
})
export class AceptarInvitacionComponent {
  private readonly auth = inject(AuthService);
  readonly ayuda = AYUDA_CONTRASENA;
  private readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';
  invitacion: InvitationInfoDto | null = null;
  formulario = new FormGroup({ nombre: obligatorio(80), apellido: obligatorio(80), contrasena: new FormControl('', { nonNullable: true, validators: [Validators.required] }) });
  cargando = true;
  enviando = false;
  listo = false;
  mensaje = '';
  error = '';

  constructor() {
    if (!this.token) { this.cargando = false; this.error = 'Este enlace de invitación no es válido.'; return; }
    this.auth.invitacion(this.token).subscribe({
      next: invitacion => {
        this.cargando = false;
        this.invitacion = invitacion;
        if (invitacion.userExists) { this.formulario.controls.nombre.disable(); this.formulario.controls.apellido.disable(); }
        else this.formulario.controls.contrasena.addValidators(CONTRASENA);
      },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'La invitación no existe o ya venció. Pedile al administrador que te envíe otra.'); }
    });
  }

  aceptar(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); this.error = this.invitacion?.userExists ? 'Ingresá tu contraseña.' : `Completá tu nombre y apellido. ${AYUDA_CONTRASENA}`; return; }
    const datos = this.formulario.getRawValue();
    const nuevo = !this.invitacion?.userExists;
    this.enviando = true;
    this.error = '';
    this.auth.aceptarInvitacion(this.token, datos.contrasena, nuevo ? datos.nombre.trim() : null, nuevo ? datos.apellido.trim() : null).subscribe({
      next: () => { this.enviando = false; this.listo = true; this.mensaje = 'Listo: ya formás parte del consultorio. Iniciá sesión para empezar.'; },
      error: error => { this.enviando = false; this.error = mensajeDeError(error, 'No se pudo aceptar la invitación.'); }
    });
  }
}

@Component({
  selector: 'app-registro', standalone: true, imports: [ReactiveFormsModule, MarcoCuentaComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-marco-cuenta titulo="Crear una cuenta" [mensaje]="mensaje" [error]="error">
      @if (!listo) {
        <form class="od-form" [formGroup]="formulario" (ngSubmit)="registrar()">
          <p>Registrá tu consultorio. Vas a ser su administrador y después podés invitar al resto del equipo.</p>
          <label>Nombre del consultorio<input type="text" autocomplete="organization" formControlName="consultorio"></label>
          <div class="od-form-row">
            <label>Tu nombre<input type="text" autocomplete="given-name" formControlName="nombre"></label>
            <label>Tu apellido<input type="text" autocomplete="family-name" formControlName="apellido"></label>
          </div>
          <label>Correo electrónico<input type="email" autocomplete="email" formControlName="correo"></label>
          <label>Contraseña<input type="password" autocomplete="new-password" formControlName="contrasena"></label>
          <p class="od-note">{{ ayuda }}</p>
          <label class="check"><input type="checkbox" formControlName="profesional"> Soy odontólogo/a y atiendo pacientes</label>
          <button type="submit" class="od-button" [disabled]="enviando">{{ enviando ? 'Creando la cuenta…' : 'Crear la cuenta' }}</button>
        </form>
      }</app-marco-cuenta>`
})
export class RegistroComponent {
  private readonly auth = inject(AuthService);
  readonly ayuda = AYUDA_CONTRASENA;
  formulario = new FormGroup({
    consultorio: obligatorio(150), nombre: obligatorio(80), apellido: obligatorio(80),
    correo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    contrasena: new FormControl('', { nonNullable: true, validators: CONTRASENA }),
    profesional: new FormControl(true, { nonNullable: true })
  });
  enviando = false;
  listo = false;
  mensaje = '';
  error = '';

  registrar(): void {
    if (this.formulario.invalid) { this.formulario.markAllAsTouched(); this.error = `Completá todos los campos con un correo válido. ${AYUDA_CONTRASENA}`; return; }
    const datos = this.formulario.getRawValue();
    this.enviando = true;
    this.error = '';
    this.auth.registrar({
      email: datos.correo.trim(), password: datos.contrasena, firstName: datos.nombre.trim(), lastName: datos.apellido.trim(),
      consultorioName: datos.consultorio.trim(), isProfessional: datos.profesional,
      timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone
    }).subscribe({
      next: () => { this.enviando = false; this.listo = true; this.mensaje = 'Te enviamos un correo para confirmar la cuenta. Abrí el enlace y después iniciá sesión.'; },
      error: error => { this.enviando = false; this.error = mensajeDeError(error, 'No se pudo crear la cuenta. El registro público puede estar deshabilitado: pedile una invitación a un administrador.'); }
    });
  }
}
