import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Paciente } from '../../models/paciente.model';
import { PiezaDental } from '../../models/historia-clinica.model';
import { PacienteService } from '../../services/paciente.service';
import { HistoriaClinicaService } from '../../services/historia-clinica.service';

@Component({ selector: 'app-odontograma', standalone: true, imports: [CommonModule, FormsModule, RouterLink], templateUrl: './odontograma.component.html', styleUrl: './odontograma.component.css' })
export class OdontogramaComponent {
  paciente: Paciente | undefined;
  perfil = 'odontologo';
  piezas: PiezaDental[] = [];
  piezaSeleccionada: PiezaDental | null = null;
  mensaje = '';
  caraSeleccionada: typeof this.caras[number] = 'oclusal';
  readonly superiores = [18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28];
  readonly inferiores = [48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38];
  readonly temporalesSuperiores = [55,54,53,52,51,61,62,63,64,65];
  readonly temporalesInferiores = [85,84,83,82,81,71,72,73,74,75];
  readonly caras = ['oclusal', 'mesial', 'distal', 'vestibular', 'lingual'] as const;

  constructor(private route: ActivatedRoute, private pacientes: PacienteService, private historias: HistoriaClinicaService) {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.paciente = this.pacientes.obtenerTodos().find(item => item.id === id);
    this.perfil = this.route.snapshot.queryParamMap.get('perfil') || 'odontologo';
    if (this.paciente) this.piezas = this.historias.obtenerOdontograma(this.paciente.id);
  }

  seleccionarPieza(numero: number): void {
    this.piezaSeleccionada = this.piezas.find(pieza => pieza.numero === numero) || null;
    this.mensaje = '';
    this.caraSeleccionada = 'oclusal';
  }

  seleccionarCara(cara: typeof this.caras[number]): void { this.caraSeleccionada = cara; }
  seleccionarCaraPieza(numero: number, cara: typeof this.caras[number]): void { this.seleccionarPieza(numero); this.caraSeleccionada = cara; }

  actualizarCara(cara: typeof this.caras[number], estado: string): void {
    if (!this.piezaSeleccionada) return;
    this.piezaSeleccionada.caras = { ...(this.piezaSeleccionada.caras || { oclusal: 'Sana', mesial: 'Sana', distal: 'Sana', vestibular: 'Sana', lingual: 'Sana' }), [cara]: estado } as any;
  }

  guardarPieza(): void {
    if (!this.paciente || !this.piezaSeleccionada) return;
    this.historias.actualizarPieza(this.paciente.id, this.piezaSeleccionada);
    this.mensaje = `Se actualizó la pieza ${this.piezaSeleccionada.numero}.` ;
  }

  claseEstado(pieza: PiezaDental): string { return pieza.estado.toLowerCase().replace('ó','o').replace(' ','-'); }

  colorCara(numero: number, cara: 'oclusal' | 'mesial' | 'distal' | 'vestibular' | 'lingual'): string {
    const valor = this.piezas.find(p => p.numero === numero)?.caras?.[cara] || 'Sana';
    if (valor === 'Sana') return '#f8fbfc';
    if (valor === 'Sellante') return '#bde8cf';
    if (valor === 'Caries') return '#f4a477';
    if (valor === 'Obturación') return '#8db5e8';
    return '#d7e1e5';
  }

  clasePieza(numero: number): string {
    const pieza = this.piezas.find(item => item.numero === numero);
    return pieza ? this.claseEstado(pieza) : 'sana';
  }
}
