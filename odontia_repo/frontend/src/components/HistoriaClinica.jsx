import { useMemo, useState } from 'react';
import { api } from '../api/client';
import {
  CARACTER, CIE10, parsearDiagnostico, PRACTICAS, SIGNOS_VITALES, TIPOS_CONSULTA, TIPOS_REGISTRO,
} from '../utils/clinica';
import { fmtFecha, fmtFechaHora, fmtHora, fmtMoneda, hoyIso } from '../utils/formato';
import Icono from './Icono';
import { FILAS } from './odontograma/fdi';
import './HistoriaClinica.css';

const FILTROS = [{ id: '', texto: 'Todo' }, ...Object.entries(TIPOS_REGISTRO).map(([id, texto]) => ({ id, texto }))];

/**
 * Historia clínica del paciente (solo odontólogo): registros en orden cronológico inverso,
 * filtros por tipo, impresión y el formulario de "Nueva consulta".
 */
export default function HistoriaClinica({ paciente, turnos, atenciones, turnoInicialId, onCambio }) {
  const [filtro, setFiltro] = useState('');
  const [nueva, setNueva] = useState(Boolean(turnoInicialId));
  const visibles = filtro ? atenciones.filter((a) => a.tipoRegistro === filtro) : atenciones;

  return (
    <div className="pagina">
      {nueva ? (
        <ConsultaForm
          paciente={paciente}
          turnos={turnos}
          primeraVez={atenciones.length === 0}
          turnoInicialId={turnoInicialId}
          onCancelar={() => setNueva(false)}
          onGuardado={async () => {
            setNueva(false);
            await onCambio();
          }}
        />
      ) : (
        <div className="hc__barra no-imprimir">
          <div className="pildoras" role="group" aria-label="Tipo de registro">
            {FILTROS.map((f) => (
              <button key={f.id} type="button" aria-pressed={filtro === f.id} onClick={() => setFiltro(f.id)}>{f.texto}</button>
            ))}
          </div>
          <div className="filtros">
            <button type="button" onClick={() => window.print()}><Icono nombre="imprimir" tamano={16} /> Imprimir / PDF</button>
            <button type="button" className="primario" onClick={() => setNueva(true)}><Icono nombre="mas" tamano={16} /> Nueva consulta</button>
          </div>
        </div>
      )}

      {visibles.length === 0 ? (
        <p className="vacio">{atenciones.length === 0 ? 'Sin registros en la historia clínica.' : 'No hay registros de este tipo.'}</p>
      ) : (
        visibles.map((a) => <RegistroClinico key={a.id} atencion={a} />)
      )}
    </div>
  );
}

// ------------------------------------------------------------------------------------------------

