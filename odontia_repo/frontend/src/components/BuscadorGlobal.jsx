import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { iniciales } from '../utils/formato';
import Icono from './Icono';

/** "Buscar paciente, DNI o teléfono…" de la barra superior: abre la ficha del paciente elegido. */
export default function BuscadorGlobal() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [resultados, setResultados] = useState([]);
  const [abierto, setAbierto] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (q.trim().length < 2) {
      setResultados([]);
      return;
    }
    const ctrl = new AbortController();
    const t = setTimeout(() => {
      api(`/pacientes?q=${encodeURIComponent(q.trim())}`, { signal: ctrl.signal })
        .then((r) => {
          setResultados(r.slice(0, 8));
          setAbierto(true);
        })
        .catch(() => {});
    }, 250);
    return () => {
      clearTimeout(t);
      ctrl.abort();
    };
  }, [q]);

  useEffect(() => {
    const cerrar = (e) => ref.current && !ref.current.contains(e.target) && setAbierto(false);
    document.addEventListener('mousedown', cerrar);
    return () => document.removeEventListener('mousedown', cerrar);
  }, []);

  function abrir(p) {
    setQ('');
    setAbierto(false);
    navigate(`/pacientes/${p.id}`);
  }

  return (
    <div className="buscador" ref={ref}>
      <label className="buscador__campo">
        <Icono nombre="buscar" tamano={16} />
        <input
          type="search"
          placeholder="Buscar paciente, DNI o teléfono…"
          aria-label="Buscar paciente"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onFocus={() => resultados.length && setAbierto(true)}
          onKeyDown={(e) => e.key === 'Enter' && resultados[0] && abrir(resultados[0])}
        />
      </label>
      {abierto && q.trim().length >= 2 && (
        <ul className="buscador__resultados">
          {resultados.length === 0 && <li className="vacio" style={{ padding: '0.5rem' }}>Sin resultados.</li>}
          {resultados.map((p) => (
            <li key={p.id}>
              <button type="button" onClick={() => abrir(p)}>
                <span className="persona">
                  <span className="avatar">{iniciales(`${p.nombre} ${p.apellido}`)}</span>
                  <span>
                    <span className="persona__nombre">{p.apellido}, {p.nombre}</span>
                    <span className="persona__detalle" style={{ display: 'block' }}>
                      DNI {p.dni}{p.telefono && ` · ${p.telefono}`}
                    </span>
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
