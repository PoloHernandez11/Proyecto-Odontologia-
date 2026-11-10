// Modelo: define los datos personales y administrativos de cada paciente.
export interface Paciente {
  id: number;
  nombre: string;
  apellido: string;
  dni: string;
  telefono: string;
  email: string;
  obraSocial: string;
  ultimaVisita: string;
  proximoTurno: string;
  estado: 'Activo' | 'Inactivo';
}
