import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import Icono from '../components/Icono';
import Modal from '../components/Modal';
import TurnoAcciones from '../components/TurnoAcciones';
import TurnoForm from '../components/TurnoForm';
import {
  capitalizar, claseEstado, ESTADOS_TURNO, finTurno, fmtFecha, fmtFechaLarga, fmtHora, horaDeMinutos, hoyIso,
  iniciales, lunesDe, minutos, sumarDias, turnoActivo,
} from '../utils/formato';
import './Agenda.css';

const CONFIG_POR_DEFECTO = {
  horaApertura: '08:00:00', horaCierre: '20:00:00', almuerzoInicio: '13:00:00', almuerzoFin: '14:00:00',
  atiendeSabados: true, duracionTurnoMinutos: 30,
};
const DIAS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

function rango(vista, fecha) {
  if (vista === 'dia') return [fecha, fecha];
  if (vista === 'semana') {
    const l = lunesDe(fecha);
    return [l, sumarDias(l, 6)];
  }
  const primero = `${fecha.slice(0, 8)}01`;
  const desde = lunesDe(primero);
  return [desde, sumarDias(desde, 41)]; // 6 semanas
}

function titulo(vista, fecha) {
  if (vista === 'dia') return capitalizar(fmtFechaLarga(fecha));
  if (vista === 'semana') {
    const [d, h] = rango('semana', fecha);
    return `Semana del ${fmtFecha(d)} al ${fmtFecha(h)}`;
  }
  return capitalizar(new Date(`${fecha}T00:00:00`).toLocaleDateString('es-AR', { month: 'long', year: 'numeric' }));
}

/** Agenda del consultorio: un solo odontólogo, una sola columna de turnos. */
export default function Agenda() {
  const [vista, setVista] = useState('dia');
  const [fecha, setFecha] = useState(hoyIso());
  const [turnos, setTurnos] = useState([]);
  const [config, setConfig] = useState(CONFIG_POR_DEFECTO);
  const [seleccionado, setSeleccionado] = useState(null);
  const [nuevo, setNuevo] = useState(null); // { fecha, hora }
  const [error, setError] = useState(null);

  useEffect(() => {
    api('/turnos/configuracion').then(setConfig).catch(() => {});
  }, []);

  const [desde, hasta] = rango(vista, fecha);
  const cargar = useCallback(async () => {
    setError(null);
    try {
      const t = await api(`/turnos?desde=${desde}&hasta=${hasta}`);
      setTurnos(t);
      setSeleccionado((s) => (s ? t.find((x) => x.id === s.id) ?? null : null));
    } catch (e) {
      setError(e.message);
    }
  }, [desde, hasta]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const porDia = useMemo(() => {
    const m = new Map();
    for (const t of turnos) m.set(t.fecha, [...(m.get(t.fecha) ?? []), t]);
    return m;
  }, [turnos]);

  function mover(signo) {
    const dias = vista === 'dia' ? 1 : vista === 'semana' ? 7 : 0;
    if (dias) setFecha((f) => sumarDias(f, signo * dias));
    else {
      const d = new Date(`${fecha.slice(0, 8)}01T00:00:00`);
      d.setMonth(d.getMonth() + signo);
      setFecha(d.toLocaleDateString('en-CA'));
    }
  }

  function irADia(f) {
    setFecha(f);
    setVista('dia');
  }

  const delDia = (porDia.get(fecha) ?? []).filter(turnoActivo);
  const minutosOcupados = delDia.reduce((s, t) => s + t.duracionMinutos, 0);

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Agenda</h1>
          <p className="subtitulo">Turnos del consultorio por día, semana o mes</p>
        </div>
        <button type="button" className="primario" onClick={() => setNuevo({ fecha: fecha < hoyIso() ? hoyIso() : fecha })}>
          <Icono nombre="mas" tamano={16} /> Nuevo turno
        </button>
      </div>

      <div className="agenda__barra">
        <div className="filtros" style={{ alignItems: 'center' }}>
          <button type="button" onClick={() => setFecha(hoyIso())}>Hoy</button>
          <button type="button" className="fantasma" aria-label="Anterior" onClick={() => mover(-1)}><Icono nombre="izquierda" /></button>
          <button type="button" className="fantasma" aria-label="Siguiente" onClick={() => mover(1)}><Icono nombre="derecha" /></button>
          <b>{titulo(vista, fecha)}</b>
          <input type="date" aria-label="Ir a fecha" value={fecha} onChange={(e) => e.target.value && setFecha(e.target.value)} />
        </div>
        <div className="segmentado" role="group" aria-label="Vista">
          {[['dia', 'Día'], ['semana', 'Semana'], ['mes', 'Mes']].map(([v, t]) => (
            <button key={v} type="button" aria-pressed={vista === v} onClick={() => setVista(v)}>{t}</button>
          ))}
        </div>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <div className="agenda">
        <section className="tarjeta agenda__principal">
          {vista === 'dia' && (
            <LineaDeTiempo
              dias={[fecha]}
              porDia={porDia}
              config={config}
              seleccionado={seleccionado}
              onElegir={setSeleccionado}
              onLibre={(f, hora) => f >= hoyIso() && setNuevo({ fecha: f, hora })}
            />
          )}
          {vista === 'semana' && (
            <LineaDeTiempo
              dias={Array.from({ length: config.atiendeSabados ? 6 : 5 }, (_, i) => sumarDias(desde, i))}
              porDia={porDia}
              config={config}
              seleccionado={seleccionado}
              onElegir={setSeleccionado}
              onLibre={(f, hora) => f >= hoyIso() && setNuevo({ fecha: f, hora })}
              onDia={irADia}
              compacta
            />
          )}
          {vista === 'mes' && <Mes desde={desde} mes={fecha.slice(0, 7)} porDia={porDia} onDia={irADia} />}
        </section>

        <aside className="pagina">
          <section className="tarjeta">
            <h2>Turno seleccionado</h2>
            {seleccionado ? (
              <DetalleTurno turno={seleccionado} onCambio={cargar} onError={setError} />
            ) : (
              <p className="vacio">Elegí un turno en la agenda para ver el detalle, o hacé clic en un horario libre para agendar.</p>
            )}
          </section>

          <section className="tarjeta">
            <h2>Estados del turno</h2>
            <div className="leyenda">
              {['Pendiente', 'Confirmado', 'EnSala', 'Atendido', 'Ausente', 'Cancelado'].map((e) => (
                <span key={e} className={claseEstado(e)}>{ESTADOS_TURNO[e]}</span>
              ))}
              <span className="chip chip--urgencia">Urgencia</span>
            </div>
          </section>

          {vista === 'dia' && (
            <section className="tarjeta">
              <h2>Resumen del día</h2>
              <ul className="lista">
                <li><span>Turnos agendados</span><b>{delDia.length}</b></li>
                <li><span>Horas ocupadas</span><b>{Math.floor(minutosOcupados / 60)} h {minutosOcupados % 60} min</b></li>
                <li><span>Atendidos</span><b>{delDia.filter((t) => t.estado === 'Atendido').length}</b></li>
              </ul>
            </section>
          )}
        </aside>
      </div>

      {nuevo && (
        <Modal titulo="Nuevo turno" subtitulo="Completá los datos para agendar al paciente" onCerrar={() => setNuevo(null)}>
          <TurnoForm
            fechaInicial={nuevo.fecha}
            horaInicial={nuevo.hora}
            onCancelar={() => setNuevo(null)}
            onGuardado={(t) => {
              setNuevo(null);
              setFecha(t.fecha);
              setSeleccionado(t);
              cargar();
            }}
          />
        </Modal>
      )}
    </div>
  );
}

