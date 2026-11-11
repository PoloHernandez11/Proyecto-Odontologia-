import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

    getRolActual(): string{
        return localStorage.getItem('perfil') || localStorage.getItem('rol') || 'secretaria';
    }

    esOdontologo(): boolean{
        return this.getRolActual() === 'odontologo';
    }
    puedeVerFinanzas(): boolean{
        return this.esOdontologo();
    }
    puedeVerHistorialClinico(): boolean{
        return this.esOdontologo();
    }
  /*getUser() {
    const userStr = localStorage.getItem('user');
    return userStr ? JSON.parse(userStr) : null;
  }

  getRole(): string {
    const user = this.getUser();
    return user ? user.rol : ''; // Ej: 'ADMIN', 'SECRETARIA', 'ODONTOLOGO'
  }

  hasRole(allowedRoles: string[]): boolean {
    const userRole = this.getRole();
    return allowedRoles.includes(userRole);
  }
    */
}