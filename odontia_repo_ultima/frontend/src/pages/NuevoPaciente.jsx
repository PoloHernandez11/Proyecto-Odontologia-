import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Modal from '../components/Modal';
import PacienteForm, { SECCIONES_PACIENTE } from '../components/PacienteForm';
import TurnoForm from '../components/TurnoForm';

/** Registro de paciente a página completa: datos, cobertura, antecedentes y consentimiento. */
export default function NuevoPaciente() {
  const navigate = useNavigate();
  const [paraTurno, setParaTurno] = useState(null);

  return (
    <div className="pagina">
      <div className="pagina__cabecera">
        <div>
          <h1>Nuevo paciente</h1>
          <p className="subtitulo">Pacientes › Registro de paciente</p>
        </div>
      </div>

      <div className="columnas">
        <section className="tarjeta">
          <PacienteForm
            permitirDarTurno
            onCancelar={() => navigate('/pacientes')}
            onGuardado={(p, { darTurno }) => (darTurno ? setParaTurno(p) : navigate(`/pacientes/${p.id}`))}
          />
        </section>
        <aside className="tarjeta">
          <h2>Progreso del registro</h2>
          <ol style={{ margin: 0, paddingLeft: '1.2rem', display: 'grid', gap: '0.4rem' }}>
            {SECCIONES_PACIENTE.map((s) => (
              <li key={s.id}><a href={`#seccion-${s.id}`}>{s.titulo}</a></li>
            ))}
            <li>Consentimiento informado</li>
          </ol>
          <p className="tenue">Solo nombre, apellido y DNI son obligatorios; el resto se puede completar después desde la ficha.</p>
        </aside>
      </div>

      {paraTurno && (
        <Modal titulo="Nuevo turno" subtitulo={`${paraTurno.apellido}, ${paraTurno.nombre}`} onCerrar={() => navigate(`/pacientes/${paraTurno.id}`)}>
          <TurnoForm
            paciente={paraTurno}
            onCancelar={() => navigate(`/pacientes/${paraTurno.id}`)}
            onGuardado={() => navigate(`/pacientes/${paraTurno.id}?tab=turnos`)}
          />
        </Modal>
      )}
    </div>
  );
}
