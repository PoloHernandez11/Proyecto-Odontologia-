import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { firebaseConfigurado } from '../auth/firebase';
import { rutaInicio, useAuth } from '../auth/AuthContext';
import BotonGoogle from '../components/BotonGoogle';
import Icono from '../components/Icono';
import type { EstadoConsultorioDto } from '../types/api';
import AccesoMarca from './AccesoMarca';
import './Login.css';

/**
 * Ingreso con Google (vía Firebase Authentication). No hay contraseñas en la app: Firebase verifica a la persona
 * y el servidor decide si su correo está autorizado en el consultorio.
 */
export default function Login() {
  const { loginGoogle } = useAuth();
  const navigate = useNavigate();
  const [estado, setEstado] = useState<EstadoConsultorioDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<EstadoConsultorioDto>('/auth/estado')
      .then((e) => {
        setEstado(e);
        // Sin consultorio, el primer ingreso es el alta del odontólogo dueño.
        if (!e.registrado) navigate('/registro', { replace: true });
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'No se pudo conectar con el servidor.'));
  }, [navigate]);

  const listo = firebaseConfigurado && (estado?.googleConfigurado ?? false);

  return (
    <div className="login">
      <AccesoMarca />

      <section className="login__panel">
        <div className="tarjeta login__form">
          <div>
            <h2>Iniciar sesión</h2>
            <p className="subtitulo">Ingresá con tu cuenta de Google. Solo entran las personas autorizadas en el consultorio.</p>
          </div>

          {!firebaseConfigurado && (
            <p className="error" role="alert">
              Falta configurar Firebase en el frontend (VITE_FIREBASE_*). Revisá frontend/.env.local.
            </p>
          )}
          {estado && !estado.googleConfigurado && (
            <p className="error" role="alert">
              El servidor no tiene configurado el proyecto de Firebase (Firebase:ProjectId).
            </p>
          )}

          <BotonGoogle
            listo={listo}
            onError={setError}
            onToken={async (token) => {
              setError(null);
              const u = await loginGoogle(token);
              navigate(rutaInicio(u.rol), { replace: true });
            }}
          />

          {error && <p className="error" role="alert">{error}</p>}

          <p className="login__aviso">
            <Icono nombre="escudo" tamano={16} />
            Odontia no guarda contraseñas. La identidad la verifica Google a través de Firebase, y el consultorio solo acepta correos que el odontólogo haya autorizado.
          </p>
        </div>
      </section>
    </div>
  );
}
