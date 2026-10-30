import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { ESTADOS_LAB, fmtFecha, fmtMoneda, hoyIso, MEDIOS_PAGO, TIPOS_TRABAJO_LAB } from '../utils/formato';
import BuscadorPaciente from './BuscadorPaciente';
import Modal from './Modal';

/**
 * Lista de trabajos de laboratorio con sus acciones (recibir, colocar, reenviar, pagar).
 * Con `paciente` muestra solo los de ese paciente (pestaña de la ficha); sin él, todos (página Laboratorio).
 */
export default function TrabajosLaboratorio({ paciente }) {
  const { esOdontologo } = useAuth();
  const [estado, setEstado] = useState(paciente ? '' : 'Enviado');
  const [trabajos, setTrabajos] = useState([]);
  const [modal, setModal] = useState(null); // { tipo: 'nuevo' | 'editar' | 'pagar', trabajo? }
  const [error, setError] = useState(null);

  const cargar = useCallback(async () => {
    setError(null);
    const params = new URLSearchParams();
    if (estado) params.set('estado', estado);
    if (paciente) params.set('pacienteId', paciente.id);
    try {
      setTrabajos(await api(`/laboratorio?${params}`));
    } catch (e) {
      setError(e.message);
    }
  }, [estado, paciente]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function cambiarEstado(t, nuevo) {
    if (nuevo === 'Cancelado' && !window.confirm('¿Cancelar este trabajo?')) return;
    try {
      await api(`/laboratorio/${t.id}/estado`, { method: 'PATCH', body: { estado: nuevo, fecha: hoyIso() } });
      await cargar();
    } catch (e) {
      setError(e.message);
    }
  }

  const hoy = hoyIso();
  const cerrar = () => setModal(null);
  const guardado = () => {
    setModal(null);
    cargar();
  };

  return (
    <div className="pagina">
      <div className="filtros" style={{ justifyContent: 'space-between' }}>
        <label className="campo">
          Estado
          <select value={estado} onChange={(e) => setEstado(e.target.value)}>
            <option value="">Todos</option>
            {Object.entries(ESTADOS_LAB).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <button type="button" className="primario" onClick={() => setModal({ tipo: 'nuevo' })}>
          + Enviar trabajo a laboratorio
        </button>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <section className="tarjeta tabla-scroll">
        {trabajos.length === 0 ? (
          <p className="vacio">No hay trabajos{estado ? ` en estado "${ESTADOS_LAB[estado]}"` : ''}.</p>
        ) : (
          <table className="tabla">
            <thead>
              <tr>
                {!paciente && <th>Paciente</th>}
                <th>Trabajo</th>
                <th>Laboratorio</th>
                <th>Salida</th>
                <th>Entrega est.</th>
                <th>Ingreso</th>
                <th>Estado</th>
                <th className="num">Costo</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {trabajos.map((t) => {
                const atrasado = t.estado === 'Enviado' && t.fechaEntregaEstimada && t.fechaEntregaEstimada < hoy;
                return (
                  <tr key={t.id}>
                    {!paciente && <td><Link to={`/pacientes/${t.pacienteId}`}>{t.paciente}</Link></td>}
                    <td>
                      {t.tipoTrabajo}
                      {t.piezas && <span className="vacio"> · piezas {t.piezas}</span>}
                      {t.descripcion && <div className="vacio" style={{ fontSize: '0.82rem' }}>{t.descripcion}</div>}
                    </td>
                    <td>{t.laboratorio}</td>
                    <td>{fmtFecha(t.fechaEnvio)}</td>
                    <td className={atrasado ? 'negativo' : ''}>
                      {fmtFecha(t.fechaEntregaEstimada)}{atrasado && ' ⚠'}
                    </td>
                    <td>{fmtFecha(t.fechaRecepcion)}</td>
                    <td><span className={`chip chip--${t.estado.toLowerCase()}`}>{ESTADOS_LAB[t.estado]}</span></td>
                    <td className="num">
                      {t.costo != null ? fmtMoneda(t.costo) : '—'}
                      {t.costo != null && <div className={t.pagado ? 'positivo' : 'vacio'} style={{ fontSize: '0.75rem' }}>{t.pagado ? 'Pagado' : 'Impago'}</div>}
                    </td>
                    <td style={{ whiteSpace: 'nowrap', textAlign: 'right' }}>
                      {t.estado === 'Enviado' && (
                        <button type="button" className="chico" onClick={() => cambiarEstado(t, 'Recibido')}>Recibido</button>
                      )}
                      {t.estado === 'Recibido' && (
                        <>
                          <button type="button" className="chico" onClick={() => cambiarEstado(t, 'Colocado')}>Colocado</button>{' '}
                          <button type="button" className="chico" onClick={() => cambiarEstado(t, 'Enviado')} title="Devolver al laboratorio para corregir">Reenviar</button>
                        </>
                      )}{' '}
                      {esOdontologo && !t.pagado && t.costo > 0 && t.estado !== 'Cancelado' && (
                        <button type="button" className="chico" onClick={() => setModal({ tipo: 'pagar', trabajo: t })}>Pagar</button>
                      )}{' '}
                      {t.estado !== 'Cancelado' && (
                        <button type="button" className="chico" onClick={() => setModal({ tipo: 'editar', trabajo: t })}>Editar</button>
                      )}{' '}
                      {(t.estado === 'Enviado' || t.estado === 'Recibido') && !t.pagado && (
                        <button type="button" className="chico peligro" onClick={() => cambiarEstado(t, 'Cancelado')} aria-label="Cancelar trabajo">✕</button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </section>

      {(modal?.tipo === 'nuevo' || modal?.tipo === 'editar') && (
        <Modal titulo={modal.trabajo ? 'Editar trabajo' : 'Enviar trabajo a laboratorio'} onCerrar={cerrar}>
          <TrabajoForm trabajo={modal.trabajo} paciente={paciente} onCancelar={cerrar} onGuardado={guardado} />
        </Modal>
      )}
      {modal?.tipo === 'pagar' && (
        <Modal titulo="Pagar trabajo al laboratorio" onCerrar={cerrar}>
          <PagarTrabajoForm trabajo={modal.trabajo} onCancelar={cerrar} onGuardado={guardado} />
        </Modal>
      )}
    </div>
  );
}

function TrabajoForm({ trabajo, paciente: pacienteFijo, onGuardado, onCancelar }) {
  const [paciente, setPaciente] = useState(
    pacienteFijo ?? (trabajo ? { id: trabajo.pacienteId, apellido: trabajo.paciente, nombre: '', dni: '' } : null),
  );
  const [form, setForm] = useState({
    laboratorio: trabajo?.laboratorio ?? '',
    tipoTrabajo: trabajo?.tipoTrabajo ?? TIPOS_TRABAJO_LAB[0],
    piezas: trabajo?.piezas ?? '',
    descripcion: trabajo?.descripcion ?? '',
    fechaEnvio: trabajo?.fechaEnvio ?? hoyIso(),
    fechaEntregaEstimada: trabajo?.fechaEntregaEstimada ?? '',
    costo: trabajo?.costo ?? '',
  });
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);
  const set = (campo) => (e) => setForm((f) => ({ ...f, [campo]: e.target.value }));

  async function guardar(e) {
    e.preventDefault();
    if (!paciente) return setError('Elegí un paciente.');
    setGuardando(true);
    setError(null);
    const body = {
      ...form,
      pacienteId: paciente.id,
      fechaEntregaEstimada: form.fechaEntregaEstimada || null,
      costo: form.costo === '' ? null : Number(form.costo),
    };
    try {
      if (trabajo) await api(`/laboratorio/${trabajo.id}`, { method: 'PUT', body });
      else await api('/laboratorio', { method: 'POST', body });
      onGuardado();
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form onSubmit={guardar} style={{ display: 'grid', gap: '0.9rem' }}>
      {pacienteFijo || trabajo ? (
        <p style={{ margin: 0 }}>Paciente: <b>{pacienteFijo ? `${pacienteFijo.apellido}, ${pacienteFijo.nombre}` : trabajo.paciente}</b></p>
      ) : (
        <BuscadorPaciente seleccionado={paciente} onSeleccionar={setPaciente} />
      )}
      <div className="form-grilla">
        <label>Laboratorio<input required maxLength={100} value={form.laboratorio} onChange={set('laboratorio')} /></label>
        <label>
          Tipo de trabajo
          <input required maxLength={100} list="tipos-trabajo-lab" value={form.tipoTrabajo} onChange={set('tipoTrabajo')} />
          <datalist id="tipos-trabajo-lab">{TIPOS_TRABAJO_LAB.map((t) => <option key={t} value={t} />)}</datalist>
        </label>
        <label>Piezas (FDI)<input maxLength={100} placeholder="Ej: 11, 21" value={form.piezas} onChange={set('piezas')} /></label>
        <label>Fecha de salida<input type="date" required value={form.fechaEnvio} onChange={set('fechaEnvio')} /></label>
        <label>Entrega estimada<input type="date" min={form.fechaEnvio} value={form.fechaEntregaEstimada} onChange={set('fechaEntregaEstimada')} /></label>
        <label>Costo<input type="number" min="0" step="0.01" disabled={trabajo?.pagado} value={form.costo} onChange={set('costo')} /></label>
        <label className="ancho-total">
          Indicaciones al laboratorio
          <textarea rows={3} placeholder="Color, material, observaciones…" value={form.descripcion} onChange={set('descripcion')} />
        </label>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones" style={{ marginTop: 0 }}>
        <button type="button" onClick={onCancelar} disabled={guardando}>Cancelar</button>
        <button type="submit" className="primario" disabled={guardando}>{guardando ? 'Guardando…' : 'Guardar'}</button>
      </div>
    </form>
  );
}

function PagarTrabajoForm({ trabajo, onGuardado, onCancelar }) {
  const [medioPago, setMedioPago] = useState('Transferencia');
  const [fecha, setFecha] = useState(hoyIso());
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);

  async function pagar(e) {
    e.preventDefault();
    setGuardando(true);
    setError(null);
    try {
      await api(`/laboratorio/${trabajo.id}/pagar`, { method: 'PATCH', body: { medioPago, fecha } });
      onGuardado();
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form onSubmit={pagar}>
      <p style={{ marginTop: 0 }}>
        {trabajo.laboratorio}: {trabajo.tipoTrabajo} — <b>{fmtMoneda(trabajo.costo)}</b>.
        Se registrará como egreso en la caja.
      </p>
      <div className="form-grilla">
        <label>
          Medio de pago
          <select value={medioPago} onChange={(e) => setMedioPago(e.target.value)}>
            {Object.entries(MEDIOS_PAGO).filter(([k]) => k !== 'ObraSocial').map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label>Fecha<input type="date" required value={fecha} onChange={(e) => setFecha(e.target.value)} /></label>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones">
        <button type="button" onClick={onCancelar} disabled={guardando}>Cancelar</button>
        <button type="submit" className="primario" disabled={guardando}>{guardando ? 'Registrando…' : 'Registrar pago'}</button>
      </div>
    </form>
  );
}
