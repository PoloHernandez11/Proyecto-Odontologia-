import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import Modal from '../components/Modal';
import Tareas from '../components/Tareas';
import TurnoAcciones from '../components/TurnoAcciones';
import TurnoForm from '../components/TurnoForm';
import { claseEstado, ESTADOS_TURNO, fmtFecha, fmtHora, hoyIso, iniciales, sumarDias } from '../utils/formato';

const FILTROS = [
  { id: 'todos', texto: 'Todos', pasa: (t) => t.estado !== 'Cancelado' },
  { id: 'Pendiente', texto: 'Por confirmar', pasa: (t) => t.estado === 'Pendiente' },
  { id: 'Confirmado', texto: 'Confirmados', pasa: (t) => t.estado === 'Confirmado' },
  { id: 'urgencia', texto: 'Urgencias', pasa: (t) => t.urgencia && t.estado !== 'Cancelado' },
  { id: 'Cancelado', texto: 'Cancelados', pasa: (t) => t.estado === 'Cancelado' },
];

function etiquetaDia(fecha) {
  const hoy = hoyIso();
  if (fecha === hoy) return 'Hoy';
  if (fecha === sumarDias(hoy, 1)) return 'Mañana';
  const d = new Date(`${fecha}T00:00:00`);
  return `${d.toLocaleDateString('es-AR', { weekday: 'short' })} ${d.getDate()}`;
}

/** Recepción: confirmar, recordar, reprogramar y organizar el trabajo diario. */
export default function TurnosYTareas() {
  const [dias, setDias] = useState(14);
  const [filtro, setFiltro] = useState('todos');
  const [turnos, setTurnos] = useState([]);
  const [nuevo, setNuevo] = useState(false);
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    setError(null);
    try {
      setTurnos(await api(`/turnos?desde=${hoyIso()}&hasta=${sumarDias(hoyIso(), dias)}`));
    } catch (e) {
      setError(e.message);
    }
  }, [dias]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const f = FILTROS.find((x) => x.id === filtro);
  const visibles = turnos.filter(f.pasa);

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Turnos y tareas</h1>
          <p className="subtitulo">Confirmá, reprogramá y organizá el trabajo diario de la recepción</p>
        </div>
        <button type="button" className="primario" onClick={() => setNuevo(true)}>+ Nuevo turno</button>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <div className="columnas">
        <section className="tarjeta">
          <div className="tarjeta__cabecera">
            <div className="pildoras" role="group" aria-label="Filtro">
              {FILTROS.map((x) => (
                <button key={x.id} type="button" aria-pressed={filtro === x.id} onClick={() => setFiltro(x.id)}>
                  {x.texto} · {turnos.filter(x.pasa).length}
                </button>
              ))}
            </div>
            <select aria-label="Período" value={dias} onChange={(e) => setDias(Number(e.target.value))}>
              <option value={7}>Próximos 7 días</option>
              <option value={14}>Próximos 14 días</option>
              <option value={30}>Próximos 30 días</option>
            </select>
          </div>
          {visibles.length === 0 ? (
            <p className="vacio">No hay turnos con este filtro.</p>
          ) : (
            <div className="tabla-scroll">
              <table className="tabla">
                <thead>
                  <tr><th>Fecha y hora</th><th>Paciente</th><th>Motivo</th><th>Estado</th><th style={{ textAlign: 'right' }}>Acciones</th></tr>
                </thead>
                <tbody>
                  {visibles.map((t) => (
                    <tr key={t.id}>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        <span className="tenue">{etiquetaDia(t.fecha)}</span>
                        <div className="hora">{fmtHora(t.hora)}</div>
                      </td>
                      <td>
                        <span className="persona">
                          <span className="avatar">{iniciales(t.paciente)}</span>
                          <span>
                            <Link to={`/pacientes/${t.pacienteId}`}>{t.paciente}</Link>
                            <span className="persona__detalle" style={{ display: 'block' }}>{t.pacienteTelefono ?? 'Sin teléfono'}</span>
                          </span>
                        </span>
                      </td>
                      <td>{t.motivo}{t.notas && <div className="tenue">{t.notas}</div>}</td>
                      <td>
                        <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
                        {t.urgencia && <div style={{ marginTop: 3 }}><span className="chip chip--urgencia">Urgencia</span></div>}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <TurnoAcciones turno={t} onCambio={cargar} onError={setError} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="tenue" style={{ marginBottom: 0 }}>
            Del {fmtFecha(hoyIso())} al {fmtFecha(sumarDias(hoyIso(), dias))}. El botón WhatsApp abre el mensaje de recordatorio listo para enviar.
          </p>
        </section>

        <Tareas />
      </div>

      {nuevo && (
        <Modal titulo="Nuevo turno" subtitulo="Completá los datos para agendar al paciente" onCerrar={() => setNuevo(false)}>
          <TurnoForm
            onCancelar={() => setNuevo(false)}
            onGuardado={() => {
              setNuevo(false);
              cargar();
            }}
          />
        </Modal>
      )}
    </div>
  );
}