/** Vista de un registro, como la ficha de consulta: tabla de campos + prácticas realizadas. */
export function RegistroClinico({ atencion: a }) {
  const signos = SIGNOS_VITALES.filter((s) => a[s.name] != null);
  const ta = a.taMax != null || a.taMin != null ? `${a.taMax ?? '—'}/${a.taMin ?? '—'} mmHg` : null;
  const [principal, ...otros] = a.diagnosticos;

  return (
    <article className="registro">
      <header className="registro__cabecera">
        <span className="avatar"><Icono nombre="usuario" tamano={16} /></span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <b>{a.profesional}</b>
          <div className="alertas" style={{ marginTop: 4 }}>
            <span className="chip chip--sin-punto">{TIPOS_REGISTRO[a.tipoRegistro]}{a.tipoConsulta && ` · ${TIPOS_CONSULTA[a.tipoConsulta]}`}</span>
            {a.turnoFecha && <span className="registro__sello registro__sello--turno">Turno: {fmtFecha(a.turnoFecha)} {fmtHora(a.turnoHora)}</span>}
            <span className="registro__sello registro__sello--registro">Registro: {fmtFechaHora(a.fecha)}</span>
            {a.piezas && <span className="chip chip--sin-punto"><Icono nombre="diente" tamano={12} /> {a.piezas}</span>}
          </div>
        </div>
        {a.importe != null && <b>{fmtMoneda(a.importe)}</b>}
      </header>

      <dl className="registro__campos">
        {(ta || signos.length > 0) && (
          <>
            <dt>Signos vitales</dt>
            <dd>
              {[ta && `T.A. ${ta}`, ...signos.filter((s) => s.name !== 'taMax' && s.name !== 'taMin').map((s) => `${s.label.replace(/\s*\(.*\)/, '')}: ${a[s.name]}`)]
                .filter(Boolean).join(' · ')}
            </dd>
          </>
        )}
        {a.motivoConsulta && <><dt>Motivo de consulta</dt><dd>{a.motivoConsulta}</dd></>}
        {a.antecedentesExamen && <><dt>Antecedentes / examen físico</dt><dd>{a.antecedentesExamen}</dd></>}
        {principal && (
          <>
            <dt>Diagnóstico principal</dt>
            <dd>
              {principal.descripcion}{principal.codigo && ` (${principal.codigo})`}{' '}
              <span className={`chip chip--sin-punto ${principal.caracter === 'Confirmado' ? 'chip--confirmado-dx' : 'chip--presuntivo'}`}>{CARACTER[principal.caracter]}</span>
            </dd>
          </>
        )}
        {otros.length > 0 && (
          <>
            <dt>Otros diagnósticos</dt>
            <dd>
              {otros.map((d, i) => (
                <div key={i}>
                  {d.descripcion}{d.codigo && ` (${d.codigo})`}{' '}
                  <span className={`chip chip--sin-punto ${d.caracter === 'Confirmado' ? 'chip--confirmado-dx' : 'chip--presuntivo'}`}>{CARACTER[d.caracter]}</span>
                </div>
              ))}
            </dd>
          </>
        )}
        {a.diagnostico && <><dt>{principal ? 'Notas del diagnóstico' : 'Diagnóstico'}</dt><dd>{a.diagnostico}</dd></>}
        {a.tratamiento && <><dt>Tratamiento / plan</dt><dd>{a.tratamiento}</dd></>}
        {a.prescripcion && <><dt>Prescripción de medicamentos</dt><dd>{a.prescripcion}</dd></>}
        {a.informe && <><dt>Informe / resultado</dt><dd>{a.informe}</dd></>}
        {a.descripcion && <><dt>Evolución / indicaciones</dt><dd>{a.descripcion}</dd></>}
      </dl>

      {a.practicas.length > 0 && (
        <div className="registro__practicas">
          <div className="registro__practicas-titulo">Estudios y prácticas realizadas</div>
          <ul>
            {a.practicas.map((p, i) => (
              <li key={i}>
                <Icono nombre="diente" tamano={14} />
                <span>
                  {p.codigo && <span className="tenue">({p.codigo}) </span>}
                  <b>{p.descripcion}</b>
                  {p.piezas && <> · pieza{p.piezas.includes(',') ? 's' : ''} {p.piezas}</>}
                  {p.observaciones && <div className="tenue">{p.observaciones}</div>}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </article>
  );
}

// ------------------------------------------------------------------------------------------------

const DIAGNOSTICO_VACIO = { texto: '', caracter: 'Presuntivo' };
const PRACTICA_VACIA = { descripcion: '', piezas: [], observaciones: '' };

function nuevaConsulta(turno, primeraVez) {
  return {
    tipoRegistro: 'Consulta',
    tipoConsulta: primeraVez ? 'PrimeraVez' : 'Ulterior',
    ...Object.fromEntries(SIGNOS_VITALES.map((s) => [s.name, ''])),
    motivoConsulta: turno?.motivo ?? '',
    antecedentesExamen: '',
    diagnosticos: [{ ...DIAGNOSTICO_VACIO }, { ...DIAGNOSTICO_VACIO }, { ...DIAGNOSTICO_VACIO }],
    diagnostico: '',
    tratamiento: '',
    prescripcion: '',
    practicas: [],
    informe: '',
    descripcion: '',
    importe: '',
  };
}

/** "Nueva consulta": mismo orden que la planilla de atención ambulatoria. */
function ConsultaForm({ paciente, turnos, primeraVez, turnoInicialId, onCancelar, onGuardado }) {
  const hoy = hoyIso();
  // Turnos a los que se puede asociar la atención: hoy o anteriores, no cancelados ni atendidos.
  const asociables = useMemo(
    () => turnos.filter((t) => t.fecha <= hoy && !['Cancelado', 'Atendido'].includes(t.estado)).slice(0, 10),
    [turnos, hoy],
  );
  const inicial = asociables.find((t) => String(t.id) === String(turnoInicialId)) ?? asociables.find((t) => t.fecha === hoy);

  const [turnoId, setTurnoId] = useState(inicial?.id ?? '');
  const [f, setF] = useState(() => nuevaConsulta(inicial, primeraVez));
  const [practica, setPractica] = useState(PRACTICA_VACIA);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);
  const set = (campo) => (e) => setF((x) => ({ ...x, [campo]: e.target.value }));

  function setDiagnostico(i, cambios) {
    setF((x) => ({ ...x, diagnosticos: x.diagnosticos.map((d, j) => (j === i ? { ...d, ...cambios } : d)) }));
  }

  function elegirTurno(valor) {
    setTurnoId(valor);
    const t = asociables.find((x) => String(x.id) === valor);
    if (t?.motivo && !f.motivoConsulta) setF((x) => ({ ...x, motivoConsulta: t.motivo }));
  }

  function togglePieza(n) {
    setPractica((p) => ({ ...p, piezas: p.piezas.includes(n) ? p.piezas.filter((x) => x !== n) : [...p.piezas, n] }));
  }

  function agregarPractica() {
    if (!practica.descripcion.trim()) return;
    const m = practica.descripcion.trim().match(/^\((\d{2}\.\d{2})\)\s*(.+)$/); // "(01.06) Consulta…"
    setF((x) => ({
      ...x,
      practicas: [
        ...x.practicas,
        {
          codigo: m ? m[1] : null,
          descripcion: m ? m[2] : practica.descripcion.trim(),
          piezas: practica.piezas.join(',') || null,
          observaciones: practica.observaciones.trim() || null,
        },
      ],
    }));
    setPractica(PRACTICA_VACIA);
  }

  async function guardar(e) {
    e.preventDefault();
    setGuardando(true);
    setError(null);
    try {
      const num = (v) => (v === '' || v == null ? null : Number(v));
      const piezas = [...new Set(f.practicas.flatMap((p) => (p.piezas ? p.piezas.split(',') : [])))].join(',') || null;
      await api(`/pacientes/${paciente.id}/historial`, {
        method: 'POST',
        body: {
          turnoId: turnoId === '' ? null : Number(turnoId),
          tipoRegistro: f.tipoRegistro,
          tipoConsulta: f.tipoRegistro === 'Consulta' ? f.tipoConsulta : null,
          ...Object.fromEntries(SIGNOS_VITALES.map((s) => [s.name, num(f[s.name])])),
          motivoConsulta: f.motivoConsulta || null,
          antecedentesExamen: f.antecedentesExamen || null,
          diagnosticos: f.diagnosticos
            .filter((d) => d.texto.trim())
            .map((d) => ({ ...parsearDiagnostico(d.texto), caracter: d.caracter })),
          diagnostico: f.diagnostico || null,
          tratamiento: f.tratamiento || null,
          prescripcion: f.prescripcion || null,
          practicas: f.practicas,
          informe: f.informe || null,
          piezas,
          descripcion: f.descripcion || null,
          importe: num(f.importe),
        },
      });
      await onGuardado(); // recarga registros y turnos (el turno pasa a "Atendido")
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form className="tarjeta consulta" onSubmit={guardar}>
      <div className="tarjeta__cabecera">
        <h2>Nueva consulta</h2>
        <span className="tenue">{paciente.apellido}, {paciente.nombre}</span>
      </div>

      {paciente.alertas.length > 0 && (
        <div className="alertas" style={{ marginBottom: '1rem' }}>
          {paciente.alertas.map((a) => <span key={a} className="alerta"><Icono nombre="alerta" tamano={13} /> {a}</span>)}
        </div>
      )}

      <div className="form-grilla">
        <label>
          Turno
          <select value={turnoId} onChange={(e) => elegirTurno(e.target.value)}>
            <option value="">Sin turno asociado</option>
            {asociables.map((t) => (
              <option key={t.id} value={t.id}>{fmtFecha(t.fecha)} {fmtHora(t.hora)} — {t.motivo ?? 'sin motivo'}</option>
            ))}
          </select>
        </label>
        <label>
          Tipo de registro
          <select value={f.tipoRegistro} onChange={set('tipoRegistro')}>
            {Object.entries(TIPOS_REGISTRO).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        {f.tipoRegistro === 'Consulta' && (
          <div className="campo">
            Consulta
            <div style={{ display: 'flex', gap: '1rem', minHeight: '2.3rem', alignItems: 'center' }}>
              {Object.entries(TIPOS_CONSULTA).map(([k, v]) => (
                <label key={k} className="check">
                  <input type="radio" name="tipoConsulta" checked={f.tipoConsulta === k} onChange={() => setF((x) => ({ ...x, tipoConsulta: k }))} />
                  {v}
                </label>
              ))}
            </div>
          </div>
        )}
      </div>

      <h3 className="consulta__seccion">Datos antropométricos y signos vitales</h3>
      <div className="consulta__signos">
        {SIGNOS_VITALES.map((s) => (
          <label key={s.name} className="campo">
            {s.label}
            <input type="number" inputMode="decimal" min={s.min} max={s.max} step={s.step ?? 1} value={f[s.name]} onChange={set(s.name)} />
          </label>
        ))}
      </div>

      <div className="form-grilla" style={{ marginTop: '1rem' }}>
        <label className="ancho-total">
          Motivo de consulta
          <input maxLength={255} value={f.motivoConsulta} onChange={set('motivoConsulta')} />
        </label>
        <label className="ancho-total">
          Antecedentes de enfermedad actual / examen físico
          <textarea rows={3} value={f.antecedentesExamen} onChange={set('antecedentesExamen')} />
        </label>
      </div>

      <h3 className="consulta__seccion">Diagnóstico CIE-10</h3>
      <datalist id="cie10">
        {CIE10.map((d) => <option key={d.codigo} value={`${d.codigo} · ${d.descripcion}`} />)}
      </datalist>
      <div className="consulta__diagnosticos">
        {f.diagnosticos.map((d, i) => (
          <div key={i} className="consulta__diagnostico">
            <label className="campo">
              {i === 0 ? 'Diagnóstico principal' : 'Otro diagnóstico'}
              <input list="cie10" placeholder="Buscar: caries, K02.1, gingivitis…" value={d.texto} onChange={(e) => setDiagnostico(i, { texto: e.target.value })} />
            </label>
            <div className="consulta__caracter" role="radiogroup" aria-label="Carácter">
              {Object.entries(CARACTER).map(([k, v]) => (
                <label key={k} className="check" title={v}>
                  <input type="radio" name={`caracter-${i}`} checked={d.caracter === k} onChange={() => setDiagnostico(i, { caracter: k })} />
                  {k[0]}
                </label>
              ))}
            </div>
          </div>
        ))}
        <p className="tenue" style={{ margin: 0 }}>P = presuntivo · C = confirmado. Podés escribir un diagnóstico sin código.</p>
      </div>

      <div className="form-grilla" style={{ marginTop: '1rem' }}>
        <label className="ancho-total">
          Tratamiento / plan
          <textarea rows={3} value={f.tratamiento} onChange={set('tratamiento')} />
        </label>
        <label className="ancho-total">
          Prescripción de medicamentos
          <textarea rows={2} value={f.prescripcion} onChange={set('prescripcion')} placeholder="Ej: ibuprofeno 400 mg cada 8 h si hay dolor" />
        </label>
      </div>

      <h3 className="consulta__seccion">Hallazgos y prácticas</h3>
      <div className="consulta__piezas" aria-label="Piezas dentarias">
        {FILAS.map((fila) => (
          <div key={fila.id} className={`consulta__fila consulta__fila--${fila.id}`}>
            {fila.cuadrantes.map((piezas, i) => (
              <div key={i} className={`consulta__cuadrante ${i === 0 ? 'consulta__cuadrante--der' : ''}`}>
                {piezas.map((n) => (
                  <button key={n} type="button" aria-pressed={practica.piezas.includes(n)} onClick={() => togglePieza(n)}>{n}</button>
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>
      <datalist id="practicas">
        {PRACTICAS.map((p) => <option key={p} value={p} />)}
      </datalist>
      <div className="consulta__practica">
        <label className="campo">
          Hallazgo / práctica
          <input list="practicas" placeholder="Buscar práctica…" value={practica.descripcion}
            onChange={(e) => setPractica((p) => ({ ...p, descripcion: e.target.value }))}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); agregarPractica(); } }} />
        </label>
        <label className="campo">
          Pieza/s
          <input value={practica.piezas.join(', ')} readOnly placeholder="Elegilas arriba" />
        </label>
        <label className="campo" style={{ gridColumn: '1 / -1' }}>
          Observaciones / detalles
          <input value={practica.observaciones} onChange={(e) => setPractica((p) => ({ ...p, observaciones: e.target.value }))} />
        </label>
        <div>
          <button type="button" onClick={agregarPractica} disabled={!practica.descripcion.trim()}>Agregar a la lista</button>
        </div>
      </div>
      {f.practicas.length > 0 && (
        <table className="tabla" style={{ marginTop: '0.75rem' }}>
          <thead><tr><th>Hallazgo / práctica</th><th>Piezas</th><th>Observaciones</th><th /></tr></thead>
          <tbody>
            {f.practicas.map((p, i) => (
              <tr key={i}>
                <td>{p.codigo && <span className="tenue">({p.codigo}) </span>}{p.descripcion}</td>
                <td>{p.piezas ?? '—'}</td>
                <td>{p.observaciones ?? '—'}</td>
                <td className="num">
                  <button type="button" className="chico peligro" aria-label="Quitar"
                    onClick={() => setF((x) => ({ ...x, practicas: x.practicas.filter((_, j) => j !== i) }))}>✕</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="form-grilla" style={{ marginTop: '1rem' }}>
        <label className="ancho-total">
          Informe / resultado
          <textarea rows={2} value={f.informe} onChange={set('informe')} placeholder="Resultado de estudios o radiografías" />
        </label>
        <label className="ancho-total">
          Evolución / indicaciones
          <textarea rows={3} value={f.descripcion} onChange={set('descripcion')} placeholder="Indicaciones, próximos pasos, tareas para la próxima sesión…" />
        </label>
        <label>
          Honorarios (cargo en cuenta corriente)
          <input type="number" min="0" step="0.01" value={f.importe} onChange={set('importe')} placeholder="Opcional" />
        </label>
      </div>

      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones">
        <button type="button" onClick={onCancelar} disabled={guardando}>Cancelar</button>
        <button type="submit" className="primario" disabled={guardando}>
          {guardando ? 'Guardando…' : turnoId ? 'Guardar y marcar atendido' : 'Guardar consulta'}
        </button>
      </div>
      <p className="tenue" style={{ marginBottom: 0 }}>
        Los registros de la historia clínica no se editan ni se borran: las correcciones se agregan como un registro nuevo.
      </p>
    </form>
  );
}
