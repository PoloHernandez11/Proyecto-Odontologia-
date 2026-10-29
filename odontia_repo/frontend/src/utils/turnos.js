import { api } from '../api/client';
import { fmtFecha, fmtHora, hoyIso, nombreNatural, sumarDias } from './formato';

/**
 * Cambia el estado de un turno. 'Cancelado' pide confirmación y usa su propio endpoint.
 * Devuelve false si el usuario se arrepintió.
 */
export async function cambiarEstadoTurno(turno, estado) {
  if (estado === 'Cancelado') {
    if (!window.confirm(`¿Cancelar el turno de ${turno.paciente} del ${fmtFecha(turno.fecha)} a las ${fmtHora(turno.hora)}?`)) return false;
    await api(`/turnos/${turno.id}/cancelar`, { method: 'PATCH' });
  } else {
    await api(`/turnos/${turno.id}/estado`, { method: 'PATCH', body: { estado } });
  }
  return true;
}

function cuandoTexto(fecha) {
  if (fecha === hoyIso()) return 'hoy';
  if (fecha === sumarDias(hoyIso(), 1)) return 'mañana';
  return `el ${fmtFecha(fecha)}`;
}

/** Link de WhatsApp con el recordatorio del turno (lo envía la secretaria desde su teléfono). */
export function linkWhatsApp(turno) {
  const tel = (turno.pacienteTelefono ?? '').replace(/\D/g, '');
  if (!tel) return null;
  const texto =
    `Hola ${nombreNatural(turno.paciente).split(' ')[0]}, te recordamos tu turno odontológico ` +
    `${cuandoTexto(turno.fecha)} a las ${fmtHora(turno.hora)}. ` +
    'Por favor respondé este mensaje para confirmar. ¡Gracias!';
  return `https://wa.me/${tel}?text=${encodeURIComponent(texto)}`;
}

/** Acciones que tienen sentido según el estado del turno y el rol. */
export function accionesTurno(turno, esOdontologo) {
  const abierto = ['Pendiente', 'Confirmado', 'EnSala'].includes(turno.estado);
  const esHoy = turno.fecha === hoyIso();
  return {
    confirmar: turno.estado === 'Pendiente',
    llego: esHoy && (turno.estado === 'Pendiente' || turno.estado === 'Confirmado'),
    atender: esOdontologo && esHoy && abierto,
    ausente: abierto && turno.fecha <= hoyIso(),
    reprogramar: abierto,
    cancelar: abierto,
    recordar: turno.estado === 'Pendiente' || turno.estado === 'Confirmado',
  };
}
