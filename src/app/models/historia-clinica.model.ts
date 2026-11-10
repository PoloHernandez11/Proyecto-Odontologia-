// Modelo: representa una consulta o anotación de la historia clínica.
export interface RegistroClinico {
  id: number;
  pacienteId: number;
  fecha: string;
  profesional: string;
  motivo: string;
  diagnostico: string;
  tratamiento: string;
  observaciones: string;
}

// Modelo: representa el estado registrado para una pieza dental.
export interface PiezaDental {
  numero: number;
  estado: 'Sana' | 'Caries' | 'Restauración' | 'Ausente' | 'Corona';
  observacion: string;
  caras?: { oclusal: string; mesial: string; distal: string; vestibular: string; lingual: string };
}
