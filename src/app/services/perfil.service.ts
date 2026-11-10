import { Injectable } from '@angular/core';

export type TipoPerfil = 'secretaria' | 'odontologo';
export interface DatosPerfil { perfil: TipoPerfil; nombre: string; correo: string; }

@Injectable({ providedIn: 'root' })
export class PerfilService {
  private readonly clave = 'odontia_perfil_usuario';
  private datos: DatosPerfil = { perfil: 'odontologo', nombre: 'Dra. Laura Méndez', correo: 'laura@odontia.demo' };

  constructor() {
    if (typeof localStorage === 'undefined') return;
    try {
      const guardado = localStorage.getItem(this.clave);
      if (guardado) {
        const datos: unknown = JSON.parse(guardado);
        if (datos && typeof datos === 'object') {
          const candidato = datos as Partial<DatosPerfil>;
          this.datos = {
            perfil: candidato.perfil === 'secretaria' || candidato.perfil === 'odontologo' ? candidato.perfil : this.datos.perfil,
            nombre: typeof candidato.nombre === 'string' && candidato.nombre.trim() ? candidato.nombre.trim() : this.datos.nombre,
            correo: typeof candidato.correo === 'string' ? candidato.correo : this.datos.correo
          };
        }
      }
    } catch { /* Si el dato local está dañado, se utilizan los valores de demostración. */ }
  }

  obtener(): DatosPerfil { return { ...this.datos }; }
  guardar(datos: DatosPerfil): void {
    this.datos = { ...datos };
    if (typeof localStorage !== 'undefined') {
      try { localStorage.setItem(this.clave, JSON.stringify(this.datos)); }
      catch { /* La sesión sigue funcionando aunque el navegador no permita persistir. */ }
    }
  }
}
