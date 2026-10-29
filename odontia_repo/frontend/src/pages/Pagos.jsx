import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import BuscadorPaciente from '../components/BuscadorPaciente';
import Modal from '../components/Modal';
import PagosPaciente from '../components/PagosPaciente';
import { fmtFechaHora, fmtMoneda, hoyIso, MEDIOS_PAGO, primerDiaDelMesIso } from '../utils/formato';

/** Registro y consulta de los cobros a pacientes (ambos roles). Cada pago se refleja solo en Finanzas. */
export default function Pagos() {
  const { esOdontologo } = useAuth();
  const [desde, setDesde] = useState(primerDiaDelMesIso());
  const [hasta, setHasta] = useState(hoyIso());
  const [medio, setMedio] = useState('');
  const [q, setQ] = useState('');
  const [pagos, setPagos] = useState([]);
  const [registrar, setRegistrar] = useState(false);
  const [paciente, setPaciente] = useState(null);
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    setError(null);
    try {
      setPagos(await api(`/pagos?desde=${desde}&hasta=${hasta}`));
    } catch (e) {
      setError(e.message);
    }
  }, [desde, hasta]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const validos = pagos.filter((p) => !p.anulado);
  const hoy = hoyIso();
  const cobradoHoy = validos.filter((p) => new Date(p.fecha).toLocaleDateString('en-CA') === hoy).reduce((s, p) => s + p.monto, 0);
  const cobradoPeriodo = validos.reduce((s, p) => s + p.monto, 0);
  const visibles = pagos.filter(
    (p) => (!medio || p.medioPago === medio) && (!q || p.paciente.toLowerCase().includes(q.toLowerCase())),
  );

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Pagos de pacientes</h1>
          <p className="subtitulo">Registro y consulta de los cobros</p>
        </div>
        <button type="button" className="primario" onClick={() => setRegistrar(true)}>Registrar pago</button>
      </div>

      <div className="kpis">
        <div className="kpi"><div className="kpi__titulo">Cobrado hoy</div><div className="kpi__valor">{fmtMoneda(cobradoHoy)}</div></div>
        <div className="kpi">
          <div className="kpi__titulo">Cobrado en el período</div>
          <div className="kpi__valor">{fmtMoneda(cobradoPeriodo)}</div>
          <div className="kpi__detalle">{validos.length} pagos</div>
        </div>
      </div>

      <div className="filtros">
        <label className="campo">Buscar<input placeholder="Paciente…" value={q} onChange={(e) => setQ(e.target.value)} /></label>
        <label className="campo">Desde<input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} /></label>
        <label className="campo">Hasta<input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} /></label>
        <label className="campo">
          Método
          <select value={medio} onChange={(e) => setMedio(e.target.value)}>
            <option value="">Todos</option>
            {Object.entries(MEDIOS_PAGO).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <section className="tarjeta tabla-scroll" style={{ padding: 0 }}>
        {visibles.length === 0 ? (
          <p className="vacio" style={{ padding: '1rem' }}>Sin pagos en el período.</p>
        ) : (
          <table className="tabla">
            <thead>
              <tr><th>Fecha</th><th>Paciente</th><th>Concepto</th><th>Método</th><th>Registró</th><th className="num">Importe</th><th>Estado</th></tr>
            </thead>
            <tbody>
              {visibles.map((p) => (
                <tr key={p.id} className={p.anulado ? 'anulado' : ''}>
                  <td>{fmtFechaHora(p.fecha)}</td>
                  <td><Link to={`/pacientes/${p.pacienteId}?tab=pagos`}>{p.paciente}</Link></td>
                  <td>{p.concepto ?? '—'}</td>
                  <td>{MEDIOS_PAGO[p.medioPago]}</td>
                  <td>{p.registradoPor}</td>
                  <td className="num">{fmtMoneda(p.monto)}</td>
                  <td><span className={`chip ${p.anulado ? 'chip--cancelado' : 'chip--atendido'}`}>{p.anulado ? 'Anulado' : 'Registrado'}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
      <p className="tenue">
        Cada pago se registra una sola vez y se refleja automáticamente{esOdontologo ? ' en Finanzas' : ''} como ingreso.
        Los pagos no se borran: {esOdontologo ? 'se anulan desde la cuenta del paciente' : 'solo el odontólogo puede anularlos'}.
      </p>

      {registrar && (
        <Modal titulo="Registrar pago" subtitulo="Se asocia a la cuenta corriente del paciente" ancho={760}
          onCerrar={() => { setRegistrar(false); setPaciente(null); cargar(); }}>
          {paciente ? (
            <>
              <p style={{ marginTop: 0 }}>
                <b>{paciente.apellido}, {paciente.nombre}</b> · DNI {paciente.dni}{' '}
                <button type="button" className="chico" onClick={() => setPaciente(null)}>Cambiar</button>
              </p>
              <PagosPaciente pacienteId={paciente.id} />
            </>
          ) : (
            <BuscadorPaciente seleccionado={null} onSeleccionar={setPaciente} />
          )}
        </Modal>
      )}
    </div>
  );
}
