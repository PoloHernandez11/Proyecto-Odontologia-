import { Injectable } from '@angular/core';
import { Paciente } from '../models/paciente.model';

// Servicio que centraliza el listado y las operaciones de pacientes.
@Injectable({ providedIn: 'root' })
export class PacienteService {
  private pacientes: Paciente[] = [
    { id: 1, nombre: 'Martín', apellido: 'Rodríguez', dni: '32400112', telefono: '+54 9 11 5523-8810', email: 'martin.rodriguez@email.com', obraSocial: 'OSDE 31', ultimaVisita: '05/10/2026', proximoTurno: 'Hoy · 09:15', estado: 'Activo' },
    { id: 2, nombre: 'Sofía', apellido: 'Herrera', dni: '40112334', telefono: '+54 9 11 4410-2231', email: 'sofia.herrera@email.com', obraSocial: 'OSDE 21', ultimaVisita: '05/10/2026', proximoTurno: 'Sin turno', estado: 'Activo' },
    { id: 3, nombre: 'Jorge', apellido: 'Castro', dni: '28567890', telefono: '+54 9 11 6021-7781', email: 'jorge.castro@email.com', obraSocial: 'Galeno 22', ultimaVisita: '28/09/2026', proximoTurno: 'Hoy · 10:30', estado: 'Activo' },
    { id: 4, nombre: 'Lucía', apellido: 'Fernández', dni: '36774520', telefono: '+54 9 11 3388-1290', email: 'lucia.fernandez@email.com', obraSocial: 'Particular', ultimaVisita: '12/10/2026', proximoTurno: 'Hoy · 10:00', estado: 'Activo' },
    { id: 5, nombre: 'Valentina', apellido: 'López', dni: '43588910', telefono: '+54 9 11 2201-9934', email: 'valentina.lopez@email.com', obraSocial: 'OSDE 21', ultimaVisita: '01/10/2026', proximoTurno: 'Hoy · 11:15', estado: 'Activo' },
    { id: 6, nombre: 'Diego', apellido: 'Romero', dni: '39500211', telefono: '+54 9 11 5099-4412', email: 'diego.romero@email.com', obraSocial: 'Swiss Medical', ultimaVisita: '15/09/2026', proximoTurno: 'Hoy · 12:00', estado: 'Activo' },
    { id: 7, nombre: 'Paula', apellido: 'Giménez', dni: '33221640', telefono: '+54 9 11 4776-0021', email: 'paula.gimenez@email.com', obraSocial: 'Galeno 22', ultimaVisita: '08/10/2026', proximoTurno: 'Hoy · 15:00', estado: 'Activo' },
    { id: 8, nombre: 'Ricardo', apellido: 'Sosa', dni: '29190566', telefono: '+54 9 11 4550-1908', email: 'ricardo.sosa@email.com', obraSocial: 'OSDE 310', ultimaVisita: 'Sin visitas', proximoTurno: 'Sin turno', estado: 'Activo' },
    { id: 9, nombre: 'Ana', apellido: 'Torres', dni: '36098774', telefono: '+54 9 11 4420-1908', email: 'ana.torres@email.com', obraSocial: 'Particular', ultimaVisita: '19/09/2026', proximoTurno: 'Sin turno', estado: 'Inactivo' }
  ];

  obtenerTodos(): Paciente[] {
    return this.pacientes;
  }

  agregar(paciente: Paciente): void {
    this.pacientes.push(paciente);
  }

  actualizar(pacienteActualizado: Paciente): void {
    const indice = this.pacientes.findIndex(paciente => paciente.id === pacienteActualizado.id);
    if (indice !== -1) this.pacientes[indice] = pacienteActualizado;
  }
}
