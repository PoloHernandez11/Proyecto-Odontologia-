import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { rutaInicio, useAuth } from '../auth/AuthContext';
import BotonGoogle from '../components/BotonGoogle';
import Icono from '../components/Icono';
import AccesoMarca from './AccesoMarca';
import './Login.css';

/**
 * Primer ingreso: el odontólogo nombra el consultorio e inicia sesión con Google (vía Firebase).
 * Esa cuenta queda como administradora. Solo se muestra mientras no exista ningún usuario.
 */
export default function RegistroDueno() {
  const { registrarDueno } = useAuth();
  const navigate = useNavigate();
  const [nombreConsultorio, setNombreConsultorio] = useState('');
  // El callback puede conservar un valor viejo: leemos siempre el actual desde la referencia.
  const nombreRef = useRef(nombreConsultorio);
  const [error, setError] = useState<string | null>(null);

  function cambiarNombre(valor: string) {
    nombreRef.current = valor;
    setNombreConsultorio(valor);
  }

  return (
    <div className="login">
      <AccesoMarca />
      <section className="login__panel">
        <div className="tarjeta login__form">
          <div>
            <h2>Crear el consultorio</h2>
            <p className="subtitulo">Sos el odontólogo dueño: administrás la agenda, las finanzas y el equipo.</p>
          </div>

          <label className="campo">
            Nombre del consultorio
            <input
              required
              maxLength={120}
              value={nombreConsultorio}
              onChange={(e) => cambiarNombre(e.target.value)}
              placeholder="Consultorio Dra. Méndez"
            />
          </label>

          <BotonGoogle
            texto="Crear con Google"
            listo={nombreConsultorio.trim().length > 0}
            onError={setError}
            onToken={async (token) => {
              setError(null);
              const nombre = nombreRef.current.trim();
              try {
                const u = await registrarDueno({ credential: token, nombreConsultorio: nombre });
                navigate(rutaInicio(u.rol), { replace: true });
              } catch (err) {
                // 409: ya hay un consultorio registrado; el alta ya no corresponde.
                if (err instanceof ApiError && err.status === 409) navigate('/login', { replace: true });
                throw err;
              }
            }}
          />

          {error && <p className="error" role="alert">{error}</p>}

          <p className="login__aviso">
            <Icono nombre="escudo" tamano={16} />
            Después podrás invitar a tu secretaria desde Perfil y configuración. Ella solo entra con esa invitación y su cuenta de Google.
          </p>
          <p className="tenue centrado">
            ¿Ya tenés cuenta? <Link to="/login">Iniciar sesión</Link>
          </p>
        </div>
      </section>
    </div>
  );
}
