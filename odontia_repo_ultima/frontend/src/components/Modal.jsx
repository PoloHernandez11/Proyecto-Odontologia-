import { useEffect, useRef } from 'react';
import Icono from './Icono';

/** Diálogo nativo (<dialog>): foco, Esc y fondo los maneja el navegador. */
export default function Modal({ titulo, subtitulo, ancho = 640, onCerrar, children }) {
  const ref = useRef(null);

  useEffect(() => {
    const dialog = ref.current;
    dialog.showModal();
    return () => dialog.close();
  }, []);

  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault();
        onCerrar();
      }}
      style={{
        width: `min(${ancho}px, calc(100vw - 2rem))`,
        border: 0,
        borderRadius: 14,
        padding: '1.25rem 1.4rem',
        boxShadow: '0 24px 48px -12px rgba(16,24,40,.25)',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', marginBottom: '1rem' }}>
        <div>
          <h2 style={{ margin: 0, fontSize: '1.15rem' }}>{titulo}</h2>
          {subtitulo && <p className="subtitulo">{subtitulo}</p>}
        </div>
        <button type="button" className="fantasma chico" onClick={onCerrar} aria-label="Cerrar">
          <Icono nombre="cerrar" />
        </button>
      </div>
      {children}
    </dialog>
  );
}
