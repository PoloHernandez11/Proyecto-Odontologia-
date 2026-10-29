import { useState } from 'react';
import { api } from '../api/client';

export const ANTECEDENTES = [
  { name: 'diabetes', label: 'Diabetes' },
  { name: 'hipertension', label: 'Hipertensión' },
  { name: 'embarazo', label: 'Embarazo' },
  { name: 'asma', label: 'Asma' },
  { name: 'cardiopatia', label: 'Cardiopatía' },
  { name: 'anticoagulado', label: 'Anticoagulado' },
  { name: 'hepatitis', label: 'Hepatitis' },
  { name: 'fumador', label: 'Fumador/a' },
];

/** Secciones del formulario (también las usa la ficha para mostrar los datos). */
export const SECCIONES_PACIENTE = [
  {
    id: 'personales',
    titulo: 'Datos personales',
    campos: [
      { name: 'nombre', label: 'Nombre', required: true },
      { name: 'apellido', label: 'Apellido', required: true },
      { name: 'dni', label: 'DNI (sin puntos)', required: true, inputMode: 'numeric', pattern: '\\d{7,9}' },
      { name: 'fechaNacimiento', label: 'Fecha de nacimiento', type: 'date' },
      { name: 'sexo', label: 'Sexo', opciones: ['Femenino', 'Masculino', 'Otro'] },
      { name: 'ocupacion', label: 'Ocupación' },
    ],
  },
  {
    id: 'contacto',
    titulo: 'Contacto y cobertura',
    campos: [
      { name: 'telefono', label: 'Teléfono celular', type: 'tel', placeholder: '+54 9 11 …' },
      { name: 'email', label: 'Correo electrónico', type: 'email' },
      { name: 'domicilio', label: 'Domicilio' },
      { name: 'contactoEmergencia', label: 'Contacto de emergencia', placeholder: 'Nombre · vínculo · teléfono' },
      { name: 'obraSocial', label: 'Obra social / prepaga', placeholder: 'Particular si no tiene' },
      { name: 'plan', label: 'Plan' },
      { name: 'nroAfiliado', label: 'N.º de afiliado' },
    ],
  },
  {
    id: 'antecedentes',
    titulo: 'Antecedentes médicos',
    ayuda: 'Información visible para el odontólogo en la ficha clínica. Las alertas (alergias, anticoagulación…) también las ve la recepción.',
    campos: [
      { name: 'grupoSanguineo', label: 'Grupo sanguíneo', opciones: ['0+', '0-', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-'] },
      { name: 'alergias', label: 'Alergias', placeholder: 'Ninguna conocida' },
      { name: 'medicacionActual', label: 'Medicación actual' },
      { name: 'otrosAntecedentes', label: 'Otros antecedentes', textarea: true, ancho: true },
    ],
  },
];

const CAMPOS = SECCIONES_PACIENTE.flatMap((s) => s.campos);

const vacio = {
  ...Object.fromEntries(CAMPOS.map((c) => [c.name, ''])),
  ...Object.fromEntries(ANTECEDENTES.map((a) => [a.name, false])),
  consentimientoFirmado: false,
};

/**
 * Alta (sin `paciente`) o edición de un paciente, en secciones como el diseño de "Nuevo paciente".
 * Llama a onGuardado(paciente, { darTurno }) con el paciente que devuelve la API.
 */
export default function PacienteForm({ paciente, onGuardado, onCancelar, permitirDarTurno = false }) {
  const [form, setForm] = useState(() => ({ ...vacio, ...paciente }));
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);
  const set = (name, valor) => setForm((f) => ({ ...f, [name]: valor }));

  async function guardar(e, darTurno = false) {
    e.preventDefault();
    const formEl = e.target.closest('form');
    if (formEl && !formEl.reportValidity()) return;
    setGuardando(true);
    setError(null);
    try {
      const body = {
        ...Object.fromEntries(CAMPOS.map((c) => [c.name, form[c.name] === '' || form[c.name] == null ? null : form[c.name]])),
        ...Object.fromEntries(ANTECEDENTES.map((a) => [a.name, Boolean(form[a.name])])),
        consentimientoFirmado: Boolean(form.consentimientoFirmado),
      };
      const guardado = paciente?.id
        ? await api(`/pacientes/${paciente.id}`, { method: 'PUT', body })
        : await api('/pacientes', { method: 'POST', body });
      onGuardado(guardado, { darTurno });
    } catch (err) {
      setError(err.message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form onSubmit={guardar} style={{ display: 'grid', gap: '1.25rem' }}>
      {SECCIONES_PACIENTE.map((s) => (
        <fieldset key={s.id} id={`seccion-${s.id}`} style={{ border: 0, padding: 0, margin: 0 }}>
          <legend style={{ fontWeight: 700, marginBottom: '0.25rem' }}>{s.titulo}</legend>
          {s.ayuda && <p className="tenue" style={{ marginTop: 0 }}>{s.ayuda}</p>}
          <div className="form-grilla">
            {s.campos.map(({ name, label, opciones, textarea, ancho, ...attrs }) => (
              <label key={name} className={ancho ? 'ancho-total' : undefined}>
                {label}
                {opciones ? (
                  <select value={form[name] ?? ''} onChange={(e) => set(name, e.target.value)}>
                    <option value="">—</option>
                    {opciones.map((o) => <option key={o}>{o}</option>)}
                  </select>
                ) : textarea ? (
                  <textarea rows={2} value={form[name] ?? ''} onChange={(e) => set(name, e.target.value)} />
                ) : (
                  <input name={name} value={form[name] ?? ''} onChange={(e) => set(name, e.target.value)} {...attrs} />
                )}
              </label>
            ))}
          </div>
          {s.id === 'antecedentes' && (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.6rem 1.25rem', marginTop: '0.85rem' }}>
              {ANTECEDENTES.map((a) => (
                <label key={a.name} className="check">
                  <input type="checkbox" checked={Boolean(form[a.name])} onChange={(e) => set(a.name, e.target.checked)} />
                  {a.label}
                </label>
              ))}
            </div>
          )}
        </fieldset>
      ))}

      <fieldset style={{ border: 0, padding: 0, margin: 0 }}>
        <legend style={{ fontWeight: 700, marginBottom: '0.25rem' }}>Consentimiento informado</legend>
        <label className="check">
          <input type="checkbox" checked={Boolean(form.consentimientoFirmado)} onChange={(e) => set('consentimientoFirmado', e.target.checked)} />
          El paciente firmó el consentimiento informado
        </label>
      </fieldset>

      {error && <p className="error" role="alert">{error}</p>}
      <div className="acciones" style={{ marginTop: 0 }}>
        {onCancelar && (
          <button type="button" onClick={onCancelar} disabled={guardando}>
            Cancelar
          </button>
        )}
        {permitirDarTurno && !paciente?.id && (
          <button type="button" disabled={guardando} onClick={(e) => guardar(e, true)}>
            Guardar y dar turno
          </button>
        )}
        <button type="submit" className="primario" disabled={guardando}>
          {guardando ? 'Guardando…' : paciente?.id ? 'Guardar cambios' : 'Guardar paciente'}
        </button>
      </div>
    </form>
  );
}
