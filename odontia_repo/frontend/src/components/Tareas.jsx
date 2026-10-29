import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { CATEGORIAS_TAREA, fmtFecha, hoyIso, iniciales, PRIORIDADES } from '../utils/formato';
import './Tareas.css';

const VACIA = { texto: '', prioridad: 'Media', categoria: 'Recepción', vence: hoyIso() };

/**
 * Tareas compartidas del consultorio.
 * - compacto: lista corta de abiertas (para el inicio), con link a "Turnos y tareas".
 * - completo: alta rápida (Enter), agrupadas en Hoy / Próximas / Completadas.
 */
export default function Tareas({ compacto = false, onCambio }) {
  const [tareas, setTareas] = useState([]);
  const [nueva, setNueva] = useState(VACIA);
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    try {
      setTareas(await api(`/tareas?completadas=${!compacto}`));
    } catch (e) {
      setError(e.message);
    }
  }, [compacto]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function completar(t) {
    try {
      await api(`/tareas/${t.id}/completar?valor=${!t.completada}`, { method: 'PATCH' });
      await cargar();
      onCambio?.();
    } catch (e) {
      setError(e.message);
    }
  }

  async function eliminar(t) {
    if (!window.confirm(`¿Eliminar la tarea "${t.texto}"?`)) return;
    try {
      await api(`/tareas/${t.id}`, { method: 'DELETE' });
      await cargar();
      onCambio?.();
    } catch (e) {
      setError(e.message);
    }
  }

  async function crear(e) {
    e.preventDefault();
    if (!nueva.texto.trim()) return;
    try {
      await api('/tareas', { method: 'POST', body: { ...nueva, vence: nueva.vence || null } });
      setNueva((n) => ({ ...VACIA, prioridad: n.prioridad, categoria: n.categoria }));
      await cargar();
      onCambio?.();
    } catch (err) {
      setError(err.message);
    }
  }

  const hoy = hoyIso();
  const abiertas = tareas.filter((t) => !t.completada);
  const grupos = [
    { titulo: 'Hoy', items: abiertas.filter((t) => !t.vence || t.vence <= hoy) },
    { titulo: 'Próximas', items: abiertas.filter((t) => t.vence && t.vence > hoy) },
    { titulo: 'Completadas', items: tareas.filter((t) => t.completada) },
  ];

  const item = (t) => (
    <li key={t.id} className={`tarea ${t.completada ? 'tarea--hecha' : ''}`}>
      <input type="checkbox" checked={t.completada} onChange={() => completar(t)} aria-label="Completada" />
      <div className="tarea__cuerpo">
        <span className="tarea__texto">{t.texto}</span>
        <span className="tarea__meta">
          <span className={`prioridad prioridad--${t.prioridad.toLowerCase()}`}>{PRIORIDADES[t.prioridad]}</span>
          {t.categoria && <span className="chip chip--sin-punto">{t.categoria}</span>}
          {t.vence && (
            <span className={t.vence < hoy && !t.completada ? 'negativo' : ''}>
              {t.vence === hoy ? 'Hoy' : t.vence < hoy ? `Venció ${fmtFecha(t.vence)}` : fmtFecha(t.vence)}
            </span>
          )}
          {!compacto && <span title={t.creadaPor}>· <span className="avatar avatar--mini">{iniciales(t.creadaPor)}</span></span>}
        </span>
      </div>
      {!compacto && (
        <button type="button" className="fantasma chico" aria-label="Eliminar tarea" onClick={() => eliminar(t)}>✕</button>
      )}
    </li>
  );

  if (compacto) {
    return (
      <section className="tarjeta">
        <div className="tarjeta__cabecera">
          <h2>Tareas pendientes</h2>
          <Link to="/turnos">Ver todas</Link>
        </div>
        {error && <p className="error">{error}</p>}
        {abiertas.length === 0 ? (
          <p className="vacio">No hay tareas abiertas.</p>
        ) : (
          <ul className="tareas">{abiertas.slice(0, 6).map(item)}</ul>
        )}
      </section>
    );
  }

  return (
    <section className="tarjeta">
      <div className="tarjeta__cabecera">
        <h2>Tareas</h2>
        <span className="tenue">{abiertas.length} abiertas</span>
      </div>
      <form className="tarea-nueva" onSubmit={crear}>
        <input
          placeholder="Escribí una tarea y presioná Enter…"
          maxLength={255}
          value={nueva.texto}
          onChange={(e) => setNueva((n) => ({ ...n, texto: e.target.value }))}
        />
        <div className="tarea-nueva__opciones">
          <select aria-label="Prioridad" value={nueva.prioridad} onChange={(e) => setNueva((n) => ({ ...n, prioridad: e.target.value }))}>
            {Object.entries(PRIORIDADES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
          <select aria-label="Categoría" value={nueva.categoria} onChange={(e) => setNueva((n) => ({ ...n, categoria: e.target.value }))}>
            {CATEGORIAS_TAREA.map((c) => <option key={c}>{c}</option>)}
          </select>
          <input type="date" aria-label="Vence" value={nueva.vence} onChange={(e) => setNueva((n) => ({ ...n, vence: e.target.value }))} />
          <button type="submit" className="primario chico">Agregar</button>
        </div>
      </form>
      {error && <p className="error">{error}</p>}
      {grupos.map((g) =>
        g.items.length === 0 ? null : (
          <div key={g.titulo} className="tareas__grupo">
            <h3>{g.titulo} · {g.items.length}</h3>
            <ul className="tareas">{g.items.map(item)}</ul>
          </div>
        ),
      )}
      {tareas.length === 0 && <p className="vacio">Todavía no hay tareas.</p>}
    </section>
  );
}
