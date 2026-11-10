import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RegistroClinico, PiezaDental } from '../models/historia-clinica.model';

// Servicio: centraliza la historia clínica y el odontograma de los pacientes.
@Injectable({ providedIn: 'root' })
export class HistoriaClinicaService {
  private registros: RegistroClinico[] = [
    { id: 1, pacienteId: 1, fecha: '2026-10-05', profesional: 'Dra. Laura Méndez', motivo: 'Dolor al masticar', diagnostico: 'Caries profunda en pieza 36', tratamiento: 'Endodoncia · primera sesión', observaciones: 'Se indica continuar el tratamiento en la próxima consulta.' }
  ];
  private piezas: { [pacienteId: number]: PiezaDental[] } = {};
  private esNavegador: boolean;

  constructor(@Inject(PLATFORM_ID) plataforma: object) {
    this.esNavegador = isPlatformBrowser(plataforma);
    if (this.esNavegador) this.cargarDatos();
  }

  obtenerRegistros(pacienteId: number): RegistroClinico[] {
    return this.registros.filter(registro => registro.pacienteId === pacienteId);
  }

  agregarRegistro(registro: RegistroClinico): void {
    this.registros.push(registro);
    this.guardarDatos();
  }

  obtenerOdontograma(pacienteId: number): PiezaDental[] {
    if (!this.piezas[pacienteId]) {
      this.piezas[pacienteId] = this.crearOdontogramaInicial();
    }
    return this.piezas[pacienteId];
  }

  actualizarPieza(pacienteId: number, piezaActualizada: PiezaDental): void {
    const piezasPaciente = this.obtenerOdontograma(pacienteId);
    const indice = piezasPaciente.findIndex(pieza => pieza.numero === piezaActualizada.numero);
    if (indice !== -1) piezasPaciente[indice] = piezaActualizada;
    this.guardarDatos();
  }

  private crearOdontogramaInicial(): PiezaDental[] {
    const numeros = [18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28,48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38,55,54,53,52,51,61,62,63,64,65,85,84,83,82,81,71,72,73,74,75];
    return numeros.map(numero => ({ numero, estado: 'Sana' as const, observacion: '', caras: { oclusal: 'Sana', mesial: 'Sana', distal: 'Sana', vestibular: 'Sana', lingual: 'Sana' } }));
  }

  private cargarDatos(): void {
    try {
      const registrosGuardados = localStorage.getItem('odontia-historias');
      const piezasGuardadas = localStorage.getItem('odontia-odontogramas');
      if (registrosGuardados) this.registros = JSON.parse(registrosGuardados);
      if (piezasGuardadas) this.piezas = JSON.parse(piezasGuardadas);
    } catch {
      // Si el navegador no puede leer los datos, se mantienen los datos iniciales.
    }
  }

  private guardarDatos(): void {
    if (!this.esNavegador) return;
    localStorage.setItem('odontia-historias', JSON.stringify(this.registros));
    localStorage.setItem('odontia-odontogramas', JSON.stringify(this.piezas));
  }
}
