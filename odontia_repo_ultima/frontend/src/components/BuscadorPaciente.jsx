import { useEffect, useState } from 'react';
import { api } from '../api/client';

/** Busca por apellido, nombre o DNI (con espera de 300 ms entre teclas) y devuelve el paciente elegido. */
export default function BuscadorPaciente({ seleccionado, onSeleccionar }) {
  const [q, setQ] = useState('');
  const [resultados, setResultados] = useState([]);

  useEffect(() => {
    if (q.trim().length < 2) {
      setResultados([]);
      return;
    }
    const ctrl = new AbortController();
    const t = setTimeout(() => {
      api(`/pacientes?q=${encodeURIComponent(q.trim())}`, { signal: ctrl.signal })
        .then(setResultados)
        .catch(() => {});
    }, 300);
    return () => {
      clearTimeout(t);
      ctrl.abort();
    };
  }, [q]);

  if (seleccionado) {
    return (
      <div className="campo">
        Paciente
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', fontWeight: 400 }}>
          <span>
            <b>{seleccionado.apellido}, {seleccionado.nombre}</b> · DNI {seleccionado.dni}
          </span>
          <button type="button" className="chico" onClick={() => onSeleccionar(null)}>
            Cambiar
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="campo" style={{ position: 'relative' }}>
      Paciente
      <input placeholder="Apellido, nombre o DNI…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
      {resultados.length > 0 && (
        <ul
          style={{
            position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 5, margin: 0, padding: 0, listStyle: 'none',
            background: '#fff', border: '1px solid #c9d1e6', borderRadius: 6, maxHeight: 240, overflowY: 'auto', fontWeight: 400,
          }}
        >
          {resultados.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                style={{ width: '100%', textAlign: 'left', border: 0, borderRadius: 0 }}
                onClick={() => onSeleccionar(p)}
              >
                {p.apellido}, {p.nombre} · DNI {p.dni}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
