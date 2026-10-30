import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import Icono from '../components/Icono';
import Tareas from '../components/Tareas';
import TurnoAcciones from '../components/TurnoAcciones';
import {
  capitalizar, claseEstado, ESTADOS_TURNO, fmtFechaLarga, fmtHora, fmtMoneda, hoyIso, iniciales, minutos, saludo,
  turnoActivo,
} from '../utils/formato';
import './DashboardOdontologo.css';

/** Inicio del odontólogo: paciente en sala, alertas clínicas, turnos del día, laboratorio y finanzas del mes. */
export default function DashboardOdontologo() {
  const { usuario } = useAuth();
  const navigate = useNavigate();
  const [turnos, setTurnos] = useState([]);
  const [stats, setStats] = useState(null);
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    try {
      const [t, s] = await Promise.all([api('/turnos'), api('/turnos/estadisticas')]);
      setTurnos(t.filter(turnoActivo));
      setStats(s);
    } catch (e) {
      setError(e.message);
    }
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const ahora = new Date().getHours() * 60 + new Date().getMinutes();
  const restantes = turnos.filter((t) => ['Pendiente', 'Confirmado', 'EnSala'].includes(t.estado));
  const proximo = restantes.find((t) => minutos(t.hora) >= ahora - 15) ?? restantes[0];
  const enSala = turnos.find((t) => t.estado === 'EnSala');
  const conAlertas = turnos.filter((t) => t.pacienteAlertas.length > 0 && t.estado !== 'Atendido');
  const balance = stats ? stats.ingresosMes - stats.egresosMes : 0;

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>{saludo()}, {usuario.nombre}</h1>
          <p className="subtitulo">
            {capitalizar(fmtFechaLarga(hoyIso()))} · Tenés {turnos.length} paciente{turnos.length === 1 ? '' : 's'} agendado{turnos.length === 1 ? '' : 's'}
          </p>
        </div>
        <button type="button" onClick={() => navigate('/agenda')}>Ver agenda</button>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      {stats && (
        <div className="kpis">
          <div className="kpi">
            <div className="kpi__titulo">Pacientes de hoy</div>
            <div className="kpi__valor">{stats.turnosHoy}</div>
            <div className="kpi__detalle">{stats.atendidosHoy} atendidos · {stats.pendientesHoy} restantes</div>
          </div>
          <div className="kpi">
            <div className="kpi__titulo">Próximo turno</div>
            <div className="kpi__valor">{proximo ? fmtHora(proximo.hora) : '—'}</div>
            <div className="kpi__detalle">{proximo ? `${proximo.paciente} · ${proximo.motivo ?? 'sin motivo'}` : 'No quedan turnos hoy'}</div>
          </div>
          <div className="kpi">
            <div className="kpi__titulo">Trabajos en laboratorio</div>
            <div className="kpi__valor">{stats.trabajosLabPendientes}</div>
            <div className="kpi__detalle">{stats.trabajosLabRecibidos} recibidos sin colocar</div>
          </div>
          <div className="kpi">
            <div className="kpi__titulo">Tareas abiertas</div>
            <div className="kpi__valor">{stats.tareasAbiertas}</div>
            <div className="kpi__detalle">{stats.porConfirmar} turnos por confirmar (hoy y mañana)</div>
          </div>
        </div>
      )}

      <div className="columnas">
        <div className="pagina">
          {enSala ? (
            <section className="tarjeta en-sala">
              <span className="en-sala__etiqueta">En sala · llegó {fmtHora(enSala.horaLlegada)}</span>
              <div className="en-sala__fila">
                <span className="persona">
                  <span className="avatar avatar--grande">{iniciales(enSala.paciente)}</span>
                  <span>
                    <span className="persona__nombre" style={{ fontSize: '1.1rem' }}>
                      {enSala.paciente}{enSala.pacienteEdad != null && `, ${enSala.pacienteEdad} años`}
                    </span>
                    <span className="persona__detalle" style={{ display: 'block' }}>
                      {enSala.motivo ?? 'Sin motivo'} · Turno {fmtHora(enSala.hora)}
                      {enSala.pacienteObraSocial && ` · ${enSala.pacienteObraSocial}`}
                    </span>
                  </span>
                </span>
                <div className="filtros">
                  <button type="button" className="primario"
                    onClick={() => navigate(`/pacientes/${enSala.pacienteId}?tab=historia&consulta=${enSala.id}`)}>
                    Iniciar consulta
                  </button>
                  <button type="button" onClick={() => navigate(`/pacientes/${enSala.pacienteId}?tab=odontograma`)}>
                    Abrir odontograma
                  </button>
                </div>
              </div>
              {enSala.pacienteAlertas.length > 0 && (
                <div className="alertas">
                  {enSala.pacienteAlertas.map((a) => <span key={a} className="alerta"><Icono nombre="alerta" tamano={13} /> {a}</span>)}
                </div>
              )}
            </section>
          ) : (
            <section className="tarjeta">
              <p className="vacio" style={{ margin: 0 }}>No hay pacientes en sala de espera. La recepción los marca al llegar.</p>
            </section>
          )}

          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Mis turnos de hoy</h2>
              <Link to="/agenda">Ver agenda</Link>
            </div>
            {turnos.length === 0 ? (
              <p className="vacio">No hay turnos para hoy.</p>
            ) : (
              <ul className="lista">
                {turnos.map((t) => (
                  <li key={t.id}>
                    <span className="persona">
                      <span className="hora" style={{ width: '3.2rem' }}>{fmtHora(t.hora)}</span>
                      <span>
                        <Link to={`/pacientes/${t.pacienteId}`} className="persona__nombre">{t.paciente}</Link>
                        <span className="persona__detalle" style={{ display: 'block' }}>{t.motivo ?? 'Sin motivo'}</span>
                      </span>
                    </span>
                    <span style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                      <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
                      <TurnoAcciones turno={t} onCambio={cargar} onError={setError} compacto />
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <div className="pagina">
          <section className="tarjeta">
            <h2>Alertas clínicas de hoy</h2>
            {conAlertas.length === 0 ? (
              <p className="vacio">Ningún paciente de hoy tiene alertas médicas cargadas.</p>
            ) : (
              <ul className="lista">
                {conAlertas.map((t) => (
                  <li key={t.id} style={{ display: 'block' }}>
                    <Link to={`/pacientes/${t.pacienteId}`}><b>{t.paciente}</b></Link> <span className="tenue">· {fmtHora(t.hora)}</span>
                    <div className="alertas" style={{ marginTop: 4 }}>
                      {t.pacienteAlertas.map((a) => <span key={a} className="alerta">{a}</span>)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {stats && (
            <section className="tarjeta">
              <div className="tarjeta__cabecera">
                <h2>Finanzas del mes</h2>
                <Link to="/caja">Ver</Link>
              </div>
              <ul className="lista">
                <li><span>Ingresos registrados</span><b className="positivo">{fmtMoneda(stats.ingresosMes)}</b></li>
                <li><span>Egresos registrados</span><b className="negativo">{fmtMoneda(stats.egresosMes)}</b></li>
                <li><span>Balance neto</span><b className={balance >= 0 ? 'positivo' : 'negativo'}>{fmtMoneda(balance)}</b></li>
              </ul>
            </section>
          )}

          <Tareas compacto onCambio={cargar} />
        </div>
      </div>
    </div>
  );
}
