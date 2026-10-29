import { useEffect, useState } from 'react';
import { api } from '../api/client';
import { fmtHora, hoyIso } from '../utils/formato';
import BuscadorPaciente from './BuscadorPaciente';

const DURACIONES = [15, 30, 45, 60, 90, 120];

/**
 * Agendar (sin `turno`) o reprogramar (con `turno`).
 * Un solo odontólogo: los horarios libres salen de /turnos/disponibles según la duración elegida;
 * una urgencia (sobreturno) permite elegir cualquier hora. La validación definitiva la hace el backend.
 */
export default function TurnoForm({ turno, paciente: pacienteInicial, fechaInicial, horaInicial, onGuardado, onCancelar }) {
  const reprogramando = Boolean(turno);
  const [paciente, setPaciente] = useState(
    turno ? { id: turno.pacienteId, apellido: turno.paciente, nombre: '', dni: turno.pacienteDni } : pacienteInicial ?? null,
  );
  const [fecha, setFecha] = useState(turno?.fecha ?? fechaInicial ?? hoyIso());
  const [duracion, setDuracion] = useState(turno?.duracionMinutos ?? 30);
  const [hora, setHora] = useState(horaInicial ?? turno?.hora ?? '');
  const [motivo, setMotivo] = useState(turno?.motivo ?? '');
  const [notas, setNotas] = useState(turno?.notas ?? '');
  const [urgencia, setUrgencia] = useState(turno?.urgencia ?? false);
  const [libres, setLibres] = useState([]);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    const ctrl = new AbortController();
    const excluir = turno ? `&excluir=${turno.id}` : '';
    api(`/turnos/disponibles?fecha=${fecha}&duracion=${duracion}${excluir}`, { signal: ctrl.signal })
      .then((h) => {
        setLibres(h);
        if (!urgencia) setHora((actual) => (h.some((x) => fmtHora(x) === fmtHora(actual)) ? h.find((x) => fmtHora(x) === fmtHora(actual)) : ''));
      })
      .catch((e) => e.name !== 'AbortError' && setError(e.message));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fecha, duracion, turno]);

  async function guardar(e) {
    e.preventDefault();
    if (!paciente) return setError('Elegí un paciente.');
    if (!hora) return setError('Elegí un horario.');
    setGuardando(true);
    setError(null);
    try {
      const body = {
        fecha,
        hora: hora.length === 5 ? `${hora}:00` : hora,
        duracionMinutos: Number(duracion),
        motivo: motivo || null,
        notas: notas || null,
        urgencia,
      };
      const guardado = reprogramando
        ? await api(`/turnos/${turno.id}`, { method: 'PUT', body })
        : await api('/turnos', { method: 'POST', body: { ...body, pacienteId: paciente.id } });
      onGuardado(guardado);
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form onSubmit={guardar} style={{ display: 'grid', gap: '0.9rem' }}>
      {reprogramando ? (
        <p style={{ margin: 0 }}>
          Paciente: <b>{turno.paciente}</b>
        </p>
      ) : (
        <BuscadorPaciente seleccionado={paciente} onSeleccionar={setPaciente} />
      )}
      <div className="form-grilla">
        <label>
          Fecha
          <input type="date" required min={hoyIso()} value={fecha} onChange={(e) => setFecha(e.target.value)} />
        </label>
        <label>
          Duración
          <select value={duracion} onChange={(e) => setDuracion(Number(e.target.value))}>
            {DURACIONES.map((d) => <option key={d} value={d}>{d} minutos</option>)}
          </select>
        </label>
        <label>
          Hora
          {urgencia ? (
            <input type="time" required step={300} value={fmtHora(hora)} onChange={(e) => setHora(e.target.value)} />
          ) : (
            <select required value={hora} onChange={(e) => setHora(e.target.value)}>
              <option value="">{libres.length ? 'Horarios disponibles' : 'Sin horarios libres'}</option>
              {libres.map((h) => (
                <option key={h} value={h}>{fmtHora(h)}</option>
              ))}
            </select>
          )}
        </label>
        <label className="ancho-total">
          Motivo / tratamiento
          <input maxLength={255} placeholder="Control semestral, endodoncia pieza 36 · 2ª sesión…" value={motivo} onChange={(e) => setMotivo(e.target.value)} />
        </label>
        <label className="ancho-total">
          Notas para el turno
          <input maxLength={255} placeholder="Ej: traer radiografía panorámica reciente" value={notas} onChange={(e) => setNotas(e.target.value)} />
        </label>
        <label className="check ancho-total">
          <input type="checkbox" checked={urgencia} onChange={(e) => {
            setUrgencia(e.target.checked);
            if (!e.target.checked) setHora('');
          }} />
          Marcar como urgencia (sobreturno: se permite aunque se superponga con otro turno)
        </label>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones" style={{ marginTop: 0 }}>
        <button type="button" onClick={onCancelar} disabled={guardando}>
          Cancelar
        </button>
        <button type="submit" className="primario" disabled={guardando}>
          {guardando ? 'Guardando…' : reprogramando ? 'Reprogramar' : 'Agendar turno'}
        </button>
      </div>
    </form>
  );
}
