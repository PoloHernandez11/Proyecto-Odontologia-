import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { accionesTurno, cambiarEstadoTurno, linkWhatsApp } from '../utils/turnos';
import Modal from './Modal';
import TurnoForm from './TurnoForm';

/**
 * Botones de acción de un turno según su estado y el rol.
 * `compacto` muestra solo las acciones principales (para filas de tabla).
 */
export default function TurnoAcciones({ turno, onCambio, onError, compacto = false }) {
  const { esOdontologo } = useAuth();
  const navigate = useNavigate();
  const [reprogramando, setReprogramando] = useState(false);
  const [ocupado, setOcupado] = useState(false);
  const a = accionesTurno(turno, esOdontologo);

  async function estado(nuevo) {
    setOcupado(true);
    try {
      if (await cambiarEstadoTurno(turno, nuevo)) await onCambio?.();
    } catch (e) {
      onError?.(e.message);
    } finally {
      setOcupado(false);
    }
  }

  const wa = a.recordar && linkWhatsApp(turno);

  return (
    <>
      <span style={{ display: 'inline-flex', flexWrap: 'wrap', gap: '0.35rem', justifyContent: 'flex-end' }}>
        {a.atender && (
          <button type="button" className="chico primario" disabled={ocupado}
            onClick={() => navigate(`/pacientes/${turno.pacienteId}?tab=historia&consulta=${turno.id}`)}>
            Iniciar consulta
          </button>
        )}
        {a.confirmar && <button type="button" className="chico" disabled={ocupado} onClick={() => estado('Confirmado')}>Confirmar</button>}
        {a.llego && <button type="button" className="chico" disabled={ocupado} onClick={() => estado('EnSala')}>Llegó</button>}
        {!compacto && a.ausente && <button type="button" className="chico" disabled={ocupado} onClick={() => estado('Ausente')}>No vino</button>}
        {!compacto && wa && <a className="chico-enlace" href={wa} target="_blank" rel="noreferrer">WhatsApp</a>}
        {a.reprogramar && <button type="button" className="chico" disabled={ocupado} onClick={() => setReprogramando(true)}>Reprogramar</button>}
        {!compacto && a.cancelar && <button type="button" className="chico peligro" disabled={ocupado} onClick={() => estado('Cancelado')}>Cancelar</button>}
      </span>
      {reprogramando && (
        <Modal titulo="Reprogramar turno" onCerrar={() => setReprogramando(false)}>
          <TurnoForm
            turno={turno}
            onCancelar={() => setReprogramando(false)}
            onGuardado={async () => {
              setReprogramando(false);
              await onCambio?.();
            }}
          />
        </Modal>
      )}
    </>
  );
}
