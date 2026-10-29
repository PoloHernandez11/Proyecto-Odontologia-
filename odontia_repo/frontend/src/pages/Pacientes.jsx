import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import Icono from '../components/Icono';
import { calcularEdad, fmtFecha, fmtHora, hoyIso, iniciales } from '../utils/formato';

export default function Pacientes() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [pacientes, setPacientes] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const ctrl = new AbortController();
    const t = setTimeout(() => {
      setCargando(true);
      api(`/pacientes?q=${encodeURIComponent(q.trim())}`, { signal: ctrl.signal })
        .then(setPacientes)
        .catch((e) => e.name !== 'AbortError' && setError(e.message))
        .finally(() => setCargando(false));
    }, 250);
    return () => {
      clearTimeout(t);
      ctrl.abort();
    };
  }, [q]);

  const hoy = hoyIso();

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Pacientes</h1>
          <p className="subtitulo">{q ? `${pacientes.length} resultado${pacientes.length === 1 ? '' : 's'}` : 'Mostrando los primeros 50 por apellido'}</p>
        </div>
        <button type="button" className="primario" onClick={() => navigate('/pacientes/nuevo')}>
          <Icono nombre="mas" tamano={16} /> Nuevo paciente
        </button>
      </div>

      <label className="buscador__campo" style={{ maxWidth: 460 }}>
        <Icono nombre="buscar" tamano={16} />
        <input placeholder="Buscar por nombre, apellido, DNI o teléfono…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
      </label>
      {error && <p className="error" role="alert">{error}</p>}

      <section className="tarjeta tabla-scroll" style={{ padding: 0 }}>
        {pacientes.length === 0 ? (
          <p className="vacio" style={{ padding: '1rem' }}>{cargando ? 'Buscando…' : 'Sin resultados.'}</p>
        ) : (
          <table className="tabla">
            <thead>
              <tr>
                <th>Paciente</th>
                <th>Edad</th>
                <th>Teléfono</th>
                <th>Obra social</th>
                <th>Última visita</th>
                <th>Próximo turno</th>
              </tr>
            </thead>
            <tbody>
              {pacientes.map((p) => (
                <tr key={p.id} className="clicable" onClick={() => navigate(`/pacientes/${p.id}`)}>
                  <td>
                    <span className="persona">
                      <span className="avatar">{iniciales(`${p.nombre} ${p.apellido}`)}</span>
                      <span>
                        <span className="persona__nombre">{p.apellido}, {p.nombre}</span>
                        <span className="persona__detalle" style={{ display: 'block' }}>DNI {p.dni}</span>
                        {p.alertas.length > 0 && (
                          <span className="alertas" style={{ marginTop: 3 }}>
                            {p.alertas.slice(0, 2).map((a) => <span key={a} className="alerta">{a}</span>)}
                            {p.alertas.length > 2 && <span className="tenue">+{p.alertas.length - 2}</span>}
                          </span>
                        )}
                      </span>
                    </span>
                  </td>
                  <td>{calcularEdad(p.fechaNacimiento) ?? '—'}</td>
                  <td>{p.telefono ?? '—'}</td>
                  <td>
                    {p.obraSocial ?? 'Particular'}
                    {p.plan && <span className="tenue"> {p.plan}</span>}
                  </td>
                  <td>{p.ultimaVisita ? fmtFecha(p.ultimaVisita) : '—'}</td>
                  <td>
                    {p.proximoTurnoFecha
                      ? <>{p.proximoTurnoFecha === hoy ? 'Hoy' : fmtFecha(p.proximoTurnoFecha)} <span className="tenue">{fmtHora(p.proximoTurnoHora)}</span></>
                      : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}
