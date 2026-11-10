// Modelo que representa un trabajo enviado a un laboratorio dental.
export type EstadoTrabajo =
  | 'Solicitado'
  | 'En proceso'
  | 'Listo para retirar'
  | 'Recibido'
  | 'Entregado al paciente';

export interface TrabajoLaboratorio {
  id: number;
  pacienteId: number;
  pacienteNombre: string;
  tipoTrabajo: string;
  piezaDental: string;
  laboratorio: string;
  fechaSolicitud: string;
  fechaEntrega: string;
  fechaRecepcion: string;
  fechaEntregaPaciente: string;
  estado: EstadoTrabajo;
  costo: number;
  observaciones: string;
}
