import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { TrabajoLaboratorio, EstadoTrabajo } from '../models/trabajo-laboratorio.model';

// Servicio encargado de consultar, registrar y actualizar trabajos de laboratorio.
@Injectable({ providedIn: 'root' })
export class LaboratorioService {
  private readonly claveStorage = 'odontia-trabajos-laboratorio';
  private trabajos: TrabajoLaboratorio[] = [];
  private readonly navegador: boolean;

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.navegador = isPlatformBrowser(platformId);
    this.cargarDatos();
  }

  private fechaRelativa(dias: number): string {
    const fecha = new Date();
    fecha.setDate(fecha.getDate() + dias);
    return fecha.toISOString().slice(0, 10);
  }

  private cargarDatos(): void {
    if (this.navegador) {
      const guardados = localStorage.getItem(this.claveStorage);
      if (guardados) {
        try {
          this.trabajos = JSON.parse(guardados) as TrabajoLaboratorio[];
          return;
        } catch {
          localStorage.removeItem(this.claveStorage);
        }
      }
    }

    // Trabajos de ejemplo inspirados en los casos que aparecen en Figma.
    this.trabajos = [
      { id: 1, pacienteId: 3, pacienteNombre: 'Jorge Castro', tipoTrabajo: 'Corona de zirconio', piezaDental: '21', laboratorio: 'Dental Sur', fechaSolicitud: this.fechaRelativa(-3), fechaEntrega: this.fechaRelativa(1), fechaRecepcion: '', fechaEntregaPaciente: '', estado: 'En proceso', costo: 85000, observaciones: 'Controlar color antes de cementar.' },
      { id: 2, pacienteId: 8, pacienteNombre: 'Ricardo Sosa', tipoTrabajo: 'Corona sobre implante', piezaDental: '46', laboratorio: 'Lab. Belgrano', fechaSolicitud: this.fechaRelativa(-12), fechaEntrega: this.fechaRelativa(-2), fechaRecepcion: '', fechaEntregaPaciente: '', estado: 'En proceso', costo: 120000, observaciones: 'Trabajo demorado. Consultar fecha.' },
      { id: 3, pacienteId: 2, pacienteNombre: 'Sofía Herrera', tipoTrabajo: 'Prótesis parcial superior', piezaDental: '—', laboratorio: 'Dental Sur', fechaSolicitud: this.fechaRelativa(-2), fechaEntrega: this.fechaRelativa(3), fechaRecepcion: '', fechaEntregaPaciente: '', estado: 'Solicitado', costo: 145000, observaciones: '' },
      { id: 4, pacienteId: 5, pacienteNombre: 'Valentina López', tipoTrabajo: 'Placa de contención', piezaDental: 'Superior', laboratorio: 'OrtoLab', fechaSolicitud: this.fechaRelativa(-5), fechaEntrega: this.fechaRelativa(2), fechaRecepcion: this.fechaRelativa(0), fechaEntregaPaciente: '', estado: 'Listo para retirar', costo: 45000, observaciones: 'Avisar al paciente para coordinar entrega.' },
      { id: 5, pacienteId: 1, pacienteNombre: 'Martín Rodríguez', tipoTrabajo: 'Perno colado', piezaDental: '36', laboratorio: 'Lab. Belgrano', fechaSolicitud: this.fechaRelativa(-20), fechaEntrega: this.fechaRelativa(-10), fechaRecepcion: this.fechaRelativa(-9), fechaEntregaPaciente: this.fechaRelativa(-8), estado: 'Entregado al paciente', costo: 65000, observaciones: '' }
    ];
    this.guardarDatos();
  }

  private guardarDatos(): void {
    if (this.navegador) {
      localStorage.setItem(this.claveStorage, JSON.stringify(this.trabajos));
    }
  }

  obtenerTodos(): TrabajoLaboratorio[] {
    return [...this.trabajos];
  }

  agregar(trabajo: Omit<TrabajoLaboratorio, 'id'>): void {
    const nuevo: TrabajoLaboratorio = {
      ...trabajo,
      id: Math.max(0, ...this.trabajos.map(item => item.id)) + 1
    };
    this.trabajos = [nuevo, ...this.trabajos];
    this.guardarDatos();
  }

  actualizarEstado(id: number, estado: EstadoTrabajo): void {
    const trabajo = this.trabajos.find(item => item.id === id);
    if (!trabajo) return;

    trabajo.estado = estado;
    if (estado === 'Recibido' && !trabajo.fechaRecepcion) {
      trabajo.fechaRecepcion = this.fechaRelativa(0);
    }
    if (estado === 'Entregado al paciente' && !trabajo.fechaEntregaPaciente) {
      trabajo.fechaEntregaPaciente = this.fechaRelativa(0);
      if (!trabajo.fechaRecepcion) trabajo.fechaRecepcion = this.fechaRelativa(0);
    }
    this.guardarDatos();
  }
}
