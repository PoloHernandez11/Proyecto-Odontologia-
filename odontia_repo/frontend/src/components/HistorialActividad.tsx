import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { ActividadPermisoDto } from '../types/permisos';
import { ETIQUETA_ACCION, ETIQUETA_MODULO, ETIQUETA_NIVEL } from './MatrizPermisos';
import './EquipoInvitaciones.css';

/** Acciones que hizo la secretaria en módulos con permiso. Solo lo ve el odontólogo. */
export default function HistorialActividad() {
  const [registros, setRegistros] = useState<ActividadPermisoDto[] | null>(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cargar = useCallback(() => {
    setCargando(true);
    setError(null);
    api<ActividadPermisoDto[]>('/permisos/actividad')
      .then(setRegistros)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No se pudo cargar el historial.'))
      .finally(() => setCargando(false));
  }, []);

  useEffect(cargar, [cargar]);

  return (
    <section className="tarjeta tabla-scroll">
      <div className="historial__cabecera">
        <div>
          <h2>Historial de la secretaria</h2>
          <p className="tenue" style={{ marginTop: 0 }}>
            Cada acción que hace la secretaria en un módulo con permiso, con el nivel con que se hizo.
            Las últimas 200.
          </p>
        </div>
        <button type="button" className="secundario" onClick={cargar} disabled={cargando}>
          {cargando ? 'Actualizando…' : 'Actualizar'}
        </button>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      {registros && registros.length === 0 && (
        <p className="tenue">Todavía no hay acciones registradas de la secretaria.</p>
      )}

      {registros && registros.length > 0 && (
        <table className="tabla">
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Quién</th>
              <th>Módulo</th>
              <th>Acción</th>
              <th>Nivel</th>
            </tr>
          </thead>
          <tbody>
            {registros.map((r) => (
              <tr key={r.id}>
                <td>{new Date(r.creadoEn).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })}</td>
                <td>
                  <span className="persona__nombre">{r.usuario}</span>
                  <small style={{ display: 'block', color: '#6b7b80' }}>{r.email}</small>
                </td>
                <td>{ETIQUETA_MODULO[r.modulo].titulo}</td>
                <td>{ETIQUETA_ACCION[r.accion]}</td>
                <td>
                  <span className={`chip chip--${r.nivel === 'ConAprobacion' ? 'aviso' : 'ok'}`}>
                    {ETIQUETA_NIVEL[r.nivel]}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
