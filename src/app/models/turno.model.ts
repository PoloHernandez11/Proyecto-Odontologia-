// Modelo: define qué información necesita cada turno de Odontia.
export interface Turno {
  id: number;
  fecha: string;
  hora: string;
  duracion: number;
  paciente: string;
  telefono: string;
  profesional: string;
  consultorio: string;
  tratamiento: string;
  estado: 'Confirmado' | 'Pendiente' | 'En sala' | 'Atendido' | 'Cancelado';
}
