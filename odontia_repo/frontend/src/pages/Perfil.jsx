import EquipoInvitaciones from '../components/EquipoInvitaciones';
import MatrizPermisos from '../components/MatrizPermisos';
import HistorialActividad from '../components/HistorialActividad';
import { useAuth } from '../auth/AuthContext';
import { iniciales } from '../utils/formato';

export default function Perfil() {
  const { usuario, esOdontologo } = useAuth();

  return (
    <div className="pagina">
      <div>
        <h1>Perfil y configuración</h1>
        <p className="subtitulo">Tu cuenta, el equipo del consultorio y los permisos de cada rol</p>
      </div>

      <div className="columnas columnas--mitad">
        <section className="tarjeta">
          <h2>Mi perfil</h2>
          <div className="persona" style={{ marginBottom: '1rem' }}>
            <span className="avatar avatar--grande">{iniciales(usuario.nombre)}</span>
            <span>
              <span className="persona__nombre">{usuario.nombre}</span>
              <span className="persona__detalle" style={{ display: 'block' }}>{usuario.email}</span>
              <span className="persona__detalle" style={{ display: 'block' }}>
                {esOdontologo ? 'Odontólogo/a · Administrador del consultorio' : 'Secretaria · Recepción'}
              </span>
            </span>
          </div>
          <p className="tenue" style={{ marginBottom: 0 }}>
            El acceso se hace con tu cuenta de Google. Odontia no guarda contraseñas.
          </p>
        </section>

        <div className="pagina">
          {esOdontologo && <EquipoInvitaciones />}
        </div>
      </div>

      {esOdontologo && <MatrizPermisos />}
      {esOdontologo && <HistorialActividad />}
    </div>
  );
}
