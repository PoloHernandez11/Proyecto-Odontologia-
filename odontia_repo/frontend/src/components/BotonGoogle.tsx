import { useState } from 'react';
import { obtenerTokenGoogle } from '../auth/firebase';

interface Props {
  /** Recibe el ID token de Firebase. Puede ser async (por ejemplo, para iniciar sesión en la API). */
  onToken: (token: string) => Promise<void> | void;
  onError: (mensaje: string) => void;
  /** Si es false, el botón no abre Google (por ejemplo, falta un dato del formulario). */
  listo?: boolean;
  texto?: string;
}

/** Botón "Continuar con Google" oficial en estilo, que delega en Firebase. */
export default function BotonGoogle({ onToken, onError, listo = true, texto = 'Continuar con Google' }: Props) {
  const [cargando, setCargando] = useState(false);

  async function alPresionar() {
    setCargando(true);
    try {
      const token = await obtenerTokenGoogle();
      await onToken(token);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'Error inesperado.');
    } finally {
      setCargando(false);
    }
  }

  return (
    <button type="button" className="boton-google" onClick={alPresionar} disabled={!listo || cargando}>
      <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true" focusable="false">
        <path fill="#EA4335" d="M24 9.5c3.5 0 6.6 1.2 9 3.5l6.7-6.7C35.6 2.4 30.2 0 24 0 14.6 0 6.5 5.4 2.6 13.2l7.8 6C12.3 13.5 17.7 9.5 24 9.5z" />
        <path fill="#4285F4" d="M46.5 24.5c0-1.6-.1-3.1-.4-4.5H24v9h12.7c-.6 3-2.3 5.5-4.8 7.2l7.5 5.8c4.4-4 7.1-10 7.1-17.5z" />
        <path fill="#FBBC05" d="M10.4 28.8c-.5-1.4-.8-2.9-.8-4.8s.3-3.4.8-4.8l-7.8-6C.9 16.4 0 20.1 0 24s.9 7.6 2.6 10.8l7.8-6z" />
        <path fill="#34A853" d="M24 48c6.5 0 11.9-2.1 15.9-5.8l-7.5-5.8c-2.1 1.4-4.8 2.3-8.4 2.3-6.3 0-11.7-4.1-13.6-9.8l-7.8 6C6.5 42.6 14.6 48 24 48z" />
      </svg>
      <span>{cargando ? 'Abriendo Google…' : texto}</span>
    </button>
  );
}
