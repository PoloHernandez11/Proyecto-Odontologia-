import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { rutaInicio, useAuth } from '../auth/AuthContext';
import BotonGoogle from '../components/BotonGoogle';
import Icono from '../components/Icono';
import type { InvitacionPublicaDto } from '../types/api';
import AccesoMarca from './AccesoMarca';
import './Login.css';

/**
 * La secretaria llega desde el enlace /invitacion/:token. Verifica la invitación y, al iniciar sesión
 * con Google (vía Firebase), el servidor comprueba que sea la cuenta del correo invitado.
 */
export default function AceptarInvitacion() {
  const { token = '' } = useParams();
  const { aceptarInvitacion } = useAuth();
  const navigate = useNavigate();
  const [invitacion, setInvitacion] = useState<InvitacionPublicaDto | null>(null);
  const [errorCarga, setErrorCarga] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    api<InvitacionPublicaDto>(`/auth/invitaciones/${encodeURIComponent(token)}`)
      .then((data) => {
        if (vigente) setInvitacion(data);
      })
      .catch((err: unknown) => {
        if (vigente) setErrorCarga(err instanceof Error ? err.message : 'No se pudo verificar la invitación.');
      })
      .finally(() => {
        if (vigente) setCargando(false);
      });
    return () => {
      vigente = false;
    };
  }, [token]);

  return (
    <div className="login">
      <AccesoMarca />
      <section className="login__panel">
        <div className="tarjeta login__form">
          {cargando && <p className="tenue">Verificando la invitación…</p>}

          {!cargando && errorCarga && (
            <>
              <h2>Invitación no válida</h2>
              <p className="error" role="alert">{errorCarga}</p>
              <p className="tenue">Pedile al odontólogo que te envíe una invitación nueva.</p>
              <Link to="/login" className="primario" style={{ display: 'inline-block', textAlign: 'center', textDecoration: 'none' }}>
                Ir al inicio de sesión
              </Link>
            </>
          )}

          {!cargando && invitacion && (
            <>
              <div>
                <h2>Unirte a {invitacion.consultorioNombre}</h2>
                <p className="subtitulo">
                  Entrá como Secretaria con la cuenta de Google de <b>{invitacion.email}</b>.
                </p>
              </div>

              <BotonGoogle
                onError={setError}
                onToken={async (googleToken) => {
                  setError(null);
                  const u = await aceptarInvitacion({ token, credential: googleToken });
                  navigate(rutaInicio(u.rol), { replace: true });
                }}
              />

              {error && <p className="error" role="alert">{error}</p>}

              <p className="login__aviso">
                <Icono nombre="escudo" tamano={16} />
                La invitación vence el {new Date(invitacion.expiraEn).toLocaleDateString('es-AR')} y solo se puede usar una vez.
              </p>
            </>
          )}
        </div>
      </section>
    </div>
  );
}
