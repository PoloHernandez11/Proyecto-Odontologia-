export interface Tarea {
  id: number;
  titulo: string;
  descripcion?: string;
  prioridad: 'Baja' | 'Media' | 'Alta';
  completada: boolean;
  fechaLimite: string;
}