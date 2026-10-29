import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import Modal from '../components/Modal';
import Tareas from '../components/Tareas';
import TurnoAcciones from '../components/TurnoAcciones';
import TurnoForm from '../components/TurnoForm';
import {
  capitalizar, claseEstado, ESTADOS_TURNO, fmtFecha, fmtFechaLarga, fmtHora, fmtMoneda, hoyIso, iniciales,
  MEDIOS_PAGO, minutos, saludo, sumarDias, turnoActivo,
} from '../utils/formato';

function minutosDesde(hora) {
  const ahora = new Date();
  return Math.max(0, ahora.getHours() * 60 + ahora.getMinutes() - minutos(hora));
}

/** Inicio de la recepción: sala de espera, agenda del día, confirmaciones, tareas, laboratorio y cobros. */
export default function DashboardSecretaria() {
  const { usuario } = useAuth();
  const navigate = useNavigate();
  const [turnos, setTurnos] = useState([]);
  const [manana, setManana] = useState([]);
  const [cobros, setCobros] = useState([]);
  const [lab, setLab] = useState([]);
  const [tareasAbiertas, setTareasAbiertas] = useState([]);
  const [nuevoTurno, setNuevoTurno] = useState(false);
  const [error, setError] = useState(null);
  const hoy = hoyIso();

  const cargar = useCallback(async () => {
    setError(null);
    try {
      const [t, m, c, l, ta] = await Promise.all([
        api(`/turnos?desde=${hoy}`),
        api(`/turnos?desde=${sumarDias(hoy, 1)}`),
        api(`/pagos?desde=${hoy}`),
        api('/laboratorio?estado=Enviado'),
        api('/tareas'),
      ]);
      setTurnos(t);
      setManana(m);
      setCobros(c.filter((p) => !p.anulado));
      setLab(l);
      setTareasAbiertas(ta);
    } catch (e) {
      setError(e.message);
    }
  }, [hoy]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const activos = turnos.filter(turnoActivo);
  const confirmados = activos.filter((t) => t.estado === 'Confirmado').length;
  const porConfirmarHoy = activos.filter((t) => t.estado === 'Pendiente').length;
  const enSala = turnos.filter((t) => t.estado === 'EnSala');
  const esperaMedia = enSala.length
    ? Math.round(enSala.reduce((s, t) => s + minutosDesde(t.horaLlegada ?? t.hora), 0) / enSala.length)
    : 0;
  const porConfirmarManana = manana.filter((t) => t.estado === 'Pendiente').length;
  const vencenHoy = tareasAbiertas.filter((t) => t.vence && t.vence <= hoy).length;
  const totalCobrado = cobros.reduce((s, p) => s + p.monto, 0);
  const proximasEntregas = [...lab]
    .filter((x) => x.fechaEntregaEstimada)
    .sort((a, b) => a.fechaEntregaEstimada.localeCompare(b.fechaEntregaEstimada))
    .slice(0, 4);

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>{saludo()}, {usuario.nombre.split(' ')[0]}</h1>
          <p className="subtitulo">{capitalizar(fmtFechaLarga(hoy))} · Vista compartida de recepción</p>
        </div>
        <div className="filtros">
          <button type="button" onClick={() => navigate('/pacientes/nuevo')}>Nuevo paciente</button>
          <button type="button" className="primario" onClick={() => setNuevoTurno(true)}>+ Nuevo turno</button>
        </div>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <div className="kpis">
        <div className="kpi">
          <div className="kpi__titulo">Turnos de hoy</div>
          <div className="kpi__valor">{activos.length}</div>
          <div className="kpi__detalle">{confirmados} confirmados · {porConfirmarHoy} por confirmar</div>
        </div>
        <div className="kpi">
          <div className="kpi__titulo">En sala de espera</div>
          <div className="kpi__valor">{enSala.length}</div>
          <div className="kpi__detalle">{enSala.length ? `Tiempo medio de espera: ${esperaMedia} min` : 'Nadie esperando'}</div>
        </div>
        <div className="kpi">
          <div className="kpi__titulo">Confirmaciones pendientes</div>
          <div className="kpi__valor">{porConfirmarManana}</div>
          <div className="kpi__detalle"><Link to="/turnos">Enviar recordatorio por WhatsApp</Link></div>
        </div>
        <div className="kpi">
          <div className="kpi__titulo">Tareas abiertas</div>
          <div className="kpi__valor">{tareasAbiertas.length}</div>
          <div className="kpi__detalle">{vencenHoy} vencen hoy</div>
        </div>
      </div>

      <div className="columnas">
        <div className="pagina">
          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Sala de espera</h2>
              <span className="tenue">{enSala.length} paciente{enSala.length === 1 ? '' : 's'}</span>
            </div>
            {enSala.length === 0 ? (
              <p className="vacio">Cuando un paciente llegue, marcá “Llegó” en su turno.</p>
            ) : (
              <ul className="lista">
                {enSala.map((t) => (
                  <li key={t.id}>
                    <span className="persona">
                      <span className="avatar">{iniciales(t.paciente)}</span>
                      <span>
                        <Link to={`/pacientes/${t.pacienteId}`} className="persona__nombre">{t.paciente}</Link>
                        <span className="persona__detalle" style={{ display: 'block' }}>
                          Turno {fmtHora(t.hora)} · {t.motivo ?? 'Sin motivo'}
                        </span>
                      </span>
                    </span>
                    <span className="tenue">Llegó {fmtHora(t.horaLlegada)} · {minutosDesde(t.horaLlegada ?? t.hora)} min</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Agenda de hoy</h2>
              <Link to="/agenda">Ver agenda completa</Link>
            </div>
            {turnos.length === 0 ? (
              <p className="vacio">No hay turnos para hoy.</p>
            ) : (
              <div className="tabla-scroll">
                <table className="tabla">
                  <thead>
                    <tr><th>Hora</th><th>Paciente</th><th>Motivo</th><th>Estado</th><th /></tr>
                  </thead>
                  <tbody>
                    {turnos.map((t) => (
                      <tr key={t.id}>
                        <td className="hora">{fmtHora(t.hora)}</td>
                        <td>
                          <span className="persona">
                            <span className="avatar">{iniciales(t.paciente)}</span>
                            <span>
                              <Link to={`/pacientes/${t.pacienteId}`}>{t.paciente}</Link>
                              {t.pacienteAlertas.length > 0 && (
                                <span className="alertas" style={{ marginTop: 2 }}>
                                  {t.pacienteAlertas.slice(0, 2).map((a) => <span key={a} className="alerta">{a}</span>)}
                                </span>
                              )}
                            </span>
                          </span>
                        </td>
                        <td>{t.motivo}{t.notas && <div className="tenue">{t.notas}</div>}</td>
                        <td>
                          <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
                          {t.urgencia && <> <span className="chip chip--urgencia">Urgencia</span></>}
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <TurnoAcciones turno={t} onCambio={cargar} onError={setError} compacto />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>

        <div className="pagina">
          <Tareas compacto onCambio={cargar} />

          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Laboratorio · próximas entregas</h2>
              <Link to="/laboratorio">Ver</Link>
            </div>
            {proximasEntregas.length === 0 ? (
              <p className="vacio">Sin trabajos en el laboratorio.</p>
            ) : (
              <ul className="lista">
                {proximasEntregas.map((x) => (
                  <li key={x.id}>
                    <span>
                      <b>{x.paciente}</b>
                      <span className="persona__detalle" style={{ display: 'block' }}>
                        {x.tipoTrabajo}{x.piezas && ` · ${x.piezas}`} · {x.laboratorio}
                      </span>
                    </span>
                    <span className={x.fechaEntregaEstimada < hoy ? 'negativo' : 'tenue'}>
                      {x.fechaEntregaEstimada === hoy ? 'Hoy' : fmtFecha(x.fechaEntregaEstimada)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Cobros del día</h2>
              <b>{fmtMoneda(totalCobrado)}</b>
            </div>
            {cobros.length === 0 ? (
              <p className="vacio">Sin cobros registrados hoy.</p>
            ) : (
              <ul className="lista">
                {cobros.map((p) => (
                  <li key={p.id}>
                    <span>{p.paciente}<span className="persona__detalle" style={{ display: 'block' }}>{MEDIOS_PAGO[p.medioPago]}</span></span>
                    <b>{fmtMoneda(p.monto)}</b>
                  </li>
                ))}
              </ul>
            )}
            <Link to="/pagos">Ir a Pagos de pacientes</Link>
          </section>
        </div>
      </div>

      {nuevoTurno && (
        <Modal titulo="Nuevo turno" subtitulo="Completá los datos para agendar al paciente" onCerrar={() => setNuevoTurno(false)}>
          <TurnoForm
            onCancelar={() => setNuevoTurno(false)}
            onGuardado={() => {
              setNuevoTurno(false);
              cargar();
            }}
          />
        </Modal>
      )}
    </div>
  );
}
