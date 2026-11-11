import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PerfilService } from '../../services/perfil.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  constructor(private router: Router, private perfilService: PerfilService) { this.perfilSeleccionado = this.perfilService.obtener().perfil; }
  // Perfil seleccionado en la pantalla de inicio de sesión.
  perfilSeleccionado = 'odontologo';

  // Formulario reactivo: Angular controla los valores y las validaciones.
  formularioLogin = new FormGroup({
    correo: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    contrasena: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)]
    }),
    recordar: new FormControl(true, { nonNullable: true })
  });
  mostrarContrasena = false;
  mensaje = '';

  seleccionarPerfil(perfil: string): void {
    this.perfilSeleccionado = perfil;
    this.mensaje = '';
  }

  cambiarVisibilidadContrasena(): void {
    this.mostrarContrasena = !this.mostrarContrasena;
  }

  recuperarContrasena(): void {
    this.mensaje = 'La recuperación de contraseña estará disponible cuando conectemos el sistema con el servidor.';
  }

  ingresar(): void {
    if (this.formularioLogin.invalid) {
      this.formularioLogin.markAllAsTouched();
      this.mensaje = 'Revisá el correo electrónico y la contraseña para continuar.';
      return;
    }

    try {
      // 1. Guardamos el token que exige el authGuard
      localStorage.setItem('token', 'mock-jwt-token-123456');

      // 2. Guardamos el perfil y rol para los permisos de roles (Odontólogo / Secretaria)
      localStorage.setItem('perfil', this.perfilSeleccionado);
      localStorage.setItem('rol', this.perfilSeleccionado);

      const actual = this.perfilService.obtener();
      this.perfilService.guardar({ ...actual, perfil: this.perfilSeleccionado as 'secretaria' | 'odontologo' });

      // 3. Redirigimos al dashboard
      this.router.navigate(['/dashboard'], { queryParams: { perfil: this.perfilSeleccionado } });

    } catch (e) {
      console.error('Error en el login:', e);
    }
  }
}
