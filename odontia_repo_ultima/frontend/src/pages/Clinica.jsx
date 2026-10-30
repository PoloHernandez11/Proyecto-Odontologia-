import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import BuscadorPaciente from '../components/BuscadorPaciente';
import { claseEstado, ESTADOS_TURNO, fmtHora, iniciales, turnoActivo } from '../utils/formato';

const VISTAS = {
  historia: { titulo: 'Historia clínica', tab: 'historia' },
  odontograma: { titulo: 'Odontograma', tab: 'odontograma' },
};

/**
 * Acceso directo desde el menú "Clínica": elegir el paciente (los de hoy primero)
 * y abrir su historia clínica u odontograma.
 */
export default function Clinica() {
  const { vista } = useParams();
  const v = VISTAS[vista] ?? VISTAS.historia;
  const [turnos, setTurnos] = useState([]);
  const [elegido, setElegido] = useState(null);

  useEffect(() => {
    api('/turnos').then((t) => setTurnos(t.filter(turnoActivo))).catch(() => {});
  }, []);

  return (
    <div className="pagina">
      <div>
        <h1>{v.titulo}</h1>
        <p className="subtitulo">Elegí el paciente</p>
      </div>

      <div className="columnas columnas--mitad">
        <section className="tarjeta">
          <h2>Pacientes de hoy</h2>
          {turnos.length === 0 ? (
            <p className="vacio">No hay turnos hoy.</p>
          ) : (
            <ul className="lista">
              {turnos.map((t) => (
                <li key={t.id}>
                  <span className="persona">
                    <span className="hora" style={{ width: '3rem' }}>{fmtHora(t.hora)}</span>
                    <span className="avatar">{iniciales(t.paciente)}</span>
                    <span>
                      <Link to={`/pacientes/${t.pacienteId}?tab=${v.tab}`} className="persona__nombre">{t.paciente}</Link>
                      <span className="persona__detalle" style={{ display: 'block' }}>{t.motivo ?? 'Sin motivo'}</span>
                    </span>
                  </span>
                  <span className={claseEstado(t.estado)}>{ESTADOS_TURNO[t.estado]}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="tarjeta">
          <h2>Buscar otro paciente</h2>
          <BuscadorPaciente seleccionado={elegido} onSeleccionar={setElegido} />
          {elegido && (
            <div className="acciones">
              <Link to={`/pacientes/${elegido.id}?tab=${v.tab}`}>Abrir {v.titulo.toLowerCase()} de {elegido.nombre} {elegido.apellido} →</Link>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
