import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PerfilService, TipoPerfil } from '../../services/perfil.service';

@Component({
  selector: 'app-configuracion', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './configuracion.component.html',
  styleUrl: './configuracion.component.css'
})
export class ConfiguracionComponent {
  perfil: TipoPerfil = 'secretaria';
  nombre = '';
  correo = '';
  mensaje = '';
  error = '';

  constructor(private route: ActivatedRoute, private router: Router, private perfilService: PerfilService) {
    const datos = this.perfilService.obtener();
    this.perfil = datos.perfil;
    this.nombre = datos.nombre;
    this.correo = datos.correo;
  }

  guardar(): void {
    this.error = ''; this.mensaje = '';
    if (this.nombre.trim().length < 2) { this.error = 'Ingresá un nombre de al menos 2 caracteres.'; return; }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.correo.trim())) { this.error = 'Ingresá un correo electrónico válido.'; return; }
    this.perfilService.guardar({ perfil: this.perfil, nombre: this.nombre.trim(), correo: this.correo.trim().toLowerCase() });
    this.mensaje = 'Perfil y configuración guardados correctamente.';
    this.router.navigate([], { relativeTo: this.route, queryParams: { perfil: this.perfil }, queryParamsHandling: 'merge' });
  }
}
