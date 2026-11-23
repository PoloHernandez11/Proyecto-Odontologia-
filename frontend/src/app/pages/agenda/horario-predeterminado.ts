import { fechaEnZona, minutosEnZona } from '../../core/zona-horaria';

/** Jornada que se usa para proponer el horario de un turno nuevo. */
export const INICIO_DE_JORNADA = 9;
export const FIN_DE_JORNADA = 20;

function sumarDias(fecha: string, dias: number): string {
  const [anio, mes, dia] = fecha.split('-').map(Number);
  const resultado = new Date(Date.UTC(anio, mes - 1, dia + dias));
  return resultado.toISOString().slice(0, 10);
}

const hhmm = (minutos: number) => `${String(Math.floor(minutos / 60)).padStart(2, '0')}:${String(minutos % 60).padStart(2, '0')}`;

/**
 * Fecha y hora que se proponen al abrir "Nuevo turno":
 * - un día futuro → el inicio de la jornada de ese día;
 * - hoy (o un día pasado) → el próximo horario múltiplo de `paso` minutos desde ahora,
 *   o el inicio de la jornada de mañana si ya no entra un turno antes del cierre.
 * Todo se calcula en la zona horaria del consultorio.
 */
export function horarioPredeterminado(ahora: Date, fechaElegida: string, zona: string, paso = 15): { fecha: string; hora: string } {
  const hoy = fechaEnZona(ahora, zona);
  const inicio = INICIO_DE_JORNADA * 60, fin = FIN_DE_JORNADA * 60;
  if (fechaElegida > hoy) return { fecha: fechaElegida, hora: hhmm(inicio) };
  const siguiente = (Math.floor(minutosEnZona(ahora, zona) / paso) + 1) * paso;
  if (siguiente < inicio) return { fecha: hoy, hora: hhmm(inicio) };
  if (siguiente + paso > fin) return { fecha: sumarDias(hoy, 1), hora: hhmm(inicio) };
  return { fecha: hoy, hora: hhmm(siguiente) };
}
