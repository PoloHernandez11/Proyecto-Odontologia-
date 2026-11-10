import { Injectable } from '@angular/core';
import { Turno } from '../models/turno.model';

@Injectable({ providedIn: 'root' })
export class TurnoService {
  private readonly clave = 'odontia_turnos';
  private turnos: Turno[] = [
    { id: 1, fecha: this.fechaRelativa(0), hora: '08:30', duracion: 45, paciente: 'Sofía Herrera', telefono: '11 5555-0101', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: 'Control semestral', estado: 'Atendido' },
    { id: 2, fecha: this.fechaRelativa(0), hora: '09:15', duracion: 60, paciente: 'Martín Rodríguez', telefono: '11 5555-0102', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: 'Endodoncia pieza 36 · 2ª sesión', estado: 'En sala' },
    { id: 3, fecha: this.fechaRelativa(0), hora: '10:00', duracion: 45, paciente: 'Lucía Fernández', telefono: '11 5555-0103', profesional: 'Dr. Tomás Paz', consultorio: 'Consultorio 2', tratamiento: 'Limpieza y flúor', estado: 'Confirmado' },
    { id: 4, fecha: this.fechaRelativa(0), hora: '10:30', duracion: 45, paciente: 'Jorge Castro', telefono: '11 5555-0104', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: 'Prueba de corona 21', estado: 'Confirmado' },
    { id: 5, fecha: this.fechaRelativa(0), hora: '11:15', duracion: 45, paciente: 'Valentina López', telefono: '11 5555-0105', profesional: 'Dr. Tomás Paz', consultorio: 'Consultorio 2', tratamiento: 'Consulta ortodoncia', estado: 'Pendiente' },
    { id: 6, fecha: this.fechaRelativa(0), hora: '12:00', duracion: 60, paciente: 'Diego Romero', telefono: '11 5555-0106', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: 'Extracción pieza 48', estado: 'Confirmado' },
    { id: 7, fecha: this.fechaRelativa(0), hora: '13:30', duracion: 30, paciente: 'Nicolás Vera', telefono: '11 5555-0107', profesional: 'Dr. Tomás Paz', consultorio: 'Consultorio 2', tratamiento: 'Evaluación implante 46', estado: 'Confirmado' },
    { id: 8, fecha: this.fechaRelativa(0), hora: '14:30', duracion: 60, paciente: 'Ana Torres', telefono: '11 5555-0108', profesional: 'Dr. Tomás Paz', consultorio: 'Consultorio 2', tratamiento: 'Blanqueamiento', estado: 'Cancelado' },
    { id: 9, fecha: this.fechaRelativa(0), hora: '15:00', duracion: 45, paciente: 'Paula Giménez', telefono: '11 5555-0109', profesional: 'Dra. Laura Méndez', consultorio: 'Consultorio 1', tratamiento: 'Obturación 14 y 15', estado: 'Pendiente' },
    { id: 10, fecha: this.fechaRelativa(1), hora: '09:00', duracion: 45, paciente: 'Camila Ortiz', telefono: '11 5555-0110', profesional: 'Dr. Tomás Paz', consultorio: 'Consultorio 2', tratamiento: 'Control de brackets', estado: 'Confirmado' }
  ];

  constructor() {
    if (typeof localStorage === 'undefined') return;
    try {
      const guardados = localStorage.getItem(this.clave);
      if (guardados) {
        const datos: unknown = JSON.parse(guardados);
        this.turnos = Array.isArray(datos) ? datos.filter(this.esTurnoValido) : [...this.turnos];
        if (!Array.isArray(datos)) this.persistir();
      } else this.persistir();
    } catch { this.persistir(); }
  }

  private fechaRelativa(dias: number): string {
    const fecha = new Date(); fecha.setDate(fecha.getDate() + dias);
    return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
  }
  private esTurnoValido = (valor: unknown): valor is Turno => {
    if (!valor || typeof valor !== 'object') return false;
    const turno = valor as Partial<Turno>;
    return Number.isFinite(turno.id) && typeof turno.fecha === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(turno.fecha) && typeof turno.hora === 'string' && typeof turno.paciente === 'string' && typeof turno.telefono === 'string' && typeof turno.profesional === 'string' && typeof turno.consultorio === 'string' && typeof turno.tratamiento === 'string' && ['Confirmado', 'Pendiente', 'En sala', 'Atendido', 'Cancelado'].includes(String(turno.estado));
  };
  private persistir(): void { if (typeof localStorage !== 'undefined') { try { localStorage.setItem(this.clave, JSON.stringify(this.turnos)); } catch { /* El navegador puede bloquear o limitar el almacenamiento. */ } } }
  obtenerTodos(): Turno[] { return this.turnos.map(turno => ({ ...turno })); }
  agregar(turno: Turno): Turno {
    const id = this.turnos.some(actual => actual.id === turno.id) ? Math.max(0, ...this.turnos.map(actual => actual.id)) + 1 : turno.id;
    const guardado = { ...turno, id };
    this.turnos.push(guardado);
    this.persistir();
    return { ...guardado };
  }
  actualizar(turnoActualizado: Turno): void {
    const indice = this.turnos.findIndex(turno => turno.id === turnoActualizado.id);
    if (indice !== -1) { this.turnos[indice] = { ...turnoActualizado }; this.persistir(); }
  }
}
