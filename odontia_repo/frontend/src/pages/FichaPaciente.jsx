import { useCallback, useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import HistoriaClinica from '../components/HistoriaClinica';
import Icono from '../components/Icono';
import Modal from '../components/Modal';
import Odontograma from '../components/odontograma/Odontograma';
import PacienteForm, { ANTECEDENTES } from '../components/PacienteForm';
import PagosPaciente from '../components/PagosPaciente';
import TrabajosLaboratorio from '../components/TrabajosLaboratorio';
import TurnoAcciones from '../components/TurnoAcciones';
import TurnoForm from '../components/TurnoForm';
import {
  calcularEdad, claseEstado, ESTADOS_TURNO, finTurno, fmtFecha, fmtFechaHora, fmtHora, fmtMoneda, hoyIso, iniciales,
} from '../utils/formato';
import './FichaPaciente.css';

const PESTANAS = [
  { id: 'resumen', titulo: 'Resumen', soloOdontologo: false },
  { id: 'odontograma', titulo: 'Odontograma', soloOdontologo: true },
  { id: 'historia', titulo: 'Historia clínica', soloOdontologo: true },
  { id: 'turnos', titulo: 'Turnos', soloOdontologo: false },
  { id: 'laboratorio', titulo: 'Laboratorio', soloOdontologo: false },
  { id: 'pagos', titulo: 'Pagos y cuenta corriente', soloOdontologo: false },
];

/**
 * Ficha del paciente.
 * - Secretaria: resumen (datos, cobertura y alertas), turnos, laboratorio y pagos.
 * - Odontólogo: además odontograma e historia clínica (consultas, diagnósticos, recetas, estudios).
 * Las pestañas clínicas ni siquiera se piden al backend si el rol no corresponde (y el backend igual las bloquea).
 */
export default function FichaPaciente() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const { esOdontologo } = useAuth();
  const [paciente, setPaciente] = useState(null);
  const [turnos, setTurnos] = useState([]);
  const [atenciones, setAtenciones] = useState([]);
  const [modal, setModal] = useState(null); // 'editar' | 'turno'
  const [error, setError] = useState(null);

  const pestanas = PESTANAS.filter((p) => esOdontologo || !p.soloOdontologo);
  const pedida = params.get('tab');
  const pestana = pestanas.some((p) => p.id === pedida) ? pedida : 'resumen';
  const turnoConsulta = params.get('consulta');

  const cargar = useCallback(async () => {
    setError(null);
    try {
      const [p, t, a] = await Promise.all([
        api(`/pacientes/${id}`),
        api(`/pacientes/${id}/turnos`),
        esOdontologo ? api(`/pacientes/${id}/historial`) : Promise.resolve([]),
      ]);
      setPaciente(p);
      setTurnos(t);
      setAtenciones(a);
    } catch (e) {
      setError(e.message);
    }
  }, [id, esOdontologo]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  function irA(tab) {
    setParams(tab === 'resumen' ? {} : { tab });
  }

  if (error && !paciente) return <p className="error" role="alert">{error}</p>;
  if (!paciente) return <p className="vacio">Cargando ficha…</p>;

  const edad = calcularEdad(paciente.fechaNacimiento);
  const enTratamiento = turnos.some((t) => t.fecha >= hoyIso() && ['Pendiente', 'Confirmado', 'EnSala'].includes(t.estado));

  return (
    <div className="pagina">
      <div>
        <h1 style={{ marginBottom: 0 }}>Ficha del paciente</h1>
        <p className="subtitulo">
          <Link to="/pacientes">Pacientes</Link> › {paciente.nombre} {paciente.apellido}
          {pestana !== 'resumen' && <> › {pestanas.find((p) => p.id === pestana).titulo}</>}
        </p>
      </div>

      <section className="tarjeta ficha__cabecera">
        <div className="ficha__identidad">
          <span className="avatar avatar--grande">{iniciales(`${paciente.nombre} ${paciente.apellido}`)}</span>
          <div style={{ minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
              <h2 className="ficha__nombre">{paciente.nombre} {paciente.apellido}</h2>
              <span className={`chip ${enTratamiento ? 'chip--confirmado' : ''}`}>{enTratamiento ? 'Con turnos próximos' : 'Sin turnos próximos'}</span>
            </div>
            <p className="ficha__datos">
              {edad !== null && <span><Icono nombre="usuario" tamano={14} /> {edad} años</span>}
              <span>DNI {paciente.dni}</span>
              <span><Icono nombre="escudo" tamano={14} /> {paciente.obraSocial ?? 'Particular'}{paciente.plan && ` ${paciente.plan}`}</span>
              {paciente.telefono && <a href={`tel:${paciente.telefono}`}><Icono nombre="telefono" tamano={14} /> {paciente.telefono}</a>}
            </p>
          </div>
        </div>
        <div className="ficha__acciones">
          {paciente.alertas.map((a) => <span key={a} className="alerta"><Icono nombre="alerta" tamano={13} /> {a}</span>)}
          <button type="button" onClick={() => setModal('editar')}><Icono nombre="editar" tamano={15} /> Editar</button>
          <button type="button" className="primario" onClick={() => setModal('turno')}><Icono nombre="mas" tamano={15} /> Nuevo turno</button>
        </div>
      </section>

      <div className="ficha__pestanas" role="tablist">
        {pestanas.map((p) => (
          <button key={p.id} role="tab" type="button" aria-selected={pestana === p.id}
            className={pestana === p.id ? 'activa' : ''} onClick={() => irA(p.id)}>
            {p.titulo}
          </button>
        ))}
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      <section role="tabpanel">
        {pestana === 'resumen' && (
          <Resumen paciente={paciente} turnos={turnos} atenciones={atenciones} esOdontologo={esOdontologo} irA={irA} />
        )}
        {pestana === 'odontograma' && esOdontologo && (
          <div className="tarjeta"><Odontograma pacienteId={paciente.id} editable /></div>
        )}
        {pestana === 'historia' && esOdontologo && (
          <HistoriaClinica
            key={turnoConsulta ?? 'hc'}
            paciente={paciente}
            turnos={turnos}
            atenciones={atenciones}
            turnoInicialId={turnoConsulta}
            onCambio={async () => {
              if (turnoConsulta) setParams({ tab: 'historia' });
              await cargar();
            }}
          />
        )}
        {pestana === 'turnos' && <TurnosPaciente turnos={turnos} atenciones={atenciones} onCambio={cargar} onError={setError} />}
        {pestana === 'laboratorio' && <TrabajosLaboratorio paciente={paciente} />}
        {pestana === 'pagos' && <PagosPaciente pacienteId={paciente.id} />}
      </section>

      {modal === 'editar' && (
        <Modal titulo="Editar paciente" ancho={820} onCerrar={() => setModal(null)}>
          <PacienteForm
            paciente={paciente}
            onCancelar={() => setModal(null)}
            onGuardado={(p) => {
              setPaciente(p);
              setModal(null);
            }}
          />
        </Modal>
      )}
      {modal === 'turno' && (
        <Modal titulo="Nuevo turno" subtitulo={`${paciente.apellido}, ${paciente.nombre}`} onCerrar={() => setModal(null)}>
          <TurnoForm
            paciente={paciente}
            onCancelar={() => setModal(null)}
            onGuardado={() => {
              setModal(null);
              cargar();
            }}
          />
        </Modal>
      )}
    </div>
  );
}

// ------------------------------------------------------------------------------------------------

function Dato({ titulo, children }) {
  return (
    <div>
      <dt>{titulo}</dt>
      <dd>{children || '—'}</dd>
    </div>
  );
}

function Resumen({ paciente: p, turnos, atenciones, esOdontologo, irA }) {
  const [cuenta, setCuenta] = useState(null);
  useEffect(() => {
    api(`/pacientes/${p.id}/cuenta`).then(setCuenta).catch(() => {});
  }, [p.id]);

  const hoy = hoyIso();
  const proximos = turnos
    .filter((t) => t.fecha >= hoy && ['Pendiente', 'Confirmado', 'EnSala'].includes(t.estado))
    .reverse()
    .slice(0, 3);
  const antecedentes = ANTECEDENTES.filter((a) => p[a.name]).map((a) => a.label);
  const ultima = atenciones[0];

  return (
    <div className="columnas">
      <div className="pagina">
        <section className="tarjeta">
          <h2>Datos personales</h2>
          <dl className="datos__grilla">
            <Dato titulo="Fecha de nacimiento">{fmtFecha(p.fechaNacimiento)}</Dato>
            <Dato titulo="Sexo">{p.sexo}</Dato>
            <Dato titulo="Ocupación">{p.ocupacion}</Dato>
            <Dato titulo="Domicilio">{p.domicilio}</Dato>
            <Dato titulo="Correo">{p.email}</Dato>
            <Dato titulo="Contacto de emergencia">{p.contactoEmergencia}</Dato>
            <Dato titulo="Obra social / plan">{[p.obraSocial, p.plan].filter(Boolean).join(' ')}</Dato>
            <Dato titulo="N.º de afiliado">{p.nroAfiliado}</Dato>
            <Dato titulo="Consentimiento informado">{p.consentimientoFirmado ? 'Firmado' : 'Pendiente'}</Dato>
          </dl>
        </section>

        {esOdontologo && (
          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Última evolución</h2>
              <button type="button" className="link" onClick={() => irA('historia')}>Ver historia</button>
            </div>
            {ultima ? (
              <div>
                <p className="tenue" style={{ marginTop: 0 }}>{fmtFechaHora(ultima.fecha)} · {ultima.profesional}{ultima.piezas && ` · pieza ${ultima.piezas}`}</p>
                {ultima.motivoConsulta && <p style={{ margin: '0 0 0.3rem' }}><b>{ultima.motivoConsulta}</b></p>}
                <p style={{ margin: 0, whiteSpace: 'pre-wrap' }}>
                  {ultima.descripcion || ultima.tratamiento || ultima.diagnosticos[0]?.descripcion || '—'}
                </p>
              </div>
            ) : (
              <p className="vacio">Sin registros clínicos todavía.</p>
            )}
          </section>
        )}
      </div>

      <div className="pagina">
        <section className="tarjeta">
          <h2>Antecedentes y alertas</h2>
          {p.alertas.length > 0 && (
            <div className="alertas" style={{ marginBottom: '0.6rem' }}>
              {p.alertas.map((a) => <span key={a} className="alerta">{a}</span>)}
            </div>
          )}
          <ul className="lista">
            <li><span>Antecedentes</span><span>{antecedentes.length ? antecedentes.join(', ') : 'Sin antecedentes marcados'}</span></li>
            <li><span>Fumador/a</span><span>{p.fumador ? 'Sí' : 'No'}</span></li>
            <li><span>Medicación</span><span>{p.medicacionActual ?? '—'}</span></li>
            <li><span>Grupo sanguíneo</span><span>{p.grupoSanguineo ?? '—'}</span></li>
          </ul>
          {p.otrosAntecedentes && <p style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>{p.otrosAntecedentes}</p>}
        </section>

        <section className="tarjeta">
          <div className="tarjeta__cabecera">
            <h2>Próximos turnos</h2>
            <button type="button" className="link" onClick={() => irA('turnos')}>Ver todos</button>
          </div>
          {proximos.length === 0 ? (
            <p className="vacio">Sin turnos agendados.</p>
          ) : (
            <ul className="lista">
              {proximos.map((t) => (
                <li key={t.id}>
                  <span>
                    <b>{t.fecha === hoy ? 'Hoy' : fmtFecha(t.fecha)} {fmtHora(t.hora)}</b>
                    <span className="persona__detalle" style={{ display: 'block' }}>{t.motivo ?? 'Sin motivo'}</span>
                  </span>
                  <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        {cuenta && (
          <section className="tarjeta">
            <div className="tarjeta__cabecera">
              <h2>Cuenta corriente</h2>
              <button type="button" className="link" onClick={() => irA('pagos')}>Ver cuenta</button>
            </div>
            <ul className="lista">
              <li><span>Facturado</span><b>{fmtMoneda(cuenta.totalCargos)}</b></li>
              <li><span>Total abonado</span><b className="positivo">{fmtMoneda(cuenta.totalPagos)}</b></li>
              <li>
                <span>{cuenta.saldo >= 0 ? 'Saldo pendiente' : 'Saldo a favor'}</span>
                <b className={cuenta.saldo > 0 ? 'negativo' : 'positivo'}>{fmtMoneda(Math.abs(cuenta.saldo))}</b>
              </li>
            </ul>
          </section>
        )}
      </div>
    </div>
  );
}

// ------------------------------------------------------------------------------------------------

/** Turnos del paciente. Para el odontólogo, cada turno muestra también lo registrado en esa atención. */
function TurnosPaciente({ turnos, atenciones, onCambio, onError }) {
  if (turnos.length === 0) return <p className="vacio">El paciente todavía no tiene turnos.</p>;

  return (
    <section className="tarjeta">
      <ol className="linea-tiempo">
        {turnos.map((t) => {
          const registros = atenciones.filter((a) => a.turnoId === t.id);
          return (
            <li key={t.id} className={`linea-tiempo__item estado--${t.estado.toLowerCase()}`}>
              <div className="linea-tiempo__fecha">
                {fmtFecha(t.fecha)} <span>{fmtHora(t.hora)}–{finTurno(t)}</span>
              </div>
              <div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span>
                    <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>{' '}
                    {t.urgencia && <span className="chip chip--urgencia">Urgencia</span>}{' '}
                    {t.motivo ?? <em>Sin motivo</em>}
                  </span>
                  <TurnoAcciones turno={t} onCambio={onCambio} onError={onError} compacto />
                </div>
                {registros.map((a) => (
                  <div key={a.id} className="registro-compacto">
                    {a.diagnosticos[0] && <div><b>Dx:</b> {a.diagnosticos[0].descripcion}{a.diagnosticos[0].codigo && ` (${a.diagnosticos[0].codigo})`}</div>}
                    {a.practicas.length > 0 && <div><b>Prácticas:</b> {a.practicas.map((p) => `${p.descripcion}${p.piezas ? ` (${p.piezas})` : ''}`).join(', ')}</div>}
                    {a.descripcion && <div><b>Evolución:</b> {a.descripcion}</div>}
                  </div>
                ))}
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