// ------------------------------------------------------------------------------------------------

/** Grilla horaria de uno (día) o varios días (semana). */
function LineaDeTiempo({ dias, porDia, config, seleccionado, onElegir, onLibre, onDia, compacta = false }) {
  const apertura = minutos(config.horaApertura);
  const cierre = minutos(config.horaCierre);
  const ppm = compacta ? 1 : 1.5; // píxeles por minuto
  const alto = (cierre - apertura) * ppm;
  const horas = [];
  for (let m = Math.ceil(apertura / 60) * 60; m <= cierre; m += 60) horas.push(m);
  const almuerzo = config.almuerzoInicio && config.almuerzoFin
    ? [minutos(config.almuerzoInicio), minutos(config.almuerzoFin)]
    : null;
  const hoy = hoyIso();

  function clicLibre(e, dia) {
    if (e.target !== e.currentTarget) return;
    const y = e.nativeEvent.offsetY;
    const m = Math.floor((apertura + y / ppm) / 15) * 15;
    onLibre(dia, horaDeMinutos(m));
  }

  return (
    <div className={`linea ${compacta ? 'linea--compacta' : ''}`}>
      {dias.length > 1 && (
        <div className="linea__cabeceras" style={{ gridTemplateColumns: `3.2rem repeat(${dias.length}, minmax(0, 1fr))` }}>
          <span />
          {dias.map((d) => {
            const fechaD = new Date(`${d}T00:00:00`);
            return (
              <button key={d} type="button" className={`linea__dia ${d === hoy ? 'linea__dia--hoy' : ''}`} onClick={() => onDia?.(d)}>
                {DIAS[(fechaD.getDay() + 6) % 7]} <b>{fechaD.getDate()}</b>
                <small>{(porDia.get(d) ?? []).filter(turnoActivo).length} turnos</small>
              </button>
            );
          })}
        </div>
      )}
      <div className="linea__cuerpo" style={{ gridTemplateColumns: `3.2rem repeat(${dias.length}, minmax(0, 1fr))` }}>
        <div className="linea__horas" style={{ height: alto }}>
          {horas.map((m) => (
            <span key={m} style={{ top: (m - apertura) * ppm }}>{horaDeMinutos(m)}</span>
          ))}
        </div>
        {dias.map((d) => (
          <div key={d} className="linea__columna" style={{ height: alto }} onClick={(e) => clicLibre(e, d)}
            title={d >= hoy ? 'Clic en un horario libre para agendar' : undefined}>
            {horas.map((m) => <span key={m} className="linea__guia" style={{ top: (m - apertura) * ppm }} />)}
            {almuerzo && (
              <div className="linea__bloqueo" style={{ top: (almuerzo[0] - apertura) * ppm, height: (almuerzo[1] - almuerzo[0]) * ppm }}>
                Horario bloqueado · Almuerzo
              </div>
            )}
            {(porDia.get(d) ?? []).map((t) => {
              const top = (minutos(t.hora) - apertura) * ppm;
              const h = Math.max(t.duracionMinutos * ppm, 22);
              return (
                <button
                  key={t.id}
                  type="button"
                  className={`bloque bloque--${t.estado.toLowerCase()} ${t.urgencia ? 'bloque--urgencia' : ''} ${seleccionado?.id === t.id ? 'bloque--activo' : ''}`}
                  style={{ top, height: h }}
                  onClick={() => onElegir(t)}
                  title={`${fmtHora(t.hora)}–${finTurno(t)} · ${t.paciente} · ${ESTADOS_TURNO[t.estado]}`}
                >
                  <span className="bloque__hora">{fmtHora(t.hora)}{!compacta && `–${finTurno(t)}`}</span>
                  <span className="bloque__paciente">{t.paciente}</span>
                  {!compacta && h > 40 && <span className="bloque__motivo">{t.motivo}</span>}
                </button>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}

function Mes({ desde, mes, porDia, onDia }) {
  const hoy = hoyIso();
  const celdas = Array.from({ length: 42 }, (_, i) => sumarDias(desde, i));
  return (
    <div className="mes">
      {DIAS.map((d) => <span key={d} className="mes__cabecera">{d}</span>)}
      {celdas.map((d) => {
        const del = (porDia.get(d) ?? []).filter(turnoActivo);
        const fuera = d.slice(0, 7) !== mes;
        return (
          <button key={d} type="button" className={`mes__dia ${fuera ? 'mes__dia--fuera' : ''} ${d === hoy ? 'mes__dia--hoy' : ''}`} onClick={() => onDia(d)}>
            <span className="mes__numero">{Number(d.slice(8))}</span>
            {del.length > 0 && <span className="mes__cantidad">{del.length} turno{del.length === 1 ? '' : 's'}</span>}
            {del.slice(0, 2).map((t) => (
              <span key={t.id} className={`mes__turno bloque--${t.estado.toLowerCase()}`}>
                {fmtHora(t.hora)} {t.paciente.split(',')[0]}
              </span>
            ))}
            {del.length > 2 && <span className="tenue">+{del.length - 2} más</span>}
          </button>
        );
      })}
    </div>
  );
}

function DetalleTurno({ turno: t, onCambio, onError }) {
  return (
    <div className="detalle-turno">
      <div className="persona">
        <span className="avatar avatar--grande">{iniciales(t.paciente)}</span>
        <span>
          <span className="persona__nombre">{t.paciente}</span>
          <span className="persona__detalle" style={{ display: 'block' }}>
            {t.pacienteEdad != null && `${t.pacienteEdad} años · `}{t.pacienteObraSocial ?? 'Particular'}
          </span>
        </span>
      </div>
      <div className="alertas">
        <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
        {t.urgencia && <span className="chip chip--urgencia">Urgencia</span>}
        {t.pacienteAlertas.map((a) => <span key={a} className="alerta">{a}</span>)}
      </div>
      <dl>
        <dt>Fecha y hora</dt>
        <dd>{fmtFecha(t.fecha)}, {fmtHora(t.hora)}–{finTurno(t)} ({t.duracionMinutos} min)</dd>
        <dt>Motivo</dt>
        <dd>{t.motivo ?? '—'}</dd>
        {t.notas && (<><dt>Notas</dt><dd>{t.notas}</dd></>)}
        {t.pacienteTelefono && (<><dt>Teléfono</dt><dd><a href={`tel:${t.pacienteTelefono}`}>{t.pacienteTelefono}</a></dd></>)}
        {t.horaLlegada && (<><dt>Llegó</dt><dd>{fmtHora(t.horaLlegada)}</dd></>)}
        <dt>Registrado por</dt>
        <dd>{t.registradoPor}</dd>
      </dl>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem', alignItems: 'center' }}>
        <Link to={`/pacientes/${t.pacienteId}`}>Ver ficha</Link>
        <span style={{ flex: 1 }} />
        <TurnoAcciones turno={t} onCambio={onCambio} onError={onError} />
      </div>
    </div>
  );
}
