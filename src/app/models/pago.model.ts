// Modelos del módulo de cobros. Un concepto representa una deuda/tratamiento;
// cada Pago representa una operación de dinero efectivamente recibida.
export type MetodoPago = 'Efectivo' | 'Transferencia' | 'Tarjeta de débito' | 'Tarjeta de crédito';
export type EstadoConcepto = 'Pendiente' | 'Pago parcial' | 'Pagado';

export interface ConceptoCobro {
  id: number;
  pacienteId: number;
  pacienteNombre: string;
  descripcion: string;
  fecha: string;
  importeTotal: number;
  estado: EstadoConcepto;
}

export interface Pago {
  id: number;
  conceptoId: number;
  pacienteId: number;
  pacienteNombre: string;
  conceptoDescripcion: string;
  fecha: string;
  importe: number;
  metodo: MetodoPago;
  comprobante: string;
  observaciones: string;
  anulado: boolean;
  cuotas?: number;
  importeCuota?: number;
}
