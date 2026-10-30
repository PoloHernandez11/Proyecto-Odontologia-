import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { fmtFechaHora, fmtMoneda, MEDIOS_PAGO } from '../utils/formato';

/**
 * Cuenta corriente del paciente: cargos (atenciones con importe), pagos y saldo.
 * La secretaria registra cobros; el odontólogo además puede anularlos.
 */
export default function PagosPaciente({ pacienteId }) {
  const { esOdontologo } = useAuth();
  const [cuenta, setCuenta] = useState(null);
  const [form, setForm] = useState({ monto: '', medioPago: 'Efectivo', concepto: '' });
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    try {
      setCuenta(await api(`/pacientes/${pacienteId}/cuenta`));
    } catch (e) {
      setError(e.message);
    }
  }, [pacienteId]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function registrar(e) {
    e.preventDefault();
    setGuardando(true);
    setError(null);
    try {
      await api(`/pacientes/${pacienteId}/pagos`, {
        method: 'POST',
        body: { ...form, monto: Number(form.monto), concepto: form.concepto || null },
      });
      setForm({ monto: '', medioPago: form.medioPago, concepto: '' });
      await cargar();
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  async function anular(p) {
    if (!window.confirm(`¿Anular el pago de ${fmtMoneda(p.monto)} del ${fmtFechaHora(p.fecha)}?`)) return;
    try {
      await api(`/pagos/${p.id}/anular`, { method: 'PATCH' });
      await cargar();
    } catch (e) {
      setError(e.message);
    }
  }

  if (!cuenta) return error ? <p className="error">{error}</p> : <p className="vacio">Cargando…</p>;

  const set = (campo) => (e) => setForm((f) => ({ ...f, [campo]: e.target.value }));

  return (
    <div className="pagina">
      <div className="kpis">
        <div className="kpi"><div className="kpi__valor">{fmtMoneda(cuenta.totalCargos)}</div><div className="kpi__titulo">Total atenciones</div></div>
        <div className="kpi"><div className="kpi__valor positivo">{fmtMoneda(cuenta.totalPagos)}</div><div className="kpi__titulo">Total pagado</div></div>
        <div className="kpi">
          <div className={`kpi__valor ${cuenta.saldo > 0 ? 'negativo' : 'positivo'}`}>{fmtMoneda(Math.abs(cuenta.saldo))}</div>
          <div className="kpi__titulo">{cuenta.saldo > 0 ? 'Saldo adeudado' : cuenta.saldo < 0 ? 'Saldo a favor' : 'Sin deuda'}</div>
        </div>
      </div>

      <form className="tarjeta" onSubmit={registrar}>
        <h2>Registrar pago</h2>
        <div className="form-grilla">
          <label>Monto<input type="number" required min="0.01" step="0.01" value={form.monto} onChange={set('monto')} /></label>
          <label>
            Medio de pago
            <select value={form.medioPago} onChange={set('medioPago')}>
              {Object.entries(MEDIOS_PAGO).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </label>
          <label>Concepto<input maxLength={255} placeholder="Ej: seña tratamiento de conducto" value={form.concepto} onChange={set('concepto')} /></label>
        </div>
        {error && <p className="error" role="alert">{error}</p>}
        <div className="acciones">
          <button type="submit" className="primario" disabled={guardando}>{guardando ? 'Registrando…' : 'Registrar pago'}</button>
        </div>
      </form>

      <section className="tarjeta tabla-scroll">
        <h2>Pagos</h2>
        {cuenta.pagos.length === 0 ? (
          <p className="vacio">Sin pagos registrados.</p>
        ) : (
          <table className="tabla">
            <thead>
              <tr><th>Fecha</th><th>Concepto</th><th>Medio</th><th>Registró</th><th className="num">Monto</th><th /></tr>
            </thead>
            <tbody>
              {cuenta.pagos.map((p) => (
                <tr key={p.id} className={p.anulado ? 'anulado' : ''}>
                  <td>{fmtFechaHora(p.fecha)}</td>
                  <td>{p.concepto}{p.anulado && ' (anulado)'}</td>
                  <td>{MEDIOS_PAGO[p.medioPago]}</td>
                  <td>{p.registradoPor}</td>
                  <td className="num">{fmtMoneda(p.monto)}</td>
                  <td>
                    {esOdontologo && !p.anulado && (
                      <button type="button" className="chico peligro" onClick={() => anular(p)}>Anular</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {cuenta.cargos.length > 0 && (
        <section className="tarjeta">
          <h2>Atenciones con cargo</h2>
          <table className="tabla">
            <tbody>
              {cuenta.cargos.map((c) => (
                <tr key={c.historialId}>
                  <td>Atención del {fmtFechaHora(c.fecha)}</td>
                  <td className="num">{fmtMoneda(c.importe)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  );
}
