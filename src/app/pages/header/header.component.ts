import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-header',
  template: `
    <header class="topbar">
      <span>Bienvenido/a, {{ usuario?.nombre }} {{ usuario?.apellido }} ({{ usuario?.rol }})</span>
    </header>
  `
})
export class HeaderComponent implements OnInit {
  usuario: any;

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.usuario = this.authService.getUser();
  }
}