export type TipoMovimiento = 'Ingreso' | 'Egreso';
export type CategoriaFinanciera = 'Servicios' | 'Insumos' | 'Sueldos' | 'Alquiler' | 'Laboratorio dental' | 'Mantenimiento' | 'Otros';

// Los movimientos manuales no incluyen cobros de pacientes: esos vienen de PagoService.
export interface MovimientoFinanciero {
  id: number;
  tipo: TipoMovimiento;
  fecha: string;
  concepto: string;
  categoria: CategoriaFinanciera;
  importe: number;
  metodo: string;
  origen: string;
  anulado: boolean;
  trabajoLaboratorioId?: number;
}

export interface MovimientoVista {
  id: string;
  tipo: TipoMovimiento;
  fecha: string;
  concepto: string;
  categoria: string;
  importe: number;
  metodo: string;
  origen: string;
  anulado: boolean;
}
