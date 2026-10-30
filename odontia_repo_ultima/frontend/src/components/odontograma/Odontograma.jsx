import { Diente } from './Diente';
import PanelPieza from './PanelPieza';
import { AZUL, HERRAMIENTAS, MODOS, ROJO, VERDE } from './estados';
import { FILAS } from './fdi';
import { useOdontograma } from './useOdontograma';
import './Odontograma.css';

const fechaHora = (iso) =>
  new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' });

/**
 * Odontograma interactivo con el mismo layout que la ficha dental en papel.
 * `editable` lo decide el padre según el rol; además, las versiones anteriores siempre se muestran en solo lectura.
 */
export default function Odontograma({ pacienteId, editable = false }) {
  const o = useOdontograma(pacienteId);
  const { state } = o;
  const puedeEditar = editable && o.versionVista === null;

  if (o.cargando && !state.meta && !o.error) return <p className="odonto__estado">Cargando odontograma…</p>;

  return (
    <section className="odonto">
      <header className="odonto__cabecera">
        <div>
          {state.meta ? (
            <span>
              Registrado el {fechaHora(state.meta.fechaRegistro)} por {state.meta.profesional}
            </span>
          ) : (
            <span>Sin odontograma registrado todavía.</span>
          )}
        </div>
        {o.versiones.length > 1 && (
          <label className="odonto__versiones">
            Versión:{' '}
            <select value={o.versionVista ?? ''} onChange={(e) => o.verVersion(e.target.value ? Number(e.target.value) : null)}>
              {o.versiones.map((v, i) => (
                <option key={v.id} value={i === 0 ? '' : v.id}>
                  {fechaHora(v.fechaRegistro)} {i === 0 ? '(actual)' : ''}
                </option>
              ))}
            </select>
          </label>
        )}
      </header>

      {puedeEditar && (
        <div className="odonto__herramientas" role="toolbar" aria-label="Herramientas del odontograma">
          <div className="odonto__modos" role="radiogroup" aria-label="Estado de la prestación">
            {MODOS.map((m) => (
              <button
                key={m.id}
                type="button"
                role="radio"
                aria-checked={o.modo.id === m.id}
                className={`modo ${o.modo.id === m.id ? 'modo--activo' : ''}`}
                style={{ '--color': m.color }}
                onClick={() => o.setModo(m)}
              >
                <span className="herramienta__muestra" />
                {m.etiqueta}
              </button>
            ))}
          </div>
          {HERRAMIENTAS.map((h) => (
            <button
              key={h.id}
              type="button"
              className={`herramienta ${o.herramienta.id === h.id ? 'herramienta--activa' : ''}`}
              style={{ '--color': h.conModo ? o.modo.color : h.color }}
              aria-pressed={o.herramienta.id === h.id}
              onClick={() => o.setHerramienta(h)}
            >
              <span className="herramienta__muestra" />
              {h.etiqueta}
            </button>
          ))}
        </div>
      )}

      <div className={`odonto__cuerpo ${o.seleccion ? 'odonto__cuerpo--con-panel' : ''}`}>
      <div className="odonto__grilla">
        <div className="odonto__lados">
          <span>Derecha</span>
          <span>Izquierda</span>
        </div>
        {FILAS.map((fila) => (
          <div key={fila.id} className={`odonto__fila odonto__fila--${fila.id}`} aria-label={fila.titulo}>
            {fila.cuadrantes.map((piezas, i) => (
              <div key={i} className={`odonto__cuadrante odonto__cuadrante--${i === 0 ? 'der' : 'izq'}`}>
                {piezas.map((n) => (
                  <Diente
                    key={n}
                    numero={n}
                    diente={state.dientes[n]}
                    editable={puedeEditar}
                    seleccionada={o.seleccion?.numero === n}
                    onAplicar={o.aplicar}
                    onSeleccionar={o.seleccionar}
                  />
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>

      {o.seleccion && (
        <PanelPieza
          numero={o.seleccion.numero}
          diente={state.dientes[o.seleccion.numero]}
          caraSeleccionada={o.seleccion.cara}
          editable={puedeEditar}
          herramienta={o.herramienta}
          modoEtiqueta={o.modo.etiqueta}
          historial={o.historialPieza}
          onSeleccionarCara={o.seleccionarCara}
          onAplicar={() => o.aplicar(o.seleccion.numero, o.seleccion.cara ?? 'oclusal')}
          onNota={(texto) => o.setNota(o.seleccion.numero, texto)}
          onCerrar={o.cerrarPanel}
        />
      )}
      </div>

      <ul className="odonto__leyenda">
        <li><span className="muestra" style={{ background: ROJO }} /> A realizar</li>
        <li><span className="muestra" style={{ background: AZUL }} /> Existente</li>
        <li><span className="muestra" style={{ background: VERDE }} /> Sellante</li>
        <li><b style={{ color: ROJO }}>✕</b> Extracción · <b style={{ color: AZUL }}>✕</b> Ausente</li>
        <li>○ Corona · │ Endodoncia · □ Implante · ▢ Prótesis fija · ⬚ Prótesis removible</li>
      </ul>

      <label className="odonto__observaciones">
        Observaciones
        <textarea
          rows={3}
          value={state.observaciones}
          readOnly={!puedeEditar}
          onChange={(e) => o.setObservaciones(e.target.value)}
        />
      </label>

      {o.error && <p className="odonto__error" role="alert">{o.error}</p>}

      {puedeEditar && (
        <footer className="odonto__acciones">
          <button type="button" onClick={o.descartar} disabled={!state.cambios || o.guardando}>
            Descartar cambios
          </button>
          <button type="button" className="primario" onClick={o.guardar} disabled={!state.cambios || o.guardando}>
            {o.guardando ? 'Guardando…' : 'Guardar odontograma'}
          </button>
        </footer>
      )}
      {editable && o.versionVista !== null && (
        <p className="odonto__estado">
          Estás viendo una versión anterior (solo lectura).{' '}
          <button type="button" className="link" onClick={() => o.verVersion(null)}>Volver a la actual</button>
        </p>
      )}
    </section>
  );
}
