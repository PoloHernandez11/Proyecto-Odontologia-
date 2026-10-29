import Icono from '../components/Icono';

/** Panel izquierdo de las pantallas de acceso (login, alta del dueño, aceptación de invitación). */
export default function AccesoMarca() {
  return (
    <section className="login__marca">
      <div className="login__logo">
        <span className="marca__logo"><Icono nombre="diente" tamano={20} /></span>
        <b>Odontia</b>
      </div>

      <div className="login__hero">
        <h1>Tu consultorio, organizado en un solo lugar.</h1>
        <p>Agenda, pacientes, historia clínica, odontograma y laboratorio, conectados para el odontólogo y la recepción.</p>
        <ul className="login__beneficios">
          <li><span><Icono nombre="agenda" tamano={16} /></span> Agenda diaria, semanal y mensual</li>
          <li><span><Icono nombre="diente" tamano={16} /></span> Odontograma FDI interactivo</li>
          <li><span><Icono nombre="escudo" tamano={16} /></span> Accesos diferenciados por rol</li>
        </ul>
      </div>

      <small className="login__pie">© 2026 Odontia · Soporte: soporte@odontia.app</small>
    </section>
  );
}
