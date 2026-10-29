import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import './EquipoInvitaciones.css';
import { iniciales } from '../utils/formato';
import type {
  ActivarUsuarioDto,
  CrearInvitacionDto,
  EstadoInvitacion,
  InvitacionCreadaDto,
  InvitacionDto,
  UsuarioDto,
} from '../types/api';

/** Usuarios del consultorio + invitaciones a la secretaria. Solo lo ve el odontólogo. */
export default function EquipoInvitaciones() {
  const { usuario } = useAuth();
  const [equipo, setEquipo] = useState<UsuarioDto[]>([]);
  const [invitaciones, setInvitaciones] = useState<InvitacionDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const cargar = useCallback(() => {
    Promise.all([api<UsuarioDto[]>('/auth/usuarios'), api<InvitacionDto[]>('/invitaciones')])
      .then(([u, i]) => {
        setEquipo(u);
        setInvitaciones(i);
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'No se pudo cargar el equipo.'));
  }, []);

  useEffect(cargar, [cargar]);

  async function cambiarActivo(u: UsuarioDto) {
    setError(null);
    const body: ActivarUsuarioDto = { activo: !u.activo };
    try {
      await api(`/usuarios/${u.id}/activo`, { method: 'PUT', body });
      cargar();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar el estado.');
    }
  }

  async function revocar(inv: InvitacionDto) {
    setError(null);
    try {
      await api(`/invitaciones/${inv.id}/revocar`, { method: 'POST' });
      cargar();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo revocar la invitación.');
    }
  }

  return (
    <section className="tarjeta">
      <h2>Usuarios y roles</h2>
      <p className="tenue" style={{ marginTop: 0 }}>
        Odontólogo: administración total. Secretaria: solo entra con invitación tuya.
      </p>

      {error && <p className="error" role="alert">{error}</p>}

      <ul className="lista">
        {equipo.map((u) => (
          <li key={u.id}>
            <span className="persona">
              <span className="avatar">{iniciales(u.nombre)}</span>
              <span>
                <span className="persona__nombre">{u.nombre}</span>
                <span className="persona__detalle" style={{ display: 'block' }}>
                  {u.email}
                </span>
              </span>
            </span>
            <span className="equipo__acciones">
              <span className="chip chip--sin-punto">{u.rol === 'Odontologo' ? 'Odontólogo · Admin' : 'Secretaria'}</span>
              <button
                type="button"
                role="switch"
                aria-checked={u.activo}
                aria-label={u.activo ? `Desactivar a ${u.nombre}` : `Activar a ${u.nombre}`}
                disabled={u.id === usuario?.id}
                className={`interruptor ${u.activo ? 'interruptor--on' : ''}`}
                onClick={() => cambiarActivo(u)}
              />
            </span>
          </li>
        ))}
      </ul>

      <h3 style={{ marginTop: '1.25rem' }}>Invitar a la secretaria</h3>
      <FormularioInvitacion onCreada={cargar} />

      {invitaciones.length > 0 && (
        <table className="tabla" style={{ marginTop: '1rem' }}>
          <thead>
            <tr><th>Correo</th><th>Estado</th><th>Vence</th><th /></tr>
          </thead>
          <tbody>
            {invitaciones.map((i) => (
              <tr key={i.id}>
                <td>{i.email}</td>
                <td><span className={`chip chip--${claseEstado(i.estado)}`}>{i.estado}</span></td>
                <td>{new Date(i.expiraEn).toLocaleDateString('es-AR')}</td>
                <td>
                  {i.estado === 'Pendiente' && (
                    <button type="button" className="fantasma chico" onClick={() => revocar(i)}>Revocar</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

function FormularioInvitacion({ onCreada }: { onCreada: () => void }) {
  const { usuario } = useAuth();
  const [email, setEmail] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // El enlace solo se muestra una vez: el backend guarda el hash, no el token.
  const [enlace, setEnlace] = useState<string | null>(null);
  const [destinatario, setDestinatario] = useState('');
  const [copiado, setCopiado] = useState(false);

  async function invitar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setEnviando(true);
    setError(null);
    setCopiado(false);
    try {
      const body: CrearInvitacionDto = { email: email.trim() };
      const creada = await api<InvitacionCreadaDto>('/invitaciones', { method: 'POST', body });
      setEnlace(`${window.location.origin}/invitacion/${creada.token}`);
      setDestinatario(body.email);
      setEmail('');
      onCreada();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear la invitación.');
    } finally {
      setEnviando(false);
    }
  }

  async function copiar() {
    if (!enlace) return;
    try {
      await navigator.clipboard.writeText(enlace);
      setCopiado(true);
    } catch {
      setError('No se pudo copiar. Seleccioná el enlace y copialo a mano.');
    }
  }

  /** Destinatario, asunto y texto del correo. Nada se envía solo: el odontólogo puede editar antes de enviar. */
  function datosCorreo() {
    const asunto = 'Invitación para sumarte a Odontia';
    const cuerpo = [
      'Hola,',
      '',
      'Te invito a sumarte como secretaria al consultorio en Odontia.',
      'Entrá con este enlace usando tu cuenta de Google:',
      '',
      enlace ?? '',
      '',
      'El enlace vence en 7 días y se usa una sola vez.',
      '',
      'Saludos,',
      usuario?.nombre ?? '',
    ].join('\n');
    return { para: destinatario, asunto, cuerpo };
  }

  /** Abre Gmail en el navegador con el correo ya redactado. */
  function gmailHref(): string {
    const { para, asunto, cuerpo } = datosCorreo();
    const params = new URLSearchParams({ view: 'cm', fs: '1', to: para, su: asunto, body: cuerpo });
    return `https://mail.google.com/mail/?${params.toString()}`;
  }

  /** Abre el programa de correo del equipo (Outlook, Mail, etc.). Si no hay ninguno, el navegador no muestra nada. */
  function correoHref(): string {
    const { para, asunto, cuerpo } = datosCorreo();
    return `mailto:${encodeURIComponent(para)}?subject=${encodeURIComponent(asunto)}&body=${encodeURIComponent(cuerpo)}`;
  }

  return (
    <>
      <form onSubmit={invitar} className="equipo__invitar">
        <input
          type="email"
          required
          maxLength={150}
          placeholder="correo@secretaria.com"
          aria-label="Correo de la secretaria"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <button type="submit" className="primario" disabled={enviando}>
          {enviando ? 'Creando…' : 'Crear invitación'}
        </button>
      </form>

      {error && <p className="error" role="alert">{error}</p>}

      {enlace && (
        <div className="equipo__enlace" role="status">
          <p>Vence en 7 días, se usa una sola vez y no vuelve a mostrarse. Podés abrir el correo ya redactado o copiar el enlace.</p>
          <div className="equipo__enlace-fila">
            <input readOnly value={enlace} onFocus={(e) => e.currentTarget.select()} aria-label="Enlace de invitación" />
            <button type="button" className="secundario" onClick={copiar}>{copiado ? 'Copiado' : 'Copiar'}</button>
          </div>
          <p style={{ marginTop: '0.6rem', display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <a className="primario" href={gmailHref()} target="_blank" rel="noopener noreferrer">
              Redactar en Gmail a {destinatario}
            </a>
            <a className="secundario" href={correoHref()}>Abrir en mi programa de correo</a>
          </p>
        </div>
      )}
    </>
  );
}

function claseEstado(estado: EstadoInvitacion): string {
  switch (estado) {
    case 'Aceptada': return 'ok';
    case 'Pendiente': return 'aviso';
    default: return 'neutro';
  }
}
