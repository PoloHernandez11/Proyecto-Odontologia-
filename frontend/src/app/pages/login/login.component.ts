
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { codigoDeError, mensajeDeError } from '../../core/api';
import { MotivoDeCierre } from '../../core/auth.interceptor';
import { AuthService } from '../../core/auth.service';

const MOTIVOS_DE_CIERRE: Record<MotivoDeCierre, string> = {
  cerrada: 'Tu sesión se cerró porque se finalizó desde otro dispositivo o fue revocada. Ingresá de nuevo para continuar.',
  vencida: 'Tu sesión venció. Ingresá de nuevo para continuar.'
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  /** Envuelve el código: sin [formGroup] el formulario no emite (ngSubmit) (no se usa FormsModule). */
  readonly formularioCodigo = new FormGroup({ codigo: this.codigo });
  private readonly desafio = signal<string | null>(null);
  readonly pideCodigo = computed(() => this.desafio() !== null);
  readonly mostrarContrasena = signal(false);
  readonly enviando = signal(false);
  readonly mensaje = signal(MOTIVOS_DE_CIERRE[this.route.snapshot.queryParamMap.get('sesion') as MotivoDeCierre] ?? '');

  cambiarVisibilidadContrasena(): void {
    this.mostrarContrasena.update(visible => !visible);
  }

  recuperarContrasena(): void {
    const correo = this.formularioLogin.controls.correo;
    if (correo.invalid) {
      correo.markAsTouched();
      this.mensaje.set('Escribí tu correo electrónico y volvé a tocar "¿Olvidaste tu contraseña?".');
      return;
    }
    this.auth.recuperarContrasena(correo.value).subscribe({
      next: () => this.mensaje.set('Si el correo pertenece a una cuenta, vas a recibir un enlace para restablecer la contraseña.'),
      error: error => this.mensaje.set(mensajeDeError(error, 'No se pudo enviar el correo de recuperación.'))
    });
  }

  private entrar(): void {
    const volver = this.route.snapshot.queryParamMap.get('volver');
    this.router.navigateByUrl(volver && volver.startsWith('/') && !volver.startsWith('//') ? volver : '/dashboard');
  }

  verificarCodigo(): void {
    const desafio = this.desafio();
    if (!desafio || this.codigo.invalid) { this.codigo.markAsTouched(); this.mensaje.set('El código tiene 6 dígitos.'); return; }
    this.enviando.set(true);
    this.auth.completarDosFactores(desafio, this.codigo.value).subscribe({
      next: () => { this.enviando.set(false); this.entrar(); },
      error: error => {
        this.enviando.set(false);
        this.codigo.reset('');
        const codigo = codigoDeError(error);
        if (codigo === 'two_factor_code_reused') {
          this.mensaje.set('Ese código ya se usó; esperá el siguiente código de tu aplicación autenticadora.');
        } else if (codigo === 'two_factor_expired') {
          // El desafío ya se usó o venció: hay que volver a ingresar el correo y la contraseña.
          this.desafio.set(null);
          this.formularioLogin.controls.contrasena.reset('');
          this.mensaje.set('La verificación venció. Ingresá de nuevo tu correo y tu contraseña.');
        } else {
          this.mensaje.set(mensajeDeError(error, 'El código no es correcto o ya venció.'));
        }
      }
    });
  }

  volverAlInicio(): void { this.desafio.set(null); this.mensaje.set(''); }

  ingresar(): void {
    if (this.formularioLogin.invalid) {
      this.formularioLogin.markAllAsTouched();
      this.mensaje.set('Revisá el correo electrónico y la contraseña para continuar.');
      return;
    }

    const { correo, contrasena } = this.formularioLogin.getRawValue();
    this.enviando.set(true);
    this.mensaje.set('');
    this.auth.iniciarSesion(correo.trim(), contrasena).subscribe({
      next: respuesta => {
        this.enviando.set(false);
        if (respuesta.requiresTwoFactor) {
          this.desafio.set(respuesta.twoFactorChallengeToken ?? null);
          this.codigo.reset('');
          this.mensaje.set('Ingresá el código de 6 dígitos de tu aplicación autenticadora.');
          return;
        }
        this.entrar();
      },
      error: error => {
        this.enviando.set(false);
        this.formularioLogin.controls.contrasena.reset('');
        const codigo = codigoDeError(error);
        if (codigo === 'invalid_credentials') this.mensaje.set('El correo o la contraseña no son correctos.');
        else if (codigo === 'email_not_confirmed') this.mensaje.set('Todavía no confirmaste tu correo electrónico. Revisá tu casilla y seguí el enlace de confirmación.');
        else this.mensaje.set(mensajeDeError(error, 'No se pudo iniciar sesión.'));
      }
    });
  }
}
