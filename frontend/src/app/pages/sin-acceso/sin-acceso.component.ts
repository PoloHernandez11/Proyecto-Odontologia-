import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sin-acceso',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="aviso">
      <h1>No tenés acceso a esta sección</h1>
      <p>Tu rol en el consultorio no incluye este módulo. Si lo necesitás, pedile a un administrador que revise tus permisos.</p>
      <a routerLink="/dashboard">Volver al inicio</a>
    </main>
  `,
  styles: [`
    .aviso { max-width: 520px; margin: 12vh auto 0; padding: 32px; border: 1px solid #dce4e7; border-radius: 14px; background: #fff; text-align: center; color: #26383e; }
    h1 { margin: 0 0 10px; font-size: 22px; }
    p { margin: 0 0 22px; color: #5c7078; line-height: 1.5; }
    a { display: inline-block; padding: 10px 18px; border-radius: 9px; background: #105c6c; color: #fff; text-decoration: none; font-weight: 600; }
  `]
})
export class SinAccesoComponent {}
