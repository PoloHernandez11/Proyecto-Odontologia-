// Espejo de backend/OdontoApp.Api/Models/Permisos.cs y Dtos/PermisosDtos.cs.
// Los nombres coinciden con el backend y con la base de datos (permiso_rol, actividad_permiso).

export type ModuloPermiso =
  | 'PagosPacientes'
  | 'FinanzasGenerales'
  | 'LaboratorioTrabajos'
  | 'LaboratorioPagos'
  | 'LaboratorioResultadoEconomico'
  | 'HistoriaClinicaOdontograma';

export type AccionPermiso = 'Registrar' | 'EditarCorregir' | 'Anular' | 'VerResumenes' | 'Confidencial';

/** En pantalla: Permitido = "Con acceso", ConAprobacion = "Con registro". Ambos se aplican al instante. */
export type NivelPermiso = 'SinAcceso' | 'Permitido' | 'ConAprobacion';

/** PermisoItemDto */
export interface PermisoItemDto {
  modulo: ModuloPermiso;
  accion: AccionPermiso;
  nivel: NivelPermiso;
}

/** ActividadPermisoDto: una acción de la secretaria en el historial. */
export interface ActividadPermisoDto {
  id: number;
  usuario: string;
  email: string;
  modulo: ModuloPermiso;
  accion: AccionPermiso;
  nivel: NivelPermiso;
  creadoEn: string;
}

/** MatrizPermisosDto */
export interface MatrizPermisosDto {
  items: PermisoItemDto[];
}

export const MODULOS: ModuloPermiso[] = [
  'PagosPacientes',
  'FinanzasGenerales',
  'LaboratorioTrabajos',
  'LaboratorioPagos',
  'LaboratorioResultadoEconomico',
  'HistoriaClinicaOdontograma',
];

export const ACCIONES: AccionPermiso[] = ['Registrar', 'EditarCorregir', 'Anular', 'VerResumenes', 'Confidencial'];

export const NIVELES: NivelPermiso[] = ['SinAcceso', 'Permitido', 'ConAprobacion'];
