import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import Modal from '../components/Modal';
import { CATEGORIAS, etiquetaCategoria, fmtFecha, fmtMoneda, hoyIso, MEDIOS_PAGO, primerDiaDelMesIso } from '../utils/formato';

/** Ingresos y egresos del consultorio (solo odontólogo). */
export default function Caja() {
  const [desde, setDesde] = useState(primerDiaDelMesIso());
  const [hasta, setHasta] = useState(hoyIso());
  const [tipo, setTipo] = useState('');
  const [resumen, setResumen] = useState(null);
  const [nuevo, setNuevo] = useState(null); // 'Ingreso' | 'Egreso'
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    setError(null);
    try {
      setResumen(await api(`/caja?desde=${desde}&hasta=${hasta}${tipo ? `&tipo=${tipo}` : ''}`));
    } catch (e) {
      setError(e.message);
    }
  }, [desde, hasta, tipo]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function eliminar(m) {
    if (!window.confirm(`¿Eliminar "${m.descripcion}" por ${fmtMoneda(m.monto)}?`)) return;
    try {
      await api(`/caja/${m.id}`, { method: 'DELETE' });
      await cargar();
    } catch (e) {
      setError(e.message);
    }
  }

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Finanzas</h1>
          <p className="subtitulo">Ingresos y egresos del consultorio · importes en pesos (ARS)</p>
        </div>
        <div className="filtros">
          <button type="button" className="primario" onClick={() => setNuevo('Ingreso')}>+ Ingreso</button>
          <button type="button" className="peligro" onClick={() => setNuevo('Egreso')}>+ Egreso</button>
        </div>
      </div>

      <div className="filtros">
        <label className="campo">Desde<input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} /></label>
        <label className="campo">Hasta<input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} /></label>
        <label className="campo">
          Tipo
          <select value={tipo} onChange={(e) => setTipo(e.target.value)}>
            <option value="">Todos</option>
            <option value="Ingreso">Ingresos</option>
            <option value="Egreso">Egresos</option>
          </select>
        </label>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      {resumen && (
        <>
          <div className="kpis">
            <div className="kpi"><div className="kpi__valor positivo">{fmtMoneda(resumen.totalIngresos)}</div><div className="kpi__titulo">Ingresos</div></div>
            <div className="kpi"><div className="kpi__valor negativo">{fmtMoneda(resumen.totalEgresos)}</div><div className="kpi__titulo">Egresos</div></div>
            <div className="kpi">
              <div className={`kpi__valor ${resumen.balance >= 0 ? 'positivo' : 'negativo'}`}>{fmtMoneda(resumen.balance)}</div>
              <div className="kpi__titulo">Balance</div>
            </div>
          </div>

          {resumen.porCategoria.length > 0 && (
            <section className="tarjeta">
              <h2>Por categoría</h2>
              <table className="tabla">
                <tbody>
                  {resumen.porCategoria.map((c) => (
                    <tr key={`${c.tipo}-${c.categoria}`}>
                      <td><span className={`chip chip--${c.tipo.toLowerCase()}`}>{c.tipo}</span></td>
                      <td>{etiquetaCategoria(c.categoria)}</td>
                      <td className="num">{fmtMoneda(c.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          <section className="tarjeta tabla-scroll">
            <h2>Movimientos</h2>
            {resumen.movimientos.length === 0 ? (
              <p className="vacio">Sin movimientos en el período.</p>
            ) : (
              <table className="tabla">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Tipo</th>
                    <th>Categoría</th>
                    <th>Descripción</th>
                    <th>Medio</th>
                    <th className="num">Monto</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {resumen.movimientos.map((m) => (
                    <tr key={m.id}>
                      <td>{fmtFecha(m.fecha)}</td>
                      <td><span className={`chip chip--${m.tipo.toLowerCase()}`}>{m.tipo}</span></td>
                      <td>{etiquetaCategoria(m.categoria)}</td>
                      <td>
                        {m.descripcion}
                        {m.automatico && <span className="vacio" title="Generado desde un pago o un trabajo de laboratorio"> · auto</span>}
                      </td>
                      <td>{MEDIOS_PAGO[m.medioPago]}</td>
                      <td className={`num ${m.tipo === 'Ingreso' ? 'positivo' : 'negativo'}`}>
                        {m.tipo === 'Egreso' ? '−' : ''}{fmtMoneda(m.monto)}
                      </td>
                      <td>
                        {!m.automatico && (
                          <button type="button" className="chico peligro" onClick={() => eliminar(m)} aria-label="Eliminar">✕</button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}

      {nuevo && (
        <Modal titulo={`Nuevo ${nuevo.toLowerCase()}`} onCerrar={() => setNuevo(null)}>
          <MovimientoForm
            tipo={nuevo}
            onCancelar={() => setNuevo(null)}
            onGuardado={() => {
              setNuevo(null);
              cargar();
            }}
          />
        </Modal>
      )}
    </div>
  );
}

function MovimientoForm({ tipo, onGuardado, onCancelar }) {
  // Los cobros a pacientes se cargan desde la ficha (Pagos), no acá.
  const categorias = Object.entries(CATEGORIAS[tipo]).filter(([k]) => k !== 'CobroPaciente');
  const [form, setForm] = useState({
    categoria: categorias[0][0],
    fecha: hoyIso(),
    descripcion: '',
    monto: '',
    medioPago: 'Efectivo',
  });
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);
  const set = (campo) => (e) => setForm((f) => ({ ...f, [campo]: e.target.value }));

  async function guardar(e) {
    e.preventDefault();
    setGuardando(true);
    setError(null);
    try {
      await api('/caja', { method: 'POST', body: { ...form, tipo, monto: Number(form.monto) } });
      onGuardado();
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form onSubmit={guardar}>
      <div className="form-grilla">
        <label>
          Categoría
          <select value={form.categoria} onChange={set('categoria')}>
            {categorias.map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label>Fecha<input type="date" required value={form.fecha} onChange={set('fecha')} /></label>
        <label>Monto<input type="number" required min="0.01" step="0.01" value={form.monto} onChange={set('monto')} /></label>
        <label>
          Medio de pago
          <select value={form.medioPago} onChange={set('medioPago')}>
            {Object.entries(MEDIOS_PAGO).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label className="ancho-total">
          Descripción
          <input required maxLength={255} value={form.descripcion} onChange={set('descripcion')}
            placeholder={tipo === 'Egreso' ? 'Ej: Compra de anestesia, alquiler octubre…' : 'Ej: Liquidación OSDE septiembre'} />
        </label>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones">
        <button type="button" onClick={onCancelar} disabled={guardando}>Cancelar</button>
        <button type="submit" className="primario" disabled={guardando}>{guardando ? 'Guardando…' : 'Registrar'}</button>
      </div>
    </form>
  );
}
