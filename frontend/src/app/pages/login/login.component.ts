
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiProblem } from '../../api/schema';
import { mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Formulario reactivo: Angular controla los valores y las validaciones.
  formularioLogin = new FormGroup({
    correo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    contrasena: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] })
  });
  /** Código de la app autenticadora, sólo para cuentas con verificación en dos pasos. */
  codigo = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{6}$/)] });
  private desafio: string | null = null;
  get pideCodigo(): boolean { return this.desafio !== null; }
  mostrarContrasena = false;
  enviando = false;
  mensaje = '';

  cambiarVisibilidadContrasena(): void {
    this.mostrarContrasena = !this.mostrarContrasena;
  }

  recuperarContrasena(): void {
    const correo = this.formularioLogin.controls.correo;
    if (correo.invalid) {
      correo.markAsTouched();
      this.mensaje = 'Escribí tu correo electrónico y volvé a tocar "¿Olvidaste tu contraseña?".';
      return;
    }
    this.auth.recuperarContrasena(correo.value).subscribe({
      next: () => (this.mensaje = 'Si el correo pertenece a una cuenta, vas a recibir un enlace para restablecer la contraseña.'),
      error: error => (this.mensaje = mensajeDeError(error, 'No se pudo enviar el correo de recuperación.'))
    });
  }

  private entrar(): void {
    const volver = this.route.snapshot.queryParamMap.get('volver');
    this.router.navigateByUrl(volver && volver.startsWith('/') && !volver.startsWith('//') ? volver : '/dashboard');
  }

  verificarCodigo(): void {
    if (!this.desafio || this.codigo.invalid) { this.codigo.markAsTouched(); this.mensaje = 'El código tiene 6 dígitos.'; return; }
    this.enviando = true;
    this.auth.completarDosFactores(this.desafio, this.codigo.value).subscribe({
      next: () => { this.enviando = false; this.entrar(); },
      error: error => { this.enviando = false; this.codigo.reset(''); this.mensaje = mensajeDeError(error, 'El código no es correcto o ya venció.'); }
    });
  }

  volverAlInicio(): void { this.desafio = null; this.mensaje = ''; }

  ingresar(): void {
    if (this.formularioLogin.invalid) {
      this.formularioLogin.markAllAsTouched();
      this.mensaje = 'Revisá el correo electrónico y la contraseña para continuar.';
      return;
    }

    const { correo, contrasena } = this.formularioLogin.getRawValue();
    this.enviando = true;
    this.mensaje = '';
    this.auth.iniciarSesion(correo.trim(), contrasena).subscribe({
      next: respuesta => {
        this.enviando = false;
        if (respuesta.requiresTwoFactor) {
          this.desafio = respuesta.twoFactorChallengeToken ?? null;
          this.codigo.reset('');
          this.mensaje = 'Ingresá el código de 6 dígitos de tu aplicación autenticadora.';
          return;
        }
        this.entrar();
      },
      error: (error: HttpErrorResponse) => {
        this.enviando = false;
        this.formularioLogin.controls.contrasena.reset('');
        const codigo = (error.error as ApiProblem | null)?.code;
        if (codigo === 'invalid_credentials') this.mensaje = 'El correo o la contraseña no son correctos.';
        else if (codigo === 'email_not_confirmed') this.mensaje = 'Todavía no confirmaste tu correo electrónico. Revisá tu casilla y seguí el enlace de confirmación.';
        else this.mensaje = mensajeDeError(error, 'No se pudo iniciar sesión.');
      }
    });
  }
}
